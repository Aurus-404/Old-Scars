using System;
using System.Collections.Generic;
using OldScars.Core.Actors;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;

namespace OldScars.Core.Items
{
    /// <summary>
    /// Atomic equipment bridge for actor-owned grid compartments other than the
    /// direct personal inventory. The established personal/equipment service
    /// remains the authority for its original path.
    /// </summary>
    internal static class EquipmentOwnedStorageTransactionService
    {
        internal static IReadOnlyList<EquipmentSlotSet> GetCompatibleSlotSets(
            ActorEquipmentComponent equipment,
            IGridStorageOwner source,
            string instanceId)
        {
            var result = new List<EquipmentSlotSet>();
            if (!TryResolveSource(equipment, source, instanceId, out ItemStorageEntry entry, out _, out _))
                return result;

            string[][] alternatives = ResolveSlotSets(ResolveDefinition(entry.DefinitionId));
            if (alternatives == null)
                return result;

            for (int index = 0; index < alternatives.Length; index++)
            {
                string[] slots = alternatives[index];
                if (IsDeclaredSlotSetAvailable(equipment, slots))
                    result.Add(new EquipmentSlotSet(slots));
            }
            return result;
        }

        internal static EquipmentPreview PreviewEquip(
            ActorEquipmentComponent equipment,
            IGridStorageOwner source,
            string instanceId,
            IReadOnlyList<string> requestedSlots)
        {
            if (!TryResolveSource(equipment, source, instanceId, out ItemStorageEntry entry, out EquipmentFailureCode failure, out string error))
                return Invalid(equipment, failure, error, instanceId);
            if (entry.Quantity != 1)
                return Invalid(equipment, EquipmentFailureCode.InvalidQuantity, "Equipped items must have quantity 1.", instanceId);

            ItemDefinition definition = ResolveDefinition(entry.DefinitionId);
            if (!IsEquipEnabled(definition))
                return Invalid(equipment, EquipmentFailureCode.NotEquipable, "El objeto no es compatible con equipamiento.", instanceId);

            string[] requested = Copy(requestedSlots);
            if (requested.Length == 0)
            {
                IReadOnlyList<EquipmentSlotSet> compatible = GetCompatibleSlotSets(equipment, source, instanceId);
                var free = new List<EquipmentSlotSet>();
                for (int index = 0; index < compatible.Count; index++)
                {
                    if (AreSlotsFree(equipment, compatible[index].SlotIds))
                        free.Add(compatible[index]);
                }
                if (free.Count == 0)
                    return Invalid(equipment, EquipmentFailureCode.SlotOccupied, "No hay un slot compatible libre.", instanceId);
                if (free.Count > 1)
                    return CreatePreview(equipment, source, true, true, EquipmentFailureCode.None, null, instanceId, null);
                requested = free[0].SlotIds;
            }

            if (!IsDeclaredAlternative(definition, requested) || !IsDeclaredSlotSetAvailable(equipment, requested))
                return Invalid(equipment, EquipmentFailureCode.InvalidSlotSet, "El objeto no es compatible con ese slot.", instanceId);
            if (!AreSlotsFree(equipment, requested))
                return Invalid(equipment, EquipmentFailureCode.SlotOccupied, "El slot de equipamiento está ocupado.", instanceId);

            return CreatePreview(equipment, source, true, false, EquipmentFailureCode.None, null, instanceId, requested);
        }

