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
    /// 把运行时生成的本机 Owner 绑定到 FreeLook 摄像机、血量 UI、存档和命中反馈。
    /// 采用场景级 Binder：这里只处理“本机看见和操控谁”，不参与会话启动、菜单跳转或网络权威规则。
    /// </summary>
    public sealed class GameplayLocalViewBinder : MonoBehaviour
    {
        [SerializeField] private PlayerHealthUI healthUi;
        [SerializeField] private SaveManager saveManager;
        [SerializeField] private CombatImpactFeedback[] impactFeedbacks;
        [SerializeField] private CinemachineFreeLook freeLookCamera;
        [SerializeField] private CinemachineInputProvider cameraInputProvider;

        private PlayerController _localPlayer;
        private float _defaultHorizontalSpeed = 280f;
        private float _defaultVerticalSpeed = 2f;
        private float _cameraSensitivity = 1f;
        private bool _gameplayInputEnabled;

        public PlayerController LocalPlayer => _localPlayer;
        public CinemachineFreeLook FreeLookCamera => freeLookCamera;
        public bool IsGameplayInputEnabled => _gameplayInputEnabled;

        private void Awake()
        {
            ResolveCameraReferences();
            CacheDefaultCameraSpeed();
        }

        /// <summary>
        /// 绑定本机 Owner。远端玩家不会调用此入口，因此不会抢占本机摄像机和 HUD。
        /// 重绑时主动清除 FreeLook 历史状态，避免复活或重新加入后沿用旧玩家的镜头缓存。
        /// </summary>
        public void Bind(PlayerController player, bool multiplayer, ApplicationContext context)
        {
            if (player == null)
            {
                return;
            }

            _localPlayer = player;
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

            ResolveCameraReferences();
            if (freeLookCamera != null)
            {
                var target = FindCameraTarget(player.transform);
                freeLookCamera.Follow = target;
                freeLookCamera.LookAt = target;
                freeLookCamera.m_XAxis.Value = 0f;
                freeLookCamera.m_YAxis.Value = 0.5f;
                freeLookCamera.PreviousStateIsValid = false;
            }

            SetCameraSensitivity(_cameraSensitivity);
            SetGameplayInputEnabled(_gameplayInputEnabled);
        }

        /// <summary>
        /// 统一启停本机玩家和 FreeLook 输入。联机 ESC 菜单只影响本机，不修改 Time.timeScale。
        /// </summary>
        public void SetGameplayInputEnabled(bool inputEnabled)
        {
            _gameplayInputEnabled = inputEnabled;
            _localPlayer?.SetLocalInputEnabled(inputEnabled);
            if (cameraInputProvider != null)
            {
                cameraInputProvider.enabled = inputEnabled;
            }
        }

        /// <summary>
        /// 按原 FreeLook 轴速度的倍率应用灵敏度，避免直接覆盖美术已经调好的水平与垂直手感比例。
        /// </summary>
        public void SetCameraSensitivity(float sensitivity)
        {
            _cameraSensitivity = Mathf.Clamp(sensitivity, 0.2f, 2f);
            ResolveCameraReferences();
            if (freeLookCamera == null)
            {
                return;
            }

            freeLookCamera.m_XAxis.m_MaxSpeed = _defaultHorizontalSpeed * _cameraSensitivity;
            freeLookCamera.m_YAxis.m_MaxSpeed = _defaultVerticalSpeed * _cameraSensitivity;
        }

        /// <summary>
        /// 会话关闭时解除旧玩家引用，防止下一次创建房间前摄像机继续追踪已销毁对象。
        /// </summary>
        public void Clear()
        {
            _localPlayer?.SetLocalInputEnabled(false);
            _localPlayer = null;
            _gameplayInputEnabled = false;
            if (cameraInputProvider != null)
            {
                cameraInputProvider.enabled = false;
            }

            if (freeLookCamera != null)
            {
                freeLookCamera.Follow = null;
                freeLookCamera.LookAt = null;
                freeLookCamera.PreviousStateIsValid = false;
            }
        }

        private void ResolveCameraReferences()
        {
            if (freeLookCamera == null)
            {
                freeLookCamera = FindObjectsByType<CinemachineFreeLook>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .OrderByDescending(camera => camera.Priority)
                    .FirstOrDefault();
            }

            if (cameraInputProvider == null && freeLookCamera != null)
            {
                cameraInputProvider = freeLookCamera.GetComponent<CinemachineInputProvider>();
            }
        }

        private void CacheDefaultCameraSpeed()
        {
            if (freeLookCamera == null)
            {
                return;
            }

            _defaultHorizontalSpeed = Mathf.Max(1f, freeLookCamera.m_XAxis.m_MaxSpeed);
            _defaultVerticalSpeed = Mathf.Max(0.1f, freeLookCamera.m_YAxis.m_MaxSpeed);
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
