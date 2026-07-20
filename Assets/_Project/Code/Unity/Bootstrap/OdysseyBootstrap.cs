using Odyssey.Gameplay.Save;
using Odyssey.Unity.Config;
using Odyssey.Unity.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Odyssey.Bootstrap
{
    /// <summary>
    /// 作为应用级 Composition Root 创建跨场景配置与存档服务，并把具体场景装配委托给 Installer。
    /// 集中装配可避免角色自行访问全局单例或 Resources；应用与场景生命周期的边界只存在于该 Unity 入口。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class OdysseyBootstrap : MonoBehaviour
    {
        private const string ConfigResourcePath = "Config/GameConfigDatabase";

        private GameConfigAsset _configs;
        private ApplicationContext _context;

        /// <summary>
        /// 在首个场景加载前创建唯一应用组合根；静态入口只创建 GameObject，不承载任何业务服务。
        /// 这样场景可以保持可编辑，应用级对象又能跨场景存活。
        /// </summary>
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

        /// <summary>
        /// 读取只读配置并创建存档服务，再登记场景回调；配置缺失时停止装配，避免角色带着半初始化依赖运行。
        /// </summary>
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            _configs = Resources.Load<GameConfigAsset>(ConfigResourcePath);
            if (_configs == null)
            {
                Debug.LogError($"Resources/{ConfigResourcePath} 缺少运行时配置。");
                return;
            }

            var savePath = System.IO.Path.Combine(Application.persistentDataPath, "SaveData.json");
            var saveService = new AtomicFileSaveService<PlayerSaveData>(
                savePath,
                new JsonSaveCodec<PlayerSaveData>());
            _context = new ApplicationContext(_configs, saveService);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            InstallScene(SceneManager.GetActiveScene());
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InstallScene(scene);
        }

        /// <summary>
        /// 为目标场景查找或创建唯一 Installer，并显式交付应用上下文。
        /// Bootstrap 只了解场景装配入口，不再直接依赖玩家、UI 或其他具体业务组件。
        /// </summary>
        private void InstallScene(Scene scene)
        {
            if (_context == null || !scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            GameplaySceneInstaller installer = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                installer = root.GetComponentInChildren<GameplaySceneInstaller>(true);
                if (installer != null)
                {
                    break;
                }
            }

            if (installer == null)
            {
                var installerRoot = new GameObject("[玩法场景安装器]");
                SceneManager.MoveGameObjectToScene(installerRoot, scene);
                installer = installerRoot.AddComponent<GameplaySceneInstaller>();
            }

            installer.Install(_context);
        }
    }
}
