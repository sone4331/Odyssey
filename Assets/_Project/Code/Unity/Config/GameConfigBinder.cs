using System;
using Odyssey.Gameplay.Config;

namespace Odyssey.Unity.Config
{
    /// <summary>
    /// 定义能够接收某一种纯 C# 配置记录的 Unity 运行时目标，使配置装配不依赖具体玩家或怪物类型。
    /// 采用泛型端口消除按角色复制 Binder 的重复代码，同时保留编译期类型检查。
    /// </summary>
    public interface IConfigTarget<in TConfig> where TConfig : class, IConfigRecord
    {
        void Apply(TConfig config);
    }

    /// <summary>
    /// 从只读配置仓库解析指定类型和 ID，再交给运行时目标应用，是场景 Composition Root 使用的统一绑定入口。
    /// 采用 Repository、依赖倒置与泛型方法，使角色不知道 CSV、ScriptableObject 或 Resources 的存在。
    /// </summary>
    public static class GameConfigBinder
    {
        public static void Bind<TConfig>(
            IGameConfigProvider provider,
            string configId,
            IConfigTarget<TConfig> target)
            where TConfig : class, IConfigRecord
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (string.IsNullOrWhiteSpace(configId)) throw new ArgumentException("必须提供配置 ID。", nameof(configId));
            if (target == null) throw new ArgumentNullException(nameof(target));

            target.Apply(provider.Get<TConfig>(configId));
        }
    }
}
