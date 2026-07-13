using Odyssey.Gameplay.Combat;

internal static class HealthSpecs
{
    public static void Register()
    {
        Spec.Run("伤害结果限制生命值不低于零", DamageIsClampedAtZero);
        Spec.Run("生命事件报告实际伤害", HealthChangedReportsAppliedDamage);
        Spec.Run("死亡目标拒绝额外伤害", DeadTargetRejectsAdditionalDamage);
        Spec.Run("非正数伤害被拒绝", NonPositiveDamageIsRejected);
        Spec.Run("生命恢复不超过最大值", HealthCanBeRestoredWithoutExceedingMaximum);
        Spec.Run("重置恢复最大生命值", ResetRestoresMaximumHealth);
    }

    private static void DamageIsClampedAtZero()
    {
        var health = new Health(5);

        var result = health.Apply(new DamageRequest(12, "player"));

        Spec.Equal(0, health.Current, "生命值低于零");
        Spec.Equal(5, result.AppliedAmount, "实际伤害未被限制");
        Spec.True(result.Killed, "致死伤害未报告击杀");
    }

    private static void HealthChangedReportsAppliedDamage()
    {
        var health = new Health(5);
        HealthChanged observed = default;
        health.Changed += change => observed = change;

        health.Apply(new DamageRequest(2, "enemy"));

        Spec.Equal(5, observed.Previous, "事件中的变更前生命值错误");
        Spec.Equal(3, observed.Current, "事件中的当前生命值错误");
        Spec.Equal("enemy", observed.SourceId, "事件来源错误");
    }

    private static void DeadTargetRejectsAdditionalDamage()
    {
        var health = new Health(1);
        health.Apply(new DamageRequest(1, "enemy"));

        var result = health.Apply(new DamageRequest(1, "enemy"));

        Spec.True(!result.Accepted, "死亡目标接受了额外伤害");
        Spec.Equal(0, result.AppliedAmount, "死亡目标应用了额外伤害");
    }

    private static void NonPositiveDamageIsRejected()
    {
        var health = new Health(5);

        var result = health.Apply(new DamageRequest(0, "enemy"));

        Spec.True(!result.Accepted, "零伤害请求被错误接受");
        Spec.Equal(5, health.Current, "零伤害改变了生命值");
    }

    private static void HealthCanBeRestoredWithoutExceedingMaximum()
    {
        var health = new Health(5);
        health.Apply(new DamageRequest(4, "enemy"));

        health.Restore(10, "load");

        Spec.Equal(5, health.Current, "恢复后的生命值超过上限");
    }

    private static void ResetRestoresMaximumHealth()
    {
        var health = new Health(5);
        health.Apply(new DamageRequest(5, "enemy"));

        health.Reset("respawn");

        Spec.Equal(5, health.Current, "重置未恢复最大生命值");
        Spec.True(!health.IsDead, "生命值重置后仍处于死亡状态");
    }
}
