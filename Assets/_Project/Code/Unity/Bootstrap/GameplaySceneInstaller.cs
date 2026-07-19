using System;
using Odyssey.Characters.Enemies;
using Odyssey.Characters.Player;
using Odyssey.Systems;
using Odyssey.Unity.Config;
using UnityEngine;

namespace Odyssey.Bootstrap
{
    /// <summary>
    /// 作为单个玩法场景的 Composition Root，把应用级服务注入该场景中的玩家与存档适配器。
    /// 采用 Installer 模式使 Bootstrap 不依赖具体业务组件；当前不创建空壳 Session，待联网出现真实会话状态后再引入。
    /// </summary>
    public sealed class GameplaySceneInstaller : MonoBehaviour
    {
        private ApplicationContext _context;
        public ApplicationContext Context => _context;

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
            InstallSceneTargets();
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
                    GameConfigBinder.Bind(_context.Configs, player.ConfigId, player);
                }

                foreach (var enemy in root.GetComponentsInChildren<Enemy>(true))
                {
                    GameConfigBinder.Bind(_context.Configs, enemy.ConfigId, enemy);
                }

                foreach (var saveManager in root.GetComponentsInChildren<SaveManager>(true))
                {
                    saveManager.Configure(_context.SaveService);
                }
            }
        }

        /// <summary>
        /// 为 NGO 在场景启动后生成的玩家补做配置和存档装配；仍由场景 Composition Root 持有应用服务。
        /// </summary>
        public void InstallRuntimePlayer(PlayerController player)
        {
            if (_context == null || player == null)
            {
                return;
            }

            GameConfigBinder.Bind(_context.Configs, player.ConfigId, player);
        }
    }
}
