using System.Collections.Generic;
using OldScars.Core.Actors;
using OldScars.Core.Data.Definitions;

namespace OldScars.Core.Items
{
    public static class InventoryContextActionResolver
    {
        public static IReadOnlyList<InventoryContextAction> ResolvePersonalCompartment(
            IGridStorageOwner sourceOwner,
            InventoryComponent inventory,
            ActorEquipmentComponent equipment,
            PersonalStorageNavigator navigator,
            string instanceId,
            bool hasExternalDestination)
        {
            var compartmentActions = new List<InventoryContextAction>();
            if (sourceOwner == null ||
                !sourceOwner.TryGetEntryByInstanceId(instanceId, out _, out ItemStorageEntry compartmentEntry) ||
                compartmentEntry?.Item == null)
            {
                return compartmentActions;
            }

            if (InventoryItemUseService.IsConsumable(compartmentEntry))
                compartmentActions.Add(new InventoryContextAction(InventoryContextActionKind.Use, "Usar / Consumir"));

            AddEquipActions(compartmentActions, equipment, sourceOwner, instanceId);

            if (ReferenceEquals(sourceOwner, inventory) && compartmentEntry.Item.HasOwnedStorage &&
                navigator != null && !navigator.IsEquippedOwnedStorage(instanceId))
            {
                compartmentActions.Add(new InventoryContextAction(
                    InventoryContextActionKind.ReviewOwnedStorage,
                    "Revisar contenedor"));
            }

            if (!ReferenceEquals(sourceOwner, inventory))
                AddMoveToPersonalActions(compartmentActions, compartmentEntry);

            IReadOnlyList<PersonalStorageOption> options = navigator?.GetOptions();
            if (!compartmentEntry.Item.HasOwnedStorage && options != null)
            {
                for (int index = 1; index < options.Count; index++)
                {
                    if (!ReferenceEquals(options[index].Owner, sourceOwner))
                        AddMoveToOwnedStorageActions(compartmentActions, compartmentEntry, options[index]);
                }
            }

            if (hasExternalDestination)
                AddDepositActions(compartmentActions, compartmentEntry);

            AddDropActions(compartmentActions, compartmentEntry);
            return compartmentActions;
        }

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

            AddEquipActions(actions, equipment, inventory, instanceId);

            if (hasExternalDestination)
            {
                if (entry.Quantity == 1)
                {
                    actions.Add(new InventoryContextAction(InventoryContextActionKind.DepositStack, "Depositar"));
                }
                else
                {
                    actions.Add(new InventoryContextAction(InventoryContextActionKind.DepositOne, "Depositar 1"));
                    actions.Add(new InventoryContextAction(InventoryContextActionKind.DepositAmount, "Depositar cantidad..."));
                    actions.Add(new InventoryContextAction(InventoryContextActionKind.DepositStack, "Depositar todo"));
                }
            }

            AddDropActions(actions, entry);
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

