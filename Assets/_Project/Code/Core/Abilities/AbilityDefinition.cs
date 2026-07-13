using System;
using System.Collections.Generic;
using Odyssey.Core.Tags;

namespace Odyssey.Core.Abilities
{
    /// <summary>
    /// 描述技能的激活约束、冷却与标签副作用，是 Ability 系统的不可变配置对象。
    /// 采用数据对象模式把规则声明与运行时状态分离，使玩家、AI 和网络校验能够复用同一份定义。
    /// </summary>
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
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("技能 ID 不能为空。", nameof(id)) : id;
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
