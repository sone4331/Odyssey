using System;
using Odyssey.Gameplay.Config;

internal static class ConfigDatabaseSpecs
{
    public static void Register()
    {
        Spec.Run("配置数据库拒绝重复 ID", ConfigDatabaseRejectsDuplicateIds);
        Spec.Run("配置数据库返回指定类型记录", ConfigDatabaseReturnsTypedRecord);
        Spec.Run("玩家配置拒绝非正数速度", PlayerConfigRejectsNonPositiveSpeed);
        Spec.Run("玩家配置保存完整战斗参数", PlayerConfigStoresCompleteCombatValues);
        Spec.Run("玩家配置拒绝非法战斗参数", PlayerConfigRejectsInvalidCombatValues);
        Spec.Run("玩家配置拒绝非法手感参数", PlayerConfigRejectsInvalidFeelValues);
        Spec.Run("敌人配置拒绝超过追击范围的攻击范围", EnemyConfigRejectsAttackRangeBeyondChaseRange);
        Spec.Run("敌人配置保存完整战斗参数", EnemyConfigStoresCompleteCombatValues);
        Spec.Run("敌人配置拒绝非法战斗参数", EnemyConfigRejectsInvalidCombatValues);
    }

    private static void ConfigDatabaseRejectsDuplicateIds()
    {
        Spec.Throws<ArgumentException>(() => new GameConfigDatabase(
            new PlayerConfigData("player", 6f, 10f),
            new PlayerConfigData("player", 7f, 11f)), "重复配置 ID 被错误接受");
    }

    private static void ConfigDatabaseReturnsTypedRecord()
    {
        var expected = new PlayerConfigData("player", 6f, 10f);
        var database = new GameConfigDatabase(expected);

        var actual = database.Get<PlayerConfigData>("player");

        Spec.True(ReferenceEquals(expected, actual), "类型化配置查询返回了错误记录");
    }

    private static void PlayerConfigRejectsNonPositiveSpeed()
    {
        var result = GameConfigValidator.Validate(new PlayerConfigData("player", 0f, 10f));

        Spec.True(!result.IsValid, "非正数移动速度被错误接受");
    }

    private static void PlayerConfigStoresCompleteCombatValues()
    {
        var config = new PlayerConfigData(
            "player", 6f, 10f,
            gravity: -18f,
            dashForce: 22f,
            dashDuration: 0.25f,
            dashCooldown: 0.6f,
            jumpHeight: 3.5f,
            chargeJumpHeight: 5.5f,
            minChargeTime: 0.4f,
            airJumpHeight: 2.5f,
            wallSlideSpeed: -2.5f,
            wallJumpUpForce: 12f,
            wallJumpSideForce: 9f,
            attackDamage: 2,
            attackRange: 2.2f,
            attackCooldown: 0.45f,
            maxHealth: 6,
            groundAcceleration: 20f,
            groundDeceleration: 25f,
            minTurnSpeed: 400f,
            maxTurnSpeed: 1200f,
            attackAdvanceSpeed: 1.5f);

        Spec.Equal(-18f, config.Gravity, "重力参数未被保存");
        Spec.Equal(22f, config.DashForce, "冲刺力度参数未被保存");
        Spec.Equal(12f, config.WallJumpUpForce, "墙跳向上力度参数未被保存");
        Spec.Equal(9f, config.WallJumpSideForce, "墙跳侧向力度参数未被保存");
        Spec.Equal(2, config.AttackDamage, "攻击伤害参数未被保存");
        Spec.Equal(6, config.MaxHealth, "最大生命参数未被保存");
        Spec.Equal(20f, config.GroundAcceleration, "地面加速度参数未被保存");
        Spec.Equal(1200f, config.MaxTurnSpeed, "最大转向速度参数未被保存");
        Spec.Equal(1.5f, config.AttackAdvanceSpeed, "攻击前移速度参数未被保存");
    }

    private static void PlayerConfigRejectsInvalidCombatValues()
    {
        var config = new PlayerConfigData(
            "player", 6f, 10f,
            attackDamage: 0,
            attackRange: 0f,
            maxHealth: 0);

        var result = GameConfigValidator.Validate(config);

        Spec.True(!result.IsValid, "非法战斗参数被错误接受");
    }

    private static void PlayerConfigRejectsInvalidFeelValues()
    {
        var config = new PlayerConfigData(
            "player", 4f, 8f,
            groundAcceleration: 0f,
            groundDeceleration: -1f,
            minTurnSpeed: 800f,
            maxTurnSpeed: 400f,
            attackAdvanceSpeed: -1f);

        var result = GameConfigValidator.Validate(config);

        Spec.True(!result.IsValid, "非法移动手感参数被错误接受");
    }

    private static void EnemyConfigRejectsAttackRangeBeyondChaseRange()
    {
        var result = GameConfigValidator.Validate(new EnemyConfigData("chomper", 2f, 5f));

        Spec.True(!result.IsValid, "超过追击范围的攻击范围被错误接受");
    }

    private static void EnemyConfigStoresCompleteCombatValues()
    {
        var config = new EnemyConfigData(
            "chomper", 10f, 2f,
            maxHealth: 4,
            attackDamage: 2,
            attackCooldown: 1.5f);

        Spec.Equal(4, config.MaxHealth, "敌人最大生命没有被保存");
        Spec.Equal(2, config.AttackDamage, "敌人攻击伤害没有被保存");
        Spec.Equal(1.5f, config.AttackCooldown, "敌人攻击冷却没有被保存");
    }

    private static void EnemyConfigRejectsInvalidCombatValues()
    {
        var config = new EnemyConfigData(
            "chomper", 10f, 2f,
            maxHealth: 0,
            attackDamage: 0,
            attackCooldown: -1f);

        var result = GameConfigValidator.Validate(config);

        Spec.True(!result.IsValid, "敌人非法战斗参数被错误接受");
    }
}
