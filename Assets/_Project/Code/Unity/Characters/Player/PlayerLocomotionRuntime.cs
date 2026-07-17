using System;
using System.Collections.Generic;
using Odyssey.Core.FSM;
using Odyssey.Gameplay.Characters;
using Odyssey.Characters.Enemies;
using Odyssey.Inputs;
using UnityEngine;

namespace Odyssey.Characters.Player
{
    /// <summary>
    /// 玩家移动轴的 Unity 适配器，负责把 CharacterController、输入和物理感知转换为可测试的状态机事实。
    /// 采用组合模式持有 Grounded、Airborne、WallSlide 三个缓存状态，并使用延迟提交 FSM 防止状态切换后旧逻辑继续执行。
    /// 这样攻击、冲刺和受击可以由另一条动作轴独立运行，不再制造 IdleAttack、AirDash 等组合状态。
    /// </summary>
    internal sealed class PlayerLocomotionRuntime
    {
        private readonly PlayerController _player;
        private readonly DeferredStateMachine<PlayerLocomotionStateId> _machine;
        private readonly GroundedState _groundedState;
        private readonly AirborneState _airborneState;
        private readonly WallSlideState _wallSlideState;
        private readonly PlayerWallClearanceSolver _wallClearance;
        private float _currentPlanarSpeed;
        private Vector3 _desiredMoveDirection;
        private float _groundSlopeAngle;
        private bool _preserveRunSpeedUntilInputReleased;

        public PlayerLocomotionRuntime(PlayerController player)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _groundedState = new GroundedState(this);
            _airborneState = new AirborneState(this);
            _wallSlideState = new WallSlideState(this);
            _wallClearance = new PlayerWallClearanceSolver(player);
            _machine = new DeferredStateMachine<PlayerLocomotionStateId>(
                new Dictionary<PlayerLocomotionStateId, IState<PlayerLocomotionStateId>>
                {
                    [PlayerLocomotionStateId.Grounded] = _groundedState,
                    [PlayerLocomotionStateId.Airborne] = _airborneState,
                    [PlayerLocomotionStateId.WallSlide] = _wallSlideState
                });
        }

        public PlayerLocomotionStateId CurrentStateId => _machine.CurrentId;
        public Vector3 WallNormal { get; set; }
        public Vector3 Momentum { get; set; }
        public float CurrentPlanarSpeed => _currentPlanarSpeed;
        public Vector3 DesiredMoveDirection => _desiredMoveDirection;
        public float GroundSlopeAngle => _groundSlopeAngle;
        public bool WallClearanceActive => _wallClearance.IsActive;

        /// <summary>
        /// 进入初始地面状态。初始化只执行一次，复活应调用 Reset，避免重复装配导致事件和计时器残留。
        /// </summary>
        public void Initialize()
        {
            _machine.Initialize(PlayerLocomotionStateId.Grounded);
        }

        /// <summary>
        /// 驱动移动轴；动作轴阻塞移动时仍更新感知和状态，但不重复应用水平位移。
        /// 采用“感知、决策、表现”顺序，让状态迁移在本帧末提交，保证每帧只有一个状态负责移动。
        /// </summary>
        public void Tick(float deltaTime, bool movementEnabled)
        {
            _player.MovementEnabled = movementEnabled;
            _machine.Tick(deltaTime);
        }

        /// <summary>
        /// 复活时清理移动轴的临时动量和旧状态生命周期，再从地面状态开始。
        /// </summary>
        public void Reset()
        {
            Momentum = Vector3.zero;
            WallNormal = Vector3.zero;
            _currentPlanarSpeed = 0f;
            _desiredMoveDirection = Vector3.zero;
            _groundSlopeAngle = 0f;
            _preserveRunSpeedUntilInputReleased = false;
            _machine.Reset(PlayerLocomotionStateId.Grounded);
        }

