using System;
using System.Collections.Generic;
using OldScars.Core.Actors;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;

namespace OldScars.Core.Items
{
    public static class EquipmentTransactionService
    {
        private const string LegacyRightHandSlotId = "right_hand";

        public static IReadOnlyList<EquipmentSlotSet> GetCompatibleSlotSets(
            ActorEquipmentComponent equipment,
            string instanceId,
            bool availableOnly)
        {
            var result = new List<EquipmentSlotSet>();
            if (!TryResolvePersonalEntry(equipment, instanceId, out ItemStorageEntry entry, out _))
                return result;

            ItemDefinition definition = ResolveItemDefinition(entry.DefinitionId);
            string[][] candidates = ResolveSlotSets(definition);
            if (candidates == null)
                return result;

            for (int index = 0; index < candidates.Length; index++)
            {
                string[] candidate = candidates[index];
                if (!IsSlotSetCompatible(equipment, candidate, availableOnly, instanceId))
                    continue;
                result.Add(new EquipmentSlotSet(candidate));
            }
            return result;
        }

        public static EquipmentPreview PreviewEquip(
            ActorEquipmentComponent equipment,
            string instanceId,
            IReadOnlyList<string> requestedSlotSet)
        {
            if (!TryResolvePersonalEntry(equipment, instanceId, out ItemStorageEntry entry, out string error))
                return Invalid(error, instanceId, equipment);
            if (entry.Quantity != 1)
                return Invalid($"Item instance '{instanceId}' must have quantity 1 to equip.", instanceId, equipment);

            ItemDefinition definition = ResolveItemDefinition(entry.DefinitionId);
            if (!IsEquipEnabled(definition))
                return Invalid($"Item '{entry.DefinitionId}' is not equipable.", instanceId, equipment);
            if (definition.max_stack != 1)
                return Invalid($"Equipable item '{entry.DefinitionId}' must use max_stack = 1.", instanceId, equipment);

            IReadOnlyList<EquipmentSlotSet> available = GetCompatibleSlotSets(equipment, instanceId, true);
            if (requestedSlotSet == null || requestedSlotSet.Count == 0)
            {
                if (available.Count == 0)
                    return Invalid("No compatible free slot set is available.", instanceId, equipment);
                if (available.Count > 1)
                    return ChoiceRequired(instanceId, equipment);
                return Valid(instanceId, available[0].SlotIds, null, equipment);
            }

            string[] requested = Copy(requestedSlotSet);
            bool declared = false;
            IReadOnlyList<EquipmentSlotSet> compatible = GetCompatibleSlotSets(equipment, instanceId, false);
            for (int index = 0; index < compatible.Count; index++)
            {
                if (SameSlots(compatible[index].SlotIds, requested))
                {
                    declared = true;
                    break;
                }
            }
            if (!declared)
                return Invalid("Requested slots are not a declared complete alternative for this item.", instanceId, equipment);
            if (!IsSlotSetCompatible(equipment, requested, true, instanceId))
                return Invalid("One or more requested equipment slots are occupied or unavailable.", instanceId, equipment);

            return Valid(instanceId, requested, null, equipment);
        }

