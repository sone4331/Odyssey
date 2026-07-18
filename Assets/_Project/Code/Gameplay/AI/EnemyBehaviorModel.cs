using Odyssey.Gameplay.Config;

namespace Odyssey.Gameplay.AI
{
    /// <summary>
    /// 表示 Inspector 和测试可观察的怪物高层行为，不包含 Animator 或 NavMesh 的实现细节。
    /// </summary>
    public enum EnemyGoal
    {
        Idle,
        Patrol,
        Chase,
        Attack,
        Retreat,
        Hit,
        Dead
    }

    /// <summary>
    /// 定义行为树可以调用的动作端口。Gameplay 只依赖该抽象，Unity 层负责实现导航、动画和伤害时序。
    /// 采用端口与适配器模式，使行为选择可以在 EditMode 中脱离场景独立测试。
    /// </summary>
    public interface IEnemyBehaviorActions
    {
        BehaviorStatus Tick(EnemyGoal goal, float currentTime, float attackCooldown, float deltaTime);
        void Abort(EnemyGoal goal);
    }

    /// <summary>
    /// 保存单个怪物本帧的完整事实与最近行为结果。
    /// 采用 Blackboard 模式隔离感知、决策和执行，所有字段一次性更新，避免读取到跨帧混合状态。
    /// </summary>
    public sealed class EnemyBlackboard
    {
        public bool HasTarget { get; private set; }
        public float DistanceToTarget { get; private set; } = float.MaxValue;
        public float DetectionRange { get; private set; }
        public float ForgetRange { get; private set; }
        public float AttackRange { get; private set; }
        public float MinimumAttackRange { get; private set; }
        public float HealthRatio { get; private set; } = 1f;
        public bool AttackReady { get; private set; }
        public bool AttackInProgress { get; private set; }
        public bool IsHitReacting { get; private set; }
        public bool IsDead { get; private set; }
        public bool HasPatrolRoute { get; private set; }
        public EnemyAttackMode AttackMode { get; private set; }
        public EnemyGoal CurrentGoal { get; private set; } = EnemyGoal.Idle;
        public string CurrentBehaviorPath { get; private set; } = "尚未运行";

        /// <summary>
        /// 一次提交本帧完整感知快照；行为树随后只读取该快照，不直接访问 Unity 场景。
        /// </summary>
        public void UpdatePerception(
            bool hasTarget,
            float distanceToTarget,
            float detectionRange,
            float forgetRange,
            float attackRange,
            float minimumAttackRange,
            float healthRatio,
            bool attackReady,
            bool attackInProgress,
            bool isHitReacting,
            bool isDead,
            bool hasPatrolRoute,
            EnemyAttackMode attackMode)
        {
            HasTarget = hasTarget;
            DistanceToTarget = distanceToTarget;
            DetectionRange = detectionRange;
            ForgetRange = forgetRange;
            AttackRange = attackRange;
            MinimumAttackRange = minimumAttackRange;
            HealthRatio = healthRatio;
            AttackReady = attackReady;
            AttackInProgress = attackInProgress;
            IsHitReacting = isHitReacting;
            IsDead = isDead;
            HasPatrolRoute = hasPatrolRoute;
            AttackMode = attackMode;
        }

        public void CommitBehavior(EnemyGoal goal, string path)
        {
            CurrentGoal = goal;
            CurrentBehaviorPath = string.IsNullOrWhiteSpace(path) ? "尚未运行" : path;
        }
    }

    /// <summary>
    /// 汇集行为树每帧需要的黑板、动作端口与时间数据，是纯 C# 决策和 Unity 适配层之间的调用上下文。
    /// </summary>
    public sealed class EnemyBehaviorContext
    {
        public EnemyBehaviorContext(EnemyBlackboard blackboard, IEnemyBehaviorActions actions)
        {
            Blackboard = blackboard;
            Actions = actions;
        }

        public EnemyBlackboard Blackboard { get; }
        public IEnemyBehaviorActions Actions { get; }
        public float CurrentTime { get; set; }
        public float AttackCooldown { get; set; }
        public float DeltaTime { get; set; }
    }

