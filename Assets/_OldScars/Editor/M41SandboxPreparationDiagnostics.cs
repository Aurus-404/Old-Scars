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
    public static class M41SandboxPreparationDiagnostics
    {
        private const string PendingKey = "OldScars.M41SandboxPreparation.Pending";
        private const string StageKey = "OldScars.M41SandboxPreparation.Stage";
        private const string FailureKey = "OldScars.M41SandboxPreparation.Failure";
        private const string RootKey = "OldScars.M41SandboxPreparation.Root";
        private const long WorldSeed = 941401002L;
        private const long SandboxSeed = 41410002L;
        private const string IncapacitatedAffiliationId = "diagnostic_incapacitated";
        private const string IncapacitatedWoundA = "wound_f6666666666666666666666666666666";
        private const string IncapacitatedWoundB = "wound_f7777777777777777777777777777777";
        private const float RequiredTravelDistance = 0.75f;

        private struct TravelObservation
        {
            public Vector3 PreviousPosition;
            public float Distance;

            public void Reset(ActorRuntimeIdentity actor)
            {
                PreviousPosition = actor.transform.position;
                Distance = 0f;
            }

            public void Sample(ActorRuntimeIdentity actor)
            {
                Vector3 current = actor.transform.position;
                Distance += Vector3.ProjectOnPlane(current - PreviousPosition, Vector3.up).magnitude;
                PreviousPosition = current;
            }
        }

        private static WorldRuntimeSceneController runtime;
        private static SandboxNpcController sandbox;
        private static ActorRuntimeIdentity player;
        private static ActorRuntimeIdentity blue;
        private static ActorRuntimeIdentity red;
        private static ActorRuntimeIdentity white;
        private static float stageStartedAt;
        private static TravelObservation blueInitialTravel;
        private static TravelObservation redInitialTravel;
        private static TravelObservation whiteInitialTravel;
        private static TravelObservation redResumeTravel;
        private static TravelObservation inactiveTravel;
        private static int redAmbientOrdersAtThreat;
        private static int inactiveOwnerRevision;
        private static int inactiveTransitionRevision;
        private static int inactiveAttackCount;
        private static int inactiveScanCount;
        private static int inactiveAmbientOrderCount;
        private static float inactiveAmbientTravel;

        static M41SandboxPreparationDiagnostics()
        {
            EditorApplication.update -= Continue;
            EditorApplication.update += Continue;
        }

        public static void RunBatchWorldRuntime()
        {
            if (!Application.isBatchMode || EditorApplication.isCompiling)
                throw new InvalidOperationException("M41 sandbox preparation diagnostics require compiled Unity batchmode.");

            string root = Path.Combine(Path.GetTempPath(), "OldScars_M41_Preparation_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            SessionState.SetString(RootKey, root);
            SessionState.SetBool(PendingKey, true);
            SessionState.SetInt(StageKey, 0);
            SessionState.EraseString(FailureKey);
            WorldRuntimeTerrainDevelopmentSettings.SetDiagnosticSelectionOverride(
                WorldRuntimeTerrainDevelopmentSelection.UnityTerrain);
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
                    WorldRuntimeTerrainDevelopmentSelection.UnityTerrain);
                if (EditorApplication.isPlaying)
                {
                    RunPlayStage();
                    return;
                }
                if (!EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetInt(StageKey, 0) == 99)
                    Finish(0);
                else if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    Finish(1, "M41 sandbox preparation diagnostic was interrupted before completion.");
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
                    throw new InvalidOperationException("WorldRuntime did not reach sandbox preparation readiness.");
                return;
            }

            switch (stage)
            {
                case 1:
                    SetupAffiliationAndRoaming();
                    break;
                case 2:
                    ObserveInitialRoaming();
                    break;
                case 3:
                    ObserveThreatInterruption();
                    break;
                case 4:
                    ObserveEncounterOwnership();
                    break;
                case 5:
                    ObserveAmbientResumeAndSetupIncapacity();
                    break;
                case 6:
                    EstablishStableInactive();
                    break;
                case 7:
                    VerifyStableInactive();
                    break;
            }
        }

        private static void CreateWorldSessionAndLoadRuntime()
        {
            WorldSessionService.Close();
            var store = new PersistenceFileStore(SessionState.GetString(RootKey, string.Empty));
            WorldSessionOperationResult created = WorldSessionService.Create(
                "M41 Sandbox Preparation", new OldScars.Core.World.WorldSeed(WorldSeed),
                WorldGenerationSettings.ResolvePreset(WorldSizePreset.Small),
                LandCoveragePreset.High, GameDataManager.Instance.LoadedContentSet, store);
            Require(created.Success, "Could not create M41 sandbox preparation WorldSession: " + created.Failure);
            SceneManager.LoadScene(WorldApplicationScenes.WorldRuntimeSceneName, LoadSceneMode.Single);
        }

        private static void SetupAffiliationAndRoaming()
        {
            Require(runtime.TerrainSelection == WorldRuntimeTerrainDevelopmentSelection.UnityTerrain,
                "Sandbox preparation did not use the default Unity Terrain WorldRuntime.");
            sandbox = runtime.GameplayRuntimeComposition.SandboxNpcController;
            player = runtime.PlayerComposition.PlayerIdentity;
            string seedError = null;
            Require(sandbox != null && player != null && player.IsRegistered &&
                    sandbox.TrySetBaseSeed(SandboxSeed.ToString(), out seedError),
                "Sandbox/player authority or deterministic seed is unavailable: " + seedError);
            Require(sandbox.TrySpawnBlueNpc(out SandboxNpcMetadata blueMetadata, out string blueError),
                "Blue spawn failed: " + blueError);
            Require(sandbox.TrySpawnRedNpc(out SandboxNpcMetadata redMetadata, out string redError),
                "Red spawn failed: " + redError);
            Require(sandbox.TrySpawnRandomNpc(out SandboxNpcMetadata whiteMetadata, out string whiteError),
                "White sandbox spawn failed: " + whiteError);
            blue = blueMetadata.GetComponent<ActorRuntimeIdentity>();
            red = redMetadata.GetComponent<ActorRuntimeIdentity>();
            white = whiteMetadata.GetComponent<ActorRuntimeIdentity>();
            blue.GetComponent<ActorThreatAcquisitionController>().enabled = false;
            red.GetComponent<ActorThreatAcquisitionController>().enabled = false;
            blue.GetComponent<HumanEncounterAIController>().ClearThreat("Sandbox preparation baseline");
            red.GetComponent<HumanEncounterAIController>().ClearThreat("Sandbox preparation baseline");

            ActorAffiliationComponent playerAffiliation = player.GetComponent<ActorAffiliationComponent>();
            ActorAffiliationComponent blueAffiliation = blue.GetComponent<ActorAffiliationComponent>();
            ActorAffiliationComponent redAffiliation = red.GetComponent<ActorAffiliationComponent>();
            Require(blueAffiliation.GetDispositionToward(redAffiliation) == ActorDisposition.Hostile &&
                    redAffiliation.GetDispositionToward(blueAffiliation) == ActorDisposition.Hostile &&
                    redAffiliation.GetDispositionToward(playerAffiliation) == ActorDisposition.Hostile &&
                    blueAffiliation.GetDispositionToward(playerAffiliation) == ActorDisposition.Neutral,
                "Blue/Red/Player affiliation matrix is incorrect.");
            Require(blue.GetComponent<ActorBehaviorController>()?.IsAmbientConfigured == true &&
                    red.GetComponent<ActorBehaviorController>()?.IsAmbientConfigured == true &&
                    white.GetComponent<ActorBehaviorController>()?.IsAmbientConfigured == true,
                "Blue/Red/White sandbox spawns did not receive configured behavior ownership.");
            Require(blue.GetComponent<ActorBehaviorController>().Owner == ActorBehaviorOwner.Ambient &&
                    red.GetComponent<ActorBehaviorController>().Owner == ActorBehaviorOwner.Ambient &&
                    white.GetComponent<ActorBehaviorController>().Owner == ActorBehaviorOwner.Ambient,
                "Blue/Red/White did not begin with Ambient behavior ownership.");
            blueInitialTravel.Reset(blue);
            redInitialTravel.Reset(red);
            whiteInitialTravel.Reset(white);
            SetStage(2);
        }

        private static void ObserveInitialRoaming()
        {
            blueInitialTravel.Sample(blue);
            redInitialTravel.Sample(red);
            whiteInitialTravel.Sample(white);
            if (blueInitialTravel.Distance < RequiredTravelDistance ||
                redInitialTravel.Distance < RequiredTravelDistance ||
                whiteInitialTravel.Distance < RequiredTravelDistance)
            {
                Require(Time.time - stageStartedAt <= 12f,
                    "Blue/Red/White did not each travel the required physical Ambient distance. " +
                    $"Blue={blueInitialTravel.Distance:0.###}; Red={redInitialTravel.Distance:0.###}; " +
                    $"White={whiteInitialTravel.Distance:0.###}.");
                return;
            }
            ActorBehaviorController blueBehavior = blue.GetComponent<ActorBehaviorController>();
            ActorBehaviorController redBehavior = red.GetComponent<ActorBehaviorController>();
            ActorBehaviorController whiteBehavior = white.GetComponent<ActorBehaviorController>();
            Require(Vector3.Distance(blue.transform.position, blueBehavior.HomeAnchor) <= blueBehavior.MaximumRoamRadius + 3.5f &&
                    Vector3.Distance(red.transform.position, redBehavior.HomeAnchor) <= redBehavior.MaximumRoamRadius + 3.5f &&
                    Vector3.Distance(white.transform.position, whiteBehavior.HomeAnchor) <= whiteBehavior.MaximumRoamRadius + 3.5f,
                "Idle roaming drifted beyond its bounded spawn/home anchor radius.");

            Require(TryPlacePairWithClearPerception(red, blue, 6f, out string placementError),
                "Could not place Blue/Red acquisition pair: " + placementError);
            Require(blueBehavior.EnterEncounter("Stationary diagnostic encounter target"),
                "Blue diagnostic target could not hold explicit Encounter ownership.");
            ActorAffiliationComponent redAffiliation = red.GetComponent<ActorAffiliationComponent>();
            Require(redAffiliation.TryConfigure(SandboxNpcController.RedAffiliationId, "Red",
                    new[] { SandboxNpcController.BlueAffiliationId }, out string affiliationError),
                "Could not isolate Red->Blue interruption relation: " + affiliationError);
            red.GetComponent<ActorThreatAcquisitionController>().enabled = true;
            SetStage(3);
        }

        private static void ObserveThreatInterruption()
        {
            HumanEncounterAIController encounter = red.GetComponent<HumanEncounterAIController>();
            if (encounter.Threat != blue)
            {
                Require(Time.time - stageStartedAt <= 5f,
                    "Blue/Red hostility did not acquire through the existing perception/acquisition chain.");
                return;
            }
            ActorBehaviorController behavior = red.GetComponent<ActorBehaviorController>();
            Require(behavior.Owner == ActorBehaviorOwner.Encounter,
                "Recognized threat did not explicitly transfer behavior ownership to Encounter.");
            Require(encounter.TryOverrideResponse(HumanEncounterResponse.Avoid, out string overrideError),
                "Could not keep the ownership diagnostic non-lethal: " + overrideError);
            redAmbientOrdersAtThreat = behavior.AmbientAcceptedOrderCount;
            SetStage(4);
        }

        private static void ObserveEncounterOwnership()
        {
            HumanEncounterAIController encounter = red.GetComponent<HumanEncounterAIController>();
            ActorBehaviorController behavior = red.GetComponent<ActorBehaviorController>();
            Require(encounter.Threat == blue && behavior.Owner == ActorBehaviorOwner.Encounter &&
                    behavior.AmbientAcceptedOrderCount == redAmbientOrdersAtThreat,
                "Ambient emitted an order or lost ownership while Encounter remained active.");
            if (Time.time - stageStartedAt < 0.75f)
                return;
            red.GetComponent<ActorThreatAcquisitionController>().enabled = false;
            encounter.ClearThreat("Sandbox preparation interruption complete");
            Require(behavior.Owner == ActorBehaviorOwner.Ambient,
                "Releasing Encounter did not explicitly return ownership to Ambient.");
            redResumeTravel.Reset(red);
            SetStage(5);
        }

        private static void ObserveAmbientResumeAndSetupIncapacity()
        {
            redResumeTravel.Sample(red);
            ActorBehaviorController behavior = red.GetComponent<ActorBehaviorController>();
            if (redResumeTravel.Distance < RequiredTravelDistance)
            {
                Require(Time.time - stageStartedAt <= 12f,
                    $"Red did not resume physical Ambient travel after Encounter release. Travel={redResumeTravel.Distance:0.###}.");
                return;
            }
            Require(behavior.Owner == ActorBehaviorOwner.Ambient &&
                    behavior.AmbientAcceptedOrderCount > redAmbientOrdersAtThreat,
                "Red moved after release without a new Ambient-owned order.");
            ActorAffiliationComponent affiliation = red.GetComponent<ActorAffiliationComponent>();
            Require(affiliation.TryConfigure(IncapacitatedAffiliationId, "Incapacity Diagnostic",
                    new[] { "diagnostic_absent" }, out string affiliationError),
                "Could not isolate incapacity acquisition candidates: " + affiliationError);
            HumanEncounterAIController encounter = red.GetComponent<HumanEncounterAIController>();
            encounter.ClearThreat("Prepare incapacity regression");
            red.GetComponent<ActorNavigationController>().Stop();
            ActorThreatAcquisitionController acquisition = red.GetComponent<ActorThreatAcquisitionController>();
            acquisition.enabled = true;
            ActorMedicalStateComponent medical = red.GetComponent<ActorMedicalStateComponent>();
            Require(medical.TryApplyWound(IncapacitatedWoundA, BodyRegion.Head, WoundType.Blunt, 0.4f, 0f, 0.3f, out string failure) &&
                    medical.TryApplyWound(IncapacitatedWoundB, BodyRegion.Head, WoundType.Blunt, 0.5f, 0f, 0.05f, out failure),
                "Could not create recoverable incapacitation through existing medical trauma: " + failure);
            Require(red.GetComponent<ActorConditionComponent>().IsUnconscious,
                "Integrated incapacity fixture did not become Unconscious.");
            SetStage(6);
        }

        private static void EstablishStableInactive()
        {
            HumanEncounterAIController encounter = red.GetComponent<HumanEncounterAIController>();
            if (encounter.State != HumanEncounterAIState.Inactive)
            {
                Require(Time.time - stageStartedAt <= 2f,
                    "Functionally incapacitated actor did not enter Inactive.");
                return;
            }
            ActorBehaviorController behavior = red.GetComponent<ActorBehaviorController>();
            if (behavior.Owner != ActorBehaviorOwner.Inactive)
            {
                Require(Time.time - stageStartedAt <= 5f,
                    "Incapacitated actor did not settle with Inactive behavior ownership.");
                return;
            }
            inactiveOwnerRevision = behavior.OwnerRevision;
            inactiveTransitionRevision = encounter.TransitionRevision;
            inactiveAttackCount = encounter.AttackCount;
            inactiveScanCount = red.GetComponent<ActorThreatAcquisitionController>().AcquisitionScanCount;
            inactiveAmbientOrderCount = behavior.AmbientAcceptedOrderCount;
            inactiveAmbientTravel = behavior.AmbientDistanceTravelled;
            inactiveTravel.Reset(red);
            SetStage(7);
        }

        private static void VerifyStableInactive()
        {
            if (Time.time - stageStartedAt < 0.5f)
                return;
            inactiveTravel.Sample(red);
            HumanEncounterAIController encounter = red.GetComponent<HumanEncounterAIController>();
            ActorThreatAcquisitionController acquisition = red.GetComponent<ActorThreatAcquisitionController>();
            ActorBehaviorController behavior = red.GetComponent<ActorBehaviorController>();
            Require(acquisition.enabled && encounter.State == HumanEncounterAIState.Inactive && encounter.Threat == null &&
                    encounter.TransitionRevision == inactiveTransitionRevision && encounter.AttackCount == inactiveAttackCount &&
                    acquisition.AcquisitionScanCount == inactiveScanCount &&
                    behavior.Owner == ActorBehaviorOwner.Inactive && behavior.OwnerRevision == inactiveOwnerRevision &&
                    behavior.AmbientAcceptedOrderCount == inactiveAmbientOrderCount &&
                    Mathf.Abs(behavior.AmbientDistanceTravelled - inactiveAmbientTravel) < 0.001f &&
                    red.GetComponent<ActorNavigationController>().State != ActorNavigationState.Moving,
                "Inactive actor changed owner, travelled, acquired, navigated or attacked while acquisition remained enabled. " +
                $"AcquisitionEnabled={acquisition.enabled}; State={encounter.State}; Threat={encounter.ThreatActorInstanceId ?? "<NONE>"}; " +
                $"Transition={encounter.TransitionRevision}/{inactiveTransitionRevision}; Attack={encounter.AttackCount}/{inactiveAttackCount}; " +
                $"Scans={acquisition.AcquisitionScanCount}/{inactiveScanCount}; Owner={behavior.Owner}; " +
                $"OwnerRevision={behavior.OwnerRevision}/{inactiveOwnerRevision}; AmbientOrders={behavior.AmbientAcceptedOrderCount}/{inactiveAmbientOrderCount}; " +
                $"AmbientTravel={behavior.AmbientDistanceTravelled:0.###}/{inactiveAmbientTravel:0.###}; " +
                $"PhysicalCollapseTravel={inactiveTravel.Distance:0.###}; Nav={red.GetComponent<ActorNavigationController>().State}.");
            Debug.Log(
                "M41 Sandbox Preparation Diagnostics: PASS\n" +
                "- Relations: Blue<->Red hostile; Red->Player hostile; Blue->Player neutral\n" +
                $"- Ambient physical travel: Blue={blueInitialTravel.Distance:0.###}m; " +
                $"Red={redInitialTravel.Distance:0.###}m; White={whiteInitialTravel.Distance:0.###}m\n" +
                $"- Ownership: Ambient -> Encounter -> Ambient; resumed Red travel={redResumeTravel.Distance:0.###}m\n" +
                $"- Incapacity: Inactive stable; ambient travel delta=0m; physical collapse travel={inactiveTravel.Distance:0.###}m; " +
                "no threat/navigation/attack");
            SessionState.SetInt(StageKey, 99);
            EditorApplication.ExitPlaymode();
        }

        private static bool TryPlacePairWithClearPerception(
            ActorRuntimeIdentity observer, ActorRuntimeIdentity target, float requestedDistance, out string error)
        {
            Vector3 center = player.transform.position;
            ActorVisualPerceptionReason lastReason = ActorVisualPerceptionReason.LineOfSightMiss;
            for (int index = 0; index < 24; index++)
            {
                Vector3 direction = Quaternion.Euler(0f, index * 15f, 0f) * Vector3.forward;
                Vector3 lateral = Vector3.Cross(Vector3.up, direction).normalized;
                Vector3 pairCenter = center + lateral * 18f;
                if (!NavMesh.SamplePosition(pairCenter - direction * requestedDistance * 0.5f,
                        out NavMeshHit observerHit, 6f, NavMesh.AllAreas) ||
                    !NavMesh.SamplePosition(pairCenter + direction * requestedDistance * 0.5f,
                        out NavMeshHit targetHit, 6f, NavMesh.AllAreas))
                    continue;
                Place(observer, observerHit.position, Quaternion.LookRotation(direction));
                Place(target, targetHit.position, Quaternion.LookRotation(-direction));
                Physics.SyncTransforms();
                ActorVisualPerceptionResult perception = observer.GetComponent<ActorVisualPerceptionService>().Evaluate(target);
                lastReason = perception.Reason;
                if (perception.Perceived)
                {
                    error = null;
                    return true;
                }
            }
            error = "No clear Blue/Red NavMesh pair was found (last reason=" + lastReason + ").";
            return false;
        }

        private static void Place(ActorRuntimeIdentity actor, Vector3 position, Quaternion rotation)
        {
            actor.GetComponent<HumanEncounterAIController>().ClearThreat("Sandbox preparation placement");
            ActorNavigationController navigation = actor.GetComponent<ActorNavigationController>();
            navigation.Stop();
            Require(navigation.Agent != null && navigation.Agent.isOnNavMesh && navigation.Agent.Warp(position),
                "Diagnostic actor could not warp through existing NavMeshAgent: " + actor.ActorInstanceId);
            actor.transform.rotation = rotation;
            navigation.Agent.nextPosition = position;
        }

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
            blue = null;
            red = null;
            white = null;
            if (!string.IsNullOrWhiteSpace(failure))
                Debug.LogError("M41 Sandbox Preparation Diagnostics: FAIL\n- " + failure);
            EditorApplication.Exit(string.IsNullOrWhiteSpace(failure) && exitCode == 0 ? 0 : 1);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
