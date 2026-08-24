using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using OldScars.Core;
using OldScars.Core.ApplicationShell;
using OldScars.Core.Data.Loading;
using OldScars.Core.Data.Validation;
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
        private const string PlayTopologyHashKey = "OldScars.WorldSessionApplicationDiagnostics.PlayTopologyHash";
        private const string FreshRootEnvironment = "OLD_SCARS_WORLD_SESSION_DIAGNOSTIC_ROOT";
        private const long ExplicitSeed = -3141592653589793L;

        static WorldSessionApplicationDiagnostics()
        {
            EditorApplication.update += RunPlayModeWhenReady;
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
                "- world_session_v1 schema 4 semantic preflight and exact committed geography/water round-trip",
                "- duplicate display names with distinct WorldId slots",
                "- safe catalog filtering plus corrupt-save isolation",
                "- create/close/load lifecycle without partial publication",
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
                  first.HasMacroWater && first.HasGameplayQuality &&
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
                  firstPayload["creationContentProvenance"]?["sources"] is JArray,
                "world_session_v1 schema 4 must expose identity/context/plan/geography/water/topology/provenance.",
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

            JObject invalidActiveSector = (JObject)valid.DeepClone();
            SectorId otherSector = SectorId.FromDeterministicDomain(
                WorldDeterminism.DeriveDomainKey(
                    create.Session.GenerationContext, "topology", "other_sector"));
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
            Check(FindSceneComponents<WorldRuntimeSceneController>(runtime).Count == 1,
                "J. World Runtime scene must wire one WorldRuntimeSceneController.", failures);
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

                    runtime.OpenMenu();
                    if (!runtime.IsMenuOpen || !runtime.SaveGame(store))
                    {
                        FailPlay("In-game menu did not open or Save did not invoke WorldSession persistence.", root);
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
                    string topologyHash = SessionState.GetString(PlayTopologyHashKey, string.Empty);
                    WorldRuntimeSceneController runtime =
                        UnityEngine.Object.FindAnyObjectByType<WorldRuntimeSceneController>();
                    if (loaded == null || runtime == null || loaded.WorldId.Canonical != worldId ||
                        !loaded.HasMacroWorldPlan || loaded.MacroWorldPlan.CanonicalHash != planHash ||
                        !loaded.HasMacroGeography || loaded.MacroGeography.CanonicalHash != geographyHash ||
                        !loaded.HasMacroWater || loaded.MacroWater.CanonicalHash != waterHash ||
                        loaded.Topology.CanonicalHash != topologyHash)
                    {
                        FailPlay("Load action did not restore the recorded world/topology in World Runtime.", root);
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
                        FailPlay("Final Return to Main Menu left a stale WorldSession.", root);
                        return;
                    }

                    CleanupPlayState(root);
                    Debug.Log(
                        "World Session Application Play Flow: PASS\n" +
                        "- Main Menu Create -> persisted session -> World Runtime\n" +
                        "- in-game menu open/continue/save\n" +
                        "- Return to Main Menu clears session without implicit save\n" +
                        "- Main Menu Load restores same world and re-enters runtime\n" +
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
            SessionState.EraseString(PlayTopologyHashKey);
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
                        "TopologyHash: " + session.Topology.CanonicalHash + "\n" +
                        "ActiveSectorId: " + session.ActiveSectorId.Canonical);
                }
                else
                {
                    string[] record = File.ReadAllLines(recordPath);
                    if (record.Length != 9)
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
                        session.Topology.CanonicalHash != record[7] ||
                        session.ActiveSectorId.Canonical != record[8])
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
    }
}
