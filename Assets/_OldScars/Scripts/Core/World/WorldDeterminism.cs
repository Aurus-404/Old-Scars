using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OldScars.Core.World
{
    /// <summary>
    /// Stable SHA-256 key for one generation scope/pass domain. This is not a
    /// PRNG and is not a save or generation compatibility decision.
    /// </summary>
    public readonly struct DeterministicDomainKey : IEquatable<DeterministicDomainKey>
    {
        private const int HexLength = 64;
        private readonly string canonical;

        internal DeterministicDomainKey(string canonical)
        {
            this.canonical = canonical;
        }

        public string Canonical => canonical ?? string.Empty;
        public bool IsValid => IsValidFormat(canonical);

        public static DeterministicDomainKey Parse(string raw)
        {
            if (!TryParse(raw, out DeterministicDomainKey key, out string error))
                throw new FormatException($"Invalid deterministic domain key '{Safe(raw)}': {error}.");
            return key;
        }

        public static bool TryParse(string raw, out DeterministicDomainKey key, out string error)
        {
            key = default;
            error = null;
            if (!IsValidFormat(raw))
            {
                error = "expected exactly 64 lowercase hexadecimal characters";
                return false;
            }
            key = new DeterministicDomainKey(raw);
            return true;
        }

        public static bool IsValidFormat(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != HexLength)
                return false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!(character >= '0' && character <= '9' || character >= 'a' && character <= 'f'))
                    return false;
            }
            return true;
        }

        public bool Equals(DeterministicDomainKey other)
        {
            return string.Equals(canonical, other.canonical, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is DeterministicDomainKey other && Equals(other);

        public override int GetHashCode()
        {
            // Collection equality only. Domain derivation never consumes this.
            return WorldCanonicalEncoding.GetStableCollectionHashCode(canonical);
        }

        public override string ToString() => Canonical;
        public static bool operator ==(DeterministicDomainKey left, DeterministicDomainKey right) => left.Equals(right);
        public static bool operator !=(DeterministicDomainKey left, DeterministicDomainKey right) => !left.Equals(right);

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<EMPTY>" : value;
        }
    }

    public static class WorldDeterminism
    {
        private const string DomainContract = "old_scars_world_domain_v1";

        /// <summary>
        /// Derives a domain key solely from the world seed, the owning pass's
        /// stable generation contract, and explicit scope/pass keys. The overall
        /// pipeline GeneratorVersion, WorldId and content provenance are absent
        /// by contract; execution order and global random state are irrelevant.
        /// </summary>
        public static DeterministicDomainKey DerivePassDomainKey(
            WorldSeed worldSeed,
            string passGenerationContract,
            string scopeStableKey,
            string passKey)
        {
            WorldStableKey.Require(passGenerationContract, nameof(passGenerationContract));
            WorldStableKey.Require(scopeStableKey, nameof(scopeStableKey));
            WorldStableKey.Require(passKey, nameof(passKey));

            string hash = WorldCanonicalEncoding.ComputeSha256(stream =>
            {
                WorldCanonicalEncoding.WriteString(stream, DomainContract);
                WorldCanonicalEncoding.WriteInt64(stream, worldSeed.Value);
                WorldCanonicalEncoding.WriteString(stream, passGenerationContract);
                WorldCanonicalEncoding.WriteString(stream, scopeStableKey);
                WorldCanonicalEncoding.WriteString(stream, passKey);
            });
            return new DeterministicDomainKey(hash);
        }
    }

    internal static class WorldStableKey
    {
        private const int MaximumLength = 128;

        internal static void Require(string value, string parameterName)
        {
            if (!TryValidate(value, out string error))
                throw new ArgumentException($"Stable key '{Safe(value)}' is invalid: {error}.", parameterName);
        }

        internal static bool TryValidate(string value, out string error)
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
                             character >= '0' && character <= '9' || character == '_';
                if (valid)
                    continue;
                error = $"invalid character '{character}' at position {index}; " +
                        "use lowercase ASCII letters, digits or '_'";
                return false;
            }
            return true;
        }

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<EMPTY>" : value;
        }
    }

    internal static class WorldCanonicalEncoding
    {
        internal static int GetStableCollectionHashCode(string value)
        {
            if (value == null)
                return 0;
            unchecked
            {
                const int offset = (int)2166136261;
                const int prime = 16777619;
                int hash = offset;
                for (int index = 0; index < value.Length; index++)
                {
                    hash ^= value[index];
                    hash *= prime;
                }
                return hash;
            }
        }

        internal static string ComputeSha256(Action<Stream> writeCanonical)
        {
            if (writeCanonical == null)
                throw new ArgumentNullException(nameof(writeCanonical));
            using (var stream = new MemoryStream())
            {
                writeCanonical(stream);
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] hash = sha256.ComputeHash(stream.ToArray());
                    var result = new StringBuilder(hash.Length * 2);
                    for (int index = 0; index < hash.Length; index++)
                        result.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                    return result.ToString();
                }
            }
        }

        internal static void WriteString(Stream stream, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            WriteInt64(stream, bytes.LongLength);
            stream.Write(bytes, 0, bytes.Length);
        }

        internal static void WriteInt64(Stream stream, long value)
        {
            unchecked
            {
                for (int shift = 56; shift >= 0; shift -= 8)
                    stream.WriteByte((byte)(value >> shift));
            }
        }
    }
}
