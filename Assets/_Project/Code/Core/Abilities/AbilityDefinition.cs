using System;
using System.Collections.Generic;
using Odyssey.Core.Tags;

namespace Odyssey.Core.Abilities
{
    public sealed class AbilityDefinition
    {
        public AbilityDefinition(
            string id,
            float cooldownSeconds = 0f,
            IReadOnlyList<GameplayTag> requiredTags = null,
            IReadOnlyList<GameplayTag> blockedTags = null,
            IReadOnlyList<GameplayTag> ownedTags = null,
            IReadOnlyList<GameplayTag> cancelTags = null)
        {
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Ability id cannot be empty.", nameof(id)) : id;
            CooldownSeconds = cooldownSeconds;
            RequiredTags = requiredTags ?? Array.Empty<GameplayTag>();
            BlockedTags = blockedTags ?? Array.Empty<GameplayTag>();
            OwnedTags = ownedTags ?? Array.Empty<GameplayTag>();
            CancelTags = cancelTags ?? Array.Empty<GameplayTag>();
        }

        public string Id { get; }
        public float CooldownSeconds { get; }
        public IReadOnlyList<GameplayTag> RequiredTags { get; }
        public IReadOnlyList<GameplayTag> BlockedTags { get; }
        public IReadOnlyList<GameplayTag> OwnedTags { get; }
        public IReadOnlyList<GameplayTag> CancelTags { get; }
    }
}
