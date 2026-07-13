using System;
using System.Collections.Generic;

namespace Odyssey.Gameplay.AI
{
    /// <summary>
    /// 声明一个可用性条件和若干评分考量组成的 AI 目标。
    /// 使用 Strategy 组合评分函数，使新目标和新考量可以独立扩展，而不修改选择器流程。
    /// </summary>
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

    /// <summary>
    /// 封装 Utility 选择结果及最终分数，供行为层执行并供调试界面解释决策依据。
    /// </summary>
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

    /// <summary>
    /// 根据只读上下文选择当前最高分的可用目标，是 AI 决策层的纯 C# 领域服务。
    /// 采用 Utility AI 与 Strategy 模式分离“如何评分”和“如何执行”，使决策可测试且不依赖 Unity 场景。
    /// </summary>
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
