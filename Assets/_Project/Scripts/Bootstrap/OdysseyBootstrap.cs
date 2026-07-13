using Odyssey.Characters.Player;
using Odyssey.Unity.Config;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Odyssey.Bootstrap
{
    /// <summary>
    /// 作为 Composition Root 创建跨场景服务，并在场景加载后完成配置到角色的依赖绑定。
    /// 集中装配可避免角色自行访问全局单例或 Resources；当前 Resources 查找只存在于该 Unity 边界。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class OdysseyBootstrap : MonoBehaviour
    {
        private const string ConfigResourcePath = "Config/GameConfigDatabase";

        private GameConfigAsset _configs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<OdysseyBootstrap>() != null)
            {
                return;
            }

            var root = new GameObject("[Odyssey Bootstrap]");
            DontDestroyOnLoad(root);
            root.AddComponent<OdysseyBootstrap>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            _configs = Resources.Load<GameConfigAsset>(ConfigResourcePath);
            if (_configs == null)
            {
                Debug.LogError($"Resources/{ConfigResourcePath} 缺少运行时配置。");
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            BindPlayers();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            BindPlayers();
        }

        /// <summary>
        /// 在场景稳定后查找配置目标并一次性注入只读配置。
        /// 绑定失败不应静默回退到另一条数据源，缺失配置由 Provider 明确抛错以暴露构建问题。
        /// </summary>
        private void BindPlayers()
        {
            if (_configs == null)
            {
                return;
            }

            var players = FindObjectsByType<PlayerController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (var player in players)
            {
                PlayerConfigBinder.Bind(_configs, player.ConfigId, player);
            }
        }
    }
}
