using Odyssey.Core.Tags;

namespace Odyssey.Core.Abilities
{
    public enum AbilityActivationFailure
    {
        None,
        UnknownAbility,
        AlreadyActive,
        MissingRequiredTag,
        BlockedByTag,
        OnCooldown
    }

    public readonly struct AbilityActivationResult
    {
        public AbilityActivationResult(AbilityActivationFailure failure)
        {
            Failure = failure;
        }

        public AbilityActivationFailure Failure { get; }
        public bool Succeeded => Failure == AbilityActivationFailure.None;
    }

    public interface IAbilitySystem
    {
        GameplayTagSet Tags { get; }
        AbilityActivationResult TryActivate(string abilityId, float currentTime);
        bool End(string abilityId);
        bool IsActive(string abilityId);
        void AddTag(GameplayTag tag);
        void RemoveTag(GameplayTag tag);
    }
}
