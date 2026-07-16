using System;
using Odyssey.Gameplay.Characters;
using UnityEngine;

namespace Odyssey.Characters.Player
{
    /// <summary>
    /// 集中管理玩家 Animator 参数、状态名和短过渡，避免移动轴与动作轴分别触发同一套动画条件。
    /// 采用 Adapter 模式把 Animator 的字符串协议封装在单一位置；直接 CrossFade 只用于离散动作，Locomotion 仍由 BlendTree 平滑混合。
    /// </summary>
    internal sealed class PlayerAnimationDriver
    {
        private const float LocomotionBlendTime = 0.08f;
        private const float AirBlendTime = 0.1f;
        private const float ActionBlendTime = 0.08f;

        private static readonly int Speed = Animator.StringToHash("Speed");
        private static readonly AnimatorStateId Locomotion = CreateStateId("Locomotion");
        private static readonly AnimatorStateId JumpUp = CreateStateId("EllenJumpGoesUp");
        private static readonly AnimatorStateId JumpDown = CreateStateId("EllenJumpGoesDown");
        private static readonly AnimatorStateId Hit = CreateStateId("EllenHitFront");
        private static readonly AnimatorStateId Death = CreateStateId("EllenDeath");
        private static readonly AnimatorStateId[] ComboStates =
        {
            CreateStateId("EllenCombo_1"),
            CreateStateId("EllenCombo_2"),
            CreateStateId("EllenCombo_3"),
            CreateStateId("EllenCombo_4")
        };

        private readonly Animator _animator;

        public PlayerAnimationDriver(Animator animator)
        {
            _animator = animator != null ? animator : throw new ArgumentNullException(nameof(animator));
        }

        /// <summary>
        /// 平滑更新 BlendTree 速度，不重播 Locomotion，从而避免待机与移动之间产生脚步跳帧。
        /// </summary>
        public void SetLocomotionSpeed(float normalizedSpeed, float deltaTime)
        {
            var value = Mathf.Clamp01(normalizedSpeed);
            if (deltaTime <= 0f)
            {
                _animator.SetFloat(Speed, value);
                return;
            }

            _animator.SetFloat(Speed, value, 0.1f, deltaTime);
        }

        public void PlayGrounded()
        {
            CrossFadeIfNeeded(Locomotion, LocomotionBlendTime);
        }

        public void PlayJump()
        {
            CrossFadeIfNeeded(JumpUp, AirBlendTime);
        }

        public void PlayFall()
        {
            CrossFadeIfNeeded(JumpDown, AirBlendTime);
        }

        /// <summary>
        /// 从当前姿势短交叉淡化到指定连击段，避免依赖 Any State + Exit Time 导致连击输入延迟。
        /// </summary>
        public void PlayAttack(int comboIndex)
        {
            var index = Mathf.Clamp(comboIndex, 1, ComboStates.Length) - 1;
            _animator.CrossFadeInFixedTime(ComboStates[index].FullPathHash, ActionBlendTime, 0, 0f);
        }

        public void PlayDash(PlayerLocomotionStateId locomotionState, float verticalVelocity)
        {
            if (locomotionState == PlayerLocomotionStateId.Grounded)
            {
                PlayGrounded();
                _animator.SetFloat(Speed, 1f);
                return;
            }

            if (verticalVelocity > 0f)
            {
                PlayJump();
            }
            else
            {
                PlayFall();
            }
        }

        public void PlayHit()
        {
            _animator.CrossFadeInFixedTime(Hit.FullPathHash, ActionBlendTime, 0, 0f);
        }

        public void PlayDeath()
        {
            _animator.CrossFadeInFixedTime(Death.FullPathHash, ActionBlendTime, 0, 0f);
        }

        /// <summary>
        /// 动作结束或被打断后，根据仍然有效的移动状态恢复正确姿势，避免攻击结束后停留在最后一帧。
        /// </summary>
        public void Recover(PlayerLocomotionStateId locomotionState, float verticalVelocity)
        {
            switch (locomotionState)
            {
                case PlayerLocomotionStateId.Grounded:
                    PlayGrounded();
                    break;
                case PlayerLocomotionStateId.WallSlide:
                    PlayFall();
                    break;
                default:
                    if (verticalVelocity > 0f)
                    {
                        PlayJump();
                    }
                    else
                    {
                        PlayFall();
                    }

                    break;
            }
        }

        private void CrossFadeIfNeeded(AnimatorStateId targetState, float duration)
        {
            var current = _animator.GetCurrentAnimatorStateInfo(0);
            var next = _animator.GetNextAnimatorStateInfo(0);
            if (current.shortNameHash == targetState.ShortNameHash ||
                (_animator.IsInTransition(0) && next.shortNameHash == targetState.ShortNameHash))
            {
                return;
            }

            _animator.CrossFadeInFixedTime(targetState.FullPathHash, duration, 0);
        }

        private static AnimatorStateId CreateStateId(string stateName)
        {
            return new AnimatorStateId(
                Animator.StringToHash($"Base Layer.{stateName}"),
                Animator.StringToHash(stateName));
        }

        /// <summary>
        /// 同时保存播放所需的完整路径哈希和运行态判断所需的短名称哈希，避免两类 Animator API 混用。
        /// </summary>
        private readonly struct AnimatorStateId
        {
            public AnimatorStateId(int fullPathHash, int shortNameHash)
            {
                FullPathHash = fullPathHash;
                ShortNameHash = shortNameHash;
            }

            public int FullPathHash { get; }
            public int ShortNameHash { get; }
        }
    }
}
