using System;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Interactions;
using UnityEngine;

namespace OldScars.Core.Items
{
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

            return sourceEndpoint.TransferBackend.PreviewMergeIntoTarget(
                destinationEndpoint.TransferBackend,
                sourceInstanceId,
                destinationInstanceId);
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

            string definitionId = ResolveDefinitionId(source, sourceInstanceId);
            InventoryMutationResult result = sourceEndpoint.TransferBackend.MergeIntoTarget(
                destinationEndpoint.TransferBackend,
                sourceInstanceId,
                destinationInstanceId);

            if (result.Success)
            {
                NotifyCommitted(
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

            return sourceEndpoint.TransferBackend.PreviewTransferToExact(
                targetEndpoint.TransferBackend,
                sourceInstanceId,
                targetX,
                targetY,
                isRotated);
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

            string definitionId = ResolveDefinitionId(source, sourceInstanceId);
            InventoryMutationResult result = sourceEndpoint.TransferBackend.TransferToExact(
                targetEndpoint.TransferBackend,
                sourceInstanceId,
                targetX,
                targetY,
                isRotated);

            if (result.Success)
                NotifyCommitted(sourceEndpoint, targetEndpoint, new GridStorageTransferReceipt(definitionId, result), context);

            return result;
        }

        public static InventoryMutationResult TransferStackAuto(
            IGridStorageOwner source,
            IGridStorageOwner target,
            string sourceInstanceId,
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

            return TransferQuantityAuto(source, target, sourceInstanceId, entry.Quantity, true, context);
        }

        public static InventoryMutationResult TransferQuantityAuto(
            IGridStorageOwner source,
            IGridStorageOwner target,
            string sourceInstanceId,
            int quantity,
            bool requireExactQuantity,
            GridStorageTransferContext context)
        {
            if (quantity < 1)
            {
                return InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    "Transfer quantity must be at least 1.",
                    quantity,
                    sourceInstanceId);
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
                    sourceInstanceId);
            }

            if (!source.TryGetEntryByInstanceId(sourceInstanceId, out _, out ItemStorageEntry sourceEntry) ||
                sourceEntry == null || sourceEntry.Item == null || sourceEntry.Quantity < quantity)
            {
                return InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.InsufficientQuantity,
                    $"Source item instance '{sourceInstanceId}' does not contain x{quantity}.",
                    quantity,
                    sourceInstanceId);
            }

            string definitionId = sourceEntry.DefinitionId;
            InventoryMutationResult result = sourceEndpoint.TransferBackend.TransferTo(
                targetEndpoint.TransferBackend,
                sourceInstanceId,
                quantity);

            if (requireExactQuantity && result.Success && result.AffectedQuantity != quantity)
            {
                Debug.LogError(
                    $"[GridStorageTransferService] Backend returned partial success x{result.AffectedQuantity}, expected x{quantity}.");
            }

            if (result.Success)
                NotifyCommitted(sourceEndpoint, targetEndpoint, new GridStorageTransferReceipt(definitionId, result), context);

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

        private static string ResolveDefinitionId(IGridStorageOwner source, string instanceId)
        {
            return source != null && source.TryGetEntryByInstanceId(instanceId, out _, out ItemStorageEntry entry)
                ? entry?.DefinitionId
                : null;
        }

        private static void NotifyCommitted(
            IGridStorageTransferEndpoint source,
            IGridStorageTransferEndpoint target,
            GridStorageTransferReceipt receipt,
            GridStorageTransferContext context)
        {
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
