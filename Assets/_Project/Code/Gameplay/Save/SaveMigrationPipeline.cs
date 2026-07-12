using System;
using System.Collections.Generic;

namespace Odyssey.Gameplay.Save
{
    public interface IVersionedSave
    {
        int Version { get; set; }
    }

    public interface ISaveMigration<in TSave> where TSave : IVersionedSave
    {
        int FromVersion { get; }
        void Apply(TSave save);
    }

    public interface ISaveService<TSave>
    {
        void Save(TSave data);
        bool TryLoad(out TSave data);
    }

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
