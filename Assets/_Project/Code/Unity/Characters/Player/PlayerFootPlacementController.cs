using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Odyssey.Characters.Player
{
    /// <summary>
    /// 驱动 Generic Ellen 的左右脚目标和骨盆补偿，使走跑与落地姿势贴合斜坡而不改变 CharacterController 权威位置。
    /// 采用表现层 Animation Rigging Adapter：玩法状态只决定权重开关，Two Bone IK 只修改最终骨骼姿势，不回写移动逻辑。
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class PlayerFootPlacementController : MonoBehaviour
    {
        private const float RayStartHeight = 0.25f;
        private const float RayDistance = 0.65f;
        private const float SoleOffset = 0.03f;
        private const float MaximumPelvisOffset = 0.12f;
        private const float WeightSpeed = 10f;
        private const float TargetFollowSpeed = 14f;

        [SerializeField] private PlayerController player;
        [SerializeField] private Rig footRig;
        [SerializeField] private TwoBoneIKConstraint leftFootConstraint;
        [SerializeField] private TwoBoneIKConstraint rightFootConstraint;
        [SerializeField] private OverrideTransform pelvisConstraint;
        [SerializeField] private Transform leftFoot;
        [SerializeField] private Transform rightFoot;
        [SerializeField] private Transform leftTarget;
        [SerializeField] private Transform rightTarget;

        private float _pelvisOffset;

        public float CurrentWeight => footRig == null ? 0f : footRig.weight;

        private void Awake()
        {
            player ??= GetComponent<PlayerController>();
        }

        private void Update()
        {
            if (!HasValidSetup())
            {
                return;
            }

            var shouldPlaceFeet = player.LocomotionState == Odyssey.Gameplay.Characters.PlayerLocomotionStateId.Grounded &&
                                  player.ActionState == Odyssey.Gameplay.Characters.PlayerActionStateId.Free;
            footRig.weight = Mathf.MoveTowards(
                footRig.weight,
                shouldPlaceFeet ? 1f : 0f,
                WeightSpeed * Time.deltaTime);

            var leftOffset = UpdateFootTarget(leftFoot, leftTarget);
            var rightOffset = UpdateFootTarget(rightFoot, rightTarget);
            var desiredPelvisOffset = shouldPlaceFeet
                ? Mathf.Clamp(Mathf.Min(leftOffset, rightOffset), -MaximumPelvisOffset, MaximumPelvisOffset)
                : 0f;
            _pelvisOffset = Mathf.MoveTowards(_pelvisOffset, desiredPelvisOffset, WeightSpeed * Time.deltaTime);

            ref var pelvisData = ref pelvisConstraint.data;
            pelvisData.position = Vector3.up * _pelvisOffset;
            // 单个 Rig 权重负责整体淡入淡出，约束自身保持满权重，避免两级权重相乘导致脚掌响应迟缓。
            pelvisConstraint.weight = 1f;
            leftFootConstraint.weight = 1f;
            rightFootConstraint.weight = 1f;
        }

        /// <summary>
        /// 从动画脚掌上方向地面射线采样，平滑更新目标位置和朝向，并返回脚掌所需的垂直补偿量。
        /// </summary>
        private float UpdateFootTarget(Transform foot, Transform target)
        {
            var origin = foot.position + Vector3.up * RayStartHeight;
            var hasGround = Physics.Raycast(
                origin,
                Vector3.down,
                out var hit,
                RayDistance,
                player.GroundLayer,
                QueryTriggerInteraction.Ignore);
            var desiredPosition = hasGround ? hit.point + hit.normal * SoleOffset : foot.position;
            var desiredRotation = foot.rotation;
            if (hasGround)
            {
                var forward = Vector3.ProjectOnPlane(player.transform.forward, hit.normal);
                if (forward.sqrMagnitude > 0.0001f)
                {
                    desiredRotation = Quaternion.LookRotation(forward.normalized, hit.normal);
                }
            }

            var follow = 1f - Mathf.Exp(-TargetFollowSpeed * Time.deltaTime);
            target.position = Vector3.Lerp(target.position, desiredPosition, follow);
            target.rotation = Quaternion.Slerp(target.rotation, desiredRotation, follow);
            return hasGround ? desiredPosition.y - foot.position.y : 0f;
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
