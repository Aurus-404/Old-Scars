using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace OldScars.Core.World
{
    public enum LandCoveragePreset : byte
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    /// <summary>
    /// Resolved inputs owned only by the Macro Water V1 pass. They never enter
    /// MacroWorldPlan or MacroGeography deterministic keys. Worker/execution
    /// settings are deliberately absent.
    /// </summary>
    public sealed class MacroWaterGenerationSettings
    {
        public const string CurrentContract = "macro_water_v1";

        private MacroWaterGenerationSettings(
            LandCoveragePreset landCoverage,
            int sampleColumns,
            int sampleRows,
            int targetLandRatioQ16,
            int minimumBasinCells)
        {
            LandCoverage = landCoverage;
            SampleColumns = sampleColumns;
            SampleRows = sampleRows;
            TargetLandRatioQ16 = targetLandRatioQ16;
            MinimumBasinCells = minimumBasinCells;
        }

        public string GenerationContract => CurrentContract;
        public LandCoveragePreset LandCoverage { get; }
        public int SampleColumns { get; }
        public int SampleRows { get; }
        public int TargetLandRatioQ16 { get; }
        public int MinimumBasinCells { get; }

        public static MacroWaterGenerationSettings Resolve(
            LandCoveragePreset coverage,
            MacroGeographyPlan geography)
        {
            if (geography == null)
                throw new ArgumentNullException(nameof(geography));
            int target;
            switch (coverage)
            {
                case LandCoveragePreset.Low: target = 36044; break;   // 55%
                case LandCoveragePreset.Medium: target = 44564; break; // 68%
                case LandCoveragePreset.High: target = 51117; break;  // 78%
                default:
                    throw new ArgumentOutOfRangeException(nameof(coverage), coverage,
                        "Unknown LandCoveragePreset.");
            }
            return CreateValidated(
                coverage, geography.SampleColumns, geography.SampleRows, target, 3);
        }

        public static bool TryCreateResolved(
            string generationContract,
            LandCoveragePreset coverage,
            int sampleColumns,
            int sampleRows,
            int targetLandRatioQ16,
            int minimumBasinCells,
            out MacroWaterGenerationSettings settings,
            out string error)
        {
            settings = null;
            error = null;
            if (!string.Equals(generationContract, CurrentContract, StringComparison.Ordinal))
            {
                error = "unsupported water generation contract '" + Safe(generationContract) + "'";
                return false;
            }
            if (!Enum.IsDefined(typeof(LandCoveragePreset), coverage))
            {
                error = "unknown LandCoveragePreset value '" + ((int)coverage).ToString(CultureInfo.InvariantCulture) + "'";
                return false;
            }
            if (sampleColumns < 2 || sampleRows < 2 || sampleColumns > 1025 || sampleRows > 1025)
            {
                error = "water sample columns/rows must be between 2 and 1025";
                return false;
            }
            if (targetLandRatioQ16 < 32768 || targetLandRatioQ16 > 58982)
            {
                error = "target land ratio must remain between 50% and 90% in Q16";
                return false;
            }
            if (minimumBasinCells < 1 || minimumBasinCells > 1024)
            {
                error = "minimum basin cells must be between 1 and 1024";
                return false;
            }

            settings = new MacroWaterGenerationSettings(
                coverage, sampleColumns, sampleRows, targetLandRatioQ16, minimumBasinCells);
            return true;
        }

        public static string ToCanonical(LandCoveragePreset coverage)
        {
            switch (coverage)
            {
                case LandCoveragePreset.Low: return "low";
                case LandCoveragePreset.Medium: return "medium";
                case LandCoveragePreset.High: return "high";
                default: throw new ArgumentOutOfRangeException(nameof(coverage), coverage,
                    "Unknown LandCoveragePreset.");
            }
        }

        public static bool TryParseCoverage(
            string raw,
            out LandCoveragePreset coverage,
            out string error)
        {
            error = null;
            switch (raw)
            {
                case "low": coverage = LandCoveragePreset.Low; return true;
                case "medium": coverage = LandCoveragePreset.Medium; return true;
                case "high": coverage = LandCoveragePreset.High; return true;
                default:
                    coverage = default;
                    error = "expected one of: low, medium, high";
                    return false;
            }
        }

        private static MacroWaterGenerationSettings CreateValidated(
            LandCoveragePreset coverage,
            int columns,
            int rows,
            int targetLandRatioQ16,
            int minimumBasinCells)
        {
            if (!TryCreateResolved(
                    CurrentContract, coverage, columns, rows, targetLandRatioQ16,
                    minimumBasinCells, out MacroWaterGenerationSettings settings, out string error))
            {
                throw new InvalidOperationException("Built-in macro water tuning is invalid: " + error + ".");
            }
            return settings;
        }

        private static string Safe(string value) => string.IsNullOrEmpty(value) ? "<EMPTY>" : value;
    }

    public sealed class MacroOceanBody
    {
        internal MacroOceanBody(ushort bodyId, int sampleCount)
        {
            BodyId = bodyId;
            SampleCount = sampleCount;
        }

        public ushort BodyId { get; }
        public int SampleCount { get; }
    }

    public sealed class MacroBasinCandidate
    {
        public MacroBasinCandidate(
            int representativeSampleIndex,
            int sampleCount,
            ushort spillElevation,
            ushort maximumFillDepth)
        {
            RepresentativeSampleIndex = representativeSampleIndex;
            SampleCount = sampleCount;
            SpillElevation = spillElevation;
            MaximumFillDepth = maximumFillDepth;
        }

        public int RepresentativeSampleIndex { get; }
        public int SampleCount { get; }
        public ushort SpillElevation { get; }
        public ushort MaximumFillDepth { get; }
    }

    public readonly struct MacroWaterSample
    {
        internal MacroWaterSample(
            bool isOcean,
            bool isCoastline,
            ushort oceanBodyId,
            ushort conditionedElevation,
            byte drainageDirection)
        {
            IsOcean = isOcean;
            IsCoastline = isCoastline;
            OceanBodyId = oceanBodyId;
            ConditionedElevation = conditionedElevation;
            DrainageDirection = drainageDirection;
        }

        public bool IsOcean { get; }
        public bool IsLand => !IsOcean;
        public bool IsCoastline { get; }
        public ushort OceanBodyId { get; }
        public ushort ConditionedElevation { get; }
        public byte DrainageDirection { get; }
    }

    /// <summary>
    /// Immutable committed world-wide Macro Water V1 truth. The raster shares
    /// MacroGeography coordinates and never uses sectors, topology edges,
    /// Unity objects, or runtime simulation as authority.
    /// </summary>
    public sealed class MacroWaterPlan
    {
        public const byte DrainageOutlet = byte.MaxValue;
        private const string CanonicalContract = "old_scars_macro_water_plan_v1";
        private readonly byte[] oceanMask;
        private readonly ushort[] oceanBodyLabels;
        private readonly byte[] coastlineMask;
        private readonly ushort[] conditionedElevations;
        private readonly byte[] drainageDirections;
        private readonly ReadOnlyCollection<MacroOceanBody> oceanBodies;
        private readonly ReadOnlyCollection<MacroBasinCandidate> basinCandidates;

        private MacroWaterPlan(
            MacroWaterGenerationSettings generationSettings,
            FiniteMacroWorldBounds worldBounds,
            ushort seaLevel,
            byte[] oceanSamples,
            ushort[] oceanLabels,
            byte[] coastlineSamples,
            ushort[] conditionedSamples,
            byte[] drainageSamples,
            IList<MacroOceanBody> bodies,
            IList<MacroBasinCandidate> basins)
        {
            GenerationSettings = generationSettings;
            WorldBounds = worldBounds;
            SeaLevel = seaLevel;
            oceanMask = (byte[])oceanSamples.Clone();
            oceanBodyLabels = (ushort[])oceanLabels.Clone();
            coastlineMask = (byte[])coastlineSamples.Clone();
            conditionedElevations = (ushort[])conditionedSamples.Clone();
            drainageDirections = (byte[])drainageSamples.Clone();
            oceanBodies = new ReadOnlyCollection<MacroOceanBody>(new List<MacroOceanBody>(bodies));
            basinCandidates = new ReadOnlyCollection<MacroBasinCandidate>(new List<MacroBasinCandidate>(basins));
            CanonicalHash = BuildCanonicalHash();
        }

        public MacroWaterGenerationSettings GenerationSettings { get; }
        public FiniteMacroWorldBounds WorldBounds { get; }
        public ushort SeaLevel { get; }
        public int SampleColumns => GenerationSettings.SampleColumns;
        public int SampleRows => GenerationSettings.SampleRows;
        public int SampleCount => oceanMask.Length;
        public IReadOnlyList<MacroOceanBody> OceanBodies => oceanBodies;
        public IReadOnlyList<MacroBasinCandidate> BasinCandidates => basinCandidates;
        public string CanonicalHash { get; }

        public int OceanSampleCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < oceanMask.Length; index++)
                    if (oceanMask[index] != 0) count++;
                return count;
            }
        }

        public int CoastlineSampleCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < coastlineMask.Length; index++)
                    if (coastlineMask[index] != 0) count++;
                return count;
            }
        }

        public int LandRatioQ16 => SampleCount == 0
            ? 0
            : (int)((long)(SampleCount - OceanSampleCount) * 65535 / SampleCount);

        public static bool TryCreate(
            MacroWaterGenerationSettings generationSettings,
            MacroGeographyPlan geography,
            ushort seaLevel,
            byte[] oceanSamples,
            ushort[] oceanLabels,
            byte[] coastlineSamples,
            ushort[] conditionedSamples,
            byte[] drainageSamples,
            IEnumerable<MacroBasinCandidate> basinInputs,
            out MacroWaterPlan plan,
            out string error)
        {
            plan = null;
            error = null;
            if (generationSettings == null || geography == null)
            {
                error = "MacroWaterPlan requires resolved water settings and committed MacroGeography";
                return false;
            }
            if (generationSettings.SampleColumns != geography.SampleColumns ||
                generationSettings.SampleRows != geography.SampleRows)
            {
                error = "water settings grid does not match committed MacroGeography";
                return false;
            }
            int expected = geography.SampleCount;
            if (oceanSamples == null || oceanLabels == null || coastlineSamples == null ||
                conditionedSamples == null || drainageSamples == null ||
                oceanSamples.Length != expected || oceanLabels.Length != expected ||
                coastlineSamples.Length != expected || conditionedSamples.Length != expected ||
                drainageSamples.Length != expected)
            {
                error = "all MacroWaterPlan rasters must exactly match the geography grid";
                return false;
            }

            var labels = (ushort[])oceanLabels.Clone();
            var bodies = new List<MacroOceanBody>();
            if (!ValidateOceanThreshold(
                    generationSettings.SampleColumns, generationSettings.SampleRows,
                    geography, seaLevel, oceanSamples, out error))
                return false;
            if (!ValidateAndBuildOceanBodies(
                    generationSettings.SampleColumns, generationSettings.SampleRows,
                    oceanSamples, labels, bodies, out error))
                return false;
            if (!ValidateCoastline(
                    generationSettings.SampleColumns, generationSettings.SampleRows,
                    oceanSamples, coastlineSamples, out error))
                return false;
            if (!ValidateDrainage(
                    geography, oceanSamples, conditionedSamples, drainageSamples, out error))
                return false;

            var basins = basinInputs == null
                ? new List<MacroBasinCandidate>()
                : new List<MacroBasinCandidate>(basinInputs);
            basins.Sort((left, right) =>
                left.RepresentativeSampleIndex.CompareTo(right.RepresentativeSampleIndex));
            if (!ValidateBasins(generationSettings, geography, conditionedSamples, basins, out error))
                return false;

            plan = new MacroWaterPlan(
                generationSettings, geography.WorldBounds, seaLevel,
                oceanSamples, labels, coastlineSamples, conditionedSamples,
                drainageSamples, bodies, basins);
            return true;
        }

        public MacroWaterSample SampleAt(MacroPoint2D position)
        {
            if (!WorldBounds.Contains(position))
                throw new ArgumentOutOfRangeException(nameof(position),
                    "Macro water position is outside finite WorldBounds.");
            int column = ResolveNearestAxis(
                position.X, WorldBounds.MinX, WorldBounds.Width, SampleColumns);
            int row = ResolveNearestAxis(
                position.Y, WorldBounds.MinY, WorldBounds.Height, SampleRows);
            return SampleAt(column, row);
        }

        public MacroWaterSample SampleAt(int column, int row)
        {
            int index = RequireSampleIndex(column, row);
            return new MacroWaterSample(
                oceanMask[index] != 0,
                coastlineMask[index] != 0,
                oceanBodyLabels[index],
                conditionedElevations[index],
                drainageDirections[index]);
        }

        public bool IsOceanAt(MacroPoint2D position) => SampleAt(position).IsOcean;
        public bool IsLandAt(MacroPoint2D position) => !SampleAt(position).IsOcean;

        public bool TryGetDownstreamSample(
            int column,
            int row,
            out int downstreamColumn,
            out int downstreamRow)
        {
            int index = RequireSampleIndex(column, row);
            downstreamColumn = -1;
            downstreamRow = -1;
            if (drainageDirections[index] == DrainageOutlet ||
                !TryResolveDownstream(
                    index, SampleColumns, SampleRows, drainageDirections[index], out int downstream))
                return false;
            downstreamColumn = downstream % SampleColumns;
            downstreamRow = downstream / SampleColumns;
            return true;
        }

        internal byte[] CopyOceanMask() => (byte[])oceanMask.Clone();
        internal ushort[] CopyOceanBodyLabels() => (ushort[])oceanBodyLabels.Clone();
        internal byte[] CopyCoastlineMask() => (byte[])coastlineMask.Clone();
        internal ushort[] CopyConditionedElevations() => (ushort[])conditionedElevations.Clone();
        internal byte[] CopyDrainageDirections() => (byte[])drainageDirections.Clone();

        private static bool ValidateOceanThreshold(
            int columns,
            int rows,
            MacroGeographyPlan geography,
            ushort seaLevel,
            byte[] ocean,
            out string error)
        {
            error = null;
            var expected = new bool[ocean.Length];
            var queue = new Queue<int>();
            for (int index = 0; index < ocean.Length; index++)
            {
                int x = index % columns;
                int y = index / columns;
                if (x != 0 && y != 0 && x != columns - 1 && y != rows - 1) continue;
                if (geography.ElevationSampleAt(x, y) > seaLevel || expected[index]) continue;
                expected[index] = true;
                queue.Enqueue(index);
            }
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int currentX = current % columns;
                int currentY = current / columns;
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0) continue;
                    int nextX = currentX + offsetX;
                    int nextY = currentY + offsetY;
                    if (nextX < 0 || nextX >= columns || nextY < 0 || nextY >= rows) continue;
                    int next = nextY * columns + nextX;
                    if (expected[next] || geography.ElevationSampleAt(nextX, nextY) > seaLevel) continue;
                    expected[next] = true;
                    queue.Enqueue(next);
                }
            }
            for (int index = 0; index < ocean.Length; index++)
            {
                if ((ocean[index] != 0) == expected[index]) continue;
                error = "ocean mask disagrees with sea level and finite-boundary connectivity at sample " + index;
                return false;
            }
            return true;
        }

        private static bool ValidateAndBuildOceanBodies(
            int columns,
            int rows,
            byte[] mask,
            ushort[] labels,
            List<MacroOceanBody> bodies,
            out string error)
        {
            error = null;
            int oceanCount = 0;
            int landCount = 0;
            ushort maximumLabel = 0;
            for (int index = 0; index < mask.Length; index++)
            {
                if (mask[index] > 1)
                {
                    error = "ocean mask contains a value other than 0/1 at sample " + index;
                    return false;
                }
                if (mask[index] == 0)
                {
                    landCount++;
                    if (labels[index] != 0)
                    {
                        error = "land sample " + index + " has a non-zero ocean body label";
                        return false;
                    }
                }
                else
                {
                    oceanCount++;
                    if (labels[index] == 0)
                    {
                        error = "ocean sample " + index + " has no connected body label";
                        return false;
                    }
                    maximumLabel = Math.Max(maximumLabel, labels[index]);
                }
            }
            if (oceanCount == 0 || landCount == 0)
            {
                error = "MacroWaterPlan requires both meaningful land and ocean samples";
                return false;
            }

            var counts = new int[maximumLabel + 1];
            var visited = new bool[mask.Length];
            var queue = new Queue<int>();
            for (int index = 0; index < mask.Length; index++)
            {
                if (mask[index] == 0) continue;
                counts[labels[index]]++;
                int x = index % columns;
                int y = index / columns;
                if ((x == 0 || y == 0 || x == columns - 1 || y == rows - 1) &&
                    !visited[index])
                {
                    ushort expectedLabel = labels[index];
                    visited[index] = true;
                    queue.Enqueue(index);
                    while (queue.Count > 0)
                    {
                        int current = queue.Dequeue();
                        int currentX = current % columns;
                        int currentY = current / columns;
                        for (int offsetY = -1; offsetY <= 1; offsetY++)
                        for (int offsetX = -1; offsetX <= 1; offsetX++)
                        {
                            if (offsetX == 0 && offsetY == 0) continue;
                            int nextX = currentX + offsetX;
                            int nextY = currentY + offsetY;
                            if (nextX < 0 || nextX >= columns || nextY < 0 || nextY >= rows) continue;
                            int next = nextY * columns + nextX;
                            if (mask[next] == 0 || visited[next]) continue;
                            if (labels[next] != expectedLabel)
                            {
                                error = "touching ocean samples disagree on connected body identity";
                                return false;
                            }
                            visited[next] = true;
                            queue.Enqueue(next);
                        }
                    }
                }
            }
            for (int index = 0; index < mask.Length; index++)
            {
                if (mask[index] != 0 && !visited[index])
                {
                    error = "ocean sample " + index + " is not connected to a finite-world boundary";
                    return false;
                }
            }
            for (int body = 1; body <= maximumLabel; body++)
            {
                if (counts[body] == 0)
                {
                    error = "ocean body labels are not contiguous";
                    return false;
                }
                bodies.Add(new MacroOceanBody((ushort)body, counts[body]));
            }
            return true;
        }

        private static bool ValidateCoastline(
            int columns,
            int rows,
            byte[] ocean,
            byte[] coastline,
            out string error)
        {
            error = null;
            int count = 0;
            for (int index = 0; index < coastline.Length; index++)
            {
                int x = index % columns;
                int y = index / columns;
                bool expected = ocean[index] == 0 && HasOceanNeighbor(x, y, columns, rows, ocean);
                if (coastline[index] > 1 || (coastline[index] != 0) != expected)
                {
                    error = "coastline mask disagrees with the global land/ocean boundary at sample " + index;
                    return false;
                }
                if (expected) count++;
            }
            if (count == 0)
            {
                error = "MacroWaterPlan has no global coastline samples";
                return false;
            }
            return true;
        }

        private static bool ValidateDrainage(
            MacroGeographyPlan geography,
            byte[] ocean,
            ushort[] conditioned,
            byte[] drainage,
            out string error)
        {
            error = null;
            int columns = geography.SampleColumns;
            int rows = geography.SampleRows;
            var state = new byte[drainage.Length];
            for (int start = 0; start < drainage.Length; start++)
            {
                if (conditioned[start] < geography.ElevationSampleAt(start % columns, start / columns))
                {
                    error = "conditioned drainage surface lowers committed elevation at sample " + start;
                    return false;
                }
                if (ocean[start] != 0)
                {
                    if (drainage[start] != DrainageOutlet)
                    {
                        error = "ocean drainage sample " + start + " must be an outlet";
                        return false;
                    }
                    state[start] = 2;
                }
                else if (drainage[start] > 7)
                {
                    error = "land drainage sample " + start + " has an invalid D8 direction";
                    return false;
                }
            }

            for (int start = 0; start < drainage.Length; start++)
            {
                if (state[start] == 2) continue;
                int current = start;
                var path = new List<int>();
                while (state[current] == 0)
                {
                    state[current] = 1;
                    path.Add(current);
                    if (!TryResolveDownstream(current, columns, rows, drainage[current], out int next))
                    {
                        error = "drainage direction exits finite grid at sample " + current;
                        return false;
                    }
                    if (conditioned[next] > conditioned[current])
                    {
                        error = "conditioned drainage climbs uphill from sample " + current;
                        return false;
                    }
                    current = next;
                    if (state[current] == 1)
                    {
                        error = "drainage contains a cycle reachable from sample " + start;
                        return false;
                    }
                }
                if (state[current] != 2)
                {
                    error = "drainage does not terminate in a committed ocean outlet";
                    return false;
                }
                for (int index = 0; index < path.Count; index++) state[path[index]] = 2;
            }
            return true;
        }

        private static bool ValidateBasins(
            MacroWaterGenerationSettings settings,
            MacroGeographyPlan geography,
            ushort[] conditioned,
            IList<MacroBasinCandidate> basins,
            out string error)
        {
            error = null;
            int previous = -1;
            for (int index = 0; index < basins.Count; index++)
            {
                MacroBasinCandidate basin = basins[index];
                if (basin == null || basin.RepresentativeSampleIndex <= previous ||
                    basin.RepresentativeSampleIndex < 0 ||
                    basin.RepresentativeSampleIndex >= geography.SampleCount)
                {
                    error = "basin candidates require unique canonical representative sample indices";
                    return false;
                }
                if (basin.SampleCount < settings.MinimumBasinCells || basin.MaximumFillDepth == 0)
                {
                    error = "basin candidate " + basin.RepresentativeSampleIndex +
                            " does not satisfy resolved minimum area/depth";
                    return false;
                }
                int x = basin.RepresentativeSampleIndex % geography.SampleColumns;
                int y = basin.RepresentativeSampleIndex / geography.SampleColumns;
                if (conditioned[basin.RepresentativeSampleIndex] <= geography.ElevationSampleAt(x, y))
                {
                    error = "basin representative is not part of the conditioned fill surface";
                    return false;
                }
                previous = basin.RepresentativeSampleIndex;
            }
            List<MacroBasinCandidate> expected = MacroWaterGenerator.BuildBasinCandidates(
                geography.CopyElevationSamples(), conditioned,
                geography.SampleColumns, geography.SampleRows,
                settings.MinimumBasinCells);
            if (expected.Count != basins.Count)
            {
                error = "basin candidate count does not match the conditioned drainage surface";
                return false;
            }
            for (int index = 0; index < expected.Count; index++)
            {
                MacroBasinCandidate left = expected[index];
                MacroBasinCandidate right = basins[index];
                if (left.RepresentativeSampleIndex != right.RepresentativeSampleIndex ||
                    left.SampleCount != right.SampleCount ||
                    left.SpillElevation != right.SpillElevation ||
                    left.MaximumFillDepth != right.MaximumFillDepth)
                {
                    error = "basin candidate " + index +
                            " does not match the conditioned drainage surface";
                    return false;
                }
            }
            return true;
        }

        private int RequireSampleIndex(int column, int row)
        {
            if (column < 0 || column >= SampleColumns || row < 0 || row >= SampleRows)
                throw new ArgumentOutOfRangeException(nameof(column),
                    "Macro water sample coordinate is outside the committed grid.");
            return row * SampleColumns + column;
        }

        private string BuildCanonicalHash()
        {
            return WorldCanonicalEncoding.ComputeSha256(stream =>
            {
                WorldCanonicalEncoding.WriteString(stream, CanonicalContract);
                WorldCanonicalEncoding.WriteString(stream, GenerationSettings.GenerationContract);
                WorldCanonicalEncoding.WriteString(
                    stream, MacroWaterGenerationSettings.ToCanonical(GenerationSettings.LandCoverage));
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.SampleColumns);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.SampleRows);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.TargetLandRatioQ16);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.MinimumBasinCells);
                WorldCanonicalEncoding.WriteInt64(stream, WorldBounds.MinX);
                WorldCanonicalEncoding.WriteInt64(stream, WorldBounds.MinY);
                WorldCanonicalEncoding.WriteInt64(stream, WorldBounds.MaxXExclusive);
                WorldCanonicalEncoding.WriteInt64(stream, WorldBounds.MaxYExclusive);
                WorldCanonicalEncoding.WriteInt64(stream, SeaLevel);
                WorldCanonicalEncoding.WriteInt64(stream, SampleCount);
                for (int index = 0; index < SampleCount; index++)
                {
                    stream.WriteByte(oceanMask[index]);
                    stream.WriteByte((byte)(oceanBodyLabels[index] >> 8));
                    stream.WriteByte((byte)oceanBodyLabels[index]);
                    stream.WriteByte(coastlineMask[index]);
                    stream.WriteByte((byte)(conditionedElevations[index] >> 8));
                    stream.WriteByte((byte)conditionedElevations[index]);
                    stream.WriteByte(drainageDirections[index]);
                }
                WorldCanonicalEncoding.WriteInt64(stream, basinCandidates.Count);
                for (int index = 0; index < basinCandidates.Count; index++)
                {
                    MacroBasinCandidate basin = basinCandidates[index];
                    WorldCanonicalEncoding.WriteInt64(stream, basin.RepresentativeSampleIndex);
                    WorldCanonicalEncoding.WriteInt64(stream, basin.SampleCount);
                    WorldCanonicalEncoding.WriteInt64(stream, basin.SpillElevation);
                    WorldCanonicalEncoding.WriteInt64(stream, basin.MaximumFillDepth);
                }
            });
        }

        internal static bool TryResolveDownstream(
            int index, int columns, int rows, byte direction, out int downstream)
        {
            downstream = -1;
            if (direction > 7) return false;
            int x = index % columns;
            int y = index / columns;
            int nextX = x + DirectionX[direction];
            int nextY = y + DirectionY[direction];
            if (nextX < 0 || nextX >= columns || nextY < 0 || nextY >= rows) return false;
            downstream = nextY * columns + nextX;
            return true;
        }

        internal static readonly int[] DirectionX = { 0, 1, 1, 1, 0, -1, -1, -1 };
        internal static readonly int[] DirectionY = { 1, 1, 0, -1, -1, -1, 0, 1 };

        private static bool HasOceanNeighbor(
            int x, int y, int columns, int rows, byte[] ocean)
        {
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                if (offsetX == 0 && offsetY == 0) continue;
                int sampleX = x + offsetX;
                int sampleY = y + offsetY;
                if (sampleX >= 0 && sampleX < columns && sampleY >= 0 && sampleY < rows &&
                    ocean[sampleY * columns + sampleX] != 0)
                    return true;
            }
            return false;
        }

        private static int ResolveNearestAxis(long coordinate, long minimum, long extent, int count)
        {
            long numerator = (coordinate - minimum) * (count - 1L);
            long denominator = extent - 1;
            return (int)Math.Max(0, Math.Min(count - 1L,
                (numerator * 2 + denominator) / (denominator * 2)));
        }
    }
}
