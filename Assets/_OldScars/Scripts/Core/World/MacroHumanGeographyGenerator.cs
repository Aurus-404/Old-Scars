using System;
using System.Collections.Generic;
using System.Globalization;

namespace OldScars.Core.World
{
    /// <summary>
    /// Generation-time producer for the first global human layer. It consumes
    /// committed macro truth and never reads WorldTopology edges or sector borders.
    /// </summary>
    public static class MacroHumanGeographyGenerator
    {
        public const string DeterministicGenerationContract = "macro_human_roads_v1";
        private const int Impassable = int.MaxValue;

        public static bool TryGenerate(
            WorldGenerationContext context,
            MacroWorldPlan worldPlan,
            MacroGeographyPlan geography,
            MacroWaterPlan water,
            WorldGameplayQualityAnalysis worldQuality,
            SectorId starterSector,
            out MacroHumanGeographyPlan humanPlan,
            out string error)
        {
            humanPlan = null;
            error = null;
            if (context == null || worldPlan == null || geography == null || water == null || worldQuality == null)
            {
                error = "Macro Human Geography requires context and committed plan/geography/water/quality";
                return false;
            }
            if (!worldQuality.MeetsHardRequirements ||
                worldPlan.WorldBounds != geography.WorldBounds || geography.WorldBounds != water.WorldBounds ||
                geography.SampleColumns != water.SampleColumns || geography.SampleRows != water.SampleRows)
            {
                error = "Macro Human Geography inputs are invalid or disagree on bounds/grid";
                return false;
            }
            if (!worldPlan.TryGetSectorPlacement(starterSector, out MacroSectorPlacement starterPlacement))
            {
                error = "Starter sector is absent from MacroWorldPlan";
                return false;
            }

            MacroHumanGeographyGenerationSettings settings = MacroHumanGeographyGenerationSettings.Resolve(
                worldPlan.GenerationSettings.WorldSizePreset, geography.SampleColumns, geography.SampleRows);
            int[] componentByCell = MacroHumanRaster.BuildLandComponents(water, out int[] componentSizes);
            List<AnchorCandidate> candidates = BuildAnchorCandidates(
                context.WorldSeed, settings, geography, water, worldQuality, componentByCell);
            MacroHumanRaster.ToSample(starterPlacement.Position, water, out int starterX, out int starterY);
            int starterCell = starterY * water.SampleColumns + starterX;
            int starterComponent = componentByCell[starterCell];
            if (starterComponent < 1)
            {
                error = "Starter sector is not on a routed land component";
                return false;
            }

            List<AnchorCandidate> regional = SelectRegionalAnchors(
                candidates, componentSizes, starterComponent, settings);
            if (regional.Count < 2)
            {
                error = "Fewer than two deterministic RegionalHub anchors could be selected";
                return false;
            }
            List<AnchorCandidate> local = SelectLocalAnchors(
                candidates, regional, starterCell, starterComponent, settings);
            if (local.Count < 1)
            {
                error = "No deterministic LocalHub anchor could be selected";
                return false;
            }

            var sites = new List<MacroHumanSite>(regional.Count + local.Count);
            AddSites(regional, MacroHumanHubKind.RegionalHub, context.WorldSeed, settings,
                geography.WorldBounds, geography.SampleColumns, geography.SampleRows, sites);
            AddSites(local, MacroHumanHubKind.LocalHub, context.WorldSeed, settings,
                geography.WorldBounds, geography.SampleColumns, geography.SampleRows, sites);

            int[] traversalCosts = BuildTraversalCosts(geography, water, worldQuality);
            if (!TryBuildRoadNetwork(
                    context.WorldSeed, settings, water, sites, traversalCosts,
                    out List<MacroRoad> roads, out error))
                return false;

            return MacroHumanGeographyPlan.TryCreate(
                settings, worldPlan, geography, water, worldQuality, starterSector,
                sites, roads, out humanPlan, out error);
        }

