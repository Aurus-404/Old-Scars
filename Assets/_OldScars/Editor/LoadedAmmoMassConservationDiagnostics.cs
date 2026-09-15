using System;
using System.IO;
using System.Linq;
using OldScars.Core;
using OldScars.Core.Actors;
using OldScars.Core.Combat;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Data.Validation;
using OldScars.Core.Interactions;
using OldScars.Core.Items;
using OldScars.Core.Persistence;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OldScars.Editor
{
    [InitializeOnLoad]
    public static class LoadedAmmoMassConservationDiagnostics
    {
        private const string Menu = "Old Scars/Diagnostics/Items/Run Loaded Ammo Mass Conservation";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string PhaseKey = "OldScars.Issue0022.Phase";
        private const string RootKey = "OldScars.Issue0022.Root";
        private const string ErrorKey = "OldScars.Issue0022.Error";
        private const string InitialSlot = "issue0022_initial";
        private const string LoadedSlot = "issue0022_loaded";
        private const string RifleId = "core:lee_enfield_rifle_01";
        private const string AmmoItemId = "core:ammo_303_british_01";
        private const string AmmoProfileId = "core:ammo_303_british_01_profile";
        private const double Epsilon = 0.000001d;

        static LoadedAmmoMassConservationDiagnostics() => EditorApplication.update += Continue;

        [MenuItem(Menu)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Loaded ammo mass diagnostics require idle Edit Mode.");

            ValidateDataContractFixtures();
            ClearSession();
            string root = Path.Combine(Path.GetTempPath(), "OldScars_Issue0022_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            SessionState.SetString(RootKey, root);
            SessionState.SetString(ErrorKey, string.Empty);
            SessionState.SetString(PhaseKey, "enter");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void Continue()
        {
            string phase = SessionState.GetString(PhaseKey, string.Empty);
            if (string.IsNullOrWhiteSpace(phase))
                return;

            if (phase == "enter" && Ready())
            {
                try
                {
                    WorldClock.Current.AdvanceDuringGameplay = false;
                    RunPlayModeCases();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    SessionState.SetString(ErrorKey, exception.Message);
                }

                SessionState.SetString(PhaseKey, "finish");
                EditorApplication.ExitPlaymode();
            }
            else if (phase == "finish" && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                FinalizeRun();
            }
        }

        private static bool Ready() =>
            EditorApplication.isPlaying && Time.frameCount >= 5 && WorldClock.Current != null &&
            GameDataManager.Instance != null && GameDataManager.Instance.IsReady;

        private static void RunPlayModeCases()
        {
            ActorInteractionContext player = UnityEngine.Object
                .FindObjectsByType<ActorInteractionContext>(FindObjectsInactive.Exclude)
                .Single(candidate => candidate.ActorTags.Contains("player"));
            ActorItemOwnershipComponent ownership = player.GetComponent<ActorItemOwnershipComponent>();
            InventoryComponent inventory = ownership?.PersonalInventory;
            ActorEquipmentComponent equipment = ownership?.Equipment;
            ActorCarryWeightComponent carry = player.GetComponent<ActorCarryWeightComponent>();
            DebugActionProgressController progress = UnityEngine.Object.FindAnyObjectByType<DebugActionProgressController>();
            Require(ownership != null && inventory != null && equipment != null && carry != null && progress != null,
                "Player fixture lacks ownership, inventory, equipment, carry-weight or action-progress authority.");

            CurrentSliceSaveData initial = Capture("initial");
            Write(InitialSlot, initial);

            AmmoProfileDefinition ammoProfile = GameDataManager.Instance.Database.GetAmmoProfile(AmmoProfileId);
            Require(ammoProfile != null && Nearly(ammoProfile.round_weight_kg, 0.025d),
                "Core .303 round_weight_kg is not the expected 0.025 kg.");

            ItemInstance rifle = inventory.AddItemByDefinitionId(RifleId, 1);
            ItemInstance looseAmmo = inventory.AddItemByDefinitionId(AmmoItemId, 7);
            Require(rifle != null && looseAmmo != null, "Could not create rifle/ammo fixtures.");
            EquipRifle(equipment, inventory, rifle.InstanceId);

            double emptyBaseline = Weight(carry);
            int ammoBeforeCancel = Quantity(ownership, AmmoItemId);
            bool completionCalled = false;
            Require(progress.TryStartTimedOperation(1f, "Reload", rifle.InstanceId, () =>
                {
                    completionCalled = true;
                    return DebugActionExecutionResult.Info("Reload", "unexpected");
                }) && progress.TryCancelActiveAction("ISSUE-0022 cancellation") && !completionCalled &&
                rifle.LoadedRounds == 0 && Quantity(ownership, AmmoItemId) == ammoBeforeCancel && Nearly(Weight(carry), emptyBaseline),
                "Cancelled reload mutated ammo, firearm state or carried mass.");

            WeaponCombatResult partial = WeaponCombatService.ReloadEquipped(ownership, rifle.InstanceId);
            Require(partial.Success && partial.Quantity == 7 && rifle.LoadedRounds == 7 &&
                    Quantity(ownership, AmmoItemId) == 0 && Nearly(Weight(carry), emptyBaseline),
                "Partial/insufficient reload did not conserve total carried mass: " + partial.Message);

            Require(inventory.AddItemByDefinitionId(AmmoItemId, 10) != null, "Could not add full-reload ammo fixture.");
            double beforeFull = Weight(carry);
            WeaponCombatResult full = WeaponCombatService.ReloadEquipped(ownership, rifle.InstanceId);
            Require(full.Success && full.Quantity == 3 && rifle.LoadedRounds == 10 &&
                    Quantity(ownership, AmmoItemId) == 7 && Nearly(Weight(carry), beforeFull),
                "Full reload did not conserve total carried mass: " + full.Message);

            string weightError = null;
            Require(TryFindEntry(ownership, rifle.InstanceId, out ItemStorageEntry rifleEntry) &&
                    ItemWeightResolver.TryGetEntryWeight(rifleEntry, 1, out double loadedRifleWeight, out weightError) &&
                    Nearly(loadedRifleWeight, 4.45d),
                "Loaded rifle did not resolve to 4.45 kg: " + weightError);

            CurrentSliceSaveData loaded = Capture("loaded firearm");
            Write(LoadedSlot, loaded);
            double beforeLoad = Weight(carry);
            CurrentSliceLoadResult load = CurrentSliceLoadService.Load(LoadedSlot, Store());
            Require(load.Success && Nearly(Weight(carry), beforeLoad),
                "Current Slice load changed derived loaded-firearm mass: " + load.Failure);
            rifle = ownership.GetAllOwnedEntries().Single(entry => entry.Item.InstanceId == rifle.InstanceId).Item;
            Require(rifle.LoadedRounds == 10 && rifle.LoadedAmmoProfileId == AmmoProfileId,
                "Current Slice load did not restore loaded firearm state.");

            EquipmentPreview unequip = equipment.PreviewUnequip(rifle.InstanceId);
            double beforeUnequip = Weight(carry);
            Require(unequip.Success && equipment.Unequip(unequip).Success && Nearly(Weight(carry), beforeUnequip),
                "Unequip within the same root owner changed carried mass.");
            EquipRifle(equipment, inventory, rifle.InstanceId);
            Require(Nearly(Weight(carry), beforeUnequip), "Equip within the same root owner changed carried mass.");

            RemoveAllPersonal(inventory);
            unequip = equipment.PreviewUnequip(rifle.InstanceId);
            Require(unequip.Success && equipment.Unequip(unequip).Success, "Could not prepare backpack transfer.");
            ItemInstance backpack = inventory.AddItemByDefinitionId("core:small_backpack_01", 1);
            ItemOwnedStorageRuntime backpackStorage = null;
            Require(backpack != null && ItemOwnedStorageRegistry.Instance.TryResolveOwnedStorage(
                    backpack.InstanceId, out backpackStorage),
                "Could not create item-owned backpack storage.");
            double beforeBackpackMove = Weight(carry);
            InventoryMutationResult intoBackpack = GridStorageTransferService.TransferStackAuto(
                inventory, backpackStorage, rifle.InstanceId, GridStorageTransferQuantityPolicy.Exact, default);
            Require(intoBackpack.Success && Nearly(Weight(carry), beforeBackpackMove),
                "Moving loaded firearm into owned backpack changed carried mass: " + intoBackpack.Message);
            InventoryMutationResult outOfBackpack = GridStorageTransferService.TransferStackAuto(
                backpackStorage, inventory, rifle.InstanceId, GridStorageTransferQuantityPolicy.Exact, default);
            Require(outOfBackpack.Success && Nearly(Weight(carry), beforeBackpackMove),
                "Moving loaded firearm out of owned backpack changed carried mass: " + outOfBackpack.Message);
            EquipRifle(equipment, inventory, rifle.InstanceId);

            double beforeOneShot = Weight(carry);
            WeaponCombatResult oneShot = WeaponCombatService.FireEquipped(ownership, rifle.InstanceId, null, Vector3.zero);
            Require(oneShot.Code == WeaponCombatCode.Miss && rifle.LoadedRounds == 9 &&
                    Nearly(Weight(carry), beforeOneShot - ammoProfile.round_weight_kg),
                "One fired round did not reduce carried mass by round_weight_kg.");

            double beforeMultiple = Weight(carry);
            for (int index = 0; index < 3; index++)
                Require(WeaponCombatService.FireEquipped(ownership, rifle.InstanceId, null, Vector3.zero).Code == WeaponCombatCode.Miss,
                    "Multi-fire fixture did not consume a productive round.");
            Require(rifle.LoadedRounds == 6 && Nearly(Weight(carry), beforeMultiple - 3d * ammoProfile.round_weight_kg),
                "Multiple fired rounds did not reduce carried mass cumulatively.");

            Require(rifle.TrySetFirearmState(null, 0, out string stateFailure), "Could not prepare dry-fire fixture: " + stateFailure);
            double beforeDry = Weight(carry);
            Require(WeaponCombatService.FireEquipped(ownership, rifle.InstanceId, null, Vector3.zero).Code == WeaponCombatCode.Unloaded &&
                    Nearly(Weight(carry), beforeDry),
                "Dry fire changed carried mass.");

            Require(rifle.TrySetFirearmState("example:missing_profile", 1, out stateFailure),
                "Could not prepare invalid-profile fixture: " + stateFailure);
            string invalidError = null;
            Require(TryFindEntry(ownership, rifle.InstanceId, out rifleEntry) &&
                    !ItemWeightResolver.TryGetEntryWeight(rifleEntry, 1, out _, out invalidError) &&
                    invalidError.Contains("example:missing_profile"),
                "Invalid loaded-ammo state was silently treated as zero mass.");
            Require(rifle.TrySetFirearmState(AmmoProfileId, 10, out stateFailure),
                "Could not restore loaded drop fixture: " + stateFailure);

            double beforeDrop = Weight(carry);
            Require(DroppedWorldItemSpawner.TryDrop(equipment, rifle.InstanceId, inventory, "core:drop", "Drop", out string dropFailure),
                "Could not drop loaded rifle: " + dropFailure);
            double afterDrop = Weight(carry);
            Require(Nearly(beforeDrop - afterDrop, 4.45d),
                $"Dropping loaded rifle removed {beforeDrop - afterDrop:0.######} kg instead of 4.45 kg.");
            WorldItemPickup pickup = UnityEngine.Object.FindObjectsByType<WorldItemPickup>(FindObjectsInactive.Exclude)
                .Single(candidate => candidate.GridStorageEntries.Any(entry => entry.Item.InstanceId == rifle.InstanceId));
            DebugActionExecutionResult pickupResult = pickup.PickUp(player, pickup.GetComponent<WorldObjectTags>());
            Require(pickupResult.hasResult && Nearly(Weight(carry), beforeDrop),
                "Picking up loaded rifle did not restore its full internal mass: " + pickupResult.body);

            CurrentSliceLoadResult cleanup = CurrentSliceLoadService.Load(InitialSlot, Store());
            Require(cleanup.Success, "Initial-state cleanup failed: " + cleanup.Failure);
            CurrentSliceComparisonResult comparison = CurrentSliceSnapshotService.Compare(initial, Capture("cleanup"));
            Require(comparison.Equivalent, "Diagnostic cleanup did not restore initial state: " + comparison.Difference);
        }

        private static void ValidateDataContractFixtures()
        {
            var profile = new AmmoProfileDefinition { id = "example:shared_round", round_weight_kg = 0.025f };
            var first = AmmoItem("example:first_round", profile.id, 0.025f);
            var second = AmmoItem("example:second_round", profile.id, 0.025f);
            Require(DataValidator.TryValidateAmmoItemRoundWeight(first, profile, out _) &&
                    DataValidator.TryValidateAmmoItemRoundWeight(second, profile, out _),
                "Two mod item definitions could not share one canonical ammo-profile round mass.");
            second.physical.weight_kg = 0.026f;
            Require(!DataValidator.TryValidateAmmoItemRoundWeight(second, profile, out string mismatch) &&
                    mismatch.Contains(second.id) && mismatch.Contains(profile.id),
                "Ammo item/profile mass mismatch did not fail with both content IDs.");
        }

        private static ItemDefinition AmmoItem(string id, string profileId, float weightKg) => new ItemDefinition
        {
            id = id,
            ammo_profile_id = profileId,
            physical = new ItemPhysical { weight_kg = weightKg }
        };

        private static double Weight(ActorCarryWeightComponent carry)
        {
            CarryWeightSnapshot snapshot = carry.GetSnapshot();
            Require(snapshot.IsValid, "Carry weight snapshot is invalid: " + snapshot.Error);
            return snapshot.CurrentWeightKg;
        }

        private static bool TryFindEntry(ActorItemOwnershipComponent ownership, string instanceId, out ItemStorageEntry entry)
        {
            entry = ownership.GetAllOwnedEntries().FirstOrDefault(candidate => candidate?.Item?.InstanceId == instanceId);
            return entry != null;
        }

        private static int Quantity(ActorItemOwnershipComponent ownership, string definitionId) =>
            ownership.GetAllOwnedEntries().Where(entry => entry.DefinitionId == definitionId).Sum(entry => entry.Quantity);

        private static void RemoveAllPersonal(InventoryComponent inventory)
        {
            while (inventory.Entries.Count > 0)
            {
                ItemStorageEntry entry = inventory.Entries[0];
                Require(entry != null && inventory.TryRemoveItemAt(0, entry.Quantity),
                    "Could not clear personal inventory for the owned-backpack fixture.");
            }
        }

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

        private static CurrentSliceSaveData Capture(string label)
        {
            CurrentSliceResult result = CurrentSliceSnapshotService.Capture();
            Require(result.Success, label + " capture failed: " + result.Failure);
            return result.Snapshot;
        }

        private static PersistenceFileStore Store() => new PersistenceFileStore(SessionState.GetString(RootKey, string.Empty));

        private static void Write(string slot, CurrentSliceSaveData data)
        {
            PersistenceWriteResult result = Store().Write(slot, CurrentSliceSnapshotService.ToPayload(data));
            Require(result.Success, $"Slot '{slot}' write failed: {result.Failure}");
        }

        private static bool Nearly(double left, double right) => Math.Abs(left - right) <= Epsilon;
        private static void Require(bool condition, string failure) { if (!condition) throw new InvalidOperationException(failure); }

        private static void FinalizeRun()
        {
            string failure = SessionState.GetString(ErrorKey, string.Empty);
            string root = SessionState.GetString(RootKey, string.Empty);
            if (EditorSceneManager.GetActiveScene().isDirty)
                failure = Append(failure, "Diagnostics left SampleScene dirty.");
            try
            {
                if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
                    Directory.Delete(root, true);
            }
            catch (Exception exception)
            {
                failure = Append(failure, "Temporary cleanup failed: " + exception.Message);
            }

            bool success = string.IsNullOrWhiteSpace(failure);
            ClearSession();
            if (success)
                Debug.Log("Loaded Ammo Mass Conservation Diagnostics: PASS");
            else
                Debug.LogError("Loaded Ammo Mass Conservation Diagnostics: FAIL\n- " + failure);
            if (Application.isBatchMode)
                EditorApplication.Exit(success ? 0 : 1);
        }

        private static string Append(string current, string value) =>
            string.IsNullOrWhiteSpace(current) ? value : current + "\n- " + value;

        private static void ClearSession()
        {
            SessionState.EraseString(PhaseKey);
            SessionState.EraseString(RootKey);
            SessionState.EraseString(ErrorKey);
        }
    }
}
