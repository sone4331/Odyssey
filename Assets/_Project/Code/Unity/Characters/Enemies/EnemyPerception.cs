using Odyssey.Gameplay.AI;
using UnityEngine;

namespace Odyssey.Characters.Enemies
{
    /// <summary>
    /// 负责获取玩家目标并把距离、生命和冷却事实写入单个怪物的 Blackboard。
    /// 采用 Sensor/Perception 分层，只有本类读取场景对象；决策模型只消费纯 C# 快照。
    /// </summary>
    internal sealed class EnemyPerception
    {
        private readonly Transform _owner;
        private Transform _target;

        public EnemyPerception(Transform owner)
        {
            _owner = owner;
        }

        public Transform Target => _target;

        /// <summary>
        /// 每帧写入一份完整感知快照；目标丢失时按 Tag 重新获取，避免 Enemy 自己承担查找和距离计算。
        /// </summary>
        public void Sense(
            EnemyBlackboard blackboard,
            int currentHealth,
            int maximumHealth,
            float chaseRange,
            float attackRange,
            float minimumAttackRange,
            bool attackReady)
        {
            if (_target == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                _target = player == null ? null : player.transform;
            }

            var hasTarget = _target != null && _target.gameObject.activeInHierarchy;
            var distance = hasTarget
                ? Vector3.Distance(_owner.position, _target.position)
                : float.MaxValue;
            var healthRatio = maximumHealth <= 0
                ? 0f
                : Mathf.Clamp01((float)currentHealth / maximumHealth);

            blackboard.UpdatePerception(
                hasTarget,
                distance,
                chaseRange,
                attackRange,
                minimumAttackRange,
                healthRatio,
                attackReady);
        }
    }
}