        public static int EvaluateTraversalCost(
            MacroGeographyPlan geography,
            MacroWaterPlan water,
            WorldGameplayQualityAnalysis quality,
            int column,
            int row)
        {
            if (geography == null || water == null || quality == null)
                throw new ArgumentNullException("Road cost inputs are required.");
            if (water.SampleAt(column, row).IsOcean) return Impassable;
            int cost;
            switch (geography.LandformSampleAt(column, row))
            {
                case MacroLandform.Plains: cost = 100; break;
                case MacroLandform.RollingHills: cost = 165; break;
                case MacroLandform.Highlands: cost = 320; break;
                case MacroLandform.Mountains: cost = 950; break;
                default: throw new InvalidOperationException("Unknown landform in road cost field.");
            }
            int gradient = quality.GradientSampleAt(column, row);
            int relief = quality.LocalReliefSampleAt(column, row);
            cost += gradient / 24 + relief / 48;
            if (gradient > WorldGameplayQualityCriteria.MaximumTraversalGradient * 2)
                cost += 3500;
            else if (gradient > WorldGameplayQualityCriteria.MaximumTraversalGradient)
                cost += 900;
            if (quality.HasTraversalPotentialAt(column, row)) cost = Math.Max(80, cost - 35);
            return cost;
        }

        private static List<AnchorCandidate> BuildAnchorCandidates(
            WorldSeed seed,
            MacroHumanGeographyGenerationSettings settings,
            MacroGeographyPlan geography,
            MacroWaterPlan water,
            WorldGameplayQualityAnalysis quality,
            int[] componentByCell)
        {
            ulong tieSeed = NumericSeed(WorldDeterminism.DerivePassDomainKey(
                seed, DeterministicGenerationContract, settings.DeterministicKey, "anchor_ranking"));
            var candidates = new List<AnchorCandidate>();
            for (int y = 0; y < water.SampleRows; y++)
            for (int x = 0; x < water.SampleColumns; x++)
            {
                int index = y * water.SampleColumns + x;
                if (componentByCell[index] < 1 || !quality.HasSitePotentialAt(x, y)) continue;
                int coastDistance = DistanceToCoast(water, x, y, 5);
                long suitability = 1_000_000L;
                suitability += quality.HasTraversalPotentialAt(x, y) ? 200_000L : 0L;
                suitability += geography.LandformSampleAt(x, y) == MacroLandform.Plains ? 80_000L : 30_000L;
                suitability += coastDistance <= 3 ? 30_000L - coastDistance * 7_500L : 0L;
                suitability -= quality.GradientSampleAt(x, y) * 16L;
                suitability -= quality.LocalReliefSampleAt(x, y) * 6L;
                candidates.Add(new AnchorCandidate(
                    index, x, y, componentByCell[index], suitability,
                    StableTie(tieSeed, index)));
            }
            candidates.Sort(CompareCandidateCanonical);
            return candidates;
        }

