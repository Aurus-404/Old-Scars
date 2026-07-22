using System;

namespace OldScars.Core.Items
{
    public sealed class InventoryMutationResult
    {
        public enum MutationStatus
        {
            Succeeded,
            Rejected,
            RolledBack
        }

        public enum MutationFailure
        {
            None,
            InvalidArguments,
            SourceNotFound,
            InsufficientQuantity,
            ItemDefinitionNotFound,
            InvalidFootprint,
            GridLayoutUnavailable,
            NoGridSpace,
            PlacementConflict,
            ExactTransferRequiresFullStack,
            ExactTransferWouldMerge,
            IncompatibleStack,
            StackFull,
            CarryWeightLimitExceeded,
            StalePlan,
            CommitFailed,
            OwnedStorageNotEmpty
        }

        public MutationStatus Status { get; }
        public MutationFailure Failure { get; }
        public string Message { get; }
        public int RequestedQuantity { get; }
        public int AffectedQuantity { get; }
        public int ActualTransferredQuantity => AffectedQuantity;
        public int SourceRemainingQuantity { get; }
        public bool WasLimitedByWeight { get; }
        public int WeightLimitQuantity { get; }
        public string SourceInstanceId { get; }
        public string DestinationInstanceId { get; }
        public int MergedQuantity { get; }
        public string[] CreatedInstanceIds { get; }
        public string[] RemovedInstanceIds { get; }
        public GridPlacement[] AddedPlacements { get; }
        public GridPlacement[] UpdatedPlacements { get; }
        public string[] RemovedPlacementInstanceIds { get; }
        public bool UsedFallbackFootprint { get; }
        public bool Success => Status == MutationStatus.Succeeded;

        private InventoryMutationResult(
            MutationStatus status,
            MutationFailure failure,
            string message,
            int requestedQuantity,
            int affectedQuantity,
            string sourceInstanceId,
            string destinationInstanceId,
            int mergedQuantity,
            string[] createdInstanceIds,
            string[] removedInstanceIds,
            GridPlacement[] addedPlacements,
            GridPlacement[] updatedPlacements,
            string[] removedPlacementInstanceIds,
            bool usedFallbackFootprint,
            int sourceRemainingQuantity = -1,
            bool wasLimitedByWeight = false,
            int weightLimitQuantity = -1)
        {
            Status = status;
            Failure = failure;
            Message = message;
            RequestedQuantity = requestedQuantity;
            AffectedQuantity = affectedQuantity;
            SourceInstanceId = sourceInstanceId;
            DestinationInstanceId = destinationInstanceId;
            MergedQuantity = mergedQuantity;
            CreatedInstanceIds = createdInstanceIds ?? Array.Empty<string>();
            RemovedInstanceIds = removedInstanceIds ?? Array.Empty<string>();
            AddedPlacements = addedPlacements ?? Array.Empty<GridPlacement>();
            UpdatedPlacements = updatedPlacements ?? Array.Empty<GridPlacement>();
            RemovedPlacementInstanceIds = removedPlacementInstanceIds ?? Array.Empty<string>();
            UsedFallbackFootprint = usedFallbackFootprint;
            SourceRemainingQuantity = sourceRemainingQuantity;
            WasLimitedByWeight = wasLimitedByWeight;
            WeightLimitQuantity = weightLimitQuantity;
        }

        internal static InventoryMutationResult Succeeded(
            int requestedQuantity,
            int affectedQuantity,
            string sourceInstanceId,
            string destinationInstanceId,
            int mergedQuantity,
            string[] createdInstanceIds,
            string[] removedInstanceIds,
            GridPlacement[] addedPlacements,
            string[] removedPlacementInstanceIds,
            bool usedFallbackFootprint)
        {
            return new InventoryMutationResult(
                MutationStatus.Succeeded,
                MutationFailure.None,
                null,
                requestedQuantity,
                affectedQuantity,
                sourceInstanceId,
                destinationInstanceId,
                mergedQuantity,
                createdInstanceIds,
                removedInstanceIds,
                addedPlacements,
                null,
                removedPlacementInstanceIds,
                usedFallbackFootprint);
        }

        internal static InventoryMutationResult SucceededPlacementMove(
            string instanceId,
            GridPlacement placement,
            bool usedFallbackFootprint)
        {
            return new InventoryMutationResult(
                MutationStatus.Succeeded,
                MutationFailure.None,
                null,
                0,
                0,
                instanceId,
                instanceId,
                0,
                null,
                null,
                null,
                placement != null ? new[] { placement } : null,
                null,
                usedFallbackFootprint);
        }

        internal static InventoryMutationResult Rejected(MutationFailure failure, string message, int requestedQuantity, string sourceInstanceId = null)
        {
            return new InventoryMutationResult(
                MutationStatus.Rejected,
                failure,
                message,
                requestedQuantity,
                0,
                sourceInstanceId,
                null,
                0,
                null,
                null,
                null,
                null,
                null,
                false);
        }

        internal static InventoryMutationResult RolledBack(string message, int requestedQuantity, string sourceInstanceId, bool usedFallbackFootprint)
        {
            return new InventoryMutationResult(
                MutationStatus.RolledBack,
                MutationFailure.CommitFailed,
                message,
                requestedQuantity,
                0,
                sourceInstanceId,
                null,
                0,
                null,
                null,
                null,
                null,
                null,
                usedFallbackFootprint);
        }

        internal InventoryMutationResult WithTransferMetadata(
            int requestedQuantity,
            int sourceRemainingQuantity,
            bool wasLimitedByWeight,
            int weightLimitQuantity)
        {
            return new InventoryMutationResult(
                Status,
                Failure,
                Message,
                requestedQuantity,
                AffectedQuantity,
                SourceInstanceId,
                DestinationInstanceId,
                MergedQuantity,
                CreatedInstanceIds,
                RemovedInstanceIds,
                AddedPlacements,
                UpdatedPlacements,
                RemovedPlacementInstanceIds,
                UsedFallbackFootprint,
                sourceRemainingQuantity,
                wasLimitedByWeight,
                weightLimitQuantity);
        }
    }
}
