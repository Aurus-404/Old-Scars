using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OldScars.Core.Data.Loading;
using OldScars.Core.Data.Validation;
using OldScars.Core.Persistence;
using OldScars.Core.World;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace OldScars.EditorTools
{
    public static class MacroEnvironmentDiagnostics
    {
        private const long GoldenSeed = 8675309123456789L;
        private const string PreviewDirectoryEnvironment =
            "OLD_SCARS_MACRO_ENVIRONMENT_PREVIEW_DIR";
        private const string GoldenPlanHash =
            "3f300ba2129962493d2ab8f2ad6ec0863e96aa0ceeb400f9899f91889a34e91a";
        private const string GoldenGeographyHash =
            "c2d412fcdcb1b0e1b41f4fdbda2df01258758e6db9c6b93aac59b446be7dbd3e";
        private const string GoldenWaterHash =
            "ec29f501e4f36ae3b2313d3da6089f2fe6e92b052f18079c649e21ce8faabfc0";
        private const string GoldenClimateHash =
            "a4b7869a7d8deab093eb9b9c5f7a2da118156f22c61ac466fbd0a9e64958eec1";
        private const string GoldenHumanHash =
            "a786f018ce3bdea44aeb066c80e38cb1f5dc8e114c65bd7eb352489628245ba6";
        private const string GoldenEnvironmentHash =
            "f8081c040da64ccce5e5eb5ffed941c2c2c44cd7ac5442582ee5d331c3abd1c5";
        private const int StressSeedsPerCombination = 6;

        public static void Run()
        {
            var failures = new List<string>();
            var softFindings = new List<string>();
            var measurements = new List<string>();
            string root = Path.Combine(
                Path.GetTempPath(), "OldScars_MacroEnvironment_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string previewPath = null;
            try
            {
                LoadedContentSet content = LoadValidatedCore(failures);
                ValidateTaxonomyAndSettings(failures);
                ValidateDeterminismIsolationAndGoldens(content, failures);
                ValidateClassificationQuality(
                    GoldenSeed, WorldSizePreset.Large, LandCoveragePreset.High,
                    "golden", failures, softFindings, out _);
                ValidatePersistence(root, content, failures);
                MeasurePresets(content, measurements, failures);
                previewPath = ExportGoldenPreview();
            }
            finally
            {
                WorldSessionService.Close();
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }

            Complete(
                "Macro Environment / Biome Regions V1 Diagnostics",
                failures,
                "- contract: " + MacroEnvironmentGenerator.DeterministicGenerationContract,
                "- taxonomy: 14 terrestrial families + None; Primary/Secondary/TransitionQ16",
                "- upstream goldens: " + GoldenPlanHash + " / " + GoldenGeographyHash +
                " / " + GoldenWaterHash + " / " + GoldenClimateHash + " / " + GoldenHumanHash,
                "- Environment golden: " + GoldenEnvironmentHash,
                "- schema: world_session_v1 v7; schemas 1-6 remain legacy without fabricated Environment",
                "- performance: " + string.Join(" | ", measurements),
                "- soft findings: " + softFindings.Count.ToString(CultureInfo.InvariantCulture) +
                (softFindings.Count == 0 ? "" : " | " + string.Join(" | ", softFindings)),
                "- preview: " + Safe(previewPath));
        }

        public static void RunStress()
        {
            var failures = new List<string>();
            var softFindings = new List<string>();
            int attempted = 0;
            int completed = 0;
            var stopwatch = Stopwatch.StartNew();
            foreach (WorldSizePreset size in Enum.GetValues(typeof(WorldSizePreset)))
            foreach (LandCoveragePreset coverage in Enum.GetValues(typeof(LandCoveragePreset)))
            for (int index = 0; index < StressSeedsPerCombination; index++)
            {
                attempted++;
                long seed = 0x45E17L + index * 104729L + (int)size * 1000003L +
                            (int)coverage * 7000003L;
                ValidateClassificationQuality(
                    seed, size, coverage,
                    size + "/" + coverage + "/" + seed,
                    failures, softFindings, out bool generated);
                if (generated) completed++;
            }
            stopwatch.Stop();

            Complete(
                "Macro Environment Stress Corpus",
                failures,
                "- attempted/completed: " + attempted + "/" + completed,
                "- combinations: 6 seeds x 4 sizes x 3 land coverages",
                "- elapsedMs: " + stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture),
                "- soft findings: " + softFindings.Count.ToString(CultureInfo.InvariantCulture) +
                (softFindings.Count == 0 ? "" : " | " + string.Join(" | ", softFindings)));
        }

        public static void ExportRepresentativePreviews()
        {
            string directory = ResolvePreviewDirectory();
            Directory.CreateDirectory(directory);
            var paths = new List<string>();
            foreach (WorldSizePreset size in Enum.GetValues(typeof(WorldSizePreset)))
            {
                if (!TryGenerate(
                        GoldenSeed, size, LandCoveragePreset.High,
                        WorldSessionBootstrap.CurrentGeneratorVersion,
                        MacroClimateGenerator.DeterministicGenerationContract,
                        MacroEnvironmentGenerator.DeterministicGenerationContract,
                        out GeneratedWorld world, out string error))
                {
                    throw new InvalidOperationException(
                        "Representative Environment preview failed for " + size + ": " + error);
                }
                string path = Path.Combine(
                    directory, "OldScars_MacroEnvironment_" + size + "_High_" +
                               GoldenSeed.ToString(CultureInfo.InvariantCulture) + ".png");
                MacroGeographyPreviewExporter.Export(
                    world.Plan, world.Geography, world.Water, world.Climate,
                    world.Environment, world.Quality, world.Human, world.Starter,
                    path, 320, 320, true);
                paths.Add(path);
            }
            Debug.Log(
                "Macro Environment Representative Preview Export: PASS\n" +
                "- paths: " + string.Join(" | ", paths) + "\n" +
                "- presets: Small/Medium/Large/Huge; coverage High; seed " + GoldenSeed);
        }

        private static void ValidateTaxonomyAndSettings(List<string> failures)
        {
            Check(Enum.GetValues(typeof(MacroBiomeFamily)).Length == 15,
                "Taxonomy must contain None plus exactly 14 terrestrial families.", failures);
            Check((byte)MacroBiomeFamily.None == 0 &&
                  (byte)MacroBiomeFamily.TropicalRainforest == 14,
                "MacroBiomeFamily durable values must remain contiguous 0..14.", failures);
            Check(!Enum.IsDefined(typeof(MacroBiomeFamily), (byte)15),
                "Taxonomy must not contain speculative V1 families.", failures);
        }

        private static void ValidateDeterminismIsolationAndGoldens(
            LoadedContentSet content,
            List<string> failures)
        {
            Check(TryGenerate(
                    GoldenSeed, WorldSizePreset.Large, LandCoveragePreset.High,
                    WorldSessionBootstrap.CurrentGeneratorVersion,
                    MacroClimateGenerator.DeterministicGenerationContract,
                    MacroEnvironmentGenerator.DeterministicGenerationContract,
                    out GeneratedWorld first, out string firstError),
                "Golden Environment generation failed: " + Safe(firstError), failures);
            Check(TryGenerate(
                    GoldenSeed, WorldSizePreset.Large, LandCoveragePreset.High,
                    WorldSessionBootstrap.CurrentGeneratorVersion,
                    MacroClimateGenerator.DeterministicGenerationContract,
                    MacroEnvironmentGenerator.DeterministicGenerationContract,
                    out GeneratedWorld repeat, out string repeatError),
                "Repeated Environment generation failed: " + Safe(repeatError), failures);
            if (first == null || repeat == null) return;

            Check(AllHashesEqual(first, repeat),
                "Same seed/settings must reproduce exact Environment and upstream hashes.", failures);
            Check(TryGenerate(
                    GoldenSeed + 1, WorldSizePreset.Large, LandCoveragePreset.High,
                    WorldSessionBootstrap.CurrentGeneratorVersion,
                    MacroClimateGenerator.DeterministicGenerationContract,
                    MacroEnvironmentGenerator.DeterministicGenerationContract,
                    out GeneratedWorld differentSeed, out string differentSeedError) &&
                  differentSeed.Environment.CanonicalHash != first.Environment.CanonicalHash,
                "Different WorldSeed should produce different Environment truth: " +
                Safe(differentSeedError), failures);

            Check(TryGenerate(
                    GoldenSeed, WorldSizePreset.Large, LandCoveragePreset.High,
                    "world_pipeline_environment_isolation_probe",
                    MacroClimateGenerator.DeterministicGenerationContract,
                    MacroEnvironmentGenerator.DeterministicGenerationContract,
                    out GeneratedWorld differentPipeline, out string pipelineError) &&
                  AllHashesEqual(first, differentPipeline),
                "Global pipeline metadata alone must not reseed any pass: " +
                Safe(pipelineError), failures);

            Check(TryGenerate(
                    GoldenSeed, WorldSizePreset.Large, LandCoveragePreset.High,
                    WorldSessionBootstrap.CurrentGeneratorVersion,
                    MacroClimateGenerator.DeterministicGenerationContract,
                    "macro_environment_diagnostic_probe",
                    out GeneratedWorld differentEnvironmentContract,
                    out string environmentContractError) &&
                  UpstreamAndHumanEqual(first, differentEnvironmentContract) &&
                  differentEnvironmentContract.Environment.CanonicalHash !=
                  first.Environment.CanonicalHash,
                "Changing only the Environment contract must change only Environment evidence: " +
                Safe(environmentContractError), failures);

            Check(TryGenerate(
                    GoldenSeed, WorldSizePreset.Large, LandCoveragePreset.High,
                    WorldSessionBootstrap.CurrentGeneratorVersion,
                    "macro_climate_environment_dependency_probe",
                    MacroEnvironmentGenerator.DeterministicGenerationContract,
                    out GeneratedWorld differentClimate, out string climateError) &&
                  first.Plan.CanonicalHash == differentClimate.Plan.CanonicalHash &&
                  first.Geography.CanonicalHash == differentClimate.Geography.CanonicalHash &&
                  first.Water.CanonicalHash == differentClimate.Water.CanonicalHash &&
                  first.Human.CanonicalHash == differentClimate.Human.CanonicalHash &&
                  first.Climate.CanonicalHash != differentClimate.Climate.CanonicalHash &&
                  first.Environment.CanonicalHash != differentClimate.Environment.CanonicalHash,
                "Climate contract change must affect Climate/Environment but not upstream/Human: " +
                Safe(climateError), failures);

            Check(first.Plan.CanonicalHash == GoldenPlanHash,
                "Plan golden drifted: " + first.Plan.CanonicalHash, failures);
            Check(first.Geography.CanonicalHash == GoldenGeographyHash,
                "Geography golden drifted: " + first.Geography.CanonicalHash, failures);
            Check(first.Water.CanonicalHash == GoldenWaterHash,
                "Water golden drifted: " + first.Water.CanonicalHash, failures);
            Check(first.Climate.CanonicalHash == GoldenClimateHash,
                "Climate golden drifted: " + first.Climate.CanonicalHash, failures);
            Check(first.Human.CanonicalHash == GoldenHumanHash,
                "Human golden drifted: " + first.Human.CanonicalHash, failures);
            Check(first.Environment.CanonicalHash == GoldenEnvironmentHash,
                "Environment golden is " + first.Environment.CanonicalHash +
                " (expected " + GoldenEnvironmentHash + ").", failures);

            Check(TryGenerate(
                    GoldenSeed, WorldSizePreset.Large, LandCoveragePreset.Low,
                    WorldSessionBootstrap.CurrentGeneratorVersion,
                    MacroClimateGenerator.DeterministicGenerationContract,
                    MacroEnvironmentGenerator.DeterministicGenerationContract,
                    out GeneratedWorld lowCoverage, out string lowCoverageError) &&
                  first.Plan.CanonicalHash == lowCoverage.Plan.CanonicalHash &&
                  first.Geography.CanonicalHash == lowCoverage.Geography.CanonicalHash &&
                  first.Water.CanonicalHash != lowCoverage.Water.CanonicalHash &&
                  first.Climate.CanonicalHash != lowCoverage.Climate.CanonicalHash &&
                  first.Environment.CanonicalHash != lowCoverage.Environment.CanonicalHash,
                "LandCoverage must preserve Plan/Geography while downstream truth may change: " +
                Safe(lowCoverageError), failures);

            if (content == null) return;
            bool worldABuilt = WorldSessionBootstrap.TryBuildNew(
                    "Environment World A", new WorldSeed(GoldenSeed),
                    WorldGenerationSettings.ResolvePreset(WorldSizePreset.Large),
                    LandCoveragePreset.High, content,
                    out WorldSession worldA, out string worldAError);
            bool worldBBuilt = WorldSessionBootstrap.TryBuildNew(
                    "Environment World B", new WorldSeed(GoldenSeed),
                    WorldGenerationSettings.ResolvePreset(WorldSizePreset.Large),
                    LandCoveragePreset.High, content,
                    out WorldSession worldB, out string worldBError);
            Check(worldABuilt && worldBBuilt &&
                  worldA.WorldId != worldB.WorldId &&
                  worldA.MacroEnvironment.CanonicalHash == worldB.MacroEnvironment.CanonicalHash,
                "WorldId must not alter Environment truth: " +
                Safe(worldAError) + " / " + Safe(worldBError), failures);
        }

        private static void ValidateClassificationQuality(
            long seed,
            WorldSizePreset size,
            LandCoveragePreset coverage,
            string label,
            List<string> failures,
            List<string> softFindings,
            out bool generated)
        {
            generated = TryGenerate(
                seed, size, coverage,
                WorldSessionBootstrap.CurrentGeneratorVersion,
                MacroClimateGenerator.DeterministicGenerationContract,
                MacroEnvironmentGenerator.DeterministicGenerationContract,
                out GeneratedWorld world, out string error);
            Check(generated, label + " generation failed: " + Safe(error), failures);
            if (!generated) return;

            EnvironmentStatistics statistics = Analyze(world);
            Check(statistics.LandSamples > 0 && statistics.OceanSamples > 0,
                label + " requires both land and ocean environment samples.", failures);
            Check(statistics.InvalidSamples == 0,
                label + " contains invalid Environment samples.", failures);
            Check(statistics.DistinctPrimaryFamilies >= 2,
                label + " produced a constant/broken terrestrial classification.", failures);
            Check(statistics.NeighborDisagreementQ16 < 50000,
                label + " has checkerboard-level neighbor disagreement.", failures);

            if (statistics.DistinctPrimaryFamilies < 4)
                softFindings.Add(label + " low diversity=" + statistics.DistinctPrimaryFamilies);
            if (statistics.LargestComponentRatioQ16 > 50000)
                softFindings.Add(label + " dominant component=" + statistics.LargestComponentRatioQ16);
            if (statistics.TinyComponentCount > statistics.LandSamples / 20)
                softFindings.Add(label + " tiny components=" + statistics.TinyComponentCount);
            if (statistics.ColdCorrelationQ16 < 49151 && statistics.VeryColdSamples > 0)
                softFindings.Add(label + " cold correlation=" + statistics.ColdCorrelationQ16);
            if (statistics.HotDryCorrelationQ16 < 49151 && statistics.HotDrySamples > 0)
                softFindings.Add(label + " hot/dry correlation=" + statistics.HotDryCorrelationQ16);
            if (statistics.WetTemperateCorrelationQ16 < 49151 && statistics.WetTemperateSamples > 0)
                softFindings.Add(label + " wet/temperate correlation=" +
                                 statistics.WetTemperateCorrelationQ16);

            Debug.Log("[MacroEnvironment][QUALITY] " + label + " " + statistics.Describe());
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
                "Environment Schema 7", new WorldSeed(20260828),
                WorldGenerationSettings.ResolvePreset(WorldSizePreset.Medium),
                LandCoveragePreset.Medium, content, store);
            Check(create.Success, "Schema 7 creation failed: " + Safe(create.Failure), failures);
            if (!create.Success) return;

            WorldSession expected = create.Session;
            JObject payload = (JObject)WorldSessionPersistenceService.ToPayload(expected);
            Check((int)payload["schemaVersion"] == WorldSessionPersistenceService.CurrentSchemaVersion &&
                  payload["macroEnvironment"]?["primarySamplesBase64"]?.Type == JTokenType.String &&
                  payload["macroEnvironment"]?["secondarySamplesBase64"]?.Type == JTokenType.String &&
                  payload["macroEnvironment"]?["transitionSamplesBase64"]?.Type == JTokenType.String &&
                  payload["macroEnvironment"]?["generationSettings"]?["profiles"] is JArray,
                "Schema 7 must persist exact Environment fields/settings.", failures);

            WorldSessionService.Close();
            WorldSessionPersistenceResult read =
                WorldSessionPersistenceService.Read(expected.WorldId.Canonical, store);
            Check(read.Success && read.Session.HasMacroEnvironment &&
                  SessionsHaveSameCommittedTruth(expected, read.Session),
                "Schema 7 did not reconstruct exact committed Environment/world truth: " +
                Safe(read.Failure), failures);

            JObject corruptHash = (JObject)payload.DeepClone();
            corruptHash["macroEnvironment"]["canonicalHash"] = new string('0', 64);
            Check(!WorldSessionPersistenceService.FromPayload(corruptHash).Success,
                "Malformed Environment hash must fail strict preflight.", failures);

            int firstLand = FindFirstSample(expected.MacroWater, ocean: false);
            int firstOcean = FindFirstSample(expected.MacroWater, ocean: true);
            JObject invalidLand = (JObject)payload.DeepClone();
            byte[] invalidLandPrimary = Convert.FromBase64String(
                (string)invalidLand["macroEnvironment"]["primarySamplesBase64"]);
            invalidLandPrimary[firstLand] = (byte)MacroBiomeFamily.None;
            invalidLand["macroEnvironment"]["primarySamplesBase64"] =
                Convert.ToBase64String(invalidLandPrimary);
            Check(!WorldSessionPersistenceService.FromPayload(invalidLand).Success,
                "Land Primary=None must fail strict Environment preflight.", failures);

            JObject invalidOcean = (JObject)payload.DeepClone();
            byte[] invalidOceanPrimary = Convert.FromBase64String(
                (string)invalidOcean["macroEnvironment"]["primarySamplesBase64"]);
            invalidOceanPrimary[firstOcean] = (byte)MacroBiomeFamily.Tundra;
            invalidOcean["macroEnvironment"]["primarySamplesBase64"] =
                Convert.ToBase64String(invalidOceanPrimary);
            Check(!WorldSessionPersistenceService.FromPayload(invalidOcean).Success,
                "Ocean terrestrial biome must fail strict Environment preflight.", failures);

            JObject identicalFamilies = (JObject)payload.DeepClone();
            byte[] primary = Convert.FromBase64String(
                (string)identicalFamilies["macroEnvironment"]["primarySamplesBase64"]);
            byte[] secondary = Convert.FromBase64String(
                (string)identicalFamilies["macroEnvironment"]["secondarySamplesBase64"]);
            secondary[firstLand] = primary[firstLand];
            identicalFamilies["macroEnvironment"]["secondarySamplesBase64"] =
                Convert.ToBase64String(secondary);
            Check(!WorldSessionPersistenceService.FromPayload(identicalFamilies).Success,
                "Primary==Secondary must fail strict Environment preflight.", failures);

            JObject schemaSix = BuildSchemaSixPayload(payload);
            WorldSessionPersistenceResult legacySix =
                WorldSessionPersistenceService.FromPayload(schemaSix);
            Check(legacySix.Success && legacySix.Session.HasMacroClimate &&
                  legacySix.Session.HasMacroHumanGeography &&
                  !legacySix.Session.HasMacroEnvironment &&
                  (int)WorldSessionPersistenceService.ToPayload(legacySix.Session)["schemaVersion"] ==
                  WorldSessionPersistenceService.MacroClimateSchemaVersion,
                "Schema 6 must preserve Climate/Human without fabricating Environment: " +
                Safe(legacySix.Failure), failures);

            for (int schema = WorldSessionPersistenceService.LegacySchemaVersion;
                 schema <= WorldSessionPersistenceService.MacroClimateSchemaVersion;
                 schema++)
            {
                JObject legacyPayload = BuildLegacyPayload(payload, schema);
                WorldSessionPersistenceResult legacy =
                    WorldSessionPersistenceService.FromPayload(legacyPayload);
                Check(legacy.Success && !legacy.Session.HasMacroEnvironment &&
                      (int)WorldSessionPersistenceService.ToPayload(legacy.Session)["schemaVersion"] == schema,
                    "Schema " + schema +
                    " must load/re-save without Environment fabrication: " +
                    Safe(legacy.Failure), failures);
            }
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
                    "Environment Size Evidence", new WorldSeed(99887766),
                    WorldGenerationSettings.ResolvePreset(size), LandCoveragePreset.High,
                    content, out WorldSession session, out string error);
                stopwatch.Stop();
                Check(built, "Environment performance fixture failed for " + size + ": " +
                    Safe(error), failures);
                if (!built) continue;
                int serializedBytes = Encoding.UTF8.GetByteCount(
                    WorldSessionPersistenceService.ToPayload(session).ToString(Formatting.None));
                int environmentRawBytes = session.MacroEnvironment.SampleCount * 4;
                measurements.Add(
                    size + "=" + stopwatch.ElapsedMilliseconds + "ms/" +
                    serializedBytes.ToString(CultureInfo.InvariantCulture) + "B payload/" +
                    environmentRawBytes.ToString(CultureInfo.InvariantCulture) +
                    "B environment raw");
            }
        }

        private static EnvironmentStatistics Analyze(GeneratedWorld world)
        {
            int columns = world.Environment.SampleColumns;
            int rows = world.Environment.SampleRows;
            int count = world.Environment.SampleCount;
            var familyCounts = new int[15];
            var transitions = new List<ushort>(count);
            int land = 0;
            int ocean = 0;
            int invalid = 0;
            int disagreements = 0;
            int neighborPairs = 0;
            int veryCold = 0;
            int veryColdMatched = 0;
            int hotDry = 0;
            int hotDryMatched = 0;
            int wetTemperate = 0;
            int wetTemperateMatched = 0;

            for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
            {
                MacroWaterSample water = world.Water.SampleAt(x, y);
                MacroClimateSample climate = world.Climate.SampleAt(x, y);
                MacroEnvironmentSample environment = world.Environment.SampleAt(x, y);
                if (water.IsOcean)
                {
                    ocean++;
                    if (environment.PrimaryBiome != MacroBiomeFamily.None ||
                        environment.SecondaryBiome != MacroBiomeFamily.None ||
                        environment.TransitionQ16 != 0) invalid++;
                    continue;
                }

                land++;
                if (environment.PrimaryBiome == MacroBiomeFamily.None ||
                    environment.SecondaryBiome == MacroBiomeFamily.None ||
                    environment.PrimaryBiome == environment.SecondaryBiome ||
                    !Enum.IsDefined(typeof(MacroBiomeFamily), environment.PrimaryBiome) ||
                    !Enum.IsDefined(typeof(MacroBiomeFamily), environment.SecondaryBiome))
                    invalid++;
                familyCounts[(byte)environment.PrimaryBiome]++;
                transitions.Add(environment.TransitionQ16);

                if (climate.ThermalIndex < 15000)
                {
                    veryCold++;
                    if (IsCold(environment.PrimaryBiome)) veryColdMatched++;
                }
                if (climate.ThermalIndex > 45000 && climate.MoistureIndex < 18000)
                {
                    hotDry++;
                    if (environment.PrimaryBiome == MacroBiomeFamily.HotDesert ||
                        environment.PrimaryBiome == MacroBiomeFamily.Savanna)
                        hotDryMatched++;
                }
                if (climate.ThermalIndex >= 25000 && climate.ThermalIndex <= 43000 &&
                    climate.MoistureIndex > 45000)
                {
                    wetTemperate++;
                    if (environment.PrimaryBiome == MacroBiomeFamily.BorealForest ||
                        environment.PrimaryBiome == MacroBiomeFamily.TemperateForest ||
                        environment.PrimaryBiome == MacroBiomeFamily.TemperateRainforest)
                        wetTemperateMatched++;
                }

                AccumulateNeighbor(x + 1, y);
                AccumulateNeighbor(x, y + 1);

                void AccumulateNeighbor(int nextX, int nextY)
                {
                    if (nextX >= columns || nextY >= rows ||
                        world.Water.SampleAt(nextX, nextY).IsOcean) return;
                    neighborPairs++;
                    if (environment.PrimaryBiome !=
                        world.Environment.SampleAt(nextX, nextY).PrimaryBiome)
                        disagreements++;
                }
            }

            int distinct = 0;
            for (int family = 1; family < familyCounts.Length; family++)
                if (familyCounts[family] > 0) distinct++;
            transitions.Sort();
            ushort transitionP50 = transitions.Count == 0 ? (ushort)0 :
                transitions[(transitions.Count - 1) / 2];
            ushort transitionP90 = transitions.Count == 0 ? (ushort)0 :
                transitions[(transitions.Count - 1) * 9 / 10];
            int boundarySamples = 0;
            for (int index = 0; index < transitions.Count; index++)
                if (transitions[index] >= 49152) boundarySamples++;

            AnalyzeComponents(
                world.Environment, world.Water,
                out int componentCount, out int largestComponent,
                out int tinyComponents, out int meanComponentSize,
                out int medianComponentSize);
            return new EnvironmentStatistics(
                land, ocean, invalid, distinct, familyCounts,
                transitionP50, transitionP90,
                RatioQ16(boundarySamples, land),
                componentCount,
                RatioQ16(largestComponent, land),
                meanComponentSize,
                medianComponentSize,
                tinyComponents,
                RatioQ16(disagreements, neighborPairs),
                veryCold, RatioQ16(veryColdMatched, veryCold),
                hotDry, RatioQ16(hotDryMatched, hotDry),
                wetTemperate, RatioQ16(wetTemperateMatched, wetTemperate));
        }

        private static void AnalyzeComponents(
            MacroEnvironmentPlan environment,
            MacroWaterPlan water,
            out int componentCount,
            out int largestComponent,
            out int tinyComponents,
            out int meanComponentSize,
            out int medianComponentSize)
        {
            var visited = new bool[environment.SampleCount];
            var queue = new Queue<int>();
            var sizes = new List<int>();
            for (int start = 0; start < environment.SampleCount; start++)
            {
                int startX = start % environment.SampleColumns;
                int startY = start / environment.SampleColumns;
                if (visited[start] || water.SampleAt(startX, startY).IsOcean) continue;
                MacroBiomeFamily family = environment.SampleAt(startX, startY).PrimaryBiome;
                visited[start] = true;
                queue.Enqueue(start);
                int size = 0;
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    size++;
                    int x = current % environment.SampleColumns;
                    int y = current / environment.SampleColumns;
                    Visit(x - 1, y);
                    Visit(x + 1, y);
                    Visit(x, y - 1);
                    Visit(x, y + 1);
                }
                sizes.Add(size);

                void Visit(int x, int y)
                {
                    if (x < 0 || x >= environment.SampleColumns ||
                        y < 0 || y >= environment.SampleRows) return;
                    int index = y * environment.SampleColumns + x;
                    if (visited[index] || water.SampleAt(x, y).IsOcean ||
                        environment.SampleAt(x, y).PrimaryBiome != family) return;
                    visited[index] = true;
                    queue.Enqueue(index);
                }
            }

            sizes.Sort();
            componentCount = sizes.Count;
            largestComponent = sizes.Count == 0 ? 0 : sizes[sizes.Count - 1];
            tinyComponents = 0;
            long total = 0;
            for (int index = 0; index < sizes.Count; index++)
            {
                total += sizes[index];
                if (sizes[index] <= 3) tinyComponents++;
            }
            meanComponentSize = sizes.Count == 0 ? 0 : (int)(total / sizes.Count);
            medianComponentSize = sizes.Count == 0 ? 0 : sizes[(sizes.Count - 1) / 2];
        }

        private static JObject BuildSchemaSixPayload(JObject current)
        {
            var schemaSix = (JObject)current.DeepClone();
            schemaSix["schemaVersion"] = WorldSessionPersistenceService.MacroClimateSchemaVersion;
            schemaSix.Remove("macroEnvironment");
            return schemaSix;
        }

        private static JObject BuildLegacyPayload(JObject current, int schema)
        {
            if (schema == WorldSessionPersistenceService.MacroClimateSchemaVersion)
                return BuildSchemaSixPayload(current);
            var legacy = new JObject
            {
                ["snapshotType"] = current["snapshotType"].DeepClone(),
                ["schemaVersion"] = schema,
                ["worldId"] = current["worldId"].DeepClone(),
                ["displayName"] = current["displayName"].DeepClone(),
                ["generationContext"] = current["generationContext"].DeepClone(),
                ["activeSectorId"] = current["activeSectorId"].DeepClone(),
                ["creationContentProvenance"] = current["creationContentProvenance"].DeepClone()
            };
            if (schema == WorldSessionPersistenceService.LegacySchemaVersion)
            {
                legacy["topology"] = current["macroWorldPlan"]["topology"].DeepClone();
                return legacy;
            }
            legacy["macroWorldPlan"] = current["macroWorldPlan"].DeepClone();
            if (schema >= WorldSessionPersistenceService.MacroGeographySchemaVersion)
                legacy["macroGeography"] = current["macroGeography"].DeepClone();
            if (schema >= WorldSessionPersistenceService.MacroWaterSchemaVersion)
                legacy["macroWater"] = current["macroWater"].DeepClone();
            if (schema >= WorldSessionPersistenceService.MacroHumanGeographySchemaVersion)
                legacy["macroHumanGeography"] = current["macroHumanGeography"].DeepClone();
            return legacy;
        }

        private static string ExportGoldenPreview()
        {
            if (!TryGenerate(
                    GoldenSeed, WorldSizePreset.Large, LandCoveragePreset.High,
                    WorldSessionBootstrap.CurrentGeneratorVersion,
                    MacroClimateGenerator.DeterministicGenerationContract,
                    MacroEnvironmentGenerator.DeterministicGenerationContract,
                    out GeneratedWorld world, out string error))
                throw new InvalidOperationException("Golden Environment preview failed: " + error);
            string directory = ResolvePreviewDirectory();
            Directory.CreateDirectory(directory);
            string path = Path.Combine(
                directory, "OldScars_MacroEnvironment_Golden_Large_High.png");
            MacroGeographyPreviewExporter.Export(
                world.Plan, world.Geography, world.Water, world.Climate,
                world.Environment, world.Quality, world.Human, world.Starter,
                path, 320, 320, true);
            return path;
        }

        private static string ResolvePreviewDirectory()
        {
            string directory = Environment.GetEnvironmentVariable(PreviewDirectoryEnvironment);
            return string.IsNullOrWhiteSpace(directory)
                ? Path.Combine(Path.GetTempPath(), "OldScars_MacroEnvironmentPreviews")
                : directory;
        }

        private static bool TryGenerate(
            long seed,
            WorldSizePreset size,
            LandCoveragePreset coverage,
            string pipelineVersion,
            string climatePassContract,
            string environmentPassContract,
            out GeneratedWorld world,
            out string error)
        {
            world = null;
            error = null;
            var context = new WorldGenerationContext(
                new WorldSeed(seed), GeneratorVersion.Parse(pipelineVersion));
            if (!MacroWorldPlanGenerator.TryGenerate(
                    context, WorldGenerationSettings.ResolvePreset(size),
                    out MacroWorldPlan plan, out error)) return false;
            if (!MacroGeographyGenerator.TryGenerate(
                    context, plan, out MacroGeographyPlan geography, out error)) return false;
            if (!MacroWaterGenerator.TryGenerate(
                    plan, geography, coverage, out MacroWaterPlan water, out error)) return false;
            bool climateBuilt = string.Equals(
                climatePassContract,
                MacroClimateGenerator.DeterministicGenerationContract,
                StringComparison.Ordinal)
                ? MacroClimateGenerator.TryGenerate(
                    context, plan, geography, water, out MacroClimatePlan climate, out error)
                : MacroClimateGenerator.TryGenerateForPassContractDiagnostics(
                    context, plan, geography, water, climatePassContract,
                    out climate, out error);
            if (!climateBuilt) return false;
            bool environmentBuilt = string.Equals(
                environmentPassContract,
                MacroEnvironmentGenerator.DeterministicGenerationContract,
                StringComparison.Ordinal)
                ? MacroEnvironmentGenerator.TryGenerate(
                    context, climate, water, out MacroEnvironmentPlan environment, out error)
                : MacroEnvironmentGenerator.TryGenerateForPassContractDiagnostics(
                    context, climate, water, environmentPassContract,
                    out environment, out error);
            if (!environmentBuilt) return false;
            if (!WorldGameplayQualityAnalyzer.TryAnalyze(
                    plan, geography, water,
                    out WorldGameplayQualityAnalysis quality, out error)) return false;
            if (!WorldStarterSectorSelector.TrySelect(
                    quality, out SectorId starter, out error)) return false;
            if (!MacroHumanGeographyGenerator.TryGenerate(
                    context, plan, geography, water, quality, starter,
                    out MacroHumanGeographyPlan human, out error)) return false;
            world = new GeneratedWorld(
                context, plan, geography, water, climate, environment, quality, starter, human);
            return true;
        }

        private static bool SessionsHaveSameCommittedTruth(
            WorldSession expected,
            WorldSession actual)
        {
            return expected != null && actual != null &&
                   expected.WorldId == actual.WorldId &&
                   expected.GenerationContext.WorldSeed == actual.GenerationContext.WorldSeed &&
                   expected.GenerationContext.GeneratorVersion == actual.GenerationContext.GeneratorVersion &&
                   expected.MacroWorldPlan.CanonicalHash == actual.MacroWorldPlan.CanonicalHash &&
                   expected.MacroGeography.CanonicalHash == actual.MacroGeography.CanonicalHash &&
                   expected.MacroWater.CanonicalHash == actual.MacroWater.CanonicalHash &&
                   expected.MacroClimate.CanonicalHash == actual.MacroClimate.CanonicalHash &&
                   expected.MacroEnvironment.CanonicalHash == actual.MacroEnvironment.CanonicalHash &&
                   expected.MacroHumanGeography.CanonicalHash == actual.MacroHumanGeography.CanonicalHash &&
                   expected.Topology.CanonicalHash == actual.Topology.CanonicalHash &&
                   expected.ActiveSectorId == actual.ActiveSectorId;
        }

        private static bool AllHashesEqual(GeneratedWorld first, GeneratedWorld second)
        {
            return UpstreamAndHumanEqual(first, second) &&
                   first.Environment.CanonicalHash == second.Environment.CanonicalHash &&
                   first.Starter == second.Starter;
        }

        private static bool UpstreamAndHumanEqual(GeneratedWorld first, GeneratedWorld second)
        {
            return first.Plan.CanonicalHash == second.Plan.CanonicalHash &&
                   first.Geography.CanonicalHash == second.Geography.CanonicalHash &&
                   first.Water.CanonicalHash == second.Water.CanonicalHash &&
                   first.Climate.CanonicalHash == second.Climate.CanonicalHash &&
                   first.Human.CanonicalHash == second.Human.CanonicalHash &&
                   first.Starter == second.Starter;
        }

        private static int FindFirstSample(MacroWaterPlan water, bool ocean)
        {
            for (int index = 0; index < water.SampleCount; index++)
                if (water.SampleAt(index % water.SampleColumns, index / water.SampleColumns).IsOcean == ocean)
                    return index;
            throw new InvalidOperationException("Expected water fixture sample was not found.");
        }

        private static bool IsCold(MacroBiomeFamily family)
        {
            return family == MacroBiomeFamily.PolarBarrens ||
                   family == MacroBiomeFamily.Tundra ||
                   family == MacroBiomeFamily.ColdDesert ||
                   family == MacroBiomeFamily.ColdSteppe ||
                   family == MacroBiomeFamily.BorealForest;
        }

        private static int RatioQ16(int numerator, int denominator)
        {
            return denominator <= 0 ? 0 :
                (int)Math.Min(ushort.MaxValue, (long)numerator * ushort.MaxValue / denominator);
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

        private static void Complete(
            string title,
            IList<string> failures,
            params string[] evidence)
        {
            if (failures.Count > 0)
            {
                string failure = title + ": FAIL\n- " + string.Join("\n- ", failures);
                Debug.LogError(failure);
                throw new InvalidOperationException(failure);
            }
            Debug.Log(title + ": PASS\n" + string.Join("\n", evidence));
        }

        private static void Check(
            bool condition,
            string failure,
            ICollection<string> failures)
        {
            if (!condition) failures.Add(failure);
        }

        private static string Safe(string value) =>
            string.IsNullOrEmpty(value) ? "<NONE>" : value;

        private sealed class GeneratedWorld
        {
            public GeneratedWorld(
                WorldGenerationContext context,
                MacroWorldPlan plan,
                MacroGeographyPlan geography,
                MacroWaterPlan water,
                MacroClimatePlan climate,
                MacroEnvironmentPlan environment,
                WorldGameplayQualityAnalysis quality,
                SectorId starter,
                MacroHumanGeographyPlan human)
            {
                Context = context;
                Plan = plan;
                Geography = geography;
                Water = water;
                Climate = climate;
                Environment = environment;
                Quality = quality;
                Starter = starter;
                Human = human;
            }

            public WorldGenerationContext Context { get; }
            public MacroWorldPlan Plan { get; }
            public MacroGeographyPlan Geography { get; }
            public MacroWaterPlan Water { get; }
            public MacroClimatePlan Climate { get; }
            public MacroEnvironmentPlan Environment { get; }
            public WorldGameplayQualityAnalysis Quality { get; }
            public SectorId Starter { get; }
            public MacroHumanGeographyPlan Human { get; }
        }

        private sealed class EnvironmentStatistics
        {
            public EnvironmentStatistics(
                int landSamples,
                int oceanSamples,
                int invalidSamples,
                int distinctPrimaryFamilies,
                int[] familyCounts,
                ushort transitionP50,
                ushort transitionP90,
                int boundarySampleRatioQ16,
                int componentCount,
                int largestComponentRatioQ16,
                int meanComponentSize,
                int medianComponentSize,
                int tinyComponentCount,
                int neighborDisagreementQ16,
                int veryColdSamples,
                int coldCorrelationQ16,
                int hotDrySamples,
                int hotDryCorrelationQ16,
                int wetTemperateSamples,
                int wetTemperateCorrelationQ16)
            {
                LandSamples = landSamples;
                OceanSamples = oceanSamples;
                InvalidSamples = invalidSamples;
                DistinctPrimaryFamilies = distinctPrimaryFamilies;
                FamilyCounts = familyCounts;
                TransitionP50 = transitionP50;
                TransitionP90 = transitionP90;
                BoundarySampleRatioQ16 = boundarySampleRatioQ16;
                ComponentCount = componentCount;
                LargestComponentRatioQ16 = largestComponentRatioQ16;
                MeanComponentSize = meanComponentSize;
                MedianComponentSize = medianComponentSize;
                TinyComponentCount = tinyComponentCount;
                NeighborDisagreementQ16 = neighborDisagreementQ16;
                VeryColdSamples = veryColdSamples;
                ColdCorrelationQ16 = coldCorrelationQ16;
                HotDrySamples = hotDrySamples;
                HotDryCorrelationQ16 = hotDryCorrelationQ16;
                WetTemperateSamples = wetTemperateSamples;
                WetTemperateCorrelationQ16 = wetTemperateCorrelationQ16;
            }

            public int LandSamples { get; }
            public int OceanSamples { get; }
            public int InvalidSamples { get; }
            public int DistinctPrimaryFamilies { get; }
            public int[] FamilyCounts { get; }
            public ushort TransitionP50 { get; }
            public ushort TransitionP90 { get; }
            public int BoundarySampleRatioQ16 { get; }
            public int ComponentCount { get; }
            public int LargestComponentRatioQ16 { get; }
            public int MeanComponentSize { get; }
            public int MedianComponentSize { get; }
            public int TinyComponentCount { get; }
            public int NeighborDisagreementQ16 { get; }
            public int VeryColdSamples { get; }
            public int ColdCorrelationQ16 { get; }
            public int HotDrySamples { get; }
            public int HotDryCorrelationQ16 { get; }
            public int WetTemperateSamples { get; }
            public int WetTemperateCorrelationQ16 { get; }

            public string Describe()
            {
                var distribution = new List<string>();
                for (int family = 1; family < FamilyCounts.Length; family++)
                {
                    if (FamilyCounts[family] == 0) continue;
                    distribution.Add(
                        MacroEnvironmentGenerationSettings.ToCanonical((MacroBiomeFamily)family) + "=" +
                        RatioQ16(FamilyCounts[family], LandSamples));
                }
                return "land/ocean=" + LandSamples + "/" + OceanSamples +
                       " families=" + DistinctPrimaryFamilies +
                       " distributionQ16=[" + string.Join(",", distribution) + "]" +
                       " transitionP50/P90=" + TransitionP50 + "/" + TransitionP90 +
                       " boundaryQ16=" + BoundarySampleRatioQ16 +
                       " components=" + ComponentCount +
                       " largestQ16=" + LargestComponentRatioQ16 +
                       " componentMean/Median=" + MeanComponentSize + "/" + MedianComponentSize +
                       " tiny=" + TinyComponentCount +
                       " neighborDisagreementQ16=" + NeighborDisagreementQ16 +
                       " correlationCold/HotDry/WetTemp=" + ColdCorrelationQ16 + "/" +
                       HotDryCorrelationQ16 + "/" + WetTemperateCorrelationQ16;
            }
        }
    }
}
