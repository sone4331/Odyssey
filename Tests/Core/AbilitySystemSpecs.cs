using Odyssey.Core.Abilities;
using Odyssey.Core.Tags;

internal static class AbilitySystemSpecs
{
    public static void Register()
    {
        Spec.Run("技能要求全部必需标签", AbilityRequiresAllRequiredTags);
        Spec.Run("阻止标签拒绝技能激活", AbilityIsRejectedByBlockingTag);
        Spec.Run("激活技能持有标签直到结束", ActiveAbilityOwnsTagsUntilEnd);
        Spec.Run("技能冷却阻止提前再次激活", AbilityCooldownBlocksEarlyReactivation);
        Spec.Run("技能可以取消冲突中的激活技能", AbilityCanCancelConflictingActiveAbility);
    }

    private static void AbilityRequiresAllRequiredTags()
    {
        var system = NewSystem(new AbilityDefinition(
            "attack",
            requiredTags: new[] { Tag("Equipment.Weapon") }));

        var result = system.TryActivate("attack", 0f);

        Spec.Equal(AbilityActivationFailure.MissingRequiredTag, result.Failure, "缺少必需标签时技能未被拒绝");
    }

    private static void AbilityIsRejectedByBlockingTag()
    {
        var system = NewSystem(new AbilityDefinition(
            "attack",
            blockedTags: new[] { Tag("State.Stunned") }));
        system.AddTag(Tag("State.Stunned.Heavy"));

        var result = system.TryActivate("attack", 0f);

        Spec.Equal(AbilityActivationFailure.BlockedByTag, result.Failure, "阻止用的父级标签被忽略");
    }

    private static void ActiveAbilityOwnsTagsUntilEnd()
    {
        var system = NewSystem(new AbilityDefinition(
            "attack",
            ownedTags: new[] { Tag("State.Combat.Attacking") }));

        var result = system.TryActivate("attack", 0f);

        Spec.True(result.Succeeded, "技能未能激活");
        Spec.True(system.Tags.Has(Tag("State.Combat")), "技能持有标签未被添加");
        system.End("attack");
        Spec.True(!system.Tags.Has(Tag("State.Combat")), "技能结束后持有标签未被移除");
    }

    private static void AbilityCooldownBlocksEarlyReactivation()
    {
        var system = NewSystem(new AbilityDefinition("dash", cooldownSeconds: 1f));
        system.TryActivate("dash", 5f);
        system.End("dash");

        var early = system.TryActivate("dash", 5.5f);
        var ready = system.TryActivate("dash", 6f);

        Spec.Equal(AbilityActivationFailure.OnCooldown, early.Failure, "冷却期间的提前激活未被拒绝");
        Spec.True(ready.Succeeded, "冷却结束后技能未能再次激活");
    }

    private static void AbilityCanCancelConflictingActiveAbility()
    {
        var attack = new AbilityDefinition("attack", ownedTags: new[] { Tag("Ability.Attack") });
        var hit = new AbilityDefinition("hit", cancelTags: new[] { Tag("Ability.Attack") });
        var system = NewSystem(attack, hit);
        system.TryActivate("attack", 0f);

        system.TryActivate("hit", 0f);

        Spec.True(!system.IsActive("attack"), "冲突技能仍处于激活状态");
        Spec.True(system.IsActive("hit"), "新技能未进入激活状态");
    }

    private static AbilitySystem NewSystem(params AbilityDefinition[] definitions)
    {
        return new AbilitySystem(definitions);
    }

    private static GameplayTag Tag(string value) => GameplayTag.Parse(value);
}
