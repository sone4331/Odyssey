internal static class Program
{
    private static int Main()
    {
        StateMachineSpecs.Register();
        GameplayTagSpecs.Register();
        AbilitySystemSpecs.Register();
        HealthSpecs.Register();
        return Spec.Complete();
    }
}
