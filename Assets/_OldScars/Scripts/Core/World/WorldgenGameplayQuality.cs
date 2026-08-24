using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OldScars.Core.World
{
    /// <summary>
    /// Fixed-point, units-free quality criteria justified by the pre-milestone
    /// 192-world baseline corpus. These are macro traversal/site potentials,
    /// never final Walkable, Buildable, NavMesh, or physical slope claims.
    /// </summary>
    public static class WorldGameplayQualityCriteria
    {
        public const string CurrentContract = "worldgen_gameplay_quality_v1";
        public const int MaximumTraversalGradient = 2000;
        public const int MaximumTraversalLocalRelief = 4000;
        public const int MaximumSiteGradient = 1500;
        public const int MaximumSiteLocalRelief = 3000;
        public const int MinimumGlobalLowReliefShareQ16 = 30000;
        public const int MinimumGlobalTravelRegionQ16 = 12000;
        public const int MinimumLandTravelRegionQ16 = 2000;
        public const int MinimumRuggedShareQ16 = 5000;
        public const int MinimumSuitableStarterCandidates = 2;
    }

    public sealed class WorldStarterCandidate
    {
        internal WorldStarterCandidate(
            SectorId sectorId,
            bool isSuitable,
            bool hasSitePotential,
            MacroLandform landform,
            int gradient,
            int localRelief,
            int connectedTravelSamples,
            int connectedTravelRatioQ16,
            int centralityQ16,
            long suitabilityScore)
        {
            SectorId = sectorId;
            IsSuitable = isSuitable;
            HasSitePotential = hasSitePotential;
            Landform = landform;
            Gradient = gradient;
            LocalRelief = localRelief;
            ConnectedTravelSamples = connectedTravelSamples;
            ConnectedTravelRatioQ16 = connectedTravelRatioQ16;
            CentralityQ16 = centralityQ16;
            SuitabilityScore = suitabilityScore;
        }

        public SectorId SectorId { get; }
        public bool IsSuitable { get; }
        public bool HasSitePotential { get; }
        public MacroLandform Landform { get; }
        public int Gradient { get; }
        public int LocalRelief { get; }
        public int ConnectedTravelSamples { get; }
        public int ConnectedTravelRatioQ16 { get; }
        public int CentralityQ16 { get; }
        public long SuitabilityScore { get; }
    }

    /// <summary>
    /// Immutable cheap analysis derived from committed plan/geography/water.
    /// It is available for generation diagnostics and starter selection, but
    /// is not an additional geography or navigation authority.
    /// </summary>
    public sealed class WorldGameplayQualityAnalysis
    {
        private readonly ushort[] gradients;
        private readonly ushort[] localReliefs;
        private readonly byte[] traversalPotential;
        private readonly byte[] sitePotential;
        private readonly ReadOnlyCollection<WorldStarterCandidate> starterCandidates;
        private readonly ReadOnlyCollection<string> hardFailures;
        private readonly ReadOnlyCollection<string> softFindings;

        internal WorldGameplayQualityAnalysis(
            int columns,
            int rows,
            ushort[] maximumGradients,
            ushort[] localReliefSamples,
            byte[] traversalSamples,
            byte[] siteSamples,
            int globalLowReliefCount,
            int largestGlobalTravelRegion,
            int largestLandTravelRegion,
            int ruggedCount,
            IList<WorldStarterCandidate> candidates,
            IList<string> failures,
            IList<string> findings)
        {
            SampleColumns = columns;
            SampleRows = rows;
            gradients = (ushort[])maximumGradients.Clone();
            localReliefs = (ushort[])localReliefSamples.Clone();
            traversalPotential = (byte[])traversalSamples.Clone();
            sitePotential = (byte[])siteSamples.Clone();
            GlobalLowReliefSampleCount = globalLowReliefCount;
            LargestGlobalTravelRegion = largestGlobalTravelRegion;
            LargestLandTravelRegion = largestLandTravelRegion;
            RuggedSampleCount = ruggedCount;
            starterCandidates = new ReadOnlyCollection<WorldStarterCandidate>(
                new List<WorldStarterCandidate>(candidates));
            hardFailures = new ReadOnlyCollection<string>(new List<string>(failures));
            softFindings = new ReadOnlyCollection<string>(new List<string>(findings));
        }

        public string AnalysisContract => WorldGameplayQualityCriteria.CurrentContract;
        public int SampleColumns { get; }
        public int SampleRows { get; }
        public int SampleCount => gradients.Length;
        public int GlobalLowReliefSampleCount { get; }
        public int LargestGlobalTravelRegion { get; }
        public int LargestLandTravelRegion { get; }
        public int RuggedSampleCount { get; }
        public IReadOnlyList<WorldStarterCandidate> StarterCandidates => starterCandidates;
        public IReadOnlyList<string> HardFailures => hardFailures;
        public IReadOnlyList<string> SoftFindings => softFindings;
        public bool MeetsHardRequirements => hardFailures.Count == 0;

        public int GlobalLowReliefShareQ16 => RatioQ16(GlobalLowReliefSampleCount);
        public int LargestGlobalTravelRegionQ16 => RatioQ16(LargestGlobalTravelRegion);
        public int LargestLandTravelRegionQ16 => RatioQ16(LargestLandTravelRegion);
        public int RuggedShareQ16 => RatioQ16(RuggedSampleCount);

        public int SuitableStarterCandidateCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < starterCandidates.Count; index++)
                    if (starterCandidates[index].IsSuitable) count++;
                return count;
            }
        }

        public ushort GradientSampleAt(int column, int row) => gradients[RequireIndex(column, row)];
        public ushort LocalReliefSampleAt(int column, int row) => localReliefs[RequireIndex(column, row)];
        public bool HasTraversalPotentialAt(int column, int row) =>
            traversalPotential[RequireIndex(column, row)] != 0;
        public bool HasSitePotentialAt(int column, int row) =>
            sitePotential[RequireIndex(column, row)] != 0;

        private int RatioQ16(int count) => SampleCount == 0
            ? 0
            : (int)((long)count * 65535 / SampleCount);

        private int RequireIndex(int column, int row)
        {
            if (column < 0 || column >= SampleColumns || row < 0 || row >= SampleRows)
                throw new ArgumentOutOfRangeException(nameof(column),
                    "Gameplay-quality sample coordinate is outside the committed grid.");
            return row * SampleColumns + column;
        }
    }

    public static class WorldGameplayQualityAnalyzer
    {
        public static bool TryAnalyze(
            MacroWorldPlan plan,
            MacroGeographyPlan geography,
            MacroWaterPlan water,
            out WorldGameplayQualityAnalysis analysis,
            out string error)
        {
            analysis = null;
            error = null;
            if (plan == null || geography == null || water == null)
            {
                error = "Gameplay-quality analysis requires committed plan, geography, and water";
                return false;
            }
            if (plan.WorldBounds != geography.WorldBounds ||
                geography.WorldBounds != water.WorldBounds ||
                geography.SampleColumns != water.SampleColumns ||
                geography.SampleRows != water.SampleRows)
            {
                error = "Gameplay-quality inputs disagree on finite world bounds/grid";
                return false;
            }

            int columns = geography.SampleColumns;
            int rows = geography.SampleRows;
            var gradients = new ushort[geography.SampleCount];
            var localReliefs = new ushort[geography.SampleCount];
            var traversal = new byte[geography.SampleCount];
            var site = new byte[geography.SampleCount];
            var landTraversal = new bool[geography.SampleCount];
            int globalLowRelief = 0;
            int rugged = 0;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int index = y * columns + x;
                    ComputeRelief(geography, x, y, out int gradient, out int localRelief);
                    gradients[index] = (ushort)Math.Min(ushort.MaxValue, gradient);
                    localReliefs[index] = (ushort)Math.Min(ushort.MaxValue, localRelief);
                    MacroLandform landform = geography.LandformSampleAt(x, y);
                    bool lowRelief = gradient <= WorldGameplayQualityCriteria.MaximumTraversalGradient &&
                                     localRelief <= WorldGameplayQualityCriteria.MaximumTraversalLocalRelief &&
                                     landform != MacroLandform.Mountains;
                    bool siteCandidate = gradient <= WorldGameplayQualityCriteria.MaximumSiteGradient &&
                                         localRelief <= WorldGameplayQualityCriteria.MaximumSiteLocalRelief &&
                                         (landform == MacroLandform.Plains ||
                                          landform == MacroLandform.RollingHills);
                    if (lowRelief)
                    {
                        traversal[index] = 1;
                        globalLowRelief++;
                    }
                    if (siteCandidate && !water.SampleAt(x, y).IsOcean)
                        site[index] = 1;
                    landTraversal[index] = lowRelief && !water.SampleAt(x, y).IsOcean;
                    if (landform == MacroLandform.Mountains || gradient > 5000)
                        rugged++;
                }
            }

            int largestGlobal = LargestRegion(columns, rows, index => traversal[index] != 0, null, null);
            var componentBySample = new int[geography.SampleCount];
            var componentSizes = new List<int> { 0 };
            int largestLand = LargestRegion(
                columns, rows, index => landTraversal[index], componentBySample, componentSizes);
            List<WorldStarterCandidate> candidates = AnalyzeStarterAnchors(
                plan, geography, water, gradients, localReliefs, site,
                componentBySample, componentSizes);

            var hardFailures = new List<string>();
            var softFindings = new List<string>();
            int sampleCount = geography.SampleCount;
            int lowReliefQ16 = RatioQ16(globalLowRelief, sampleCount);
            int globalRegionQ16 = RatioQ16(largestGlobal, sampleCount);
            int landRegionQ16 = RatioQ16(largestLand, sampleCount);
            int ruggedQ16 = RatioQ16(rugged, sampleCount);
            int suitableCount = 0;
            int siteCount = 0;
            for (int index = 0; index < candidates.Count; index++)
                if (candidates[index].IsSuitable) suitableCount++;
            for (int index = 0; index < site.Length; index++)
                if (site[index] != 0) siteCount++;

            if (lowReliefQ16 < WorldGameplayQualityCriteria.MinimumGlobalLowReliefShareQ16)
                hardFailures.Add("global low-relief/traversal potential is pathologically scarce");
            if (globalRegionQ16 < WorldGameplayQualityCriteria.MinimumGlobalTravelRegionQ16)
                hardFailures.Add("no broad connected macro travel-corridor potential exists");
            if (landRegionQ16 < WorldGameplayQualityCriteria.MinimumLandTravelRegionQ16)
                hardFailures.Add("water leaves no broad connected low-relief land corridor");
            if (ruggedQ16 < WorldGameplayQualityCriteria.MinimumRuggedShareQ16)
                hardFailures.Add("macro quality validation would eliminate meaningful rugged terrain");
            if (suitableCount < WorldGameplayQualityCriteria.MinimumSuitableStarterCandidates)
                hardFailures.Add("fewer than two valid starter-sector anchors remain after water");

            int targetDifference = Math.Abs(
                water.LandRatioQ16 - water.GenerationSettings.TargetLandRatioQ16);
            if (targetDifference > 3000)
                softFindings.Add("boundary-connected ocean coverage differs materially from the selected target");
            if (suitableCount < 5)
                softFindings.Add("starter-sector choice has a narrow but valid candidate set");
            if (landRegionQ16 < 6000)
                softFindings.Add("water separates low-relief land travel potential into smaller regional corridors");
            if (RatioQ16(siteCount, sampleCount) < 5000)
                softFindings.Add("low-relief macro site-placement potential is limited");
            if (water.OceanBodies.Count > 12)
                softFindings.Add("the finite world has many disconnected boundary ocean bodies");

            analysis = new WorldGameplayQualityAnalysis(
                columns, rows, gradients, localReliefs, traversal, site,
                globalLowRelief, largestGlobal, largestLand, rugged,
                candidates, hardFailures, softFindings);
            return true;
        }

        private static List<WorldStarterCandidate> AnalyzeStarterAnchors(
            MacroWorldPlan plan,
            MacroGeographyPlan geography,
            MacroWaterPlan water,
            ushort[] gradients,
            ushort[] localReliefs,
            byte[] sitePotential,
            int[] componentBySample,
            IList<int> componentSizes)
        {
            var candidates = new List<WorldStarterCandidate>(plan.SectorPlacements.Count);
            int minimumConnectedSamples = Math.Max(4, geography.SampleCount / 100);
            long doubledCenterX = plan.WorldBounds.MinX + plan.WorldBounds.MaxXExclusive;
            long doubledCenterY = plan.WorldBounds.MinY + plan.WorldBounds.MaxYExclusive;
            long maximumDistance = plan.WorldBounds.Width * plan.WorldBounds.Width +
                                   plan.WorldBounds.Height * plan.WorldBounds.Height;
            for (int index = 0; index < plan.SectorPlacements.Count; index++)
            {
                MacroSectorPlacement placement = plan.SectorPlacements[index];
                int x = NearestSample(
                    placement.Position.X, plan.WorldBounds.MinX, plan.WorldBounds.Width,
                    geography.SampleColumns);
                int y = NearestSample(
                    placement.Position.Y, plan.WorldBounds.MinY, plan.WorldBounds.Height,
                    geography.SampleRows);
                int sample = y * geography.SampleColumns + x;
                MacroWaterSample waterSample = water.SampleAt(x, y);
                MacroLandform landform = geography.LandformSampleAt(x, y);
                int selectedSample = sample;
                int componentSize = 0;
                bool hasSite = false;
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    int sampleX = Math.Max(0, Math.Min(geography.SampleColumns - 1, x + offsetX));
                    int sampleY = Math.Max(0, Math.Min(geography.SampleRows - 1, y + offsetY));
                    int localSample = sampleY * geography.SampleColumns + sampleX;
                    if (water.SampleAt(sampleX, sampleY).IsOcean) continue;
                    int component = componentBySample[localSample];
                    int localComponentSize = component > 0 && component < componentSizes.Count
                        ? componentSizes[component]
                        : 0;
                    bool localSite = sitePotential[localSample] != 0;
                    if (localComponentSize > componentSize ||
                        localComponentSize == componentSize && localSite && !hasSite ||
                        localComponentSize == componentSize && localSite == hasSite &&
                        gradients[localSample] < gradients[selectedSample] ||
                        localComponentSize == componentSize && localSite == hasSite &&
                        gradients[localSample] == gradients[selectedSample] &&
                        localReliefs[localSample] < localReliefs[selectedSample] ||
                        localComponentSize == componentSize && localSite == hasSite &&
                        gradients[localSample] == gradients[selectedSample] &&
                        localReliefs[localSample] == localReliefs[selectedSample] &&
                        localSample < selectedSample)
                    {
                        selectedSample = localSample;
                        componentSize = localComponentSize;
                        hasSite = localSite;
                    }
                }
                bool suitable = !waterSample.IsOcean &&
                                landform != MacroLandform.Mountains &&
                                componentSize >= minimumConnectedSamples;
                int componentQ16 = RatioQ16(componentSize, geography.SampleCount);
                long dx = placement.Position.X * 2 - doubledCenterX;
                long dy = placement.Position.Y * 2 - doubledCenterY;
                long doubledDistance = dx * dx + dy * dy;
                int centralityQ16 = maximumDistance <= 0
                    ? 65535
                    : 65535 - (int)Math.Min(65535,
                        doubledDistance * 65535 / Math.Max(1, maximumDistance * 4));
                long score = (hasSite ? 1_000_000_000_000L : 0L) +
                             (long)componentQ16 * 10_000_000L +
                             (long)centralityQ16 * 10_000L +
                             (WorldGameplayQualityCriteria.MaximumTraversalGradient -
                              Math.Min(WorldGameplayQualityCriteria.MaximumTraversalGradient,
                                  (int)gradients[selectedSample])) * 10L +
                             (WorldGameplayQualityCriteria.MaximumTraversalLocalRelief -
                              Math.Min(WorldGameplayQualityCriteria.MaximumTraversalLocalRelief,
                                  (int)localReliefs[selectedSample]));
                candidates.Add(new WorldStarterCandidate(
                    placement.SectorId, suitable, hasSite, landform,
                    gradients[selectedSample], localReliefs[selectedSample], componentSize,
                    componentQ16, centralityQ16, score));
            }
            candidates.Sort((left, right) => left.SectorId.CompareTo(right.SectorId));
            return candidates;
        }

        private static void ComputeRelief(
            MacroGeographyPlan geography,
            int x,
            int y,
            out int maximumGradient,
            out int localRelief)
        {
            int center = geography.ElevationSampleAt(x, y);
            maximumGradient = 0;
            int minimum = center;
            int maximum = center;
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                int sampleX = Math.Max(0, Math.Min(geography.SampleColumns - 1, x + offsetX));
                int sampleY = Math.Max(0, Math.Min(geography.SampleRows - 1, y + offsetY));
                int elevation = geography.ElevationSampleAt(sampleX, sampleY);
                minimum = Math.Min(minimum, elevation);
                maximum = Math.Max(maximum, elevation);
                if (Math.Abs(offsetX) + Math.Abs(offsetY) == 1)
                    maximumGradient = Math.Max(maximumGradient, Math.Abs(center - elevation));
            }
            localRelief = maximum - minimum;
        }

        private static int LargestRegion(
            int columns,
            int rows,
            Func<int, bool> accepts,
            int[] componentBySample,
            IList<int> componentSizes)
        {
            var visited = new bool[columns * rows];
            var queue = new Queue<int>();
            int largest = 0;
            int component = 0;
            for (int start = 0; start < visited.Length; start++)
            {
                if (visited[start] || !accepts(start)) continue;
                component++;
                visited[start] = true;
                queue.Enqueue(start);
                int count = 0;
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    count++;
                    if (componentBySample != null) componentBySample[current] = component;
                    int x = current % columns;
                    int y = current / columns;
                    Visit(x - 1, y);
                    Visit(x + 1, y);
                    Visit(x, y - 1);
                    Visit(x, y + 1);
                }
                if (componentSizes != null) componentSizes.Add(count);
                largest = Math.Max(largest, count);
            }
            return largest;

            void Visit(int x, int y)
            {
                if (x < 0 || x >= columns || y < 0 || y >= rows) return;
                int index = y * columns + x;
                if (visited[index] || !accepts(index)) return;
                visited[index] = true;
                queue.Enqueue(index);
            }
        }

        private static int NearestSample(long coordinate, long minimum, long extent, int count)
        {
            long numerator = (coordinate - minimum) * (count - 1L);
            long denominator = extent - 1;
            return (int)Math.Max(0, Math.Min(count - 1L,
                (numerator * 2 + denominator) / (denominator * 2)));
        }

        private static int RatioQ16(int count, int total) => total == 0
            ? 0
            : (int)((long)count * 65535 / total);
    }

    public static class WorldStarterSectorSelector
    {
        public static bool TrySelect(
            WorldGameplayQualityAnalysis analysis,
            out SectorId starterSector,
            out string error)
        {
            starterSector = default;
            error = null;
            if (analysis == null)
            {
                error = "Starter selection requires gameplay-quality analysis";
                return false;
            }
            if (!analysis.MeetsHardRequirements)
            {
                error = "world failed hard gameplay-quality validation: " +
                        string.Join(" | ", analysis.HardFailures);
                return false;
            }

            WorldStarterCandidate best = null;
            for (int index = 0; index < analysis.StarterCandidates.Count; index++)
            {
                WorldStarterCandidate candidate = analysis.StarterCandidates[index];
                if (!candidate.IsSuitable) continue;
                if (best == null || candidate.SuitabilityScore > best.SuitabilityScore ||
                    candidate.SuitabilityScore == best.SuitabilityScore &&
                    candidate.SectorId.CompareTo(best.SectorId) < 0)
                {
                    best = candidate;
                }
            }
            if (best == null)
            {
                error = "No suitable starter-sector anchor survived hard validation";
                return false;
            }
            starterSector = best.SectorId;
            return true;
        }
    }
}
