using System;
using System.Linq;
using OldScars.Core;
using OldScars.Core.Actors;
using OldScars.Core.Combat;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Interactions;
using OldScars.Core.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace OldScars.Editor
{
    [InitializeOnLoad]
    public static class TimedBandagingDiagnostics
    {
        private const string PhaseKey = "OldScars.TimedBandaging.Phase";
        private const string ErrorKey = "OldScars.TimedBandaging.Error";
        private const string Enter = "enter";
        private const string Running = "running";
        private const string Finish = "finish";
        private const string FightProfile = "core:debug_encounter_fight_01";
        private const string TargetProfile = "core:debug_navigation_npc_01";

        private static GameObject coreFixture;
        private static GameObject invalidationFixture;
        private static GameObject playerFixture;
        private static ActorRuntimeIdentity routineActor;
        private static ActorRuntimeIdentity routineThreat;
        private static ActorRuntimeIdentity singleBandageActor;
        private static ActorRuntimeIdentity noBandageActor;
        private static ActorRuntimeIdentity mildActor;
        private static ActorRuntimeIdentity mildThreat;
        private static ActorRuntimeIdentity emergencyActor;
        private static ActorRuntimeIdentity emergencyThreat;
        private static ActorRuntimeIdentity incapacitationActor;
        private static ActorRuntimeIdentity incapacitationThreat;
        private static GameObject barrier;
        private static int stage;
        private static double stageStartedAt;
        private static double operationStartedAt;
        private static double x1Duration;
        private static double x100Duration;
        private static float playerWalkDistance;
        private static float routineWalkDistance;
        private static float routineCalmDelay;
        private static float mildTimeToFatal;
        private static float emergencyTimeToFatal;
        private static Vector3 positionBefore;
        private static int quantityBefore;
        private static int attacksBefore;
        private static int reloadsBefore;

        static TimedBandagingDiagnostics()
        {
            EditorApplication.update -= Continue;
            EditorApplication.update += Continue;
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Timed Bandaging diagnostics require idle compiled Edit Mode.");
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
                    GameDataManager.Instance?.IsReady == true && WorldClock.Current != null)
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
            ItemDefinition bandage = GameDataManager.Instance.Database.GetItem("core:bandage_01");
            Require(bandage?.consumable?.wound_treatment != null &&
                    Near(bandage.consumable.wound_treatment.application_seconds, 4f),
                "core:bandage_01 does not declare application_seconds=4.0.");
            WorldClock.Current.AdvanceDuringGameplay = false;
            WorldClock.Current.ResetDebugTimeMultiplier();
            barrier = M41SampleSceneNavigationTools.FindBarrier();
            Require(barrier != null, "Navigation LOS barrier fixture is unavailable.");
            barrier.SetActive(false);

            coreFixture = CreateTreatmentFixture("Timed Bandaging Core Fixture", FixturePoint(-7f, -7f), false);
            ApplyWound(coreFixture, WoundId(1), BodyRegion.LeftArm, 0.08f);
            AddBandages(coreFixture, 1);
            ActorWoundTreatmentController treatment = Treatment(coreFixture);
            quantityBefore = BandageCount(coreFixture);
            Require(treatment.TryStart(WoundId(1), ActorWoundTreatmentPurpose.Manual, out string failure),
                "Shared treatment did not start: " + failure);
            Require(BandageCount(coreFixture) == quantityBefore && IsUnbandaged(coreFixture, WoundId(1)),
                "START consumed inventory or mutated the wound.");
            operationStartedAt = Now;
            SetStage(1);
        }

        private static void TickRun()
        {
            if (Now - stageStartedAt > 18d)
                throw new InvalidOperationException("Timed Bandaging diagnostic stage timed out: " + stage);
            switch (stage)
            {
                case 1: WaitForX1Completion(); break;
                case 2: WaitForInvalidatedCompletion(); break;
                case 3: WaitForWalkingProgress(); break;
                case 4: WaitForWalkingCompletion(); break;
                case 5: TestAlreadySprinting(); break;
                case 6: WaitForSprintCancellation(); break;
                case 7: TestCombatCancellationAndStartNpcTimeline(); break;
                case 8: WaitForFight(); break;
                case 9: WaitForLostContact(); break;
                case 10: WaitForSearching(); break;
                case 11: WaitForPostCombatCalm(); break;
                case 12: WaitForRoutineStart(); break;
                case 13: TestRoutineThreatInterruption(); break;
                case 14: WaitForRestartedRoutine(); break;
                case 15: WaitForSequentialRoutineCompletion(); break;
                case 16: StartEmergencyScenarios(); break;
                case 17: WaitForEmergencyStarts(); break;
                case 18: WaitForEmergencyResults(); break;
                case 19: CompleteEmergencyResults(); break;
            }
        }

        private static void WaitForX1Completion()
        {
            ActorWoundTreatmentController treatment = Treatment(coreFixture);
            double elapsed = Now - operationStartedAt;
            if (elapsed < 3.9d)
                return;
            if (elapsed < 4d)
            {
                Require(treatment.IsTreating, "Four-second treatment completed before 4.0 real seconds.");
                return;
            }
            if (treatment.IsTreating)
                return;
            x1Duration = elapsed;
            Require(treatment.LastOutcome == ActorWoundTreatmentOutcome.Completed &&
                    BandageCount(coreFixture) == quantityBefore - 1 && IsBandaged(coreFixture, WoundId(1)),
                "Shared COMPLETE did not bandage durably and consume exactly x1.");

            ApplyWound(coreFixture, WoundId(2), BodyRegion.RightArm, 0.04f);
            AddBandages(coreFixture, 1);
            int beforeCancel = BandageCount(coreFixture);
            Require(treatment.TryStart(WoundId(2), ActorWoundTreatmentPurpose.Manual, out string cancelFailure),
                "Cancellation fixture did not start: " + cancelFailure);
            Require(treatment.Cancel("Diagnostic cancellation") && BandageCount(coreFixture) == beforeCancel &&
                    IsUnbandaged(coreFixture, WoundId(2)),
                "Cancellation consumed a bandage or mutated the wound.");

            invalidationFixture = CreateTreatmentFixture("Timed Bandaging Ownership Fixture", FixturePoint(-5f, -7f), false);
            ApplyWound(invalidationFixture, WoundId(3), BodyRegion.LeftLeg, 0.05f);
            ItemInstance exact = AddBandages(invalidationFixture, 1);
            ActorWoundTreatmentController invalidated = Treatment(invalidationFixture);
            Require(invalidated.TryStart(WoundId(3), exact.InstanceId, ActorWoundTreatmentPurpose.Manual,
                    out string invalidFailure), "Ownership invalidation fixture did not start: " + invalidFailure);
            InventoryComponent inventory = invalidationFixture.GetComponent<InventoryComponent>();
            Require(inventory.TryGetEntryByInstanceId(exact.InstanceId, out int index, out _) &&
                    inventory.TryRemoveItemAt(index, 1),
                "Could not remove the exact treatment instance during the operation.");
            operationStartedAt = Now;
            SetStage(2);
        }

        private static void WaitForInvalidatedCompletion()
        {
            ActorWoundTreatmentController treatment = Treatment(invalidationFixture);
            if (treatment.IsTreating)
                return;
            Require(Now - operationStartedAt >= 3.9d && treatment.LastOutcome == ActorWoundTreatmentOutcome.Failed &&
                    IsUnbandaged(invalidationFixture, WoundId(3)) && BandageCount(invalidationFixture) == 0,
                "Exact-instance ownership invalidation produced an invalid medical commit.");

            playerFixture = CreateTreatmentFixture("Timed Bandaging Player Fixture", FixturePoint(-3f, -7f), true);
            ApplyWound(playerFixture, WoundId(4), BodyRegion.RightLeg, 0.05f);
            AddBandages(playerFixture, 1);
            Require(WorldClock.Current.TrySetDebugTimeMultiplier(100f, out string clockFailure),
                "Could not set WorldClock x100: " + clockFailure);
            PlayerMovementController movement = playerFixture.GetComponent<PlayerMovementController>();
            movement.SetMovementDirection(Vector3.right);
            quantityBefore = BandageCount(playerFixture);
            Require(Treatment(playerFixture).TryStart(WoundId(4), ActorWoundTreatmentPurpose.Manual,
                    out string startFailure), "Walking treatment did not start: " + startFailure);
            positionBefore = playerFixture.transform.position;
            operationStartedAt = Now;
            SetStage(3);
        }

        private static void WaitForWalkingProgress()
        {
            if (Now - operationStartedAt < 0.5d)
                return;
            playerWalkDistance = FlatDistance(positionBefore, playerFixture.transform.position);
            ActorWoundTreatmentController treatment = Treatment(playerFixture);
            Require(playerWalkDistance > 0.1f && treatment.IsTreating && treatment.Progress > 0.05f,
                "Walking did not continue alongside treatment progress.");
            SetStage(4);
        }

        private static void WaitForWalkingCompletion()
        {
            ActorWoundTreatmentController treatment = Treatment(playerFixture);
            double elapsed = Now - operationStartedAt;
            if (elapsed < 3.9d)
                return;
            if (elapsed < 4d)
            {
                Require(treatment.IsTreating, "WorldClock x100 accelerated real-time bandaging.");
                return;
            }
            if (treatment.IsTreating)
                return;
            x100Duration = elapsed;
            Require(treatment.LastOutcome == ActorWoundTreatmentOutcome.Completed &&
                    BandageCount(playerFixture) == quantityBefore - 1 && IsBandaged(playerFixture, WoundId(4)),
                "Walking/x100 treatment did not complete transactionally.");
            PlayerMovementController movement = playerFixture.GetComponent<PlayerMovementController>();
            movement.SetSprintRequested(true);
            movement.SetMovementDirection(Vector3.right);
            SetStage(5);
        }

        private static void TestAlreadySprinting()
        {
            PlayerMovementController movement = playerFixture.GetComponent<PlayerMovementController>();
            if (!movement.IsSprinting)
                return;
            ApplyWound(playerFixture, WoundId(5), BodyRegion.LeftArm, 0.03f);
            AddBandages(playerFixture, 1);
            int before = BandageCount(playerFixture);
            Require(!Treatment(playerFixture).TryStart(WoundId(5), ActorWoundTreatmentPurpose.Manual, out _) &&
                    BandageCount(playerFixture) == before && IsUnbandaged(playerFixture, WoundId(5)),
                "Treatment started while Player was already sprinting.");
            movement.SetSprintRequested(false);
            SetStage(6);
        }

        private static void WaitForSprintCancellation()
        {
            PlayerMovementController movement = playerFixture.GetComponent<PlayerMovementController>();
            if (movement.IsSprinting)
                return;
            ActorWoundTreatmentController treatment = Treatment(playerFixture);
            Require(treatment.TryStart(WoundId(5), ActorWoundTreatmentPurpose.Manual, out string failure),
                "Sprint-cancellation treatment did not start: " + failure);
            int before = BandageCount(playerFixture);
            movement.SetSprintRequested(true);
            stageStartedAt = Now;
            stage = 7;
            quantityBefore = before;
        }

        private static void TestCombatCancellationAndStartNpcTimeline()
        {
            PlayerMovementController movement = playerFixture.GetComponent<PlayerMovementController>();
            ActorWoundTreatmentController treatment = Treatment(playerFixture);
            if (!movement.IsSprinting || treatment.IsTreating)
                return;
            Require(treatment.LastOutcome == ActorWoundTreatmentOutcome.Cancelled &&
                    BandageCount(playerFixture) == quantityBefore && IsUnbandaged(playerFixture, WoundId(5)),
                "Starting sprint did not cancel treatment atomically while allowing sprint.");
            movement.SetSprintRequested(false);
            movement.ClearMovement();
            ApplyWound(playerFixture, WoundId(6), BodyRegion.Torso, 0.04f);
            AddBandages(playerFixture, 1);
            int beforeCombat = BandageCount(playerFixture);
            Require(treatment.TryStart(WoundId(6), ActorWoundTreatmentPurpose.Manual, out string startFailure),
                "Combat-cancellation treatment did not start: " + startFailure);
            FirearmDebugController firearm = playerFixture.AddComponent<FirearmDebugController>();
            firearm.TryStartReload();
            Require(!treatment.IsTreating && treatment.LastOutcome == ActorWoundTreatmentOutcome.Cancelled &&
                    BandageCount(playerFixture) == beforeCombat && IsUnbandaged(playerFixture, WoundId(6)),
                "Player combat adapter did not cancel treatment without consuming it.");
            WorldClock.Current.ResetDebugTimeMultiplier();
            StartNpcTimeline();
        }

        private static void StartNpcTimeline()
        {
            Vector3 observer = Marker(M41SampleSceneNavigationTools.ObserverName).position;
            Vector3 target = Marker(M41SampleSceneNavigationTools.TargetName).position;
            barrier.SetActive(false);
            routineActor = Spawn(FightProfile, observer, Face(target - observer), "Timed Bandaging Routine AI");
            routineThreat = Spawn(TargetProfile, target, Quaternion.identity, "Timed Bandaging Routine Threat");
            routineActor.GetComponent<ActorBehaviorController>().ConfigureAmbient(71001L);
            DisableAcquisition(routineActor);
            DisableAcquisition(routineThreat);
            ApplyWound(routineActor.gameObject, WoundId(10), BodyRegion.LeftArm, 0.02f);
            ApplyWound(routineActor.gameObject, WoundId(11), BodyRegion.RightLeg, 0.08f);
            AddBandages(routineActor.gameObject, 2);
            Require(Controller(routineActor).TryAssignThreat(routineThreat, out string failure),
                "Routine Fight threat assignment failed: " + failure);
            quantityBefore = BandageCount(routineActor.gameObject);
            SetStage(8);
        }

        private static void WaitForFight()
        {
            HumanEncounterAIController controller = Controller(routineActor);
            Require(!Treatment(routineActor.gameObject).IsTreating,
                "Routine self-treatment started before encounter completion.");
            if (controller.State != HumanEncounterAIState.Fighting)
                return;
            attacksBefore = controller.AttackCount;
            barrier.SetActive(true);
            MoveOutsidePerception(routineThreat);
            SetStage(9);
        }

        private static void WaitForLostContact()
        {
            HumanEncounterAIController controller = Controller(routineActor);
            Require(!Treatment(routineActor.gameObject).IsTreating,
                "Routine self-treatment started during LostContact transition.");
            if (controller.State != HumanEncounterAIState.LostContact)
                return;
            SetStage(10);
        }

        private static void WaitForSearching()
        {
            HumanEncounterAIController controller = Controller(routineActor);
            Require(!Treatment(routineActor.gameObject).IsTreating,
                "Routine self-treatment started while Search owned behavior.");
            if (controller.State != HumanEncounterAIState.Searching)
                return;
            SetStage(11);
        }

        private static void WaitForPostCombatCalm()
        {
            HumanEncounterAIController controller = Controller(routineActor);
            Require(!Treatment(routineActor.gameObject).IsTreating,
                "Routine self-treatment started before Search released to Idle/Ambient.");
            if (!controller.IsSelfTreatmentCalm)
                return;
            routineCalmDelay = controller.ResolvedSelfTreatmentCalmSeconds;
            Require(routineCalmDelay >= 2f && routineCalmDelay <= 5f &&
                    controller.GetComponent<ActorBehaviorController>().Owner == ActorBehaviorOwner.Ambient &&
                    controller.Threat == null,
                "Post-combat calm contract or deterministic 2..5s delay is invalid.");
            CreateParallelNpcFixtures();
            SetStage(12);
        }

        private static void WaitForRoutineStart()
        {
            ActorWoundTreatmentController treatment = Treatment(routineActor.gameObject);
            double calmElapsed = Now - Controller(routineActor).SelfTreatmentCalmStartedAt;
            if (calmElapsed < 2d)
                Require(!treatment.IsTreating, "Routine treatment started before the minimum two real seconds of calm.");
            if (!treatment.IsTreating)
                return;
            Require(treatment.Purpose == ActorWoundTreatmentPurpose.RoutineSelfTreatment &&
                    treatment.WoundId == WoundId(11) && BandageCount(routineActor.gameObject) == quantityBefore,
                "Routine treatment did not select the highest effective bleeding wound or mutated at START.");
            positionBefore = routineActor.transform.position;
            attacksBefore = Controller(routineActor).AttackCount;
            reloadsBefore = Controller(routineActor).ReloadCount;
            stageStartedAt = Now;
            stage = 13;
        }

        private static void TestRoutineThreatInterruption()
        {
            if (Now - stageStartedAt < 2d)
                return;
            routineWalkDistance = FlatDistance(positionBefore, routineActor.transform.position);
            ActorWoundTreatmentController treatment = Treatment(routineActor.gameObject);
            Require(treatment.IsTreating && Controller(routineActor).AttackCount == attacksBefore &&
                    Controller(routineActor).ReloadCount == reloadsBefore && routineWalkDistance > 0.05f,
                "NPC did not retain normal locomotion or executed weapon actions while treatment occupied its hands.");
            Require(Controller(routineActor).TryAssignThreat(routineThreat, out string failure),
                "Could not reintroduce threat during routine treatment: " + failure);
            Require(!treatment.IsTreating && treatment.LastOutcome == ActorWoundTreatmentOutcome.Cancelled &&
                    BandageCount(routineActor.gameObject) == quantityBefore && IsUnbandaged(routineActor.gameObject, WoundId(11)),
                "Renewed threat did not cancel routine treatment atomically.");
            Controller(routineActor).ClearThreat("Diagnostic routine restart");
            SetStage(14);
        }

        private static void WaitForRestartedRoutine()
        {
            ActorWoundTreatmentController treatment = Treatment(routineActor.gameObject);
            if (!treatment.IsTreating)
                return;
            Require(treatment.WoundId == WoundId(11), "Restarted routine treatment changed worst-wound selection.");
            SetStage(15);
        }

        private static void WaitForSequentialRoutineCompletion()
        {
            ActorWoundTreatmentController treatment = Treatment(routineActor.gameObject);
            if (treatment.CompletedCount < 2 || treatment.IsTreating)
                return;
            Require(IsBandaged(routineActor.gameObject, WoundId(11)) &&
                    IsBandaged(routineActor.gameObject, WoundId(10)) &&
                    BandageCount(routineActor.gameObject) == quantityBefore - 2 &&
                    treatment.StartedCount >= 3,
                "Two wounds/two bandages did not complete sequentially with exact inventory consumption.");

            ActorWoundTreatmentController single = Treatment(singleBandageActor.gameObject);
            Require(IsBandaged(singleBandageActor.gameObject, WoundId(20)) &&
                    IsUnbandaged(singleBandageActor.gameObject, WoundId(21)) &&
                    BandageCount(singleBandageActor.gameObject) == 0 && !single.IsTreating,
                "Two wounds/one bandage did not treat only the worst bleeding wound.");
            Require(Treatment(noBandageActor.gameObject).StartedCount == 0 &&
                    IsUnbandaged(noBandageActor.gameObject, WoundId(22)),
                "NPC without a real bandage started self-treatment.");
            SetStage(16);
        }

        private static void StartEmergencyScenarios()
        {
            Vector3 mildPos = FixturePoint(4f, -5f);
            Vector3 mildThreatPos = FixturePoint(4f, -1f);
            mildActor = Spawn(FightProfile, mildPos, Face(mildThreatPos - mildPos), "Timed Bandaging Mild AI");
            mildThreat = Spawn(TargetProfile, mildThreatPos, Quaternion.identity, "Timed Bandaging Mild Threat");
            DisableAcquisition(mildActor);
            DisableAcquisition(mildThreat);
            ApplyWound(mildActor.gameObject, WoundId(30), BodyRegion.LeftLeg, 0.01f);
            AddBandages(mildActor.gameObject, 1);
            Require(Controller(mildActor).TryAssignThreat(mildThreat, out string mildFailure),
                "Mild bleeding combat fixture failed: " + mildFailure);
            mildTimeToFatal = Controller(mildActor).EstimatedRealSecondsUntilFatalBleeding;
            Require(mildTimeToFatal > Controller(mildActor).ResolvedSelfTreatmentCalmSeconds + 4f,
                "Mild bleeding fixture is unexpectedly emergency-eligible.");
            stageStartedAt = Now;
            stage = 17;
        }

        private static void WaitForEmergencyStarts()
        {
            if (Now - stageStartedAt < 0.6d)
                return;
            Require(!Treatment(mildActor.gameObject).IsTreating &&
                    Treatment(mildActor.gameObject).StartedCount == 0,
                "Non-emergency bleeding started treatment during combat.");
            RemoveRuntime(mildActor, mildThreat);
            mildActor = null;
            mildThreat = null;

            Require(WorldClock.Current.TrySetDebugTimeMultiplier(100f, out string clockFailure),
                "Could not set emergency WorldClock rate: " + clockFailure);
            emergencyActor = Spawn(FightProfile, FixturePoint(4f, 1f), Quaternion.identity, "Timed Bandaging Emergency AI");
            emergencyThreat = Spawn(TargetProfile, FixturePoint(4f, 5f), Quaternion.identity, "Timed Bandaging Emergency Threat");
            incapacitationActor = Spawn(FightProfile, FixturePoint(7f, 1f), Quaternion.identity, "Timed Bandaging Incap AI");
            incapacitationThreat = Spawn(TargetProfile, FixturePoint(7f, 5f), Quaternion.identity, "Timed Bandaging Incap Threat");
            foreach (ActorRuntimeIdentity actor in new[] { emergencyActor, emergencyThreat, incapacitationActor, incapacitationThreat })
                DisableAcquisition(actor);
            ApplyWound(emergencyActor.gameObject, WoundId(31), BodyRegion.Torso, 1f);
            ApplyWound(incapacitationActor.gameObject, WoundId(32), BodyRegion.Torso, 1f);
            AddBandages(emergencyActor.gameObject, 1);
            AddBandages(incapacitationActor.gameObject, 1);
            Require(Controller(emergencyActor).TryAssignThreat(emergencyThreat, out string emergencyFailure),
                "Emergency combat fixture failed: " + emergencyFailure);
            Require(Controller(incapacitationActor).TryAssignThreat(incapacitationThreat, out string incapFailure),
                "Incapacitation combat fixture failed: " + incapFailure);
            emergencyTimeToFatal = Controller(emergencyActor).EstimatedRealSecondsUntilFatalBleeding;
            SetStage(18);
        }

        private static void WaitForEmergencyResults()
        {
            ActorWoundTreatmentController emergency = Treatment(emergencyActor.gameObject);
            ActorWoundTreatmentController incap = Treatment(incapacitationActor.gameObject);
            if (!emergency.IsTreating || !incap.IsTreating)
                return;
            Require(emergency.Purpose == ActorWoundTreatmentPurpose.EmergencySelfTreatment &&
                    incap.Purpose == ActorWoundTreatmentPurpose.EmergencySelfTreatment &&
                    emergencyTimeToFatal <= Controller(emergencyActor).ResolvedSelfTreatmentCalmSeconds + 4f,
                "Fatal-risk estimate did not permit emergency treatment during combat.");
            attacksBefore = Controller(emergencyActor).AttackCount;
            reloadsBefore = Controller(emergencyActor).ReloadCount;
            int emergencyQuantity = BandageCount(emergencyActor.gameObject);
            int incapQuantity = BandageCount(incapacitationActor.gameObject);
            Require(incapacitationActor.GetComponent<ActorConditionComponent>().TryApplyPersistenceState(
                    new ActorConditionStateData { bloodFraction = 1f, transientTrauma = 1f }, out string conditionFailure),
                "Could not force diagnostic incapacity: " + conditionFailure);
            stageStartedAt = Now;
            stage = 19;
            quantityBefore = emergencyQuantity;
            SessionState.SetInt("OldScars.TimedBandaging.IncapQuantity", incapQuantity);
        }

        private static void CompleteEmergencyResults()
        {
            ActorWoundTreatmentController emergency = Treatment(emergencyActor.gameObject);
            ActorWoundTreatmentController incap = Treatment(incapacitationActor.gameObject);
            if (Now - stageStartedAt < 0.2d)
                return;
            Require(!incap.IsTreating && incap.LastOutcome == ActorWoundTreatmentOutcome.Cancelled &&
                    BandageCount(incapacitationActor.gameObject) == SessionState.GetInt("OldScars.TimedBandaging.IncapQuantity", -1) &&
                    IsUnbandaged(incapacitationActor.gameObject, WoundId(32)),
                "Incapacitation did not cancel emergency treatment without phantom commit.");
            if (emergency.IsTreating)
            {
                Require(Controller(emergencyActor).AttackCount == attacksBefore &&
                        Controller(emergencyActor).ReloadCount == reloadsBefore,
                    "Emergency treatment allowed simultaneous NPC combat actions.");
                return;
            }
            Require(emergency.LastOutcome == ActorWoundTreatmentOutcome.Completed &&
                    BandageCount(emergencyActor.gameObject) == quantityBefore - 1 &&
                    IsBandaged(emergencyActor.gameObject, WoundId(31)),
                "Emergency treatment did not consume one real bandage only at COMPLETE.");
            CompleteRun();
        }

        private static void CreateParallelNpcFixtures()
        {
            singleBandageActor = Spawn(FightProfile, FixturePoint(5f, -6f), Quaternion.identity,
                "Timed Bandaging Single Bandage AI");
            noBandageActor = Spawn(FightProfile, FixturePoint(7f, -6f), Quaternion.identity,
                "Timed Bandaging No Bandage AI");
            singleBandageActor.GetComponent<ActorBehaviorController>().ConfigureAmbient(71002L);
            noBandageActor.GetComponent<ActorBehaviorController>().ConfigureAmbient(71003L);
            DisableAcquisition(singleBandageActor);
            DisableAcquisition(noBandageActor);
            ApplyWound(singleBandageActor.gameObject, WoundId(20), BodyRegion.Head, 0.09f);
            ApplyWound(singleBandageActor.gameObject, WoundId(21), BodyRegion.RightArm, 0.02f);
            AddBandages(singleBandageActor.gameObject, 1);
            ApplyWound(noBandageActor.gameObject, WoundId(22), BodyRegion.LeftLeg, 0.07f);
        }

        private static GameObject CreateTreatmentFixture(string name, Vector3 position, bool movement)
        {
            var root = new GameObject(name);
            root.transform.position = position;
            root.AddComponent<InventoryComponent>();
            root.AddComponent<ActorItemOwnershipComponent>();
            root.AddComponent<ActorHealthComponent>();
            if (movement)
            {
                root.AddComponent<CharacterController>();
                root.AddComponent<ActorStaminaComponent>();
                root.AddComponent<PlayerMovementController>();
            }
            return root;
        }

        private static ActorRuntimeIdentity Spawn(string profileId, Vector3 position, Quaternion rotation, string name)
        {
            Require(ActorSpawnService.TrySpawn(profileId, position, rotation,
                    out ActorRuntimeIdentity identity, out string failure),
                name + " spawn failed: " + failure);
            identity.name = name;
            return identity;
        }

        private static void DisableAcquisition(ActorRuntimeIdentity actor)
        {
            ActorThreatAcquisitionController acquisition = actor != null
                ? actor.GetComponent<ActorThreatAcquisitionController>()
                : null;
            if (acquisition != null)
                acquisition.enabled = false;
        }

        private static void MoveOutsidePerception(ActorRuntimeIdentity actor)
        {
            NavMeshAgent agent = actor.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled)
                agent.enabled = false;
            actor.transform.SetPositionAndRotation(FixturePoint(0f, 60f), Quaternion.identity);
            Physics.SyncTransforms();
        }

        private static void ApplyWound(GameObject actor, string woundId, BodyRegion region, float bleeding)
        {
            ActorMedicalStateComponent medical = actor.GetComponent<ActorMedicalStateComponent>();
            string failure = medical == null ? "Actor medical authority is unavailable." : null;
            Require(medical != null && medical.TryApplyWound(
                    woundId, region, WoundType.Laceration, 0.1f, bleeding, 0.01f, out failure),
                "Could not create wound " + woundId + ": " + failure);
        }

        private static ItemInstance AddBandages(GameObject actor, int quantity)
        {
            ItemInstance item = actor.GetComponent<InventoryComponent>().AddItemByDefinitionId("core:bandage_01", quantity);
            Require(item != null, "Could not seed controlled bandage inventory on " + actor.name + ".");
            return item;
        }

        private static int BandageCount(GameObject actor) => InventoryItemUseService.GetAvailableWoundTreatmentQuantity(
            actor.GetComponent<ActorItemOwnershipComponent>());

        private static ActorWoundTreatmentController Treatment(GameObject actor)
        {
            ActorWoundTreatmentController result = actor.GetComponent<ActorWoundTreatmentController>();
            Require(result != null, actor.name + " lacks ActorWoundTreatmentController.");
            return result;
        }

        private static HumanEncounterAIController Controller(ActorRuntimeIdentity actor)
        {
            HumanEncounterAIController result = actor.GetComponent<HumanEncounterAIController>();
            Require(result != null && result.IsConfigured, actor.name + " lacks configured Human Encounter AI.");
            return result;
        }

        private static bool IsBandaged(GameObject actor, string woundId) =>
            actor.GetComponent<ActorMedicalStateComponent>().GetWound(woundId)?.treatmentState ==
            WoundTreatmentState.Bandaged.ToString();

        private static bool IsUnbandaged(GameObject actor, string woundId) =>
            actor.GetComponent<ActorMedicalStateComponent>().GetWound(woundId)?.treatmentState ==
            WoundTreatmentState.Unbandaged.ToString();

        private static void SetStage(int next)
        {
            stage = next;
            stageStartedAt = Now;
        }

        private static void CompleteRun()
        {
            Debug.Log(
                "Timed Bandaging / NPC Self-Treatment Diagnostics: PASS" +
                $"\n  ApplicationSeconds: 4" +
                $"\n  X1RealDuration: {x1Duration:0.###}" +
                $"\n  X100RealDuration: {x100Duration:0.###}" +
                $"\n  PlayerWalkDistance: {playerWalkDistance:0.###}" +
                $"\n  RoutineCalmDelay: {routineCalmDelay:0.###}" +
                $"\n  RoutineWalkDistance: {routineWalkDistance:0.###}" +
                $"\n  MildTimeToFatalRealSeconds: {mildTimeToFatal:0.###}" +
                $"\n  EmergencyTimeToFatalRealSeconds: {emergencyTimeToFatal:0.###}" +
                "\n  ExactOwnershipInvalidation: rejected" +
                "\n  WorstBleedingSelection: PASS" +
                "\n  SequentialTreatment: PASS" +
                "\n  RoutineThreatCancellation: PASS" +
                "\n  EmergencyIncapacitationCancellation: PASS");
            CleanupRuntime();
            SessionState.SetString(PhaseKey, Finish);
            EditorApplication.ExitPlaymode();
        }

        private static void FinalizeRun()
        {
            string failure = SessionState.GetString(ErrorKey, string.Empty);
            bool success = string.IsNullOrEmpty(failure) && !EditorSceneManager.GetActiveScene().isDirty;
            if (!success)
                Debug.LogError("Timed Bandaging / NPC Self-Treatment Diagnostics: FAIL\n- " +
                               (string.IsNullOrEmpty(failure) ? "Diagnostic dirtied SampleScene." : failure));
            ClearRun();
            if (Application.isBatchMode)
                EditorApplication.Exit(success ? 0 : 1);
        }

        private static void CleanupRuntime()
        {
            WorldClock.Current?.ResetDebugTimeMultiplier();
            if (barrier != null)
                barrier.SetActive(true);
            foreach (ActorRuntimeIdentity actor in ActorRuntimeRegistry.ActiveRepresentations
                         .Where(value => value != null && value.OriginKind == ActorOriginKind.Runtime &&
                                         value.name.StartsWith("Timed Bandaging ", StringComparison.Ordinal)).ToArray())
                ActorSpawnService.TryRemoveRuntimeRepresentationForRestore(actor.ActorInstanceId, out _);
            if (coreFixture != null) UnityEngine.Object.Destroy(coreFixture);
            if (invalidationFixture != null) UnityEngine.Object.Destroy(invalidationFixture);
            if (playerFixture != null) UnityEngine.Object.Destroy(playerFixture);
        }

        private static void RemoveRuntime(params ActorRuntimeIdentity[] actors)
        {
            foreach (ActorRuntimeIdentity actor in actors)
            {
                if (actor != null && actor.IsRegistered)
                    ActorSpawnService.TryRemoveRuntimeRepresentationForRestore(actor.ActorInstanceId, out _);
            }
        }

        private static void ClearRun()
        {
            SessionState.EraseString(PhaseKey);
            SessionState.EraseString(ErrorKey);
            SessionState.EraseInt("OldScars.TimedBandaging.IncapQuantity");
        }

        private static Transform Marker(string name)
        {
            Transform marker = M41SampleSceneNavigationTools.FindMarker(name);
            Require(marker != null, "Navigation fixture marker is missing: " + name);
            return marker;
        }

        private static Vector3 FixturePoint(float x, float z)
        {
            GameObject root = GameObject.Find(M41SampleSceneNavigationTools.FixtureRootName);
            Require(root != null, "Navigation fixture root is missing.");
            return root.transform.TransformPoint(new Vector3(x, 0f, z));
        }

        private static Quaternion Face(Vector3 direction)
        {
            Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up);
            return flat.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(flat) : Quaternion.identity;
        }

        private static string WoundId(int value) => "wound_" + value.ToString("x32");
        private static float FlatDistance(Vector3 left, Vector3 right)
        {
            left.y = 0f;
            right.y = 0f;
            return Vector3.Distance(left, right);
        }
        private static bool Near(float left, float right, float tolerance = 0.001f) => Mathf.Abs(left - right) <= tolerance;
        private static double Now => Time.realtimeSinceStartupAsDouble;

        private static void Require(bool condition, string failure)
        {
            if (!condition)
                throw new InvalidOperationException(failure);
        }
    }
}
