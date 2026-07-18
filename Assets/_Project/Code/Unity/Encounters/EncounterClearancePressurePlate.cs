using System.Collections.Generic;
using Odyssey.Characters.Player;
using Odyssey.Gameplay.Encounters;
using UnityEngine;

namespace Odyssey.Encounters
{
    /// <summary>
    /// 把玩家“完成第一战区后重新踩入踏板”的物理事实转换为隔离门开门命令。
    /// 采用条件门与 Adapter 模式；它只读取遭遇结果，不控制怪物，也不会在清怪事件发生时主动开门。
    /// </summary>
    public sealed class EncounterClearancePressurePlate : MonoBehaviour
    {
        [SerializeField] private CombatEncounterController encounter;
        [SerializeField] private EncounterClearanceGate gate;

        private readonly HashSet<PlayerController> _playersInside = new HashSet<PlayerController>();

        public int PlayersInside => _playersInside.Count;

        private void Awake()
        {
            if (encounter == null || gate == null)
            {
                Debug.LogError("清怪踏板缺少第一战区或隔离门引用。", this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            _playersInside.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null || !_playersInside.Add(player))
            {
                return;
            }

            // 只在新的进入事件发生时检查完成状态，因此玩家站在板上清完怪不会自动开门。
            if (encounter.State == CombatEncounterState.Completed)
            {
                gate.Open();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                _playersInside.Remove(player);
            }
        }
    }
}
