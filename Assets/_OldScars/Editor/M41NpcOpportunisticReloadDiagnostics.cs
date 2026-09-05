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
    public static class M41NpcOpportunisticReloadDiagnostics
    {
        private const string PhaseKey = "OldScars.M41.OpportunisticReload.Phase";
        private const string ErrorKey = "OldScars.M41.OpportunisticReload.Error";
        private const string Enter = "enter";
        private const string Running = "running";
        private const string Finish = "finish";
        private const string FightProfile = "core:debug_encounter_fight_01";
        private const string TargetProfile = "core:debug_navigation_npc_01";
        private const string AmmoItemId = "core:ammo_303_british_01";

        private static ActorRuntimeIdentity ambient;
        private static ActorRuntimeIdentity ambientThreat;
        private static ActorRuntimeIdentity encounter;
        private static ActorRuntimeIdentity encounterThreat;
        private static ActorRuntimeIdentity invalidation;
        private static GameObject barrier;
        private static int stage;
        private static double stageStartedAt;
        private static double x1Duration;
        private static double x100Duration;
        private static double x100StartedAt;
        private static int ammoBefore;
        private static int reloadsBefore;
        private static string pendingWeapon;
        private static double pendingCompletion;

        static M41NpcOpportunisticReloadDiagnostics()
        {
            EditorApplication.update -= Continue;
            EditorApplication.update += Continue;
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Opportunistic reload diagnostics require idle compiled Edit Mode.");
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
            FirearmProfileDefinition leeEnfield = GameDataManager.Instance.Database.GetFirearmProfile("core:lee_enfield_rifle_01_profile");
            Require(leeEnfield != null && leeEnfield.magazine_capacity == 10 && Near(leeEnfield.reload_duration, 2.5f),
                "Lee-Enfield profile does not provide the expected 10-round, 2.5-second contract.");
            WorldClock.Current.AdvanceDuringGameplay = false;
            WorldClock.Current.ResetDebugTimeMultiplier();
            x100StartedAt = double.NaN;
            barrier = M41SampleSceneNavigationTools.FindBarrier();
            Require(barrier != null, "Navigation LOS barrier fixture is unavailable.");
            barrier.SetActive(false);
            ambient = Spawn(FightProfile, FixturePoint(-7f, -6f), Quaternion.identity, "Opportunistic Reload Ambient");
            ambient.GetComponent<ActorBehaviorController>().ConfigureAmbient(81101L);
            DisableAcquisition(ambient);
            SetRounds(ambient, 7);
            AddAmmo(ambient, 3);
            ammoBefore = AmmoCount(ambient);
            reloadsBefore = Controller(ambient).ReloadCount;
            SetStage(1);
        }

        private static void TickRun()
        {
            if (Now - stageStartedAt > 12d)
                throw new InvalidOperationException("Opportunistic reload diagnostic stage timed out: " + stage);
            switch (stage)
            {
                case 1: VerifyAmbientPending(); break;
                case 2: VerifyAmbientComplete(); break;
                case 3: VerifyNineRoundTopOff(); break;
                case 4: VerifySafeNoOpAndThreatCancellation(); break;
                case 5: VerifyFreshCombatAndSearchPartial(); break;
                case 6: VerifyEmptySearchContinuation(); break;
                case 7: VerifyReacquisitionAndComplete(); break;
                case 8: VerifyInvalidations(); break;
                case 9: BeginWeaponInvalidation(); break;
                case 10: StartX100Reload(); break;
                case 11: WaitForX100(); break;
            }
        }

        private static void VerifyAmbientPending()
        {
            HumanEncounterAIController controller = Controller(ambient);
            if (!controller.IsReloadPending)
                return;
            Require(controller.PendingReloadPurpose == "AmbientTopOff" && Rounds(ambient) == 7 && AmmoCount(ambient) == ammoBefore,
                "Ambient 7/10 top-off did not remain side-effect free before completion.");
            pendingWeapon = controller.ReloadWeaponInstanceId;
            pendingCompletion = controller.ReloadCompletionTime;
            SetStage(2);
        }

        private static void VerifyAmbientComplete()
        {
            HumanEncounterAIController controller = Controller(ambient);
            double elapsed = Now - stageStartedAt;
            if (elapsed < 2.25d)
            {
                Require(controller.IsReloadPending && Rounds(ambient) == 7 && AmmoCount(ambient) == ammoBefore,
                    "Ambient reload committed before its 2.5-second real-time duration.");
                return;
            }
            if (controller.IsReloadPending)
                return;
            x1Duration = elapsed;
            Require(Rounds(ambient) == 10 && AmmoCount(ambient) == ammoBefore - 3 &&
                    controller.ReloadCount == reloadsBefore + 1,
                "Ambient top-off did not commit exactly three owned rounds once.");
            SetRounds(ambient, 9);
            AddAmmo(ambient, 1);
            reloadsBefore = controller.ReloadCount;
            SetStage(3);
        }

        private static void VerifyNineRoundTopOff()
        {
            HumanEncounterAIController controller = Controller(ambient);
            if (controller.IsReloadPending)
            {
                Require(controller.PendingReloadPurpose == "AmbientTopOff" && Rounds(ambient) == 9,
                    "Ambient 9/10 did not start the same top-off policy.");
                return;
            }
            if (Rounds(ambient) != 10)
                return;
            Require(controller.ReloadCount == reloadsBefore + 1, "Ambient 9/10 did not complete exactly once.");
            int fullAmmo = AmmoCount(ambient);
            SetStage(4);
            Require(!controller.IsReloadPending && Rounds(ambient) == 10 && AmmoCount(ambient) == fullAmmo,
                "A full firearm started an unnecessary reload.");
            SetRounds(ambient, 7);
            RemoveAllAmmo(ambient);
            stageStartedAt = Now;
        }

        private static void VerifySafeNoOpAndThreatCancellation()
        {
            HumanEncounterAIController controller = Controller(ambient);
            if (Now - stageStartedAt < 0.35d)
            {
                Require(!controller.IsReloadPending && Rounds(ambient) == 7,
                    "A partial firearm without compatible owned ammo started a reload.");
                return;
            }
            AddAmmo(ambient, 3);
            if (!controller.IsReloadPending)
                return;
            ammoBefore = AmmoCount(ambient);
            ambientThreat = Spawn(TargetProfile, FixturePoint(-3f, -6f), Quaternion.identity, "Opportunistic Reload Ambient Threat");
            DisableAcquisition(ambientThreat);
            Require(controller.TryAssignThreat(ambientThreat, out string error), "Could not assign threat during Ambient top-off: " + error);
            Require(!controller.IsReloadPending && Rounds(ambient) == 7 && AmmoCount(ambient) == ammoBefore,
                "A newly assigned threat did not atomically cancel AmbientTopOff.");
            RemoveRuntime(ambient, ambientThreat);
            ambient = null;
            ambientThreat = null;
            StartEncounterFixture();
            SetStage(5);
        }

        private static void StartEncounterFixture()
        {
            Vector3 observer = Marker(M41SampleSceneNavigationTools.ObserverName).position;
            Vector3 target = Marker(M41SampleSceneNavigationTools.TargetName).position;
            barrier.SetActive(false);
            encounter = Spawn(FightProfile, observer, Face(target - observer), "Opportunistic Reload Encounter");
            encounterThreat = Spawn(TargetProfile, target, Quaternion.identity, "Opportunistic Reload Threat");
            DisableAcquisition(encounter);
            DisableAcquisition(encounterThreat);
            SetRounds(encounter, 7);
            AddAmmo(encounter, 15);
            Require(Controller(encounter).TryAssignThreat(encounterThreat, out string error), "Encounter fixture threat assignment failed: " + error);
        }

        private static void VerifyFreshCombatAndSearchPartial()
        {
            HumanEncounterAIController controller = Controller(encounter);
            if (controller.State != HumanEncounterAIState.Fighting)
                return;
            Require(!controller.IsReloadPending,
                "Fresh Fighting started an opportunistic partial top-off.");
            SetRounds(encounter, 7);
            barrier.SetActive(true);
            MoveOutsidePerception(encounterThreat);
            SetStage(6);
        }

        private static void VerifyEmptySearchContinuation()
        {
            HumanEncounterAIController controller = Controller(encounter);
            if (controller.State == HumanEncounterAIState.LostContact)
            {
                Require(!controller.IsReloadPending && Rounds(encounter) == 7,
                    "LostContact started a partial tactical top-off.");
                return;
            }
            if (controller.State != HumanEncounterAIState.Searching)
                return;
            Require(!controller.IsReloadPending && Rounds(encounter) == 7 &&
                    encounter.GetComponent<ActorBehaviorController>().Owner == ActorBehaviorOwner.Search,
                "Search started a partial top-off or lost Navigation ownership.");
            SetRounds(encounter, 0);
            stageStartedAt = Now;
            stage = 7;
        }

        private static void VerifyReacquisitionAndComplete()
        {
            HumanEncounterAIController controller = Controller(encounter);
            if (!controller.IsReloadPending)
                return;
            Require(controller.PendingReloadPurpose == "EmptyWeapon" &&
                    encounter.GetComponent<ActorBehaviorController>().Owner == ActorBehaviorOwner.Search,
                "Empty Search reload did not begin while Search retained Navigation ownership.");
            pendingWeapon = controller.ReloadWeaponInstanceId;
            pendingCompletion = controller.ReloadCompletionTime;
            ammoBefore = AmmoCount(encounter);
            reloadsBefore = controller.ReloadCount;
            barrier.SetActive(false);
            encounterThreat.transform.SetPositionAndRotation(Marker(M41SampleSceneNavigationTools.TargetName).position, Quaternion.identity);
            Physics.SyncTransforms();
            Require(controller.TryAssignThreat(encounterThreat, out string error), "Explicit Search reacquisition failed: " + error);
            Require(controller.IsReloadPending && controller.PendingReloadPurpose == "EmptyWeapon" &&
                    controller.ReloadWeaponInstanceId == pendingWeapon && Near((float)controller.ReloadCompletionTime, (float)pendingCompletion, 0.02f),
                "Empty reload was reset or cancelled by Search reacquisition.");
            stageStartedAt = Now;
            stage = 8;
        }

        private static void VerifyInvalidations()
        {
            HumanEncounterAIController controller = Controller(encounter);
            if (controller.IsReloadPending)
                return;
            Require(Rounds(encounter) > 0 && AmmoCount(encounter) == ammoBefore - 10 &&
                    controller.ReloadCount == reloadsBefore + 1,
                "Empty reload did not complete once after LostContact/Search/reacquisition continuity.");
            RemoveRuntime(encounter, encounterThreat);
            encounter = null;
            encounterThreat = null;
            invalidation = Spawn(FightProfile, FixturePoint(5f, -6f), Quaternion.identity, "Opportunistic Reload Invalidation");
            invalidation.GetComponent<ActorBehaviorController>().ConfigureAmbient(81102L);
            DisableAcquisition(invalidation);
            SetRounds(invalidation, 7);
            AddAmmo(invalidation, 3);
            SetStage(9);
        }

        private static void BeginWeaponInvalidation()
        {
            HumanEncounterAIController controller = Controller(invalidation);
            if (!controller.IsReloadPending)
                return;
            int before = AmmoCount(invalidation);
            string weaponId = controller.ReloadWeaponInstanceId;
            ActorEquipmentComponent equipment = invalidation.GetComponent<ActorEquipmentComponent>();
            Require(equipment.Unequip(equipment.PreviewUnequip(weaponId)).Success, "Could not unequip firearm during pending reload.");
            pendingWeapon = weaponId;
            ammoBefore = before;
            SetStage(10);
        }

        private static void StartX100Reload()
        {
            HumanEncounterAIController controller = Controller(invalidation);
            if (controller.IsReloadPending)
                return;
            Require(RoundsInInventory(invalidation, pendingWeapon) == 7 && AmmoCount(invalidation) == ammoBefore,
                "Weapon swap/equipment invalidation consumed phantom ammunition.");
            Require(WorldClock.Current.TrySetDebugTimeMultiplier(100f, out string failure), "Could not set WorldClock x100: " + failure);
            EquipFirearm(invalidation, pendingWeapon);
            SetRounds(invalidation, 7);
            SetStage(11);
        }

        private static void WaitForX100()
        {
            HumanEncounterAIController controller = Controller(invalidation);
            if (controller.IsReloadPending)
            {
                if (double.IsNaN(x100StartedAt))
                    x100StartedAt = Now;
                return;
            }
            if (double.IsNaN(x100StartedAt))
                return;
            x100Duration = Now - x100StartedAt;
            Require(Math.Abs(x1Duration - x100Duration) < 0.65d,
                "WorldClock x100 changed the NPC reload real-time duration.");
            CompleteRun();
        }

        private static void CompleteRun()
        {
            Debug.Log("M41 NPC Opportunistic Reload Diagnostics: PASS" +
                      $"\n  X1RealDuration: {x1Duration:0.###}" +
                      $"\n  X100RealDuration: {x100Duration:0.###}" +
                      "\n  AmbientTopOff: 7/10 and 9/10, exact owned ammo commit" +
                      "\n  EmptyWeapon: Fighting -> LostContact -> Search -> reacquisition" +
                      "\n  Invalidation: equipped firearm change without phantom ammo");
            CleanupRuntime();
            SessionState.SetString(PhaseKey, Finish);
            EditorApplication.ExitPlaymode();
        }

        private static void CleanupRuntime()
        {
            WorldClock.Current?.ResetDebugTimeMultiplier();
            if (barrier != null) barrier.SetActive(true);
            RemoveRuntime(ambient, ambientThreat, encounter, encounterThreat, invalidation);
        }

        private static void FinalizeRun()
        {
            string failure = SessionState.GetString(ErrorKey, string.Empty);
            bool success = string.IsNullOrEmpty(failure) && !EditorSceneManager.GetActiveScene().isDirty;
            if (!success)
                Debug.LogError("M41 NPC Opportunistic Reload Diagnostics: FAIL\n- " +
                               (string.IsNullOrEmpty(failure) ? "Diagnostic dirtied SampleScene." : failure));
            ClearRun();
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
        }

        private static ActorRuntimeIdentity Spawn(string profileId, Vector3 position, Quaternion rotation, string name)
        {
            Require(ActorSpawnService.TrySpawn(profileId, position, rotation, out ActorRuntimeIdentity actor, out string failure),
                name + " spawn failed: " + failure);
            actor.name = name;
            return actor;
        }

        private static void RemoveRuntime(params ActorRuntimeIdentity[] actors)
        {
            foreach (ActorRuntimeIdentity actor in actors.Where(value => value != null && value.IsRegistered).ToArray())
                ActorSpawnService.TryRemoveRuntimeRepresentationForRestore(actor.ActorInstanceId, out _);
        }

        private static void DisableAcquisition(ActorRuntimeIdentity actor)
        {
            ActorThreatAcquisitionController acquisition = actor?.GetComponent<ActorThreatAcquisitionController>();
            if (acquisition != null) acquisition.enabled = false;
        }

        private static void MoveOutsidePerception(ActorRuntimeIdentity actor)
        {
            NavMeshAgent agent = actor.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled) agent.enabled = false;
            actor.transform.SetPositionAndRotation(FixturePoint(0f, 60f), Quaternion.identity);
            Physics.SyncTransforms();
        }

        private static HumanEncounterAIController Controller(ActorRuntimeIdentity actor)
        {
            HumanEncounterAIController result = actor.GetComponent<HumanEncounterAIController>();
            Require(result != null && result.IsConfigured, actor.name + " lacks configured Human Encounter AI.");
            return result;
        }

        private static ItemInstance Firearm(ActorRuntimeIdentity actor)
        {
            Require(WeaponCombatService.TryGetEquippedWeapon(actor.GetComponent<ActorItemOwnershipComponent>(), out ItemInstance weapon,
                out _, out FirearmProfileDefinition firearm, out _) && firearm != null, actor.name + " lacks an equipped firearm.");
            return weapon;
        }

        private static void SetRounds(ActorRuntimeIdentity actor, int rounds)
        {
            ItemInstance weapon = Firearm(actor);
            Require(weapon.TrySetFirearmState(rounds == 0 ? null : "core:ammo_303_british_01_profile", rounds, out string failure),
                "Could not set firearm rounds: " + failure);
        }

        private static int Rounds(ActorRuntimeIdentity actor) => Firearm(actor).LoadedRounds;
        private static int RoundsInInventory(ActorRuntimeIdentity actor, string instanceId) =>
            actor.GetComponent<ActorItemOwnershipComponent>().GetAllOwnedEntries().Where(entry => entry.Item.InstanceId == instanceId)
                .Select(entry => entry.Item.LoadedRounds).DefaultIfEmpty(-1).Single();

        private static void AddAmmo(ActorRuntimeIdentity actor, int quantity)
        {
            Require(actor.GetComponent<InventoryComponent>().AddItemByDefinitionId(AmmoItemId, quantity) != null,
                "Could not seed compatible owned ammo.");
        }

        private static int AmmoCount(ActorRuntimeIdentity actor) => actor.GetComponent<ActorItemOwnershipComponent>().GetAllOwnedEntries()
            .Where(entry => entry.DefinitionId == AmmoItemId).Sum(entry => entry.Quantity);

        private static void RemoveAllAmmo(ActorRuntimeIdentity actor)
        {
            InventoryComponent inventory = actor.GetComponent<InventoryComponent>();
            foreach (ItemStorageEntry entry in inventory.Entries.Where(value => value.DefinitionId == AmmoItemId).ToArray())
            {
                Require(inventory.TryGetEntryByInstanceId(entry.Item.InstanceId, out int index, out ItemStorageEntry live) &&
                        inventory.TryRemoveItemAt(index, live.Quantity), "Could not remove owned ammo for no-ammo fixture.");
            }
        }

        private static void EquipFirearm(ActorRuntimeIdentity actor, string instanceId)
        {
            ActorEquipmentComponent equipment = actor.GetComponent<ActorEquipmentComponent>();
            EquipmentPreview preview = equipment.PreviewEquip(instanceId, new[]
            {
                ActorEquipmentComponent.HandLeftSlotId,
                ActorEquipmentComponent.HandRightSlotId
            });
            Require(preview.Success && equipment.Equip(preview).Success, "Could not re-equip firearm after invalidation.");
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

        private static void SetStage(int next)
        {
            stage = next;
            stageStartedAt = Now;
        }

        private static bool Near(float left, float right, float tolerance = 0.001f) => Mathf.Abs(left - right) <= tolerance;
        private static double Now => Time.realtimeSinceStartupAsDouble;
        private static void Require(bool condition, string failure) { if (!condition) throw new InvalidOperationException(failure); }
        private static void ClearRun()
        {
            SessionState.EraseString(PhaseKey);
            SessionState.EraseString(ErrorKey);
        }
    }
}
