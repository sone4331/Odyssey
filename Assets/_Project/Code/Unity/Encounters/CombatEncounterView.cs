using UnityEngine;
using UnityEngine.UI;

namespace Odyssey.Encounters
{
    /// <summary>
    /// 将遭遇事件转换为中文 HUD 文案，只负责显示，不读取敌人生命或修改玩法状态。
    /// 采用被动 View 与观察者模式，确保 UI 可以替换而不会影响战斗规则和后续网络权威。
    /// </summary>
    public sealed class CombatEncounterView : MonoBehaviour
    {
        [SerializeField] private CombatEncounterController encounter;
        [SerializeField] private Text statusText;

        private void OnEnable()
        {
            if (encounter == null)
            {
                Debug.LogError("遭遇 HUD 缺少 CombatEncounterController 引用。", this);
                return;
            }

            encounter.EncounterStarted += ShowStarted;
            encounter.EnemyDefeated += ShowProgress;
            encounter.EncounterCompleted += ShowCompleted;
            SetText($"{encounter.DisplayName}：清理全部敌人");
        }

        private void OnDisable()
        {
            if (encounter == null)
            {
                return;
            }

            encounter.EncounterStarted -= ShowStarted;
            encounter.EnemyDefeated -= ShowProgress;
            encounter.EncounterCompleted -= ShowCompleted;
        }

        private void ShowStarted()
        {
            SetText($"{encounter.DisplayName}开始　剩余敌人：{encounter.RemainingEnemies}");
        }

        private void ShowProgress(Odyssey.Characters.Enemies.Enemy _)
        {
            SetText($"{encounter.DisplayName}　剩余敌人：{encounter.RemainingEnemies}");
        }

        private void ShowCompleted()
        {
            SetText(encounter.DisplayName == "第一战区"
                ? "第一战区完成　重新踩下踏板开启隔离门"
                : $"{encounter.DisplayName}完成");
        }

        private void SetText(string value)
        {
            if (statusText != null)
            {
                statusText.text = value;
            }
        }
    }
}
