using UnityEngine;

namespace Odyssey.Characters.Player
{
    public class PlayerJumpState : PlayerState
    {
        private float _jumpHeight; // 用于存传入的高度

        // [修改] 构造函数增加 jumpHeight 参数
        public PlayerJumpState(PlayerController controller, float jumpHeight) : base(controller)
        {
            _jumpHeight = jumpHeight;
        }

        public override void Enter()
        {
            base.Enter();
    
            // [修改] 使用传入的 _jumpHeight 计算速度
            _core.VerticalVelocity = Mathf.Sqrt(-2f * _core.Gravity * _jumpHeight);
    
            // 播放动画
            PlayAnimation("Jump");
    
            // 注意：这里不要把 CanAirJump 设为 false。
            // 逻辑是：地面跳起来后，空中依然保留一次二段跳机会。
        }

        public override void Tick()
        {
            base.Tick();

            // 3. 起跳一瞬间后，立马把控制权交给 AirState (由它处理重力衰减)
            // 为什么要立马切？因为 JumpState 只负责"给一个向上的初速度"
            _core.StateMachine.ChangeState(new PlayerAirState(_core));
        }
    }
}