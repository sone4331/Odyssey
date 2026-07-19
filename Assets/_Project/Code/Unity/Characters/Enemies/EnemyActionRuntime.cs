using System;
using Odyssey.Gameplay.AI;
using Odyssey.Gameplay.Config;
using UnityEngine;
using UnityEngine.AI;

namespace Odyssey.Characters.Enemies
{
    /// <summary>
    /// 将行为树的 Patrol、Chase、Attack、Retreat 等意图翻译为 NavMesh、朝向、Animator 和伤害时序。
    /// 采用端口适配器与 Strategy 模式：本类只回答“如何执行”，不判断“何时选择”，从而避免决策和表现再次耦合。
    /// </summary>
    internal sealed class EnemyActionRuntime : IEnemyBehaviorActions
    {
        private const float ChaseRefreshInterval = 0.15f;
        private const float RetreatRefreshInterval = 0.25f;

        private readonly Transform _owner;
        private readonly Animator _animator;
        private readonly NavMeshAgent _agent;
        private readonly float _combatMoveSpeed;
        private readonly float _combatStoppingDistance;
        private readonly NavMeshPath _patrolPath = new NavMeshPath();
        private EnemyPatrolRoute _patrolRoute;
        private Transform _target;
        private string _currentAnimation = string.Empty;
        private float _lastAttackTime = float.NegativeInfinity;
        private float _hitLockRemaining;
        private float _attackLockRemaining;
        private float _chaseRefreshRemaining;
        private float _retreatRefreshRemaining;
        private Vector3 _lastPatrolDestination;
        private bool _hasPatrolDestination;
        private EnemyAttackMode _attackMode;
        private float _attackWindup;
        private float _pendingAttackRemaining = -1f;
        private Vector3 _pendingTargetPosition;
        private Action<Vector3> _launchProjectile;
        private Action<bool> _setTelegraph;

        public EnemyActionRuntime(Transform owner, Animator animator, NavMeshAgent agent)
        {
            _owner = owner;
            _animator = animator;
            _agent = agent;
            _combatMoveSpeed = agent == null ? 0f : agent.speed;
            _combatStoppingDistance = agent == null ? 0f : agent.stoppingDistance;
        }

        public bool IsHitReacting => _hitLockRemaining > 0f;
        public bool IsAttackInProgress => _attackLockRemaining > 0f;

        public bool CanAttack(float currentTime, float cooldown)
        {
            return !IsHitReacting && !IsAttackInProgress && currentTime >= _lastAttackTime + cooldown;
        }

        public void SetTarget(Transform target) => _target = target;

        /// <summary>
        /// 推进受击锁、攻击锁和远程攻击前摇。近战伤害由身体碰撞体重叠决定，不再使用时间猜测命中帧。
        /// </summary>
        public void TickTimers(float deltaTime)
        {
            _hitLockRemaining = Mathf.Max(0f, _hitLockRemaining - deltaTime);
            _attackLockRemaining = Mathf.Max(0f, _attackLockRemaining - deltaTime);
            TickPendingAttack(deltaTime);
        }

        /// <summary>
        /// 注入场景配置的巡逻路线；动作层不创建点位，也不持有战区控制器。
        /// </summary>
        public void ConfigurePatrol(EnemyPatrolRoute patrolRoute) => _patrolRoute = patrolRoute;

        /// <summary>
        /// 注入攻击表现与投射物策略。近战攻击只播放动画，Spitter 才在前摇结束后创建投射物。
        /// </summary>
        public void ConfigureAttack(
            EnemyAttackMode attackMode,
            float attackWindup,
            Action<Vector3> launchProjectile,
            Action<bool> setTelegraph)
        {
            _attackMode = attackMode;
            _attackWindup = Mathf.Max(0f, attackWindup);
            _launchProjectile = launchProjectile;
            _setTelegraph = setTelegraph;
            CancelPendingAttack();
        }

        public BehaviorStatus Tick(EnemyGoal goal, float currentTime, float attackCooldown, float deltaTime)
        {
            switch (goal)
            {
                case EnemyGoal.Patrol:
                    Patrol(deltaTime);
                    break;
                case EnemyGoal.Chase:
                    Chase(deltaTime);
                    break;
                case EnemyGoal.Attack:
                    return Attack(currentTime, attackCooldown);
                case EnemyGoal.Retreat:
                    Retreat(deltaTime);
                    break;
                case EnemyGoal.Hit:
                    StopNavigation();
                    break;
                case EnemyGoal.Dead:
                    DisableNavigation();
                    break;
                default:
                    Idle(deltaTime);
                    break;
            }

            return BehaviorStatus.Running;
        }