        /// <summary>
        /// 在攻击、受击等明确的动作边界清空地面惯性，避免动作结束后恢复旧速度造成滑步。
        /// </summary>
        public void StopPlanarMotion()
        {
            _currentPlanarSpeed = 0f;
            _desiredMoveDirection = Vector3.zero;
            _preserveRunSpeedUntilInputReleased = false;
        }

        /// <summary>
        /// 冲刺结束且玩家仍保持方向输入时，以奔跑上限把位移权交还给移动轴。
        /// 采用短生命周期速度继承而非修改基础加速度：持续按住方向保持奔跑，松开后立即恢复普通 Walk/Run 规则。
        /// </summary>
        public void ResumeAtMaximumSpeed(Vector3 dashDirection)
        {
            _currentPlanarSpeed = _player.RunSpeed;
            _desiredMoveDirection = dashDirection.sqrMagnitude > 0.0001f
                ? dashDirection.normalized
                : _player.transform.forward;
            _preserveRunSpeedUntilInputReleased = true;
            _player.Animation.SetLocomotionSpeed(1f, 0f);
        }

        private abstract class LocomotionState : IState<PlayerLocomotionStateId>
        {
            protected LocomotionState(PlayerLocomotionRuntime runtime)
            {
                Runtime = runtime;
            }

            protected PlayerLocomotionRuntime Runtime { get; }
            protected PlayerController Player => Runtime._player;

            public abstract void Enter();
            public abstract void Exit();
            public abstract StateTransition<PlayerLocomotionStateId> Tick(float deltaTime);

            protected Vector3 ReadCameraDirection(Vector2 input)
            {
                var camera = Player.MainCameraTransform;
                var forward = camera == null ? Vector3.forward : camera.forward;
                var right = camera == null ? Vector3.right : camera.right;
                forward.y = 0f;
                right.y = 0f;
                forward.Normalize();
                right.Normalize();
                return forward * input.y + right * input.x;
            }

            protected void ApplyGroundMovement(
                float deltaTime,
                float speedMultiplier = 1f,
                Vector3 additionalVelocity = default)
            {
                if (!Player.MovementEnabled)
                {
                    return;
                }

                var input = Player.InputReader == null
                    ? Vector2.zero
                    : Vector2.ClampMagnitude(Player.InputReader.MovementValue, 1f);
                var direction = ReadCameraDirection(input);
                if (direction.sqrMagnitude > 1f)
                {
                    direction.Normalize();
                }

                Runtime._desiredMoveDirection = direction;
                if (input.sqrMagnitude <= 0.0001f)
                {
                    Runtime._preserveRunSpeedUntilInputReleased = false;
                }

                var shouldRun = Player.InputReader != null && Player.InputReader.IsSprinting ||
                                Runtime._preserveRunSpeedUntilInputReleased;
                var maximumSpeed = (shouldRun ? Player.RunSpeed : Player.WalkSpeed) * speedMultiplier;
                var targetSpeed = input.magnitude * maximumSpeed;
                var acceleration = targetSpeed > Runtime._currentPlanarSpeed
                    ? Player.GroundAcceleration
                    : Player.GroundDeceleration;
                Runtime._currentPlanarSpeed = Mathf.MoveTowards(
                    Runtime._currentPlanarSpeed,
                    targetSpeed,
                    acceleration * deltaTime);

                if (direction != Vector3.zero)
                {
                    var speedRatio = Mathf.Clamp01(Runtime._currentPlanarSpeed / Mathf.Max(0.01f, Player.RunSpeed));
                    var turnSpeed = Mathf.Lerp(Player.MaxTurnSpeed, Player.MinTurnSpeed, speedRatio);
                    if (Runtime.CurrentStateId != PlayerLocomotionStateId.Grounded)
                    {
                        turnSpeed *= 0.65f;
                    }

                    Player.transform.rotation = Quaternion.RotateTowards(
                        Player.transform.rotation,
                        Quaternion.LookRotation(direction),
                        turnSpeed * deltaTime);
                }

                var movementDirection = Player.transform.forward;
                Runtime._groundSlopeAngle = 0f;
                if (Runtime.CurrentStateId == PlayerLocomotionStateId.Grounded &&
                    TryGetGroundHit(out var groundHit))
                {
                    Runtime._groundSlopeAngle = Vector3.Angle(Vector3.up, groundHit.normal);
                    movementDirection = PlayerMovementMath.ProjectDirectionOnGround(
                        movementDirection,
                        groundHit.normal);
                }

                var velocity = movementDirection * Runtime._currentPlanarSpeed + additionalVelocity;
                velocity.y += Player.VerticalVelocity;
                var displacement = velocity * deltaTime;
                if (Runtime.CurrentStateId == PlayerLocomotionStateId.Grounded)
                {
                    displacement = Runtime._wallClearance.Constrain(displacement);
                }

                Player.Controller.Move(displacement);

                var animationSpeed = Runtime._currentPlanarSpeed / Mathf.Max(0.01f, Player.RunSpeed);
                Player.Animation.SetLocomotionSpeed(animationSpeed, deltaTime);
            }

