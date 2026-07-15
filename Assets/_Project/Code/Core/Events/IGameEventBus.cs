using System;

namespace Odyssey.Core.Events
{
    /// <summary>
    /// 定义同一游戏会话内的强类型事件发布与订阅端口，不负责跨线程调度、网络复制或状态持久化。
    /// 采用观察者与中介者模式，让 UI、音频、特效和调试功能观察已经发生的领域事实，而不反向依赖事件发布者。
    /// </summary>
    public interface IGameEventBus : IDisposable
    {
        /// <summary>
        /// 订阅指定载荷类型，并返回代表本次订阅所有权的释放令牌。
        /// 调用方必须在所属场景或会话结束时释放令牌，防止旧对象继续接收下一局事件。
        /// </summary>
        IDisposable Subscribe<TEvent>(Action<TEvent> handler);

        /// <summary>
        /// 在当前线程同步发布不可变事件事实；载荷类型决定订阅通道，不使用字符串或反射路由。
        /// </summary>
        void Publish<TEvent>(TEvent eventData);
    }
}
