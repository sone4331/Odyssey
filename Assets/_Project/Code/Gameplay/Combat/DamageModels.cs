namespace Odyssey.Gameplay.Combat
{
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
