using System;
using System.Collections.Generic;
using Odyssey.Gameplay.Config;
using UnityEngine;

namespace Odyssey.Unity.Config
{
    /// <summary>
    /// 将 Unity 可序列化配置条目适配为纯 C# 的 IGameConfigProvider。
    /// 采用 Adapter 与 Repository 模式，让 Inspector/导表资产停留在 Unity 边界，Gameplay 只读取不可变数据。
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfigDatabase", menuName = "Odyssey/配置/游戏配置数据库")]
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
    /// <summary>
    /// 玩家配置的 Unity 序列化 DTO，仅负责资产存储并转换为纯 C# PlayerConfigData。
    /// </summary>
    public sealed class PlayerConfigEntry
    {
        public string id;
        public float walkSpeed;
        public float runSpeed;
        public float gravity;
        public float dashForce;
        public float dashDuration;
        public float dashCooldown;
        public float jumpHeight;
        public float chargeJumpHeight;
        public float minChargeTime;
        public float airJumpHeight;
        public float wallSlideSpeed;
        public float wallJumpUpForce;
        public float wallJumpSideForce;
        public int attackDamage;
        public float attackRange;
        public float attackCooldown;
        public int maxHealth;
        public float groundAcceleration;
        public float groundDeceleration;
        public float minTurnSpeed;
        public float maxTurnSpeed;
        public float attackAdvanceSpeed;

        public PlayerConfigData ToData() => new PlayerConfigData(
            id, walkSpeed, runSpeed, gravity, dashForce, dashDuration, dashCooldown,
            jumpHeight, chargeJumpHeight, minChargeTime, airJumpHeight,
            wallSlideSpeed, wallJumpUpForce, wallJumpSideForce,
            attackDamage, attackRange, attackCooldown, maxHealth,
            groundAcceleration, groundDeceleration, minTurnSpeed, maxTurnSpeed, attackAdvanceSpeed);
    }

    [Serializable]
    /// <summary>
    /// 敌人配置的 Unity 序列化 DTO，仅负责资产存储并转换为纯 C# EnemyConfigData。
    /// </summary>
    public sealed class EnemyConfigEntry
    {
        public string id;
        public float chaseRange;
        public float attackRange;
        public int maxHealth;
        public int attackDamage;
        public float attackCooldown;

        public EnemyConfigData ToData() => new EnemyConfigData(
            id, chaseRange, attackRange, maxHealth, attackDamage, attackCooldown);
    }
}
