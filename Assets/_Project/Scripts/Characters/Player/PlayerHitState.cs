using UnityEngine;

namespace Odyssey.Characters.Player
{
    public class PlayerHitState : PlayerState
    {
        private Vector3 _knockback;
        private float _timer;

        public PlayerHitState(PlayerController controller, Vector3 knockback) : base(controller)
        {
            _knockback = knockback;
        }

        public override void Enter()
        {
            base.Enter();
            
            _core.Animator.SetTrigger("Hit"); 
            _timer = 0.5f; 

            // --- 【架构优化】在此处清空空中能力 ---
            // 逻辑：只要进入受击状态，不管是地面的还是空中的，都立刻失去滞空额外能力
            _core.CanAirJump = false;
            _core.CanAirDash = false;
        }

        public override void Tick()
        {
            _timer -= Time.deltaTime;

            _core.VerticalVelocity += _core.Gravity * Time.deltaTime;
            _knockback = Vector3.Lerp(_knockback, Vector3.zero, Time.deltaTime * 5f);

            Vector3 velocity = _knockback + Vector3.up * _core.VerticalVelocity;
            _core.Controller.Move(velocity * Time.deltaTime);

            if (_timer <= 0f)
            {
                if (_core.Controller.isGrounded)
                    _core.StateMachine.ChangeState(new PlayerIdleState(_core));
                else
                    _core.StateMachine.ChangeState(new PlayerAirState(_core));
            }
        }
    }
}