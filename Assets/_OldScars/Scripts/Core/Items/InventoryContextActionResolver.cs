using System.Collections.Generic;
using OldScars.Core.Actors;
using OldScars.Core.Data.Definitions;

namespace OldScars.Core.Items
{
    public static class InventoryContextActionResolver
    {
        public static IReadOnlyList<InventoryContextAction> ResolvePersonal(
            InventoryComponent inventory,
            ActorEquipmentComponent equipment,
            string instanceId,
            bool hasExternalDestination)
        {
            var actions = new List<InventoryContextAction>();
            if (inventory == null ||
                !inventory.TryGetEntryByInstanceId(instanceId, out _, out ItemStorageEntry entry) ||
                entry?.Item == null)
            {
                return actions;
            }

            if (InventoryItemUseService.IsConsumable(entry))
                actions.Add(new InventoryContextAction(InventoryContextActionKind.Use, "Usar / Consumir"));

            AddEquipActions(actions, equipment, instanceId);

            if (hasExternalDestination)
            {
                actions.Add(new InventoryContextAction(InventoryContextActionKind.DepositOne, "Depositar 1"));
                if (entry.Quantity > 1)
                    actions.Add(new InventoryContextAction(InventoryContextActionKind.DepositAmount, "Depositar cantidad..."));
                actions.Add(new InventoryContextAction(InventoryContextActionKind.DepositStack, "Depositar todo"));
            }

            actions.Add(new InventoryContextAction(InventoryContextActionKind.DropOne, "Soltar 1"));
            if (entry.Quantity > 1)
                actions.Add(new InventoryContextAction(InventoryContextActionKind.DropAmount, "Soltar cantidad..."));
            actions.Add(new InventoryContextAction(InventoryContextActionKind.DropStack, "Soltar todo"));
            actions.Add(new InventoryContextAction(InventoryContextActionKind.ShowDetails, "Ver detalles"));
            return actions;
        }

        public static IReadOnlyList<InventoryContextAction> ResolveExternal(
            IGridStorageOwner externalOwner,
            string instanceId)
        {
            var actions = new List<InventoryContextAction>();
            if (externalOwner == null ||
                !externalOwner.TryGetEntryByInstanceId(instanceId, out _, out ItemStorageEntry entry) ||
                entry?.Item == null)
            {
                return actions;
            }

            actions.Add(new InventoryContextAction(InventoryContextActionKind.TakeOne, "Tomar 1"));
            if (entry.Quantity > 1)
                actions.Add(new InventoryContextAction(InventoryContextActionKind.TakeAmount, "Tomar cantidad..."));
            actions.Add(new InventoryContextAction(InventoryContextActionKind.TakeStack, "Tomar todo"));
            actions.Add(new InventoryContextAction(InventoryContextActionKind.ShowDetails, "Ver detalles"));
            return actions;
        }

        public static IReadOnlyList<InventoryContextAction> ResolveEquipment(
            ActorEquipmentComponent equipment,
            string equipmentSlotId,
            string instanceId)
        {
            var actions = new List<InventoryContextAction>();
            if (equipment == null || string.IsNullOrWhiteSpace(equipmentSlotId) ||
                !equipment.TryGetEntryByInstanceId(instanceId, out ItemStorageEntry entry) ||
                entry?.Item == null)
            {
                return actions;
            }

            EquipmentPreview preview = equipment.PreviewUnequip(instanceId);
            actions.Add(new InventoryContextAction(
                InventoryContextActionKind.Unequip,
                "Desequipar al inventario",
                preview.Success,
                preview.Success ? null : preview.Message));
            actions.Add(new InventoryContextAction(InventoryContextActionKind.ShowDetails, "Ver detalles"));
            return actions;
        }

        private static void AddEquipActions(
            List<InventoryContextAction> actions,
            ActorEquipmentComponent equipment,
            string instanceId)
        {
            if (equipment == null)
                return;

            IReadOnlyList<EquipmentSlotSet> alternatives = equipment.GetCompatibleSlotSets(instanceId);
            for (int index = 0; index < alternatives.Count; index++)
            {
                string[] slotIds = alternatives[index].SlotIds;
                EquipmentPreview preview = equipment.PreviewEquip(instanceId, slotIds);
                actions.Add(new InventoryContextAction(
                    InventoryContextActionKind.Equip,
                    $"Equipar - {GetSlotSetLabel(equipment, slotIds)}",
                    preview.Success && !preview.RequiresChoice,
                    preview.Success ? null : preview.Message,
                    slotIds));
            }
        }

        private static string GetSlotSetLabel(
            ActorEquipmentComponent equipment,
            IReadOnlyList<string> slotIds)
        {
            if (slotIds == null || slotIds.Count == 0)
                return "(none)";

            bool left = false;
            bool right = false;
            for (int index = 0; index < slotIds.Count; index++)
            {
                left |= slotIds[index] == ActorEquipmentComponent.HandLeftSlotId;
                right |= slotIds[index] == ActorEquipmentComponent.HandRightSlotId;
            }
            if (slotIds.Count == 2 && left && right)
                return "Ambas manos";

            var labels = new string[slotIds.Count];
            for (int index = 0; index < slotIds.Count; index++)
            {
                EquipmentSlotDefinition definition = equipment.GetSlotDefinition(slotIds[index]);
                labels[index] = definition != null && !string.IsNullOrWhiteSpace(definition.display_name)
                    ? definition.display_name
                    : slotIds[index];
            }
            return string.Join(" + ", labels);
        }
    }
}
