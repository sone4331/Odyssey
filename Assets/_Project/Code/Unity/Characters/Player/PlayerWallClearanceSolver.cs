using System;
using UnityEngine;

namespace Odyssey.Characters.Player
{
    /// <summary>
    /// 在常态移动阶段为 Ellen 的手臂轮廓预留墙面距离，弥补物理胶囊只覆盖躯干而不覆盖动画四肢的问题。
    /// 采用局部约束求解器而非额外碰撞体：只投影朝墙位移并修复轻微侵入，不参与攻击、武器或空中动作。
    /// </summary>
    internal sealed class PlayerWallClearanceSolver
    {
        public const float VisualRadius = 0.64f;
        private const float MaximumCorrection = 0.08f;
        private const float MaximumWallNormalY = 0.35f;
        private static readonly float[] SampleHeights = { 0.75f, 1.25f };
        private static readonly Collider[] OverlapBuffer = new Collider[16];

        private readonly PlayerController _player;

        public PlayerWallClearanceSolver(PlayerController player)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
        }

        public bool IsActive { get; private set; }

        /// <summary>
        /// 在预计位置查询腰部和双手高度附近的墙面，将朝墙分量改为沿墙滑动，并限制单帧推出距离。
        /// </summary>
        public Vector3 Constrain(Vector3 displacement)
        {
            IsActive = false;
            var vertical = Vector3.up * displacement.y;
            var horizontal = displacement - vertical;
            var correction = Vector3.zero;

            foreach (var height in SampleHeights)
            {
                var sampleCenter = _player.transform.position + horizontal + Vector3.up * height;
                var count = Physics.OverlapSphereNonAlloc(
                    sampleCenter,
                    VisualRadius,
                    OverlapBuffer,
                    _player.WallLayer,
                    QueryTriggerInteraction.Ignore);

                for (var index = 0; index < count; index++)
                {
                    var collider = OverlapBuffer[index];
                    if (collider == null)
                    {
                        continue;
                    }

                    var closest = GetClosestPointWithoutPhysicsWarning(collider, sampleCenter);
                    var separation = sampleCenter - closest;
                    var distance = separation.magnitude;
                    if (distance <= 0.0001f || distance >= VisualRadius)
                    {
                        continue;
                    }

                    var normal = separation / distance;
                    if (Mathf.Abs(normal.y) > MaximumWallNormalY)
                    {
                        continue;
                    }

                    normal.y = 0f;
                    normal.Normalize();
                    if (Vector3.Dot(horizontal, normal) < 0f)
                    {
                        horizontal = Vector3.ProjectOnPlane(horizontal, normal);
                    }

                    var penetration = Mathf.Min(VisualRadius - distance, MaximumCorrection);
                    correction += normal * penetration;
                    IsActive = true;
                }
            }

            return horizontal + Vector3.ClampMagnitude(correction, MaximumCorrection) + vertical;
        }

        /// <summary>
        /// Unity 的 Collider.ClosestPoint 不支持 TerrainCollider 和非凸 MeshCollider。
        /// 对这两类关卡碰撞体退化为包围盒查询，避免墙边保护在每帧产生引擎警告。
        /// </summary>
        private static Vector3 GetClosestPointWithoutPhysicsWarning(Collider collider, Vector3 point)
        {
            if (collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider ||
                collider is MeshCollider meshCollider && meshCollider.convex)
            {
                return collider.ClosestPoint(point);
            }

            return collider.bounds.ClosestPoint(point);
        }
    }
}
