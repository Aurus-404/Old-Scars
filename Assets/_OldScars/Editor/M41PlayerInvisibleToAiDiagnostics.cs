using System;
using System.IO;
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
    [InitializeOnLoad]
    public static class M41PlayerInvisibleToAiDiagnostics
    {
        private const string PendingKey = "OldScars.M41.PlayerInvisibleToAi.Pending";
        private const string StageKey = "OldScars.M41.PlayerInvisibleToAi.Stage";
        private const string FailureKey = "OldScars.M41.PlayerInvisibleToAi.Failure";
        private const string RootKey = "OldScars.M41.PlayerInvisibleToAi.Root";
        private const long WorldSeed = 941413001L;
        private const long SandboxSeed = 41413001L;
        private const float TargetDistance = 8f;

        private static WorldRuntimeSceneController runtime;
        private static SandboxNpcController sandbox;
        private static ActorRuntimeIdentity player;
        private static ActorRuntimeIdentity observer;
        private static ActorRuntimeIdentity blue;
        private static ActorDebugAiAcquisitionExclusion exclusion;
        private static float stageStartedAt;
        private static float playerFirstAcquisitionSeconds;
        private static float playerSecondAcquisitionSeconds;
        private static float blueAcquisitionSeconds;
        private static int playerPerceptionEvaluationsBeforeExclusion;
        private static int playerPerceptionEvaluationsAfterExclusion;

        static M41PlayerInvisibleToAiDiagnostics()
        {
            EditorApplication.update -= Continue;
            EditorApplication.update += Continue;
        }

        public static void RunBatchWorldRuntime()
        {
            if (!Application.isBatchMode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Player Invisible-to-AI diagnostics require compiled Unity batchmode.");

            string root = Path.Combine(Path.GetTempPath(), "OldScars_M41_PlayerInvisible_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            SessionState.SetString(RootKey, root);
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
                    Finish(1, "Player Invisible-to-AI WorldRuntime diagnostic was interrupted before completion.");
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
                    throw new InvalidOperationException("real WorldRuntime did not reach Player Invisible-to-AI readiness.");
                return;
            }

            switch (stage)
            {
                case 1: SetupPlayerEligibleScenario(); SetStage(2); break;
                case 2: WaitForInitialPlayerThreat(); break;
                case 3: TogglePlayerExcludedAndPrepareBlue(); SetStage(4); break;
                case 4: WaitForBlueThreatWithoutPlayerRecognition(); break;
                case 5: RestorePlayerEligibility(); SetStage(6); break;
                case 6: WaitForRestoredPlayerThreat(); break;
            }
        }

        private static void CreateWorldSessionAndLoadRuntime()
        {
            WorldSessionService.Close();
            var store = new PersistenceFileStore(SessionState.GetString(RootKey, string.Empty));
            WorldSessionOperationResult created = WorldSessionService.Create(
                "M41 Player Invisible-to-AI", new OldScars.Core.World.WorldSeed(WorldSeed),
                WorldGenerationSettings.ResolvePreset(WorldSizePreset.Small),
                LandCoveragePreset.High, GameDataManager.Instance.LoadedContentSet, store);
            Require(created.Success, "Could not create Player Invisible-to-AI WorldSession: " + created.Failure);
            SceneManager.LoadScene(WorldApplicationScenes.WorldRuntimeSceneName, LoadSceneMode.Single);
        }

        private static void SetupPlayerEligibleScenario()
        {
            Require(runtime.TerrainSelection == WorldRuntimeTerrainDevelopmentSelection.VolumetricIndexedMarchingCubes &&
                    runtime.VolumetricTerrainController?.IsReady == true,
                "Player Invisible-to-AI did not run in real volumetric WorldRuntime.");
            sandbox = runtime.GameplayRuntimeComposition.SandboxNpcController;
            player = runtime.PlayerComposition.PlayerIdentity;
            string seedError = "Sandbox or Player authority is unavailable.";
            Require(sandbox != null && player != null && sandbox.TrySetBaseSeed(SandboxSeed.ToString(), out seedError),
                "Player Invisible-to-AI sandbox setup failed: " + seedError);
            Require(sandbox.TrySpawnRedNpc(out SandboxNpcMetadata observerMetadata, out string observerError),
                "Red observer spawn failed: " + observerError);
            Require(sandbox.TrySpawnBlueNpc(out SandboxNpcMetadata blueMetadata, out string blueError),
                "Blue candidate spawn failed: " + blueError);
            observer = observerMetadata.GetComponent<ActorRuntimeIdentity>();
            blue = blueMetadata.GetComponent<ActorRuntimeIdentity>();
            blue.GetComponent<ActorThreatAcquisitionController>().enabled = false;
            exclusion = player.GetComponent<ActorDebugAiAcquisitionExclusion>() ??
                        player.gameObject.AddComponent<ActorDebugAiAcquisitionExclusion>();
            exclusion.SetExcludedFromAutomaticThreatAcquisition(false);

            Require(ConfigureObserverHostility(SandboxNpcController.PlayerAffiliationId),
                "Could not configure Red hostility toward Player.");
            Require(TryPlaceObserverAndPlayer(out string placementError),
                "Could not establish clear physical Player perception: " + placementError);
            Require(observer.GetComponent<ActorVisualPerceptionService>().Evaluate(player).Perceived,
                "Player was not perceived under the eligible baseline geometry.");
            Require(player.gameObject.activeInHierarchy && player.IsRegistered &&
                    player.GetComponent<CharacterController>() != null,
                "Player baseline did not remain active, registered and physically collidable.");
        }

        private static void WaitForInitialPlayerThreat()
        {
            if (Encounter().Threat == player)
            {
                playerFirstAcquisitionSeconds = Time.time - stageStartedAt;
                SetStage(3);
                return;
            }
            if (Time.time - stageStartedAt > 2f)
                throw new InvalidOperationException("Eligible Player was not acquired through the real recognition pipeline.");
        }

        private static void TogglePlayerExcludedAndPrepareBlue()
        {
            playerPerceptionEvaluationsBeforeExclusion = Acquisition().PerceptionEvaluationCount;
            exclusion.SetExcludedFromAutomaticThreatAcquisition(true);
            Require(exclusion.IsExcludedFromAutomaticThreatAcquisition && player.gameObject.activeInHierarchy &&
                    player.IsRegistered && player.GetComponent<CharacterController>() != null,
                "Excluding Player changed its debug-physical representation contract.");
            Require(ConfigureObserverHostility(
                    SandboxNpcController.PlayerAffiliationId, SandboxNpcController.BlueAffiliationId),
                "Could not configure Red hostility toward Player and Blue.");
            Require(TryPlaceNpcInFrontOfObserver(blue, TargetDistance * 0.6f),
                "Could not place visible Blue candidate for excluded-Player scenario.");
            Require(observer.GetComponent<ActorVisualPerceptionService>().Evaluate(blue).Perceived,
                "Blue was not physically perceived in excluded-Player scenario.");
        }

        private static void WaitForBlueThreatWithoutPlayerRecognition()
        {
            ActorThreatAcquisitionController acquisition = Acquisition();
            bool playerHasRecognition = acquisition.TryGetRecognitionProgress(player, out _);
            Require(!playerHasRecognition && Encounter().Threat != player,
                "Excluded Player remained in automatic recognition or threat state.");
            if (Encounter().Threat == blue)
            {
                blueAcquisitionSeconds = Time.time - stageStartedAt;
                playerPerceptionEvaluationsAfterExclusion = acquisition.PerceptionEvaluationCount;
                Require(playerPerceptionEvaluationsAfterExclusion > playerPerceptionEvaluationsBeforeExclusion,
                    "Excluded Player prevented normal Blue acquisition evaluation.");
                SetStage(5);
                return;
            }
            if (Time.time - stageStartedAt > 2f)
                throw new InvalidOperationException("Eligible Blue was not acquired while Player was excluded.");
        }

        private static void RestorePlayerEligibility()
        {
            exclusion.SetExcludedFromAutomaticThreatAcquisition(false);
            Require(!exclusion.IsExcludedFromAutomaticThreatAcquisition,
                "Player Invisible-to-AI toggle did not restore OFF state.");
            Require(TryPlaceNpcBehindObserver(blue, TargetDistance * 2f),
                "Could not remove Blue from the restored Player LOS path.");
            Require(ConfigureObserverHostility(SandboxNpcController.PlayerAffiliationId),
                "Could not restore Red hostility toward Player only.");
            Vector3 playerDirection = Vector3.ProjectOnPlane(
                player.transform.position - observer.transform.position, Vector3.up);
            Require(playerDirection.sqrMagnitude > 0.0001f, "Player and Red observer overlapped during restored eligibility.");
            observer.transform.rotation = Quaternion.LookRotation(playerDirection.normalized);
            observer.GetComponent<ActorGazeController>()?.ConfigureFromIdentity();
            Physics.SyncTransforms();
            Require(observer.GetComponent<ActorVisualPerceptionService>().Evaluate(player).Perceived,
                "Restored Player was not physically perceived.");
        }

        private static void WaitForRestoredPlayerThreat()
        {
            if (Encounter().Threat == player)
            {
                playerSecondAcquisitionSeconds = Time.time - stageStartedAt;
                Require(Acquisition().TryGetRecognitionProgress(player, out _) == false,
                    "Assigned Player should clear temporary recognition state after automatic assignment.");
                Debug.Log(
                    "M41 Player Invisible-to-AI Diagnostics: PASS\n" +
                    "- OFF: Player acquired through recognition in " + playerFirstAcquisitionSeconds.ToString("0.###") + "s\n" +
                    "- ON: Player active/registered/CharacterController intact; no recognition or threat; Blue acquired in " +
                    blueAcquisitionSeconds.ToString("0.###") + "s\n" +
                    "- Runtime toggle: Player threat released before Blue assignment; acquisition evaluations " +
                    playerPerceptionEvaluationsBeforeExclusion + " -> " + playerPerceptionEvaluationsAfterExclusion + "\n" +
                    "- OFF restored: Player reacquired through recognition in " +
                    playerSecondAcquisitionSeconds.ToString("0.###") + "s");
                SessionState.SetInt(StageKey, 99);
                EditorApplication.ExitPlaymode();
                return;
            }
            if (Time.time - stageStartedAt > 2f)
                throw new InvalidOperationException("Player was not automatically eligible again after toggling Invisible-to-AI OFF.");
        }

        private static bool ConfigureObserverHostility(params string[] hostileAffiliations)
        {
            return observer.GetComponent<ActorAffiliationComponent>().TryConfigure(
                SandboxNpcController.RedAffiliationId, "Red", hostileAffiliations, out _);
        }

        private static bool TryPlaceObserverAndPlayer(out string error)
        {
            ActorVisualPerceptionReason lastReason = ActorVisualPerceptionReason.LineOfSightMiss;
            Vector3 center = player.transform.position;
            for (int index = 0; index < 24; index++)
            {
                Vector3 direction = Quaternion.Euler(0f, index * 15f, 0f) * Vector3.forward;
                Vector3 lateral = Vector3.Cross(Vector3.up, direction).normalized;
                Vector3 pairCenter = center + lateral * 18f;
                if (!NavMesh.SamplePosition(pairCenter - direction * TargetDistance * 0.5f,
                        out NavMeshHit observerHit, 7f, NavMesh.AllAreas) ||
                    !NavMesh.SamplePosition(pairCenter + direction * TargetDistance * 0.5f,
                        out NavMeshHit playerHit, 7f, NavMesh.AllAreas))
                    continue;
                PlaceNpc(observer, observerHit.position, Quaternion.LookRotation(direction));
                runtime.PlayerComposition.PlacePlayerAtSurface(playerHit.position, Quaternion.LookRotation(-direction));
                Physics.SyncTransforms();
                ActorVisualPerceptionResult result = observer.GetComponent<ActorVisualPerceptionService>().Evaluate(player);
                lastReason = result.Reason;
                if (result.Perceived)
                {
                    error = null;
                    return true;
                }
            }
            error = "No clear Player pair at " + TargetDistance + "m; lastReason=" + lastReason + ".";
            return false;
        }

        private static bool TryPlaceNpcInFrontOfObserver(ActorRuntimeIdentity target, float requestedDistance)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector3 requested = observer.transform.position + observer.transform.forward * (requestedDistance - attempt * 0.25f);
                if (!NavMesh.SamplePosition(requested, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                    continue;
                PlaceNpc(target, hit.position, Quaternion.LookRotation(-observer.transform.forward));
                Physics.SyncTransforms();
                if (observer.GetComponent<ActorVisualPerceptionService>().Evaluate(target).Perceived)
                    return true;
            }
            return false;
        }

        private static bool TryPlaceNpcBehindObserver(ActorRuntimeIdentity target, float requestedDistance)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector3 requested = observer.transform.position - observer.transform.forward *
                    (requestedDistance - attempt * 0.25f);
                if (!NavMesh.SamplePosition(requested, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                    continue;
                PlaceNpc(target, hit.position, Quaternion.LookRotation(observer.transform.forward));
                Physics.SyncTransforms();
                return true;
            }
            return false;
        }

        private static void PlaceNpc(ActorRuntimeIdentity actor, Vector3 position, Quaternion rotation)
        {
            actor.GetComponent<HumanEncounterAIController>().ClearThreat("Player Invisible-to-AI diagnostic placement");
            ActorNavigationController navigation = actor.GetComponent<ActorNavigationController>();
            navigation.Stop();
            Require(navigation.Agent != null && navigation.Agent.isOnNavMesh && navigation.Agent.Warp(position),
                "Actor could not warp through its existing NavMeshAgent: " + actor.ActorInstanceId);
            actor.transform.rotation = rotation;
            actor.GetComponent<ActorGazeController>()?.ConfigureFromIdentity();
            navigation.Agent.nextPosition = position;
        }

        private static ActorThreatAcquisitionController Acquisition() =>
            observer.GetComponent<ActorThreatAcquisitionController>();

        private static HumanEncounterAIController Encounter() =>
            observer.GetComponent<HumanEncounterAIController>();

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
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
                Directory.Delete(root, true);
            runtime = null;
            sandbox = null;
            player = null;
            observer = null;
            blue = null;
            exclusion = null;
            if (!string.IsNullOrWhiteSpace(failure))
                Debug.LogError("M41 Player Invisible-to-AI Diagnostics: FAIL\n" + failure);
            EditorApplication.Exit(string.IsNullOrWhiteSpace(failure) && exitCode == 0 ? 0 : 1);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
