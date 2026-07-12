using System;

namespace Odyssey.Core.Tags
{
    public readonly struct GameplayTag : IEquatable<GameplayTag>
    {
        private GameplayTag(string value)
        {
            Value = value;
        }

        public string Value { get; }

        public bool Matches(GameplayTag parent)
        {
            return Equals(parent) || Value.StartsWith(parent.Value + ".", StringComparison.Ordinal);
        }

        public static GameplayTag Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A gameplay tag cannot be empty.", nameof(value));
            }

            return new GameplayTag(value.Trim());
        }

        public bool Equals(GameplayTag other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is GameplayTag other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
    }
}
