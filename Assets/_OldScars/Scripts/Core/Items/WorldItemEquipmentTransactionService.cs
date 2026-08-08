using System;
using System.Collections.Generic;
using OldScars.Core.Actors;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Interactions;

namespace OldScars.Core.Items
{
    /// <summary>
    /// Narrow atomic coordinator for moving one stable world-item instance into
    /// actor equipment. World presentation is finalized only after the storage,
    /// slot, ownership, and replacement mutations have committed successfully.
    /// </summary>
    internal static class WorldItemEquipmentTransactionService
    {
        internal static IReadOnlyList<EquipmentSlotSet> GetCompatibleSlotSets(
            ActorEquipmentComponent equipment,
            WorldItemPickup source,
            string instanceId)
        {
            var result = new List<EquipmentSlotSet>();
            if (!TryResolveSource(equipment, source, instanceId, out ItemStorageEntry entry, out _, out _))
                return result;

            string[][] alternatives = EquipmentOwnedStorageTransactionService.ResolveSlotSets(
                EquipmentOwnedStorageTransactionService.ResolveDefinition(entry.DefinitionId));
            if (alternatives == null)
                return result;

            for (int index = 0; index < alternatives.Length; index++)
            {
                string[] slots = alternatives[index];
                if (EquipmentOwnedStorageTransactionService.IsDeclaredSlotSetAvailable(equipment, slots))
                    result.Add(new EquipmentSlotSet(slots));
            }

            return result;
        }

        internal static EquipmentPreview PreviewEquip(
            ActorEquipmentComponent equipment,
            WorldItemPickup source,
            string instanceId,
            IReadOnlyList<string> requestedSlots)
        {
            if (!TryResolveSource(equipment, source, instanceId, out ItemStorageEntry entry, out EquipmentFailureCode failure, out string error))
                return InvalidPreview(equipment, source, failure, error, instanceId);
            if (entry.Quantity != 1)
                return InvalidPreview(equipment, source, EquipmentFailureCode.InvalidQuantity, "Equipped items must have quantity 1.", instanceId);
            if (!CanAcceptIncomingWeight(equipment.PersonalInventory, entry, out string weightError))
                return InvalidPreview(equipment, source, EquipmentFailureCode.StorageMutationFailed, weightError, instanceId);

            ItemDefinition definition = EquipmentOwnedStorageTransactionService.ResolveDefinition(entry.DefinitionId);
            if (!EquipmentOwnedStorageTransactionService.IsEquipEnabled(definition))
                return InvalidPreview(equipment, source, EquipmentFailureCode.NotEquipable, "El objeto no es compatible con equipamiento.", instanceId);

            string[] requested = EquipmentOwnedStorageTransactionService.Copy(requestedSlots);
            if (requested.Length == 0)
            {
                IReadOnlyList<EquipmentSlotSet> compatible = GetCompatibleSlotSets(equipment, source, instanceId);
                var free = new List<EquipmentSlotSet>();
                for (int index = 0; index < compatible.Count; index++)
                {
                    if (EquipmentOwnedStorageTransactionService.AreSlotsFree(equipment, compatible[index].SlotIds))
                        free.Add(compatible[index]);
                }

                if (free.Count == 0)
                    return InvalidPreview(equipment, source, EquipmentFailureCode.SlotOccupied, "No hay un slot compatible libre.", instanceId);
                if (free.Count > 1)
                    return CreatePreview(equipment, source, true, true, EquipmentFailureCode.None, null, instanceId, null);
                requested = free[0].SlotIds;
            }

            if (!EquipmentOwnedStorageTransactionService.IsDeclaredAlternative(definition, requested) ||
                !EquipmentOwnedStorageTransactionService.IsDeclaredSlotSetAvailable(equipment, requested))
            {
                return InvalidPreview(equipment, source, EquipmentFailureCode.InvalidSlotSet, "El objeto no es compatible con ese slot.", instanceId);
            }

            if (!EquipmentOwnedStorageTransactionService.AreSlotsFree(equipment, requested))
                return InvalidPreview(equipment, source, EquipmentFailureCode.SlotOccupied, "El slot de equipamiento estÃ¡ ocupado.", instanceId);

            return CreatePreview(equipment, source, true, false, EquipmentFailureCode.None, null, instanceId, requested);
        }

