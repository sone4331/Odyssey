using Odyssey.Characters.Player;
using UnityEditor;

namespace Odyssey.Editor.Characters
{
    /// <summary>
    /// 在标准玩家 Inspector 下方显示运行时状态证据，便于调试和面试演示两条正交状态轴。
    /// 采用自定义 Inspector 作为只读调试适配器，不向 PlayerController 注入 Editor 依赖，也不修改玩法状态。
    /// </summary>
    [CustomEditor(typeof(PlayerController))]
    public sealed class PlayerControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var player = (PlayerController)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("运行时状态（只读）", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("移动状态", player.LocomotionState.ToString());
                EditorGUILayout.TextField("动作状态", player.ActionState.ToString());
                EditorGUILayout.FloatField("平面速度", player.CurrentPlanarSpeed);
                EditorGUILayout.Vector3Field("期望移动方向", player.DesiredMoveDirection);
                EditorGUILayout.FloatField("地面坡度", player.GroundSlopeAngle);
                EditorGUILayout.Toggle("墙边保护", player.WallClearanceActive);
                EditorGUILayout.Toggle("伤害免疫", player.IsDamageImmune);
                EditorGUILayout.IntField("当前生命", player.CurrentHealth);
                EditorGUILayout.IntField("最大生命", player.MaxHealth);
            }

            if (EditorApplication.isPlaying)
            {
                Repaint();
            }
        }
    }
}
