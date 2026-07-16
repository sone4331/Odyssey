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
    /// 采用正交状态机、命令请求和对象池式查询缓冲区，让动作不再伪装成 Grounded/Airborne 的子类，也避免每次连击产生状态对象和物理数组。
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
            private readonly HashSet<Enemy> _damagedEnemies = new HashSet<Enemy>();
            private int _comboIndex;
            private bool _comboQueued;
            private bool _damageApplied;
            private float _timer;

            public AttackState(PlayerActionRuntime runtime) : base(runtime)
            {
            }

            public void QueueNextCombo()
            {
                _comboQueued = true;
            }

            public override void Enter()
            {
                ClearRequest();
                _comboIndex = 1;
                _comboQueued = false;
                _damageApplied = false;
                _timer = 0f;
                Player.VerticalVelocity = 0f;
                Player.Controller.Move(Vector3.zero);
                Player.Animator.SetFloat("Speed", 0f);
                Player.Animator.SetInteger("ComboIndex", _comboIndex);
                Player.Animator.SetTrigger("Attack");
                FaceCameraForward();
            }

            public override void Exit()
            {
                Player.EndAbility(PlayerController.AttackAbilityId);
            }

            public override StateTransition<PlayerActionStateId> Tick(float deltaTime)
            {
                var interrupt = ReadInterruptRequest();
                if (interrupt.IsRequested)
                {
                    return interrupt;
                }

                _timer += deltaTime;
                Player.Controller.Move(Vector3.zero);
                var info = Player.Animator.GetCurrentAnimatorStateInfo(0);
                var normalizedTime = _timer < 0.15f
                    ? 0f
                    : info.IsTag("Attack") ? info.normalizedTime : _timer / 0.8f;
                if (normalizedTime >= 0.3f && !_damageApplied)
                {
                    ApplyDamage();
                    _damageApplied = true;
                }

                if (normalizedTime >= 0.4f && normalizedTime < 0.9f && _comboQueued && _comboIndex < 4)
                {
                    _comboIndex++;
                    _comboQueued = false;
                    _damageApplied = false;
                    _timer = 0f;
                    Player.Animator.SetInteger("ComboIndex", _comboIndex);
                    Player.Animator.SetTrigger("Attack");
                    return StateTransition<PlayerActionStateId>.None;
                }

                return normalizedTime >= 0.95f
                    ? StateTransition<PlayerActionStateId>.To(PlayerActionStateId.Free)
                    : StateTransition<PlayerActionStateId>.None;
            }

            private void ApplyDamage()
            {
                _damagedEnemies.Clear();
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

            private void FaceCameraForward()
            {
                var camera = Player.MainCameraTransform;
                if (camera == null)
                {
                    return;
                }

                var direction = camera.forward;
                direction.y = 0f;
                if (direction != Vector3.zero)
                {
                    Player.transform.forward = direction.normalized;
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
                _direction = Player.transform.forward;
                if (!Player.Controller.isGrounded)
                {
                    Player.CanAirDash = false;
                }

                Player.Animator.SetTrigger("Run");
                Player.Animator.speed = 2.5f;
            }

            public override void Exit()
            {
                Player.EndAbility(PlayerController.DashAbilityId);
                Player.Animator.speed = 1f;
            }

            public override StateTransition<PlayerActionStateId> Tick(float deltaTime)
            {
                var interrupt = ReadInterruptRequest();
                if (interrupt.IsRequested)
                {
                    return interrupt;
                }

                _remaining -= deltaTime;
                Player.Controller.Move(_direction * Player.DashForce * deltaTime);
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
                Player.Animator.SetTrigger("Hit");
                Player.CanAirJump = false;
                Player.CanAirDash = false;
            }

            public override void Exit()
            {
                Player.EndAbility(PlayerController.HitAbilityId);
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