        private static List<AnchorCandidate> SelectRegionalAnchors(
            IList<AnchorCandidate> candidates,
            int[] componentSizes,
            int starterComponent,
            MacroHumanGeographyGenerationSettings settings)
        {
            var selected = new List<AnchorCandidate>();
            var selectedCells = new HashSet<int>();
            int landCount = 0;
            for (int index = 1; index < componentSizes.Length; index++) landCount += componentSizes[index];
            int significant = Math.Max(8, landCount / 80);

            AddBestInComponent(starterComponent);
            AddBestInComponent(starterComponent);
            AddBestInComponent(starterComponent);
            var components = new List<int>();
            for (int component = 1; component < componentSizes.Length; component++)
                if (component != starterComponent && componentSizes[component] >= significant)
                    components.Add(component);
            components.Sort((left, right) =>
            {
                int size = componentSizes[right].CompareTo(componentSizes[left]);
                return size != 0 ? size : left.CompareTo(right);
            });
            for (int index = 0; index < components.Count && selected.Count < settings.RegionalHubTarget; index++)
                AddBestInComponent(components[index]);

            FillMaximin(candidates, selected, selectedCells, settings.RegionalHubTarget,
                settings.MinimumRegionalSpacingCells, settings.SampleColumns, null);
            if (selected.Count < settings.RegionalHubTarget)
                FillMaximin(candidates, selected, selectedCells, settings.RegionalHubTarget,
                    2, settings.SampleColumns, null);
            return selected;

            void AddBestInComponent(int component)
            {
                AnchorCandidate best = null;
                long bestScore = long.MinValue;
                for (int index = 0; index < candidates.Count; index++)
                {
                    AnchorCandidate candidate = candidates[index];
                    if (candidate.Component != component || selectedCells.Contains(candidate.Cell)) continue;
                    int minimumDistance = int.MaxValue;
                    for (int selectedIndex = 0; selectedIndex < selected.Count; selectedIndex++)
                    {
                        AnchorCandidate value = selected[selectedIndex];
                        if (value.Component != component) continue;
                        minimumDistance = Math.Min(minimumDistance,
                            Math.Max(Math.Abs(candidate.X - value.X), Math.Abs(candidate.Y - value.Y)));
                    }
                    if (minimumDistance != int.MaxValue &&
                        minimumDistance < settings.MinimumRegionalSpacingCells) continue;
                    long distanceScore = minimumDistance == int.MaxValue
                        ? 0L
                        : (long)minimumDistance * minimumDistance * 10_000_000L;
                    long score = distanceScore + candidate.Suitability * 100L +
                                 (long)(uint.MaxValue - candidate.Tie);
                    if (best == null || score > bestScore ||
                        score == bestScore && candidate.Cell < best.Cell)
                    {
                        best = candidate;
                        bestScore = score;
                    }
                }
                if (best != null)
                {
                    selected.Add(best);
                    selectedCells.Add(best.Cell);
                }
            }
        }

        private static List<AnchorCandidate> SelectLocalAnchors(
            IList<AnchorCandidate> candidates,
            IList<AnchorCandidate> regional,
            int starterCell,
            int starterComponent,
            MacroHumanGeographyGenerationSettings settings)
        {
            var components = new HashSet<int>();
            var occupied = new HashSet<int>();
            for (int index = 0; index < regional.Count; index++)
            {
                components.Add(regional[index].Component);
                occupied.Add(regional[index].Cell);
            }
            var selected = new List<AnchorCandidate>();
            AnchorCandidate nearStarter = null;
            int starterX = starterCell % settings.SampleColumns;
            int starterY = starterCell / settings.SampleColumns;
            for (int index = 0; index < candidates.Count; index++)
            {
                AnchorCandidate candidate = candidates[index];
                if (candidate.Component != starterComponent || occupied.Contains(candidate.Cell)) continue;
                int distance = Math.Max(Math.Abs(candidate.X - starterX), Math.Abs(candidate.Y - starterY));
                if (nearStarter == null || distance < Math.Max(
                        Math.Abs(nearStarter.X - starterX), Math.Abs(nearStarter.Y - starterY)) ||
                    distance == Math.Max(Math.Abs(nearStarter.X - starterX), Math.Abs(nearStarter.Y - starterY)) &&
                    CompareCandidateCanonical(candidate, nearStarter) < 0)
                    nearStarter = candidate;
            }
            if (nearStarter != null)
            {
                selected.Add(nearStarter);
                occupied.Add(nearStarter.Cell);
            }
            FillMaximin(candidates, selected, occupied, settings.LocalHubTarget,
                settings.MinimumLocalSpacingCells, settings.SampleColumns, components);
            if (selected.Count < settings.LocalHubTarget)
                FillMaximin(candidates, selected, occupied, settings.LocalHubTarget,
                    1, settings.SampleColumns, components);
            return selected;
        }

