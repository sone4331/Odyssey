using System;
using System.Collections.Generic;
using Odyssey.Core.FSM;
using Odyssey.Gameplay.Characters;
using Odyssey.Characters.Enemies;
using UnityEngine;

namespace Odyssey.Characters.Player
{
    /// <summary>
    /// 玩家动作轴的 Unity 适配器，负责攻击、冲刺和受击的表现与移动覆盖。
    /// 采用正交状态机、命令请求和复用物理查询缓冲区，让动作不再伪装成 Grounded/Airborne 的子类，也避免每次连击产生状态对象和物理数组。
    /// </summary>
    internal sealed class PlayerActionRuntime
    {
        private readonly PlayerController _player;
        private readonly DeferredStateMachine<PlayerActionStateId> _machine;
        private readonly FreeState _freeState;
        private readonly AttackState _attackState;
        private readonly DashState _dashState;
        private readonly HitState _hitState;
        private PlayerActionRequest _pendingRequest;
        private Vector3 _pendingKnockback;

        public PlayerActionRuntime(PlayerController player)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _freeState = new FreeState(this);
            _attackState = new AttackState(this);
            _dashState = new DashState(this);
            _hitState = new HitState(this);
            _machine = new DeferredStateMachine<PlayerActionStateId>(
                new Dictionary<PlayerActionStateId, IState<PlayerActionStateId>>
                {
                    [PlayerActionStateId.Free] = _freeState,
                    [PlayerActionStateId.Attack] = _attackState,
                    [PlayerActionStateId.Dash] = _dashState,
                    [PlayerActionStateId.Hit] = _hitState
                });
        }

        public PlayerActionStateId CurrentStateId => _machine.CurrentId;
        public bool BlocksLocomotion => CurrentStateId != PlayerActionStateId.Free || _pendingRequest != PlayerActionRequest.None;

        /// <summary>
        /// 初始化动作轴为空闲状态；动作状态不拥有玩家生命或输入资源，避免生命周期互相订阅。
        /// </summary>
        public void Initialize()
        {
            _pendingRequest = PlayerActionRequest.None;
            _machine.Initialize(PlayerActionStateId.Free);
        }

        /// <summary>
        /// 让动作轴在本帧末统一提交输入或受击请求，调用方不直接切换状态。
        /// 受击请求拥有最高优先级，即使攻击或冲刺正在执行也会在下一次 Tick 安全打断。
        /// </summary>
        public void Tick(float deltaTime)
        {
            _machine.Tick(deltaTime);
        }

        /// <summary>
        /// 提交攻击意图；连击期间只记录下一招，不重新创建或切换同名状态。
        /// </summary>
        public bool RequestAttack()
        {
            if (CurrentStateId == PlayerActionStateId.Attack)
            {
                _attackState.QueueNextCombo();
                return true;
            }

            if (CurrentStateId != PlayerActionStateId.Free || !CanActivate(PlayerController.AttackAbilityId))
            {
                return false;
            }

            _pendingRequest = PlayerActionRequest.Attack;
            return true;
        }

        /// <summary>
        /// 提交冲刺意图，冷却和标签约束由共享 AbilitySystem 统一校验。
        /// </summary>
        public bool RequestDash()
        {
            if (CurrentStateId != PlayerActionStateId.Free || !CanActivate(PlayerController.DashAbilityId))
            {
                return false;
            }

            _pendingRequest = PlayerActionRequest.Dash;
            return true;
        }

        /// <summary>
        /// 提交受击意图并携带击退动量，动作层只保存值对象，不依赖碰撞来源对象的生命周期。
        /// </summary>
        public void RequestHit(Vector3 knockback)
        {
            _pendingKnockback = knockback;
            _pendingRequest = PlayerActionRequest.Hit;
        }

        /// <summary>
        /// 接收动画剪辑作者标注的命中窗口，使玩法判定与真实挥击帧对齐，而不是猜测固定归一化时间。
        /// </summary>
        public void OpenAttackWindow()
        {
            if (CurrentStateId == PlayerActionStateId.Attack)
            {
                _attackState.SetDamageWindow(true);
            }
        }

        public void CloseAttackWindow()
        {
            if (CurrentStateId == PlayerActionStateId.Attack)
            {
                _attackState.SetDamageWindow(false);
            }
        }

        /// <summary>
        /// 复活时结束当前动作并清理待处理命令，保证旧的攻击命中窗口不会穿透到新生命周期。
        /// </summary>
        public void Reset()
        {
            _pendingRequest = PlayerActionRequest.None;
            _pendingKnockback = Vector3.zero;
            _machine.Reset(PlayerActionStateId.Free);
        }

        private bool CanActivate(string abilityId)
        {
            return _player.TryActivateAbility(abilityId);
        }

