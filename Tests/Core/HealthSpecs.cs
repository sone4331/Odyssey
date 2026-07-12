using Odyssey.Gameplay.Combat;

internal static class HealthSpecs
{
    public static void Register()
    {
        Spec.Run("damage_is_clamped_at_zero", DamageIsClampedAtZero);
        Spec.Run("health_changed_reports_applied_damage", HealthChangedReportsAppliedDamage);
        Spec.Run("dead_target_rejects_additional_damage", DeadTargetRejectsAdditionalDamage);
        Spec.Run("non_positive_damage_is_rejected", NonPositiveDamageIsRejected);
        Spec.Run("health_can_be_restored_without_exceeding_maximum", HealthCanBeRestoredWithoutExceedingMaximum);
        Spec.Run("reset_restores_maximum_health", ResetRestoresMaximumHealth);
    }

    private static void DamageIsClampedAtZero()
    {
        var health = new Health(5);

        var result = health.Apply(new DamageRequest(12, "player"));

        Spec.Equal(0, health.Current, "health went below zero");
        Spec.Equal(5, result.AppliedAmount, "applied damage was not clamped");
        Spec.True(result.Killed, "lethal damage did not report a kill");
    }

    private static void HealthChangedReportsAppliedDamage()
    {
        var health = new Health(5);
        HealthChanged observed = default;
        health.Changed += change => observed = change;

        health.Apply(new DamageRequest(2, "enemy"));

        Spec.Equal(5, observed.Previous, "event previous health was incorrect");
        Spec.Equal(3, observed.Current, "event current health was incorrect");
        Spec.Equal("enemy", observed.SourceId, "event source was incorrect");
    }

    private static void DeadTargetRejectsAdditionalDamage()
    {
        var health = new Health(1);
        health.Apply(new DamageRequest(1, "enemy"));

        var result = health.Apply(new DamageRequest(1, "enemy"));

        Spec.True(!result.Accepted, "dead target accepted additional damage");
        Spec.Equal(0, result.AppliedAmount, "dead target applied additional damage");
    }

    private static void NonPositiveDamageIsRejected()
    {
        var health = new Health(5);

        var result = health.Apply(new DamageRequest(0, "enemy"));

        Spec.True(!result.Accepted, "zero damage request was accepted");
        Spec.Equal(5, health.Current, "zero damage changed health");
    }

    private static void HealthCanBeRestoredWithoutExceedingMaximum()
    {
        var health = new Health(5);
        health.Apply(new DamageRequest(4, "enemy"));

        health.Restore(10, "load");

        Spec.Equal(5, health.Current, "restored health exceeded maximum");
    }

    private static void ResetRestoresMaximumHealth()
    {
        var health = new Health(5);
        health.Apply(new DamageRequest(5, "enemy"));

        health.Reset("respawn");

        Spec.Equal(5, health.Current, "reset did not restore maximum health");
        Spec.True(!health.IsDead, "reset health remained dead");
    }
}
