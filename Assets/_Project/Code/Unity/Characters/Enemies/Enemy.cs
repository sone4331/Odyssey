using Odyssey.Gameplay.Combat;
using Odyssey.Gameplay.Config;
using Odyssey.Unity.Config;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace Odyssey.Characters.Enemies
{
    /// <summary>
    /// 作为现阶段怪物的 Unity 适配器驱动导航、动画与死亡表现，并把生命规则委托给共享 Health。
    /// 采用 Adapter 与配置目标模式保留现有场景和动画事件兼容；AI 决策将在后续模块拆出，本类不再自行维护第二套伤害规则。
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

        private Animator _animator;
        private NavMeshAgent _agent;
        private Transform _targetPlayer;
        private Health _health;
        private EnemyConfigData _appliedConfig;
        private float _lastAttackTime;
        private float _actionLockTimer;
        private bool _isDead;
        private string _currentAnimation = string.Empty;
        private Vector3 _deathFlyDirection;
        private float _deathFlySpeed;

        public string ConfigId => configId;
        public int CurrentHealth => _health?.Current ?? maxHealth;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _agent = GetComponent<NavMeshAgent>();
            RebuildHealth(maxHealth, maxHealth);
        }

        private void Start()
        {
            var playerObject = GameObject.FindGameObjectWithTag("Player");
            _targetPlayer = playerObject != null ? playerObject.transform : null;
        }

        private void Update()
        {
            if (_isDead)
            {
                UpdateDeathPresentation();
                return;
            }

            if (_targetPlayer == null || _agent == null || !_agent.isOnNavMesh)
            {
                return;
            }

            if (_actionLockTimer > 0f)
            {
                _actionLockTimer -= Time.deltaTime;
                StopNavigation();
                return;
            }

            var distance = Vector3.Distance(transform.position, _targetPlayer.position);
            if (distance <= AttackRange)
            {
                StopNavigation();
                if (Time.time >= _lastAttackTime + AttackCooldown)
                {
                    ExecuteAttack();
                }
                else
                {
                    PlayAnimation("Idle");
                }
            }
            else if (distance <= ChaseRange)
            {
                _agent.isStopped = false;
                _agent.SetDestination(_targetPlayer.position);
                PlayAnimation("Run");
            }
            else
            {
                StopNavigation();
                PlayAnimation("Idle");
            }
        }

        /// <summary>
        /// 应用导表后的不可变敌人配置，并在生命上限变化时保留不超过新上限的当前生命。
        /// 配置装配只改变数值，不查找资源或修改场景引用。
        /// </summary>
        public void Apply(EnemyConfigData config)
        {
            if (config == null)
            {
                throw new System.ArgumentNullException(nameof(config));
            }

            if (ReferenceEquals(_appliedConfig, config))
            {
                return;
            }

            var preservedHealth = _appliedConfig == null ? config.MaxHealth : CurrentHealth;
            ChaseRange = config.ChaseRange;
            AttackRange = config.AttackRange;
            AttackDamage = config.AttackDamage;
            AttackCooldown = config.AttackCooldown;
            maxHealth = config.MaxHealth;
            RebuildHealth(maxHealth, preservedHealth);
            _appliedConfig = config;
        }

        /// <summary>
        /// 通过共享 IDamageable 端口提交伤害，并根据同一个 DamageResult 触发受击或死亡表现。
        /// 领域状态先提交，Animator 与 NavMesh 只消费结果，保证以后 Host 权威验证和本地表现使用同一事实。
        /// </summary>
        public DamageResult Apply(DamageRequest request)
        {
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
                PlayAnimation("Hit" + Random.Range(1, 5));
                _currentAnimation = string.Empty;
                _actionLockTimer = 0.5f;
                StopNavigation();
            }

            return result;
        }

        /// <summary>
        /// 保留现有玩家攻击调用入口，并把旧参数适配为统一 DamageRequest。
        /// </summary>
        public void TakeDamage(int damage)
        {
            Apply(new DamageRequest(damage, "player"));
        }

        /// <summary>
        /// 由攻击动画命中帧调用；物理查询只负责找到玩家，最终扣血仍交给玩家的统一生命管线。
        /// </summary>
        public void AttackBegin()
        {
            if (_isDead)
            {
                return;
            }

            var attackCenter = transform.position + Vector3.up + transform.forward * 0.5f;
            foreach (var hit in Physics.OverlapSphere(attackCenter, 2f))
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

        private void ExecuteAttack()
        {
            _lastAttackTime = Time.time;
            _actionLockTimer = 1.2f;
            var direction = (_targetPlayer.position - transform.position).normalized;
            direction.y = 0f;
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }

            PlayAnimation("Attack");
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

        private void StopNavigation()
        {
            if (_agent == null || !_agent.isOnNavMesh)
            {
                return;
            }

            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
        }

        private void PlayAnimation(string animationName)
        {
            if (_animator == null || _currentAnimation == animationName)
            {
                return;
            }

            _animator.CrossFade(animationName, 0.1f);
            _currentAnimation = animationName;
        }

        private void Die()
        {
            _isDead = true;
            if (_agent != null)
            {
                _agent.enabled = false;
            }

            var collision = GetComponent<Collider>();
            if (collision != null)
            {
                collision.enabled = false;
            }

            _deathFlyDirection = _targetPlayer != null
                ? (transform.position - _targetPlayer.position).normalized
                : Vector3.up;
            _deathFlyDirection.y = 1.5f;
            _deathFlySpeed = 8f;
            if (_animator != null)
            {
                _animator.enabled = false;
            }

            Destroy(gameObject, 2f);
        }

        private void UpdateDeathPresentation()
        {
            transform.position += _deathFlyDirection * _deathFlySpeed * Time.deltaTime;
            _deathFlyDirection.y -= 5f * Time.deltaTime;
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.zero, Time.deltaTime * 3f);
        }

        // 以下空方法保留给第三方动画片段中的既有 Animation Event，避免资源在迁移期间持续报错。
        public void AttackEnd() { }
        public void PlayStep() { }
        public void Grunt() { }
        public void MeleeAttackStart() { }
        public void MeleeAttackEnd() { }
    }
}
