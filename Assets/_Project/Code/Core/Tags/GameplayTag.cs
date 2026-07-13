using System;

namespace Odyssey.Core.Tags
{
    /// <summary>
    /// 表示可分层匹配的玩法语义标签，例如 State.Combat.Attacking。
    /// 采用值对象模式统一跨系统条件语言，避免 Ability、AI 和网络校验依赖具体状态类型。
    /// </summary>
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
                throw new ArgumentException("玩法标签不能为空。", nameof(value));
            }

            return new GameplayTag(value.Trim());
        }

        public bool Equals(GameplayTag other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is GameplayTag other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
    }
}
