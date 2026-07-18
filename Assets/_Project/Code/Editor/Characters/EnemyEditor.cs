using Odyssey.Characters.Enemies;
using UnityEditor;
using UnityEngine;

namespace Odyssey.Editor.Characters
{
    /// <summary>
    /// 在 Inspector 与 Scene 视图展示怪物 Blackboard、Utility 结果和感知半径。
    /// 采用只读调试适配器提供面试可视化证据，不向运行时代码引入 Editor 依赖或通用调试框架。
    /// </summary>
    [CustomEditor(typeof(Enemy))]
    public sealed class EnemyEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var enemy = (Enemy)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("AI 运行时状态（只读）", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("当前目标", enemy.CurrentGoal.ToString());
                EditorGUILayout.FloatField("Utility 分数", enemy.DecisionScore);
                EditorGUILayout.FloatField("目标距离", enemy.TargetDistance);
                EditorGUILayout.Slider("生命比例", enemy.HealthRatio, 0f, 1f);
                EditorGUILayout.TextField("攻击方式", enemy.AttackMode == Odyssey.Gameplay.Config.EnemyAttackMode.Projectile
                    ? "投射物"
                    : "近战");
                EditorGUILayout.FloatField("最小安全距离", enemy.MinimumAttackRange);
                EditorGUILayout.Toggle("遭遇已激活", enemy.IsEncounterActive);
                EditorGUILayout.Toggle("巡逻路线有效", enemy.HasPatrolRoute);
                EditorGUILayout.TextField("当前巡逻点", enemy.CurrentPatrolPointName);
            }

            if (EditorApplication.isPlaying)
            {
                Repaint();
            }
        }

        private void OnSceneGUI()
        {
            var enemy = (Enemy)target;
            Handles.color = new Color(1f, 0.8f, 0f, 0.8f);
            Handles.DrawWireDisc(enemy.transform.position, Vector3.up, enemy.ChaseRange);
            Handles.color = new Color(1f, 0.2f, 0.2f, 0.9f);
            Handles.DrawWireDisc(enemy.transform.position, Vector3.up, enemy.AttackRange);
            if (enemy.MinimumAttackRange > 0f)
            {
                Handles.color = new Color(0.3f, 0.9f, 1f, 0.9f);
                Handles.DrawWireDisc(enemy.transform.position, Vector3.up, enemy.MinimumAttackRange);
            }
        }
    }
}