        private static void FillMaximin(
            IList<AnchorCandidate> candidates,
            IList<AnchorCandidate> selected,
            ISet<int> occupied,
            int target,
            int minimumSpacing,
            int columns,
            ISet<int> allowedComponents)
        {
            while (selected.Count < target)
            {
                AnchorCandidate best = null;
                long bestScore = long.MinValue;
                for (int index = 0; index < candidates.Count; index++)
                {
                    AnchorCandidate candidate = candidates[index];
                    if (occupied.Contains(candidate.Cell) ||
                        allowedComponents != null && !allowedComponents.Contains(candidate.Component)) continue;
                    int minimumDistance = int.MaxValue;
                    foreach (int occupiedCell in occupied)
                    {
                        int ox = occupiedCell % columns;
                        int oy = occupiedCell / columns;
                        minimumDistance = Math.Min(minimumDistance,
                            Math.Max(Math.Abs(candidate.X - ox), Math.Abs(candidate.Y - oy)));
                    }
                    if (minimumDistance == int.MaxValue) minimumDistance = minimumSpacing;
                    if (minimumDistance < minimumSpacing) continue;
                    long score = (long)minimumDistance * minimumDistance * 10_000_000L +
                                 candidate.Suitability * 100L + (long)(uint.MaxValue - candidate.Tie);
                    if (best == null || score > bestScore || score == bestScore && candidate.Cell < best.Cell)
                    {
                        best = candidate;
                        bestScore = score;
                    }
                }
                if (best == null) break;
                selected.Add(best);
                occupied.Add(best.Cell);
            }
        }

        private static void AddSites(
            IList<AnchorCandidate> candidates,
            MacroHumanHubKind kind,
            WorldSeed seed,
            MacroHumanGeographyGenerationSettings settings,
            FiniteMacroWorldBounds bounds,
            int columns,
            int rows,
            IList<MacroHumanSite> sites)
        {
            for (int index = 0; index < candidates.Count; index++)
            {
                AnchorCandidate candidate = candidates[index];
                string pass = (kind == MacroHumanHubKind.RegionalHub ? "regional_" : "local_") +
                              candidate.Cell.ToString("D6", CultureInfo.InvariantCulture);
                MacroHumanSiteId id = MacroHumanSiteId.FromDeterministicDomain(
                    WorldDeterminism.DerivePassDomainKey(
                        seed, DeterministicGenerationContract, settings.DeterministicKey, pass));
                var site = new MacroHumanSite(
                    id, kind, MacroHumanRaster.PointAt(candidate.X, candidate.Y, bounds, columns, rows),
                    candidate.Component);
                sites.Add(site);
            }
        }

        private static int[] BuildTraversalCosts(
            MacroGeographyPlan geography,
            MacroWaterPlan water,
            WorldGameplayQualityAnalysis quality)
        {
            var costs = new int[water.SampleCount];
            for (int y = 0; y < water.SampleRows; y++)
            for (int x = 0; x < water.SampleColumns; x++)
                costs[y * water.SampleColumns + x] = EvaluateTraversalCost(geography, water, quality, x, y);
            return costs;
        }

