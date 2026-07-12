using Odyssey.Core.Abilities;
using Odyssey.Core.Tags;

internal static class AbilitySystemSpecs
{
    public static void Register()
    {
        Spec.Run("ability_requires_all_required_tags", AbilityRequiresAllRequiredTags);
        Spec.Run("ability_is_rejected_by_blocking_tag", AbilityIsRejectedByBlockingTag);
        Spec.Run("active_ability_owns_tags_until_end", ActiveAbilityOwnsTagsUntilEnd);
        Spec.Run("ability_cooldown_blocks_early_reactivation", AbilityCooldownBlocksEarlyReactivation);
        Spec.Run("ability_can_cancel_conflicting_active_ability", AbilityCanCancelConflictingActiveAbility);
    }

    private static void AbilityRequiresAllRequiredTags()
    {
        var system = NewSystem(new AbilityDefinition(
            "attack",
            requiredTags: new[] { Tag("Equipment.Weapon") }));

        var result = system.TryActivate("attack", 0f);

        Spec.Equal(AbilityActivationFailure.MissingRequiredTag, result.Failure, "missing tag did not reject ability");
    }

    private static void AbilityIsRejectedByBlockingTag()
    {
        var system = NewSystem(new AbilityDefinition(
            "attack",
            blockedTags: new[] { Tag("State.Stunned") }));
        system.AddTag(Tag("State.Stunned.Heavy"));

        var result = system.TryActivate("attack", 0f);

        Spec.Equal(AbilityActivationFailure.BlockedByTag, result.Failure, "blocking parent tag was ignored");
    }

    private static void ActiveAbilityOwnsTagsUntilEnd()
    {
        var system = NewSystem(new AbilityDefinition(
            "attack",
            ownedTags: new[] { Tag("State.Combat.Attacking") }));

        var result = system.TryActivate("attack", 0f);

        Spec.True(result.Succeeded, "ability did not activate");
        Spec.True(system.Tags.Has(Tag("State.Combat")), "owned tag was not applied");
        system.End("attack");
        Spec.True(!system.Tags.Has(Tag("State.Combat")), "owned tag was not removed on end");
    }

    private static void AbilityCooldownBlocksEarlyReactivation()
    {
        var system = NewSystem(new AbilityDefinition("dash", cooldownSeconds: 1f));
        system.TryActivate("dash", 5f);
        system.End("dash");

        var early = system.TryActivate("dash", 5.5f);
        var ready = system.TryActivate("dash", 6f);

        Spec.Equal(AbilityActivationFailure.OnCooldown, early.Failure, "cooldown did not reject early activation");
        Spec.True(ready.Succeeded, "ability did not reactivate when cooldown expired");
    }

    private static void AbilityCanCancelConflictingActiveAbility()
    {
        var attack = new AbilityDefinition("attack", ownedTags: new[] { Tag("Ability.Attack") });
        var hit = new AbilityDefinition("hit", cancelTags: new[] { Tag("Ability.Attack") });
        var system = NewSystem(attack, hit);
        system.TryActivate("attack", 0f);

        system.TryActivate("hit", 0f);

        Spec.True(!system.IsActive("attack"), "conflicting ability remained active");
        Spec.True(system.IsActive("hit"), "new ability was not active");
    }

    private static AbilitySystem NewSystem(params AbilityDefinition[] definitions)
    {
        return new AbilitySystem(definitions);
    }

    private static GameplayTag Tag(string value) => GameplayTag.Parse(value);
}