        private abstract class ActionState : IState<PlayerActionStateId>
        {
            protected ActionState(PlayerActionRuntime runtime)
            {
                Runtime = runtime;
            }

            protected PlayerActionRuntime Runtime { get; }
            protected PlayerController Player => Runtime._player;

            public abstract void Enter();
            public abstract void Exit();
            public abstract StateTransition<PlayerActionStateId> Tick(float deltaTime);

            protected StateTransition<PlayerActionStateId> ReadInterruptRequest()
            {
                var request = Runtime._pendingRequest;
                if (request == PlayerActionRequest.Hit)
                {
                    return StateTransition<PlayerActionStateId>.To(PlayerActionStateId.Hit);
                }

                return StateTransition<PlayerActionStateId>.None;
            }

            protected void ClearRequest()
            {
                Runtime._pendingRequest = PlayerActionRequest.None;
            }
        }

        private sealed class FreeState : ActionState
        {
            public FreeState(PlayerActionRuntime runtime) : base(runtime)
            {
            }

            public override void Enter()
            {
                ClearRequest();
            }

            public override void Exit()
            {
            }

            public override StateTransition<PlayerActionStateId> Tick(float deltaTime)
            {
                var request = Runtime._pendingRequest;
                if (request == PlayerActionRequest.Hit)
                {
                    return StateTransition<PlayerActionStateId>.To(PlayerActionStateId.Hit);
                }

                if (request == PlayerActionRequest.Attack)
                {
                    return StateTransition<PlayerActionStateId>.To(PlayerActionStateId.Attack);
                }

                if (request == PlayerActionRequest.Dash)
                {
                    return StateTransition<PlayerActionStateId>.To(PlayerActionStateId.Dash);
                }

                return StateTransition<PlayerActionStateId>.None;
            }
        }

        private sealed class AttackState : ActionState
        {
            private static readonly Collider[] HitBuffer = new Collider[16];
            private static readonly float[] ComboCommitTimes = { 0.38f, 0.34f, 0.41f, 1f };
            private static readonly float[] RecoveryTimes = { 0.65f, 0.65f, 0.65f, 0.72f };
            private readonly HashSet<Enemy> _damagedEnemies = new HashSet<Enemy>();
            private int _comboIndex;
            private bool _comboQueued;
            private bool _damageWindowOpen;
            private float _timer;
            private Quaternion _targetFacing;

            public AttackState(PlayerActionRuntime runtime) : base(runtime)
            {
            }

            public void QueueNextCombo()
            {
                _comboQueued = true;
            }

            public void SetDamageWindow(bool isOpen)
            {
                _damageWindowOpen = isOpen;
            }

            public override void Enter()
            {
                ClearRequest();
                _comboIndex = 1;
                _comboQueued = false;
                _damageWindowOpen = false;
                _damagedEnemies.Clear();
                _timer = 0f;
                Player.VerticalVelocity = 0f;
                Player.Controller.Move(Vector3.zero);
                Player.StopPlanarMotion();
                Player.Animation.SetLocomotionSpeed(0f, 0f);
                Player.Animation.PlayAttack(_comboIndex);
                CaptureTargetFacing();
            }

            public override void Exit()
            {
                Player.EndAbility(PlayerController.AttackAbilityId);
                Player.Animation.Recover(Player.LocomotionState, Player.VerticalVelocity);
            }

            public override StateTransition<PlayerActionStateId> Tick(float deltaTime)
            {
                var interrupt = ReadInterruptRequest();
                if (interrupt.IsRequested)
                {
                    return interrupt;
                }

                _timer += deltaTime;
                // 攻击期间仍提交地面吸附速度，避免 CharacterController 因只有零位移而丢失接地事实。
                var groundStickVelocity = Player.LocomotionState == PlayerLocomotionStateId.Grounded
                    ? Vector3.up * Player.VerticalVelocity
                    : Vector3.zero;
                var advanceVelocity = _damageWindowOpen
                    ? Player.transform.forward * Player.AttackAdvanceSpeed
                    : Vector3.zero;
                Player.Controller.Move((groundStickVelocity + advanceVelocity) * deltaTime);
                Player.transform.rotation = Quaternion.RotateTowards(
                    Player.transform.rotation,
                    _targetFacing,
                    720f * deltaTime);

                if (_damageWindowOpen)
                {
                    ApplyDamage();
                }

                var info = Player.Animator.GetCurrentAnimatorStateInfo(0);
                var normalizedTime = _timer < 0.15f
                    ? 0f
                    : info.IsTag("Attack") ? info.normalizedTime : _timer / 0.8f;
                var comboSlot = _comboIndex - 1;
                if (normalizedTime >= ComboCommitTimes[comboSlot] && _comboQueued && _comboIndex < 4)
                {
                    _comboIndex++;
                    _comboQueued = false;
                    _damageWindowOpen = false;
                    _damagedEnemies.Clear();
                    _timer = 0f;
                    Player.Animation.PlayAttack(_comboIndex);
                    return StateTransition<PlayerActionStateId>.None;
                }

                return normalizedTime >= RecoveryTimes[comboSlot]
                    ? StateTransition<PlayerActionStateId>.To(PlayerActionStateId.Free)
                    : StateTransition<PlayerActionStateId>.None;
            }

