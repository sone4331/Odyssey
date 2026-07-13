using System;
using Odyssey.Gameplay.Config;

internal static class ConfigDatabaseSpecs
{
    public static void Register()
    {
        Spec.Run("配置数据库拒绝重复 ID", ConfigDatabaseRejectsDuplicateIds);
        Spec.Run("配置数据库返回指定类型记录", ConfigDatabaseReturnsTypedRecord);
        Spec.Run("玩家配置拒绝非正数速度", PlayerConfigRejectsNonPositiveSpeed);
        Spec.Run("敌人配置拒绝超过追击范围的攻击范围", EnemyConfigRejectsAttackRangeBeyondChaseRange);
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

    private static void EnemyConfigRejectsAttackRangeBeyondChaseRange()
    {
        var result = GameConfigValidator.Validate(new EnemyConfigData("chomper", 2f, 5f));

        Spec.True(!result.IsValid, "超过追击范围的攻击范围被错误接受");
    }
}
