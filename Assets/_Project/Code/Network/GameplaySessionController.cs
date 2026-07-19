using System;
using System.Net;
using Odyssey.Bootstrap;
using Odyssey.Characters.Player;
using Unity.Netcode;
using Unity.Netcode.Transports.SinglePlayer;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Odyssey.Networking
{
    /// <summary>
    /// 表示 Level_01 当前采用的运行模式，明确区分未启动、无端口单机、Host 与 Client 生命周期。
    /// </summary>
    public enum GameplaySessionMode
    {
        None,
        SinglePlayer,
        Host,
        Client
    }

    /// <summary>
    /// 负责原关卡的单机、Host 和 Client 启动、连接审批、出生点与本机玩家装配。
    /// 采用场景级 Facade 和 Composition Root；它不是单例，且不会把 NGO API 泄漏给玩家、AI 或 UI。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport), typeof(SinglePlayerTransport))]
    public sealed class GameplaySessionController : MonoBehaviour
    {
        public const ushort DefaultPort = 7777;
        public const int MaximumPlayers = 2;

        [SerializeField] private Transform[] spawnPoints = Array.Empty<Transform>();
        [SerializeField] private GameplayLocalViewBinder localViewBinder;

        private NetworkManager _manager;
        private UnityTransport _unityTransport;
        private SinglePlayerTransport _singlePlayerTransport;
        private bool _shutdownRequested;

        public event Action StatusChanged;
        public GameplaySessionMode Mode { get; private set; }
        public bool IsStarted => _manager != null && _manager.IsListening;
        public bool IsMultiplayer => Mode == GameplaySessionMode.Host || Mode == GameplaySessionMode.Client;
        public bool IsAuthority => _manager != null && _manager.IsServer;
        public NetworkManager Manager => _manager;
        public string LastDisconnectReason { get; private set; } = string.Empty;
        public int ConnectedPlayerCount => _manager == null || !_manager.IsListening
            ? 0
            : _manager.IsServer ? _manager.ConnectedClientsIds.Count : (_manager.IsConnectedClient ? 1 : 0);

        public string StatusText
        {
            get
            {
                switch (Mode)
                {
                    case GameplaySessionMode.SinglePlayer:
                        return IsStarted ? "单机游戏运行中" : "正在启动单机游戏";
                    case GameplaySessionMode.Host:
                        return IsStarted ? $"Host 运行中（{ConnectedPlayerCount}/{MaximumPlayers}）" : "正在创建 Host";
                    case GameplaySessionMode.Client:
                        return _manager != null && _manager.IsConnectedClient ? "Client 已连接" : "Client 正在连接";
                    default:
                        return string.IsNullOrWhiteSpace(LastDisconnectReason)
                            ? "请选择游戏模式"
                            : LastDisconnectReason;
                }
            }
        }

        private void Awake()
        {
            Application.runInBackground = true;
            Time.timeScale = 1f;
            _manager = GetComponent<NetworkManager>();
            _unityTransport = GetComponent<UnityTransport>();
            _singlePlayerTransport = GetComponent<SinglePlayerTransport>();
        }

        private void OnEnable()
        {
            _manager.OnClientConnectedCallback += HandleClientConnected;
            _manager.OnClientDisconnectCallback += HandleClientDisconnected;
            _manager.OnTransportFailure += HandleTransportFailure;
        }

        private void OnDisable()
        {
            if (_manager == null)
            {
                return;
            }

            _manager.OnClientConnectedCallback -= HandleClientConnected;
            _manager.OnClientDisconnectCallback -= HandleClientDisconnected;
            _manager.OnTransportFailure -= HandleTransportFailure;
            _manager.ConnectionApprovalCallback = null;
        }

        /// <summary>
        /// 使用 NGO 原生单机传输启动一个封闭的一人 Host，使单机与联机共享同一关卡对象生命周期且不占用 UDP 端口。
        /// </summary>
        public bool StartSinglePlayer()
        {
            if (!PrepareStart(GameplaySessionMode.SinglePlayer, _singlePlayerTransport))
            {
                return false;
            }

            _manager.NetworkConfig.ConnectionApproval = true;
            _manager.ConnectionApprovalCallback = ApproveConnection;
            return CompleteStart(_manager.StartHost());
        }

        public bool StartHost()
        {
            if (!PrepareStart(GameplaySessionMode.Host, _unityTransport))
            {
                return false;
            }

            _unityTransport.SetConnectionData("127.0.0.1", DefaultPort, "0.0.0.0");
            _manager.NetworkConfig.ConnectionApproval = true;
            _manager.ConnectionApprovalCallback = ApproveConnection;
            return CompleteStart(_manager.StartHost());
        }

        public bool StartClient(string ipv4)
        {
            if (!IsValidIpv4(ipv4) || !PrepareStart(GameplaySessionMode.Client, _unityTransport))
            {
                return false;
            }

            _unityTransport.SetConnectionData(ipv4.Trim(), DefaultPort);
            return CompleteStart(_manager.StartClient());
        }

        public void Shutdown()
        {
            _shutdownRequested = true;
            if (_manager != null && _manager.IsListening)
            {
                _manager.Shutdown();
            }

            Mode = GameplaySessionMode.None;
            Time.timeScale = 1f;
            StatusChanged?.Invoke();
        }

        public Vector3 GetSpawnPosition(ulong clientId)
        {
            var spawnPoint = GetSpawnPoint(clientId);
            if (spawnPoint != null)
            {
                return spawnPoint.position;
            }

            return clientId % 2 == 0 ? new Vector3(-1f, 0.1f, 0f) : new Vector3(1f, 0.1f, 0f);
        }

        /// <summary>
        /// 返回场景中实际的出生点引用，供单机玩家恢复原 PlayerController 的复活语义。
        /// 联机仍只由 Host 读取其位置，不把场景 Transform 作为网络状态复制。
        /// </summary>
        public Transform GetSpawnPoint(ulong clientId)
        {
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                var index = (int)(clientId % (ulong)spawnPoints.Length);
                if (spawnPoints[index] != null)
                {
                    return spawnPoints[index];
                }
            }

            return null;
        }

        public void RegisterLocalPlayer(PlayerController player)
        {
            if (player == null)
            {
                return;
            }

            var installer = FindFirstObjectByType<GameplaySceneInstaller>();
            installer?.InstallRuntimePlayer(player);
            localViewBinder?.Bind(player, IsMultiplayer, installer?.Context);
            StatusChanged?.Invoke();
        }

        public ulong GetRoundTripTimeMilliseconds()
        {
            if (_manager == null || !_manager.IsListening || Mode == GameplaySessionMode.SinglePlayer)
            {
                return 0;
            }

            return _manager.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId);
        }

        public static bool IsValidIpv4(string value)
        {
            return IPAddress.TryParse(value, out var address) &&
                   address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
        }

        private bool PrepareStart(GameplaySessionMode mode, NetworkTransport transport)
        {
            if (_manager == null || _manager.IsListening || transport == null)
            {
                return false;
            }

            _shutdownRequested = false;
            LastDisconnectReason = string.Empty;
            Mode = mode;
            _manager.NetworkConfig.NetworkTransport = transport;
            StatusChanged?.Invoke();
            return true;
        }

        private bool CompleteStart(bool started)
        {
            if (!started)
            {
                Mode = GameplaySessionMode.None;
            }

            StatusChanged?.Invoke();
            return started;
        }

        private void ApproveConnection(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            var capacity = Mode == GameplaySessionMode.SinglePlayer ? 1 : MaximumPlayers;
            var approved = _manager.ConnectedClientsIds.Count < capacity;
            response.Approved = approved;
            response.CreatePlayerObject = approved;
            response.PlayerPrefabHash = null;
            response.Position = GetSpawnPosition(request.ClientNetworkId);
            response.Rotation = Quaternion.identity;
            response.Pending = false;
            response.Reason = approved ? string.Empty : "合作房间已满，最多支持两名玩家。";
        }

        private void HandleClientConnected(ulong _)
        {
            StatusChanged?.Invoke();
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (!_shutdownRequested && Mode == GameplaySessionMode.Client && clientId == _manager.LocalClientId)
            {
                LastDisconnectReason = string.IsNullOrWhiteSpace(_manager.DisconnectReason)
                    ? "与 Host 的连接已断开。"
                    : _manager.DisconnectReason;
                Mode = GameplaySessionMode.None;
            }

            StatusChanged?.Invoke();
        }

        private void HandleTransportFailure()
        {
            Debug.LogError("联机传输失败，请检查 UDP 7777 端口、防火墙和局域网地址。", this);
            LastDisconnectReason = "联机传输失败，请检查 UDP 7777 端口、防火墙和局域网地址。";
            Mode = GameplaySessionMode.None;
            StatusChanged?.Invoke();
        }
    }
}
