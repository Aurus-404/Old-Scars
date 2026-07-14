using System;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Interactions;
using UnityEngine;

namespace OldScars.Core.Items
{
    public enum GridStorageTransferQuantityPolicy
    {
        Exact,
        ClampIncomingToActorHardLimit
    }

    internal interface IGridStorageTransferEndpoint
    {
        GridInventoryBackend TransferBackend { get; }
        bool CanTransferOut(GridStorageTransferContext context, out string reason);
        bool CanTransferIn(GridStorageTransferContext context, out string reason);
        void OnTransferCommittedOut(GridStorageTransferReceipt receipt, GridStorageTransferContext context);
        void OnTransferCommittedIn(GridStorageTransferReceipt receipt, GridStorageTransferContext context);
    }

    public readonly struct GridStorageTransferContext
    {
        public GridStorageTransferContext(DebugActionExecutionContext executionContext, ActionDefinition action)
        {
            ExecutionContext = executionContext;
            Action = action;
        }

        public DebugActionExecutionContext ExecutionContext { get; }
        public ActionDefinition Action { get; }
    }

    public readonly struct GridStorageTransferReceipt
    {
        public GridStorageTransferReceipt(string definitionId, InventoryMutationResult result)
        {
            DefinitionId = definitionId;
            Result = result;
        }

        public string DefinitionId { get; }
        public InventoryMutationResult Result { get; }
        public int TransferredQuantity => Result != null ? Result.AffectedQuantity : 0;
        public int RequestedQuantity => Result != null ? Result.RequestedQuantity : 0;
        public int ActualTransferredQuantity => Result != null ? Result.ActualTransferredQuantity : 0;
        public int SourceRemainingQuantity => Result != null ? Result.SourceRemainingQuantity : -1;
        public bool WasLimitedByWeight => Result != null && Result.WasLimitedByWeight;
        public int WeightLimitQuantity => Result != null ? Result.WeightLimitQuantity : -1;
        public string SourceInstanceId => Result != null ? Result.SourceInstanceId : null;
        public string DestinationInstanceId => Result != null ? Result.DestinationInstanceId : null;
        public bool SourceWasRemoved
        {
            get
            {
                if (Result == null || Result.RemovedInstanceIds == null || string.IsNullOrWhiteSpace(Result.SourceInstanceId))
                    return false;

                for (int index = 0; index < Result.RemovedInstanceIds.Length; index++)
                {
                    if (Result.RemovedInstanceIds[index] == Result.SourceInstanceId)
                        return true;
                }

                return false;
            }
        }
    }

    public readonly struct GridStorageAutoTransferPreview
    {
        private GridStorageAutoTransferPreview(
            bool isValid,
            InventoryMutationResult.MutationFailure failure,
            string message,
            string definitionId,
            string sourceInstanceId,
            int requestedQuantity,
            int sourceQuantity,
            int effectiveQuantity,
            int weightLimitQuantity,
            bool wasLimitedByWeight)
        {
            IsValid = isValid;
            Failure = failure;
            Message = message;
            DefinitionId = definitionId;
            SourceInstanceId = sourceInstanceId;
            RequestedQuantity = requestedQuantity;
            SourceQuantity = sourceQuantity;
            EffectiveQuantity = effectiveQuantity;
            WeightLimitQuantity = weightLimitQuantity;
            WasLimitedByWeight = wasLimitedByWeight;
        }

        public bool IsValid { get; }
        public InventoryMutationResult.MutationFailure Failure { get; }
        public string Message { get; }
        public string DefinitionId { get; }
        public string SourceInstanceId { get; }
        public int RequestedQuantity { get; }
        public int SourceQuantity { get; }
        public int EffectiveQuantity { get; }
        public int WeightLimitQuantity { get; }
        public bool WasLimitedByWeight { get; }

        internal static GridStorageAutoTransferPreview Valid(
            string definitionId,
            string sourceInstanceId,
            int requestedQuantity,
            int sourceQuantity,
            int effectiveQuantity,
            int weightLimitQuantity,
            bool wasLimitedByWeight)
        {
            return new GridStorageAutoTransferPreview(
                true,
                InventoryMutationResult.MutationFailure.None,
                null,
                definitionId,
                sourceInstanceId,
                requestedQuantity,
                sourceQuantity,
                effectiveQuantity,
                weightLimitQuantity,
                wasLimitedByWeight);
        }

        internal static GridStorageAutoTransferPreview Invalid(
            InventoryMutationResult.MutationFailure failure,
            string message,
            string definitionId,
            string sourceInstanceId,
            int requestedQuantity,
            int sourceQuantity,
            int weightLimitQuantity,
            bool wasLimitedByWeight)
        {
            return new GridStorageAutoTransferPreview(
                false,
                failure,
                message,
                definitionId,
                sourceInstanceId,
                requestedQuantity,
                sourceQuantity,
                0,
                weightLimitQuantity,
                wasLimitedByWeight);
        }
    }

