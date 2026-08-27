using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using OldScars.Core;
using OldScars.Core.Actors;
using OldScars.Core.ApplicationShell;
using OldScars.Core.Data.Loading;
using OldScars.Core.Data.Validation;
using OldScars.Core.Interactions;
using OldScars.Core.Identity;
using OldScars.Core.Items;
using OldScars.Core.Persistence;
using OldScars.Core.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldScars.EditorTools
{
    [InitializeOnLoad]
    public static class WorldSessionApplicationDiagnostics
    {
        private const string PlayPendingKey = "OldScars.WorldSessionApplicationDiagnostics.PlayPending";
        private const string PlayRootKey = "OldScars.WorldSessionApplicationDiagnostics.PlayRoot";
        private const string PlayPhaseKey = "OldScars.WorldSessionApplicationDiagnostics.PlayPhase";
        private const string PlayWorldIdKey = "OldScars.WorldSessionApplicationDiagnostics.PlayWorldId";
        private const string PlayPlanHashKey = "OldScars.WorldSessionApplicationDiagnostics.PlayPlanHash";
        private const string PlayGeographyHashKey = "OldScars.WorldSessionApplicationDiagnostics.PlayGeographyHash";
        private const string PlayWaterHashKey = "OldScars.WorldSessionApplicationDiagnostics.PlayWaterHash";
        private const string PlayHumanHashKey = "OldScars.WorldSessionApplicationDiagnostics.PlayHumanHash";
        private const string PlayTopologyHashKey = "OldScars.WorldSessionApplicationDiagnostics.PlayTopologyHash";
        private const string PlayPlayerActorIdKey = "OldScars.WorldSessionApplicationDiagnostics.PlayerActorId";
        private const string PlayPlayerPersistentIdKey = "OldScars.WorldSessionApplicationDiagnostics.PlayerPersistentId";
        private const string PlayPlayerPositionXKey = "OldScars.WorldSessionApplicationDiagnostics.PlayerPositionX";
        private const string PlayPlayerPositionYKey = "OldScars.WorldSessionApplicationDiagnostics.PlayerPositionY";
        private const string PlayPlayerPositionZKey = "OldScars.WorldSessionApplicationDiagnostics.PlayerPositionZ";
        private const string PlayPlayerHealthKey = "OldScars.WorldSessionApplicationDiagnostics.PlayerHealth";
        private const string PlayMovementOriginXKey = "OldScars.WorldSessionApplicationDiagnostics.MovementOriginX";
        private const string PlayMovementOriginYKey = "OldScars.WorldSessionApplicationDiagnostics.MovementOriginY";
        private const string PlayMovementOriginZKey = "OldScars.WorldSessionApplicationDiagnostics.MovementOriginZ";
        private const string PlayFixtureContainerQuantityKey = "OldScars.WorldSessionApplicationDiagnostics.FixtureContainerQuantity";
        private const string PlayFixtureContainerIdKey = "OldScars.WorldSessionApplicationDiagnostics.FixtureContainerId";
        private const string PlayFixtureDoorIdKey = "OldScars.WorldSessionApplicationDiagnostics.FixtureDoorId";
        private const string PlayFixturePickedItemIdKey = "OldScars.WorldSessionApplicationDiagnostics.FixturePickedItemId";
        private const string PlayWorldBIdKey = "OldScars.WorldSessionApplicationDiagnostics.WorldBId";
        private const string PlayWorldCreatedLogCountKey = "OldScars.WorldSessionApplicationDiagnostics.WorldCreatedLogs";
        private const string PlayLoadOkLogCountKey = "OldScars.WorldSessionApplicationDiagnostics.LoadOkLogs";
        private const string PlaySessionReadyLogCountKey = "OldScars.WorldSessionApplicationDiagnostics.SessionReadyLogs";
        private const string PlayMaterializationReadyLogCountKey = "OldScars.WorldSessionApplicationDiagnostics.MaterializationReadyLogs";
        private const string PlaySaveOkLogCountKey = "OldScars.WorldSessionApplicationDiagnostics.SaveOkLogs";
        private const string PlayWriteCommitLogCountKey = "OldScars.WorldSessionApplicationDiagnostics.WriteCommitLogs";
        private const string PlayGameplaySaveOkLogCountKey = "OldScars.WorldSessionApplicationDiagnostics.GameplaySaveOkLogs";
        private const string PlayGameplayLoadOkLogCountKey = "OldScars.WorldSessionApplicationDiagnostics.GameplayLoadOkLogs";
        private const string PlayGameplayAbsentLogCountKey = "OldScars.WorldSessionApplicationDiagnostics.GameplayAbsentLogs";
        private const string PlayGameplayLoadFailLogCountKey = "OldScars.WorldSessionApplicationDiagnostics.GameplayLoadFailLogs";
        private const string PlayPlayerBoundLogCountKey = "OldScars.WorldSessionApplicationDiagnostics.PlayerBoundLogs";
        private const string FreshRootEnvironment = "OLD_SCARS_WORLD_SESSION_DIAGNOSTIC_ROOT";
        private const long ExplicitSeed = -3141592653589793L;

        static WorldSessionApplicationDiagnostics()
        {
            EditorApplication.update += RunPlayModeWhenReady;
            Application.logMessageReceived += CapturePlayLifecycleLog;
        }

        public static void Run()
        {
            var failures = new List<string>();
            string root = Path.Combine(
                Path.GetTempPath(),
                "OldScars_WorldSessionApplication_" + Guid.NewGuid().ToString("N"));
            try
            {
                LoadedContentSet content = LoadValidatedCore(failures);
                if (content != null)
                {
                    ValidateLifecycleObservability(root, content, failures);
                    ValidateCreateRoundTripCatalogAndLifecycle(root, content, failures);
                    ValidateSemanticRejection(root, content, failures);
                }
                ValidateSceneContracts(failures);
            }
            catch (Exception exception)
            {
                failures.Add($"Diagnostic fixture threw {exception.GetType().Name}: {exception.Message}");
            }
            finally
            {
                WorldSessionService.Close();
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }

            Complete(
                "World Session / Persistence V1 Application Shell Diagnostics",
                failures,
                "- explicit/random seed Macro World Plan V1 and immediate M37 write",
                "- same-seed WorldId independence and deterministic macro plan",
                "- world_session_v1 schema 6 semantic preflight and exact committed geography/water/climate/human-geography round-trip",
                "- duplicate display names with distinct WorldId slots",
                "- safe catalog filtering plus corrupt-save isolation",
                "- create/close/load lifecycle without partial publication",
                "- one-shot structured create/load/save evidence plus explicit schema 1/2/3 absence",
                "- Main Menu / World Runtime / Build Settings contracts",
                "- temporary persistence fixtures removed");
        }

        public static void RunBatchPlayMode()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("World Session application Play Mode diagnostics require batchmode.");

            string root = Path.Combine(
                Path.GetTempPath(),
                "OldScars_WorldSessionPlayMode_" + Guid.NewGuid().ToString("N"));
            SessionState.SetString(PlayRootKey, root);
            SessionState.SetInt(PlayPhaseKey, 0);
            SessionState.SetInt(PlayWorldCreatedLogCountKey, 0);
            SessionState.SetInt(PlayLoadOkLogCountKey, 0);
            SessionState.SetInt(PlaySessionReadyLogCountKey, 0);
            SessionState.SetInt(PlayMaterializationReadyLogCountKey, 0);
            SessionState.SetInt(PlaySaveOkLogCountKey, 0);
            SessionState.SetInt(PlayWriteCommitLogCountKey, 0);
            SessionState.SetInt(PlayGameplaySaveOkLogCountKey, 0);
            SessionState.SetInt(PlayGameplayLoadOkLogCountKey, 0);
            SessionState.SetInt(PlayGameplayAbsentLogCountKey, 0);
            SessionState.SetInt(PlayGameplayLoadFailLogCountKey, 0);
            SessionState.SetInt(PlayPlayerBoundLogCountKey, 0);
            SessionState.SetBool(PlayPendingKey, true);
            EditorSceneManager.OpenScene(WorldApplicationScenes.MainMenuScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        public static void FreshProcessCreate()
        {
            RunFreshProcess(create: true);
        }

        public static void FreshProcessLoad()
        {
            RunFreshProcess(create: false);
        }

        private static void ValidateCreateRoundTripCatalogAndLifecycle(
            string root,
            LoadedContentSet content,
            List<string> failures)
        {
            var store = new PersistenceFileStore(root);
            var seed = new WorldSeed(ExplicitSeed);
            WorldSessionService.Close();

            WorldSeed generatedSeed = WorldSessionBootstrap.CreateRandomSeed();
            Check(WorldSeed.TryParse(generatedSeed.Canonical, out WorldSeed parsedGeneratedSeed, out _) &&
                  parsedGeneratedSeed == generatedSeed,
                "A. blank-seed factory must produce an exact signed 64-bit WorldSeed without global Unity random.",
                failures);

            WorldSessionOperationResult firstCreate = WorldSessionService.Create(
                "Shared Display Name", seed,
                WorldGenerationSettings.ResolvePreset(WorldSizePreset.Large),
                LandCoveragePreset.Medium, content, store);
            Check(firstCreate.Success && WorldSessionService.ActiveSession == firstCreate.Session,
                "A. explicit-seed New Game must publish only after its initial save succeeds.", failures);
            if (!firstCreate.Success)
                return;

            WorldSession first = firstCreate.Session;
            Check(first.WorldId.IsValid && first.GenerationContext.WorldSeed == seed &&
                  first.HasMacroWorldPlan &&
                  first.HasMacroGeography &&
                  first.HasMacroWater && first.HasMacroClimate && first.HasGameplayQuality &&
                  first.HasMacroHumanGeography &&
                  first.MacroWater.GenerationSettings.LandCoverage == LandCoveragePreset.Medium &&
                  first.MacroWorldPlan.GenerationSettings.WorldSizePreset == WorldSizePreset.Large &&
                  first.Topology.Sectors.Count == 128 && first.Topology.Connections.Count == 127,
                "A. New Game must produce a valid WorldId and selected deterministic Macro World Plan.", failures);
            Check(store.TryGetPaths(first.WorldId.Canonical, out string firstPrimary, out _, out _) &&
                  File.Exists(firstPrimary),
                "A. New Game must create the primary world save before runtime publication.", failures);

            JToken firstPayload = WorldSessionPersistenceService.ToPayload(first);
            Check((string)firstPayload["snapshotType"] == WorldSessionPersistenceService.SnapshotType &&
                  (int)firstPayload["schemaVersion"] == WorldSessionPersistenceService.CurrentSchemaVersion &&
                  firstPayload["generationContext"]?["worldSeed"]?.Type == JTokenType.String &&
                  firstPayload["macroWorldPlan"]?["sectorPlacements"] is JArray &&
                  firstPayload["macroWorldPlan"]?["topology"]?["sectors"] is JArray &&
                  firstPayload["macroGeography"]?["elevationSamplesBase64"]?.Type == JTokenType.String &&
                  firstPayload["macroGeography"]?["landformSamplesBase64"]?.Type == JTokenType.String &&
                  firstPayload["macroWater"]?["oceanMaskBase64"]?.Type == JTokenType.String &&
                  firstPayload["macroWater"]?["drainageDirectionsBase64"]?.Type == JTokenType.String &&
                  firstPayload["macroClimate"]?["thermalSamplesBase64"]?.Type == JTokenType.String &&
                  firstPayload["macroClimate"]?["moistureSamplesBase64"]?.Type == JTokenType.String &&
                  firstPayload["macroHumanGeography"]?["sites"] is JArray &&
                  firstPayload["macroHumanGeography"]?["roads"] is JArray &&
                  firstPayload["creationContentProvenance"]?["sources"] is JArray,
                "world_session_v1 schema 6 must expose identity/context/plan/geography/water/climate/human/topology/provenance.",
                failures);

            WorldSessionOperationResult overwrite = WorldSessionService.Save(store);
            Check(overwrite.Success, "In-game Save lifecycle operation must persist the active WorldSession.", failures);
            WorldSessionService.Close();
            Check(!WorldSessionService.HasActiveSession,
                "H. Close/Unload must clear the active WorldSession.", failures);

            WorldSessionOperationResult secondCreate = WorldSessionService.Create(
                "Shared Display Name", seed,
                WorldGenerationSettings.ResolvePreset(WorldSizePreset.Large),
                LandCoveragePreset.Medium, content, store);
            Check(secondCreate.Success, "B/D. second same-seed, same-name New Game must succeed.", failures);
            if (!secondCreate.Success)
                return;

            WorldSession second = secondCreate.Session;
            Check(second.WorldId != first.WorldId,
                "B/D. same seed/display name must still create a distinct WorldId.", failures);
            Check(second.MacroWorldPlan.CanonicalHash == first.MacroWorldPlan.CanonicalHash &&
                  second.MacroGeography.CanonicalHash == first.MacroGeography.CanonicalHash &&
                  second.MacroWater.CanonicalHash == first.MacroWater.CanonicalHash &&
                  second.MacroHumanGeography.CanonicalHash == first.MacroHumanGeography.CanonicalHash &&
                  second.Topology.CanonicalHash == first.Topology.CanonicalHash &&
                  second.ActiveSectorId == first.ActiveSectorId,
                "B. WorldId must not alter deterministic macro plan/topology evidence.", failures);
            Check(store.TryGetPaths(second.WorldId.Canonical, out string secondPrimary, out _, out _) &&
                  File.Exists(secondPrimary) && firstPrimary != secondPrimary,
                "D. duplicate display names must use distinct WorldId-backed files.", failures);

            WorldSessionService.Close();
            WorldSessionPersistenceResult roundTrip = WorldSessionPersistenceService.Read(
                first.WorldId.Canonical, store);
            Check(roundTrip.Success && SessionsEquivalent(first, roundTrip.Session),
                "C. saved WorldSession must round-trip exact identity/context/topology/active sector/provenance evidence.",
                failures);

            WorldSessionOperationResult lifecycleLoad = WorldSessionService.Load(first.WorldId.Canonical, store);
            Check(lifecycleLoad.Success && SessionsEquivalent(first, WorldSessionService.ActiveSession),
                "H. Load must publish the same preflighted WorldSession after Close.", failures);
            WorldSessionService.Close();

            store.Write("m37_current_slice_debug", new JObject
            {
                ["snapshotType"] = "current_slice_v1",
                ["schemaVersion"] = 1
            });
            store.Write("unrelated_debug", new JObject { ["kind"] = "unrelated" });
            store.TryGetPaths(first.WorldId.Canonical, out _, out string backup, out string temp);
            File.WriteAllText(backup, "ignored backup fixture");
            File.WriteAllText(temp, "ignored temp fixture");

            WorldId corruptId = WorldId.CreateNew();
            store.TryGetPaths(corruptId.Canonical, out string corruptPrimary, out _, out _);
            Directory.CreateDirectory(store.SavesDirectory);
            File.WriteAllText(corruptPrimary, "{ malformed world save");

            WorldSaveCatalogResult catalog = WorldSaveCatalog.Discover(store);
            Check(catalog.Success && catalog.Entries.Count == 2 &&
                  ContainsCatalogWorld(catalog, first.WorldId) && ContainsCatalogWorld(catalog, second.WorldId),
                "E. catalog must discover valid primary world saves only.", failures);
            Check(!ContainsCatalogSlot(catalog, "m37_current_slice_debug") &&
                  !ContainsCatalogSlot(catalog, "unrelated_debug"),
                "E. catalog must ignore Current Slice and unrelated slots.", failures);
            Check(ContainsCatalogIssue(catalog, corruptId.Canonical),
                "F. corrupt WorldId-shaped save must be isolated as an actionable catalog issue.", failures);
        }

        private static void ValidateLifecycleObservability(
            string root,
            LoadedContentSet content,
            List<string> failures)
        {
            string observabilityRoot = Path.Combine(root, "observability");
            var store = new PersistenceFileStore(observabilityRoot);
            using (var logs = new LifecycleLogCapture())
            {
                WorldSessionService.Close();
                WorldSessionOperationResult create = WorldSessionService.Create(
                    "Observability World", new WorldSeed(ExplicitSeed),
                    WorldGenerationSettings.ResolvePreset(WorldSizePreset.Small),
                    LandCoveragePreset.Medium, content, store);
                Check(create.Success,
                    "Observability fixture New Game failed: " +
                    (string.IsNullOrEmpty(create.Failure) ? "<NONE>" : create.Failure), failures);
                if (!create.Success)
                    return;

                WorldSession currentSession = create.Session;
                JObject currentPayload = (JObject)WorldSessionPersistenceService.ToPayload(currentSession);
                string created = logs.Single(WorldCreatedPrefix);
                Check(logs.Count(WorldCreatedPrefix) == 1 &&
                      ContainsAll(created,
                          "WorldId: " + currentSession.WorldId.Canonical,
                          "Seed: " + currentSession.GenerationContext.WorldSeed.Canonical,
                          "PipelineVersion: " + currentSession.GenerationContext.GeneratorVersion.Canonical,
                          "WorldSize: Small",
                          "LandCoverage: Medium",
                          "MacroWorldPlanContract: " + MacroWorldPlanGenerator.DeterministicGenerationContract,
                          "MacroWorldPlanHash: " + currentSession.MacroWorldPlan.CanonicalHash,
                          "SectorCount: " + currentSession.MacroWorldPlan.SectorPlacements.Count,
                          "MacroGeographyContract: " + MacroGeographyGenerator.DeterministicGenerationContract,
                          "MacroGeographyHash: " + currentSession.MacroGeography.CanonicalHash,
                          "MacroWaterContract: " + currentSession.MacroWater.GenerationSettings.GenerationContract,
                          "MacroWaterHash: " + currentSession.MacroWater.CanonicalHash,
                          "MacroClimateContract: " + currentSession.MacroClimate.GenerationSettings.GenerationContract,
                          "MacroClimateHash: " + currentSession.MacroClimate.CanonicalHash,
                          "PrevailingMoistureDirection: " + currentSession.MacroClimate.PrevailingMoistureDirection,
                          "MacroHumanGeographyContract: " + currentSession.MacroHumanGeography.GenerationSettings.GenerationContract,
                          "MacroHumanGeographyHash: " + currentSession.MacroHumanGeography.CanonicalHash,
                          "RegionalHubs:", "LocalHubs:", "PrimaryRoads:", "SecondaryRoads:",
                          "RoadGeometryPoints:", "StarterDistanceToNetworkCells:",
                          "SeaLevel: " + currentSession.MacroWater.SeaLevel + "/65535",
                          "ActiveSector: " + currentSession.ActiveSectorId.Canonical,
                          "StarterLandform:", "StarterElevation:", "StarterSurface:",
                          "SuitableStarterCandidates:", "GenerationElapsedMs:"),
                    "New Game must emit exactly one complete [Worldgen][WORLD_CREATED] record.", failures);

                WorldSessionOperationResult save = WorldSessionService.Save(store);
                Check(save.Success && logs.Count(SaveOkPrefix) == 1 &&
                      logs.Count(WriteCommitPrefix) == 2 &&
                      ContainsAll(logs.Single(SaveOkPrefix),
                          "WorldId: " + currentSession.WorldId.Canonical,
                          "SchemaVersion: " + WorldSessionPersistenceService.CurrentSchemaVersion,
                          "ActiveSector: " + currentSession.ActiveSectorId.Canonical),
                    "Explicit Save must keep WRITE_COMMIT authority and add exactly one semantic SAVE_OK.", failures);

                WorldSessionService.Close();
                WorldSessionOperationResult currentLoad =
                    WorldSessionService.Load(currentSession.WorldId.Canonical, store);
                string currentLoadLog = logs.Last(LoadOkPrefix);
                Check(currentLoad.Success && logs.Count(LoadOkPrefix) == 1 &&
                      ContainsAll(currentLoadLog,
                          "SchemaVersion: " + WorldSessionPersistenceService.CurrentSchemaVersion,
                          "MacroWorldPlanHash: " + currentSession.MacroWorldPlan.CanonicalHash,
                          "MacroGeographyHash: " + currentSession.MacroGeography.CanonicalHash,
                          "MacroWaterHash: " + currentSession.MacroWater.CanonicalHash,
                          "MacroClimateHash: " + currentSession.MacroClimate.CanonicalHash,
                          "PrevailingMoistureDirection: " + currentSession.MacroClimate.PrevailingMoistureDirection,
                          "MacroHumanGeographyHash: " + currentSession.MacroHumanGeography.CanonicalHash,
                          "LegacyState: none (current schema)"),
                    "Current world Load must emit exactly one complete LOAD_OK record.", failures);

                ValidateLegacyLoadObservability(
                    store, currentPayload,
                    WorldSessionPersistenceService.MacroHumanGeographySchemaVersion,
                    "schema 5; MacroClimate absent by contract",
                    new[] { "MacroWorldPlanHash: " + currentSession.MacroWorldPlan.CanonicalHash,
                            "MacroGeographyHash: " + currentSession.MacroGeography.CanonicalHash,
                            "MacroWaterHash: " + currentSession.MacroWater.CanonicalHash,
                            "MacroClimateHash: <ABSENT>",
                            "PrevailingMoistureDirection: <ABSENT>",
                            "MacroHumanGeographyHash: " + currentSession.MacroHumanGeography.CanonicalHash },
                    logs, failures);
                ValidateLegacyLoadObservability(
                    store, currentPayload, WorldSessionPersistenceService.MacroWaterSchemaVersion,
                    "schema 4; MacroHumanGeography/Climate absent by contract",
                    new[] { "MacroWorldPlanHash: " + currentSession.MacroWorldPlan.CanonicalHash,
                            "MacroGeographyHash: " + currentSession.MacroGeography.CanonicalHash,
                            "MacroWaterHash: " + currentSession.MacroWater.CanonicalHash,
                            "MacroClimateHash: <ABSENT>",
                            "MacroHumanGeographyHash: <ABSENT>" }, logs, failures);
                ValidateLegacyLoadObservability(
                    store, currentPayload, WorldSessionPersistenceService.MacroGeographySchemaVersion,
                    "schema 3; MacroWater/Climate/HumanGeography absent by contract",
                    new[] { "MacroWorldPlanHash: " + currentSession.MacroWorldPlan.CanonicalHash,
                            "MacroGeographyHash: " + currentSession.MacroGeography.CanonicalHash,
                            "MacroWaterHash: <ABSENT>", "MacroClimateHash: <ABSENT>",
                            "MacroHumanGeographyHash: <ABSENT>" }, logs, failures);
                ValidateLegacyLoadObservability(
                    store, currentPayload, WorldSessionPersistenceService.MacroPlanSchemaVersion,
                    "schema 2; MacroGeography/Water/Climate/HumanGeography absent by contract",
                    new[] { "MacroWorldPlanHash: " + currentSession.MacroWorldPlan.CanonicalHash,
                            "MacroGeographyHash: <ABSENT>", "MacroWaterHash: <ABSENT>",
                            "MacroClimateHash: <ABSENT>",
                            "MacroHumanGeographyHash: <ABSENT>" },
                    logs, failures);
                ValidateLegacySchemaOneObservability(
                    store, currentPayload, logs, failures);

                Check(logs.Count(WorldCreatedPrefix) == 1 &&
                      logs.Count(SaveOkPrefix) == 1 &&
                      logs.Count(SessionReadyPrefix) == 0,
                    "Edit-mode lifecycle calls must not repeat create/save logs or fabricate runtime entry.", failures);
            }
            WorldSessionService.Close();
        }

        private static void ValidateLegacyLoadObservability(
            PersistenceFileStore store,
            JObject currentPayload,
            int schemaVersion,
            string expectedLegacyState,
            string[] expectedHashLines,
            LifecycleLogCapture logs,
            List<string> failures)
        {
            WorldId worldId = WorldId.CreateNew();
            JObject payload = (JObject)currentPayload.DeepClone();
            payload["worldId"] = worldId.Canonical;
            payload["displayName"] = "Legacy Observability " + schemaVersion;
            payload["schemaVersion"] = schemaVersion;
            payload.Remove("macroClimate");
            if (schemaVersion < WorldSessionPersistenceService.MacroHumanGeographySchemaVersion)
                payload.Remove("macroHumanGeography");
            if (schemaVersion <= WorldSessionPersistenceService.MacroGeographySchemaVersion)
                payload.Remove("macroWater");
            if (schemaVersion == WorldSessionPersistenceService.MacroPlanSchemaVersion)
                payload.Remove("macroGeography");

            WorldSessionService.Close();
            int previousLoads = logs.Count(LoadOkPrefix);
            PersistenceWriteResult write = store.Write(worldId.Canonical, payload);
            WorldSessionOperationResult load = WorldSessionService.Load(worldId.Canonical, store);
            string message = logs.Last(LoadOkPrefix);
            Check(write.Success && load.Success && logs.Count(LoadOkPrefix) == previousLoads + 1 &&
                  ContainsAll(message,
                      "WorldId: " + worldId.Canonical,
                      "SchemaVersion: " + schemaVersion,
                      "LegacyState: " + expectedLegacyState) &&
                  ContainsAll(message, expectedHashLines),
                "Legacy schema " + schemaVersion +
                " Load must report absent later-pass truth exactly once without fabricating it.", failures);
        }

        private static void ValidateLegacySchemaOneObservability(
            PersistenceFileStore store,
            JObject currentPayload,
            LifecycleLogCapture logs,
            List<string> failures)
        {
            WorldId worldId = WorldId.CreateNew();
            var payload = new JObject
            {
                ["snapshotType"] = WorldSessionPersistenceService.SnapshotType,
                ["schemaVersion"] = WorldSessionPersistenceService.LegacySchemaVersion,
                ["worldId"] = worldId.Canonical,
                ["displayName"] = "Legacy Observability 1",
                ["generationContext"] = currentPayload["generationContext"].DeepClone(),
                ["topology"] = currentPayload["macroWorldPlan"]["topology"].DeepClone(),
                ["activeSectorId"] = currentPayload["activeSectorId"].DeepClone(),
                ["creationContentProvenance"] = currentPayload["creationContentProvenance"].DeepClone()
            };

            WorldSessionService.Close();
            int previousLoads = logs.Count(LoadOkPrefix);
            PersistenceWriteResult write = store.Write(worldId.Canonical, payload);
            WorldSessionOperationResult load = WorldSessionService.Load(worldId.Canonical, store);
            string message = logs.Last(LoadOkPrefix);
            Check(write.Success && load.Success && logs.Count(LoadOkPrefix) == previousLoads + 1 &&
                  ContainsAll(message,
                      "WorldId: " + worldId.Canonical,
                      "SchemaVersion: 1",
                      "MacroWorldPlanHash: <ABSENT>",
                      "MacroGeographyHash: <ABSENT>",
                      "MacroWaterHash: <ABSENT>",
                      "MacroClimateHash: <ABSENT>",
                      "MacroHumanGeographyHash: <ABSENT>",
                      "LegacyState: schema 1; MacroWorldPlan/Geography/Water/Climate/HumanGeography absent by contract"),
                "Legacy schema 1 Load must explicitly report all absent macro truth exactly once.", failures);
        }

        private static void ValidateSemanticRejection(
            string root,
            LoadedContentSet content,
            List<string> failures)
        {
            string semanticRoot = Path.Combine(root, "semantic");
            var store = new PersistenceFileStore(semanticRoot);
            WorldSessionService.Close();
            WorldSessionOperationResult create = WorldSessionService.Create(
                "Semantic Baseline", new WorldSeed(42),
                WorldGenerationSettings.ResolvePreset(WorldSizePreset.Small), content, store);
            if (!create.Success)
            {
                failures.Add("G. semantic rejection baseline could not be created: " + create.Failure);
                return;
            }

            JToken valid = WorldSessionPersistenceService.ToPayload(create.Session);
            JObject invalidWorldId = (JObject)valid.DeepClone();
            invalidWorldId["worldId"] = "world_invalid";
            Check(!WorldSessionPersistenceService.FromPayload(invalidWorldId).Success,
                "G. invalid WorldId must fail semantic preflight.", failures);

            JObject invalidTopology = (JObject)valid.DeepClone();
            invalidTopology["macroWorldPlan"]["topology"]["canonicalHash"] = new string('0', 64);
            Check(!WorldSessionPersistenceService.FromPayload(invalidTopology).Success,
                "G. canonical topology evidence mismatch must fail semantic preflight.", failures);

            JObject invalidGeography = (JObject)valid.DeepClone();
            invalidGeography["macroGeography"]["canonicalHash"] = new string('0', 64);
            Check(!WorldSessionPersistenceService.FromPayload(invalidGeography).Success,
                "G. canonical macro geography evidence mismatch must fail semantic preflight.", failures);

            JObject invalidWater = (JObject)valid.DeepClone();
            invalidWater["macroWater"]["canonicalHash"] = new string('0', 64);
            Check(!WorldSessionPersistenceService.FromPayload(invalidWater).Success,
                "G. canonical Macro Water evidence mismatch must fail semantic preflight.", failures);

            JObject invalidHuman = (JObject)valid.DeepClone();
            invalidHuman["macroHumanGeography"]["canonicalHash"] = new string('0', 64);
            Check(!WorldSessionPersistenceService.FromPayload(invalidHuman).Success,
                "G. canonical Macro Human Geography evidence mismatch must fail semantic preflight.", failures);

            JObject invalidActiveSector = (JObject)valid.DeepClone();
            SectorId otherSector = SectorId.FromDeterministicDomain(
                WorldDeterminism.DerivePassDomainKey(
                    create.Session.GenerationContext.WorldSeed,
                    MacroWorldPlanGenerator.DeterministicGenerationContract,
                    "topology",
                    "other_sector"));
            invalidActiveSector["activeSectorId"] = otherSector.Canonical;
            Check(!WorldSessionPersistenceService.FromPayload(invalidActiveSector).Success,
                "G. active SectorId absent from topology must fail semantic preflight.", failures);

            WorldSessionService.Close();
            WorldId invalidSlotId = WorldId.CreateNew();
            invalidActiveSector["worldId"] = invalidSlotId.Canonical;
            PersistenceWriteResult invalidWrite = store.Write(invalidSlotId.Canonical, invalidActiveSector);
            WorldSessionOperationResult invalidLoad = WorldSessionService.Load(invalidSlotId.Canonical, store);
            Check(invalidWrite.Success && !invalidLoad.Success && !WorldSessionService.HasActiveSession,
                "G. semantically invalid save must be rejected before active-session publication.", failures);
        }

        private static void ValidateSceneContracts(List<string> failures)
        {
            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            Check(buildScenes.Length >= 3 && buildScenes[0].enabled &&
                  buildScenes[0].path == WorldApplicationScenes.MainMenuScenePath &&
                  buildScenes[1].enabled && buildScenes[1].path == WorldApplicationScenes.WorldRuntimeScenePath &&
                  ContainsBuildScene(buildScenes, WorldApplicationScenes.SampleScenePath),
                "I. Build Settings must start with Main Menu, include World Runtime, and retain SampleScene.", failures);

            Scene menu = EditorSceneManager.OpenScene(
                WorldApplicationScenes.MainMenuScenePath, OpenSceneMode.Single);
            List<MainMenuSceneController> menuControllers = FindSceneComponents<MainMenuSceneController>(menu);
            List<GameDataManager> dataManagers = FindSceneComponents<GameDataManager>(menu);
            Check(menuControllers.Count == 1 && dataManagers.Count == 1 &&
                  menuControllers[0].gameObject != dataManagers[0].gameObject,
                "I. Main Menu must wire one controller and one persistent GameDataManager on separate roots.",
                failures);

            Scene runtime = EditorSceneManager.OpenScene(
                WorldApplicationScenes.WorldRuntimeScenePath, OpenSceneMode.Single);
            Check(FindSceneComponents<WorldRuntimeSceneController>(runtime).Count == 1 &&
                  FindSceneComponents<PlayerGameplayComposition>(runtime).Count == 0 &&
                  FindSceneComponents<CameraRigController>(runtime).Count == 0 &&
                  FindSceneComponents<Camera>(runtime).Count == 0,
                "J. World Runtime must wire one shell controller and no parallel authored player/camera fixture.",
                failures);

            PlayerGameplayComposition sharedPrefab =
                AssetDatabase.LoadAssetAtPath<PlayerGameplayComposition>(
                    "Assets/_OldScars/Resources/PFB_PlayerGameplayComposition.prefab");
            string prefabFailure = null;
            Check(sharedPrefab != null && sharedPrefab.TryValidateStructure(out prefabFailure),
                "Shared product player prefab is missing or incomplete: " +
                (prefabFailure ?? "<NO PREFAB>"), failures);

            Scene sample = EditorSceneManager.OpenScene(
                WorldApplicationScenes.SampleScenePath, OpenSceneMode.Single);
            List<PlayerGameplayComposition> sampleCompositions =
                FindSceneComponents<PlayerGameplayComposition>(sample);
            List<SampleSceneGameplayRuntimeBootstrap> sampleBootstraps =
                FindSceneComponents<SampleSceneGameplayRuntimeBootstrap>(sample);
            List<DevelopmentGameplayIntegrationFixture> sampleFixtures =
                FindSceneComponents<DevelopmentGameplayIntegrationFixture>(sample);
            Check(sampleCompositions.Count == 1 &&
                  PrefabUtility.GetCorrespondingObjectFromSource(sampleCompositions[0]) == sharedPrefab &&
                  FindSceneComponents<ActorInteractionContext>(sample)
                      .FindAll(context => Array.IndexOf(context.ActorTags, "player") >= 0).Count == 1 &&
                   FindSceneComponents<CameraRigController>(sample).Count == 1 &&
                   FindSceneComponents<Camera>(sample).Count == 1 &&
                   sampleBootstraps.Count == 1 && sampleFixtures.Count == 1 &&
                   PrefabUtility.GetCorrespondingObjectFromSource(sampleFixtures[0]) != null &&
                   FindSceneComponents<InventoryUISessionController>(sample).Count == 0 &&
                   FindSceneComponents<WorldInteractionDebugTester>(sample).Count == 0,
                "SampleScene must consume exactly one instance of the same authored player/camera composition.",
                failures);
            Check(sharedPrefab.PersistentIdentity != null &&
                  sharedPrefab.PersistentIdentity.PersistentId == "scene_sample_scene_actor_player_primary" &&
                  sampleCompositions[0].PersistentIdentity.PersistentId ==
                  sharedPrefab.PersistentIdentity.PersistentId,
                "Shared authored player identity must remain prefab-owned and identical in mutually exclusive runtimes.",
                failures);
            Check(File.Exists(WorldApplicationScenes.SampleScenePath),
                "SampleScene must remain available as the regression laboratory.", failures);
        }

        private static void RunPlayModeWhenReady()
        {
            if (!SessionState.GetBool(PlayPendingKey, false) || !EditorApplication.isPlaying)
                return;

            try
            {
                string root = SessionState.GetString(PlayRootKey, string.Empty);
                var store = new PersistenceFileStore(root);
                int phase = SessionState.GetInt(PlayPhaseKey, 0);
                if (phase == 0)
                {
                    if (GameDataManager.Instance == null || !GameDataManager.Instance.IsReady ||
                        GameDataManager.Instance.LoadedContentSet == null ||
                        SceneManager.GetActiveScene().name != WorldApplicationScenes.MainMenuSceneName)
                    {
                        return;
                    }

                    MainMenuSceneController menu =
                        UnityEngine.Object.FindAnyObjectByType<MainMenuSceneController>();
                    if (menu == null || !menu.TryCreateWorld(
                            "Play Flow World", ExplicitSeed.ToString(), WorldSizePreset.Medium,
                            LandCoveragePreset.Medium, store))
                    {
                        FailPlay("Main Menu Create action failed to create, save and request World Runtime entry.", root);
                        return;
                    }

                    WorldSession created = WorldSessionService.ActiveSession;
                    if (created == null)
                    {
                        FailPlay("Main Menu Create returned without publishing the saved WorldSession.", root);
                        return;
                    }
                    SessionState.SetString(PlayWorldIdKey, created.WorldId.Canonical);
                    SessionState.SetString(PlayPlanHashKey, created.MacroWorldPlan.CanonicalHash);
                    SessionState.SetString(PlayGeographyHashKey, created.MacroGeography.CanonicalHash);
                    SessionState.SetString(PlayWaterHashKey, created.MacroWater.CanonicalHash);
                    SessionState.SetString(PlayHumanHashKey, created.MacroHumanGeography.CanonicalHash);
                    SessionState.SetString(PlayTopologyHashKey, created.Topology.CanonicalHash);
                    SessionState.SetInt(PlayPhaseKey, 1);
                    return;
                }

                if (phase == 1)
                {
                    if (SceneManager.GetActiveScene().name != WorldApplicationScenes.WorldRuntimeSceneName)
                        return;
                    WorldRuntimeSceneController runtime =
                        UnityEngine.Object.FindAnyObjectByType<WorldRuntimeSceneController>();
                    MainMenuSceneController leakedMenu =
                        UnityEngine.Object.FindAnyObjectByType<MainMenuSceneController>();
                    if (runtime == null || leakedMenu != null || !WorldSessionService.HasActiveSession)
                    {
                        FailPlay("World Runtime is missing its authority or retained a Main Menu controller.", root);
                        return;
                    }
                    if (!runtime.GameplayStateReady)
                        return;
                    if (!ValidateRuntimeMaterialization(runtime, out string materializationFailure))
                    {
                        FailPlay("World Runtime terrain materialization failed: " + materializationFailure, root);
                        return;
                    }
                    if (!ValidateIntegratedGameplayRuntime(runtime, exerciseUi: true, out string integrationFailure))
                    {
                        FailPlay("Shared gameplay runtime/fixture integration failed: " + integrationFailure, root);
                        return;
                    }
                    if (PlayLogCount(PlayWorldCreatedLogCountKey) != 1 ||
                        PlayLogCount(PlaySessionReadyLogCountKey) != 1 ||
                        PlayLogCount(PlayMaterializationReadyLogCountKey) != 1 ||
                        PlayLogCount(PlayLoadOkLogCountKey) != 0 ||
                        PlayLogCount(PlayPlayerBoundLogCountKey) != 1 ||
                        runtime.PlayerBindSource != WorldRuntimePlayerBindSource.NewGameSafeSpawn ||
                        runtime.GameplayRestoreAttempted)
                    {
                        FailPlay("New Game did not bind exactly one shared player from safe-spawn bootstrap.", root);
                        return;
                    }

                    PlayerGameplayComposition player = runtime.PlayerComposition;
                    Vector3 movementOrigin = player.PlayerTransform.position;
                    SessionState.SetFloat(PlayMovementOriginXKey, movementOrigin.x);
                    SessionState.SetFloat(PlayMovementOriginYKey, movementOrigin.y);
                    SessionState.SetFloat(PlayMovementOriginZKey, movementOrigin.z);
                    // Batchmode has no physical keyboard. Suspend the input reader while
                    // injecting directly into the existing movement authority; otherwise
                    // its normal zero-input frame would immediately clear this request.
                    player.MovementInput.enabled = false;
                    player.MovementController.SetMovementDirection(Vector3.right);
                    SessionState.SetInt(PlayPhaseKey, 101);
                    return;
                }

                if (phase == 101)
                {
                    WorldRuntimeSceneController runtime =
                        UnityEngine.Object.FindAnyObjectByType<WorldRuntimeSceneController>();
                    if (runtime == null || !runtime.GameplayStateReady || runtime.PlayerComposition == null)
                    {
                        FailPlay("Shared player disappeared during movement proof.", root);
                        return;
                    }

                    PlayerGameplayComposition player = runtime.PlayerComposition;
                    Vector3 movementOrigin = new Vector3(
                        SessionState.GetFloat(PlayMovementOriginXKey, 0f),
                        SessionState.GetFloat(PlayMovementOriginYKey, 0f),
                        SessionState.GetFloat(PlayMovementOriginZKey, 0f));
                    if (Vector3.Distance(player.PlayerTransform.position, movementOrigin) < 0.08f)
                        return;
                    player.MovementController.ClearMovement();
                    player.MovementInput.enabled = true;

                    if (!player.CameraRig.HasContinuousFollow || player.CameraRig.AllowsIndependentPan ||
                        player.GameplayCamera != Camera.main)
                    {
                        FailPlay("Existing CameraRig follow/MainCamera contract is not active on the shared player.", root);
                        return;
                    }
                    player.CameraRig.OrbitAroundTarget(12f);
                    player.CameraRig.ApplyZoom(1f);
                    player.CameraRig.FollowTargetNow();

                    TerrainMaterializationResult materialized = runtime.MaterializationController.Result;
                    player.PlacePlayerAtSurface(materialized.PathDestination, Quaternion.Euler(0f, 33f, 0f));
                    ActorHealthComponent health = player.PlayerContext.GetComponent<ActorHealthComponent>();
                    if (health == null || !health.ApplyDamage(7f))
                    {
                        FailPlay("Health mutation fixture could not establish non-pose Current Slice evidence.", root);
                        return;
                    }
                    if (!MutateIntegratedFixture(
                            runtime, out int fixtureContainerQuantity,
                            out string pickedItemId, out string fixtureMutationFailure))
                    {
                        FailPlay("Integrated fixture mutation failed: " + fixtureMutationFailure, root);
                        return;
                    }
                    SessionState.SetInt(PlayFixtureContainerQuantityKey, fixtureContainerQuantity);
                    SessionState.SetString(PlayFixturePickedItemIdKey, pickedItemId);

                    Vector3 savedPosition = player.PlayerTransform.position;
                    SessionState.SetString(PlayPlayerActorIdKey, player.PlayerIdentity.ActorInstanceId);
                    SessionState.SetString(PlayPlayerPersistentIdKey, player.PersistentIdentity.PersistentId);
                    SessionState.SetFloat(PlayPlayerPositionXKey, savedPosition.x);
                    SessionState.SetFloat(PlayPlayerPositionYKey, savedPosition.y);
                    SessionState.SetFloat(PlayPlayerPositionZKey, savedPosition.z);
                    SessionState.SetFloat(PlayPlayerHealthKey, health.CurrentHealth);

                    int writesBeforeFailure = PlayLogCount(PlayWriteCommitLogCountKey);
                    WorldGameplayPersistenceService.DiagnosticInjectPrepareFailure = true;
                    runtime.OpenMenu();
                    bool injectedSave = runtime.SaveGame(store);
                    if (injectedSave || PlayLogCount(PlayWriteCommitLogCountKey) != writesBeforeFailure ||
                        PlayLogCount(PlaySaveOkLogCountKey) != 0 ||
                        PlayLogCount(PlayGameplaySaveOkLogCountKey) != 0)
                    {
                        FailPlay("Gameplay capture failure incorrectly reported/committed an overall Save.", root);
                        return;
                    }

                    if (!runtime.SaveGame(store))
                    {
                        FailPlay("In-game Save did not commit the world-bound Current Slice sibling.", root);
                        return;
                    }
                    if (PlayLogCount(PlaySaveOkLogCountKey) != 1 ||
                        PlayLogCount(PlayGameplaySaveOkLogCountKey) != 1 ||
                        PlayLogCount(PlayWriteCommitLogCountKey) != 3)
                    {
                        FailPlay("Initial world write plus one coherent Save must emit three WRITE_COMMIT, one gameplay and one overall SAVE_OK.", root);
                        return;
                    }
                    PersistenceLoadResult gameplayRead = store.Read(
                        WorldGameplayPersistenceService.GetSlotId(WorldSessionService.ActiveSession.WorldId));
                    if (!gameplayRead.Success ||
                        gameplayRead.Payload?["snapshotType"]?.Value<string>() !=
                        WorldGameplayPersistenceService.SnapshotType ||
                        gameplayRead.Payload?["currentSlice"]?["snapshotType"]?.Value<string>() !=
                        "current_slice_v1")
                    {
                        FailPlay("World gameplay sibling was not persisted through the M37 envelope/store.", root);
                        return;
                    }

                    runtime.ContinueGame();
                    if (runtime.IsMenuOpen)
                    {
                        FailPlay("Continue did not close the in-game menu.", root);
                        return;
                    }
                    runtime.OpenMenu();
                    runtime.ReturnToMainMenu();
                    SessionState.SetInt(PlayPhaseKey, 2);
                    return;
                }

                if (phase == 2)
                {
                    if (SceneManager.GetActiveScene().name != WorldApplicationScenes.MainMenuSceneName)
                        return;
                    if (WorldSessionService.HasActiveSession)
                    {
                        FailPlay("Return to Main Menu left a stale active WorldSession.", root);
                        return;
                    }
                    if (GameDataManager.Instance == null || !GameDataManager.Instance.IsReady ||
                        GameDataManager.Instance.LoadedContentSet == null)
                    {
                        return;
                    }

                    MainMenuSceneController menu =
                        UnityEngine.Object.FindAnyObjectByType<MainMenuSceneController>();
                    string worldId = SessionState.GetString(PlayWorldIdKey, string.Empty);
                    if (menu == null || !menu.TryLoadWorld(worldId, store))
                    {
                        FailPlay("Main Menu Load action failed to request World Runtime entry.", root);
                        return;
                    }
                    SessionState.SetInt(PlayPhaseKey, 3);
                    return;
                }

                if (phase == 3)
                {
                    if (SceneManager.GetActiveScene().name != WorldApplicationScenes.WorldRuntimeSceneName)
                        return;
                    WorldSession loaded = WorldSessionService.ActiveSession;
                    string worldId = SessionState.GetString(PlayWorldIdKey, string.Empty);
                    string planHash = SessionState.GetString(PlayPlanHashKey, string.Empty);
                    string geographyHash = SessionState.GetString(PlayGeographyHashKey, string.Empty);
                    string waterHash = SessionState.GetString(PlayWaterHashKey, string.Empty);
                    string humanHash = SessionState.GetString(PlayHumanHashKey, string.Empty);
                    string topologyHash = SessionState.GetString(PlayTopologyHashKey, string.Empty);
                    WorldRuntimeSceneController runtime =
                        UnityEngine.Object.FindAnyObjectByType<WorldRuntimeSceneController>();
                    if (runtime == null || !runtime.GameplayStateReady)
                        return;
                    if (loaded == null || runtime == null || loaded.WorldId.Canonical != worldId ||
                        !loaded.HasMacroWorldPlan || loaded.MacroWorldPlan.CanonicalHash != planHash ||
                        !loaded.HasMacroGeography || loaded.MacroGeography.CanonicalHash != geographyHash ||
                        !loaded.HasMacroWater || loaded.MacroWater.CanonicalHash != waterHash ||
                        !loaded.HasMacroHumanGeography || loaded.MacroHumanGeography.CanonicalHash != humanHash ||
                        loaded.Topology.CanonicalHash != topologyHash)
                    {
                        FailPlay("Load action did not restore the recorded world/topology in World Runtime.", root);
                        return;
                    }
                    if (!ValidateRuntimeMaterialization(runtime, out string materializationFailure))
                    {
                        FailPlay("Loaded World Runtime terrain materialization failed: " + materializationFailure, root);
                        return;
                    }
                    if (!ValidateRestoredPlayer(runtime, out string playerFailure))
                    {
                        FailPlay("Current Slice did not restore saved player evidence: " + playerFailure, root);
                        return;
                    }
                    bool integratedRuntimeValid = ValidateIntegratedGameplayRuntime(
                        runtime, exerciseUi: false, out string integrationFailure);
                    bool fixtureRestored = ValidateRestoredFixture(runtime, out string fixtureFailure);
                    if (!integratedRuntimeValid || !fixtureRestored)
                    {
                        FailPlay("Integrated gameplay/fixture state did not restore: " +
                                 (integrationFailure ?? fixtureFailure), root);
                        return;
                    }
                    if (PlayLogCount(PlayWorldCreatedLogCountKey) != 1 ||
                        PlayLogCount(PlayLoadOkLogCountKey) != 1 ||
                        PlayLogCount(PlaySessionReadyLogCountKey) != 2 ||
                        PlayLogCount(PlayMaterializationReadyLogCountKey) != 2 ||
                        PlayLogCount(PlaySaveOkLogCountKey) != 1 ||
                        PlayLogCount(PlayGameplayLoadOkLogCountKey) != 1 ||
                        PlayLogCount(PlayPlayerBoundLogCountKey) != 2 ||
                        runtime.PlayerBindSource != WorldRuntimePlayerBindSource.SaveRestore ||
                        !runtime.GameplayRestoreAttempted || !runtime.CompositionReadyBeforeRestore)
                    {
                        FailPlay("Load flow did not restore gameplay after materialization/composition and before camera binding.", root);
                        return;
                    }

                    runtime.OpenMenu();
                    if (!runtime.SaveGame(store) ||
                        PlayLogCount(PlaySaveOkLogCountKey) != 2 ||
                        PlayLogCount(PlayGameplaySaveOkLogCountKey) != 2 ||
                        PlayLogCount(PlayWriteCommitLogCountKey) != 5)
                    {
                        FailPlay("Repeated Save failed to preserve coherent world/gameplay commit evidence.", root);
                        return;
                    }
                    if (!RecordExpectedPlayerState(runtime.PlayerComposition, out string recordFailure))
                    {
                        FailPlay("Repeated Save evidence could not be recorded: " + recordFailure, root);
                        return;
                    }
                    runtime.ReturnToMainMenu();
                    SessionState.SetInt(PlayPhaseKey, 4);
                    return;
                }

                if (phase == 4)
                {
                    if (SceneManager.GetActiveScene().name != WorldApplicationScenes.MainMenuSceneName)
                        return;
                    if (WorldSessionService.HasActiveSession)
                    {
                        FailPlay("Second Return to Main Menu left a stale WorldSession.", root);
                        return;
                    }

                    MainMenuSceneController menu =
                        UnityEngine.Object.FindAnyObjectByType<MainMenuSceneController>();
                    string worldId = SessionState.GetString(PlayWorldIdKey, string.Empty);
                    if (menu == null || !menu.TryLoadWorld(worldId, store))
                    {
                        FailPlay("Repeated Main Menu Load could not reopen world A.", root);
                        return;
                    }
                    SessionState.SetInt(PlayPhaseKey, 5);
                    return;
                }

                if (phase == 5)
                {
                    if (SceneManager.GetActiveScene().name != WorldApplicationScenes.WorldRuntimeSceneName)
                        return;
                    WorldRuntimeSceneController runtime =
                        UnityEngine.Object.FindAnyObjectByType<WorldRuntimeSceneController>();
                    if (runtime == null || !runtime.GameplayStateReady)
                        return;
                    bool materializationValid =
                        ValidateRuntimeMaterialization(runtime, out string materializationFailure);
                    bool playerValid = ValidateRestoredPlayer(runtime, out string playerFailure);
                    if (!materializationValid || !playerValid ||
                        UnityEngine.Object.FindObjectsByType<PlayerGameplayComposition>(
                            FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1 ||
                        UnityEngine.Object.FindObjectsByType<CameraRigController>(
                            FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1)
                    {
                        FailPlay("Repeated Save->Menu->Load duplicated or changed the shared player: " +
                                 (materializationFailure ?? playerFailure), root);
                        return;
                    }

                    runtime.ReturnToMainMenu();
                    SessionState.SetInt(PlayPhaseKey, 6);
                    return;
                }

                if (phase == 6)
                {
                    if (SceneManager.GetActiveScene().name != WorldApplicationScenes.MainMenuSceneName)
                        return;
                    MainMenuSceneController menu =
                        UnityEngine.Object.FindAnyObjectByType<MainMenuSceneController>();
                    if (menu == null || !menu.TryCreateWorld(
                            "Legacy Gameplay State World", (ExplicitSeed + 1L).ToString(),
                            WorldSizePreset.Small, LandCoveragePreset.Medium, store))
                    {
                        FailPlay("World B fixture could not be created without a gameplay sibling.", root);
                        return;
                    }
                    SessionState.SetString(
                        PlayWorldBIdKey,
                        WorldSessionService.ActiveSession.WorldId.Canonical);
                    SessionState.SetInt(PlayPhaseKey, 7);
                    return;
                }

                if (phase == 7)
                {
                    if (SceneManager.GetActiveScene().name != WorldApplicationScenes.WorldRuntimeSceneName)
                        return;
                    WorldRuntimeSceneController runtime =
                        UnityEngine.Object.FindAnyObjectByType<WorldRuntimeSceneController>();
                    WorldSession worldB = WorldSessionService.ActiveSession;
                    if (runtime == null || !runtime.GameplayStateReady)
                        return;
                    if (!ValidateRuntimeMaterialization(runtime, out string materializationFailure) ||
                        runtime.PlayerBindSource != WorldRuntimePlayerBindSource.NewGameSafeSpawn ||
                        store.Read(WorldGameplayPersistenceService.GetSlotId(worldB.WorldId)).Success)
                    {
                        FailPlay("New world B did not begin from one unsaved safe-spawn gameplay composition: " +
                                 materializationFailure, root);
                        return;
                    }
                    runtime.ReturnToMainMenu();
                    SessionState.SetInt(PlayPhaseKey, 8);
                    return;
                }

                if (phase == 8)
                {
                    if (SceneManager.GetActiveScene().name != WorldApplicationScenes.MainMenuSceneName)
                        return;
                    MainMenuSceneController menu =
                        UnityEngine.Object.FindAnyObjectByType<MainMenuSceneController>();
                    string worldB = SessionState.GetString(PlayWorldBIdKey, string.Empty);
                    if (menu == null || !menu.TryLoadWorld(worldB, store))
                    {
                        FailPlay("Legacy world B could not be loaded without a gameplay sibling.", root);
                        return;
                    }
                    SessionState.SetInt(PlayPhaseKey, 9);
                    return;
                }

                if (phase == 9)
                {
                    if (SceneManager.GetActiveScene().name != WorldApplicationScenes.WorldRuntimeSceneName)
                        return;
                    WorldRuntimeSceneController runtime =
                        UnityEngine.Object.FindAnyObjectByType<WorldRuntimeSceneController>();
                    if (runtime == null || !runtime.GameplayStateReady)
                        return;
                    if (!ValidateRuntimeMaterialization(runtime, out string materializationFailure) ||
                        runtime.PlayerBindSource != WorldRuntimePlayerBindSource.LegacySafeSpawn ||
                        runtime.GameplayLoadResult?.Disposition != WorldGameplayLoadDisposition.AbsentLegacy ||
                        PlayLogCount(PlayGameplayAbsentLogCountKey) != 1)
                    {
                        FailPlay("Missing schema-5 gameplay sibling did not use explicit legacy safe bootstrap: " +
                                 materializationFailure, root);
                        return;
                    }

                    if (!WorldId.TryParse(
                            SessionState.GetString(PlayWorldIdKey, string.Empty),
                            out WorldId worldA,
                            out _) ||
                        !WorldId.TryParse(
                            SessionState.GetString(PlayWorldBIdKey, string.Empty),
                            out WorldId worldB,
                            out _))
                    {
                        FailPlay("World A/B diagnostic identities are invalid.", root);
                        return;
                    }
                    PersistenceLoadResult source = store.Read(
                        WorldGameplayPersistenceService.GetSlotId(worldA));
                    PersistenceWriteResult contamination = source.Success
                        ? store.Write(WorldGameplayPersistenceService.GetSlotId(worldB), source.Payload)
                        : null;
                    if (!source.Success || contamination == null || !contamination.Success)
                    {
                        FailPlay("Could not establish the intentional world A->B contamination fixture.", root);
                        return;
                    }
                    runtime.ReturnToMainMenu();
                    SessionState.SetInt(PlayPhaseKey, 10);
                    return;
                }

                if (phase == 10)
                {
                    if (SceneManager.GetActiveScene().name != WorldApplicationScenes.MainMenuSceneName)
                        return;
                    MainMenuSceneController menu =
                        UnityEngine.Object.FindAnyObjectByType<MainMenuSceneController>();
                    string worldB = SessionState.GetString(PlayWorldBIdKey, string.Empty);
                    if (menu == null || !menu.TryLoadWorld(worldB, store))
                    {
                        FailPlay("Contamination rejection fixture could not enter world B runtime.", root);
                        return;
                    }
                    SessionState.SetInt(PlayPhaseKey, 11);
                    return;
                }

                if (phase == 11)
                {
                    if (SceneManager.GetActiveScene().name != WorldApplicationScenes.WorldRuntimeSceneName)
                        return;
                    WorldRuntimeSceneController runtime =
                        UnityEngine.Object.FindAnyObjectByType<WorldRuntimeSceneController>();
                    if (runtime != null && runtime.GameplayLoadResult == null)
                        return;
                    if (runtime == null || runtime.GameplayStateReady ||
                        runtime.GameplayLoadResult?.Disposition != WorldGameplayLoadDisposition.Failed ||
                        runtime.GameplayLoadResult.Phase != "SemanticPreflight" ||
                        PlayLogCount(PlayGameplayLoadFailLogCountKey) != 1 ||
                        PlayLogCount(PlayPlayerBoundLogCountKey) != 5)
                    {
                        FailPlay("World A gameplay payload was not rejected before publication in world B.", root);
                        return;
                    }

                    runtime.ReturnToMainMenu();
                    SessionState.SetInt(PlayPhaseKey, 12);
                    return;
                }

                if (phase == 12)
                {
                    if (SceneManager.GetActiveScene().name != WorldApplicationScenes.MainMenuSceneName)
                        return;
                    if (WorldSessionService.HasActiveSession)
                    {
                        FailPlay("Final Return to Main Menu left a stale WorldSession.", root);
                        return;
                    }

                    CleanupPlayState(root);
                    Debug.Log(
                        "World Session Application Play Flow: PASS\n" +
                        "- shared player movement/camera/full gameplay composition validated\n" +
                        "- shared inventory/health/needs/interaction runtime exercised in WorldRuntime\n" +
                        "- container transfer, authored pickup and door action restored through Current Slice\n" +
                        "- moved local pose, authored identity and health restored through Current Slice\n" +
                        "- repeated Save->Menu->Load retained exactly one player/camera composition\n" +
                        "- missing gameplay sibling used explicit legacy bootstrap\n" +
                        "- world A gameplay payload was rejected in world B before apply\n" +
                        "- gameplay capture failure emitted no overall Save OK/write\n" +
                        "- temporary persistence root removed");
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                FailPlay($"Play flow threw {exception.GetType().Name}: {exception.Message}",
                    SessionState.GetString(PlayRootKey, string.Empty));
            }
        }

        private static void FailPlay(string failure, string root)
        {
            Debug.LogError("World Session Application Play Flow: FAIL\n- " + failure);
            CleanupPlayState(root);
            EditorApplication.Exit(1);
        }

        private static void CleanupPlayState(string root)
        {
            Time.timeScale = 1f;
            WorldSessionService.Close();
            SessionState.EraseBool(PlayPendingKey);
            SessionState.EraseString(PlayRootKey);
            SessionState.EraseInt(PlayPhaseKey);
            SessionState.EraseString(PlayWorldIdKey);
            SessionState.EraseString(PlayPlanHashKey);
            SessionState.EraseString(PlayGeographyHashKey);
            SessionState.EraseString(PlayWaterHashKey);
            SessionState.EraseString(PlayHumanHashKey);
            SessionState.EraseString(PlayTopologyHashKey);
            SessionState.EraseString(PlayPlayerActorIdKey);
            SessionState.EraseString(PlayPlayerPersistentIdKey);
            SessionState.EraseFloat(PlayPlayerPositionXKey);
            SessionState.EraseFloat(PlayPlayerPositionYKey);
            SessionState.EraseFloat(PlayPlayerPositionZKey);
            SessionState.EraseFloat(PlayPlayerHealthKey);
            SessionState.EraseFloat(PlayMovementOriginXKey);
            SessionState.EraseFloat(PlayMovementOriginYKey);
            SessionState.EraseFloat(PlayMovementOriginZKey);
            SessionState.EraseInt(PlayFixtureContainerQuantityKey);
            SessionState.EraseString(PlayFixtureContainerIdKey);
            SessionState.EraseString(PlayFixtureDoorIdKey);
            SessionState.EraseString(PlayFixturePickedItemIdKey);
            SessionState.EraseString(PlayWorldBIdKey);
            SessionState.EraseInt(PlayWorldCreatedLogCountKey);
            SessionState.EraseInt(PlayLoadOkLogCountKey);
            SessionState.EraseInt(PlaySessionReadyLogCountKey);
            SessionState.EraseInt(PlayMaterializationReadyLogCountKey);
            SessionState.EraseInt(PlaySaveOkLogCountKey);
            SessionState.EraseInt(PlayWriteCommitLogCountKey);
            SessionState.EraseInt(PlayGameplaySaveOkLogCountKey);
            SessionState.EraseInt(PlayGameplayLoadOkLogCountKey);
            SessionState.EraseInt(PlayGameplayAbsentLogCountKey);
            SessionState.EraseInt(PlayGameplayLoadFailLogCountKey);
            SessionState.EraseInt(PlayPlayerBoundLogCountKey);
            if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
                Directory.Delete(root, true);
        }

        private static void RunFreshProcess(bool create)
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Fresh-process World Session diagnostics require batchmode.");

            string root = Environment.GetEnvironmentVariable(FreshRootEnvironment);
            if (string.IsNullOrWhiteSpace(root))
            {
                Debug.LogError("Fresh-process diagnostic root environment variable is missing.");
                EditorApplication.Exit(2);
                return;
            }

            try
            {
                if (WorldSessionService.HasActiveSession)
                    throw new InvalidOperationException("Fresh process began with an unexpected active WorldSession.");

                string recordPath = Path.Combine(root, "fresh-process-record.txt");
                var store = new PersistenceFileStore(root);
                if (create)
                {
                    var failures = new List<string>();
                    LoadedContentSet content = LoadValidatedCore(failures);
                    if (content == null || failures.Count > 0)
                        throw new InvalidOperationException("Real Core could not provide creation provenance: " +
                                                            string.Join(" | ", failures));

                    WorldSessionOperationResult result = WorldSessionService.Create(
                        "Fresh Process World", new WorldSeed(ExplicitSeed),
                        WorldGenerationSettings.ResolvePreset(WorldSizePreset.Huge),
                        LandCoveragePreset.High, content, store);
                    if (!result.Success)
                        throw new InvalidOperationException(result.Phase + ": " + result.Failure);

                    WorldSession session = result.Session;
                    Directory.CreateDirectory(root);
                    File.WriteAllLines(recordPath, new[]
                    {
                        session.WorldId.Canonical,
                        session.GenerationContext.WorldSeed.Canonical,
                        WorldGenerationSettings.ToCanonical(
                            session.MacroWorldPlan.GenerationSettings.WorldSizePreset),
                        session.MacroWorldPlan.CanonicalHash,
                        session.MacroGeography.CanonicalHash,
                        MacroWaterGenerationSettings.ToCanonical(
                            session.MacroWater.GenerationSettings.LandCoverage),
                        session.MacroWater.CanonicalHash,
                        session.MacroClimate.CanonicalHash,
                        MacroClimateGenerationSettings.ToCanonical(
                            session.MacroClimate.PrevailingMoistureDirection),
                        session.MacroHumanGeography.CanonicalHash,
                        session.Topology.CanonicalHash,
                        session.ActiveSectorId.Canonical
                    });
                    Debug.Log(
                        "World Session Fresh Process A: PASS\n" +
                        "WorldId: " + session.WorldId.Canonical + "\n" +
                        "Seed: " + session.GenerationContext.WorldSeed.Canonical + "\n" +
                        "Size: " + session.MacroWorldPlan.GenerationSettings.WorldSizePreset + "\n" +
                        "MacroWorldPlanHash: " + session.MacroWorldPlan.CanonicalHash + "\n" +
                        "MacroGeographyHash: " + session.MacroGeography.CanonicalHash + "\n" +
                        "LandCoverage: " + session.MacroWater.GenerationSettings.LandCoverage + "\n" +
                        "MacroWaterHash: " + session.MacroWater.CanonicalHash + "\n" +
                        "MacroClimateHash: " + session.MacroClimate.CanonicalHash + "\n" +
                        "PrevailingMoistureDirection: " +
                        session.MacroClimate.PrevailingMoistureDirection + "\n" +
                        "MacroHumanGeographyHash: " + session.MacroHumanGeography.CanonicalHash + "\n" +
                        "TopologyHash: " + session.Topology.CanonicalHash + "\n" +
                        "ActiveSectorId: " + session.ActiveSectorId.Canonical);
                }
                else
                {
                    string[] record = File.ReadAllLines(recordPath);
                    if (record.Length != 12)
                        throw new InvalidOperationException("Fresh-process record is missing or malformed.");

                    WorldSaveCatalogResult catalog = WorldSaveCatalog.Discover(store);
                    if (!catalog.Success || catalog.Entries.Count != 1 || catalog.Entries[0].SlotId != record[0])
                        throw new InvalidOperationException("Fresh process did not discover exactly the recorded world save.");

                    WorldSessionOperationResult result = WorldSessionService.Load(record[0], store);
                    if (!result.Success)
                        throw new InvalidOperationException(result.Phase + ": " + result.Failure);
                    WorldSession session = result.Session;
                    if (session.WorldId.Canonical != record[0] ||
                        session.GenerationContext.WorldSeed.Canonical != record[1] ||
                        !session.HasMacroWorldPlan ||
                        WorldGenerationSettings.ToCanonical(
                            session.MacroWorldPlan.GenerationSettings.WorldSizePreset) != record[2] ||
                        session.MacroWorldPlan.CanonicalHash != record[3] ||
                        !session.HasMacroGeography ||
                        session.MacroGeography.CanonicalHash != record[4] ||
                        !session.HasMacroWater ||
                        MacroWaterGenerationSettings.ToCanonical(
                            session.MacroWater.GenerationSettings.LandCoverage) != record[5] ||
                        session.MacroWater.CanonicalHash != record[6] ||
                        !session.HasMacroClimate ||
                        session.MacroClimate.CanonicalHash != record[7] ||
                        MacroClimateGenerationSettings.ToCanonical(
                            session.MacroClimate.PrevailingMoistureDirection) != record[8] ||
                        !session.HasMacroHumanGeography ||
                        session.MacroHumanGeography.CanonicalHash != record[9] ||
                        session.Topology.CanonicalHash != record[10] ||
                        session.ActiveSectorId.Canonical != record[11])
                    {
                        throw new InvalidOperationException("Fresh-process loaded evidence differs from Process A.");
                    }

                    Debug.Log(
                        "World Session Fresh Process B: PASS\n" +
                        "WorldId: " + session.WorldId.Canonical + "\n" +
                        "Seed: " + session.GenerationContext.WorldSeed.Canonical + "\n" +
                        "Size: " + session.MacroWorldPlan.GenerationSettings.WorldSizePreset + "\n" +
                        "MacroWorldPlanHash: " + session.MacroWorldPlan.CanonicalHash + "\n" +
                        "MacroGeographyHash: " + session.MacroGeography.CanonicalHash + "\n" +
                        "LandCoverage: " + session.MacroWater.GenerationSettings.LandCoverage + "\n" +
                        "MacroWaterHash: " + session.MacroWater.CanonicalHash + "\n" +
                        "MacroClimateHash: " + session.MacroClimate.CanonicalHash + "\n" +
                        "PrevailingMoistureDirection: " +
                        session.MacroClimate.PrevailingMoistureDirection + "\n" +
                        "MacroHumanGeographyHash: " + session.MacroHumanGeography.CanonicalHash + "\n" +
                        "TopologyHash: " + session.Topology.CanonicalHash + "\n" +
                        "ActiveSectorId: " + session.ActiveSectorId.Canonical);
                    WorldSessionService.Close();
                    Directory.Delete(root, true);
                }

                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"World Session Fresh Process {(create ? "A" : "B")}: FAIL\n" +
                    exception.GetType().Name + ": " + exception.Message);
                EditorApplication.Exit(1);
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

        private static bool SessionsEquivalent(WorldSession expected, WorldSession actual)
        {
            if (expected == null || actual == null ||
                expected.WorldId != actual.WorldId ||
                expected.DisplayName != actual.DisplayName ||
                expected.GenerationContext.WorldSeed != actual.GenerationContext.WorldSeed ||
                expected.GenerationContext.GeneratorVersion != actual.GenerationContext.GeneratorVersion ||
                 expected.HasMacroWorldPlan != actual.HasMacroWorldPlan ||
                 expected.HasMacroWorldPlan && expected.MacroWorldPlan.CanonicalHash != actual.MacroWorldPlan.CanonicalHash ||
                 expected.HasMacroGeography != actual.HasMacroGeography ||
                 expected.HasMacroGeography && expected.MacroGeography.CanonicalHash != actual.MacroGeography.CanonicalHash ||
                 expected.HasMacroWater != actual.HasMacroWater ||
                 expected.HasMacroWater && expected.MacroWater.CanonicalHash != actual.MacroWater.CanonicalHash ||
                 expected.HasMacroClimate != actual.HasMacroClimate ||
                 expected.HasMacroClimate && expected.MacroClimate.CanonicalHash != actual.MacroClimate.CanonicalHash ||
                 expected.HasMacroHumanGeography != actual.HasMacroHumanGeography ||
                 expected.HasMacroHumanGeography && expected.MacroHumanGeography.CanonicalHash != actual.MacroHumanGeography.CanonicalHash ||
                 expected.Topology.CanonicalHash != actual.Topology.CanonicalHash ||
                expected.ActiveSectorId != actual.ActiveSectorId ||
                expected.CreationContentEvidence.LoadedContentSetFingerprint !=
                actual.CreationContentEvidence.LoadedContentSetFingerprint ||
                expected.CreationContentEvidence.Sources.Count != actual.CreationContentEvidence.Sources.Count)
            {
                return false;
            }

            for (int index = 0; index < expected.CreationContentEvidence.Sources.Count; index++)
            {
                WorldCreationContentSourceEvidence left = expected.CreationContentEvidence.Sources[index];
                WorldCreationContentSourceEvidence right = actual.CreationContentEvidence.Sources[index];
                if (left.SourceId != right.SourceId || left.OwnedNamespace != right.OwnedNamespace ||
                    left.Version != right.Version || left.IsOfficialCore != right.IsOfficialCore ||
                    left.ProvenanceFingerprint != right.ProvenanceFingerprint)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool ContainsCatalogWorld(WorldSaveCatalogResult catalog, WorldId worldId)
        {
            return ContainsCatalogSlot(catalog, worldId.Canonical);
        }

        private static bool ContainsCatalogSlot(WorldSaveCatalogResult catalog, string slotId)
        {
            for (int index = 0; index < catalog.Entries.Count; index++)
            {
                if (catalog.Entries[index].SlotId == slotId)
                    return true;
            }
            return false;
        }

        private static bool ContainsCatalogIssue(WorldSaveCatalogResult catalog, string slotId)
        {
            for (int index = 0; index < catalog.Issues.Count; index++)
            {
                if (catalog.Issues[index].SlotId == slotId &&
                    !string.IsNullOrWhiteSpace(catalog.Issues[index].Failure))
                    return true;
            }
            return false;
        }

        private static bool ContainsBuildScene(EditorBuildSettingsScene[] scenes, string path)
        {
            for (int index = 0; index < scenes.Length; index++)
            {
                if (scenes[index].enabled && scenes[index].path == path)
                    return true;
            }
            return false;
        }

        private static List<T> FindSceneComponents<T>(Scene scene) where T : Component
        {
            var result = new List<T>();
            T[] all = Resources.FindObjectsOfTypeAll<T>();
            for (int index = 0; index < all.Length; index++)
            {
                if (all[index] != null && all[index].gameObject.scene == scene)
                    result.Add(all[index]);
            }
            return result;
        }

        private static void Complete(string title, List<string> failures, params string[] successLines)
        {
            if (failures.Count > 0)
            {
                string failure = title + ": FAIL\n- " + string.Join("\n- ", failures);
                Debug.LogError(failure);
                throw new InvalidOperationException(failure);
            }

            Debug.Log(title + ": PASS\n" + string.Join("\n", successLines));
        }

        private static void Check(bool condition, string failure, List<string> failures)
        {
            if (!condition)
                failures.Add(failure);
        }

        private static bool ValidateRestoredPlayer(
            WorldRuntimeSceneController runtime,
            out string failure)
        {
            failure = null;
            PlayerGameplayComposition player = runtime?.PlayerComposition;
            if (runtime == null || player == null || !runtime.GameplayStateReady ||
                runtime.PlayerBindSource != WorldRuntimePlayerBindSource.SaveRestore ||
                runtime.GameplayLoadResult?.Disposition != WorldGameplayLoadDisposition.Restored ||
                runtime.GameplayLoadResult.CurrentSliceResult?.Success != true)
            {
                failure = "runtime did not publish a transactionally restored Current Slice";
                return false;
            }
            if (!player.TryValidateRuntime(out failure))
                return false;

            Vector3 expectedPosition = new Vector3(
                SessionState.GetFloat(PlayPlayerPositionXKey, float.NaN),
                SessionState.GetFloat(PlayPlayerPositionYKey, float.NaN),
                SessionState.GetFloat(PlayPlayerPositionZKey, float.NaN));
            float expectedHealth = SessionState.GetFloat(PlayPlayerHealthKey, float.NaN);
            ActorHealthComponent health = player.PlayerContext.GetComponent<ActorHealthComponent>();
            if (!float.IsFinite(expectedPosition.x) || !float.IsFinite(expectedPosition.y) ||
                !float.IsFinite(expectedPosition.z) || !float.IsFinite(expectedHealth) ||
                Vector3.Distance(player.PlayerTransform.position, expectedPosition) > 0.05f)
            {
                failure = "saved local pose differs from restored pose; saved=" + expectedPosition +
                          ", restored=" + player.PlayerTransform.position;
                return false;
            }
            if (health == null || Mathf.Abs(health.CurrentHealth - expectedHealth) > 0.001f)
            {
                failure = "saved health differs from restored health; saved=" + expectedHealth +
                          ", restored=" + (health == null ? "<NONE>" : health.CurrentHealth.ToString("R"));
                return false;
            }
            if (player.PlayerIdentity.ActorInstanceId !=
                    SessionState.GetString(PlayPlayerActorIdKey, string.Empty) ||
                player.PersistentIdentity.PersistentId !=
                    SessionState.GetString(PlayPlayerPersistentIdKey, string.Empty))
            {
                failure = "authored player ActorInstanceId/PersistentSceneObjectId changed across load";
                return false;
            }

            int playerRoles = 0;
            foreach (ActorInteractionContext context in UnityEngine.Object.FindObjectsByType<ActorInteractionContext>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (context != null && Array.IndexOf(context.ActorTags, "player") >= 0)
                    playerRoles++;
            }
            ActorRuntimeIdentity[] registeredIdentities =
                UnityEngine.Object.FindObjectsByType<ActorRuntimeIdentity>(
                        FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(identity => identity != null && identity.IsRegistered)
                    .ToArray();
            bool playerRegistrationValid = ActorRuntimeRegistry.TryGet(
                player.PlayerIdentity.ActorInstanceId, out ActorRuntimeIdentity registeredPlayer) &&
                registeredPlayer == player.PlayerIdentity;
            if (playerRoles != 1 || !playerRegistrationValid ||
                ActorRuntimeRegistry.ActiveCount != registeredIdentities.Length ||
                UnityEngine.Object.FindObjectsByType<PlayerGameplayComposition>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1 ||
                UnityEngine.Object.FindObjectsByType<PlayerMovementController>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1 ||
                UnityEngine.Object.FindObjectsByType<PlayerMovementInputController>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1 ||
                UnityEngine.Object.FindObjectsByType<CameraRigController>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1 ||
                UnityEngine.Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1)
            {
                failure = "loaded runtime contains duplicate/missing player, movement, identity or camera authorities";
                return false;
            }
            return true;
        }

        private static bool RecordExpectedPlayerState(
            PlayerGameplayComposition player,
            out string failure)
        {
            failure = null;
            ActorHealthComponent health = player?.PlayerContext?.GetComponent<ActorHealthComponent>();
            if (player == null || player.PlayerTransform == null || player.PlayerIdentity == null ||
                player.PersistentIdentity == null || health == null)
            {
                failure = "shared player identity/pose/health authority is incomplete";
                return false;
            }
            Vector3 position = player.PlayerTransform.position;
            SessionState.SetString(PlayPlayerActorIdKey, player.PlayerIdentity.ActorInstanceId);
            SessionState.SetString(PlayPlayerPersistentIdKey, player.PersistentIdentity.PersistentId);
            SessionState.SetFloat(PlayPlayerPositionXKey, position.x);
            SessionState.SetFloat(PlayPlayerPositionYKey, position.y);
            SessionState.SetFloat(PlayPlayerPositionZKey, position.z);
            SessionState.SetFloat(PlayPlayerHealthKey, health.CurrentHealth);
            return true;
        }

        private static bool ValidateIntegratedGameplayRuntime(
            WorldRuntimeSceneController runtime,
            bool exerciseUi,
            out string failure)
        {
            failure = null;
            GameplayRuntimeComposition composition = runtime?.GameplayRuntimeComposition;
            DevelopmentGameplayIntegrationFixture fixture = runtime?.DevelopmentFixture;
            if (composition == null || !composition.TryValidate(out failure))
                return false;
            if (fixture == null || !fixture.TryValidate(out failure) || fixture.PlacementHeightRange > 3.001f)
            {
                failure = failure ?? "development fixture is absent or was not placed on a safe low-relief footprint";
                return false;
            }
            if (composition.Player != runtime.PlayerComposition ||
                composition.NeedsPanel == null ||
                composition.HealthWindow == null || composition.InventorySession == null ||
                composition.InventoryPanel == null || composition.StoragePanel == null ||
                composition.WorldInteraction == null || composition.FirearmController == null)
            {
                failure = "shared runtime surfaces are not bound to the actual product player";
                return false;
            }

            if (exerciseUi)
            {
                composition.InventorySession.OpenPersonal();
                if (!composition.InventorySession.IsOpen || !composition.InventoryPanel.IsVisible ||
                    !composition.InputBlocker.BlocksWorldInput)
                {
                    failure = "I/inventory session did not open the existing panel and block world input";
                    return false;
                }
                composition.HealthWindow.Open();
                if (!composition.HealthWindow.IsOpen || composition.InventorySession.IsOpen)
                {
                    failure = "H/health window did not arbitrate against the inventory session";
                    return false;
                }
                composition.InventorySession.OpenPersonal();
                if (composition.HealthWindow.IsOpen || !composition.InventorySession.IsOpen)
                {
                    failure = "inventory session did not close the health window on reopen";
                    return false;
                }
                composition.InventorySession.CloseSession();
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            PersistentSceneObjectId[] identities = UnityEngine.Object.FindObjectsByType<PersistentSceneObjectId>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < identities.Length; index++)
            {
                string id = identities[index].PersistentId;
                if (!PersistentSceneObjectId.IsValidFormat(id) || !ids.Add(id))
                {
                    failure = "loaded runtime contains an invalid or duplicate durable authored object identity";
                    return false;
                }
            }
            return true;
        }

        private static bool MutateIntegratedFixture(
            WorldRuntimeSceneController runtime,
            out int remainingContainerQuantity,
            out string pickedItemId,
            out string failure)
        {
            remainingContainerQuantity = -1;
            pickedItemId = null;
            failure = null;
            DevelopmentGameplayIntegrationFixture fixture = runtime?.DevelopmentFixture;
            PlayerGameplayComposition player = runtime?.PlayerComposition;
            if (fixture == null || player == null)
            {
                failure = "fixture/player is unavailable";
                return false;
            }

            ActorInteractionContext actor = player.PlayerContext;
            InventoryComponent inventory = actor.GetInventoryComponent();
            ContainerLootComponent selectedContainer = null;
            PersistentSceneObjectId selectedContainerIdentity = null;
            ContainerLootComponent[] containers = fixture.GetComponentsInChildren<ContainerLootComponent>(true);
            Array.Sort(containers, (left, right) => string.CompareOrdinal(left.name, right.name));
            for (int index = 0; index < containers.Length; index++)
            {
                ContainerLootComponent container = containers[index];
                WorldObjectTags tags = container.GetComponent<WorldObjectTags>();
                var context = new DebugActionExecutionContext(actor, tags, actor.GetEquippedItemDefinitionId());
                container.Search(context);
                if (!container.HasStoredItems) continue;
                int moved = container.TakeItem(0, 1, inventory, context, null, out _);
                if (moved != 1) continue;
                selectedContainer = container;
                selectedContainerIdentity = container.GetComponent<PersistentSceneObjectId>();
                remainingContainerQuantity = container.StoredItemQuantity;
                break;
            }
            if (selectedContainer == null || selectedContainerIdentity == null)
            {
                failure = "no authored fixture container completed search + transfer to player";
                return false;
            }
            SessionState.SetString(PlayFixtureContainerIdKey, selectedContainerIdentity.PersistentId);

            WorldItemPickup[] pickups = fixture.GetComponentsInChildren<WorldItemPickup>(true);
            Array.Sort(pickups, (left, right) => string.CompareOrdinal(
                left.AuthoredItemInstanceId, right.AuthoredItemInstanceId));
            WorldItemPickup pickup = null;
            for (int index = 0; index < pickups.Length; index++)
            {
                if (pickups[index].ItemDefinitionId == "core:rusted_crowbar_01" ||
                    pickups[index].ItemDefinitionId == "rusted_crowbar_01")
                {
                    pickup = pickups[index];
                    break;
                }
            }
            if (pickup == null || string.IsNullOrWhiteSpace(pickup.AuthoredItemInstanceId))
            {
                failure = "authored crowbar pickup fixture is missing";
                return false;
            }
            pickedItemId = pickup.AuthoredItemInstanceId;
            DebugActionExecutionResult pickupResult = pickup.PickUp(
                actor, pickup.GetComponent<WorldObjectTags>());
            if (!pickupResult.hasResult || pickup.Quantity != 0 ||
                !pickup.GetComponent<WorldObjectTags>().HasTag("picked_up"))
            {
                failure = "authored world item pickup did not commit through the existing transfer authority";
                return false;
            }
            if (!inventory.TryGetEntryByInstanceId(pickedItemId, out int pickedIndex, out _) ||
                !inventory.TryEquipIndexToRightHand(pickedIndex) ||
                actor.GetEquippedItemDefinitionId() != "core:rusted_crowbar_01")
            {
                failure = "picked authored crowbar did not equip through the existing inventory/equipment authority";
                return false;
            }

            DoorSwingController[] doors = fixture.GetComponentsInChildren<DoorSwingController>(true);
            Array.Sort(doors, (left, right) => string.CompareOrdinal(left.name, right.name));
            DoorSwingController door = null;
            for (int index = 0; index < doors.Length; index++)
            {
                WorldObjectTags tags = doors[index].GetComponent<WorldObjectTags>();
                if (tags != null && tags.HasTag("locked_door"))
                {
                    door = doors[index];
                    break;
                }
            }
            PersistentSceneObjectId doorIdentity = door?.GetComponent<PersistentSceneObjectId>();
            if (door == null || doorIdentity == null)
            {
                failure = "locked authored door fixture is missing";
                return false;
            }
            WorldObjectTags doorTags = door.GetComponent<WorldObjectTags>();
            var query = new InteractionQuery
            {
                Database = GameDataManager.Instance.Database,
                ActorTags = actor.ActorTags,
                ActorStats = actor.BuildActorStatsDictionary(),
                EquippedItemId = actor.GetEquippedItemDefinitionId(),
                Target = doorTags,
                RequiredContext = "world_interaction"
            };
            bool forceAvailable = false;
            foreach (var action in new InteractionSystem().GetAvailableActions(query))
                if (action.id == "core:force_door") forceAvailable = true;
            if (!forceAvailable)
            {
                failure = "contextual resolver did not expose the existing force-door action";
                return false;
            }
            DebugActionExecutor.Execute(
                GameDataManager.Instance.Database.GetAction("core:force_door"),
                new DebugActionExecutionContext(actor, doorTags, actor.GetEquippedItemDefinitionId()));
            if (!doorTags.HasTag("opened_door") || doorTags.HasTag("locked_door"))
            {
                failure = "existing force-door action did not mutate the authored fixture door";
                return false;
            }
            SessionState.SetString(PlayFixtureDoorIdKey, doorIdentity.PersistentId);
            return true;
        }

        private static bool ValidateRestoredFixture(
            WorldRuntimeSceneController runtime,
            out string failure)
        {
            failure = null;
            DevelopmentGameplayIntegrationFixture fixture = runtime?.DevelopmentFixture;
            if (fixture == null)
            {
                failure = "development fixture was not re-established before Current Slice apply";
                return false;
            }

            string containerId = SessionState.GetString(PlayFixtureContainerIdKey, string.Empty);
            string doorId = SessionState.GetString(PlayFixtureDoorIdKey, string.Empty);
            string pickedId = SessionState.GetString(PlayFixturePickedItemIdKey, string.Empty);
            int expectedQuantity = SessionState.GetInt(PlayFixtureContainerQuantityKey, -1);
            ContainerLootComponent restoredContainer = null;
            DoorSwingController restoredDoor = null;
            PersistentSceneObjectId[] identities = fixture.GetComponentsInChildren<PersistentSceneObjectId>(true);
            for (int index = 0; index < identities.Length; index++)
            {
                if (identities[index].PersistentId == containerId)
                    restoredContainer = identities[index].GetComponent<ContainerLootComponent>();
                if (identities[index].PersistentId == doorId)
                    restoredDoor = identities[index].GetComponent<DoorSwingController>();
            }
            if (restoredContainer == null || restoredContainer.StoredItemQuantity != expectedQuantity)
            {
                failure = "container contents did not round-trip through Current Slice";
                return false;
            }
            WorldObjectTags doorTags = restoredDoor != null ? restoredDoor.GetComponent<WorldObjectTags>() : null;
            if (doorTags == null || !doorTags.HasTag("opened_door") || doorTags.HasTag("locked_door"))
            {
                failure = "door state did not round-trip through Current Slice";
                return false;
            }
            WorldItemPickup[] pickups = fixture.GetComponentsInChildren<WorldItemPickup>(true);
            WorldItemPickup restoredPickup = null;
            for (int index = 0; index < pickups.Length; index++)
                if (pickups[index].AuthoredItemInstanceId == pickedId) restoredPickup = pickups[index];
            WorldObjectTags pickupTags = restoredPickup != null
                ? restoredPickup.GetComponent<WorldObjectTags>()
                : null;
            if (restoredPickup == null || restoredPickup.Quantity != 0 ||
                pickupTags == null || !pickupTags.HasTag("picked_up"))
            {
                failure = "authored pickup absence did not round-trip through Current Slice";
                return false;
            }
            return true;
        }

        private static bool ValidateRuntimeMaterialization(
            WorldRuntimeSceneController runtime,
            out string failure)
        {
            failure = null;
            WorldTerrainMaterializationController materialization = runtime?.MaterializationController;
            TerrainMaterializationResult result = materialization?.Result;
            if (materialization == null || !materialization.IsReady || result == null)
            {
                failure = materialization?.Failure ?? "materialization controller/result is absent";
                return false;
            }
            PlayerGameplayComposition player = runtime.PlayerComposition;
            string playerFailure = null;
            if (result.Terrain == null || result.TerrainCollider == null ||
                player == null || !player.TryValidateRuntime(out playerFailure) ||
                player.PlayerContext.GetComponent<UnityEngine.AI.NavMeshAgent>() != null ||
                materialization.GeneratedRoot.GetComponentInChildren<PlayerGameplayComposition>(true) != null ||
                materialization.GeneratedRoot.GetComponentInChildren<Camera>(true) != null ||
                result.NavMeshSurface == null || result.NavMeshSurface.navMeshData == null ||
                result.NavMeshVertexCount < 1 || result.PathCorners.Count < 2)
            {
                failure = "Terrain/player-composition/local NavMesh/path contract is incomplete" +
                          (string.IsNullOrWhiteSpace(playerFailure) ? string.Empty : ": " + playerFailure);
                return false;
            }
            return ValidateActorNavigationOnMaterializedTerrain(result, out failure);
        }

        private static bool ValidateActorNavigationOnMaterializedTerrain(
            TerrainMaterializationResult result,
            out string failure)
        {
            const string profileId = "core:debug_navigation_npc_01";
            failure = null;
            ActorRuntimeIdentity identity = null;
            bool valid = false;
            try
            {
                Vector3 start = result.PathCorners[0];
                Vector3 destination = result.PathCorners[result.PathCorners.Count - 1];
                if (!ActorSpawnService.TrySpawn(
                        profileId, start, Quaternion.identity, out identity, out string spawnError))
                {
                    failure = "ActorNavigationController fixture could not spawn: " + spawnError;
                }
                else
                {
                    ActorNavigationController navigation = identity.GetComponent<ActorNavigationController>();
                    UnityEngine.AI.NavMeshAgent agent = identity.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (navigation == null || agent == null || !agent.isOnNavMesh)
                    {
                        failure = "generated-terrain actor is missing its configured navigation authority or NavMesh binding";
                    }
                    else if (!navigation.TryNavigate(destination, out ActorNavigationResult order) ||
                             !order.Accepted || order.Failure != ActorNavigationFailure.None)
                    {
                        failure = "ActorNavigationController rejected the generated-terrain path: " +
                                  order.Failure + " / " + order.Detail;
                    }
                    else
                    {
                        valid = true;
                    }
                }
            }
            finally
            {
                if (identity != null && identity.IsRegistered &&
                    !ActorSpawnService.TryRemoveRuntimeRepresentationForRestore(
                        identity.ActorInstanceId, out string cleanupError))
                {
                    valid = false;
                    failure = "ActorNavigationController fixture cleanup failed: " + cleanupError;
                }
            }
            return valid;
        }

        private const string WorldCreatedPrefix = "[Worldgen][WORLD_CREATED]";
        private const string LoadOkPrefix = "[WorldSession][LOAD_OK]";
        private const string SessionReadyPrefix = "[WorldRuntime][SESSION_READY]";
        private const string MaterializationReadyPrefix = "[WorldMaterialization][READY]";
        private const string SaveOkPrefix = "[WorldSession][SAVE_OK]";
        private const string WriteCommitPrefix = "[Persistence][WRITE_COMMIT]";
        private const string GameplaySaveOkPrefix = "[WorldSave][GAMEPLAY_SAVE_OK]";
        private const string GameplayLoadOkPrefix = "[WorldSave][GAMEPLAY_LOAD_OK]";
        private const string GameplayAbsentPrefix = "[WorldSave][GAMEPLAY_STATE_ABSENT_LEGACY]";
        private const string GameplayLoadFailPrefix = "[WorldSave][GAMEPLAY_LOAD_FAIL]";
        private const string PlayerBoundPrefix = "[WorldRuntime][PLAYER_BOUND]";

        private static void CapturePlayLifecycleLog(string message, string stackTrace, LogType type)
        {
            if (!SessionState.GetBool(PlayPendingKey, false) || string.IsNullOrEmpty(message))
                return;
            if (message.StartsWith(WorldCreatedPrefix, StringComparison.Ordinal))
                IncrementPlayLog(PlayWorldCreatedLogCountKey);
            else if (message.StartsWith(LoadOkPrefix, StringComparison.Ordinal))
                IncrementPlayLog(PlayLoadOkLogCountKey);
            else if (message.StartsWith(SessionReadyPrefix, StringComparison.Ordinal))
                IncrementPlayLog(PlaySessionReadyLogCountKey);
            else if (message.StartsWith(MaterializationReadyPrefix, StringComparison.Ordinal))
                IncrementPlayLog(PlayMaterializationReadyLogCountKey);
            else if (message.StartsWith(SaveOkPrefix, StringComparison.Ordinal))
                IncrementPlayLog(PlaySaveOkLogCountKey);
            else if (message.StartsWith(WriteCommitPrefix, StringComparison.Ordinal))
                IncrementPlayLog(PlayWriteCommitLogCountKey);
            else if (message.StartsWith(GameplaySaveOkPrefix, StringComparison.Ordinal))
                IncrementPlayLog(PlayGameplaySaveOkLogCountKey);
            else if (message.StartsWith(GameplayLoadOkPrefix, StringComparison.Ordinal))
                IncrementPlayLog(PlayGameplayLoadOkLogCountKey);
            else if (message.StartsWith(GameplayAbsentPrefix, StringComparison.Ordinal))
                IncrementPlayLog(PlayGameplayAbsentLogCountKey);
            else if (message.StartsWith(GameplayLoadFailPrefix, StringComparison.Ordinal))
                IncrementPlayLog(PlayGameplayLoadFailLogCountKey);
            else if (message.StartsWith(PlayerBoundPrefix, StringComparison.Ordinal))
                IncrementPlayLog(PlayPlayerBoundLogCountKey);
        }

        private static void IncrementPlayLog(string key) =>
            SessionState.SetInt(key, SessionState.GetInt(key, 0) + 1);

        private static int PlayLogCount(string key) => SessionState.GetInt(key, 0);

        private static bool ContainsAll(string message, params string[] values)
        {
            if (string.IsNullOrEmpty(message))
                return false;
            for (int index = 0; index < values.Length; index++)
                if (!message.Contains(values[index]))
                    return false;
            return true;
        }

        private sealed class LifecycleLogCapture : IDisposable
        {
            private readonly List<string> messages = new List<string>();

            public LifecycleLogCapture()
            {
                Application.logMessageReceived += Capture;
            }

            public int Count(string prefix)
            {
                int count = 0;
                for (int index = 0; index < messages.Count; index++)
                    if (messages[index].StartsWith(prefix, StringComparison.Ordinal)) count++;
                return count;
            }

            public string Single(string prefix)
            {
                string found = null;
                for (int index = 0; index < messages.Count; index++)
                {
                    if (!messages[index].StartsWith(prefix, StringComparison.Ordinal)) continue;
                    if (found != null) return null;
                    found = messages[index];
                }
                return found;
            }

            public string Last(string prefix)
            {
                for (int index = messages.Count - 1; index >= 0; index--)
                    if (messages[index].StartsWith(prefix, StringComparison.Ordinal)) return messages[index];
                return null;
            }

            public void Dispose()
            {
                Application.logMessageReceived -= Capture;
            }

            private void Capture(string message, string stackTrace, LogType type)
            {
                messages.Add(message);
            }
        }
    }
}
