using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using OldScars.Core;
using OldScars.Core.Actors;
using OldScars.Core.Combat;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Interactions;
using OldScars.Core.Items;
using OldScars.Core.Persistence;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OldScars.Editor
{
    [InitializeOnLoad]
    public static class M40CombatWeaponsDiagnostics
    {
        private const string Menu = "Old Scars/Diagnostics/Combat/Run M40.0 Combat Resolution & Weapons";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string PhaseKey = "OldScars.M40.Combat.Phase";
        private const string RootKey = "OldScars.M40.Combat.Root";
        private const string ErrorKey = "OldScars.M40.Combat.Error";
        private const string EnterA = "enter_a";
        private const string ExitA = "exit_a";
        private const string EnterB = "enter_b";
        private const string Finish = "finish";
        private const string InitialSlot = "m40_initial";
        private const string TargetSlot = "m40_target";
        private const string LegacySlot = "m40_legacy_unloaded";
        private const string NegativeSlot = "m40_invalid_negative";
        private const string OverflowSlot = "m40_invalid_overflow";
        private const string AmmoSlot = "m40_invalid_ammo";
        private const string NullSlot = "m40_invalid_null";
        private const string DuplicateSlot = "m40_invalid_duplicate";
        private const string RifleId = "core:lee_enfield_rifle_01";
        private const string AmmoItemId = "core:ammo_303_british_01";
        private const string AmmoProfileId = "core:ammo_303_british_01_profile";

        static M40CombatWeaponsDiagnostics() => EditorApplication.update += Continue;

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("M40.0 diagnostics require idle Edit Mode.");
            ClearSession();
            string root = Path.Combine(Path.GetTempPath(), "OldScars_M40_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            SessionState.SetString(RootKey, root);
            SessionState.SetString(ErrorKey, string.Empty);
            SessionState.SetString(PhaseKey, EnterA);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static bool ValidateRun() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;

        private static void Continue()
        {
            string phase = SessionState.GetString(PhaseKey, string.Empty);
            if (string.IsNullOrWhiteSpace(phase)) return;
            if ((phase == EnterA || phase == EnterB) && EditorApplication.isPlaying && WorldClock.Current != null)
                WorldClock.Current.AdvanceDuringGameplay = false;
            if (phase == EnterA && Ready()) { Execute(RunSessionA, ExitA); return; }
            if (phase == ExitA && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                SessionState.SetString(PhaseKey, EnterB);
                EditorApplication.EnterPlaymode();
                return;
            }
            if (phase == EnterB && Ready()) { Execute(RunSessionB, Finish); return; }
            if (phase == Finish && !EditorApplication.isPlayingOrWillChangePlaymode) FinalizeRun();
        }

        private static bool Ready() => EditorApplication.isPlaying && Time.frameCount >= 5 && WorldClock.Current != null &&
                                       GameDataManager.Instance != null && GameDataManager.Instance.IsReady;

        private static void Execute(Action action, string next)
        {
            try { action(); SessionState.SetString(PhaseKey, next); }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetString(ErrorKey, exception.Message);
                SessionState.SetString(PhaseKey, Finish);
            }
            EditorApplication.ExitPlaymode();
        }

        private static void RunSessionA()
        {
            ActorInteractionContext player = Player();
            ActorItemOwnershipComponent ownership = player.GetComponent<ActorItemOwnershipComponent>();
            InventoryComponent inventory = ownership.PersonalInventory;
            ActorEquipmentComponent equipment = ownership.Equipment;
            FirearmDebugController input = player.GetComponent<FirearmDebugController>();
            DebugActionProgressController progress = UnityEngine.Object.FindAnyObjectByType<DebugActionProgressController>();
            Require(ownership != null && inventory != null && equipment != null && input != null && progress != null,
                $"A. Player combat foundations are incomplete (ownership={ownership != null}, inventory={inventory != null}, " +
                $"equipment={equipment != null}, input={input != null}, progress={progress != null}).");
            CurrentSliceSaveData initial = Capture("M40 initial");
            Write(InitialSlot, initial);

            GameDatabase database = GameDataManager.Instance.Database;
            FirearmProfileDefinition firearm = database.GetFirearmProfile("core:lee_enfield_rifle_01_profile");
            AmmoProfileDefinition ammo = database.GetAmmoProfile(AmmoProfileId);
            WeaponProfileDefinition melee = database.GetWeaponProfile("core:improvised_blunt_medium");
            Require(firearm != null && firearm.magazine_capacity == 10 && firearm.reload_duration > 0f &&
                    ammo?.wound_type == WoundType.Puncture.ToString() && melee?.wound_type == WoundType.Blunt.ToString(),
                "A/B. Data-driven firearm, ammo or melee medical contracts are invalid.");
            Require(input.PreservesMovementInput, "Q. Combat adapter disabled WASD movement authority.");

            ItemInstance crowbar = inventory.AddItemByDefinitionId("core:rusted_crowbar_01", 1);
            Require(crowbar != null, "K. Could not add the data-driven crowbar fixture.");
            EquipmentPreview crowbarPlan = equipment.PreviewEquip(
                inventory, crowbar.InstanceId, new[] { ActorEquipmentComponent.HandRightSlotId });
            Require(crowbarPlan.Success && equipment.Equip(inventory, crowbarPlan).Success &&
                    equipment.GetEquippedInstance(ActorEquipmentComponent.HandRightSlotId)?.InstanceId == crowbar.InstanceId,
                "K. Crowbar could not become the equipped melee weapon.");
            ActorRuntimeIdentity source = ActorRuntimeRegistry.ActiveRepresentations.First(identity =>
                identity != null && identity.GetComponent<ActorInteractionContext>()?.ActorTags.Contains("player") != true &&
                identity.OriginKind == ActorOriginKind.Authored && identity.LifecycleState == ActorLifecycleState.Alive);
            Require(ActorSpawnService.TrySpawn(source.ActorProfileId, player.transform.position + player.transform.right * 1.1f,
                    player.transform.rotation, out ActorRuntimeIdentity target, out string failure),
                "C. Runtime combat target spawn failed: " + failure);
            Collider collider = target.GetComponent<Collider>();
            ActorMedicalStateComponent medical = target.GetComponent<ActorMedicalStateComponent>();
            ActorHealthComponent health = target.GetComponent<ActorHealthComponent>();
            Require(collider != null && medical != null && health != null && target.OriginKind == ActorOriginKind.Runtime,
                "C. Runtime combat target lacks collider/medical/lifecycle authority.");
            Physics.SyncTransforms();
            AssertSixRegions(target.transform, collider);

            Vector3 torso = RegionPoint(collider.bounds, 0f, 0.65f);
            WeaponCombatResult meleeHit = WeaponCombatService.StrikeEquipped(ownership, crowbar.InstanceId, collider, torso);
            Require(meleeHit.Success && meleeHit.Combat.Region == BodyRegion.Torso && medical.WoundCount == 1 &&
                    medical.GetWounds(BodyRegion.Torso).Single().woundType == WoundType.Blunt.ToString(),
                "K. Crowbar did not apply one deterministic M39 Blunt torso wound: " + meleeHit.Message);
            int wounds = medical.WoundCount;
            WeaponCombatResult outOfRange = WeaponCombatService.StrikeEquipped(
                ownership, crowbar.InstanceId, collider, player.transform.position + Vector3.forward * (melee.melee_range + 3f));
            Require(outOfRange.Code == WeaponCombatCode.OutOfRange && medical.WoundCount == wounds,
                "L. Out-of-range melee was not rejected without mutation.");

            ItemInstance rifle = inventory.AddItemByDefinitionId(RifleId, 1);
            Require(rifle != null && rifle.HasFirearmState && rifle.LoadedRounds == 0 && rifle.LoadedAmmoProfileId == null,
                "B. New firearm instance did not bootstrap unloaded.");
            EquipRifle(equipment, inventory, rifle.InstanceId);
            WeaponCombatResult dry = WeaponCombatService.FireEquipped(ownership, rifle.InstanceId, collider, torso);
            Require(dry.Code == WeaponCombatCode.Unloaded && medical.WoundCount == wounds,
                "H. Dry fire mutated target or did not report unloaded.");
            bool completionCalled = false;
            Require(progress.TryStartTimedOperation(firearm.reload_duration, "Reload", rifle.InstanceId, () =>
                {
                    completionCalled = true;
                    return DebugActionExecutionResult.Info("Reload", "unexpected");
                }) && progress.TryCancelActiveAction("M40 movement cancellation") && !completionCalled && rifle.LoadedRounds == 0,
                "C. Timed reload cancellation committed gameplay state.");
            Require(WeaponCombatService.ReloadEquipped(ownership, rifle.InstanceId).Code == WeaponCombatCode.NoCompatibleAmmo,
                "F. Reload accepted non-ammo ownership.");

            Require(inventory.AddItemByDefinitionId(AmmoItemId, 7) != null, "C. Could not add partial reload ammo.");
            int ammoBefore = Quantity(ownership, AmmoItemId);
            WeaponCombatResult partial = WeaponCombatService.ReloadEquipped(ownership, rifle.InstanceId);
            Require(partial.Success && partial.Quantity == 7 && rifle.LoadedRounds == 7 &&
                    rifle.LoadedAmmoProfileId == AmmoProfileId && Quantity(ownership, AmmoItemId) == ammoBefore - 7,
                "C/D. Partial reload quantity/state was not exact: " + partial.Message);
            Require(inventory.AddItemByDefinitionId(AmmoItemId, 10) != null, "D. Could not add full reload ammo.");
            ammoBefore = Quantity(ownership, AmmoItemId);
            WeaponCombatResult full = WeaponCombatService.ReloadEquipped(ownership, rifle.InstanceId);
            Require(full.Success && full.Quantity == 3 && rifle.LoadedRounds == 10 && Quantity(ownership, AmmoItemId) == ammoBefore - 3,
                "D. Reload did not consume exactly remaining capacity.");
            int fullAmmo = Quantity(ownership, AmmoItemId);
            Require(WeaponCombatService.ReloadEquipped(ownership, rifle.InstanceId).Code == WeaponCombatCode.Full &&
                    rifle.LoadedRounds == 10 && Quantity(ownership, AmmoItemId) == fullAmmo,
                "E. Full reload rejection mutated firearm or ammo.");

            target.transform.position = player.transform.position + player.transform.right * 2.5f;
            target.transform.rotation = Quaternion.LookRotation(
                player.transform.position - target.transform.position, Vector3.up);
            Physics.SyncTransforms();
            torso = RegionPoint(collider.bounds, 0f, 0.65f);
            Require(input.DiagnosticResolvePhysicalShot(
                    torso, firearm.range, out Collider unobstructedCollider, out Vector3 unobstructedPoint,
                    out Vector3 physicalOrigin) && IsTargetCollider(unobstructedCollider, target),
                "G. Clear physical shot did not resolve the target actor before near-cover setup.");

            Vector3 shotDirection = torso - physicalOrigin;
            shotDirection.y = 0f;
            shotDirection.Normalize();
            var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cover.name = "M40 Near Cover Diagnostic";
            cover.transform.SetPositionAndRotation(
                physicalOrigin + shotDirection * 0.65f,
                Quaternion.LookRotation(shotDirection, Vector3.up));
            cover.transform.localScale = new Vector3(1.5f, 2f, 0.1f);
            Physics.SyncTransforms();

            Require(input.DiagnosticResolvePhysicalShot(
                    torso, firearm.range, out Collider blockedCollider, out Vector3 blockedPoint, out Vector3 blockedOrigin) &&
                    blockedCollider == cover.GetComponent<Collider>() && Vector3.Distance(blockedOrigin, physicalOrigin) < 0.001f,
                "G. Near cover was not the first collider from the physical shot origin.");
            int roundsBeforeCover = rifle.LoadedRounds;
            WeaponCombatResult blockedShot = WeaponCombatService.FireEquipped(
                ownership, rifle.InstanceId, blockedCollider, blockedPoint);
            Require(blockedShot.Code == WeaponCombatCode.Miss &&
                    blockedShot.Combat.Code == CombatResolutionCode.InvalidTarget &&
                    rifle.LoadedRounds == roundsBeforeCover - 1 && medical.WoundCount == wounds,
                "G. Near cover did not consume exactly one round and block the target wound: " + blockedShot.Message);

            UnityEngine.Object.DestroyImmediate(cover);
            Physics.SyncTransforms();
            Require(input.DiagnosticResolvePhysicalShot(
                    torso, firearm.range, out unobstructedCollider, out unobstructedPoint, out _) &&
                    IsTargetCollider(unobstructedCollider, target),
                "G. Clear shot did not resolve the same target after near-cover removal.");
            WeaponCombatResult shot = WeaponCombatService.FireEquipped(
                ownership, rifle.InstanceId, unobstructedCollider, unobstructedPoint);
            Require(shot.Success && rifle.LoadedRounds == 8 && medical.WoundCount == wounds + 1 &&
                    shot.Combat.Region == BodyRegion.Torso && medical.GetWound(shot.Combat.WoundId).woundType == WoundType.Puncture.ToString(),
                "G. Firearm hit did not consume one round and apply one Puncture wound: " + shot.Message);
            wounds = medical.WoundCount;
            WeaponCombatResult miss = WeaponCombatService.FireEquipped(ownership, rifle.InstanceId, null, Vector3.zero);
            Require(miss.Code == WeaponCombatCode.Miss && rifle.LoadedRounds == 7 && medical.WoundCount == wounds,
                "G. Miss did not consume exactly one loaded round without a wound.");
            input.DiagnosticStartCycle(10f);
            Require(!input.IsAttackReady, "I. Bolt cycle gate did not close after a shot.");
            input.DiagnosticStartCycle(0f);
            Require(input.IsAttackReady, "I. Bolt cycle gate did not reopen after its duration.");

            health.ApplyInitialHealth(health.MaxHealth, 4f);
            Require(WorldClock.Current.TryAdvanceGameTime(WorldClock.SecondsPerHour, out failure),
                "J. Combat bleeding clock advance failed: " + failure);
            Require(health.IsDead && target.LifecycleState == ActorLifecycleState.Dead &&
                    target.GetComponent<WorldObjectTags>()?.HasTag(ActorHealthComponent.LootableActorTag) == true,
                "J. Combat wound did not preserve M39 health -> M38 corpse continuity.");

            Require(rifle.TrySetFirearmState(AmmoProfileId, 5, out failure), "P. Drop fixture state failed: " + failure);
            Require(DroppedWorldItemSpawner.TryDrop(equipment, rifle.InstanceId, inventory, "core:drop", "Drop", out failure),
                "P. Equipped firearm drop failed: " + failure);
            WorldItemPickup pickup = UnityEngine.Object.FindObjectsByType<WorldItemPickup>(FindObjectsInactive.Exclude)
                .Single(candidate => candidate.GridStorageEntries.Any(entry => entry.Item.InstanceId == rifle.InstanceId));
            ItemInstance dropped = pickup.GridStorageEntries.Single().Item;
            Require(ReferenceEquals(dropped, rifle) && dropped.LoadedRounds == 5,
                "P. Drop did not preserve firearm instance identity/state.");
            RemovePersonal(inventory, crowbar.InstanceId);
            ItemStorageEntry remainingAmmo = ownership.GetAllOwnedEntries().FirstOrDefault(entry => entry.DefinitionId == AmmoItemId);
            if (remainingAmmo != null && inventory.TryGetEntryByInstanceId(remainingAmmo.Item.InstanceId, out _, out _))
                RemovePersonal(inventory, remainingAmmo.Item.InstanceId);
            DebugActionExecutionResult picked = pickup.PickUp(player, pickup.GetComponent<WorldObjectTags>());
            Require(picked.hasResult && inventory.TryGetEntryByInstanceId(rifle.InstanceId, out _, out ItemStorageEntry pickedEntry) &&
                    ReferenceEquals(pickedEntry.Item, rifle) && rifle.LoadedRounds == 5,
                "P. Pickup did not preserve firearm instance identity/state: " + picked.body);
            EquipRifle(equipment, inventory, rifle.InstanceId);

            InventoryUISessionController inventoryUi = UnityEngine.Object.FindAnyObjectByType<InventoryUISessionController>();
            DebugWorldUiInputBlocker blocker = UnityEngine.Object.FindAnyObjectByType<DebugWorldUiInputBlocker>();
            Require(inventoryUi != null && blocker != null, "Q. Inventory/UI input blocker foundations are missing.");
            inventoryUi.OpenPersonal();
            Require(inventoryUi.IsOpen && blocker.BlocksWorldInput && input.PreservesMovementInput,
                "Q. Inventory did not block combat input or combat disabled movement.");
            inventoryUi.CloseSession();

            CurrentSliceSaveData targetSave = Capture("M40 target");
            ItemState rifleState = targetSave.items.Single(item => item.instanceId == rifle.InstanceId);
            Require(rifleState.firearmState?.loadedRounds == 5 && rifleState.firearmState.ammoProfileId == AmmoProfileId &&
                    targetSave.actors.Any(actor => actor.actorInstanceId == target.ActorInstanceId && actor.lifecycleState == "Dead" &&
                        actor.medicalState.wounds.Any(wound => wound.woundType == WoundType.Puncture.ToString())),
                "S. Snapshot omitted equipped firearm state or combat medical/death state.");
            Write(TargetSlot, targetSave);
            WritePayloads(targetSave, rifle.InstanceId);
        }

        private static void RunSessionB()
        {
            ActorInteractionContext player = Player();
            ActorItemOwnershipComponent ownership = player.GetComponent<ActorItemOwnershipComponent>();
            Require(ownership.GetAllOwnedEntries().Where(entry => Definition(entry.Item)?.firearm_profile_id != null)
                    .All(entry => entry.Item.HasFirearmState && entry.Item.LoadedRounds == 0),
                "T. Fresh session firearm bootstrap was not unloaded.");
            CurrentSliceSaveData target = Read(TargetSlot);
            CurrentSliceLoadResult loaded = CurrentSliceLoadService.Load(TargetSlot, Store());
            Require(loaded.Success, "T. Fresh-session M40 load failed: " + loaded.Failure);
            AssertEquivalent(target, Capture("M40 fresh load"), "T. exact M40 round-trip");
            ItemState targetRifle = target.items.Single(item => item.definitionId == RifleId && item.firearmState?.loadedRounds == 5);
            ItemInstance rifle = ownership.GetAllOwnedEntries().Single(entry => entry.Item.InstanceId == targetRifle.instanceId).Item;
            Require(rifle.LoadedRounds == 5 && rifle.LoadedAmmoProfileId == AmmoProfileId &&
                    ownership.Equipment.IsEquipped(rifle.InstanceId) &&
                    target.actors.Where(actor => actor.lifecycleState == "Dead").All(actor =>
                        ActorRuntimeRegistry.TryGet(actor.actorInstanceId, out ActorRuntimeIdentity identity) && identity.LifecycleState == ActorLifecycleState.Dead),
                "T. Firearm equipment or combat actor lifecycle did not rehydrate exactly.");

            CurrentSliceSaveData legacy = Read(LegacySlot);
            Require(legacy.items.Where(item => Definition(item.definitionId)?.firearm_profile_id != null)
                    .All(item => item.firearmState != null && item.firearmState.loadedRounds == 0 && item.firearmState.ammoProfileId == null),
                "U. Omitted firearm state did not normalize to unloaded.");
            Require(CurrentSliceLoadService.Load(LegacySlot, Store()).Success &&
                    ownership.GetAllOwnedEntries().Where(entry => Definition(entry.Item)?.firearm_profile_id != null)
                        .All(entry => entry.Item.LoadedRounds == 0),
                "U. Legacy unloaded firearm state did not apply safely.");

            CurrentSliceSaveData beforeInvalid = Capture("M40 preflight baseline");
            AssertPreflightNoMutation(NegativeSlot, beforeInvalid);
            AssertPreflightNoMutation(OverflowSlot, beforeInvalid);
            AssertPreflightNoMutation(AmmoSlot, beforeInvalid);
            AssertPreflightNoMutation(NullSlot, beforeInvalid);
            AssertPreflightNoMutation(DuplicateSlot, beforeInvalid);

            ItemInstance liveRifle = ownership.GetAllOwnedEntries().First(entry => Definition(entry.Item)?.firearm_profile_id != null).Item;
            Require(liveRifle.TrySetFirearmState(AmmoProfileId, 3, out string failure), "W. Rollback fixture failed: " + failure);
            CurrentSliceSaveData beforeFault = Capture("M40 pre-fault");
            CurrentSliceLoadService.DiagnosticInjectFailureAfterFirearmStateRestore = true;
            CurrentSliceLoadResult fault = CurrentSliceLoadService.Load(TargetSlot, Store());
            Require(fault.FailureCode == CurrentSliceLoadFailureCode.ApplyFailed && fault.RollbackAttempted && fault.RollbackSucceeded,
                "W. Post-firearm fault did not report exact rollback: " + fault.Failure);
            AssertEquivalent(beforeFault, Capture("M40 post-fault"), "W. exact firearm rollback");

            CurrentSliceSaveData initial = Read(InitialSlot);
            CurrentSliceLoadResult cleanup = CurrentSliceLoadService.Load(InitialSlot, Store());
            Require(cleanup.Success, "M40 initial cleanup failed: " + cleanup.Failure);
            AssertEquivalent(initial, Capture("M40 cleanup"), "M40 diagnostic cleanup");
            CurrentSliceLoadService.DiagnosticInjectFailureAfterFirearmStateRestore = false;
        }

        private static void WritePayloads(CurrentSliceSaveData target, string rifleInstanceId)
        {
            JObject legacy = Payload(target); foreach (JObject item in legacy["items"].Children<JObject>()) item.Remove("firearmState"); WritePayload(LegacySlot, legacy);
            JObject negative = Payload(target); Firearm(negative, rifleInstanceId)["loadedRounds"] = -1; WritePayload(NegativeSlot, negative);
            JObject overflow = Payload(target); Firearm(overflow, rifleInstanceId)["loadedRounds"] = 11; WritePayload(OverflowSlot, overflow);
            JObject incompatible = Payload(target); Firearm(incompatible, rifleInstanceId)["ammoProfileId"] = "core:improvised_blunt_medium"; WritePayload(AmmoSlot, incompatible);
            JObject presentNull = Payload(target); Item(presentNull, rifleInstanceId)["firearmState"] = JValue.CreateNull(); WritePayload(NullSlot, presentNull);
            JObject duplicate = Payload(target); ((JArray)duplicate["items"]).Add(Item(duplicate, rifleInstanceId).DeepClone()); WritePayload(DuplicateSlot, duplicate);
        }

        private static JObject Payload(CurrentSliceSaveData data) => (JObject)CurrentSliceSnapshotService.ToPayload(data).DeepClone();
        private static JObject Item(JObject payload, string instanceId) => payload["items"].Children<JObject>().Single(item => (string)item["instanceId"] == instanceId);
        private static JObject Firearm(JObject payload, string instanceId) => (JObject)Item(payload, instanceId)["firearmState"];

        private static void AssertPreflightNoMutation(string slot, CurrentSliceSaveData before)
        {
            CurrentSliceLoadResult result = CurrentSliceLoadService.Load(slot, Store());
            Require(result.FailureCode == CurrentSliceLoadFailureCode.SemanticPreflightFailed && !result.MutationStarted,
                $"V. Slot '{slot}' was not rejected before mutation: {result.Failure}");
            AssertEquivalent(before, Capture("post " + slot), "V. no mutation " + slot);
        }

        private static void AssertSixRegions(Transform actor, Collider collider)
        {
            Bounds bounds = collider.bounds;
            Require(CombatResolutionService.ResolveBodyRegion(actor, collider, RegionPoint(bounds, 0f, .9f)) == BodyRegion.Head &&
                    CombatResolutionService.ResolveBodyRegion(actor, collider, RegionPoint(bounds, 0f, .65f)) == BodyRegion.Torso &&
                    CombatResolutionService.ResolveBodyRegion(actor, collider, RegionPoint(bounds, -.9f, .65f)) == BodyRegion.LeftArm &&
                    CombatResolutionService.ResolveBodyRegion(actor, collider, RegionPoint(bounds, .9f, .65f)) == BodyRegion.RightArm &&
                    CombatResolutionService.ResolveBodyRegion(actor, collider, RegionPoint(bounds, -.4f, .25f)) == BodyRegion.LeftLeg &&
                    CombatResolutionService.ResolveBodyRegion(actor, collider, RegionPoint(bounds, .4f, .25f)) == BodyRegion.RightLeg,
                "C. Deterministic six-region body resolver mapping failed.");
        }

        private static Vector3 RegionPoint(Bounds bounds, float normalizedX, float normalizedY) =>
            new Vector3(bounds.center.x + bounds.extents.x * normalizedX, bounds.min.y + bounds.size.y * normalizedY, bounds.center.z);

        private static bool IsTargetCollider(Collider collider, ActorRuntimeIdentity target) =>
            collider != null && target != null &&
            (collider.transform == target.transform || collider.transform.IsChildOf(target.transform));

        private static void EquipRifle(ActorEquipmentComponent equipment, InventoryComponent inventory, string instanceId)
        {
            string[] slots = { ActorEquipmentComponent.HandLeftSlotId, ActorEquipmentComponent.HandRightSlotId };
            EquipmentMutationResult result;
            if (slots.Any(slot => equipment.GetEquippedInstance(slot) != null))
            {
                EquipmentReplacementPlan plan = equipment.PreviewEquipReplacing(inventory, instanceId, slots);
                Require(plan.Success, "Rifle replacement preview failed: " + plan.Message);
                result = equipment.EquipReplacing(inventory, plan);
            }
            else
            {
                EquipmentPreview plan = equipment.PreviewEquip(inventory, instanceId, slots);
                Require(plan.Success, "Rifle equip preview failed: " + plan.Message);
                result = equipment.Equip(inventory, plan);
            }
            Require(result.Success && equipment.IsEquipped(instanceId), "Rifle equip failed: " + result.Message);
        }

        private static int Quantity(ActorItemOwnershipComponent ownership, string definitionId) =>
            ownership.GetAllOwnedEntries().Where(entry => entry.DefinitionId == definitionId).Sum(entry => entry.Quantity);

        private static void RemovePersonal(InventoryComponent inventory, string instanceId)
        {
            Require(inventory.TryGetEntryByInstanceId(instanceId, out int index, out ItemStorageEntry entry) &&
                    inventory.TryRemoveItemAt(index, entry.Quantity),
                "P. Could not clear personal-grid space for pickup fixture '" + instanceId + "'.");
        }
        private static ItemDefinition Definition(ItemInstance item) => item == null ? null : Definition(item.DefinitionId);
        private static ItemDefinition Definition(string id) => GameDataManager.Instance.Database.GetItem(id);

        private static ActorInteractionContext Player()
        {
            ActorInteractionContext[] actors = UnityEngine.Object.FindObjectsByType<ActorInteractionContext>(FindObjectsInactive.Exclude);
            ActorInteractionContext player = actors.SingleOrDefault(candidate => candidate.ActorTags.Contains("player"));
            Require(player != null, $"Expected exactly one player; found {actors.Length} actors.");
            return player;
        }

        private static PersistenceFileStore Store() => new PersistenceFileStore(SessionState.GetString(RootKey, string.Empty));
        private static CurrentSliceSaveData Capture(string label)
        {
            CurrentSliceResult result = CurrentSliceSnapshotService.Capture();
            Require(result.Success, label + " capture failed: " + result.Failure);
            return result.Snapshot;
        }
        private static void Write(string slot, CurrentSliceSaveData data) => WritePayload(slot, CurrentSliceSnapshotService.ToPayload(data));
        private static void WritePayload(string slot, JToken payload)
        {
            PersistenceWriteResult result = Store().Write(slot, payload);
            Require(result.Success, $"Slot '{slot}' write failed: {result.Failure}");
        }
        private static CurrentSliceSaveData Read(string slot)
        {
            CurrentSliceResult result = CurrentSliceSnapshotService.Read(slot, Store());
            Require(result.Success, $"Slot '{slot}' read failed: {result.Failure}");
            return result.Snapshot;
        }
        private static void AssertEquivalent(CurrentSliceSaveData expected, CurrentSliceSaveData actual, string label)
        {
            CurrentSliceComparisonResult result = CurrentSliceSnapshotService.Compare(expected, actual);
            Require(result.Equivalent, label + " differs: " + result.Difference);
        }
        private static void Require(bool condition, string failure) { if (!condition) throw new InvalidOperationException(failure); }

        private static void FinalizeRun()
        {
            string failure = SessionState.GetString(ErrorKey, string.Empty);
            string root = SessionState.GetString(RootKey, string.Empty);
            if (EditorSceneManager.GetActiveScene().isDirty) failure = Append(failure, "Diagnostics left SampleScene dirty.");
            try { if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root)) Directory.Delete(root, true); }
            catch (Exception exception) { failure = Append(failure, "Temporary cleanup failed: " + exception.Message); }
            bool success = string.IsNullOrWhiteSpace(failure);
            ClearSession();
            if (success) Debug.Log("M40.0 Combat Resolution & Weapons Diagnostics: PASS");
            else Debug.LogError("M40.0 Combat Resolution & Weapons Diagnostics: FAIL\n- " + failure);
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
        }

        private static string Append(string current, string value) => string.IsNullOrWhiteSpace(current) ? value : current + "\n- " + value;
        private static void ClearSession()
        {
            CurrentSliceLoadService.DiagnosticInjectFailureAfterFirearmStateRestore = false;
            SessionState.EraseString(PhaseKey); SessionState.EraseString(RootKey); SessionState.EraseString(ErrorKey);
        }
    }
}