            /// <summary>
            /// 使用略小于胶囊半径的球形探测读取稳定坡面法线，避免单点射线在台阶边缘频繁跳变。
            /// </summary>
            private bool TryGetGroundHit(out RaycastHit hit)
            {
                var radius = Mathf.Max(0.05f, Player.Controller.radius - Player.Controller.skinWidth - 0.02f);
                var origin = Player.transform.position + Vector3.up * (radius + 0.15f);
                return Physics.SphereCast(
                    origin,
                    radius,
                    Vector3.down,
                    out hit,
                    radius + 0.35f,
                    Player.GroundLayer,
                    QueryTriggerInteraction.Ignore);
            }

            protected bool TryFindWall(out RaycastHit hit)
            {
                var start = Player.transform.position + Vector3.up;
                Debug.DrawRay(start, Player.transform.forward, Color.yellow);
                return Physics.Raycast(
                    start,
                    Player.transform.forward,
                    out hit,
                    1f,
                    Player.WallLayer,
                    QueryTriggerInteraction.Ignore);
            }

            protected bool IsWallNormalUsable(Vector3 normal)
            {
                var angle = Vector3.Angle(Vector3.up, normal);
                return angle > 70f && angle < 110f;
            }

            protected StateTransition<PlayerLocomotionStateId> TransitionIfNeeded(
                PlayerLocomotionObservation observation)
            {
                var next = PlayerLocomotionTransitionPolicy.SelectNext(
                    Runtime.CurrentStateId,
                    observation);
                return next == Runtime.CurrentStateId
                    ? StateTransition<PlayerLocomotionStateId>.None
                    : StateTransition<PlayerLocomotionStateId>.To(next);
            }
        }

        private sealed class GroundedState : LocomotionState
        {
            private const float LandingImpactThreshold = -6f;
            private const float LandingInputUnlockTime = 0.08f;
            private float _groundGraceTimer;
            private float _chargeTimer;
            private float _landingRemaining;
            private float _landingUnlockRemaining;
            private bool _charging;

            public GroundedState(PlayerLocomotionRuntime runtime) : base(runtime)
            {
            }

            public override void Enter()
            {
                var landingVelocity = Player.VerticalVelocity;
                Player.VerticalVelocity = -5f;
                _groundGraceTimer = 0.2f;
                _chargeTimer = 0f;
                _charging = false;
                Player.CanAirJump = true;
                Player.CanAirDash = true;
                if (Player.MovementEnabled)
                {
                    if (landingVelocity <= LandingImpactThreshold)
                    {
                        var speedRatio = Runtime._currentPlanarSpeed / Mathf.Max(0.01f, Player.RunSpeed);
                        _landingRemaining = Runtime._currentPlanarSpeed > 0.1f ? 0.22f : 0.32f;
                        _landingUnlockRemaining = LandingInputUnlockTime;
                        Player.Animation.PlayLanding(speedRatio);
                    }
                    else
                    {
                        _landingRemaining = 0f;
                        _landingUnlockRemaining = 0f;
                        Player.Animation.PlayGrounded();
                    }
                }
            }

