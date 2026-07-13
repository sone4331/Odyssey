using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Odyssey.Unity.UI
{
    /// <summary>
    /// 管理血量图标的容量、显隐与 Sprite 状态，是 PlayerHealthUI 使用的可复用视图对象池。
    /// 采用 Object Pool 模式，只在最大生命增长时扩容；普通伤害与治疗只更新既有对象，避免持续实例化和销毁。
    /// </summary>
    public sealed class HealthIconPool
    {
        private readonly List<Image> _icons = new List<Image>();
        private readonly Image _template;

        public HealthIconPool(IEnumerable<Image> seedIcons)
        {
            if (seedIcons == null)
            {
                return;
            }

            foreach (var icon in seedIcons)
            {
                if (icon != null && !_icons.Contains(icon))
                {
                    _icons.Add(icon);
                }
            }

            if (_icons.Count > 0)
            {
                _template = _icons[0];
            }
        }

        public int Count => _icons.Count;
        public Image this[int index] => _icons[index];

        public int ActiveCount
        {
            get
            {
                var count = 0;
                foreach (var icon in _icons)
                {
                    if (icon.gameObject.activeSelf)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// 确保图标容量覆盖最大生命，并一次性提交显隐和满血/空血 Sprite。
        /// 返回 false 表示场景没有可克隆模板，调用方应输出配置错误而不是静默缺格。
        /// </summary>
        public bool SetHealth(int current, int maximum, Sprite fullSprite, Sprite emptySprite)
        {
            maximum = Mathf.Max(0, maximum);
            current = Mathf.Clamp(current, 0, maximum);
            if (maximum > 0 && _template == null)
            {
                return false;
            }

            while (_icons.Count < maximum)
            {
                var icon = Object.Instantiate(_template, _template.transform.parent, false);
                icon.gameObject.name = $"生命图标 {_icons.Count + 1}";
                _icons.Add(icon);
            }

            for (var index = 0; index < _icons.Count; index++)
            {
                var active = index < maximum;
                _icons[index].gameObject.SetActive(active);
                if (active)
                {
                    _icons[index].sprite = index < current ? fullSprite : emptySprite;
                }
            }

            return true;
        }
    }
}
