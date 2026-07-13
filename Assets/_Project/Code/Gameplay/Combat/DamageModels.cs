namespace Odyssey.Gameplay.Combat
{
    /// <summary>
    /// 表示一次待校验的伤害命令，携带数值与来源身份而不直接修改目标状态。
    /// 采用命令值对象统一玩家、AI 和网络 Host 的伤害入口，便于审计与拒绝非法请求。
    /// </summary>
    public readonly struct DamageRequest
    {
        public DamageRequest(int amount, string sourceId)
        {
            Amount = amount;
            SourceId = sourceId;
        }

        public int Amount { get; }
        public string SourceId { get; }
    }

    /// <summary>
    /// 表示伤害管线的权威处理结果，明确区分是否接受、实际伤害和致死状态。
    /// 返回结果而非让表现层读取可变字段，可保证 UI、音效和网络复制基于同一事实。
    /// </summary>
    public readonly struct DamageResult
    {
        public DamageResult(bool accepted, int appliedAmount, bool killed)
        {
            Accepted = accepted;
            AppliedAmount = appliedAmount;
            Killed = killed;
        }

        public bool Accepted { get; }
        public int AppliedAmount { get; }
        public bool Killed { get; }
    }

    /// <summary>
    /// 描述生命值变更前后状态及来源，是 Gameplay 向表现层发布的领域事件。
    /// 使用不可变事件实现观察者模式，避免 UI 轮询或直接控制 Health。
    /// </summary>
    public readonly struct HealthChanged
    {
        public HealthChanged(int previous, int current, string sourceId)
        {
            Previous = previous;
            Current = current;
            SourceId = sourceId;
        }

        public int Previous { get; }
        public int Current { get; }
        public string SourceId { get; }
    }
}
