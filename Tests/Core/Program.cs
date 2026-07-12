internal static class Program
{
    private static int Main()
    {
        StateMachineSpecs.Register();
        GameplayTagSpecs.Register();
        AbilitySystemSpecs.Register();
        HealthSpecs.Register();
        UtilityGoalSelectorSpecs.Register();
        SaveMigrationSpecs.Register();
        ConfigDatabaseSpecs.Register();
        return Spec.Complete();
    }
}
