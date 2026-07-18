using System;
using Odyssey.Characters.Enemies;
using Odyssey.Gameplay.Combat;
using UnityEngine;

namespace Odyssey.Characters.Player
{
    /// <summary>
    /// 集中执行玩家脚底踩踏与墙面接触查询，把 Unity Physics 结果翻译为移动状态机可消费的环境事实。
    /// 采用 Sensor/Adapter 模式并复用查询缓冲区：状态只负责决策，感知器只负责胶囊几何、层级过滤和命中有效性。
    /// </summary>
    internal sealed class PlayerEnvironmentSensor
    {
        private const float StompRadiusRatio = 0.7f;
        private const float MinimumStompDistance = 0.1f;
        private const float StompSafetyDistance = 0.05f;
        private const float MinimumStompNormalUp = 0.5f;
        private const float WallProbeDistance = 0.08f;
        private const float MaximumWallNormalUp = 0.35f;
        private readonly RaycastHit[] _stompHits = new RaycastHit[8];
        private readonly RaycastHit[] _wallHits = new RaycastHit[8];
        private readonly PlayerController _player;

        public PlayerEnvironmentSensor(PlayerController player)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
        }

        /// <summary>
        /// 从 CharacterController 的真实脚底向下扫描本帧预计移动距离，只接受敌人朝上的表面并返回最近目标。
        /// 动态距离覆盖高速下落，避免固定短射线在低帧率下从敌人头顶穿过。
        /// </summary>
        public bool TryFindStompTarget(
            float verticalVelocity,
            float deltaTime,
            out IDamageable damageable)
        {
            damageable = null;
            if (verticalVelocity >= 0f || deltaTime <= 0f)
            {
                return false;
            }

            var controller = _player.Controller;
            var up = _player.transform.up;
            var center = _player.transform.TransformPoint(controller.center);
            var footBottom = center - up * (controller.height * 0.5f);
            var probeRadius = Mathf.Max(0.05f, controller.radius * StompRadiusRatio);
            var origin = footBottom + up * (probeRadius + controller.skinWidth);
            var distance = Mathf.Max(
                MinimumStompDistance,
                -verticalVelocity * deltaTime + controller.skinWidth + StompSafetyDistance);
            var count = Physics.SphereCastNonAlloc(
                origin,
                probeRadius,
                -up,
                _stompHits,
                distance,
                _player.EnemyLayer,
                QueryTriggerInteraction.Ignore);

            var nearestDistance = float.PositiveInfinity;
            for (var index = 0; index < count; index++)
            {
                var hit = _stompHits[index];
                if (hit.collider == null || Vector3.Dot(hit.normal, up) < MinimumStompNormalUp)
                {
                    continue;
                }

                var enemy = hit.collider.GetComponentInParent<Enemy>();
                if (enemy == null || hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = hit.distance;
                damageable = enemy;
            }

            return damageable != null;
        }

        /// <summary>
        /// 使用与 CharacterController 同形的胶囊沿指定水平方向探测近距离墙面，覆盖腰部射线容易漏掉的侧面和墙角。
        /// 只返回法线接近水平且确实阻挡探测方向的最近表面，地面、斜坡和背向表面会被过滤。
        /// </summary>
        public bool TryFindWall(Vector3 direction, out RaycastHit nearestHit)
        {
            nearestHit = default;
            var up = _player.transform.up;
            var horizontalDirection = Vector3.ProjectOnPlane(direction, up);
            if (horizontalDirection.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            horizontalDirection.Normalize();
            var controller = _player.Controller;
            var center = _player.transform.TransformPoint(controller.center);
            var radius = Mathf.Max(0.05f, controller.radius - controller.skinWidth);
            var sphereOffset = Mathf.Max(0f, controller.height * 0.5f - radius);
            var top = center + up * sphereOffset;
            var bottom = center - up * sphereOffset;
            var count = Physics.CapsuleCastNonAlloc(
                top,
                bottom,
                radius,
                horizontalDirection,
                _wallHits,
                controller.skinWidth + WallProbeDistance,
                _player.WallLayer,
                QueryTriggerInteraction.Ignore);

            var found = false;
            var nearestDistance = float.PositiveInfinity;
            for (var index = 0; index < count; index++)
            {
                var hit = _wallHits[index];
                if (hit.collider == null ||
                    Mathf.Abs(Vector3.Dot(hit.normal, up)) > MaximumWallNormalUp ||
                    Vector3.Dot(horizontalDirection, hit.normal) >= -0.05f ||
                    hit.distance >= nearestDistance)
                {
                    continue;
                }

                found = true;
                nearestDistance = hit.distance;
                nearestHit = hit;
            }

            return found;
        }
    }
}