        internal static EquipmentMutationResult Equip(
            ActorEquipmentComponent equipment,
            WorldItemPickup source,
            EquipmentPreview preview,
            ActorInteractionContext actorContext,
            WorldObjectTags targetTags)
        {
            if (actorContext == null || targetTags == null)
                return EquipmentMutationResult.Rejected("World pickup context is missing.", preview?.InstanceId, EquipmentFailureCode.MissingDependencies);

            if (preview == null || !preview.Success || preview.RequiresChoice)
            {
                return EquipmentMutationResult.Rejected(
                    preview?.Message ?? "Se requiere un preview vÃ¡lido.",
                    preview?.InstanceId,
                    preview?.FailureCode ?? EquipmentFailureCode.InvalidPreview);
            }

            if (!EquipmentOwnedStorageTransactionService.EquipmentVersionsMatch(equipment, preview))
                return EquipmentMutationResult.Rejected("Equipment preview is stale; retry the operation.", preview.InstanceId, EquipmentFailureCode.StaleState);

            if (!TryResolveCurrentSource(source, preview.InstanceId, preview.SourceStorageVersion, out ItemStorageEntry sourceEntry, out EquipmentFailureCode sourceFailure, out string sourceError))
                return EquipmentMutationResult.Rejected(sourceError, preview.InstanceId, sourceFailure);

            EquipmentPreview current = PreviewEquip(equipment, source, preview.InstanceId, preview.SlotIds);
            if (!current.Success || current.RequiresChoice ||
                !EquipmentOwnedStorageTransactionService.SameSlots(current.SlotIds, preview.SlotIds))
            {
                return EquipmentMutationResult.Rejected(current.Message ?? "El estado de equipment cambiÃ³.", preview.InstanceId, current.Success ? EquipmentFailureCode.StaleState : current.FailureCode);
            }

            GridInventoryBackend.BackendStateSnapshot sourceSnapshot = source.TransactionBackend.CaptureBackendState();
            GridInventoryBackend.BackendStateSnapshot equipmentSnapshot = equipment.Backend.CaptureBackendState();
            ActorEquipmentComponent.EquipmentStateSnapshot slotsSnapshot = equipment.CaptureEquipmentState();
            ItemInstance sourceItem = sourceEntry.Item;
            using ItemInstanceIdRegistry.ItemInstanceIdReservationScope identityScope =
                ItemInstanceIdRegistry.Instance.BeginReservationScope();
            bool ownershipTransferred = false;
            try
            {
                equipment.ValidateActorOwnedItems();
                ItemOwnedStorageRegistry.Instance.ValidateBinding(preview.InstanceId, source);
                InventoryMutationResult transfer = source.TransactionBackend.TransferTo(equipment.Backend, preview.InstanceId, 1);
                if (!transfer.Success || transfer.AffectedQuantity != 1 ||
                    transfer.DestinationInstanceId != preview.InstanceId ||
                    !source.IsTransactionSourceEmpty(preview.InstanceId) ||
                    equipment.Backend.Storage.GetEntryByInstanceId(preview.InstanceId)?.Item == null)
                {
                    throw new InvalidOperationException(transfer.Message ?? "FallÃ³ la transferencia a equipment.");
                }

                equipment.AssignSlots(preview.InstanceId, preview.SlotIds);
                ValidateOwnership(equipment);
                equipment.PersonalInventory.ClearLegacyRightHandForEquipmentAuthority();
                ItemOwnedStorageRegistry.Instance.TransferBinding(
                    preview.InstanceId,
                    source,
                    equipment.PersonalInventory);
                ownershipTransferred = true;
                equipment.ValidateActorOwnedItems();
                identityScope.Commit();
            }
            catch (Exception exception)
            {
                source.TransactionBackend.RestoreBackendState(sourceSnapshot);
                equipment.Backend.RestoreBackendState(equipmentSnapshot);
                equipment.RestoreEquipmentState(slotsSnapshot);
                if (ownershipTransferred)
                {
                    ItemOwnedStorageRegistry.Instance.TransferBinding(
                        preview.InstanceId,
                        equipment.PersonalInventory,
                        source);
                }
                UnityEngine.Debug.LogError($"[Equipment][TRANSACTION_FAILED]\n  Operation: EquipFromWorld\n  Actor: {equipment.name}\n  DefinitionId: {sourceItem?.DefinitionId ?? "<UNKNOWN>"}\n  InstanceId: {preview.InstanceId}\n  SourceOwner: WorldItemPickup({source.name})\n  TargetOwner: InventoryComponent({equipment.PersonalInventory.name})\n  Slots: {(preview.SlotIds != null ? string.Join(", ", preview.SlotIds) : "<NONE>")}\n  MutationCommitted: false\n  RollbackAttempted: true\n  RollbackSucceeded: true\n  FailureCode: EquipmentRejected\n  Failure: {exception.Message}");
                return EquipmentMutationResult.Rejected($"Equip rolled back: {exception.Message}", preview.InstanceId, EquipmentFailureCode.StorageMutationFailed);
            }

            source.FinalizeCommittedPickup(actorContext, targetTags, sourceItem, 1, "Recoger y equipar", "Recogiste y equipaste");
            equipment.RecordEquipped(sourceItem);
            equipment.CommitVisualState(EquipmentVisualCommitKind.Equip);
            return new EquipmentMutationResult(true, EquipmentFailureCode.None, "Item equipped from world.", preview.InstanceId, preview.SlotIds);
        }

