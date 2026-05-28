using System.Collections.Generic;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Feedback;
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

        public DebugActionExecutionResult Search(DebugActionExecutionContext executionContext, ActionDefinition action = null)
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
            MarkContainerLooted(targetTags, executionContext, action);

            if (addedCounts.Count == 0)
                return DebugActionExecutionResult.Info("Buscar contenedor", "No encontraste nada util.");

            RecordLootReceived(addedCounts, database, executionContext, action);
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

        private static void MarkContainerLooted(WorldObjectTags targetTags, DebugActionExecutionContext executionContext, ActionDefinition action)
        {
            bool removedLootable = targetTags.RemoveTag(LootableContainerTag);
            bool addedLooted = targetTags.AddTag(LootedContainerTag);

            Debug.Log(
                "[ContainerLootComponent] Container looted." +
                $"\n  Target: {targetTags.name}" +
                $"\n  Runtime tags: {FormatTags(targetTags.RuntimeTags)}");

            RecordTargetStateChanged(executionContext, action, addedLooted, removedLootable);
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

        private static void RecordLootReceived(Dictionary<string, int> addedCounts, GameDatabase database, DebugActionExecutionContext executionContext, ActionDefinition action)
        {
            if (addedCounts == null || addedCounts.Count == 0)
                return;

            foreach (KeyValuePair<string, int> added in addedCounts)
            {
                string itemDisplayName = GetItemDisplayName(added.Key, database);
                GameplayFeedbackLog.TryRecord(new GameplayFeedbackEntry(
                    GameplayFeedbackEntryType.LootReceived,
                    $"Encontraste {itemDisplayName} x{added.Value}.",
                    actorId: GetActorName(executionContext.ActorContext),
                    actorDisplayName: GetActorName(executionContext.ActorContext),
                    targetId: GetTargetName(executionContext.Target),
                    targetDisplayName: GetTargetDisplayName(executionContext.Target),
                    itemId: added.Key,
                    itemDisplayName: itemDisplayName,
                    actionId: action != null ? action.id : null,
                    actionDisplayName: GetActionDisplayName(action),
                    quantity: added.Value));
            }
        }

        private static void RecordTargetStateChanged(DebugActionExecutionContext executionContext, ActionDefinition action, bool addedLooted, bool removedLootable)
        {
            if (!addedLooted && !removedLootable)
                return;

            string targetDisplayName = GetTargetDisplayName(executionContext.Target);
            GameplayFeedbackLog.TryRecord(new GameplayFeedbackEntry(
                GameplayFeedbackEntryType.TargetStateChanged,
                $"Estado actualizado: {SafeText(targetDisplayName)}.",
                actorId: GetActorName(executionContext.ActorContext),
                actorDisplayName: GetActorName(executionContext.ActorContext),
                targetId: GetTargetName(executionContext.Target),
                targetDisplayName: targetDisplayName,
                actionId: action != null ? action.id : null,
                actionDisplayName: GetActionDisplayName(action),
                addedTags: addedLooted ? new[] { LootedContainerTag } : null,
                removedTags: removedLootable ? new[] { LootableContainerTag } : null,
                debugOnly: true));
        }

        private static string GetActionDisplayName(ActionDefinition action)
        {
            if (action == null)
                return null;

            if (action.display != null && !string.IsNullOrWhiteSpace(action.display.name))
                return action.display.name;

            return action.id;
        }

        private static string GetActorName(ActorInteractionContext actorContext)
        {
            return actorContext != null ? actorContext.name : null;
        }

        private static string GetTargetName(WorldObjectTags targetTags)
        {
            return targetTags != null ? targetTags.name : null;
        }

        private static string GetTargetDisplayName(WorldObjectTags targetTags)
        {
            if (targetTags == null)
                return null;

            WorldObjectDebugInfo debugInfo = targetTags.GetComponent<WorldObjectDebugInfo>();
            return debugInfo != null ? debugInfo.GetDisplayNameOrFallback(targetTags.name) : targetTags.name;
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
