using UnityEngine;

namespace Odyssey.Characters.Player
{
    // 继承自 GroundedState，自动拥有了“检测跳跃”和“检测掉落”的能力
    public class PlayerMoveState : PlayerGroundedState
    {
        public PlayerMoveState(PlayerController controller) : base(controller)
        {
        }

        public override void Enter()
        {
            base.Enter();
            Debug.Log("进入移动状态"); // 调试用，确信代码跑通了
        }

        public override void Tick()
        {
            base.Tick(); // 执行父类的检测（比如跳跃、坠落检测）
            if (_core.StateMachine.CurrentState != this) return;

            // 1. 获取移动输入
            Vector2 input = _core.InputReader.MovementValue;

            // 2. 计算相机相对方向
            Vector3 cameraForward = _core.MainCameraTransform.forward;
            Vector3 cameraRight = _core.MainCameraTransform.right;

            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward.Normalize();
            cameraRight.Normalize();

            Vector3 moveDirection = (cameraForward * input.y) + (cameraRight * input.x);

            // --- [修复重点] ---
            // 计算最终速度向量，必须包含垂直方向的吸附力！
    
            // 原代码: float currentSpeed = _core.MoveSpeed;
            // 新代码:
            float currentSpeed = _core.InputReader.IsSprinting ? _core.RunSpeed : _core.WalkSpeed;
            Vector3 finalVelocity = moveDirection * currentSpeed;

            // B. 【核心修复】注入垂直吸附力
            // 你的旧代码漏了这句，导致水平移动时丢失了重力
            finalVelocity.y = _core.VerticalVelocity; 

            // 3. 最终移动 (一次性应用水平+垂直)
            _core.Controller.Move(finalVelocity * Time.deltaTime);

            // 4. 驱动动画
            // 如果有输入，且不是在撞墙，则播放跑动动画
            float animationSpeed = input == Vector2.zero ? 0f : 1f;
            _core.Animator.SetFloat("Speed", animationSpeed, 0.1f, Time.deltaTime);

            // 5. 旋转角色
            if (moveDirection != Vector3.zero)
            {
                _core.transform.forward = Vector3.Slerp(_core.transform.forward, moveDirection, Time.deltaTime * 10f);
            }

            // 6. 状态切换
            if (input == Vector2.zero)
            {
                _core.StateMachine.ChangeState(new PlayerIdleState(_core));
            }
        }
    }
}
