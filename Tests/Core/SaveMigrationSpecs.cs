using System;
using Odyssey.Gameplay.Save;

internal static class SaveMigrationSpecs
{
    public static void Register()
    {
        Spec.Run("存档迁移按顺序执行", SaveMigrationsRunSequentially);
        Spec.Run("未来版本存档被拒绝", FutureSaveVersionIsRejected);
        Spec.Run("缺失存档迁移步骤时被拒绝", MissingSaveMigrationIsRejected);
    }

    private static void SaveMigrationsRunSequentially()
    {
        var data = new TestSave { Version = 0, Value = 1 };
        var pipeline = new SaveMigrationPipeline<TestSave>(2,
            new TestMigration(0, save => { save.Value += 2; save.Version = 1; }),
            new TestMigration(1, save => { save.Value *= 3; save.Version = 2; }));

        pipeline.Migrate(data);

        Spec.Equal(2, data.Version, "存档未迁移到当前版本");
        Spec.Equal(9, data.Value, "迁移未按顺序执行");
    }

    private static void FutureSaveVersionIsRejected()
    {
        var pipeline = new SaveMigrationPipeline<TestSave>(2);
        var data = new TestSave { Version = 3 };

        Spec.Throws<InvalidOperationException>(() => pipeline.Migrate(data), "未来版本存档被错误接受");
    }

    private static void MissingSaveMigrationIsRejected()
    {
        var pipeline = new SaveMigrationPipeline<TestSave>(2, new TestMigration(0, save => save.Version = 1));
        var data = new TestSave { Version = 0 };

        Spec.Throws<InvalidOperationException>(() => pipeline.Migrate(data), "缺失迁移步骤时未被拒绝");
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
