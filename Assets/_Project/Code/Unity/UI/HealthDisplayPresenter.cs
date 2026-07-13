using System;
using Odyssey.Gameplay.Combat;

namespace Odyssey.Unity.UI
{
    /// <summary>
    /// 定义血量展示所需的最小视图操作，屏蔽 UGUI、图标数量和闪屏实现细节。
    /// </summary>
    public interface IHealthDisplayView
    {
        void SetHealth(int current, int maximum);
        void ShowDamageFlash();
    }

    /// <summary>
    /// 将 HealthChanged 领域事件转换为视图刷新和受伤反馈，不持有 Gameplay 可变状态。
    /// 采用 Passive View/MVP 模式彻底分离 UI 表现与生命规则，并使展示逻辑可脱离 Unity 测试。
    /// </summary>
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
