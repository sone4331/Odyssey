using System;
using Odyssey.Characters.Player;
using Odyssey.Gameplay.Save;
using Odyssey.Unity.Save;
using UnityEngine;
using UnityEngine.Serialization;

namespace Odyssey.Systems
{
    /// <summary>
    /// 定义当前玩家存档的版本化 DTO，只保存可序列化数据，不包含场景对象或业务行为。
    /// 通过显式版本字段支持后续迁移，避免将 Unity 组件结构直接固化到磁盘格式。
    /// </summary>
    [Serializable]
    public sealed class PlayerSaveData : IVersionedSave
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int health;
        public float posX;
        public float posY;
        public float posZ;

        public int Version
        {
            get => version;
            set => version = value;
        }
    }

    /// <summary>
    /// 作为 Unity 场景适配器收集与恢复玩家快照，并把持久化委托给 ISaveService。
    /// 采用 Facade 与 Adapter 模式兼容现有按钮绑定，同时将 JSON、原子写入和版本规则移出 MonoBehaviour。
    /// </summary>
    public sealed class SaveManager : MonoBehaviour
    {
        [Header("UI")]
        [FormerlySerializedAs("PauseMenuPanel")]
        [SerializeField] private GameObject pauseMenuPanel;

        [Header("Player")]
        [FormerlySerializedAs("Player")]
        [SerializeField] private PlayerController player;

        private bool _isPaused;
        private AtomicFileSaveService<PlayerSaveData> _saveService;

        private void Awake()
        {
            var path = System.IO.Path.Combine(Application.persistentDataPath, "SaveData.json");
            _saveService = new AtomicFileSaveService<PlayerSaveData>(path, new JsonSaveCodec<PlayerSaveData>());
        }

        private void Update()
        {
            // Pause input is migrated to InputReader in the player/input milestone.
            if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Escape))
            {
                SetPaused(!_isPaused);
            }
        }

        public void PauseGame() => SetPaused(true);
        public void ResumeGame() => SetPaused(false);

        public void SaveGame()
        {
            if (player == null)
            {
                Debug.LogError("Save failed: no player is assigned.", this);
                return;
            }

            var position = player.transform.position;
            _saveService.Save(new PlayerSaveData
            {
                health = player.CurrentHealth,
                posX = position.x,
                posY = position.y,
                posZ = position.z
            });

            Debug.Log("Game saved.", this);
            SetPaused(false);
        }

        public void LoadGame()
        {
            if (player == null)
            {
                Debug.LogError("Load failed: no player is assigned.", this);
                return;
            }

            if (!_saveService.TryLoad(out var data))
            {
                Debug.LogWarning("No valid save file was found.", this);
                return;
            }

            if (data.Version != PlayerSaveData.CurrentVersion)
            {
                Debug.LogError($"Unsupported save version {data.Version}.", this);
                return;
            }

            player.SetHealth(data.health, "load");
            var controller = player.Controller;
            controller.enabled = false;
            player.transform.position = new Vector3(data.posX, data.posY, data.posZ);
            controller.enabled = true;

            Debug.Log("Game loaded.", this);
            SetPaused(false);
        }

        // Kept temporarily so existing scene Button events remain valid during migration.
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

        private void SetPaused(bool paused)
        {
            _isPaused = paused;
            Time.timeScale = paused ? 0f : 1f;

            if (pauseMenuPanel != null)
            {
                pauseMenuPanel.SetActive(paused);
            }

            Cursor.visible = paused;
            Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
        }
    }
}