    public readonly struct GridStorageMergePreview
    {
        private GridStorageMergePreview(
            bool isValid,
            InventoryMutationResult.MutationFailure failure,
            string message,
            string sourceInstanceId,
            string destinationInstanceId,
            int sourceQuantity,
            int destinationCapacity,
            int transferQuantity)
        {
            IsValid = isValid;
            Failure = failure;
            Message = message;
            SourceInstanceId = sourceInstanceId;
            DestinationInstanceId = destinationInstanceId;
            SourceQuantity = sourceQuantity;
            DestinationCapacity = destinationCapacity;
            TransferQuantity = transferQuantity;
        }

        public bool IsValid { get; }
        public InventoryMutationResult.MutationFailure Failure { get; }
        public string Message { get; }
        public string SourceInstanceId { get; }
        public string DestinationInstanceId { get; }
        public int SourceQuantity { get; }
        public int DestinationCapacity { get; }
        public int TransferQuantity { get; }

        internal static GridStorageMergePreview Valid(
            string sourceInstanceId,
            string destinationInstanceId,
            int sourceQuantity,
            int destinationCapacity,
            int transferQuantity)
        {
            return new GridStorageMergePreview(
                true,
                InventoryMutationResult.MutationFailure.None,
                null,
                sourceInstanceId,
                destinationInstanceId,
                sourceQuantity,
                destinationCapacity,
                transferQuantity);
        }

        internal static GridStorageMergePreview Invalid(
            InventoryMutationResult.MutationFailure failure,
            string message,
            string sourceInstanceId,
            string destinationInstanceId)
        {
            return new GridStorageMergePreview(
                false,
                failure,
                message,
                sourceInstanceId,
                destinationInstanceId,
                0,
                0,
                0);
        }
    }

    public static class GridStorageTransferService
    {
        public static GridStorageTransferQuantityPolicy GetAutomaticQuantityPolicy(
            IGridStorageOwner source,
            IGridStorageOwner target)
        {
            if (source == null || target == null || ItemOwnedStorageRegistry.Instance.ShareRootOwner(source, target))
                return GridStorageTransferQuantityPolicy.Exact;

            return TryResolveCarryWeightOwner(target, out ICarryWeightLimitedOwner limitedOwner) && limitedOwner.HasCarryWeightLimit
                ? GridStorageTransferQuantityPolicy.ClampIncomingToActorHardLimit
                : GridStorageTransferQuantityPolicy.Exact;
        }

        public static GridStorageMergePreview PreviewMergeIntoTarget(
            IGridStorageOwner source,
            string sourceInstanceId,
            IGridStorageOwner destination,
            string destinationInstanceId,
            GridStorageTransferContext context)
        {
            if (ReferenceEquals(source, destination) || sourceInstanceId == destinationInstanceId)
            {
                return GridStorageMergePreview.Invalid(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    "Directed merge requires different owners and instance ids.",
                    sourceInstanceId,
                    destinationInstanceId);
            }

            if (!TryResolveEndpoints(source, destination, context,
                    out IGridStorageTransferEndpoint sourceEndpoint,
                    out IGridStorageTransferEndpoint destinationEndpoint,
                    out string error))
            {
                return GridStorageMergePreview.Invalid(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    error,
                    sourceInstanceId,
                    destinationInstanceId);
            }

            if (!source.TryGetEntryByInstanceId(sourceInstanceId, out _, out ItemStorageEntry sourceEntry) ||
                sourceEntry == null || sourceEntry.Item == null)
            {
                return GridStorageMergePreview.Invalid(
                    InventoryMutationResult.MutationFailure.SourceNotFound,
                    $"Source item instance '{sourceInstanceId}' was not found.",
                    sourceInstanceId,
                    destinationInstanceId);
            }

            if (!CanAcceptIncoming(destination, sourceEntry, sourceEntry.Quantity, out string guardReason))
            {
                return GridStorageMergePreview.Invalid(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    guardReason,
                    sourceInstanceId,
                    destinationInstanceId);
            }

            GridStorageMergePreview preview = sourceEndpoint.TransferBackend.PreviewMergeIntoTarget(
                destinationEndpoint.TransferBackend,
                sourceInstanceId,
                destinationInstanceId);
            if (!preview.IsValid)
                return preview;

            string definitionId = sourceEntry.DefinitionId;
            if (TryRejectIncomingWeight(
                    source,
                    destination,
                    sourceEntry,
                    preview.TransferQuantity,
                    out CarryWeightAcceptance acceptance))
            {
                return GridStorageMergePreview.Invalid(
                    InventoryMutationResult.MutationFailure.CarryWeightLimitExceeded,
                    GetCarryWeightRejectionMessage(acceptance),
                    sourceInstanceId,
                    destinationInstanceId);
            }

            return preview;
        }

