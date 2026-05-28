using System.Collections.Generic;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Items;
using UnityEngine;

namespace OldScars.Core.Interactions
{
    /// <summary>
    /// Runtime bridge for searchable container loot v0.
    ///
    /// This is not a final container inventory, loot UI, random loot system,
    /// save state, stack system, crafting system, or economy system.
    /// </summary>
    public sealed class ContainerLootComponent : MonoBehaviour
    {
        private const string LootableContainerTag = "lootable_container";
        private const string LootedContainerTag = "looted_container";

        [SerializeField] private string lootTableId;

        public string LootTableId => lootTableId;

        public DebugActionExecutionResult Search(DebugActionExecutionContext executionContext)
        {
            WorldObjectTags targetTags = executionContext.Target;
            if (targetTags == null)
            {
                Debug.LogWarning("[ContainerLootComponent] Cannot search container without target tags.");
                return DebugActionExecutionResult.Info("Buscar contenedor", "Error: contenedor sin tags de mundo.");
            }

            if (!targetTags.HasTag(LootableContainerTag))
            {
                Debug.Log("[ContainerLootComponent] Container is not lootable; search ignored.");
                return DebugActionExecutionResult.Info("Buscar contenedor", "Este contenedor ya no se puede saquear.");
            }

            ActorInteractionContext actorContext = executionContext.ActorContext;
            if (actorContext == null)
            {
                Debug.LogWarning("[ContainerLootComponent] Cannot search container without an actor context.");
                return DebugActionExecutionResult.Info("Buscar contenedor", "Error: actor no configurado para saquear.");
            }

            InventoryComponent inventory = actorContext.GetInventoryComponent();
            if (inventory == null)
            {
                Debug.LogWarning("[ContainerLootComponent] Actor has no InventoryComponent.");
                return DebugActionExecutionResult.Info("Buscar contenedor", "Error: el actor no tiene inventario v0 configurado.");
            }

            if (GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
            {
                Debug.LogWarning("[ContainerLootComponent] GameDataManager is not ready.");
                return DebugActionExecutionResult.Info("Buscar contenedor", "Error: la base de datos no esta lista.");
            }

            GameDatabase database = GameDataManager.Instance.Database;
            LootTableDefinition lootTable = database != null ? database.GetLootTable(lootTableId) : null;
            if (lootTable == null)
            {
                Debug.LogWarning($"[ContainerLootComponent] Loot table '{SafeText(lootTableId)}' was not found.");
                return DebugActionExecutionResult.Info("Buscar contenedor", $"Error: loot table no encontrada: {SafeText(lootTableId)}.");
            }

            if (HasBrokenLootData(lootTable, database, out string dataError))
            {
                Debug.LogWarning($"[ContainerLootComponent] Loot table '{SafeText(lootTableId)}' has invalid data: {dataError}");
                return DebugActionExecutionResult.Info("Buscar contenedor", $"Error: loot table invalida: {dataError}.");
            }

            Dictionary<string, int> addedCounts = AddLootToInventory(lootTable, inventory);
            MarkContainerLooted(targetTags);

            if (addedCounts.Count == 0)
                return DebugActionExecutionResult.Info("Buscar contenedor", "No encontraste nada util.");

            return DebugActionExecutionResult.Info("Buscar contenedor", $"Encontraste: {FormatAddedLoot(addedCounts, database)}.");
        }

        private static bool HasBrokenLootData(LootTableDefinition lootTable, GameDatabase database, out string error)
        {
            error = null;

            if (lootTable == null)
            {
                error = "loot table null";
                return true;
            }

            if (lootTable.entries == null || lootTable.entries.Length == 0)
            {
                error = "entries array is required and must not be empty";
                return true;
            }

            for (int index = 0; index < lootTable.entries.Length; index++)
            {
                LootTableEntryDefinition entry = lootTable.entries[index];
                if (entry == null)
                {
                    error = $"entry[{index}] null";
                    return true;
                }

                if (string.IsNullOrWhiteSpace(entry.item_id))
                {
                    error = $"entry[{index}] missing item_id";
                    return true;
                }

                if (entry.count <= 0)
                {
                    error = $"entry[{index}] count must be > 0";
                    return true;
                }

                if (database == null || database.GetItem(entry.item_id) == null)
                {
                    error = $"entry[{index}] item_id '{entry.item_id}' was not found";
                    return true;
                }
            }

            return false;
        }

        private static Dictionary<string, int> AddLootToInventory(LootTableDefinition lootTable, InventoryComponent inventory)
        {
            var addedCounts = new Dictionary<string, int>();

            if (lootTable.entries == null)
                return addedCounts;

            for (int entryIndex = 0; entryIndex < lootTable.entries.Length; entryIndex++)
            {
                LootTableEntryDefinition entry = lootTable.entries[entryIndex];

                for (int countIndex = 0; countIndex < entry.count; countIndex++)
                {
                    ItemInstance item = inventory.AddItemByDefinitionId(entry.item_id);
                    if (item == null)
                        continue;

                    if (!addedCounts.ContainsKey(item.DefinitionId))
                        addedCounts[item.DefinitionId] = 0;

                    addedCounts[item.DefinitionId]++;
                }
            }

            return addedCounts;
        }

        private static void MarkContainerLooted(WorldObjectTags targetTags)
        {
            targetTags.RemoveTag(LootableContainerTag);
            targetTags.AddTag(LootedContainerTag);

            Debug.Log(
                "[ContainerLootComponent] Container looted." +
                $"\n  Target: {targetTags.name}" +
                $"\n  Runtime tags: {FormatTags(targetTags.RuntimeTags)}");
        }

        private static string FormatAddedLoot(Dictionary<string, int> addedCounts, GameDatabase database)
        {
            var parts = new List<string>();

            foreach (KeyValuePair<string, int> added in addedCounts)
            {
                string displayName = GetItemDisplayName(added.Key, database);
                parts.Add($"{displayName} x{added.Value}");
            }

            return parts.Count > 0 ? string.Join(", ", parts) : "nada";
        }

        private static string GetItemDisplayName(string itemId, GameDatabase database)
        {
            ItemDefinition item = database != null ? database.GetItem(itemId) : null;
            if (item == null || item.display == null || string.IsNullOrWhiteSpace(item.display.name))
                return SafeText(itemId);

            return item.display.name;
        }

        private static string FormatTags(string[] tags)
        {
            return tags != null && tags.Length > 0 ? string.Join(", ", tags) : "(none)";
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
        }
    }
}
