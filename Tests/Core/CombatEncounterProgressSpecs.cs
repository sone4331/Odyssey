using Odyssey.Gameplay.Encounters;

/// <summary>
/// 验证固定成员遭遇战的开始、计数和完成边界，确保场景触发器或网络重复消息不会重复推进进度。
/// </summary>
internal static class CombatEncounterProgressSpecs
{
    public static void Register()
    {
        Spec.Run("遭遇战只允许开始一次", EncounterStartsOnlyOnce);
        Spec.Run("全部敌人击败后遭遇战完成", AllDefeatsCompleteEncounter);
        Spec.Run("完成后的重复击败不会继续计数", DuplicateDefeatAfterCompletionIsRejected);
    }

    private static void EncounterStartsOnlyOnce()
    {
        var progress = new CombatEncounterProgress(3);

        Spec.True(progress.Start(), "首次开始遭遇战被拒绝");
        Spec.True(!progress.Start(), "遭遇战被重复开始");
    }

    private static void AllDefeatsCompleteEncounter()
    {
        var progress = new CombatEncounterProgress(3);
        progress.Start();

        progress.RegisterDefeat();
        progress.RegisterDefeat();
        progress.RegisterDefeat();

        Spec.Equal(CombatEncounterState.Completed, progress.State, "全部敌人击败后遭遇战没有完成");
        Spec.Equal(0, progress.RemainingEnemies, "完成时剩余敌人数不为零");
    }

    private static void DuplicateDefeatAfterCompletionIsRejected()
    {
        var progress = new CombatEncounterProgress(1);
        progress.Start();
        Spec.True(progress.RegisterDefeat(), "有效击败没有推进遭遇战");

        Spec.True(!progress.RegisterDefeat(), "完成后的重复击败被错误接受");
        Spec.Equal(0, progress.RemainingEnemies, "重复击败导致剩余数量小于零");
    }
}
