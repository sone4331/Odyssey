using System;
using System.Collections.Generic;
using System.Linq;
using Odyssey.Core.Tags;

namespace Odyssey.Core.Abilities
{
    public sealed class AbilitySystem : IAbilitySystem
    {
        private readonly Dictionary<string, AbilityDefinition> _definitions;
        private readonly Dictionary<string, AbilityDefinition> _active = new Dictionary<string, AbilityDefinition>();
        private readonly Dictionary<string, float> _cooldownEndTimes = new Dictionary<string, float>();

        public AbilitySystem(IEnumerable<AbilityDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            _definitions = definitions.ToDictionary(definition => definition.Id, StringComparer.Ordinal);
            Tags = new GameplayTagSet();
        }

        public GameplayTagSet Tags { get; }

        public AbilityActivationResult TryActivate(string abilityId, float currentTime)
        {
            if (!_definitions.TryGetValue(abilityId, out var definition))
            {
                return Failed(AbilityActivationFailure.UnknownAbility);
            }

            if (_active.ContainsKey(abilityId))
            {
                return Failed(AbilityActivationFailure.AlreadyActive);
            }

            foreach (var requiredTag in definition.RequiredTags)
            {
                if (!Tags.Has(requiredTag))
                {
                    return Failed(AbilityActivationFailure.MissingRequiredTag);
                }
            }

            foreach (var blockedTag in definition.BlockedTags)
            {
                if (Tags.Has(blockedTag))
                {
                    return Failed(AbilityActivationFailure.BlockedByTag);
                }
            }

            if (_cooldownEndTimes.TryGetValue(abilityId, out var cooldownEnd) && currentTime < cooldownEnd)
            {
                return Failed(AbilityActivationFailure.OnCooldown);
            }

            CancelConflictingAbilities(definition);
            _active.Add(abilityId, definition);
            foreach (var ownedTag in definition.OwnedTags)
            {
                Tags.Add(ownedTag);
            }

            _cooldownEndTimes[abilityId] = currentTime + definition.CooldownSeconds;
            return new AbilityActivationResult(AbilityActivationFailure.None);
        }

        public bool End(string abilityId)
        {
            if (!_active.TryGetValue(abilityId, out var definition))
            {
                return false;
            }

            _active.Remove(abilityId);
            foreach (var ownedTag in definition.OwnedTags)
            {
                Tags.Remove(ownedTag);
            }

            return true;
        }

        public bool IsActive(string abilityId) => _active.ContainsKey(abilityId);
        public void AddTag(GameplayTag tag) => Tags.Add(tag);
        public void RemoveTag(GameplayTag tag) => Tags.Remove(tag);

        private void CancelConflictingAbilities(AbilityDefinition activating)
        {
            if (activating.CancelTags.Count == 0)
            {
                return;
            }

            var cancelled = new List<string>();
            foreach (var pair in _active)
            {
                foreach (var ownedTag in pair.Value.OwnedTags)
                {
                    if (MatchesAny(ownedTag, activating.CancelTags))
                    {
                        cancelled.Add(pair.Key);
                        break;
                    }
                }
            }

            foreach (var abilityId in cancelled)
            {
                End(abilityId);
            }
        }

        private static bool MatchesAny(GameplayTag ownedTag, IReadOnlyList<GameplayTag> queries)
        {
            foreach (var query in queries)
            {
                if (ownedTag.Matches(query))
                {
                    return true;
                }
            }

            return false;
        }

        private static AbilityActivationResult Failed(AbilityActivationFailure failure)
        {
            return new AbilityActivationResult(failure);
        }
    }
}
