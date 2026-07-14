using System;

namespace OldScars.Core.Items
{
    public enum EquipmentFailureCode
    {
        None,
        InvalidPreview,
        MissingDependencies,
        LayoutUnavailable,
        SourceNotFound,
        InvalidQuantity,
        NotEquipable,
        InvalidSlotSet,
        SlotOccupied,
        NoPersonalInventorySpace,
        StaleState,
        OwnershipChanged,
        StorageMutationFailed,
        InternalFailure
    }

    public enum ActorItemStorageNodeKind
    {
        Personal,
        Equipment
    }

    public readonly struct EquipmentSlotSet
    {
        public EquipmentSlotSet(string[] slotIds)
        {
            SlotIds = slotIds != null ? (string[])slotIds.Clone() : Array.Empty<string>();
        }

        public string[] SlotIds { get; }
    }

    public sealed class EquipmentPreview
    {
        internal EquipmentPreview(
            bool success,
            bool requiresChoice,
            EquipmentFailureCode failureCode,
            string message,
            string instanceId,
            string[] slotIds,
            GridPlacement destinationPlacement,
            int personalStorageVersion,
            int personalLayoutVersion,
            int equipmentStorageVersion,
            int equipmentVersion,
            string sourceContainerInstanceId = null,
            int sourceStorageVersion = 0,
            int sourceLayoutVersion = 0,
            GridPlacement sourcePlacement = null)
        {
            Success = success;
            RequiresChoice = requiresChoice;
            FailureCode = failureCode;
            Message = message;
            InstanceId = instanceId;
            SlotIds = slotIds != null ? (string[])slotIds.Clone() : Array.Empty<string>();
            DestinationPlacement = destinationPlacement;
            PersonalStorageVersion = personalStorageVersion;
            PersonalLayoutVersion = personalLayoutVersion;
            EquipmentStorageVersion = equipmentStorageVersion;
            EquipmentVersion = equipmentVersion;
            SourceContainerInstanceId = sourceContainerInstanceId;
            SourceStorageVersion = sourceStorageVersion;
            SourceLayoutVersion = sourceLayoutVersion;
            SourcePlacement = sourcePlacement;
        }

        public bool Success { get; }
        public bool RequiresChoice { get; }
        public EquipmentFailureCode FailureCode { get; }
        public string Message { get; }
        public string InstanceId { get; }
        public string[] SlotIds { get; }
        public GridPlacement DestinationPlacement { get; }
        internal int PersonalStorageVersion { get; }
        internal int PersonalLayoutVersion { get; }
        internal int EquipmentStorageVersion { get; }
        internal int EquipmentVersion { get; }
        internal string SourceContainerInstanceId { get; }
        internal int SourceStorageVersion { get; }
        internal int SourceLayoutVersion { get; }
        internal GridPlacement SourcePlacement { get; }
    }

    public readonly struct EquipmentMutationResult
    {
        public EquipmentMutationResult(
            bool success,
            EquipmentFailureCode failureCode,
            string message,
            string instanceId,
            string[] slotIds)
        {
            Success = success;
            FailureCode = failureCode;
            Message = message;
            InstanceId = instanceId;
            SlotIds = slotIds != null ? (string[])slotIds.Clone() : Array.Empty<string>();
        }

        public bool Success { get; }
        public EquipmentFailureCode FailureCode { get; }
        public string Message { get; }
        public string InstanceId { get; }
        public string[] SlotIds { get; }

        public static EquipmentMutationResult Rejected(
            string message,
            string instanceId = null,
            EquipmentFailureCode failureCode = EquipmentFailureCode.InternalFailure)
        {
            return new EquipmentMutationResult(false, failureCode, message, instanceId, null);
        }
    }

    public sealed class EquipmentDisplacementPlan
    {
        internal EquipmentDisplacementPlan(
            string instanceId,
            string[] releasedSlotIds,
            GridPlacement destinationPlacement)
        {
            InstanceId = instanceId;
            ReleasedSlotIds = releasedSlotIds != null
                ? (string[])releasedSlotIds.Clone()
                : Array.Empty<string>();
            DestinationPlacement = destinationPlacement;
        }

        public string InstanceId { get; }
        public string[] ReleasedSlotIds { get; }
        public GridPlacement DestinationPlacement { get; }
    }

    public sealed class EquipmentReplacementPlan
    {
        internal EquipmentReplacementPlan(
            bool success,
            EquipmentFailureCode failureCode,
            string message,
            string sourceInstanceId,
            string[] requestedSlotSet,
            EquipmentDisplacementPlan[] displacedItems,
            int personalStorageVersion,
            int personalLayoutVersion,
            int equipmentStorageVersion,
            int equipmentVersion,
            string sourceContainerInstanceId = null,
            int sourceStorageVersion = 0,
            int sourceLayoutVersion = 0,
            GridPlacement sourcePlacement = null)
        {
            Success = success;
            FailureCode = failureCode;
            Message = message;
            SourceInstanceId = sourceInstanceId;
            RequestedSlotSet = requestedSlotSet != null
                ? (string[])requestedSlotSet.Clone()
                : Array.Empty<string>();
            DisplacedItems = displacedItems != null
                ? (EquipmentDisplacementPlan[])displacedItems.Clone()
                : Array.Empty<EquipmentDisplacementPlan>();
            PersonalStorageVersion = personalStorageVersion;
            PersonalLayoutVersion = personalLayoutVersion;
            EquipmentStorageVersion = equipmentStorageVersion;
            EquipmentVersion = equipmentVersion;
            SourceContainerInstanceId = sourceContainerInstanceId;
            SourceStorageVersion = sourceStorageVersion;
            SourceLayoutVersion = sourceLayoutVersion;
            SourcePlacement = sourcePlacement;
        }

        public bool Success { get; }
        public EquipmentFailureCode FailureCode { get; }
        public string Message { get; }
        public string SourceInstanceId { get; }
        public string[] RequestedSlotSet { get; }
        public EquipmentDisplacementPlan[] DisplacedItems { get; }
        internal int PersonalStorageVersion { get; }
        internal int PersonalLayoutVersion { get; }
        internal int EquipmentStorageVersion { get; }
        internal int EquipmentVersion { get; }
        internal string SourceContainerInstanceId { get; }
        internal int SourceStorageVersion { get; }
        internal int SourceLayoutVersion { get; }
        internal GridPlacement SourcePlacement { get; }
    }

    public readonly struct ActorItemOwnershipSnapshot
    {
        public ActorItemOwnershipSnapshot(
            int personalStorageVersion,
            int personalLayoutVersion,
            int equipmentStorageVersion,
            int equipmentVersion,
            int instanceCount,
            bool isValid,
            string error)
        {
            PersonalStorageVersion = personalStorageVersion;
            PersonalLayoutVersion = personalLayoutVersion;
            EquipmentStorageVersion = equipmentStorageVersion;
            EquipmentVersion = equipmentVersion;
            InstanceCount = instanceCount;
            IsValid = isValid;
            Error = error;
        }

        public int PersonalStorageVersion { get; }
        public int PersonalLayoutVersion { get; }
        public int EquipmentStorageVersion { get; }
        public int EquipmentVersion { get; }
        public int InstanceCount { get; }
        public bool IsValid { get; }
        public string Error { get; }
    }
}
