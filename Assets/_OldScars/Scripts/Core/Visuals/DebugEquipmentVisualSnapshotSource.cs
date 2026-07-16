using System;
using System.Collections.Generic;
using OldScars.Core.Actors;
using UnityEngine;

namespace OldScars.Core.Visuals
{
    [DisallowMultipleComponent]
    public sealed class DebugEquipmentVisualSnapshotSource : MonoBehaviour, IEquipmentVisualSource
    {
        [SerializeField] private string equipmentLayoutId = "debug_cargo_visual_layout";
        [SerializeField] private string instanceId = "debug_visual_backpack_0001";
        [SerializeField] private string definitionId = "small_backpack_01";
        [SerializeField] private string[] occupiedSlots = { "back" };
        [SerializeField] private bool includeItem = true;

        private long committedRevision;
        private int equipmentVersion;
        private int storageVersion;

        public event EventHandler<EquipmentVisualStateCommittedEventArgs> VisualStateCommitted;

        public EquipmentVisualStateSnapshot CaptureVisualSnapshot()
        {
            var items = new List<EquipmentVisualItemSnapshot>();
            if (includeItem)
            {
                items.Add(new EquipmentVisualItemSnapshot(
                    instanceId,
                    definitionId,
                    occupiedSlots ?? Array.Empty<string>()));
            }
            return new EquipmentVisualStateSnapshot(
                committedRevision,
                equipmentVersion,
                storageVersion,
                equipmentLayoutId,
                items);
        }

        [ContextMenu("Publish Backpack Equipped Snapshot")]
        public void PublishBackpackEquipped()
        {
            includeItem = true;
            Publish(EquipmentVisualCommitKind.Equip);
        }

        [ContextMenu("Publish Empty Snapshot")]
        public void PublishEmpty()
        {
            includeItem = false;
            Publish(EquipmentVisualCommitKind.Unequip);
        }

        private void Publish(EquipmentVisualCommitKind kind)
        {
            equipmentVersion++;
            storageVersion++;
            committedRevision++;
            EquipmentVisualStateSnapshot snapshot = CaptureVisualSnapshot();
            VisualStateCommitted?.Invoke(this, new EquipmentVisualStateCommittedEventArgs(kind, snapshot));
        }
    }
}
