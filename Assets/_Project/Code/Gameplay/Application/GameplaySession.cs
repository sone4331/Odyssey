using System;
using Odyssey.Core.Events;

namespace Odyssey.Gameplay.Application
{
    /// <summary>
    /// 表示一局单机游戏或一次网络连接的领域生命周期，并拥有只服务本局的事件总线。
    /// 采用作用域对象与所有权模式集中释放会话资源，避免返回菜单后旧 UI、AI 或网络订阅继续接收事件。
    /// </summary>
    public sealed class GameplaySession : IDisposable
    {
        private bool _isDisposed;

        public GameplaySession()
        {
            Events = new GameEventBus();
        }

        /// <summary>
        /// 获取本局唯一的事件总线；调用方只能订阅或发布，不能把它缓存到会话生命周期之外。
        /// </summary>
        public IGameEventBus Events { get; }

        /// <summary>
        /// 结束本局并释放全部事件订阅。方法允许重复调用，便于场景卸载与异常清理共享同一条退出路径。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            Events.Dispose();
        }
    }
}
