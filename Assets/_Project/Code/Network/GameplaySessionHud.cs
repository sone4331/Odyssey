using UnityEngine;
using UnityEngine.InputSystem;

namespace Odyssey.Networking
{
    /// <summary>
    /// 提供 Level_01 的中文模式选择与轻量网络诊断，只调用会话门面，不参与任何玩法或权威规则。
    /// 使用自包含 IMGUI 让自动搭建后无需手工配置 Canvas，范围仅限作品集启动与调试入口。
    /// </summary>
    [RequireComponent(typeof(GameplaySessionController))]
    public sealed class GameplaySessionHud : MonoBehaviour
    {
        private GameplaySessionController _session;
        private string _address = "127.0.0.1";
        private GUIStyle _title;
        private GUIStyle _label;
        private GUIStyle _box;

        private void Awake()
        {
            _session = GetComponent<GameplaySessionController>();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            if (_session.IsStarted || Keyboard.current == null)
            {
                return;
            }

            // 数字快捷键既方便本机双开时快速切换窗口，也让没有可访问性控件的 IMGUI 启动页可稳定验收。
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                _session.StartSinglePlayer();
            }
            else if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                _session.StartHost();
            }
            else if (Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                _session.StartClient(_address);
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (!_session.IsStarted)
            {
                HandleGuiShortcut(Event.current);
                DrawLauncher();
                return;
            }

            DrawDiagnostics();
        }

        private void HandleGuiShortcut(Event current)
        {
            if (current == null || current.type != EventType.KeyDown)
            {
                return;
            }

            switch (current.keyCode)
            {
                case KeyCode.Alpha1:
                case KeyCode.Keypad1:
                    _session.StartSinglePlayer();
                    current.Use();
                    break;
                case KeyCode.Alpha2:
                case KeyCode.Keypad2:
                    _session.StartHost();
                    current.Use();
                    break;
                case KeyCode.Alpha3:
                case KeyCode.Keypad3:
                    _session.StartClient(_address);
                    current.Use();
                    break;
            }
        }

        private void DrawLauncher()
        {
            GUILayout.BeginArea(new Rect(20f, 20f, 430f, 300f), _box);
            GUILayout.Label("Odyssey 游戏模式", _title);
            GUILayout.Label("单机与双人合作使用同一个 Level_01。", _label);
            GUILayout.Label("快捷键：1 单机 / 2 Host / 3 加入默认地址", _label);
            GUILayout.Space(10f);
            if (GUILayout.Button("开始单机游戏", GUILayout.Height(38f)))
            {
                _session.StartSinglePlayer();
            }

            if (GUILayout.Button("创建双人 Host", GUILayout.Height(38f)))
            {
                _session.StartHost();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Host IPv4：", GUILayout.Width(90f));
            _address = GUILayout.TextField(_address, GUILayout.Height(30f));
            GUILayout.EndHorizontal();
            if (GUILayout.Button("加入双人 Client", GUILayout.Height(38f)))
            {
                _session.StartClient(_address);
            }

            if (!GameplaySessionController.IsValidIpv4(_address))
            {
                GUILayout.Label("请输入有效 IPv4；本机双开使用 127.0.0.1。", _label);
            }

            GUILayout.Label($"状态：{_session.StatusText}", _label);
            GUILayout.EndArea();
        }

        private void DrawDiagnostics()
        {
            GUILayout.BeginArea(new Rect(Screen.width - 320f, 20f, 300f, 190f), _box);
            GUILayout.Label(_session.StatusText, _label);
            if (_session.IsMultiplayer)
            {
                GUILayout.Label($"RTT：{_session.GetRoundTripTimeMilliseconds()} ms", _label);
                GUILayout.Label("战斗、AI、生命与门禁：Host 权威", _label);
                var localPlayer = FindLocalPlayer();
                if (localPlayer != null)
                {
                    GUILayout.Label($"权威生命：{localPlayer.CurrentHealth}", _label);
                    GUILayout.Label($"最近攻击序号：{localPlayer.LastAttackSequence}", _label);
                    GUILayout.Label(
                        localPlayer.LastAttackSequence == 0
                            ? "Host 校验：尚未提交攻击"
                            : localPlayer.LastAttackAccepted
                                ? "Host 校验：Host 已接受"
                                : localPlayer.LastAttackRejection == NetworkAttackRejection.None
                                    ? "Host 校验：等待响应"
                                    : $"Host 校验：{NetworkAttackRules.ToChinese(localPlayer.LastAttackRejection)}",
                        _label);
                }
            }

            GUILayout.EndArea();
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

        private void EnsureStyles()
        {
            if (_title != null)
            {
                return;
            }

            _title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            _box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(16, 16, 14, 14) };
        }
    }
}
