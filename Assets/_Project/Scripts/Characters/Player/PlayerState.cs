using UnityEngine;
using Odyssey.Core.FSM;

namespace Odyssey.Characters.Player
{
    // 继承自 BaseState，并把 T 锁定为 PlayerController
    public abstract class PlayerState : BaseState<PlayerController>
    {
        // 构造函数：把 controller 传给父类
        public PlayerState(PlayerController controller) : base(controller)
        {
        }
        
        
        // --- 这里可以写所有玩家状态通用的逻辑 ---
        
        // 比如：一个简化的动画播放方法
        // 状态机调用：PlayAnimation("Jump");
        protected void PlayAnimation(string triggerName)
        {
            // _core 是我们在 BaseState 里定义的 PlayerController 引用
            _core.Animator.SetTrigger(triggerName); 
        }
        
        // 比如：通用的重力计算或者地面检测逻辑
        // 地面检测将在 Locomotion 重构时统一迁移到感知适配器。
    }
}