            public override void Exit()
            {
                _charging = false;
            }

            public override StateTransition<PlayerLocomotionStateId> Tick(float deltaTime)
            {
                var input = Player.InputReader;
                if (!Player.MovementEnabled)
                {
                    // 动作轴拥有控制权时取消蓄力，避免攻击或冲刺结束后补触发一次旧跳跃。
                    _charging = false;
                    _chargeTimer = 0f;
                    _landingRemaining = 0f;
                }
                else if (input != null && input.IsJumpPressed)
                {
                    _charging = true;
                    _chargeTimer += deltaTime;
                }
                else if (_charging)
                {
                    _charging = false;
                    var jumpHeight = _chargeTimer >= Player.MinChargeTime
                        ? Player.ChargeJumpHeight
                        : Player.JumpHeight;
                    Player.VerticalVelocity = Mathf.Sqrt(-2f * Player.Gravity * jumpHeight);
                    Player.Animation.PlayJump();
                    return StateTransition<PlayerLocomotionStateId>.To(PlayerLocomotionStateId.Airborne);
                }

                if (!Player.Controller.isGrounded)
                {
                    _groundGraceTimer -= deltaTime;
                    Player.VerticalVelocity += Player.Gravity * deltaTime;
                    if (_groundGraceTimer <= 0f)
                    {
                        return StateTransition<PlayerLocomotionStateId>.To(PlayerLocomotionStateId.Airborne);
                    }
                }
                else
                {
                    _groundGraceTimer = 0.2f;
                    Player.VerticalVelocity = -10f;
                }

                ApplyGroundMovement(deltaTime);
                UpdateLandingPresentation(input, deltaTime);
                return StateTransition<PlayerLocomotionStateId>.None;
            }

            private void UpdateLandingPresentation(InputReader input, float deltaTime)
            {
                if (_landingRemaining <= 0f || !Player.MovementEnabled)
                {
                    return;
                }

                _landingRemaining -= deltaTime;
                _landingUnlockRemaining -= deltaTime;
                var hasMoveInput = input != null && input.MovementValue.sqrMagnitude > 0.04f;
                if (_landingRemaining <= 0f || (_landingUnlockRemaining <= 0f && hasMoveInput))
                {
                    _landingRemaining = 0f;
                    Player.Animation.PlayGrounded();
                }
            }
        }

        private sealed class AirborneState : LocomotionState
        {
            private static readonly RaycastHit[] StompHits = new RaycastHit[8];
            private bool _fallAnimationStarted;

            public AirborneState(PlayerLocomotionRuntime runtime) : base(runtime)
            {
            }

            public override void Enter()
            {
                _fallAnimationStarted = Player.VerticalVelocity <= 0f;
                if (!Player.MovementEnabled)
                {
                    return;
                }

                Player.Animation.PlayAirborne(Player.VerticalVelocity);
            }

            public override void Exit()
            {
            }

