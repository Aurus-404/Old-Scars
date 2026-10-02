using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using OldScars.Core;
using OldScars.Core.Actors;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Data.Loading;
using OldScars.Core.Data.Validation;
using OldScars.Core.Interactions;
using OldScars.Core.Items;
using OldScars.Core.Persistence;
using UnityEditor;
using UnityEngine;

namespace OldScars.Editor
{
    [InitializeOnLoad]
    public static class ContainerCarryDiagnostics
    {
        private const string PhaseKey = "OldScars.IMPL0066.Phase";
        private const string ResultKey = "OldScars.IMPL0066.Result";
        private const string Ammo = "core:ammo_303_british_01";
        private const string Rifle = "core:lee_enfield_rifle_01";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        static ContainerCarryDiagnostics() => EditorApplication.update += Continue;

        [MenuItem("Old Scars/Diagnostics/Items/Run Container Carry IMPL-0066")]
        public static void Run()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling,
                "Requires idle compiled Edit Mode.");
            Require(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == "Assets/Scenes/SampleScene.unity" &&
                !UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty,
                "Open the saved SampleScene; the diagnostic does not replace or save scenes.");
            SessionState.SetString(ResultKey, "PENDING");
            SessionState.SetString(PhaseKey, "enter");
            EditorApplication.EnterPlaymode();
        }

        private static void Continue()
        {
            string phase = SessionState.GetString(PhaseKey, "");
            if (phase == "enter" && EditorApplication.isPlaying && Time.frameCount >= 5 &&
                GameDataManager.Instance?.IsReady == true && WorldClock.Current != null)
            {
                SessionState.SetString(PhaseKey, "running");
                try
                {
                    WorldClock.Current.AdvanceDuringGameplay = false;
                    DataCases();
                    GameplayCases();
                    OldScars.EditorTools.InventoryInteractionUxDiagnostics.Run();
                    SessionState.SetString(ResultKey, "PASS");
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    SessionState.SetString(ResultKey, "FAIL: " + exception.Message);
                }
                SessionState.SetString(PhaseKey, "finish");
                EditorApplication.ExitPlaymode();
            }
            else if (phase == "finish" && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.EraseString(PhaseKey);
                string result = SessionState.GetString(ResultKey, "INCONCLUSIVE");
                string evidenceRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/IMPL0066"));
                Directory.CreateDirectory(evidenceRoot);
                File.WriteAllText(Path.Combine(evidenceRoot, "focused-result.txt"), result);
                if (result == "PASS") Debug.Log("[IMPL0066] Container Carry Diagnostics: PASS; manual acceptance still pending.");
                else Debug.LogError("[IMPL0066] Container Carry Diagnostics: " + result);
            }
        }

        private static void DataCases()
        {
            var omitted = JsonConvert.DeserializeObject<ItemStorageProfileDefinition>(
                "{\"type\":\"item_storage_profile\",\"id\":\"example:carrier\",\"display_name\":\"Legacy\",\"width\":8,\"height\":10}");
            Require(!omitted.max_content_weight_kg.HasValue && !omitted.carried_weight_multiplier.HasValue,
                "Omitted optional JSON fields did not remain nullable.");
            ValidateProfile(omitted, true);
            omitted.max_content_weight_kg = 20f;
            omitted.carried_weight_multiplier = 0.8f;
            ValidateProfile(omitted, true);
            Debug.Log("[IMPL0066][DATA] BEGIN expected invalid-profile errors");
            foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                omitted.max_content_weight_kg = invalid;
                ValidateProfile(omitted, false);
            }
            omitted.max_content_weight_kg = null;
            foreach (float invalid in new[] { 0f, -1f, 1.01f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                omitted.carried_weight_multiplier = invalid;
                ValidateProfile(omitted, false);
            }
            Debug.Log("[IMPL0066][DATA] END expected errors; optional/default/finite bounds: PASS");
            string[] ids = { "core:backpack_small_01", "core:backpack_medium_01", "core:backpack_large_01" };
            float[] capacities = { 20f, 30f, 40f }, multipliers = { 0.8f, 0.7f, 0.6f };
            for (int i = 0; i < ids.Length; i++)
            {
                ItemStorageProfileDefinition profile = GameDataManager.Instance.Database.GetItemStorageProfile(ids[i]);
                Require(profile?.max_content_weight_kg == capacities[i] && profile.carried_weight_multiplier == multipliers[i],
                    "Core provisional tuning was not loaded: " + ids[i]);
                ValidateProfile(profile, true);
            }
        }

        private static void ValidateProfile(ItemStorageProfileDefinition profile, bool valid)
        {
            var report = new DataLoadReport();
            var database = new GameDatabase(report);
            database.RegisterItemStorageProfile(profile, report);
            var validator = new DataValidator(database, new TagRegistry(), report);
            typeof(DataValidator).GetMethod("ValidateItemStorageProfiles", PrivateInstance).Invoke(validator, null);
            Require(report.HasErrors != valid, "Profile validation did not match expected validity.");
        }

        private static void GameplayCases()
        {
            ActorInteractionContext player = UnityEngine.Object.FindObjectsByType<ActorInteractionContext>(FindObjectsInactive.Exclude)
                .Single(actor => actor.ActorTags.Contains("player"));
            InventoryComponent inventory = player.GetComponent<InventoryComponent>();
            ActorEquipmentComponent equipment = player.GetComponent<ActorEquipmentComponent>();
            ActorCarryWeightComponent carry = player.GetComponent<ActorCarryWeightComponent>();
            CurrentSliceSaveData initial = Capture();
            float initialCapacity = carry.BaseCarryCapacityKg;
            ItemStorageProfileDefinition profile = GameDataManager.Instance.Database.GetItemStorageProfile("core:backpack_small_01");
            float? originalMaximum = profile.max_content_weight_kg, originalMultiplier = profile.carried_weight_multiplier;
            GameObject sourceRoot = null;
            try
            {
                foreach (ItemStorageEntry entry in equipment.Entries.ToArray())
                {
                    EquipmentPreview plan = equipment.PreviewUnequip(entry.Item.InstanceId);
                    Require(plan.Success && equipment.Unequip(plan).Success, "Could not prepare clean player equipment.");
                }
                Clear(inventory);
                ItemInstance backpack = inventory.AddItemByDefinitionId("core:small_backpack_01", 1);
                Require(backpack?.OwnedStorage != null, "Backpack fixture failed.");
                ItemOwnedStorageRuntime storage = backpack.OwnedStorage;
                sourceRoot = new GameObject("IMPL0066 external source");
                InventoryComponent source = sourceRoot.AddComponent<InventoryComponent>();
                double unit = GameDataManager.Instance.Database.GetItem(Ammo).physical.weight_kg.Value;
                profile.max_content_weight_kg = null;
                profile.carried_weight_multiplier = null;
                Require(storage.MaxContentWeightKg == null && storage.CarriedWeightMultiplier == 1f, "Legacy runtime defaults failed.");
                ItemInstance rounds = source.AddItemByDefinitionId(Ammo, 10);
                Require(GridStorageTransferService.TransferStackAuto(source, storage, rounds.InstanceId, default).Success,
                    "Unlimited legacy ingress failed.");
                profile.max_content_weight_kg = (float)(unit * 20d);
                ItemInstance incoming = source.AddItemByDefinitionId(Ammo, 11);
                Require(GridStorageTransferService.TransferQuantityAuto(source, storage, incoming.InstanceId, 5, true, default).Success,
                    "Below-cap transfer failed.");
                Require(GridStorageTransferService.TransferQuantityAuto(source, storage, incoming.InstanceId, 5, true, default).Success,
                    "Equal-cap transfer failed.");
                string before = Fingerprint(source, storage);
                InventoryMutationResult rejected = GridStorageTransferService.TransferStackAuto(source, storage, incoming.InstanceId, default);
                Require(!rejected.Success && rejected.Message.Contains("kg") && before == Fingerprint(source, storage),
                    "Over-cap full stack ingress mutated state or lacks an actionable reason.");
                Require(!GridStorageTransferService.TransferExact(source, storage, incoming.InstanceId, 6, 0, false, default).Success &&
                    before == Fingerprint(source, storage), "Over-cap exact ingress was not atomic.");
                profile.max_content_weight_kg = null;
                Require(!GridStorageTransferService.PreviewTransferExact(source, storage, incoming.InstanceId,
                    storage.GridWidth, 0, false, default).IsValid, "Grid bounds were relaxed by unlimited kg capacity.");
                Require(GridStorageTransferService.TransferExact(source, storage, incoming.InstanceId, 6, 0, false, default).Success,
                    "Valid exact ingress failed with unlimited kg capacity.");
                ItemInstance nested = source.AddItemByDefinitionId("core:small_backpack_01", 1);
                before = Fingerprint(source, storage);
                Require(!GridStorageTransferService.TransferStackAuto(source, storage, nested.InstanceId, default).Success &&
                    before == Fingerprint(source, storage), "No-nesting rejection was not atomic.");
                Drain(storage, source); Clear(source);

                int maxStack = GameDataManager.Instance.Database.GetItem(Ammo).max_stack;
                profile.max_content_weight_kg = (float)(unit * maxStack);
                rounds = source.AddItemByDefinitionId(Ammo, maxStack - 1);
                Require(GridStorageTransferService.TransferStackAuto(source, storage, rounds.InstanceId, default).Success,
                    "Directed merge destination setup failed.");
                string destinationId = storage.GridStorageEntries.Single().Item.InstanceId;
                incoming = source.AddItemByDefinitionId(Ammo, maxStack);
                GridStorageMergePreview preview = GridStorageTransferService.PreviewMergeIntoTarget(source, incoming.InstanceId, storage, destinationId, default);
                Require(preview.IsValid && preview.TransferQuantity == 1, "Directed merge checked source stack instead of actual one-round transfer.");
                InventoryMutationResult merged = GridStorageTransferService.MergeIntoTarget(source, incoming.InstanceId, storage, destinationId, default);
                Require(merged.Success && merged.AffectedQuantity == 1 && source.Entries.Single().Quantity == maxStack - 1,
                    "Directed merge did not commit its actual quantity.");
                // A merge with stack room must still reject if its actual quantity is too heavy.
                Drain(storage, source); Clear(source);
                profile.max_content_weight_kg = (float)(unit * 20d);
                rounds = source.AddItemByDefinitionId(Ammo, 20);
                Require(GridStorageTransferService.TransferStackAuto(source, storage, rounds.InstanceId, default).Success, "Cap setup failed.");
                incoming = source.AddItemByDefinitionId(Ammo, 2);
                before = Fingerprint(source, storage);
                Require(!GridStorageTransferService.PreviewMergeIntoTarget(source, incoming.InstanceId, storage, rounds.InstanceId, default).IsValid &&
                    !GridStorageTransferService.MergeIntoTarget(source, incoming.InstanceId, storage, rounds.InstanceId, default).Success &&
                    before == Fingerprint(source, storage), "Over-cap directed merge was not atomic.");
                Drain(storage, source); Clear(source);

                ItemInstance rifle = source.AddItemByDefinitionId(Rifle, 1);
                Require(rifle.TrySetFirearmState("core:ammo_303_british_01_profile", 10, out _), "Loaded firearm setup failed.");
                double emptyRifle = GameDataManager.Instance.Database.GetItem(Rifle).physical.weight_kg.Value;
                profile.max_content_weight_kg = (float)(emptyRifle + unit * 9d);
                before = Fingerprint(source, storage);
                Require(!GridStorageTransferService.TransferStackAuto(source, storage, rifle.InstanceId, default).Success &&
                    before == Fingerprint(source, storage), "Structural capacity omitted internal loaded-ammo mass.");
                profile.max_content_weight_kg = (float)(emptyRifle + unit * 10d);
                Require(GridStorageTransferService.TransferStackAuto(source, storage, rifle.InstanceId, default).Success,
                    "Equal loaded-firearm mass was rejected.");
                Drain(storage, source);
                Require(GridStorageTransferService.TransferStackAuto(source, inventory, rifle.InstanceId, default).Success, "Rifle personal setup failed.");
                Equip(equipment, inventory, rifle.InstanceId, ActorEquipmentComponent.HandLeftSlotId, ActorEquipmentComponent.HandRightSlotId);
                profile.max_content_weight_kg = (float)(emptyRifle + unit * 9d);
                EquipmentStorageTransferPlan equipmentPlan = equipment.PreviewTransferEquippedToStorage(rifle.InstanceId, storage, default);
                Require(!equipmentPlan.Success && equipment.IsEquipped(rifle.InstanceId) && storage.GridStorageEntries.Count == 0,
                    "Equipment ingress bypassed structural capacity.");
                profile.max_content_weight_kg = (float)(emptyRifle + unit * 10d);
                equipmentPlan = equipment.PreviewTransferEquippedToStorage(rifle.InstanceId, storage, default);
                Require(equipmentPlan.Success && equipment.TransferEquippedToStorage(storage, equipmentPlan, default).Success &&
                    !equipment.IsEquipped(rifle.InstanceId), "Equipment ingress below/equal cap failed.");
                Drain(storage, source); Clear(source);
                Debug.Log("[IMPL0066][STORAGE] grid/kg independent, default unlimited, quantity/full/exact/actual merge, loaded ammo, nesting, Equipment and atomic state: PASS");

                profile.max_content_weight_kg = originalMaximum;
                profile.carried_weight_multiplier = originalMultiplier;
                rounds = inventory.AddItemByDefinitionId(Ammo, 400);
                CarryWeightSnapshot personal = Snapshot(carry);
                Require(GridStorageTransferService.TransferStackAuto(inventory, storage, rounds.InstanceId, default).Success, "Content move failed.");
                CarryWeightSnapshot unequipped = Snapshot(carry);
                Equal(personal.CurrentWeightKg, unequipped.CurrentWeightKg, "Physical mass changed moving to unequipped carrier.");
                Equal(personal.EffectiveLoadKg, unequipped.EffectiveLoadKg, "Unequipped carrier applied ergonomics.");
                Equip(equipment, inventory, backpack.InstanceId, ActorEquipmentComponent.BackSlotId);
                CarryWeightSnapshot equipped = Snapshot(carry);
                double ownMass = GameDataManager.Instance.Database.GetItem(backpack.DefinitionId).physical.weight_kg.Value;
                Equal(equipped.CurrentWeightKg, ownMass + unit * 400d, "Physical carrier subtree mass changed.");
                Equal(equipped.EffectiveLoadKg, ownMass + unit * 400d * originalMultiplier.Value, "Own mass was discounted or content benefit missing.");
                Unequip(equipment, backpack.InstanceId);
                Equal(Snapshot(carry).EffectiveLoadKg, unequipped.EffectiveLoadKg, "Unequip did not immediately remove ergonomics.");
                Equip(equipment, inventory, backpack.InstanceId, ActorEquipmentComponent.BackSlotId);
                Require(GridStorageTransferService.TransferStackAuto(storage, inventory, rounds.InstanceId, default).Success, "Move out failed.");
                Equal(Snapshot(carry).CurrentWeightKg, equipped.CurrentWeightKg, "Physical mass changed on equipped carrier egress.");
                Equal(Snapshot(carry).EffectiveLoadKg, unequipped.EffectiveLoadKg, "Effective load failed to rise on carrier egress.");
                Require(GridStorageTransferService.TransferStackAuto(inventory, storage, rounds.InstanceId, default).Success, "Move back failed.");
                Set(carry, "baseCarryCapacityKg", (float)(equipped.EffectiveLoadKg / 0.75d));
                Require(carry.State == CarryWeightState.Normal && carry.LocomotionFactor == 1f, "75% effective threshold failed.");
                Set(carry, "baseCarryCapacityKg", (float)(equipped.EffectiveLoadKg / 0.875d));
                Require(carry.State == CarryWeightState.Encumbered && carry.CurrentWeightKg > carry.CarryCapacityKg &&
                    Math.Abs(carry.LocomotionFactor - 0.575f) < 0.00001f, "Carry thresholds used physical mass instead of effective load.");
                Unequip(equipment, backpack.InstanceId);
                Require(carry.State == CarryWeightState.Overloaded && carry.LocomotionFactor == 0f, "Unequip did not change derived locomotion.");
                Set(carry, "baseCarryCapacityKg", (float)(equipped.EffectiveLoadKg * 0.99d));
                Equip(equipment, inventory, backpack.InstanceId, ActorEquipmentComponent.BackSlotId);
                Require(carry.State == CarryWeightState.Overloaded && carry.LocomotionFactor == 0f, "Effective overload no longer stops locomotion.");
                Require(GridStorageTransferService.TransferQuantityAuto(storage, inventory, rounds.InstanceId, 100, true, default).Success,
                    "Overloaded actor could not reorganize content.");
                Require(GridStorageTransferService.TransferQuantityAuto(inventory, source, inventory.Entries.First(e => e.DefinitionId == Ammo).Item.InstanceId,
                    100, true, default).Success && carry.State != CarryWeightState.Overloaded && carry.LocomotionFactor > 0f,
                    "Unloading did not restore locomotion.");
                Set(carry, "baseCarryCapacityKg", initialCapacity);
                double physicalBeforeDrop = carry.CurrentWeightKg;
                ItemWeightResolver.TryGetEntryWeight(equipment.Entries.Single(), 1, out double subtreeMass, out _);
                Require(DroppedWorldItemSpawner.TryDrop(equipment, backpack.InstanceId, inventory, "core:drop", "Drop", out string dropError),
                    "Loaded backpack drop failed: " + dropError);
                Equal(physicalBeforeDrop - carry.CurrentWeightKg, subtreeMass, "Drop did not remove the complete subtree once.");
                WorldItemPickup pickup = UnityEngine.Object.FindObjectsByType<WorldItemPickup>(FindObjectsInactive.Exclude)
                    .Single(world => world.GridStorageEntries.Any(e => e.Item.InstanceId == backpack.InstanceId));
                Require(pickup.PickUp(player, pickup.GetComponent<WorldObjectTags>()).hasResult, "Loaded backpack pickup failed.");
                Equal(carry.CurrentWeightKg, physicalBeforeDrop, "Pickup changed physical mass.");
                Equal(carry.EffectiveLoadKg, carry.CurrentWeightKg, "Personal pickup retained ergonomic benefit.");
                Clear(source); UnityEngine.Object.DestroyImmediate(sourceRoot); sourceRoot = null;
                Debug.Log("[IMPL0066][CARRY] own/content physical mass, equip/unequip, placement ergonomics, effective thresholds, overload/unload and loaded carrier drop/pickup: PASS");

                // Seed a legacy over-cap state through previously unlimited ingress; restore must not use today's ingress rule.
                profile.max_content_weight_kg = null;
                Require(inventory.AddItemByDefinitionId(Ammo, 600) != null, "Legacy extra content setup failed.");
                foreach (ItemStorageEntry entry in inventory.Entries.Where(e => e.DefinitionId == Ammo).ToArray())
                    Require(GridStorageTransferService.TransferStackAuto(inventory, backpack.OwnedStorage, entry.Item.InstanceId, default).Success,
                        "Legacy content ingress setup failed.");
                profile.max_content_weight_kg = originalMaximum;
                Require(ContentMass(backpack.OwnedStorage) > originalMaximum.Value, "Legacy fixture is not actually over cap.");
                Equip(equipment, inventory, backpack.InstanceId, ActorEquipmentComponent.BackSlotId);
                CarryWeightSnapshot savedCarry = Snapshot(carry);
                CurrentSliceSaveData loaded = Capture();
                CurrentSliceLoadResult result = CurrentSliceLoadService.LoadPayload(CurrentSliceSnapshotService.ToPayload(loaded), "IMPL0066-over-cap");
                Require(result.Success && CurrentSliceSnapshotService.Compare(loaded, Capture()).Equivalent,
                    "Over-cap restore lost contents/equipment/identity: " + result.Failure);
                Equal(Snapshot(carry).CurrentWeightKg, savedCarry.CurrentWeightKg, "Restore changed physical mass.");
                Equal(Snapshot(carry).EffectiveLoadKg, savedCarry.EffectiveLoadKg, "Restore changed derived effective load.");
                backpack = equipment.GetEquippedInstance(ActorEquipmentComponent.BackSlotId);
                ItemInstance extra = inventory.AddItemByDefinitionId(Ammo, 1);
                before = Fingerprint(inventory, backpack.OwnedStorage);
                Require(!GridStorageTransferService.TransferStackAuto(inventory, backpack.OwnedStorage, extra.InstanceId, default).Success &&
                    before == Fingerprint(inventory, backpack.OwnedStorage), "Restored over-cap storage accepted future ingress.");
                foreach (ItemStorageEntry entry in backpack.OwnedStorage.GridStorageEntries.ToArray())
                    Require(GridStorageTransferService.TransferStackAuto(backpack.OwnedStorage, inventory, entry.Item.InstanceId, default).Success,
                        "Over-cap restored content could not be removed.");
                Require(GridStorageTransferService.TransferQuantityAuto(inventory, backpack.OwnedStorage, extra.InstanceId, 1, true, default).Success,
                    "Reducing over-cap content did not restore ingress.");
                Debug.Log("[IMPL0066][PERSISTENCE] real Current Slice capture/load, over-cap exact preservation, derived physical/effective, future-ingress rejection and recovery: PASS");
            }
            finally
            {
                profile.max_content_weight_kg = originalMaximum; profile.carried_weight_multiplier = originalMultiplier;
                Set(carry, "baseCarryCapacityKg", initialCapacity);
                if (sourceRoot != null) { Clear(sourceRoot.GetComponent<InventoryComponent>()); UnityEngine.Object.DestroyImmediate(sourceRoot); }
                CurrentSliceLoadResult cleanup = CurrentSliceLoadService.LoadPayload(CurrentSliceSnapshotService.ToPayload(initial), "IMPL0066-cleanup");
                Require(cleanup.Success && CurrentSliceSnapshotService.Compare(initial, Capture()).Equivalent,
                    "Initial Current Slice cleanup failed: " + cleanup.Failure);
            }
        }

        private static string Fingerprint(params IGridStorageOwner[] owners) => string.Join("|", owners.Select(owner =>
        {
            object backend = owner.GetType().GetProperty(owner is InventoryComponent ? "InternalGridBackend" : "Backend", PrivateInstance).GetValue(owner);
            object version = backend.GetType().GetProperty("StorageVersion", PrivateInstance).GetValue(backend);
            object layout = backend.GetType().GetProperty("LayoutVersion", PrivateInstance).GetValue(backend);
            return version + ":" + layout + ":" + string.Join(";", owner.GridStorageEntries.Select(entry =>
            {
                owner.TryGetGridPlacement(entry.Item.InstanceId, out GridPlacement placement);
                ItemOwnedStorageRegistry.Instance.TryResolveRootOwner(entry.Item.InstanceId, out object root, out string error);
                return entry.Item.InstanceId + ":" + entry.Quantity + ":" + JsonConvert.SerializeObject(placement) + ":" + root?.GetHashCode() + ":" + error;
            }));
        }));
        private static void Drain(ItemOwnedStorageRuntime storage, InventoryComponent target)
        {
            foreach (ItemStorageEntry entry in storage.GridStorageEntries.ToArray())
                Require(GridStorageTransferService.TransferStackAuto(storage, target, entry.Item.InstanceId, default).Success, "Fixture content removal failed.");
        }
        private static void Clear(InventoryComponent inventory)
        {
            for (int i = inventory.Entries.Count - 1; i >= 0; i--)
                Require(inventory.TryRemoveItemAt(i, inventory.Entries[i].Quantity), "Fixture clear failed.");
        }
        private static void Equip(ActorEquipmentComponent equipment, InventoryComponent inventory, string id, params string[] slots)
        {
            EquipmentPreview plan = equipment.PreviewEquip(inventory, id, slots);
            Require(plan.Success && equipment.Equip(inventory, plan).Success, "Fixture equip failed: " + plan.Message);
        }
        private static void Unequip(ActorEquipmentComponent equipment, string id)
        {
            EquipmentPreview plan = equipment.PreviewUnequip(id);
            Require(plan.Success && equipment.Unequip(plan).Success, "Fixture unequip failed: " + plan.Message);
        }
        private static CarryWeightSnapshot Snapshot(ActorCarryWeightComponent carry)
        {
            CarryWeightSnapshot snapshot = carry.GetSnapshot(); Require(snapshot.IsValid, snapshot.Error); return snapshot;
        }
        private static double ContentMass(ItemOwnedStorageRuntime storage)
        {
            double total = 0d;
            foreach (ItemStorageEntry entry in storage.GridStorageEntries)
            {
                Require(ItemWeightResolver.TryGetEntryWeight(entry, entry.Quantity, out double mass, out string error), error); total += mass;
            }
            return total;
        }
        private static CurrentSliceSaveData Capture()
        {
            CurrentSliceResult result = CurrentSliceSnapshotService.Capture(); Require(result.Success, "Capture failed: " + result.Failure); return result.Snapshot;
        }
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, PrivateInstance).SetValue(owner, value);
        private static void Equal(double a, double b, string error) => Require(Math.Abs(a - b) <= 0.000001d, error + $" ({a:R} vs {b:R})");
        private static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
    }
}
