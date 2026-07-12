using System;

namespace Odyssey.Gameplay.Combat
{
    public interface IHealth
    {
        int Current { get; }
        int Maximum { get; }
        bool IsDead { get; }
        event Action<HealthChanged> Changed;
    }

    public interface IDamageable
    {
        DamageResult Apply(DamageRequest request);
    }
}