        private static bool TryBuildRoadNetwork(
            WorldSeed seed,
            MacroHumanGeographyGenerationSettings settings,
            MacroWaterPlan water,
            IList<MacroHumanSite> sites,
            int[] traversalCosts,
            out List<MacroRoad> roads,
            out string error)
        {
            roads = new List<MacroRoad>();
            error = null;
            var regionalByComponent = GroupSites(sites, MacroHumanHubKind.RegionalHub);
            var localByComponent = GroupSites(sites, MacroHumanHubKind.LocalHub);
            var existing = new HashSet<string>(StringComparer.Ordinal);
            var extraCandidates = new List<SitePair>();

            var regionalComponents = new List<int>(regionalByComponent.Keys);
            regionalComponents.Sort();
            for (int componentIndex = 0; componentIndex < regionalComponents.Count; componentIndex++)
            {
                List<MacroHumanSite> hubs = regionalByComponent[regionalComponents[componentIndex]];
                hubs.Sort((left, right) => left.SiteId.CompareTo(right.SiteId));
                List<SitePair> tree = BuildSpatialTree(hubs, water);
                for (int index = 0; index < tree.Count; index++)
                {
                    SitePair pair = tree[index];
                    existing.Add(PairKey(pair.First.SiteId, pair.Second.SiteId));
                    if (!TryCreateRoad(seed, settings, water, traversalCosts, MacroRoadClass.Primary,
                            pair.First, pair.Second, out MacroRoad road, out error)) return false;
                    roads.Add(road);
                }
                for (int first = 0; first < hubs.Count; first++)
                for (int second = first + 1; second < hubs.Count; second++)
                {
                    string key = PairKey(hubs[first].SiteId, hubs[second].SiteId);
                    if (!existing.Contains(key)) extraCandidates.Add(new SitePair(
                        hubs[first], hubs[second], SampleDistanceSquared(hubs[first], hubs[second], water)));
                }
            }
            extraCandidates.Sort(ComparePairs);
            int extras = Math.Min(settings.ExtraPrimaryLinkTarget, extraCandidates.Count);
            for (int index = 0; index < extras; index++)
            {
                SitePair pair = extraCandidates[index];
                existing.Add(PairKey(pair.First.SiteId, pair.Second.SiteId));
                if (!TryCreateRoad(seed, settings, water, traversalCosts, MacroRoadClass.Primary,
                        pair.First, pair.Second, out MacroRoad road, out error)) return false;
                roads.Add(road);
            }

            var localComponents = new List<int>(localByComponent.Keys);
            localComponents.Sort();
            for (int componentIndex = 0; componentIndex < localComponents.Count; componentIndex++)
            {
                int component = localComponents[componentIndex];
                List<MacroHumanSite> localSites = localByComponent[component];
                if (!regionalByComponent.TryGetValue(component, out List<MacroHumanSite> regional) || regional.Count == 0)
                {
                    error = "LocalHub land component lacks a RegionalHub";
                    return false;
                }
                localSites.Sort((left, right) => left.SiteId.CompareTo(right.SiteId));
                for (int index = 0; index < localSites.Count; index++)
                {
                    MacroHumanSite local = localSites[index];
                    MacroHumanSite nearest = regional[0];
                    long nearestDistance = SampleDistanceSquared(local, nearest, water);
                    for (int regionalIndex = 1; regionalIndex < regional.Count; regionalIndex++)
                    {
                        long distance = SampleDistanceSquared(local, regional[regionalIndex], water);
                        if (distance < nearestDistance ||
                            distance == nearestDistance && regional[regionalIndex].SiteId.CompareTo(nearest.SiteId) < 0)
                        {
                            nearest = regional[regionalIndex];
                            nearestDistance = distance;
                        }
                    }
                    if (!TryCreateRoad(seed, settings, water, traversalCosts, MacroRoadClass.Secondary,
                            local, nearest, out MacroRoad road, out error)) return false;
                    roads.Add(road);
                }
            }
            return true;
        }

        private static Dictionary<int, List<MacroHumanSite>> GroupSites(
            IList<MacroHumanSite> sites,
            MacroHumanHubKind kind)
        {
            var groups = new Dictionary<int, List<MacroHumanSite>>();
            for (int index = 0; index < sites.Count; index++)
            {
                MacroHumanSite site = sites[index];
                if (site.Kind != kind) continue;
                if (!groups.TryGetValue(site.LandComponentId, out List<MacroHumanSite> list))
                {
                    list = new List<MacroHumanSite>();
                    groups.Add(site.LandComponentId, list);
                }
                list.Add(site);
            }
            return groups;
        }

