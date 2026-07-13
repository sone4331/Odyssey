using System;
using System.Collections.Generic;

namespace Odyssey.Gameplay.Config
{
    /// <summary>
    /// 标识具有稳定 ID 的配置记录，是类型化配置数据库的最小数据契约。
    /// </summary>
    public interface IConfigRecord
    {
        string Id { get; }
    }

    /// <summary>
    /// 定义按类型和 ID 查询只读配置的端口，隔离 Gameplay 与 CSV、ScriptableObject 等存储形式。
    /// 采用 Repository 模式与依赖倒置，使测试可使用内存数据库，Unity 运行时可使用资产适配器。
    /// </summary>
    public interface IGameConfigProvider
    {
        T Get<T>(string id) where T : class, IConfigRecord;
        bool TryGet<T>(string id, out T record) where T : class, IConfigRecord;
    }

    /// <summary>
    /// 保存玩家运行时所需的纯 C# 配置数据，避免 Gameplay 直接依赖 Unity 序列化对象。
    /// </summary>
    public sealed class PlayerConfigData : IConfigRecord
    {
        public PlayerConfigData(
            string id,
            float walkSpeed,
            float runSpeed,
            float gravity = -15f,
            float dashForce = 20f,
            float dashDuration = 0.2f,
            float dashCooldown = 0.5f,
            float jumpHeight = 3f,
            float chargeJumpHeight = 5f,
            float minChargeTime = 0.5f,
            float airJumpHeight = 2f,
            float wallSlideSpeed = -3f,
            float wallJumpUpForce = 12f,
            float wallJumpSideForce = 10f,
            int attackDamage = 1,
            float attackRange = 2f,
            float attackCooldown = 0.5f,
            int maxHealth = 5)
        {
            Id = id;
            WalkSpeed = walkSpeed;
            RunSpeed = runSpeed;
            Gravity = gravity;
            DashForce = dashForce;
            DashDuration = dashDuration;
            DashCooldown = dashCooldown;
            JumpHeight = jumpHeight;
            ChargeJumpHeight = chargeJumpHeight;
            MinChargeTime = minChargeTime;
            AirJumpHeight = airJumpHeight;
            WallSlideSpeed = wallSlideSpeed;
            WallJumpUpForce = wallJumpUpForce;
            WallJumpSideForce = wallJumpSideForce;
            AttackDamage = attackDamage;
            AttackRange = attackRange;
            AttackCooldown = attackCooldown;
            MaxHealth = maxHealth;
        }

        public string Id { get; }
        public float WalkSpeed { get; }
        public float RunSpeed { get; }
        public float Gravity { get; }
        public float DashForce { get; }
        public float DashDuration { get; }
        public float DashCooldown { get; }
        public float JumpHeight { get; }
        public float ChargeJumpHeight { get; }
        public float MinChargeTime { get; }
        public float AirJumpHeight { get; }
        public float WallSlideSpeed { get; }
        public float WallJumpUpForce { get; }
        public float WallJumpSideForce { get; }
        public int AttackDamage { get; }
        public float AttackRange { get; }
        public float AttackCooldown { get; }
        public int MaxHealth { get; }
    }

    /// <summary>
    /// 保存敌人运行时所需的纯 C# 配置数据，为后续 AI 与 Unity Adapter 提供稳定输入。
    /// </summary>
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

    /// <summary>
    /// 将不同类型的配置记录构建为只读类型化索引，并拒绝重复 ID。
    /// 使用内存 Repository 模式保证查询不依赖 Unity，同时把配置唯一性不变量集中在一个边界维护。
    /// </summary>
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
                    throw new ArgumentException("配置记录必须包含非空 ID。", nameof(records));
                }

                var type = record.GetType();
                if (!_records.TryGetValue(type, out var typedRecords))
                {
                    typedRecords = new Dictionary<string, IConfigRecord>(StringComparer.Ordinal);
                    _records.Add(type, typedRecords);
                }

                if (typedRecords.ContainsKey(record.Id))
                {
                    throw new ArgumentException($"{type.Name} 配置 ID“{record.Id}”重复。", nameof(records));
                }

                typedRecords.Add(record.Id, record);
            }
        }

        public T Get<T>(string id) where T : class, IConfigRecord
        {
            if (!TryGet<T>(id, out var record))
            {
                throw new KeyNotFoundException($"不存在 ID 为“{id}”的 {typeof(T).Name} 配置。");
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
