using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace Odyssey.Networking
{
    /// <summary>
    /// 提供 NetworkArena 的连接入口、运行状态和 Host 校验结果展示。
    /// 使用场景内 IMGUI 是为了让本机双开零配置可验收；它只承担调试与演示表现，不参与任何网络规则。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkSessionController))]
    public sealed class NetworkArenaHud : MonoBehaviour
    {
        private NetworkSessionController _session;
        private string _address = "127.0.0.1";
        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _boxStyle;

        private void Awake()
        {
            _session = GetComponent<NetworkSessionController>();
            _address = _session.Address;
        }

        private void OnGUI()
        {
            EnsureStyles();
            GUILayout.BeginArea(new Rect(20f, 20f, 390f, 330f), _boxStyle);
            GUILayout.Label("Odyssey 本地联机竞技场", _titleStyle);
            GUILayout.Space(8f);
            GUILayout.Label($"状态：{_session.StatusText}", _labelStyle);

            if (!_session.IsListening)
            {
                DrawConnectionControls();
            }
            else
            {
                DrawRuntimeDiagnostics();
            }

            GUILayout.Space(8f);
            GUILayout.Label("操作：WASD 移动，J / 鼠标左键攻击", _labelStyle);
            GUILayout.Label("规则：Host 决定位置、命中、伤害、死亡与复活", _labelStyle);
            GUILayout.EndArea();
        }

        private void DrawConnectionControls()
        {
            GUILayout.Label("本机双开时两端都使用 127.0.0.1", _labelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Host IPv4：", GUILayout.Width(85f));
            _address = GUILayout.TextField(_address, GUILayout.Height(28f));
            GUILayout.EndHorizontal();
            GUILayout.Label($"UDP 端口：{_session.Port}", _labelStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("创建 Host", GUILayout.Height(36f)))
            {
                _session.StartHost();
            }

            if (GUILayout.Button("加入 Client", GUILayout.Height(36f)))
            {
                _session.Address = _address;
                _session.StartClient();
            }

            GUILayout.EndHorizontal();

            if (!NetworkSessionController.IsValidAddress(_address))
            {
                GUILayout.Label("请输入有效的 IPv4 地址。", _labelStyle);
            }
        }

        private void DrawRuntimeDiagnostics()
        {
            var manager = _session.Manager;
            GUILayout.Label($"连接人数：{_session.ConnectedPlayerCount}/2", _labelStyle);
            GUILayout.Label($"RTT：{_session.GetRoundTripTimeMilliseconds()} ms", _labelStyle);

            var localPlayer = FindObjectsByType<NetworkPlayerAvatar>(FindObjectsSortMode.None)
                .FirstOrDefault(player => player.IsOwner);
            if (localPlayer != null)
            {
                GUILayout.Label($"权威生命：{localPlayer.CurrentHealth}/{localPlayer.MaxHealth}", _labelStyle);
                GUILayout.Label($"本地攻击序号：{localPlayer.LastLocalAttackSequence}", _labelStyle);
                if (localPlayer.LastLocalAttackSequence > 0)
                {
                    var result = localPlayer.LastAttackAccepted
                        ? "Host 已接受"
                        : $"Host 拒绝：{NetworkAttackRules.ToChinese(localPlayer.LastAttackRejection)}";
                    GUILayout.Label($"最近请求：{result}", _labelStyle);
                }
            }

            if (GUILayout.Button("断开连接", GUILayout.Height(32f)))
            {
                _session.Shutdown();
            }

            if (!manager.IsHost && !manager.IsConnectedClient && !string.IsNullOrEmpty(manager.DisconnectReason))
            {
                GUILayout.Label($"断开原因：{manager.DisconnectReason}", _labelStyle);
            }
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null)
            {
                return;
            }

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(16, 16, 14, 14)
            };
        }
    }
}
