using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using OldScars.Core.Data.Loading;
using OldScars.Core.Data.Validation;
using OldScars.Core.Persistence;
using OldScars.Core.World;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace OldScars.EditorTools
{
    public static class MacroElevationLandformsDiagnostics
    {
        private const long GoldenSeed = 8675309123456789L;
        private const string GoldenGeographyHash =
            "7ea378c2fb710ad3c6ad8ebe98f71663cdfd4b4903c5213dc078afc6f229d343";
        private const int FuzzSeedsPerPreset = 8;

        public static void Run()
        {
            var failures = new List<string>();
            var performance = new List<string>();
            string root = Path.Combine(
                Path.GetTempPath(), "OldScars_MacroGeography_" + Guid.NewGuid().ToString("N"));
            string previewPath = Path.Combine(root, "diagnostic-preview.png");
            try
            {
                LoadedContentSet content = LoadValidatedCore(failures);
                ValidateDeterminismAndWorldId(content, failures);
                ValidateFieldAndContinuity(failures);
                ValidateOrderGoldenAndFuzz(failures);
                ValidatePersistenceAndLegacy(root, content, failures);
                ValidatePreview(previewPath, failures);
                MeasurePresets(performance, failures);
            }
            catch (Exception exception)
            {
                failures.Add("Diagnostic threw " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                WorldSessionService.Close();
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }

            if (failures.Count > 0)
            {
                string failure = "Macro Elevation / Landforms V1 Diagnostics: FAIL\n- " +
                                 string.Join("\n- ", failures);
                Debug.LogError(failure);
                throw new InvalidOperationException(failure);
            }

            Debug.Log(
                "Macro Elevation / Landforms V1 Diagnostics: PASS\n" +
                "- deterministic multi-field fixed-point geography; WorldId independent\n" +
                "- exact finite bounds, boundary-safe interpolation and shared world-coordinate queries\n" +
                "- Plains / RollingHills / Highlands / Mountains variety and regional coherence\n" +
                "- insertion/order independence and no sector/topology terrain authority\n" +
                "- golden geography hash: " + GoldenGeographyHash + "\n" +
                "- fuzz: " + FuzzSeedsPerPreset + " seeds x 4 size presets\n" +
                "- schema-4 session preserves committed geography; schemas 1/2 remain explicit legacy\n" +
                "- diagnostic elevation/landform PNG export succeeded and temporary file was removed\n" +
                "- approximate plan + geography timings: " + string.Join("; ", performance));
        }

        private static void ValidateDeterminismAndWorldId(
            LoadedContentSet content,
            List<string> failures)
        {
            Generate(
                GoldenSeed, WorldSizePreset.Large,
                out MacroWorldPlan firstPlan, out MacroGeographyPlan first, failures, "A first");
            Generate(
                GoldenSeed, WorldSizePreset.Large,
                out _, out MacroGeographyPlan repeated, failures, "A repeated");
            Check(first != null && repeated != null &&
                  first.CanonicalHash == repeated.CanonicalHash,
                "A. Same seed/settings must produce exact same elevation/landforms evidence.", failures);

            if (content != null)
            {
                WorldGenerationSettings settings =
                    WorldGenerationSettings.ResolvePreset(WorldSizePreset.Large);
                bool builtA = WorldSessionBootstrap.TryBuildNew(
                    "Geography A", new WorldSeed(GoldenSeed), settings, content,
                    out WorldSession worldA, out string errorA);
                bool builtB = WorldSessionBootstrap.TryBuildNew(
                    "Geography B", new WorldSeed(GoldenSeed), settings, content,
                    out WorldSession worldB, out string errorB);
                Check(builtA && builtB && worldA.WorldId != worldB.WorldId &&
                      worldA.MacroWorldPlan.CanonicalHash == worldB.MacroWorldPlan.CanonicalHash &&
                      worldA.MacroGeography.CanonicalHash == worldB.MacroGeography.CanonicalHash,
                    "B. WorldId must not alter macro plan/geography. " + Safe(errorA) + " " + Safe(errorB),
                    failures);
            }

            Generate(
                GoldenSeed + 1, WorldSizePreset.Large,
                out _, out MacroGeographyPlan different, failures, "C different seed");
            Check(first != null && different != null &&
                  first.CanonicalHash != different.CanonicalHash,
                "C. Different seed must produce different macro geography.", failures);
            Check(firstPlan != null && first != null && first.WorldBounds == firstPlan.WorldBounds,
                "D. Geography must cover the exact MacroWorldPlan finite bounds.", failures);
        }

        private static void ValidateFieldAndContinuity(List<string> failures)
        {
            Generate(
                -3141592653589793L, WorldSizePreset.Huge,
                out MacroWorldPlan plan, out MacroGeographyPlan geography, failures, "D-K field");
            if (plan == null || geography == null)
                return;

            FiniteMacroWorldBounds bounds = plan.WorldBounds;
            var minimum = new MacroPoint2D(bounds.MinX, bounds.MinY);
            var maximum = new MacroPoint2D(bounds.MaxXExclusive - 1, bounds.MaxYExclusive - 1);
            var outsideX = new MacroPoint2D(bounds.MaxXExclusive, bounds.MinY);
            var center = new MacroPoint2D(
                bounds.MinX + bounds.Width / 2,
                bounds.MinY + bounds.Height / 2);
            Check(geography.TrySampleAt(minimum, out MacroGeographySample minimumSample) &&
                  geography.TrySampleAt(maximum, out MacroGeographySample maximumSample) &&
                  !geography.TrySampleAt(outsideX, out _) &&
                  geography.TrySampleAt(center, out MacroGeographySample centerFirst) &&
                  geography.TrySampleAt(center, out MacroGeographySample centerSecond) &&
                  centerFirst.Elevation == centerSecond.Elevation &&
                  centerFirst.Landform == centerSecond.Landform &&
                  Enum.IsDefined(typeof(MacroLandform), minimumSample.Landform) &&
                  Enum.IsDefined(typeof(MacroLandform), maximumSample.Landform),
                "D/E. Field extent/interpolation queries must be deterministic and boundary-safe.", failures);

            Check(plan.Topology.Connections.Count > 0,
                "F. Continuity fixture requires at least one logical sector relation.", failures);
            if (plan.Topology.Connections.Count > 0)
            {
                SectorConnection connection = plan.Topology.Connections[0];
                bool foundA = plan.TryGetSectorPlacement(connection.FirstEndpoint, out MacroSectorPlacement a);
                bool foundB = plan.TryGetSectorPlacement(connection.SecondEndpoint, out MacroSectorPlacement b);
                if (foundA && foundB)
                {
                    var midpoint = new MacroPoint2D(
                        a.Position.X + (b.Position.X - a.Position.X) / 2,
                        a.Position.Y + (b.Position.Y - a.Position.Y) / 2);
                    long dx = Math.Sign(b.Position.X - a.Position.X);
                    long dy = Math.Sign(b.Position.Y - a.Position.Y);
                    var before = new MacroPoint2D(midpoint.X - dx, midpoint.Y - dy);
                    var after = new MacroPoint2D(midpoint.X + dx, midpoint.Y + dy);
                    Check(geography.TrySampleAt(before, out MacroGeographySample beforeSample) &&
                          geography.TrySampleAt(after, out MacroGeographySample afterSample) &&
                          Math.Abs(beforeSample.Elevation - afterSample.Elevation) < 1000,
                        "F. Opposite sides of a future sector relation must sample one continuous global field.",
                        failures);
                }
                else
                {
                    failures.Add("F. Connected sector placements could not be resolved from MacroWorldPlan.");
                }
            }

            MacroGeographyAnalysis analysis = geography.Analyze();
            Check(analysis.MeetsCommittedVariety(out string varietyFailure),
                "G/J. Regional landform variety/coherence failed: " + Safe(varietyFailure), failures);
            Check(analysis.MountainsCount > 0 && analysis.LargestMountainRegion >=
                  Math.Max(8, analysis.SampleCount / 160),
                "H. Meaningful connected mountain regions must exist.", failures);
            Check(analysis.PlainsCount > 0 && analysis.LargestPlainsRegion >=
                  Math.Max(12, analysis.SampleCount / 80),
                "I. Broad connected plains regions must exist.", failures);
            Check(analysis.MinimumElevation < analysis.MaximumElevation &&
                  analysis.MaximumElevation - analysis.MinimumElevation >= 10000 &&
                  analysis.AverageMountainRoughness > analysis.AveragePlainsRoughness,
                "K. Elevation range and landform-specific roughness invariants must hold.", failures);
        }

        private static void ValidateOrderGoldenAndFuzz(List<string> failures)
        {
            Generate(
                GoldenSeed, WorldSizePreset.Large,
                out MacroWorldPlan goldenPlan, out MacroGeographyPlan golden, failures, "M golden");
            Check(golden != null && golden.CanonicalHash == GoldenGeographyHash,
                "M. Golden geography evidence drifted. Expected " + GoldenGeographyHash +
                ", got " + (golden == null ? "<NULL>" : golden.CanonicalHash) + ".", failures);

            if (goldenPlan != null && golden != null)
            {
                var reversedPlacements = new List<MacroSectorPlacement>();
                for (int index = goldenPlan.SectorPlacements.Count - 1; index >= 0; index--)
                    reversedPlacements.Add(goldenPlan.SectorPlacements[index]);
                var reversedSectors = new List<SectorId>();
                for (int index = goldenPlan.Topology.Sectors.Count - 1; index >= 0; index--)
                    reversedSectors.Add(goldenPlan.Topology.Sectors[index]);
                var reversedConnections = new List<SectorConnection>();
                for (int index = goldenPlan.Topology.Connections.Count - 1; index >= 0; index--)
                    reversedConnections.Add(goldenPlan.Topology.Connections[index]);
                bool topologyBuilt = WorldTopology.TryCreate(
                    reversedSectors, reversedConnections,
                    out WorldTopology reversedTopology, out WorldTopologyValidationResult topologyValidation);
                MacroWorldPlan reorderedPlan = null;
                string planError = null;
                bool planBuilt = topologyBuilt && MacroWorldPlan.TryCreate(
                    goldenPlan.GenerationSettings, goldenPlan.WorldBounds,
                    reversedPlacements, reversedTopology,
                    out reorderedPlan, out planError);
                MacroGeographyPlan reorderedGeography = null;
                string geographyError = null;
                bool geographyBuilt = planBuilt && MacroGeographyGenerator.TryGenerate(
                    Context(new WorldSeed(GoldenSeed)), reorderedPlan,
                    out reorderedGeography, out geographyError);
                Check(geographyBuilt && reorderedGeography.CanonicalHash == golden.CanonicalHash,
                    "L. Geography must be independent of placement/topology insertion order. " +
                    topologyValidation.Description + " " + Safe(planError) + " " + Safe(geographyError),
                    failures);
            }

            WorldSizePreset[] presets = Presets();
            for (int presetIndex = 0; presetIndex < presets.Length; presetIndex++)
            {
                for (int seedIndex = 0; seedIndex < FuzzSeedsPerPreset; seedIndex++)
                {
                    long seed = unchecked(
                        (long)0x734f209e8a61c5d7UL + presetIndex * 1000003L + seedIndex * 7919L);
                    string label = "N fuzz " + presets[presetIndex] + " seed " + seedIndex;
                    Generate(seed, presets[presetIndex], out MacroWorldPlan plan,
                        out MacroGeographyPlan geography, failures, label);
                    if (plan == null || geography == null)
                        continue;
                    MacroGeographyAnalysis analysis = geography.Analyze();
                    Check(geography.WorldBounds == plan.WorldBounds &&
                          analysis.MeetsCommittedVariety(out _),
                        label + " violated finite bounds/variety invariants.", failures);
                }
            }
        }

        private static void ValidatePersistenceAndLegacy(
            string root,
            LoadedContentSet content,
            List<string> failures)
        {
            if (content == null)
                return;
            var store = new PersistenceFileStore(root);
            WorldSessionService.Close();
            WorldSessionOperationResult create = WorldSessionService.Create(
                "Geography Round Trip", new WorldSeed(20260824),
                WorldGenerationSettings.ResolvePreset(WorldSizePreset.Medium), content, store);
            Check(create.Success, "O. Schema-3 creation failed: " + Safe(create.Failure), failures);
            if (!create.Success)
                return;

            WorldSession expected = create.Session;
            JToken currentPayload = WorldSessionPersistenceService.ToPayload(expected);
            Check((int)currentPayload["schemaVersion"] == WorldSessionPersistenceService.CurrentSchemaVersion &&
                  currentPayload["macroGeography"] != null,
                "O. Current World Session must use schema 4 with committed macro geography.", failures);
            WorldSessionService.Close();
            WorldSessionPersistenceResult read =
                WorldSessionPersistenceService.Read(expected.WorldId.Canonical, store);
            Check(read.Success && read.Session.HasMacroGeography &&
                  read.Session.WorldId == expected.WorldId &&
                  read.Session.MacroWorldPlan.CanonicalHash == expected.MacroWorldPlan.CanonicalHash &&
                  read.Session.MacroGeography.CanonicalHash == expected.MacroGeography.CanonicalHash &&
                  read.Session.ActiveSectorId == expected.ActiveSectorId,
                "O. Schema-3 save/read must reconstruct exact committed geography. " + Safe(read.Failure),
                failures);

            JObject invalid = (JObject)currentPayload.DeepClone();
            invalid["macroGeography"]["elevationSamplesBase64"] = "AA==";
            Check(!WorldSessionPersistenceService.FromPayload(invalid).Success,
                "O. Corrupt committed geography bytes must fail before session publication.", failures);

            JObject schemaTwo = (JObject)currentPayload.DeepClone();
            schemaTwo["schemaVersion"] = WorldSessionPersistenceService.MacroPlanSchemaVersion;
            schemaTwo.Remove("macroGeography");
            schemaTwo.Remove("macroWater");
            WorldSessionPersistenceResult legacyTwo =
                WorldSessionPersistenceService.FromPayload(schemaTwo);
            Check(legacyTwo.Success && legacyTwo.Session.IsLegacySchemaV2 &&
                  !legacyTwo.Session.HasMacroGeography &&
                  (int)WorldSessionPersistenceService.ToPayload(legacyTwo.Session)["schemaVersion"] ==
                  WorldSessionPersistenceService.MacroPlanSchemaVersion,
                "P. Schema 2 must load/re-save without fabricated geography. " + Safe(legacyTwo.Failure),
                failures);
        }

        private static void ValidatePreview(string previewPath, List<string> failures)
        {
            Generate(
                GoldenSeed, WorldSizePreset.Large,
                out MacroWorldPlan plan, out MacroGeographyPlan geography, failures, "preview");
            if (plan == null || geography == null)
                return;
            MacroGeographyPreviewExporter.Export(plan, geography, previewPath, 256, 256, true);
            Check(File.Exists(previewPath) && new FileInfo(previewPath).Length > 4096,
                "Debug preview PNG must export non-empty elevation/landform panels.", failures);
        }

        private static void MeasurePresets(List<string> performance, List<string> failures)
        {
            WorldSizePreset[] presets = Presets();
            for (int index = 0; index < presets.Length; index++)
            {
                var stopwatch = Stopwatch.StartNew();
                Generate(99887766, presets[index], out _, out MacroGeographyPlan geography,
                    failures, "R performance " + presets[index]);
                stopwatch.Stop();
                if (geography != null)
                {
                    long committedBytes = geography.SampleCount * 3L;
                    performance.Add(
                        presets[index] + "=" + stopwatch.ElapsedMilliseconds + "ms/" +
                        geography.SampleColumns + "x" + geography.SampleRows + "/" +
                        committedBytes + " raw bytes");
                }
            }
        }

        private static void Generate(
            long seed,
            WorldSizePreset preset,
            out MacroWorldPlan plan,
            out MacroGeographyPlan geography,
            List<string> failures,
            string label)
        {
            WorldGenerationContext context = Context(new WorldSeed(seed));
            if (!MacroWorldPlanGenerator.TryGenerate(
                    context, WorldGenerationSettings.ResolvePreset(preset),
                    out plan, out string planError))
            {
                failures.Add(label + " MacroWorldPlan failed: " + planError);
                geography = null;
                return;
            }
            if (!MacroGeographyGenerator.TryGenerate(
                    context, plan, out geography, out string geographyError))
            {
                failures.Add(label + " macro geography failed: " + geographyError);
            }
        }

        private static WorldGenerationContext Context(WorldSeed seed)
        {
            return new WorldGenerationContext(
                seed, GeneratorVersion.Parse(WorldSessionBootstrap.CurrentGeneratorVersion));
        }

        private static WorldSizePreset[] Presets()
        {
            return new[]
            {
                WorldSizePreset.Small,
                WorldSizePreset.Medium,
                WorldSizePreset.Large,
                WorldSizePreset.Huge
            };
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
                failures.Add("Real Core content failed loader/DataValidator/provenance validation.");
                return null;
            }
            return content;
        }

        private static void Check(bool condition, string failure, List<string> failures)
        {
            if (!condition)
                failures.Add(failure);
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "<NO DETAIL>" : value;
        }
    }
}
