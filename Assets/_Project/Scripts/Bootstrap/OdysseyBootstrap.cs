using Odyssey.Characters.Player;
using Odyssey.Unity.Config;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Odyssey.Bootstrap
{
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
                Debug.LogError($"Missing runtime config at Resources/{ConfigResourcePath}.");
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
