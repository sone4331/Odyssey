using Odyssey.Core.Tags;

internal static class GameplayTagSpecs
{
    public static void Register()
    {
        Spec.Run("tag_set_matches_parent_tag", TagSetMatchesParentTag);
        Spec.Run("tag_set_removes_exact_tag", TagSetRemovesExactTag);
    }

    private static void TagSetMatchesParentTag()
    {
        var tags = new GameplayTagSet();
        tags.Add(GameplayTag.Parse("State.Combat.Attacking"));

        Spec.True(tags.Has(GameplayTag.Parse("State.Combat")), "parent tag was not matched");
        Spec.True(!tags.Has(GameplayTag.Parse("State.Movement")), "unrelated tag matched");
    }

    private static void TagSetRemovesExactTag()
    {
        var tags = new GameplayTagSet();
        var attacking = GameplayTag.Parse("State.Combat.Attacking");
        tags.Add(attacking);
        tags.Remove(attacking);

        Spec.True(!tags.Has(GameplayTag.Parse("State.Combat")), "removed tag still contributed to the set");
    }
}
