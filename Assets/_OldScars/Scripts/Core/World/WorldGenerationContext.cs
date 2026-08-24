using System;

namespace OldScars.Core.World
{
    /// <summary>
    /// Canonical overall world-generation pipeline version recorded as creation
    /// metadata. Individual procedural passes own separate stable deterministic
    /// generation contracts; this value does not seed their domains and does not
    /// negotiate compatibility with existing worlds.
    /// </summary>
    public readonly struct GeneratorVersion : IEquatable<GeneratorVersion>
    {
        private const int MaximumLength = 64;
        private readonly string canonical;

        private GeneratorVersion(string canonical)
        {
            this.canonical = canonical;
        }

        public string Canonical => canonical ?? string.Empty;
        public bool IsValid => TryValidate(canonical, out _);

        public static GeneratorVersion Parse(string raw)
        {
            if (!TryParse(raw, out GeneratorVersion version, out string error))
                throw new FormatException($"Invalid GeneratorVersion '{Safe(raw)}': {error}.");
            return version;
        }

        public static bool TryParse(string raw, out GeneratorVersion version, out string error)
        {
            version = default;
            if (!TryValidate(raw, out error))
                return false;
            version = new GeneratorVersion(raw);
            return true;
        }

        public bool Equals(GeneratorVersion other)
        {
            return string.Equals(canonical, other.canonical, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is GeneratorVersion other && Equals(other);

        public override int GetHashCode()
        {
            // Collection equality only. Generation hashes Canonical explicitly.
            return WorldCanonicalEncoding.GetStableCollectionHashCode(canonical);
        }

        public override string ToString() => Canonical;
        public static bool operator ==(GeneratorVersion left, GeneratorVersion right) => left.Equals(right);
        public static bool operator !=(GeneratorVersion left, GeneratorVersion right) => !left.Equals(right);

        private static bool TryValidate(string value, out string error)
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

            if (value.Length > MaximumLength)
            {
                error = $"length exceeds {MaximumLength} characters";
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool valid = character >= 'a' && character <= 'z' ||
                             character >= '0' && character <= '9' ||
                             character == '_' || character == '-' || character == '.';
                if (valid)
                    continue;
                error = $"invalid character '{character}' at position {index}; " +
                        "use lowercase ASCII letters, digits, '_', '-' or '.'";
                return false;
            }

            char first = value[0];
            char last = value[value.Length - 1];
            if (!IsAlphaNumeric(first) || !IsAlphaNumeric(last))
            {
                error = "first and last characters must be lowercase ASCII letters or digits";
                return false;
            }

            return true;
        }

        private static bool IsAlphaNumeric(char value)
        {
            return value >= 'a' && value <= 'z' || value >= '0' && value <= '9';
        }

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<EMPTY>" : value;
        }
    }

    /// <summary>
    /// Minimum immutable context for current logical generation. There are no
    /// generation-relevant content families yet, so LoadedContentSet provenance
    /// is intentionally not an input. Future explicit inputs must extend this
    /// contract through a reviewed versioned change.
    /// </summary>
    public sealed class WorldGenerationContext
    {
        public WorldGenerationContext(WorldSeed worldSeed, GeneratorVersion generatorVersion)
        {
            if (!generatorVersion.IsValid)
                throw new ArgumentException("A valid GeneratorVersion is required.", nameof(generatorVersion));
            WorldSeed = worldSeed;
            GeneratorVersion = generatorVersion;
        }

        public WorldSeed WorldSeed { get; }
        public GeneratorVersion GeneratorVersion { get; }
    }
}