        public static EquipmentMutationResult Equip(
            ActorEquipmentComponent equipment,
            EquipmentPreview preview)
        {
            if (equipment == null || preview == null || !preview.Success || preview.RequiresChoice)
                return EquipmentMutationResult.Rejected(preview?.Message ?? "A valid equip preview is required.", preview?.InstanceId);
            if (!VersionsMatch(equipment, preview))
                return EquipmentMutationResult.Rejected("Equipment preview is stale; retry the operation.", preview.InstanceId);

            EquipmentPreview current = PreviewEquip(equipment, preview.InstanceId, preview.SlotIds);
            if (!current.Success || current.RequiresChoice)
                return EquipmentMutationResult.Rejected(current.Message, preview.InstanceId);

            InventoryComponent inventory = equipment.PersonalInventory;
            ActorItemOwnershipComponent ownership = equipment.Ownership;
            GridInventoryBackend.BackendStateSnapshot personalSnapshot = inventory.InternalGridBackend.CaptureBackendState();
            GridInventoryBackend.BackendStateSnapshot equipmentStorageSnapshot = equipment.Backend.CaptureBackendState();
            ActorEquipmentComponent.EquipmentStateSnapshot slotSnapshot = equipment.CaptureEquipmentState();
            int idSequenceSnapshot = ItemInstance.CaptureIdSequence();

            try
            {
                ItemStorageEntry sourceEntry = inventory.GetStorageEntryByInstanceId(preview.InstanceId);
                ItemInstance item = sourceEntry != null ? sourceEntry.Item : null;
                InventoryMutationResult transfer = inventory.InternalGridBackend.TransferTo(
                    equipment.Backend,
                    preview.InstanceId,
                    1);
                if (!transfer.Success)
                    throw new InvalidOperationException(transfer.Message ?? "Equipment storage transfer failed.");

                equipment.AssignSlots(preview.InstanceId, preview.SlotIds);
                inventory.ClearLegacyRightHandForEquipmentAuthority();
                if (ownership == null)
                    throw new InvalidOperationException("Actor ownership validation failed after equip.");
                if (!ownership.ValidateUniqueOwnership(out string ownershipError))
                    throw new InvalidOperationException(ownershipError ?? "Actor ownership validation failed after equip.");

                equipment.RecordEquipped(item);
                return new EquipmentMutationResult(true, "Item equipped.", preview.InstanceId, preview.SlotIds);
            }
            catch (Exception exception)
            {
                inventory.InternalGridBackend.RestoreBackendState(personalSnapshot);
                equipment.Backend.RestoreBackendState(equipmentStorageSnapshot);
                equipment.RestoreEquipmentState(slotSnapshot);
                ItemInstance.RestoreIdSequence(idSequenceSnapshot);
                return EquipmentMutationResult.Rejected($"Equip rolled back: {exception.Message}", preview.InstanceId);
            }
        }

        public static EquipmentPreview PreviewUnequip(
            ActorEquipmentComponent equipment,
            string instanceId)
        {
            if (equipment == null || equipment.PersonalInventory == null || equipment.Ownership == null)
                return Invalid("Actor equipment dependencies are missing.", instanceId, equipment);
            if (!equipment.Ownership.TryLocateInstance(instanceId, out ActorItemStorageNodeKind node, out ItemStorageEntry entry) ||
                node != ActorItemStorageNodeKind.Equipment || entry == null || entry.Item == null)
            {
                return Invalid($"Equipped item instance '{instanceId}' was not found.", instanceId, equipment);
            }

            IReadOnlyList<string> occupied = equipment.GetSlotsOccupiedBy(instanceId);
            if (occupied.Count == 0)
                return Invalid($"Item instance '{instanceId}' has no occupied slots.", instanceId, equipment);

            GridPlacementValidationResult placementPreview = equipment.Backend.PreviewTransferTo(
                equipment.PersonalInventory.InternalGridBackend,
                instanceId);
            if (!placementPreview.IsValid)
                return Invalid(placementPreview.Message ?? "Personal inventory has no valid destination placement.", instanceId, equipment);

            return Valid(instanceId, Copy(occupied), placementPreview.Candidate, equipment);
        }