        internal static EquipmentMutationResult Equip(
            ActorEquipmentComponent equipment,
            IGridStorageOwner source,
            EquipmentPreview preview)
        {
            if (preview == null || !preview.Success || preview.RequiresChoice)
                return EquipmentMutationResult.Rejected(preview?.Message ?? "Se requiere un preview válido.", preview?.InstanceId, preview?.FailureCode ?? EquipmentFailureCode.InvalidPreview);

            if (!EquipmentVersionsMatch(equipment, preview))
                return EquipmentMutationResult.Rejected("Equipment preview is stale; retry the operation.", preview.InstanceId, EquipmentFailureCode.StaleState);

            if (!TryResolveCurrentSource(
                    equipment,
                    preview.SourceContainerInstanceId,
                    preview.InstanceId,
                    preview.SourceStorageVersion,
                    preview.SourceLayoutVersion,
                    preview.SourcePlacement,
                    out ItemOwnedStorageRuntime currentSource,
                    out IGridStorageTransferEndpoint sourceEndpoint,
                    out EquipmentFailureCode sourceFailure,
                    out string sourceError))
            {
                return EquipmentMutationResult.Rejected(sourceError, preview.InstanceId, sourceFailure);
            }

            EquipmentPreview current = PreviewEquip(equipment, currentSource, preview.InstanceId, preview.SlotIds);
            if (!current.Success || current.RequiresChoice)
                return EquipmentMutationResult.Rejected(current.Message, preview.InstanceId, current.FailureCode);

            source = currentSource;

            GridInventoryBackend.BackendStateSnapshot sourceSnapshot = sourceEndpoint.TransferBackend.CaptureBackendState();
            GridInventoryBackend.BackendStateSnapshot equipmentSnapshot = equipment.Backend.CaptureBackendState();
            ActorEquipmentComponent.EquipmentStateSnapshot slotsSnapshot = equipment.CaptureEquipmentState();
            using ItemInstanceIdRegistry.ItemInstanceIdReservationScope identityScope =
                ItemInstanceIdRegistry.Instance.BeginReservationScope();
            try
            {
                ItemInstance item = sourceEndpoint.TransferBackend.Storage.GetEntryByInstanceId(preview.InstanceId)?.Item;
                InventoryMutationResult transfer = sourceEndpoint.TransferBackend.TransferTo(equipment.Backend, preview.InstanceId, 1);
                if (!transfer.Success)
                    throw new InvalidOperationException(transfer.Message ?? "Falló la transferencia a equipment.");

                equipment.AssignSlots(preview.InstanceId, preview.SlotIds);
                string ownershipError = null;
                if (equipment.Ownership == null || !equipment.Ownership.ValidateUniqueOwnership(out ownershipError))
                    throw new InvalidOperationException(ownershipError ?? "Falló la validación de ownership.");

                equipment.PersonalInventory.ClearLegacyRightHandForEquipmentAuthority();
                ItemOwnedStorageRegistry.Instance.UnbindItem(preview.InstanceId);
                ItemOwnedStorageRegistry.Instance.BindEntries(source.GridStorageEntries, source);
                equipment.RebindActorOwnedItems();
                equipment.RecordEquipped(item);
                equipment.CommitVisualState(EquipmentVisualCommitKind.EquipFromItemOwnedStorage);
                identityScope.Commit();
                return new EquipmentMutationResult(true, EquipmentFailureCode.None, "Item equipped.", preview.InstanceId, preview.SlotIds);
            }
            catch (Exception exception)
            {
                sourceEndpoint.TransferBackend.RestoreBackendState(sourceSnapshot);
                equipment.Backend.RestoreBackendState(equipmentSnapshot);
                equipment.RestoreEquipmentState(slotsSnapshot);
                ItemOwnedStorageRegistry.Instance.BindEntries(source.GridStorageEntries, source);
                equipment.RebindActorOwnedItems();
                return EquipmentMutationResult.Rejected($"Equip rolled back: {exception.Message}", preview.InstanceId, EquipmentFailureCode.StorageMutationFailed);
            }
        }

