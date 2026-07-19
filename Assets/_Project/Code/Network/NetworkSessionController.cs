using System;
using System.Net;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Odyssey.Networking
{
    /// <summary>
    /// 负责 NetworkArena 场景内 Host/Client 的创建、直连地址配置与连接生命周期。
    /// 采用场景级 Facade：UI 只调用本类，不直接操作 NGO；它不是单例，离开联机场景后会随场景一起销毁。
    /// 这样既保留原生 NGO 的可读性，也避免把一次作品集演示扩展成匹配、账号或公网服务框架。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
    public sealed class NetworkSessionController : MonoBehaviour
    {
        public const ushort DefaultPort = 7777;
        public const int MaximumPlayers = 2;

        [SerializeField] private string address = "127.0.0.1";
        [SerializeField] private ushort port = DefaultPort;

        private NetworkManager _networkManager;
        private UnityTransport _transport;

        public event Action StatusChanged;

        public string Address
        {
            get => address;
            set => address = string.IsNullOrWhiteSpace(value) ? "127.0.0.1" : value.Trim();
        }

        public ushort Port => port;
        public NetworkManager Manager => _networkManager;
        public bool IsListening => _networkManager != null && _networkManager.IsListening;
        public int ConnectedPlayerCount => _networkManager != null && _networkManager.IsListening
            ? _networkManager.ConnectedClientsIds.Count
            : 0;

        public string StatusText
        {
            get
            {
                if (_networkManager == null || !_networkManager.IsListening)
                {
                    return "尚未启动联机会话";
                }

                if (_networkManager.IsHost)
                {
                    return $"Host 运行中（{ConnectedPlayerCount}/{MaximumPlayers}）";
                }

                return _networkManager.IsConnectedClient ? "Client 已连接" : "Client 正在连接";
            }
        }

        private void Awake()
        {
            Application.runInBackground = true;
            _networkManager = GetComponent<NetworkManager>();
            _transport = GetComponent<UnityTransport>();
        }

        private void OnEnable()
        {
            if (_networkManager == null)
            {
                return;
            }

            _networkManager.OnClientConnectedCallback += HandleClientChanged;
            _networkManager.OnClientDisconnectCallback += HandleClientChanged;
            _networkManager.OnTransportFailure += HandleTransportFailure;
        }

        private void OnDisable()
        {
            if (_networkManager == null)
            {
                return;
            }

            _networkManager.OnClientConnectedCallback -= HandleClientChanged;
            _networkManager.OnClientDisconnectCallback -= HandleClientChanged;
            _networkManager.OnTransportFailure -= HandleTransportFailure;
            _networkManager.ConnectionApprovalCallback = null;
        }

        /// <summary>
        /// 在本机同时启动 Server 与 Client，并监听所有网卡，使同一构建既支持 localhost 也支持局域网 IPv4。
        /// Host 权威并不等同于专用服务器；本方法只复用玩家机器作为服务器，符合无远程服务器的约束。
        /// </summary>
        public bool StartHost()
        {
            if (!CanStartSession())
            {
                return false;
            }

            _transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            _networkManager.NetworkConfig.ConnectionApproval = true;
            _networkManager.ConnectionApprovalCallback = ApproveConnection;
            var started = _networkManager.StartHost();
            StatusChanged?.Invoke();
            return started;
        }

        /// <summary>
        /// 使用输入的 IPv4 地址直连 Host；本机双开填写 127.0.0.1，同一局域网填写 Host 的 IPv4。
        /// 这里只配置 Unity Transport 的 UDP 端点，不引入 Relay、NAT 穿透或房间服务。
        /// </summary>
        public bool StartClient()
        {
            if (!CanStartSession() || !IsValidAddress(address))
            {
                StatusChanged?.Invoke();
                return false;
            }

            _transport.SetConnectionData(address, port);
            var started = _networkManager.StartClient();
            StatusChanged?.Invoke();
            return started;
        }

        public void Shutdown()
        {
            if (_networkManager != null && _networkManager.IsListening)
            {
                _networkManager.Shutdown();
            }

            StatusChanged?.Invoke();
        }

        public ulong GetRoundTripTimeMilliseconds()
        {
            if (_networkManager == null || !_networkManager.IsListening)
            {
                return 0;
            }

            return _networkManager.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId);
        }

        public static bool IsValidAddress(string value)
        {
            return IPAddress.TryParse(value, out var parsed) && parsed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
        }

        private bool CanStartSession()
        {
            return _networkManager != null && _transport != null && !_networkManager.IsListening;
        }

        private void ApproveConnection(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            var hasCapacity = _networkManager.ConnectedClientsIds.Count < MaximumPlayers;
            response.Approved = hasCapacity;
            response.CreatePlayerObject = hasCapacity;
            response.PlayerPrefabHash = null;
            response.Position = NetworkSpawnPoints.Get(request.ClientNetworkId);
            response.Rotation = Quaternion.identity;
            response.Pending = false;
            response.Reason = hasCapacity ? string.Empty : "房间已满，最多支持两名玩家。";
        }

        private void HandleClientChanged(ulong _)
        {
            StatusChanged?.Invoke();
        }

        private void HandleTransportFailure()
        {
            Debug.LogError("网络传输发生错误，请确认端口未被占用且防火墙允许当前程序通信。", this);
            StatusChanged?.Invoke();
        }
    }

    /// <summary>
    /// 提供双人竞技场的固定出生点，避免为两个位置引入额外配置资产或全局生成管理器。
    /// 固定规则集中在一处，Host 的连接审批与死亡复活可以复用同一结果。
    /// </summary>
    public static class NetworkSpawnPoints
    {
        public static Vector3 Get(ulong clientId)
        {
            return clientId % 2 == 0 ? new Vector3(-4f, 0.05f, 0f) : new Vector3(4f, 0.05f, 0f);
        }
    }
}
