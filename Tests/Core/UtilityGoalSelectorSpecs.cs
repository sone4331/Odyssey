using System;
using Odyssey.Gameplay.AI;

internal static class UtilityGoalSelectorSpecs
{
    private enum Goal
    {
        Patrol,
        Chase,
        Attack
    }

    public static void Register()
    {
        Spec.Run("Utility 选择器选择最高分", UtilitySelectorPicksHighestScore);
        Spec.Run("Utility 选择器跳过不可用目标", UtilitySelectorSkipsUnavailableGoal);
        Spec.Run("Utility 选择器限制考量分数", UtilitySelectorClampsConsiderationScores);
        Spec.Run("Utility 同分时保持注册顺序", UtilitySelectorKeepsRegistrationOrderOnTie);
    }

    private static void UtilitySelectorPicksHighestScore()
    {
        var selector = new UtilityGoalSelector<Goal, float>(
            new UtilityGoal<Goal, float>(Goal.Patrol, _ => true, _ => 0.2f),
            new UtilityGoal<Goal, float>(Goal.Chase, _ => true, distance => 1f - distance));

        var selection = selector.Select(0.25f);

        Spec.Equal(Goal.Chase, selection.Goal, "未选择最高分目标");
        Spec.Equal(0.75f, selection.Score, "选择结果的分数错误");
    }

    private static void UtilitySelectorSkipsUnavailableGoal()
    {
        var selector = new UtilityGoalSelector<Goal, bool>(
            new UtilityGoal<Goal, bool>(Goal.Attack, canAttack => canAttack, _ => 1f),
            new UtilityGoal<Goal, bool>(Goal.Patrol, _ => true, _ => 0.1f));

        var selection = selector.Select(false);

        Spec.Equal(Goal.Patrol, selection.Goal, "不可用目标被错误选择");
    }

    private static void UtilitySelectorClampsConsiderationScores()
    {
        var selector = new UtilityGoalSelector<Goal, int>(
            new UtilityGoal<Goal, int>(Goal.Patrol, _ => true, _ => 2f, _ => -1f));

        var selection = selector.Select(0);

        Spec.Equal(0f, selection.Score, "考量分数未限制在零到一之间");
    }

    private static void UtilitySelectorKeepsRegistrationOrderOnTie()
    {
        var selector = new UtilityGoalSelector<Goal, int>(
            new UtilityGoal<Goal, int>(Goal.Patrol, _ => true, _ => 0.5f),
            new UtilityGoal<Goal, int>(Goal.Chase, _ => true, _ => 0.5f));

        var selection = selector.Select(0);

        Spec.Equal(Goal.Patrol, selection.Goal, "同分时未保持确定性的注册顺序");
    }
}