            public override StateTransition<PlayerLocomotionStateId> Tick(float deltaTime)
            {
                var input = Player.InputReader;
                if (Player.MovementEnabled && input != null && input.IsJumpPressed && Player.CanAirJump)
                {
                    input.ConsumeJump();
                    Player.CanAirJump = false;
                    Player.VerticalVelocity = Mathf.Sqrt(-2f * Player.Gravity * Player.AirJumpHeight);
                    _fallAnimationStarted = false;
                    Player.Animation.PlayAirborne(Player.VerticalVelocity);
                }

                if (Player.MovementEnabled)
                {
                    Player.VerticalVelocity = Mathf.Max(
                        Player.VerticalVelocity + Player.Gravity * deltaTime,
                        -20f);
                    Runtime.Momentum = Vector3.Lerp(Runtime.Momentum, Vector3.zero, deltaTime * 5f);
                }

                if (Player.MovementEnabled)
                {
                    Player.Animation.SetVerticalSpeed(Player.VerticalVelocity);
                    if (!_fallAnimationStarted && Player.VerticalVelocity <= 0f)
                    {
                        _fallAnimationStarted = true;
                    }
                }

                if (Player.MovementEnabled && Player.VerticalVelocity < 0f && TryStompEnemy())
                {
                    Player.VerticalVelocity = 8f;
                    Player.CanAirJump = true;
                    Player.CanAirDash = true;
                    _fallAnimationStarted = false;
                    Player.Animation.PlayAirborne(Player.VerticalVelocity);
                    return StateTransition<PlayerLocomotionStateId>.None;
                }

                var touchingWall = false;
                if (Player.VerticalVelocity < 0f && TryFindWall(out var wallHit) && IsWallNormalUsable(wallHit.normal))
                {
                    touchingWall = true;
                    Runtime.WallNormal = wallHit.normal;
                }

                if (Player.MovementEnabled)
                {
                    ApplyGroundMovement(deltaTime, 0.7f, Runtime.Momentum);
                }

                var observation = new PlayerLocomotionObservation(
                    Player.Controller.isGrounded && Player.VerticalVelocity < -2f,
                    touchingWall,
                    Player.VerticalVelocity < 0f,
                    false);
                var transition = TransitionIfNeeded(observation);
                return transition;
            }

            private bool TryStompEnemy()
            {
                var origin = Player.transform.position + Vector3.up * 0.1f;
                var count = Physics.SphereCastNonAlloc(
                    origin,
                    0.2f,
                    Vector3.down,
                    StompHits,
                    0.2f,
                    Player.EnemyLayer,
                    QueryTriggerInteraction.Ignore);
                for (var i = 0; i < count; i++)
                {
                    var enemy = StompHits[i].collider == null
                        ? null
                        : StompHits[i].collider.GetComponentInParent<Enemy>();
                    if (enemy == null)
                    {
                        continue;
                    }

                    enemy.TakeDamage(Player.AttackDamage);
                    return true;
                }

                return false;
            }
        }

        private sealed class WallSlideState : LocomotionState
        {
            public WallSlideState(PlayerLocomotionRuntime runtime) : base(runtime)
            {
            }

            public override void Enter()
            {
                if (Player.MovementEnabled)
                {
                    Player.Animation.PlayAirborne(Player.VerticalVelocity);
                }

                if (Runtime.WallNormal != Vector3.zero)
                {
                    Player.transform.forward = -Runtime.WallNormal;
                }
            }

            public override void Exit()
            {
            }

            public override StateTransition<PlayerLocomotionStateId> Tick(float deltaTime)
            {
                var input = Player.InputReader;
                var jumpRequested = Player.MovementEnabled && input != null && input.IsJumpPressed;
                if (jumpRequested)
                {
                    input.ConsumeJump();
                    Player.CanAirJump = true;
                    Player.CanAirDash = true;
                    Runtime.Momentum = Runtime.WallNormal * Player.WallJumpSideForce;
                    Player.VerticalVelocity = Player.WallJumpUpForce;
                    Player.transform.forward = Runtime.WallNormal;
                    return StateTransition<PlayerLocomotionStateId>.To(PlayerLocomotionStateId.Airborne);
                }

                var touchingWall = TryFindWall(out var hit) && IsWallNormalUsable(hit.normal);
                if (touchingWall)
                {
                    Runtime.WallNormal = hit.normal;
                }

                Player.VerticalVelocity = Player.WallSlideSpeed;
                if (Player.MovementEnabled)
                {
                    Player.Controller.Move((Vector3.up * Player.VerticalVelocity + Player.transform.forward * 0.5f) * deltaTime);
                }

                var observation = new PlayerLocomotionObservation(
                    Player.Controller.isGrounded,
                    touchingWall,
                    true,
                    jumpRequested);
                return TransitionIfNeeded(observation);
            }
        }
    }
}
