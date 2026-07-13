using System;
using Odyssey.Core.Abilities;
using Odyssey.Core.Tags;
using Odyssey.Gameplay.Combat;
using Odyssey.Gameplay.Config;

namespace Odyssey.Gameplay.Characters
{
    /// <summary>
    /// 根据玩家配置创建并持有 Health 与 Ability 运行时，是玩家战斗规则的纯 C# 聚合。
    /// 采用 Facade 与聚合根模式，把领域系统装配从 MonoBehaviour 移出，使配置冷却、生命和打断规则可独立测试。
    /// </summary>
    public sealed class PlayerRuntimeSystems
    {
        public const string AttackAbilityId = "player.attack";
        public const string DashAbilityId = "player.dash";
        public const string HitAbilityId = "player.hit";

        public PlayerRuntimeSystems(PlayerConfigData config, int startingHealth)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            Health = new Health(config.MaxHealth);
            var initialDamage = Health.Maximum - Math.Max(0, Math.Min(startingHealth, Health.Maximum));
            if (initialDamage > 0)
            {
                Health.Apply(new DamageRequest(initialDamage, "initial"));
            }

            Abilities = new AbilitySystem(new[]
            {
                new AbilityDefinition(
                    AttackAbilityId,
                    config.AttackCooldown,
                    blockedTags: new[] { GameplayTag.Parse("State.Hit") },
                    ownedTags: new[] { GameplayTag.Parse("Ability.Attack") }),
                new AbilityDefinition(
                    DashAbilityId,
                    config.DashCooldown,
                    blockedTags: new[] { GameplayTag.Parse("State.Hit") },
                    ownedTags: new[] { GameplayTag.Parse("Ability.Dash") }),
                new AbilityDefinition(
                    HitAbilityId,
                    ownedTags: new[] { GameplayTag.Parse("State.Hit") },
                    cancelTags: new[]
                    {
                        GameplayTag.Parse("Ability.Attack"),
                        GameplayTag.Parse("Ability.Dash")
                    })
            });
        }

        public Health Health { get; }
        public IAbilitySystem Abilities { get; }
    }
}
