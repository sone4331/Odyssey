using Odyssey.Encounters;
using Odyssey.Gameplay.Encounters;
using Unity.Netcode;
using UnityEngine;

namespace Odyssey.Networking
{
    /// <summary>
    /// 复制固定战区的阶段与剩余怪物数量；Host 监听真实死亡事件，Client 只应用只读快照供 HUD 使用。
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkEncounterAdapter : NetworkBehaviour
    {
        [SerializeField] private CombatEncounterController encounter;
        private readonly NetworkVariable<CombatEncounterState> _state = new NetworkVariable<CombatEncounterState>(
            CombatEncounterState.Waiting,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _remaining = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private void Awake()
        {
            if (encounter != null)
            {
                encounter.enabled = false;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (encounter == null)
            {
                return;
            }

            _state.OnValueChanged += HandleStateChanged;
            _remaining.OnValueChanged += HandleRemainingChanged;
            if (IsServer)
            {
                encounter.EncounterStarted += PublishSnapshot;
                encounter.EnemyDefeated += HandleEnemyDefeated;
                encounter.EncounterCompleted += PublishSnapshot;
                _remaining.Value = encounter.RemainingEnemies;
                encounter.enabled = true;
            }
            else
            {
                encounter.ApplyAuthoritativeSnapshot(_state.Value, _remaining.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            _state.OnValueChanged -= HandleStateChanged;
            _remaining.OnValueChanged -= HandleRemainingChanged;
            if (encounter != null)
            {
                encounter.EncounterStarted -= PublishSnapshot;
                encounter.EnemyDefeated -= HandleEnemyDefeated;
                encounter.EncounterCompleted -= PublishSnapshot;
            }
        }

        private void HandleEnemyDefeated(Odyssey.Characters.Enemies.Enemy _)
        {
            PublishSnapshot();
        }

        private void PublishSnapshot()
        {
            if (!IsServer)
            {
                return;
            }

            _state.Value = encounter.State;
            _remaining.Value = encounter.RemainingEnemies;
        }

        private void HandleStateChanged(CombatEncounterState _, CombatEncounterState __)
        {
            ApplyClientSnapshot();
        }

        private void HandleRemainingChanged(int _, int __)
        {
            ApplyClientSnapshot();
        }

        private void ApplyClientSnapshot()
        {
            if (!IsServer)
            {
                encounter.ApplyAuthoritativeSnapshot(_state.Value, _remaining.Value);
            }
        }
    }
}
