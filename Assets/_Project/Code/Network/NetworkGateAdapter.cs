using Odyssey.Encounters;
using Unity.Netcode;
using UnityEngine;

namespace Odyssey.Networking
{
    /// <summary>
    /// 让踏板条件只在 Host 判断，并把隔离门的开启事实复制给所有 Client。
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkGateAdapter : NetworkBehaviour
    {
        [SerializeField] private EncounterClearancePressurePlate pressurePlate;
        [SerializeField] private EncounterClearanceGate gate;

        private readonly NetworkVariable<bool> _isOpen = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public override void OnNetworkSpawn()
        {
            if (gate == null)
            {
                gate = GetComponent<EncounterClearanceGate>();
            }

            _isOpen.OnValueChanged += HandleOpenChanged;
            if (pressurePlate != null)
            {
                pressurePlate.enabled = IsServer;
            }

            if (IsServer && gate != null)
            {
                gate.OpeningStarted += HandleHostOpening;
            }
            else if (_isOpen.Value)
            {
                gate?.Open();
            }
        }

        public override void OnNetworkDespawn()
        {
            _isOpen.OnValueChanged -= HandleOpenChanged;
            if (gate != null)
            {
                gate.OpeningStarted -= HandleHostOpening;
            }
        }

        private void HandleHostOpening()
        {
            if (IsServer)
            {
                _isOpen.Value = true;
            }
        }

        private void HandleOpenChanged(bool _, bool current)
        {
            if (!IsServer && current)
            {
                gate?.Open();
            }
        }
    }
}