        internal static EquipmentReplacementPlan PreviewEquipReplacing(
            ActorEquipmentComponent equipment,
            IGridStorageOwner source,
            string instanceId,
            IReadOnlyList<string> requestedSlots)
        {
            if (!TryResolveSource(equipment, source, instanceId, out ItemStorageEntry entry, out EquipmentFailureCode failure, out string error))
                return InvalidReplacement(equipment, failure, error, instanceId, requestedSlots);
            if (entry.Quantity != 1)
                return InvalidReplacement(equipment, EquipmentFailureCode.InvalidQuantity, "Equipped items must have quantity 1.", instanceId, requestedSlots);

            ItemDefinition definition = ResolveDefinition(entry.DefinitionId);
            string[] requested = Copy(requestedSlots);
            if (!IsEquipEnabled(definition) || !IsDeclaredAlternative(definition, requested) || !IsDeclaredSlotSetAvailable(equipment, requested))
                return InvalidReplacement(equipment, EquipmentFailureCode.InvalidSlotSet, "El objeto no es compatible con ese slot.", instanceId, requested);

            var displacedIds = new List<string>();
            for (int index = 0; index < requested.Length; index++)
            {
                if (equipment.TryGetSlotOccupant(requested[index], out string occupied) && !displacedIds.Contains(occupied))
                    displacedIds.Add(occupied);
            }
            if (displacedIds.Count == 0)
                return InvalidReplacement(equipment, EquipmentFailureCode.InvalidPreview, "No hay equipment para reemplazar.", instanceId, requested);

            var displacedEntries = new List<ItemStorageEntry>(displacedIds.Count);
            var plans = new EquipmentDisplacementPlan[displacedIds.Count];
            for (int index = 0; index < displacedIds.Count; index++)
            {
                if (!equipment.TryGetEntryByInstanceId(displacedIds[index], out ItemStorageEntry displaced))
                    return InvalidReplacement(equipment, EquipmentFailureCode.OwnershipChanged, "El equipment desplazado cambió.", instanceId, requested);
                displacedEntries.Add(displaced);
                plans[index] = new EquipmentDisplacementPlan(displacedIds[index], Copy(equipment.GetSlotsOccupiedBy(displacedIds[index])), null);
            }

            InventoryComponent personal = equipment.PersonalInventory;
            if (!personal.InternalGridBackend.TryReserveIncoming(displacedEntries, out GridPlacement[] placements, out InventoryMutationResult.MutationFailure placementFailure, out string placementError))
            {
                return InvalidReplacement(
                    equipment,
                    placementFailure == InventoryMutationResult.MutationFailure.NoGridSpace ? EquipmentFailureCode.NoPersonalInventorySpace : EquipmentFailureCode.StaleState,
                    placementError ?? "No hay espacio para el equipment desplazado.",
                    instanceId,
                    requested);
            }

            for (int index = 0; index < plans.Length; index++)
                plans[index] = new EquipmentDisplacementPlan(plans[index].InstanceId, plans[index].ReleasedSlotIds, placements[index]);

            return CreateReplacement(equipment, source, true, EquipmentFailureCode.None, null, instanceId, requested, plans);
        }

