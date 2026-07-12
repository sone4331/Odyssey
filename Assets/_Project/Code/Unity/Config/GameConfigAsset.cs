using System;
using System.Collections.Generic;
using Odyssey.Gameplay.Config;
using UnityEngine;

namespace Odyssey.Unity.Config
{
    [CreateAssetMenu(fileName = "GameConfigDatabase", menuName = "Odyssey/Config/Game Config Database")]
    public sealed class GameConfigAsset : ScriptableObject, IGameConfigProvider
    {
        [SerializeField] private List<PlayerConfigEntry> players = new List<PlayerConfigEntry>();
        [SerializeField] private List<EnemyConfigEntry> enemies = new List<EnemyConfigEntry>();

        private GameConfigDatabase _database;

        public IReadOnlyList<PlayerConfigEntry> Players => players;
        public IReadOnlyList<EnemyConfigEntry> Enemies => enemies;

        public void Replace(IEnumerable<PlayerConfigEntry> playerEntries, IEnumerable<EnemyConfigEntry> enemyEntries)
        {
            players = new List<PlayerConfigEntry>(playerEntries ?? Array.Empty<PlayerConfigEntry>());
            enemies = new List<EnemyConfigEntry>(enemyEntries ?? Array.Empty<EnemyConfigEntry>());
            Rebuild();
        }

        public T Get<T>(string id) where T : class, IConfigRecord
        {
            EnsureDatabase();
            return _database.Get<T>(id);
        }

        public bool TryGet<T>(string id, out T record) where T : class, IConfigRecord
        {
            EnsureDatabase();
            return _database.TryGet(id, out record);
        }

        private void OnEnable() => Rebuild();

        private void EnsureDatabase()
        {
            if (_database == null)
            {
                Rebuild();
            }
        }

        private void Rebuild()
        {
            var records = new List<IConfigRecord>(players.Count + enemies.Count);
            foreach (var entry in players)
            {
                records.Add(entry.ToData());
            }

            foreach (var entry in enemies)
            {
                records.Add(entry.ToData());
            }

            _database = new GameConfigDatabase(records.ToArray());
        }
    }

    [Serializable]
    public sealed class PlayerConfigEntry
    {
        public string id;
        public float walkSpeed;
        public float runSpeed;

        public PlayerConfigData ToData() => new PlayerConfigData(id, walkSpeed, runSpeed);
    }

    [Serializable]
    public sealed class EnemyConfigEntry
    {
        public string id;
        public float chaseRange;
        public float attackRange;

        public EnemyConfigData ToData() => new EnemyConfigData(id, chaseRange, attackRange);
    }
}
