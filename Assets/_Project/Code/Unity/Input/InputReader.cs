using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Odyssey.Inputs
{
    /// <summary>
    /// 把 Input System 回调转换为玩法层可读取的连续输入快照与离散动作命令。
    /// 采用 Adapter 与 Observer 模式：移动、跳跃和奔跑保存当前状态，攻击与冲刺通过事件通知玩家门面，
    /// 从而让角色状态机不依赖 InputAction 生命周期，也不在每帧重复订阅或查询设备。
    /// </summary>
    [CreateAssetMenu(fileName = "InputReader", menuName = "Odyssey/输入/输入读取器")]
    public sealed class InputReader : ScriptableObject, GameInput.IGameplayActions
    {
        private GameInput _gameInput;

        public event Action AttackRequested;
        public event Action DashRequested;

        public Vector2 MovementValue { get; private set; }
        public bool IsJumpPressed { get; private set; }
        public bool IsSprinting { get; private set; }

        private void OnEnable()
        {
            _gameInput ??= new GameInput();
            _gameInput.Gameplay.SetCallbacks(this);
            _gameInput.Gameplay.Enable();
        }

        private void OnDisable()
        {
            _gameInput?.Gameplay.Disable();
            ClearSnapshot();
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            MovementValue = context.ReadValue<Vector2>();
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                IsJumpPressed = true;
            }
            else if (context.canceled)
            {
                IsJumpPressed = false;
            }
        }

        public void OnAttack(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                AttackRequested?.Invoke();
            }
        }

        public void OnSprint(InputAction.CallbackContext context)
        {
            if (context.started)
            {
                DashRequested?.Invoke();
            }

            if (context.performed)
            {
                IsSprinting = true;
            }
            else if (context.canceled)
            {
                IsSprinting = false;
            }
        }

        /// <summary>
        /// 消费本次跳跃按下状态，使空中跳和墙跳在按键未释放前只响应一次。
        /// </summary>
        public void ConsumeJump()
        {
            IsJumpPressed = false;
        }

        /// <summary>
        /// 清空当前连续输入快照，供 ESC 菜单接管操作权时消除残留方向和按键状态。
        /// 输入资产仍保持启用，避免共享 ScriptableObject 被某个本地玩家关闭后影响后续重新绑定。
        /// </summary>
        public void ClearSnapshot()
        {
            MovementValue = Vector2.zero;
            IsJumpPressed = false;
            IsSprinting = false;
        }
    }
}