        private static List<SitePair> BuildSpatialTree(IList<MacroHumanSite> hubs, MacroWaterPlan water)
        {
            var result = new List<SitePair>();
            if (hubs.Count < 2) return result;
            var connected = new HashSet<MacroHumanSiteId> { hubs[0].SiteId };
            while (connected.Count < hubs.Count)
            {
                SitePair best = null;
                for (int first = 0; first < hubs.Count; first++)
                {
                    if (!connected.Contains(hubs[first].SiteId)) continue;
                    for (int second = 0; second < hubs.Count; second++)
                    {
                        if (connected.Contains(hubs[second].SiteId)) continue;
                        var candidate = new SitePair(
                            hubs[first], hubs[second], SampleDistanceSquared(hubs[first], hubs[second], water));
                        if (best == null || ComparePairs(candidate, best) < 0) best = candidate;
                    }
                }
                if (best == null) throw new InvalidOperationException("Regional spatial tree could not connect its hubs.");
                result.Add(best);
                connected.Add(connected.Contains(best.First.SiteId) ? best.Second.SiteId : best.First.SiteId);
            }
            return result;
        }

        private static bool TryCreateRoad(
            WorldSeed seed,
            MacroHumanGeographyGenerationSettings settings,
            MacroWaterPlan water,
            int[] costs,
            MacroRoadClass roadClass,
            MacroHumanSite first,
            MacroHumanSite second,
            out MacroRoad road,
            out string error)
        {
            road = null;
            error = null;
            if (first.SiteId.CompareTo(second.SiteId) > 0)
            {
                MacroHumanSite swap = first; first = second; second = swap;
            }
            MacroHumanRaster.ToSample(first.Position, water, out int startX, out int startY);
            MacroHumanRaster.ToSample(second.Position, water, out int goalX, out int goalY);
            if (!TryRoute(costs, water.SampleColumns, water.SampleRows,
                    startY * water.SampleColumns + startX,
                    goalY * water.SampleColumns + goalX,
                    out List<int> cells, out long totalCost))
            {
                error = "No land route exists between hubs '" + first.SiteId.Canonical + "' and '" +
                        second.SiteId.Canonical + "'";
                return false;
            }
            List<MacroPoint2D> polyline = SimplifyCollinear(cells, water);
            string pass = "road_" + (roadClass == MacroRoadClass.Primary ? "primary_" : "secondary_") +
                          first.SiteId.Canonical.Substring("human_site_".Length) + "_" +
                          second.SiteId.Canonical.Substring("human_site_".Length);
            MacroRoadId id = MacroRoadId.FromDeterministicDomain(
                WorldDeterminism.DerivePassDomainKey(
                    seed, DeterministicGenerationContract, settings.DeterministicKey, pass));
            road = new MacroRoad(
                id, roadClass, first.SiteId, second.SiteId, polyline, cells.Count, totalCost);
            return true;
        }

        private static bool TryRoute(
            int[] costs,
            int columns,
            int rows,
            int start,
            int goal,
            out List<int> path,
            out long totalCost)
        {
            path = null;
            totalCost = 0;
            var g = new long[costs.Length];
            var previous = new int[costs.Length];
            for (int index = 0; index < g.Length; index++) { g[index] = long.MaxValue; previous[index] = -1; }
            var open = new RouteHeap();
            g[start] = 0;
            open.Push(new RouteNode(start, Heuristic(start, goal, columns), 0));
            int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };
            while (open.Count > 0)
            {
                RouteNode current = open.Pop();
                if (current.G != g[current.Cell]) continue;
                if (current.Cell == goal) break;
                int x = current.Cell % columns;
                int y = current.Cell / columns;
                for (int direction = 0; direction < dx.Length; direction++)
                {
                    int nx = x + dx[direction];
                    int ny = y + dy[direction];
                    if (nx < 0 || nx >= columns || ny < 0 || ny >= rows) continue;
                    int next = ny * columns + nx;
                    if (costs[next] == Impassable) continue;
                    bool diagonal = dx[direction] != 0 && dy[direction] != 0;
                    if (diagonal && (costs[y * columns + nx] == Impassable ||
                                     costs[ny * columns + x] == Impassable)) continue;
                    long step = (long)costs[next] * (diagonal ? 141 : 100) / 100;
                    long candidate = current.G + step;
                    if (candidate > g[next] || candidate == g[next] && previous[next] <= current.Cell) continue;
                    g[next] = candidate;
                    previous[next] = current.Cell;
                    open.Push(new RouteNode(next, candidate + Heuristic(next, goal, columns), candidate));
                }
            }
            if (g[goal] == long.MaxValue) return false;
            var reversed = new List<int>();
            int cursor = goal;
            while (cursor >= 0)
            {
                reversed.Add(cursor);
                if (cursor == start) break;
                cursor = previous[cursor];
            }
            if (reversed[reversed.Count - 1] != start) return false;
            reversed.Reverse();
            path = reversed;
            totalCost = g[goal];
            return true;
        }