        internal static EquipmentMutationResult EquipReplacing(
            ActorEquipmentComponent equipment,
            IGridStorageOwner source,
            EquipmentReplacementPlan plan)
        {
            if (plan == null || !plan.Success)
                return EquipmentMutationResult.Rejected(plan?.Message ?? "Se requiere un plan de reemplazo válido.", plan?.SourceInstanceId, plan?.FailureCode ?? EquipmentFailureCode.InvalidPreview);

            if (!EquipmentVersionsMatch(equipment, plan))
                return EquipmentMutationResult.Rejected("Equipment replacement plan is stale; retry the operation.", plan.SourceInstanceId, EquipmentFailureCode.StaleState);

            if (!TryResolveCurrentSource(
                    equipment,
                    plan.SourceContainerInstanceId,
                    plan.SourceInstanceId,
                    plan.SourceStorageVersion,
                    plan.SourceLayoutVersion,
                    plan.SourcePlacement,
                    out ItemOwnedStorageRuntime currentSource,
                    out IGridStorageTransferEndpoint sourceEndpoint,
                    out EquipmentFailureCode sourceFailure,
                    out string sourceError))
            {
                return EquipmentMutationResult.Rejected(sourceError, plan.SourceInstanceId, sourceFailure);
            }

            EquipmentReplacementPlan current = PreviewEquipReplacing(equipment, currentSource, plan.SourceInstanceId, plan.RequestedSlotSet);
            if (!current.Success || !SamePlan(plan, current))
                return EquipmentMutationResult.Rejected(current.Message ?? "El estado de equipment cambió.", plan.SourceInstanceId, current.Success ? EquipmentFailureCode.StaleState : current.FailureCode);

            source = currentSource;

            InventoryComponent personal = equipment.PersonalInventory;
            GridInventoryBackend.BackendStateSnapshot sourceSnapshot = sourceEndpoint.TransferBackend.CaptureBackendState();
            GridInventoryBackend.BackendStateSnapshot personalSnapshot = personal.InternalGridBackend.CaptureBackendState();
            GridInventoryBackend.BackendStateSnapshot equipmentSnapshot = equipment.Backend.CaptureBackendState();
            ActorEquipmentComponent.EquipmentStateSnapshot slotsSnapshot = equipment.CaptureEquipmentState();
            using ItemInstanceIdRegistry.ItemInstanceIdReservationScope identityScope =
                ItemInstanceIdRegistry.Instance.BeginReservationScope();
            var displacedItems = new ItemInstance[plan.DisplacedItems.Length];
            try
            {
                ItemInstance sourceItem = sourceEndpoint.TransferBackend.Storage.GetEntryByInstanceId(plan.SourceInstanceId)?.Item;
                InventoryMutationResult sourceTransfer = sourceEndpoint.TransferBackend.TransferTo(equipment.Backend, plan.SourceInstanceId, 1);
                if (!sourceTransfer.Success)
                    throw new InvalidOperationException(sourceTransfer.Message ?? "Falló la transferencia a equipment.");

                for (int index = 0; index < plan.DisplacedItems.Length; index++)
                {
                    EquipmentDisplacementPlan displaced = plan.DisplacedItems[index];
                    displacedItems[index] = equipment.Backend.Storage.GetEntryByInstanceId(displaced.InstanceId)?.Item;
                    InventoryMutationResult moved = equipment.Backend.TransferToExact(
                        personal.InternalGridBackend,
                        displaced.InstanceId,
                        displaced.DestinationPlacement.X,
                        displaced.DestinationPlacement.Y,
                        displaced.DestinationPlacement.IsRotated);
                    if (!moved.Success)
                        throw new InvalidOperationException(moved.Message ?? "Falló el desplazamiento de equipment.");
                    equipment.ClearSlots(displaced.InstanceId);
                }

                equipment.AssignSlots(plan.SourceInstanceId, plan.RequestedSlotSet);
                string ownershipError = null;
                if (equipment.Ownership == null || !equipment.Ownership.ValidateUniqueOwnership(out ownershipError))
                    throw new InvalidOperationException(ownershipError ?? "Falló la validación de ownership.");

                personal.ClearLegacyRightHandForEquipmentAuthority();
                ItemOwnedStorageRegistry.Instance.UnbindItem(plan.SourceInstanceId);
                ItemOwnedStorageRegistry.Instance.BindEntries(source.GridStorageEntries, source);
                equipment.RebindActorOwnedItems();
                for (int index = 0; index < displacedItems.Length; index++)
                    equipment.RecordUnequipped(displacedItems[index]);
                equipment.RecordEquipped(sourceItem);
                equipment.CommitVisualState(EquipmentVisualCommitKind.ReplacementFromItemOwnedStorage);
                identityScope.Commit();
                return new EquipmentMutationResult(true, EquipmentFailureCode.None, "Equipment replaced.", plan.SourceInstanceId, plan.RequestedSlotSet);
            }
            catch (Exception exception)
            {
                sourceEndpoint.TransferBackend.RestoreBackendState(sourceSnapshot);
                personal.InternalGridBackend.RestoreBackendState(personalSnapshot);
                equipment.Backend.RestoreBackendState(equipmentSnapshot);
                equipment.RestoreEquipmentState(slotsSnapshot);
                ItemOwnedStorageRegistry.Instance.BindEntries(source.GridStorageEntries, source);
                equipment.RebindActorOwnedItems();
                return EquipmentMutationResult.Rejected($"Equipment replacement rolled back: {exception.Message}", plan.SourceInstanceId, EquipmentFailureCode.StorageMutationFailed);
            }
        }

