using System;

namespace OldScars.Core.World
{
    /// <summary>
    /// Stable logical identity of a world sector. It is not a scene name,
    /// coordinate, collection index, ContentId or persistent scene-object ID.
    /// </summary>
    public readonly struct SectorId : IEquatable<SectorId>, IComparable<SectorId>
    {
        private const string Prefix = "sector_";
        private readonly string canonical;

        private SectorId(string canonical)
        {
            this.canonical = canonical;
        }

        public string Canonical => canonical ?? string.Empty;
        public bool IsValid => WorldId.TryValidate(canonical, Prefix, "SectorId", out _);

        public static SectorId FromDeterministicDomain(DeterministicDomainKey domainKey)
        {
            if (!domainKey.IsValid)
                throw new ArgumentException("A valid deterministic domain key is required.", nameof(domainKey));
            return new SectorId(Prefix + domainKey.Canonical.Substring(0, 32));
        }

        public static SectorId Parse(string raw)
        {
            if (!TryParse(raw, out SectorId sectorId, out string error))
                throw new FormatException($"Invalid SectorId '{Safe(raw)}': {error}.");
            return sectorId;
        }

        public static bool TryParse(string raw, out SectorId sectorId, out string error)
        {
            sectorId = default;
            if (!WorldId.TryValidate(raw, Prefix, "SectorId", out error))
                return false;
            sectorId = new SectorId(raw);
            return true;
        }

        public int CompareTo(SectorId other)
        {
            return string.CompareOrdinal(canonical, other.canonical);
        }

        public bool Equals(SectorId other)
        {
            return string.Equals(canonical, other.canonical, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is SectorId other && Equals(other);

        public override int GetHashCode()
        {
            // Collection equality only. Topology hashing writes Canonical.
            return WorldCanonicalEncoding.GetStableCollectionHashCode(canonical);
        }

        public override string ToString() => Canonical;
        public static bool operator ==(SectorId left, SectorId right) => left.Equals(right);
        public static bool operator !=(SectorId left, SectorId right) => !left.Equals(right);

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<EMPTY>" : value;
        }
    }
}
