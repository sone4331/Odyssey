using UnityEngine;

namespace Odyssey.Characters.Player
{
    public class PlayerWallSlideState : PlayerState
    {
        private Vector3 _wallNormal;

        public PlayerWallSlideState(PlayerController controller, Vector3 wallNormal) : base(controller)
        {
            _wallNormal = wallNormal;
        }

        public override void Enter()
        {
            base.Enter();
            // [调试] 打印日志，确认是否成功进入状态
            Debug.Log($"<color=green> >>> 进入滑墙状态 (WallSlide) </color>");
            
            _core.Animator.CrossFade("EllenJumpGoesDown", 0.1f); 
            _core.transform.forward = -_wallNormal;
        }

        public override void Exit()
        {
            base.Exit();
            // [调试] 打印日志，确认何时退出
            Debug.Log($"<color=red> <<< 退出滑墙状态 </color>");
        }

        public override void Tick()
        {
            // 1. 蹬墙跳逻辑
            if (_core.InputReader.IsJumpKeyPressed)
            {
                Debug.Log("触发蹬墙跳！"); // [调试]
                _core.InputReader.UseJumpInput();
                _core.CanAirJump = true; 
                _core.CanAirDash = true; 

                Vector3 jumpMomentum = _wallNormal * 10f; // 向外弹的力
                _core.VerticalVelocity = 12f; // 向上的力
                
                _core.transform.forward = _wallNormal;
                _core.StateMachine.ChangeState(new PlayerAirState(_core, jumpMomentum));
                return;
            }

            // 2. 落地逻辑
            if (_core.Controller.isGrounded)
            {
                _core.StateMachine.ChangeState(new PlayerIdleState(_core));
                return;
            }

            // 3. 离开墙壁检测 (更稳定的射线)
            // 提高射线起点：从“脚底”改为“胸口” (up * 1.0f)
            Vector3 startPoint = _core.transform.position + Vector3.up * 1.0f;
            
            // [调试] 画出射线！(在 Scene 窗口可见，红色线)
            Debug.DrawRay(startPoint, _core.transform.forward * 1.2f, Color.red);

            // [核心修复] "严出"：
            // 检测距离设为 1.2f (比进入时的 1.0f 稍微长一点)，形成“容错区”，防止抖动
            // 只要前方 1.2米 内还有墙，就保持滑墙状态
            bool isWallThere = Physics.Raycast(startPoint, _core.transform.forward, out RaycastHit hit, 1.2f);

            if (!isWallThere)
            {
                Debug.Log("前方没墙了，切回下落");
                _core.StateMachine.ChangeState(new PlayerAirState(_core));
                return;
            }

            // 4. 缓降移动
            // 锁定速度
            _core.VerticalVelocity = -2.5f; 
            
            // 稍微给一点向墙里挤的力 (forward * 0.1f)，保证不脱钩
            Vector3 slideMove = Vector3.up * _core.VerticalVelocity + _core.transform.forward * 0.5f;
            _core.Controller.Move(slideMove * Time.deltaTime);
        }
    }
}