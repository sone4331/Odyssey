using System;
using Odyssey.Gameplay.Save;

internal static class SaveMigrationSpecs
{
    public static void Register()
    {
        Spec.Run("save_migrations_run_sequentially", SaveMigrationsRunSequentially);
        Spec.Run("future_save_version_is_rejected", FutureSaveVersionIsRejected);
        Spec.Run("missing_save_migration_is_rejected", MissingSaveMigrationIsRejected);
    }

    private static void SaveMigrationsRunSequentially()
    {
        var data = new TestSave { Version = 0, Value = 1 };
        var pipeline = new SaveMigrationPipeline<TestSave>(2,
            new TestMigration(0, save => { save.Value += 2; save.Version = 1; }),
            new TestMigration(1, save => { save.Value *= 3; save.Version = 2; }));

        pipeline.Migrate(data);

        Spec.Equal(2, data.Version, "save did not reach current version");
        Spec.Equal(9, data.Value, "migrations did not execute sequentially");
    }

    private static void FutureSaveVersionIsRejected()
    {
        var pipeline = new SaveMigrationPipeline<TestSave>(2);
        var data = new TestSave { Version = 3 };

        Spec.Throws<InvalidOperationException>(() => pipeline.Migrate(data), "future save version was accepted");
    }

    private static void MissingSaveMigrationIsRejected()
    {
        var pipeline = new SaveMigrationPipeline<TestSave>(2, new TestMigration(0, save => save.Version = 1));
        var data = new TestSave { Version = 0 };

        Spec.Throws<InvalidOperationException>(() => pipeline.Migrate(data), "missing migration step was accepted");
    }

    private sealed class TestSave : IVersionedSave
    {
        public int Version { get; set; }
        public int Value { get; set; }
    }

    private sealed class TestMigration : ISaveMigration<TestSave>
    {
        private readonly Action<TestSave> _apply;

        public TestMigration(int fromVersion, Action<TestSave> apply)
        {
            FromVersion = fromVersion;
            _apply = apply;
        }

        public int FromVersion { get; }
        public void Apply(TestSave save) => _apply(save);
    }
}
