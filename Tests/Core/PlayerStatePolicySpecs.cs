using Odyssey.Gameplay.Characters;

/// <summary>
/// 验证玩家移动轴与动作轴的纯规则，确保状态划分不依赖 Unity 场景也能回归测试。
/// 采用规格测试描述状态转换意图，防止后续新增技能时重新把移动和动作耦合成单一巨型状态机。
/// </summary>
internal static class PlayerStatePolicySpecs
{
    public static void Register()
    {
        Spec.Run("地面失去支撑后进入空中", GroundedFallsIntoAirborne);
        Spec.Run("下降并接触墙面时进入滑墙", AirborneDescendingAtWallStartsWallSlide);
        Spec.Run("滑墙失去墙面后回到空中", WallSlideWithoutWallReturnsAirborne);
        Spec.Run("滑墙落地后进入地面", WallSlideLandingReturnsGrounded);
        Spec.Run("受击请求可以打断攻击动作", HitRequestInterruptsAttack);
        Spec.Run("普通动作不会覆盖正在执行的受击", NormalActionCannotOverrideHit);
    }

    private static void GroundedFallsIntoAirborne()
    {
        var observation = new PlayerLocomotionObservation(
            isGrounded: false,
            isTouchingWall: false,
            isDescending: true,
            jumpRequested: false);

        var next = PlayerLocomotionTransitionPolicy.SelectNext(PlayerLocomotionStateId.Grounded, observation);

        Spec.Equal(PlayerLocomotionStateId.Airborne, next, "离地后没有进入空中状态");
    }

    private static void AirborneDescendingAtWallStartsWallSlide()
    {
        var observation = new PlayerLocomotionObservation(
            isGrounded: false,
            isTouchingWall: true,
            isDescending: true,
            jumpRequested: false);

        var next = PlayerLocomotionTransitionPolicy.SelectNext(PlayerLocomotionStateId.Airborne, observation);

        Spec.Equal(PlayerLocomotionStateId.WallSlide, next, "下降接触墙面后没有进入滑墙状态");
    }

    private static void WallSlideWithoutWallReturnsAirborne()
    {
        var observation = new PlayerLocomotionObservation(
            isGrounded: false,
            isTouchingWall: false,
            isDescending: true,
            jumpRequested: false);

        var next = PlayerLocomotionTransitionPolicy.SelectNext(PlayerLocomotionStateId.WallSlide, observation);

        Spec.Equal(PlayerLocomotionStateId.Airborne, next, "离开墙面后没有回到空中状态");
    }

    private static void WallSlideLandingReturnsGrounded()
    {
        var observation = new PlayerLocomotionObservation(
            isGrounded: true,
            isTouchingWall: true,
            isDescending: true,
            jumpRequested: false);

        var next = PlayerLocomotionTransitionPolicy.SelectNext(PlayerLocomotionStateId.WallSlide, observation);

        Spec.Equal(PlayerLocomotionStateId.Grounded, next, "滑墙落地后没有进入地面状态");
    }

    private static void HitRequestInterruptsAttack()
    {
        var next = PlayerActionTransitionPolicy.SelectNext(
            PlayerActionStateId.Attack,
            PlayerActionRequest.Hit,
            actionFinished: false);

        Spec.Equal(PlayerActionStateId.Hit, next, "受击请求没有打断攻击动作");
    }

    private static void NormalActionCannotOverrideHit()
    {
        var next = PlayerActionTransitionPolicy.SelectNext(
            PlayerActionStateId.Hit,
            PlayerActionRequest.Dash,
            actionFinished: false);

        Spec.Equal(PlayerActionStateId.Hit, next, "普通动作错误覆盖了受击动作");
    }
}
