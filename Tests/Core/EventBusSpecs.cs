using System;
using Odyssey.Core.Events;

/// <summary>
/// 固化会话级事件总线的类型隔离、订阅生命周期和发布稳定性约束。
/// 这些规格防止场景切换后的悬挂订阅，以及单个表现层异常阻断其他观察者。
/// </summary>
internal static class EventBusSpecs
{
    public static void Register()
    {
        Spec.Run("事件只通知相同载荷类型的订阅者", PublishesOnlyToMatchingEventType);
        Spec.Run("释放订阅后不再接收事件", DisposedSubscriptionStopsReceivingEvents);
        Spec.Run("发布期间新增订阅从下一次发布生效", SubscriptionChangesApplyToNextPublish);
        Spec.Run("异常订阅者不阻断其他订阅者", SubscriberFailureDoesNotBlockRemainingSubscribers);
        Spec.Run("释放事件总线后拒绝继续使用", DisposedBusRejectsFurtherUse);
    }

    private static void PublishesOnlyToMatchingEventType()
    {
        using (var bus = new GameEventBus())
        {
            var numberTotal = 0;
            var textCount = 0;
            bus.Subscribe<int>(value => numberTotal += value);
            bus.Subscribe<string>(_ => textCount++);

            bus.Publish(3);

            Spec.Equal(3, numberTotal, "整数事件没有传递给对应订阅者");
            Spec.Equal(0, textCount, "整数事件错误通知了字符串订阅者");
        }
    }

    private static void DisposedSubscriptionStopsReceivingEvents()
    {
        using (var bus = new GameEventBus())
        {
            var receivedCount = 0;
            var subscription = bus.Subscribe<TestEvent>(_ => receivedCount++);
            bus.Publish(new TestEvent());

            subscription.Dispose();
            subscription.Dispose();
            bus.Publish(new TestEvent());

            Spec.Equal(1, receivedCount, "已经释放的订阅仍然收到了事件");
        }
    }

    private static void SubscriptionChangesApplyToNextPublish()
    {
        using (var bus = new GameEventBus())
        {
            var lateSubscriberCount = 0;
            IDisposable lateSubscription = null;
            bus.Subscribe<TestEvent>(_ =>
            {
                if (lateSubscription == null)
                {
                    lateSubscription = bus.Subscribe<TestEvent>(__ => lateSubscriberCount++);
                }
            });

            bus.Publish(new TestEvent());
            Spec.Equal(0, lateSubscriberCount, "发布中新增的订阅错误参与了当前轮发布");

            bus.Publish(new TestEvent());
            Spec.Equal(1, lateSubscriberCount, "新增订阅没有从下一轮发布开始生效");
            lateSubscription.Dispose();
        }
    }

    private static void SubscriberFailureDoesNotBlockRemainingSubscribers()
    {
        using (var bus = new GameEventBus())
        {
            var healthySubscriberCount = 0;
            bus.Subscribe<TestEvent>(_ => throw new InvalidOperationException("用于验证隔离策略的预期异常"));
            bus.Subscribe<TestEvent>(_ => healthySubscriberCount++);

            var exception = Spec.Throws<AggregateException>(
                () => bus.Publish(new TestEvent()),
                "订阅者异常没有被事件总线统一报告");

            Spec.Equal(1, healthySubscriberCount, "前一个订阅者异常阻断了后续订阅者");
            Spec.Equal(1, exception.InnerExceptions.Count, "聚合异常数量与失败订阅者数量不一致");
        }
    }

    private static void DisposedBusRejectsFurtherUse()
    {
        var bus = new GameEventBus();
        bus.Dispose();

        Spec.Throws<ObjectDisposedException>(
            () => bus.Subscribe<TestEvent>(_ => { }),
            "释放后的事件总线仍允许新增订阅");
        Spec.Throws<ObjectDisposedException>(
            () => bus.Publish(new TestEvent()),
            "释放后的事件总线仍允许发布事件");
    }

    private readonly struct TestEvent
    {
    }
}
