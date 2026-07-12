using System;
using System.Collections.Generic;

namespace Odyssey.Gameplay.AI
{
    public sealed class UtilityGoal<TGoal, TContext>
    {
        public UtilityGoal(
            TGoal goal,
            Func<TContext, bool> isAvailable,
            params Func<TContext, float>[] considerations)
        {
            Goal = goal;
            IsAvailable = isAvailable ?? throw new ArgumentNullException(nameof(isAvailable));
            Considerations = considerations ?? throw new ArgumentNullException(nameof(considerations));
        }

        public TGoal Goal { get; }
        public Func<TContext, bool> IsAvailable { get; }
        public IReadOnlyList<Func<TContext, float>> Considerations { get; }
    }

    public readonly struct UtilitySelection<TGoal>
    {
        public UtilitySelection(TGoal goal, float score)
        {
            Goal = goal;
            Score = score;
        }

        public TGoal Goal { get; }
        public float Score { get; }
    }

    public sealed class UtilityGoalSelector<TGoal, TContext>
    {
        private readonly IReadOnlyList<UtilityGoal<TGoal, TContext>> _goals;

        public UtilityGoalSelector(params UtilityGoal<TGoal, TContext>[] goals)
        {
            _goals = goals ?? throw new ArgumentNullException(nameof(goals));
        }

        public UtilitySelection<TGoal> Select(TContext context)
        {
            var hasSelection = false;
            var selectedGoal = default(TGoal);
            var selectedScore = float.MinValue;

            foreach (var goal in _goals)
            {
                if (!goal.IsAvailable(context))
                {
                    continue;
                }

                var score = 1f;
                foreach (var consideration in goal.Considerations)
                {
                    score *= Clamp01(consideration(context));
                }

                if (!hasSelection || score > selectedScore)
                {
                    hasSelection = true;
                    selectedGoal = goal.Goal;
                    selectedScore = score;
                }
            }

            return hasSelection
                ? new UtilitySelection<TGoal>(selectedGoal, selectedScore)
                : default;
        }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value) || value <= 0f)
            {
                return 0f;
            }

            if (value >= 1f)
            {
                return 1f;
            }

            return value;
        }
    }
}