        internal static EquipmentReplacementPlan PreviewEquipReplacing(
            ActorEquipmentComponent equipment,
            WorldItemPickup source,
            string instanceId,
            IReadOnlyList<string> requestedSlots)
        {
            if (!TryResolveSource(equipment, source, instanceId, out ItemStorageEntry entry, out EquipmentFailureCode failure, out string error))
                return InvalidReplacement(equipment, source, failure, error, instanceId, requestedSlots);
            if (entry.Quantity != 1)
                return InvalidReplacement(equipment, source, EquipmentFailureCode.InvalidQuantity, "Equipped items must have quantity 1.", instanceId, requestedSlots);
            if (!CanAcceptIncomingWeight(equipment.PersonalInventory, entry, out string weightError))
                return InvalidReplacement(equipment, source, EquipmentFailureCode.StorageMutationFailed, weightError, instanceId, requestedSlots);

            ItemDefinition definition = EquipmentOwnedStorageTransactionService.ResolveDefinition(entry.DefinitionId);
            string[] requested = EquipmentOwnedStorageTransactionService.Copy(requestedSlots);
            if (!EquipmentOwnedStorageTransactionService.IsEquipEnabled(definition) ||
                !EquipmentOwnedStorageTransactionService.IsDeclaredAlternative(definition, requested) ||
                !EquipmentOwnedStorageTransactionService.IsDeclaredSlotSetAvailable(equipment, requested))
            {
                return InvalidReplacement(equipment, source, EquipmentFailureCode.InvalidSlotSet, "El objeto no es compatible con ese slot.", instanceId, requested);
            }

            var displacedIds = new List<string>();
            for (int index = 0; index < requested.Length; index++)
            {
                if (equipment.TryGetSlotOccupant(requested[index], out string occupied) && !displacedIds.Contains(occupied))
                    displacedIds.Add(occupied);
            }

            if (displacedIds.Count == 0)
                return InvalidReplacement(equipment, source, EquipmentFailureCode.InvalidPreview, "No hay equipment para reemplazar.", instanceId, requested);

            var displacedEntries = new List<ItemStorageEntry>(displacedIds.Count);
            var plans = new EquipmentDisplacementPlan[displacedIds.Count];
            for (int index = 0; index < displacedIds.Count; index++)
            {
                if (!equipment.TryGetEntryByInstanceId(displacedIds[index], out ItemStorageEntry displaced))
                    return InvalidReplacement(equipment, source, EquipmentFailureCode.OwnershipChanged, "El equipment desplazado cambiÃ³.", instanceId, requested);

                displacedEntries.Add(displaced);
                plans[index] = new EquipmentDisplacementPlan(
                    displacedIds[index],
                    EquipmentOwnedStorageTransactionService.Copy(equipment.GetSlotsOccupiedBy(displacedIds[index])),
                    null);
            }

            InventoryComponent personal = equipment.PersonalInventory;
            if (personal == null)
            {
                return InvalidReplacement(
                    equipment,
                    source,
                    EquipmentFailureCode.MissingDependencies,
                    "El actor no tiene inventario personal disponible.",
                    instanceId,
                    requested);
            }

            if (!personal.InternalGridBackend.TryReserveIncoming(
                    displacedEntries,
                    out GridPlacement[] placements,
                    out InventoryMutationResult.MutationFailure placementFailure,
                    out string placementError))
            {
                return InvalidReplacement(
                    equipment,
                    source,
                    placementFailure == InventoryMutationResult.MutationFailure.NoGridSpace
                        ? EquipmentFailureCode.NoPersonalInventorySpace
                        : EquipmentFailureCode.StaleState,
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
            WorldItemPickup source,
            EquipmentReplacementPlan plan,
            ActorInteractionContext actorContext,
            WorldObjectTags targetTags)
        {
            if (actorContext == null || targetTags == null)
                return EquipmentMutationResult.Rejected("World pickup context is missing.", plan?.SourceInstanceId, EquipmentFailureCode.MissingDependencies);

            if (plan == null || !plan.Success)
            {
                return EquipmentMutationResult.Rejected(
                    plan?.Message ?? "Se requiere un plan de reemplazo vÃ¡lido.",
                    plan?.SourceInstanceId,
                    plan?.FailureCode ?? EquipmentFailureCode.InvalidPreview);
            }

            if (!EquipmentOwnedStorageTransactionService.EquipmentVersionsMatch(equipment, plan))
                return EquipmentMutationResult.Rejected("Equipment replacement plan is stale; retry the operation.", plan.SourceInstanceId, EquipmentFailureCode.StaleState);

            if (!TryResolveCurrentSource(source, plan.SourceInstanceId, plan.SourceStorageVersion, out ItemStorageEntry sourceEntry, out EquipmentFailureCode sourceFailure, out string sourceError))
                return EquipmentMutationResult.Rejected(sourceError, plan.SourceInstanceId, sourceFailure);

            EquipmentReplacementPlan current = PreviewEquipReplacing(equipment, source, plan.SourceInstanceId, plan.RequestedSlotSet);
            if (!current.Success || !SameReplacementPlan(plan, current))
            {
                return EquipmentMutationResult.Rejected(
                    current.Message ?? "El estado de equipment cambiÃ³.",
                    plan.SourceInstanceId,
                    current.Success ? EquipmentFailureCode.StaleState : current.FailureCode);
            }

            InventoryComponent personal = equipment.PersonalInventory;
            GridInventoryBackend.BackendStateSnapshot sourceSnapshot = source.TransactionBackend.CaptureBackendState();
            GridInventoryBackend.BackendStateSnapshot personalSnapshot = personal.InternalGridBackend.CaptureBackendState();
            GridInventoryBackend.BackendStateSnapshot equipmentSnapshot = equipment.Backend.CaptureBackendState();
            ActorEquipmentComponent.EquipmentStateSnapshot slotsSnapshot = equipment.CaptureEquipmentState();
            ItemInstance sourceItem = sourceEntry.Item;
            var displacedItems = new ItemInstance[plan.DisplacedItems.Length];
            using ItemInstanceIdRegistry.ItemInstanceIdReservationScope identityScope =
                ItemInstanceIdRegistry.Instance.BeginReservationScope();
            bool ownershipTransferred = false;
            try
            {
                equipment.ValidateActorOwnedItems();
                ItemOwnedStorageRegistry.Instance.ValidateBinding(plan.SourceInstanceId, source);
                InventoryMutationResult sourceTransfer = source.TransactionBackend.TransferTo(equipment.Backend, plan.SourceInstanceId, 1);
                if (!sourceTransfer.Success || sourceTransfer.AffectedQuantity != 1 ||
                    sourceTransfer.DestinationInstanceId != plan.SourceInstanceId ||
                    !source.IsTransactionSourceEmpty(plan.SourceInstanceId) ||
                    equipment.Backend.Storage.GetEntryByInstanceId(plan.SourceInstanceId)?.Item == null)
                {
                    throw new InvalidOperationException(sourceTransfer.Message ?? "FallÃ³ la transferencia a equipment.");
                }

                for (int index = 0; index < plan.DisplacedItems.Length; index++)
                {
                    EquipmentDisplacementPlan displaced = plan.DisplacedItems[index];
                    displacedItems[index] = equipment.Backend.Storage.GetEntryByInstanceId(displaced.InstanceId)?.Item;
                    GridPlacement placement = displaced.DestinationPlacement;
                    if (placement == null)
                        throw new InvalidOperationException("Falta el destino reservado para equipment desplazado.");

                    InventoryMutationResult moved = equipment.Backend.TransferToExact(
                        personal.InternalGridBackend,
                        displaced.InstanceId,
                        placement.X,
                        placement.Y,
                        placement.IsRotated);
                    if (!moved.Success || moved.AffectedQuantity != 1)
                        throw new InvalidOperationException(moved.Message ?? "FallÃ³ el desplazamiento de equipment.");

                    equipment.ClearSlots(displaced.InstanceId);
                }

                equipment.AssignSlots(plan.SourceInstanceId, plan.RequestedSlotSet);
                ValidateOwnership(equipment);
                personal.ClearLegacyRightHandForEquipmentAuthority();
                ItemOwnedStorageRegistry.Instance.TransferBinding(
                    plan.SourceInstanceId,
                    source,
                    personal);
                ownershipTransferred = true;
                equipment.ValidateActorOwnedItems();
                identityScope.Commit();
            }
            catch (Exception exception)
            {
                source.TransactionBackend.RestoreBackendState(sourceSnapshot);
                personal.InternalGridBackend.RestoreBackendState(personalSnapshot);
                equipment.Backend.RestoreBackendState(equipmentSnapshot);
                equipment.RestoreEquipmentState(slotsSnapshot);
                if (ownershipTransferred)
                {
                    ItemOwnedStorageRegistry.Instance.TransferBinding(
                        plan.SourceInstanceId,
                        personal,
                        source);
                }
                UnityEngine.Debug.LogError($"[Equipment][TRANSACTION_FAILED]\n  Operation: ReplaceFromWorld\n  Actor: {equipment.name}\n  DefinitionId: {sourceItem?.DefinitionId ?? "<UNKNOWN>"}\n  InstanceId: {plan.SourceInstanceId}\n  SourceOwner: WorldItemPickup({source.name})\n  TargetOwner: InventoryComponent({personal.name})\n  Slots: {(plan.RequestedSlotSet != null ? string.Join(", ", plan.RequestedSlotSet) : "<NONE>")}\n  MutationCommitted: false\n  RollbackAttempted: true\n  RollbackSucceeded: true\n  FailureCode: EquipmentRejected\n  Failure: {exception.Message}");
                return EquipmentMutationResult.Rejected($"Equipment replacement rolled back: {exception.Message}", plan.SourceInstanceId, EquipmentFailureCode.StorageMutationFailed);
            }

            source.FinalizeCommittedPickup(actorContext, targetTags, sourceItem, 1, "Recoger y reemplazar", "Recogiste y reemplazaste");
            for (int index = 0; index < displacedItems.Length; index++)
                equipment.RecordUnequipped(displacedItems[index]);
            equipment.RecordEquipped(sourceItem);
            equipment.CommitVisualState(EquipmentVisualCommitKind.Replacement);
            return new EquipmentMutationResult(true, EquipmentFailureCode.None, "Equipment replaced from world.", plan.SourceInstanceId, plan.RequestedSlotSet);
        }

        private static bool TryResolveSource(
            ActorEquipmentComponent equipment,
            WorldItemPickup source,
            string instanceId,
            out ItemStorageEntry entry,
            out EquipmentFailureCode failure,
            out string error)
        {
            entry = null;
            failure = EquipmentFailureCode.None;
            error = null;
            if (equipment == null || equipment.PersonalInventory == null || equipment.Ownership == null || source == null)
            {
                failure = EquipmentFailureCode.MissingDependencies;
                error = "Actor equipment dependencies or world source are missing.";
                return false;
            }

            if (!source.TryPrepareTransactionSource(out entry, out error) || entry?.Item == null || entry.Item.InstanceId != instanceId)
            {
                failure = EquipmentFailureCode.SourceNotFound;
                error = error ?? $"Item instance '{instanceId}' was not found in the world source.";
                return false;
            }

            return true;
        }

        private static bool TryResolveCurrentSource(
            WorldItemPickup source,
            string instanceId,
            int expectedContentVersion,
            out ItemStorageEntry entry,
            out EquipmentFailureCode failure,
            out string error)
        {
            entry = null;
            failure = EquipmentFailureCode.None;
            error = null;
            if (source == null || !source.isActiveAndEnabled || source.ContentVersion != expectedContentVersion)
            {
                failure = EquipmentFailureCode.StaleState;
                error = "La fuente mundial cambiÃ³. IntentÃ¡ nuevamente.";
                return false;
            }

            if (!source.TryGetEntryByInstanceId(instanceId, out _, out entry) || entry?.Item == null || entry.Quantity != 1)
            {
                failure = EquipmentFailureCode.StaleState;
                error = "La instancia o cantidad mundial cambiÃ³. IntentÃ¡ nuevamente.";
                return false;
            }

            return true;
        }

        private static void ValidateOwnership(ActorEquipmentComponent equipment)
        {
            if (equipment.Ownership == null)
                throw new InvalidOperationException("FallÃ³ la validaciÃ³n de ownership.");

            if (!equipment.Ownership.ValidateUniqueOwnership(out string ownershipError))
                throw new InvalidOperationException(ownershipError ?? "FallÃ³ la validaciÃ³n de ownership.");
        }

        private static bool CanAcceptIncomingWeight(
            InventoryComponent personal,
            ItemStorageEntry entry,
            out string error)
        {
            error = null;
            if (personal == null || !personal.HasCarryWeightLimit)
                return true;

            CarryWeightAcceptance acceptance = personal.EvaluateIncomingEntry(entry, entry != null ? entry.Quantity : 0);
            if (acceptance.Accepted)
                return true;

            error = acceptance.FailureReason ??
                    $"No podÃ©s cargar ese objeto por el lÃ­mite de peso ({acceptance.ProjectedWeightKg:0.00} / {acceptance.HardLimitKg:0.00} kg).";
            return false;
        }

        private static EquipmentPreview InvalidPreview(
            ActorEquipmentComponent equipment,
            WorldItemPickup source,
            EquipmentFailureCode failure,
            string message,
            string instanceId)
        {
            return CreatePreview(equipment, source, false, false, failure, message, instanceId, null);
        }

        private static EquipmentPreview CreatePreview(
            ActorEquipmentComponent equipment,
            WorldItemPickup source,
            bool success,
            bool requiresChoice,
            EquipmentFailureCode failure,
            string message,
            string instanceId,
            string[] slots)
        {
            InventoryComponent personal = equipment != null ? equipment.PersonalInventory : null;
            return new EquipmentPreview(
                success,
                requiresChoice,
                failure,
                message,
                instanceId,
                slots,
                null,
                personal != null ? personal.InternalGridBackend.StorageVersion : 0,
                personal != null ? personal.InternalGridBackend.LayoutVersion : 0,
                equipment != null ? equipment.StorageVersion : 0,
                equipment != null ? equipment.Version : 0,
                sourceStorageVersion: source != null ? source.ContentVersion : 0);
        }

        private static EquipmentReplacementPlan InvalidReplacement(
            ActorEquipmentComponent equipment,
            WorldItemPickup source,
            EquipmentFailureCode failure,
            string message,
            string instanceId,
            IReadOnlyList<string> slots)
        {
            return CreateReplacement(
                equipment,
                source,
                false,
                failure,
                message,
                instanceId,
                EquipmentOwnedStorageTransactionService.Copy(slots),
                null);
        }

        private static EquipmentReplacementPlan CreateReplacement(
            ActorEquipmentComponent equipment,
            WorldItemPickup source,
            bool success,
            EquipmentFailureCode failure,
            string message,
            string instanceId,
            string[] slots,
            EquipmentDisplacementPlan[] displaced)
        {
            InventoryComponent personal = equipment != null ? equipment.PersonalInventory : null;
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
                sourceStorageVersion: source != null ? source.ContentVersion : 0);
        }

        private static bool SameReplacementPlan(EquipmentReplacementPlan left, EquipmentReplacementPlan right)
        {
            if (left == null || right == null ||
                left.SourceStorageVersion != right.SourceStorageVersion ||
                !EquipmentOwnedStorageTransactionService.SameSlots(left.RequestedSlotSet, right.RequestedSlotSet) ||
                left.DisplacedItems.Length != right.DisplacedItems.Length)
            {
                return false;
            }

            for (int index = 0; index < left.DisplacedItems.Length; index++)
            {
                EquipmentDisplacementPlan a = left.DisplacedItems[index];
                EquipmentDisplacementPlan b = right.DisplacedItems[index];
                if (a == null || b == null || a.InstanceId != b.InstanceId || !SamePlacement(a.DestinationPlacement, b.DestinationPlacement))
                    return false;
            }

            return true;
        }

        private static bool SamePlacement(GridPlacement left, GridPlacement right)
        {
            if (left == null || right == null)
                return left == null && right == null;

            return left.X == right.X &&
                   left.Y == right.Y &&
                   left.IsRotated == right.IsRotated &&
                   left.EffectiveWidth == right.EffectiveWidth &&
                   left.EffectiveHeight == right.EffectiveHeight;
        }
    }
}
