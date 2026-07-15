using System;
using Odyssey.Gameplay.Application;

/// <summary>
/// 固化游戏会话对事件总线的所有权，确保返回菜单或断开连接后不会继续使用上一局订阅。
/// </summary>
internal static class GameplaySessionSpecs
{
    public static void Register()
    {
        Spec.Run("不同游戏会话拥有独立事件总线", SessionsOwnIndependentEventBuses);
        Spec.Run("释放游戏会话会同时释放事件总线", DisposingSessionDisposesEventBus);
    }

    private static void SessionsOwnIndependentEventBuses()
    {
        using (var first = new GameplaySession())
        using (var second = new GameplaySession())
        {
            Spec.True(!ReferenceEquals(first.Events, second.Events), "两个游戏会话错误共享了同一个事件总线");
        }
    }

    private static void DisposingSessionDisposesEventBus()
    {
        var session = new GameplaySession();
        var events = session.Events;

        session.Dispose();
        session.Dispose();

        Spec.Throws<ObjectDisposedException>(
            () => events.Publish(new SessionClosedEvent()),
            "会话释放后仍然可以使用旧事件总线");
    }

    private readonly struct SessionClosedEvent
    {
    }
}