        /// <summary>
        /// 清理被行为树抢占的动作副作用。尤其要取消尚未结算的攻击前摇，防止目标已经丢失后仍在原地造成伤害。
        /// </summary>
        public void Abort(EnemyGoal goal)
        {
            if (goal == EnemyGoal.Attack)
            {
                _attackLockRemaining = 0f;
                CancelPendingAttack();
            }

            if (goal == EnemyGoal.Chase)
            {
                _chaseRefreshRemaining = 0f;
            }
            else if (goal == EnemyGoal.Retreat)
            {
                _retreatRefreshRemaining = 0f;
            }
            else if (goal == EnemyGoal.Patrol)
            {
                _hasPatrolDestination = false;
            }

            StopNavigation();
        }

        public void NotifyHit()
        {
            _attackLockRemaining = 0f;
            CancelPendingAttack();
            _hitLockRemaining = 0.5f;
            StopNavigation();
            PlayAnimation(_attackMode == EnemyAttackMode.Projectile
                ? "TopHit"
                : "Hit" + UnityEngine.Random.Range(1, 5), 0.06f);
        }

        public void DisableNavigation()
        {
            CancelPendingAttack();
            if (_agent != null && _agent.enabled)
            {
                _agent.enabled = false;
            }
        }

        private void Idle(float deltaTime)
        {
            StopNavigation();
            FaceTarget(deltaTime);
            PlayAnimation("Idle", 0.12f);
        }

        /// <summary>
        /// 追击目的地按固定低频刷新，而 NavMeshAgent 自身仍逐帧移动；这样既能跟随玩家，也避免每帧重复寻路。
        /// </summary>
        private void Chase(float deltaTime)
        {
            if (!CanNavigate() || _target == null)
            {
                Idle(deltaTime);
                return;
            }

            RestoreCombatNavigation();
            _chaseRefreshRemaining -= deltaTime;
            if (_chaseRefreshRemaining <= 0f)
            {
                _chaseRefreshRemaining = ChaseRefreshInterval;
                _agent.isStopped = false;
                _agent.SetDestination(_target.position);
            }

            PlayAnimation(_attackMode == EnemyAttackMode.Projectile ? "Fleeing" : "Run", 0.12f);
        }

        private BehaviorStatus Attack(float currentTime, float cooldown)
        {
            StopNavigation();
            if (IsAttackInProgress)
            {
                FaceTarget(0.02f);
                return BehaviorStatus.Running;
            }

            if (!CanAttack(currentTime, cooldown) || _target == null)
            {
                return BehaviorStatus.Failure;
            }

            FaceTarget(1f);
            _lastAttackTime = currentTime;
            _attackLockRemaining = Mathf.Max(1.2f, _attackWindup + 0.35f);
            if (_attackMode == EnemyAttackMode.Projectile)
            {
                _pendingAttackRemaining = _attackWindup;
                _pendingTargetPosition = _target.position + Vector3.up * 0.75f;
                _setTelegraph?.Invoke(true);
            }
            else
            {
                _pendingAttackRemaining = -1f;
            }
            PlayAnimation("Attack", 0.08f);
            return BehaviorStatus.Running;
        }

        private void Retreat(float deltaTime)
        {
            if (!CanNavigate() || _target == null)
            {
                Idle(deltaTime);
                return;
            }

            RestoreCombatNavigation();
            _agent.stoppingDistance = 0.1f;
            _retreatRefreshRemaining -= deltaTime;
            if (_retreatRefreshRemaining <= 0f)
            {
                _retreatRefreshRemaining = RetreatRefreshInterval;
                var away = Vector3.ProjectOnPlane(_owner.position - _target.position, Vector3.up);
                if (away.sqrMagnitude <= 0.001f)
                {
                    away = -_owner.forward;
                }

                var desired = _owner.position + away.normalized * 3f;
                var filter = new NavMeshQueryFilter
                {
                    agentTypeID = _agent.agentTypeID,
                    areaMask = _agent.areaMask
                };
                if (NavMesh.SamplePosition(desired, out var hit, 2f, filter))
                {
                    _agent.isStopped = false;
                    _agent.SetDestination(hit.position);
                }
                else
                {
                    StopNavigation();
                }
            }

            PlayAnimation(_attackMode == EnemyAttackMode.Projectile ? "Fleeing" : "Run", 0.12f);
        }

