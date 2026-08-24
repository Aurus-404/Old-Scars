using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace OldScars.Core.World
{
    public readonly struct MacroPoint2D : IEquatable<MacroPoint2D>
    {
        public MacroPoint2D(long x, long y)
        {
            X = x;
            Y = y;
        }

        public long X { get; }
        public long Y { get; }

        public bool Equals(MacroPoint2D other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is MacroPoint2D other && Equals(other);
        public override int GetHashCode()
        {
            // Collection equality only; canonical plan evidence writes X/Y explicitly.
            return WorldCanonicalEncoding.GetStableCollectionHashCode(ToString());
        }
        public override string ToString() =>
            X.ToString(CultureInfo.InvariantCulture) + "," + Y.ToString(CultureInfo.InvariantCulture);
        public static bool operator ==(MacroPoint2D left, MacroPoint2D right) => left.Equals(right);
        public static bool operator !=(MacroPoint2D left, MacroPoint2D right) => !left.Equals(right);
    }

    /// <summary>
    /// Complete finite logical world extent. Max values are exclusive. This is
    /// neither Unity space nor the future geometry of any individual sector.
    /// </summary>
    public readonly struct FiniteMacroWorldBounds : IEquatable<FiniteMacroWorldBounds>
    {
        public FiniteMacroWorldBounds(long minX, long minY, long maxXExclusive, long maxYExclusive)
        {
            if (maxXExclusive <= minX || maxYExclusive <= minY)
                throw new ArgumentException("Finite macro world bounds require positive width and height.");
            MinX = minX;
            MinY = minY;
            MaxXExclusive = maxXExclusive;
            MaxYExclusive = maxYExclusive;
        }

        public long MinX { get; }
        public long MinY { get; }
        public long MaxXExclusive { get; }
        public long MaxYExclusive { get; }
        public long Width => MaxXExclusive - MinX;
        public long Height => MaxYExclusive - MinY;

        public bool Contains(MacroPoint2D point)
        {
            return point.X >= MinX && point.X < MaxXExclusive &&
                   point.Y >= MinY && point.Y < MaxYExclusive;
        }

        public bool Equals(FiniteMacroWorldBounds other)
        {
            return MinX == other.MinX && MinY == other.MinY &&
                   MaxXExclusive == other.MaxXExclusive && MaxYExclusive == other.MaxYExclusive;
        }
        public override bool Equals(object obj) => obj is FiniteMacroWorldBounds other && Equals(other);
        public override int GetHashCode()
        {
            // Collection equality only; canonical plan evidence writes each bound explicitly.
            string canonical = MinX.ToString(CultureInfo.InvariantCulture) + "|" +
                               MinY.ToString(CultureInfo.InvariantCulture) + "|" +
                               MaxXExclusive.ToString(CultureInfo.InvariantCulture) + "|" +
                               MaxYExclusive.ToString(CultureInfo.InvariantCulture);
            return WorldCanonicalEncoding.GetStableCollectionHashCode(canonical);
        }
        public static bool operator ==(FiniteMacroWorldBounds left, FiniteMacroWorldBounds right) => left.Equals(right);
        public static bool operator !=(FiniteMacroWorldBounds left, FiniteMacroWorldBounds right) => !left.Equals(right);
    }

    public sealed class MacroSectorPlacement
    {
        public MacroSectorPlacement(SectorId sectorId, MacroPoint2D position)
        {
            if (!sectorId.IsValid)
                throw new ArgumentException("A valid SectorId is required.", nameof(sectorId));
            SectorId = sectorId;
            Position = position;
        }

        public SectorId SectorId { get; }
        public MacroPoint2D Position { get; }
    }

    /// <summary>
    /// Immutable validated global skeleton generated before any local sector
    /// realization. It contains no Unity objects, terrain, geography or history.
    /// </summary>
    public sealed class MacroWorldPlan
    {
        private const string CanonicalContract = "old_scars_macro_world_plan_v1";
        private readonly ReadOnlyCollection<MacroSectorPlacement> sectorPlacements;

        private MacroWorldPlan(
            WorldGenerationSettings generationSettings,
            FiniteMacroWorldBounds worldBounds,
            IList<MacroSectorPlacement> placements,
            WorldTopology topology)
        {
            GenerationSettings = generationSettings;
            WorldBounds = worldBounds;
            sectorPlacements = new ReadOnlyCollection<MacroSectorPlacement>(
                new List<MacroSectorPlacement>(placements));
            Topology = topology;
            CanonicalDescription = BuildCanonicalDescription();
            CanonicalHash = BuildCanonicalHash();
        }

        public WorldGenerationSettings GenerationSettings { get; }
        public FiniteMacroWorldBounds WorldBounds { get; }
        public IReadOnlyList<MacroSectorPlacement> SectorPlacements => sectorPlacements;
        public WorldTopology Topology { get; }
        public string CanonicalDescription { get; }
        public string CanonicalHash { get; }

        public SectorId FindCentralSectorId()
        {
            long doubledCenterX = WorldBounds.MinX + WorldBounds.MaxXExclusive;
            long doubledCenterY = WorldBounds.MinY + WorldBounds.MaxYExclusive;
            MacroSectorPlacement best = sectorPlacements[0];
            long bestDistance = DistanceSquaredDoubled(best.Position, doubledCenterX, doubledCenterY);
            for (int index = 1; index < sectorPlacements.Count; index++)
            {
                MacroSectorPlacement candidate = sectorPlacements[index];
                long distance = DistanceSquaredDoubled(candidate.Position, doubledCenterX, doubledCenterY);
                if (distance < bestDistance ||
                    distance == bestDistance && candidate.SectorId.CompareTo(best.SectorId) < 0)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return best.SectorId;
        }

        public static bool TryCreate(
            WorldGenerationSettings generationSettings,
            FiniteMacroWorldBounds worldBounds,
            IEnumerable<MacroSectorPlacement> placementInputs,
            WorldTopology topology,
            out MacroWorldPlan plan,
            out string error)
        {
            plan = null;
            error = null;
            if (generationSettings == null)
            {
                error = "MacroWorldPlan requires validated WorldGenerationSettings";
                return false;
            }
            if (worldBounds.Width != generationSettings.ResolvedWorldWidth ||
                worldBounds.Height != generationSettings.ResolvedWorldHeight)
            {
                error = "Macro world bounds do not match the persisted resolved world extents";
                return false;
            }
            if (placementInputs == null)
            {
                error = "Macro sector placement collection is required";
                return false;
            }
            if (topology == null)
            {
                error = "MacroWorldPlan requires a validated WorldTopology";
                return false;
            }

            var placements = new List<MacroSectorPlacement>(placementInputs);
            placements.Sort(ComparePlacements);
            if (placements.Count != generationSettings.ResolvedSectorCount)
            {
                error = $"Macro sector placement count {placements.Count} does not match resolved count " +
                        generationSettings.ResolvedSectorCount + ".";
                return false;
            }

            long minimumSpacingSquared = generationSettings.ResolvedMinimumSectorSpacing *
                                         generationSettings.ResolvedMinimumSectorSpacing;
            for (int index = 0; index < placements.Count; index++)
            {
                MacroSectorPlacement placement = placements[index];
                if (placement == null)
                {
                    error = $"Macro sector placement[{index}] is null";
                    return false;
                }
                if (!worldBounds.Contains(placement.Position))
                {
                    error = $"Sector '{placement.SectorId.Canonical}' macro position {placement.Position} " +
                            "is outside finite world bounds";
                    return false;
                }
                if (index > 0 && placement.SectorId == placements[index - 1].SectorId)
                {
                    error = $"Duplicate macro placement SectorId '{placement.SectorId.Canonical}'";
                    return false;
                }
                for (int previous = 0; previous < index; previous++)
                {
                    MacroSectorPlacement other = placements[previous];
                    if (placement.Position == other.Position)
                    {
                        error = $"Duplicate macro position '{placement.Position}'";
                        return false;
                    }
                    if (DistanceSquared(placement.Position, other.Position) < minimumSpacingSquared)
                    {
                        error = $"Macro positions for '{placement.SectorId.Canonical}' and " +
                                $"'{other.SectorId.Canonical}' violate resolved minimum spacing " +
                                generationSettings.ResolvedMinimumSectorSpacing + ".";
                        return false;
                    }
                }
            }

            if (topology.Sectors.Count != placements.Count)
            {
                error = "WorldTopology sector count does not match MacroWorldPlan placements";
                return false;
            }
            for (int index = 0; index < placements.Count; index++)
            {
                if (placements[index].SectorId != topology.Sectors[index])
                {
                    error = "WorldTopology sector identities do not exactly match MacroWorldPlan placements";
                    return false;
                }
            }

            plan = new MacroWorldPlan(generationSettings, worldBounds, placements, topology);
            return true;
        }

        internal static long DistanceSquared(MacroPoint2D left, MacroPoint2D right)
        {
            long dx = left.X - right.X;
            long dy = left.Y - right.Y;
            return dx * dx + dy * dy;
        }

        private static long DistanceSquaredDoubled(MacroPoint2D point, long centerX, long centerY)
        {
            long dx = point.X * 2 - centerX;
            long dy = point.Y * 2 - centerY;
            return dx * dx + dy * dy;
        }

        private static int ComparePlacements(MacroSectorPlacement left, MacroSectorPlacement right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            return left.SectorId.CompareTo(right.SectorId);
        }

        private string BuildCanonicalDescription()
        {
            var builder = new StringBuilder();
            builder.Append(CanonicalContract).Append('\n');
            builder.Append("size|").Append(WorldGenerationSettings.ToCanonical(GenerationSettings.WorldSizePreset))
                .Append('|').Append(GenerationSettings.ResolvedSectorCount.ToString(CultureInfo.InvariantCulture))
                .Append('|').Append(GenerationSettings.ResolvedWorldWidth.ToString(CultureInfo.InvariantCulture))
                .Append('|').Append(GenerationSettings.ResolvedWorldHeight.ToString(CultureInfo.InvariantCulture))
                .Append('|').Append(GenerationSettings.ResolvedMinimumSectorSpacing.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
            builder.Append("bounds|").Append(WorldBounds.MinX.ToString(CultureInfo.InvariantCulture))
                .Append('|').Append(WorldBounds.MinY.ToString(CultureInfo.InvariantCulture))
                .Append('|').Append(WorldBounds.MaxXExclusive.ToString(CultureInfo.InvariantCulture))
                .Append('|').Append(WorldBounds.MaxYExclusive.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
            builder.Append("placements|").Append(sectorPlacements.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < sectorPlacements.Count; index++)
            {
                MacroSectorPlacement placement = sectorPlacements[index];
                builder.Append('\n').Append("placement|").Append(placement.SectorId.Canonical)
                    .Append('|').Append(placement.Position.X.ToString(CultureInfo.InvariantCulture))
                    .Append('|').Append(placement.Position.Y.ToString(CultureInfo.InvariantCulture));
            }
            builder.Append('\n').Append("topology|").Append(Topology.CanonicalDescription);
            return builder.ToString();
        }

        private string BuildCanonicalHash()
        {
            return WorldCanonicalEncoding.ComputeSha256(stream =>
            {
                WorldCanonicalEncoding.WriteString(stream, CanonicalContract);
                WorldCanonicalEncoding.WriteString(
                    stream, WorldGenerationSettings.ToCanonical(GenerationSettings.WorldSizePreset));
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.ResolvedSectorCount);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.ResolvedWorldWidth);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.ResolvedWorldHeight);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.ResolvedMinimumSectorSpacing);
                WorldCanonicalEncoding.WriteInt64(stream, WorldBounds.MinX);
                WorldCanonicalEncoding.WriteInt64(stream, WorldBounds.MinY);
                WorldCanonicalEncoding.WriteInt64(stream, WorldBounds.MaxXExclusive);
                WorldCanonicalEncoding.WriteInt64(stream, WorldBounds.MaxYExclusive);
                WorldCanonicalEncoding.WriteInt64(stream, sectorPlacements.Count);
                for (int index = 0; index < sectorPlacements.Count; index++)
                {
                    MacroSectorPlacement placement = sectorPlacements[index];
                    WorldCanonicalEncoding.WriteString(stream, placement.SectorId.Canonical);
                    WorldCanonicalEncoding.WriteInt64(stream, placement.Position.X);
                    WorldCanonicalEncoding.WriteInt64(stream, placement.Position.Y);
                }
                WorldCanonicalEncoding.WriteString(stream, Topology.CanonicalHash);
            });
        }
    }
}
