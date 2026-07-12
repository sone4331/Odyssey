using System;
using Odyssey.Gameplay.Combat;

namespace Odyssey.Unity.UI
{
    public interface IHealthDisplayView
    {
        void SetHealth(int current, int maximum);
        void ShowDamageFlash();
    }

    public sealed class HealthDisplayPresenter
    {
        private readonly IHealthDisplayView _view;
        private readonly int _maximum;

        public HealthDisplayPresenter(IHealthDisplayView view, int maximum)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _maximum = maximum > 0
                ? maximum
                : throw new ArgumentOutOfRangeException(nameof(maximum));
        }

        public void Initialize(int current)
        {
            _view.SetHealth(Clamp(current), _maximum);
        }

        public void Handle(HealthChanged change)
        {
            _view.SetHealth(Clamp(change.Current), _maximum);
            if (change.Current < change.Previous)
            {
                _view.ShowDamageFlash();
            }
        }

        private int Clamp(int value)
        {
            return Math.Max(0, Math.Min(_maximum, value));
        }
    }
}
