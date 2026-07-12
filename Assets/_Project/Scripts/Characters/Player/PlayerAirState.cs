using UnityEngine;
using Odyssey.Characters.Enemies;

namespace Odyssey.Characters.Player
{
    public class PlayerAirState : PlayerState
    {
        private Vector3 _momentum; // 记录外部施加的瞬间力（如蹬墙跳的反弹力）

        // 构造函数：支持从外部状态（如滑墙状态）传入一个初始动量
        public PlayerAirState(PlayerController controller, Vector3 initialMomentum = default) : base(controller)
        {
            _momentum = initialMomentum;
        }

        public override void Enter()
        {
            base.Enter();
            
            // 告诉动画状态机我们离地了
            _core.Animator.SetBool("IsGrounded", false);
            
            // 根据垂直速度决定播放向上跳还是向下落的动画
            if (_core.VerticalVelocity > 2f) 
            {
                PlayAnimation("Jump"); 
            }
            else 
            {
                PlayAnimation("Fall"); 
            }
            
            // 订阅冲刺事件
            _core.InputReader.DashEvent += OnDash;
        }

        public override void Tick()
        {
            // 1. 二段跳检测：必须放在逻辑最顶端
            if (_core.InputReader.IsJumpKeyPressed && _core.CanAirJump)
            {
                _core.InputReader.UseJumpInput(); 
                _core.CanAirJump = false; // 消耗跳跃次数

                // 重置垂直速度，实现空中小跳
                _core.VerticalVelocity = Mathf.Sqrt(-2f * _core.Gravity * _core.AirJumpHeight);
                _core.Animator.CrossFade("EllenJumpGoesUp", 0.1f); 
            }
            
            // 执行基类逻辑（如果有通用逻辑的话）
            base.Tick();

            // 2. 物理模拟：重力累加
            _core.VerticalVelocity += _core.Gravity * Time.deltaTime;
            
            // 限制最大下落速度，防止掉速太快穿地板
            if (_core.VerticalVelocity < -20f) _core.VerticalVelocity = -20f;

            // 3. 计算水平控制力
            Vector2 input = _core.InputReader.MovementValue;
            Vector3 cameraForward = _core.MainCameraTransform.forward;
            Vector3 cameraRight = _core.MainCameraTransform.right;
            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward.Normalize();
            cameraRight.Normalize();
            
            Vector3 moveDirection = (cameraForward * input.y) + (cameraRight * input.x);

            // 4. 处理惯性衰减：模拟空气阻力，让蹬墙跳的反弹力逐渐平滑消失
            _momentum = Vector3.Lerp(_momentum, Vector3.zero, Time.deltaTime * 5f);
            
            // --- 【新增】踩头杀逻辑 ---
            if (_core.VerticalVelocity < 0)
            {
                // 1. 起点：刚好在脚底板偏上一点点 (0.1f)
                Vector3 footPos = _core.transform.position + Vector3.up * 0.1f; 
    
                // 2. 射线半径缩减为 0.2f，向下只探测 0.2f 的距离！
                // 相当于在鞋底贴了一个非常薄的感应圆盘，绝对不会误触
                if (Physics.SphereCast(footPos, 0.2f, Vector3.down, out RaycastHit hit, 0.2f, _core.EnemyLayer))
                {
                    Enemy enemy = hit.collider.GetComponent<Enemy>();
                    if (enemy == null) enemy = hit.collider.GetComponentInParent<Enemy>();

                    if (enemy != null)
                    {
                        enemy.TakeDamage(_core.AttackDamage);
                        _core.VerticalVelocity = 8f; 
                        _core.CanAirJump = true; 
                        _core.CanAirDash = true; 
                        PlayAnimation("Jump");
                        return; 
                    }
                }
            }
            
            // 5. 滑墙触发检测
            if (_core.VerticalVelocity < 0)
            {
                Vector3 startPoint = _core.transform.position + Vector3.up * 1.0f; // 确保起点高度一致
    
                // [调试] 画出射线 (黄色线)
                // 只有当射线变红 (进入滑墙状态) 才是成功的
                Debug.DrawRay(startPoint, _core.transform.forward * 1.0f, Color.yellow);

                if (Physics.Raycast(startPoint, _core.transform.forward, out RaycastHit hit, 1f))
                {
                    // 角度判断 (70~110度)
                    float angle = Vector3.Angle(Vector3.up, hit.normal);
                    if (angle > 70f && angle < 110f)
                    {
                        _core.StateMachine.ChangeState(new PlayerWallSlideState(_core, hit.normal));
                        return;
                    }
                }
            }

            // 6. 执行最终移动
            // 使用当前应有的速度（走或跑），空中控制力通常减弱（乘以0.7f）
            float currentSpeed = _core.InputReader.IsSprinting ? _core.RunSpeed : _core.WalkSpeed;
            
            // 速度 = 玩家操作方向速度 + 之前的惯性残留 + 垂直速度
            Vector3 finalVelocity = moveDirection * (currentSpeed * 0.7f) + _momentum + Vector3.up * _core.VerticalVelocity;
            _core.Controller.Move(finalVelocity * Time.deltaTime);

            // 7. 旋转角色面朝移动方向
            if (moveDirection != Vector3.zero)
            {
                _core.transform.forward = Vector3.Slerp(_core.transform.forward, moveDirection, Time.deltaTime * 5f);
            }

            // 8. 落地检测
            if (_core.VerticalVelocity < -2f && _core.Controller.isGrounded)
            {
                _core.StateMachine.ChangeState(new PlayerIdleState(_core));
            }
        }

        public override void Exit()
        {
            base.Exit();
            // 必须取消订阅事件，防止内存泄漏和逻辑错误
            _core.InputReader.DashEvent -= OnDash;
        }

        private void OnDash()
        {
            if (_core.CanAirDash)
            {
                _core.StateMachine.ChangeState(new PlayerDashState(_core));
            }
        }
    }
}