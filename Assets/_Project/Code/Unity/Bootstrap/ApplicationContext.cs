using System;
using Odyssey.Gameplay.Config;
using Odyssey.Gameplay.Save;

namespace Odyssey.Bootstrap
{
    /// <summary>
    /// 保存一次应用运行期间共享的基础服务，只暴露明确命名的端口，不提供按类型任意查询的 Service Locator。
    /// 采用显式依赖容器与依赖注入，使场景 Installer 能获得同一份配置和存档能力，同时让业务对象仍然声明真实依赖。
    /// </summary>
    public sealed class ApplicationContext
    {
        public ApplicationContext(
            IGameConfigProvider configs,
            ISaveService<PlayerSaveData> saveService)
        {
            Configs = configs ?? throw new ArgumentNullException(nameof(configs));
            SaveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
        }

        /// <summary>
        /// 获取应用级只读配置端口；具体数据可以来自 ScriptableObject 或测试内存数据库。
        /// </summary>
        public IGameConfigProvider Configs { get; }

        /// <summary>
        /// 获取应用级玩家存档端口；调用方不知道其底层使用本地 JSON、云存档还是测试替身。
        /// </summary>
        public ISaveService<PlayerSaveData> SaveService { get; }
    }
}
