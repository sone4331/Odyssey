using System;

namespace Odyssey.Gameplay.Encounters
{
    /// <summary>
    /// 表示场景遭遇战的领域阶段，供控制器、UI 和后续网络复制共享同一套明确状态。
    /// </summary>
    public enum CombatEncounterState
    {
        Waiting,
        Active,
        Completed
    }

    /// <summary>
    /// 维护一次固定成员遭遇战的开始、剩余数量与完成不变量，不持有任何 Unity 场景对象。
    /// 采用状态对象模式把玩法规则从 MonoBehaviour 分离，使重复开始、重复击杀和完成边界可独立测试。
    /// </summary>
    public sealed class CombatEncounterProgress
    {
        public CombatEncounterProgress(int totalEnemies)
        {
            if (totalEnemies <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalEnemies), "遭遇战至少需要一名敌人。");
            }

            TotalEnemies = totalEnemies;
            RemainingEnemies = totalEnemies;
        }

        public int TotalEnemies { get; }
        public int RemainingEnemies { get; private set; }
        public CombatEncounterState State { get; private set; } = CombatEncounterState.Waiting;

        public bool Start()
        {
            if (State != CombatEncounterState.Waiting)
            {
                return false;
            }

            State = CombatEncounterState.Active;
            return true;
        }

        public bool RegisterDefeat()
        {
            if (State != CombatEncounterState.Active || RemainingEnemies <= 0)
            {
                return false;
            }

            RemainingEnemies--;
            if (RemainingEnemies == 0)
            {
                State = CombatEncounterState.Completed;
            }

            return true;
        }

        /// <summary>
        /// 在非权威客户端应用 Host 快照；只接受合法范围和单向阶段推进，避免网络旧包让遭遇倒退。
        /// </summary>
        public bool ApplySnapshot(CombatEncounterState state, int remainingEnemies)
        {
            if (state < State || remainingEnemies < 0 || remainingEnemies > TotalEnemies)
            {
                return false;
            }

            if (state == CombatEncounterState.Completed && remainingEnemies != 0)
            {
                return false;
            }

            State = state;
            RemainingEnemies = remainingEnemies;
            return true;
        }
    }
}
