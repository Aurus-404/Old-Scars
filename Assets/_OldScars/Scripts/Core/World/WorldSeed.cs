using System;
using System.Globalization;

namespace OldScars.Core.World
{
    /// <summary>
    /// Exact signed 64-bit input to logical generation. It is deliberately
    /// separate from WorldId and never represented as floating point.
    /// </summary>
    public readonly struct WorldSeed : IEquatable<WorldSeed>
    {
        public WorldSeed(long value)
        {
            Value = value;
        }

        public long Value { get; }
        public string Canonical => Value.ToString(CultureInfo.InvariantCulture);

        public static WorldSeed Parse(string raw)
        {
            if (!TryParse(raw, out WorldSeed seed, out string error))
                throw new FormatException($"Invalid WorldSeed '{Safe(raw)}': {error}.");
            return seed;
        }

        public static bool TryParse(string raw, out WorldSeed seed, out string error)
        {
            seed = default;
            error = null;
            if (string.IsNullOrWhiteSpace(raw))
            {
                error = "value is null, empty or whitespace";
                return false;
            }

            if (!long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value))
            {
                error = "expected a signed 64-bit base-10 integer";
                return false;
            }

            string canonical = value.ToString(CultureInfo.InvariantCulture);
            if (!string.Equals(raw, canonical, StringComparison.Ordinal))
            {
                error = $"non-canonical seed text; expected '{canonical}'";
                return false;
            }

            seed = new WorldSeed(value);
            return true;
        }

        public bool Equals(WorldSeed other) => Value == other.Value;
        public override bool Equals(object obj) => obj is WorldSeed other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Canonical;
        public static bool operator ==(WorldSeed left, WorldSeed right) => left.Equals(right);
        public static bool operator !=(WorldSeed left, WorldSeed right) => !left.Equals(right);

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<EMPTY>" : value;
        }
    }
}
