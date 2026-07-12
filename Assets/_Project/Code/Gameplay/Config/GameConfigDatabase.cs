using System;
using System.Collections.Generic;

namespace Odyssey.Gameplay.Config
{
    public interface IConfigRecord
    {
        string Id { get; }
    }

    public interface IGameConfigProvider
    {
        T Get<T>(string id) where T : class, IConfigRecord;
        bool TryGet<T>(string id, out T record) where T : class, IConfigRecord;
    }

    public sealed class PlayerConfigData : IConfigRecord
    {
        public PlayerConfigData(string id, float walkSpeed, float runSpeed)
        {
            Id = id;
            WalkSpeed = walkSpeed;
            RunSpeed = runSpeed;
        }

        public string Id { get; }
        public float WalkSpeed { get; }
        public float RunSpeed { get; }
    }

    public sealed class EnemyConfigData : IConfigRecord
    {
        public EnemyConfigData(string id, float chaseRange, float attackRange)
        {
            Id = id;
            ChaseRange = chaseRange;
            AttackRange = attackRange;
        }

        public string Id { get; }
        public float ChaseRange { get; }
        public float AttackRange { get; }
    }

    public sealed class GameConfigDatabase : IGameConfigProvider
    {
        private readonly Dictionary<Type, Dictionary<string, IConfigRecord>> _records =
            new Dictionary<Type, Dictionary<string, IConfigRecord>>();

        public GameConfigDatabase(params IConfigRecord[] records)
        {
            foreach (var record in records ?? Array.Empty<IConfigRecord>())
            {
                if (record == null || string.IsNullOrWhiteSpace(record.Id))
                {
                    throw new ArgumentException("Config records require a non-empty id.", nameof(records));
                }

                var type = record.GetType();
                if (!_records.TryGetValue(type, out var typedRecords))
                {
                    typedRecords = new Dictionary<string, IConfigRecord>(StringComparer.Ordinal);
                    _records.Add(type, typedRecords);
                }

                if (typedRecords.ContainsKey(record.Id))
                {
                    throw new ArgumentException($"Duplicate {type.Name} config id '{record.Id}'.", nameof(records));
                }

                typedRecords.Add(record.Id, record);
            }
        }

        public T Get<T>(string id) where T : class, IConfigRecord
        {
            if (!TryGet<T>(id, out var record))
            {
                throw new KeyNotFoundException($"No {typeof(T).Name} config exists for id '{id}'.");
            }

            return record;
        }

        public bool TryGet<T>(string id, out T record) where T : class, IConfigRecord
        {
            if (_records.TryGetValue(typeof(T), out var typedRecords) &&
                typedRecords.TryGetValue(id, out var candidate))
            {
                record = (T)candidate;
                return true;
            }

            record = null;
            return false;
        }
    }
}