        public static EquipmentMutationResult Unequip(
            ActorEquipmentComponent equipment,
            EquipmentPreview preview)
        {
            if (equipment == null || preview == null || !preview.Success)
                return EquipmentMutationResult.Rejected(preview?.Message ?? "A valid unequip preview is required.", preview?.InstanceId);
            if (!VersionsMatch(equipment, preview))
                return EquipmentMutationResult.Rejected("Equipment preview is stale; retry the operation.", preview.InstanceId);

            EquipmentPreview current = PreviewUnequip(equipment, preview.InstanceId);
            if (!current.Success)
                return EquipmentMutationResult.Rejected(current.Message, preview.InstanceId);

            InventoryComponent inventory = equipment.PersonalInventory;
            ActorItemOwnershipComponent ownership = equipment.Ownership;
            GridInventoryBackend.BackendStateSnapshot personalSnapshot = inventory.InternalGridBackend.CaptureBackendState();
            GridInventoryBackend.BackendStateSnapshot equipmentStorageSnapshot = equipment.Backend.CaptureBackendState();
            ActorEquipmentComponent.EquipmentStateSnapshot slotSnapshot = equipment.CaptureEquipmentState();
            int idSequenceSnapshot = ItemInstance.CaptureIdSequence();

            try
            {
                ItemStorageEntry sourceEntry = equipment.Backend.Storage.GetEntryByInstanceId(preview.InstanceId);
                ItemInstance item = sourceEntry != null ? sourceEntry.Item : null;
                InventoryMutationResult transfer = equipment.Backend.TransferTo(
                    inventory.InternalGridBackend,
                    preview.InstanceId,
                    1);
                if (!transfer.Success)
                    throw new InvalidOperationException(transfer.Message ?? "Personal inventory transfer failed.");

                equipment.ClearSlots(preview.InstanceId);
                inventory.ClearLegacyRightHandForEquipmentAuthority();
                if (ownership == null)
                    throw new InvalidOperationException("Actor ownership validation failed after unequip.");
                if (!ownership.ValidateUniqueOwnership(out string ownershipError))
                    throw new InvalidOperationException(ownershipError ?? "Actor ownership validation failed after unequip.");

                equipment.RecordUnequipped(item);
                return new EquipmentMutationResult(true, "Item unequipped to personal inventory.", preview.InstanceId, preview.SlotIds);
            }
            catch (Exception exception)
            {
                inventory.InternalGridBackend.RestoreBackendState(personalSnapshot);
                equipment.Backend.RestoreBackendState(equipmentStorageSnapshot);
                equipment.RestoreEquipmentState(slotSnapshot);
                ItemInstance.RestoreIdSequence(idSequenceSnapshot);
                return EquipmentMutationResult.Rejected($"Unequip rolled back: {exception.Message}", preview.InstanceId);
            }
        }

        private static bool TryResolvePersonalEntry(
            ActorEquipmentComponent equipment,
            string instanceId,
            out ItemStorageEntry entry,
            out string error)
        {
            entry = null;
            error = null;
            if (equipment == null || equipment.PersonalInventory == null || equipment.Ownership == null)
            {
                error = "Actor equipment dependencies are missing.";
                return false;
            }
            if (equipment.GetActiveLayout() == null)
            {
                error = $"Equipment layout '{equipment.EquipmentLayoutId}' is unavailable.";
                return false;
            }
            if (!equipment.Ownership.TryLocateInstance(instanceId, out ActorItemStorageNodeKind node, out entry) ||
                node != ActorItemStorageNodeKind.Personal || entry == null || entry.Item == null)
            {
                error = $"Personal item instance '{instanceId}' was not found.";
                return false;
            }
            return true;
        }

        private static bool IsSlotSetCompatible(
            ActorEquipmentComponent equipment,
            string[] slotIds,
            bool requireFree,
            string instanceId)
        {
            if (equipment == null || slotIds == null || slotIds.Length == 0)
                return false;
            var seen = new HashSet<string>();
            for (int index = 0; index < slotIds.Length; index++)
            {
                string slotId = slotIds[index];
                if (string.IsNullOrWhiteSpace(slotId) || !seen.Add(slotId) || !equipment.HasSlot(slotId))
                    return false;
                if (requireFree && !equipment.IsSlotFree(slotId, instanceId))
                    return false;
            }
            return true;
        }

