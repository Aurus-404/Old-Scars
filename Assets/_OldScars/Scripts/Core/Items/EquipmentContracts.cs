using System;

namespace OldScars.Core.Items
{
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
            string message,
            string instanceId,
            string[] slotIds,
            GridPlacement destinationPlacement,
            int personalStorageVersion,
            int personalLayoutVersion,
            int equipmentStorageVersion,
            int equipmentVersion)
        {
            Success = success;
            RequiresChoice = requiresChoice;
            Message = message;
            InstanceId = instanceId;
            SlotIds = slotIds != null ? (string[])slotIds.Clone() : Array.Empty<string>();
            DestinationPlacement = destinationPlacement;
            PersonalStorageVersion = personalStorageVersion;
            PersonalLayoutVersion = personalLayoutVersion;
            EquipmentStorageVersion = equipmentStorageVersion;
            EquipmentVersion = equipmentVersion;
        }

        public bool Success { get; }
        public bool RequiresChoice { get; }
        public string Message { get; }
        public string InstanceId { get; }
        public string[] SlotIds { get; }
        public GridPlacement DestinationPlacement { get; }
        internal int PersonalStorageVersion { get; }
        internal int PersonalLayoutVersion { get; }
        internal int EquipmentStorageVersion { get; }
        internal int EquipmentVersion { get; }
    }

    public readonly struct EquipmentMutationResult
    {
        public EquipmentMutationResult(bool success, string message, string instanceId, string[] slotIds)
        {
            Success = success;
            Message = message;
            InstanceId = instanceId;
            SlotIds = slotIds != null ? (string[])slotIds.Clone() : Array.Empty<string>();
        }

        public bool Success { get; }
        public string Message { get; }
        public string InstanceId { get; }
        public string[] SlotIds { get; }

        public static EquipmentMutationResult Rejected(string message, string instanceId = null)
        {
            return new EquipmentMutationResult(false, message, instanceId, null);
        }
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
