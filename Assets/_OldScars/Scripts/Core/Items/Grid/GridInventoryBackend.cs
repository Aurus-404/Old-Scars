using System;
using System.Collections.Generic;
using OldScars.Core.Data.Definitions;

namespace OldScars.Core.Items
{
    public sealed class GridInventoryBackend
    {
        private readonly ItemStorage storage;
        private readonly Func<string, ItemDefinition> definitionResolver;
        private GridInventoryLayout layout;

        public bool UsesGridLayout => layout != null;
        public int GridWidth => layout != null ? layout.Width : 0;
        public int GridHeight => layout != null ? layout.Height : 0;

        internal ItemStorage Storage => storage;
        internal int StorageVersion => storage.Version;
        internal int LayoutVersion => layout != null ? layout.Version : 0;

        public GridInventoryBackend(ItemStorage storage, Func<string, ItemDefinition> definitionResolver)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            this.definitionResolver = definitionResolver ?? throw new ArgumentNullException(nameof(definitionResolver));
        }

        public bool TryEnableLayout(int width, int height, out string error)
        {
            error = null;
            if (width <= 0 || height <= 0)
            {
                error = $"Grid dimensions must be positive (got {width}x{height}).";
                return false;
            }

            var candidate = new GridInventoryLayout(width, height);
            var reservations = new List<GridInventoryLayout.ReservedRect>();
            IReadOnlyList<ItemStorageEntry> entries = storage.Entries;

            for (int index = 0; index < entries.Count; index++)
            {
                ItemStorageEntry entry = entries[index];
                ItemInstance item = entry != null ? entry.Item : null;
                if (item == null)
                {
                    error = $"Cannot initialize grid because storage entry {index} has no item instance.";
                    return false;
                }

                if (!TryResolveFootprint(item.DefinitionId, out GridFootprint footprint, out _, out error))
                    return false;

                if (!candidate.TryFindFirstFit(footprint, reservations, out GridInventoryLayout.ReservedRect reservation))
                {
                    error = $"Cannot initialize {width}x{height} grid: item '{item.DefinitionId}' [{item.InstanceId}] does not fit.";
                    return false;
                }

                reservations.Add(reservation);
            }

            for (int index = 0; index < entries.Count; index++)
            {
                ItemStorageEntry entry = entries[index];
                if (!candidate.TryAddPlacement(entry.Item.InstanceId, reservations[index]))
                {
                    error = $"Cannot initialize grid placement for item instance '{entry.Item.InstanceId}'.";
                    return false;
                }
            }

            if (!ValidateLayoutMatchesStorage(storage, candidate))
            {
                error = "Grid initialization invariant failed; the inventory remains linear.";
                return false;
            }

            layout = candidate;
            return true;
        }

        internal void DisableLayout()
        {
            layout = null;
        }

        public bool TryGetPlacement(string instanceId, out GridPlacement placement)
        {
            placement = null;
            return layout != null && layout.TryGetPlacement(instanceId, out placement);
        }

        public bool TryResolveFootprint(string definitionId, out GridFootprint footprint, out bool usedFallback, out string error)
        {
            ItemDefinition definition = definitionResolver(definitionId);
            return GridFootprint.TryResolve(definition, out footprint, out usedFallback, out error);
        }

        internal bool TryReserveIncomingAfterRemoving(
            string removedInstanceId,
            IReadOnlyList<ItemStorageEntry> incomingEntries,
            out GridPlacement[] reservedPlacements,
            out InventoryMutationResult.MutationFailure failure,
            out string error)
        {
            return TryReserveIncomingInternal(
                removedInstanceId,
                true,
                incomingEntries,
                out reservedPlacements,
                out failure,
                out error);
        }

        internal bool TryReserveIncoming(
            IReadOnlyList<ItemStorageEntry> incomingEntries,
            out GridPlacement[] reservedPlacements,
            out InventoryMutationResult.MutationFailure failure,
            out string error)
        {
            return TryReserveIncomingInternal(
                null,
                false,
                incomingEntries,
                out reservedPlacements,
                out failure,
                out error);
        }

        private bool TryReserveIncomingInternal(
            string removedInstanceId,
            bool removeExistingSource,
            IReadOnlyList<ItemStorageEntry> incomingEntries,
            out GridPlacement[] reservedPlacements,
            out InventoryMutationResult.MutationFailure failure,
            out string error)
        {
            int incomingCount = incomingEntries != null ? incomingEntries.Count : 0;
            reservedPlacements = new GridPlacement[incomingCount];
            failure = InventoryMutationResult.MutationFailure.None;
            error = null;

            if (layout == null)
                return true;
            if (!ValidateLayoutMatchesStorage(storage, layout))
            {
                failure = InventoryMutationResult.MutationFailure.StalePlan;
                error = "Personal grid layout does not match its storage.";
                return false;
            }
            if (removeExistingSource &&
                (string.IsNullOrWhiteSpace(removedInstanceId) ||
                 storage.GetEntryByInstanceId(removedInstanceId) == null ||
                 !layout.TryGetPlacement(removedInstanceId, out _)))
            {
                failure = InventoryMutationResult.MutationFailure.SourceNotFound;
                error = $"Source placement '{removedInstanceId}' was not found.";
                return false;
            }

            var simulated = new GridInventoryLayout(layout.Width, layout.Height);
            foreach (GridPlacement placement in layout.Placements)
            {
                if (placement == null || (removeExistingSource && placement.InstanceId == removedInstanceId))
                    continue;

                var occupied = new GridInventoryLayout.ReservedRect(
                    placement.X,
                    placement.Y,
                    placement.EffectiveWidth,
                    placement.EffectiveHeight,
                    placement.IsRotated);
                if (!simulated.TryAddPlacement(placement.InstanceId, occupied))
                {
                    failure = InventoryMutationResult.MutationFailure.StalePlan;
                    error = $"Existing placement '{placement.InstanceId}' could not be copied into the simulation.";
                    return false;
                }
            }

            var reservations = new List<GridInventoryLayout.ReservedRect>(incomingCount);
            var incomingIds = new HashSet<string>();
            for (int index = 0; index < incomingCount; index++)
            {
                ItemStorageEntry entry = incomingEntries[index];
                ItemInstance item = entry != null ? entry.Item : null;
                if (item == null || string.IsNullOrWhiteSpace(item.InstanceId) || !incomingIds.Add(item.InstanceId))
                {
                    failure = InventoryMutationResult.MutationFailure.InvalidArguments;
                    error = "Incoming placement simulation requires unique valid item instances.";
                    return false;
                }
                if (simulated.TryGetPlacement(item.InstanceId, out _))
                {
                    failure = InventoryMutationResult.MutationFailure.PlacementConflict;
                    error = $"Incoming instance '{item.InstanceId}' already has a personal grid placement.";
                    return false;
                }
                if (!TryResolveFootprint(item.DefinitionId, out GridFootprint footprint, out _, out error))
                {
                    failure = InventoryMutationResult.MutationFailure.InvalidFootprint;
                    return false;
                }
                if (!simulated.TryFindFirstFit(footprint, reservations, out GridInventoryLayout.ReservedRect reservation))
                {
                    failure = InventoryMutationResult.MutationFailure.NoGridSpace;
                    error = $"No personal grid placement is available for '{item.InstanceId}'.";
                    return false;
                }

                reservations.Add(reservation);
                reservedPlacements[index] = new GridPlacement(
                    item.InstanceId,
                    reservation.X,
                    reservation.Y,
                    reservation.IsRotated,
                    reservation.Width,
                    reservation.Height);
            }

            return true;
        }

