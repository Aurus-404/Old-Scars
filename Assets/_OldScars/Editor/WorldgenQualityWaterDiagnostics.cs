using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using OldScars.Core.Data.Loading;
using OldScars.Core.Data.Validation;
using OldScars.Core.Persistence;
using OldScars.Core.World;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace OldScars.EditorTools
{
    public static class WorldgenQualityWaterDiagnostics
    {
        private const long GoldenSeed = 8675309123456789L;
        private const string GoldenWaterHash =
            "c4563b2469d9315fb6c966b3b5bf7297d1ebca2de48e253df4c01abce0c8b727";
        private const int RoutineSeedsPerPreset = 4;

        public static void Run()
        {
            var failures = new List<string>();
            var timings = new List<string>();
            string root = Path.Combine(
                Path.GetTempPath(), "OldScars_WaterQuality_" + Guid.NewGuid().ToString("N"));
            try
            {
                LoadedContentSet content = LoadValidatedCore(failures);
                ValidateDeterminismAndSettingIsolation(content, failures);
                ValidateWaterQualityAndGolden(failures);
                ValidateRoutineFuzz(failures);
                ValidatePersistenceAndLegacy(root, content, failures);
                ValidateInspectorExport(root, failures);
                MeasurePresets(content, timings, failures);
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

            if (failures.Count > 0)
            {
                string failure = "Worldgen Gameplay Quality + Macro Water V1 Diagnostics: FAIL\n- " +
                                 string.Join("\n- ", failures);
                Debug.LogError(failure);
                throw new InvalidOperationException(failure);
            }

            Debug.Log(
                "Worldgen Gameplay Quality + Macro Water V1 Diagnostics: PASS\n" +
                "- MacroWorldPlan/MacroGeography unchanged across Land Coverage; Water changes independently\n" +
                "- boundary-connected ocean bodies, global coastline, acyclic conditioned drainage and basins\n" +
                "- conservative macro traversal/site potentials with hard failures separated from soft findings\n" +
                "- suitable deterministic starter selected after Water; WorldId independent\n" +
                "- golden Water hash: " + GoldenWaterHash + "\n" +
                "- routine fuzz: " + RoutineSeedsPerPreset + " seeds x 4 sizes x 3 coverages\n" +
                "- schema 4 round-trip; schemas 1/2/3 remain explicit legacy without fabricated later truth\n" +
                "- six-panel Worldgen Inspector PNG export succeeded\n" +
                "- approximate generation timings and serialized schema-4 sizes: " + string.Join("; ", timings));
        }

        private static void ValidateDeterminismAndSettingIsolation(
            LoadedContentSet content,
            List<string> failures)
        {
            Generate(GoldenSeed, WorldSizePreset.Large, LandCoveragePreset.High,
                out MacroWorldPlan planA, out MacroGeographyPlan geographyA,
                out MacroWaterPlan waterA, out WorldGameplayQualityAnalysis qualityA,
                out SectorId starterA, failures, "A first");
            Generate(GoldenSeed, WorldSizePreset.Large, LandCoveragePreset.High,
                out MacroWorldPlan planB, out MacroGeographyPlan geographyB,
                out MacroWaterPlan waterB, out _, out SectorId starterB, failures, "A repeated");
            Check(planA != null && planB != null &&
                  planA.CanonicalHash == planB.CanonicalHash &&
                  geographyA.CanonicalHash == geographyB.CanonicalHash &&
                  waterA.CanonicalHash == waterB.CanonicalHash && starterA == starterB,
                "A. Same input must produce exact plan/geography/water/starter evidence.", failures);

            Generate(GoldenSeed, WorldSizePreset.Large, LandCoveragePreset.Low,
                out MacroWorldPlan lowPlan, out MacroGeographyPlan lowGeography,
                out MacroWaterPlan lowWater, out _, out _, failures, "coverage Low");
            Generate(GoldenSeed, WorldSizePreset.Large, LandCoveragePreset.Medium,
                out MacroWorldPlan mediumPlan, out MacroGeographyPlan mediumGeography,
                out MacroWaterPlan mediumWater, out _, out _, failures, "coverage Medium");
            Check(lowPlan != null && mediumPlan != null && planA != null &&
                  lowPlan.CanonicalHash == mediumPlan.CanonicalHash &&
                  mediumPlan.CanonicalHash == planA.CanonicalHash &&
                  lowGeography.CanonicalHash == mediumGeography.CanonicalHash &&
                  mediumGeography.CanonicalHash == geographyA.CanonicalHash &&
                  lowWater.CanonicalHash != mediumWater.CanonicalHash &&
                  mediumWater.CanonicalHash != waterA.CanonicalHash &&
                  lowWater.LandRatioQ16 < mediumWater.LandRatioQ16 &&
                  mediumWater.LandRatioQ16 < waterA.LandRatioQ16,
                "Land Coverage must change only Water and monotonically change actual land share.", failures);
            Check(qualityA != null && qualityA.MeetsHardRequirements,
                "Current golden world must meet hard gameplay-quality requirements.", failures);

            if (content != null)
            {
                WorldGenerationSettings settings =
                    WorldGenerationSettings.ResolvePreset(WorldSizePreset.Large);
                bool builtA = WorldSessionBootstrap.TryBuildNew(
                    "Water A", new WorldSeed(GoldenSeed), settings, LandCoveragePreset.High,
                    content, out WorldSession sessionA, out string errorA);
                bool builtB = WorldSessionBootstrap.TryBuildNew(
                    "Water B", new WorldSeed(GoldenSeed), settings, LandCoveragePreset.High,
                    content, out WorldSession sessionB, out string errorB);
                Check(builtA && builtB && sessionA.WorldId != sessionB.WorldId &&
                      sessionA.MacroWorldPlan.CanonicalHash == sessionB.MacroWorldPlan.CanonicalHash &&
                      sessionA.MacroGeography.CanonicalHash == sessionB.MacroGeography.CanonicalHash &&
                      sessionA.MacroWater.CanonicalHash == sessionB.MacroWater.CanonicalHash &&
                      sessionA.ActiveSectorId == sessionB.ActiveSectorId,
                    "WorldId must not alter plan/geography/water/starter. " + Safe(errorA) + " " + Safe(errorB),
                    failures);
            }
        }

        private static void ValidateWaterQualityAndGolden(List<string> failures)
        {
            Generate(GoldenSeed, WorldSizePreset.Large, LandCoveragePreset.High,
                out MacroWorldPlan plan, out MacroGeographyPlan geography,
                out MacroWaterPlan water, out WorldGameplayQualityAnalysis quality,
                out SectorId starter, failures, "golden");
            if (plan == null || geography == null || water == null || quality == null) return;

            Check(water.CanonicalHash == GoldenWaterHash,
                "Golden Water evidence drifted. Expected " + GoldenWaterHash +
                ", got " + water.CanonicalHash + ".", failures);
            Check(water.OceanSampleCount > 0 && water.OceanSampleCount < water.SampleCount &&
                  water.CoastlineSampleCount > 0 && water.OceanBodies.Count > 0,
                "Water must contain meaningful land/ocean/coastline and connected ocean body metadata.", failures);
            Check(quality.MeetsHardRequirements &&
                  quality.GlobalLowReliefShareQ16 >= WorldGameplayQualityCriteria.MinimumGlobalLowReliefShareQ16 &&
                  quality.LargestGlobalTravelRegionQ16 >= WorldGameplayQualityCriteria.MinimumGlobalTravelRegionQ16 &&
                  quality.LargestLandTravelRegionQ16 >= WorldGameplayQualityCriteria.MinimumLandTravelRegionQ16 &&
                  quality.RuggedShareQ16 >= WorldGameplayQualityCriteria.MinimumRuggedShareQ16 &&
                  quality.SuitableStarterCandidateCount >= 2,
                "Gameplay quality must retain broad corridors/sites and rugged terrain with multiple starters.",
                failures);
            Check(plan.TryGetSectorPlacement(starter, out MacroSectorPlacement placement) &&
                  water.IsLandAt(placement.Position) &&
                  geography.LandformAt(placement.Position) != MacroLandform.Mountains,
                "Selected starter must be land, non-mountain, and present in the plan.", failures);

            int columns = water.SampleColumns;
            int rows = water.SampleRows;
            for (int index = 0; index < water.SampleCount; index++)
            {
                MacroWaterSample sample = water.SampleAt(index % columns, index / columns);
                if (sample.IsOcean)
                {
                    Check(sample.DrainageDirection == MacroWaterPlan.DrainageOutlet,
                        "Ocean sample must be a drainage outlet.", failures);
                    continue;
                }
                int current = index;
                int steps = 0;
                while (steps <= water.SampleCount)
                {
                    MacroWaterSample currentSample = water.SampleAt(current % columns, current / columns);
                    if (currentSample.IsOcean) break;
                    if (!water.TryGetDownstreamSample(
                            current % columns, current / columns,
                            out int downstreamX, out int downstreamY))
                    {
                        failures.Add("Drainage exited finite bounds from sample " + index + ".");
                        break;
                    }
                    current = downstreamY * columns + downstreamX;
                    steps++;
                }
                if (steps > water.SampleCount)
                    failures.Add("Drainage failed to terminate from sample " + index + ".");
            }
        }

        private static void ValidateRoutineFuzz(List<string> failures)
        {
            foreach (WorldSizePreset size in Enum.GetValues(typeof(WorldSizePreset)))
            foreach (LandCoveragePreset coverage in Enum.GetValues(typeof(LandCoveragePreset)))
            for (int seedIndex = 0; seedIndex < RoutineSeedsPerPreset; seedIndex++)
            {
                long seed = unchecked(
                    (long)0x728feac3d10b4a69UL + (long)size * 1000003L +
                    (long)coverage * 65537L + seedIndex * 7919L);
                string label = "fuzz " + size + "/" + coverage + "/" + seedIndex;
                Generate(seed, size, coverage, out MacroWorldPlan plan,
                    out MacroGeographyPlan geography, out MacroWaterPlan water,
                    out WorldGameplayQualityAnalysis quality, out SectorId starter,
                    failures, label);
                if (plan == null || geography == null || water == null || quality == null) continue;
                Check(plan.WorldBounds == geography.WorldBounds &&
                      geography.WorldBounds == water.WorldBounds &&
                      quality.MeetsHardRequirements && starter.IsValid,
                    label + " violated shared bounds/quality/starter invariants.", failures);
            }
        }

        private static void ValidatePersistenceAndLegacy(
            string root,
            LoadedContentSet content,
            List<string> failures)
        {
            if (content == null) return;
            var store = new PersistenceFileStore(root);
            WorldSessionService.Close();
            WorldSessionOperationResult create = WorldSessionService.Create(
                "Water Round Trip", new WorldSeed(20260824),
                WorldGenerationSettings.ResolvePreset(WorldSizePreset.Medium),
                LandCoveragePreset.Medium, content, store);
            Check(create.Success, "Schema-4 creation failed: " + Safe(create.Failure), failures);
            if (!create.Success) return;
            WorldSession expected = create.Session;
            JToken payload = WorldSessionPersistenceService.ToPayload(expected);
            Check((int)payload["schemaVersion"] == WorldSessionPersistenceService.CurrentSchemaVersion &&
                  payload["macroWater"]?["oceanMaskBase64"]?.Type == JTokenType.String &&
                  payload["macroWater"]?["drainageDirectionsBase64"]?.Type == JTokenType.String,
                "Schema 4 must persist committed Macro Water truth.", failures);
            WorldSessionService.Close();
            WorldSessionPersistenceResult read =
                WorldSessionPersistenceService.Read(expected.WorldId.Canonical, store);
            Check(read.Success && read.Session.HasMacroWater && read.Session.HasGameplayQuality &&
                  read.Session.MacroWorldPlan.CanonicalHash == expected.MacroWorldPlan.CanonicalHash &&
                  read.Session.MacroGeography.CanonicalHash == expected.MacroGeography.CanonicalHash &&
                  read.Session.MacroWater.CanonicalHash == expected.MacroWater.CanonicalHash &&
                  read.Session.ActiveSectorId == expected.ActiveSectorId,
                "Schema-4 save/read must reconstruct exact committed Water and starter. " + Safe(read.Failure),
                failures);

            JObject corrupt = (JObject)payload.DeepClone();
            corrupt["macroWater"]["canonicalHash"] = new string('0', 64);
            Check(!WorldSessionPersistenceService.FromPayload(corrupt).Success,
                "Corrupt Water evidence must fail semantic preflight.", failures);

            JObject corruptOcean = (JObject)payload.DeepClone();
            byte[] corruptOceanBytes = Convert.FromBase64String(
                (string)corruptOcean["macroWater"]["oceanMaskBase64"]);
            corruptOceanBytes[0] = corruptOceanBytes[0] == 0 ? (byte)1 : (byte)0;
            corruptOcean["macroWater"]["oceanMaskBase64"] = Convert.ToBase64String(corruptOceanBytes);
            WorldSessionPersistenceResult corruptOceanResult =
                WorldSessionPersistenceService.FromPayload(corruptOcean);
            Check(!corruptOceanResult.Success &&
                  corruptOceanResult.Failure != null &&
                  corruptOceanResult.Failure.Contains("sea level"),
                "Ocean mask corruption must fail sea-level/boundary semantic preflight.", failures);

            JObject schemaThree = (JObject)payload.DeepClone();
            schemaThree["schemaVersion"] = WorldSessionPersistenceService.MacroGeographySchemaVersion;
            schemaThree.Remove("macroWater");
            WorldSessionPersistenceResult legacyThree =
                WorldSessionPersistenceService.FromPayload(schemaThree);
            Check(legacyThree.Success && legacyThree.Session.IsLegacySchemaV3 &&
                  legacyThree.Session.HasMacroGeography && !legacyThree.Session.HasMacroWater &&
                  !legacyThree.Session.HasGameplayQuality &&
                  (int)WorldSessionPersistenceService.ToPayload(legacyThree.Session)["schemaVersion"] ==
                  WorldSessionPersistenceService.MacroGeographySchemaVersion,
                "Schema 3 must load/re-save without fabricated Water/quality. " + Safe(legacyThree.Failure),
                failures);

            JObject schemaTwo = (JObject)schemaThree.DeepClone();
            schemaTwo["schemaVersion"] = WorldSessionPersistenceService.MacroPlanSchemaVersion;
            schemaTwo.Remove("macroGeography");
            WorldSessionPersistenceResult legacyTwo = WorldSessionPersistenceService.FromPayload(schemaTwo);
            Check(legacyTwo.Success && legacyTwo.Session.IsLegacySchemaV2 &&
                  !legacyTwo.Session.HasMacroGeography && !legacyTwo.Session.HasMacroWater &&
                  (int)WorldSessionPersistenceService.ToPayload(legacyTwo.Session)["schemaVersion"] ==
                  WorldSessionPersistenceService.MacroPlanSchemaVersion,
                "Schema 2 must remain legacy without fabricated geography/water.", failures);

            JObject schemaOne = (JObject)schemaTwo.DeepClone();
            schemaOne["schemaVersion"] = WorldSessionPersistenceService.LegacySchemaVersion;
            schemaOne["topology"] = schemaOne["macroWorldPlan"]["topology"].DeepClone();
            schemaOne.Remove("macroWorldPlan");
            WorldSessionPersistenceResult legacyOne = WorldSessionPersistenceService.FromPayload(schemaOne);
            Check(legacyOne.Success && legacyOne.Session.IsLegacySchemaV1 &&
                  !legacyOne.Session.HasMacroWorldPlan &&
                  (int)WorldSessionPersistenceService.ToPayload(legacyOne.Session)["schemaVersion"] ==
                  WorldSessionPersistenceService.LegacySchemaVersion,
                "Schema 1 must remain legacy without fabricated later-pass truth.", failures);
        }

        private static void MeasurePresets(
            LoadedContentSet content,
            List<string> timings,
            List<string> failures)
        {
            if (content == null) return;
            foreach (WorldSizePreset size in Enum.GetValues(typeof(WorldSizePreset)))
            {
                var stopwatch = Stopwatch.StartNew();
                bool built = WorldSessionBootstrap.TryBuildNew(
                    "Water Size Evidence",
                    new WorldSeed(99887766),
                    WorldGenerationSettings.ResolvePreset(size),
                    LandCoveragePreset.High,
                    content,
                    out WorldSession session,
                    out string error);
                stopwatch.Stop();
                Check(built, "Timing/size world failed for " + size + ": " + Safe(error), failures);
                if (built)
                {
                    MacroWaterPlan water = session.MacroWater;
                    long rawBytes = water.SampleCount * 7L + water.BasinCandidates.Count * 12L;
                    int serializedBytes = Encoding.UTF8.GetByteCount(
                        WorldSessionPersistenceService.ToPayload(session).ToString(
                            Newtonsoft.Json.Formatting.None));
                    timings.Add(size + "=" + stopwatch.ElapsedMilliseconds + "ms/" +
                                serializedBytes + " serialized payload bytes/" +
                                rawBytes + " estimated raw Water bytes");
                }
            }
        }

        private static void ValidateInspectorExport(string root, List<string> failures)
        {
            Generate(GoldenSeed, WorldSizePreset.Large, LandCoveragePreset.High,
                out MacroWorldPlan plan, out MacroGeographyPlan geography,
                out MacroWaterPlan water, out WorldGameplayQualityAnalysis quality,
                out SectorId starter, failures, "inspector preview");
            if (plan == null || geography == null || water == null || quality == null) return;
            string path = Path.Combine(root, "worldgen-inspector.png");
            MacroGeographyPreviewExporter.Export(
                plan, geography, water, quality, starter, path, 192, 192, true);
            Check(File.Exists(path) && new FileInfo(path).Length > 12000,
                "Worldgen Inspector must export a non-empty six-panel PNG.", failures);
        }

        private static void Generate(
            long seed,
            WorldSizePreset size,
            LandCoveragePreset coverage,
            out MacroWorldPlan plan,
            out MacroGeographyPlan geography,
            out MacroWaterPlan water,
            out WorldGameplayQualityAnalysis quality,
            out SectorId starter,
            List<string> failures,
            string label)
        {
            plan = null;
            geography = null;
            water = null;
            quality = null;
            starter = default;
            var context = new WorldGenerationContext(
                new WorldSeed(seed), GeneratorVersion.Parse(WorldSessionBootstrap.CurrentGeneratorVersion));
            if (!MacroWorldPlanGenerator.TryGenerate(
                    context, WorldGenerationSettings.ResolvePreset(size), out plan, out string planError))
            {
                failures.Add(label + " plan failed: " + planError);
                return;
            }
            if (!MacroGeographyGenerator.TryGenerate(
                    context, plan, out geography, out string geographyError))
            {
                failures.Add(label + " geography failed: " + geographyError);
                return;
            }
            if (!MacroWaterGenerator.TryGenerate(
                    plan, geography, coverage, out water, out string waterError))
            {
                failures.Add(label + " water failed: " + waterError);
                return;
            }
            if (!WorldGameplayQualityAnalyzer.TryAnalyze(
                    plan, geography, water, out quality, out string qualityError))
            {
                failures.Add(label + " quality analysis failed: " + qualityError);
                return;
            }
            if (!WorldStarterSectorSelector.TrySelect(quality, out starter, out string starterError))
                failures.Add(label + " starter selection failed: " + starterError +
                             " [landRegionQ16=" + quality.LargestLandTravelRegionQ16 +
                             ", candidates=" + quality.SuitableStarterCandidateCount +
                             ", landRatioQ16=" + water.LandRatioQ16 + "]");
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

        private static void Check(bool condition, string failure, List<string> failures)
        {
            if (!condition) failures.Add(failure);
        }

        private static string Safe(string value) => string.IsNullOrEmpty(value) ? "<NONE>" : value;
    }
}
