using UnityEngine;

namespace Odyssey.Characters.Player
{
    /// <summary>
    /// 保存玩家移动中不依赖场景对象的几何规则，使坡面投影可以独立测试并避免状态重复实现向量细节。
    /// 采用纯函数策略：输入期望方向和地面法线，返回等长的坡面切向方向，不读取或修改运行时状态。
    /// </summary>
    public static class PlayerMovementMath
    {
        public static Vector3 ProjectDirectionOnGround(Vector3 direction, Vector3 groundNormal)
        {
            if (direction.sqrMagnitude <= 0.0001f || groundNormal.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            var projected = Vector3.ProjectOnPlane(direction, groundNormal);
            return projected.sqrMagnitude <= 0.0001f
                ? Vector3.zero
                : projected.normalized * direction.magnitude;
        }
    }
}
