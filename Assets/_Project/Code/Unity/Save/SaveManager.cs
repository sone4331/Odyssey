using Odyssey.Characters.Player;
using Odyssey.Gameplay.Save;
using Odyssey.Unity.Save;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Odyssey.Systems
{
    /// <summary>
    /// 保留场景按钮与旧方法名的兼容门面，把暂停副作用和玩家快照读写分别委托给独立运行时对象。
    /// 采用 Facade 模式维持现有场景 GUID 和 Button 引用，同时避免一个 MonoBehaviour 同时维护三类状态。
    /// </summary>
    public sealed class SaveManager : MonoBehaviour
    {
        [Header("界面")]
        [FormerlySerializedAs("PauseMenuPanel")]
        [SerializeField] private GameObject pauseMenuPanel;

        [Header("玩家")]
        [FormerlySerializedAs("Player")]
        [SerializeField] private PlayerController player;

        private PauseRuntime _pause;
        private PlayerSaveRuntime _playerSave;

        private void Awake()
        {
            _pause = new PauseRuntime(pauseMenuPanel);
        }

        /// <summary>
        /// 由场景 Installer 注入应用级存档端口，避免场景组件自行决定文件路径、编码器和服务生命周期。
        /// 重复注入同一实例保持幂等，便于 Bootstrap 的场景加载回调安全重入。
        /// </summary>
        public void Configure(ISaveService<PlayerSaveData> saveService)
        {
            _playerSave = new PlayerSaveRuntime(
                player,
                saveService ?? throw new System.ArgumentNullException(nameof(saveService)));
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.pKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
            {
                _pause.SetPaused(!_pause.IsPaused);
            }
        }

        private void OnDestroy()
        {
            _pause?.Dispose();
        }

        public void PauseGame() => _pause.SetPaused(true);
        public void ResumeGame() => _pause.SetPaused(false);

        public void SaveGame()
        {
            if (_playerSave == null)
            {
                Debug.LogError("保存失败：场景尚未注入存档服务。", this);
                return;
            }

            if (_playerSave.Save())
            {
                Debug.Log("游戏已保存。", this);
                _pause.SetPaused(false);
            }
        }

        public void LoadGame()
        {
            if (_playerSave == null)
            {
                Debug.LogError("读取失败：场景尚未注入存档服务。", this);
                return;
            }

            if (_playerSave.Load())
            {
                Debug.Log("游戏已读取。", this);
                _pause.SetPaused(false);
            }
        }

        // 暂时保留旧方法名，保证重构期间场景中的 Button 事件引用不失效。
        public void SaveGame_JSON() => SaveGame();
        public void LoadGame_JSON() => LoadGame();

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

    }
}
