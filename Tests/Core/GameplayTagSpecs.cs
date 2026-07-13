using System;
using Odyssey.Core.Tags;

internal static class GameplayTagSpecs
{
    public static void Register()
    {
        Spec.Run("标签集合匹配父级标签", TagSetMatchesParentTag);
        Spec.Run("标签集合移除精确标签", TagSetRemovesExactTag);
        Spec.Run("空玩法标签被拒绝", EmptyGameplayTagIsRejected);
    }

    private static void EmptyGameplayTagIsRejected()
    {
        Spec.Throws<ArgumentException>(() => GameplayTag.Parse("  "), "空玩法标签被错误接受");
    }

    private static void TagSetMatchesParentTag()
    {
        var tags = new GameplayTagSet();
        tags.Add(GameplayTag.Parse("State.Combat.Attacking"));

        Spec.True(tags.Has(GameplayTag.Parse("State.Combat")), "父级标签未被匹配");
        Spec.True(!tags.Has(GameplayTag.Parse("State.Movement")), "无关标签被错误匹配");
    }

    private static void TagSetRemovesExactTag()
    {
        var tags = new GameplayTagSet();
        var attacking = GameplayTag.Parse("State.Combat.Attacking");
        tags.Add(attacking);
        tags.Remove(attacking);

        Spec.True(!tags.Has(GameplayTag.Parse("State.Combat")), "已移除标签仍影响集合查询");
    }
}
