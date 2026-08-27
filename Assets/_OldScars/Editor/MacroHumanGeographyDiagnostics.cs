using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OldScars.Core.Data.Loading;
using OldScars.Core.Data.Validation;
using OldScars.Core.Persistence;
using OldScars.Core.World;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace OldScars.EditorTools
{
    public static class MacroHumanGeographyDiagnostics
    {
        private const long GoldenSeed = 8675309123456789L;
        private const string GoldenPlanHash =
            "3f300ba2129962493d2ab8f2ad6ec0863e96aa0ceeb400f9899f91889a34e91a";
        private const string GoldenGeographyHash =
            "c2d412fcdcb1b0e1b41f4fdbda2df01258758e6db9c6b93aac59b446be7dbd3e";
        private const string GoldenWaterHash =
            "ec29f501e4f36ae3b2313d3da6089f2fe6e92b052f18079c649e21ce8faabfc0";
        private const string GoldenHumanHash =
            "a786f018ce3bdea44aeb066c80e38cb1f5dc8e114c65bd7eb352489628245ba6";
        private const int RoutineSeedsPerCombination = 3;
        private const int StressSeedsPerCombination = 12;

        public static void Run()
        {
            var failures = new List<string>();
            var measurements = new List<string>();
            string root = Path.Combine(
                Path.GetTempPath(), "OldScars_MacroHumanGeography_" + Guid.NewGuid().ToString("N"));
            try
            {
                ValidateDeterminismIsolationAndGolden(failures);
                ValidateNetworkContract(failures);
                ValidateRoutineCorpus(failures);
                LoadedContentSet content = LoadValidatedCore(failures);
                ValidatePersistence(root, content, failures);
                MeasurePresets(content, measurements, failures);
            }
            catch (Exception exception)
            {
                failures.Add("Diagnostic threw " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                WorldSessionService.Close();
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }

            Complete("Macro Human Geography / Road Network V1 Diagnostics", failures,
                "- deterministic world-first regional/local hubs and terrain-cost road geometry",
                "- upstream Plan/Geography/Water goldens unchanged; WorldId and pipeline-version independent",
                "- landmass-local primary backbone plus redundant links and secondary branches",
                "- all hubs/geometry on global land; no sector-local generation or ocean crossing",
                "- canonical insertion-order-independent Human Geography evidence",
                "- routine corpus: " + RoutineSeedsPerCombination + " seeds x 4 sizes x 3 coverages",
                "- current-schema exact Human Geography round-trip and corruption preflight",
                "- timings / serialized payload sizes: " + string.Join("; ", measurements));
        }

        public static void RunStress()
        {
            var failures = new List<string>();
            int generated = 0;
            int hardRejected = 0;
            int softWorlds = 0;
            var stopwatch = Stopwatch.StartNew();
            foreach (WorldSizePreset size in Enum.GetValues(typeof(WorldSizePreset)))
            foreach (LandCoveragePreset coverage in Enum.GetValues(typeof(LandCoveragePreset)))
            for (int index = 0; index < StressSeedsPerCombination; index++)
            {
                long seed = unchecked((long)0xd67199f143583b21UL +
                                      (long)size * 1000003L +
                                      (long)coverage * 65537L + index * 104729L);
                if (!TryGenerate(seed, size, coverage, out GeneratedWorld world, out string error))
                {
                    hardRejected++;
                    failures.Add(size + "/" + coverage + "/" + index + ": " + error);
                    continue;
                }
                generated++;
                if (world.Human.Quality.SoftFindings.Count > 0) softWorlds++;
                ValidateBasic(world, size + "/" + coverage + "/" + index, failures);
            }
            stopwatch.Stop();
            if (hardRejected > 0)
                failures.Add("Stress corpus had " + hardRejected + " generation rejection(s)." );
            Complete("Macro Human Geography Stress Corpus", failures,
                "- attempted: " + (StressSeedsPerCombination * 12),
                "- generated: " + generated,
                "- hard rejected: " + hardRejected,
                "- worlds with soft findings: " + softWorlds,
                "- elapsed ms: " + stopwatch.ElapsedMilliseconds);
        }

        private static void ValidateDeterminismIsolationAndGolden(List<string> failures)
        {
            Check(TryGenerate(GoldenSeed, WorldSizePreset.Large, LandCoveragePreset.High,
                      out GeneratedWorld first, out string firstError),
                "Golden generation failed: " + Safe(firstError), failures);
            Check(TryGenerate(GoldenSeed, WorldSizePreset.Large, LandCoveragePreset.High,
                      out GeneratedWorld repeated, out string repeatedError),
                "Repeated golden generation failed: " + Safe(repeatedError), failures);
            if (first == null || repeated == null) return;

            Check(first.Plan.CanonicalHash == repeated.Plan.CanonicalHash &&
                  first.Geography.CanonicalHash == repeated.Geography.CanonicalHash &&
                  first.Water.CanonicalHash == repeated.Water.CanonicalHash &&
                  first.Human.CanonicalHash == repeated.Human.CanonicalHash,
                "A. Same seed/settings/pass contracts must produce exact repeated evidence.", failures);
            Check(first.Plan.CanonicalHash == GoldenPlanHash,
                "Previous MacroWorldPlan golden drifted: " + first.Plan.CanonicalHash, failures);
            Check(first.Geography.CanonicalHash == GoldenGeographyHash,
                "Previous MacroGeography golden drifted: " + first.Geography.CanonicalHash, failures);
            Check(first.Water.CanonicalHash == GoldenWaterHash,
                "Previous MacroWater golden drifted: " + first.Water.CanonicalHash, failures);
            Check(first.Human.CanonicalHash == GoldenHumanHash,
                "Human Geography golden is " + first.Human.CanonicalHash +
                " (expected " + GoldenHumanHash + ").", failures);

            var alternateContext = new WorldGenerationContext(
                new WorldSeed(GoldenSeed), GeneratorVersion.Parse("synthetic_future_pipeline_v99"));
            Check(MacroHumanGeographyGenerator.TryGenerate(
                      alternateContext, first.Plan, first.Geography, first.Water, first.Quality, first.Starter,
                      out MacroHumanGeographyPlan alternateHuman, out string alternateError) &&
                  alternateHuman.CanonicalHash == first.Human.CanonicalHash,
                "Pass-local contract must isolate Human Geography from overall pipeline metadata: " +
                Safe(alternateError), failures);

            Check(TryGenerate(GoldenSeed + 1, WorldSizePreset.Large, LandCoveragePreset.High,
                      out GeneratedWorld differentSeed, out string differentError) &&
                  differentSeed.Human.CanonicalHash != first.Human.CanonicalHash,
                "Changed WorldSeed must change Human Geography: " + Safe(differentError), failures);

            var reversedSites = new List<MacroHumanSite>(first.Human.Sites);
            var reversedRoads = new List<MacroRoad>(first.Human.Roads);
            reversedSites.Reverse();
            reversedRoads.Reverse();
            Check(MacroHumanGeographyPlan.TryCreate(
                      first.Human.GenerationSettings, first.Plan, first.Geography, first.Water,
                      first.Quality, first.Starter, reversedSites, reversedRoads,
                      out MacroHumanGeographyPlan reversed, out string reversedError) &&
                  reversed.CanonicalHash == first.Human.CanonicalHash,
                "Canonical evidence must ignore site/road insertion order: " + Safe(reversedError), failures);
        }

        private static void ValidateNetworkContract(List<string> failures)
        {
            if (!TryGenerate(GoldenSeed, WorldSizePreset.Large, LandCoveragePreset.High,
                    out GeneratedWorld world, out string error))
            {
                failures.Add("Network contract fixture failed: " + error);
                return;
            }
            ValidateBasic(world, "golden", failures);
            Check(world.Human.Quality.IndependentCycleCount > 0,
                "Regional road graph must contain selected redundancy/cycles, not only an MST.", failures);
            Check(world.Human.SecondaryRoadCount == world.Human.LocalHubCount,
                "Every LocalHub must contribute one deterministic secondary branch.", failures);
            Check(StarterComponentConnected(world.Human),
                "Starter landmass hubs must form one useful road network.", failures);
            Check(CountTerrainAvoidanceRoads(world) > 0,
                "Golden road network must demonstrate terrain-aware detour over a costlier straight route.", failures);

            MacroHumanSite isolated = null;
            for (int first = 0; first < world.Human.Sites.Count && isolated == null; first++)
            {
                MacroHumanSite candidate = world.Human.Sites[first];
                if (candidate.Kind != MacroHumanHubKind.RegionalHub) continue;
                for (int second = 0; second < world.Human.Sites.Count; second++)
                {
                    MacroHumanSite other = world.Human.Sites[second];
                    if (other.Kind == MacroHumanHubKind.RegionalHub &&
                        other.SiteId != candidate.SiteId &&
                        other.LandComponentId == candidate.LandComponentId)
                    {
                        isolated = candidate;
                        break;
                    }
                }
            }
            if (isolated == null)
            {
                failures.Add("Golden fixture lacks two RegionalHubs on one landmass for corruption preflight.");
                return;
            }
            var disconnectedRoads = new List<MacroRoad>(world.Human.Roads);
            disconnectedRoads.RemoveAll(road =>
                road.RoadClass == MacroRoadClass.Primary &&
                (road.FirstEndpoint == isolated.SiteId || road.SecondEndpoint == isolated.SiteId));
            Check(!MacroHumanGeographyPlan.TryCreate(
                      world.Human.GenerationSettings, world.Plan, world.Geography, world.Water,
                      world.Quality, world.Starter, world.Human.Sites, disconnectedRoads,
                      out _, out string disconnectedError) &&
                  disconnectedError.Contains("backbone is disconnected"),
                "Disconnected persisted Primary backbone must fail actionably: " +
                Safe(disconnectedError), failures);
        }

        private static void ValidateRoutineCorpus(List<string> failures)
        {
            foreach (WorldSizePreset size in Enum.GetValues(typeof(WorldSizePreset)))
            foreach (LandCoveragePreset coverage in Enum.GetValues(typeof(LandCoveragePreset)))
            for (int index = 0; index < RoutineSeedsPerCombination; index++)
            {
                long seed = unchecked((long)0x5dc2ea26bc74f819UL +
                                      (long)size * 1000003L +
                                      (long)coverage * 65537L + index * 7919L);
                string label = "fuzz " + size + "/" + coverage + "/" + index;
                if (!TryGenerate(seed, size, coverage, out GeneratedWorld world, out string error))
                {
                    failures.Add(label + " failed: " + error);
                    continue;
                }
                ValidateBasic(world, label, failures);
            }
        }

        private static void ValidateBasic(
            GeneratedWorld world,
            string label,
            List<string> failures)
        {
            Check(world.Human.Quality.MeetsHardRequirements,
                label + " failed committed Human Geography quality.", failures);
            Check(world.Human.RegionalHubCount >= 2 && world.Human.LocalHubCount >= 1 &&
                  world.Human.PrimaryRoadCount > 0 &&
                  world.Human.SecondaryRoadCount > 0,
                label + " lacks required hubs/backbone/branches.", failures);
            Check(world.Human.Quality.RoadCoverageQ16 < 30000 &&
                  world.Human.Quality.StarterDistanceToNetworkCells <= 8,
                label + " has pathological density or starter access.", failures);
            for (int index = 0; index < world.Human.Sites.Count; index++)
            {
                MacroHumanSite site = world.Human.Sites[index];
                Check(world.Water.IsLandAt(site.Position),
                    label + " site " + site.SiteId.Canonical + " is not on land.", failures);
            }
            for (int index = 0; index < world.Human.Roads.Count; index++)
            {
                MacroRoad road = world.Human.Roads[index];
                Check(world.Human.TryGetSite(road.FirstEndpoint, out _) &&
                      world.Human.TryGetSite(road.SecondEndpoint, out _),
                    label + " road has an invalid endpoint.", failures);
                for (int point = 0; point < road.Polyline.Count; point++)
                    Check(world.Water.IsLandAt(road.Polyline[point]),
                        label + " road point crosses ocean.", failures);
            }
        }

        private static void ValidatePersistence(
            string root,
            LoadedContentSet content,
            List<string> failures)
        {
            if (content == null) return;
            var store = new PersistenceFileStore(root);
            WorldSessionService.Close();
            WorldSessionOperationResult create = WorldSessionService.Create(
                "Human Geography Round Trip", new WorldSeed(20260824),
                WorldGenerationSettings.ResolvePreset(WorldSizePreset.Medium),
                LandCoveragePreset.Medium, content, store);
            Check(create.Success, "Current-schema creation failed: " + Safe(create.Failure), failures);
            if (!create.Success) return;
            WorldSession expected = create.Session;
            JToken payload = WorldSessionPersistenceService.ToPayload(expected);
            Check((int)payload["schemaVersion"] == WorldSessionPersistenceService.CurrentSchemaVersion &&
                  payload["macroHumanGeography"]?["sites"] is JArray &&
                  payload["macroHumanGeography"]?["roads"] is JArray,
                "Current schema must persist Human Geography sites and roads.", failures);
            WorldSessionService.Close();
            WorldSessionPersistenceResult read =
                WorldSessionPersistenceService.Read(expected.WorldId.Canonical, store);
            Check(read.Success && read.Session.HasMacroHumanGeography &&
                  read.Session.MacroHumanGeography.CanonicalHash == expected.MacroHumanGeography.CanonicalHash &&
                  read.Session.ActiveSectorId == expected.ActiveSectorId,
                "Current schema must reconstruct exact committed Human Geography: " + Safe(read.Failure), failures);

            JObject corrupt = (JObject)payload.DeepClone();
            corrupt["macroHumanGeography"]["canonicalHash"] = new string('0', 64);
            Check(!WorldSessionPersistenceService.FromPayload(corrupt).Success,
                "Human Geography hash corruption must fail semantic preflight.", failures);

            JObject legacyFour = (JObject)payload.DeepClone();
            legacyFour["schemaVersion"] = WorldSessionPersistenceService.MacroWaterSchemaVersion;
            legacyFour.Remove("macroClimate");
            legacyFour.Remove("macroHumanGeography");
            WorldSessionPersistenceResult legacyRead =
                WorldSessionPersistenceService.FromPayload(legacyFour);
            Check(legacyRead.Success && legacyRead.Session.IsLegacySchemaV4 &&
                  !legacyRead.Session.HasMacroHumanGeography &&
                  (int)WorldSessionPersistenceService.ToPayload(legacyRead.Session)["schemaVersion"] ==
                  WorldSessionPersistenceService.MacroWaterSchemaVersion,
                "Schema 4 must load/re-save without fabricating Human Geography: " +
                Safe(legacyRead.Failure), failures);
        }

        private static void MeasurePresets(
            LoadedContentSet content,
            IList<string> measurements,
            List<string> failures)
        {
            if (content == null) return;
            foreach (WorldSizePreset size in Enum.GetValues(typeof(WorldSizePreset)))
            {
                var stopwatch = Stopwatch.StartNew();
                bool built = WorldSessionBootstrap.TryBuildNew(
                    "Human Geography Size Evidence", new WorldSeed(99887766),
                    WorldGenerationSettings.ResolvePreset(size), LandCoveragePreset.High,
                    content, out WorldSession session, out string error);
                stopwatch.Stop();
                Check(built, "Performance fixture failed for " + size + ": " + Safe(error), failures);
                if (!built) continue;
                int serializedBytes = Encoding.UTF8.GetByteCount(
                    WorldSessionPersistenceService.ToPayload(session).ToString(Formatting.None));
                measurements.Add(size + "=" + stopwatch.ElapsedMilliseconds + "ms/" +
                                 serializedBytes.ToString(CultureInfo.InvariantCulture) + "B/" +
                                 session.MacroHumanGeography.GeometryPointCount + " road points");
            }
        }

        private static bool TryGenerate(
            long seed,
            WorldSizePreset size,
            LandCoveragePreset coverage,
            out GeneratedWorld world,
            out string error)
        {
            world = null;
            error = null;
            var context = new WorldGenerationContext(
                new WorldSeed(seed), GeneratorVersion.Parse(WorldSessionBootstrap.CurrentGeneratorVersion));
            if (!MacroWorldPlanGenerator.TryGenerate(
                    context, WorldGenerationSettings.ResolvePreset(size),
                    out MacroWorldPlan plan, out error)) return false;
            if (!MacroGeographyGenerator.TryGenerate(
                    context, plan, out MacroGeographyPlan geography, out error)) return false;
            if (!MacroWaterGenerator.TryGenerate(
                    plan, geography, coverage, out MacroWaterPlan water, out error)) return false;
            if (!WorldGameplayQualityAnalyzer.TryAnalyze(
                    plan, geography, water,
                    out WorldGameplayQualityAnalysis quality, out error)) return false;
            if (!WorldStarterSectorSelector.TrySelect(quality, out SectorId starter, out error)) return false;
            if (!MacroHumanGeographyGenerator.TryGenerate(
                    context, plan, geography, water, quality, starter,
                    out MacroHumanGeographyPlan human, out error)) return false;
            world = new GeneratedWorld(context, plan, geography, water, quality, starter, human);
            return true;
        }

        private static bool StarterComponentConnected(MacroHumanGeographyPlan human)
        {
            MacroHumanSite start = null;
            for (int index = 0; index < human.Sites.Count; index++)
            {
                if (human.Sites[index].Kind == MacroHumanHubKind.RegionalHub)
                {
                    start = human.Sites[index];
                    break;
                }
            }
            if (start == null) return false;
            int component = start.LandComponentId;
            var reached = new HashSet<MacroHumanSiteId> { start.SiteId };
            bool changed;
            do
            {
                changed = false;
                for (int index = 0; index < human.Roads.Count; index++)
                {
                    MacroRoad road = human.Roads[index];
                    if (reached.Contains(road.FirstEndpoint) && reached.Add(road.SecondEndpoint)) changed = true;
                    if (reached.Contains(road.SecondEndpoint) && reached.Add(road.FirstEndpoint)) changed = true;
                }
            } while (changed);
            for (int index = 0; index < human.Sites.Count; index++)
                if (human.Sites[index].LandComponentId == component &&
                    !reached.Contains(human.Sites[index].SiteId)) return false;
            return true;
        }

        private static int CountTerrainAvoidanceRoads(GeneratedWorld world)
        {
            int count = 0;
            for (int index = 0; index < world.Human.Roads.Count; index++)
            {
                MacroRoad road = world.Human.Roads[index];
                if (!world.Human.TryGetSite(road.FirstEndpoint, out MacroHumanSite first) ||
                    !world.Human.TryGetSite(road.SecondEndpoint, out MacroHumanSite second)) continue;
                ToSample(first.Position, world.Water, out int x0, out int y0);
                ToSample(second.Position, world.Water, out int x1, out int y1);
                if (!TryStraightCost(world, x0, y0, x1, y1, out long directCost, out int directCells))
                    continue;
                if (road.RoutedCellCount > directCells && road.TotalTraversalCost < directCost) count++;
            }
            return count;
        }

        private static bool TryStraightCost(
            GeneratedWorld world,
            int x0,
            int y0,
            int x1,
            int y1,
            out long cost,
            out int cells)
        {
            cost = 0;
            cells = 1;
            int dx = Math.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            while (x0 != x1 || y0 != y1)
            {
                int previousX = x0;
                int previousY = y0;
                int twice = error * 2;
                if (twice >= dy) { error += dy; x0 += sx; }
                if (twice <= dx) { error += dx; y0 += sy; }
                if (world.Water.SampleAt(x0, y0).IsOcean) return false;
                int sampleCost = MacroHumanGeographyGenerator.EvaluateTraversalCost(
                    world.Geography, world.Water, world.Quality, x0, y0);
                cost += (long)sampleCost *
                        (previousX != x0 && previousY != y0 ? 141 : 100) / 100;
                cells++;
            }
            return true;
        }

        private static void ToSample(
            MacroPoint2D point,
            MacroWaterPlan water,
            out int x,
            out int y)
        {
            x = Nearest(point.X, water.WorldBounds.MinX, water.WorldBounds.Width, water.SampleColumns);
            y = Nearest(point.Y, water.WorldBounds.MinY, water.WorldBounds.Height, water.SampleRows);
        }

        private static int Nearest(long value, long minimum, long extent, int count)
        {
            long numerator = (value - minimum) * (count - 1L);
            long denominator = extent - 1;
            return (int)Math.Max(0, Math.Min(count - 1L,
                (numerator * 2 + denominator) / (denominator * 2)));
        }

        private static LoadedContentSet LoadValidatedCore(List<string> failures)
        {
            string modsRoot = Path.Combine(Application.streamingAssetsPath, "Mods");
            var report = new DataLoadReport();
            var loader = new GameDataLoader(modsRoot, report);
            loader.LoadAll();
            var validator = new DataValidator(loader.Database, loader.Tags, report);
            validator.Validate();
            if (report.HasErrors || !loader.TryBuildLoadedContentSet(out LoadedContentSet content))
            {
                failures.Add("Real Core failed loader/DataValidator/provenance validation.");
                return null;
            }
            return content;
        }

        private static void Complete(string title, IList<string> failures, params string[] evidence)
        {
            if (failures.Count > 0)
            {
                string failure = title + ": FAIL\n- " + string.Join("\n- ", failures);
                Debug.LogError(failure);
                throw new InvalidOperationException(failure);
            }
            Debug.Log(title + ": PASS\n" + string.Join("\n", evidence));
        }

        private static void Check(bool condition, string failure, ICollection<string> failures)
        {
            if (!condition) failures.Add(failure);
        }

        private static string Safe(string value) => string.IsNullOrEmpty(value) ? "<NONE>" : value;

        private sealed class GeneratedWorld
        {
            public GeneratedWorld(
                WorldGenerationContext context,
                MacroWorldPlan plan,
                MacroGeographyPlan geography,
                MacroWaterPlan water,
                WorldGameplayQualityAnalysis quality,
                SectorId starter,
                MacroHumanGeographyPlan human)
            {
                Context = context;
                Plan = plan;
                Geography = geography;
                Water = water;
                Quality = quality;
                Starter = starter;
                Human = human;
            }

            public WorldGenerationContext Context { get; }
            public MacroWorldPlan Plan { get; }
            public MacroGeographyPlan Geography { get; }
            public MacroWaterPlan Water { get; }
            public WorldGameplayQualityAnalysis Quality { get; }
            public SectorId Starter { get; }
            public MacroHumanGeographyPlan Human { get; }
        }
    }
}