            if (entry.Quantity == 1)
            {
                actions.Add(new InventoryContextAction(InventoryContextActionKind.TakeStack, "Tomar"));
            }
            else
            {
                actions.Add(new InventoryContextAction(InventoryContextActionKind.TakeOne, "Tomar 1"));
                actions.Add(new InventoryContextAction(InventoryContextActionKind.TakeAmount, "Tomar cantidad..."));
                actions.Add(new InventoryContextAction(InventoryContextActionKind.TakeStack, "Tomar todo"));
            }
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
                preview.Success
                    ? null
                    : EquipmentFailureMessageFormatter.FormatFailure(
                        preview.FailureCode,
                        equipment,
                        preview.SlotIds,
                        new[] { instanceId })));
            return actions;
        }

        private static void AddEquipActions(
            List<InventoryContextAction> actions,
            ActorEquipmentComponent equipment,
            IGridStorageOwner sourceOwner,
            string instanceId)
        {
            if (equipment == null)
                return;

            IReadOnlyList<EquipmentSlotSet> alternatives = equipment.GetCompatibleSlotSets(sourceOwner, instanceId);
            for (int index = 0; index < alternatives.Count; index++)
            {
                string[] slotIds = alternatives[index].SlotIds;
                EquipmentPreview preview = equipment.PreviewEquip(sourceOwner, instanceId, slotIds);
                string slotLabel = GetSlotSetLabel(equipment, slotIds);
                if (preview.Success && !preview.RequiresChoice)
                {
                    actions.Add(new InventoryContextAction(
                        InventoryContextActionKind.Equip,
                        $"Equipar — {slotLabel}",
                        true,
                        null,
                        slotIds));
                    continue;
                }

                if (preview.FailureCode == EquipmentFailureCode.SlotOccupied)
                {
                    EquipmentReplacementPlan replacement = equipment.PreviewEquipReplacing(sourceOwner, instanceId, slotIds);
                    string[] displacedIds = GetDisplacedIds(replacement);
                    actions.Add(new InventoryContextAction(
                        InventoryContextActionKind.EquipReplacing,
                        $"Equipar y reemplazar — {slotLabel}",
                        replacement.Success,
                        replacement.Success
                            ? null
                            : EquipmentFailureMessageFormatter.FormatFailure(
                                replacement.FailureCode,
                                equipment,
                                slotIds,
                                displacedIds),
                        slotIds,
                        EquipmentFailureMessageFormatter.FormatReplacementSummary(equipment, replacement)));
                    continue;
                }

                actions.Add(new InventoryContextAction(
                    InventoryContextActionKind.Equip,
                    $"Equipar — {slotLabel}",
                    false,
                    EquipmentFailureMessageFormatter.FormatFailure(preview.FailureCode, equipment, slotIds),
                    slotIds));
            }
        }

        private static void AddDropActions(List<InventoryContextAction> actions, ItemStorageEntry entry)
        {
            if (entry.Quantity == 1)
            {
                actions.Add(new InventoryContextAction(InventoryContextActionKind.DropStack, "Soltar"));
                return;
            }

            actions.Add(new InventoryContextAction(InventoryContextActionKind.DropOne, "Soltar 1"));
            actions.Add(new InventoryContextAction(InventoryContextActionKind.DropAmount, "Soltar cantidad..."));
            actions.Add(new InventoryContextAction(InventoryContextActionKind.DropStack, "Soltar todo"));
        }

        private static void AddMoveToPersonalActions(List<InventoryContextAction> actions, ItemStorageEntry entry)
        {
            if (entry.Quantity == 1)
            {
                actions.Add(new InventoryContextAction(InventoryContextActionKind.MoveToPersonalStack, "Mover a Inventario personal"));
                return;
            }

            actions.Add(new InventoryContextAction(InventoryContextActionKind.MoveToPersonalOne, "Mover 1 a Inventario personal"));
            actions.Add(new InventoryContextAction(InventoryContextActionKind.MoveToPersonalAmount, "Mover cantidad a Inventario personal..."));
            actions.Add(new InventoryContextAction(InventoryContextActionKind.MoveToPersonalStack, "Mover todo a Inventario personal"));
        }

        private static void AddMoveToOwnedStorageActions(
            List<InventoryContextAction> actions,
            ItemStorageEntry entry,
            PersonalStorageOption option)
        {
            if (entry.Quantity == 1)
            {
                actions.Add(new InventoryContextAction(
                    InventoryContextActionKind.MoveToOwnedStorageStack,
                    $"Mover a {option.Label}",
                    targetContainerInstanceId: option.ContainerInstanceId));
                return;
            }

            actions.Add(new InventoryContextAction(
                InventoryContextActionKind.MoveToOwnedStorageOne,
                $"Mover 1 a {option.Label}",
                targetContainerInstanceId: option.ContainerInstanceId));
            actions.Add(new InventoryContextAction(
                InventoryContextActionKind.MoveToOwnedStorageAmount,
                $"Mover cantidad a {option.Label}...",
                targetContainerInstanceId: option.ContainerInstanceId));
            actions.Add(new InventoryContextAction(
                InventoryContextActionKind.MoveToOwnedStorageStack,
                $"Mover todo a {option.Label}",
                targetContainerInstanceId: option.ContainerInstanceId));
        }

        private static void AddDepositActions(List<InventoryContextAction> actions, ItemStorageEntry entry)
        {
            if (entry.Quantity == 1)
            {
                actions.Add(new InventoryContextAction(InventoryContextActionKind.DepositStack, "Depositar"));
                return;
            }

            actions.Add(new InventoryContextAction(InventoryContextActionKind.DepositOne, "Depositar 1"));
            actions.Add(new InventoryContextAction(InventoryContextActionKind.DepositAmount, "Depositar cantidad..."));
            actions.Add(new InventoryContextAction(InventoryContextActionKind.DepositStack, "Depositar todo"));
        }

        private static string[] GetDisplacedIds(EquipmentReplacementPlan plan)
        {
            int count = plan?.DisplacedItems?.Length ?? 0;
            var result = new string[count];
            for (int index = 0; index < count; index++)
                result[index] = plan.DisplacedItems[index]?.InstanceId;
            return result;
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
