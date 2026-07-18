namespace Odyssey.Gameplay.AI
{
    /// <summary>
    /// 表示怪物当前希望执行的高层目标；受击和死亡属于事件打断，不参与常规 Utility 竞争。
    /// </summary>
    public enum EnemyGoal
    {
        Idle,
        Patrol,
        Chase,
        Attack,
        Retreat
    }

    /// <summary>
    /// 保存一次 AI 决策所需的只读事实，是 Perception 写入 Blackboard 后提供给决策层的不可变快照。
    /// </summary>
    public readonly struct EnemyDecisionContext
    {
        public EnemyDecisionContext(
            bool hasTarget,
            float distanceToTarget,
            float chaseRange,
            float attackRange,
            float healthRatio,
            bool attackReady,
            bool hasPatrolRoute = false)
            : this(
                hasTarget,
                distanceToTarget,
                chaseRange,
                attackRange,
                0f,
                healthRatio,
                attackReady,
                hasPatrolRoute)
        {
        }

        public EnemyDecisionContext(
            bool hasTarget,
            float distanceToTarget,
            float chaseRange,
            float attackRange,
            float minimumAttackRange,
            float healthRatio,
            bool attackReady,
            bool hasPatrolRoute = false)
        {
            HasTarget = hasTarget;
            DistanceToTarget = distanceToTarget;
            ChaseRange = chaseRange;
            AttackRange = attackRange;
            MinimumAttackRange = minimumAttackRange;
            HealthRatio = healthRatio;
            AttackReady = attackReady;
            HasPatrolRoute = hasPatrolRoute;
        }

        public bool HasTarget { get; }
        public float DistanceToTarget { get; }
        public float ChaseRange { get; }
        public float AttackRange { get; }
        public float MinimumAttackRange { get; }
        public float HealthRatio { get; }
        public bool AttackReady { get; }
        public bool HasPatrolRoute { get; }
    }

    /// <summary>
    /// 表示 Utility 决策的目标和最终分数，供动作层执行并供调试视图解释。
    /// </summary>
    public readonly struct EnemyDecision
    {
        public EnemyDecision(EnemyGoal goal, float score)
        {
            Goal = goal;
            Score = score;
        }

        public EnemyGoal Goal { get; }
        public float Score { get; }
    }

    /// <summary>
    /// 保存感知事实与最近一次决策结果，是单个怪物实例独享的轻量黑板。
    /// 采用 Blackboard 模式让感知、决策、执行和调试视图通过同一份事实协作，而不互相持有具体实现。
    /// </summary>
    public sealed class EnemyBlackboard
    {
        public bool HasTarget { get; private set; }
        public float DistanceToTarget { get; private set; } = float.MaxValue;
        public float ChaseRange { get; private set; }
        public float AttackRange { get; private set; }
        public float MinimumAttackRange { get; private set; }
        public float HealthRatio { get; private set; } = 1f;
        public bool AttackReady { get; private set; }
        public bool HasPatrolRoute { get; private set; }
        public EnemyGoal CurrentGoal { get; private set; } = EnemyGoal.Idle;
        public float CurrentScore { get; private set; }

        public EnemyDecisionContext Context => new EnemyDecisionContext(
            HasTarget,
            DistanceToTarget,
            ChaseRange,
            AttackRange,
            MinimumAttackRange,
            HealthRatio,
            AttackReady,
            HasPatrolRoute);

        /// <summary>
        /// 一次性覆盖本帧全部感知事实，避免消费者读取到新旧帧混合的黑板状态。
        /// </summary>
        public void UpdatePerception(
            bool hasTarget,
            float distanceToTarget,
            float chaseRange,
            float attackRange,
            float minimumAttackRange,
            float healthRatio,
            bool attackReady,
            bool hasPatrolRoute)
        {
            HasTarget = hasTarget;
            DistanceToTarget = distanceToTarget;
            ChaseRange = chaseRange;
            AttackRange = attackRange;
            MinimumAttackRange = minimumAttackRange;
            HealthRatio = healthRatio;
            AttackReady = attackReady;
            HasPatrolRoute = hasPatrolRoute;
        }

        public void CommitDecision(EnemyDecision decision)
        {
            CurrentGoal = decision.Goal;
            CurrentScore = decision.Score;
        }
    }

    /// <summary>
    /// 根据巡逻路线、目标距离、攻击冷却和生命比例选择 Patrol、Chase、Attack 或 Retreat。
    /// 采用 Utility AI 与 Strategy 组合，不构建通用行为树框架；目标数量只覆盖当前两组遭遇需要的行为闭环。
    /// </summary>
    public sealed class EnemyDecisionModel
    {
        // 当前演示敌人默认只有 3 点生命；阈值略高于三分之一，保证剩余 1 点生命时撤退目标真实可达。
        private const float RetreatHealthThreshold = 0.34f;
        private readonly UtilityGoalSelector<EnemyGoal, EnemyDecisionContext> _selector;

        public EnemyDecisionModel()
        {
            _selector = new UtilityGoalSelector<EnemyGoal, EnemyDecisionContext>(
                new UtilityGoal<EnemyGoal, EnemyDecisionContext>(
                    EnemyGoal.Attack,
                    context => context.HasTarget &&
                               context.HealthRatio > RetreatHealthThreshold &&
                               context.AttackReady &&
                               context.DistanceToTarget >= context.MinimumAttackRange &&
                               context.DistanceToTarget <= context.AttackRange,
                    _ => 1f),
                new UtilityGoal<EnemyGoal, EnemyDecisionContext>(
                    EnemyGoal.Retreat,
                    context => context.HasTarget &&
                               (context.HealthRatio <= RetreatHealthThreshold ||
                                context.DistanceToTarget < context.MinimumAttackRange) &&
                               context.DistanceToTarget <= context.ChaseRange,
                    context => 0.75f + (1f - context.HealthRatio) * 0.25f,
                    context => 0.8f + Proximity(context) * 0.2f),
                new UtilityGoal<EnemyGoal, EnemyDecisionContext>(
                    EnemyGoal.Chase,
                    context => context.HasTarget &&
                               context.DistanceToTarget > context.AttackRange &&
                               context.DistanceToTarget <= context.ChaseRange,
                    context => 0.4f + Proximity(context) * 0.5f),
                new UtilityGoal<EnemyGoal, EnemyDecisionContext>(
                    EnemyGoal.Patrol,
                    context => !context.HasTarget && context.HasPatrolRoute,
                    _ => 0.2f),
                new UtilityGoal<EnemyGoal, EnemyDecisionContext>(
                    EnemyGoal.Idle,
                    _ => true,
                    _ => 0.1f));
        }

        public EnemyDecision Decide(EnemyDecisionContext context)
        {
            var selection = _selector.Select(context);
            return new EnemyDecision(selection.Goal, selection.Score);
        }

        private static float Proximity(EnemyDecisionContext context)
        {
            if (context.ChaseRange <= 0f)
            {
                return 0f;
            }

            return 1f - context.DistanceToTarget / context.ChaseRange;
        }
    }
}
