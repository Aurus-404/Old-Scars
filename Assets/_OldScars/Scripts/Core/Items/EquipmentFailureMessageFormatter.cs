using System.Collections.Generic;
using OldScars.Core.Actors;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;

namespace OldScars.Core.Items
{
    public static class EquipmentFailureMessageFormatter
    {
        public static string FormatFailure(
            EquipmentFailureCode failureCode,
            ActorEquipmentComponent equipment,
            IReadOnlyList<string> requestedSlotIds = null,
            IReadOnlyList<string> displacedInstanceIds = null)
        {
            switch (failureCode)
            {
                case EquipmentFailureCode.InvalidQuantity:
                    return "El objeto debe tener cantidad 1 para equiparse.";
                case EquipmentFailureCode.NotEquipable:
                case EquipmentFailureCode.InvalidSlotSet:
                case EquipmentFailureCode.LayoutUnavailable:
                    return "Este objeto no puede equiparse en ese slot.";
                case EquipmentFailureCode.SlotOccupied:
                    return FormatOccupiedSlots(requestedSlotIds);
                case EquipmentFailureCode.NoPersonalInventorySpace:
                    return FormatNoSpace(equipment, displacedInstanceIds);
                case EquipmentFailureCode.SourceNotFound:
                    return "El objeto ya no está disponible.";
                case EquipmentFailureCode.StaleState:
                case EquipmentFailureCode.OwnershipChanged:
                    return "El estado del inventario cambió. Intentá nuevamente.";
                default:
                    return "No se pudo completar la acción de equipamiento.";
            }
        }

        public static string FormatReplacementSummary(
            ActorEquipmentComponent equipment,
            EquipmentReplacementPlan plan)
        {
            if (plan == null || plan.DisplacedItems == null || plan.DisplacedItems.Length == 0)
                return string.Empty;

            var names = new string[plan.DisplacedItems.Length];
            for (int index = 0; index < plan.DisplacedItems.Length; index++)
                names[index] = GetDisplayName(equipment, plan.DisplacedItems[index]?.InstanceId);
            return $"Reemplaza: {string.Join(", ", names)}";
        }

        public static string FormatSuccess(
            ActorEquipmentComponent equipment,
            string instanceId,
            bool unequipped,
            bool replaced)
        {
            string displayName = GetDisplayName(equipment, instanceId);
            if (unequipped)
                return $"Guardaste {displayName} en el inventario.";
            return replaced
                ? $"Equipaste {displayName} y guardaste el equipamiento reemplazado."
                : $"Equipaste {displayName}.";
        }

        public static string GetDisplayName(ActorEquipmentComponent equipment, string instanceId)
        {
            ItemStorageEntry entry = null;
            if (equipment != null)
            {
                if (!equipment.TryGetEntryByInstanceId(instanceId, out entry))
                    equipment.PersonalInventory?.TryGetEntryByInstanceId(instanceId, out _, out entry);
            }

            string definitionId = entry?.DefinitionId;
            GameDatabase database = GameDataManager.Instance != null && GameDataManager.Instance.IsReady
                ? GameDataManager.Instance.Database
                : null;
            ItemDefinition definition = database != null && !string.IsNullOrWhiteSpace(definitionId)
                ? database.GetItem(definitionId)
                : null;
            if (definition?.display != null && !string.IsNullOrWhiteSpace(definition.display.name))
                return definition.display.name;
            return !string.IsNullOrWhiteSpace(definitionId) ? definitionId : "el objeto";
        }

        private static string FormatOccupiedSlots(IReadOnlyList<string> slotIds)
        {
            bool left = Contains(slotIds, ActorEquipmentComponent.HandLeftSlotId);
            bool right = Contains(slotIds, ActorEquipmentComponent.HandRightSlotId);
            if (left && right)
                return "Ambas manos deben estar disponibles.";
            if (right)
                return "Mano derecha ocupada.";
            if (left)
                return "Mano izquierda ocupada.";
            return "El slot de equipamiento está ocupado.";
        }

        private static string FormatNoSpace(
            ActorEquipmentComponent equipment,
            IReadOnlyList<string> displacedInstanceIds)
        {
            int count = displacedInstanceIds?.Count ?? 0;
            if (count == 1)
                return $"No hay espacio para guardar {GetDisplayName(equipment, displacedInstanceIds[0])}.";
            return "No hay espacio para guardar los objetos reemplazados.";
        }

        private static bool Contains(IReadOnlyList<string> values, string expected)
        {
            if (values == null)
                return false;
            for (int index = 0; index < values.Count; index++)
            {
                if (values[index] == expected)
                    return true;
            }
            return false;
        }
    }
}
