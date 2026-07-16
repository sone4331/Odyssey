namespace Odyssey.Gameplay.Characters
{
    /// <summary>
    /// 表示玩家移动轴的互斥状态，只描述角色与地面、空中和墙面的关系。
    /// 将移动状态与攻击、冲刺、受击分离，是正交状态机模式的第一条状态轴，避免组合状态数量指数增长。
    /// </summary>
    public enum PlayerLocomotionStateId
    {
        Grounded,
        Airborne,
        WallSlide
    }

    /// <summary>
    /// 表示玩家动作轴的互斥状态，负责会暂时改变移动控制权的动作。
    /// 独立动作轴让角色可以保持“位于空中”的移动事实，同时执行冲刺或受击，便于动画、网络和调试分别解释。
    /// </summary>
    public enum PlayerActionStateId
    {
        Free,
        Attack,
        Dash,
        Hit
    }

    /// <summary>
    /// 表示外部系统向动作轴提交的一次意图，None 代表本帧没有新请求。
    /// 请求与当前状态分离，使输入、AI 或网络命令只提交意图，状态机统一决定能否打断和何时切换。
    /// </summary>
    public enum PlayerActionRequest
    {
        None,
        Attack,
        Dash,
        Hit
    }

    /// <summary>
    /// 保存一次移动状态判定所需的最小事实集合，是不包含 Unity 对象的不可变值对象。
    /// 采用快照模式将物理感知与状态决策分离，使转换规则可以在 EditMode 和纯 C# 环境稳定测试。
    /// </summary>
    public readonly struct PlayerLocomotionObservation
    {
        public PlayerLocomotionObservation(
            bool isGrounded,
            bool isTouchingWall,
            bool isDescending,
            bool jumpRequested)
        {
            IsGrounded = isGrounded;
            IsTouchingWall = isTouchingWall;
            IsDescending = isDescending;
            JumpRequested = jumpRequested;
        }

        public bool IsGrounded { get; }
        public bool IsTouchingWall { get; }
        public bool IsDescending { get; }
        public bool JumpRequested { get; }
    }

    /// <summary>
    /// 集中定义移动状态转换优先级，不执行位移、动画或物理查询。
    /// 采用策略模式把“为什么切换”从 Unity 状态实现中提取出来，避免不同状态各自复制并逐渐产生冲突规则。
    /// </summary>
    public static class PlayerLocomotionTransitionPolicy
    {
        /// <summary>
        /// 根据当前状态与本帧感知快照选择目标状态；没有转换时返回当前状态。
        /// 判定顺序体现业务优先级：落地高于贴墙，主动墙跳高于继续滑墙，保证同一帧只有一个明确结果。
        /// </summary>
        public static PlayerLocomotionStateId SelectNext(
            PlayerLocomotionStateId current,
            PlayerLocomotionObservation observation)
        {
            switch (current)
            {
                case PlayerLocomotionStateId.Grounded:
                    return observation.IsGrounded
                        ? PlayerLocomotionStateId.Grounded
                        : PlayerLocomotionStateId.Airborne;

                case PlayerLocomotionStateId.Airborne:
                    if (observation.IsGrounded && observation.IsDescending)
                    {
                        return PlayerLocomotionStateId.Grounded;
                    }

                    if (observation.IsTouchingWall && observation.IsDescending)
                    {
                        return PlayerLocomotionStateId.WallSlide;
                    }

                    return PlayerLocomotionStateId.Airborne;

                case PlayerLocomotionStateId.WallSlide:
                    if (observation.IsGrounded)
                    {
                        return PlayerLocomotionStateId.Grounded;
                    }

                    if (observation.JumpRequested || !observation.IsTouchingWall)
                    {
                        return PlayerLocomotionStateId.Airborne;
                    }

                    return PlayerLocomotionStateId.WallSlide;

                default:
                    return PlayerLocomotionStateId.Airborne;
            }
        }
    }

    /// <summary>
    /// 集中定义动作打断优先级，保证受击等高优先级动作不会被普通输入覆盖。
    /// 采用策略模式和显式优先级规则，使客户端预测与未来 Host 权威校验可以复用同一套可解释结论。
    /// </summary>
    public static class PlayerActionTransitionPolicy
    {
        /// <summary>
        /// 根据当前动作、外部请求和动作完成标记选择下一状态。
        /// 受击拥有最高优先级；受击结束前忽略普通请求；其余动作完成后统一回到 Free，避免散落的返回逻辑。
        /// </summary>
        public static PlayerActionStateId SelectNext(
            PlayerActionStateId current,
            PlayerActionRequest request,
            bool actionFinished)
        {
            if (request == PlayerActionRequest.Hit)
            {
                return PlayerActionStateId.Hit;
            }

            if (current == PlayerActionStateId.Hit)
            {
                return actionFinished ? PlayerActionStateId.Free : PlayerActionStateId.Hit;
            }

            if (actionFinished)
            {
                return PlayerActionStateId.Free;
            }

            if (current != PlayerActionStateId.Free)
            {
                return current;
            }

            switch (request)
            {
                case PlayerActionRequest.Attack:
                    return PlayerActionStateId.Attack;
                case PlayerActionRequest.Dash:
                    return PlayerActionStateId.Dash;
                default:
                    return PlayerActionStateId.Free;
            }
        }
    }
}