        private static string[][] ResolveSlotSets(ItemDefinition definition)
        {
            if (definition?.equip == null)
                return null;
            if (definition.equip.slot_sets != null && definition.equip.slot_sets.Length > 0)
                return Copy(definition.equip.slot_sets);

            string[] occupied = definition.equip.occupied_slots;
            if (occupied != null && occupied.Length > 0)
                return new[] { MapLegacySlots(occupied) };

            string[] allowed = definition.equip.allowed_slots;
            if (allowed == null || allowed.Length == 0)
                return null;

            var alternatives = new string[allowed.Length][];
            for (int index = 0; index < allowed.Length; index++)
                alternatives[index] = new[] { MapLegacySlot(allowed[index]) };
            return alternatives;
        }

        private static string[] MapLegacySlots(string[] legacySlots)
        {
            var mapped = new string[legacySlots.Length];
            for (int index = 0; index < legacySlots.Length; index++)
                mapped[index] = MapLegacySlot(legacySlots[index]);
            return mapped;
        }

        private static string MapLegacySlot(string slotId)
        {
            return slotId == LegacyRightHandSlotId ? ActorEquipmentComponent.HandRightSlotId : slotId;
        }

        private static bool VersionsMatch(ActorEquipmentComponent equipment, EquipmentPreview preview)
        {
            InventoryComponent inventory = equipment.PersonalInventory;
            return inventory != null &&
                   inventory.InternalGridBackend.StorageVersion == preview.PersonalStorageVersion &&
                   inventory.InternalGridBackend.LayoutVersion == preview.PersonalLayoutVersion &&
                   equipment.StorageVersion == preview.EquipmentStorageVersion &&
                   equipment.Version == preview.EquipmentVersion;
        }

        private static EquipmentPreview Valid(
            string instanceId,
            string[] slotIds,
            GridPlacement placement,
            ActorEquipmentComponent equipment)
        {
            return CreatePreview(true, false, null, instanceId, slotIds, placement, equipment);
        }

        private static EquipmentPreview ChoiceRequired(string instanceId, ActorEquipmentComponent equipment)
        {
            return CreatePreview(true, true, "Choose one compatible slot alternative.", instanceId, null, null, equipment);
        }

        private static EquipmentPreview Invalid(string message, string instanceId, ActorEquipmentComponent equipment)
        {
            return CreatePreview(false, false, message, instanceId, null, null, equipment);
        }

        private static EquipmentPreview CreatePreview(
            bool success,
            bool requiresChoice,
            string message,
            string instanceId,
            string[] slotIds,
            GridPlacement placement,
            ActorEquipmentComponent equipment)
        {
            InventoryComponent inventory = equipment != null ? equipment.PersonalInventory : null;
            return new EquipmentPreview(
                success,
                requiresChoice,
                message,
                instanceId,
                slotIds,
                placement,
                inventory != null ? inventory.InternalGridBackend.StorageVersion : 0,
                inventory != null ? inventory.InternalGridBackend.LayoutVersion : 0,
                equipment != null ? equipment.StorageVersion : 0,
                equipment != null ? equipment.Version : 0);
        }

        private static ItemDefinition ResolveItemDefinition(string definitionId)
        {
            GameDatabase database = GameDataManager.Instance != null && GameDataManager.Instance.IsReady
                ? GameDataManager.Instance.Database
                : null;
            return database != null ? database.GetItem(definitionId) : null;
        }

        private static bool IsEquipEnabled(ItemDefinition definition)
        {
            if (definition?.equip?.equippable.HasValue == true)
                return definition.equip.equippable.Value;
            return definition != null && definition.equippable.GetValueOrDefault(false);
        }

        private static bool SameSlots(string[] left, string[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;
            for (int index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                    return false;
            }
            return true;
        }

        private static string[] Copy(IReadOnlyList<string> values)
        {
            if (values == null)
                return Array.Empty<string>();
            var result = new string[values.Count];
            for (int index = 0; index < values.Count; index++)
                result[index] = values[index];
            return result;
        }

        private static string[][] Copy(string[][] values)
        {
            var result = new string[values.Length][];
            for (int index = 0; index < values.Length; index++)
                result[index] = values[index] != null ? (string[])values[index].Clone() : Array.Empty<string>();
            return result;
        }
    }
}
