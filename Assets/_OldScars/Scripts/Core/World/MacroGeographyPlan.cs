using System;
using System.Collections.Generic;
using System.Globalization;

namespace OldScars.Core.World
{
    public enum MacroLandform : byte
    {
        Plains = 0,
        RollingHills = 1,
        Highlands = 2,
        Mountains = 3
    }

    public readonly struct MacroGeographySample
    {
        public MacroGeographySample(ushort elevation, MacroLandform landform)
        {
            Elevation = elevation;
            Landform = landform;
        }

        /// <summary>
        /// Normalized fixed-point macro elevation in [0, 65535]. No physical
        /// metre conversion is frozen by this foundation.
        /// </summary>
        public ushort Elevation { get; }
        public MacroLandform Landform { get; }
    }

    /// <summary>
    /// Resolved generation-relevant tuning for the committed macro geography
    /// raster. It is persisted with the samples so later tuning cannot
    /// reinterpret an existing world. Worker count is deliberately absent.
    /// </summary>
    public sealed class MacroGeographyGenerationSettings
    {
        public const string CurrentContract = "macro_elevation_landforms_v1";
        public const int MaximumAttempts = 4;

        private MacroGeographyGenerationSettings(
            int sampleColumns,
            int sampleRows,
            int regionalFrequencyQ16,
            int baseElevationFrequencyQ16,
            int detailFrequencyQ16,
            int roughnessFrequencyQ16,
            int resolvedAttempt)
        {
            SampleColumns = sampleColumns;
            SampleRows = sampleRows;
            RegionalFrequencyQ16 = regionalFrequencyQ16;
            BaseElevationFrequencyQ16 = baseElevationFrequencyQ16;
            DetailFrequencyQ16 = detailFrequencyQ16;
            RoughnessFrequencyQ16 = roughnessFrequencyQ16;
            ResolvedAttempt = resolvedAttempt;
        }

        public string GenerationContract => CurrentContract;
        public int SampleColumns { get; }
        public int SampleRows { get; }
        public int RegionalFrequencyQ16 { get; }
        public int BaseElevationFrequencyQ16 { get; }
        public int DetailFrequencyQ16 { get; }
        public int RoughnessFrequencyQ16 { get; }
        public int ResolvedAttempt { get; }

        internal string DeterministicKey =>
            "geography_" + SampleColumns.ToString(CultureInfo.InvariantCulture) + "_" +
            SampleRows.ToString(CultureInfo.InvariantCulture) + "_" +
            RegionalFrequencyQ16.ToString(CultureInfo.InvariantCulture) + "_" +
            BaseElevationFrequencyQ16.ToString(CultureInfo.InvariantCulture) + "_" +
            DetailFrequencyQ16.ToString(CultureInfo.InvariantCulture) + "_" +
            RoughnessFrequencyQ16.ToString(CultureInfo.InvariantCulture) + "_" +
            ResolvedAttempt.ToString("D2", CultureInfo.InvariantCulture);

        public static MacroGeographyGenerationSettings ResolvePreset(
            WorldSizePreset preset,
            int resolvedAttempt)
        {
            switch (preset)
            {
                case WorldSizePreset.Small:
                    return CreateValidated(49, 49, 3 << 16, 2 << 16, 9 << 16, 18 << 16,
                        resolvedAttempt);
                case WorldSizePreset.Medium:
                    return CreateValidated(65, 65, 4 << 16, 2 << 16, 12 << 16, 24 << 16,
                        resolvedAttempt);
                case WorldSizePreset.Large:
                    return CreateValidated(81, 81, 5 << 16, 3 << 16, 16 << 16, 32 << 16,
                        resolvedAttempt);
                case WorldSizePreset.Huge:
                    return CreateValidated(113, 113, 7 << 16, 4 << 16, 22 << 16, 44 << 16,
                        resolvedAttempt);
                default:
                    throw new ArgumentOutOfRangeException(nameof(preset), preset,
                        "Unknown WorldSizePreset.");
            }
        }

