internal static class Program
{
    private static int Main()
    {
        StateMachineSpecs.Register();
        EventBusSpecs.Register();
        GameplayTagSpecs.Register();
        AbilitySystemSpecs.Register();
        HealthSpecs.Register();
        UtilityGoalSelectorSpecs.Register();
        SaveMigrationSpecs.Register();
        AtomicFileSaveServiceSpecs.Register();
        ConfigDatabaseSpecs.Register();
        CsvTableParserSpecs.Register();
        PlayerRuntimeSystemsSpecs.Register();
        return Spec.Complete();
    }
}
