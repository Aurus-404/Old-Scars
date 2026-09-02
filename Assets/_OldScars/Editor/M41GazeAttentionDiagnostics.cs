using System;
using System.Collections.Generic;
using OldScars.Core;
using OldScars.Core.Actors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OldScars.Editor
{
    [InitializeOnLoad]
    public static class M41GazeAttentionDiagnostics
    {
        private const string PhaseKey = "OldScars.M41.GazeAttention.Phase";
        private const string ErrorKey = "OldScars.M41.GazeAttention.Error";
        private const string Enter = "enter";
        private const string Running = "running";
        private const string Finish = "finish";
        private const string ObserverProfile = "core:debug_encounter_fight_01";
        private const string TargetProfile = "core:debug_navigation_npc_01";
        private const string IncapacitatedWoundA = "wound_f6666666666666666666666666666666";
        private const string IncapacitatedWoundB = "wound_f7777777777777777777777777777777";
        private const long GazeSeed = 41303003L;
        private const long AlternateSeed = 41303004L;

        private static readonly List<Vector3> ambientDirections = new List<Vector3>(4);
        private static ActorRuntimeIdentity observer;
        private static ActorRuntimeIdentity target;
        private static ActorGazeController gaze;
        private static ActorBehaviorController behavior;
        private static ActorVisualPerceptionService perception;
        private static Vector3 initialPosition;
        private static Quaternion initialBodyRotation;
        private static Vector3 initialGaze;
        private static int lastAmbientDecisionCount;
        private static float maximumAmbientBodyYaw;
        private static float maximumAmbientAngularStep;
        private static float maximumAmbientGazeChange;
        private static float candidateInitialError;
        private static float candidateFinalError;
        private static float encounterInitialError;
        private static float encounterFinalError;
        private static Vector3 lostKnownPosition;
        private static Vector3 lostDesiredDirection;
        private static Vector3 inactiveDirection;
        private static int inactiveRevision;
        private static double deadline;
        private static double stageStartedAt;
        private static int stage;
        private static float sameSeedYaw;
        private static float alternateSeedYaw;

        static M41GazeAttentionDiagnostics()
        {
            EditorApplication.update -= Continue;
            EditorApplication.update += Continue;
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("M41 Gaze/Attention diagnostics require idle compiled Edit Mode.");
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
            Vector3 observerPosition = Marker(M41SampleSceneNavigationTools.ObserverName).position;
            Vector3 targetPosition = Marker(M41SampleSceneNavigationTools.TargetName).position;
            Quaternion bodyFacing = Face(targetPosition - observerPosition);
            observer = Spawn(ObserverProfile, observerPosition, bodyFacing, "M41.3 Diagnostic Gaze Observer");
            target = Spawn(TargetProfile, targetPosition, Quaternion.identity, "M41.3 Diagnostic Gaze Target");
            gaze = observer.GetComponent<ActorGazeController>();
            behavior = observer.GetComponent<ActorBehaviorController>();
            perception = observer.GetComponent<ActorVisualPerceptionService>();
            Require(gaze?.IsConfigured == true && behavior != null && perception?.IsConfigured == true,
                "Observer lacks configured Gaze, Behavior or Visual Perception.");
            observer.GetComponent<ActorNavigationController>().Stop();
            target.GetComponent<ActorNavigationController>().Stop();
            gaze.Configure(GazeSeed);

            Quaternion sameA = SandboxNpcController.DeriveInitialFacing(GazeSeed);
            Quaternion sameB = SandboxNpcController.DeriveInitialFacing(GazeSeed);
            Quaternion alternate = SandboxNpcController.DeriveInitialFacing(AlternateSeed);
            sameSeedYaw = sameA.eulerAngles.y;
            alternateSeedYaw = alternate.eulerAngles.y;
            Require(Quaternion.Angle(sameA, sameB) < 0.001f,
                "Identical spawn seed did not reproduce initial facing.");
            Require(Quaternion.Angle(sameA, alternate) > 1f,
                "Distinct spawn seeds did not vary initial facing.");

            ambientDirections.Clear();
            lastAmbientDecisionCount = 0;
            maximumAmbientBodyYaw = 0f;
            maximumAmbientAngularStep = 0f;
            maximumAmbientGazeChange = 0f;
            initialPosition = observer.transform.position;
            initialBodyRotation = observer.transform.rotation;
            initialGaze = gaze.CurrentGazeDirection;
            SetStage(1, 8d);
        }

        private static void TickRun()
        {
            if (Time.timeAsDouble > deadline)
                throw new InvalidOperationException("M41 Gaze/Attention diagnostic stage timed out: " + stage);
            switch (stage)
            {
                case 1: ObserveIndependentAmbientGaze(); break;
                case 2: ObserveCandidateConvergence(); break;
                case 3: ObserveEncounterConvergence(); break;
                case 4: ObserveLostContact(); break;
                case 5: WaitForInactive(); break;
                case 6: VerifyInactiveStability(); break;
            }
        }

        private static void ObserveIndependentAmbientGaze()
        {
            Require(Vector3.Distance(observer.transform.position, initialPosition) < 0.01f,
                "Ambient gaze moved the stationary actor root.");
            Require(Quaternion.Angle(observer.transform.rotation, initialBodyRotation) < 0.01f,
                "Ambient gaze rotated the actor body/root.");
            Require(gaze.Mode == ActorAttentionMode.Ambient,
                "Stationary functional actor did not retain Ambient attention.");
            maximumAmbientAngularStep = Mathf.Max(maximumAmbientAngularStep, gaze.LastAngularStepDegrees);
            maximumAmbientBodyYaw = Mathf.Max(maximumAmbientBodyYaw, Mathf.Abs(gaze.CurrentBodyRelativeYaw));
            maximumAmbientGazeChange = Mathf.Max(
                maximumAmbientGazeChange, Vector3.Angle(initialGaze, gaze.CurrentGazeDirection));
            Require(gaze.LastAngularStepDegrees <= gaze.AngularSpeed * Time.deltaTime + 0.15f,
                "Ambient gaze exceeded its per-frame angular speed bound.");
            Require(Mathf.Abs(gaze.CurrentBodyRelativeYaw) <= gaze.MaximumBodyRelativeYaw + 0.1f,
                "Ambient gaze exceeded its body-relative human yaw bound.");

            if (gaze.AmbientDecisionCount != lastAmbientDecisionCount)
            {
                lastAmbientDecisionCount = gaze.AmbientDecisionCount;
                if (ambientDirections.Count == 0 ||
                    Vector3.Angle(ambientDirections[ambientDirections.Count - 1], gaze.DesiredGazeDirection) > 5f)
                    ambientDirections.Add(gaze.DesiredGazeDirection);
            }
            if (ambientDirections.Count < 2 || maximumAmbientGazeChange < 10f)
                return;

            Vector3 bodyForward = FlatForward(observer.transform);
            Place(target, observer.transform.position - bodyForward * 3f, Quaternion.identity);
            Physics.SyncTransforms();
            ActorVisualPerceptionResult rejected = perception.Evaluate(target);
            Require(!rejected.Perceived && rejected.Reason == ActorVisualPerceptionReason.OutsideFov,
                "Registry-only candidate fixture was not outside production body-forward FOV. " +
                $"Reason={rejected.Reason}; Angle={rejected.HorizontalAngle:0.###}; " +
                $"Observer={observer.transform.position}; Body={bodyForward}; Target={target.transform.position}.");
            int revisionBeforeRejected = gaze.AttentionRevision;
            Vector3 desiredBeforeRejected = gaze.DesiredGazeDirection;
            Require(!gaze.TryAttendCandidate(rejected) && gaze.AttentionRevision == revisionBeforeRejected &&
                    Vector3.Angle(gaze.DesiredGazeDirection, desiredBeforeRejected) < 0.001f,
                "Unperceived registry candidate changed gaze attention.");

            float candidateYaw = gaze.CurrentBodyRelativeYaw >= 0f ? -50f : 50f;
            Vector3 candidateDirection = Quaternion.AngleAxis(candidateYaw, Vector3.up) * bodyForward;
            Place(target, observer.transform.position + candidateDirection * 5f, Quaternion.identity);
            Physics.SyncTransforms();
            ActorVisualPerceptionResult perceived = perception.Evaluate(target);
            Require(perceived.Perceived, "Candidate fixture did not produce a legitimate perceived observation.");
            ActorGazeController foreignGaze = target.GetComponent<ActorGazeController>();
            ActorBehaviorController foreignBehavior = target.GetComponent<ActorBehaviorController>();
            int foreignRevision = foreignGaze.AttentionRevision;
            Vector3 foreignDesired = foreignGaze.DesiredGazeDirection;
            Require(!foreignGaze.TryAttendCandidate(perceived) &&
                    foreignGaze.AttentionRevision == foreignRevision &&
                    Vector3.Angle(foreignGaze.DesiredGazeDirection, foreignDesired) < 0.001f,
                "NPC B accepted NPC A's legitimate perception result as Candidate attention.");
            Require(foreignBehavior.EnterEncounter("Cross-observer gaze rejection diagnostic"),
                "Foreign actor could not enter diagnostic Encounter ownership.");
            Require(!foreignGaze.TryAttendEncounter(perceived) &&
                    foreignGaze.AttentionRevision == foreignRevision &&
                    Vector3.Angle(foreignGaze.DesiredGazeDirection, foreignDesired) < 0.001f,
                "NPC B accepted NPC A's legitimate perception result as Encounter attention.");
            foreignBehavior.ExitEncounter("Cross-observer gaze rejection complete");
            Require(gaze.TryAttendCandidate(perceived) && gaze.Mode == ActorAttentionMode.Candidate,
                "Perceived candidate did not claim Candidate attention.");
            candidateInitialError = gaze.AngularError;
            Require(candidateInitialError > 5f,
                "Candidate attention snapped or lacked a useful convergence angle.");
            SetStage(2, 1d);
        }

        private static void ObserveCandidateConvergence()
        {
            Require(gaze.Mode == ActorAttentionMode.Candidate,
                "Candidate attention expired before its bounded convergence observation.");
            if (Time.timeAsDouble - stageStartedAt < 0.3d)
                return;
            Require(gaze.AngularError < candidateInitialError - 8f,
                "Candidate gaze did not converge progressively toward the perceived observation.");
            candidateFinalError = gaze.AngularError;

            Require(behavior.EnterEncounter("Gaze diagnostic Encounter"),
                "Behavior rejected diagnostic Encounter ownership.");
            Vector3 bodyForward = FlatForward(observer.transform);
            float encounterYaw = gaze.CurrentBodyRelativeYaw >= 0f ? -55f : 55f;
            Vector3 encounterDirection = Quaternion.AngleAxis(encounterYaw, Vector3.up) * bodyForward;
            Place(target, observer.transform.position + encounterDirection * 5f, Quaternion.identity);
            Physics.SyncTransforms();
            ActorVisualPerceptionResult perceived = perception.Evaluate(target);
            Require(perceived.Perceived && gaze.TryAttendEncounter(perceived) &&
                    gaze.Mode == ActorAttentionMode.Encounter,
                "Encounter gaze did not accept a fresh legitimate observation.");
            encounterInitialError = gaze.AngularError;
            Require(encounterInitialError > 5f, "Encounter gaze snapped or lacked a useful convergence angle.");
            lostKnownPosition = perceived.ObservedPosition;
            SetStage(3, 1d);
        }

        private static void ObserveEncounterConvergence()
        {
            Require(gaze.Mode == ActorAttentionMode.Encounter,
                "Encounter attention changed source unexpectedly.");
            if (Time.timeAsDouble - stageStartedAt < 0.3d)
                return;
            Require(gaze.AngularError < encounterInitialError - 8f,
                "Encounter gaze did not converge progressively toward observed position.");
            encounterFinalError = gaze.AngularError;
            Require(gaze.TryAttendLostContact(lostKnownPosition) &&
                    gaze.Mode == ActorAttentionMode.LostContact,
                "LostContact gaze rejected legitimate LastKnownPosition.");
            lostDesiredDirection = gaze.DesiredGazeDirection;
            Place(target, observer.transform.position - FlatForward(observer.transform) * 6f, Quaternion.identity);
            Physics.SyncTransforms();
            SetStage(4, 1d);
        }

        private static void ObserveLostContact()
        {
            if (Time.timeAsDouble - stageStartedAt < 0.3d)
                return;
            Vector3 hiddenActualDirection = Vector3.ProjectOnPlane(
                target.GetComponent<Collider>().bounds.center - observer.transform.position, Vector3.up).normalized;
            Require(gaze.Mode == ActorAttentionMode.LostContact &&
                    Vector3.Angle(gaze.DesiredGazeDirection, lostDesiredDirection) < 0.001f &&
                    Vector3.Angle(gaze.DesiredGazeDirection, hiddenActualDirection) > 20f,
                "LostContact gaze followed hidden target position instead of retained LastKnownPosition.");

            ActorMedicalStateComponent medical = observer.GetComponent<ActorMedicalStateComponent>();
            Require(medical.TryApplyWound(IncapacitatedWoundA, BodyRegion.Head, WoundType.Blunt,
                        0.4f, 0f, 0.3f, out string failure) &&
                    medical.TryApplyWound(IncapacitatedWoundB, BodyRegion.Head, WoundType.Blunt,
                        0.5f, 0f, 0.05f, out failure),
                "Could not create diagnostic incapacity: " + failure);
            Require(observer.GetComponent<ActorConditionComponent>().IsUnconscious,
                "Gaze incapacity fixture did not become Unconscious.");
            SetStage(5, 2d);
        }

        private static void WaitForInactive()
        {
            if (gaze.Mode != ActorAttentionMode.Inactive || behavior.Owner != ActorBehaviorOwner.Inactive)
                return;
            inactiveDirection = gaze.CurrentGazeDirection;
            inactiveRevision = gaze.AttentionRevision;
            SetStage(6, 1d);
        }

        private static void VerifyInactiveStability()
        {
            if (Time.timeAsDouble - stageStartedAt < 0.5d)
                return;
            Require(gaze.Mode == ActorAttentionMode.Inactive && behavior.Owner == ActorBehaviorOwner.Inactive &&
                    gaze.AttentionRevision == inactiveRevision &&
                    Vector3.Angle(gaze.CurrentGazeDirection, inactiveDirection) < 0.001f &&
                    gaze.LastAngularStepDegrees == 0f,
                "Inactive gaze changed direction/source or disturbed Behavior ownership.");
            Debug.Log(
                "M41 Gaze & Attention Diagnostics: PASS\n" +
                $"- Independent Ambient: directions={ambientDirections.Count}; gaze change={maximumAmbientGazeChange:0.###}deg; " +
                $"max yaw={maximumAmbientBodyYaw:0.###}deg; max frame step={maximumAmbientAngularStep:0.###}deg; " +
                $"configured speed={gaze.AngularSpeed:0.###}deg/s\n" +
                $"- Initial facing determinism: same seed yaw={sameSeedYaw:0.###}deg; alternate seed yaw={alternateSeedYaw:0.###}deg\n" +
                $"- Candidate convergence: {candidateInitialError:0.###} -> {candidateFinalError:0.###}deg; " +
                $"Encounter convergence: {encounterInitialError:0.###} -> {encounterFinalError:0.###}deg\n" +
                "- Candidate/Encounter rejected cross-observer results; Encounter used own Perceived observation\n" +
                "- LostContact retained LastKnownPosition; Inactive remained stable\n" +
                "- Production ActorVisualPerceptionService remained body-forward");
            CompleteRun();
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
            navigation.Stop();
            navigation.ApplyPersistencePose(position, rotation);
        }

        private static Transform Marker(string name)
        {
            Transform marker = M41SampleSceneNavigationTools.FindMarker(name);
            Require(marker != null, "M41.0 fixture marker is missing: " + name);
            return marker;
        }

        private static Quaternion Face(Vector3 direction)
        {
            Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up);
            return flat.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(flat) : Quaternion.identity;
        }

        private static Vector3 FlatForward(Transform value)
        {
            Vector3 forward = Vector3.ProjectOnPlane(value.forward, Vector3.up);
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
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
                Debug.LogError("M41 Gaze & Attention Diagnostics: FAIL\n- " +
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
            gaze = null;
            behavior = null;
            perception = null;
            stage = 0;
            ambientDirections.Clear();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