        public GridPlacementValidationResult PreviewMovePlacement(
            string instanceId,
            int x,
            int y,
            bool isRotated)
        {
            if (layout == null)
            {
                return GridPlacementValidationResult.Invalid(
                    InventoryMutationResult.MutationFailure.GridLayoutUnavailable,
                    "Grid layout is not active.");
            }

            if (string.IsNullOrWhiteSpace(instanceId))
            {
                return GridPlacementValidationResult.Invalid(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    "A valid item instance id is required.");
            }

            ItemStorageEntry entry = storage.GetEntryByInstanceId(instanceId);
            if (entry == null || entry.Item == null)
            {
                return GridPlacementValidationResult.Invalid(
                    InventoryMutationResult.MutationFailure.SourceNotFound,
                    $"Item instance '{instanceId}' was not found.");
            }

            if (!layout.TryGetPlacement(instanceId, out _))
            {
                return GridPlacementValidationResult.Invalid(
                    InventoryMutationResult.MutationFailure.PlacementConflict,
                    $"Grid placement for item instance '{instanceId}' was not found.");
            }

            ItemDefinition definition = definitionResolver(entry.DefinitionId);
            if (definition == null)
            {
                return GridPlacementValidationResult.Invalid(
                    InventoryMutationResult.MutationFailure.ItemDefinitionNotFound,
                    $"Item definition '{entry.DefinitionId}' was not found.");
            }

            if (!GridFootprint.TryResolve(definition, out GridFootprint footprint, out bool usedFallback, out string footprintError))
            {
                return GridPlacementValidationResult.Invalid(
                    InventoryMutationResult.MutationFailure.InvalidFootprint,
                    footprintError);
            }

            if (!layout.TryCreateMoveCandidate(instanceId, footprint, x, y, isRotated, out GridPlacement candidate))
            {
                return GridPlacementValidationResult.Invalid(
                    InventoryMutationResult.MutationFailure.PlacementConflict,
                    $"Placement ({x},{y}) for item instance '{instanceId}' is outside the grid or overlaps another item.");
            }

            return GridPlacementValidationResult.Valid(candidate, usedFallback);
        }

        public InventoryMutationResult MovePlacement(
            string instanceId,
            int x,
            int y,
            bool isRotated)
        {
            GridPlacementValidationResult preview = PreviewMovePlacement(instanceId, x, y, isRotated);
            if (!preview.IsValid)
                return InventoryMutationResult.Rejected(preview.Failure, preview.Message, 0, instanceId);

            if (!layout.TryMovePlacement(preview.Candidate))
            {
                return InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.PlacementConflict,
                    $"Grid placement for item instance '{instanceId}' changed before commit.",
                    0,
                    instanceId);
            }

