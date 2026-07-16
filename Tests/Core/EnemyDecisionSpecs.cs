using Odyssey.Gameplay.AI;

/// <summary>
/// 验证怪物 Utility 决策的业务优先级，确保 AI 分层后仍能脱离 Unity 场景回归。
/// </summary>
internal static class EnemyDecisionSpecs
{
    public static void Register()
    {
        Spec.Run("目标进入攻击范围且冷却完成时选择攻击", ReadyTargetInRangeSelectsAttack);
        Spec.Run("目标位于追击范围时选择追击", TargetInChaseRangeSelectsChase);
        Spec.Run("低生命近距离时选择撤退", LowHealthAtCloseRangeSelectsRetreat);
        Spec.Run("没有有效目标时选择待机", MissingTargetSelectsIdle);
        Spec.Run("攻击冷却期间在近距离保持待机", CooldownAtCloseRangeSelectsIdle);
    }

    private static void ReadyTargetInRangeSelectsAttack()
    {
        var decision = CreateModel().Decide(new EnemyDecisionContext(
            hasTarget: true,
            distanceToTarget: 1f,
            chaseRange: 10f,
            attackRange: 2f,
            healthRatio: 1f,
            attackReady: true));

        Spec.Equal(EnemyGoal.Attack, decision.Goal, "近距离可攻击目标未选择攻击");
    }

    private static void TargetInChaseRangeSelectsChase()
    {
        var decision = CreateModel().Decide(new EnemyDecisionContext(
            hasTarget: true,
            distanceToTarget: 6f,
            chaseRange: 10f,
            attackRange: 2f,
            healthRatio: 1f,
            attackReady: true));

        Spec.Equal(EnemyGoal.Chase, decision.Goal, "追击范围内目标未选择追击");
    }

    private static void LowHealthAtCloseRangeSelectsRetreat()
    {
        var decision = CreateModel().Decide(new EnemyDecisionContext(
            hasTarget: true,
            distanceToTarget: 1f,
            chaseRange: 10f,
            attackRange: 2f,
            healthRatio: 0.2f,
            attackReady: true));

        Spec.Equal(EnemyGoal.Retreat, decision.Goal, "低生命近距离未选择撤退");
    }

    private static void MissingTargetSelectsIdle()
    {
        var decision = CreateModel().Decide(new EnemyDecisionContext(
            hasTarget: false,
            distanceToTarget: float.MaxValue,
            chaseRange: 10f,
            attackRange: 2f,
            healthRatio: 1f,
            attackReady: true));

        Spec.Equal(EnemyGoal.Idle, decision.Goal, "没有目标时未选择待机");
    }

    private static void CooldownAtCloseRangeSelectsIdle()
    {
        var decision = CreateModel().Decide(new EnemyDecisionContext(
            hasTarget: true,
            distanceToTarget: 1f,
            chaseRange: 10f,
            attackRange: 2f,
            healthRatio: 1f,
            attackReady: false));

        Spec.Equal(EnemyGoal.Idle, decision.Goal, "攻击冷却期间近距离行为不稳定");
    }

    private static EnemyDecisionModel CreateModel()
    {
        return new EnemyDecisionModel();
    }
}
