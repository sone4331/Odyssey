using UnityEngine;

namespace Odyssey.Core.FSM
{
    // abstract: 意味着这个类不能直接挂在物体上，必须被继承
    public abstract class BaseState
    {
        // 核心：所有状态都至少需要拿到主角的控制器（上下文 Context），否则它控制谁呢？
        // 这里我们用 protected，只有子类（比如JumpState）能访问，外面的人不能乱改
        // 注意：这里先用 object 占位，之后我们会把它改成具体的 PlayerController
        // 或者使用泛型 T 来让怪物也能复用
        
        // 为了简单起见，且为了你的面试加分，我们要用【泛型】！
        // 这样这个状态机既能给 Player 用，也能给 Boss 用。
    }

    // --- 泛型版本 (面试加分项) ---
    public abstract class BaseState<T> where T : class
    {
        protected T _core; // 持有控制者的引用（比如 PlayerController）

        public BaseState(T core)
        {
            _core = core;
        }

        // 1. 进入状态时触发（比如开始跳跃的那一帧：播放音效、播放动画）
        public virtual void Enter() { }

        // 2. 离开状态时触发（比如落地的那一帧：重置重力数据）
        public virtual void Exit() { }

        // 3. 对应 Update()，每帧运行逻辑（检测输入、计时器）
        public virtual void Tick() { }

        // 4. 对应 FixedUpdate()，处理物理（移动、施加力）
        public virtual void PhysicsTick() { }
    }
}