using System.Linq;
using Cinemachine;
using Odyssey.Bootstrap;
using Odyssey.Characters.Player;
using Odyssey.Encounters;
using Odyssey.Systems;
using UnityEngine;

namespace Odyssey.Networking
{
    /// <summary>
    /// 把运行时生成的本机 Owner 绑定到 Cinemachine、血量 UI、存档和命中反馈。
    /// 采用场景级 Presenter/Installer，避免网络玩家 Prefab 反向查找并控制具体 UI 对象。
    /// </summary>
    public sealed class GameplayLocalViewBinder : MonoBehaviour
    {
        [SerializeField] private PlayerHealthUI healthUi;
        [SerializeField] private SaveManager saveManager;
        [SerializeField] private CombatImpactFeedback[] impactFeedbacks;

        public void Bind(PlayerController player, bool multiplayer, ApplicationContext context)
        {
            if (player == null)
            {
                return;
            }

            healthUi?.Bind(player);
            if (saveManager != null)
            {
                saveManager.SetMultiplayerMode(multiplayer);
                if (context != null)
                {
                    saveManager.BindPlayer(player, context.SaveService);
                }
            }

            if (impactFeedbacks != null)
            {
                foreach (var feedback in impactFeedbacks)
                {
                    feedback?.BindPlayer(player);
                }
            }

            var target = FindCameraTarget(player.transform);
            var activeCamera = FindObjectsByType<CinemachineVirtualCamera>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .OrderByDescending(camera => camera.Priority)
                .FirstOrDefault();
            if (activeCamera != null)
            {
                activeCamera.Follow = target;
                activeCamera.LookAt = target;
            }
        }

        private static Transform FindCameraTarget(Transform player)
        {
            foreach (var child in player.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "StrafeCamTarget")
                {
                    return child;
                }
            }

            return player;
        }
    }
}
