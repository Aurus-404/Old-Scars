using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OldScars.Core.Actors
{
    public enum EquipmentVisualCommitKind
    {
        Equip,
        Unequip,
        Replacement,
        EquipFromItemOwnedStorage,
        ReplacementFromItemOwnedStorage,
        LegacyMigration
    }

    public sealed class EquipmentVisualItemSnapshot
    {
        private readonly ReadOnlyCollection<string> occupiedSlots;

        public EquipmentVisualItemSnapshot(string instanceId, string definitionId, IReadOnlyList<string> slots)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;

            var copy = new string[slots != null ? slots.Count : 0];
            for (int index = 0; index < copy.Length; index++)
                copy[index] = slots[index];
            occupiedSlots = Array.AsReadOnly(copy);
        }

        public string InstanceId { get; }
        public string DefinitionId { get; }
        public IReadOnlyList<string> OccupiedSlots => occupiedSlots;
    }

    public sealed class EquipmentVisualStateSnapshot
    {
        private readonly ReadOnlyCollection<EquipmentVisualItemSnapshot> equippedItems;

        public EquipmentVisualStateSnapshot(
            long committedRevision,
            int equipmentVersion,
            int storageVersion,
            string equipmentLayoutId,
            IReadOnlyList<EquipmentVisualItemSnapshot> items)
        {
            CommittedRevision = committedRevision;
            EquipmentVersion = equipmentVersion;
            StorageVersion = storageVersion;
            EquipmentLayoutId = equipmentLayoutId;

            var copy = new EquipmentVisualItemSnapshot[items != null ? items.Count : 0];
            for (int index = 0; index < copy.Length; index++)
                copy[index] = items[index];
            equippedItems = Array.AsReadOnly(copy);
        }

        public long CommittedRevision { get; }
        public int EquipmentVersion { get; }
        public int StorageVersion { get; }
        public string EquipmentLayoutId { get; }
        public IReadOnlyList<EquipmentVisualItemSnapshot> EquippedItems => equippedItems;
    }

    public sealed class EquipmentVisualStateCommittedEventArgs : EventArgs
    {
        public EquipmentVisualStateCommittedEventArgs(
            EquipmentVisualCommitKind commitKind,
            EquipmentVisualStateSnapshot snapshot)
        {
            CommitKind = commitKind;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public EquipmentVisualCommitKind CommitKind { get; }
        public EquipmentVisualStateSnapshot Snapshot { get; }
    }

    public interface IEquipmentVisualSource
    {
        event EventHandler<EquipmentVisualStateCommittedEventArgs> VisualStateCommitted;

        EquipmentVisualStateSnapshot CaptureVisualSnapshot();
    }
}
