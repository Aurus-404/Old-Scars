using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// <summary>
    /// M41.2 contract coverage for Core equipment definitions, item-owned
    /// storage and generic firearm modes. It runs against a real loaded Core
    /// database and the existing SampleScene player/runtime authorities.
    /// </summary>
    [InitializeOnLoad]
    public static class M41EquipmentWeaponCoverageDiagnostics
    {
        private const string BatchPendingKey = "OldScars.M41_2.BatchPending";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string LayoutId = "core:human_standard_01";
        private const string AmmoProfileId = "core:ammo_303_british_01_profile";
        private const string AmmoItemId = "core:ammo_303_british_01";
        private const string ScrapItemId = "core:scrap_metal_01";
        private const string MeleeItemId = "core:rusted_crowbar_01";
        private static readonly string[] BackpackIds =
        {
            "core:small_backpack_01",
            "core:medium_backpack_01",
            "core:large_backpack_01"
        };

        static M41EquipmentWeaponCoverageDiagnostics() => EditorApplication.update += RunBatchWhenReady;

        public static void Run()
        {
            if (!EditorApplication.isPlaying || GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
                throw new InvalidOperationException("M41.2 diagnostics require Play Mode with Core data ready.");

            string root = Path.Combine(Path.GetTempPath(), "OldScars_M41_2_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            GameObject fixture = null;
            GameObject beyondRange = null;
            try
            {
                GameDatabase database = GameDataManager.Instance.Database;
                Require(database != null, "Core GameDatabase is unavailable after DataValidator publication.");
                ValidateEquipmentCoverage(database);
                ValidateFirearmDefinitions(database);
                ValidateBackpackDefinitions(database);

                fixture = CreateActorFixture("M41.2 Equipment Weapon Fixture", out InventoryComponent fixtureInventory,
                    out ActorItemOwnershipComponent fixtureOwnership, out ActorEquipmentComponent fixtureEquipment);
                ValidateBackpackTransactions(fixtureInventory, fixtureOwnership, fixtureEquipment, database);
                ValidateFireModesAndRange(fixtureInventory, fixtureOwnership, fixtureEquipment, database, ref beyondRange);

                ValidatePlayerCurrentSliceRoundTrip(database, new PersistenceFileStore(root));
                Debug.Log("M41.2 Basic Equipment & Weapon Coverage Diagnostics: PASS");
            }
            finally
            {
                if (beyondRange != null)
                    UnityEngine.Object.DestroyImmediate(beyondRange);
                if (fixture != null)
                    UnityEngine.Object.DestroyImmediate(fixture);
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("M41.2 batch diagnostics require Unity batchmode.");
            if (EditorApplication.isCompiling)
                throw new InvalidOperationException("M41.2 batch diagnostics cannot start while compiling.");

            SessionState.SetBool(BatchPendingKey, true);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void RunBatchWhenReady()
        {
            if (!SessionState.GetBool(BatchPendingKey, false) || !EditorApplication.isPlaying ||
                GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
            {
                return;
            }

            SessionState.EraseBool(BatchPendingKey);
            try
            {
                Run();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ValidateEquipmentCoverage(GameDatabase database)
        {
            EquipmentLayoutDefinition layout = database.GetEquipmentLayout(LayoutId);
            Require(layout?.slots != null && layout.slots.Length > 0,
                "Loaded human equipment layout is missing its definition-owned slots.");

            ItemDefinition[] items = database.GetAllItems().Where(item => item != null).ToArray();
            var report = new List<string>();
            for (int index = 0; index < layout.slots.Length; index++)
            {
                string slotId = layout.slots[index]?.slot_id;
                Require(!string.IsNullOrWhiteSpace(slotId), "Equipment layout contains an empty slot id.");
                string[] coveringIds = items
                    .Where(item => CoversSlot(item, slotId))
                    .Select(item => item.id)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray();
                Require(coveringIds.Length > 0,
                    "No data-declared equip.slot_sets cover loaded layout slot '" + slotId + "'.");
                report.Add(slotId + " <- " + string.Join(", ", coveringIds));
            }

            Debug.Log("[M41.2][EQUIPMENT_COVERAGE]\n" + string.Join("\n", report));
        }

        private static void ValidateBackpackDefinitions(GameDatabase database)
        {
            var sizes = new HashSet<string>(StringComparer.Ordinal);
            foreach (string backpackId in BackpackIds)
            {
                ItemDefinition backpack = database.GetItem(backpackId);
                Require(backpack?.owned_storage_profile_id != null && backpack.equip?.slot_sets != null,
                    "Backpack '" + backpackId + "' lacks its real item-owned storage or equipment declaration.");
                ItemStorageProfileDefinition profile = database.GetItemStorageProfile(backpack.owned_storage_profile_id);
                Require(profile != null && profile.width > 0 && profile.height > 0,
                    "Backpack '" + backpackId + "' references an invalid storage profile.");
                Require(sizes.Add(profile.width + "x" + profile.height),
                    "Backpack tiers must have distinct physical storage dimensions.");
            }
        }

        private static void ValidateFirearmDefinitions(GameDatabase database)
        {
            AssertFirearm(database, "core:lee_enfield_rifle_01", FirearmActionModes.ManualCycle, 80f);
            AssertFirearm(database, "core:semi_automatic_rifle_01", FirearmActionModes.SemiAutomatic, 75f);
            AssertFirearm(database, "core:automatic_rifle_01", FirearmActionModes.Automatic, 60f);
        }

        private static void AssertFirearm(GameDatabase database, string itemId, string expectedMode, float expectedRange)
        {
            ItemDefinition item = database.GetItem(itemId);
            FirearmProfileDefinition profile = item != null ? database.GetFirearmProfile(item.firearm_profile_id) : null;
            Require(profile != null && profile.fire_mode == expectedMode && Mathf.Abs(profile.range - expectedRange) < 0.0001f,
                "Firearm '" + itemId + "' has an invalid data-driven mode or physical range.");
        }

        private static void ValidateBackpackTransactions(
            InventoryComponent inventory,
            ActorItemOwnershipComponent ownership,
            ActorEquipmentComponent equipment,
            GameDatabase database)
        {
            foreach (string backpackId in BackpackIds)
            {
                ItemInstance backpack = inventory.AddItemByDefinitionId(backpackId, 1);
                ItemOwnedStorageRuntime storage = null;
                Require(backpack != null && ItemOwnedStorageRegistry.Instance.TryResolveOwnedStorage(backpack.InstanceId, out storage),
                    "Could not create item-owned storage for backpack '" + backpackId + "'.");
                ItemStorageProfileDefinition profile = database.GetItemStorageProfile(database.GetItem(backpackId).owned_storage_profile_id);
                Require(storage.ProfileId == profile.id && storage.GridWidth == profile.width && storage.GridHeight == profile.height,
                    "Backpack '" + backpackId + "' did not materialize its declared storage dimensions.");

                ItemInstance scrap = inventory.AddItemByDefinitionId(ScrapItemId, 3);
                Require(scrap != null, "Could not create backpack-content fixture.");
                InventoryMutationResult transferIn = GridStorageTransferService.TransferQuantityAuto(
                    inventory, storage, scrap.InstanceId, 3, true, default);
                Require(transferIn.Success && storage.GridStorageEntries.Any(entry => entry?.DefinitionId == ScrapItemId && entry.Quantity == 3),
                    "Backpack '" + backpackId + "' did not retain transferred content.");

                EquipmentPreview equipPreview = equipment.PreviewEquip(inventory, backpack.InstanceId, new[] { ActorEquipmentComponent.BackSlotId });
                Require(equipPreview.Success && equipment.Equip(inventory, equipPreview).Success && equipment.IsEquipped(backpack.InstanceId),
                    "Backpack '" + backpackId + "' could not equip through ActorEquipmentComponent.");
                Require(ownership.GetAllOwnedEntries().Any(entry => entry?.Item?.InstanceId == backpack.InstanceId) &&
                        ownership.GetAllOwnedEntries().Any(entry => entry?.DefinitionId == ScrapItemId),
                    "Equipped backpack content is not reachable through the existing actor ownership graph.");

                EquipmentPreview unequipPreview = equipment.PreviewUnequip(backpack.InstanceId);
                Require(unequipPreview.Success && equipment.Unequip(unequipPreview).Success && !equipment.IsEquipped(backpack.InstanceId),
                    "Backpack '" + backpackId + "' could not unequip transactionally.");
                InventoryMutationResult transferOut = GridStorageTransferService.TransferStackAuto(
                    storage, inventory, storage.GridStorageEntries.Single(entry => entry.DefinitionId == ScrapItemId).Item.InstanceId,
                    GridStorageTransferQuantityPolicy.Exact, default);
                Require(transferOut.Success && storage.GridStorageEntries.Count == 0,
                    "Backpack '" + backpackId + "' transfer-out did not preserve one ownership path.");
                Require(inventory.TryGetEntryByInstanceId(backpack.InstanceId, out int backpackIndex, out _) &&
                        inventory.TryRemoveItemAt(backpackIndex, 1),
                    "Empty backpack '" + backpackId + "' could not leave the temporary inventory cleanly.");
            }
        }

        private static void ValidateFireModesAndRange(
            InventoryComponent inventory,
            ActorItemOwnershipComponent ownership,
            ActorEquipmentComponent equipment,
            GameDatabase database,
            ref GameObject beyondRange)
        {
            Require(FirearmActionModes.ShouldAttemptFire(FirearmActionModes.ManualCycle, true, true) &&
                    !FirearmActionModes.ShouldAttemptFire(FirearmActionModes.ManualCycle, false, true) &&
                    FirearmActionModes.ShouldAttemptFire(FirearmActionModes.SemiAutomatic, true, true) &&
                    !FirearmActionModes.ShouldAttemptFire(FirearmActionModes.SemiAutomatic, false, true) &&
                    FirearmActionModes.ShouldAttemptFire(FirearmActionModes.Automatic, false, true) &&
                    !FirearmActionModes.ShouldAttemptFire(FirearmActionModes.Automatic, false, false),
                "Generic trigger policy does not distinguish press-only and held automatic fire.");

            ValidateTriggerSequence(inventory, ownership, equipment, database, "core:lee_enfield_rifle_01", 1);
            ValidateTriggerSequence(inventory, ownership, equipment, database, "core:semi_automatic_rifle_01", 1);
            ValidateTriggerSequence(inventory, ownership, equipment, database, "core:automatic_rifle_01", 3);

            ItemInstance semi = EquipFirearm(inventory, equipment, "core:semi_automatic_rifle_01");
            FirearmProfileDefinition semiProfile = database.GetFirearmProfile(database.GetItem(semi.DefinitionId).firearm_profile_id);
            Require(semi.TrySetFirearmState(AmmoProfileId, 2, out string loadFailure), "Could not load range fixture: " + loadFailure);
            Vector3 origin = ownership.transform.position + Vector3.up;
            beyondRange = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beyondRange.name = "M41.2 Beyond Firearm Range";
            beyondRange.transform.position = origin + Vector3.forward * (semiProfile.range + 1f);
            Physics.SyncTransforms();
            int beforeOutOfRange = semi.LoadedRounds;
            WeaponCombatResult outOfRange = WeaponCombatService.FireEquipped(
                ownership, semi.InstanceId, beyondRange.GetComponent<Collider>(), beyondRange.transform.position);
            Require(outOfRange.Code == WeaponCombatCode.OutOfRange && semi.LoadedRounds == beforeOutOfRange,
                "Direct firearm service path accepted a hit beyond firearm.range.");

            PhysicalShotResolution physicalMiss = PhysicalShotPathResolver.Resolve(
                ownership.transform, origin, Vector3.forward, semiProfile.range, 0.65f);
            Require(Vector3.Distance(origin, physicalMiss.EndPoint) <= semiProfile.range + 0.01f,
                "Physical hitscan produced an endpoint beyond firearm.range.");
            int beforeInsideRange = semi.LoadedRounds;
            WeaponCombatResult insideRange = WeaponCombatService.FireEquipped(
                ownership, semi.InstanceId, beyondRange.GetComponent<Collider>(), origin + Vector3.forward * (semiProfile.range - 0.1f));
            Require(insideRange.Code != WeaponCombatCode.OutOfRange && semi.LoadedRounds == beforeInsideRange - 1,
                "Firearm target immediately inside firearm.range was rejected or did not consume exactly one round.");

            ItemInstance crowbar = inventory.AddItemByDefinitionId(MeleeItemId, 1);
            Require(crowbar != null, "Could not create melee-range fixture.");
            EquipReplacing(equipment, inventory, crowbar.InstanceId, new[] { ActorEquipmentComponent.HandRightSlotId });
            WeaponProfileDefinition melee = database.GetWeaponProfile(database.GetItem(MeleeItemId).combat.weapon_profile);
            WeaponCombatResult meleeOutOfRange = WeaponCombatService.StrikeEquipped(
                ownership, crowbar.InstanceId, beyondRange.GetComponent<Collider>(),
                ownership.transform.position + Vector3.forward * (melee.melee_range + 1f));
            Require(meleeOutOfRange.Code == WeaponCombatCode.OutOfRange,
                "Melee target beyond melee_range was not rejected.");
        }

        private static void ValidateTriggerSequence(
            InventoryComponent inventory,
            ActorItemOwnershipComponent ownership,
            ActorEquipmentComponent equipment,
            GameDatabase database,
            string firearmId,
            int expectedShots)
        {
            ItemInstance firearm = EquipFirearm(inventory, equipment, firearmId);
            FirearmProfileDefinition profile = database.GetFirearmProfile(database.GetItem(firearmId).firearm_profile_id);
            Require(firearm.TrySetFirearmState(AmmoProfileId, profile.magazine_capacity, out string loadFailure),
                "Could not load '" + firearmId + "': " + loadFailure);

            int shots = 0;
            float nextShotTime = 0f;
            for (int frame = 0; frame < 7; frame++)
            {
                bool pressed = frame == 0;
                bool held = frame < 6;
                float elapsed = frame * profile.cycle_time * 0.5f;
                if (!FirearmActionModes.ShouldAttemptFire(profile.fire_mode, pressed, held) || elapsed < nextShotTime)
                    continue;

                int before = firearm.LoadedRounds;
                WeaponCombatResult shot = WeaponCombatService.FireEquipped(ownership, firearm.InstanceId, null, Vector3.zero);
                Require(shot.Code == WeaponCombatCode.Miss && shot.Quantity == 1 && firearm.LoadedRounds == before - 1,
                    "Firearm '" + firearmId + "' did not consume exactly one round per accepted trigger event.");
                shots++;
                nextShotTime = elapsed + profile.cycle_time;
            }
            Require(shots == expectedShots,
                "Firearm '" + firearmId + "' accepted " + shots + " trigger event(s); expected " + expectedShots + ".");

            Require(firearm.TrySetFirearmState(null, 0, out loadFailure),
                "Could not prepare zero-ammo trigger fixture: " + loadFailure);
            WeaponCombatResult empty = WeaponCombatService.FireEquipped(ownership, firearm.InstanceId, null, Vector3.zero);
            Require(empty.Code == WeaponCombatCode.Unloaded && empty.Quantity == 0 && firearm.LoadedRounds == 0,
                "Empty firearm accepted a trigger event as a shot.");
        }

        private static void ValidatePlayerCurrentSliceRoundTrip(GameDatabase database, PersistenceFileStore store)
        {
            ActorInteractionContext player = UnityEngine.Object.FindObjectsByType<ActorInteractionContext>(FindObjectsInactive.Exclude)
                .SingleOrDefault(actor => actor != null && actor.ActorTags.Contains("player"));
            Require(player != null, "SampleScene player runtime is unavailable for Current Slice coverage.");
            ActorItemOwnershipComponent ownership = player.GetComponent<ActorItemOwnershipComponent>();
            InventoryComponent inventory = ownership != null ? ownership.PersonalInventory : null;
            ActorEquipmentComponent equipment = ownership != null ? ownership.Equipment : null;
            Require(ownership != null && inventory != null && equipment != null,
                "Player ownership/equipment authorities are incomplete.");

            ItemInstance backpack = inventory.AddItemByDefinitionId("core:medium_backpack_01", 1);
            ItemOwnedStorageRuntime storage = null;
            Require(backpack != null && ItemOwnedStorageRegistry.Instance.TryResolveOwnedStorage(backpack.InstanceId, out storage),
                "Player-owned medium backpack could not materialize storage.");
            ItemInstance scrap = inventory.AddItemByDefinitionId(ScrapItemId, 2);
            Require(scrap != null && GridStorageTransferService.TransferQuantityAuto(
                    inventory, storage, scrap.InstanceId, 2, true, default).Success,
                "Player backpack content transfer failed before Current Slice capture.");
            EquipReplacing(equipment, inventory, backpack.InstanceId, new[] { ActorEquipmentComponent.BackSlotId });

            ItemInstance firearm = EquipFirearm(inventory, equipment, "core:automatic_rifle_01");
            Require(firearm.TrySetFirearmState(AmmoProfileId, 7, out string firearmFailure),
                "Could not prepare persisted firearm state: " + firearmFailure);

            CurrentSliceResult capture = CurrentSliceSnapshotService.Capture();
            Require(capture.Success, "M41.2 Current Slice capture failed: " + capture.Failure);
            PersistenceWriteResult write = store.Write("m41_2_equipment", CurrentSliceSnapshotService.ToPayload(capture.Snapshot));
            Require(write.Success, "M41.2 Current Slice write failed: " + write.Failure);
            CurrentSliceLoadResult load = CurrentSliceLoadService.Load("m41_2_equipment", store);
            Require(load.Success, "M41.2 Current Slice semantic preflight/apply failed: " + load.Failure);
            CurrentSliceResult restored = CurrentSliceSnapshotService.Capture();
            Require(restored.Success && CurrentSliceSnapshotService.Compare(capture.Snapshot, restored.Snapshot).Equivalent,
                "M41.2 Current Slice round-trip changed item-owned storage, equipment or firearm state.");
            Require(ItemOwnedStorageRegistry.Instance.TryResolveOwnedStorage(backpack.InstanceId, out ItemOwnedStorageRuntime restoredStorage) &&
                    restoredStorage.GridStorageEntries.Any(entry => entry?.DefinitionId == ScrapItemId && entry.Quantity == 2) &&
                    equipment.IsEquipped(backpack.InstanceId) && firearm.LoadedRounds == 7 &&
                    database.GetItem(backpack.DefinitionId)?.owned_storage_profile_id == restoredStorage.ProfileId,
                "M41.2 Current Slice restore did not preserve backpack ownership/content or firearm loaded state.");
        }

        private static GameObject CreateActorFixture(
            string name,
            out InventoryComponent inventory,
            out ActorItemOwnershipComponent ownership,
            out ActorEquipmentComponent equipment)
        {
            GameObject fixture = new GameObject(name);
            inventory = fixture.AddComponent<InventoryComponent>();
            ownership = fixture.AddComponent<ActorItemOwnershipComponent>();
            equipment = fixture.AddComponent<ActorEquipmentComponent>();
            Require(ownership.PersonalInventory != null && ownership.Equipment != null,
                "Temporary actor fixture did not bind the existing ownership/equipment components.");
            return fixture;
        }

        private static ItemInstance EquipFirearm(InventoryComponent inventory, ActorEquipmentComponent equipment, string definitionId)
        {
            ItemInstance firearm = inventory.AddItemByDefinitionId(definitionId, 1);
            Require(firearm != null && firearm.HasFirearmState, "Could not create firearm fixture '" + definitionId + "'.");
            EquipReplacing(equipment, inventory, firearm.InstanceId,
                new[] { ActorEquipmentComponent.HandLeftSlotId, ActorEquipmentComponent.HandRightSlotId });
            return firearm;
        }

        private static void EquipReplacing(
            ActorEquipmentComponent equipment,
            InventoryComponent inventory,
            string instanceId,
            string[] slots)
        {
            EquipmentMutationResult result;
            if (slots.Any(slot => equipment.GetEquippedInstance(slot) != null))
            {
                EquipmentReplacementPlan plan = equipment.PreviewEquipReplacing(inventory, instanceId, slots);
                result = plan.Success ? equipment.EquipReplacing(inventory, plan) :
                    EquipmentMutationResult.Rejected(plan.Message, instanceId, plan.FailureCode);
            }
            else
            {
                EquipmentPreview preview = equipment.PreviewEquip(inventory, instanceId, slots);
                result = preview.Success ? equipment.Equip(inventory, preview) :
                    EquipmentMutationResult.Rejected(preview.Message, instanceId, preview.FailureCode);
            }

            Require(result.Success && equipment.IsEquipped(instanceId),
                "Could not equip item '" + instanceId + "' through the existing equipment transaction service.");
        }

        private static bool CoversSlot(ItemDefinition item, string slotId)
        {
            if (item?.equip?.slot_sets == null)
                return false;
            return item.equip.slot_sets.Any(slotSet => slotSet != null && slotSet.Contains(slotId));
        }

        private static void Require(bool condition, string failure)
        {
            if (!condition)
                throw new InvalidOperationException(failure);
        }
    }
}
