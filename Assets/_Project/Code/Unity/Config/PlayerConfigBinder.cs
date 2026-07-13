using System;
using Odyssey.Gameplay.Config;

namespace Odyssey.Unity.Config
{
    /// <summary>
    /// 定义能够接收纯 C# 玩家配置的运行时目标，避免配置适配器依赖具体 PlayerController。
    /// </summary>
    public interface IPlayerConfigTarget
    {
        void Apply(PlayerConfigData config);
    }

    /// <summary>
    /// 从配置端口解析指定玩家记录并应用到运行时目标，是 Composition Root 使用的绑定服务。
    /// 采用依赖倒置与 Parameter Object，集中处理配置选择，避免角色脚本自行查找全局资产。
    /// </summary>
    public static class PlayerConfigBinder
    {
        public static void Bind(
            IGameConfigProvider provider,
            string configId,
            IPlayerConfigTarget target)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (string.IsNullOrWhiteSpace(configId)) throw new ArgumentException("Config id is required.", nameof(configId));
            if (target == null) throw new ArgumentNullException(nameof(target));

            target.Apply(provider.Get<PlayerConfigData>(configId));
        }
    }
}
