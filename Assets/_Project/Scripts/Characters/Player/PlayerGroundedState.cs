using UnityEngine;

namespace Odyssey.Characters.Player
{
    public abstract class PlayerGroundedState : PlayerState
    {
        // 容错计时器：防止下坡或颠簸时的瞬间浮空判定
        private float _timeLeftGrounded;
    
        // [新增] 蓄力相关变量
        private float _startChargeTime; 
        private bool _isCharging;
        

        public PlayerGroundedState(PlayerController controller) : base(controller)
        {
        }

        public override void Enter()
        {
            base.Enter();
            _core.VerticalVelocity = -5f; 
            _timeLeftGrounded = 0.2f;

            // [新增] 进入地面状态，强制 Animator 认为在地面
            // 这样哪怕中间有 0.1秒 的物理悬空，动画参数依然是 true，不会鬼畜
            _core.Animator.SetBool("IsGrounded", true);
            
            // --- [新增] 2. 动画状态清洗 (关键修复) ---
            // 落地瞬间，把之前可能残留的“起跳”和“下落”命令全部撤销！
            // 这样 Animator 就不会因为“记仇”而乱跳了。
            _core.Animator.ResetTrigger("Jump");
            _core.Animator.ResetTrigger("Fall");
            
            // [新增] 落地充能：允许一次二段跳
            _core.CanAirJump = true;
            
            // 进入地面时，重置蓄力状态
            _isCharging = false;
            
            // 落地充能：重置空中冲刺次数
            _core.CanAirDash = true; 
            
    
            // 订阅事件
            _core.InputReader.DashEvent += OnDash;
            _core.InputReader.AttackEvent += OnAttack;
        }

        public override void Tick()
        {
            base.Tick();
            
            _core.VerticalVelocity = -5f;
            
            // 1. 检测按下：开始蓄力
            if (_core.InputReader.IsJumpKeyPressed)
            {
                if (!_isCharging)
                {
                    _isCharging = true;
                    _startChargeTime = Time.time;
                    // 这里可以加一行代码播放“蓄力特效”或“下蹲动作”
                    // _core.Animator.SetBool("Charging", true); 
                }
            }
            // 2. 检测松开：触发跳跃
            else 
            {
                // 如果之前在蓄力，现在松开了 -> 起跳！
                if (_isCharging)
                {
                    _isCharging = false;
                    // _core.Animator.SetBool("Charging", false);

                    // 计算蓄力时长
                    float chargeDuration = Time.time - _startChargeTime;
                
                    // 判断高度：如果时长达标，用蓄力高度(5)，否则用普通高度(3)
                    float targetHeight = chargeDuration >= _core.MinChargeTime 
                        ? _core.ChargeJumpHeight 
                        : _core.JumpHeight;

                    // [关键] 传入计算好的高度，切换到跳跃状态
                    _core.StateMachine.ChangeState(new PlayerJumpState(_core, targetHeight));
                    return;
                }
            }

            // 2. 检测是否离开地面 (带有容错)
            if (!_core.Controller.isGrounded)
            {
                // [修改] 悬空时，不再死锁 -10，而是让重力自然生效！
                // 这样走下悬崖的手感就和跳跃下落完全一致了。
                _core.VerticalVelocity += _core.Gravity * Time.deltaTime;
        
                _timeLeftGrounded -= Time.deltaTime;

                if (_timeLeftGrounded <= 0)
                {
                    _core.StateMachine.ChangeState(new PlayerAirState(_core));
                }
            }
            else
            {
                _timeLeftGrounded = 0.2f;
        
                // [修改] 在地面时，始终保持一个强劲的下压力
                // 注意：这里不要直接 Move，因为 MoveState 也会调用 Move。
                // 我们只更新 VerticalVelocity，让 MoveState 去统一应用。
                _core.VerticalVelocity = -10f; 
        
                // 强制同步动画
                _core.Animator.SetBool("IsGrounded", true);
            }
        }
        public override void Exit()
        {
            base.Exit();
            // 记得取消订阅！否则会报错
            _core.InputReader.DashEvent -= OnDash;
            _core.InputReader.AttackEvent -= OnAttack;
        }
        private void OnDash()
        {
            if (_core.TryActivateAbility(PlayerController.DashAbilityId))
            {
                _core.StateMachine.ChangeState(new PlayerDashState(_core));
            }
        }
        
        private void OnAttack()
        {
            if (_core.TryActivateAbility(PlayerController.AttackAbilityId))
            {
                _core.StateMachine.ChangeState(new PlayerAttackState(_core, 1));
            }
        }
    }
}
