using UnityEngine;

namespace OldScars.Core.Items
{
    public enum InventoryUIActiveSide
    {
        None,
        Personal,
        Equipment,
        External
    }

    public sealed class InventoryUISessionSelection
    {
        public string SelectedEquipmentSlotId { get; private set; }
        public string SelectedEquippedInstanceId { get; private set; }
        public string SelectedPersonalItemInstanceId { get; private set; }
        public string SelectedExternalItemInstanceId { get; private set; }
        public Vector2 EquipmentScrollPosition { get; set; }
        public InventoryUIActiveSide ActiveSide { get; private set; }
        public string PendingEquipmentAutoScrollSlotId { get; private set; }

        public void SelectPersonal(string instanceId)
        {
            SelectedPersonalItemInstanceId = instanceId;
            ActiveSide = InventoryUIActiveSide.Personal;
        }

        public void SelectExternal(string instanceId)
        {
            SelectedExternalItemInstanceId = instanceId;
            ActiveSide = InventoryUIActiveSide.External;
        }

        public void SelectPersonalFromContext(string instanceId)
        {
            SelectedExternalItemInstanceId = null;
            SelectedEquipmentSlotId = null;
            SelectedEquippedInstanceId = null;
            PendingEquipmentAutoScrollSlotId = null;
            SelectPersonal(instanceId);
        }

        public void SelectExternalFromContext(string instanceId)
        {
            SelectedPersonalItemInstanceId = null;
            SelectedEquipmentSlotId = null;
            SelectedEquippedInstanceId = null;
            PendingEquipmentAutoScrollSlotId = null;
            SelectExternal(instanceId);
        }

        public void SelectEquipmentFromContext(string slotId, string instanceId, bool autoScroll = false)
        {
            SelectedPersonalItemInstanceId = null;
            SelectedExternalItemInstanceId = null;
            SelectEquipment(slotId, instanceId, autoScroll);
        }

        public void SelectEquipment(string slotId, string instanceId, bool autoScroll)
        {
            SelectedEquipmentSlotId = slotId;
            SelectedEquippedInstanceId = instanceId;
            ActiveSide = InventoryUIActiveSide.Equipment;
            if (autoScroll)
                PendingEquipmentAutoScrollSlotId = slotId;
        }

        public bool TryConsumeEquipmentAutoScroll(out string slotId)
        {
            slotId = PendingEquipmentAutoScrollSlotId;
            PendingEquipmentAutoScrollSlotId = null;
            return !string.IsNullOrWhiteSpace(slotId);
        }

        public void ClearPersonalIfMissing(string instanceId)
        {
            if (!string.IsNullOrWhiteSpace(SelectedPersonalItemInstanceId) &&
                SelectedPersonalItemInstanceId == instanceId)
            {
                SelectedPersonalItemInstanceId = null;
                if (ActiveSide == InventoryUIActiveSide.Personal)
                    ActiveSide = InventoryUIActiveSide.None;
            }
        }

        public void ClearExternalIfMissing(string instanceId)
        {
            if (!string.IsNullOrWhiteSpace(SelectedExternalItemInstanceId) &&
                SelectedExternalItemInstanceId == instanceId)
            {
                SelectedExternalItemInstanceId = null;
                if (ActiveSide == InventoryUIActiveSide.External)
                    ActiveSide = InventoryUIActiveSide.None;
            }
        }

        public void ClearEquipment()
        {
            SelectedEquipmentSlotId = null;
            SelectedEquippedInstanceId = null;
            if (ActiveSide == InventoryUIActiveSide.Equipment)
                ActiveSide = InventoryUIActiveSide.None;
        }

        public void ResetTransient()
        {
            SelectedPersonalItemInstanceId = null;
            SelectedExternalItemInstanceId = null;
            SelectedEquipmentSlotId = null;
            SelectedEquippedInstanceId = null;
            PendingEquipmentAutoScrollSlotId = null;
            ActiveSide = InventoryUIActiveSide.None;
        }
    }
}
