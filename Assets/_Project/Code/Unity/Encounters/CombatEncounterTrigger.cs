using Odyssey.Characters.Player;
using UnityEngine;

namespace Odyssey.Encounters
{
    /// <summary>
    /// 将玩家进入物理触发区转换为遭遇开始请求，并用 TriggerStay 覆盖玩家出生时已位于区域内部的情况。
    /// 采用专用 Adapter 隔离 Unity 物理回调；刚体只负责保证 Trigger 消息产生，不参与实际动力学模拟。
    /// </summary>
    [RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
    public sealed class CombatEncounterTrigger : MonoBehaviour
    {
        [SerializeField] private CombatEncounterController encounter;

        private void Awake()
        {
            var trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            var body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            if (encounter == null)
            {
                Debug.LogError("战斗触发区缺少 CombatEncounterController 引用。", this);
            }
        }

        /// <summary>
        /// Unity 不保证对“场景载入时已经重叠”的 CharacterController 立即发送 TriggerEnter，
        /// 因此首帧显式检查一次包围盒；后续进入仍由物理回调处理，不做每帧全局扫描。
        /// </summary>
        private void Start()
        {
            if (encounter == null)
            {
                return;
            }

            var player = FindFirstObjectByType<PlayerController>();
            var playerController = player == null ? null : player.GetComponent<CharacterController>();
            if (playerController != null && GetComponent<BoxCollider>().bounds.Intersects(playerController.bounds))
            {
                encounter.StartEncounter();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            TryStartEncounter(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryStartEncounter(other);
        }

        private void TryStartEncounter(Collider other)
        {
            if (encounter != null && other.GetComponentInParent<PlayerController>() != null)
            {
                encounter.StartEncounter();
            }
        }
    }
}
