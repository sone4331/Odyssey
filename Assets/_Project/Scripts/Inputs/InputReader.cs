using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Odyssey.Inputs; // 引用刚才生成的代码

[CreateAssetMenu(fileName = "InputReader", menuName = "Odyssey/输入/输入读取器")]
public class InputReader : ScriptableObject, GameInput.IGameplayActions
{
    // 定义事件：当移动/跳跃发生时，通知其他人
    public event UnityAction<Vector2> MoveEvent;
    public event UnityAction JumpEvent;
    public event UnityAction JumpCanceledEvent;
    public event UnityAction AttackEvent;
    public event UnityAction DashEvent; // 定义“冲刺”闹钟
    public bool IsSprinting { get; private set; } // 定义“加速跑”状态
    
    public Vector2 MovementValue { get; private set; }

    private GameInput _gameInput;
    
    public bool IsJumpKeyPressed { get; private set; }

    private void OnEnable()
    {
        if (_gameInput == null)
        {
            _gameInput = new GameInput();
            _gameInput.Gameplay.SetCallbacks(this); // 告诉Input System把消息发给我
        }
        _gameInput.Gameplay.Enable();
    }

    private void OnDisable()
    {
        _gameInput.Gameplay.Disable();
    }

    // --- 下面是接口实现 ---
    public void OnMove(InputAction.CallbackContext context)
    {
        MovementValue = context.ReadValue<Vector2>(); // 存下来，供 State 随时查询
        MoveEvent?.Invoke(MovementValue); // 同时也广播出去
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
        {
            IsJumpKeyPressed = true; // 按下
            JumpEvent?.Invoke();
        }
        else if (context.phase == InputActionPhase.Canceled)
        {
            IsJumpKeyPressed = false; // 松开
            JumpCanceledEvent?.Invoke();
        }
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
            AttackEvent?.Invoke();
    }
    
    // 绑定到 Input System 的 Sprint Action (Shift)
    public void OnSprint(InputAction.CallbackContext context)
    {
        // Phase.Started = 刚按下的那一瞬间 (用于冲刺)
        if (context.phase == InputActionPhase.Started)
        {
            DashEvent?.Invoke(); // 响铃！通知所有监听的人
        }
    
        // Phase.Performed = 按住不放 (用于加速跑)
        if (context.phase == InputActionPhase.Performed)
        {
            IsSprinting = true;
        }
        // Phase.Canceled = 松开
        else if (context.phase == InputActionPhase.Canceled)
        {
            IsSprinting = false;
        }
    }
    
    // 在 InputReader 类中添加这个方法
    public void UseJumpInput()
    {
        IsJumpKeyPressed = false;
    }
    
    
    
    

}
