using System;
using Odyssey.Gameplay.Config;

internal static class ConfigDatabaseSpecs
{
    public static void Register()
    {
        Spec.Run("config_database_rejects_duplicate_ids", ConfigDatabaseRejectsDuplicateIds);
        Spec.Run("config_database_returns_typed_record", ConfigDatabaseReturnsTypedRecord);
        Spec.Run("player_config_rejects_non_positive_speed", PlayerConfigRejectsNonPositiveSpeed);
        Spec.Run("enemy_config_rejects_attack_range_beyond_chase_range", EnemyConfigRejectsAttackRangeBeyondChaseRange);
    }

    private static void ConfigDatabaseRejectsDuplicateIds()
    {
        Spec.Throws<ArgumentException>(() => new GameConfigDatabase(
            new PlayerConfigData("player", 6f, 10f),
            new PlayerConfigData("player", 7f, 11f)), "duplicate config id was accepted");
    }

    private static void ConfigDatabaseReturnsTypedRecord()
    {
        var expected = new PlayerConfigData("player", 6f, 10f);
        var database = new GameConfigDatabase(expected);

        var actual = database.Get<PlayerConfigData>("player");

        Spec.True(ReferenceEquals(expected, actual), "typed config lookup returned the wrong record");
    }

    private static void PlayerConfigRejectsNonPositiveSpeed()
    {
        var result = GameConfigValidator.Validate(new PlayerConfigData("player", 0f, 10f));

        Spec.True(!result.IsValid, "non-positive walk speed was accepted");
    }

    private static void EnemyConfigRejectsAttackRangeBeyondChaseRange()
    {
        var result = GameConfigValidator.Validate(new EnemyConfigData("chomper", 2f, 5f));

        Spec.True(!result.IsValid, "attack range beyond chase range was accepted");
    }
}
