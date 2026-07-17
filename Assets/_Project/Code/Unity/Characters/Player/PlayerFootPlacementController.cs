using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Odyssey.Characters.Player
{
    /// <summary>
    /// 驱动 Generic Ellen 的站立脚部目标，使角色停止移动后贴合地面而不改变 CharacterController 权威位置。
    /// 采用表现层 Animation Rigging Adapter：移动时由原动画完整接管，低速站立时只修正脚掌位置，避免 Generic 骨轴被错误旋转。
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class PlayerFootPlacementController : MonoBehaviour
    {
        private const float RayStartHeight = 0.25f;
        private const float RayDistance = 0.65f;
        private const float SoleOffset = 0.03f;
        private const float WeightSpeed = 10f;
        private const float TargetFollowSpeed = 14f;
        private const float MaximumPlacementSpeed = 0.1f;

        [SerializeField] private PlayerController player;
        [SerializeField] private Rig footRig;
        [SerializeField] private TwoBoneIKConstraint leftFootConstraint;
        [SerializeField] private TwoBoneIKConstraint rightFootConstraint;
        [SerializeField] private OverrideTransform pelvisConstraint;
        [SerializeField] private Transform leftFoot;
        [SerializeField] private Transform rightFoot;
        [SerializeField] private Transform leftTarget;
        [SerializeField] private Transform rightTarget;

        private bool _wasPlacementRequested;
        private int _startupFramesRemaining = 2;

        public float CurrentWeight => footRig == null ? 0f : footRig.weight;

        private void Awake()
        {
            player ??= GetComponent<PlayerController>();
            PrepareRigForSafeStartup();
        }

        private void Update()
        {
            if (!HasValidSetup())
            {
                return;
            }

            if (_startupFramesRemaining > 0)
            {
                // Animator 第一次求值发生在首帧 Update 之后。这里保持 Rig 为零并等待完整动画姿势，
                // 防止把 Generic 绑定姿势中靠近身体中心的脚位误当成运行时贴地目标。
                footRig.weight = 0f;
                SnapTargetsToAnimatedFeet();
                _startupFramesRemaining--;
                return;
            }

            var shouldPlaceFeet = player.LocomotionState == Odyssey.Gameplay.Characters.PlayerLocomotionStateId.Grounded &&
                                  player.ActionState == Odyssey.Gameplay.Characters.PlayerActionStateId.Free &&
                                  player.CurrentPlanarSpeed <= MaximumPlacementSpeed;
            if (shouldPlaceFeet && !_wasPlacementRequested)
            {
                // 在 Rig 权重仍接近零时捕获原动画脚位，保证 IK 从当前姿势无缝接管。
                SnapTargetsToAnimatedFeet();
            }

            _wasPlacementRequested = shouldPlaceFeet;
            footRig.weight = Mathf.MoveTowards(
                footRig.weight,
                shouldPlaceFeet ? 1f : 0f,
                WeightSpeed * Time.deltaTime);

            if (!shouldPlaceFeet)
            {
                // 走跑时持续跟随 Animator 输出；Rig 淡出后不会把双脚锁死在上一帧位置。
                SnapTargetsToAnimatedFeet();
            }
            else
            {
                UpdateFootTarget(leftTarget);
                UpdateFootTarget(rightTarget);
            }

            ref var pelvisData = ref pelvisConstraint.data;
            // 当前 Generic 骨架无法提供可靠的未约束足部采样点，骨盆保持动画原值，
            // 避免读取上一帧 IK 结果形成反馈循环，使角色越站双腿越直。
            pelvisData.position = Vector3.zero;
            // 单个 Rig 权重负责整体淡入淡出，约束自身保持满权重，避免两级权重相乘导致脚掌响应迟缓。
            pelvisConstraint.weight = 1f;
            leftFootConstraint.weight = 1f;
            rightFootConstraint.weight = 1f;
        }

        /// <summary>
        /// 从当前已校准目标上方向地面射线采样，只平滑修正垂直位置，不覆盖 Generic 脚骨旋转。
        /// </summary>
        private void UpdateFootTarget(Transform target)
        {
            var origin = target.position + Vector3.up * RayStartHeight;
            var hasGround = Physics.Raycast(
                origin,
                Vector3.down,
                out var hit,
                RayDistance,
                player.GroundLayer,
                QueryTriggerInteraction.Ignore);
            if (!hasGround)
            {
                return;
            }

            var desiredPosition = hit.point + hit.normal * SoleOffset;
            var follow = 1f - Mathf.Exp(-TargetFollowSpeed * Time.deltaTime);
            target.position = Vector3.Lerp(target.position, desiredPosition, follow);
        }

        private void PrepareRigForSafeStartup()
        {
            if (footRig != null)
            {
                footRig.weight = 0f;
            }

            // Ellen 是 Generic Avatar，脚骨本地轴并不等同于世界前/上方向。
            // 只让 Two Bone IK 修正位置，保留动画剪辑原本的脚掌旋转，避免整条腿被扭转。
            SetPositionOnly(leftFootConstraint);
            SetPositionOnly(rightFootConstraint);
        }

        private static void SetPositionOnly(TwoBoneIKConstraint constraint)
        {
            if (constraint == null)
            {
                return;
            }

            ref var data = ref constraint.data;
            data.targetPositionWeight = 1f;
            data.targetRotationWeight = 0f;
        }

        private void SnapTargetsToAnimatedFeet()
        {
            SnapTarget(leftFoot, leftTarget);
            SnapTarget(rightFoot, rightTarget);
        }

        private static void SnapTarget(Transform foot, Transform target)
        {
            if (foot == null || target == null)
            {
                return;
            }

            target.SetPositionAndRotation(foot.position, foot.rotation);
        }

        private bool HasValidSetup()
        {
            return player != null && footRig != null &&
                   leftFootConstraint != null && rightFootConstraint != null && pelvisConstraint != null &&
                   leftFoot != null && rightFoot != null && leftTarget != null && rightTarget != null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            DrawFootProbe(leftFoot, leftTarget);
            DrawFootProbe(rightFoot, rightTarget);
        }

        private static void DrawFootProbe(Transform foot, Transform target)
        {
            if (foot == null)
            {
                return;
            }

            var origin = foot.position + Vector3.up * RayStartHeight;
            Gizmos.DrawLine(origin, origin + Vector3.down * RayDistance);
            if (target != null)
            {
                Gizmos.DrawWireSphere(target.position, 0.04f);
            }
        }
    }
}