    /// <summary>
    /// 组装当前作品切片所需的固定轻量行为树：死亡、受击、远程后撤、攻击、追击、巡逻和待机。
    /// 使用代码组树让结构可直接阅读和断点调试，同时避免为六只固定敌人制作资产化节点编辑器。
    /// </summary>
    public sealed class EnemyBehaviorModel
    {
        private readonly EnemyBlackboard _blackboard;
        private readonly EnemyBehaviorContext _context;
        private readonly BehaviorTreeRunner<EnemyBehaviorContext> _runner;
        private EnemyGoal _pendingGoal = EnemyGoal.Idle;

        public EnemyBehaviorModel(EnemyBlackboard blackboard, IEnemyBehaviorActions actions)
        {
            _blackboard = blackboard;
            _context = new EnemyBehaviorContext(blackboard, actions);
            _runner = new BehaviorTreeRunner<EnemyBehaviorContext>(BuildTree());
        }

        public string CurrentPath => _runner.CurrentPath;

        public void Tick(float currentTime, float attackCooldown, float deltaTime)
        {
            _context.CurrentTime = currentTime;
            _context.AttackCooldown = attackCooldown;
            _context.DeltaTime = deltaTime;
            _runner.Tick(_context);
            _blackboard.CommitBehavior(_pendingGoal, _runner.CurrentPath);
        }

        private BehaviorNode<EnemyBehaviorContext> BuildTree()
        {
            return new ReactiveSelector<EnemyBehaviorContext>(
                "怪物行为树",
                Branch("死亡", context => context.Blackboard.IsDead, EnemyGoal.Dead),
                Branch("受击打断", context => context.Blackboard.IsHitReacting, EnemyGoal.Hit),
                new Sequence<EnemyBehaviorContext>(
                    "交战",
                    Condition("已感知玩家", context => context.Blackboard.HasTarget),
                    new ReactiveSelector<EnemyBehaviorContext>(
                        "选择作战动作",
                        new Sequence<EnemyBehaviorContext>(
                            "保持远程安全距离",
                            Condition("玩家距离过近", ShouldRetreat),
                            Action("后撤", EnemyGoal.Retreat)),
                        new Sequence<EnemyBehaviorContext>(
                            "满足攻击条件",
                            Condition("攻击可执行", CanAttack),
                            Action("攻击", EnemyGoal.Attack)),
                        new Sequence<EnemyBehaviorContext>(
                            "接近目标",
                            Condition("尚未进入攻击距离", NeedsChase),
                            Action("追击", EnemyGoal.Chase)),
                        Action("等待攻击冷却", EnemyGoal.Idle))),
                new Sequence<EnemyBehaviorContext>(
                    "日常巡逻",
                    Condition("巡逻路线有效", context => context.Blackboard.HasPatrolRoute),
                    Action("巡逻", EnemyGoal.Patrol)),
                Action("原地待机", EnemyGoal.Idle));
        }

        private Sequence<EnemyBehaviorContext> Branch(
            string name,
            System.Func<EnemyBehaviorContext, bool> condition,
            EnemyGoal goal)
        {
            return new Sequence<EnemyBehaviorContext>(name, Condition(name + "条件", condition), Action(name, goal));
        }

        private static ConditionNode<EnemyBehaviorContext> Condition(
            string name,
            System.Func<EnemyBehaviorContext, bool> condition)
        {
            return new ConditionNode<EnemyBehaviorContext>(name, condition);
        }

        private ActionNode<EnemyBehaviorContext> Action(string name, EnemyGoal goal)
        {
            return new ActionNode<EnemyBehaviorContext>(
                name,
                context =>
                {
                    _pendingGoal = goal;
                    return context.Actions.Tick(
                        goal,
                        context.CurrentTime,
                        context.AttackCooldown,
                        context.DeltaTime);
                },
                context => context.Actions.Abort(goal));
        }

        private static bool ShouldRetreat(EnemyBehaviorContext context)
        {
            var blackboard = context.Blackboard;
            return blackboard.AttackMode == EnemyAttackMode.Projectile &&
                   blackboard.DistanceToTarget < blackboard.MinimumAttackRange;
        }

        private static bool CanAttack(EnemyBehaviorContext context)
        {
            var blackboard = context.Blackboard;
            if (blackboard.AttackInProgress)
            {
                return true;
            }

            return blackboard.AttackReady &&
                   blackboard.DistanceToTarget >= blackboard.MinimumAttackRange &&
                   blackboard.DistanceToTarget <= blackboard.AttackRange;
        }

        private static bool NeedsChase(EnemyBehaviorContext context)
        {
            return context.Blackboard.DistanceToTarget > context.Blackboard.AttackRange;
        }
    }
}