        private static bool TryResolveSource(
            ActorEquipmentComponent equipment,
            IGridStorageOwner source,
            string instanceId,
            out ItemStorageEntry entry,
            out EquipmentFailureCode failure,
            out string error)
        {
            entry = null;
            failure = EquipmentFailureCode.None;
            error = null;
            if (equipment == null || equipment.PersonalInventory == null || equipment.Ownership == null ||
                !(source is ItemOwnedStorageRuntime) || !(source is IGridStorageTransferEndpoint))
            {
                failure = EquipmentFailureCode.MissingDependencies;
                error = "Actor equipment dependencies are missing.";
                return false;
            }
            if (!ItemOwnedStorageRegistry.Instance.ShareRootOwner(source, equipment.PersonalInventory))
            {
                failure = EquipmentFailureCode.OwnershipChanged;
                error = "El objeto ya no pertenece al actor.";
                return false;
            }
            if (!source.TryGetEntryByInstanceId(instanceId, out _, out entry) || entry?.Item == null)
            {
                failure = EquipmentFailureCode.SourceNotFound;
                error = $"Item instance '{instanceId}' was not found in the source compartment.";
                return false;
            }
            return true;
        }

        internal static bool IsDeclaredSlotSetAvailable(ActorEquipmentComponent equipment, IReadOnlyList<string> slots)
        {
            if (slots == null || slots.Count == 0)
                return false;
            var seen = new HashSet<string>();
            for (int index = 0; index < slots.Count; index++)
            {
                if (!equipment.HasSlot(slots[index]) || !seen.Add(slots[index]))
                    return false;
            }
            return true;
        }

        internal static bool AreSlotsFree(ActorEquipmentComponent equipment, IReadOnlyList<string> slots)
        {
            for (int index = 0; index < slots.Count; index++)
            {
                if (!equipment.IsSlotFree(slots[index]))
                    return false;
            }
            return true;
        }

        internal static bool IsDeclaredAlternative(ItemDefinition definition, IReadOnlyList<string> requested)
        {
            string[][] alternatives = ResolveSlotSets(definition);
            if (alternatives == null)
                return false;
            for (int index = 0; index < alternatives.Length; index++)
            {
                if (SameSlots(alternatives[index], requested))
                    return true;
            }
            return false;
        }

        internal static string[][] ResolveSlotSets(ItemDefinition definition)
        {
            if (definition?.equip?.slot_sets != null && definition.equip.slot_sets.Length > 0)
                return definition.equip.slot_sets;
            if (definition?.equip?.occupied_slots != null && definition.equip.occupied_slots.Length > 0)
                return new[] { MapLegacySlots(definition.equip.occupied_slots) };
            if (definition?.equip?.allowed_slots != null && definition.equip.allowed_slots.Length > 0)
            {
                var alternatives = new string[definition.equip.allowed_slots.Length][];
                for (int index = 0; index < alternatives.Length; index++)
                    alternatives[index] = new[] { MapLegacySlot(definition.equip.allowed_slots[index]) };
                return alternatives;
            }
            return null;
        }

        private static string[] MapLegacySlots(string[] slots)
        {
            var result = new string[slots.Length];
            for (int index = 0; index < slots.Length; index++)
                result[index] = MapLegacySlot(slots[index]);
            return result;
        }

        private static string MapLegacySlot(string slotId)
        {
            return slotId == "right_hand" ? ActorEquipmentComponent.HandRightSlotId : slotId;
        }

        internal static bool IsEquipEnabled(ItemDefinition definition)
        {
            return definition?.equip?.equippable ?? definition?.equippable ?? false;
        }

        internal static ItemDefinition ResolveDefinition(string definitionId)
        {
            return GameDataManager.Instance != null && GameDataManager.Instance.IsReady
                ? GameDataManager.Instance.Database?.GetItem(definitionId)
                : null;
        }

