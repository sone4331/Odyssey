using System;
using System.Collections.Generic;

namespace Odyssey.Core.Events
{
    /// <summary>
    /// 保存一次游戏会话内的类型化订阅，并以稳定快照同步通知所有观察者。
    /// 采用观察者与中介者模式隔离跨功能通知；实例由会话 Composition Root 持有，禁止作为静态全局事件中心使用。
    /// </summary>
    public sealed class GameEventBus : IGameEventBus
    {
        private readonly Dictionary<Type, List<IEventSubscription>> _subscriptions =
            new Dictionary<Type, List<IEventSubscription>>();

        private bool _isDisposed;

        /// <summary>
        /// 建立一个独立订阅。即使同一委托被重复注册，每个返回令牌也只拥有并释放对应的那一次注册。
        /// </summary>
        public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        {
            EnsureAvailable();
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var eventType = typeof(TEvent);
            if (!_subscriptions.TryGetValue(eventType, out var typedSubscriptions))
            {
                typedSubscriptions = new List<IEventSubscription>();
                _subscriptions.Add(eventType, typedSubscriptions);
            }

            var subscription = new EventSubscription<TEvent>(handler, Remove);
            typedSubscriptions.Add(subscription);
            return subscription;
        }

        /// <summary>
        /// 先复制当前类型的订阅集合，再依次执行观察者，使发布过程中新增或移除订阅不会改变本轮遍历。
        /// 单个观察者异常会被收集，剩余观察者仍会收到事件；本轮结束后统一抛出聚合异常，避免错误被静默吞掉。
        /// </summary>
        public void Publish<TEvent>(TEvent eventData)
        {
            EnsureAvailable();
            if (!_subscriptions.TryGetValue(typeof(TEvent), out var typedSubscriptions) ||
                typedSubscriptions.Count == 0)
            {
                return;
            }

            var snapshot = typedSubscriptions.ToArray();
            List<Exception> failures = null;
            foreach (var subscription in snapshot)
            {
                try
                {
                    subscription.Invoke(eventData);
                }
                catch (Exception exception)
                {
                    if (failures == null)
                    {
                        failures = new List<Exception>();
                    }

                    failures.Add(exception);
                }
            }

            if (failures != null)
            {
                throw new AggregateException("一个或多个事件订阅者执行失败。", failures);
            }
        }

        /// <summary>
        /// 结束当前会话的全部订阅关系。释放后拒绝继续使用，尽早暴露跨会话持有旧 EventBus 的生命周期错误。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _subscriptions.Clear();
        }

        private void Remove(Type eventType, IEventSubscription subscription)
        {
            if (_isDisposed || !_subscriptions.TryGetValue(eventType, out var typedSubscriptions))
            {
                return;
            }

            typedSubscriptions.Remove(subscription);
            if (typedSubscriptions.Count == 0)
            {
                _subscriptions.Remove(eventType);
            }
        }

        private void EnsureAvailable()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(GameEventBus), "事件总线所属会话已经结束。");
            }
        }

        /// <summary>
        /// 统一不同载荷类型的调用入口，使总线可以按 Type 建立索引而不通过反射执行委托。
        /// </summary>
        private interface IEventSubscription
        {
            void Invoke(object eventData);
        }

        /// <summary>
        /// 表示一次订阅的所有权令牌；释放令牌只移除自身注册，并允许调用方安全地重复释放。
        /// </summary>
        private sealed class EventSubscription<TEvent> : IEventSubscription, IDisposable
        {
            private readonly Action<TEvent> _handler;
            private readonly Action<Type, IEventSubscription> _remove;
            private bool _isDisposed;

            public EventSubscription(
                Action<TEvent> handler,
                Action<Type, IEventSubscription> remove)
            {
                _handler = handler;
                _remove = remove;
            }

            public void Invoke(object eventData)
            {
                _handler((TEvent)eventData);
            }

            public void Dispose()
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
                _remove(typeof(TEvent), this);
            }
        }
    }
}
