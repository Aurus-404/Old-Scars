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
    public static class MacroWorldPlanDiagnostics
    {
        private const long GoldenSeed = 8675309123456789L;
        private const string GoldenPlanHash =
            "3f300ba2129962493d2ab8f2ad6ec0863e96aa0ceeb400f9899f91889a34e91a";
        private const int FuzzSeedsPerPreset = 12;

        public static void Run()
        {
            var failures = new List<string>();
            var performance = new List<string>();
            string root = Path.Combine(
                Path.GetTempPath(), "OldScars_MacroWorldPlan_" + Guid.NewGuid().ToString("N"));
            try
            {
                LoadedContentSet content = LoadValidatedCore(failures);
                ValidateDeterminismSizesAndWorldId(content, failures);
                ValidatePlanInvariantsAndOrder(failures);
                ValidateGoldenAndFuzz(failures);
                ValidatePersistenceAndLegacy(root, content, failures);
                MeasurePresetGeneration(performance, failures);
            }
            catch (Exception exception)
            {
                failures.Add($"Diagnostic threw {exception.GetType().Name}: {exception.Message}");
            }
            finally
            {
                WorldSessionService.Close();
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }

            if (failures.Count > 0)
            {
                string failure = "Macro World Plan V1 Diagnostics: FAIL\n- " +
                                 string.Join("\n- ", failures);
                Debug.LogError(failure);
                throw new InvalidOperationException(failure);
            }

            Debug.Log(
                "Macro World Plan V1 Diagnostics: PASS\n" +
                "- same seed/settings and WorldId independence\n" +
                "- Small < Medium < Large < Huge resolved scale\n" +
                "- finite bounds, unique placements, spacing and connected spatial topology\n" +
                "- insertion-order-independent canonical plan evidence\n" +
                $"- golden plan hash: {GoldenPlanHash}\n" +
                $"- fuzz: {FuzzSeedsPerPreset} seeds x 4 presets\n" +
                "- schema-4 save round-trip plus explicit schema-1/schema-2 legacy paths\n" +
                "- approximate generation timings: " + string.Join("; ", performance) + "\n" +
                "- temporary persistence fixtures removed");
        }

        private static void ValidateDeterminismSizesAndWorldId(
            LoadedContentSet content,
            List<string> failures)
        {
            var context = Context(new WorldSeed(GoldenSeed));
            MacroWorldPlan first = Generate(context, WorldSizePreset.Large, failures, "A same-input first");
            MacroWorldPlan repeated = Generate(context, WorldSizePreset.Large, failures, "A same-input repeat");
            Check(first != null && repeated != null &&
                  first.CanonicalHash == repeated.CanonicalHash &&
                  first.CanonicalDescription == repeated.CanonicalDescription,
                "A. Same seed/version/settings must produce the exact same MacroWorldPlan.", failures);

            if (content != null)
            {
                WorldGenerationSettings settings = WorldGenerationSettings.ResolvePreset(WorldSizePreset.Large);
                bool firstBuilt = WorldSessionBootstrap.TryBuildNew(
                    "World A", context.WorldSeed, settings, content,
                    out WorldSession worldA, out string firstError);
                bool secondBuilt = WorldSessionBootstrap.TryBuildNew(
                    "World B", context.WorldSeed, settings, content,
                    out WorldSession worldB, out string secondError);
                Check(firstBuilt && secondBuilt &&
                      worldA.WorldId != worldB.WorldId &&
                      worldA.MacroWorldPlan.CanonicalHash == worldB.MacroWorldPlan.CanonicalHash,
                    "B. Different WorldIds with the same seed/settings must retain the same plan. " +
                    Safe(firstError) + " " + Safe(secondError), failures);
            }

            WorldGenerationSettings small = WorldGenerationSettings.ResolvePreset(WorldSizePreset.Small);
            WorldGenerationSettings medium = WorldGenerationSettings.ResolvePreset(WorldSizePreset.Medium);
            WorldGenerationSettings large = WorldGenerationSettings.ResolvePreset(WorldSizePreset.Large);
            WorldGenerationSettings huge = WorldGenerationSettings.ResolvePreset(WorldSizePreset.Huge);
            Check(small.ResolvedSectorCount < medium.ResolvedSectorCount &&
                  medium.ResolvedSectorCount < large.ResolvedSectorCount &&
                  large.ResolvedSectorCount < huge.ResolvedSectorCount &&
                  small.ResolvedWorldWidth < medium.ResolvedWorldWidth &&
                  medium.ResolvedWorldWidth < large.ResolvedWorldWidth &&
                  large.ResolvedWorldWidth < huge.ResolvedWorldWidth,
                "C. Small < Medium < Large < Huge must be real resolved logical scale differences.", failures);

            MacroWorldPlan smallPlan = Generate(context, WorldSizePreset.Small, failures, "D small");
            MacroWorldPlan hugePlan = Generate(context, WorldSizePreset.Huge, failures, "D huge");
            Check(smallPlan != null && hugePlan != null &&
                  smallPlan.CanonicalHash != hugePlan.CanonicalHash &&
                  smallPlan.SectorPlacements.Count < hugePlan.SectorPlacements.Count,
                "D. Different selected sizes with the same seed must produce different valid plans.", failures);
        }

        private static void ValidatePlanInvariantsAndOrder(List<string> failures)
        {
            MacroWorldPlan plan = Generate(
                Context(new WorldSeed(-1234567890L)), WorldSizePreset.Huge, failures, "E-H baseline");
            if (plan == null)
                return;

            CheckPlanInvariants(plan, "E-H baseline", failures);
            var reversed = new List<MacroSectorPlacement>(plan.SectorPlacements.Count);
            for (int index = plan.SectorPlacements.Count - 1; index >= 0; index--)
                reversed.Add(plan.SectorPlacements[index]);
            Check(MacroWorldPlan.TryCreate(
                      plan.GenerationSettings,
                      plan.WorldBounds,
                      reversed,
                      plan.Topology,
                      out MacroWorldPlan reordered,
                      out string reorderError) &&
                  reordered.CanonicalHash == plan.CanonicalHash &&
                  reordered.CanonicalDescription == plan.CanonicalDescription,
                "H. MacroWorldPlan canonical evidence must be independent of placement insertion order. " +
                Safe(reorderError), failures);
        }

        private static void ValidateGoldenAndFuzz(List<string> failures)
        {
            MacroWorldPlan golden = Generate(
                Context(new WorldSeed(GoldenSeed)), WorldSizePreset.Large, failures, "I golden");
            Check(golden != null && golden.CanonicalHash == GoldenPlanHash,
                $"I. Golden MacroWorldPlan hash drifted. Expected {GoldenPlanHash}, " +
                $"got {golden?.CanonicalHash ?? "<NULL>"}.", failures);

            WorldSizePreset[] presets =
            {
                WorldSizePreset.Small,
                WorldSizePreset.Medium,
                WorldSizePreset.Large,
                WorldSizePreset.Huge
            };
            for (int presetIndex = 0; presetIndex < presets.Length; presetIndex++)
            {
                for (int seedIndex = 0; seedIndex < FuzzSeedsPerPreset; seedIndex++)
                {
                    long value = unchecked(
                        (long)0x5a17d3c4b289e601UL + presetIndex * 1000003L + seedIndex * 7919L);
                    string label = $"J fuzz {presets[presetIndex]} seed {seedIndex}";
                    MacroWorldPlan plan = Generate(
                        Context(new WorldSeed(value)), presets[presetIndex], failures, label);
                    if (plan != null)
                        CheckPlanInvariants(plan, label, failures);
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
                "Macro Round Trip",
                new WorldSeed(20260824),
                WorldGenerationSettings.ResolvePreset(WorldSizePreset.Medium),
                content,
                store);
            Check(create.Success, "K. Macro World Session creation/save failed: " + Safe(create.Failure), failures);
            if (!create.Success)
                return;
            WorldSession expected = create.Session;
            WorldSessionService.Close();
            WorldSessionPersistenceResult read = WorldSessionPersistenceService.Read(
                expected.WorldId.Canonical, store);
            Check(read.Success && read.Session.HasMacroWorldPlan &&
                  read.Session.HasMacroGeography &&
                  read.Session.WorldId == expected.WorldId &&
                  read.Session.GenerationContext.WorldSeed == expected.GenerationContext.WorldSeed &&
                  read.Session.MacroWorldPlan.CanonicalHash == expected.MacroWorldPlan.CanonicalHash &&
                  read.Session.MacroGeography.CanonicalHash == expected.MacroGeography.CanonicalHash &&
                  read.Session.Topology.CanonicalHash == expected.Topology.CanonicalHash &&
                  read.Session.ActiveSectorId == expected.ActiveSectorId,
                "K. Schema-3 save/read must reconstruct the identical logical plan/geography/session. " +
                Safe(read.Failure), failures);

            JToken current = WorldSessionPersistenceService.ToPayload(expected);
            JObject schemaTwoPayload = (JObject)current.DeepClone();
            schemaTwoPayload["schemaVersion"] = WorldSessionPersistenceService.MacroPlanSchemaVersion;
            schemaTwoPayload.Remove("macroGeography");
            schemaTwoPayload.Remove("macroWater");
            schemaTwoPayload.Remove("macroHumanGeography");
            WorldSessionPersistenceResult schemaTwoRead =
                WorldSessionPersistenceService.FromPayload(schemaTwoPayload);
            Check(schemaTwoRead.Success && schemaTwoRead.Session.IsLegacySchemaV2 &&
                  schemaTwoRead.Session.HasMacroWorldPlan &&
                  !schemaTwoRead.Session.HasMacroGeography &&
                  schemaTwoRead.Session.MacroWorldPlan.CanonicalHash ==
                  expected.MacroWorldPlan.CanonicalHash &&
                  (int)WorldSessionPersistenceService.ToPayload(schemaTwoRead.Session)["schemaVersion"] ==
                  WorldSessionPersistenceService.MacroPlanSchemaVersion &&
                  WorldSessionPersistenceService.ToPayload(schemaTwoRead.Session)["macroGeography"] == null,
                "L. Existing schema-2 world must load and re-save without fabricated macro geography. " +
                Safe(schemaTwoRead.Failure), failures);

            var legacyContext = new WorldGenerationContext(
                new WorldSeed(77), GeneratorVersion.Parse(WorldSessionBootstrap.LegacyGeneratorVersion));
            SectorId legacySector = SectorId.FromDeterministicDomain(
                WorldDeterminism.DerivePassDomainKey(
                    legacyContext.WorldSeed,
                    WorldSessionBootstrap.LegacyGeneratorVersion,
                    "topology",
                    "starter_sector"));
            Check(WorldTopology.TryCreate(
                      new[] { legacySector }, Array.Empty<SectorConnection>(),
                      out WorldTopology legacyTopology, out WorldTopologyValidationResult legacyValidation),
                "L. Schema-1 legacy fixture topology failed: " + legacyValidation.Description, failures);
            if (legacyTopology == null)
                return;
            JObject legacyPayload = BuildLegacyPayload(
                legacyContext, legacySector, legacyTopology,
                current["creationContentProvenance"].DeepClone());
            WorldSessionPersistenceResult legacyRead = WorldSessionPersistenceService.FromPayload(legacyPayload);
            Check(legacyRead.Success && legacyRead.Session.IsLegacySchemaV1 &&
                  !legacyRead.Session.HasMacroWorldPlan &&
                  legacyRead.Session.Topology.CanonicalHash == legacyTopology.CanonicalHash &&
                  (int)WorldSessionPersistenceService.ToPayload(legacyRead.Session)["schemaVersion"] ==
                  WorldSessionPersistenceService.LegacySchemaVersion &&
                  WorldSessionPersistenceService.ToPayload(legacyRead.Session)["macroWorldPlan"] == null,
                "L. Existing schema-1 world must load and re-save through the explicit legacy path " +
                "without a fabricated size/plan. " + Safe(legacyRead.Failure), failures);
        }

        private static JObject BuildLegacyPayload(
            WorldGenerationContext context,
            SectorId sector,
            WorldTopology topology,
            JToken contentEvidence)
        {
            return new JObject
            {
                ["snapshotType"] = WorldSessionPersistenceService.SnapshotType,
                ["schemaVersion"] = WorldSessionPersistenceService.LegacySchemaVersion,
                ["worldId"] = "world_0123456789abcdef0123456789abcdef",
                ["displayName"] = "Legacy Schema One",
                ["generationContext"] = new JObject
                {
                    ["worldSeed"] = context.WorldSeed.Canonical,
                    ["generatorVersion"] = context.GeneratorVersion.Canonical
                },
                ["topology"] = new JObject
                {
                    ["canonicalHash"] = topology.CanonicalHash,
                    ["sectors"] = new JArray(sector.Canonical),
                    ["connections"] = new JArray()
                },
                ["activeSectorId"] = sector.Canonical,
                ["creationContentProvenance"] = contentEvidence
            };
        }

        private static void MeasurePresetGeneration(
            List<string> performance,
            List<string> failures)
        {
            WorldSizePreset[] presets =
            {
                WorldSizePreset.Small,
                WorldSizePreset.Medium,
                WorldSizePreset.Large,
                WorldSizePreset.Huge
            };
            for (int index = 0; index < presets.Length; index++)
            {
                var stopwatch = Stopwatch.StartNew();
                MacroWorldPlan plan = Generate(
                    Context(new WorldSeed(99887766)), presets[index], failures,
                    "O performance " + presets[index]);
                stopwatch.Stop();
                if (plan != null)
                    performance.Add(presets[index] + "=" + stopwatch.ElapsedMilliseconds + "ms");
            }
        }

        private static MacroWorldPlan Generate(
            WorldGenerationContext context,
            WorldSizePreset preset,
            List<string> failures,
            string label)
        {
            if (!MacroWorldPlanGenerator.TryGenerate(
                    context,
                    WorldGenerationSettings.ResolvePreset(preset),
                    out MacroWorldPlan plan,
                    out string error))
            {
                failures.Add(label + " generation failed: " + error);
                return null;
            }
            return plan;
        }

        private static WorldGenerationContext Context(WorldSeed seed)
        {
            return new WorldGenerationContext(
                seed, GeneratorVersion.Parse(WorldSessionBootstrap.CurrentGeneratorVersion));
        }

        private static void CheckPlanInvariants(
            MacroWorldPlan plan,
            string label,
            List<string> failures)
        {
            Check(plan.SectorPlacements.Count == plan.GenerationSettings.ResolvedSectorCount,
                label + ": resolved sector count mismatch.", failures);
            Check(plan.Topology.Sectors.Count == plan.SectorPlacements.Count &&
                  plan.Topology.Connections.Count == Math.Max(0, plan.SectorPlacements.Count - 1),
                label + ": spatial topology does not cover/connect every placement.", failures);
            long minimumSquared = plan.GenerationSettings.ResolvedMinimumSectorSpacing *
                                  plan.GenerationSettings.ResolvedMinimumSectorSpacing;
            for (int index = 0; index < plan.SectorPlacements.Count; index++)
            {
                MacroSectorPlacement placement = plan.SectorPlacements[index];
                Check(plan.WorldBounds.Contains(placement.Position),
                    label + $": placement {index} is outside finite bounds.", failures);
                for (int otherIndex = 0; otherIndex < index; otherIndex++)
                {
                    MacroSectorPlacement other = plan.SectorPlacements[otherIndex];
                    Check(placement.SectorId != other.SectorId,
                        label + $": duplicate SectorId at {index}/{otherIndex}.", failures);
                    Check(placement.Position != other.Position,
                        label + $": duplicate macro position at {index}/{otherIndex}.", failures);
                    long dx = placement.Position.X - other.Position.X;
                    long dy = placement.Position.Y - other.Position.Y;
                    Check(dx * dx + dy * dy >= minimumSquared,
                        label + $": spacing violation at {index}/{otherIndex}.", failures);
                }
            }
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
            return string.IsNullOrWhiteSpace(value) ? "<NONE>" : value;
        }
    }
}
