using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace OldScars.Core.World
{
    public enum MacroHumanHubKind : byte
    {
        RegionalHub = 0,
        LocalHub = 1
    }

    public enum MacroRoadClass : byte
    {
        Primary = 0,
        Secondary = 1
    }

    public readonly struct MacroHumanSiteId : IEquatable<MacroHumanSiteId>, IComparable<MacroHumanSiteId>
    {
        private const string Prefix = "human_site_";
        private readonly string canonical;

        private MacroHumanSiteId(string canonicalValue) { canonical = canonicalValue; }
        public string Canonical => canonical ?? string.Empty;
        public bool IsValid => WorldId.TryValidate(canonical, Prefix, "MacroHumanSiteId", out _);

        public static MacroHumanSiteId FromDeterministicDomain(DeterministicDomainKey domain)
        {
            if (!domain.IsValid) throw new ArgumentException("A valid deterministic domain is required.", nameof(domain));
            return new MacroHumanSiteId(Prefix + domain.Canonical.Substring(0, 32));
        }

        public static bool TryParse(string raw, out MacroHumanSiteId value, out string error)
        {
            value = default;
            if (!WorldId.TryValidate(raw, Prefix, "MacroHumanSiteId", out error)) return false;
            value = new MacroHumanSiteId(raw);
            return true;
        }

        public int CompareTo(MacroHumanSiteId other) => string.CompareOrdinal(canonical, other.canonical);
        public bool Equals(MacroHumanSiteId other) => string.Equals(canonical, other.canonical, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is MacroHumanSiteId other && Equals(other);
        public override int GetHashCode() => WorldCanonicalEncoding.GetStableCollectionHashCode(canonical);
        public override string ToString() => Canonical;
        public static bool operator ==(MacroHumanSiteId left, MacroHumanSiteId right) => left.Equals(right);
        public static bool operator !=(MacroHumanSiteId left, MacroHumanSiteId right) => !left.Equals(right);
    }

    public readonly struct MacroRoadId : IEquatable<MacroRoadId>, IComparable<MacroRoadId>
    {
        private const string Prefix = "macro_road_";
        private readonly string canonical;

        private MacroRoadId(string canonicalValue) { canonical = canonicalValue; }
        public string Canonical => canonical ?? string.Empty;
        public bool IsValid => WorldId.TryValidate(canonical, Prefix, "MacroRoadId", out _);

        public static MacroRoadId FromDeterministicDomain(DeterministicDomainKey domain)
        {
            if (!domain.IsValid) throw new ArgumentException("A valid deterministic domain is required.", nameof(domain));
            return new MacroRoadId(Prefix + domain.Canonical.Substring(0, 32));
        }

        public static bool TryParse(string raw, out MacroRoadId value, out string error)
        {
            value = default;
            if (!WorldId.TryValidate(raw, Prefix, "MacroRoadId", out error)) return false;
            value = new MacroRoadId(raw);
            return true;
        }

        public int CompareTo(MacroRoadId other) => string.CompareOrdinal(canonical, other.canonical);
        public bool Equals(MacroRoadId other) => string.Equals(canonical, other.canonical, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is MacroRoadId other && Equals(other);
        public override int GetHashCode() => WorldCanonicalEncoding.GetStableCollectionHashCode(canonical);
        public override string ToString() => Canonical;
        public static bool operator ==(MacroRoadId left, MacroRoadId right) => left.Equals(right);
        public static bool operator !=(MacroRoadId left, MacroRoadId right) => !left.Equals(right);
    }

    public sealed class MacroHumanGeographyGenerationSettings
    {
        public const string CurrentContract = "macro_human_roads_v1";

        private MacroHumanGeographyGenerationSettings(
            int columns,
            int rows,
            int regionalHubTarget,
            int localHubTarget,
            int minimumRegionalSpacingCells,
            int minimumLocalSpacingCells,
            int extraPrimaryLinkTarget)
        {
            SampleColumns = columns;
            SampleRows = rows;
            RegionalHubTarget = regionalHubTarget;
            LocalHubTarget = localHubTarget;
            MinimumRegionalSpacingCells = minimumRegionalSpacingCells;
            MinimumLocalSpacingCells = minimumLocalSpacingCells;
            ExtraPrimaryLinkTarget = extraPrimaryLinkTarget;
        }

        public string GenerationContract => CurrentContract;
        public int SampleColumns { get; }
        public int SampleRows { get; }
        public int RegionalHubTarget { get; }
        public int LocalHubTarget { get; }
        public int MinimumRegionalSpacingCells { get; }
        public int MinimumLocalSpacingCells { get; }
        public int ExtraPrimaryLinkTarget { get; }

        internal string DeterministicKey =>
            "human_roads_" + SampleColumns.ToString(CultureInfo.InvariantCulture) + "_" +
            SampleRows.ToString(CultureInfo.InvariantCulture) + "_" +
            RegionalHubTarget.ToString(CultureInfo.InvariantCulture) + "_" +
            LocalHubTarget.ToString(CultureInfo.InvariantCulture) + "_" +
            MinimumRegionalSpacingCells.ToString(CultureInfo.InvariantCulture) + "_" +
            MinimumLocalSpacingCells.ToString(CultureInfo.InvariantCulture) + "_" +
            ExtraPrimaryLinkTarget.ToString(CultureInfo.InvariantCulture);

        public static MacroHumanGeographyGenerationSettings Resolve(
            WorldSizePreset size,
            int columns,
            int rows)
        {
            switch (size)
            {
                case WorldSizePreset.Small: return CreateValidated(columns, rows, 4, 8, 8, 3, 1);
                case WorldSizePreset.Medium: return CreateValidated(columns, rows, 7, 16, 8, 3, 2);
                case WorldSizePreset.Large: return CreateValidated(columns, rows, 10, 28, 9, 3, 3);
                case WorldSizePreset.Huge: return CreateValidated(columns, rows, 16, 48, 10, 4, 5);
                default: throw new ArgumentOutOfRangeException(nameof(size), size, "Unknown world size.");
            }
        }

        public static bool TryCreateResolved(
            string generationContract,
            int columns,
            int rows,
            int regionalHubTarget,
            int localHubTarget,
            int minimumRegionalSpacingCells,
            int minimumLocalSpacingCells,
            int extraPrimaryLinkTarget,
            out MacroHumanGeographyGenerationSettings settings,
            out string error)
        {
            settings = null;
            error = null;
            if (!string.Equals(generationContract, CurrentContract, StringComparison.Ordinal))
            {
                error = "unsupported generation contract '" + (generationContract ?? "<NULL>") + "'";
                return false;
            }
            if (columns < 2 || rows < 2 || columns > 1025 || rows > 1025)
            {
                error = "sample columns/rows must be between 2 and 1025";
                return false;
            }
            if (regionalHubTarget < 1 || localHubTarget < 1 || localHubTarget < regionalHubTarget)
            {
                error = "hub targets must be positive and local target must not be smaller than regional target";
                return false;
            }
            if (minimumRegionalSpacingCells < 1 || minimumLocalSpacingCells < 1)
            {
                error = "hub spacing must be positive";
                return false;
            }
            if (extraPrimaryLinkTarget < 0 || extraPrimaryLinkTarget > regionalHubTarget)
            {
                error = "extra primary link target is outside the bounded network contract";
                return false;
            }
            settings = new MacroHumanGeographyGenerationSettings(
                columns, rows, regionalHubTarget, localHubTarget,
                minimumRegionalSpacingCells, minimumLocalSpacingCells, extraPrimaryLinkTarget);
            return true;
        }

        private static MacroHumanGeographyGenerationSettings CreateValidated(
            int columns,
            int rows,
            int regional,
            int local,
            int regionalSpacing,
            int localSpacing,
            int extras)
        {
            if (!TryCreateResolved(
                    CurrentContract, columns, rows, regional, local,
                    regionalSpacing, localSpacing, extras, out MacroHumanGeographyGenerationSettings result,
                    out string error))
                throw new InvalidOperationException("Built-in human-geography settings are invalid: " + error + ".");
            return result;
        }
    }

    public sealed class MacroHumanSite
    {
        public MacroHumanSite(
            MacroHumanSiteId siteId,
            MacroHumanHubKind kind,
            MacroPoint2D position,
            int landComponentId)
        {
            if (!siteId.IsValid) throw new ArgumentException("A valid human site ID is required.", nameof(siteId));
            if (!Enum.IsDefined(typeof(MacroHumanHubKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown human hub kind.");
            if (landComponentId < 1) throw new ArgumentOutOfRangeException(nameof(landComponentId));
            SiteId = siteId;
            Kind = kind;
            Position = position;
            LandComponentId = landComponentId;
        }

        public MacroHumanSiteId SiteId { get; }
        public MacroHumanHubKind Kind { get; }
        public MacroPoint2D Position { get; }
        public int LandComponentId { get; }
    }

    public sealed class MacroRoad
    {
        private readonly ReadOnlyCollection<MacroPoint2D> polyline;

        public MacroRoad(
            MacroRoadId roadId,
            MacroRoadClass roadClass,
            MacroHumanSiteId firstEndpoint,
            MacroHumanSiteId secondEndpoint,
            IEnumerable<MacroPoint2D> points,
            int routedCellCount,
            long totalTraversalCost)
        {
            if (!roadId.IsValid) throw new ArgumentException("A valid road ID is required.", nameof(roadId));
            if (!Enum.IsDefined(typeof(MacroRoadClass), roadClass))
                throw new ArgumentOutOfRangeException(nameof(roadClass), roadClass, "Unknown road class.");
            if (!firstEndpoint.IsValid || !secondEndpoint.IsValid || firstEndpoint == secondEndpoint)
                throw new ArgumentException("Road endpoints must be distinct valid human site IDs.");
            var copied = points == null ? new List<MacroPoint2D>() : new List<MacroPoint2D>(points);
            if (copied.Count < 2) throw new ArgumentException("Road polyline requires at least two points.", nameof(points));
            if (routedCellCount < 2) throw new ArgumentOutOfRangeException(nameof(routedCellCount));
            if (totalTraversalCost < 1) throw new ArgumentOutOfRangeException(nameof(totalTraversalCost));
            RoadId = roadId;
            RoadClass = roadClass;
            FirstEndpoint = firstEndpoint;
            SecondEndpoint = secondEndpoint;
            polyline = new ReadOnlyCollection<MacroPoint2D>(copied);
            RoutedCellCount = routedCellCount;
            TotalTraversalCost = totalTraversalCost;
        }

        public MacroRoadId RoadId { get; }
        public MacroRoadClass RoadClass { get; }
        public MacroHumanSiteId FirstEndpoint { get; }
        public MacroHumanSiteId SecondEndpoint { get; }
        public IReadOnlyList<MacroPoint2D> Polyline => polyline;
        public int RoutedCellCount { get; }
        public long TotalTraversalCost { get; }
    }

    public sealed class MacroHumanGeographyQualityAnalysis
    {
        private readonly ReadOnlyCollection<string> hardFailures;
        private readonly ReadOnlyCollection<string> softFindings;

        internal MacroHumanGeographyQualityAnalysis(
            int minimumRegionalHubSpacingCells,
            int minimumAnyHubSpacingCells,
            int roadCoverageQ16,
            int longestUsefulGapCells,
            int averageDetourRatioQ16,
            long averageTraversalCostPerCell,
            int starterDistanceToNetworkCells,
            int independentCycleCount,
            int primaryRoadCount,
            int secondaryRoadCount,
            IList<string> failures,
            IList<string> findings)
        {
            MinimumRegionalHubSpacingCells = minimumRegionalHubSpacingCells;
            MinimumAnyHubSpacingCells = minimumAnyHubSpacingCells;
            RoadCoverageQ16 = roadCoverageQ16;
            LongestUsefulGapCells = longestUsefulGapCells;
            AverageDetourRatioQ16 = averageDetourRatioQ16;
            AverageTraversalCostPerCell = averageTraversalCostPerCell;
            StarterDistanceToNetworkCells = starterDistanceToNetworkCells;
            IndependentCycleCount = independentCycleCount;
            PrimaryRoadCount = primaryRoadCount;
            SecondaryRoadCount = secondaryRoadCount;
            hardFailures = new ReadOnlyCollection<string>(new List<string>(failures));
            softFindings = new ReadOnlyCollection<string>(new List<string>(findings));
        }

        public int MinimumRegionalHubSpacingCells { get; }
        public int MinimumAnyHubSpacingCells { get; }
        public int RoadCoverageQ16 { get; }
        public int LongestUsefulGapCells { get; }
        public int AverageDetourRatioQ16 { get; }
        public long AverageTraversalCostPerCell { get; }
        public int StarterDistanceToNetworkCells { get; }
        public int IndependentCycleCount { get; }
        public int PrimaryRoadCount { get; }
        public int SecondaryRoadCount { get; }
        public IReadOnlyList<string> HardFailures => hardFailures;
        public IReadOnlyList<string> SoftFindings => softFindings;
        public bool MeetsHardRequirements => hardFailures.Count == 0;
    }

    /// <summary>
    /// Immutable committed world-first human anchors and road geometry. It is
    /// not WorldTopology, sector-local content, physical terrain, or runtime routing.
    /// </summary>
    public sealed class MacroHumanGeographyPlan
    {
        private const string CanonicalContract = "old_scars_macro_human_geography_v1";
        private readonly ReadOnlyCollection<MacroHumanSite> sites;
        private readonly ReadOnlyCollection<MacroRoad> roads;

        private MacroHumanGeographyPlan(
            MacroHumanGeographyGenerationSettings settings,
            FiniteMacroWorldBounds bounds,
            SectorId starterAccessSectorId,
            IList<MacroHumanSite> orderedSites,
            IList<MacroRoad> orderedRoads,
            MacroHumanGeographyQualityAnalysis quality)
        {
            GenerationSettings = settings;
            WorldBounds = bounds;
            StarterAccessSectorId = starterAccessSectorId;
            sites = new ReadOnlyCollection<MacroHumanSite>(new List<MacroHumanSite>(orderedSites));
            roads = new ReadOnlyCollection<MacroRoad>(new List<MacroRoad>(orderedRoads));
            Quality = quality;
            CanonicalHash = BuildCanonicalHash();
        }

        public MacroHumanGeographyGenerationSettings GenerationSettings { get; }
        public FiniteMacroWorldBounds WorldBounds { get; }
        public SectorId StarterAccessSectorId { get; }
        public IReadOnlyList<MacroHumanSite> Sites => sites;
        public IReadOnlyList<MacroRoad> Roads => roads;
        public MacroHumanGeographyQualityAnalysis Quality { get; }
        public string CanonicalHash { get; }

        public int RegionalHubCount => CountSites(MacroHumanHubKind.RegionalHub);
        public int LocalHubCount => CountSites(MacroHumanHubKind.LocalHub);
        public int PrimaryRoadCount => CountRoads(MacroRoadClass.Primary);
        public int SecondaryRoadCount => CountRoads(MacroRoadClass.Secondary);
        public int GeometryPointCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < roads.Count; index++) count += roads[index].Polyline.Count;
                return count;
            }
        }

        public bool TryGetSite(MacroHumanSiteId siteId, out MacroHumanSite site)
        {
            int low = 0;
            int high = sites.Count - 1;
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                int comparison = sites[middle].SiteId.CompareTo(siteId);
                if (comparison == 0) { site = sites[middle]; return true; }
                if (comparison < 0) low = middle + 1; else high = middle - 1;
            }
            site = null;
            return false;
        }

        public static bool TryCreate(
            MacroHumanGeographyGenerationSettings settings,
            MacroWorldPlan worldPlan,
            MacroGeographyPlan geography,
            MacroWaterPlan water,
            WorldGameplayQualityAnalysis worldQuality,
            SectorId starterAccessSectorId,
            IEnumerable<MacroHumanSite> siteInputs,
            IEnumerable<MacroRoad> roadInputs,
            out MacroHumanGeographyPlan plan,
            out string error)
        {
            plan = null;
            error = null;
            if (settings == null || worldPlan == null || geography == null || water == null || worldQuality == null)
            {
                error = "Human geography requires resolved settings and committed plan/geography/water/quality";
                return false;
            }
            if (worldPlan.WorldBounds != geography.WorldBounds || geography.WorldBounds != water.WorldBounds ||
                settings.SampleColumns != geography.SampleColumns || settings.SampleRows != geography.SampleRows)
            {
                error = "Human geography inputs disagree on finite bounds/grid";
                return false;
            }
            if (!worldPlan.TryGetSectorPlacement(starterAccessSectorId, out MacroSectorPlacement starterPlacement))
            {
                error = "Human geography starter-access sector is absent from MacroWorldPlan";
                return false;
            }
            if (siteInputs == null || roadInputs == null)
            {
                error = "Human site and road collections are required";
                return false;
            }

            var orderedSites = new List<MacroHumanSite>(siteInputs);
            orderedSites.Sort((left, right) => left.SiteId.CompareTo(right.SiteId));
            var occupied = new HashSet<MacroPoint2D>();
            var siteById = new Dictionary<MacroHumanSiteId, MacroHumanSite>();
            int[] landComponents = MacroHumanRaster.BuildLandComponents(water, out _);
            for (int index = 0; index < orderedSites.Count; index++)
            {
                MacroHumanSite site = orderedSites[index];
                if (site == null || !site.SiteId.IsValid || !worldPlan.WorldBounds.Contains(site.Position) ||
                    !water.IsLandAt(site.Position))
                {
                    error = "Human site[" + index.ToString(CultureInfo.InvariantCulture) + "] is null, invalid, outside bounds, or ocean";
                    return false;
                }
                MacroHumanRaster.ToSample(site.Position, water, out int siteX, out int siteY);
                if (landComponents[siteY * water.SampleColumns + siteX] != site.LandComponentId)
                {
                    error = "Human site[" + index.ToString(CultureInfo.InvariantCulture) +
                            "] landComponentId does not match the global Water landmass";
                    return false;
                }
                if (!occupied.Add(site.Position) || !siteById.TryAdd(site.SiteId, site))
                {
                    error = "Human sites contain duplicate identity or macro position";
                    return false;
                }
            }
            if (orderedSites.Count < 2)
            {
                error = "Human geography requires at least two hubs";
                return false;
            }

            var orderedRoads = new List<MacroRoad>(roadInputs);
            orderedRoads.Sort((left, right) => left.RoadId.CompareTo(right.RoadId));
            var roadIds = new HashSet<MacroRoadId>();
            var endpointPairs = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < orderedRoads.Count; index++)
            {
                MacroRoad road = orderedRoads[index];
                if (road == null || !road.RoadId.IsValid || !roadIds.Add(road.RoadId) ||
                    !siteById.TryGetValue(road.FirstEndpoint, out MacroHumanSite first) ||
                    !siteById.TryGetValue(road.SecondEndpoint, out MacroHumanSite second))
                {
                    error = "Road[" + index.ToString(CultureInfo.InvariantCulture) + "] has invalid identity or endpoints";
                    return false;
                }
                if (first.LandComponentId != second.LandComponentId)
                {
                    error = "Road '" + road.RoadId.Canonical + "' crosses distinct landmasses";
                    return false;
                }
                if (road.Polyline[0] != first.Position || road.Polyline[road.Polyline.Count - 1] != second.Position)
                {
                    error = "Road '" + road.RoadId.Canonical + "' polyline endpoints do not match its hubs";
                    return false;
                }
                string pair = CanonicalPair(road.RoadClass, road.FirstEndpoint, road.SecondEndpoint);
                if (!endpointPairs.Add(pair))
                {
                    error = "Duplicate road class/endpoints pair '" + pair + "'";
                    return false;
                }
                for (int point = 0; point < road.Polyline.Count; point++)
                {
                    if (!worldPlan.WorldBounds.Contains(road.Polyline[point]) || water.IsOceanAt(road.Polyline[point]))
                    {
                        error = "Road '" + road.RoadId.Canonical + "' contains an out-of-bounds or ocean point";
                        return false;
                    }
                    if (point > 0 && !MacroHumanRaster.SegmentIsLand(
                            road.Polyline[point - 1], road.Polyline[point], water))
                    {
                        error = "Road '" + road.RoadId.Canonical + "' crosses ocean between polyline points";
                        return false;
                    }
                }
                if (!ValidateRouteMetadata(
                        road, geography, water, worldQuality, out string metadataError))
                {
                    error = "Road '" + road.RoadId.Canonical + "' metadata is inconsistent: " +
                            metadataError;
                    return false;
                }
            }

            if (!ValidateNetworkStructure(orderedSites, orderedRoads, siteById, out error))
                return false;

            MacroHumanGeographyQualityAnalysis quality = Analyze(
                settings, worldPlan, water, worldQuality, starterPlacement,
                orderedSites, orderedRoads);
            if (!quality.MeetsHardRequirements)
            {
                error = "human geography failed hard validation: " + string.Join(" | ", quality.HardFailures);
                return false;
            }
            plan = new MacroHumanGeographyPlan(
                settings, worldPlan.WorldBounds, starterAccessSectorId,
                orderedSites, orderedRoads, quality);
            return true;
        }

        private static bool ValidateNetworkStructure(
            IList<MacroHumanSite> siteList,
            IList<MacroRoad> roadList,
            IReadOnlyDictionary<MacroHumanSiteId, MacroHumanSite> siteById,
            out string error)
        {
            error = null;
            var regionalByComponent = new Dictionary<int, List<MacroHumanSiteId>>();
            var primaryAdjacency = new Dictionary<MacroHumanSiteId, List<MacroHumanSiteId>>();
            var localsWithBranch = new HashSet<MacroHumanSiteId>();
            for (int index = 0; index < siteList.Count; index++)
            {
                MacroHumanSite site = siteList[index];
                if (site.Kind == MacroHumanHubKind.RegionalHub)
                {
                    if (!regionalByComponent.TryGetValue(site.LandComponentId, out List<MacroHumanSiteId> component))
                    {
                        component = new List<MacroHumanSiteId>();
                        regionalByComponent.Add(site.LandComponentId, component);
                    }
                    component.Add(site.SiteId);
                    primaryAdjacency.Add(site.SiteId, new List<MacroHumanSiteId>());
                }
            }

            for (int index = 0; index < roadList.Count; index++)
            {
                MacroRoad road = roadList[index];
                MacroHumanSite first = siteById[road.FirstEndpoint];
                MacroHumanSite second = siteById[road.SecondEndpoint];
                if (road.RoadClass == MacroRoadClass.Primary)
                {
                    if (first.Kind != MacroHumanHubKind.RegionalHub ||
                        second.Kind != MacroHumanHubKind.RegionalHub)
                    {
                        error = "Primary road '" + road.RoadId.Canonical +
                                "' must connect two RegionalHub anchors";
                        return false;
                    }
                    primaryAdjacency[first.SiteId].Add(second.SiteId);
                    primaryAdjacency[second.SiteId].Add(first.SiteId);
                    continue;
                }

                bool firstLocal = first.Kind == MacroHumanHubKind.LocalHub;
                bool secondLocal = second.Kind == MacroHumanHubKind.LocalHub;
                if (firstLocal == secondLocal)
                {
                    error = "Secondary road '" + road.RoadId.Canonical +
                            "' must connect one LocalHub and one RegionalHub";
                    return false;
                }
                localsWithBranch.Add(firstLocal ? first.SiteId : second.SiteId);
            }

            var componentIds = new List<int>(regionalByComponent.Keys);
            componentIds.Sort();
            for (int componentIndex = 0; componentIndex < componentIds.Count; componentIndex++)
            {
                List<MacroHumanSiteId> component = regionalByComponent[componentIds[componentIndex]];
                component.Sort();
                var visited = new HashSet<MacroHumanSiteId>();
                var queue = new Queue<MacroHumanSiteId>();
                visited.Add(component[0]);
                queue.Enqueue(component[0]);
                while (queue.Count > 0)
                {
                    MacroHumanSiteId current = queue.Dequeue();
                    List<MacroHumanSiteId> neighbours = primaryAdjacency[current];
                    neighbours.Sort();
                    for (int neighbourIndex = 0; neighbourIndex < neighbours.Count; neighbourIndex++)
                        if (visited.Add(neighbours[neighbourIndex])) queue.Enqueue(neighbours[neighbourIndex]);
                }
                for (int siteIndex = 0; siteIndex < component.Count; siteIndex++)
                {
                    if (visited.Contains(component[siteIndex])) continue;
                    error = "Primary road backbone is disconnected on land component " +
                            componentIds[componentIndex].ToString(CultureInfo.InvariantCulture);
                    return false;
                }
            }

            for (int index = 0; index < siteList.Count; index++)
            {
                MacroHumanSite site = siteList[index];
                if (site.Kind == MacroHumanHubKind.LocalHub && !localsWithBranch.Contains(site.SiteId))
                {
                    error = "LocalHub '" + site.SiteId.Canonical + "' has no Secondary road branch";
                    return false;
                }
            }
            return true;
        }

        private static bool ValidateRouteMetadata(
            MacroRoad road,
            MacroGeographyPlan geography,
            MacroWaterPlan water,
            WorldGameplayQualityAnalysis worldQuality,
            out string error)
        {
            error = null;
            int cellCount = 0;
            long traversalCost = 0;
            int previousX = 0;
            int previousY = 0;
            bool hasPrevious = false;
            for (int segment = 1; segment < road.Polyline.Count; segment++)
            {
                int segmentPoint = 0;
                MacroHumanRaster.RasterizeSegment(
                    road.Polyline[segment - 1], road.Polyline[segment], water,
                    (x, y) =>
                    {
                        if (segment > 1 && segmentPoint++ == 0) return;
                        if (hasPrevious)
                        {
                            int sampleCost = MacroHumanGeographyGenerator.EvaluateTraversalCost(
                                geography, water, worldQuality, x, y);
                            traversalCost += (long)sampleCost *
                                             (previousX != x && previousY != y ? 141 : 100) / 100;
                        }
                        previousX = x;
                        previousY = y;
                        hasPrevious = true;
                        cellCount++;
                    });
            }
            if (cellCount != road.RoutedCellCount)
            {
                error = "routedCellCount persisted " + road.RoutedCellCount.ToString(CultureInfo.InvariantCulture) +
                        " but geometry reconstructs " + cellCount.ToString(CultureInfo.InvariantCulture);
                return false;
            }
            if (traversalCost != road.TotalTraversalCost)
            {
                error = "totalTraversalCost persisted " + road.TotalTraversalCost.ToString(CultureInfo.InvariantCulture) +
                        " but geometry reconstructs " + traversalCost.ToString(CultureInfo.InvariantCulture);
                return false;
            }
            return true;
        }

        private static MacroHumanGeographyQualityAnalysis Analyze(
            MacroHumanGeographyGenerationSettings settings,
            MacroWorldPlan worldPlan,
            MacroWaterPlan water,
            WorldGameplayQualityAnalysis worldQuality,
            MacroSectorPlacement starterPlacement,
            IList<MacroHumanSite> siteList,
            IList<MacroRoad> roadList)
        {
            int minimumRegional = int.MaxValue;
            int minimumAny = int.MaxValue;
            int regionalCount = 0;
            int localCount = 0;
            for (int first = 0; first < siteList.Count; first++)
            {
                if (siteList[first].Kind == MacroHumanHubKind.RegionalHub) regionalCount++; else localCount++;
                MacroHumanRaster.ToSample(siteList[first].Position, water, out int firstX, out int firstY);
                for (int second = first + 1; second < siteList.Count; second++)
                {
                    MacroHumanRaster.ToSample(siteList[second].Position, water, out int secondX, out int secondY);
                    int distance = Math.Max(Math.Abs(firstX - secondX), Math.Abs(firstY - secondY));
                    minimumAny = Math.Min(minimumAny, distance);
                    if (siteList[first].Kind == MacroHumanHubKind.RegionalHub &&
                        siteList[second].Kind == MacroHumanHubKind.RegionalHub)
                        minimumRegional = Math.Min(minimumRegional, distance);
                }
            }
            if (minimumRegional == int.MaxValue) minimumRegional = 0;
            if (minimumAny == int.MaxValue) minimumAny = 0;

            var roadCells = new HashSet<int>();
            int primary = 0;
            int secondary = 0;
            long totalDetour = 0;
            long totalCost = 0;
            long totalSteps = 0;
            var regionalComponents = new HashSet<int>();
            for (int index = 0; index < siteList.Count; index++)
                if (siteList[index].Kind == MacroHumanHubKind.RegionalHub)
                    regionalComponents.Add(siteList[index].LandComponentId);
            for (int index = 0; index < roadList.Count; index++)
            {
                MacroRoad road = roadList[index];
                if (road.RoadClass == MacroRoadClass.Primary) primary++; else secondary++;
                MacroHumanSite first = FindSite(siteList, road.FirstEndpoint);
                MacroHumanSite second = FindSite(siteList, road.SecondEndpoint);
                MacroHumanRaster.ToSample(first.Position, water, out int firstX, out int firstY);
                MacroHumanRaster.ToSample(second.Position, water, out int secondX, out int secondY);
                int direct = Math.Max(1, Math.Max(Math.Abs(firstX - secondX), Math.Abs(firstY - secondY)) + 1);
                totalDetour += (long)road.RoutedCellCount * 65535 / direct;
                totalCost += road.TotalTraversalCost;
                totalSteps += Math.Max(1, road.RoutedCellCount - 1);
                AddRoadCells(road, water, roadCells);
            }

            int landCount = Math.Max(1, water.SampleCount - water.OceanSampleCount);
            int coverageQ16 = (int)((long)roadCells.Count * 65535 / landCount);
            int averageDetour = roadList.Count == 0 ? 0 : (int)(totalDetour / roadList.Count);
            long averageCost = totalSteps == 0 ? 0 : totalCost / totalSteps;
            int[] distanceToRoad = BuildDistanceToRoad(water, roadCells);
            int longestGap = 0;
            for (int y = 0; y < water.SampleRows; y++)
            for (int x = 0; x < water.SampleColumns; x++)
            {
                int sample = y * water.SampleColumns + x;
                if (!water.SampleAt(x, y).IsOcean && worldQuality.HasSitePotentialAt(x, y))
                    longestGap = Math.Max(longestGap, distanceToRoad[sample]);
            }
            MacroHumanRaster.ToSample(starterPlacement.Position, water, out int starterX, out int starterY);
            int starterDistance = distanceToRoad[starterY * water.SampleColumns + starterX];
            int independentCycles = primary - regionalCount + regionalComponents.Count;

            var failures = new List<string>();
            var findings = new List<string>();
            if (regionalCount < 2 || localCount < 1) failures.Add("insufficient regional/local hub foundation");
            if (roadList.Count == 0 || primary == 0 || secondary == 0) failures.Add("primary and secondary roads are both required");
            if (regionalCount >= 3 && independentCycles < 1) failures.Add("regional network is only a spanning tree");
            if (starterDistance > 8) failures.Add("starter is pathologically far from the road network");
            if (coverageQ16 > 30000) failures.Add("road density is pathologically excessive");
            if (averageDetour > 260000) failures.Add("road detours are pathologically large");
            if (minimumRegional < Math.Max(2, settings.MinimumRegionalSpacingCells / 2))
                findings.Add("some regional hubs are closer than the preferred spacing after landmass allocation");
            if (coverageQ16 < 1000) findings.Add("road coverage is sparse but valid");
            if (longestGap > Math.Max(24, Math.Max(settings.SampleColumns, settings.SampleRows) / 3))
                findings.Add("some useful land regions remain remote from the sparse V1 road network");
            if (averageDetour > 120000) findings.Add("terrain/water constraints create substantial route detours");
            if (starterDistance > 3) findings.Add("starter reaches the network indirectly rather than at a hub");

            return new MacroHumanGeographyQualityAnalysis(
                minimumRegional, minimumAny, coverageQ16, longestGap,
                averageDetour, averageCost, starterDistance, independentCycles,
                primary, secondary, failures, findings);
        }

        private static void AddRoadCells(MacroRoad road, MacroWaterPlan water, ISet<int> cells)
        {
            for (int point = 1; point < road.Polyline.Count; point++)
            {
                MacroHumanRaster.RasterizeSegment(
                    road.Polyline[point - 1], road.Polyline[point], water,
                    (x, y) => cells.Add(y * water.SampleColumns + x));
            }
        }

        private static int[] BuildDistanceToRoad(MacroWaterPlan water, ISet<int> roadCells)
        {
            int count = water.SampleCount;
            var distances = new int[count];
            for (int index = 0; index < count; index++) distances[index] = int.MaxValue / 4;
            var queue = new Queue<int>();
            foreach (int cell in roadCells) { distances[cell] = 0; queue.Enqueue(cell); }
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int x = current % water.SampleColumns;
                int y = current / water.SampleColumns;
                Visit(x - 1, y); Visit(x + 1, y); Visit(x, y - 1); Visit(x, y + 1);
                void Visit(int nx, int ny)
                {
                    if (nx < 0 || nx >= water.SampleColumns || ny < 0 || ny >= water.SampleRows) return;
                    int next = ny * water.SampleColumns + nx;
                    if (water.SampleAt(nx, ny).IsOcean || distances[next] <= distances[current] + 1) return;
                    distances[next] = distances[current] + 1;
                    queue.Enqueue(next);
                }
            }
            return distances;
        }

        private static MacroHumanSite FindSite(IList<MacroHumanSite> values, MacroHumanSiteId id)
        {
            for (int index = 0; index < values.Count; index++) if (values[index].SiteId == id) return values[index];
            return null;
        }

        private static string CanonicalPair(
            MacroRoadClass roadClass,
            MacroHumanSiteId first,
            MacroHumanSiteId second)
        {
            if (first.CompareTo(second) > 0) { MacroHumanSiteId swap = first; first = second; second = swap; }
            return ((int)roadClass).ToString(CultureInfo.InvariantCulture) + "|" + first.Canonical + "|" + second.Canonical;
        }

        private int CountSites(MacroHumanHubKind kind)
        {
            int count = 0;
            for (int index = 0; index < sites.Count; index++) if (sites[index].Kind == kind) count++;
            return count;
        }

        private int CountRoads(MacroRoadClass roadClass)
        {
            int count = 0;
            for (int index = 0; index < roads.Count; index++) if (roads[index].RoadClass == roadClass) count++;
            return count;
        }

        private string BuildCanonicalHash()
        {
            return WorldCanonicalEncoding.ComputeSha256(stream =>
            {
                WorldCanonicalEncoding.WriteString(stream, CanonicalContract);
                WorldCanonicalEncoding.WriteString(stream, GenerationSettings.GenerationContract);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.SampleColumns);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.SampleRows);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.RegionalHubTarget);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.LocalHubTarget);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.MinimumRegionalSpacingCells);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.MinimumLocalSpacingCells);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.ExtraPrimaryLinkTarget);
                WorldCanonicalEncoding.WriteInt64(stream, WorldBounds.MinX);
                WorldCanonicalEncoding.WriteInt64(stream, WorldBounds.MinY);
                WorldCanonicalEncoding.WriteInt64(stream, WorldBounds.MaxXExclusive);
                WorldCanonicalEncoding.WriteInt64(stream, WorldBounds.MaxYExclusive);
                WorldCanonicalEncoding.WriteString(stream, StarterAccessSectorId.Canonical);
                WorldCanonicalEncoding.WriteInt64(stream, sites.Count);
                for (int index = 0; index < sites.Count; index++)
                {
                    MacroHumanSite site = sites[index];
                    WorldCanonicalEncoding.WriteString(stream, site.SiteId.Canonical);
                    WorldCanonicalEncoding.WriteInt64(stream, (int)site.Kind);
                    WorldCanonicalEncoding.WriteInt64(stream, site.Position.X);
                    WorldCanonicalEncoding.WriteInt64(stream, site.Position.Y);
                    WorldCanonicalEncoding.WriteInt64(stream, site.LandComponentId);
                }
                WorldCanonicalEncoding.WriteInt64(stream, roads.Count);
                for (int index = 0; index < roads.Count; index++)
                {
                    MacroRoad road = roads[index];
                    WorldCanonicalEncoding.WriteString(stream, road.RoadId.Canonical);
                    WorldCanonicalEncoding.WriteInt64(stream, (int)road.RoadClass);
                    WorldCanonicalEncoding.WriteString(stream, road.FirstEndpoint.Canonical);
                    WorldCanonicalEncoding.WriteString(stream, road.SecondEndpoint.Canonical);
                    WorldCanonicalEncoding.WriteInt64(stream, road.RoutedCellCount);
                    WorldCanonicalEncoding.WriteInt64(stream, road.TotalTraversalCost);
                    WorldCanonicalEncoding.WriteInt64(stream, road.Polyline.Count);
                    for (int point = 0; point < road.Polyline.Count; point++)
                    {
                        WorldCanonicalEncoding.WriteInt64(stream, road.Polyline[point].X);
                        WorldCanonicalEncoding.WriteInt64(stream, road.Polyline[point].Y);
                    }
                }
            });
        }
    }

    internal static class MacroHumanRaster
    {
        internal static int[] BuildLandComponents(MacroWaterPlan water, out int[] componentSizes)
        {
            if (water == null) throw new ArgumentNullException(nameof(water));
            var componentByCell = new int[water.SampleCount];
            var sizes = new List<int> { 0 };
            var queue = new Queue<int>();
            int component = 0;
            for (int start = 0; start < water.SampleCount; start++)
            {
                if (componentByCell[start] != 0 ||
                    water.SampleAt(start % water.SampleColumns, start / water.SampleColumns).IsOcean)
                    continue;
                component++;
                int count = 0;
                componentByCell[start] = component;
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    count++;
                    int x = current % water.SampleColumns;
                    int y = current / water.SampleColumns;
                    Visit(x - 1, y);
                    Visit(x + 1, y);
                    Visit(x, y - 1);
                    Visit(x, y + 1);

                    void Visit(int nx, int ny)
                    {
                        if (nx < 0 || nx >= water.SampleColumns || ny < 0 || ny >= water.SampleRows)
                            return;
                        int next = ny * water.SampleColumns + nx;
                        if (componentByCell[next] != 0 || water.SampleAt(nx, ny).IsOcean)
                            return;
                        componentByCell[next] = component;
                        queue.Enqueue(next);
                    }
                }
                sizes.Add(count);
            }
            componentSizes = sizes.ToArray();
            return componentByCell;
        }

        internal static MacroPoint2D PointAt(
            int column,
            int row,
            FiniteMacroWorldBounds bounds,
            int columns,
            int rows)
        {
            long x = bounds.MinX + (bounds.Width - 1) * column / (columns - 1L);
            long y = bounds.MinY + (bounds.Height - 1) * row / (rows - 1L);
            return new MacroPoint2D(x, y);
        }

        internal static void ToSample(MacroPoint2D point, MacroWaterPlan water, out int column, out int row)
        {
            column = Nearest(point.X, water.WorldBounds.MinX, water.WorldBounds.Width, water.SampleColumns);
            row = Nearest(point.Y, water.WorldBounds.MinY, water.WorldBounds.Height, water.SampleRows);
        }

        internal static bool SegmentIsLand(MacroPoint2D first, MacroPoint2D second, MacroWaterPlan water)
        {
            bool land = true;
            RasterizeSegment(first, second, water, (x, y) =>
            {
                if (water.SampleAt(x, y).IsOcean) land = false;
            });
            return land;
        }

        internal static void RasterizeSegment(
            MacroPoint2D first,
            MacroPoint2D second,
            MacroWaterPlan water,
            Action<int, int> visit)
        {
            ToSample(first, water, out int x0, out int y0);
            ToSample(second, water, out int x1, out int y1);
            int dx = Math.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            while (true)
            {
                visit(x0, y0);
                if (x0 == x1 && y0 == y1) break;
                int twice = error * 2;
                if (twice >= dy) { error += dy; x0 += sx; }
                if (twice <= dx) { error += dx; y0 += sy; }
            }
        }

        private static int Nearest(long coordinate, long minimum, long extent, int count)
        {
            long numerator = (coordinate - minimum) * (count - 1L);
            long denominator = extent - 1;
            return (int)Math.Max(0, Math.Min(count - 1L,
                (numerator * 2 + denominator) / (denominator * 2)));
        }
    }
}
