using System;

namespace OldScars.Core.World
{
    /// <summary>
    /// Durable identity of one created world/save lineage. WorldId is never an
    /// input to procedural generation; worlds created from the same generation
    /// context may intentionally have different identities.
    /// </summary>
    public readonly struct WorldId : IEquatable<WorldId>
    {
        private const string Prefix = "world_";
        private const int HexLength = 32;
        private readonly string canonical;

        private WorldId(string canonical)
        {
            this.canonical = canonical;
        }

        public string Canonical => canonical ?? string.Empty;
        public bool IsValid => IsValidFormat(canonical);

        public static WorldId CreateNew()
        {
            return new WorldId(Prefix + Guid.NewGuid().ToString("N"));
        }

        public static WorldId Parse(string raw)
        {
            if (!TryParse(raw, out WorldId worldId, out string error))
                throw new FormatException($"Invalid WorldId '{Safe(raw)}': {error}.");
            return worldId;
        }

        public static bool TryParse(string raw, out WorldId worldId, out string error)
        {
            worldId = default;
            if (!TryValidate(raw, Prefix, "WorldId", out error))
                return false;
            worldId = new WorldId(raw);
            return true;
        }

        public static bool IsValidFormat(string value)
        {
            return TryValidate(value, Prefix, "WorldId", out _);
        }

        public bool Equals(WorldId other)
        {
            return string.Equals(canonical, other.canonical, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is WorldId other && Equals(other);
        }

        public override int GetHashCode()
        {
            // Collection equality only. Generation never consumes GetHashCode().
            return WorldCanonicalEncoding.GetStableCollectionHashCode(canonical);
        }

        public override string ToString()
        {
            return Canonical;
        }

        public static bool operator ==(WorldId left, WorldId right) => left.Equals(right);
        public static bool operator !=(WorldId left, WorldId right) => !left.Equals(right);

        internal static bool TryValidate(
            string value,
            string prefix,
            string label,
            out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                error = "value is null, empty or whitespace";
                return false;
            }

            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                error = "leading or trailing whitespace is not allowed";
                return false;
            }

            if (!value.StartsWith(prefix, StringComparison.Ordinal) ||
                value.Length != prefix.Length + HexLength)
            {
                error = $"expected {prefix}<32 lowercase hexadecimal characters>";
                return false;
            }

            for (int index = prefix.Length; index < value.Length; index++)
            {
                char character = value[index];
                bool lowerHex = character >= '0' && character <= '9' ||
                                character >= 'a' && character <= 'f';
                if (lowerHex)
                    continue;
                error = $"{label} contains non-lowercase-hex character '{character}' at position {index}";
                return false;
            }

            return true;
        }

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<EMPTY>" : value;
        }
    }
}
