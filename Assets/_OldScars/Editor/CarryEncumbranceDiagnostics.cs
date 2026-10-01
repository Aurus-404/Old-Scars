using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using OldScars.Core;
using OldScars.Core.Actors;
using OldScars.Core.Data;
using OldScars.Core.Interactions;
using OldScars.Core.Items;
using OldScars.Core.Persistence;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OldScars.Editor
{
    [InitializeOnLoad]
    public static class CarryEncumbranceDiagnostics
    {
        private const string PhaseKey = "OldScars.IMPL0020.Phase";
        private const string NavOnlyKey = "OldScars.IMPL0020.NavigationOnly";
        private const string ErrorKey = "OldScars.IMPL0020.Error";
        private const string Ammo = "core:ammo_303_british_01";
        private static IEnumerator run;
        private static double editorDeadline;

        static CarryEncumbranceDiagnostics() => EditorApplication.update += Continue;

        [MenuItem("Old Scars/Diagnostics/Actors/Run Carry Encumbrance IMPL-0020")]
        public static void Run()
        {
            SessionState.SetBool(NavOnlyKey, false);
            Start();
        }

        public static void RunNavigation()
        {
            SessionState.SetBool(NavOnlyKey, true);
            Start();
        }

        private static void Start()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Carry diagnostic requires idle compiled Edit Mode.");
            SessionState.SetString(ErrorKey, string.Empty);
            SessionState.SetString(PhaseKey, "enter");
            editorDeadline = EditorApplication.timeSinceStartup + 120d;
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void Continue()
        {
            string phase = SessionState.GetString(PhaseKey, string.Empty);
            if (string.IsNullOrEmpty(phase)) return;
            try
            {
                if (phase == "enter" && EditorApplication.isPlaying && Time.frameCount >= 5 &&
                    GameDataManager.Instance?.IsReady == true && WorldClock.Current != null)
                {
                    WorldClock.Current.AdvanceDuringGameplay = false;
                    Time.captureDeltaTime = 1f / 60f;
                    run = Cases();
                    SessionState.SetString(PhaseKey, "running");
                }
                else if (phase == "running" && EditorApplication.isPlaying && !run.MoveNext())
                {
                    run = null;
                    SessionState.SetString(PhaseKey, "finish");
                    Time.captureDeltaTime = 0f;
                    EditorApplication.ExitPlaymode();
                }
                else if (phase == "finish" && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    string error = SessionState.GetString(ErrorKey, string.Empty);
                    Require(!EditorSceneManager.GetActiveScene().isDirty, "Fixture left the scene dirty.");
                    SessionState.EraseString(PhaseKey);
                    SessionState.EraseString(ErrorKey);
                    SessionState.EraseBool(NavOnlyKey);
                    if (error.Length == 0) Debug.Log("IMPL-0020 Carry Encumbrance Diagnostics: PASS");
                    else Debug.LogError("IMPL-0020 Carry Encumbrance Diagnostics: FAIL: " + error);
                    if (Application.isBatchMode) EditorApplication.Exit(error.Length == 0 ? 0 : 1);
                }
                if (phase != "finish" && editorDeadline > 0d && EditorApplication.timeSinceStartup > editorDeadline)
                    throw new InvalidOperationException("Carry fixture exceeded its bounded runner timeout.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetString(ErrorKey, exception.Message);
                SessionState.SetString(PhaseKey, "finish");
                run = null;
                if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
            }
        }

        private static IEnumerator Cases()
        {
            if (!SessionState.GetBool(NavOnlyKey, false))
            {
            // Real content, ownership and CharacterController; isolated runtime objects, no scene edits.
            GameObject body = new GameObject("IMPL0020 Player fixture");
            body.transform.position = new Vector3(0f, 100f, 0f);
            InventoryComponent inventory = body.AddComponent<InventoryComponent>();
            body.AddComponent<ActorItemOwnershipComponent>();
            ActorCarryWeightComponent carry = body.AddComponent<ActorCarryWeightComponent>();
            body.AddComponent<CharacterController>();
            ActorStaminaComponent stamina = body.AddComponent<ActorStaminaComponent>();
            PlayerMovementController movement = body.AddComponent<PlayerMovementController>();
            Require(carry.CarryCapacityKg == 30d, "Shared initial capacity changed from 30 kg.");
            double roundMass = GameDataManager.Instance.Database.GetItem(Ammo).physical.weight_kg ?? 0f;
            Require(roundMass > 0d, "Ammo fixture needs positive item mass.");
            Set(carry, "baseCarryCapacityKg", (float)(roundMass * 1200d));
            int[] counts = { 600, 900, 1050, 1200, 1201 };
            CarryWeightState[] states = { CarryWeightState.Normal, CarryWeightState.Normal,
                CarryWeightState.Encumbered, CarryWeightState.Encumbered, CarryWeightState.Overloaded };
            float previousFactor = 1f;
            for (int i = 0; i < counts.Length; i++)
            {
                Clear(inventory);
                Require(inventory.AddItemByDefinitionId(Ammo, counts[i]) != null, "Normal Add refused valid overweight content.");
                CarryWeightSnapshot snapshot = carry.GetSnapshot();
                Require(snapshot.IsValid && snapshot.State == states[i], "Threshold state differs at quantity " + counts[i]);
                Require(i < 2 ? snapshot.LocomotionFactor == 1f : snapshot.LocomotionFactor < previousFactor,
                    "Threshold factor is not normal/progressively reduced.");
                if (i == 3) Require(snapshot.LocomotionFactor > 0f, "Exactly 100% lost physical locomotion.");
                movement.SetMovementDirection(Vector3.right);
                movement.SetSprintRequested(i == 4);
                Vector3 before = body.transform.position;
                Quaternion facing = body.transform.rotation;
                float staminaBefore = stamina.CurrentStamina;
                double until = Time.timeAsDouble + 0.25d;
                while (Time.timeAsDouble < until) yield return null;
                float travel = Flat(before, body.transform.position);
                Require(i == 4 ? travel < 0.0001f : travel > 0.02f, $"Player displacement failed: qty={counts[i]} travel={travel:F6} factor={snapshot.LocomotionFactor:F6} speed={movement.EffectiveMovementSpeed:F6} dt={Time.deltaTime:F6}");
                if (i == 4)
                {
                    Require(body.transform.position.y < before.y && Quaternion.Angle(facing, body.transform.rotation) < 1f,
                        "Overload stopped gravity or changed a settled facing.");
                    movement.SetMovementDirection(Vector3.forward);
                    until = Time.timeAsDouble + 0.25d;
                    while (Time.timeAsDouble < until) yield return null;
                    Require(Quaternion.Angle(facing, body.transform.rotation) > 10f &&
                        !movement.IsSprinting && stamina.CurrentStamina >= staminaBefore,
                        "Overload stopped rotation or caused fake sprint/stamina drain.");
                }
                Debug.Log($"[IMPL0020][PLAYER] qty={counts[i]} ratio={snapshot.LoadRatio:F6} state={snapshot.State} factor={snapshot.LocomotionFactor:F3} travel={travel:F4}");
                previousFactor = snapshot.LocomotionFactor;
            }
            Require(DroppedWorldItemSpawner.TryDrop(inventory, inventory.Entries.Count - 1,
                inventory.Entries.Last().Quantity, "core:drop", "Drop", out string dropError), "Player drop failed: " + dropError);
            Vector3 recoveryStart = body.transform.position;
            double recoveryEnd = Time.timeAsDouble + 0.3d;
            while (Time.timeAsDouble < recoveryEnd) yield return null;
            Require(carry.State != CarryWeightState.Overloaded && Flat(recoveryStart, body.transform.position) > 0.02f,
                "Player did not automatically recover its retained movement intent after drop.");
            movement.ClearMovement();
            StorageCases(inventory, carry);
            UnityEngine.Object.Destroy(body);
            yield return null;

            // Capture/apply the real Current Slice seam without a schema or duplicated Carry state.
            PersistenceCases();
            OldScars.EditorTools.InventoryInteractionUxDiagnostics.Run();
            }

            Transform fixture = GameObject.Find(M41SampleSceneNavigationTools.FixtureRootName)?.transform;
            Require(fixture != null, "Warm SampleScene lacks the existing navigation fixture.");
            Vector3 origin = fixture.TransformPoint(new Vector3(-4f, 0f, -4f));
            Vector3 destination = fixture.TransformPoint(new Vector3(-4f, 0f, 4f));
            Require(ActorSpawnService.TrySpawn("core:debug_encounter_fight_01", origin, Quaternion.identity,
                out ActorRuntimeIdentity npc, out string spawnError), "Runtime spawn: " + spawnError);
            ActorCarryWeightComponent npcCarry = npc.GetComponent<ActorCarryWeightComponent>();
            InventoryComponent npcInventory = npc.GetComponent<InventoryComponent>();
            ActorNavigationController nav = npc.GetComponent<ActorNavigationController>();
            ActorBehaviorController behavior = npc.GetComponent<ActorBehaviorController>();
            HumanEncounterAIController ai = npc.GetComponent<HumanEncounterAIController>();
            Require(npcCarry != null && npcCarry.CarryCapacityKg == 30d, "Runtime NPC lacks shared 30 kg Carry.");
            ActorThreatAcquisitionController acquisition = npc.GetComponent<ActorThreatAcquisitionController>();
            if (acquisition != null) acquisition.enabled = false;
            ai.enabled = false;
            Require(behavior.EnterEncounter("IMPL0020 fixture"), "Could not reserve existing Behavior authority.");
            Require(npcInventory.AddItemByDefinitionId(Ammo, 100) != null, "NPC fixture load failed.");
            Set(npcCarry, "baseCarryCapacityKg", (float)(npcCarry.CurrentWeightKg / 0.875d));
            Require(nav.TryNavigate(destination, out _), "NPC valid path was rejected.");
            yield return null;
            Require(Math.Abs(nav.Agent.speed - nav.ConfiguredSpeed * npcCarry.LocomotionFactor) < 0.001f,
                "NPC uses a different or cumulative locomotion factor.");
            Vector3 slowStart = npc.transform.position;
            double untilNpc = Time.timeAsDouble + 0.4d;
            while (Time.timeAsDouble < untilNpc) yield return null;
            Require(Flat(slowStart, npc.transform.position) > 0.02f, "Encumbered NPC never translated.");
            Require(npcInventory.AddItemByDefinitionId(Ammo, 1000) != null && npcCarry.State == CarryWeightState.Overloaded,
                "NPC incoming content was weight-clamped.");
            yield return null;
            Vector3 pausedAt = npc.transform.position;
            untilNpc = Time.timeAsDouble + 0.4d;
            while (Time.timeAsDouble < untilNpc) yield return null;
            Require(Flat(pausedAt, npc.transform.position) < 0.001f && nav.State == ActorNavigationState.Moving &&
                nav.HasDestination && nav.Agent.hasPath && nav.Agent.isStopped && nav.Failure == ActorNavigationFailure.None,
                "Overload lost the NPC path, translated or became a navigation failure.");
            Clear(npcInventory);
            untilNpc = Time.timeAsDouble + 0.4d;
            while (Time.timeAsDouble < untilNpc) yield return null;
            Require(Flat(pausedAt, npc.transform.position) > 0.02f && !nav.Agent.isStopped,
                "Unloading did not resume the same NPC order.");
            Debug.Log("[IMPL0020][NPC] shared component/factor, real path pause and automatic resume: PASS");

            // A real Search order, preconditioned anchor and shortened deadline. No substitute AI/navigation.
            Require(ActorSpawnService.TrySpawn("core:debug_navigation_npc_01",
                destination, Quaternion.identity,
                out ActorRuntimeIdentity target, out spawnError), "Search target spawn: " + spawnError);
            target.GetComponent<ActorBehaviorController>().EnterEncounter("Fixture stationary target");
            target.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
            target.transform.position = fixture.TransformPoint(new Vector3(0f, 0f, 85f));
            Physics.SyncTransforms();
            Require(ai.TryAssignThreat(target, out string threatError), "Search threat setup: " + threatError);
            Set(ai, "lastKnownPosition", destination);
            Set(ai, "hasLastKnownPosition", true);
            Invoke(ai, "BeginSearch", Time.timeAsDouble);
            Require(ai.State == HumanEncounterAIState.Searching && nav.State == ActorNavigationState.Moving,
                "Production Search did not create a valid navigation order.");
            Require(npcInventory.AddItemByDefinitionId(Ammo, 1400) != null, "Search overload setup failed.");
            double originalDeadline = Time.timeAsDouble + 0.75d;
            Set(ai, "searchNavigationDeadline", originalDeadline);
            ai.enabled = true;
            double blockedUntil = Time.timeAsDouble + 1.3d;
            while (Time.timeAsDouble < blockedUntil) yield return null;
            Require(Time.timeAsDouble > originalDeadline && ai.State == HumanEncounterAIState.Searching &&
                ai.LastSearchOutcome == HumanEncounterSearchOutcome.Navigating && nav.State == ActorNavigationState.Moving,
                "Temporary overload consumed Search's bounded travel deadline.");
            Clear(npcInventory);
            Vector3 searchRecoveryStart = npc.transform.position;
            untilNpc = Time.timeAsDouble + 0.4d;
            while (Time.timeAsDouble < untilNpc) yield return null;
            Require(Flat(searchRecoveryStart, npc.transform.position) > 0.02f && ai.State == HumanEncounterAIState.Searching,
                "Search did not continue after unloading.");
            Set(ai, "searchNavigationDeadline", Time.timeAsDouble - 0.01d);
            Invoke(ai, "UpdateSearch", Time.timeAsDouble);
            Require(ai.LastSearchOutcome == HumanEncounterSearchOutcome.Failed,
                "A legitimate unblocked Search travel deadline became unbounded.");
            Require(!nav.TryNavigate(new Vector3(10000f, 0f, 10000f), out _) && nav.State == ActorNavigationState.Failed,
                "A real off-NavMesh destination lost normal failure semantics.");
            Debug.Log("[IMPL0020][SEARCH] blocked beyond original deadline, recovery, unblocked deadline and path failure: PASS");
            ActorSpawnService.TryRemoveRuntimeRepresentationForRestore(npc.ActorInstanceId, out _);
            ActorSpawnService.TryRemoveRuntimeRepresentationForRestore(target.ActorInstanceId, out _);
            yield return null;
        }

        private static void StorageCases(InventoryComponent target, ActorCarryWeightComponent carry)
        {
            Clear(target);
            Set(carry, "baseCarryCapacityKg", 0.1f);
            GameObject sourceRoot = new GameObject("IMPL0020 Storage fixture");
            InventoryComponent source = sourceRoot.AddComponent<InventoryComponent>();
            ItemInstance ammo = source.AddItemByDefinitionId(Ammo, 100);
            Require(ammo != null, "External content fixture failed.");
            InventoryMutationResult transfer = GridStorageTransferService.TransferStackAuto(source, target, ammo.InstanceId, default);
            Require(transfer.Success && transfer.AffectedQuantity == 100 && source.IsEmpty && carry.State == CarryWeightState.Overloaded &&
                target.Entries.Single().Item.InstanceId == ammo.InstanceId, "Full overweight transfer did not commit exact quantity/identity.");
            InventoryMutationResult invalid = GridStorageTransferService.TransferQuantityAuto(target, source, ammo.InstanceId, 101, true, default);
            Require(!invalid.Success && target.Entries.Single().Quantity == 100 && source.IsEmpty,
                "Insufficient-quantity rejection was not atomic.");
            ItemInstance pack = target.AddItemByDefinitionId("core:small_backpack_01", 1);
            ItemOwnedStorageRuntime bag = null;
            Require(pack != null && ItemOwnedStorageRegistry.Instance.TryResolveOwnedStorage(pack.InstanceId, out bag),
                "Owned-storage fixture failed.");
            double mass = carry.CurrentWeightKg;
            Require(GridStorageTransferService.TransferStackAuto(target, bag, ammo.InstanceId, default).Success &&
                Math.Abs(carry.CurrentWeightKg - mass) < 0.000001d, "Same-owner bag move changed derived mass.");
            ItemInstance incoming = source.AddItemByDefinitionId(Ammo, 50);
            Require(GridStorageTransferService.TransferStackAuto(source, bag, incoming.InstanceId, default).Success &&
                carry.CurrentWeightKg > mass, "External owned-storage ingress was weight-vetoed.");
            ItemInstance otherPack = source.AddItemByDefinitionId("core:small_backpack_01", 1);
            Require(!GridStorageTransferService.TransferStackAuto(source, bag, otherPack.InstanceId, default).Success &&
                source.TryGetEntryByInstanceId(otherPack.InstanceId, out _, out _), "Legitimate no-nesting guard stopped rejecting atomically.");
            ItemInstance rifle = source.AddItemByDefinitionId("core:lee_enfield_rifle_01", 1);
            Require(GridStorageTransferService.PreviewTransferQuantityAuto(source, bag, rifle.InstanceId, 999, default).Failure ==
                InventoryMutationResult.MutationFailure.InsufficientQuantity, "Stack quantity guard disappeared.");
            // Exercise the real grid rejection with a too-small storage; no content/schema edits.
            GameObject tinyRoot = new GameObject("IMPL0020 Tiny grid fixture");
            tinyRoot.SetActive(false);
            InventoryComponent tiny = tinyRoot.AddComponent<InventoryComponent>();
            Set(tiny, "useGridLayout", true); Set(tiny, "gridWidth", 1); Set(tiny, "gridHeight", 1);
            tinyRoot.SetActive(true);
            InventoryMutationResult gridReject = GridStorageTransferService.TransferStackAuto(source, tiny, rifle.InstanceId, default);
            Require(!gridReject.Success && tiny.IsEmpty && source.TryGetEntryByInstanceId(rifle.InstanceId, out _, out _),
                "Legitimate grid rejection lost source/target atomicity.");
            Debug.Log("[IMPL0020][STORAGE] overweight exact commit/identity, same-root mass, external bag ingress, no-nesting/quantity/grid rejection: PASS");
            UnityEngine.Object.Destroy(sourceRoot); UnityEngine.Object.Destroy(tinyRoot);
        }

        private static void PersistenceCases()
        {
            ActorInteractionContext player = UnityEngine.Object.FindObjectsByType<ActorInteractionContext>(FindObjectsInactive.Exclude)
                .Single(value => value.ActorTags.Contains("player"));
            ActorCarryWeightComponent carry = player.GetComponent<ActorCarryWeightComponent>();
            InventoryComponent inventory = player.GetComponent<InventoryComponent>();
            CurrentSliceResult initial = CurrentSliceSnapshotService.Capture();
            Require(initial.Success, "Initial persistence capture failed: " + initial.Failure);
            float originalCapacity = carry.BaseCarryCapacityKg;
            double before = carry.CurrentWeightKg;
            Set(carry, "baseCarryCapacityKg", (float)(before + 0.5d));
            Require(inventory.AddItemByDefinitionId(Ammo, 50) != null && carry.State == CarryWeightState.Overloaded,
                "Could not prepare overweight restore content.");
            CurrentSliceResult loaded = CurrentSliceSnapshotService.Capture();
            Require(loaded.Success, "Overloaded capture failed: " + loaded.Failure);
            double mass = carry.CurrentWeightKg;
            CurrentSliceLoadResult result = CurrentSliceLoadService.LoadPayload(CurrentSliceSnapshotService.ToPayload(loaded.Snapshot), "IMPL0020-overloaded");
            Require(result.Success && carry.State == CarryWeightState.Overloaded && carry.LocomotionFactor == 0f &&
                Math.Abs(carry.CurrentWeightKg - mass) < 0.000001d, "Overweight restore discarded/clamped items: " + result.Failure);
            CurrentSliceResult restored = CurrentSliceSnapshotService.Capture();
            Require(restored.Success && CurrentSliceSnapshotService.Compare(loaded.Snapshot, restored.Snapshot).Equivalent,
                "Overloaded restore changed item identities, quantities or owned state.");
            int ammoIndex = inventory.Entries.ToList().FindIndex(entry => entry.DefinitionId == Ammo && entry.Quantity >= 50);
            Require(ammoIndex >= 0 && DroppedWorldItemSpawner.TryDrop(inventory, ammoIndex, 50, "core:drop", "Drop", out _),
                "Restored overloaded actor could not drop content.");
            Require(carry.State != CarryWeightState.Overloaded && carry.LocomotionFactor > 0f, "Restored actor did not recover after drop.");
            EquipmentCases(player, inventory, carry);
            Set(carry, "baseCarryCapacityKg", originalCapacity);
            Require(CurrentSliceLoadService.LoadPayload(CurrentSliceSnapshotService.ToPayload(initial.Snapshot), "IMPL0020-cleanup").Success,
                "Persistence fixture cleanup failed.");
            Debug.Log("[IMPL0020][PERSISTENCE] real overloaded Current Slice capture/apply, exact item preservation, drop recovery: PASS");
        }

        private static void EquipmentCases(ActorInteractionContext player, InventoryComponent inventory, ActorCarryWeightComponent carry)
        {
            Set(carry, "baseCarryCapacityKg", 0.1f);
            ActorEquipmentComponent equipment = player.GetComponent<ActorEquipmentComponent>();
            string[] slots = { ActorEquipmentComponent.HandLeftSlotId, ActorEquipmentComponent.HandRightSlotId };
            ItemInstance first = inventory.AddItemByDefinitionId("core:lee_enfield_rifle_01", 1);
            Require(first != null, "Equipment fixture content failed.");
            if (slots.Any(slot => equipment.GetEquippedInstance(slot) != null))
            {
                EquipmentReplacementPlan replacement = equipment.PreviewEquipReplacing(first.InstanceId, slots);
                Require(replacement.Success && equipment.EquipReplacing(replacement).Success, "Overloaded personal replacement failed.");
            }
            else
            {
                EquipmentPreview preview = equipment.PreviewEquip(first.InstanceId, slots);
                Require(preview.Success && equipment.Equip(preview).Success, "Overloaded equip failed.");
            }
            Require(DroppedWorldItemSpawner.TryDrop(equipment, first.InstanceId, inventory, "core:drop", "Drop", out _), "Equipment drop failed.");
            WorldItemPickup firstWorld = FindWorld(first.InstanceId);
            EquipmentPreview worldPreview = WorldCall<EquipmentPreview>("PreviewEquip", equipment, firstWorld, first.InstanceId, slots);
            Require(worldPreview.Success && WorldCall<EquipmentMutationResult>("Equip", equipment, firstWorld, worldPreview,
                player, firstWorld.GetComponent<WorldObjectTags>()).Success && equipment.IsEquipped(first.InstanceId),
                "World equip still vetoes overload or loses exact identity.");
            ItemInstance second = inventory.AddItemByDefinitionId("core:lee_enfield_rifle_01", 1);
            Require(second != null && inventory.TryGetEntryByInstanceId(second.InstanceId, out _, out _), "World replacement fixture failed.");
            inventory.TryGetEntryByInstanceId(second.InstanceId, out int index, out _);
            Require(DroppedWorldItemSpawner.TryDrop(inventory, index, 1, "core:drop", "Drop", out _), "World replacement drop failed.");
            WorldItemPickup secondWorld = FindWorld(second.InstanceId);
            EquipmentPreview invalidSlot = WorldCall<EquipmentPreview>("PreviewEquip", equipment, secondWorld, second.InstanceId, new[] { "core:back" });
            Require(!invalidSlot.Success, "Legitimate world Equipment slot compatibility was removed.");
            EquipmentReplacementPlan worldReplacement = WorldCall<EquipmentReplacementPlan>("PreviewEquipReplacing", equipment, secondWorld, second.InstanceId, slots);
            Require(worldReplacement.Success && WorldCall<EquipmentMutationResult>("EquipReplacing", equipment, secondWorld,
                worldReplacement, player, secondWorld.GetComponent<WorldObjectTags>()).Success &&
                equipment.IsEquipped(second.InstanceId) && inventory.TryGetEntryByInstanceId(first.InstanceId, out _, out _),
                "Overloaded world replacement lost the exact incoming/displaced instances.");
            Require(player.GetComponent<ActorItemOwnershipComponent>().ValidateUniqueOwnership(out _), "Equipment replacement duplicated ownership.");
            GameObject external = new GameObject("IMPL0020 External Equipment destination");
            InventoryComponent destination = external.AddComponent<InventoryComponent>();
            ActorCarryWeightComponent destinationCarry = external.AddComponent<ActorCarryWeightComponent>();
            Set(destinationCarry, "baseCarryCapacityKg", 0.1f);
            EquipmentStorageTransferPlan transfer = equipment.PreviewTransferEquippedToStorage(second.InstanceId, destination, default);
            Require(transfer.Success && equipment.TransferEquippedToStorage(destination, transfer, default).Success &&
                destinationCarry.State == CarryWeightState.Overloaded && destination.Entries.Single().Item.InstanceId == second.InstanceId,
                "Equipment-to-external actor transfer still vetoes Carry.");
            UnityEngine.Object.Destroy(external);
            Debug.Log("[IMPL0020][EQUIPMENT] overloaded personal/world equip+replacement, slot guard, exact identity/ownership and external overweight equipment transfer: PASS");
        }

        private static WorldItemPickup FindWorld(string id) => UnityEngine.Object.FindObjectsByType<WorldItemPickup>(FindObjectsInactive.Exclude)
            .Single(value => value.GridStorageEntries.Any(entry => entry.Item.InstanceId == id));
        private static T WorldCall<T>(string method, params object[] args) => (T)typeof(InventoryComponent).Assembly
            .GetType("OldScars.Core.Items.WorldItemEquipmentTransactionService")
            .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);

        private static void Clear(InventoryComponent inventory)
        {
            for (int i = inventory.Entries.Count - 1; i >= 0; i--)
                Require(inventory.TryRemoveItemAt(i, inventory.Entries[i].Quantity), "Fixture could not remove owned inventory content.");
        }
        private static float Flat(Vector3 a, Vector3 b) => Vector3.ProjectOnPlane(a - b, Vector3.up).magnitude;
        private static void Set(object owner, string field, object value) => owner.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
        private static void Invoke(object owner, string method, params object[] args) => owner.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);
        private static void Require(bool value, string failure) { if (!value) throw new InvalidOperationException(failure); }
    }
}
