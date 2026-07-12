using UnityEngine;

namespace Odyssey.Characters.Player
{
    public class PlayerIdleState : PlayerGroundedState
    {
        public PlayerIdleState(PlayerController controller) : base(controller)
        {
        }

        public override void Enter()
        {
            base.Enter();
            Debug.Log("进入待机状态");
            
            // 确保待机时没有残余速度
            _core.Controller.Move(Vector3.zero);
        }

        public override void Tick()
        {
            base.Tick();
            if (_core.StateMachine.CurrentState != this) return;
            
            // 应用重力 (VerticalVelocity 此时是自然变化的)
            _core.Controller.Move(Vector3.up * _core.VerticalVelocity * Time.deltaTime);
            
            // --- [新增] 动画驱动逻辑 ---
            // 待机时，把 Speed 慢慢降到 0
            _core.Animator.SetFloat("Speed", 0f, 0.1f, Time.deltaTime);

            if (_core.InputReader.MovementValue != Vector2.zero)
            {
                _core.StateMachine.ChangeState(new PlayerMoveState(_core));
            }
        }
    }
}