        public static InventoryMutationResult MergeIntoTarget(
            IGridStorageOwner source,
            string sourceInstanceId,
            IGridStorageOwner destination,
            string destinationInstanceId,
            GridStorageTransferContext context)
        {
            if (ReferenceEquals(source, destination) || sourceInstanceId == destinationInstanceId)
            {
                return InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    "Directed merge requires different owners and instance ids.",
                    0,
                    sourceInstanceId);
            }

            if (!TryResolveEndpoints(source, destination, context,
                    out IGridStorageTransferEndpoint sourceEndpoint,
                    out IGridStorageTransferEndpoint destinationEndpoint,
                    out string error))
            {
                return InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    error,
                    0,
                    sourceInstanceId);
            }

            if (!source.TryGetEntryByInstanceId(sourceInstanceId, out _, out ItemStorageEntry sourceEntry) ||
                sourceEntry == null || sourceEntry.Item == null)
            {
                return InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.SourceNotFound,
                    $"Source item instance '{sourceInstanceId}' was not found.",
                    0,
                    sourceInstanceId);
            }

            if (!CanAcceptIncoming(destination, sourceEntry, sourceEntry.Quantity, out string guardReason))
            {
                return InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    guardReason,
                    sourceEntry.Quantity,
                    sourceInstanceId);
            }

            GridStorageMergePreview currentPreview = sourceEndpoint.TransferBackend.PreviewMergeIntoTarget(
                destinationEndpoint.TransferBackend,
                sourceInstanceId,
                destinationInstanceId);
            if (!currentPreview.IsValid)
            {
                return InventoryMutationResult.Rejected(
                    currentPreview.Failure,
                    currentPreview.Message,
                    0,
                    sourceInstanceId);
            }

            string definitionId = sourceEntry.DefinitionId;
            if (TryRejectIncomingWeight(
                    source,
                    destination,
                    sourceEntry,
                    currentPreview.TransferQuantity,
                    out CarryWeightAcceptance acceptance))
            {
                return InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.CarryWeightLimitExceeded,
                    GetCarryWeightRejectionMessage(acceptance),
                    currentPreview.TransferQuantity,
                    sourceInstanceId);
            }

            InventoryMutationResult result = sourceEndpoint.TransferBackend.MergeIntoTarget(
                destinationEndpoint.TransferBackend,
                sourceInstanceId,
                destinationInstanceId);

            if (result.Success)
            {
                NotifyCommitted(
                    source,
                    destination,
                    sourceEndpoint,
                    destinationEndpoint,
                    new GridStorageTransferReceipt(definitionId, result),
                    context);
            }

            return result;
        }

        public static GridPlacementValidationResult PreviewTransferExact(
            IGridStorageOwner source,
            IGridStorageOwner target,
            string sourceInstanceId,
            int targetX,
            int targetY,
            bool isRotated,
            GridStorageTransferContext context)
        {
            if (!TryResolveEndpoints(source, target, context, out IGridStorageTransferEndpoint sourceEndpoint,
                    out IGridStorageTransferEndpoint targetEndpoint, out string error))
            {
                return GridPlacementValidationResult.Invalid(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    error);
            }

            if (!source.TryGetEntryByInstanceId(sourceInstanceId, out _, out ItemStorageEntry sourceEntry) ||
                sourceEntry == null || sourceEntry.Item == null)
            {
                return GridPlacementValidationResult.Invalid(
                    InventoryMutationResult.MutationFailure.SourceNotFound,
                    $"Source item instance '{sourceInstanceId}' was not found.");
            }

            if (!CanAcceptIncoming(target, sourceEntry, sourceEntry.Quantity, out string guardReason))
            {
                return GridPlacementValidationResult.Invalid(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    guardReason);
            }

            GridPlacementValidationResult preview = sourceEndpoint.TransferBackend.PreviewTransferToExact(
                targetEndpoint.TransferBackend,
                sourceInstanceId,
                targetX,
                targetY,
                isRotated);
            if (!preview.IsValid)
                return preview;

            if (TryRejectIncomingWeight(
                    source,
                    target,
                    sourceEntry,
                    sourceEntry.Quantity,
                    out CarryWeightAcceptance acceptance))
            {
                return GridPlacementValidationResult.Invalid(
                    InventoryMutationResult.MutationFailure.CarryWeightLimitExceeded,
                    GetCarryWeightRejectionMessage(acceptance));
            }

            return preview;
        }

        public static InventoryMutationResult TransferExact(
            IGridStorageOwner source,
            IGridStorageOwner target,
            string sourceInstanceId,
            int targetX,
            int targetY,
            bool isRotated,
            GridStorageTransferContext context)
        {
            if (!TryResolveEndpoints(source, target, context, out IGridStorageTransferEndpoint sourceEndpoint,
                    out IGridStorageTransferEndpoint targetEndpoint, out string error))
            {
                return InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    error,
                    0,
                    sourceInstanceId);
            }

            if (!source.TryGetEntryByInstanceId(sourceInstanceId, out _, out ItemStorageEntry sourceEntry) ||
                sourceEntry == null || sourceEntry.Item == null)
            {
                return InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.SourceNotFound,
                    $"Source item instance '{sourceInstanceId}' was not found.",
                    0,
                    sourceInstanceId);
            }

            if (!CanAcceptIncoming(target, sourceEntry, sourceEntry.Quantity, out string guardReason))
            {
                return InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    guardReason,
                    sourceEntry.Quantity,
                    sourceInstanceId);
            }

            GridPlacementValidationResult currentPreview = sourceEndpoint.TransferBackend.PreviewTransferToExact(
                targetEndpoint.TransferBackend,
                sourceInstanceId,
                targetX,
                targetY,
                isRotated);
            if (!currentPreview.IsValid)
            {
                return InventoryMutationResult.Rejected(
                    currentPreview.Failure,
                    currentPreview.Message,
                    0,
                    sourceInstanceId);
            }

            string definitionId = sourceEntry.DefinitionId;
            if (TryRejectIncomingWeight(
                    source,
                    target,
                    sourceEntry,
                    sourceEntry.Quantity,
                    out CarryWeightAcceptance acceptance))
            {
                return InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.CarryWeightLimitExceeded,
                    GetCarryWeightRejectionMessage(acceptance),
                    sourceEntry.Quantity,
                    sourceInstanceId);
            }

            InventoryMutationResult result = sourceEndpoint.TransferBackend.TransferToExact(
                targetEndpoint.TransferBackend,
                sourceInstanceId,
                targetX,
                targetY,
                isRotated);

            if (result.Success)
                NotifyCommitted(source, target, sourceEndpoint, targetEndpoint, new GridStorageTransferReceipt(definitionId, result), context);

            return result;
        }

        public static InventoryMutationResult TransferStackAuto(
            IGridStorageOwner source,
            IGridStorageOwner target,
            string sourceInstanceId,
            GridStorageTransferContext context)
        {
            return TransferStackAuto(
                source,
                target,
                sourceInstanceId,
                GridStorageTransferQuantityPolicy.Exact,
                context);
        }

        public static InventoryMutationResult TransferStackAuto(
            IGridStorageOwner source,
            IGridStorageOwner target,
            string sourceInstanceId,
            GridStorageTransferQuantityPolicy quantityPolicy,
            GridStorageTransferContext context)
        {
            if (source == null || !source.TryGetEntryByInstanceId(sourceInstanceId, out _, out ItemStorageEntry entry) ||
                entry == null || entry.Item == null)
            {
                return InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.SourceNotFound,
                    $"Source item instance '{sourceInstanceId}' was not found.",
                    0,
                    sourceInstanceId);
            }

            return TransferQuantityAuto(source, target, sourceInstanceId, entry.Quantity, true, quantityPolicy, context);
        }

        public static InventoryMutationResult TransferQuantityAuto(
            IGridStorageOwner source,
            IGridStorageOwner target,
            string sourceInstanceId,
            int quantity,
            bool requireExactQuantity,
            GridStorageTransferContext context)
        {
            return TransferQuantityAuto(
                source,
                target,
                sourceInstanceId,
                quantity,
                requireExactQuantity,
                GridStorageTransferQuantityPolicy.Exact,
                context);
        }

        public static GridStorageAutoTransferPreview PreviewTransferQuantityAuto(
            IGridStorageOwner source,
            IGridStorageOwner target,
            string sourceInstanceId,
            int requestedQuantity,
            GridStorageTransferQuantityPolicy quantityPolicy,
            GridStorageTransferContext context)
        {
            if (requestedQuantity < 1)
            {
                return GridStorageAutoTransferPreview.Invalid(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    "Transfer quantity must be at least 1.",
                    null,
                    sourceInstanceId,
                    requestedQuantity,
                    0,
                    -1,
                    false);
            }

            if (quantityPolicy != GridStorageTransferQuantityPolicy.Exact &&
                quantityPolicy != GridStorageTransferQuantityPolicy.ClampIncomingToActorHardLimit)
            {
                return GridStorageAutoTransferPreview.Invalid(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    $"Unsupported transfer quantity policy '{quantityPolicy}'.",
                    null,
                    sourceInstanceId,
                    requestedQuantity,
                    0,
                    -1,
                    false);
            }

            if (!TryResolveEndpoints(source, target, context,
                    out IGridStorageTransferEndpoint sourceEndpoint,
                    out IGridStorageTransferEndpoint targetEndpoint,
                    out string error))
            {
                return GridStorageAutoTransferPreview.Invalid(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    error,
                    null,
                    sourceInstanceId,
                    requestedQuantity,
                    0,
                    -1,
                    false);
            }

            if (!source.TryGetEntryByInstanceId(sourceInstanceId, out _, out ItemStorageEntry sourceEntry) ||
                sourceEntry == null || sourceEntry.Item == null)
            {
                return GridStorageAutoTransferPreview.Invalid(
                    InventoryMutationResult.MutationFailure.SourceNotFound,
                    $"Source item instance '{sourceInstanceId}' was not found.",
                    null,
                    sourceInstanceId,
                    requestedQuantity,
                    0,
                    -1,
                    false);
            }

            string definitionId = sourceEntry.DefinitionId;
            int sourceQuantity = sourceEntry.Quantity;
            if (quantityPolicy == GridStorageTransferQuantityPolicy.Exact && sourceQuantity < requestedQuantity)
            {
                return GridStorageAutoTransferPreview.Invalid(
                    InventoryMutationResult.MutationFailure.InsufficientQuantity,
                    $"Source item instance '{sourceInstanceId}' does not contain x{requestedQuantity}.",
                    definitionId,
                    sourceInstanceId,
                    requestedQuantity,
                    sourceQuantity,
                    -1,
                    false);
            }

            int effectiveQuantity = Math.Min(requestedQuantity, sourceQuantity);
            int weightLimitQuantity = -1;
            bool wasLimitedByWeight = false;
            if (!CanAcceptIncoming(target, sourceEntry, effectiveQuantity, out string guardReason))
            {
                return GridStorageAutoTransferPreview.Invalid(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    guardReason,
                    definitionId,
                    sourceInstanceId,
                    requestedQuantity,
                    sourceQuantity,
                    -1,
                    false);
            }

            bool sharesRootOwner = ItemOwnedStorageRegistry.Instance.ShareRootOwner(source, target);
            if (!sharesRootOwner && TryResolveCarryWeightOwner(target, out ICarryWeightLimitedOwner limitedOwner) && limitedOwner.HasCarryWeightLimit)
            {
                if (quantityPolicy == GridStorageTransferQuantityPolicy.ClampIncomingToActorHardLimit)
                {
                    CarryWeightQuantityLimit limit = limitedOwner.EvaluateIncomingEntryQuantityLimit(
                        sourceEntry,
                        effectiveQuantity);
                    if (!limit.IsValid)
                    {
                        return GridStorageAutoTransferPreview.Invalid(
                            InventoryMutationResult.MutationFailure.CarryWeightLimitExceeded,
                            "No se pudo calcular cuánto peso adicional podés cargar.",
                            definitionId,
                            sourceInstanceId,
                            requestedQuantity,
                            sourceQuantity,
                            0,
                            false);
                    }

                    weightLimitQuantity = limit.MaximumQuantity;
                    wasLimitedByWeight = limit.WasLimitedByWeight;
                    effectiveQuantity = Math.Min(effectiveQuantity, weightLimitQuantity);
                    if (effectiveQuantity < 1)
                    {
                        return GridStorageAutoTransferPreview.Invalid(
                            InventoryMutationResult.MutationFailure.CarryWeightLimitExceeded,
                            "No podés cargar ninguna unidad más por el límite de peso.",
                            definitionId,
                            sourceInstanceId,
                            requestedQuantity,
                            sourceQuantity,
                            weightLimitQuantity,
                            true);
                    }
                }
                else if (TryRejectIncomingWeight(
                             source,
                             target,
                             sourceEntry,
                             effectiveQuantity,
                             out _))
                {
                    return GridStorageAutoTransferPreview.Invalid(
                        InventoryMutationResult.MutationFailure.CarryWeightLimitExceeded,
                        "No podés cargar esa cantidad por el límite de peso.",
                        definitionId,
                        sourceInstanceId,
                        requestedQuantity,
                        sourceQuantity,
                        0,
                        true);
                }
            }

            GridPlacementValidationResult spatialPreview = sourceEndpoint.TransferBackend.PreviewTransferTo(
                targetEndpoint.TransferBackend,
                sourceInstanceId,
                effectiveQuantity);
            if (!spatialPreview.IsValid)
            {
                return GridStorageAutoTransferPreview.Invalid(
                    spatialPreview.Failure,
                    spatialPreview.Message,
                    definitionId,
                    sourceInstanceId,
                    requestedQuantity,
                    sourceQuantity,
                    weightLimitQuantity,
                    wasLimitedByWeight);
            }

            return GridStorageAutoTransferPreview.Valid(
                definitionId,
                sourceInstanceId,
                requestedQuantity,
                sourceQuantity,
                effectiveQuantity,
                weightLimitQuantity,
                wasLimitedByWeight);
        }

        public static InventoryMutationResult TransferQuantityAuto(
            IGridStorageOwner source,
            IGridStorageOwner target,
            string sourceInstanceId,
            int quantity,
            bool requireExactQuantity,
            GridStorageTransferQuantityPolicy quantityPolicy,
            GridStorageTransferContext context)
        {
            GridStorageAutoTransferPreview preview = PreviewTransferQuantityAuto(
                source,
                target,
                sourceInstanceId,
                quantity,
                quantityPolicy,
                context);
            if (!preview.IsValid)
            {
                return InventoryMutationResult.Rejected(
                        preview.Failure,
                        preview.Message,
                        quantity,
                        sourceInstanceId)
                    .WithTransferMetadata(
                        quantity,
                        preview.SourceQuantity,
                        preview.WasLimitedByWeight,
                        preview.WeightLimitQuantity);
            }

            if (!TryResolveEndpoints(source, target, context,
                    out IGridStorageTransferEndpoint sourceEndpoint,
                    out IGridStorageTransferEndpoint targetEndpoint,
                    out string error))
            {
                return InventoryMutationResult.Rejected(
                        InventoryMutationResult.MutationFailure.InvalidArguments,
                        error,
                        quantity,
                        sourceInstanceId)
                    .WithTransferMetadata(quantity, preview.SourceQuantity, false, preview.WeightLimitQuantity);
            }

            InventoryMutationResult result = sourceEndpoint.TransferBackend.TransferTo(
                targetEndpoint.TransferBackend,
                sourceInstanceId,
                preview.EffectiveQuantity);

            int sourceRemainingQuantity = result.Success
                ? Math.Max(0, preview.SourceQuantity - result.AffectedQuantity)
                : preview.SourceQuantity;
            result = result.WithTransferMetadata(
                quantity,
                sourceRemainingQuantity,
                preview.WasLimitedByWeight,
                preview.WeightLimitQuantity);

            if (requireExactQuantity && result.Success && result.AffectedQuantity != preview.EffectiveQuantity)
            {
                Debug.LogError(
                    $"[GridStorageTransferService] Backend returned partial success x{result.AffectedQuantity}, expected x{preview.EffectiveQuantity}.");
            }

            if (result.Success)
                NotifyCommitted(source, target, sourceEndpoint, targetEndpoint, new GridStorageTransferReceipt(preview.DefinitionId, result), context);

            return result;
        }

        private static bool TryResolveEndpoints(
            IGridStorageOwner source,
            IGridStorageOwner target,
            GridStorageTransferContext context,
            out IGridStorageTransferEndpoint sourceEndpoint,
            out IGridStorageTransferEndpoint targetEndpoint,
            out string error)
        {
            sourceEndpoint = source as IGridStorageTransferEndpoint;
            targetEndpoint = target as IGridStorageTransferEndpoint;
            error = null;

            if (source == null || target == null || ReferenceEquals(source, target) || sourceEndpoint == null || targetEndpoint == null)
            {
                error = "Valid distinct grid storage endpoints are required.";
                return false;
            }

            if (sourceEndpoint.TransferBackend == null || targetEndpoint.TransferBackend == null)
            {
                error = "Both grid storage endpoints must expose an initialized transactional backend.";
                return false;
            }

            if (!sourceEndpoint.CanTransferOut(context, out error))
                return false;

            if (!targetEndpoint.CanTransferIn(context, out error))
                return false;

            return true;
        }

        private static bool TryRejectIncomingWeight(
            IGridStorageOwner source,
            IGridStorageOwner destination,
            ItemStorageEntry entry,
            int quantity,
            out CarryWeightAcceptance acceptance)
        {
            acceptance = default;
            if (ItemOwnedStorageRegistry.Instance.ShareRootOwner(source, destination))
                return false;

            if (!TryResolveCarryWeightOwner(destination, out ICarryWeightLimitedOwner limitedOwner) || !limitedOwner.HasCarryWeightLimit)
                return false;

            acceptance = limitedOwner.EvaluateIncomingEntry(entry, quantity);
            return !acceptance.Accepted;
        }

        private static bool TryResolveCarryWeightOwner(
            IGridStorageOwner destination,
            out ICarryWeightLimitedOwner limitedOwner)
        {
            object rootOwner = ItemOwnedStorageRegistry.Instance.ResolveRootOwner(destination);
            limitedOwner = rootOwner as ICarryWeightLimitedOwner;
            return limitedOwner != null;
        }

        private static bool CanAcceptIncoming(
            IGridStorageOwner destination,
            ItemStorageEntry entry,
            int quantity,
            out string reason)
        {
            reason = null;
            return !(destination is IGridStorageIncomingGuard guard) ||
                   guard.CanAcceptIncoming(entry, quantity, out reason);
        }

        private static string GetCarryWeightRejectionMessage(CarryWeightAcceptance acceptance)
        {
            return acceptance.Accepted
                ? null
                : $"No podés cargar esa cantidad por el límite de peso " +
                  $"({acceptance.ProjectedWeightKg:0.00} / {acceptance.HardLimitKg:0.00} kg).";
        }

        private static void NotifyCommitted(
            IGridStorageOwner sourceOwner,
            IGridStorageOwner targetOwner,
            IGridStorageTransferEndpoint source,
            IGridStorageTransferEndpoint target,
            GridStorageTransferReceipt receipt,
            GridStorageTransferContext context)
        {
            ItemOwnedStorageRegistry.Instance.ReconcileCommittedTransfer(sourceOwner, targetOwner, receipt);
            TryNotify(() => source.OnTransferCommittedOut(receipt, context), "source out");
            TryNotify(() => target.OnTransferCommittedIn(receipt, context), "target in");
        }

        private static void TryNotify(Action notification, string label)
        {
            try
            {
                notification?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[GridStorageTransferService] Committed transfer hook '{label}' failed: " +
                    $"{exception.GetType().Name}: {exception.Message}");
            }
        }
    }
}
