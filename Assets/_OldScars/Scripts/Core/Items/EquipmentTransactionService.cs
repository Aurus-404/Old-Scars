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
            if (!TryResolvePersonalEntry(equipment, instanceId, out ItemStorageEntry entry, out _, out _))
                return Array.Empty<EquipmentSlotSet>();

            return GetCompatibleSlotSets(equipment, entry, instanceId, availableOnly);
        }

        public static IReadOnlyList<EquipmentSlotSet> GetCompatibleEquippedSlotSets(
            ActorEquipmentComponent equipment,
            string instanceId)
        {
            if (equipment == null || !equipment.IsEquipped(instanceId) ||
                !equipment.TryGetEntryByInstanceId(instanceId, out ItemStorageEntry entry) || entry?.Item == null)
            {
                return Array.Empty<EquipmentSlotSet>();
            }

            return GetCompatibleSlotSets(equipment, entry, instanceId, false);
        }

        private static IReadOnlyList<EquipmentSlotSet> GetCompatibleSlotSets(
            ActorEquipmentComponent equipment,
            ItemStorageEntry entry,
            string instanceId,
            bool availableOnly)
        {
            var result = new List<EquipmentSlotSet>();

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
            return Equip(equipment, preview, EquipmentVisualCommitKind.Equip);
        }

        internal static EquipmentMutationResult Equip(
            ActorEquipmentComponent equipment,
            EquipmentPreview preview,
            EquipmentVisualCommitKind? commitKind)
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
                if (commitKind.HasValue)
                    equipment.CommitVisualState(commitKind.Value);
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
                equipment.CommitVisualState(EquipmentVisualCommitKind.Replacement);
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

        public static EquipmentRelocationPlan PreviewRelocateEquipped(
            ActorEquipmentComponent equipment,
            string instanceId,
            IReadOnlyList<string> requestedSlotSet)
        {
            if (equipment == null || equipment.PersonalInventory == null || equipment.Ownership == null)
                return InvalidRelocation(equipment, EquipmentFailureCode.MissingDependencies, "Actor equipment dependencies are missing.", instanceId, requestedSlotSet);
            if (!equipment.Ownership.TryLocateInstance(instanceId, out ActorItemStorageNodeKind node, out ItemStorageEntry sourceEntry) ||
                node != ActorItemStorageNodeKind.Equipment || sourceEntry?.Item == null || !equipment.IsEquipped(instanceId))
            {
                return InvalidRelocation(equipment, EquipmentFailureCode.SourceNotFound, $"Equipped item instance '{instanceId}' was not found.", instanceId, requestedSlotSet);
            }
            if (sourceEntry.Quantity != 1)
                return InvalidRelocation(equipment, EquipmentFailureCode.InvalidQuantity, "Equipped items must have quantity 1.", instanceId, requestedSlotSet);

            string[] sourceSlots = Copy(equipment.GetSlotsOccupiedBy(instanceId));
            string[] requested = Copy(requestedSlotSet);
            if (sourceSlots.Length == 0 || requested.Length == 0 || !IsDeclaredAlternative(equipment, instanceId, requested))
                return InvalidRelocation(equipment, EquipmentFailureCode.InvalidSlotSet, "Requested slots are not a declared complete alternative for this item.", instanceId, requested, sourceSlots);
            if (SameSlotSet(sourceSlots, requested))
                return InvalidRelocation(equipment, EquipmentFailureCode.InvalidSlotSet, "The item already occupies the requested equipment slots.", instanceId, requested, sourceSlots);

            var displacedIds = new List<string>();
            var seenDisplaced = new HashSet<string>();
            for (int index = 0; index < requested.Length; index++)
            {
                string slotId = requested[index];
                if (!equipment.HasSlot(slotId))
                    return InvalidRelocation(equipment, EquipmentFailureCode.InvalidSlotSet, $"Equipment slot '{slotId}' is unavailable.", instanceId, requested, sourceSlots);
                if (equipment.TryGetSlotOccupant(slotId, out string occupantId) &&
                    occupantId != instanceId && seenDisplaced.Add(occupantId))
                {
                    displacedIds.Add(occupantId);
                }
            }

            var displacedEntries = new List<ItemStorageEntry>(displacedIds.Count);
            var displacements = new EquipmentDisplacementPlan[displacedIds.Count];
            for (int index = 0; index < displacedIds.Count; index++)
            {
                string displacedId = displacedIds[index];
                if (!equipment.Ownership.TryLocateInstance(displacedId, out ActorItemStorageNodeKind displacedNode, out ItemStorageEntry displacedEntry) ||
                    displacedNode != ActorItemStorageNodeKind.Equipment || displacedEntry?.Item == null)
                {
                    return InvalidRelocation(equipment, EquipmentFailureCode.OwnershipChanged, $"Displaced equipment instance '{displacedId}' is not uniquely owned by equipment storage.", instanceId, requested, sourceSlots);
                }

                string[] releasedSlots = Copy(equipment.GetSlotsOccupiedBy(displacedId));
                if (releasedSlots.Length == 0)
                    return InvalidRelocation(equipment, EquipmentFailureCode.OwnershipChanged, $"Displaced equipment instance '{displacedId}' has no occupied slots.", instanceId, requested, sourceSlots);

                displacedEntries.Add(displacedEntry);
                displacements[index] = new EquipmentDisplacementPlan(displacedId, releasedSlots, null);
            }

            if (!equipment.PersonalInventory.InternalGridBackend.TryReserveIncoming(
                    displacedEntries,
                    out GridPlacement[] placements,
                    out InventoryMutationResult.MutationFailure placementFailure,
                    out string placementError))
            {
                return InvalidRelocation(
                    equipment,
                    placementFailure == InventoryMutationResult.MutationFailure.NoGridSpace
                        ? EquipmentFailureCode.NoPersonalInventorySpace
                        : EquipmentFailureCode.StaleState,
                    placementError ?? "Displaced equipment does not fit in personal inventory.",
                    instanceId,
                    requested,
                    sourceSlots);
            }

            for (int index = 0; index < displacements.Length; index++)
            {
                EquipmentDisplacementPlan displacement = displacements[index];
                displacements[index] = new EquipmentDisplacementPlan(
                    displacement.InstanceId,
                    displacement.ReleasedSlotIds,
                    placements[index]);
            }

            return CreateRelocationPlan(
                equipment,
                true,
                EquipmentFailureCode.None,
                null,
                instanceId,
                sourceSlots,
                requested,
                displacements);
        }

        public static EquipmentMutationResult RelocateEquipped(
            ActorEquipmentComponent equipment,
            EquipmentRelocationPlan plan)
        {
            if (equipment == null || plan == null || !plan.Success)
            {
                return EquipmentMutationResult.Rejected(
                    plan?.Message ?? "A valid equipment relocation plan is required.",
                    plan?.SourceInstanceId,
                    plan?.FailureCode ?? EquipmentFailureCode.InvalidPreview);
            }
            if (!RelocationVersionsMatch(equipment, plan))
                return EquipmentMutationResult.Rejected("Equipment relocation plan is stale; retry the operation.", plan.SourceInstanceId, EquipmentFailureCode.StaleState);

            EquipmentRelocationPlan current = PreviewRelocateEquipped(equipment, plan.SourceInstanceId, plan.RequestedSlotSet);
            if (!current.Success || !SameRelocationPlan(plan, current))
            {
                return EquipmentMutationResult.Rejected(
                    current.Message ?? "Equipment relocation state changed.",
                    plan.SourceInstanceId,
                    current.Success ? EquipmentFailureCode.StaleState : current.FailureCode);
            }

            InventoryComponent inventory = equipment.PersonalInventory;
            ActorItemOwnershipComponent ownership = equipment.Ownership;
            GridInventoryBackend.BackendStateSnapshot personalSnapshot = inventory.InternalGridBackend.CaptureBackendState();
            GridInventoryBackend.BackendStateSnapshot equipmentStorageSnapshot = equipment.Backend.CaptureBackendState();
            ActorEquipmentComponent.EquipmentStateSnapshot slotSnapshot = equipment.CaptureEquipmentState();
            int idSequenceSnapshot = ItemInstance.CaptureIdSequence();
            var displacedItems = new ItemInstance[plan.DisplacedItems.Length];

            try
            {
                ItemStorageEntry sourceEntry = equipment.Backend.Storage.GetEntryByInstanceId(plan.SourceInstanceId);
                if (sourceEntry?.Item == null)
                    throw new InvalidOperationException("Relocation source is no longer in equipment storage.");

                for (int index = 0; index < plan.DisplacedItems.Length; index++)
                {
                    EquipmentDisplacementPlan displaced = plan.DisplacedItems[index];
                    ItemStorageEntry displacedEntry = equipment.Backend.Storage.GetEntryByInstanceId(displaced.InstanceId);
                    displacedItems[index] = displacedEntry?.Item;
                    if (displacedItems[index] == null)
                        throw new InvalidOperationException($"Displaced instance '{displaced.InstanceId}' is no longer equipped.");

                    InventoryMutationResult transfer = displaced.DestinationPlacement != null
                        ? equipment.Backend.TransferToExact(
                            inventory.InternalGridBackend,
                            displaced.InstanceId,
                            displaced.DestinationPlacement.X,
                            displaced.DestinationPlacement.Y,
                            displaced.DestinationPlacement.IsRotated)
                        : equipment.Backend.TransferTo(inventory.InternalGridBackend, displaced.InstanceId, 1);
                    if (!transfer.Success)
                        throw new InvalidOperationException(transfer.Message ?? $"Displaced instance '{displaced.InstanceId}' transfer failed.");

                    equipment.ClearSlots(displaced.InstanceId);
                }

                equipment.ClearSlots(plan.SourceInstanceId);
                equipment.AssignSlots(plan.SourceInstanceId, plan.RequestedSlotSet);
                if (!ownership.ValidateUniqueOwnership(out string ownershipError))
                    throw new InvalidOperationException(ownershipError ?? "Actor ownership validation failed after equipment relocation.");

                inventory.ClearLegacyRightHandForEquipmentAuthority();
                for (int index = 0; index < displacedItems.Length; index++)
                    equipment.RecordUnequipped(displacedItems[index]);
                equipment.RebindActorOwnedItems();
                equipment.CommitVisualState(EquipmentVisualCommitKind.Replacement);
                return new EquipmentMutationResult(
                    true,
                    EquipmentFailureCode.None,
                    plan.DisplacedItems.Length > 0 ? "Equipped item moved and target equipment replaced." : "Equipped item moved.",
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
                    $"Equipment relocation rolled back: {exception.Message}",
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
                equipment.CommitVisualState(EquipmentVisualCommitKind.Unequip);
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

        public static EquipmentStorageTransferPlan PreviewTransferEquippedToStorage(
            ActorEquipmentComponent equipment,
            string instanceId,
            IGridStorageOwner destination,
            GridStorageTransferContext context)
        {
            if (equipment == null || equipment.PersonalInventory == null || equipment.Ownership == null)
                return InvalidStorageTransfer(equipment, destination, EquipmentFailureCode.MissingDependencies, "Actor equipment dependencies are missing.", instanceId);
            if (destination == null || ReferenceEquals(destination, equipment.PersonalInventory) ||
                !(destination is IGridStorageTransferEndpoint destinationEndpoint) || destinationEndpoint.TransferBackend == null)
            {
                return InvalidStorageTransfer(equipment, destination, EquipmentFailureCode.MissingDependencies, "A distinct transactional destination storage is required.", instanceId);
            }
            if (!equipment.Ownership.TryLocateInstance(instanceId, out ActorItemStorageNodeKind node, out ItemStorageEntry entry) ||
                node != ActorItemStorageNodeKind.Equipment || entry?.Item == null || !equipment.IsEquipped(instanceId))
            {
                return InvalidStorageTransfer(equipment, destination, EquipmentFailureCode.SourceNotFound, $"Equipped item instance '{instanceId}' was not found.", instanceId);
            }
            if (entry.Quantity != 1)
                return InvalidStorageTransfer(equipment, destination, EquipmentFailureCode.InvalidQuantity, "Equipped items must have quantity 1.", instanceId);
            if (!destinationEndpoint.CanTransferIn(context, out string endpointError))
                return InvalidStorageTransfer(equipment, destination, EquipmentFailureCode.StorageMutationFailed, endpointError ?? "Destination storage rejected the transfer.", instanceId);
            if (destination is IGridStorageIncomingGuard guard && !guard.CanAcceptIncoming(entry, 1, out string guardReason))
                return InvalidStorageTransfer(equipment, destination, EquipmentFailureCode.StorageMutationFailed, guardReason ?? "Destination storage rejected the item.", instanceId);
            if (!ItemOwnedStorageRegistry.Instance.ShareRootOwner(equipment.PersonalInventory, destination))
            {
                object rootOwner = ItemOwnedStorageRegistry.Instance.ResolveRootOwner(destination);
                if (rootOwner is ICarryWeightLimitedOwner carryOwner && carryOwner.HasCarryWeightLimit)
                {
                    CarryWeightAcceptance acceptance = carryOwner.EvaluateIncomingEntry(entry, 1);
                    if (!acceptance.Accepted)
                        return InvalidStorageTransfer(equipment, destination, EquipmentFailureCode.StorageMutationFailed, acceptance.FailureReason ?? "Destination carry weight limit exceeded.", instanceId);
                }
            }

            GridPlacementValidationResult placementPreview = equipment.Backend.PreviewTransferTo(
                destinationEndpoint.TransferBackend,
                instanceId);
            if (!placementPreview.IsValid)
            {
                return InvalidStorageTransfer(
                    equipment,
                    destination,
                    placementPreview.Failure == InventoryMutationResult.MutationFailure.NoGridSpace
                        ? EquipmentFailureCode.NoPersonalInventorySpace
                        : EquipmentFailureCode.StaleState,
                    placementPreview.Message ?? "Destination storage has no valid placement.",
                    instanceId);
            }

            return CreateStorageTransferPlan(
                equipment,
                destination,
                true,
                EquipmentFailureCode.None,
                null,
                instanceId,
                Copy(equipment.GetSlotsOccupiedBy(instanceId)),
                placementPreview.Candidate);
        }

        public static EquipmentMutationResult TransferEquippedToStorage(
            ActorEquipmentComponent equipment,
            IGridStorageOwner destination,
            EquipmentStorageTransferPlan plan,
            GridStorageTransferContext context)
        {
            if (equipment == null || destination == null || plan == null || !plan.Success ||
                !ReferenceEquals(destination, plan.DestinationOwner) ||
                !(destination is IGridStorageTransferEndpoint destinationEndpoint))
            {
                return EquipmentMutationResult.Rejected(
                    plan?.Message ?? "A valid equipped-item storage transfer plan is required.",
                    plan?.SourceInstanceId,
                    plan?.FailureCode ?? EquipmentFailureCode.InvalidPreview);
            }
            if (!StorageTransferVersionsMatch(equipment, destinationEndpoint.TransferBackend, plan))
                return EquipmentMutationResult.Rejected("Equipped-item transfer plan is stale; retry the operation.", plan.SourceInstanceId, EquipmentFailureCode.StaleState);

            EquipmentStorageTransferPlan current = PreviewTransferEquippedToStorage(
                equipment,
                plan.SourceInstanceId,
                destination,
                context);
            if (!current.Success || !SameStorageTransferPlan(plan, current))
            {
                return EquipmentMutationResult.Rejected(
                    current.Message ?? "Equipped-item transfer state changed.",
                    plan.SourceInstanceId,
                    current.Success ? EquipmentFailureCode.StaleState : current.FailureCode);
            }

            GridInventoryBackend.BackendStateSnapshot equipmentStorageSnapshot = equipment.Backend.CaptureBackendState();
            GridInventoryBackend.BackendStateSnapshot destinationSnapshot = destinationEndpoint.TransferBackend.CaptureBackendState();
            ActorEquipmentComponent.EquipmentStateSnapshot slotSnapshot = equipment.CaptureEquipmentState();
            int idSequenceSnapshot = ItemInstance.CaptureIdSequence();

            try
            {
                ItemStorageEntry sourceEntry = equipment.Backend.Storage.GetEntryByInstanceId(plan.SourceInstanceId);
                ItemInstance item = sourceEntry?.Item;
                string definitionId = sourceEntry?.DefinitionId;
                if (item == null)
                    throw new InvalidOperationException("Equipped transfer source is no longer available.");

                InventoryMutationResult transfer = plan.DestinationPlacement != null
                    ? equipment.Backend.TransferToExact(
                        destinationEndpoint.TransferBackend,
                        plan.SourceInstanceId,
                        plan.DestinationPlacement.X,
                        plan.DestinationPlacement.Y,
                        plan.DestinationPlacement.IsRotated)
                    : equipment.Backend.TransferTo(destinationEndpoint.TransferBackend, plan.SourceInstanceId, 1);
                if (!transfer.Success)
                    throw new InvalidOperationException(transfer.Message ?? "Equipped item transfer failed.");

                equipment.ClearSlots(plan.SourceInstanceId);
                if (!equipment.Ownership.ValidateUniqueOwnership(out string ownershipError))
                    throw new InvalidOperationException(ownershipError ?? "Actor ownership validation failed after equipped item transfer.");

                equipment.PersonalInventory.ClearLegacyRightHandForEquipmentAuthority();
                ItemOwnedStorageRegistry.Instance.BindEntries(destination.GridStorageEntries, destination);
                equipment.RebindActorOwnedItems();
                equipment.RecordUnequipped(item);

                var receipt = new GridStorageTransferReceipt(definitionId, transfer);
                try
                {
                    destinationEndpoint.OnTransferCommittedIn(receipt, context);
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogError(
                        $"[EquipmentTransactionService] Committed destination hook failed: {exception.GetType().Name}: {exception.Message}");
                }

                equipment.CommitVisualState(EquipmentVisualCommitKind.Unequip);
                return new EquipmentMutationResult(
                    true,
                    EquipmentFailureCode.None,
                    "Equipped item transferred to storage.",
                    plan.SourceInstanceId,
                    plan.SourceSlotSet);
            }
            catch (Exception exception)
            {
                equipment.Backend.RestoreBackendState(equipmentStorageSnapshot);
                destinationEndpoint.TransferBackend.RestoreBackendState(destinationSnapshot);
                equipment.RestoreEquipmentState(slotSnapshot);
                ItemInstance.RestoreIdSequence(idSequenceSnapshot);
                ItemOwnedStorageRegistry.Instance.BindEntries(destination.GridStorageEntries, destination);
                equipment.RebindActorOwnedItems();
                return EquipmentMutationResult.Rejected(
                    $"Equipped item transfer rolled back: {exception.Message}",
                    plan.SourceInstanceId,
                    EquipmentFailureCode.StorageMutationFailed);
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

        private static bool RelocationVersionsMatch(
            ActorEquipmentComponent equipment,
            EquipmentRelocationPlan plan)
        {
            InventoryComponent inventory = equipment.PersonalInventory;
            return inventory != null &&
                   inventory.InternalGridBackend.StorageVersion == plan.PersonalStorageVersion &&
                   inventory.InternalGridBackend.LayoutVersion == plan.PersonalLayoutVersion &&
                   equipment.StorageVersion == plan.EquipmentStorageVersion &&
                   equipment.Version == plan.EquipmentVersion;
        }

        private static bool StorageTransferVersionsMatch(
            ActorEquipmentComponent equipment,
            GridInventoryBackend destinationBackend,
            EquipmentStorageTransferPlan plan)
        {
            InventoryComponent inventory = equipment.PersonalInventory;
            return inventory != null && destinationBackend != null &&
                   inventory.InternalGridBackend.StorageVersion == plan.PersonalStorageVersion &&
                   inventory.InternalGridBackend.LayoutVersion == plan.PersonalLayoutVersion &&
                   equipment.StorageVersion == plan.EquipmentStorageVersion &&
                   equipment.Version == plan.EquipmentVersion &&
                   destinationBackend.StorageVersion == plan.DestinationStorageVersion &&
                   destinationBackend.LayoutVersion == plan.DestinationLayoutVersion;
        }

        private static bool IsDeclaredAlternative(
            ActorEquipmentComponent equipment,
            string instanceId,
            string[] requested)
        {
            IReadOnlyList<EquipmentSlotSet> compatible = equipment != null && equipment.IsEquipped(instanceId)
                ? GetCompatibleEquippedSlotSets(equipment, instanceId)
                : GetCompatibleSlotSets(equipment, instanceId, false);
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

        private static bool SameRelocationPlan(
            EquipmentRelocationPlan left,
            EquipmentRelocationPlan right)
        {
            if (left == null || right == null || left.SourceInstanceId != right.SourceInstanceId ||
                !SameSlots(left.SourceSlotSet, right.SourceSlotSet) ||
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

        private static bool SameStorageTransferPlan(
            EquipmentStorageTransferPlan left,
            EquipmentStorageTransferPlan right)
        {
            return left != null && right != null &&
                   left.SourceInstanceId == right.SourceInstanceId &&
                   ReferenceEquals(left.DestinationOwner, right.DestinationOwner) &&
                   SameSlots(left.SourceSlotSet, right.SourceSlotSet) &&
                   SamePlacement(left.DestinationPlacement, right.DestinationPlacement);
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

        private static EquipmentRelocationPlan InvalidRelocation(
            ActorEquipmentComponent equipment,
            EquipmentFailureCode failureCode,
            string message,
            string sourceInstanceId,
            IReadOnlyList<string> requestedSlotSet,
            IReadOnlyList<string> sourceSlotSet = null)
        {
            return CreateRelocationPlan(
                equipment,
                false,
                failureCode,
                message,
                sourceInstanceId,
                Copy(sourceSlotSet),
                Copy(requestedSlotSet),
                null);
        }

        private static EquipmentRelocationPlan CreateRelocationPlan(
            ActorEquipmentComponent equipment,
            bool success,
            EquipmentFailureCode failureCode,
            string message,
            string sourceInstanceId,
            string[] sourceSlotSet,
            string[] requestedSlotSet,
            EquipmentDisplacementPlan[] displacedItems)
        {
            InventoryComponent inventory = equipment != null ? equipment.PersonalInventory : null;
            return new EquipmentRelocationPlan(
                success,
                failureCode,
                message,
                sourceInstanceId,
                sourceSlotSet,
                requestedSlotSet,
                displacedItems,
                inventory != null ? inventory.InternalGridBackend.StorageVersion : 0,
                inventory != null ? inventory.InternalGridBackend.LayoutVersion : 0,
                equipment != null ? equipment.StorageVersion : 0,
                equipment != null ? equipment.Version : 0);
        }

        private static EquipmentStorageTransferPlan InvalidStorageTransfer(
            ActorEquipmentComponent equipment,
            IGridStorageOwner destination,
            EquipmentFailureCode failureCode,
            string message,
            string sourceInstanceId)
        {
            return CreateStorageTransferPlan(
                equipment,
                destination,
                false,
                failureCode,
                message,
                sourceInstanceId,
                equipment != null ? Copy(equipment.GetSlotsOccupiedBy(sourceInstanceId)) : Array.Empty<string>(),
                null);
        }

        private static EquipmentStorageTransferPlan CreateStorageTransferPlan(
            ActorEquipmentComponent equipment,
            IGridStorageOwner destination,
            bool success,
            EquipmentFailureCode failureCode,
            string message,
            string sourceInstanceId,
            string[] sourceSlotSet,
            GridPlacement destinationPlacement)
        {
            InventoryComponent inventory = equipment != null ? equipment.PersonalInventory : null;
            GridInventoryBackend destinationBackend = (destination as IGridStorageTransferEndpoint)?.TransferBackend;
            return new EquipmentStorageTransferPlan(
                success,
                failureCode,
                message,
                sourceInstanceId,
                sourceSlotSet,
                destination,
                destinationPlacement,
                inventory != null ? inventory.InternalGridBackend.StorageVersion : 0,
                inventory != null ? inventory.InternalGridBackend.LayoutVersion : 0,
                equipment != null ? equipment.StorageVersion : 0,
                equipment != null ? equipment.Version : 0,
                destinationBackend != null ? destinationBackend.StorageVersion : 0,
                destinationBackend != null ? destinationBackend.LayoutVersion : 0);
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

        private static bool SameSlotSet(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            int leftCount = left?.Count ?? 0;
            int rightCount = right?.Count ?? 0;
            if (leftCount == 0 || leftCount != rightCount)
                return false;
            for (int leftIndex = 0; leftIndex < leftCount; leftIndex++)
            {
                bool found = false;
                for (int rightIndex = 0; rightIndex < rightCount; rightIndex++)
                {
                    if (left[leftIndex] == right[rightIndex])
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
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
