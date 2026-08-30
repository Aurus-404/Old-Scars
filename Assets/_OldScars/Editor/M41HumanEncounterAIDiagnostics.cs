using System;
using System.Linq;
using OldScars.Core;
using OldScars.Core.Actors;
using OldScars.Core.Combat;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace OldScars.Editor
{
    [InitializeOnLoad]
    public static class M41HumanEncounterAIDiagnostics
    {
        private const string ManualAvoidMenu = "Old Scars/Diagnostics/AI/M41.1/Prepare Manual - Avoid";
        private const string ManualFleeMenu = "Old Scars/Diagnostics/AI/M41.1/Prepare Manual - Flee";
        private const string ManualFightMenu = "Old Scars/Diagnostics/AI/M41.1/Prepare Manual - Fight";
        private const string ManualLosMenu = "Old Scars/Diagnostics/AI/M41.1/Prepare Manual - LOS";
        private const string ToggleLosBarrierMenu = "Old Scars/Diagnostics/AI/M41.1/Toggle LOS Barrier";
        private const string ManualScenarioKey = "OldScars.M41.1.HumanEncounter.ManualScenario";
        private const string PhaseKey = "OldScars.M41.1.HumanEncounter.Phase";
        private const string ErrorKey = "OldScars.M41.1.HumanEncounter.Error";
        private const string AvoidProfile = "core:debug_encounter_avoid_01";
        private const string FleeProfile = "core:debug_encounter_flee_01";
        private const string FightProfile = "core:debug_encounter_fight_01";
        private const string ArmoredTargetProfile = "core:debug_encounter_armored_target_01";
        private const string TargetProfile = "core:debug_navigation_npc_01";
        private const string Enter = "enter";
        private const string Running = "running";
        private const string Finish = "finish";

        private static ActorRuntimeIdentity ai;
        private static ActorRuntimeIdentity threat;
        private static HumanEncounterAIController controller;
        private static GameObject barrier;
        private static int stage;
        private static double deadline;
        private static Vector3 initialAIPosition;
        private static Vector3 initialThreatPosition;
        private static Vector3 frozenLastKnown;
        private static int transitionRevision;
        private static int roundsAfterFirstShot;
        private static int woundsAfterFirstShot;
        private static int attacksAfterFirstShot;
        private static int planAttempts;
        private static double assertionTime;
        public static string CurrentManualScenario => SessionState.GetString(ManualScenarioKey, "<NONE>");

        static M41HumanEncounterAIDiagnostics()
        {
            EditorApplication.update -= Continue;
            EditorApplication.update += Continue;
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("M41.1 diagnostics require idle Edit Mode.");
            ClearRun();
            SessionState.SetString(PhaseKey, Enter);
            EditorSceneManager.OpenScene(M41SampleSceneNavigationTools.ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem(ManualAvoidMenu)]
        public static void PrepareManualAvoid() => PrepareManualEncounter("Avoid", AvoidProfile, false);

        [MenuItem(ManualFleeMenu)]
        public static void PrepareManualFlee() => PrepareManualEncounter("Flee", FleeProfile, false);

        [MenuItem(ManualFightMenu)]
        public static void PrepareManualFight() => PrepareManualEncounter("Fight", FightProfile, false);

        [MenuItem(ManualLosMenu)]
        public static void PrepareManualLos() => PrepareManualEncounter("LOS", AvoidProfile, true);

        private static void PrepareManualEncounter(string mode, string profile, bool isLosScenario)
        {
            Require(EditorApplication.isPlaying && GameDataManager.Instance?.IsReady == true,
                "M41.1 manual setup requires Play Mode with ready game data.");
            RemoveNamedRuntimeActors("M41.1 Manual ");
            barrier = RequireBarrier();
            SetEncounterLosBarrierHeight(barrier, isLosScenario);
            Transform root = RequireFixtureRoot();
            bool isFightScenario = mode == "Fight";
            Vector3 actorPosition = isFightScenario ? root.TransformPoint(new Vector3(-4f, 0f, -4f)) : Marker(M41SampleSceneNavigationTools.StartName).position;
            Vector3 targetPosition = isFightScenario ? root.TransformPoint(new Vector3(-4f, 0f, 4f)) :
                isLosScenario ? Marker(M41SampleSceneNavigationTools.TargetName).position : root.TransformPoint(new Vector3(-7f, 0f, 0f));
            if (isLosScenario)
                actorPosition = Marker(M41SampleSceneNavigationTools.ObserverName).position;
            barrier.SetActive(!isLosScenario);
            ActorRuntimeIdentity manualAI = Spawn(profile, actorPosition, Face(targetPosition - actorPosition), "M41.1 Manual " + mode + " AI");
            ActorRuntimeIdentity manualThreat = Spawn(TargetProfile, targetPosition, Quaternion.identity, "M41.1 Manual " + mode + " Threat");
            HumanEncounterAIController manualController = manualAI.GetComponent<HumanEncounterAIController>();
            Require(manualController != null, "Manual actor lacks Human Encounter AI.");
            Require(manualController.TryAssignThreat(manualThreat, out string error),
                "Manual threat assignment failed: " + error);
            Debug.Log(
                "[M41.1][MANUAL_READY]" +
                $"\n  Mode: {mode}" +
                $"\n  AI: {manualAI.ActorInstanceId}" +
                $"\n  Threat: {manualThreat.ActorInstanceId}" +
                $"\n  BarrierActive: {barrier.activeSelf}" +
                "\n  Check: observe [AI][STATE], physical navigation and real combat/reload effects." +
                (isLosScenario ? "\n  Next: toggle LOS Barrier after Alerted to prove LostContact." : string.Empty));
            SessionState.SetString(ManualScenarioKey, mode);
            M41ManualEncounterStatusWindow.ShowWindow();
        }

        [MenuItem(ToggleLosBarrierMenu)]
        public static void ToggleManualLosBarrier()
        {
            Require(EditorApplication.isPlaying, "M41.1 LOS toggle requires Play Mode.");
            GameObject losBarrier = RequireBarrier();
            losBarrier.SetActive(!losBarrier.activeSelf);
            Physics.SyncTransforms();
            if (!losBarrier.activeSelf)
            {
                HumanEncounterAIController manualAI = UnityEngine.Object.FindObjectsByType<HumanEncounterAIController>()
                    .FirstOrDefault(value => value != null && value.name == "M41.1 Manual LOS AI");
                ActorRuntimeIdentity manualThreat = ActorRuntimeRegistry.ActiveRepresentations
                    .FirstOrDefault(value => value != null && value.name == "M41.1 Manual LOS Threat");
                if (manualAI != null && manualThreat != null && manualAI.Threat == null)
                    Require(manualAI.TryAssignThreat(manualThreat, out string error), "Manual LOS reacquisition failed: " + error);
            }
            Debug.Log($"[M41.1][MANUAL_LOS]\n  BarrierActive: {losBarrier.activeSelf}\n  Expected: {(losBarrier.activeSelf ? "LostContact after Occluded" : "Alerted after explicit reacquisition if timeout cleared the threat")}");
        }

        private static void Continue()
        {
            string phase = SessionState.GetString(PhaseKey, string.Empty);
            if (string.IsNullOrEmpty(phase))
                return;
            try
            {
                if (phase == Enter && EditorApplication.isPlaying && Time.frameCount >= 5 && GameDataManager.Instance?.IsReady == true)
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
            ValidateProfiles();
            barrier = RequireBarrier();
            barrier.SetActive(true);
            SetEncounterLosBarrierHeight(barrier, true);
            Transform root = RequireFixtureRoot();
            initialAIPosition = root.TransformPoint(new Vector3(-5f, 0f, 0f));
            initialThreatPosition = root.TransformPoint(new Vector3(-7f, 0f, 0f));
            ai = Spawn(AvoidProfile, initialAIPosition, Face(initialThreatPosition - initialAIPosition), "M41.1 Diagnostic Avoid AI");
            threat = Spawn(TargetProfile, initialThreatPosition, Quaternion.identity, "M41.1 Diagnostic Avoid Threat");
            controller = RequireController(ai);
            Require(controller.State == HumanEncounterAIState.Idle, "Encounter AI did not bootstrap Idle.");
            Require(controller.TryAssignThreat(threat, out string error), "Explicit threat assignment failed: " + error);
            deadline = Now + 3d;
            stage = 1;
        }

        private static void TickRun()
        {
            if (Now > deadline)
                throw new InvalidOperationException("M41.1 diagnostic stage timed out: " + stage);
            switch (stage)
            {
                case 1:
                    if (controller.State != HumanEncounterAIState.Alerted)
                        return;
                    Require(controller.HasLastKnownPosition && Near(controller.LastKnownPosition, threat.GetComponent<Collider>().bounds.center, 0.2f),
                        "Alert did not record the perceived threat position.");
                    deadline = Now + 4d;
                    stage = 2;
                    break;
                case 2:
                    Require(!InsideExpandedBarrier(ai.transform.position, 0.05f),
                        "Avoid navigator entered blocking barrier bounds.");
                    if ((controller.State != HumanEncounterAIState.Avoiding && controller.State != HumanEncounterAIState.LostContact) ||
                        Vector3.Distance(ai.transform.position, initialThreatPosition) < 9f)
                        return;
                    Require(Vector3.Distance(ai.transform.position, initialThreatPosition) > Vector3.Distance(initialAIPosition, initialThreatPosition) + 6f,
                        "Avoid policy did not physically increase threat distance around the barrier.");
                    barrier.SetActive(false);
                    Physics.SyncTransforms();
                    transitionRevision = controller.TransitionRevision;
                    Require(controller.TryOverrideResponse(HumanEncounterResponse.Flee, out string overrideError),
                        "Flee interruption failed: " + overrideError);
                    Require(controller.State == HumanEncounterAIState.Alerted &&
                            controller.GetComponent<ActorNavigationController>().State == ActorNavigationState.Idle,
                        "Avoiding order was not interrupted before Flee reevaluation.");
                    deadline = Now + 3d;
                    stage = 3;
                    break;
                case 3:
                    if (controller.State != HumanEncounterAIState.Fleeing)
                        return;
                    Require(controller.TransitionRevision > transitionRevision, "Flee transition was not observable.");
                    controller.ClearThreat("Diagnostic flee threat removed");
                    Require(controller.State == HumanEncounterAIState.Idle &&
                            controller.GetComponent<ActorNavigationController>().State == ActorNavigationState.Idle,
                        "Flee did not exit safely when its explicit threat disappeared.");
                    SetupInvalidFlee();
                    break;
                case 4:
                    if (!controller.NavigationFailureLatched)
                        return;
                    planAttempts = controller.NavigationPlanAttemptCount;
                    assertionTime = Now + 0.35d;
                    deadline = Now + 1d;
                    stage = 5;
                    break;
                case 5:
                    if (Now < assertionTime)
                        return;
                    Require(controller.NavigationFailureLatched && controller.NavigationPlanAttemptCount == planAttempts,
                        "Invalid flee destination retried or changed state in a decision loop.");
                    SetupFight();
                    break;
                case 6:
                    if (controller.State != HumanEncounterAIState.Fighting)
                        return;
                    Require(EquippedFirearm(out ItemInstance firearm) && firearm.LoadedRounds == 0 &&
                            WeaponCombatService.GetCompatibleAmmoQuantity(ai.GetComponent<ActorItemOwnershipComponent>(), firearm) == 12,
                        "Fight fixture did not start with real unloaded firearm and owned ammo.");
                    deadline = Now + 5d;
                    stage = 7;
                    break;
                case 7:
                    if (controller.ReloadCount < 1)
                        return;
                    Require(EquippedFirearm(out ItemInstance loaded) && loaded.LoadedRounds == 10 &&
                            WeaponCombatService.GetCompatibleAmmoQuantity(ai.GetComponent<ActorItemOwnershipComponent>(), loaded) == 2,
                        "Timed reload did not consume exact owned ammo into loaded state.");
                    deadline = Now + 3d;
                    stage = 8;
                    break;
                case 8:
                    if (controller.AttackCount < 1)
                        return;
                    ActorMedicalStateComponent medical = threat.GetComponent<ActorMedicalStateComponent>();
                    bool hasFiredWeapon = EquippedFirearm(out ItemInstance fired);
                    WeaponCombatResult shot = controller.LastCombatResult;
                    bool validLocalizedArmorResolution = shot.Combat.Region.HasValue &&
                        (shot.Combat.Armor.Outcome == ArmorResolutionOutcome.Penetrated ||
                         shot.Combat.Armor.Outcome == ArmorResolutionOutcome.Unarmored);
                    Require(hasFiredWeapon && fired.LoadedRounds == 9 && medical.WoundCount == 1 &&
                            shot.Quantity == 1 && shot.PhysicalShot.Termination == PhysicalShotTermination.Impact &&
                            shot.Combat.Resolved && validLocalizedArmorResolution &&
                            shot.Combat.FinalWoundType == WoundType.Puncture &&
                            shot.Combat.Armor.ResidualPower > 0f,
                        "Fight contract mismatch." +
                        $" Weapon={hasFiredWeapon}, Loaded={(hasFiredWeapon ? fired.LoadedRounds : -1)}," +
                        $" Wounds={medical.WoundCount}, Quantity={shot.Quantity}, Physical={shot.PhysicalShot.Termination}," +
                        $" Combat={shot.Combat.Code}, Region={shot.Combat.Region}, Type={shot.Combat.FinalWoundType}," +
                        $" Armor={shot.Combat.Armor.Outcome}, Residual={shot.Combat.Armor.ResidualPower:0.###}," +
                        $" Message={shot.Message ?? "<NONE>"}");
                    roundsAfterFirstShot = fired.LoadedRounds;
                    woundsAfterFirstShot = medical.WoundCount;
                    attacksAfterFirstShot = controller.AttackCount;
                    assertionTime = Now + 0.3d;
                    deadline = Now + 1d;
                    stage = 9;
                    break;
                case 9:
                    if (Now < assertionTime)
                        return;
                    Require(EquippedFirearm(out ItemInstance cycling) && cycling.LoadedRounds == roundsAfterFirstShot &&
                            threat.GetComponent<ActorMedicalStateComponent>().WoundCount == woundsAfterFirstShot &&
                            controller.AttackCount == attacksAfterFirstShot,
                        "Firearm cycle cadence allowed an early second attack.");
                    SetupLostContact();
                    break;
                case 10:
                    Vector3 clearTargetCenter = threat.GetComponent<Collider>().bounds.center;
                    if (!controller.HasLastKnownPosition || !Near(controller.LastKnownPosition, clearTargetCenter, 0.2f))
                        return;
                    frozenLastKnown = controller.LastKnownPosition;
                    Require(EquippedFirearm(out ItemInstance beforeOcclusion), "Fighter lost firearm before LOS interruption.");
                    roundsAfterFirstShot = beforeOcclusion.LoadedRounds;
                    woundsAfterFirstShot = threat.GetComponent<ActorMedicalStateComponent>().WoundCount;
                    attacksAfterFirstShot = controller.AttackCount;
                    barrier.SetActive(true);
                    threat.GetComponent<ActorNavigationController>().ApplyPersistencePose(
                        Marker(M41SampleSceneNavigationTools.TargetName).position + Vector3.forward, Quaternion.identity);
                    Physics.SyncTransforms();
                    deadline = Now + 2d;
                    stage = 11;
                    break;
                case 11:
                    if (controller.State != HumanEncounterAIState.LostContact)
                        return;
                    Require(Near(controller.LastKnownPosition, frozenLastKnown, 0.01f),
                        "Occluded perception leaked the threat's current position into last-known memory.");
                    Require(CombatInvariantsHold(), "Combat mutated after LOS was lost.");
                    deadline = Now + 2d;
                    stage = 12;
                    break;
                case 12:
                    if (controller.State != HumanEncounterAIState.Idle)
                        return;
                    Require(controller.Threat == null, "Lost-contact timeout did not clear the encounter.");
                    Require(CombatInvariantsHold(), "Combat mutated during lost-contact timeout.");
                    barrier.SetActive(false);
                    Physics.SyncTransforms();
                    Require(controller.TryAssignThreat(threat, out string reacquireError), "Explicit reacquisition failed: " + reacquireError);
                    deadline = Now + 2d;
                    stage = 13;
                    break;
                case 13:
                    if (controller.State != HumanEncounterAIState.Alerted)
                        return;
                    Require(controller.HasLastKnownPosition && !Near(controller.LastKnownPosition, frozenLastKnown, 0.1f),
                        "Reacquisition did not refresh last-known position from a positive perception.");
                    Require(EquippedFirearm(out ItemInstance beforeDeath), "Fighter lost equipped firearm before lifecycle test.");
                    roundsAfterFirstShot = beforeDeath.LoadedRounds;
                    attacksAfterFirstShot = controller.AttackCount;
                    ai.GetComponent<ActorHealthComponent>().Kill();
                    assertionTime = Now + 1.5d;
                    deadline = Now + 2d;
                    stage = 14;
                    break;
                case 14:
                    if (Now < assertionTime)
                        return;
                    Require(controller.State == HumanEncounterAIState.Inactive &&
                            controller.GetComponent<ActorNavigationController>().State == ActorNavigationState.Idle &&
                            EquippedFirearm(out ItemInstance deadWeapon) && deadWeapon.LoadedRounds == roundsAfterFirstShot &&
                            controller.AttackCount == attacksAfterFirstShot && !controller.IsReloadPending,
                        "Dead encounter AI retained navigation, reload or attack activity.");
                    CompleteRun();
                    break;
            }
        }

        private static void SetupInvalidFlee()
        {
            RemoveDiagnosticActors();
            Transform root = RequireFixtureRoot();
            Vector3 edgePosition = root.TransformPoint(new Vector3(7.3f, 0f, 0f));
            Vector3 nearbyThreat = root.TransformPoint(new Vector3(6.0f, 0f, 0f));
            ai = Spawn(FleeProfile, edgePosition, Face(nearbyThreat - edgePosition), "M41.1 Diagnostic Invalid Flee AI");
            threat = Spawn(TargetProfile, nearbyThreat, Quaternion.identity, "M41.1 Diagnostic Invalid Flee Threat");
            controller = RequireController(ai);
            Require(controller.TryAssignThreat(threat, out string error), "Invalid-path flee threat assignment failed: " + error);
            deadline = Now + 3d;
            stage = 4;
        }

        private static void SetupFight()
        {
            RemoveDiagnosticActors();
            barrier.SetActive(false);
            Transform root = RequireFixtureRoot();
            Vector3 actorPosition = root.TransformPoint(new Vector3(-4f, 0f, -4f));
            Vector3 targetPosition = root.TransformPoint(new Vector3(-4f, 0f, 4f));
            ai = Spawn(FightProfile, actorPosition, Face(targetPosition - actorPosition), "M41.1 Diagnostic Fight AI");
            threat = Spawn(ArmoredTargetProfile, targetPosition, Quaternion.identity, "M41.1 Diagnostic Fight Threat");
            controller = RequireController(ai);
            Require(controller.TryAssignThreat(threat, out string error), "Fight threat assignment failed: " + error);
            deadline = Now + 3d;
            stage = 6;
        }

        private static void SetupLostContact()
        {
            ActorNavigationController nav = ai.GetComponent<ActorNavigationController>();
            ActorNavigationController targetNav = threat.GetComponent<ActorNavigationController>();
            Vector3 observerPosition = Marker(M41SampleSceneNavigationTools.ObserverName).position;
            Vector3 targetPosition = Marker(M41SampleSceneNavigationTools.TargetName).position;
            nav.ApplyPersistencePose(observerPosition, Face(targetPosition - observerPosition));
            targetNav.ApplyPersistencePose(targetPosition, Quaternion.identity);
            barrier.SetActive(false);
            Physics.SyncTransforms();
            ActorVisualPerceptionResult clear = ai.GetComponent<ActorVisualPerceptionService>().Evaluate(threat);
            Require(clear.Perceived, "Lost-contact setup did not begin with clear perception.");
            Physics.SyncTransforms();
            deadline = Now + 2d;
            stage = 10;
        }

        private static void ValidateProfiles()
        {
            foreach (string id in new[] { AvoidProfile, FleeProfile, FightProfile })
            {
                ActorProfileDefinition profile = GameDataManager.Instance.Database.GetActorProfile(id);
                Require(profile?.navigation != null && profile.visual_perception != null && profile.encounter_ai != null,
                    "M41.1 profile lacks declared capability blocks: " + id);
            }
            ActorProfileDefinition target = GameDataManager.Instance.Database.GetActorProfile(ArmoredTargetProfile);
            Require(target?.navigation != null && target.initial_equipment?.Length == 1,
                "M41.1 armored target profile lacks its navigation/equipment fixture.");
        }

        private static bool EquippedFirearm(out ItemInstance firearm)
        {
            return WeaponCombatService.TryGetEquippedWeapon(ai.GetComponent<ActorItemOwnershipComponent>(),
                out firearm, out _, out FirearmProfileDefinition profile, out _) && profile != null;
        }

        private static bool CombatInvariantsHold() =>
            EquippedFirearm(out ItemInstance firearm) && firearm.LoadedRounds == roundsAfterFirstShot &&
            threat.GetComponent<ActorMedicalStateComponent>().WoundCount == woundsAfterFirstShot &&
            controller.AttackCount == attacksAfterFirstShot;

        private static ActorRuntimeIdentity Spawn(string profile, Vector3 position, Quaternion rotation, string name)
        {
            Require(ActorSpawnService.TrySpawn(profile, position, rotation, out ActorRuntimeIdentity identity, out string error),
                name + " spawn failed: " + error);
            identity.name = name;
            return identity;
        }

        private static HumanEncounterAIController RequireController(ActorRuntimeIdentity actor)
        {
            HumanEncounterAIController result = actor.GetComponent<HumanEncounterAIController>();
            Require(result != null && result.IsConfigured, "Actor profile did not attach configured Human Encounter AI.");
            return result;
        }

        private static void CompleteRun()
        {
            RemoveDiagnosticActors();
            barrier.SetActive(true);
            SessionState.SetString(PhaseKey, Finish);
            EditorApplication.ExitPlaymode();
        }

        private static void FinalizeRun()
        {
            string failure = SessionState.GetString(ErrorKey, string.Empty);
            bool success = string.IsNullOrEmpty(failure) && !EditorSceneManager.GetActiveScene().isDirty;
            if (success)
                Debug.Log("M41.1 Human Encounter AI Diagnostics: PASS");
            else
                Debug.LogError("M41.1 Human Encounter AI Diagnostics: FAIL\n- " +
                    (string.IsNullOrEmpty(failure) ? "Diagnostic dirtied SampleScene." : failure));
            ClearRun();
            if (Application.isBatchMode)
                EditorApplication.Exit(success ? 0 : 1);
        }

        private static void RemoveDiagnosticActors() => RemoveNamedRuntimeActors("M41.1 Diagnostic ");

        private static void RemoveNamedRuntimeActors(string prefix)
        {
            foreach (ActorRuntimeIdentity actor in ActorRuntimeRegistry.ActiveRepresentations
                         .Where(value => value != null && value.OriginKind == ActorOriginKind.Runtime &&
                                         value.name.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
                ActorSpawnService.TryRemoveRuntimeRepresentationForRestore(actor.ActorInstanceId, out _);
        }

        private static Transform RequireFixtureRoot()
        {
            GameObject root = GameObject.Find(M41SampleSceneNavigationTools.FixtureRootName);
            Require(root != null, "M41.0 navigation fixture is missing.");
            return root.transform;
        }

        private static Transform Marker(string name)
        {
            Transform marker = M41SampleSceneNavigationTools.FindMarker(name);
            Require(marker != null, "M41.0 fixture marker is missing: " + name);
            return marker;
        }

        private static GameObject RequireBarrier()
        {
            GameObject result = M41SampleSceneNavigationTools.FindBarrier();
            Require(result != null && result.GetComponent<Collider>() != null, "M41.0 barrier fixture is missing.");
            return result;
        }

        private static Quaternion Face(Vector3 direction)
        {
            Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up);
            return flat.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(flat) : Quaternion.identity;
        }

        private static bool Near(Vector3 left, Vector3 right, float tolerance) => Vector3.Distance(left, right) <= tolerance;
        private static bool InsideExpandedBarrier(Vector3 point, float expansion)
        {
            Bounds bounds = RequireBarrier().GetComponent<Collider>().bounds;
            bounds.Expand(expansion * 2f);
            return bounds.Contains(point);
        }

        private static void SetEncounterLosBarrierHeight(GameObject targetBarrier, bool tall)
        {
            Vector3 scale = targetBarrier.transform.localScale;
            scale.y = tall ? 5f : 2.5f;
            targetBarrier.transform.localScale = scale;
        }
        private static double Now => Time.realtimeSinceStartupAsDouble;

        private static void Require(bool condition, string failure)
        {
            if (!condition)
                throw new InvalidOperationException(failure);
        }

        private static void ClearRun()
        {
            SessionState.EraseString(PhaseKey);
            SessionState.EraseString(ErrorKey);
            ai = null;
            threat = null;
            controller = null;
            barrier = null;
            stage = 0;
            deadline = 0d;
        }
    }
}