        public static bool TryCreateResolved(
            string generationContract,
            int sampleColumns,
            int sampleRows,
            int regionalFrequencyQ16,
            int baseElevationFrequencyQ16,
            int detailFrequencyQ16,
            int roughnessFrequencyQ16,
            int resolvedAttempt,
            out MacroGeographyGenerationSettings settings,
            out string error)
        {
            settings = null;
            error = null;
            if (!string.Equals(generationContract, CurrentContract, StringComparison.Ordinal))
            {
                error = "unsupported generation contract '" + Safe(generationContract) + "'";
                return false;
            }
            if (sampleColumns < 2 || sampleRows < 2 || sampleColumns > 1025 || sampleRows > 1025)
            {
                error = "sample columns/rows must be between 2 and 1025";
                return false;
            }
            if (regionalFrequencyQ16 < 1 || baseElevationFrequencyQ16 < 1 ||
                detailFrequencyQ16 < 1 || roughnessFrequencyQ16 < 1)
            {
                error = "all resolved fixed-point frequencies must be positive";
                return false;
            }
            if (resolvedAttempt < 0 || resolvedAttempt >= MaximumAttempts)
            {
                error = "resolved attempt is outside the bounded retry contract";
                return false;
            }

            settings = new MacroGeographyGenerationSettings(
                sampleColumns,
                sampleRows,
                regionalFrequencyQ16,
                baseElevationFrequencyQ16,
                detailFrequencyQ16,
                roughnessFrequencyQ16,
                resolvedAttempt);
            return true;
        }

