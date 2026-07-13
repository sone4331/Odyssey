using System;

namespace Odyssey.Gameplay.Combat
{
    /// <summary>
    /// 暴露生命状态的只读端口及变更事件，避免消费者获得任意写入权限。
    /// 该接口用于表现与规则层之间的依赖倒置，使 UI 和网络适配器不依赖具体 Health 实现。
    /// </summary>
    public interface IHealth
    {
        int Current { get; }
        int Maximum { get; }
        bool IsDead { get; }
        event Action<HealthChanged> Changed;
    }

    /// <summary>
    /// 定义可接收伤害命令的领域端口，统一玩家和怪物的命中处理入口。
    /// 命令返回权威结果，调用方不得绕过该端口直接扣减生命值。
    /// </summary>
    public interface IDamageable
    {
        DamageResult Apply(DamageRequest request);
    }
}