        private void Patrol(float deltaTime)
        {
            if (!CanNavigate() || _patrolRoute == null ||
                !_patrolRoute.Evaluate(_owner.position, deltaTime, out var destination, out var waiting))
            {
                Idle(deltaTime);
                return;
            }

            if (waiting)
            {
                _hasPatrolDestination = false;
                StopNavigation();
                PlayAnimation("Idle", 0.12f);
                return;
            }

            _agent.speed = Mathf.Max(0.1f, _combatMoveSpeed * 0.55f);
            // 巡逻到点判定必须大于 Agent 的停止距离，否则 Agent 会停在判定圈外并永远无法切换下一个点。
            _agent.stoppingDistance = Mathf.Max(0.05f, _patrolRoute.ArrivalDistance * 0.8f);
            if (!_hasPatrolDestination || Vector3.Distance(_lastPatrolDestination, destination) > 0.05f)
            {
                var filter = new NavMeshQueryFilter
                {
                    agentTypeID = _agent.agentTypeID,
                    areaMask = _agent.areaMask
                };
                if (!NavMesh.CalculatePath(_owner.position, destination, filter, _patrolPath) ||
                    _patrolPath.status != NavMeshPathStatus.PathComplete)
                {
                    _patrolRoute.SkipCurrentPoint();
                    _hasPatrolDestination = false;
                    StopNavigation();
                    PlayAnimation("Idle", 0.12f);
                    return;
                }

                _agent.SetPath(_patrolPath);
                _lastPatrolDestination = destination;
                _hasPatrolDestination = true;
            }

            _agent.isStopped = false;
            PlayAnimation(_attackMode == EnemyAttackMode.Projectile ? "Fleeing" : "Run", 0.12f);
        }

        private bool CanNavigate() => _agent != null && _agent.enabled && _agent.isOnNavMesh;

        private void StopNavigation()
        {
            if (!CanNavigate())
            {
                return;
            }

            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
        }

        private void FaceTarget(float deltaTime)
        {
            if (_target == null)
            {
                return;
            }

            var direction = Vector3.ProjectOnPlane(_target.position - _owner.position, Vector3.up);
            if (direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            var desired = Quaternion.LookRotation(direction.normalized);
            _owner.rotation = Quaternion.RotateTowards(_owner.rotation, desired, 540f * Mathf.Max(0.02f, deltaTime));
        }

        private void PlayAnimation(string stateName, float transitionDuration)
        {
            if (_animator == null)
            {
                return;
            }

            var resolvedState = ResolveAnimationState(stateName);
            if (string.IsNullOrEmpty(resolvedState) || _currentAnimation == resolvedState)
            {
                return;
            }

            _animator.CrossFadeInFixedTime(
                Animator.StringToHash("Base Layer." + resolvedState),
                transitionDuration,
                0);
            _currentAnimation = resolvedState;
        }

        private string ResolveAnimationState(string requestedState)
        {
            if (HasAnimationState(requestedState))
            {
                return requestedState;
            }

            if (requestedState.StartsWith("Hit", StringComparison.Ordinal) && HasAnimationState("TopHit"))
            {
                return "TopHit";
            }

            return HasAnimationState("Idle") ? "Idle" : string.Empty;
        }

        private bool HasAnimationState(string stateName)
        {
            return _animator.HasState(0, Animator.StringToHash("Base Layer." + stateName));
        }

        private void RestoreCombatNavigation()
        {
            if (_agent != null && _combatMoveSpeed > 0f)
            {
                _agent.speed = _combatMoveSpeed;
                _agent.stoppingDistance = _combatStoppingDistance;
            }
        }

        /// <summary>
        /// 只处理远程投射物前摇；近战伤害完全由玩家与怪物身体碰撞体的重叠结果决定。
        /// </summary>
        private void TickPendingAttack(float deltaTime)
        {
            if (_pendingAttackRemaining < 0f)
            {
                return;
            }

            _pendingAttackRemaining -= deltaTime;
            if (_pendingAttackRemaining > 0f)
            {
                return;
            }

            _pendingAttackRemaining = -1f;
            _setTelegraph?.Invoke(false);
            _launchProjectile?.Invoke(_pendingTargetPosition);
        }

        private void CancelPendingAttack()
        {
            _pendingAttackRemaining = -1f;
            _setTelegraph?.Invoke(false);
        }
    }
}
