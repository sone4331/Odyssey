using UnityEngine;

namespace Odyssey.Core.FSM
{
    // 同样使用泛型 T
    public class StateMachine<T> where T : class
    {
        public BaseState<T> CurrentState { get; private set; }

        // 初始化时，设置一个默认状态（比如 Idle）
        public void Initialize(BaseState<T> startingState)
        {
            CurrentState = startingState;
            CurrentState.Enter();
        }

        // 核心功能：切换状态
        public void ChangeState(BaseState<T> newState)
        {
            // 1. 退出当前状态
            CurrentState?.Exit();

            // 2. 切换引用
            CurrentState = newState;

            // 3. 进入新状态
            CurrentState.Enter();
        }
        
        // 对应 Unity 的 Update，由外部调用
        public void Tick()
        {
            CurrentState?.Tick();
        }
        
        // 对应 Unity 的 FixedUpdate
        public void PhysicsTick()
        {
            CurrentState?.PhysicsTick();
        }
    }
}