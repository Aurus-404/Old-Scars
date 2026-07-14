using System;
using System.Collections.Generic;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;

namespace OldScars.Core.Items
{
    /// <summary>
    /// Shared weight resolver for one item entry and its item-owned storage subtree.
    /// </summary>
    public static class ItemWeightResolver
    {
        public static bool TryGetDefinitionWeight(
            string definitionId,
            int quantity,
            out double unitWeightKg,
            out double stackWeightKg,
            out string error)
        {
            unitWeightKg = 0d;
            stackWeightKg = 0d;
            error = null;

            if (string.IsNullOrWhiteSpace(definitionId))
            {
                error = "Cannot calculate carry weight without an item definition id.";
                return false;
            }

            if (quantity < 0)
            {
                error = $"Cannot calculate carry weight for '{definitionId}' with quantity {quantity}.";
                return false;
            }

            if (GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
            {
                error = $"Cannot resolve physical.weight_kg for item '{definitionId}' because game data is not ready.";
                return false;
            }

            GameDatabase database = GameDataManager.Instance.Database;
            ItemDefinition definition = database != null ? database.GetItem(definitionId) : null;
            if (definition == null)
            {
                error = $"Cannot resolve carry weight because item definition '{definitionId}' was not found.";
                return false;
            }

            if (definition.physical == null || !definition.physical.weight_kg.HasValue)
            {
                error = $"Item '{definitionId}' has no explicit physical.weight_kg.";
                return false;
            }

            double resolvedUnitWeight = definition.physical.weight_kg.Value;
            if (!IsFinite(resolvedUnitWeight) || resolvedUnitWeight < 0d)
            {
                error = $"Item '{definitionId}' has invalid physical.weight_kg '{resolvedUnitWeight}'.";
                return false;
            }

            double resolvedStackWeight = resolvedUnitWeight * quantity;
            if (!IsFinite(resolvedStackWeight) || resolvedStackWeight < 0d)
            {
                error = $"Item '{definitionId}' produced invalid stack weight for quantity {quantity}.";
                return false;
            }

            unitWeightKg = resolvedUnitWeight;
            stackWeightKg = resolvedStackWeight;
            return true;
        }

        public static bool TryGetEntryWeight(
            ItemStorageEntry entry,
            int quantity,
            out double totalWeightKg,
            out string error)
        {
            return TryGetEntryWeight(entry, quantity, new HashSet<string>(), out totalWeightKg, out error);
        }

        private static bool TryGetEntryWeight(
            ItemStorageEntry entry,
            int quantity,
            HashSet<string> visited,
            out double totalWeightKg,
            out string error)
        {
            totalWeightKg = 0d;
            error = null;
            ItemInstance item = entry != null ? entry.Item : null;
            if (item == null || quantity < 0 || quantity > entry.Quantity)
            {
                error = "Cannot calculate weight for an invalid item entry or quantity.";
                return false;
            }

            if (!visited.Add(item.InstanceId))
            {
                error = $"Item-owned storage cycle or duplicate instance detected at '{item.InstanceId}'.";
                return false;
            }

            if (!TryGetDefinitionWeight(item.DefinitionId, quantity, out _, out totalWeightKg, out error))
                return false;

            if (!item.HasOwnedStorage || quantity == 0)
                return true;

            if (quantity != 1 || entry.Quantity != 1 || item.MaxStack != 1)
            {
                error = $"Storage-owning item '{item.InstanceId}' must be a non-stackable quantity-1 entry.";
                return false;
            }

            IReadOnlyList<ItemStorageEntry> contents = item.OwnedStorage.GridStorageEntries;
            for (int index = 0; index < contents.Count; index++)
            {
                ItemStorageEntry content = contents[index];
                if (!TryGetEntryWeight(
                        content,
                        content != null ? content.Quantity : 0,
                        visited,
                        out double contentWeightKg,
                        out error))
                {
                    return false;
                }

                totalWeightKg += contentWeightKg;
            }

            return IsFinite(totalWeightKg);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