            return InventoryMutationResult.SucceededPlacementMove(
                instanceId,
                preview.Candidate,
                preview.UsedFallbackFootprint);
        }

        public InventoryMutationResult Add(ItemDefinition definition, int quantity)
        {
            if (definition == null)
            {
                return InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.ItemDefinitionNotFound,
                    "Item definition was not found.",
                    quantity);
            }

            if (quantity < 1)
            {
                return InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    "Quantity must be >= 1.",
                    quantity);
            }

            using (ItemInstanceIdRegistry.ItemInstanceIdReservationScope identityScope =
                   ItemInstanceIdRegistry.Instance.BeginReservationScope())
            {
                try
                {
                    ItemInstance item = ItemInstance.CreateNew(definition);
                    InventoryTransactionPlan plan = BuildAddPlan(definition, item, quantity, out InventoryMutationResult rejection);
                    if (plan == null)
                        return rejection;

                    InventoryMutationResult result = CommitAdd(plan, item);
                    if (result.Success)
                        identityScope.Commit();
                    return result;
                }
                catch (Exception exception)
                {
                    return InventoryMutationResult.RolledBack(exception.Message, quantity, null, false);
                }
            }
        }

        public InventoryMutationResult Remove(string instanceId, int quantity)
        {
            // This is the terminal consumption/destruction path. Ownership changes
            // must use the transfer APIs, which preserve the source identity.
            InventoryTransactionPlan plan = BuildRemovePlan(instanceId, quantity, out InventoryMutationResult rejection);
            return plan != null ? CommitRemove(plan) : rejection;
        }

        public InventoryMutationResult TransferFrom(ItemStorage sourceStorage, string sourceInstanceId, int quantity)
        {
            return ExecuteTransfer(
                sourceStorage,
                null,
                storage,
                layout,
                definitionResolver,
                sourceInstanceId,
                quantity,
                null);
        }

        public InventoryMutationResult TransferTo(ItemStorage targetStorage, string sourceInstanceId, int quantity)
        {
            return ExecuteTransfer(
                storage,
                layout,
                targetStorage,
                null,
                definitionResolver,
                sourceInstanceId,
                quantity,
                null);
        }

        public InventoryMutationResult TransferTo(GridInventoryBackend target, string sourceInstanceId, int quantity)
        {
            if (target == null)
            {
                return InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    "Target inventory backend is missing.",
                    quantity,
                    sourceInstanceId);
            }

            return ExecuteTransfer(
                storage,
                layout,
                target.storage,
                target.layout,
                target.definitionResolver,
                sourceInstanceId,
                quantity,
                null);
        }

        internal GridPlacementValidationResult PreviewTransferTo(
            GridInventoryBackend target,
            string sourceInstanceId)
        {
            if (target == null)
            {
                return GridPlacementValidationResult.Invalid(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    "Target inventory backend is missing.");
            }

            ItemStorageEntry sourceEntry = storage.GetEntryByInstanceId(sourceInstanceId);
            if (sourceEntry == null || sourceEntry.Item == null)
            {
                return GridPlacementValidationResult.Invalid(
                    InventoryMutationResult.MutationFailure.SourceNotFound,
                    $"Source item instance '{sourceInstanceId}' was not found.");
            }

            return PreviewTransferTo(target, sourceInstanceId, sourceEntry.Quantity);
        }

        internal GridPlacementValidationResult PreviewTransferTo(
            GridInventoryBackend target,
            string sourceInstanceId,
            int quantity)
        {
            if (target == null)
            {
                return GridPlacementValidationResult.Invalid(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    "Target inventory backend is missing.");
            }

            InventoryTransactionPlan plan = BuildTransferPlan(
                storage,
                layout,
                target.storage,
                target.layout,
                target.definitionResolver,
                sourceInstanceId,
                quantity,
                null,
                out InventoryMutationResult rejection);
            if (plan == null)
                return GridPlacementValidationResult.Invalid(rejection.Failure, rejection.Message);

            if (target.layout == null || plan.ReservedPlacements == null || plan.ReservedPlacements.Count == 0)
                return GridPlacementValidationResult.Valid(null, plan.UsedFallbackFootprint);

            GridInventoryLayout.ReservedRect reserved = plan.ReservedPlacements[0];
            return GridPlacementValidationResult.Valid(
                new GridPlacement(
                    sourceInstanceId,
                    reserved.X,
                    reserved.Y,
                    reserved.IsRotated,
                    reserved.Width,
                    reserved.Height),
                plan.UsedFallbackFootprint);
        }

        internal BackendStateSnapshot CaptureBackendState()
        {
            return new BackendStateSnapshot(
                storage.CaptureState(),
                layout != null ? layout.CaptureState() : default,
                layout != null);
        }

        internal void RestoreBackendState(BackendStateSnapshot snapshot)
        {
            storage.RestoreState(snapshot.Storage);
            if (snapshot.HasLayout && layout != null)
                layout.RestoreState(snapshot.Layout);
        }

        public GridPlacementValidationResult PreviewTransferToExact(
            GridInventoryBackend target,
            string sourceInstanceId,
            int targetX,
            int targetY,
            bool isRotated)
        {
            if (target == null)
            {
                return GridPlacementValidationResult.Invalid(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    "Target inventory backend is missing.");
            }

            ItemStorageEntry sourceEntry = storage.GetEntryByInstanceId(sourceInstanceId);
            if (sourceEntry == null || sourceEntry.Item == null)
            {
                return GridPlacementValidationResult.Invalid(
                    InventoryMutationResult.MutationFailure.SourceNotFound,
                    $"Source item instance '{sourceInstanceId}' was not found.");
            }

            var exactPlacement = new GridExactPlacementRequest(targetX, targetY, isRotated);
            InventoryTransactionPlan plan = BuildTransferPlan(
                storage,
                layout,
                target.storage,
                target.layout,
                target.definitionResolver,
                sourceInstanceId,
                sourceEntry.Quantity,
                exactPlacement,
                out InventoryMutationResult rejection);
            if (plan == null)
                return GridPlacementValidationResult.Invalid(rejection.Failure, rejection.Message);

            GridInventoryLayout.ReservedRect reserved = plan.ReservedPlacements[0];
            return GridPlacementValidationResult.Valid(
                new GridPlacement(
                    sourceInstanceId,
                    reserved.X,
                    reserved.Y,
                    reserved.IsRotated,
                    reserved.Width,
                    reserved.Height),
                plan.UsedFallbackFootprint);
        }

        public InventoryMutationResult TransferToExact(
            GridInventoryBackend target,
            string sourceInstanceId,
            int targetX,
            int targetY,
            bool isRotated)
        {
            if (target == null)
            {
                return InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    "Target inventory backend is missing.",
                    0,
                    sourceInstanceId);
            }

            ItemStorageEntry sourceEntry = storage.GetEntryByInstanceId(sourceInstanceId);
            if (sourceEntry == null || sourceEntry.Item == null)
            {
                return InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.SourceNotFound,
                    $"Source item instance '{sourceInstanceId}' was not found.",
                    0,
                    sourceInstanceId);
            }

            return ExecuteTransfer(
                storage,
                layout,
                target.storage,
                target.layout,
                target.definitionResolver,
                sourceInstanceId,
                sourceEntry.Quantity,
                new GridExactPlacementRequest(targetX, targetY, isRotated));
        }

        internal GridStorageMergePreview PreviewMergeIntoTarget(
            GridInventoryBackend target,
            string sourceInstanceId,
            string destinationInstanceId)
        {
            InventoryTransactionPlan plan = BuildDirectedMergePlan(
                target,
                sourceInstanceId,
                destinationInstanceId,
                out InventoryMutationResult rejection,
                out int sourceQuantity,
                out int destinationCapacity);

            return plan != null
                ? GridStorageMergePreview.Valid(
                    sourceInstanceId,
                    destinationInstanceId,
                    sourceQuantity,
                    destinationCapacity,
                    plan.Quantity)
                : GridStorageMergePreview.Invalid(
                    rejection != null ? rejection.Failure : InventoryMutationResult.MutationFailure.InvalidArguments,
                    rejection != null ? rejection.Message : "Directed merge preview failed.",
                    sourceInstanceId,
                    destinationInstanceId);
        }

        internal InventoryMutationResult MergeIntoTarget(
            GridInventoryBackend target,
            string sourceInstanceId,
            string destinationInstanceId)
        {
            InventoryTransactionPlan plan = BuildDirectedMergePlan(
                target,
                sourceInstanceId,
                destinationInstanceId,
                out InventoryMutationResult rejection,
                out _,
                out _);

            return plan != null ? CommitDirectedMerge(plan) : rejection;
        }

        private InventoryTransactionPlan BuildAddPlan(
            ItemDefinition definition,
            ItemInstance item,
            int quantity,
            out InventoryMutationResult rejection)
        {
            rejection = null;
            if (definition == null)
            {
                rejection = InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.ItemDefinitionNotFound,
                    "Item definition was not found.",
                    quantity);
                return null;
            }

            if (quantity < 1)
            {
                rejection = InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    "Quantity must be >= 1.",
                    quantity);
                return null;
            }

            var plan = new InventoryTransactionPlan
            {
                Operation = InventoryTransactionPlan.OperationKind.Add,
                TargetStorage = storage,
                TargetLayout = layout,
                Definition = definition,
                RequestedQuantity = quantity,
                Quantity = quantity,
                ExpectedTargetStorageVersion = storage.Version,
                ExpectedTargetLayoutVersion = layout != null ? layout.Version : 0
            };

            CalculateDestinationChanges(storage, item, quantity, out int mergeQuantity, out int newEntryCount);
            plan.MergeQuantity = mergeQuantity;
            plan.NewEntryCount = newEntryCount;

            if (layout == null || newEntryCount == 0)
                return plan;

            if (!GridFootprint.TryResolve(definition, out GridFootprint footprint, out bool usedFallback, out string footprintError))
            {
                rejection = InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.InvalidFootprint,
                    footprintError,
                    quantity);
                return null;
            }

            plan.Footprint = footprint;
            plan.UsedFallbackFootprint = usedFallback;
            if (!TryReservePlacements(layout, footprint, newEntryCount, plan.ReservedPlacements))
            {
                rejection = InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.NoGridSpace,
                    $"No grid space for '{definition.id}' x{quantity}.",
                    quantity);
                return null;
            }

            return plan;
        }

        private InventoryTransactionPlan BuildRemovePlan(string instanceId, int quantity, out InventoryMutationResult rejection)
        {
            rejection = null;
            if (string.IsNullOrWhiteSpace(instanceId) || quantity < 1)
            {
                rejection = InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    "A valid instance id and quantity are required.",
                    quantity,
                    instanceId);
                return null;
            }

            ItemStorageEntry entry = storage.GetEntryByInstanceId(instanceId);
            if (entry == null || entry.Item == null)
            {
                rejection = InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.SourceNotFound,
                    $"Item instance '{instanceId}' was not found.",
                    quantity,
                    instanceId);
                return null;
            }

            int removedQuantity = Math.Min(quantity, entry.Quantity);
            bool removesEntry = removedQuantity >= entry.Quantity;
            if (layout != null && removesEntry && !layout.TryGetPlacement(instanceId, out _))
            {
                rejection = InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.PlacementConflict,
                    $"Grid placement for item instance '{instanceId}' was not found.",
                    quantity,
                    instanceId);
                return null;
            }

            return new InventoryTransactionPlan
            {
                Operation = InventoryTransactionPlan.OperationKind.Remove,
                SourceStorage = storage,
                SourceLayout = layout,
                SourceInstanceId = instanceId,
                RequestedQuantity = quantity,
                Quantity = removedQuantity,
                SourceEntryWillBeRemoved = removesEntry,
                ExpectedSourceStorageVersion = storage.Version,
                ExpectedSourceLayoutVersion = layout != null ? layout.Version : 0
            };
        }

        private InventoryMutationResult CommitAdd(InventoryTransactionPlan plan, ItemInstance item)
        {
            ItemStorage.StateSnapshot storageSnapshot = storage.CaptureState();
            GridInventoryLayout.StateSnapshot layoutSnapshot = layout != null ? layout.CaptureState() : default;
            var previousIds = CollectInstanceIds(storage);

            try
            {
                EnsurePlanVersions(plan);
                ItemStorageEntry changedEntry = storage.AddItem(item, plan.Quantity);
                List<ItemStorageEntry> createdEntries = CollectNewEntries(storage, previousIds);
                GridPlacement[] addedPlacements = ApplyReservations(layout, plan.ReservedPlacements, createdEntries);

                if (layout != null && !ValidateLayoutMatchesStorage(storage, layout))
                    throw new InvalidOperationException("Grid/storage invariant failed after add.");

                if (storage.GetEntryByInstanceId(item.InstanceId) == null)
                    ItemInstanceIdRegistry.Instance.RetireAfterCommit(item.InstanceId);

                return InventoryMutationResult.Succeeded(
                    plan.RequestedQuantity,
                    plan.Quantity,
                    null,
                    changedEntry != null && changedEntry.Item != null ? changedEntry.Item.InstanceId : null,
                    plan.MergeQuantity,
                    GetInstanceIds(createdEntries),
                    null,
                    addedPlacements,
                    null,
                    plan.UsedFallbackFootprint);
            }
            catch (Exception ex)
            {
                storage.RestoreState(storageSnapshot);
                if (layout != null)
                    layout.RestoreState(layoutSnapshot);

                return InventoryMutationResult.RolledBack(ex.Message, plan.RequestedQuantity, null, plan.UsedFallbackFootprint);
            }
        }

        private InventoryMutationResult CommitRemove(InventoryTransactionPlan plan)
        {
            ItemStorage.StateSnapshot storageSnapshot = storage.CaptureState();
            GridInventoryLayout.StateSnapshot layoutSnapshot = layout != null ? layout.CaptureState() : default;

            using (ItemInstanceIdRegistry.ItemInstanceIdReservationScope identityScope =
                   ItemInstanceIdRegistry.Instance.BeginReservationScope())
            {
                try
                {
                    EnsurePlanVersions(plan);
                    int sourceIndex = storage.GetEntryIndexByInstanceId(plan.SourceInstanceId);
                    if (sourceIndex < 0 || !storage.RemoveAt(sourceIndex, plan.Quantity))
                        throw new InvalidOperationException("Source item changed before remove commit.");

                    string[] removedPlacementIds = null;
                    string[] removedInstanceIds = null;
                    if (plan.SourceEntryWillBeRemoved)
                    {
                        removedInstanceIds = new[] { plan.SourceInstanceId };
                        if (layout != null)
                        {
                            if (!layout.RemovePlacement(plan.SourceInstanceId))
                                throw new InvalidOperationException("Expected source grid placement could not be removed.");
                            removedPlacementIds = new[] { plan.SourceInstanceId };
                        }
                    }

                    if (layout != null && !ValidateLayoutMatchesStorage(storage, layout))
                        throw new InvalidOperationException("Grid/storage invariant failed after remove.");
                    if (plan.SourceEntryWillBeRemoved)
                        ItemInstanceIdRegistry.Instance.RetireAfterCommit(plan.SourceInstanceId);

                    InventoryMutationResult result = InventoryMutationResult.Succeeded(
                        plan.RequestedQuantity,
                        plan.Quantity,
                        plan.SourceInstanceId,
                        null,
                        0,
                        null,
                        removedInstanceIds,
                        null,
                        removedPlacementIds,
                        false);
                    identityScope.Commit();
                    return result;
                }
                catch (Exception ex)
                {
                    storage.RestoreState(storageSnapshot);
                    if (layout != null)
                        layout.RestoreState(layoutSnapshot);

                    return InventoryMutationResult.RolledBack(ex.Message, plan.RequestedQuantity, plan.SourceInstanceId, false);
                }
            }
        }

        private static InventoryMutationResult ExecuteTransfer(
            ItemStorage sourceStorage,
            GridInventoryLayout sourceLayout,
            ItemStorage targetStorage,
            GridInventoryLayout targetLayout,
            Func<string, ItemDefinition> definitionResolver,
            string sourceInstanceId,
            int requestedQuantity,
            GridExactPlacementRequest? exactPlacement)
        {
            InventoryTransactionPlan plan = BuildTransferPlan(
                sourceStorage,
                sourceLayout,
                targetStorage,
                targetLayout,
                definitionResolver,
                sourceInstanceId,
                requestedQuantity,
                exactPlacement,
                out InventoryMutationResult rejection);
            if (plan == null)
                return rejection;

            ItemStorage.StateSnapshot sourceSnapshot = sourceStorage.CaptureState();
            ItemStorage.StateSnapshot targetSnapshot = targetStorage.CaptureState();
            GridInventoryLayout.StateSnapshot sourceLayoutSnapshot = sourceLayout != null ? sourceLayout.CaptureState() : default;
            GridInventoryLayout.StateSnapshot targetLayoutSnapshot = targetLayout != null ? targetLayout.CaptureState() : default;
            var targetPreviousIds = CollectInstanceIds(targetStorage);
            int combinedQuantityBefore = sourceStorage.TotalQuantity + targetStorage.TotalQuantity;

            using (ItemInstanceIdRegistry.ItemInstanceIdReservationScope identityScope =
                   ItemInstanceIdRegistry.Instance.BeginReservationScope())
            {
                try
                {
                    EnsurePlanVersions(plan);
                    int sourceIndex = sourceStorage.GetEntryIndexByInstanceId(sourceInstanceId);
                    if (sourceIndex < 0)
                        throw new InvalidOperationException("Source item changed before transfer commit.");
                    ItemStorageEntry originalSourceEntry = sourceStorage.GetEntry(sourceIndex);
                    ItemInstance sourceItem = originalSourceEntry != null ? originalSourceEntry.Item : null;
                    if (sourceItem == null)
                        throw new InvalidOperationException("Source item changed before transfer commit.");

                    int transferredQuantity;
                    if (plan.UsesExactTargetPlacement)
                    {
                        if (originalSourceEntry.Quantity != plan.Quantity)
                            throw new InvalidOperationException("Exact transfer source stack changed before commit.");

                        if (!sourceStorage.RemoveAt(sourceIndex, plan.Quantity))
                            throw new InvalidOperationException("Exact transfer could not remove the source stack.");

                        targetStorage.AddItemAsSeparateEntry(sourceItem, plan.Quantity);
                        transferredQuantity = plan.Quantity;
                    }
                    else
                    {
                        transferredQuantity = sourceStorage.TransferTo(targetStorage, sourceIndex, plan.Quantity);
                        if (transferredQuantity != plan.Quantity)
                        {
                            throw new InvalidOperationException(
                                $"Transfer committed x{transferredQuantity}, expected x{plan.Quantity}.");
                        }
                    }

                    List<ItemStorageEntry> createdEntries = CollectNewEntries(targetStorage, targetPreviousIds);
                    GridPlacement[] addedPlacements = ApplyReservations(targetLayout, plan.ReservedPlacements, createdEntries);

                    string[] removedInstanceIds = null;
                    string[] removedPlacementIds = null;
                    if (plan.SourceEntryWillBeRemoved)
                    {
                        removedInstanceIds = new[] { sourceInstanceId };
                        if (sourceLayout != null)
                        {
                            if (!sourceLayout.RemovePlacement(sourceInstanceId))
                                throw new InvalidOperationException("Expected source grid placement could not be removed.");
                            removedPlacementIds = new[] { sourceInstanceId };
                        }
                    }

                    if (sourceStorage.TotalQuantity + targetStorage.TotalQuantity != combinedQuantityBefore)
                        throw new InvalidOperationException("Transfer quantity conservation failed.");
                    if (sourceLayout != null && !ValidateLayoutMatchesStorage(sourceStorage, sourceLayout))
                        throw new InvalidOperationException("Source grid/storage invariant failed after transfer.");
                    if (targetLayout != null && !ValidateLayoutMatchesStorage(targetStorage, targetLayout))
                        throw new InvalidOperationException("Target grid/storage invariant failed after transfer.");

                    string destinationInstanceId = createdEntries.Count > 0
                        ? createdEntries[0].Item.InstanceId
                        : FindFirstCompatibleInstanceId(targetStorage, sourceItem);

                    if (plan.SourceEntryWillBeRemoved && targetStorage.GetEntryByInstanceId(sourceInstanceId) == null)
                        ItemInstanceIdRegistry.Instance.RetireAfterCommit(sourceInstanceId);

                    InventoryMutationResult result = InventoryMutationResult.Succeeded(
                        requestedQuantity,
                        transferredQuantity,
                        sourceInstanceId,
                        destinationInstanceId,
                        plan.MergeQuantity,
                        GetInstanceIds(createdEntries),
                        removedInstanceIds,
                        addedPlacements,
                        removedPlacementIds,
                        plan.UsedFallbackFootprint);
                    identityScope.Commit();
                    return result;
                }
                catch (Exception ex)
                {
                    sourceStorage.RestoreState(sourceSnapshot);
                    targetStorage.RestoreState(targetSnapshot);
                    if (sourceLayout != null)
                        sourceLayout.RestoreState(sourceLayoutSnapshot);
                    if (targetLayout != null)
                        targetLayout.RestoreState(targetLayoutSnapshot);

                    return InventoryMutationResult.RolledBack(ex.Message, requestedQuantity, sourceInstanceId, plan.UsedFallbackFootprint);
                }
            }
        }

        private static InventoryTransactionPlan BuildTransferPlan(
            ItemStorage sourceStorage,
            GridInventoryLayout sourceLayout,
            ItemStorage targetStorage,
            GridInventoryLayout targetLayout,
            Func<string, ItemDefinition> definitionResolver,
            string sourceInstanceId,
            int requestedQuantity,
            GridExactPlacementRequest? exactPlacement,
            out InventoryMutationResult rejection)
        {
            rejection = null;
            if (sourceStorage == null || targetStorage == null || ReferenceEquals(sourceStorage, targetStorage) ||
                string.IsNullOrWhiteSpace(sourceInstanceId) || requestedQuantity < 1)
            {
                rejection = InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    "Valid distinct storages, instance id and quantity are required.",
                    requestedQuantity,
                    sourceInstanceId);
                return null;
            }

            ItemStorageEntry sourceEntry = sourceStorage.GetEntryByInstanceId(sourceInstanceId);
            if (sourceEntry == null || sourceEntry.Item == null)
            {
                rejection = InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.SourceNotFound,
                    $"Source item instance '{sourceInstanceId}' was not found.",
                    requestedQuantity,
                    sourceInstanceId);
                return null;
            }

            if (requestedQuantity > sourceEntry.Quantity)
            {
                rejection = InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.InsufficientQuantity,
                    $"Source item instance '{sourceInstanceId}' contains x{sourceEntry.Quantity}, requested x{requestedQuantity}.",
                    requestedQuantity,
                    sourceInstanceId);
                return null;
            }

            int transferQuantity = requestedQuantity;
            bool removesSourceEntry = transferQuantity >= sourceEntry.Quantity;

            if (exactPlacement.HasValue && requestedQuantity != sourceEntry.Quantity)
            {
                rejection = InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.ExactTransferRequiresFullStack,
                    "Exact grid transfer requires the complete source stack.",
                    requestedQuantity,
                    sourceInstanceId);
                return null;
            }

            if (sourceLayout != null && removesSourceEntry && !sourceLayout.TryGetPlacement(sourceInstanceId, out _))
            {
                rejection = InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.PlacementConflict,
                    $"Source grid placement for '{sourceInstanceId}' was not found.",
                    requestedQuantity,
                    sourceInstanceId);
                return null;
            }

            ItemDefinition definition = definitionResolver(sourceEntry.DefinitionId);
            if (definition == null)
            {
                rejection = InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.ItemDefinitionNotFound,
                    $"Item definition '{sourceEntry.DefinitionId}' was not found.",
                    requestedQuantity,
                    sourceInstanceId);
                return null;
            }

            var plan = new InventoryTransactionPlan
            {
                Operation = InventoryTransactionPlan.OperationKind.Transfer,
                SourceStorage = sourceStorage,
                TargetStorage = targetStorage,
                SourceLayout = sourceLayout,
                TargetLayout = targetLayout,
                Definition = definition,
                SourceInstanceId = sourceInstanceId,
                RequestedQuantity = requestedQuantity,
                Quantity = transferQuantity,
                SourceEntryWillBeRemoved = removesSourceEntry,
                ExpectedSourceStorageVersion = sourceStorage.Version,
                ExpectedTargetStorageVersion = targetStorage.Version,
                ExpectedSourceLayoutVersion = sourceLayout != null ? sourceLayout.Version : 0,
                ExpectedTargetLayoutVersion = targetLayout != null ? targetLayout.Version : 0
            };

            if (exactPlacement.HasValue)
            {
                if (targetLayout == null)
                {
                    rejection = InventoryMutationResult.Rejected(
                        InventoryMutationResult.MutationFailure.GridLayoutUnavailable,
                        "Exact grid transfer requires an active target layout.",
                        requestedQuantity,
                        sourceInstanceId);
                    return null;
                }

                plan.MergeQuantity = 0;
                plan.NewEntryCount = 1;
            }
            else
            {
                CalculateDestinationChanges(targetStorage, sourceEntry.Item, transferQuantity, out int mergeQuantity, out int newEntryCount);
                plan.MergeQuantity = mergeQuantity;
                plan.NewEntryCount = newEntryCount;
            }

            if (targetLayout == null || plan.NewEntryCount == 0)
                return plan;

            if (!GridFootprint.TryResolve(definition, out GridFootprint footprint, out bool usedFallback, out string footprintError))
            {
                rejection = InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.InvalidFootprint,
                    footprintError,
                    requestedQuantity,
                    sourceInstanceId);
                return null;
            }

            plan.Footprint = footprint;
            plan.UsedFallbackFootprint = usedFallback;

            if (exactPlacement.HasValue)
            {
                GridExactPlacementRequest requested = exactPlacement.Value;
                if (!targetLayout.TryCreateIncomingCandidate(
                        sourceInstanceId,
                        footprint,
                        requested.X,
                        requested.Y,
                        requested.IsRotated,
                        out GridInventoryLayout.ReservedRect exactReservation,
                        out _))
                {
                    rejection = InventoryMutationResult.Rejected(
                        InventoryMutationResult.MutationFailure.PlacementConflict,
                        $"Exact target placement ({requested.X},{requested.Y}) is outside the grid or occupied.",
                        requestedQuantity,
                        sourceInstanceId);
                    return null;
                }

                plan.UsesExactTargetPlacement = true;
                plan.ReservedPlacements.Add(exactReservation);
                return plan;
            }

            if (!TryReservePlacements(targetLayout, footprint, plan.NewEntryCount, plan.ReservedPlacements))
            {
                rejection = InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.NoGridSpace,
                    $"No grid space for '{sourceEntry.DefinitionId}' x{transferQuantity}.",
                    requestedQuantity,
                    sourceInstanceId);
                return null;
            }

            return plan;
        }

        private InventoryTransactionPlan BuildDirectedMergePlan(
            GridInventoryBackend target,
            string sourceInstanceId,
            string destinationInstanceId,
            out InventoryMutationResult rejection,
            out int sourceQuantity,
            out int destinationCapacity)
        {
            rejection = null;
            sourceQuantity = 0;
            destinationCapacity = 0;

            if (target == null || ReferenceEquals(target, this) || ReferenceEquals(target.storage, storage))
            {
                rejection = InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    "Directed merge requires two different storage owners.",
                    0,
                    sourceInstanceId);
                return null;
            }

            if (string.IsNullOrWhiteSpace(sourceInstanceId) ||
                string.IsNullOrWhiteSpace(destinationInstanceId) ||
                sourceInstanceId == destinationInstanceId)
            {
                rejection = InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    "Directed merge requires two different valid instance ids.",
                    0,
                    sourceInstanceId);
                return null;
            }

            ItemStorageEntry sourceEntry = storage.GetEntryByInstanceId(sourceInstanceId);
            if (sourceEntry == null || sourceEntry.Item == null)
            {
                rejection = InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.SourceNotFound,
                    $"Source item instance '{sourceInstanceId}' was not found.",
                    0,
                    sourceInstanceId);
                return null;
            }

            sourceQuantity = sourceEntry.Quantity;
            ItemStorageEntry destinationEntry = target.storage.GetEntryByInstanceId(destinationInstanceId);
            if (destinationEntry == null || destinationEntry.Item == null)
            {
                rejection = InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.SourceNotFound,
                    $"Destination item instance '{destinationInstanceId}' was not found.",
                    sourceQuantity,
                    sourceInstanceId);
                return null;
            }

            if (!ItemInstance.CanStackWith(sourceEntry.Item, destinationEntry.Item))
            {
                rejection = InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.IncompatibleStack,
                    "Destination occupied",
                    sourceQuantity,
                    sourceInstanceId);
                return null;
            }

            ItemDefinition sourceDefinition = definitionResolver(sourceEntry.DefinitionId);
            ItemDefinition destinationDefinition = target.definitionResolver(destinationEntry.DefinitionId);
            if (sourceDefinition == null || destinationDefinition == null)
            {
                rejection = InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.ItemDefinitionNotFound,
                    $"Item definition '{sourceEntry.DefinitionId}' was not found for directed merge.",
                    sourceQuantity,
                    sourceInstanceId);
                return null;
            }

            if (layout != null && !layout.TryGetPlacement(sourceInstanceId, out _))
            {
                rejection = InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.PlacementConflict,
                    $"Source grid placement for '{sourceInstanceId}' was not found.",
                    sourceQuantity,
                    sourceInstanceId);
                return null;
            }

            if (target.layout != null && !target.layout.TryGetPlacement(destinationInstanceId, out _))
            {
                rejection = InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.PlacementConflict,
                    $"Destination grid placement for '{destinationInstanceId}' was not found.",
                    sourceQuantity,
                    sourceInstanceId);
                return null;
            }

            destinationCapacity = Math.Max(1, destinationDefinition.max_stack) - destinationEntry.Quantity;
            if (destinationCapacity <= 0)
            {
                rejection = InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.StackFull,
                    "Stack is full",
                    sourceQuantity,
                    sourceInstanceId);
                return null;
            }

            int transferQuantity = Math.Min(sourceQuantity, destinationCapacity);
            return new InventoryTransactionPlan
            {
                Operation = InventoryTransactionPlan.OperationKind.DirectedMerge,
                SourceStorage = storage,
                TargetStorage = target.storage,
                SourceLayout = layout,
                TargetLayout = target.layout,
                SourceDefinitionResolver = definitionResolver,
                TargetDefinitionResolver = target.definitionResolver,
                Definition = destinationDefinition,
                SourceInstanceId = sourceInstanceId,
                DestinationInstanceId = destinationInstanceId,
                RequestedQuantity = sourceQuantity,
                Quantity = transferQuantity,
                MergeQuantity = transferQuantity,
                SourceEntryWillBeRemoved = transferQuantity >= sourceQuantity,
                ExpectedSourceStorageVersion = storage.Version,
                ExpectedTargetStorageVersion = target.storage.Version,
                ExpectedSourceLayoutVersion = layout != null ? layout.Version : 0,
                ExpectedTargetLayoutVersion = target.layout != null ? target.layout.Version : 0
            };
        }

        private static InventoryMutationResult CommitDirectedMerge(InventoryTransactionPlan plan)
        {
            ItemStorage sourceStorage = plan.SourceStorage;
            ItemStorage targetStorage = plan.TargetStorage;
            GridInventoryLayout sourceLayout = plan.SourceLayout;
            GridInventoryLayout targetLayout = plan.TargetLayout;
            ItemStorage.StateSnapshot sourceSnapshot = sourceStorage.CaptureState();
            ItemStorage.StateSnapshot targetSnapshot = targetStorage.CaptureState();
            GridInventoryLayout.StateSnapshot sourceLayoutSnapshot = sourceLayout != null ? sourceLayout.CaptureState() : default;
            GridInventoryLayout.StateSnapshot targetLayoutSnapshot = targetLayout != null ? targetLayout.CaptureState() : default;
            int combinedQuantityBefore = sourceStorage.TotalQuantity + targetStorage.TotalQuantity;

            using (ItemInstanceIdRegistry.ItemInstanceIdReservationScope identityScope =
                   ItemInstanceIdRegistry.Instance.BeginReservationScope())
            {
                try
                {
                    EnsurePlanVersions(plan);

                    ItemStorageEntry sourceEntry = sourceStorage.GetEntryByInstanceId(plan.SourceInstanceId);
                    ItemStorageEntry destinationEntry = targetStorage.GetEntryByInstanceId(plan.DestinationInstanceId);
                    if (sourceEntry == null || sourceEntry.Item == null)
                        throw new InvalidOperationException("Directed merge source no longer exists.");
                    if (destinationEntry == null || destinationEntry.Item == null)
                        throw new InvalidOperationException("Directed merge destination no longer exists.");
                    if (sourceEntry.Item.InstanceId == destinationEntry.Item.InstanceId ||
                        !ItemInstance.CanStackWith(sourceEntry.Item, destinationEntry.Item))
                    {
                        throw new InvalidOperationException("Directed merge endpoints are no longer compatible.");
                    }

                    ItemDefinition sourceDefinition = plan.SourceDefinitionResolver?.Invoke(sourceEntry.DefinitionId);
                    ItemDefinition destinationDefinition = plan.TargetDefinitionResolver?.Invoke(destinationEntry.DefinitionId);
                    if (sourceDefinition == null || destinationDefinition == null)
                        throw new InvalidOperationException("Directed merge definition is no longer available.");

                    if (sourceLayout != null && !sourceLayout.TryGetPlacement(plan.SourceInstanceId, out _))
                        throw new InvalidOperationException("Directed merge source placement no longer exists.");
                    if (targetLayout != null && !targetLayout.TryGetPlacement(plan.DestinationInstanceId, out _))
                        throw new InvalidOperationException("Directed merge destination placement no longer exists.");

                    int destinationCapacity = Math.Max(1, destinationDefinition.max_stack) - destinationEntry.Quantity;
                    if (destinationCapacity <= 0)
                        throw new InvalidOperationException("Directed merge destination stack is full.");

                    int transferQuantity = Math.Min(sourceEntry.Quantity, destinationCapacity);
                    bool removesSource = transferQuantity >= sourceEntry.Quantity;
                    int sourceIndex = sourceStorage.GetEntryIndexByInstanceId(plan.SourceInstanceId);
                    if (sourceIndex < 0 || !targetStorage.TryAddQuantityToEntry(plan.DestinationInstanceId, sourceEntry.Item, transferQuantity))
                        throw new InvalidOperationException("Directed merge quantities changed before commit.");
                    if (!sourceStorage.RemoveAt(sourceIndex, transferQuantity))
                        throw new InvalidOperationException("Directed merge could not update the source quantity.");

                    string[] removedInstanceIds = null;
                    string[] removedPlacementIds = null;
                    if (removesSource)
                    {
                        removedInstanceIds = new[] { plan.SourceInstanceId };
                        if (sourceLayout != null)
                        {
                            if (!sourceLayout.RemovePlacement(plan.SourceInstanceId))
                                throw new InvalidOperationException("Directed merge could not remove the consumed source placement.");
                            removedPlacementIds = new[] { plan.SourceInstanceId };
                        }
                    }

                    if (sourceStorage.TotalQuantity + targetStorage.TotalQuantity != combinedQuantityBefore)
                        throw new InvalidOperationException("Directed merge quantity conservation failed.");
                    if (sourceLayout != null && !ValidateLayoutMatchesStorage(sourceStorage, sourceLayout))
                        throw new InvalidOperationException("Source grid/storage invariant failed after directed merge.");
                    if (targetLayout != null && !ValidateLayoutMatchesStorage(targetStorage, targetLayout))
                        throw new InvalidOperationException("Target grid/storage invariant failed after directed merge.");

                    if (removesSource)
                        ItemInstanceIdRegistry.Instance.RetireAfterCommit(plan.SourceInstanceId);

                    InventoryMutationResult result = InventoryMutationResult.Succeeded(
                        plan.RequestedQuantity,
                        transferQuantity,
                        plan.SourceInstanceId,
                        plan.DestinationInstanceId,
                        transferQuantity,
                        null,
                        removedInstanceIds,
                        null,
                        removedPlacementIds,
                        false);
                    identityScope.Commit();
                    return result;
                }
                catch (Exception ex)
                {
                    sourceStorage.RestoreState(sourceSnapshot);
                    targetStorage.RestoreState(targetSnapshot);
                    if (sourceLayout != null)
                        sourceLayout.RestoreState(sourceLayoutSnapshot);
                    if (targetLayout != null)
                        targetLayout.RestoreState(targetLayoutSnapshot);

                    return InventoryMutationResult.RolledBack(
                        ex.Message,
                        plan.RequestedQuantity,
                        plan.SourceInstanceId,
                        false);
                }
            }
        }

        private static void CalculateDestinationChanges(
            ItemStorage target,
            ItemInstance item,
            int quantity,
            out int mergeQuantity,
            out int newEntryCount)
        {
            int availableMerge = 0;
            IReadOnlyList<ItemStorageEntry> entries = target.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                ItemStorageEntry entry = entries[index];
                if (entry != null && ItemInstance.CanStackWith(entry.Item, item))
                    availableMerge += entry.AvailableStackSpace;
            }

            mergeQuantity = Math.Min(quantity, availableMerge);
            int remaining = quantity - mergeQuantity;
            int safeMaxStack = item != null ? Math.Max(1, item.MaxStack) : 1;
            newEntryCount = remaining > 0 ? (remaining + safeMaxStack - 1) / safeMaxStack : 0;
        }

        private static bool TryReservePlacements(
            GridInventoryLayout targetLayout,
            GridFootprint footprint,
            int count,
            List<GridInventoryLayout.ReservedRect> reservations)
        {
            for (int index = 0; index < count; index++)
            {
                if (!targetLayout.TryFindFirstFit(footprint, reservations, out GridInventoryLayout.ReservedRect reservation))
                    return false;

                reservations.Add(reservation);
            }

            return true;
        }

        private static GridPlacement[] ApplyReservations(
            GridInventoryLayout targetLayout,
            IReadOnlyList<GridInventoryLayout.ReservedRect> reservations,
            IReadOnlyList<ItemStorageEntry> createdEntries)
        {
            if (targetLayout == null)
                return Array.Empty<GridPlacement>();

            int expectedCount = reservations != null ? reservations.Count : 0;
            if (createdEntries.Count != expectedCount)
                throw new InvalidOperationException($"Created entry count {createdEntries.Count} does not match reserved placement count {expectedCount}.");

            var added = new GridPlacement[expectedCount];
            for (int index = 0; index < expectedCount; index++)
            {
                string instanceId = createdEntries[index].Item.InstanceId;
                if (!targetLayout.TryAddPlacement(instanceId, reservations[index]) ||
                    !targetLayout.TryGetPlacement(instanceId, out GridPlacement placement))
                {
                    throw new InvalidOperationException($"Could not commit reserved placement for '{instanceId}'.");
                }

                added[index] = placement;
            }

            return added;
        }

        private static void EnsurePlanVersions(InventoryTransactionPlan plan)
        {
            if (plan.SourceStorage != null && plan.SourceStorage.Version != plan.ExpectedSourceStorageVersion)
                throw new InvalidOperationException("Source storage plan is stale.");
            if (plan.TargetStorage != null && plan.TargetStorage.Version != plan.ExpectedTargetStorageVersion)
                throw new InvalidOperationException("Target storage plan is stale.");
            if (plan.SourceLayout != null && plan.SourceLayout.Version != plan.ExpectedSourceLayoutVersion)
                throw new InvalidOperationException("Source layout plan is stale.");
            if (plan.TargetLayout != null && plan.TargetLayout.Version != plan.ExpectedTargetLayoutVersion)
                throw new InvalidOperationException("Target layout plan is stale.");
        }

        private static HashSet<string> CollectInstanceIds(ItemStorage itemStorage)
        {
            var result = new HashSet<string>();
            IReadOnlyList<ItemStorageEntry> entries = itemStorage.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                ItemInstance item = entries[index] != null ? entries[index].Item : null;
                if (item != null)
                    result.Add(item.InstanceId);
            }

            return result;
        }

        private static List<ItemStorageEntry> CollectNewEntries(ItemStorage itemStorage, HashSet<string> previousIds)
        {
            var result = new List<ItemStorageEntry>();
            IReadOnlyList<ItemStorageEntry> entries = itemStorage.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                ItemStorageEntry entry = entries[index];
                ItemInstance item = entry != null ? entry.Item : null;
                if (item != null && !previousIds.Contains(item.InstanceId))
                    result.Add(entry);
            }

            return result;
        }

        private static string[] GetInstanceIds(IReadOnlyList<ItemStorageEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return Array.Empty<string>();

            var result = new string[entries.Count];
            for (int index = 0; index < entries.Count; index++)
                result[index] = entries[index].Item.InstanceId;
            return result;
        }

        private static string FindFirstCompatibleInstanceId(ItemStorage itemStorage, ItemInstance item)
        {
            IReadOnlyList<ItemStorageEntry> entries = itemStorage.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                ItemStorageEntry entry = entries[index];
                if (entry != null && ItemInstance.CanStackWith(entry.Item, item))
                    return entry.Item.InstanceId;
            }

            return null;
        }

        private static bool ValidateLayoutMatchesStorage(ItemStorage itemStorage, GridInventoryLayout gridLayout)
        {
            if (itemStorage == null || gridLayout == null || !gridLayout.ValidateNoOverlapOrBounds())
                return false;

            IReadOnlyList<ItemStorageEntry> entries = itemStorage.Entries;
            if (gridLayout.Placements.Count != entries.Count)
                return false;

            for (int index = 0; index < entries.Count; index++)
            {
                ItemInstance item = entries[index] != null ? entries[index].Item : null;
                if (item == null || !gridLayout.TryGetPlacement(item.InstanceId, out _))
                    return false;
            }

            return true;
        }

        internal readonly struct BackendStateSnapshot
        {
            internal BackendStateSnapshot(
                ItemStorage.StateSnapshot storage,
                GridInventoryLayout.StateSnapshot layout,
                bool hasLayout)
            {
                Storage = storage;
                Layout = layout;
                HasLayout = hasLayout;
            }

            internal ItemStorage.StateSnapshot Storage { get; }
            internal GridInventoryLayout.StateSnapshot Layout { get; }
            internal bool HasLayout { get; }
        }
    }
}
