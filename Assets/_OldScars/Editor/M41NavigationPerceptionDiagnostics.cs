using System;
using System.Collections.Generic;
using System.Linq;
using OldScars.Core;
using OldScars.Core.Actors;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Interactions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace OldScars.Editor
{
    [InitializeOnLoad]
    public static class M41NavigationPerceptionDiagnostics
    {
        private const string Menu = "Old Scars/Diagnostics/AI/Run M41.0 Navigation & Perception";
        private const string ManualMenu = "Old Scars/Diagnostics/AI/M41.0 Prepare Manual Validation";
        private const string ToggleMenu = "Old Scars/Diagnostics/AI/M41.0 Toggle Manual Perception Blocker";
        private const string PhaseKey = "OldScars.M41.NavigationPerception.Phase";
        private const string ErrorKey = "OldScars.M41.NavigationPerception.Error";
        private const string Enter = "enter";
        private const string Running = "running";
        private const string Finish = "finish";
        private const string ProfileId = "core:debug_navigation_npc_01";
        private const int PerceptionStressIterations = 64;
        private const int SaturationBlockerCount = 20;

        private static readonly List<ActorRuntimeIdentity> registryDiscoveryBuffer = new List<ActorRuntimeIdentity>(8);
        private static ActorRuntimeIdentity navigator;
        private static ActorRuntimeIdentity observer;
        private static ActorRuntimeIdentity target;
        private static ActorNavigationController navigatorController;
        private static Vector3 navigationStart;
        private static Vector3 navigationGoal;
        private static Vector3 observedStart;
        private static Vector3 deadPosition;
        private static double deadline;
        private static int stage;
        private static int stableFrame;

        static M41NavigationPerceptionDiagnostics()
        {
            EditorApplication.update -= Continue;
            EditorApplication.update += Continue;
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("M41.0 diagnostics require idle Edit Mode.");

            ClearRun();
            SessionState.SetString(PhaseKey, Enter);
            EditorSceneManager.OpenScene(M41SampleSceneNavigationTools.ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static bool ValidateRun() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;

        public static void PrepareManualValidation()
        {
            Require(EditorApplication.isPlaying && GameDataManager.Instance?.IsReady == true,
                "Manual M41.0 setup requires Play Mode with ready game data.");
            RemoveManualActors();
            Transform start = Marker(M41SampleSceneNavigationTools.StartName);
            Transform goal = Marker(M41SampleSceneNavigationTools.GoalName);
            Transform observerMarker = Marker(M41SampleSceneNavigationTools.ObserverName);
            Transform targetMarker = Marker(M41SampleSceneNavigationTools.TargetName);
            GameObject barrier = Barrier();
            barrier.SetActive(true);

            ActorRuntimeIdentity manualNavigator = Spawn(start.position, start.rotation, "M41 Manual Navigator");
            ActorRuntimeIdentity manualObserver = Spawn(observerMarker.position, observerMarker.rotation, "M41 Manual Observer");
            ActorRuntimeIdentity manualTarget = Spawn(targetMarker.position, targetMarker.rotation, "M41 Manual Target");
            PlacePlayerNearFixture();
            Require(manualNavigator.GetComponent<ActorNavigationController>().TryNavigate(goal.position, out ActorNavigationResult order),
                "Manual navigator rejected goal: " + order.Failure + " / " + order.Detail);
            ResetPerceptionPair(manualObserver, manualTarget);
            Physics.SyncTransforms();
            ActorVisualPerceptionResult sight = manualObserver.GetComponent<ActorVisualPerceptionService>().Evaluate(manualTarget);
            RequireManualPerceptionContract(barrier, sight);
            Debug.Log(
                "[M41.0][MANUAL_READY]" +
                $"\n  Navigator: {manualNavigator.ActorInstanceId}" +
                $"\n  NavigationState: {manualNavigator.GetComponent<ActorNavigationController>().State}" +
                $"\n  Observer: {manualObserver.ActorInstanceId}" +
                $"\n  Target: {manualTarget.ActorInstanceId}" +
                $"\n  Perception: {sight.Reason}" +
                $"\n  Blocker: {sight.Blocker?.name ?? "<NONE>"}" +
                "\n  Check: navigator routes around the barrier and stops Reached; barrier starts opaque." +
                "\n  Next: use M41.0 Toggle Manual Perception Blocker to compare Occluded and Perceived.");
        }

        private static bool ValidateManual() => EditorApplication.isPlaying && !EditorApplication.isCompiling;

        public static void ToggleManualPerceptionBlocker()
        {
            Require(EditorApplication.isPlaying, "Manual blocker toggle requires Play Mode.");
            GameObject barrier = Barrier();
            barrier.SetActive(!barrier.activeSelf);
            Physics.SyncTransforms();
            ActorRuntimeIdentity manualObserver = ActorRuntimeRegistry.ActiveRepresentations
                .FirstOrDefault(value => value != null && value.name == "M41 Manual Observer");
            ActorRuntimeIdentity manualTarget = ActorRuntimeRegistry.ActiveRepresentations
                .FirstOrDefault(value => value != null && value.name == "M41 Manual Target");
            Require(manualObserver != null && manualTarget != null,
                "Run M41.0 Prepare Manual Validation before toggling the blocker.");
            ResetPerceptionPair(manualObserver, manualTarget);
            Physics.SyncTransforms();
            ActorVisualPerceptionResult sight = manualObserver.GetComponent<ActorVisualPerceptionService>().Evaluate(manualTarget);
            RequireManualPerceptionContract(barrier, sight);
            Debug.Log(
                "[M41.0][MANUAL_PERCEPTION]" +
                $"\n  BarrierActive: {barrier.activeSelf}" +
                $"\n  Perceived: {sight.Perceived}" +
                $"\n  Reason: {sight.Reason}" +
                $"\n  Blocker: {sight.Blocker?.name ?? "<NONE>"}");
        }

        private static bool ValidateToggle() => EditorApplication.isPlaying && !EditorApplication.isCompiling;

        private static void Continue()
        {
            string phase = SessionState.GetString(PhaseKey, string.Empty);
            if (string.IsNullOrEmpty(phase))
                return;
            try
            {
                if (phase == Enter && Ready())
                {
                    BeginPlayRun();
                    SessionState.SetString(PhaseKey, Running);
                    return;
                }
                if (phase == Running && EditorApplication.isPlaying)
                {
                    TickPlayRun();
                    return;
                }
                if (phase == Finish && !EditorApplication.isPlayingOrWillChangePlaymode)
                    FinalizeRun();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetString(ErrorKey, exception.Message);
                SessionState.SetString(PhaseKey, Finish);
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorApplication.ExitPlaymode();
            }
        }

        private static bool Ready()
        {
            return EditorApplication.isPlaying && Time.frameCount >= 5 &&
                   GameDataManager.Instance != null && GameDataManager.Instance.IsReady;
        }

        private static void BeginPlayRun()
        {
            Require(GameDataManager.Instance.Report != null && GameDataManager.Instance.Report.ErrorCount == 0,
                "Game data validation contains errors.");
            ActorProfileDefinition profile = GameDataManager.Instance.Database.GetActorProfile(ProfileId);
            Require(profile?.navigation != null && profile.visual_perception != null,
                "Diagnostic NPC profile lacks M41.0 capability blocks.");

            Transform start = Marker(M41SampleSceneNavigationTools.StartName);
            Transform goal = Marker(M41SampleSceneNavigationTools.GoalName);
            Transform observerMarker = Marker(M41SampleSceneNavigationTools.ObserverName);
            Transform targetMarker = Marker(M41SampleSceneNavigationTools.TargetName);
            bool hasStart = NavMesh.SamplePosition(start.position, out NavMeshHit startHit, 2f, NavMesh.AllAreas);
            bool hasGoal = NavMesh.SamplePosition(goal.position, out NavMeshHit goalHit, 2f, NavMesh.AllAreas);
            Require(hasStart && hasGoal,
                "Prepared start/goal do not resolve to NavMesh in Play Mode.");

            navigator = Spawn(start.position, start.rotation, "M41 Diagnostic Navigator");
            observer = Spawn(observerMarker.position, observerMarker.rotation, "M41 Diagnostic Observer");
            target = Spawn(targetMarker.position, targetMarker.rotation, "M41 Diagnostic Target");
            Require(navigator.ActorInstanceId != observer.ActorInstanceId && observer.ActorInstanceId != target.ActorInstanceId &&
                    ActorRuntimeRegistry.TryGet(navigator.ActorInstanceId, out _) &&
                    ActorRuntimeRegistry.TryGet(observer.ActorInstanceId, out _) &&
                    ActorRuntimeRegistry.TryGet(target.ActorInstanceId, out _),
                "Runtime spawn/registry identity is incoherent.");

            navigatorController = navigator.GetComponent<ActorNavigationController>();
            ActorVisualPerceptionService perception = observer.GetComponent<ActorVisualPerceptionService>();
            Require(navigatorController != null && perception != null && navigator.GetComponent<NavMeshAgent>() != null,
                "Spawned profile did not attach declared M41.0 capabilities.");
            Require(Approximately(navigatorController.Agent.speed, profile.navigation.speed) &&
                    Approximately(navigatorController.Agent.acceleration, profile.navigation.acceleration) &&
                    Approximately(navigatorController.Agent.angularSpeed, profile.navigation.angular_speed) &&
                    Approximately(navigatorController.Agent.stoppingDistance, profile.navigation.stopping_distance) &&
                    Approximately(perception.VisualRange, profile.visual_perception.visual_range) &&
                    Approximately(perception.HorizontalFovDegrees, profile.visual_perception.horizontal_fov_degrees) &&
                    Approximately(perception.EyeHeight, profile.visual_perception.eye_height),
                "Runtime M41.0 parameters do not match ActorProfile data.");

            PlayerMovementController player = UnityEngine.Object.FindAnyObjectByType<PlayerMovementController>();
            PlayerMovementInputController playerInput = UnityEngine.Object.FindAnyObjectByType<PlayerMovementInputController>();
            Require(player != null && playerInput != null && player.GetComponent<CharacterController>() != null &&
                    player.GetComponent<NavMeshAgent>() == null &&
                    player.GetComponent<ActorNavigationController>() == null,
                "Player movement authority was replaced or received NPC navigation.");
            Require(!HasCombatComponent(navigator) && !HasCombatComponent(observer) && !HasCombatComponent(target),
                "M41.0 actor capabilities unexpectedly require a Combat component.");

            navigationStart = navigator.transform.position;
            navigationGoal = goalHit.position;
            observedStart = navigationStart;
            Require(navigatorController.TryNavigate(goal.position, out ActorNavigationResult order),
                "Alive navigator rejected reachable destination: " + order.Failure + " / " + order.Detail);
            deadline = Time.realtimeSinceStartupAsDouble + 15d;
            stage = 1;
        }

        private static void TickPlayRun()
        {
            if (stage == 1)
            {
                if ((navigator.transform.position - navigationStart).sqrMagnitude > 0.25f)
                    observedStart = navigator.transform.position;
                if (navigatorController.State == ActorNavigationState.Reached)
                {
                    Require((observedStart - navigationStart).sqrMagnitude > 0.25f,
                        "Navigator reported Reached without physical displacement.");
                    Require(Vector3.ProjectOnPlane(navigator.transform.position - navigationGoal, Vector3.up).magnitude <=
                            navigatorController.Agent.stoppingDistance + 0.35f,
                        "Navigator stopped outside arrival tolerance.");
                    Require(navigatorController.Agent.isStopped,
                        "Reached navigation did not stop its NavMeshAgent.");
                    stableFrame = Time.frameCount + 3;
                    stage = 2;
                    return;
                }
                Require(Time.realtimeSinceStartupAsDouble <= deadline,
                    "Reachable navigation timed out. State=" + navigatorController.State + ", Failure=" + navigatorController.Failure);
                return;
            }

            if (stage == 2 && Time.frameCount >= stableFrame)
            {
                Require(navigatorController.State == ActorNavigationState.Reached &&
                        navigatorController.Agent.isStopped && !navigatorController.Agent.hasPath,
                    "Reached navigation retained an active path/loop after stabilization.");
                Require(!navigatorController.TryNavigate(new Vector3(10000f, 10000f, 10000f), out ActorNavigationResult invalid) &&
                        invalid.State == ActorNavigationState.Failed &&
                        invalid.Failure == ActorNavigationFailure.DestinationOffNavMesh,
                    "Invalid destination did not fail explicitly as DestinationOffNavMesh.");
                Collider floor = M41SampleSceneNavigationTools.FindMarker(M41SampleSceneNavigationTools.FloorName)
                    .GetComponent<Collider>();
                Vector3 nearOffMesh = new Vector3(floor.bounds.max.x + 0.1f, floor.bounds.max.y, floor.bounds.center.z);
                Require(!navigatorController.TryNavigate(nearOffMesh, out ActorNavigationResult nearInvalid) &&
                        nearInvalid.State == ActorNavigationState.Failed &&
                        nearInvalid.Failure == ActorNavigationFailure.DestinationOffNavMesh,
                    "Near-edge off-NavMesh destination was projected into a valid order.");
                stableFrame = Time.frameCount + 5;
                stage = 3;
                return;
            }

            if (stage == 3 && Time.frameCount >= stableFrame)
            {
                Require(navigatorController.State == ActorNavigationState.Failed &&
                        navigatorController.Failure == ActorNavigationFailure.DestinationOffNavMesh &&
                        !navigatorController.Agent.hasPath,
                    "Invalid destination failure retried or changed without a new order.");
                Transform start = Marker(M41SampleSceneNavigationTools.StartName);
                Require(navigatorController.TryNavigate(start.position, out ActorNavigationResult order),
                    "Navigator rejected a new valid order before lifecycle test: " + order.Failure);
                navigator.GetComponent<ActorHealthComponent>().Kill();
                deadPosition = navigator.transform.position;
                stableFrame = Time.frameCount + 3;
                stage = 4;
                return;
            }

            if (stage == 4 && Time.frameCount >= stableFrame)
            {
                Require(navigator.LifecycleState == ActorLifecycleState.Dead &&
                        navigatorController.State == ActorNavigationState.Failed &&
                         navigatorController.Failure == ActorNavigationFailure.Dead &&
                         navigatorController.Agent.isStopped && !navigatorController.Agent.hasPath,
                    "Dead lifecycle did not terminate active navigation.");
                Require(Vector3.ProjectOnPlane(navigator.transform.position - deadPosition, Vector3.up).sqrMagnitude <= 0.0001f,
                    "Dead navigator continued moving after lifecycle terminated its path.");
                Require(!navigatorController.TryNavigate(navigationGoal, out ActorNavigationResult deadOrder) &&
                        deadOrder.Failure == ActorNavigationFailure.Dead,
                    "Dead actor accepted a new navigation order.");
                RunPerceptionAndRestoreCases();
                CompletePlayRun();
            }
        }

        private static void PlacePlayerNearFixture()
        {
            PlayerMovementController player = UnityEngine.Object.FindAnyObjectByType<PlayerMovementController>();
            GameObject fixture = GameObject.Find(M41SampleSceneNavigationTools.FixtureRootName);
            Require(player != null && fixture != null,
                "Manual framing requires the authored player and M41.0 fixture.");
            CharacterController controller = player.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (wasEnabled)
                controller.enabled = false;
            player.transform.SetPositionAndRotation(
                fixture.transform.position + new Vector3(0f, 1f, -10f),
                Quaternion.LookRotation(Vector3.forward));
            if (wasEnabled)
                controller.enabled = true;
        }

        private static void RunPerceptionAndRestoreCases()
        {
            ActorVisualPerceptionService sight = observer.GetComponent<ActorVisualPerceptionService>();
            Transform targetMarker = Marker(M41SampleSceneNavigationTools.TargetName);
            GameObject barrier = Barrier();

            ResetPerceptionPair(observer, target);
            barrier.SetActive(false);
            Physics.SyncTransforms();
            ActorVisualPerceptionResult clear = sight.Evaluate(target);
            Require(clear.Perceived && clear.Reason == ActorVisualPerceptionReason.Perceived &&
                    clear.ObserverId == observer.ActorInstanceId && clear.TargetId == target.ActorInstanceId &&
                    clear.Distance > 0f && clear.HorizontalAngle <= sight.HorizontalFovDegrees * 0.5f &&
                    clear.HasWorldTime,
                "Clear LOS did not produce an explainable Perceived result.");

            Place(target, observer.transform.position + Vector3.right * (sight.VisualRange + 2f), Quaternion.identity);
            Physics.SyncTransforms();
            ActorVisualPerceptionResult far = sight.Evaluate(target);
            Require(!far.Perceived && far.Reason == ActorVisualPerceptionReason.OutOfRange,
                "Out-of-range target returned " + far.Reason + ".");

            Place(target, observer.transform.position - Vector3.right * 3f, Quaternion.identity);
            Physics.SyncTransforms();
            ActorVisualPerceptionResult behind = sight.Evaluate(target);
            Require(!behind.Perceived && behind.Reason == ActorVisualPerceptionReason.OutsideFov,
                "Outside-FOV target returned " + behind.Reason + ".");

            Place(target, targetMarker.position, Quaternion.identity);
            barrier.SetActive(true);
            Physics.SyncTransforms();
            ActorVisualPerceptionResult blocked = sight.Evaluate(target);
            Require(!blocked.Perceived && blocked.Reason == ActorVisualPerceptionReason.Occluded &&
                    blocked.Blocker != null && blocked.Blocker.gameObject == barrier,
                "Opaque barrier did not report the exact Occluded blocker.");

            barrier.SetActive(false);
            Physics.SyncTransforms();
            ActorVisualPerceptionResult restored = sight.Evaluate(target);
            Require(restored.Perceived && restored.Reason == ActorVisualPerceptionReason.Perceived,
                "Removing opaque geometry did not restore perception.");
            ActorVisualPerceptionResult self = sight.Evaluate(observer);
            Require(!self.Perceived && self.Reason == ActorVisualPerceptionReason.Self,
                "Self-perception was not rejected explicitly.");
            ActorVisualPerceptionResult deadObserver = navigator.GetComponent<ActorVisualPerceptionService>().Evaluate(target);
            Require(!deadObserver.Perceived && deadObserver.Reason == ActorVisualPerceptionReason.ObserverDead,
                "Dead observer remained active.");

            Collider rootCollider = target.GetComponent<Collider>();
            rootCollider.enabled = false;
            var child = new GameObject("M41 Target Child Collider");
            child.transform.SetParent(target.transform, false);
            child.AddComponent<SphereCollider>().radius = 0.45f;
            Physics.SyncTransforms();
            ActorVisualPerceptionResult childHit = sight.Evaluate(target);
            Require(childHit.Perceived && childHit.Reason == ActorVisualPerceptionReason.Perceived,
                "Child collider did not resolve to the target ActorRuntimeIdentity.");
            UnityEngine.Object.Destroy(child);
            rootCollider.enabled = true;

            RunRepeatedPerceptionAndRegistryCases(sight);
            RunLineOfSightSaturationCase(sight);

            target.GetComponent<ActorHealthComponent>().Kill();
            ActorVisualPerceptionResult deadTarget = sight.Evaluate(target);
            Require(!deadTarget.Perceived && deadTarget.Reason == ActorVisualPerceptionReason.TargetDead,
                "Dead target remained perceptible.");

            string restoredId = observer.ActorInstanceId;
            string restoredProfile = observer.ActorProfileId;
            Vector3 restoredPosition = observer.transform.position;
            Quaternion restoredRotation = observer.transform.rotation;
            Require(ActorSpawnService.TryRemoveRuntimeRepresentationForRestore(restoredId, out string removeError),
                "Runtime representation removal failed: " + removeError);
            Require(ActorSpawnService.TrySpawn(restoredProfile, restoredPosition, restoredRotation, restoredId,
                    ActorSpawnInitialization.PersistenceRestore, out ActorRuntimeIdentity restoredActor, out string spawnError),
                "PersistenceRestore spawn failed: " + spawnError);
            Require(restoredActor.ActorInstanceId == restoredId && restoredActor.OriginKind == ActorOriginKind.Runtime &&
                    restoredActor.GetComponent<ActorNavigationController>()?.State == ActorNavigationState.Idle &&
                    restoredActor.GetComponent<ActorVisualPerceptionService>()?.IsConfigured == true,
                "PersistenceRestore did not preserve identity/capabilities or reset navigation to Idle.");
            restoredActor.GetComponent<ActorNavigationController>().ApplyPersistencePose(restoredPosition, restoredRotation);
            Require(restoredActor.GetComponent<ActorNavigationController>().State == ActorNavigationState.Idle,
                "Persistence pose application resumed an ephemeral navigation order.");
        }

        private static void RunRepeatedPerceptionAndRegistryCases(ActorVisualPerceptionService sight)
        {
            VerifyRegistryDiscoverySeam();
            var stressTargets = new List<ActorRuntimeIdentity>(3) { target };
            Vector3 targetPosition = Marker(M41SampleSceneNavigationTools.TargetName).position;
            try
            {
                stressTargets.Add(Spawn(targetPosition + Vector3.forward * 1.5f, Quaternion.identity,
                    "M41 Diagnostic Perception Stress A"));
                stressTargets.Add(Spawn(targetPosition - Vector3.forward * 1.5f, Quaternion.identity,
                    "M41 Diagnostic Perception Stress B"));
                VerifyRegistryDiscoverySeam();

                EvaluateStressTargets(sight, stressTargets);
                int expansionsAfterWarmup = sight.TargetColliderBufferExpansionCount;
                int fallbacksAfterWarmup = sight.LineOfSightFallbackCount;
                for (int index = 0; index < PerceptionStressIterations; index++)
                    EvaluateStressTargets(sight, stressTargets);
                Require(sight.TargetColliderBufferExpansionCount == expansionsAfterWarmup,
                    "Repeated perception expanded the target collider buffer after warm-up.");
                Require(sight.LineOfSightFallbackCount == fallbacksAfterWarmup,
                    "Repeated clear perception unexpectedly saturated the LOS hit buffer.");
            }
            finally
            {
                for (int index = 1; index < stressTargets.Count; index++)
                {
                    ActorRuntimeIdentity stressTarget = stressTargets[index];
                    if (stressTarget != null)
                        ActorSpawnService.TryRemoveRuntimeRepresentationForRestore(stressTarget.ActorInstanceId, out _);
                }
                ResetPerceptionPair(observer, target);
                Physics.SyncTransforms();
            }
            VerifyRegistryDiscoverySeam();
        }

        private static void EvaluateStressTargets(
            ActorVisualPerceptionService sight,
            List<ActorRuntimeIdentity> stressTargets)
        {
            Vector3 observerPosition = Marker(M41SampleSceneNavigationTools.ObserverName).position;
            for (int index = 0; index < stressTargets.Count; index++)
            {
                ActorRuntimeIdentity stressTarget = stressTargets[index];
                Vector3 direction = Vector3.ProjectOnPlane(stressTarget.transform.position - observerPosition, Vector3.up);
                Require(direction.sqrMagnitude > 0.0001f, "Perception stress target overlapped the observer.");
                Place(observer, observerPosition, Quaternion.LookRotation(direction));
                Physics.SyncTransforms();
                ActorVisualPerceptionResult result = sight.Evaluate(stressTarget);
                Require(result.Perceived && result.Reason == ActorVisualPerceptionReason.Perceived,
                    "Repeated perception lost a clear stress target: " + result.Reason + ".");
            }
        }

        private static void RunLineOfSightSaturationCase(ActorVisualPerceptionService sight)
        {
            ResetPerceptionPair(observer, target);
            Physics.SyncTransforms();
            Collider targetCollider = target.GetComponent<Collider>();
            Require(targetCollider != null, "Perception saturation target has no root collider.");
            Vector3 eye = observer.transform.position + Vector3.up * sight.EyeHeight;
            Vector3 direction = targetCollider.bounds.center - eye;
            float distance = direction.magnitude;
            Require(distance > 1f, "Perception saturation fixture is too close to the observer.");
            direction /= distance;

            int fallbackCount = sight.LineOfSightFallbackCount;
            var blockerRoot = new GameObject("M41 Perception Saturation Blockers");
            try
            {
                for (int index = 0; index < SaturationBlockerCount; index++)
                {
                    GameObject blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    blocker.name = "M41 Perception Saturation Blocker " + index;
                    blocker.transform.SetParent(blockerRoot.transform, false);
                    float fraction = (index + 1f) / (SaturationBlockerCount + 1f);
                    blocker.transform.position = eye + direction * Mathf.Lerp(0.35f, distance - 0.35f, fraction);
                    blocker.transform.localScale = Vector3.one * 0.05f;
                }
                Physics.SyncTransforms();
                ActorVisualPerceptionResult saturated = sight.Evaluate(target);
                Require(!saturated.Perceived && saturated.Reason == ActorVisualPerceptionReason.Occluded &&
                        saturated.Blocker != null,
                    "Saturated LOS buffer did not preserve the nearest occlusion result.");
                Require(sight.LineOfSightFallbackCount == fallbackCount + 1,
                    "Saturated LOS buffer did not use the safe allocating fallback.");
            }
            finally
            {
                blockerRoot.SetActive(false);
                UnityEngine.Object.Destroy(blockerRoot);
                Physics.SyncTransforms();
            }
        }

        private static void VerifyRegistryDiscoverySeam()
        {
            int copied = ActorRuntimeRegistry.CopyActiveRepresentationsTo(registryDiscoveryBuffer);
            Require(copied == ActorRuntimeRegistry.ActiveCount,
                "Actor registry discovery seam lost an active representation.");
            int capacityAfterWarmup = registryDiscoveryBuffer.Capacity;
            Require(ActorRuntimeRegistry.CopyActiveRepresentationsTo(registryDiscoveryBuffer) == copied &&
                    registryDiscoveryBuffer.Capacity == capacityAfterWarmup,
                "Actor registry discovery seam did not reuse the caller buffer.");
            for (int index = 0; index < registryDiscoveryBuffer.Count; index++)
            {
                ActorRuntimeIdentity identity = registryDiscoveryBuffer[index];
                Require(identity != null && identity.IsRegistered &&
                        ActorRuntimeRegistry.TryGet(identity.ActorInstanceId, out ActorRuntimeIdentity registered) &&
                        ReferenceEquals(identity, registered),
                    "Actor registry discovery seam returned an invalid representation.");
                for (int compare = 0; compare < index; compare++)
                    Require(registryDiscoveryBuffer[compare].ActorInstanceId != identity.ActorInstanceId,
                        "Actor registry discovery seam duplicated an ActorInstanceId.");
            }
        }

        private static void CompletePlayRun()
        {
            SessionState.SetString(PhaseKey, Finish);
            EditorApplication.ExitPlaymode();
        }

        private static void FinalizeRun()
        {
            string failure = SessionState.GetString(ErrorKey, string.Empty);
            bool success = string.IsNullOrEmpty(failure) && !EditorSceneManager.GetActiveScene().isDirty;
            if (success)
            {
                Debug.Log(
                    "M41.0 Navigation & Perception Diagnostics: PASS" +
                    "\n- runtime spawn/registry and data-driven capabilities" +
                    "\n- reachable movement/Reached and invalid destination/Failed" +
                    "\n- Dead lifecycle stop/reject and player authority isolation" +
                    "\n- range, horizontal FOV, opaque LOS, restored LOS, self and child-collider ownership" +
                    "\n- dead target, repeated multi-actor perception, safe saturated-LOS fallback and registry discovery seam" +
                    "\n- PersistenceRestore identity with ephemeral navigation reset to Idle" +
                    "\n- Navigation and Perception operate without Combat components");
            }
            else
            {
                if (string.IsNullOrEmpty(failure))
                    failure = "SampleScene became dirty during diagnostics.";
                Debug.LogError("M41.0 Navigation & Perception Diagnostics: FAIL\n- " + failure);
            }
            ClearRun();
            if (Application.isBatchMode)
                EditorApplication.Exit(success ? 0 : 1);
        }

        private static ActorRuntimeIdentity Spawn(Vector3 position, Quaternion rotation, string objectName)
        {
            Require(ActorSpawnService.TrySpawn(ProfileId, position, rotation, out ActorRuntimeIdentity identity, out string error),
                objectName + " spawn failed: " + error);
            identity.name = objectName;
            return identity;
        }

        private static void Place(ActorRuntimeIdentity identity, Vector3 position, Quaternion rotation)
        {
            ActorNavigationController navigation = identity.GetComponent<ActorNavigationController>();
            if (navigation != null)
                navigation.ApplyPersistencePose(position, rotation);
            else
                identity.transform.SetPositionAndRotation(position, rotation);
        }

        private static void ResetPerceptionPair(ActorRuntimeIdentity perceptionObserver, ActorRuntimeIdentity perceptionTarget)
        {
            Place(perceptionObserver, Marker(M41SampleSceneNavigationTools.ObserverName).position,
                Quaternion.LookRotation(Vector3.right));
            Place(perceptionTarget, Marker(M41SampleSceneNavigationTools.TargetName).position, Quaternion.identity);
        }

        private static void RequireManualPerceptionContract(
            GameObject barrier,
            ActorVisualPerceptionResult sight)
        {
            bool valid = barrier.activeSelf
                ? !sight.Perceived && sight.Reason == ActorVisualPerceptionReason.Occluded &&
                  sight.Blocker != null && sight.Blocker.gameObject == barrier
                : sight.Perceived && sight.Reason == ActorVisualPerceptionReason.Perceived && sight.Blocker == null;
            Require(valid,
                $"Manual perception fixture mismatch. BarrierActive={barrier.activeSelf}, " +
                $"Perceived={sight.Perceived}, Reason={sight.Reason}, " +
                $"Blocker={(sight.Blocker != null ? sight.Blocker.name : "<NONE>")}.");
        }

        private static Transform Marker(string markerName)
        {
            Transform marker = M41SampleSceneNavigationTools.FindMarker(markerName);
            Require(marker != null, "M41.0 fixture marker is missing: " + markerName);
            return marker;
        }

        private static GameObject Barrier()
        {
            GameObject barrier = M41SampleSceneNavigationTools.FindBarrier();
            Require(barrier != null && barrier.GetComponent<Collider>() != null,
                "M41.0 fixture barrier/collider is missing.");
            return barrier;
        }

        private static bool HasCombatComponent(ActorRuntimeIdentity identity)
        {
            return identity.GetComponents<Component>().Any(component =>
                component != null && component.GetType().Namespace == "OldScars.Core.Combat");
        }

        private static void RemoveManualActors()
        {
            foreach (ActorRuntimeIdentity identity in ActorRuntimeRegistry.ActiveRepresentations
                         .Where(value => value != null && value.OriginKind == ActorOriginKind.Runtime &&
                                         value.name.StartsWith("M41 Manual ", StringComparison.Ordinal)).ToArray())
            {
                ActorSpawnService.TryRemoveRuntimeRepresentationForRestore(identity.ActorInstanceId, out _);
            }
        }

        private static bool Approximately(float left, float right) => Mathf.Abs(left - right) <= 0.0001f;

        private static void Require(bool condition, string failure)
        {
            if (!condition)
                throw new InvalidOperationException(failure);
        }

        private static void ClearRun()
        {
            SessionState.EraseString(PhaseKey);
            SessionState.EraseString(ErrorKey);
            navigator = null;
            observer = null;
            target = null;
            navigatorController = null;
            registryDiscoveryBuffer.Clear();
            stage = 0;
            stableFrame = 0;
            deadline = 0d;
        }
    }
}
