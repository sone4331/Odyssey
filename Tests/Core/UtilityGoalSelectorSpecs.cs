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
        Spec.Run("utility_selector_picks_highest_score", UtilitySelectorPicksHighestScore);
        Spec.Run("utility_selector_skips_unavailable_goal", UtilitySelectorSkipsUnavailableGoal);
        Spec.Run("utility_selector_clamps_consideration_scores", UtilitySelectorClampsConsiderationScores);
        Spec.Run("utility_selector_keeps_registration_order_on_tie", UtilitySelectorKeepsRegistrationOrderOnTie);
    }

    private static void UtilitySelectorPicksHighestScore()
    {
        var selector = new UtilityGoalSelector<Goal, float>(
            new UtilityGoal<Goal, float>(Goal.Patrol, _ => true, _ => 0.2f),
            new UtilityGoal<Goal, float>(Goal.Chase, _ => true, distance => 1f - distance));

        var selection = selector.Select(0.25f);

        Spec.Equal(Goal.Chase, selection.Goal, "highest scoring goal was not selected");
        Spec.Equal(0.75f, selection.Score, "selected score was incorrect");
    }

    private static void UtilitySelectorSkipsUnavailableGoal()
    {
        var selector = new UtilityGoalSelector<Goal, bool>(
            new UtilityGoal<Goal, bool>(Goal.Attack, canAttack => canAttack, _ => 1f),
            new UtilityGoal<Goal, bool>(Goal.Patrol, _ => true, _ => 0.1f));

        var selection = selector.Select(false);

        Spec.Equal(Goal.Patrol, selection.Goal, "unavailable goal was selected");
    }

    private static void UtilitySelectorClampsConsiderationScores()
    {
        var selector = new UtilityGoalSelector<Goal, int>(
            new UtilityGoal<Goal, int>(Goal.Patrol, _ => true, _ => 2f, _ => -1f));

        var selection = selector.Select(0);

        Spec.Equal(0f, selection.Score, "consideration scores were not clamped to zero-to-one");
    }

    private static void UtilitySelectorKeepsRegistrationOrderOnTie()
    {
        var selector = new UtilityGoalSelector<Goal, int>(
            new UtilityGoal<Goal, int>(Goal.Patrol, _ => true, _ => 0.5f),
            new UtilityGoal<Goal, int>(Goal.Chase, _ => true, _ => 0.5f));

        var selection = selector.Select(0);

        Spec.Equal(Goal.Patrol, selection.Goal, "tie did not preserve deterministic registration order");
    }
}
