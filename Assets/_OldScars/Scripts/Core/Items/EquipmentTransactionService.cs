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
            if (!TryResolvePersonalEntry(equipment, instanceId, out ItemStorageEntry entry, out _, out _))
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
            if (!TryResolvePersonalEntry(
                    equipment,
                    instanceId,
                    out ItemStorageEntry entry,
                    out EquipmentFailureCode resolutionFailure,
                    out string error))
            {
                return Invalid(resolutionFailure, error, instanceId, equipment);
            }
            if (entry.Quantity != 1)
                return Invalid(EquipmentFailureCode.InvalidQuantity, $"Item instance '{instanceId}' must have quantity 1 to equip.", instanceId, equipment);

            ItemDefinition definition = ResolveItemDefinition(entry.DefinitionId);
            if (!IsEquipEnabled(definition))
                return Invalid(EquipmentFailureCode.NotEquipable, $"Item '{entry.DefinitionId}' is not equipable.", instanceId, equipment);
            if (definition.max_stack != 1)
                return Invalid(EquipmentFailureCode.InvalidQuantity, $"Equipable item '{entry.DefinitionId}' must use max_stack = 1.", instanceId, equipment);

            IReadOnlyList<EquipmentSlotSet> available = GetCompatibleSlotSets(equipment, instanceId, true);
            if (requestedSlotSet == null || requestedSlotSet.Count == 0)
            {
                if (available.Count == 0)
                    return Invalid(EquipmentFailureCode.SlotOccupied, "No compatible free slot set is available.", instanceId, equipment);
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
                return Invalid(EquipmentFailureCode.InvalidSlotSet, "Requested slots are not a declared complete alternative for this item.", instanceId, equipment);
            if (!IsSlotSetCompatible(equipment, requested, true, instanceId))
                return Invalid(EquipmentFailureCode.SlotOccupied, "One or more requested equipment slots are occupied or unavailable.", instanceId, equipment);

            return Valid(instanceId, requested, null, equipment);
        }

        public static EquipmentMutationResult Equip(
            ActorEquipmentComponent equipment,
            EquipmentPreview preview)
        {
            if (equipment == null || preview == null || !preview.Success || preview.RequiresChoice)
                return EquipmentMutationResult.Rejected(
                    preview?.Message ?? "A valid equip preview is required.",
                    preview?.InstanceId,
                    preview?.FailureCode ?? EquipmentFailureCode.InvalidPreview);
            if (!VersionsMatch(equipment, preview))
                return EquipmentMutationResult.Rejected("Equipment preview is stale; retry the operation.", preview.InstanceId, EquipmentFailureCode.StaleState);

            EquipmentPreview current = PreviewEquip(equipment, preview.InstanceId, preview.SlotIds);
            if (!current.Success || current.RequiresChoice)
                return EquipmentMutationResult.Rejected(current.Message, preview.InstanceId, current.FailureCode);

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
                if (ownership == null)
                    throw new InvalidOperationException("Actor ownership validation failed after equip.");
                if (!ownership.ValidateUniqueOwnership(out string ownershipError))
                    throw new InvalidOperationException(ownershipError ?? "Actor ownership validation failed after equip.");

                inventory.ClearLegacyRightHandForEquipmentAuthority();
                equipment.RebindActorOwnedItems();
                equipment.RecordEquipped(item);
                return new EquipmentMutationResult(true, EquipmentFailureCode.None, "Item equipped.", preview.InstanceId, preview.SlotIds);
            }
            catch (Exception exception)
            {
                inventory.InternalGridBackend.RestoreBackendState(personalSnapshot);
                equipment.Backend.RestoreBackendState(equipmentStorageSnapshot);
                equipment.RestoreEquipmentState(slotSnapshot);
                ItemInstance.RestoreIdSequence(idSequenceSnapshot);
                equipment.RebindActorOwnedItems();
                return EquipmentMutationResult.Rejected($"Equip rolled back: {exception.Message}", preview.InstanceId, EquipmentFailureCode.StorageMutationFailed);
            }
        }

        public static EquipmentReplacementPlan PreviewEquipReplacing(
            ActorEquipmentComponent equipment,
            string instanceId,
            IReadOnlyList<string> requestedSlotSet)
        {
            if (!TryResolvePersonalEntry(
                    equipment,
                    instanceId,
                    out ItemStorageEntry sourceEntry,
                    out EquipmentFailureCode resolutionFailure,
                    out string error))
            {
                return InvalidReplacement(resolutionFailure, error, instanceId, requestedSlotSet, null, equipment);
            }
            if (sourceEntry.Quantity != 1)
            {
                return InvalidReplacement(
                    EquipmentFailureCode.InvalidQuantity,
                    $"Item instance '{instanceId}' must have quantity 1 to equip.",
                    instanceId,
                    requestedSlotSet,
                    null,
                    equipment);
            }

            ItemDefinition definition = ResolveItemDefinition(sourceEntry.DefinitionId);
            if (!IsEquipEnabled(definition))
                return InvalidReplacement(EquipmentFailureCode.NotEquipable, $"Item '{sourceEntry.DefinitionId}' is not equipable.", instanceId, requestedSlotSet, null, equipment);
            if (definition.max_stack != 1)
                return InvalidReplacement(EquipmentFailureCode.InvalidQuantity, $"Equipable item '{sourceEntry.DefinitionId}' must use max_stack = 1.", instanceId, requestedSlotSet, null, equipment);

            string[] requested = Copy(requestedSlotSet);
            if (requested.Length == 0 || !IsDeclaredAlternative(equipment, instanceId, requested))
            {
                return InvalidReplacement(
                    EquipmentFailureCode.InvalidSlotSet,
                    "Requested slots are not a declared complete alternative for this item.",
                    instanceId,
                    requested,
                    null,
                    equipment);
            }

            var displacedIds = new List<string>();
            var uniqueDisplacedIds = new HashSet<string>();
            for (int index = 0; index < requested.Length; index++)
            {
                string slotId = requested[index];
                if (!equipment.HasSlot(slotId))
                    return InvalidReplacement(EquipmentFailureCode.InvalidSlotSet, $"Equipment slot '{slotId}' is unavailable.", instanceId, requested, null, equipment);
                if (equipment.TryGetSlotOccupant(slotId, out string occupiedBy) && uniqueDisplacedIds.Add(occupiedBy))
                    displacedIds.Add(occupiedBy);
            }

            if (displacedIds.Count == 0)
            {
                return InvalidReplacement(
                    EquipmentFailureCode.InvalidPreview,
                    "Replacement requires at least one occupied requested slot.",
                    instanceId,
                    requested,
                    null,
                    equipment);
            }

            ActorItemOwnershipComponent ownership = equipment.Ownership;
            string ownershipError = null;
            if (ownership == null || !ownership.ValidateUniqueOwnership(out ownershipError))
            {
                return InvalidReplacement(
                    EquipmentFailureCode.OwnershipChanged,
                    ownershipError ?? "Actor item ownership is invalid.",
                    instanceId,
                    requested,
                    null,
                    equipment);
            }

            var displacedEntries = new List<ItemStorageEntry>(displacedIds.Count);
            var displacementPlans = new EquipmentDisplacementPlan[displacedIds.Count];
            for (int index = 0; index < displacedIds.Count; index++)
            {
                string displacedId = displacedIds[index];
                if (!ownership.TryLocateInstance(displacedId, out ActorItemStorageNodeKind node, out ItemStorageEntry displacedEntry) ||
                    node != ActorItemStorageNodeKind.Equipment || displacedEntry?.Item == null)
                {
                    return InvalidReplacement(
                        EquipmentFailureCode.OwnershipChanged,
                        $"Displaced equipment instance '{displacedId}' is not uniquely owned by equipment storage.",
                        instanceId,
                        requested,
                        displacementPlans,
                        equipment);
                }

                string[] releasedSlots = Copy(equipment.GetSlotsOccupiedBy(displacedId));
                if (releasedSlots.Length == 0)
                {
                    return InvalidReplacement(
                        EquipmentFailureCode.OwnershipChanged,
                        $"Displaced equipment instance '{displacedId}' has no occupied slots.",
                        instanceId,
                        requested,
                        displacementPlans,
                        equipment);
                }

                displacedEntries.Add(displacedEntry);
                displacementPlans[index] = new EquipmentDisplacementPlan(displacedId, releasedSlots, null);
            }

            InventoryComponent inventory = equipment.PersonalInventory;
            if (!inventory.InternalGridBackend.TryReserveIncomingAfterRemoving(
                    instanceId,
                    displacedEntries,
                    out GridPlacement[] placements,
                    out InventoryMutationResult.MutationFailure placementFailure,
                    out string placementError))
            {
                return InvalidReplacement(
                    placementFailure == InventoryMutationResult.MutationFailure.NoGridSpace
                        ? EquipmentFailureCode.NoPersonalInventorySpace
                        : EquipmentFailureCode.StaleState,
                    placementError ?? "Displaced equipment does not fit in personal inventory.",
                    instanceId,
                    requested,
                    displacementPlans,
                    equipment);
            }

            for (int index = 0; index < displacementPlans.Length; index++)
            {
                EquipmentDisplacementPlan item = displacementPlans[index];
                displacementPlans[index] = new EquipmentDisplacementPlan(
                    item.InstanceId,
                    item.ReleasedSlotIds,
                    placements[index]);
            }

            return CreateReplacementPlan(
                true,
                EquipmentFailureCode.None,
                null,
                instanceId,
                requested,
                displacementPlans,
                equipment);
        }

        public static EquipmentMutationResult EquipReplacing(
            ActorEquipmentComponent equipment,
            EquipmentReplacementPlan plan)
        {
            if (equipment == null || plan == null || !plan.Success)
            {
                return EquipmentMutationResult.Rejected(
                    plan?.Message ?? "A valid equipment replacement plan is required.",
                    plan?.SourceInstanceId,
                    plan?.FailureCode ?? EquipmentFailureCode.InvalidPreview);
            }
            if (!ReplacementVersionsMatch(equipment, plan))
            {
                return EquipmentMutationResult.Rejected(
                    "Equipment replacement plan is stale; retry the operation.",
                    plan.SourceInstanceId,
                    EquipmentFailureCode.StaleState);
            }

            ActorItemOwnershipComponent ownership = equipment.Ownership;
            string ownershipError = null;
            if (ownership == null || !ownership.ValidateUniqueOwnership(out ownershipError))
            {
                return EquipmentMutationResult.Rejected(
                    ownershipError ?? "Actor item ownership changed.",
                    plan.SourceInstanceId,
                    EquipmentFailureCode.OwnershipChanged);
            }

            EquipmentReplacementPlan current = PreviewEquipReplacing(
                equipment,
                plan.SourceInstanceId,
                plan.RequestedSlotSet);
            if (!current.Success || !SameReplacementPlan(plan, current))
            {
                return EquipmentMutationResult.Rejected(
                    current.Message ?? "Equipment replacement state changed.",
                    plan.SourceInstanceId,
                    current.Success ? EquipmentFailureCode.StaleState : current.FailureCode);
            }

            InventoryComponent inventory = equipment.PersonalInventory;
            GridInventoryBackend.BackendStateSnapshot personalSnapshot = inventory.InternalGridBackend.CaptureBackendState();
            GridInventoryBackend.BackendStateSnapshot equipmentStorageSnapshot = equipment.Backend.CaptureBackendState();
            ActorEquipmentComponent.EquipmentStateSnapshot slotSnapshot = equipment.CaptureEquipmentState();
            int idSequenceSnapshot = ItemInstance.CaptureIdSequence();
            var displacedItems = new ItemInstance[plan.DisplacedItems.Length];

            try
            {
                ItemStorageEntry sourceEntry = inventory.GetStorageEntryByInstanceId(plan.SourceInstanceId);
                ItemInstance sourceItem = sourceEntry != null ? sourceEntry.Item : null;
                if (sourceItem == null)
                    throw new InvalidOperationException("Replacement source is no longer in personal storage.");

                InventoryMutationResult sourceTransfer = inventory.InternalGridBackend.TransferTo(
                    equipment.Backend,
                    plan.SourceInstanceId,
                    1);
                if (!sourceTransfer.Success)
                    throw new InvalidOperationException(sourceTransfer.Message ?? "Replacement source transfer failed.");

                for (int index = 0; index < plan.DisplacedItems.Length; index++)
                {
                    EquipmentDisplacementPlan displaced = plan.DisplacedItems[index];
                    ItemStorageEntry displacedEntry = equipment.Backend.Storage.GetEntryByInstanceId(displaced.InstanceId);
                    displacedItems[index] = displacedEntry != null ? displacedEntry.Item : null;
                    if (displacedItems[index] == null)
                        throw new InvalidOperationException($"Displaced instance '{displaced.InstanceId}' is no longer equipped.");

                    InventoryMutationResult displacedTransfer = displaced.DestinationPlacement != null
                        ? equipment.Backend.TransferToExact(
                            inventory.InternalGridBackend,
                            displaced.InstanceId,
                            displaced.DestinationPlacement.X,
                            displaced.DestinationPlacement.Y,
                            displaced.DestinationPlacement.IsRotated)
                        : equipment.Backend.TransferTo(inventory.InternalGridBackend, displaced.InstanceId, 1);
                    if (!displacedTransfer.Success)
                        throw new InvalidOperationException(displacedTransfer.Message ?? $"Displaced instance '{displaced.InstanceId}' transfer failed.");

                    equipment.ClearSlots(displaced.InstanceId);
                }

                equipment.AssignSlots(plan.SourceInstanceId, plan.RequestedSlotSet);
                if (!ownership.ValidateUniqueOwnership(out ownershipError))
                    throw new InvalidOperationException(ownershipError ?? "Actor ownership validation failed after replacement.");

                inventory.ClearLegacyRightHandForEquipmentAuthority();
                for (int index = 0; index < displacedItems.Length; index++)
                    equipment.RecordUnequipped(displacedItems[index]);
                equipment.RebindActorOwnedItems();
                equipment.RecordEquipped(sourceItem);
                return new EquipmentMutationResult(
                    true,
                    EquipmentFailureCode.None,
                    "Equipment replaced.",
                    plan.SourceInstanceId,
                    plan.RequestedSlotSet);
            }
            catch (Exception exception)
            {
                inventory.InternalGridBackend.RestoreBackendState(personalSnapshot);
                equipment.Backend.RestoreBackendState(equipmentStorageSnapshot);
                equipment.RestoreEquipmentState(slotSnapshot);
                ItemInstance.RestoreIdSequence(idSequenceSnapshot);
                equipment.RebindActorOwnedItems();
                return EquipmentMutationResult.Rejected(
                    $"Equipment replacement rolled back: {exception.Message}",
                    plan.SourceInstanceId,
                    EquipmentFailureCode.StorageMutationFailed);
            }
        }

        public static EquipmentPreview PreviewUnequip(
            ActorEquipmentComponent equipment,
            string instanceId)
        {
            if (equipment == null || equipment.PersonalInventory == null || equipment.Ownership == null)
                return Invalid(EquipmentFailureCode.MissingDependencies, "Actor equipment dependencies are missing.", instanceId, equipment);
            if (!equipment.Ownership.TryLocateInstance(instanceId, out ActorItemStorageNodeKind node, out ItemStorageEntry entry) ||
                node != ActorItemStorageNodeKind.Equipment || entry == null || entry.Item == null)
            {
                return Invalid(EquipmentFailureCode.SourceNotFound, $"Equipped item instance '{instanceId}' was not found.", instanceId, equipment);
            }

            IReadOnlyList<string> occupied = equipment.GetSlotsOccupiedBy(instanceId);
            if (occupied.Count == 0)
                return Invalid(EquipmentFailureCode.InvalidSlotSet, $"Item instance '{instanceId}' has no occupied slots.", instanceId, equipment);

            GridPlacementValidationResult placementPreview = equipment.Backend.PreviewTransferTo(
                equipment.PersonalInventory.InternalGridBackend,
                instanceId);
            if (!placementPreview.IsValid)
                return Invalid(EquipmentFailureCode.NoPersonalInventorySpace, placementPreview.Message ?? "Personal inventory has no valid destination placement.", instanceId, equipment);

            return Valid(instanceId, Copy(occupied), placementPreview.Candidate, equipment);
        }

        public static EquipmentMutationResult Unequip(
            ActorEquipmentComponent equipment,
            EquipmentPreview preview)
        {
            if (equipment == null || preview == null || !preview.Success)
                return EquipmentMutationResult.Rejected(
                    preview?.Message ?? "A valid unequip preview is required.",
                    preview?.InstanceId,
                    preview?.FailureCode ?? EquipmentFailureCode.InvalidPreview);
            if (!VersionsMatch(equipment, preview))
                return EquipmentMutationResult.Rejected("Equipment preview is stale; retry the operation.", preview.InstanceId, EquipmentFailureCode.StaleState);

            EquipmentPreview current = PreviewUnequip(equipment, preview.InstanceId);
            if (!current.Success)
                return EquipmentMutationResult.Rejected(current.Message, preview.InstanceId, current.FailureCode);

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
                if (ownership == null)
                    throw new InvalidOperationException("Actor ownership validation failed after unequip.");
                if (!ownership.ValidateUniqueOwnership(out string ownershipError))
                    throw new InvalidOperationException(ownershipError ?? "Actor ownership validation failed after unequip.");

                inventory.ClearLegacyRightHandForEquipmentAuthority();
                equipment.RebindActorOwnedItems();
                equipment.RecordUnequipped(item);
                return new EquipmentMutationResult(true, EquipmentFailureCode.None, "Item unequipped to personal inventory.", preview.InstanceId, preview.SlotIds);
            }
            catch (Exception exception)
            {
                inventory.InternalGridBackend.RestoreBackendState(personalSnapshot);
                equipment.Backend.RestoreBackendState(equipmentStorageSnapshot);
                equipment.RestoreEquipmentState(slotSnapshot);
                ItemInstance.RestoreIdSequence(idSequenceSnapshot);
                equipment.RebindActorOwnedItems();
                return EquipmentMutationResult.Rejected($"Unequip rolled back: {exception.Message}", preview.InstanceId, EquipmentFailureCode.StorageMutationFailed);
            }
        }

        private static bool TryResolvePersonalEntry(
            ActorEquipmentComponent equipment,
            string instanceId,
            out ItemStorageEntry entry,
            out EquipmentFailureCode failureCode,
            out string error)
        {
            entry = null;
            failureCode = EquipmentFailureCode.None;
            error = null;
            if (equipment == null || equipment.PersonalInventory == null || equipment.Ownership == null)
            {
                failureCode = EquipmentFailureCode.MissingDependencies;
                error = "Actor equipment dependencies are missing.";
                return false;
            }
            if (equipment.GetActiveLayout() == null)
            {
                failureCode = EquipmentFailureCode.LayoutUnavailable;
                error = $"Equipment layout '{equipment.EquipmentLayoutId}' is unavailable.";
                return false;
            }
            if (!equipment.Ownership.TryLocateInstance(instanceId, out ActorItemStorageNodeKind node, out entry) ||
                node != ActorItemStorageNodeKind.Personal || entry == null || entry.Item == null)
            {
                failureCode = EquipmentFailureCode.SourceNotFound;
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

        private static bool ReplacementVersionsMatch(
            ActorEquipmentComponent equipment,
            EquipmentReplacementPlan plan)
        {
            InventoryComponent inventory = equipment.PersonalInventory;
            return inventory != null &&
                   inventory.InternalGridBackend.StorageVersion == plan.PersonalStorageVersion &&
                   inventory.InternalGridBackend.LayoutVersion == plan.PersonalLayoutVersion &&
                   equipment.StorageVersion == plan.EquipmentStorageVersion &&
                   equipment.Version == plan.EquipmentVersion;
        }

        private static bool IsDeclaredAlternative(
            ActorEquipmentComponent equipment,
            string instanceId,
            string[] requested)
        {
            IReadOnlyList<EquipmentSlotSet> compatible = GetCompatibleSlotSets(equipment, instanceId, false);
            for (int index = 0; index < compatible.Count; index++)
            {
                if (SameSlots(compatible[index].SlotIds, requested))
                    return true;
            }
            return false;
        }

        private static bool SameReplacementPlan(
            EquipmentReplacementPlan left,
            EquipmentReplacementPlan right)
        {
            if (left == null || right == null || left.SourceInstanceId != right.SourceInstanceId ||
                !SameSlots(left.RequestedSlotSet, right.RequestedSlotSet) ||
                left.DisplacedItems.Length != right.DisplacedItems.Length)
            {
                return false;
            }

            for (int index = 0; index < left.DisplacedItems.Length; index++)
            {
                EquipmentDisplacementPlan leftItem = left.DisplacedItems[index];
                EquipmentDisplacementPlan rightItem = right.DisplacedItems[index];
                if (leftItem == null || rightItem == null || leftItem.InstanceId != rightItem.InstanceId ||
                    !SameSlots(leftItem.ReleasedSlotIds, rightItem.ReleasedSlotIds) ||
                    !SamePlacement(leftItem.DestinationPlacement, rightItem.DestinationPlacement))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SamePlacement(GridPlacement left, GridPlacement right)
        {
            if (left == null || right == null)
                return left == null && right == null;
            return left.InstanceId == right.InstanceId &&
                   left.X == right.X &&
                   left.Y == right.Y &&
                   left.IsRotated == right.IsRotated &&
                   left.EffectiveWidth == right.EffectiveWidth &&
                   left.EffectiveHeight == right.EffectiveHeight;
        }

        private static EquipmentPreview Valid(
            string instanceId,
            string[] slotIds,
            GridPlacement placement,
            ActorEquipmentComponent equipment)
        {
            return CreatePreview(true, false, EquipmentFailureCode.None, null, instanceId, slotIds, placement, equipment);
        }

        private static EquipmentPreview ChoiceRequired(string instanceId, ActorEquipmentComponent equipment)
        {
            return CreatePreview(true, true, EquipmentFailureCode.None, "Choose one compatible slot alternative.", instanceId, null, null, equipment);
        }

        private static EquipmentPreview Invalid(
            EquipmentFailureCode failureCode,
            string message,
            string instanceId,
            ActorEquipmentComponent equipment)
        {
            return CreatePreview(false, false, failureCode, message, instanceId, null, null, equipment);
        }

        private static EquipmentPreview CreatePreview(
            bool success,
            bool requiresChoice,
            EquipmentFailureCode failureCode,
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
                failureCode,
                message,
                instanceId,
                slotIds,
                placement,
                inventory != null ? inventory.InternalGridBackend.StorageVersion : 0,
                inventory != null ? inventory.InternalGridBackend.LayoutVersion : 0,
                equipment != null ? equipment.StorageVersion : 0,
                equipment != null ? equipment.Version : 0);
        }

        private static EquipmentReplacementPlan InvalidReplacement(
            EquipmentFailureCode failureCode,
            string message,
            string sourceInstanceId,
            IReadOnlyList<string> requestedSlotSet,
            EquipmentDisplacementPlan[] displacedItems,
            ActorEquipmentComponent equipment)
        {
            return CreateReplacementPlan(
                false,
                failureCode,
                message,
                sourceInstanceId,
                Copy(requestedSlotSet),
                displacedItems,
                equipment);
        }

        private static EquipmentReplacementPlan CreateReplacementPlan(
            bool success,
            EquipmentFailureCode failureCode,
            string message,
            string sourceInstanceId,
            string[] requestedSlotSet,
            EquipmentDisplacementPlan[] displacedItems,
            ActorEquipmentComponent equipment)
        {
            InventoryComponent inventory = equipment != null ? equipment.PersonalInventory : null;
            return new EquipmentReplacementPlan(
                success,
                failureCode,
                message,
                sourceInstanceId,
                requestedSlotSet,
                displacedItems,
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