        private static EquipmentPreview Invalid(ActorEquipmentComponent equipment, EquipmentFailureCode failure, string message, string instanceId)
        {
            return CreatePreview(equipment, null, false, false, failure, message, instanceId, null);
        }

        private static EquipmentPreview CreatePreview(
            ActorEquipmentComponent equipment,
            IGridStorageOwner source,
            bool success,
            bool choice,
            EquipmentFailureCode failure,
            string message,
            string instanceId,
            string[] slots)
        {
            InventoryComponent personal = equipment != null ? equipment.PersonalInventory : null;
            ItemOwnedStorageRuntime itemStorage = source as ItemOwnedStorageRuntime;
            GridPlacement sourcePlacement = null;
            itemStorage?.TryGetGridPlacement(instanceId, out sourcePlacement);
            return new EquipmentPreview(
                success,
                choice,
                failure,
                message,
                instanceId,
                slots,
                null,
                personal != null ? personal.InternalGridBackend.StorageVersion : 0,
                personal != null ? personal.InternalGridBackend.LayoutVersion : 0,
                equipment != null ? equipment.StorageVersion : 0,
                equipment != null ? equipment.Version : 0,
                itemStorage?.ContainerInstanceId,
                itemStorage != null ? itemStorage.ContentVersion : 0,
                itemStorage != null ? itemStorage.LayoutVersion : 0,
                sourcePlacement);
        }

        private static EquipmentReplacementPlan InvalidReplacement(ActorEquipmentComponent equipment, EquipmentFailureCode failure, string message, string instanceId, IReadOnlyList<string> slots)
        {
            return CreateReplacement(equipment, null, false, failure, message, instanceId, Copy(slots), null);
        }

        private static EquipmentReplacementPlan CreateReplacement(
            ActorEquipmentComponent equipment,
            IGridStorageOwner source,
            bool success,
            EquipmentFailureCode failure,
            string message,
            string instanceId,
            string[] slots,
            EquipmentDisplacementPlan[] displaced)
        {
            InventoryComponent personal = equipment != null ? equipment.PersonalInventory : null;
            ItemOwnedStorageRuntime itemStorage = source as ItemOwnedStorageRuntime;
            GridPlacement sourcePlacement = null;
            itemStorage?.TryGetGridPlacement(instanceId, out sourcePlacement);
            return new EquipmentReplacementPlan(
                success,
                failure,
                message,
                instanceId,
                slots,
                displaced,
                personal != null ? personal.InternalGridBackend.StorageVersion : 0,
                personal != null ? personal.InternalGridBackend.LayoutVersion : 0,
                equipment != null ? equipment.StorageVersion : 0,
                equipment != null ? equipment.Version : 0,
                itemStorage?.ContainerInstanceId,
                itemStorage != null ? itemStorage.ContentVersion : 0,
                itemStorage != null ? itemStorage.LayoutVersion : 0,
                sourcePlacement);
        }

        internal static bool EquipmentVersionsMatch(
            ActorEquipmentComponent equipment,
            EquipmentPreview preview)
        {
            InventoryComponent personal = equipment != null ? equipment.PersonalInventory : null;
            return personal != null && preview != null &&
                   personal.InternalGridBackend.StorageVersion == preview.PersonalStorageVersion &&
                   personal.InternalGridBackend.LayoutVersion == preview.PersonalLayoutVersion &&
                   equipment.StorageVersion == preview.EquipmentStorageVersion &&
                   equipment.Version == preview.EquipmentVersion;
        }

        internal static bool EquipmentVersionsMatch(
            ActorEquipmentComponent equipment,
            EquipmentReplacementPlan plan)
        {
            InventoryComponent personal = equipment != null ? equipment.PersonalInventory : null;
            return personal != null && plan != null &&
                   personal.InternalGridBackend.StorageVersion == plan.PersonalStorageVersion &&
                   personal.InternalGridBackend.LayoutVersion == plan.PersonalLayoutVersion &&
                   equipment.StorageVersion == plan.EquipmentStorageVersion &&
                   equipment.Version == plan.EquipmentVersion;
        }

