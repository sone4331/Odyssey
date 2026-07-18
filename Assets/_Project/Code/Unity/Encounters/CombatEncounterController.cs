using System;
using System.Collections.Generic;
using Odyssey.Characters.Enemies;
using Odyssey.Gameplay.Encounters;
using UnityEngine;

namespace Odyssey.Encounters
{
    /// <summary>
    /// 把预先放置的场景敌人装配为一次可开始、可统计、可完成的战斗遭遇，并发布只读结果事件。
    /// 采用场景级 Controller 与观察者模式；它不是单例，不生成波次，也不直接控制 UI、门或音效。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class CombatEncounterController : MonoBehaviour
    {
        [SerializeField] private Enemy[] participants = Array.Empty<Enemy>();
        [SerializeField] private bool startOnSceneLoad;

        private readonly HashSet<Enemy> _defeated = new HashSet<Enemy>();
        private CombatEncounterProgress _progress;

        public IReadOnlyList<Enemy> Participants => participants;
        public CombatEncounterState State => _progress?.State ?? CombatEncounterState.Waiting;
        public int RemainingEnemies => _progress?.RemainingEnemies ?? participants.Length;

        public event Action EncounterStarted;
        public event Action<Enemy> EnemyDefeated;
        public event Action EncounterCompleted;

        private void Awake()
        {
            var validParticipants = new List<Enemy>();
            foreach (var enemy in participants)
            {
                if (enemy != null && !validParticipants.Contains(enemy))
                {
                    validParticipants.Add(enemy);
                }
            }

            participants = validParticipants.ToArray();
            if (participants.Length == 0)
            {
                Debug.LogError("战斗遭遇没有配置任何有效敌人。", this);
                enabled = false;
                return;
            }

            _progress = new CombatEncounterProgress(participants.Length);
            foreach (var enemy in participants)
            {
                enemy.Defeated += HandleEnemyDefeated;
                enemy.SetEncounterActive(startOnSceneLoad);
            }

            if (startOnSceneLoad)
            {
                StartEncounter();
            }
        }

        private void OnDestroy()
        {
            foreach (var enemy in participants)
            {
                if (enemy != null)
                {
                    enemy.Defeated -= HandleEnemyDefeated;
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<Odyssey.Characters.Player.PlayerController>() != null)
            {
                StartEncounter();
            }
        }

        /// <summary>
        /// 原子地激活规则状态和所有参与者；重复触发安全返回，避免多个玩家或复合碰撞体重复发布开始事件。
        /// </summary>
        public bool StartEncounter()
        {
            if (_progress == null || !_progress.Start())
            {
                return false;
            }

            foreach (var enemy in participants)
            {
                if (enemy != null)
                {
                    enemy.SetEncounterActive(true);
                }
            }

            EncounterStarted?.Invoke();
            return true;
        }

        private void HandleEnemyDefeated(Enemy enemy)
        {
            if (enemy == null || !_defeated.Add(enemy) || !_progress.RegisterDefeat())
            {
                return;
            }

            EnemyDefeated?.Invoke(enemy);
            if (_progress.State == CombatEncounterState.Completed)
            {
                EncounterCompleted?.Invoke();
            }
        }
    }
}
