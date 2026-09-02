using System;
using OldScars.Core;
using OldScars.Core.Actors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace OldScars.Editor
{
    [InitializeOnLoad]
    public static class M41GazePerceptionIntegrationDiagnostics
    {
        private const string PhaseKey = "OldScars.M41.GazePerceptionIntegration.Phase";
        private const string ErrorKey = "OldScars.M41.GazePerceptionIntegration.Error";
        private const string Enter = "enter";
        private const string Running = "running";
        private const string Finish = "finish";
        private const string ObserverProfile = "core:debug_navigation_npc_01";
        private const string TargetProfile = "core:debug_navigation_npc_01";
        private const string FallbackObserverProfile = "core:debug_npc_capsule_01";
        private const long AmbientSeed = 41305001L;
        private const float TargetRadius = 6f;

        private static ActorRuntimeIdentity observer;
        private static ActorRuntimeIdentity target;
        private static ActorRuntimeIdentity fallbackObserver;
        private static ActorVisualPerceptionService perception;
        private static ActorGazeController gaze;
        private static ActorBehaviorController behavior;
        private static GameObject barrier;
        private static int stage;
        private static double stageStartedAt;
        private static double deadline;
        private static float halfFov;
        private static float desiredGateBodyAngle;
        private static float desiredGateCurrentAngle;
        private static float desiredGateDesiredAngle;
        private static float currentArrivalGazeAngle;
        private static float bodyOnlyBodyAngle;
        private static float bodyOnlyGazeAngle;
        private static float ambientInitialBodyAngle;
        private static float ambientInitialGazeAngle;
        private static float ambientDiscoveryBodyAngle;
        private static float ambientDiscoveryGazeAngle;
        private static float ambientTargetYaw;
        private static float lateralBodyAngle;
        private static float lateralGazeAngle;
        private static float lateralSampleDelta;
        private static Vector3 lateralVelocity;
        private static float lateralSpeed;
        private static float maximumAngularStep;
        private static float humanLimitBodyAngle;
        private static float humanLimitGazeAngle;
        private static ActorVisualPerceptionReason humanLimitReason;
        private static ActorVisualPerceptionReason occludedReason;
        private static string occlusionBlocker;
        private static ActorVisualPerceptionReason reacquiredReason;
        private static ActorVisualPerceptionReason fallbackReason;

        static M41GazePerceptionIntegrationDiagnostics()
        {
            EditorApplication.update -= Continue;
            EditorApplication.update += Continue;
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("M41 Gaze/Perception integration diagnostics require idle compiled Edit Mode.");
            ClearRun();
            SessionState.SetString(PhaseKey, Enter);
            EditorSceneManager.OpenScene(M41SampleSceneNavigationTools.ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void Continue()
        {
            string phase = SessionState.GetString(PhaseKey, string.Empty);
            if (string.IsNullOrEmpty(phase))
                return;
            try
            {
                if (phase == Enter && EditorApplication.isPlaying && Time.frameCount >= 5 &&
                    GameDataManager.Instance?.IsReady == true)
                {
                    BeginRun();
                    SessionState.SetString(PhaseKey, Running);
                }
                else if (phase == Running && EditorApplication.isPlaying)
                    TickRun();
                else if (phase == Finish && !EditorApplication.isPlayingOrWillChangePlaymode)
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

        private static void BeginRun()
        {
            Require(GameDataManager.Instance.Report?.ErrorCount == 0, "Game data validation contains errors.");
            Transform observerMarker = Marker(M41SampleSceneNavigationTools.ObserverName);
            Transform targetMarker = Marker(M41SampleSceneNavigationTools.TargetName);
            observer = Spawn(ObserverProfile, observerMarker.position, observerMarker.rotation,
                "M41.5 Diagnostic Gaze Perception Observer");
            target = Spawn(TargetProfile, targetMarker.position, targetMarker.rotation,
                "M41.5 Diagnostic Gaze Perception Target");
            perception = observer.GetComponent<ActorVisualPerceptionService>();
            gaze = observer.GetComponent<ActorGazeController>();
            behavior = observer.GetComponent<ActorBehaviorController>();
            ActorThreatAcquisitionController acquisition = observer.GetComponent<ActorThreatAcquisitionController>();
            HumanEncounterAIController encounter = observer.GetComponent<HumanEncounterAIController>();
            barrier = M41SampleSceneNavigationTools.FindBarrier();
            Require(perception?.IsConfigured == true && gaze?.IsConfigured == true && behavior != null && barrier != null,
                "Integration fixture lacks configured Perception, Gaze, Behavior or barrier.");
            if (acquisition != null)
                acquisition.enabled = false;
            if (encounter != null)
                encounter.enabled = false;
            observer.GetComponent<ActorNavigationController>().Stop();
            target.GetComponent<ActorNavigationController>().Stop();
            observer.GetComponent<NavMeshAgent>().enabled = false;
            target.GetComponent<NavMeshAgent>().enabled = false;
            barrier.SetActive(false);
            halfFov = perception.HorizontalFovDegrees * 0.5f;
            Require(halfFov > 10f && halfFov < 90f,
                "Integration diagnostic requires a limited non-360 FOV profile. HalfFov=" + halfFov);
            gaze.Configure(AmbientSeed);
            Require(perception.IsUsingGazeForward &&
                    Vector3.Angle(perception.CurrentPerceptionForward, gaze.CurrentGazeDirection) < 0.01f,
                "Configured Perception did not resolve CurrentGazeDirection as its single forward.");

            PlaceTargetAtBodyYaw(halfFov - 5f);
            ActorVisualPerceptionResult initial = perception.Evaluate(target);
            Require(initial.Perceived && gaze.TryAttendCandidate(initial),
                "Current-vs-Desired setup did not begin from a legitimate perceived Candidate.");
            PlaceTargetAtBodyYaw(halfFov + 5f);
            ActorVisualPerceptionResult beforeCurrentArrives = perception.Evaluate(target);
            desiredGateBodyAngle = BodyAngleToTarget();
            desiredGateCurrentAngle = GazeAngleToTarget();
            desiredGateDesiredAngle = DesiredAngleToTarget();
            Require(!beforeCurrentArrives.Perceived &&
                    beforeCurrentArrives.Reason == ActorVisualPerceptionReason.OutsideFov &&
                    desiredGateBodyAngle > halfFov && desiredGateCurrentAngle > halfFov &&
                    desiredGateDesiredAngle < halfFov,
                "Perception used Desired gaze or body+gaze before Current gaze arrived. " +
                $"Body={desiredGateBodyAngle:0.###}; Current={desiredGateCurrentAngle:0.###}; " +
                $"Desired={desiredGateDesiredAngle:0.###}; HalfFov={halfFov:0.###}; Reason={beforeCurrentArrives.Reason}.");
            SetStage(1, 1d);
        }

        private static void TickRun()
        {
            if (Time.timeAsDouble > deadline)
                throw new InvalidOperationException("M41 Gaze/Perception integration stage timed out: " + stage);
            maximumAngularStep = Mathf.Max(maximumAngularStep, gaze != null ? gaze.LastAngularStepDegrees : 0f);
            if (gaze != null)
                Require(gaze.LastAngularStepDegrees <= gaze.AngularSpeed * Time.deltaTime + 0.15f,
                    "Integrated production FOV exceeded the configured Current-gaze angular speed.");
            switch (stage)
            {
                case 1: WaitForCurrentGazePerception(); break;
                case 2: VerifyBodyIsNotAlternativeForward(); break;
                case 3: PrepareAmbientDiscovery(); break;
                case 4: WaitForAmbientDiscovery(); break;
                case 5: CreateLateralTrackingSample(); break;
                case 6: VerifyIntegratedLateralTracking(); break;
                case 7: VerifyOcclusionAndLostContact(); break;
                case 8: VerifyFallback(); break;
            }
        }

        private static void WaitForCurrentGazePerception()
        {
            ActorVisualPerceptionResult result = perception.Evaluate(target);
            if (!result.Perceived)
                return;
            currentArrivalGazeAngle = GazeAngleToTarget();
            Require(result.Reason == ActorVisualPerceptionReason.Perceived &&
                    BodyAngleToTarget() > halfFov && currentArrivalGazeAngle <= halfFov,
                "Perception did not become Perceived exactly when Current gaze entered the cone.");
            SetStage(2, 1d);
        }

        private static void VerifyBodyIsNotAlternativeForward()
        {
            if (Mathf.Abs(gaze.CurrentBodyRelativeYaw) < 20f)
                return;
            float oppositeYaw = gaze.CurrentBodyRelativeYaw >= 0f ? -(halfFov - 5f) : halfFov - 5f;
            PlaceTargetAtBodyYaw(oppositeYaw);
            ActorVisualPerceptionResult result = perception.Evaluate(target);
            bodyOnlyBodyAngle = BodyAngleToTarget();
            bodyOnlyGazeAngle = GazeAngleToTarget();
            Require(!result.Perceived && result.Reason == ActorVisualPerceptionReason.OutsideFov &&
                    bodyOnlyBodyAngle < halfFov && bodyOnlyGazeAngle > halfFov,
                "Perception combined body and gaze FOV instead of using one gaze-centered forward. " +
                $"Body={bodyOnlyBodyAngle:0.###}; Gaze={bodyOnlyGazeAngle:0.###}; HalfFov={halfFov:0.###}; " +
                $"Reason={result.Reason}.");
            Place(target, observer.transform.position + BodyForward() * (perception.VisualRange + 3f), Quaternion.identity);
            gaze.Configure(AmbientSeed);
            Require(gaze.TrackedTargetId == null && gaze.Mode == ActorAttentionMode.Ambient,
                "Ambient discovery setup retained Candidate knowledge.");
            SetStage(3, 2d);
        }

        private static void PrepareAmbientDiscovery()
        {
            ActorVisualPerceptionResult outOfRange = perception.Evaluate(target);
            Require(!outOfRange.Perceived && gaze.TrackedTargetId == null,
                "An unobserved out-of-range actor oriented Candidate gaze. " +
                $"Reason={outOfRange.Reason}; Mode={gaze.Mode}; Tracked={gaze.TrackedTargetId ?? "<none>"}.");
            if (gaze.AmbientDecisionCount < 1)
                return;
            float desiredYaw = Vector3.SignedAngle(BodyForward(), gaze.DesiredGazeDirection, Vector3.up);
            float sign = Mathf.Sign(desiredYaw);
            Require(sign != 0f && Mathf.Abs(desiredYaw) >= 10f,
                "Ambient scan did not choose a useful deterministic direction.");
            ambientTargetYaw = sign * (halfFov + 10f);
            PlaceTargetAtBodyYaw(ambientTargetYaw);
            ActorVisualPerceptionResult initiallyOutside = perception.Evaluate(target);
            ambientInitialBodyAngle = BodyAngleToTarget();
            ambientInitialGazeAngle = GazeAngleToTarget();
            Require(!initiallyOutside.Perceived && initiallyOutside.Reason == ActorVisualPerceptionReason.OutsideFov &&
                    gaze.TrackedTargetId == null && gaze.Mode == ActorAttentionMode.Ambient,
                "Unobserved target bypassed Ambient discovery or entered attention before Perceived. " +
                $"Body={ambientInitialBodyAngle:0.###}; Gaze={ambientInitialGazeAngle:0.###}; " +
                $"HalfFov={halfFov:0.###}; Reason={initiallyOutside.Reason}.");
            SetStage(4, 2d);
        }

        private static void WaitForAmbientDiscovery()
        {
            ActorVisualPerceptionResult result = perception.Evaluate(target);
            if (!result.Perceived)
            {
                Require(gaze.TrackedTargetId == null && gaze.Mode == ActorAttentionMode.Ambient,
                    "Candidate attention changed before production Perception discovered the target.");
                return;
            }
            ambientDiscoveryBodyAngle = BodyAngleToTarget();
            ambientDiscoveryGazeAngle = GazeAngleToTarget();
            Require(ambientDiscoveryBodyAngle > halfFov && ambientDiscoveryGazeAngle <= halfFov &&
                    gaze.TryAttendCandidate(result) && gaze.TrackedTargetId == target.ActorInstanceId,
                "Ambient scan did not produce the legitimate Perception -> Candidate chain.");
            SetStage(5, 1d);
        }

        private static void CreateLateralTrackingSample()
        {
            if (Time.timeAsDouble - stageStartedAt < 0.2d)
                return;
            float sign = Mathf.Sign(ambientTargetYaw);
            PlaceTargetAtBodyYaw(ambientTargetYaw + sign * 8f);
            ActorVisualPerceptionResult moved = perception.Evaluate(target);
            Require(moved.Perceived && gaze.TryAttendCandidate(moved) && gaze.HasObservedVelocity,
                "Lateral tracking did not receive a second legitimate production Perception sample. " +
                $"Body={BodyAngleToTarget():0.###}; Gaze={GazeAngleToTarget():0.###}; Reason={moved.Reason}.");
            lateralSampleDelta = gaze.LastObservationDeltaSeconds;
            lateralVelocity = gaze.EstimatedObservedVelocity;
            lateralSpeed = lateralVelocity.magnitude;
            SetStage(6, 1d);
        }

        private static void VerifyIntegratedLateralTracking()
        {
            if (Time.timeAsDouble - stageStartedAt < 0.15d)
                return;
            float sign = Mathf.Sign(ambientTargetYaw);
            PlaceTargetAtBodyYaw(ambientTargetYaw + sign * 12f);
            ActorVisualPerceptionResult tracked = perception.Evaluate(target);
            lateralBodyAngle = BodyAngleToTarget();
            lateralGazeAngle = GazeAngleToTarget();
            Require(tracked.Perceived && lateralBodyAngle > halfFov && lateralGazeAngle <= halfFov &&
                    gaze.HasObservedVelocity && gaze.CurrentPredictionHorizonSeconds <= gaze.MaximumPredictionHorizon + 0.001f,
                "Production Perception did not follow gaze-centered lateral tracking beyond the old body cone. " +
                $"Body={lateralBodyAngle:0.###}; Gaze={lateralGazeAngle:0.###}; HalfFov={halfFov:0.###}; " +
                $"Velocity={lateralVelocity}; Reason={tracked.Reason}.");

            float extremeYaw = -sign * (halfFov - 5f);
            PlaceTargetAtBodyYaw(extremeYaw);
            ActorVisualPerceptionResult extreme = perception.Evaluate(target);
            humanLimitBodyAngle = BodyAngleToTarget();
            humanLimitGazeAngle = GazeAngleToTarget();
            humanLimitReason = extreme.Reason;
            Require(!extreme.Perceived && extreme.Reason == ActorVisualPerceptionReason.OutsideFov &&
                    humanLimitGazeAngle > halfFov,
                "Angularly extreme target remained infallibly perceived despite bounded Current gaze.");
            SetStage(7, 1d);
        }

        private static void VerifyOcclusionAndLostContact()
        {
            Transform observerMarker = Marker(M41SampleSceneNavigationTools.ObserverName);
            Transform targetMarker = Marker(M41SampleSceneNavigationTools.TargetName);
            Place(observer, observerMarker.position, Face(targetMarker.position - observerMarker.position));
            Place(target, targetMarker.position, targetMarker.rotation);
            gaze.Configure(AmbientSeed);
            barrier.SetActive(false);
            Physics.SyncTransforms();
            ActorVisualPerceptionResult clear = perception.Evaluate(target);
            Require(clear.Perceived && behavior.EnterEncounter("Gaze/Perception occlusion diagnostic") &&
                    gaze.TryAttendEncounter(clear),
                "Occlusion setup did not begin from clear gaze-centered Perception.");
            barrier.SetActive(true);
            Physics.SyncTransforms();
            ActorVisualPerceptionResult blocked = perception.Evaluate(target);
            occludedReason = blocked.Reason;
            occlusionBlocker = blocked.Blocker != null ? blocked.Blocker.name : null;
            Require(!blocked.Perceived && blocked.Reason == ActorVisualPerceptionReason.Occluded &&
                    blocked.Blocker != null && blocked.Blocker.gameObject == barrier &&
                    gaze.TryAttendLostContact(target.ActorInstanceId, clear.ObservedPosition),
                "Physical barrier did not remain authoritative while Gaze faced the target.");
            ActorVisualPerceptionResult stillBlocked = perception.Evaluate(target);
            Require(!stillBlocked.Perceived && stillBlocked.Reason == ActorVisualPerceptionReason.Occluded,
                "LostContact prediction bypassed physical LOS.");
            barrier.SetActive(false);
            Physics.SyncTransforms();
            ActorVisualPerceptionResult reacquired = perception.Evaluate(target);
            reacquiredReason = reacquired.Reason;
            Require(reacquired.Perceived && gaze.TryAttendEncounter(reacquired),
                "Removing the barrier did not allow gaze-centered reacquisition.");

            Place(observer, observer.transform.position - BodyForward() * 10f, observer.transform.rotation);
            fallbackObserver = Spawn(FallbackObserverProfile, observerMarker.position,
                Face(targetMarker.position - observerMarker.position), "M41.5 Diagnostic Body-Forward Fallback Observer");
            SetStage(8, 1d);
        }

        private static void VerifyFallback()
        {
            ActorVisualPerceptionService fallback = fallbackObserver.GetComponent<ActorVisualPerceptionService>();
            Require(fallback?.IsConfigured == true && fallbackObserver.GetComponent<ActorGazeController>() == null,
                "Fallback observer unexpectedly owns Gaze or lacks Perception.");
            ActorVisualPerceptionResult result = fallback.Evaluate(target);
            fallbackReason = result.Reason;
            Require(result.Perceived && !fallback.IsUsingGazeForward &&
                    Vector3.Angle(fallback.CurrentPerceptionForward, FlatForward(fallbackObserver.transform)) < 0.01f,
                "Observer without configured Gaze did not preserve body-forward Perception fallback.");
            Debug.Log(
                "M41 Gaze-Centered Production Perception Diagnostics: PASS\n" +
                $"- FOV source: half={halfFov:0.###}deg; Desired gate body/current/desired=" +
                $"{desiredGateBodyAngle:0.###}/{desiredGateCurrentAngle:0.###}/{desiredGateDesiredAngle:0.###}deg -> OutsideFov; " +
                $"Current arrival gaze={currentArrivalGazeAngle:0.###}deg -> Perceived\n" +
                $"- Single forward: body={bodyOnlyBodyAngle:0.###}deg inside; gaze={bodyOnlyGazeAngle:0.###}deg outside -> OutsideFov\n" +
                $"- Ambient discovery: initial body/gaze={ambientInitialBodyAngle:0.###}/{ambientInitialGazeAngle:0.###}deg -> OutsideFov; " +
                $"discovered body/gaze={ambientDiscoveryBodyAngle:0.###}/{ambientDiscoveryGazeAngle:0.###}deg -> Perceived -> Candidate\n" +
                $"- Lateral tracking: dt={lateralSampleDelta:0.###}s; velocity={lateralVelocity}; speed={lateralSpeed:0.###}m/s; " +
                $"body/gaze={lateralBodyAngle:0.###}/{lateralGazeAngle:0.###}deg -> Perceived; max step={maximumAngularStep:0.###}deg\n" +
                $"- Human limit: body/gaze={humanLimitBodyAngle:0.###}/{humanLimitGazeAngle:0.###}deg -> {humanLimitReason}\n" +
                $"- LOS: clear=Perceived; blocked={occludedReason} by {occlusionBlocker ?? "<NONE>"}; reacquired={reacquiredReason}\n" +
                $"- Fallback without Gaze: {fallbackReason}; source=body forward");
            CompleteRun();
        }

        private static void PlaceTargetAtBodyYaw(float yaw)
        {
            Vector3 direction = Quaternion.AngleAxis(yaw, Vector3.up) * BodyForward();
            Place(target, observer.transform.position + direction * TargetRadius, Quaternion.identity);
            Physics.SyncTransforms();
        }

        private static float BodyAngleToTarget() =>
            Vector3.Angle(BodyForward(), FlatDirectionToTarget());

        private static float GazeAngleToTarget() =>
            Vector3.Angle(perception.CurrentPerceptionForward, FlatDirectionToTarget());

        private static float DesiredAngleToTarget() =>
            Vector3.Angle(Vector3.ProjectOnPlane(gaze.DesiredGazeDirection, Vector3.up).normalized, FlatDirectionToTarget());

        private static Vector3 FlatDirectionToTarget()
        {
            Vector3 eye = observer.transform.position + Vector3.up * perception.EyeHeight;
            Vector3 observed = target.GetComponent<Collider>().bounds.center;
            return Vector3.ProjectOnPlane(observed - eye, Vector3.up).normalized;
        }

        private static Vector3 BodyForward() => FlatForward(observer.transform);

        private static Vector3 FlatForward(Transform value)
        {
            Vector3 forward = Vector3.ProjectOnPlane(value.forward, Vector3.up);
            return forward.sqrMagnitude > 0.000001f ? forward.normalized : Vector3.forward;
        }

        private static Quaternion Face(Vector3 direction)
        {
            Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up);
            return flat.sqrMagnitude > 0.000001f ? Quaternion.LookRotation(flat) : Quaternion.identity;
        }

        private static ActorRuntimeIdentity Spawn(string profile, Vector3 position, Quaternion rotation, string name)
        {
            Require(ActorSpawnService.TrySpawn(profile, position, rotation,
                    out ActorRuntimeIdentity identity, out string error), name + " spawn failed: " + error);
            identity.name = name;
            return identity;
        }

        private static void Place(ActorRuntimeIdentity actor, Vector3 position, Quaternion rotation)
        {
            ActorNavigationController navigation = actor.GetComponent<ActorNavigationController>();
            if (navigation != null)
            {
                navigation.Stop();
                navigation.ApplyPersistencePose(position, rotation);
            }
            else
                actor.transform.SetPositionAndRotation(position, rotation);
            Physics.SyncTransforms();
        }

        private static Transform Marker(string name)
        {
            Transform marker = M41SampleSceneNavigationTools.FindMarker(name);
            Require(marker != null, "M41.0 fixture marker is missing: " + name);
            return marker;
        }

        private static void SetStage(int value, double timeoutSeconds)
        {
            stage = value;
            stageStartedAt = Time.timeAsDouble;
            deadline = stageStartedAt + timeoutSeconds;
        }

        private static void CompleteRun()
        {
            SessionState.SetString(PhaseKey, Finish);
            EditorApplication.ExitPlaymode();
        }

        private static void FinalizeRun()
        {
            string failure = SessionState.GetString(ErrorKey, string.Empty);
            bool success = string.IsNullOrEmpty(failure) && !EditorSceneManager.GetActiveScene().isDirty;
            if (!success)
                Debug.LogError("M41 Gaze-Centered Production Perception Diagnostics: FAIL\n- " +
                               (string.IsNullOrEmpty(failure) ? "Diagnostic dirtied SampleScene." : failure));
            ClearRun();
            if (Application.isBatchMode)
                EditorApplication.Exit(success ? 0 : 1);
        }

        private static void ClearRun()
        {
            SessionState.EraseString(PhaseKey);
            SessionState.EraseString(ErrorKey);
            observer = null;
            target = null;
            fallbackObserver = null;
            perception = null;
            gaze = null;
            behavior = null;
            barrier = null;
            stage = 0;
            maximumAngularStep = 0f;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
