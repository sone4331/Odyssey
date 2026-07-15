using System;
using Odyssey.Characters.Player;
using Odyssey.Gameplay.Application;
using Odyssey.Systems;
using Odyssey.Unity.Config;
using UnityEngine;

namespace Odyssey.Bootstrap
{
    /// <summary>
    /// 作为单个玩法场景的 Composition Root 创建会话，并把应用服务注入该场景中的适配器。
    /// 采用 Installer 与作用域所有权模式，使 Bootstrap 不依赖具体玩家、UI 或存档组件，并保证场景卸载时释放会话事件。
    /// </summary>
    public sealed class GameplaySceneInstaller : MonoBehaviour, IDisposable
    {
        private ApplicationContext _context;

        /// <summary>
        /// 获取当前场景拥有的游戏会话，供后续 Presenter、AI 和网络适配器订阅会话事件。
        /// </summary>
        public GameplaySession Session { get; private set; }

        /// <summary>
        /// 使用应用上下文完成一次性场景装配。重复传入同一上下文保持幂等，传入不同上下文则快速失败以暴露生命周期错误。
        /// </summary>
        public void Install(ApplicationContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (_context != null)
            {
                if (ReferenceEquals(_context, context))
                {
                    return;
                }

                throw new InvalidOperationException("场景安装器不能在同一生命周期内更换应用上下文。");
            }

            _context = context;
            Session = new GameplaySession();
            InstallSceneTargets();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        /// <summary>
        /// 显式结束场景作用域并释放 Session；运行时由 OnDestroy 调用，测试和异常退出也可复用同一清理路径。
        /// 方法允许重复调用，避免场景卸载和上层主动清理同时发生时重复释放资源。
        /// </summary>
        public void Dispose()
        {
            Session?.Dispose();
            Session = null;
            _context = null;
        }

        /// <summary>
        /// 只遍历安装器所属场景的根对象，避免 Additive 场景之间互相绑定玩家或存档控制器。
        /// 场景扫描被限制在 Composition Root 内，Gameplay 规则和普通组件不得自行查找全局依赖。
        /// </summary>
        private void InstallSceneTargets()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var player in root.GetComponentsInChildren<PlayerController>(true))
                {
                    PlayerConfigBinder.Bind(_context.Configs, player.ConfigId, player);
                }

                foreach (var saveManager in root.GetComponentsInChildren<SaveManager>(true))
                {
                    saveManager.Configure(_context.SaveService);
                }
            }
        }
    }
}
