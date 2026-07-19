using System.Collections;
using Odyssey.Systems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Odyssey.Networking
{
    /// <summary>
    /// 统一管理启动主菜单、联机页、ESC 菜单、设置页和网络调试页。
    /// 采用单一场景控制器：所有光标、TimeScale 与本机输入副作用只在这里提交，避免多个 Manager 互相覆盖。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameMenuController : MonoBehaviour
    {
        private const string VolumeKey = "设置.主音量";
        private const string SensitivityKey = "设置.镜头灵敏度";
        private const string FullscreenKey = "设置.全屏";

        [Header("依赖")]
        [SerializeField] private GameplaySessionController session;
        [SerializeField] private GameplayLocalViewBinder localViewBinder;
        [SerializeField] private SaveManager saveManager;

        [Header("页面")]
        [SerializeField] private GameObject mainMenuPage;
        [SerializeField] private GameObject networkMenuPage;
        [SerializeField] private GameObject pauseMenuPage;
        [SerializeField] private GameObject settingsPage;
        [SerializeField] private GameObject diagnosticsPanel;

        [Header("主菜单")]
        [SerializeField] private Button singlePlayerButton;
        [SerializeField] private Button networkButton;
        [SerializeField] private Button mainSettingsButton;
        [SerializeField] private Button mainQuitButton;

        [Header("联机菜单")]
        [SerializeField] private Button hostButton;
        [SerializeField] private Button clientButton;
        [SerializeField] private Button networkBackButton;
        [SerializeField] private InputField addressInput;
        [SerializeField] private Text networkStatusText;

        [Header("ESC 菜单")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseSettingsButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private Text leaveButtonText;
        [SerializeField] private Button pauseQuitButton;
        [SerializeField] private Text pauseStatusText;

        [Header("设置")]
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Button settingsBackButton;

        [Header("调试")]
        [SerializeField] private Text diagnosticsText;

        private GameMenuPage _currentPage;
        private GameMenuPage _settingsReturnPage;
        private Font _chineseFont;
        private float _nextDiagnosticsRefresh;
        private bool _reloadingScene;
        private bool _hadGameplaySession;

        public GameMenuPage CurrentPage => _currentPage;
        public bool IsMenuVisible => _currentPage != GameMenuPage.Gameplay;

        private void Awake()
        {
            ResolveDependencies();
            ApplyChineseFont();
            RegisterControls();
            LoadAndApplySettings();
            diagnosticsPanel?.SetActive(false);
            ShowMainMenu();
        }

        private void OnEnable()
        {
            if (session != null)
            {
                session.StatusChanged += HandleSessionStatusChanged;
            }
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.StatusChanged -= HandleSessionStatusChanged;
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame && session != null && session.IsGameplayReady)
            {
                diagnosticsPanel?.SetActive(!(diagnosticsPanel?.activeSelf ?? false));
                RefreshDiagnostics();
            }

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                HandleEscape();
            }

            if (diagnosticsPanel != null && diagnosticsPanel.activeSelf && Time.unscaledTime >= _nextDiagnosticsRefresh)
            {
                _nextDiagnosticsRefresh = Time.unscaledTime + 0.25f;
                RefreshDiagnostics();
            }
        }

        public void ShowMainMenu()
        {
            // 玩家从“正在加入房间”返回时必须取消连接，避免主菜单停留期间异步连接成功并突然进入游戏。
            if (session != null && session.IsStarted && !session.IsGameplayReady)
            {
                session.Shutdown();
            }

            SetPage(GameMenuPage.MainMenu);
            SetGameplayState(false, true);
            Select(singlePlayerButton);
        }

        public void ShowNetworkMenu()
        {
            SetPage(GameMenuPage.NetworkMenu);
            SetNetworkStatus("本机双开请输入 127.0.0.1；局域网请输入房主 IPv4。 ");
            Select(hostButton);
        }

        public void StartSinglePlayer()
        {
            SetNetworkStatus(string.Empty);
            if (session == null || !session.StartSinglePlayer())
            {
                SetNetworkStatus("单人游戏启动失败，请查看控制台错误。 ");
            }
        }

        public void StartHost()
        {
            SetNetworkStatus("正在创建房间……");
            if (session == null || !session.StartHost())
            {
                SetNetworkStatus("创建房间失败，请检查 UDP 7777 端口是否被占用。 ");
            }
        }

        public void StartClient()
        {
            var address = addressInput == null ? string.Empty : addressInput.text;
            if (!GameplaySessionController.IsValidIpv4(address))
            {
                SetNetworkStatus("请输入有效的 IPv4 地址；本机双开使用 127.0.0.1。 ");
                return;
            }

            SetNetworkStatus("正在加入房间……");
            if (session == null || !session.StartClient(address))
            {
                SetNetworkStatus("无法发起连接，请检查地址、端口和防火墙。 ");
            }
        }

        public void OpenPauseMenu()
        {
            if (session == null || !session.IsGameplayReady)
            {
                return;
            }

            var singlePlayer = session.Mode == GameplaySessionMode.SinglePlayer;
            saveButton?.gameObject.SetActive(singlePlayer);
            loadButton?.gameObject.SetActive(singlePlayer);
            if (leaveButtonText != null)
            {
                leaveButtonText.text = singlePlayer ? "返回主菜单" : "离开联机";
            }

            SetPauseStatus(string.Empty);
            SetPage(GameMenuPage.PauseMenu);
            SetGameplayState(false, singlePlayer);
            Select(resumeButton);
        }

        public void ResumeGame()
        {
            if (session == null || !session.IsGameplayReady)
            {
                return;
            }

            SetPage(GameMenuPage.Gameplay);
            SetGameplayState(true, false);
        }

        public void OpenSettingsFromMainMenu()
        {
            OpenSettings(GameMenuPage.MainMenu);
        }

        public void OpenSettingsFromPauseMenu()
        {
            OpenSettings(GameMenuPage.PauseMenu);
        }

        public void BackFromSettings()
        {
            PlayerPrefs.Save();
            if (_settingsReturnPage == GameMenuPage.PauseMenu)
            {
                OpenPauseMenu();
                return;
            }

            ShowMainMenu();
        }

        public void SaveGame()
        {
            SetPauseStatus(saveManager != null && saveManager.SaveGame() ? "保存成功。" : "保存失败，请查看控制台。 ");
        }

        public void LoadGame()
        {
            SetPauseStatus(saveManager != null && saveManager.LoadGame() ? "读取成功。" : "没有可读取的有效存档。 ");
        }

        public void LeaveSession()
        {
            if (!_reloadingScene)
            {
                StartCoroutine(ReloadLevelToMainMenu());
            }
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OpenSettings(GameMenuPage returnPage)
        {
            _settingsReturnPage = returnPage;
            SetPage(GameMenuPage.Settings);
            SetGameplayState(false, returnPage == GameMenuPage.MainMenu ||
                                    session != null && session.Mode == GameplaySessionMode.SinglePlayer);
            Select(settingsBackButton);
        }

        private void HandleEscape()
        {
            switch (_currentPage)
            {
                case GameMenuPage.Gameplay:
                    OpenPauseMenu();
                    break;
                case GameMenuPage.PauseMenu:
                    ResumeGame();
                    break;
                case GameMenuPage.Settings:
                    BackFromSettings();
                    break;
                case GameMenuPage.NetworkMenu:
                    ShowMainMenu();
                    break;
            }
        }

        private void HandleSessionStatusChanged()
        {
            if (session == null)
            {
                return;
            }

            SetNetworkStatus(session.StatusText);
            if (session.IsGameplayReady)
            {
                _hadGameplaySession = true;
                SetPage(GameMenuPage.Gameplay);
                SetGameplayState(true, false);
                return;
            }

            // 连接失败时留在联机页供玩家修正地址；游戏中断线则重载关卡，彻底清理旧网络对象。
            if (session.Mode == GameplaySessionMode.None && _hadGameplaySession && !_reloadingScene)
            {
                StartCoroutine(ReloadLevelToMainMenu());
            }
        }

        private IEnumerator ReloadLevelToMainMenu()
        {
            _reloadingScene = true;
            SetGameplayState(false, false);
            session?.Shutdown();
            Time.timeScale = 1f;
            yield return null;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex, LoadSceneMode.Single);
        }

        /// <summary>
        /// 原子地提交菜单所需的光标、玩家输入、镜头输入和暂停状态，避免一半已暂停、一半仍接收输入。
        /// 联机菜单调用时 pauseWorld 始终为 false，因此不会暂停 Host、AI 或另一名玩家。
        /// </summary>
        private void SetGameplayState(bool gameplayInput, bool pauseWorld)
        {
            localViewBinder?.SetGameplayInputEnabled(gameplayInput);
            Cursor.lockState = gameplayInput ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !gameplayInput;
            Time.timeScale = pauseWorld ? 0f : 1f;
        }

        private void SetPage(GameMenuPage page)
        {
            _currentPage = page;
            mainMenuPage?.SetActive(page == GameMenuPage.MainMenu);
            networkMenuPage?.SetActive(page == GameMenuPage.NetworkMenu);
            pauseMenuPage?.SetActive(page == GameMenuPage.PauseMenu);
            settingsPage?.SetActive(page == GameMenuPage.Settings);
        }

        private void RegisterControls()
        {
            singlePlayerButton?.onClick.AddListener(StartSinglePlayer);
            networkButton?.onClick.AddListener(ShowNetworkMenu);
            mainSettingsButton?.onClick.AddListener(OpenSettingsFromMainMenu);
            mainQuitButton?.onClick.AddListener(QuitGame);
            hostButton?.onClick.AddListener(StartHost);
            clientButton?.onClick.AddListener(StartClient);
            networkBackButton?.onClick.AddListener(ShowMainMenu);
            resumeButton?.onClick.AddListener(ResumeGame);
            pauseSettingsButton?.onClick.AddListener(OpenSettingsFromPauseMenu);
            saveButton?.onClick.AddListener(SaveGame);
            loadButton?.onClick.AddListener(LoadGame);
            leaveButton?.onClick.AddListener(LeaveSession);
            pauseQuitButton?.onClick.AddListener(QuitGame);
            settingsBackButton?.onClick.AddListener(BackFromSettings);
            volumeSlider?.onValueChanged.AddListener(ApplyVolume);
            sensitivitySlider?.onValueChanged.AddListener(ApplySensitivity);
            fullscreenToggle?.onValueChanged.AddListener(ApplyFullscreen);
        }

        private void LoadAndApplySettings()
        {
            var volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
            var sensitivity = PlayerPrefs.GetFloat(SensitivityKey, 1f);
            var fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
            volumeSlider?.SetValueWithoutNotify(volume);
            sensitivitySlider?.SetValueWithoutNotify(sensitivity);
            fullscreenToggle?.SetIsOnWithoutNotify(fullscreen);
            ApplyVolume(volume);
            ApplySensitivity(sensitivity);
            ApplyFullscreen(fullscreen);
        }

        private void ApplyVolume(float value)
        {
            var volume = Mathf.Clamp01(value);
            AudioListener.volume = volume;
            PlayerPrefs.SetFloat(VolumeKey, volume);
        }

        private void ApplySensitivity(float value)
        {
            var sensitivity = Mathf.Clamp(value, 0.2f, 2f);
            localViewBinder?.SetCameraSensitivity(sensitivity);
            PlayerPrefs.SetFloat(SensitivityKey, sensitivity);
        }

        private static void ApplyFullscreen(bool fullscreen)
        {
            Screen.fullScreen = fullscreen;
            PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
        }

        private void RefreshDiagnostics()
        {
            if (diagnosticsText == null || session == null)
            {
                return;
            }

            var localPlayer = FindLocalPlayer();
            diagnosticsText.text =
                $"模式：{ToChinese(session.Mode)}\n" +
                $"状态：{session.StatusText}\n" +
                $"连接人数：{session.ConnectedPlayerCount}/{GameplaySessionController.MaximumPlayers}\n" +
                $"RTT：{session.GetRoundTripTimeMilliseconds()} ms\n" +
                $"本机权威生命：{(localPlayer == null ? "--" : localPlayer.CurrentHealth.ToString())}\n" +
                "F3：隐藏网络调试";
        }

        private static NetworkPlayerAdapter FindLocalPlayer()
        {
            foreach (var player in FindObjectsByType<NetworkPlayerAdapter>(FindObjectsSortMode.None))
            {
                if (player.IsOwner)
                {
                    return player;
                }
            }

            return null;
        }

        private static string ToChinese(GameplaySessionMode mode)
        {
            return mode switch
            {
                GameplaySessionMode.SinglePlayer => "单人游戏",
                GameplaySessionMode.Host => "创建房间",
                GameplaySessionMode.Client => "加入房间",
                _ => "未开始"
            };
        }

        private void ResolveDependencies()
        {
            session ??= GetComponent<GameplaySessionController>();
            localViewBinder ??= GetComponent<GameplayLocalViewBinder>();
            saveManager ??= GetComponent<SaveManager>();
        }

        private void ApplyChineseFont()
        {
            _chineseFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" },
                32);
            _chineseFont ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            foreach (var label in GetComponentsInChildren<Text>(true))
            {
                label.font = _chineseFont;
            }
        }

        private void SetNetworkStatus(string message)
        {
            if (networkStatusText != null)
            {
                networkStatusText.text = message?.TrimEnd() ?? string.Empty;
            }
        }

        private void SetPauseStatus(string message)
        {
            if (pauseStatusText != null)
            {
                pauseStatusText.text = message?.TrimEnd() ?? string.Empty;
            }
        }

        private static void Select(Selectable selectable)
        {
            if (selectable != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            }
        }
    }

    /// <summary>
    /// 菜单只保留玩家能感知的四类页面与游戏状态，避免为简单 UI 引入通用路由框架。
    /// </summary>
    public enum GameMenuPage
    {
        Gameplay,
        MainMenu,
        NetworkMenu,
        PauseMenu,
        Settings
    }
}