            private void ApplyDamage()
            {
                var center = Player.transform.position + Player.transform.forward + Vector3.up * 0.5f;
                var count = Physics.OverlapSphereNonAlloc(
                    center,
                    Player.AttackRange,
                    HitBuffer,
                    Player.EnemyLayer,
                    QueryTriggerInteraction.Ignore);
                for (var i = 0; i < count; i++)
                {
                    var enemy = HitBuffer[i].GetComponentInParent<Enemy>();
                    if (enemy != null && _damagedEnemies.Add(enemy))
                    {
                        enemy.TakeDamage(Player.AttackDamage);
                    }
                }
            }

            private void CaptureTargetFacing()
            {
                var direction = Player.DesiredMoveDirection;
                if (direction == Vector3.zero && Player.MainCameraTransform != null)
                {
                    direction = Player.MainCameraTransform.forward;
                }

                direction.y = 0f;
                if (direction != Vector3.zero)
                {
                    _targetFacing = Quaternion.LookRotation(direction.normalized);
                }
                else
                {
                    _targetFacing = Player.transform.rotation;
                }
            }
        }

        private sealed class DashState : ActionState
        {
            private float _remaining;
            private Vector3 _direction;

            public DashState(PlayerActionRuntime runtime) : base(runtime)
            {
            }

            public override void Enter()
            {
                ClearRequest();
                _remaining = Player.DashDuration;
                _direction = Player.DesiredMoveDirection == Vector3.zero
                    ? Player.transform.forward
                    : Player.DesiredMoveDirection.normalized;
                Player.StopPlanarMotion();
                if (!Player.Controller.isGrounded)
                {
                    Player.CanAirDash = false;
                }

                Player.Animation.PlayDash(Player.LocomotionState, Player.VerticalVelocity);
            }

            public override void Exit()
            {
                Player.EndAbility(PlayerController.DashAbilityId);
                if (Player.InputReader != null && Player.InputReader.MovementValue.sqrMagnitude > 0.01f)
                {
                    // 玩家仍保持方向时让移动轴以奔跑上限接管，消除冲刺结束后从零重新加速的顿挫。
                    Player.ResumePlanarMotionAtMaximumSpeed(_direction);
                }

                Player.Animation.Recover(Player.LocomotionState, Player.VerticalVelocity);
            }

            public override StateTransition<PlayerActionStateId> Tick(float deltaTime)
            {
                var interrupt = ReadInterruptRequest();
                if (interrupt.IsRequested)
                {
                    return interrupt;
                }

                _remaining -= deltaTime;
                // 地面冲刺同时保留向下的贴地速度；否则纯水平 Move 会让 isGrounded 短暂失真并误播下落动画。
                var groundStickVelocity = Player.LocomotionState == PlayerLocomotionStateId.Grounded
                    ? Vector3.up * Player.VerticalVelocity
                    : Vector3.zero;
                Player.Controller.Move((_direction * Player.DashForce + groundStickVelocity) * deltaTime);
                return _remaining <= 0f
                    ? StateTransition<PlayerActionStateId>.To(PlayerActionStateId.Free)
                    : StateTransition<PlayerActionStateId>.None;
            }
        }

        private sealed class HitState : ActionState
        {
            private float _remaining;
            private Vector3 _knockback;

            public HitState(PlayerActionRuntime runtime) : base(runtime)
            {
            }

            public override void Enter()
            {
                _knockback = Runtime._pendingKnockback;
                ClearRequest();
                _remaining = 0.5f;
                Player.StopPlanarMotion();
                Player.Animation.PlayHit();
                Player.CanAirJump = false;
                Player.CanAirDash = false;
            }

            public override void Exit()
            {
                Player.EndAbility(PlayerController.HitAbilityId);
                Player.Animation.Recover(Player.LocomotionState, Player.VerticalVelocity);
            }

            public override StateTransition<PlayerActionStateId> Tick(float deltaTime)
            {
                _remaining -= deltaTime;
                Player.VerticalVelocity += Player.Gravity * deltaTime;
                _knockback = Vector3.Lerp(_knockback, Vector3.zero, deltaTime * 5f);
                Player.Controller.Move((_knockback + Vector3.up * Player.VerticalVelocity) * deltaTime);
                return _remaining <= 0f
                    ? StateTransition<PlayerActionStateId>.To(PlayerActionStateId.Free)
                    : StateTransition<PlayerActionStateId>.None;
            }
        }
    }
}
