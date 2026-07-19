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
        [SerializeField] private Collider triggerVolume;

        private readonly HashSet<PlayerController> _playersInside = new HashSet<PlayerController>();
        private PlayerController _player;
        private bool _completionRequiresExit;
        private bool _freshEntryArmed;

        public int PlayersInside => _playersInside.Count;
        public CombatEncounterController RequiredEncounter => encounter;

        private void Awake()
        {
            DisableLegacyDoorCommands();
            if (encounter == null || gate == null)
            {
                Debug.LogError("清怪踏板缺少第一战区或隔离门引用。", this);
                enabled = false;
                return;
            }

            if (encounter.SequenceIndex != 0)
            {
                Debug.LogError("清怪踏板错误绑定到了非第一战区，已拒绝启用门禁。", this);
                enabled = false;
                return;
            }

            triggerVolume ??= GetComponent<Collider>();
            _player = FindFirstObjectByType<PlayerController>();
            encounter.EncounterCompleted += HandleEncounterCompleted;
        }

        /// <summary>
        /// 禁用踏板与门附近的 3D Game Kit 教学命令，确保唯一开门入口是本组件验证清怪结果后的 Gate.Open。
        /// 采用类型名适配避免运行时程序集直接依赖第三方 Assembly-CSharp。
        /// </summary>
        private void DisableLegacyDoorCommands()
        {
            if (gate == null)
            {
                return;
            }

            var blockedTypes = new HashSet<string>
            {
                "Gamekit3D.GameCommands.SendOnTriggerEnter",
                "Gamekit3D.InteractOnTrigger",
                "Gamekit3D.GameCommands.SimpleTranslator",
                "Gamekit3D.GameCommands.GameCommandReceiver"
            };
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (behaviour == null || !blockedTypes.Contains(behaviour.GetType().FullName))
                {
                    continue;
                }

                var nearPlate = Vector3.Distance(behaviour.transform.position, transform.position) < 8f;
                var nearGate = Vector3.Distance(behaviour.transform.position, gate.transform.position) < 8f;
                if (nearPlate || nearGate)
                {
                    behaviour.enabled = false;
                }
            }
        }

        private void OnDisable()
        {
            _playersInside.Clear();
        }

        private void OnDestroy()
        {
            if (encounter != null)
            {
                encounter.EncounterCompleted -= HandleEncounterCompleted;
            }
        }

        /// <summary>
        /// 用物理体积事实补偿传送、双 Trigger 和低帧率下可能遗漏的 Exit/Enter 顺序。
        /// 它只在第一战区完成后工作：站在板上清怪必须先离开，重新进入后才向 Gate 发送一次 Open 命令。
        /// </summary>
        private void FixedUpdate()
        {
            if (encounter == null || gate == null || encounter.State != CombatEncounterState.Completed)
            {
                return;
            }

            var playerInside = IsPlayerInsideTrigger();
            if (_completionRequiresExit && !_freshEntryArmed)
            {
                if (!playerInside)
                {
                    _freshEntryArmed = true;
                }

                return;
            }

            if (_freshEntryArmed && playerInside)
            {
                gate.Open();
                _freshEntryArmed = false;
            }
        }

        private void HandleEncounterCompleted()
        {
            _completionRequiresExit = IsPlayerInsideTrigger();
            _freshEntryArmed = !_completionRequiresExit;
        }

        private void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null || !_playersInside.Add(player))
            {
                return;
            }

            // 只在新的进入事件发生时检查完成状态，因此玩家站在板上清完怪不会自动开门。
            if (encounter.State == CombatEncounterState.Completed &&
                (!_completionRequiresExit || _freshEntryArmed))
            {
                gate.Open();
                _freshEntryArmed = false;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                _playersInside.Remove(player);
                if (encounter != null &&
                    encounter.State == CombatEncounterState.Completed &&
                    !IsPlayerInsideTrigger())
                {
                    _freshEntryArmed = true;
                }
            }
        }

        private bool IsPlayerInsideTrigger()
        {
            if (_player == null || triggerVolume == null || !_player.gameObject.activeInHierarchy)
            {
                return false;
            }

            var playerBounds = _player.Controller == null
                ? new Bounds(_player.transform.position, Vector3.one * 0.1f)
                : _player.Controller.bounds;
            return triggerVolume.bounds.Intersects(playerBounds);
        }
    }
}
