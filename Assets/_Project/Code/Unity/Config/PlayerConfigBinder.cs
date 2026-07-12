using System;
using Odyssey.Gameplay.Config;

namespace Odyssey.Unity.Config
{
    public interface IPlayerConfigTarget
    {
        void Apply(PlayerConfigData config);
    }

    public static class PlayerConfigBinder
    {
        public static void Bind(
            IGameConfigProvider provider,
            string configId,
            IPlayerConfigTarget target)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (string.IsNullOrWhiteSpace(configId)) throw new ArgumentException("Config id is required.", nameof(configId));
            if (target == null) throw new ArgumentNullException(nameof(target));

            target.Apply(provider.Get<PlayerConfigData>(configId));
        }
    }
}
