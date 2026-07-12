using System;
using System.Collections.Generic;

namespace Odyssey.Core.Tags
{
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
