using UnityEngine;

namespace Odyssey.Characters.Player
{
    public class PlayerDashState : PlayerState
    {
        private float _timer;
        private Vector3 _dashDirection;

        public PlayerDashState(PlayerController controller) : base(controller)
        {
        }

        public override void Enter()
        {
            base.Enter();
            
            _timer = _core.DashDuration;
            
            // 1. 锁定冲刺方向 (根据当前面朝向，而不是相机，防止乱飞)
            _dashDirection = _core.transform.forward;

            // 2. --- 动画“伪装”逻辑 ---
            if (_core.Controller.isGrounded)
            {
                // 地面：用 2倍速 播放跑步
                PlayAnimation("Run"); // 假设你的 BlendTree 参数是 Speed，这里可能需要 SetFloat("Speed", 1)
                _core.Animator.speed = 2.5f; // 【关键】加速播放！
            }
            else
            {
                // 空中：播放跳跃动作 (变身炮弹)
                PlayAnimation("Jump");
                // 消耗空中冲刺次数
                _core.CanAirDash = false;
            }

        }

        public override void Tick()
        {
            // 注意：不要调用 base.Tick()，我们不需要重力！
            
            _timer -= Time.deltaTime;

            // 4. 冲刺移动 (无视重力，保持水平)
            _core.Controller.Move(_dashDirection * _core.DashForce * Time.deltaTime);

            if (_timer <= 0f)
            {
                // 冲刺结束，切回状态
                if (_core.Controller.isGrounded)
                {
                    _core.StateMachine.ChangeState(new PlayerMoveState(_core));
                }
                else
                {
                    _core.StateMachine.ChangeState(new PlayerAirState(_core));
                }
            }
        }

        public override void Exit()
        {
            base.Exit();
            _core.EndAbility(PlayerController.DashAbilityId);
            // 【关键】退出状态时，一定要把动画速度还原！
            _core.Animator.speed = 1f; 
        }
    }
}
