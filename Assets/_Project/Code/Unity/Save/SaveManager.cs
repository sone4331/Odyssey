using Odyssey.Characters.Player;
using Odyssey.Gameplay.Save;
using Odyssey.Unity.Save;
using UnityEngine;

namespace Odyssey.Systems
{
    /// <summary>
    /// 负责单机玩家快照的保存与读取，只封装存档用例，不再监听 ESC 或控制光标、面板和 Time.timeScale。
    /// 采用 Facade 保留场景按钮可调用的稳定入口；菜单表现统一交给 GameMenuController，避免职责重叠。
    /// </summary>
    public sealed class SaveManager : MonoBehaviour
    {
        [SerializeField] private PlayerController player;

        private PlayerSaveRuntime _playerSave;
        private bool _multiplayerMode;

        public bool IsMultiplayerMode => _multiplayerMode;

        /// <summary>
        /// 由场景 Installer 注入应用级存档端口，避免本组件自行决定文件路径和序列化协议。
        /// </summary>
        public void Configure(ISaveService<PlayerSaveData> saveService)
        {
            if (player == null)
            {
                return;
            }

            _playerSave = new PlayerSaveRuntime(
                player,
                saveService ?? throw new System.ArgumentNullException(nameof(saveService)));
        }

        public bool SaveGame()
        {
            if (_multiplayerMode)
            {
                Debug.LogWarning("联机模式由 Host 维护关卡状态，不支持本地存档。", this);
                return false;
            }

            if (_playerSave == null)
            {
                Debug.LogError("保存失败：场景尚未注入存档服务或本机玩家尚未生成。", this);
                return false;
            }

            var saved = _playerSave.Save();
            Debug.Log(saved ? "游戏已保存。" : "保存失败，请查看控制台中的具体原因。", this);
            return saved;
        }

        public bool LoadGame()
        {
            if (_multiplayerMode)
            {
                Debug.LogWarning("联机模式不能读取单机快照，以免覆盖 Host 权威状态。", this);
                return false;
            }

            if (_playerSave == null)
            {
                Debug.LogError("读取失败：场景尚未注入存档服务或本机玩家尚未生成。", this);
                return false;
            }

            var loaded = _playerSave.Load();
            Debug.Log(loaded ? "游戏已读取。" : "没有可读取的有效存档。", this);
            return loaded;
        }

        // 保留旧方法名，避免外部场景或历史按钮引用在迁移期间变成 Missing Method。
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

        /// <summary>
        /// 运行时玩家生成后更新存档目标，单机据此恢复快照能力；联机仍拒绝本地读写。
        /// </summary>
        public void BindPlayer(PlayerController runtimePlayer, ISaveService<PlayerSaveData> saveService)
        {
            player = runtimePlayer;
            Configure(saveService);
        }

        public void SetMultiplayerMode(bool multiplayer)
        {
            _multiplayerMode = multiplayer;
        }
    }
}
