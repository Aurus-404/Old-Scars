using System;
using System.Globalization;
using System.IO;
using OldScars.Core;
using OldScars.Core.Actors;
using OldScars.Core.ApplicationShell;
using OldScars.Core.Persistence;
using OldScars.Core.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldScars.EditorTools
{
    [InitializeOnLoad]
    public static class WorldRuntimeContinuityFreshProcessDiagnostics
    {
        private const string RootEnvironment = "OLD_SCARS_WORLD_RUNTIME_CONTINUITY_ROOT";
        private const string PendingKey = "OldScars.WorldRuntimeFresh.Pending";
        private const string ModeKey = "OldScars.WorldRuntimeFresh.Mode";
        private const string PhaseKey = "OldScars.WorldRuntimeFresh.Phase";
        private const string WorldIdKey = "OldScars.WorldRuntimeFresh.WorldId";
        private const long Seed = 41873199110427L;

        static WorldRuntimeContinuityFreshProcessDiagnostics()
        {
            EditorApplication.update += RunWhenReady;
        }

        public static void CreateAndSave()
        {
            Begin("create");
        }

        public static void DiscoverAndLoad()
        {
            Begin("load");
        }

        private static void Begin(string mode)
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Fresh-process runtime continuity requires batchmode.");
            string root = Environment.GetEnvironmentVariable(RootEnvironment);
            if (string.IsNullOrWhiteSpace(root))
                throw new InvalidOperationException(RootEnvironment + " is missing.");

            SessionState.SetString(ModeKey, mode);
            SessionState.SetInt(PhaseKey, 0);
            SessionState.SetBool(PendingKey, true);
            EditorSceneManager.OpenScene(WorldApplicationScenes.MainMenuScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void RunWhenReady()
        {
            if (!SessionState.GetBool(PendingKey, false) || !EditorApplication.isPlaying)
                return;

            string root = Environment.GetEnvironmentVariable(RootEnvironment);
            try
            {
                if (string.IsNullOrWhiteSpace(root))
                    throw new InvalidOperationException(RootEnvironment + " disappeared.");
                string mode = SessionState.GetString(ModeKey, string.Empty);
                int phase = SessionState.GetInt(PhaseKey, 0);
                var store = new PersistenceFileStore(root);

                if (phase == 0)
                {
                    if (SceneManager.GetActiveScene().name != WorldApplicationScenes.MainMenuSceneName ||
                        GameDataManager.Instance?.IsReady != true ||
                        GameDataManager.Instance.LoadedContentSet == null)
                        return;
                    MainMenuSceneController menu =
                        UnityEngine.Object.FindAnyObjectByType<MainMenuSceneController>();
                    if (menu == null)
                        throw new InvalidOperationException("Main Menu controller is missing.");

                    if (mode == "create")
                    {
                        if (!menu.TryCreateWorld(
                                "Fresh Runtime Continuity", Seed.ToString(CultureInfo.InvariantCulture),
                                WorldSizePreset.Small, LandCoveragePreset.Medium, store))
                            throw new InvalidOperationException("Process A New Game failed.");
                        SessionState.SetString(
                            WorldIdKey,
                            WorldSessionService.ActiveSession.WorldId.Canonical);
                    }
                    else if (mode == "load")
                    {
                        string[] record = File.ReadAllLines(RecordPath(root));
                        if (record.Length != 11 || !menu.TryLoadWorld(record[0], store))
                            throw new InvalidOperationException("Process B save discovery/load failed.");
                        SessionState.SetString(WorldIdKey, record[0]);
                    }
                    else
                    {
                        throw new InvalidOperationException("Fresh-process mode is invalid: " + mode);
                    }
                    SessionState.SetInt(PhaseKey, 1);
                    return;
                }

                if (SceneManager.GetActiveScene().name != WorldApplicationScenes.WorldRuntimeSceneName)
                    return;
                WorldRuntimeSceneController runtime =
                    UnityEngine.Object.FindAnyObjectByType<WorldRuntimeSceneController>();
                if (runtime == null || runtime.MaterializationController?.IsReady != true ||
                    runtime.PlayerComposition == null || !runtime.GameplayStateReady)
                    return;

                if (mode == "create")
                    CompleteCreate(root, store, runtime);
                else
                    CompleteLoad(root, runtime);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "World Runtime Fresh Process " +
                    (SessionState.GetString(ModeKey, string.Empty) == "create" ? "A" : "B") +
                    ": FAIL\n" + exception.GetType().Name + ": " + exception.Message);
                ClearState();
                EditorApplication.Exit(1);
            }
        }

        private static void CompleteCreate(
            string root,
            PersistenceFileStore store,
            WorldRuntimeSceneController runtime)
        {
            if (!runtime.GameplayStateReady ||
                runtime.PlayerBindSource != WorldRuntimePlayerBindSource.NewGameSafeSpawn)
                throw new InvalidOperationException("Process A runtime/player bootstrap is not ready.");
            PlayerGameplayComposition player = runtime.PlayerComposition;
            player.PlacePlayerAtSurface(
                runtime.MaterializationController.Result.PathDestination,
                Quaternion.Euler(0f, 71f, 0f));
            ActorHealthComponent health = player.PlayerContext.GetComponent<ActorHealthComponent>();
            if (health == null || !health.ApplyDamage(11f))
                throw new InvalidOperationException("Process A health mutation failed.");
            runtime.OpenMenu();
            if (!runtime.SaveGame(store))
                throw new InvalidOperationException("Process A coherent runtime Save failed: " + runtime.StatusMessage);

            WorldSession session = WorldSessionService.ActiveSession;
            CurrentSliceResult capture = CurrentSliceSnapshotService.Capture();
            if (!capture.Success)
                throw new InvalidOperationException("Process A post-save capture failed: " + capture.Failure);
            PlayerState saved = capture.Snapshot.player;
            Directory.CreateDirectory(root);
            File.WriteAllLines(RecordPath(root), new[]
            {
                session.WorldId.Canonical,
                session.ActiveSectorId.Canonical,
                session.Topology.CanonicalHash,
                session.GenerationContext.WorldSeed.Canonical,
                saved.actorInstanceId,
                saved.persistentId,
                saved.pose.position.x.ToString("R", CultureInfo.InvariantCulture),
                saved.pose.position.y.ToString("R", CultureInfo.InvariantCulture),
                saved.pose.position.z.ToString("R", CultureInfo.InvariantCulture),
                saved.currentHealth.ToString("R", CultureInfo.InvariantCulture),
                WorldGameplayPersistenceService.GetSlotId(session.WorldId)
            });
            Debug.Log(
                "World Runtime Fresh Process A: PASS\n" +
                "WorldId: " + session.WorldId.Canonical + "\n" +
                "SectorId: " + session.ActiveSectorId.Canonical + "\n" +
                "ActorInstanceId: " + saved.actorInstanceId + "\n" +
                "LocalPosition: (" + saved.pose.position.x.ToString("R", CultureInfo.InvariantCulture) +
                ", " + saved.pose.position.y.ToString("R", CultureInfo.InvariantCulture) +
                ", " + saved.pose.position.z.ToString("R", CultureInfo.InvariantCulture) + ")\n" +
                "Health: " + saved.currentHealth.ToString("R", CultureInfo.InvariantCulture));
            ClearState();
            EditorApplication.Exit(0);
        }

        private static void CompleteLoad(string root, WorldRuntimeSceneController runtime)
        {
            string[] record = File.ReadAllLines(RecordPath(root));
            WorldSession session = WorldSessionService.ActiveSession;
            PlayerGameplayComposition player = runtime.PlayerComposition;
            ActorHealthComponent health = player.PlayerContext.GetComponent<ActorHealthComponent>();
            Vector3 expected = new Vector3(
                ParseFloat(record[6]), ParseFloat(record[7]), ParseFloat(record[8]));
            float expectedHealth = ParseFloat(record[9]);
            if (!runtime.GameplayStateReady ||
                runtime.PlayerBindSource != WorldRuntimePlayerBindSource.SaveRestore ||
                runtime.GameplayLoadResult?.CurrentSliceResult?.Success != true ||
                session.WorldId.Canonical != record[0] ||
                session.ActiveSectorId.Canonical != record[1] ||
                session.Topology.CanonicalHash != record[2] ||
                session.GenerationContext.WorldSeed.Canonical != record[3] ||
                player.PlayerIdentity.ActorInstanceId != record[4] ||
                player.PersistentIdentity.PersistentId != record[5] ||
                Vector3.Distance(player.PlayerTransform.position, expected) > 0.05f ||
                health == null || Mathf.Abs(health.CurrentHealth - expectedHealth) > 0.001f ||
                UnityEngine.Object.FindObjectsByType<PlayerGameplayComposition>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1)
            {
                throw new InvalidOperationException(
                    "Process B restored evidence differs from Process A. ExpectedPosition=" + expected +
                    ", ActualPosition=" + player.PlayerTransform.position +
                    ", ExpectedHealth=" + expectedHealth +
                    ", ActualHealth=" + (health == null ? "<NONE>" : health.CurrentHealth.ToString("R")));
            }
            Debug.Log(
                "World Runtime Fresh Process B: PASS\n" +
                "WorldId: " + session.WorldId.Canonical + "\n" +
                "SectorId: " + session.ActiveSectorId.Canonical + "\n" +
                "ActorInstanceId: " + player.PlayerIdentity.ActorInstanceId + "\n" +
                "LocalPosition: " + player.PlayerTransform.position + "\n" +
                "Health: " + health.CurrentHealth.ToString("R", CultureInfo.InvariantCulture));
            ClearState();
            Directory.Delete(root, true);
            EditorApplication.Exit(0);
        }

        private static float ParseFloat(string value) =>
            float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);

        private static string RecordPath(string root) =>
            Path.Combine(root, "runtime-continuity-record.txt");

        private static void ClearState()
        {
            SessionState.EraseBool(PendingKey);
            SessionState.EraseString(ModeKey);
            SessionState.EraseInt(PhaseKey);
            SessionState.EraseString(WorldIdKey);
        }
    }
}