        private static long Heuristic(int cell, int goal, int columns)
        {
            int x = cell % columns;
            int y = cell / columns;
            int gx = goal % columns;
            int gy = goal / columns;
            return Math.Max(Math.Abs(x - gx), Math.Abs(y - gy)) * 80L;
        }

        private static List<MacroPoint2D> SimplifyCollinear(IList<int> cells, MacroWaterPlan water)
        {
            var points = new List<MacroPoint2D>();
            if (cells.Count == 0) return points;
            Add(cells[0]);
            int previousDx = 0;
            int previousDy = 0;
            for (int index = 1; index < cells.Count; index++)
            {
                int previous = cells[index - 1];
                int dx = cells[index] % water.SampleColumns - previous % water.SampleColumns;
                int dy = cells[index] / water.SampleColumns - previous / water.SampleColumns;
                if (index > 1 && (dx != previousDx || dy != previousDy)) Add(previous);
                previousDx = dx;
                previousDy = dy;
            }
            Add(cells[cells.Count - 1]);
            return points;

            void Add(int cell)
            {
                MacroPoint2D point = MacroHumanRaster.PointAt(
                    cell % water.SampleColumns, cell / water.SampleColumns,
                    water.WorldBounds, water.SampleColumns, water.SampleRows);
                if (points.Count == 0 || points[points.Count - 1] != point) points.Add(point);
            }
        }

        private static int DistanceToCoast(MacroWaterPlan water, int x, int y, int maximum)
        {
            for (int distance = 0; distance <= maximum; distance++)
            {
                for (int offsetY = -distance; offsetY <= distance; offsetY++)
                for (int offsetX = -distance; offsetX <= distance; offsetX++)
                {
                    if (Math.Max(Math.Abs(offsetX), Math.Abs(offsetY)) != distance) continue;
                    int sampleX = x + offsetX;
                    int sampleY = y + offsetY;
                    if (sampleX >= 0 && sampleX < water.SampleColumns && sampleY >= 0 && sampleY < water.SampleRows &&
                        water.SampleAt(sampleX, sampleY).IsCoastline) return distance;
                }
            }
            return maximum + 1;
        }

        private static long SampleDistanceSquared(MacroHumanSite first, MacroHumanSite second, MacroWaterPlan water)
        {
            MacroHumanRaster.ToSample(first.Position, water, out int firstX, out int firstY);
            MacroHumanRaster.ToSample(second.Position, water, out int secondX, out int secondY);
            long dx = firstX - secondX;
            long dy = firstY - secondY;
            return dx * dx + dy * dy;
        }

        private static string PairKey(MacroHumanSiteId first, MacroHumanSiteId second)
        {
            if (first.CompareTo(second) > 0) { MacroHumanSiteId swap = first; first = second; second = swap; }
            return first.Canonical + "|" + second.Canonical;
        }

