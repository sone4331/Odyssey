using UnityEngine;

namespace Odyssey.Networking
{
    /// <summary>
    /// 描述 Host 拒绝攻击命令的明确原因，供运行时面板、测试和技术演示使用。
    /// 采用结果对象而非布尔值，是为了让安全校验能够被解释和自动化验证，而不是把失败静默吞掉。
    /// </summary>
    public enum NetworkAttackRejection : byte
    {
        None,
        DuplicateOrExpired,
        AttackerDead,
        Cooldown,
        NoTarget,
        OutOfRange
    }

    /// <summary>
    /// 保存一次攻击校验的只读结果，避免客户端表现层猜测 Host 是否接受命令。
    /// 该值对象属于轻量领域规则，不持有网络对象或 Unity 场景引用，因此可以独立测试。
    /// </summary>
    public readonly struct NetworkAttackDecision
    {
        public NetworkAttackDecision(bool accepted, NetworkAttackRejection rejection)
        {
            Accepted = accepted;
            Rejection = rejection;
        }

        public bool Accepted { get; }
        public NetworkAttackRejection Rejection { get; }
    }

    /// <summary>
    /// 集中执行攻击序号、生命、冷却和距离校验，是 Host 权威战斗的规则边界。
    /// 采用 Policy 模式把纯规则与 NGO RPC、动画和场景查询分离，既方便测试，也防止多处校验逐渐产生差异。
    /// </summary>
    public static class NetworkAttackRules
    {
        public static NetworkAttackDecision Validate(
            uint sequence,
            uint lastProcessedSequence,
            bool attackerAlive,
            double serverTime,
            double nextAttackTime,
            bool hasTarget,
            float targetDistance,
            float attackRange)
        {
            if (sequence <= lastProcessedSequence)
            {
                return Reject(NetworkAttackRejection.DuplicateOrExpired);
            }

            if (!attackerAlive)
            {
                return Reject(NetworkAttackRejection.AttackerDead);
            }

            if (serverTime < nextAttackTime)
            {
                return Reject(NetworkAttackRejection.Cooldown);
            }

            if (!hasTarget)
            {
                return Reject(NetworkAttackRejection.NoTarget);
            }

            if (targetDistance > Mathf.Max(0f, attackRange))
            {
                return Reject(NetworkAttackRejection.OutOfRange);
            }

            return new NetworkAttackDecision(true, NetworkAttackRejection.None);
        }

        public static string ToChinese(NetworkAttackRejection rejection)
        {
            switch (rejection)
            {
                case NetworkAttackRejection.None:
                    return "Host 已接受";
                case NetworkAttackRejection.DuplicateOrExpired:
                    return "重复或过期序号";
                case NetworkAttackRejection.AttackerDead:
                    return "角色已死亡";
                case NetworkAttackRejection.Cooldown:
                    return "攻击仍在冷却";
                case NetworkAttackRejection.NoTarget:
                    return "前方没有目标";
                case NetworkAttackRejection.OutOfRange:
                    return "目标超出攻击距离";
                default:
                    return "未知拒绝原因";
            }
        }

        private static NetworkAttackDecision Reject(NetworkAttackRejection reason)
        {
            return new NetworkAttackDecision(false, reason);
        }
    }
}
