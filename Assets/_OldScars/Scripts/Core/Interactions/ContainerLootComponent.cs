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
    /// save state, stack limits, crafting system, or economy system.
    /// </summary>
    public sealed class ContainerLootComponent : MonoBehaviour
    {
        private const string OpenedContainerTag = "opened_container";
        private const string SealedContainerTag = "sealed_container";
        private const string LootableContainerTag = "lootable_container";
        private const string LootedContainerTag = "looted_container";

        [SerializeField] private string lootTableId;

        private readonly ItemStorage storage = new ItemStorage();
        private bool storageInitialized;

        public string LootTableId => lootTableId;
        public bool HasInitializedStorage => storageInitialized;
        public bool HasStoredItems => !storage.IsEmpty;
        public bool IsStorageEmpty => storage.IsEmpty;
        public int StoredEntryCount => storage.EntryCount;
        public int StoredItemQuantity => storage.TotalQuantity;
        public IReadOnlyList<ItemStorageEntry> StorageEntries => storage.Entries;

        private void Start()
        {
            EnsureStorageInitialized();
        }

        public DebugActionExecutionResult Search(DebugActionExecutionContext executionContext, ActionDefinition action = null)
        {
            return Search(executionContext, action, out _, out _);
        }

        public DebugActionExecutionResult Search(DebugActionExecutionContext executionContext, ActionDefinition action, out bool canOpenStoragePanel, out InventoryComponent inventory)
        {
            WorldObjectTags targetTags = executionContext.Target;
            canOpenStoragePanel = false;
            inventory = null;

            if (targetTags == null)
            {
                Debug.LogWarning("[ContainerLootComponent] Cannot search container without target tags.");
                return DebugActionExecutionResult.Info("Buscar contenedor", "Error: contenedor sin tags de mundo.");
            }

            if (!CanSearch(executionContext, out string accessBlockReason))
            {
                Debug.Log($"[ContainerLootComponent] Search blocked: {accessBlockReason}");
                return DebugActionExecutionResult.Info("Buscar contenedor", accessBlockReason);
            }

            ActorInteractionContext actorContext = executionContext.ActorContext;
            if (actorContext == null)
            {
                Debug.LogWarning("[ContainerLootComponent] Cannot search container without an actor context.");
                return DebugActionExecutionResult.Info("Buscar contenedor", "Error: actor no configurado para saquear.");
            }

            inventory = actorContext.GetInventoryComponent();
            if (inventory == null)
            {
                Debug.LogWarning("[ContainerLootComponent] Actor has no InventoryComponent.");
                return DebugActionExecutionResult.Info("Buscar contenedor", "Error: el actor no tiene inventario v0 configurado.");
            }

            if (!TryGetReadyDatabase(out GameDatabase database, out string databaseError))
            {
                Debug.LogWarning($"[ContainerLootComponent] {databaseError}");
                return DebugActionExecutionResult.Info("Buscar contenedor", $"Error: {databaseError}");
            }

            if (!EnsureStorageInitialized(database, out string storageError))
            {
                Debug.LogWarning($"[ContainerLootComponent] {storageError}");
                return DebugActionExecutionResult.Info("Buscar contenedor", $"Error: {storageError}");
            }

            if (storage.IsEmpty)
            {
                MarkContainerLootedIfEmpty(targetTags, executionContext, action);
                return DebugActionExecutionResult.Info("Buscar contenedor", "No queda contenido en este contenedor.");
            }

            canOpenStoragePanel = true;
            return DebugActionExecutionResult.Info("Buscar contenedor", "Contenido disponible en Storage Debug Panel.");
        }

        public int TakeItem(int storageIndex, int quantity, InventoryComponent inventory, DebugActionExecutionContext executionContext, ActionDefinition action, out string message)
        {
            message = null;

            if (quantity < 1)
            {
                message = "Cantidad invalida.";
                return 0;
            }

            WorldObjectTags targetTags = executionContext.Target != null ? executionContext.Target : GetComponent<WorldObjectTags>();
            if (!CanAccessStorage(targetTags))
            {
                message = GetAccessBlockReason(targetTags);
                return 0;
            }

            if (inventory == null)
            {
                message = "El actor no tiene inventario v0 configurado.";
                return 0;
            }

            if (!TryGetReadyDatabase(out GameDatabase database, out string databaseError))
            {
                message = databaseError;
                return 0;
            }

            if (!EnsureStorageInitialized(database, out string storageError))
            {
                message = storageError;
                return 0;
            }

            ItemStorageEntry entry = storage.GetEntry(storageIndex);
            if (entry == null || entry.Item == null)
            {
                message = "Slot de contenedor invalido.";
                return 0;
            }

            string definitionId = entry.DefinitionId;
            int requestedQuantity = Mathf.Min(quantity, entry.Quantity);
            int transferredQuantity = inventory.TransferItemFrom(storage, storageIndex, requestedQuantity);
            if (transferredQuantity <= 0)
            {
                message = "No se pudo transferir contenido.";
                return 0;
            }

            var addedCounts = new Dictionary<string, int>
            {
                [definitionId] = transferredQuantity
            };

            RecordLootReceived(addedCounts, database, executionContext, action);
            MarkContainerLootedIfEmpty(targetTags, executionContext, action);

            message = $"Tomaste {FormatAddedLoot(addedCounts, database)}.";
            return transferredQuantity;
        }

        public bool CanAccessStorage(WorldObjectTags targetTags)
        {
            return string.IsNullOrWhiteSpace(GetAccessBlockReason(targetTags));
        }

        public bool CanSearch(DebugActionExecutionContext executionContext, out string reason)
        {
            reason = GetAccessBlockReason(executionContext.Target);
            return string.IsNullOrWhiteSpace(reason);
        }

        public string GetAccessBlockReason(WorldObjectTags targetTags)
        {
            if (targetTags == null)
                return "Error: contenedor sin tags de mundo.";

            if (targetTags.HasTag(LootedContainerTag))
                return "Este contenedor ya fue saqueado.";

            if (targetTags.HasTag(SealedContainerTag))
                return "Este contenedor esta sellado.";

            if (!targetTags.HasTag(OpenedContainerTag))
                return "Este contenedor no esta abierto.";

            if (!targetTags.HasTag(LootableContainerTag))
                return "Este contenedor ya no se puede saquear.";

            return null;
        }

        public string GetDebugStorageSummary()
        {
            return
                $"Storage initialized: {FormatYesNo(storageInitialized)}" +
                $"\nEntry count: {storage.EntryCount}" +
                $"\nTotal quantity: {storage.TotalQuantity}" +
                $"\nContents: {FormatStorageContentsByDefinitionId()}";
        }

        private bool EnsureStorageInitialized()
        {
            if (storageInitialized)
                return true;

            if (!TryGetReadyDatabase(out GameDatabase database, out string databaseError))
            {
                Debug.LogWarning($"[ContainerLootComponent] Cannot initialize storage for '{SafeText(lootTableId)}': {databaseError}");
                return false;
            }

            string storageError;
            return EnsureStorageInitialized(database, out storageError);
        }

        private bool EnsureStorageInitialized(GameDatabase database, out string error)
        {
            error = null;

            if (storageInitialized)
                return true;

            LootTableDefinition lootTable = database != null ? database.GetLootTable(lootTableId) : null;
            if (lootTable == null)
            {
                error = $"loot table no encontrada: {SafeText(lootTableId)}.";
                return false;
            }

            if (HasBrokenLootData(lootTable, database, out string dataError))
            {
                error = $"loot table invalida: {dataError}.";
                return false;
            }

            PopulateStorage(lootTable, database);
            storageInitialized = true;

            Debug.Log(
                "[ContainerLootComponent] Runtime storage initialized." +
                $"\n  Loot table: {SafeText(lootTableId)}" +
                $"\n  Entries: {storage.EntryCount}" +
                $"\n  Total quantity: {storage.TotalQuantity}" +
                $"\n  Contents: {FormatStorageContents(database)}");

            return true;
        }

        private static bool TryGetReadyDatabase(out GameDatabase database, out string error)
        {
            database = null;
            error = null;

            if (GameDataManager.Instance == null)
            {
                error = "GameDataManager.Instance no encontrado.";
                return false;
            }

            if (!GameDataManager.Instance.IsReady)
            {
                error = "la base de datos no esta lista.";
                return false;
            }

            database = GameDataManager.Instance.Database;
            if (database == null)
            {
                error = "GameDatabase no encontrada.";
                return false;
            }

            return true;
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

        private void PopulateStorage(LootTableDefinition lootTable, GameDatabase database)
        {
            if (lootTable.entries == null)
                return;

            for (int entryIndex = 0; entryIndex < lootTable.entries.Length; entryIndex++)
            {
                LootTableEntryDefinition entry = lootTable.entries[entryIndex];
                ItemDefinition definition = database.GetItem(entry.item_id);
                storage.AddItem(new ItemInstance(definition), entry.count);
            }
        }

        private string FormatStorageContents(GameDatabase database)
        {
            var parts = new List<string>();

            IReadOnlyList<ItemStorageEntry> entries = storage.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                ItemStorageEntry entry = entries[index];
                if (entry == null || entry.Item == null)
                    continue;

                string displayName = GetItemDisplayName(entry.Item.DefinitionId, database);
                parts.Add($"{displayName} x{entry.Quantity}");
            }

            return parts.Count > 0 ? string.Join(", ", parts) : "nada";
        }

        private string FormatStorageContentsByDefinitionId()
        {
            var parts = new List<string>();

            IReadOnlyList<ItemStorageEntry> entries = storage.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                ItemStorageEntry entry = entries[index];
                if (entry == null || string.IsNullOrWhiteSpace(entry.DefinitionId))
                    continue;

                parts.Add($"{entry.DefinitionId} x{entry.Quantity}");
            }

            return parts.Count > 0 ? string.Join(", ", parts) : "(empty)";
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

        private void MarkContainerLootedIfEmpty(WorldObjectTags targetTags, DebugActionExecutionContext executionContext, ActionDefinition action)
        {
            if (targetTags == null || !storage.IsEmpty)
                return;

            MarkContainerLooted(targetTags, executionContext, action);
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

        private static string FormatYesNo(bool value)
        {
            return value ? "yes" : "no";
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
        }
    }
}