        private static bool TryResolveCurrentSource(
            ActorEquipmentComponent equipment,
            string containerInstanceId,
            string sourceInstanceId,
            int expectedStorageVersion,
            int expectedLayoutVersion,
            GridPlacement expectedPlacement,
            out ItemOwnedStorageRuntime source,
            out IGridStorageTransferEndpoint endpoint,
            out EquipmentFailureCode failure,
            out string error)
        {
            source = null;
            endpoint = null;
            failure = EquipmentFailureCode.None;
            error = null;
            if (string.IsNullOrWhiteSpace(containerInstanceId) ||
                !ItemOwnedStorageRegistry.Instance.TryResolveOwnedStorage(containerInstanceId, out source) ||
                source == null)
            {
                failure = EquipmentFailureCode.SourceNotFound;
                error = "El compartimento de origen ya no está disponible.";
                return false;
            }

            if (!ItemOwnedStorageRegistry.Instance.ShareRootOwner(source, equipment.PersonalInventory))
            {
                failure = EquipmentFailureCode.OwnershipChanged;
                error = "El objeto ya no pertenece al actor.";
                return false;
            }

            if (source.ContentVersion != expectedStorageVersion || source.LayoutVersion != expectedLayoutVersion)
            {
                failure = EquipmentFailureCode.StaleState;
                error = "El contenido o layout del compartimento cambió. Intentá nuevamente.";
                return false;
            }

            if (!source.TryGetEntryByInstanceId(sourceInstanceId, out _, out ItemStorageEntry entry) ||
                entry?.Item == null || entry.Quantity != 1 ||
                !source.TryGetGridPlacement(sourceInstanceId, out GridPlacement currentPlacement) ||
                !SamePlacement(expectedPlacement, currentPlacement))
            {
                failure = EquipmentFailureCode.StaleState;
                error = "La instancia o placement de origen cambió. Intentá nuevamente.";
                return false;
            }

            endpoint = source as IGridStorageTransferEndpoint;
            if (endpoint == null)
            {
                failure = EquipmentFailureCode.MissingDependencies;
                error = "El storage de origen no expone un backend transaccional.";
                return false;
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

        private static bool SamePlan(EquipmentReplacementPlan left, EquipmentReplacementPlan right)
        {
            if (left.SourceContainerInstanceId != right.SourceContainerInstanceId ||
                left.SourceStorageVersion != right.SourceStorageVersion ||
                left.SourceLayoutVersion != right.SourceLayoutVersion ||
                !SamePlacement(left.SourcePlacement, right.SourcePlacement) ||
                !SameSlots(left.RequestedSlotSet, right.RequestedSlotSet) ||
                left.DisplacedItems.Length != right.DisplacedItems.Length)
                return false;
            for (int index = 0; index < left.DisplacedItems.Length; index++)
            {
                EquipmentDisplacementPlan a = left.DisplacedItems[index];
                EquipmentDisplacementPlan b = right.DisplacedItems[index];
                if (a.InstanceId != b.InstanceId || a.DestinationPlacement == null || b.DestinationPlacement == null ||
                    a.DestinationPlacement.X != b.DestinationPlacement.X || a.DestinationPlacement.Y != b.DestinationPlacement.Y ||
                    a.DestinationPlacement.IsRotated != b.DestinationPlacement.IsRotated)
                    return false;
            }
            return true;
        }

        internal static bool SameSlots(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            if ((left?.Count ?? 0) != (right?.Count ?? 0))
                return false;
            for (int index = 0; index < left.Count; index++)
            {
                if (left[index] != right[index])
                    return false;
            }
            return true;
        }

        internal static string[] Copy(IReadOnlyList<string> values)
        {
            if (values == null)
                return Array.Empty<string>();
            var result = new string[values.Count];
            for (int index = 0; index < values.Count; index++)
                result[index] = values[index];
            return result;
        }
    }
}
