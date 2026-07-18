using Odyssey.Gameplay.AI;
using UnityEngine;
using UnityEngine.AI;
using Odyssey.Gameplay.Config;
using System;

namespace Odyssey.Characters.Enemies
{
    /// <summary>
    /// 把 Utility 目标翻译为 NavMesh、朝向和 Animator 操作，并维护攻击冷却与受击锁定。
    /// 采用 Action 执行层隔离决策和表现；只实现当前 Demo 需要的 Idle、Chase、Attack、Retreat，不构建通用行为树节点框架。
    /// </summary>
    internal sealed class EnemyActionRuntime
    {
        private readonly Transform _owner;
        private readonly Animator _animator;
        private readonly NavMeshAgent _agent;
        private string _currentAnimation = string.Empty;
        private float _lastAttackTime = float.NegativeInfinity;
        private float _lockRemaining;
        private float _retreatRefreshRemaining;
        private EnemyAttackMode _attackMode;
        private float _attackWindup;
        private float _pendingShotRemaining;
        private Vector3 _pendingTargetPosition;
        private Action<Vector3> _launchProjectile;
        private Action<bool> _setTelegraph;

        public EnemyActionRuntime(Transform owner, Animator animator, NavMeshAgent agent)
        {
            _owner = owner;
            _animator = animator;
            _agent = agent;
        }

        public bool CanAttack(float currentTime, float cooldown)
        {
            return _lockRemaining <= 0f && currentTime >= _lastAttackTime + cooldown;
        }

        /// <summary>
        /// 注入当前配置选择的攻击执行方式；远程攻击通过委托回到 Enemy 创建场景对象，动作层只维护前摇时序。
        /// 这是一种轻量 Strategy 边界，避免为仅有两种攻击方式建立通用技能节点框架。
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
            _pendingShotRemaining = -1f;
        }

        /// <summary>
        /// 推进受击或攻击动作锁；锁定期间停止导航并跳过 Utility 决策，保证动画不会被每帧目标选择覆盖。
        /// </summary>
        public bool TickLock(float deltaTime)
        {
            if (_lockRemaining <= 0f)
            {
                return false;
            }

            _lockRemaining -= deltaTime;
            TickPendingProjectile(deltaTime);
            StopNavigation();
            return true;
        }

        public void Execute(
            EnemyDecision decision,
            Transform target,
            float currentTime,
            float attackCooldown,
            float deltaTime)
        {
            switch (decision.Goal)
            {
                case EnemyGoal.Chase:
                    Chase(target);
                    break;
                case EnemyGoal.Attack:
                    Attack(target, currentTime, attackCooldown);
                    break;
                case EnemyGoal.Retreat:
                    Retreat(target, deltaTime);
                    break;
                default:
                    Idle();
                    break;
            }
        }

        public void NotifyHit()
        {
            CancelPendingProjectile();
            _lockRemaining = 0.5f;
            StopNavigation();
            PlayAnimation(_attackMode == EnemyAttackMode.Projectile
                ? "TopHit"
                : "Hit" + UnityEngine.Random.Range(1, 5), 0.06f);
        }

        public void DisableNavigation()
        {
            CancelPendingProjectile();
            if (_agent != null)
            {
                _agent.enabled = false;
            }
        }

        private void Idle()
        {
            StopNavigation();
            PlayAnimation("Idle", 0.12f);
        }

        private void Chase(Transform target)
        {
            if (!CanNavigate() || target == null)
            {
                Idle();
                return;
            }

            _agent.isStopped = false;
            _agent.SetDestination(target.position);
            PlayAnimation(_attackMode == EnemyAttackMode.Projectile ? "Fleeing" : "Run", 0.12f);
        }

        private void Attack(Transform target, float currentTime, float cooldown)
        {
            StopNavigation();
            if (!CanAttack(currentTime, cooldown) || target == null)
            {
                PlayAnimation("Idle", 0.1f);
                return;
            }

            var direction = target.position - _owner.position;
            direction.y = 0f;
            if (direction != Vector3.zero)
            {
                _owner.rotation = Quaternion.LookRotation(direction.normalized);
            }

            _lastAttackTime = currentTime;
            _lockRemaining = Mathf.Max(1.2f, _attackWindup + 0.35f);
            if (_attackMode == EnemyAttackMode.Projectile)
            {
                _pendingTargetPosition = target.position + Vector3.up * 0.75f;
                _pendingShotRemaining = _attackWindup;
                _setTelegraph?.Invoke(true);
            }

            PlayAnimation("Attack", 0.08f);
        }

        private void Retreat(Transform target, float deltaTime)
        {
            if (!CanNavigate() || target == null)
            {
                Idle();
                return;
            }

            _retreatRefreshRemaining -= deltaTime;
            if (_retreatRefreshRemaining <= 0f)
            {
                _retreatRefreshRemaining = 0.25f;
                var away = _owner.position - target.position;
                away.y = 0f;
                if (away == Vector3.zero)
                {
                    away = -_owner.forward;
                }

                var desired = _owner.position + away.normalized * 3f;
                if (NavMesh.SamplePosition(desired, out var hit, 2f, NavMesh.AllAreas))
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

        private bool CanNavigate()
        {
            return _agent != null && _agent.enabled && _agent.isOnNavMesh;
        }

        private void StopNavigation()
        {
            if (!CanNavigate())
            {
                return;
            }

            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
        }

        private void PlayAnimation(string stateName, float transitionDuration)
        {
            if (_animator == null || _currentAnimation == stateName)
            {
                return;
            }

            _animator.CrossFadeInFixedTime(stateName, transitionDuration, 0);
            _currentAnimation = stateName;
        }

        private void TickPendingProjectile(float deltaTime)
        {
            if (_attackMode != EnemyAttackMode.Projectile || _pendingShotRemaining < 0f)
            {
                return;
            }

            _pendingShotRemaining -= deltaTime;
            if (_pendingShotRemaining > 0f)
            {
                return;
            }

            _pendingShotRemaining = -1f;
            _setTelegraph?.Invoke(false);
            _launchProjectile?.Invoke(_pendingTargetPosition);
        }

        private void CancelPendingProjectile()
        {
            _pendingShotRemaining = -1f;
            _setTelegraph?.Invoke(false);
        }
    }
}
