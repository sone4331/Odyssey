using UnityEngine;

namespace Odyssey.Encounters
{
    /// <summary>
    /// 订阅遭遇完成事件并把场景出口平滑抬起，是玩法结果到环境表现的单向适配器。
    /// 采用观察者模式避免遭遇控制器直接引用门；移动结束后关闭碰撞，保证视觉与通行状态一致。
    /// </summary>
    public sealed class CombatEncounterDoor : MonoBehaviour
    {
        [SerializeField] private CombatEncounterController encounter;
        [SerializeField] private Vector3 openOffset = Vector3.up * 4f;
        [SerializeField] private float openDuration = 1.2f;
        [SerializeField] private AudioSource audioSource;

        private Vector3 _closedPosition;
        private Vector3 _openPosition;
        private float _elapsed;
        private bool _opening;

        private void Awake()
        {
            _closedPosition = transform.position;
            _openPosition = _closedPosition + openOffset;
        }

        private void OnEnable()
        {
            if (encounter != null)
            {
                encounter.EncounterCompleted += BeginOpen;
            }
        }

        private void OnDisable()
        {
            if (encounter != null)
            {
                encounter.EncounterCompleted -= BeginOpen;
            }
        }

        private void Update()
        {
            if (!_opening)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            var progress = Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, openDuration));
            transform.position = Vector3.Lerp(_closedPosition, _openPosition, Mathf.SmoothStep(0f, 1f, progress));
            if (progress < 1f)
            {
                return;
            }

            _opening = false;
            foreach (var collision in GetComponentsInChildren<Collider>())
            {
                collision.enabled = false;
            }
        }

        private void BeginOpen()
        {
            if (_opening || transform.position == _openPosition)
            {
                return;
            }

            _elapsed = 0f;
            _opening = true;
            audioSource?.Play();
        }
    }
}
