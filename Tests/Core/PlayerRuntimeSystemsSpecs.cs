using Odyssey.Core.Abilities;
using Odyssey.Gameplay.Characters;
using Odyssey.Gameplay.Config;

internal static class PlayerRuntimeSystemsSpecs
{
    public static void Register()
    {
        Spec.Run("玩家运行时使用配置初始化生命和冷却", RuntimeUsesConfiguredHealthAndCooldown);
        Spec.Run("玩家受击技能取消攻击与冲刺", HitAbilityCancelsOffensiveAbilities);
        Spec.Run("冲刺激活期间持有无敌标签", DashOwnsInvulnerableTagUntilEnd);
    }

    private static void RuntimeUsesConfiguredHealthAndCooldown()
    {
        var config = new PlayerConfigData(
            "player", 6f, 10f,
            attackCooldown: 1.5f,
            maxHealth: 7);
        var runtime = new PlayerRuntimeSystems(config, startingHealth: 5);

        var first = runtime.Abilities.TryActivate(PlayerRuntimeSystems.AttackAbilityId, 0f);
        runtime.Abilities.End(PlayerRuntimeSystems.AttackAbilityId);
        var early = runtime.Abilities.TryActivate(PlayerRuntimeSystems.AttackAbilityId, 1f);
        var ready = runtime.Abilities.TryActivate(PlayerRuntimeSystems.AttackAbilityId, 1.5f);

        Spec.Equal(7, runtime.Health.Maximum, "运行时未使用配置的最大生命");
        Spec.Equal(5, runtime.Health.Current, "运行时未保留指定初始生命");
        Spec.True(first.Succeeded, "首次攻击技能激活失败");
        Spec.Equal(AbilityActivationFailure.OnCooldown, early.Failure, "配置冷却未阻止提前激活");
        Spec.True(ready.Succeeded, "配置冷却结束后技能未恢复可用");
    }

    private static void HitAbilityCancelsOffensiveAbilities()
    {
        var runtime = new PlayerRuntimeSystems(new PlayerConfigData("player", 6f, 10f), 5);
        runtime.Abilities.TryActivate(PlayerRuntimeSystems.AttackAbilityId, 0f);

        runtime.Abilities.TryActivate(PlayerRuntimeSystems.HitAbilityId, 0f);

        Spec.True(!runtime.Abilities.IsActive(PlayerRuntimeSystems.AttackAbilityId), "受击后攻击技能仍处于激活状态");
        Spec.True(runtime.Abilities.IsActive(PlayerRuntimeSystems.HitAbilityId), "受击技能未进入激活状态");
    }

    private static void DashOwnsInvulnerableTagUntilEnd()
    {
        var runtime = new PlayerRuntimeSystems(new PlayerConfigData("player", 6f, 10f), 5);

        runtime.Abilities.TryActivate(PlayerRuntimeSystems.DashAbilityId, 0f);
        Spec.True(runtime.Abilities.Tags.Has(PlayerRuntimeSystems.InvulnerableTag), "冲刺期间没有持有无敌标签");

        runtime.Abilities.End(PlayerRuntimeSystems.DashAbilityId);
        Spec.True(!runtime.Abilities.Tags.Has(PlayerRuntimeSystems.InvulnerableTag), "冲刺结束后无敌标签没有移除");
    }
}
