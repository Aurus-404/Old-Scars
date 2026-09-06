using System;
using System.IO;
using System.Reflection;
using OldScars.Core;
using OldScars.Core.Actors;
using OldScars.Core.ApplicationShell;
using OldScars.Core.Data;
using OldScars.Core.Persistence;
using OldScars.Core.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace OldScars.Editor
{
    /// <summary>
    /// Exercises the development-only F6 presentation against the real WorldRuntime.
    /// It deliberately reads the panel's presentation seam instead of evaluating perception itself.
    /// </summary>
    [InitializeOnLoad]
    public static class M41F6ObservabilityDiagnostics
    {
        private const string PendingKey = "OldScars.M41.F6Observability.Pending";
        private const string StageKey = "OldScars.M41.F6Observability.Stage";
        private const string FailureKey = "OldScars.M41.F6Observability.Failure";
        private const string RootKey = "OldScars.M41.F6Observability.Root";
        private const string ScreenshotKey = "OldScars.M41.F6Observability.Screenshot";
        private const long WorldSeed = 941600001L;
        private const long SandboxSeed = 41600001L;

        private static WorldRuntimeSceneController runtime;
        private static SandboxNpcController sandbox;
        private static SandboxNpcObservabilityPanel panel;
        private static SandboxNpcMetadata blueMetadata;
        private static SandboxNpcMetadata redMetadata;
        private static ActorRuntimeIdentity blue;
        private static ActorRuntimeIdentity red;
        private static float stageStartedAt;
        private static Vector3 movedBlueOrigin;
        private static int historicalEvidenceCount;
        private static bool redTerminated;

        static M41F6ObservabilityDiagnostics()
        {
            EditorApplication.update -= Continue;
            EditorApplication.update += Continue;
        }

        public static void RunBatchWorldRuntime()
        {
            if (!Application.isBatchMode || EditorApplication.isCompiling)
                throw new InvalidOperationException("F6 Observability diagnostics require compiled Unity batchmode.");

            string root = Path.Combine(Path.GetTempPath(), "OldScars_M41_F6_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            SessionState.SetString(RootKey, root);
            SessionState.SetString(ScreenshotKey, Path.Combine(root, "M41_F6_MultiNpc.png"));
            SessionState.SetBool(PendingKey, true);
            SessionState.SetInt(StageKey, 0);
            SessionState.EraseString(FailureKey);
            WorldRuntimeTerrainDevelopmentSettings.SetDiagnosticSelectionOverride(
                WorldRuntimeTerrainDevelopmentSelection.VolumetricIndexedMarchingCubes);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("GameDataManager").AddComponent<GameDataManager>();
            EditorApplication.EnterPlaymode();
        }

        private static void Continue()
        {
            if (!SessionState.GetBool(PendingKey, false))
                return;

            try
            {
                WorldRuntimeTerrainDevelopmentSettings.SetDiagnosticSelectionOverride(
                    WorldRuntimeTerrainDevelopmentSelection.VolumetricIndexedMarchingCubes);
                if (EditorApplication.isPlaying)
                {
                    RunPlayStage();
                    return;
                }

                if (!EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetInt(StageKey, 0) == 99)
                    Finish(0);
                else if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    Finish(1, "F6 Observability WorldRuntime diagnostic was interrupted before completion.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetString(FailureKey, exception.Message);
                SessionState.SetInt(StageKey, 99);
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorApplication.ExitPlaymode();
                else
                    Finish(1, exception.Message);
            }
        }

        private static void RunPlayStage()
        {
            int stage = SessionState.GetInt(StageKey, 0);
            if (stage == 0)
            {
                if (Time.frameCount < 5 || GameDataManager.Instance?.IsReady != true)
                    return;
                CreateWorldSessionAndLoadRuntime();
                SetStage(1);
                return;
            }

            runtime = UnityEngine.Object.FindAnyObjectByType<WorldRuntimeSceneController>();
            if (runtime == null || !runtime.GameplayStateReady || runtime.GameplayRuntimeComposition == null)
            {
                if (Time.time - stageStartedAt > 30f)
                    throw new InvalidOperationException("Real WorldRuntime did not reach F6 observability readiness.");
                return;
            }

            switch (stage)
            {
                case 1: SetupMultiNpcVisuals(); SetStage(2); break;
                case 2:
                    if (VerifyConcurrentCurrentVisualsAndCapture()) SetStage(3);
                    break;
                case 3:
                    if (MoveBlueAndVerifyCurrentOrigin()) SetStage(4);
                    break;
                case 4: VerifyDeadSuppression(); break;
            }
        }

        private static void CreateWorldSessionAndLoadRuntime()
        {
            WorldSessionService.Close();
            var store = new PersistenceFileStore(SessionState.GetString(RootKey, string.Empty));
            WorldSessionOperationResult created = WorldSessionService.Create(
                "M41 F6 Observability", new OldScars.Core.World.WorldSeed(WorldSeed),
                WorldGenerationSettings.ResolvePreset(WorldSizePreset.Small), LandCoveragePreset.High,
                GameDataManager.Instance.LoadedContentSet, store);
            Require(created.Success, "Could not create F6 Observability WorldSession: " + created.Failure);
            SceneManager.LoadScene(WorldApplicationScenes.WorldRuntimeSceneName, LoadSceneMode.Single);
        }

        private static void SetupMultiNpcVisuals()
        {
            sandbox = runtime.GameplayRuntimeComposition.SandboxNpcController;
            panel = runtime.GameplayRuntimeComposition.SandboxNpcObservability;
            string seedError = null;
            bool seeded = sandbox != null && panel != null &&
                          sandbox.TrySetBaseSeed(SandboxSeed.ToString(), out seedError);
            Require(seeded,
                "F6 sandbox/panel setup failed: " + seedError);
            Require(sandbox.TrySpawnBlueNpc(out blueMetadata, out string blueError),
                "Blue spawn failed: " + blueError);
            Require(sandbox.TrySpawnRedNpc(out redMetadata, out string redError),
                "Red spawn failed: " + redError);
            blue = blueMetadata.GetComponent<ActorRuntimeIdentity>();
            red = redMetadata.GetComponent<ActorRuntimeIdentity>();
            Require(blue != null && red != null && blue.IsRegistered && red.IsRegistered,
                "Blue/Red runtime identities were not registered.");

            Camera camera = runtime.PlayerComposition.GameplayCamera;
            Vector3 forward = Vector3.ProjectOnPlane(camera != null ? camera.transform.forward : Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();
            Vector3 lateral = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 center = runtime.PlayerComposition.PlayerTransform.position + forward * 11f;
            bool blueSurface = TryFindSurface(center - lateral * 3f, out Vector3 bluePosition);
            bool redSurface = TryFindSurface(center + lateral * 3f, out Vector3 redPosition);
            Require(blueSurface && redSurface,
                "Could not find visible NavMesh positions for Blue and Red.");
            PlaceNpc(blue, bluePosition, Quaternion.LookRotation(redPosition - bluePosition));
            PlaceNpc(red, redPosition, Quaternion.LookRotation(bluePosition - redPosition));
            panel.SetVisibleForDiagnostics(true);
            Require(panel.IsVisible, "F6 diagnostic visibility seam did not enable presentation.");
        }

        private static bool VerifyConcurrentCurrentVisualsAndCapture()
        {
            if (Time.time - stageStartedAt < 0.35f)
                return false;
            Require(panel.CurrentWorldVisualActorCount >= 2,
                "F6 did not present current FOV/Gaze for both Blue and Red. Count=" + panel.CurrentWorldVisualActorCount + ".");
            Require(!SandboxNpcObservabilityPanel.TryGetLastQuerySegment(default, out _, out _, out string absentLabel) &&
                    absentLabel == "LAST: No evidence", "Missing LAST evidence was presented as a valid query.");
            Camera camera = runtime.PlayerComposition.GameplayCamera;
            Vector3 blueScreen = camera.WorldToViewportPoint(ExpectedEye(blue));
            Vector3 redScreen = camera.WorldToViewportPoint(ExpectedEye(red));
            Require(OnScreen(blueScreen) && OnScreen(redScreen),
                "Eligible Blue/Red eye origins are not simultaneously drawable inside the camera viewport.");
            Require(panel.Selected == redMetadata,
                "F6 selected inspector did not retain the last-spawn Red actor while drawing multi-NPC world visuals.");
            bool blueOriginResolved = panel.TryGetCurrentVisualOrigin(blueMetadata, out Vector3 blueOrigin);
            bool redOriginResolved = panel.TryGetCurrentVisualOrigin(redMetadata, out Vector3 redOrigin);
            Require(blueOriginResolved && redOriginResolved,
                "F6 could not resolve the current eye origin for both active actors.");
            Require(Approximately(blueOrigin, ExpectedEye(blue)) && Approximately(redOrigin, ExpectedEye(red)),
                "F6 current eye origin did not match current actor transform plus configured eye height.");
            VerifySelectionAndHistoricalSegment();
            historicalEvidenceCount = panel.LastWorldPerceptionEvidenceCount;
            ScreenCapture.CaptureScreenshot(SessionState.GetString(ScreenshotKey, string.Empty));
            return true;
        }

        private static void VerifySelectionAndHistoricalSegment()
        {
            MethodInfo select = typeof(SandboxNpcObservabilityPanel).GetMethod("Select",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(select != null, "F6 selection presentation seam was not found.");
            select.Invoke(panel, new object[] { blueMetadata });
            Require(panel.Selected == blueMetadata && panel.TryGetCurrentVisualOrigin(redMetadata, out _) &&
                    panel.TryGetCurrentVisualOrigin(blueMetadata, out _),
                "Selecting Blue changed global CURRENT eligibility or selected the wrong inspector.");
            select.Invoke(panel, new object[] { redMetadata });
            Require(panel.Selected == redMetadata && panel.CurrentWorldVisualActorCount == 2,
                "Selecting Red changed global CURRENT eligibility or selected the wrong inspector.");

            // A presentation fixture only: no Evaluate, LOS query, or mutation of production evidence.
            var blocker = new GameObject("F6 historical blocker fixture");
            try
            {
                var collider = blocker.AddComponent<BoxCollider>();
                Vector3 origin = ExpectedEye(blue);
                Vector3 endpoint = ExpectedEye(red);
                var evidence = new ActorVisualPerceptionResult(false, ActorVisualPerceptionReason.Occluded,
                    blue.ActorInstanceId, red.ActorInstanceId, endpoint, Vector3.Distance(origin, endpoint),
                    0f, collider, 0d, origin, null);
                Require(SandboxNpcObservabilityPanel.TryGetLastQuerySegment(evidence,
                        out Vector3 beforeOrigin, out Vector3 beforeEndpoint, out string label) &&
                        label.StartsWith("LAST QUERY") && !label.Contains("CURRENT"),
                    "Historical query is missing or falsely labelled as CURRENT/blocker hit.");
                blocker.transform.position += Vector3.one * 20f;
                Require(SandboxNpcObservabilityPanel.TryGetLastQuerySegment(evidence,
                        out Vector3 afterOrigin, out Vector3 afterEndpoint, out _) &&
                        Approximately(beforeOrigin, afterOrigin) && Approximately(beforeEndpoint, afterEndpoint) &&
                        Approximately(afterOrigin, origin) && Approximately(afterEndpoint, endpoint),
                    "LAST query moved with the live blocker rather than retaining its historical endpoints.");
                historicalFixture = evidence;
            }
            finally { UnityEngine.Object.DestroyImmediate(blocker); }
        }

        private static ActorVisualPerceptionResult historicalFixture;

        private static bool MoveBlueAndVerifyCurrentOrigin()
        {
            if (Time.time - stageStartedAt < 0.35f)
                return false;
            Vector3 lateral = Vector3.Cross(Vector3.up, red.transform.position - blue.transform.position).normalized;
            Vector3 movedPosition = default;
            bool movedSurface = lateral.sqrMagnitude > 0.0001f &&
                                TryFindSurface(blue.transform.position + lateral * 3f, out movedPosition);
            Require(movedSurface,
                "Could not find a moved Blue NavMesh position for current-origin verification.");
            PlaceNpc(blue, movedPosition, blue.transform.rotation);
            movedBlueOrigin = ExpectedEye(blue);
            Require(SandboxNpcObservabilityPanel.TryGetLastQuerySegment(historicalFixture,
                    out Vector3 historicalOrigin, out _, out _) &&
                    Approximately(historicalOrigin, historicalFixture.ObserverOrigin) &&
                    !Approximately(historicalOrigin, movedBlueOrigin),
                "LAST origin followed the moved actor or failed after blocker destruction.");
            return true;
        }

        private static void VerifyDeadSuppression()
        {
            if (!redTerminated)
            {
                if (Time.time - stageStartedAt < 0.35f)
                    return;
                Require(panel.TryGetCurrentVisualOrigin(blueMetadata, out Vector3 currentBlueOrigin) &&
                        Approximately(currentBlueOrigin, movedBlueOrigin),
                    "F6 current eye origin did not follow Blue after movement; it may be using historical query origin.");
                red.GetComponent<ActorBehaviorController>().EnterInactive("F6 diagnostic inactive presentation check");
                Require(red.LifecycleState == ActorLifecycleState.Alive &&
                        !panel.TryGetCurrentVisualOrigin(redMetadata, out _),
                    "F6 drew CURRENT visuals for an alive Inactive actor.");
                red.GetComponent<ActorHealthComponent>().Kill();
                redTerminated = true;
            }
            if (Time.time - stageStartedAt < 0.7f)
                return;
            Require(panel.CurrentWorldVisualActorCount == 1 &&
                    !panel.TryGetCurrentVisualOrigin(redMetadata, out _),
                "F6 drew current FOV/Gaze for a Dead Red actor.");
            string screenshot = SessionState.GetString(ScreenshotKey, string.Empty);
            Debug.Log(
                "M41 F6 Observability Diagnostics: PASS\n" +
                "- Blue + Red CURRENT FOV/Gaze concurrently eligible (manual visibility pending): " + 2 + " actors; selected inspector=" +
                red.ActorInstanceId + "\n" +
                "- Current eye follows transform + EyeHeight after Blue movement: " + movedBlueOrigin.ToString("F2") + "\n" +
                "- Production LAST evidence entries observed: " + historicalEvidenceCount + "\n" +
                "- Presentation fixture: LAST query endpoints stable after actor/blocker movement and blocker destruction\n" +
                "- Blue/Red eyes project inside the camera viewport; inspector selection independent; alive Inactive suppressed\n" +
                "- Default LAST evidence displays No evidence; actual GUI draw count=" + panel.CurrentWorldDrawnActorCount + "\n" +
                "- Dead Red current visuals suppressed; LAST evidence remains separate when present\n" +
                "- Batchmode screenshot: " + (File.Exists(screenshot) ? screenshot : "not available; Game-view IMGUI requires manual visual confirmation"));
            SessionState.SetInt(StageKey, 99);
            EditorApplication.ExitPlaymode();
        }

        private static Vector3 ExpectedEye(ActorRuntimeIdentity actor) =>
            actor.transform.position + Vector3.up * actor.GetComponent<ActorVisualPerceptionService>().EyeHeight;

        private static bool TryFindSurface(Vector3 requested, out Vector3 position)
        {
            if (NavMesh.SamplePosition(requested, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            {
                position = hit.position;
                return true;
            }
            position = default;
            return false;
        }

        private static void PlaceNpc(ActorRuntimeIdentity actor, Vector3 position, Quaternion rotation)
        {
            ActorNavigationController navigation = actor.GetComponent<ActorNavigationController>();
            navigation.Stop();
            Require(navigation.Agent != null && navigation.Agent.isOnNavMesh && navigation.Agent.Warp(position),
                "Actor could not warp through its existing NavMeshAgent: " + actor.ActorInstanceId);
            actor.transform.rotation = rotation;
            actor.GetComponent<ActorGazeController>()?.ConfigureFromIdentity();
            navigation.Agent.nextPosition = position;
            Physics.SyncTransforms();
        }

        private static bool Approximately(Vector3 left, Vector3 right) =>
            (left - right).sqrMagnitude <= 0.0001f;

        private static bool OnScreen(Vector3 point) =>
            point.z > 0f && point.x >= 0f && point.x <= 1f && point.y >= 0f && point.y <= 1f;

        private static void SetStage(int stage)
        {
            SessionState.SetInt(StageKey, stage);
            stageStartedAt = Time.time;
        }

        private static void Finish(int exitCode, string immediateFailure = null)
        {
            string failure = string.IsNullOrWhiteSpace(immediateFailure)
                ? SessionState.GetString(FailureKey, string.Empty)
                : immediateFailure;
            SessionState.SetBool(PendingKey, false);
            SessionState.SetInt(StageKey, 0);
            SessionState.EraseString(FailureKey);
            WorldRuntimeTerrainDevelopmentSettings.ClearDiagnosticSelectionOverride();
            WorldSessionService.Close();
            string root = SessionState.GetString(RootKey, string.Empty);
            SessionState.EraseString(RootKey);
            SessionState.EraseString(ScreenshotKey);
            runtime = null;
            sandbox = null;
            panel = null;
            blueMetadata = null;
            redMetadata = null;
            blue = null;
            red = null;
            redTerminated = false;
            if (!string.IsNullOrWhiteSpace(failure))
                Debug.LogError("M41 F6 Observability Diagnostics: FAIL\n" + failure);
            else if (!string.IsNullOrWhiteSpace(root))
                Debug.Log("M41 F6 Observability evidence retained at " + root + ".");
            EditorApplication.Exit(string.IsNullOrWhiteSpace(failure) && exitCode == 0 ? 0 : 1);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
