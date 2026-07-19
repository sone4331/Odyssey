using UnityEngine;

namespace Odyssey.Encounters
{
    /// <summary>
    /// 控制第一战区原有 DoorHuge 门板的开启动画，只接收明确的 Open 命令，不读取怪物或踏板状态。
    /// 采用 Command Receiver 与表现适配器模式，把通关规则和门体移动分离，后续更换门模型不会影响玩法判断。
    /// </summary>
    public sealed class EncounterClearanceGate : MonoBehaviour
    {
        [SerializeField] private Transform movingPart;
        [SerializeField] private Vector3 closedLocalPosition;
        [SerializeField] private Vector3 openLocalOffset = Vector3.down * 10.1f;
        [SerializeField, Min(0.05f)] private float openDuration = 1.2f;
        [SerializeField] private AudioSource audioSource;

        private Vector3 _closedLocalPosition;
        private Vector3 _openLocalPosition;
        private float _elapsed;

        public bool IsOpening { get; private set; }
        public bool IsOpen { get; private set; }
        public Vector3 ClosedLocalPosition => _closedLocalPosition;
        public Vector3 CurrentMovingPartLocalPosition => movingPart == null
            ? Vector3.zero
            : movingPart.localPosition;

        private void Awake()
        {
            if (movingPart == null)
            {
                Debug.LogError("隔离门缺少可移动门板引用。", this);
                enabled = false;
                return;
            }

            // 无论上一次运行或第三方预览把门板留在何处，进入场景时都以构建阶段固化的关闭位置为准。
            _closedLocalPosition = closedLocalPosition;
            movingPart.localPosition = _closedLocalPosition;
            _openLocalPosition = _closedLocalPosition + openLocalOffset;
        }

        private void Update()
        {
            if (!IsOpening || movingPart == null)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            var progress = Mathf.Clamp01(_elapsed / Mathf.Max(0.05f, openDuration));
            movingPart.localPosition = Vector3.Lerp(
                _closedLocalPosition,
                _openLocalPosition,
                Mathf.SmoothStep(0f, 1f, progress));
            if (progress < 1f)
            {
                return;
            }

            IsOpening = false;
            IsOpen = true;
        }

        /// <summary>
        /// 幂等地接受一次开门命令；重复触发不会重播动画或叠加位移。
        /// </summary>
        public bool Open()
        {
            if (!enabled || IsOpening || IsOpen)
            {
                return false;
            }

            _elapsed = 0f;
            IsOpening = true;
            audioSource?.Play();
            return true;
        }
    }
}
