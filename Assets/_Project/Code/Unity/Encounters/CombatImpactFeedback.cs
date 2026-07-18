using Cinemachine;
using Odyssey.Characters.Enemies;
using Odyssey.Characters.Player;
using Odyssey.Gameplay.Combat;
using Odyssey.Gameplay.Encounters;
using UnityEngine;

namespace Odyssey.Encounters
{
    /// <summary>
    /// 订阅已提交的伤害与闪避事件，集中播放命中特效、音效和轻量镜头冲击。
    /// 采用 Presenter/Observer 模式保持表现单向依赖玩法结果；删除本组件不会改变伤害、AI 或遭遇进度。
    /// </summary>
    public sealed class CombatImpactFeedback : MonoBehaviour
    {
        [SerializeField] private CombatEncounterController encounter;
        [SerializeField] private PlayerController player;
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField] private GameObject evadeEffectPrefab;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip hitClip;
        [SerializeField] private AudioClip evadeClip;
        [SerializeField] private CinemachineImpulseSource impulseSource;
        private float _nextEvadeFeedbackTime;

        private void OnEnable()
        {
            if (encounter != null)
            {
                foreach (var enemy in encounter.Participants)
                {
                    if (enemy != null)
                    {
                        enemy.Damaged += HandleEnemyDamaged;
                    }
                }
            }

            if (player != null)
            {
                player.DamageEvaded += HandleDamageEvaded;
            }
        }

        private void OnDisable()
        {
            if (encounter != null)
            {
                foreach (var enemy in encounter.Participants)
                {
                    if (enemy != null)
                    {
                        enemy.Damaged -= HandleEnemyDamaged;
                    }
                }
            }

            if (player != null)
            {
                player.DamageEvaded -= HandleDamageEvaded;
            }
        }

        private void HandleEnemyDamaged(Enemy enemy, DamageResult result)
        {
            if (!result.Accepted || enemy == null)
            {
                return;
            }

            SpawnEffect(hitEffectPrefab, enemy.transform.position + Vector3.up * 0.7f);
            PlayOneShot(hitClip);
            impulseSource?.GenerateImpulse(0.18f);
        }

        private void HandleDamageEvaded(DamageRequest _)
        {
            if (player == null || encounter == null ||
                encounter.State != CombatEncounterState.Active ||
                Time.unscaledTime < _nextEvadeFeedbackTime)
            {
                return;
            }

            _nextEvadeFeedbackTime = Time.unscaledTime + 0.15f;
            SpawnEffect(evadeEffectPrefab, player.transform.position + Vector3.up);
            PlayOneShot(evadeClip);
            impulseSource?.GenerateImpulse(0.08f);
        }

        private static void SpawnEffect(GameObject prefab, Vector3 position)
        {
            if (prefab == null)
            {
                return;
            }

            var effect = Instantiate(prefab, position, Quaternion.identity);
            Destroy(effect, 2f);
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
    }
}
