using Odyssey.Characters.Player;
using Odyssey.Gameplay.AI;
using Odyssey.Gameplay.Config;
using UnityEngine;

namespace Odyssey.Characters.Enemies
{
    /// <summary>
    /// 负责寻找有效玩家并把距离、生命、冷却和感知滞回结果写入怪物黑板。
    /// 采用 Sensor/Perception 分层，只有本类读取场景对象；纯 C# 行为树只消费完整事实快照。
    /// </summary>
    internal sealed class EnemyPerception
    {
        private const float ForgetRangeMultiplier = 1.25f;
        private readonly Transform _owner;
        private PlayerController _targetPlayer;
        private bool _hasSensedTarget;

        public EnemyPerception(Transform owner)
        {
            _owner = owner;
        }

        public Transform Target => _targetPlayer == null ? null : _targetPlayer.transform;

        /// <summary>
        /// 使用较小发现范围和较大丢失范围形成滞回，避免玩家在边界附近移动时怪物每帧反复切换巡逻与追击。
        /// 玩家死亡或对象失效会立即清除感知，怪物无需等待距离判定即可返回巡逻。
        /// </summary>
        public void Sense(
            EnemyBlackboard blackboard,
            int currentHealth,
            int maximumHealth,
            float detectionRange,
            float attackRange,
            float minimumAttackRange,
            bool attackReady,
            bool attackInProgress,
            bool isHitReacting,
            bool isDead,
            bool hasPatrolRoute,
            EnemyAttackMode attackMode)
        {
            ResolveTarget();
            var playerAvailable = _targetPlayer != null &&
                                  _targetPlayer.isActiveAndEnabled &&
                                  _targetPlayer.CurrentHealth > 0;
            var distance = playerAvailable
                ? Vector3.ProjectOnPlane(_owner.position - _targetPlayer.transform.position, Vector3.up).magnitude
                : float.MaxValue;
            var forgetRange = Mathf.Max(detectionRange, detectionRange * ForgetRangeMultiplier);

            if (!playerAvailable || isDead)
            {
                _hasSensedTarget = false;
            }
            else if (_hasSensedTarget)
            {
                _hasSensedTarget = distance <= forgetRange;
            }
            else
            {
                _hasSensedTarget = distance <= detectionRange;
            }

            var healthRatio = maximumHealth <= 0
                ? 0f
                : Mathf.Clamp01((float)currentHealth / maximumHealth);
            blackboard.UpdatePerception(
                _hasSensedTarget,
                distance,
                detectionRange,
                forgetRange,
                attackRange,
                minimumAttackRange,
                healthRatio,
                attackReady,
                attackInProgress,
                isHitReacting,
                isDead,
                hasPatrolRoute,
                attackMode);
        }

        private void ResolveTarget()
        {
            if (_targetPlayer != null && _targetPlayer.gameObject.activeInHierarchy)
            {
                return;
            }

            var player = GameObject.FindGameObjectWithTag("Player");
            _targetPlayer = player == null ? null : player.GetComponentInParent<PlayerController>();
            _hasSensedTarget = false;
        }
    }
}
