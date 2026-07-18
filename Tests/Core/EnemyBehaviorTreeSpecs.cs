using Odyssey.Gameplay.AI;
using Odyssey.Gameplay.Config;

/// <summary>
/// 验证轻量行为树的抢占语义与怪物业务分支，确保核心 AI 可以完全脱离 Unity 场景回归。
/// </summary>
internal static class EnemyBehaviorTreeSpecs
{
    public static void Register()
    {
        Spec.Run("响应式选择器会中断低优先级运行分支", ReactiveSelectorInterruptsRunningBranch);
        Spec.Run("没有目标且存在路线时选择巡逻", MissingTargetSelectsPatrol);
        Spec.Run("感知范围内且距离较远时选择追击", SensedTargetSelectsChase);
        Spec.Run("目标进入范围且冷却完成时选择攻击", ReadyTargetSelectsAttack);
        Spec.Run("攻击冷却期间在范围内保持待机", CooldownTargetSelectsIdle);
        Spec.Run("远程敌人距离过近时选择后撤", RangedEnemyTooCloseSelectsRetreat);
        Spec.Run("受击事实会抢占正在执行的追击", HitReactionInterruptsChase);
        Spec.Run("目标丢失后恢复巡逻", TargetLossReturnsToPatrol);
    }

    private static void ReactiveSelectorInterruptsRunningBranch()
    {
        var context = new TestContext();
        var patrolAbortCount = 0;
        var runner = new BehaviorTreeRunner<TestContext>(
            new ReactiveSelector<TestContext>(
                "根节点",
                new Sequence<TestContext>(
                    "受击分支",
                    new ConditionNode<TestContext>("正在受击", value => value.IsHit),
                    new ActionNode<TestContext>("播放受击", _ => BehaviorStatus.Running)),
                new ActionNode<TestContext>(
                    "巡逻",
                    _ => BehaviorStatus.Running,
                    _ => patrolAbortCount++)));

        runner.Tick(context);
        context.IsHit = true;
        runner.Tick(context);

        Spec.Equal(1, patrolAbortCount, "高优先级分支没有中断巡逻");
        Spec.True(runner.CurrentPath.Contains("播放受击"), "当前运行路径没有反映受击分支");
    }

    private static void MissingTargetSelectsPatrol()
    {
        var fixture = CreateFixture();
        Update(fixture.Blackboard, hasTarget: false, distance: 20f);

        fixture.Model.Tick(0f, 2f, 0.02f);

        Spec.Equal(EnemyGoal.Patrol, fixture.Blackboard.CurrentGoal, "无目标怪物没有巡逻");
    }

    private static void SensedTargetSelectsChase()
    {
        var fixture = CreateFixture();
        Update(fixture.Blackboard, hasTarget: true, distance: 6f);

        fixture.Model.Tick(0f, 2f, 0.02f);

        Spec.Equal(EnemyGoal.Chase, fixture.Blackboard.CurrentGoal, "感知到远距离目标后没有追击");
    }

    private static void ReadyTargetSelectsAttack()
    {
        var fixture = CreateFixture();
        Update(fixture.Blackboard, hasTarget: true, distance: 1.5f);

        fixture.Model.Tick(0f, 2f, 0.02f);

        Spec.Equal(EnemyGoal.Attack, fixture.Blackboard.CurrentGoal, "可攻击目标没有进入攻击分支");
    }

    private static void CooldownTargetSelectsIdle()
    {
        var fixture = CreateFixture();
        Update(fixture.Blackboard, hasTarget: true, distance: 1.5f, attackReady: false);

        fixture.Model.Tick(0f, 2f, 0.02f);

        Spec.Equal(EnemyGoal.Idle, fixture.Blackboard.CurrentGoal, "攻击冷却期间近距离行为不稳定");
    }

    private static void RangedEnemyTooCloseSelectsRetreat()
    {
        var fixture = CreateFixture();
        Update(
            fixture.Blackboard,
            hasTarget: true,
            distance: 2f,
            attackMode: EnemyAttackMode.Projectile,
            minimumAttackRange: 3.5f,
            attackRange: 8f);

        fixture.Model.Tick(0f, 2f, 0.02f);

        Spec.Equal(EnemyGoal.Retreat, fixture.Blackboard.CurrentGoal, "远程敌人没有主动拉开危险距离");
    }

    private static void HitReactionInterruptsChase()
    {
        var fixture = CreateFixture();
        Update(fixture.Blackboard, hasTarget: true, distance: 6f);
        fixture.Model.Tick(0f, 2f, 0.02f);

        Update(fixture.Blackboard, hasTarget: true, distance: 6f, isHitReacting: true);
        fixture.Model.Tick(0.1f, 2f, 0.02f);

        Spec.Equal(EnemyGoal.Hit, fixture.Blackboard.CurrentGoal, "受击没有抢占追击分支");
        Spec.Equal(EnemyGoal.Chase, fixture.Actions.LastAbortedGoal, "追击动作没有收到中断清理");
    }

    private static void TargetLossReturnsToPatrol()
    {
        var fixture = CreateFixture();
        Update(fixture.Blackboard, hasTarget: true, distance: 6f);
        fixture.Model.Tick(0f, 2f, 0.02f);

        Update(fixture.Blackboard, hasTarget: false, distance: 15f);
        fixture.Model.Tick(0.1f, 2f, 0.02f);

        Spec.Equal(EnemyGoal.Patrol, fixture.Blackboard.CurrentGoal, "目标丢失后没有恢复巡逻");
    }

    private static Fixture CreateFixture()
    {
        var blackboard = new EnemyBlackboard();
        var actions = new RecordingActions();
        return new Fixture(blackboard, actions, new EnemyBehaviorModel(blackboard, actions));
    }

    private static void Update(
        EnemyBlackboard blackboard,
        bool hasTarget,
        float distance,
        bool attackReady = true,
        bool isHitReacting = false,
        EnemyAttackMode attackMode = EnemyAttackMode.Melee,
        float minimumAttackRange = 0f,
        float attackRange = 2f)
    {
        blackboard.UpdatePerception(
            hasTarget,
            distance,
            10f,
            12.5f,
            attackRange,
            minimumAttackRange,
            1f,
            attackReady,
            attackInProgress: false,
            isHitReacting,
            isDead: false,
            hasPatrolRoute: true,
            attackMode);
    }

    private sealed class TestContext
    {
        public bool IsHit { get; set; }
    }

    private sealed class RecordingActions : IEnemyBehaviorActions
    {
        public EnemyGoal LastAbortedGoal { get; private set; } = EnemyGoal.Idle;

        public BehaviorStatus Tick(EnemyGoal goal, float currentTime, float attackCooldown, float deltaTime)
        {
            return BehaviorStatus.Running;
        }

        public void Abort(EnemyGoal goal)
        {
            LastAbortedGoal = goal;
        }
    }

    private sealed class Fixture
    {
        public Fixture(EnemyBlackboard blackboard, RecordingActions actions, EnemyBehaviorModel model)
        {
            Blackboard = blackboard;
            Actions = actions;
            Model = model;
        }

        public EnemyBlackboard Blackboard { get; }
        public RecordingActions Actions { get; }
        public EnemyBehaviorModel Model { get; }
    }
}