        private static MacroGeographyGenerationSettings CreateValidated(
            int columns,
            int rows,
            int regionalFrequencyQ16,
            int baseElevationFrequencyQ16,
            int detailFrequencyQ16,
            int roughnessFrequencyQ16,
            int resolvedAttempt)
        {
            if (!TryCreateResolved(
                    CurrentContract,
                    columns,
                    rows,
                    regionalFrequencyQ16,
                    baseElevationFrequencyQ16,
                    detailFrequencyQ16,
                    roughnessFrequencyQ16,
                    resolvedAttempt,
                    out MacroGeographyGenerationSettings settings,
                    out string error))
            {
                throw new InvalidOperationException("Built-in macro geography tuning is invalid: " + error + ".");
            }
            return settings;
        }

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<EMPTY>" : value;
        }
    }

    public sealed class MacroGeographyAnalysis
    {
        internal MacroGeographyAnalysis(
            int sampleCount,
            int plainsCount,
            int rollingHillsCount,
            int highlandsCount,
            int mountainsCount,
            int matchingNeighborEdges,
            int totalNeighborEdges,
            int largestPlainsRegion,
            int largestMountainRegion,
            ushort minimumElevation,
            ushort maximumElevation,
            long plainsRoughnessTotal,
            int plainsRoughnessEdges,
            long mountainRoughnessTotal,
            int mountainRoughnessEdges)
        {
            SampleCount = sampleCount;
            PlainsCount = plainsCount;
            RollingHillsCount = rollingHillsCount;
            HighlandsCount = highlandsCount;
            MountainsCount = mountainsCount;
            MatchingNeighborEdges = matchingNeighborEdges;
            TotalNeighborEdges = totalNeighborEdges;
            LargestPlainsRegion = largestPlainsRegion;
            LargestMountainRegion = largestMountainRegion;
            MinimumElevation = minimumElevation;
            MaximumElevation = maximumElevation;
            PlainsRoughnessTotal = plainsRoughnessTotal;
            PlainsRoughnessEdges = plainsRoughnessEdges;
            MountainRoughnessTotal = mountainRoughnessTotal;
            MountainRoughnessEdges = mountainRoughnessEdges;
        }

        public int SampleCount { get; }
        public int PlainsCount { get; }
        public int RollingHillsCount { get; }
        public int HighlandsCount { get; }
        public int MountainsCount { get; }
        public int MatchingNeighborEdges { get; }
        public int TotalNeighborEdges { get; }
        public int LargestPlainsRegion { get; }
        public int LargestMountainRegion { get; }
        public ushort MinimumElevation { get; }
        public ushort MaximumElevation { get; }
        public long PlainsRoughnessTotal { get; }
        public int PlainsRoughnessEdges { get; }
        public long MountainRoughnessTotal { get; }
        public int MountainRoughnessEdges { get; }

        public int SameLandformNeighborRatioQ16 =>
            TotalNeighborEdges == 0 ? 0 : (int)((long)MatchingNeighborEdges * 65535 / TotalNeighborEdges);

        public int PlainsPercentQ16 => PercentQ16(PlainsCount);
        public int RollingHillsPercentQ16 => PercentQ16(RollingHillsCount);
        public int HighlandsPercentQ16 => PercentQ16(HighlandsCount);
        public int MountainsPercentQ16 => PercentQ16(MountainsCount);
        public int AveragePlainsRoughness =>
            PlainsRoughnessEdges == 0 ? 0 : (int)(PlainsRoughnessTotal / PlainsRoughnessEdges);
        public int AverageMountainRoughness =>
            MountainRoughnessEdges == 0 ? 0 : (int)(MountainRoughnessTotal / MountainRoughnessEdges);

        public bool MeetsCommittedVariety(out string failure)
        {
            failure = null;
            if (PlainsPercentQ16 < 15000 || RollingHillsPercentQ16 < 14000 ||
                HighlandsPercentQ16 < 8000 || MountainsPercentQ16 < 4500)
            {
                failure = "landform distribution is missing a meaningful required region";
                return false;
            }
            if (SameLandformNeighborRatioQ16 < 43000)
            {
                failure = "landform field is too fragmented for regional coherence";
                return false;
            }
            if (LargestPlainsRegion < Math.Max(12, SampleCount / 80))
            {
                failure = "no sufficiently broad connected plains region exists";
                return false;
            }
            if (LargestMountainRegion < Math.Max(8, SampleCount / 160))
            {
                failure = "no sufficiently broad connected mountain region exists";
                return false;
            }
            if (MaximumElevation - MinimumElevation < 10000)
            {
                failure = "macro elevation range is too narrow";
                return false;
            }
            if (AverageMountainRoughness <= AveragePlainsRoughness)
            {
                failure = "mountain regions are not rougher than plains";
                return false;
            }
            return true;
        }

        private int PercentQ16(int count)
        {
            return SampleCount == 0 ? 0 : (int)((long)count * 65535 / SampleCount);
        }
    }

    /// <summary>
    /// Immutable world-wide macro elevation and landform truth. Samples are a
    /// compact fixed-point raster over the complete finite WorldBounds. It is
    /// independent of sectors, topology edges, Unity terrain and GameObjects.
    /// </summary>
    public sealed class MacroGeographyPlan
    {
        private const string CanonicalContract = "old_scars_macro_geography_v1";
        private readonly ushort[] elevations;
        private readonly byte[] landforms;

        private MacroGeographyPlan(
            MacroGeographyGenerationSettings generationSettings,
            FiniteMacroWorldBounds worldBounds,
            ushort[] elevationSamples,
            byte[] landformSamples)
        {
            GenerationSettings = generationSettings;
            WorldBounds = worldBounds;
            elevations = (ushort[])elevationSamples.Clone();
            landforms = (byte[])landformSamples.Clone();
            CanonicalHash = BuildCanonicalHash();
        }

        public MacroGeographyGenerationSettings GenerationSettings { get; }
        public FiniteMacroWorldBounds WorldBounds { get; }
        public int SampleColumns => GenerationSettings.SampleColumns;
        public int SampleRows => GenerationSettings.SampleRows;
        public int SampleCount => elevations.Length;
        public string CanonicalHash { get; }

        public static bool TryCreate(
            MacroGeographyGenerationSettings generationSettings,
            FiniteMacroWorldBounds worldBounds,
            ushort[] elevationSamples,
            byte[] landformSamples,
            out MacroGeographyPlan plan,
            out string error)
        {
            plan = null;
            error = null;
            if (generationSettings == null)
            {
                error = "MacroGeographyPlan requires resolved generation settings";
                return false;
            }
            if (worldBounds.Width < 2 || worldBounds.Height < 2)
            {
                error = "MacroGeographyPlan requires finite WorldBounds with at least two logical units per axis";
                return false;
            }
            int expectedCount;
            try
            {
                expectedCount = checked(generationSettings.SampleColumns * generationSettings.SampleRows);
            }
            catch (OverflowException)
            {
                error = "resolved macro geography sample count overflowed";
                return false;
            }
            if (elevationSamples == null || landformSamples == null ||
                elevationSamples.Length != expectedCount || landformSamples.Length != expectedCount)
            {
                error = "elevation and landform sample arrays must exactly match the resolved grid";
                return false;
            }
            for (int index = 0; index < landformSamples.Length; index++)
            {
                if (landformSamples[index] > (byte)MacroLandform.Mountains)
                {
                    error = "landform sample[" + index.ToString(CultureInfo.InvariantCulture) +
                            "] contains an unknown value";
                    return false;
                }
            }

            var candidate = new MacroGeographyPlan(
                generationSettings, worldBounds, elevationSamples, landformSamples);
            MacroGeographyAnalysis analysis = candidate.Analyze();
            if (!analysis.MeetsCommittedVariety(out string varietyFailure))
            {
                error = "macro geography failed global validation: " + varietyFailure;
                return false;
            }
            plan = candidate;
            return true;
        }

        public bool TrySampleAt(MacroPoint2D position, out MacroGeographySample sample)
        {
            sample = default;
            if (!WorldBounds.Contains(position))
                return false;
            sample = new MacroGeographySample(ElevationAt(position), LandformAt(position));
            return true;
        }

        public ushort ElevationAt(MacroPoint2D position)
        {
            RequireInside(position);
            ResolveAxis(position.X, WorldBounds.MinX, WorldBounds.Width,
                SampleColumns, out int x0, out int x1, out long xNumerator, out long xDenominator);
            ResolveAxis(position.Y, WorldBounds.MinY, WorldBounds.Height,
                SampleRows, out int y0, out int y1, out long yNumerator, out long yDenominator);

            long lower = Interpolate(
                elevations[y0 * SampleColumns + x0], elevations[y0 * SampleColumns + x1],
                xNumerator, xDenominator);
            long upper = Interpolate(
                elevations[y1 * SampleColumns + x0], elevations[y1 * SampleColumns + x1],
                xNumerator, xDenominator);
            long result = Interpolate(lower, upper, yNumerator, yDenominator);
            return (ushort)Math.Max(0, Math.Min(ushort.MaxValue, result));
        }

        public MacroLandform LandformAt(MacroPoint2D position)
        {
            RequireInside(position);
            int x = ResolveNearestAxis(position.X, WorldBounds.MinX, WorldBounds.Width, SampleColumns);
            int y = ResolveNearestAxis(position.Y, WorldBounds.MinY, WorldBounds.Height, SampleRows);
            return (MacroLandform)landforms[y * SampleColumns + x];
        }

        public MacroGeographyAnalysis Analyze()
        {
            int plains = 0;
            int rolling = 0;
            int highlands = 0;
            int mountains = 0;
            int matchingEdges = 0;
            int totalEdges = 0;
            ushort minimum = ushort.MaxValue;
            ushort maximum = ushort.MinValue;
            long plainsRoughness = 0;
            int plainsRoughnessEdges = 0;
            long mountainRoughness = 0;
            int mountainRoughnessEdges = 0;

            for (int y = 0; y < SampleRows; y++)
            {
                for (int x = 0; x < SampleColumns; x++)
                {
                    int index = y * SampleColumns + x;
                    MacroLandform landform = (MacroLandform)landforms[index];
                    switch (landform)
                    {
                        case MacroLandform.Plains: plains++; break;
                        case MacroLandform.RollingHills: rolling++; break;
                        case MacroLandform.Highlands: highlands++; break;
                        case MacroLandform.Mountains: mountains++; break;
                    }
                    if (elevations[index] < minimum) minimum = elevations[index];
                    if (elevations[index] > maximum) maximum = elevations[index];

                    if (x + 1 < SampleColumns)
                        AccumulateEdge(index, index + 1, landform, ref matchingEdges, ref totalEdges,
                            ref plainsRoughness, ref plainsRoughnessEdges,
                            ref mountainRoughness, ref mountainRoughnessEdges);
                    if (y + 1 < SampleRows)
                        AccumulateEdge(index, index + SampleColumns, landform,
                            ref matchingEdges, ref totalEdges,
                            ref plainsRoughness, ref plainsRoughnessEdges,
                            ref mountainRoughness, ref mountainRoughnessEdges);
                }
            }

            return new MacroGeographyAnalysis(
                elevations.Length,
                plains,
                rolling,
                highlands,
                mountains,
                matchingEdges,
                totalEdges,
                LargestRegion(MacroLandform.Plains),
                LargestRegion(MacroLandform.Mountains),
                minimum,
                maximum,
                plainsRoughness,
                plainsRoughnessEdges,
                mountainRoughness,
                mountainRoughnessEdges);
        }

        internal ushort[] CopyElevationSamples()
        {
            return (ushort[])elevations.Clone();
        }

        internal byte[] CopyLandformSamples()
        {
            return (byte[])landforms.Clone();
        }

        private void AccumulateEdge(
            int firstIndex,
            int secondIndex,
            MacroLandform firstLandform,
            ref int matchingEdges,
            ref int totalEdges,
            ref long plainsRoughness,
            ref int plainsRoughnessEdges,
            ref long mountainRoughness,
            ref int mountainRoughnessEdges)
        {
            totalEdges++;
            MacroLandform secondLandform = (MacroLandform)landforms[secondIndex];
            if (firstLandform == secondLandform)
                matchingEdges++;
            int difference = Math.Abs(elevations[firstIndex] - elevations[secondIndex]);
            if (firstLandform == MacroLandform.Plains && secondLandform == MacroLandform.Plains)
            {
                plainsRoughness += difference;
                plainsRoughnessEdges++;
            }
            if (firstLandform == MacroLandform.Mountains && secondLandform == MacroLandform.Mountains)
            {
                mountainRoughness += difference;
                mountainRoughnessEdges++;
            }
        }

        private int LargestRegion(MacroLandform target)
        {
            var visited = new bool[landforms.Length];
            var queue = new int[landforms.Length];
            int largest = 0;
            for (int start = 0; start < landforms.Length; start++)
            {
                if (visited[start] || landforms[start] != (byte)target)
                    continue;
                int head = 0;
                int tail = 0;
                queue[tail++] = start;
                visited[start] = true;
                while (head < tail)
                {
                    int index = queue[head++];
                    int x = index % SampleColumns;
                    int y = index / SampleColumns;
                    TryEnqueue(x - 1, y, target, visited, queue, ref tail);
                    TryEnqueue(x + 1, y, target, visited, queue, ref tail);
                    TryEnqueue(x, y - 1, target, visited, queue, ref tail);
                    TryEnqueue(x, y + 1, target, visited, queue, ref tail);
                }
                if (tail > largest)
                    largest = tail;
            }
            return largest;
        }

        private void TryEnqueue(
            int x,
            int y,
            MacroLandform target,
            bool[] visited,
            int[] queue,
            ref int tail)
        {
            if (x < 0 || x >= SampleColumns || y < 0 || y >= SampleRows)
                return;
            int index = y * SampleColumns + x;
            if (visited[index] || landforms[index] != (byte)target)
                return;
            visited[index] = true;
            queue[tail++] = index;
        }

        private string BuildCanonicalHash()
        {
            return WorldCanonicalEncoding.ComputeSha256(stream =>
            {
                WorldCanonicalEncoding.WriteString(stream, CanonicalContract);
                WorldCanonicalEncoding.WriteString(stream, GenerationSettings.GenerationContract);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.SampleColumns);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.SampleRows);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.RegionalFrequencyQ16);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.BaseElevationFrequencyQ16);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.DetailFrequencyQ16);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.RoughnessFrequencyQ16);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.ResolvedAttempt);
                WorldCanonicalEncoding.WriteInt64(stream, WorldBounds.MinX);
                WorldCanonicalEncoding.WriteInt64(stream, WorldBounds.MinY);
                WorldCanonicalEncoding.WriteInt64(stream, WorldBounds.MaxXExclusive);
                WorldCanonicalEncoding.WriteInt64(stream, WorldBounds.MaxYExclusive);
                WorldCanonicalEncoding.WriteInt64(stream, elevations.Length);
                for (int index = 0; index < elevations.Length; index++)
                {
                    stream.WriteByte((byte)(elevations[index] >> 8));
                    stream.WriteByte((byte)elevations[index]);
                    stream.WriteByte(landforms[index]);
                }
            });
        }

        private void RequireInside(MacroPoint2D position)
        {
            if (!WorldBounds.Contains(position))
                throw new ArgumentOutOfRangeException(nameof(position),
                    "Macro geography coordinates must be inside the finite WorldBounds.");
        }

        private static void ResolveAxis(
            long coordinate,
            long minimum,
            long extent,
            int sampleCount,
            out int lower,
            out int upper,
            out long numerator,
            out long denominator)
        {
            denominator = extent - 1;
            long scaled = checked((coordinate - minimum) * (sampleCount - 1L));
            lower = (int)(scaled / denominator);
            numerator = scaled % denominator;
            if (lower >= sampleCount - 1)
            {
                lower = sampleCount - 1;
                upper = lower;
                numerator = 0;
            }
            else
            {
                upper = lower + 1;
            }
        }

        private static int ResolveNearestAxis(long coordinate, long minimum, long extent, int sampleCount)
        {
            long denominator = extent - 1;
            long scaled = checked((coordinate - minimum) * (sampleCount - 1L));
            long rounded = (scaled + denominator / 2) / denominator;
            return (int)Math.Max(0, Math.Min(sampleCount - 1L, rounded));
        }

        private static long Interpolate(long first, long second, long numerator, long denominator)
        {
            if (numerator == 0 || first == second)
                return first;
            long scaled = checked((second - first) * numerator);
            long rounded = scaled >= 0
                ? (scaled + denominator / 2) / denominator
                : (scaled - denominator / 2) / denominator;
            return first + rounded;
        }
    }
}
