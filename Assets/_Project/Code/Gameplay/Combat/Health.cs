using System;

namespace Odyssey.Gameplay.Combat
{
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
