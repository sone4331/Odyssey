using System;
using System.Collections.Generic;

namespace Odyssey.Core.Tags
{
    /// <summary>
    /// 维护带引用计数的运行时标签集合，并支持父级语义查询。
    /// 引用计数允许多个系统安全共享同一标签，避免任一技能结束时错误移除其他来源仍在持有的状态。
    /// </summary>
    public sealed class GameplayTagSet
    {
        private readonly Dictionary<GameplayTag, int> _tags = new Dictionary<GameplayTag, int>();

        public void Add(GameplayTag tag)
        {
            _tags.TryGetValue(tag, out var count);
            _tags[tag] = count + 1;
        }

        public void Remove(GameplayTag tag)
        {
            if (!_tags.TryGetValue(tag, out var count))
            {
                return;
            }

            if (count <= 1)
            {
                _tags.Remove(tag);
                return;
            }

            _tags[tag] = count - 1;
        }
        public bool Has(GameplayTag tag)
        {
            foreach (var ownedTag in _tags.Keys)
            {
                if (ownedTag.Matches(tag))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