        private static int ComparePairs(SitePair left, SitePair right)
        {
            int distance = left.DistanceSquared.CompareTo(right.DistanceSquared);
            if (distance != 0) return distance;
            int first = left.First.SiteId.CompareTo(right.First.SiteId);
            return first != 0 ? first : left.Second.SiteId.CompareTo(right.Second.SiteId);
        }

        private static int CompareCandidateCanonical(AnchorCandidate left, AnchorCandidate right)
        {
            int component = left.Component.CompareTo(right.Component);
            if (component != 0) return component;
            int suitability = right.Suitability.CompareTo(left.Suitability);
            if (suitability != 0) return suitability;
            int tie = left.Tie.CompareTo(right.Tie);
            return tie != 0 ? tie : left.Cell.CompareTo(right.Cell);
        }

        private static ulong NumericSeed(DeterministicDomainKey domain)
        {
            ulong value = 0;
            for (int index = 0; index < 16; index++)
            {
                char character = domain.Canonical[index];
                value = (value << 4) | (uint)(character <= '9' ? character - '0' : character - 'a' + 10);
            }
            return value;
        }

        private static uint StableTie(ulong seed, int value)
        {
            unchecked
            {
                ulong mixed = seed ^ ((ulong)(uint)value * 0x9e3779b185ebca87UL);
                mixed = (mixed ^ (mixed >> 30)) * 0xbf58476d1ce4e5b9UL;
                mixed = (mixed ^ (mixed >> 27)) * 0x94d049bb133111ebUL;
                return (uint)(mixed ^ (mixed >> 31));
            }
        }

        private sealed class AnchorCandidate
        {
            internal AnchorCandidate(int cell, int x, int y, int component, long suitability, uint tie)
            {
                Cell = cell; X = x; Y = y; Component = component; Suitability = suitability; Tie = tie;
            }
            internal int Cell { get; }
            internal int X { get; }
            internal int Y { get; }
            internal int Component { get; }
            internal long Suitability { get; }
            internal uint Tie { get; }
        }

        private sealed class SitePair
        {
            internal SitePair(MacroHumanSite first, MacroHumanSite second, long distanceSquared)
            {
                if (first.SiteId.CompareTo(second.SiteId) <= 0) { First = first; Second = second; }
                else { First = second; Second = first; }
                DistanceSquared = distanceSquared;
            }
            internal MacroHumanSite First { get; }
            internal MacroHumanSite Second { get; }
            internal long DistanceSquared { get; }
        }

        private readonly struct RouteNode
        {
            internal RouteNode(int cell, long f, long g) { Cell = cell; F = f; G = g; }
            internal int Cell { get; }
            internal long F { get; }
            internal long G { get; }
        }

        private sealed class RouteHeap
        {
            private readonly List<RouteNode> values = new List<RouteNode>();
            internal int Count => values.Count;

            internal void Push(RouteNode node)
            {
                values.Add(node);
                int index = values.Count - 1;
                while (index > 0)
                {
                    int parent = (index - 1) / 2;
                    if (Compare(values[parent], node) <= 0) break;
                    values[index] = values[parent];
                    index = parent;
                }
                values[index] = node;
            }

            internal RouteNode Pop()
            {
                RouteNode result = values[0];
                RouteNode tail = values[values.Count - 1];
                values.RemoveAt(values.Count - 1);
                if (values.Count == 0) return result;
                int index = 0;
                while (true)
                {
                    int left = index * 2 + 1;
                    if (left >= values.Count) break;
                    int right = left + 1;
                    int child = right < values.Count && Compare(values[right], values[left]) < 0 ? right : left;
                    if (Compare(tail, values[child]) <= 0) break;
                    values[index] = values[child];
                    index = child;
                }
                values[index] = tail;
                return result;
            }

            private static int Compare(RouteNode left, RouteNode right)
            {
                int f = left.F.CompareTo(right.F);
                if (f != 0) return f;
                int g = left.G.CompareTo(right.G);
                return g != 0 ? g : left.Cell.CompareTo(right.Cell);
            }
        }
    }
}
