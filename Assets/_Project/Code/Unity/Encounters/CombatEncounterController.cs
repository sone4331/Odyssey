using System;
using System.Collections.Generic;
using Odyssey.Characters.Enemies;
using Odyssey.Gameplay.Encounters;
using UnityEngine;

namespace Odyssey.Encounters
{
    /// <summary>
    /// 统计一组预先放置敌人的存活进度并发布开始、击败和完成事件。
    /// 采用场景级 Controller 与观察者模式；它不是单例，也不控制怪物 AI、UI、门或音效。
    /// </summary>
    public sealed class CombatEncounterController : MonoBehaviour
    {
        [SerializeField] private string displayName = "战斗区域";
        [SerializeField] private Enemy[] participants = Array.Empty<Enemy>();

        private readonly HashSet<Enemy> _defeated = new HashSet<Enemy>();
        private CombatEncounterProgress _progress;

        public IReadOnlyList<Enemy> Participants => participants;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "战斗区域" : displayName;
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
            }
        }

        private void Start()
        {
            // 战区只承担统计职责，因此场景开始后立即进入 Active；怪物 AI 始终独立运行。
            StartEncounter();
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

        /// <summary>
        /// 原子地启动统计状态；重复调用安全返回，且绝不改变参与怪物的行为树状态。
        /// </summary>
        public bool StartEncounter()
        {
            if (_progress == null || !_progress.Start())
            {
                return false;
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
