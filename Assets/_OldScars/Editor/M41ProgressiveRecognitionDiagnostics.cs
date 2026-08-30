using System;
using System.Collections.Generic;
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
    public static class M41ProgressiveRecognitionDiagnostics
    {
        private const string PendingKey = "OldScars.M41.ProgressiveRecognition.Pending";
        private const string StageKey = "OldScars.M41.ProgressiveRecognition.Stage";
        private const string FailureKey = "OldScars.M41.ProgressiveRecognition.Failure";
        private const string RootKey = "OldScars.M41.ProgressiveRecognition.Root";
        private const string RecognitionTargetAffiliation = "diagnostic_recognition_target";
        private const long WorldSeed = 941410441L;
        private const long SandboxSeed = 41410414L;
        private const float NearDistance = 6f;
        private const float FarDistance = 80f;
        private const int MultipleCandidateMinimum = 5;

        private static readonly List<ActorRuntimeIdentity> candidates = new List<ActorRuntimeIdentity>(8);
        private static WorldRuntimeSceneController runtime;
        private static SandboxNpcController sandbox;
        private static ActorRuntimeIdentity player;
        private static ActorRuntimeIdentity observer;
        private static ActorRuntimeIdentity nearTarget;
        private static ActorRuntimeIdentity farTarget;
        private static GameObject losWall;
        private static float stageStartedAt;
        private static float nearDetectionSeconds;
        private static float farDetectionSeconds;
        private static float partialBeforeOcclusion;
        private static float decayedProgress;
        private static bool nearPartialObserved;
        private static bool farPartialObserved;
        private static bool recoveryIncreaseObserved;
        private static bool lostContactPrepared;
        private static Vector3 lastKnownBeforeHiddenMove;
        private static Vector3 hiddenActualPosition;
        private static int attackBaseline;
        private static int visibleMultipleCandidateCount;

        static M41ProgressiveRecognitionDiagnostics()
        {
            EditorApplication.update -= Continue;
            EditorApplication.update += Continue;
        }

        public static void RunBatchWorldRuntime()
        {
            if (!Application.isBatchMode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Progressive Recognition diagnostics require compiled Unity batchmode.");

            candidates.Clear();
            string root = Path.Combine(Path.GetTempPath(), "OldScars_M41_Recognition_" + Guid.NewGuid().ToString("N"));
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
                    Finish(1, "Progressive Recognition WorldRuntime diagnostic was interrupted before completion.");
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
                    throw new InvalidOperationException("real WorldRuntime did not reach Progressive Recognition readiness.");
                return;
            }

            switch (stage)
            {
                case 1: SetupCorpusAndOutsideFov(); SetStage(2); break;
                case 2: VerifyOutsideFovThenTurn(); break;
                case 3: ObserveNearRecognition(); break;
                case 4: ObserveFarRecognition(); break;
                case 5: VerifyBlockedLosThenOpen(); break;
                case 6: ObservePartialRecognition(); break;
                case 7: VerifyDecayThenRecoverLos(); break;
                case 8: ObserveRecoveryDetection(); break;
                case 9: VerifyExistingLostContactMemory(); break;
                case 10: VerifyMultipleCandidateBound(); break;
                case 11: VerifyStateCleanupAndFinish(); break;
            }
        }

        private static void CreateWorldSessionAndLoadRuntime()
        {
            WorldSessionService.Close();
            var store = new PersistenceFileStore(SessionState.GetString(RootKey, string.Empty));
            WorldSessionOperationResult created = WorldSessionService.Create(
                "M41 Progressive Recognition", new OldScars.Core.World.WorldSeed(WorldSeed),
                WorldGenerationSettings.ResolvePreset(WorldSizePreset.Small),
                LandCoveragePreset.High, GameDataManager.Instance.LoadedContentSet, store);
            Require(created.Success, "Could not create Progressive Recognition WorldSession: " + created.Failure);
            SceneManager.LoadScene(WorldApplicationScenes.WorldRuntimeSceneName, LoadSceneMode.Single);
        }

        private static void SetupCorpusAndOutsideFov()
        {
            Require(runtime.TerrainSelection == WorldRuntimeTerrainDevelopmentSelection.VolumetricIndexedMarchingCubes &&
                    runtime.VolumetricTerrainController?.IsReady == true,
                "Progressive Recognition did not run in real volumetric WorldRuntime.");
            sandbox = runtime.GameplayRuntimeComposition.SandboxNpcController;
            player = runtime.PlayerComposition.PlayerIdentity;
            string seedError = "Sandbox or Player authority is unavailable.";
            Require(sandbox != null && player != null && sandbox.TrySetBaseSeed(SandboxSeed.ToString(), out seedError),
                "Progressive Recognition sandbox setup failed: " + seedError);

            Require(sandbox.TrySpawnRedNpc(out SandboxNpcMetadata observerMetadata, out string observerError),
                "Recognition observer spawn failed: " + observerError);
            observer = observerMetadata.GetComponent<ActorRuntimeIdentity>();
            DisableAcquisition(observer);
            for (int index = 0; index < 7; index++)
            {
                Require(sandbox.TrySpawnBlueNpc(out SandboxNpcMetadata metadata, out string error),
                    "Recognition candidate spawn failed: " + error);
                ActorRuntimeIdentity candidate = metadata.GetComponent<ActorRuntimeIdentity>();
                DisableAcquisition(candidate);
                candidates.Add(candidate);
            }
            nearTarget = candidates[0];
            farTarget = candidates[1];

            ActorVisualPerceptionService sight = observer.GetComponent<ActorVisualPerceptionService>();
            Require(sight != null && sight.IsConfigured && sight.HorizontalFovDegrees < 360f &&
                    Approximately(sight.RecognitionNearSeconds, 0.2f) &&
                    Approximately(sight.RecognitionFarSeconds, 1f) &&
                    Approximately(sight.RecognitionDecaySeconds, 0.5f),
                "Combat sandbox did not expose configurable non-360 FOV and recognition tuning.");
            Require(observer.GetComponent<ActorAffiliationComponent>().TryConfigure(
                    SandboxNpcController.RedAffiliationId, "Red", new[] { RecognitionTargetAffiliation }, out string affiliationError),
                "Observer recognition affiliation failed: " + affiliationError);
            IsolateTarget(nearTarget);
            Require(TryPlacePairWithClearPerception(observer, nearTarget, NearDistance, out string placementError),
                "Could not place near FOV pair: " + placementError);
            observer.transform.rotation = Quaternion.LookRotation(-FlatDirection(observer, nearTarget));
            Physics.SyncTransforms();
            ActorVisualPerceptionResult behind = sight.Evaluate(nearTarget);
            Require(!behind.Perceived && behind.Reason == ActorVisualPerceptionReason.OutsideFov,
                "Near target behind observer was not outside the configured frontal cone.");
            attackBaseline = observer.GetComponent<HumanEncounterAIController>().AttackCount;
            observer.GetComponent<ActorThreatAcquisitionController>().enabled = true;
        }

        private static void VerifyOutsideFovThenTurn()
        {
            ActorThreatAcquisitionController acquisition = Acquisition();
            if (Time.time - stageStartedAt < 0.45f)
                return;
            acquisition.TryGetRecognitionProgress(nearTarget, out float progress);
            Require(Encounter().Threat == null && progress <= 0f &&
                    acquisition.LastAcquisitionPerception.Reason == ActorVisualPerceptionReason.OutsideFov,
                "Target outside frontal FOV accumulated recognition or was detected.");
            observer.transform.rotation = Quaternion.LookRotation(FlatDirection(observer, nearTarget));
            Physics.SyncTransforms();
            nearPartialObserved = false;
            SetStage(3);
        }

        private static void ObserveNearRecognition()
        {
            ActorThreatAcquisitionController acquisition = Acquisition();
            acquisition.TryGetRecognitionProgress(nearTarget, out float progress);
            if (Encounter().Threat == null)
            {
                nearPartialObserved |= progress > 0f && progress < 1f &&
                                       acquisition.LastAcquisitionPerception.Perceived;
                if (Time.time - stageStartedAt > 1.2f)
                    throw new InvalidOperationException("Near target did not complete progressive recognition.");
                return;
            }

            nearDetectionSeconds = Time.time - stageStartedAt;
            Require(Encounter().Threat == nearTarget && nearPartialObserved &&
                    nearDetectionSeconds >= 0.18f && nearDetectionSeconds <= 0.75f &&
                    Encounter().AttackCount == attackBaseline &&
                    (Encounter().State == HumanEncounterAIState.Idle || Encounter().State == HumanEncounterAIState.Alerted),
                "Near LOS skipped recognition or merged recognition with encounter reaction.");
            SetupFarScenario();
            SetStage(4);
        }

        private static void SetupFarScenario()
        {
            DisableAcquisition(observer);
            Encounter().ClearThreat("Near recognition evidence complete");
            IsolateTarget(farTarget);
            Require(TryPlacePairWithClearPerception(observer, farTarget, FarDistance, out string placementError),
                "Could not place far recognition pair: " + placementError);
            farPartialObserved = false;
            observer.GetComponent<ActorThreatAcquisitionController>().enabled = true;
        }

        private static void ObserveFarRecognition()
        {
            ActorThreatAcquisitionController acquisition = Acquisition();
            acquisition.TryGetRecognitionProgress(farTarget, out float progress);
            if (Encounter().Threat == null)
            {
                farPartialObserved |= progress > 0f && progress < 1f;
                if (Time.time - stageStartedAt > 2.2f)
                    throw new InvalidOperationException("Far target did not complete distance-scaled recognition.");
                return;
            }

            farDetectionSeconds = Time.time - stageStartedAt;
            Require(Encounter().Threat == farTarget && farPartialObserved &&
                    farDetectionSeconds >= 0.7f && farDetectionSeconds > nearDetectionSeconds + 0.35f,
                "Far recognition was not clearly slower than near recognition.");
            SetupBlockedScenario();
            SetStage(5);
        }

        private static void SetupBlockedScenario()
        {
            DisableAcquisition(observer);
            Encounter().ClearThreat("Far recognition evidence complete");
            IsolateTarget(nearTarget);
            Require(TryPlacePairWithClearPerception(observer, nearTarget, NearDistance, out string placementError),
                "Could not place occlusion pair: " + placementError);
            CreateLosWall(observer, nearTarget);
            Require(observer.GetComponent<ActorVisualPerceptionService>().Evaluate(nearTarget).Reason ==
                    ActorVisualPerceptionReason.Occluded,
                "Diagnostic wall did not block eye-to-target physical LOS.");
            observer.GetComponent<ActorThreatAcquisitionController>().enabled = true;
        }

        private static void VerifyBlockedLosThenOpen()
        {
            if (Time.time - stageStartedAt < 0.45f)
                return;
            Acquisition().TryGetRecognitionProgress(nearTarget, out float progress);
            Require(Encounter().Threat == null && progress <= 0f &&
                    Acquisition().LastAcquisitionPerception.Reason == ActorVisualPerceptionReason.Occluded,
                "Blocked LOS accumulated recognition or assigned a threat.");
            DestroyWall();
            SetStage(6);
        }

        private static void ObservePartialRecognition()
        {
            Acquisition().TryGetRecognitionProgress(nearTarget, out float progress);
            if (Encounter().Threat != null)
                throw new InvalidOperationException("Near target was detected before partial-recognition decay could be observed.");
            if (progress >= 0.3f && progress < 0.9f)
            {
                partialBeforeOcclusion = progress;
                CreateLosWall(observer, nearTarget);
                SetStage(7);
                return;
            }
            if (Time.time - stageStartedAt > 1f)
                throw new InvalidOperationException("Visible target never exposed bounded partial recognition.");
        }

        private static void VerifyDecayThenRecoverLos()
        {
            Acquisition().TryGetRecognitionProgress(nearTarget, out float progress);
            if (progress < partialBeforeOcclusion - 0.05f && progress > 0f)
            {
                Require(Encounter().Threat == null &&
                        Acquisition().LastAcquisitionPerception.Reason == ActorVisualPerceptionReason.Occluded,
                    "Partial recognition became detected while LOS was lost.");
                decayedProgress = progress;
                DestroyWall();
                recoveryIncreaseObserved = false;
                SetStage(8);
                return;
            }
            if (Time.time - stageStartedAt > 0.45f)
                throw new InvalidOperationException(
                    "Partial recognition did not decay gradually before cleanup: before=" +
                    partialBeforeOcclusion.ToString("0.###") + ", current=" + progress.ToString("0.###") + ".");
        }

        private static void ObserveRecoveryDetection()
        {
            Acquisition().TryGetRecognitionProgress(nearTarget, out float progress);
            if (Encounter().Threat == null)
            {
                recoveryIncreaseObserved |= progress > decayedProgress + 0.05f;
                if (Time.time - stageStartedAt > 1.2f)
                    throw new InvalidOperationException("Recognition did not resume after physical LOS recovery.");
                return;
            }
            Require(Encounter().Threat == nearTarget && recoveryIncreaseObserved,
                "Recovered LOS did not finish through existing TryAssignThreat.");
            lostContactPrepared = false;
            SetStage(9);
        }

        private static void VerifyExistingLostContactMemory()
        {
            HumanEncounterAIController encounter = Encounter();
            if (!lostContactPrepared)
            {
                if (!encounter.HasLastKnownPosition || !encounter.LastPerception.Perceived)
                {
                    if (Time.time - stageStartedAt > 0.8f)
                        throw new InvalidOperationException("Detected threat never entered existing encounter perception memory.");
                    return;
                }
                lastKnownBeforeHiddenMove = encounter.LastKnownPosition;
                CreateLosWall(observer, nearTarget);
                Require(TryMoveTargetFartherBehindWall(nearTarget, out hiddenActualPosition),
                    "Could not move hidden target along the occluded NavMesh line.");
                lostContactPrepared = true;
                stageStartedAt = Time.time;
                return;
            }

            if (encounter.State == HumanEncounterAIState.LostContact)
            {
                Require(Vector3.Distance(encounter.LastKnownPosition, lastKnownBeforeHiddenMove) < 0.05f &&
                        Vector3.Distance(encounter.LastKnownPosition, hiddenActualPosition) > 1.5f &&
                        !encounter.LastPerception.Perceived,
                    "LostContact tracked the hidden actor's exact current position instead of LastKnownPosition.");
                SetupMultipleCandidateScenario();
                SetStage(10);
                return;
            }
            if (Time.time - stageStartedAt > 0.8f)
                throw new InvalidOperationException("Existing encounter AI did not enter LostContact after detected LOS loss.");
        }

        private static void SetupMultipleCandidateScenario()
        {
            DisableAcquisition(observer);
            Encounter().ClearThreat("Lost-contact evidence complete");
            DestroyWall();
            Require(observer.GetComponent<ActorAffiliationComponent>().TryConfigure(
                    SandboxNpcController.RedAffiliationId, "Red", new[] { RecognitionTargetAffiliation }, out string error),
                "Could not restore multi-candidate observer disposition: " + error);
            for (int index = 0; index < candidates.Count; index++)
                ConfigureCandidate(candidates[index], RecognitionTargetAffiliation);

            Require(TryPlaceMultipleVisibleCandidates(out visibleMultipleCandidateCount) &&
                    visibleMultipleCandidateCount >= MultipleCandidateMinimum,
                "Could not establish multiple simultaneous visible hostile candidates.");
            observer.GetComponent<ActorThreatAcquisitionController>().enabled = true;
        }

        private static void VerifyMultipleCandidateBound()
        {
            ActorThreatAcquisitionController acquisition = Acquisition();
            if (Encounter().Threat != null)
                throw new InvalidOperationException("Multi-candidate fixture reached detection before bounded partial state was inspected.");
            if (acquisition.RecognitionStateCount >= MultipleCandidateMinimum &&
                acquisition.HighestRecognitionProgress > 0f)
            {
                Require(acquisition.RecognitionStateCount <= visibleMultipleCandidateCount &&
                        acquisition.PeakRecognitionStateCount <= candidates.Count &&
                        acquisition.RecognitionStateBufferExpansionCount == 0,
                    "Recognition state exceeded the bounded/reused hostile candidate corpus.");
                Require(observer.GetComponent<ActorAffiliationComponent>().TryConfigure(
                        SandboxNpcController.RedAffiliationId, "Red", Array.Empty<string>(), out string error),
                    "Could not remove multi-candidate hostility: " + error);
                SetStage(11);
                return;
            }
            if (Time.time - stageStartedAt > 0.38f)
                throw new InvalidOperationException(
                    "Multiple visible candidates did not create simultaneous partial recognition state: count=" +
                    acquisition.RecognitionStateCount + ", peak=" + acquisition.PeakRecognitionStateCount + ".");
        }

        private static void VerifyStateCleanupAndFinish()
        {
            ActorThreatAcquisitionController acquisition = Acquisition();
            if (acquisition.RecognitionStateCount != 0)
            {
                if (Time.time - stageStartedAt > 0.35f)
                    throw new InvalidOperationException("Recognition state did not clean after candidates stopped being hostile.");
                return;
            }
            Require(acquisition.AcquisitionScanCount < Time.frameCount &&
                    acquisition.RegistryBufferExpansionCount == 0 &&
                    acquisition.CandidateBufferExpansionCount == 0,
                "Progressive recognition introduced per-frame scanning or growing acquisition buffers.");
            Debug.Log(
                "M41 Progressive Visual Recognition Diagnostics: PASS\n" +
                "- Chain: registry candidate -> range -> 120deg FOV -> physical eye LOS -> recognition -> TryAssignThreat\n" +
                "- Timing: Near=" + nearDetectionSeconds.ToString("0.###") +
                "s Far=" + farDetectionSeconds.ToString("0.###") + "s\n" +
                "- Occlusion: blocked=0; partial " + partialBeforeOcclusion.ToString("0.###") +
                " -> " + decayedProgress.ToString("0.###") + " -> recovered detection\n" +
                "- Memory: LostContact retained prior LastKnownPosition while hidden target moved " +
                Vector3.Distance(lastKnownBeforeHiddenMove, hiddenActualPosition).ToString("0.###") + "m\n" +
                "- Bounded state: visible=" + visibleMultipleCandidateCount +
                " peak=" + acquisition.PeakRecognitionStateCount + " expansions=0");
            SessionState.SetInt(StageKey, 99);
            EditorApplication.ExitPlaymode();
        }

        private static ActorThreatAcquisitionController Acquisition() =>
            observer.GetComponent<ActorThreatAcquisitionController>();

        private static HumanEncounterAIController Encounter() =>
            observer.GetComponent<HumanEncounterAIController>();

        private static void DisableAcquisition(ActorRuntimeIdentity actor)
        {
            actor.GetComponent<ActorThreatAcquisitionController>().enabled = false;
            actor.GetComponent<HumanEncounterAIController>().ClearThreat("Progressive Recognition diagnostic setup");
            actor.GetComponent<ActorNavigationController>().Stop();
        }

        private static void IsolateTarget(ActorRuntimeIdentity target)
        {
            for (int index = 0; index < candidates.Count; index++)
                ConfigureCandidate(candidates[index], candidates[index] == target
                    ? RecognitionTargetAffiliation
                    : SandboxNpcController.BlueAffiliationId);
        }

        private static void ConfigureCandidate(ActorRuntimeIdentity actor, string affiliationId)
        {
            Require(actor.GetComponent<ActorAffiliationComponent>().TryConfigure(
                    affiliationId, "Blue", Array.Empty<string>(), out string error),
                "Could not configure recognition candidate disposition: " + error);
        }

        private static bool TryPlacePairWithClearPerception(
            ActorRuntimeIdentity observerActor,
            ActorRuntimeIdentity target,
            float requestedDistance,
            out string error)
        {
            Vector3 center = player.transform.position;
            ActorVisualPerceptionReason lastReason = ActorVisualPerceptionReason.LineOfSightMiss;
            for (int index = 0; index < 24; index++)
            {
                Vector3 direction = Quaternion.Euler(0f, index * 15f, 0f) * Vector3.forward;
                Vector3 lateral = Vector3.Cross(Vector3.up, direction).normalized;
                Vector3 pairCenter = center + lateral * 18f;
                if (!NavMesh.SamplePosition(pairCenter - direction * requestedDistance * 0.5f,
                        out NavMeshHit observerHit, 7f, NavMesh.AllAreas) ||
                    !NavMesh.SamplePosition(pairCenter + direction * requestedDistance * 0.5f,
                        out NavMeshHit targetHit, 7f, NavMesh.AllAreas))
                    continue;
                Place(observerActor, observerHit.position, Quaternion.LookRotation(direction));
                Place(target, targetHit.position, Quaternion.LookRotation(-direction));
                Physics.SyncTransforms();
                float distance = Vector3.Distance(observerActor.transform.position, target.transform.position);
                if (distance < requestedDistance - 3f)
                    continue;
                ActorVisualPerceptionResult result = observerActor.GetComponent<ActorVisualPerceptionService>().Evaluate(target);
                lastReason = result.Reason;
                if (result.Perceived)
                {
                    error = null;
                    return true;
                }
            }
            error = "No clear NavMesh pair at " + requestedDistance + "m; lastReason=" + lastReason + ".";
            return false;
        }

        private static bool TryPlaceMultipleVisibleCandidates(out int visibleCount)
        {
            visibleCount = 0;
            Require(TryPlacePairWithClearPerception(observer, candidates[0], 25f, out _),
                "Could not anchor multi-candidate observer pair.");
            visibleCount = 1;
            float[] yawOffsets = { -50f, -32f, -16f, 16f, 32f, 50f };
            for (int index = 1; index < candidates.Count && index <= yawOffsets.Length; index++)
            {
                if (TryPlaceVisibleCandidate(candidates[index], yawOffsets[index - 1], 25f))
                    visibleCount++;
            }
            Physics.SyncTransforms();
            return visibleCount >= MultipleCandidateMinimum;
        }

        private static bool TryPlaceVisibleCandidate(ActorRuntimeIdentity candidate, float yawOffset, float distance)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                Vector3 direction = Quaternion.Euler(0f, yawOffset + attempt * 2f, 0f) * observer.transform.forward;
                Vector3 requested = observer.transform.position + direction * (distance - attempt);
                if (!NavMesh.SamplePosition(requested, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                    continue;
                Place(candidate, hit.position, Quaternion.LookRotation(-direction));
                Physics.SyncTransforms();
                if (observer.GetComponent<ActorVisualPerceptionService>().Evaluate(candidate).Perceived)
                    return true;
            }
            return false;
        }

        private static bool TryMoveTargetFartherBehindWall(ActorRuntimeIdentity target, out Vector3 position)
        {
            Vector3 direction = FlatDirection(observer, target);
            Vector3 requested = target.transform.position + direction * 4f;
            if (!NavMesh.SamplePosition(requested, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                position = default;
                return false;
            }
            Place(target, hit.position, target.transform.rotation);
            Physics.SyncTransforms();
            position = target.transform.position;
            return observer.GetComponent<ActorVisualPerceptionService>().Evaluate(target).Reason ==
                   ActorVisualPerceptionReason.Occluded;
        }

        private static void Place(ActorRuntimeIdentity actor, Vector3 position, Quaternion rotation)
        {
            actor.GetComponent<HumanEncounterAIController>().ClearThreat("Progressive Recognition placement");
            ActorNavigationController navigation = actor.GetComponent<ActorNavigationController>();
            navigation.Stop();
            Require(navigation.Agent != null && navigation.Agent.isOnNavMesh && navigation.Agent.Warp(position),
                "Actor could not warp through its existing NavMeshAgent: " + actor.ActorInstanceId);
            actor.transform.rotation = rotation;
            navigation.Agent.nextPosition = position;
        }

        private static Vector3 FlatDirection(ActorRuntimeIdentity from, ActorRuntimeIdentity to)
        {
            Vector3 direction = Vector3.ProjectOnPlane(to.transform.position - from.transform.position, Vector3.up);
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : from.transform.forward;
        }

        private static void CreateLosWall(ActorRuntimeIdentity observerActor, ActorRuntimeIdentity target)
        {
            DestroyWall();
            Vector3 eye = observerActor.transform.position + Vector3.up *
                observerActor.GetComponent<ActorVisualPerceptionService>().EyeHeight;
            Vector3 targetCenter = target.GetComponent<Collider>().bounds.center;
            Vector3 direction = targetCenter - eye;
            losWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            losWall.name = "M41 Progressive Recognition LOS Wall";
            losWall.transform.SetPositionAndRotation(Vector3.Lerp(eye, targetCenter, 0.5f),
                Quaternion.LookRotation(direction.normalized));
            losWall.transform.localScale = new Vector3(8f, 4f, 1f);
            Physics.SyncTransforms();
        }

        private static void DestroyWall()
        {
            if (losWall == null)
                return;
            UnityEngine.Object.Destroy(losWall);
            losWall = null;
            Physics.SyncTransforms();
        }

        private static bool Approximately(float left, float right) => Mathf.Abs(left - right) < 0.0001f;

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
            candidates.Clear();
            runtime = null;
            sandbox = null;
            player = null;
            observer = null;
            nearTarget = null;
            farTarget = null;
            losWall = null;
            if (!string.IsNullOrWhiteSpace(failure))
                Debug.LogError("M41 Progressive Visual Recognition Diagnostics: FAIL\n" + failure);
            EditorApplication.Exit(string.IsNullOrWhiteSpace(failure) && exitCode == 0 ? 0 : 1);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
