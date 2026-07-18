using System;
using Odyssey.Gameplay.AI;
using Odyssey.Gameplay.Combat;
using Odyssey.Gameplay.Config;
using Odyssey.Unity.Config;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace Odyssey.Characters.Enemies
{
    /// <summary>
    /// 怪物角色的 Unity 门面，负责装配 Health、Perception、Blackboard、行为树和 Action 执行层。
    /// 采用 Facade 与 Composition Root 模式保留现有场景脚本 GUID；本类只编排数据流和事件，不直接实现追击决策与导航细节。
    /// </summary>
    public sealed class Enemy : MonoBehaviour, IConfigTarget<EnemyConfigData>, IDamageable
    {
        [Header("配置")]
        [SerializeField] private string configId = "chomper";

        [Header("战斗")]
        [FormerlySerializedAs("Health")]
        [SerializeField] private int maxHealth = 3;
        public int AttackDamage = 1;
        public float AttackCooldown = 2f;

        [Header("感知")]
        public float ChaseRange = 10f;
        public float AttackRange = 2f;

        [Header("远程攻击")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Transform projectileOrigin;
        [SerializeField] private GameObject attackTelegraph;

        private EnemyAttackMode _attackMode;
        private float _minimumAttackRange;
        private float _projectileSpeed;
        private float _attackWindup;

        private Animator _animator;
        private NavMeshAgent _agent;
        private Health _health;
        private EnemyConfigData _appliedConfig;
        private EnemyBlackboard _blackboard;
        private EnemyBehaviorModel _behaviorModel;
        private EnemyPerception _perception;
        private EnemyActionRuntime _actions;
        private EnemyPatrolRoute _patrolRoute;
        private bool _meleeDamageCommitted;
        private bool _isDead;
        private Vector3 _deathFlyDirection;
        private float _deathFlySpeed;

        public string ConfigId => configId;
        public int CurrentHealth => _health?.Current ?? maxHealth;
        public EnemyGoal CurrentGoal => _blackboard?.CurrentGoal ?? EnemyGoal.Idle;
        public string CurrentBehaviorPath => _blackboard?.CurrentBehaviorPath ?? "尚未运行";
        public bool HasSensedTarget => _blackboard?.HasTarget ?? false;
        public float TargetDistance => _blackboard?.DistanceToTarget ?? float.MaxValue;
        public float ForgetRange => _blackboard?.ForgetRange ?? ChaseRange * 1.25f;
        public float HealthRatio => _blackboard?.HealthRatio ?? 1f;
        public EnemyAttackMode AttackMode => _attackMode;
        public float MinimumAttackRange => _minimumAttackRange;
        public bool HasPatrolRoute => _patrolRoute != null && _patrolRoute.HasValidRoute;
        public string CurrentPatrolPointName => _patrolRoute == null ? "无" : _patrolRoute.CurrentPointName;

        /// <summary>
        /// 怪物完成死亡提交后发布的实例事件，遭遇控制器只依赖该事实统计进度，不轮询或控制怪物行为。
        /// </summary>
        public event Action<Enemy> Defeated;
        public event Action<Enemy, DamageResult> Damaged;

        private void Awake()
        {
            EnsureRuntimeDependencies();
            RebuildHealth(maxHealth, maxHealth);
        }

        private void Start()
        {
            EnsureNavigationReady();
        }

        private void Update()
        {
            _actions.TickTimers(Time.deltaTime);
            _perception.Sense(
                _blackboard,
                CurrentHealth,
                _health.Maximum,
                ChaseRange,
                AttackRange,
                _minimumAttackRange,
                _actions.CanAttack(Time.time, AttackCooldown),
                _actions.IsAttackInProgress,
                _actions.IsHitReacting,
                _isDead,
                HasPatrolRoute,
                _attackMode);
            _actions.SetTarget(_perception.Target);
            _behaviorModel.Tick(
                Time.time,
                AttackCooldown,
                Time.deltaTime);

            if (_isDead)
            {
                UpdateDeathPresentation();
            }
        }

        /// <summary>
        /// 应用导表后的不可变敌人配置，并在生命上限变化时保留不超过新上限的当前生命。
        /// 配置装配只改变实际使用的战斗和感知数值，不创建额外 AI 框架或场景依赖。
        /// </summary>
        public void Apply(EnemyConfigData config)
        {
            if (config == null)
            {
                throw new System.ArgumentNullException(nameof(config));
            }

            EnsureRuntimeDependencies();

            if (ReferenceEquals(_appliedConfig, config))
            {
                return;
            }

            var preservedHealth = _appliedConfig == null ? config.MaxHealth : CurrentHealth;
            ChaseRange = config.ChaseRange;
            AttackRange = config.AttackRange;
            AttackDamage = config.AttackDamage;
            AttackCooldown = config.AttackCooldown;
            _attackMode = config.AttackMode;
            _minimumAttackRange = config.MinimumAttackRange;
            _projectileSpeed = config.ProjectileSpeed;
            _attackWindup = config.AttackWindup;
            maxHealth = config.MaxHealth;
            RebuildHealth(maxHealth, preservedHealth);
            _actions.ConfigureAttack(
                _attackMode,
                _attackWindup,
                BeginMeleeAttackSequence,
                AttackBegin,
                LaunchProjectile,
                SetAttackTelegraph);
            _appliedConfig = config;
        }

        /// <summary>
        /// 通过共享 Health 提交伤害，再由黑板事实让受击或死亡分支抢占常规行为。
        /// 受击与死亡拥有最高优先级，避免巡逻、追击或攻击覆盖必须完整播放的反馈。
        /// </summary>
        public DamageResult Apply(DamageRequest request)
        {
            EnsureRuntimeDependencies();
            EnsureHealth();
            var result = _health.Apply(request);
            if (!result.Accepted)
            {
                return result;
            }

            if (result.Killed)
            {
                Die();
            }
            else
            {
                _actions.NotifyHit();
            }

            Damaged?.Invoke(this, result);

            return result;
        }

        /// <summary>
        /// 保留现有玩家攻击入口，并把旧参数适配为统一 DamageRequest。
        /// </summary>
        public void TakeDamage(int damage)
        {
            Apply(new DamageRequest(damage, "player"));
        }

        /// <summary>
        /// 由代码前摇或兼容的攻击动画事件调用；每次攻击序列只允许提交一次近战伤害。
        /// 查询保持局部简单实现，不为了六只怪物的低频攻击引入全局对象池或通用战斗管理器。
        /// </summary>
        public void AttackBegin()
        {
            if (_isDead || _attackMode != EnemyAttackMode.Melee || _meleeDamageCommitted)
            {
                return;
            }

            _meleeDamageCommitted = true;

            var attackCenter = transform.position + Vector3.up + transform.forward * 0.5f;
            foreach (var hit in Physics.OverlapSphere(attackCenter, AttackRange))
            {
                var player = hit.GetComponentInParent<Player.PlayerController>();
                if (player == null)
                {
                    continue;
                }

                player.TakeDamage(AttackDamage, transform.position);
                break;
            }
        }

        private void RebuildHealth(int maximum, int current)
        {
            _health = new Health(Mathf.Max(1, maximum));
            var initialDamage = _health.Maximum - Mathf.Clamp(current, 0, _health.Maximum);
            if (initialDamage > 0)
            {
                _health.Apply(new DamageRequest(initialDamage, "configuration"));
            }
        }

        private void EnsureHealth()
        {
            if (_health == null)
            {
                RebuildHealth(maxHealth, maxHealth);
            }
        }

        /// <summary>
        /// 延迟装配怪物运行时协作者，使场景生命周期、EditMode 测试和配置工具都能安全调用同一入口。
        /// 采用惰性初始化而非要求调用方了解 Awake 顺序，避免测试或编辑器工具在组件尚未唤醒时产生空引用。
        /// </summary>
        private void EnsureRuntimeDependencies()
        {
            _animator ??= GetComponent<Animator>();
            _agent ??= GetComponent<NavMeshAgent>();
            _blackboard ??= new EnemyBlackboard();
            _perception ??= new EnemyPerception(transform);
            _patrolRoute ??= GetComponent<EnemyPatrolRoute>();
            _actions ??= new EnemyActionRuntime(transform, _animator, _agent);
            _behaviorModel ??= new EnemyBehaviorModel(_blackboard, _actions);
            _actions.ConfigurePatrol(_patrolRoute);
            _actions.ConfigureAttack(
                _attackMode,
                _attackWindup,
                BeginMeleeAttackSequence,
                AttackBegin,
                LaunchProjectile,
                SetAttackTelegraph);
        }

        /// <summary>
        /// 在场景开始后把禁用的 NavMeshAgent 放到正确类型的网格上再启用。
        /// 该过程属于角色自身初始化，与战区是否开始无关，保证怪物从游戏启动起就能自主巡逻和感知玩家。
        /// </summary>
        private void EnsureNavigationReady()
        {
            if (_agent == null || _isDead || (_agent.enabled && _agent.isOnNavMesh))
            {
                return;
            }

            var filter = new NavMeshQueryFilter
            {
                agentTypeID = _agent.agentTypeID,
                areaMask = _agent.areaMask
            };
            if (!NavMesh.SamplePosition(transform.position, out var hit, 8f, filter))
            {
                Debug.LogError("怪物附近没有对应类型的 NavMesh，无法开始巡逻。", this);
                return;
            }

            if (_agent.enabled)
            {
                _agent.enabled = false;
            }

            transform.position = hit.position;
            _agent.enabled = true;
            if (!_agent.isOnNavMesh)
            {
                _agent.enabled = false;
                Debug.LogError("怪物启用 NavMeshAgent 后仍未绑定到对应类型的网格。", this);
            }
        }

        private void BeginMeleeAttackSequence()
        {
            _meleeDamageCommitted = false;
        }

        private void Die()
        {
            _isDead = true;
            SetAttackTelegraph(false);
            _actions.DisableNavigation();
            var collision = GetComponent<Collider>();
            if (collision != null)
            {
                collision.enabled = false;
            }

            var target = _perception.Target;
            _deathFlyDirection = target != null
                ? (transform.position - target.position).normalized
                : Vector3.up;
            _deathFlyDirection.y = 1.5f;
            _deathFlySpeed = 8f;
            if (_animator != null)
            {
                _animator.enabled = false;
            }

            Defeated?.Invoke(this);

            Destroy(gameObject, 2f);
        }

        /// <summary>
        /// 在远程攻击前摇结束时创建单个代码驱动投射物；Enemy 只负责提供配置和场景引用，命中生命周期由投射物自身管理。
        /// </summary>
        private void LaunchProjectile(Vector3 targetPosition)
        {
            if (_isDead || _attackMode != EnemyAttackMode.Projectile || projectilePrefab == null)
            {
                if (projectilePrefab == null)
                {
                    Debug.LogError("远程敌人缺少投射物 Prefab，无法完成攻击。", this);
                }

                return;
            }

            var origin = projectileOrigin == null
                ? transform.position + Vector3.up * 0.8f + transform.forward * 0.5f
                : projectileOrigin.position;
            var direction = targetPosition - origin;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = transform.forward;
            }

            var instance = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(direction.normalized));
            var projectile = instance.GetComponent<EnemyProjectile>();
            if (projectile == null)
            {
                Debug.LogError("投射物 Prefab 缺少 EnemyProjectile 组件。", instance);
                Destroy(instance);
                return;
            }

            projectile.Initialize(this, direction.normalized, _projectileSpeed, AttackDamage);
        }

        private void SetAttackTelegraph(bool visible)
        {
            if (attackTelegraph != null)
            {
                attackTelegraph.SetActive(visible);
            }
        }

        private void UpdateDeathPresentation()
        {
            transform.position += _deathFlyDirection * _deathFlySpeed * Time.deltaTime;
            _deathFlyDirection.y -= 5f * Time.deltaTime;
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.zero, Time.deltaTime * 3f);
        }

        // 以下方法匹配现有 Animation Event；命中逻辑只由 AttackBegin 执行，其他事件不承担玩法规则。
        public void AttackEnd() { }
        public void PlayStep() { }
        public void Grunt() { }
        public void MeleeAttackStart() { }
        public void MeleeAttackEnd() { }
    }
}
