using Odyssey.Core.Tags;

namespace Odyssey.Core.Abilities
{
    /// <summary>
    /// 枚举技能激活失败的领域原因，使调用方可以决定表现或网络拒绝信息，而不依赖异常控制流程。
    /// </summary>
    public enum AbilityActivationFailure
    {
        None,
        UnknownAbility,
        AlreadyActive,
        MissingRequiredTag,
        BlockedByTag,
        OnCooldown
    }

    /// <summary>
    /// 表示一次技能激活的值对象；成功与失败都显式返回，避免 UI、AI 和网络层猜测运行时状态。
    /// </summary>
    public readonly struct AbilityActivationResult
    {
        public AbilityActivationResult(AbilityActivationFailure failure)
        {
            Failure = failure;
        }

        public AbilityActivationFailure Failure { get; }
        public bool Succeeded => Failure == AbilityActivationFailure.None;
    }

    /// <summary>
    /// 定义技能运行时的最小端口，隔离领域规则与角色、动画及网络适配层。
    /// 采用端口与适配器模式，使同一套 Ability 规则可以由玩家、AI 和 Host 权威逻辑驱动。
    /// </summary>
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
