using System;
using System.Collections.Generic;

namespace Odyssey.Gameplay.Save
{
    /// <summary>
    /// 标识带有数据版本的存档模型，为顺序迁移提供稳定版本入口。
    /// </summary>
    public interface IVersionedSave
    {
        int Version { get; set; }
    }

    /// <summary>
    /// 定义单个版本跨度的存档迁移策略，使历史兼容规则可以按版本独立扩展和测试。
    /// </summary>
    public interface ISaveMigration<in TSave> where TSave : IVersionedSave
    {
        int FromVersion { get; }
        void Apply(TSave save);
    }

    /// <summary>
    /// 定义存档持久化端口，隔离 Gameplay 调用方与文件系统、云存档或测试替身。
    /// </summary>
    public interface ISaveService<TSave>
    {
        void Save(TSave data);
        bool TryLoad(out TSave data);
    }

    /// <summary>
    /// 按 FromVersion 连续应用迁移策略，直到存档达到当前版本。
    /// 使用 Pipeline 与 Strategy 模式防止迁移逻辑堆积在 SaveManager，并对缺失迁移链快速失败。
    /// </summary>
    public sealed class SaveMigrationPipeline<TSave> where TSave : IVersionedSave
    {
        private readonly int _currentVersion;
        private readonly Dictionary<int, ISaveMigration<TSave>> _migrations;

        public SaveMigrationPipeline(int currentVersion, params ISaveMigration<TSave>[] migrations)
        {
            if (currentVersion < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(currentVersion));
            }

            _currentVersion = currentVersion;
            _migrations = new Dictionary<int, ISaveMigration<TSave>>();
            foreach (var migration in migrations ?? Array.Empty<ISaveMigration<TSave>>())
            {
                if (_migrations.ContainsKey(migration.FromVersion))
                {
                    throw new ArgumentException($"Multiple migrations start at version {migration.FromVersion}.", nameof(migrations));
                }

                _migrations.Add(migration.FromVersion, migration);
            }
        }

        public void Migrate(TSave save)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            if (save.Version > _currentVersion)
            {
                throw new InvalidOperationException($"Save version {save.Version} is newer than supported version {_currentVersion}.");
            }

            while (save.Version < _currentVersion)
            {
                var fromVersion = save.Version;
                if (!_migrations.TryGetValue(fromVersion, out var migration))
                {
                    throw new InvalidOperationException($"No save migration exists from version {fromVersion}.");
                }

                migration.Apply(save);
                if (save.Version != fromVersion + 1)
                {
                    throw new InvalidOperationException($"Migration from version {fromVersion} must advance exactly one version.");
                }
            }
        }
    }
}
