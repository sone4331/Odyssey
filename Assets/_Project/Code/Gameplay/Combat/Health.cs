using System;

namespace Odyssey.Gameplay.Combat
{
    /// <summary>
    /// 维护生命值不变量并统一处理伤害、恢复和重置，是与 Unity 无关的战斗聚合根。
    /// 通过领域事件发布结果，使 UI、VFX、存档和网络复制只依赖已提交状态而不直接修改内部数据。
    /// </summary>
    public sealed class Health : IHealth, IDamageable
    {
        public Health(int maximum)
        {
            Maximum = maximum;
            Current = maximum;
        }

        public int Current { get; private set; }
        public int Maximum { get; }
        public bool IsDead => Current <= 0;
        public event Action<HealthChanged> Changed;

        /// <summary>
        /// 先拒绝死亡后或无效请求，再一次性提交扣血并发布变化事件。
        /// 事件只在生命值实际变化后触发，使调用方可把“命中被接受”和“界面需要刷新”视为同一领域事实。
        /// </summary>
        public DamageResult Apply(DamageRequest request)
        {
            if (IsDead || request.Amount <= 0)
            {
                return new DamageResult(false, 0, IsDead);
            }

            var previous = Current;
            Current = Math.Max(0, Current - request.Amount);
            var appliedAmount = previous - Current;
            Changed?.Invoke(new HealthChanged(previous, Current, request.SourceId));
            return new DamageResult(true, appliedAmount, IsDead);
        }

        /// <summary>
        /// 将恢复量夹在最大生命内；满血和无效恢复保持无副作用，避免网络或 UI 收到伪造的生命变化事件。
        /// </summary>
        public int Restore(int amount, string sourceId)
        {
            if (amount <= 0 || Current >= Maximum)
            {
                return 0;
            }

            var previous = Current;
            Current = Math.Min(Maximum, Current + amount);
            Changed?.Invoke(new HealthChanged(previous, Current, sourceId));
            return Current - previous;
        }

        public void Reset(string sourceId)
        {
            if (Current == Maximum)
            {
                return;
            }

            var previous = Current;
            Current = Maximum;
            Changed?.Invoke(new HealthChanged(previous, Current, sourceId));
        }
    }
}
