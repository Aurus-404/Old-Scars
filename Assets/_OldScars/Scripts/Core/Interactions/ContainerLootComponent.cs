using System;
using System.Collections.Generic;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Feedback;
using OldScars.Core.Identity;
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
    public sealed class ContainerLootComponent : MonoBehaviour, IItemStorageDebugSource, IGridStorageTransferEndpoint
    {
        private const string OpenedContainerTag = "opened_container";
        private const string SealedContainerTag = "sealed_container";
        private const string UnsearchedContainerTag = "unsearched_container";
        private const string StorageAccessibleTag = "storage_accessible";
        private const string LootableContainerTag = "lootable_container";
        private const string LootedContainerTag = "looted_container";

        [SerializeField] private string lootTableId;
        [SerializeField] private bool useGridLayout;
        [SerializeField] private int gridWidth = 4;
        [SerializeField] private int gridHeight = 4;

        private readonly ItemStorage storage = new ItemStorage();
        private bool storageInitialized;
        private GridStorageRuntime gridStorageRuntime;

        public string LootTableId => lootTableId;
        public bool HasInitializedStorage => storageInitialized;
        public bool HasStoredItems => !storage.IsEmpty;
        public bool IsStorageEmpty => storage.IsEmpty;
        public int StoredEntryCount => storage.EntryCount;
        public int StoredItemQuantity => storage.TotalQuantity;
        public IReadOnlyList<ItemStorageEntry> StorageEntries => storage.Entries;
        public string GridStorageDisplayName => name;
        public IReadOnlyList<ItemStorageEntry> GridStorageEntries => storage.Entries;
        public bool UsesGridLayout => GetGridRuntime().UsesGridLayout;
        public int GridWidth => GetGridRuntime().GridWidth;
        public int GridHeight => GetGridRuntime().GridHeight;
        public int ConfiguredGridWidth => gridWidth;
        public int ConfiguredGridHeight => gridHeight;
        public GridStorageInitializationState GridInitializationState => GetGridRuntime().InitializationState;
        public string GridInitializationError => GetGridRuntime().InitializationError;

        GridInventoryBackend IGridStorageTransferEndpoint.TransferBackend => GetGridRuntime().Backend;

        internal void MarkPersistenceStorageInitialized()
        {
            storageInitialized = true;
        }

        private void Awake()
        {
            GetGridRuntime();
        }

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

            if (!TryPrepareStorageAccess(executionContext, out inventory, out string storageError))
            {
                Debug.LogWarning($"[ContainerLootComponent] {storageError}");
                return DebugActionExecutionResult.Info("Buscar contenedor", $"Error: {storageError}");
            }

            MarkContainerSearched(targetTags, executionContext, action);
            canOpenStoragePanel = true;
            string message = storage.IsEmpty
                ? "Primera revision completada. El almacenamiento esta vacio."
                : "Primera revision completada. Contenido disponible en Storage Debug Panel.";
            return DebugActionExecutionResult.Info("Buscar contenedor", message);
        }

        public DebugActionExecutionResult OpenStorage(DebugActionExecutionContext executionContext, out bool canOpenStoragePanel, out InventoryComponent inventory)
        {
            WorldObjectTags targetTags = executionContext.Target;
            canOpenStoragePanel = false;
            inventory = null;

            string accessBlockReason = GetOpenStorageBlockReason(targetTags);
            if (!string.IsNullOrWhiteSpace(accessBlockReason))
            {
                Debug.Log($"[ContainerLootComponent] Open storage blocked: {accessBlockReason}");
                return DebugActionExecutionResult.Info("Abrir contenedor", accessBlockReason);
            }

            if (!TryPrepareOpenedStorageAccess(executionContext, out inventory, out string storageError))
            {
                Debug.LogWarning($"[ContainerLootComponent] {storageError}");
                return DebugActionExecutionResult.Info("Abrir contenedor", $"Error: {storageError}");
            }

            canOpenStoragePanel = true;
            return DebugActionExecutionResult.Info("Abrir contenedor", "Storage Debug Panel disponible.");
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
            InventoryMutationResult transferResult = GridStorageTransferService.TransferQuantityAuto(
                this,
                inventory,
                entry.Item.InstanceId,
                requestedQuantity,
                true,
                new GridStorageTransferContext(executionContext, action));
            if (!transferResult.Success)
            {
                message = transferResult.Message ?? "No se pudo transferir contenido.";
                return 0;
            }

            int transferredQuantity = transferResult.AffectedQuantity;
            var addedCounts = new Dictionary<string, int>
            {
                [definitionId] = transferredQuantity
            };

            message = $"Tomaste {FormatAddedLoot(addedCounts, database)}.";
            return transferredQuantity;
        }

        public int DepositItem(int inventoryIndex, int quantity, InventoryComponent sourceInventory, DebugActionExecutionContext executionContext, ActionDefinition action, out string message)
        {
            message = null;

            if (quantity < 1)
            {
                message = "Cantidad invalida.";
                return 0;
            }

            WorldObjectTags targetTags = executionContext.Target != null ? executionContext.Target : GetComponent<WorldObjectTags>();
            string depositBlockReason = GetDepositAccessBlockReason(targetTags);
            if (!string.IsNullOrWhiteSpace(depositBlockReason))
            {
                message = depositBlockReason;
                return 0;
            }

            if (sourceInventory == null)
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

            ItemStorageEntry entry = sourceInventory.GetEntry(inventoryIndex);
            if (entry == null || entry.Item == null)
            {
                message = "Slot de inventario invalido.";
                return 0;
            }

            string definitionId = entry.DefinitionId;
            int requestedQuantity = Mathf.Min(quantity, entry.Quantity);
            InventoryMutationResult transferResult = GridStorageTransferService.TransferQuantityAuto(
                sourceInventory,
                this,
                entry.Item.InstanceId,
                requestedQuantity,
                true,
                new GridStorageTransferContext(executionContext, action));
            if (!transferResult.Success)
            {
                message = transferResult.Message ?? "No se pudo depositar contenido.";
                return 0;
            }

            int transferredQuantity = transferResult.AffectedQuantity;
            message = $"Depositaste {GetItemDisplayName(definitionId, database)} x{transferredQuantity}.";
            return transferredQuantity;
        }

        public bool CanAccessStorage(WorldObjectTags targetTags)
        {
            return string.IsNullOrWhiteSpace(GetAccessBlockReason(targetTags));
        }

        public bool CanSearch(DebugActionExecutionContext executionContext, out string reason)
        {
            reason = GetSearchBlockReason(executionContext.Target);
            return string.IsNullOrWhiteSpace(reason);
        }

        public string GetAccessBlockReason(WorldObjectTags targetTags)
        {
            if (targetTags == null)
                return "Error: contenedor sin tags de mundo.";

            if (targetTags.HasTag(SealedContainerTag))
                return "Este contenedor esta sellado.";

            if (!targetTags.HasTag(OpenedContainerTag))
                return "Este contenedor no esta abierto.";

            if (targetTags.HasTag(StorageAccessibleTag))
                return null;

            if (targetTags.HasTag(LootedContainerTag))
                return "Este contenedor ya fue saqueado.";

            if (!targetTags.HasTag(LootableContainerTag))
                return "Este contenedor ya no se puede saquear.";

            return null;
        }

        private static string GetSearchBlockReason(WorldObjectTags targetTags)
        {
            if (targetTags == null)
                return "Error: contenedor sin tags de mundo.";

            if (targetTags.HasTag(SealedContainerTag))
                return "Este contenedor esta sellado.";

            if (!targetTags.HasTag(OpenedContainerTag))
                return "Este contenedor no esta abierto.";

            if (!targetTags.HasTag(UnsearchedContainerTag))
                return "Este contenedor ya fue revisado.";

            return null;
        }

        private static string GetOpenStorageBlockReason(WorldObjectTags targetTags)
        {
            if (targetTags == null)
                return "Error: contenedor sin tags de mundo.";

            if (targetTags.HasTag(SealedContainerTag))
                return "Este contenedor esta sellado.";

            if (!targetTags.HasTag(OpenedContainerTag))
                return "Este contenedor no esta abierto.";

            if (!targetTags.HasTag(StorageAccessibleTag))
                return "Este almacenamiento todavia no fue descubierto.";

            return null;
        }

        private static string GetDepositAccessBlockReason(WorldObjectTags targetTags)
        {
            if (targetTags == null)
                return "Error: contenedor sin tags de mundo.";

            if (targetTags.HasTag(SealedContainerTag))
                return "Este contenedor esta sellado.";

            if (!targetTags.HasTag(OpenedContainerTag))
                return "Este contenedor no esta abierto.";

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

        public string GetStorageDebugTitle(WorldObjectTags target)
        {
            string targetName = target != null ? target.name : name;
            WorldObjectDebugInfo debugInfo = target != null ? target.GetComponent<WorldObjectDebugInfo>() : null;
            string displayName = debugInfo != null ? debugInfo.GetDisplayNameOrFallback(targetName, target) : targetName;
            return $"{displayName} Contents (Debug)";
        }

        private bool EnsureStorageInitialized()
        {
            if (storageInitialized)
                return true;

            if (!TryGetReadyDatabase(out GameDatabase database, out string databaseError))
            {
                LogStorageInitializationFailure("DatabaseUnavailable", false, false, databaseError,
                    "Initialization deferred; storage remains empty.");
                return false;
            }

            string storageError;
            return EnsureStorageInitialized(database, out storageError);
        }

        private bool TryPrepareStorageAccess(DebugActionExecutionContext executionContext, out InventoryComponent inventory, out string error)
        {
            if (!TryGetActorInventory(executionContext, out inventory, out error))
                return false;

            if (!TryGetReadyDatabase(out GameDatabase database, out error))
                return false;

            return EnsureStorageInitialized(database, out error);
        }

        private bool TryPrepareOpenedStorageAccess(DebugActionExecutionContext executionContext, out InventoryComponent inventory, out string error)
        {
            if (!TryGetActorInventory(executionContext, out inventory, out error))
                return false;

            if (!storageInitialized)
            {
                error = "el storage todavia no fue inicializado.";
                return false;
            }

            return true;
        }

        private static bool TryGetActorInventory(DebugActionExecutionContext executionContext, out InventoryComponent inventory, out string error)
        {
            inventory = null;
            error = null;

            ActorInteractionContext actorContext = executionContext.ActorContext;
            if (actorContext == null)
            {
                error = "actor no configurado para saquear.";
                return false;
            }

            inventory = actorContext.GetInventoryComponent();
            if (inventory == null)
            {
                error = "el actor no tiene inventario v0 configurado.";
                return false;
            }

            return true;
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
                LogStorageInitializationFailure("LootTableNotFound", true, false, error,
                    "Initialization aborted; storage remains empty.");
                return false;
            }

            lootTableId = lootTable.id;

            if (HasBrokenLootData(lootTable, database, out string dataError))
            {
                error = $"loot table invalida: {dataError}.";
                LogStorageInitializationFailure("InvalidLootTable", true, true, error,
                    "Initialization aborted; storage remains empty.");
                return false;
            }

            if (!TryPopulateStorage(lootTable, database, out string populationError))
            {
                error = $"no se pudo poblar el storage: {populationError}";
                LogStorageInitializationFailure("StoragePopulationFailed", true, true, error,
                    "Storage snapshot restored; new instance reservations released.");
                return false;
            }

            storageInitialized = true;

            if (!GetGridRuntime().TryInitializeLayout(out string gridError))
            {
                Debug.LogError(
                    "[ContainerLootComponent] Grid initialization failed; container remains linear and content was preserved." +
                    $"\n  Container: {name}" +
                    $"\n  Requested grid: {gridWidth}x{gridHeight}" +
                    $"\n  Reason: {SafeText(gridError)}");
            }

            PersistentSceneObjectId persistentIdentity = GetComponent<PersistentSceneObjectId>();
            Debug.Log(
                "[ContainerLootComponent][INITIALIZED]" +
                $"\n  Container: {name}" +
                $"\n  Root: {(transform.root != null ? transform.root.name : "<UNKNOWN>")}" +
                $"\n  PersistentSceneObjectId: {DiagnosticText(persistentIdentity != null ? persistentIdentity.PersistentId : null)}" +
                $"\n  LootTable: {DiagnosticText(lootTableId)}" +
                $"\n  Entries: {storage.EntryCount}" +
                $"\n  TotalQuantity: {storage.TotalQuantity}");

            return true;
        }

        public bool TryGetEntryByInstanceId(string instanceId, out int index, out ItemStorageEntry entry)
        {
            index = storage.GetEntryIndexByInstanceId(instanceId);
            entry = index >= 0 ? storage.GetEntry(index) : null;
            return entry != null;
        }

        public bool TryGetGridPlacement(string instanceId, out GridPlacement placement)
        {
            return GetGridRuntime().TryGetPlacement(instanceId, out placement);
        }

        public bool TryGetGridFootprint(string definitionId, out GridFootprint footprint, out bool usedFallback)
        {
            return GetGridRuntime().TryResolveFootprint(definitionId, out footprint, out usedFallback);
        }

        public GridPlacementValidationResult PreviewGridPlacementMove(
            string instanceId,
            int x,
            int y,
            bool isRotated)
        {
            return GetGridRuntime().PreviewMovePlacement(instanceId, x, y, isRotated);
        }

        public InventoryMutationResult MoveGridPlacement(string instanceId, int x, int y, bool isRotated)
        {
            return GetGridRuntime().MovePlacement(instanceId, x, y, isRotated);
        }

        public bool IsInstanceEquipped(string instanceId)
        {
            return false;
        }

        private GridStorageRuntime GetGridRuntime()
        {
            if (gridStorageRuntime == null)
            {
                gridStorageRuntime = new GridStorageRuntime(
                    storage,
                    ResolveItemDefinition,
                    useGridLayout,
                    gridWidth,
                    gridHeight,
                    false);
            }

            return gridStorageRuntime;
        }

        private static ItemDefinition ResolveItemDefinition(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId) || GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
                return null;

            GameDatabase database = GameDataManager.Instance.Database;
            return database != null ? database.GetItem(definitionId) : null;
        }

        bool IGridStorageTransferEndpoint.CanTransferOut(GridStorageTransferContext context, out string reason)
        {
            WorldObjectTags targetTags = context.ExecutionContext.Target != null
                ? context.ExecutionContext.Target
                : GetComponent<WorldObjectTags>();
            reason = GetAccessBlockReason(targetTags);
            return string.IsNullOrWhiteSpace(reason);
        }

        bool IGridStorageTransferEndpoint.CanTransferIn(GridStorageTransferContext context, out string reason)
        {
            WorldObjectTags targetTags = context.ExecutionContext.Target != null
                ? context.ExecutionContext.Target
                : GetComponent<WorldObjectTags>();
            reason = GetDepositAccessBlockReason(targetTags);
            return string.IsNullOrWhiteSpace(reason);
        }

        void IGridStorageTransferEndpoint.OnTransferCommittedOut(
            GridStorageTransferReceipt receipt,
            GridStorageTransferContext context)
        {
            WorldObjectTags targetTags = context.ExecutionContext.Target != null
                ? context.ExecutionContext.Target
                : GetComponent<WorldObjectTags>();
            if (!string.IsNullOrWhiteSpace(receipt.DefinitionId) &&
                TryGetReadyDatabase(out GameDatabase database, out _))
            {
                var addedCounts = new Dictionary<string, int>
                {
                    [receipt.DefinitionId] = receipt.TransferredQuantity
                };
                RecordLootReceived(addedCounts, database, context.ExecutionContext, context.Action);
            }

            MarkContainerLootedIfEmpty(targetTags, context.ExecutionContext, context.Action);
        }

        void IGridStorageTransferEndpoint.OnTransferCommittedIn(
            GridStorageTransferReceipt receipt,
            GridStorageTransferContext context)
        {
            WorldObjectTags targetTags = context.ExecutionContext.Target != null
                ? context.ExecutionContext.Target
                : GetComponent<WorldObjectTags>();
            RestoreContainerContentState(targetTags);
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

        private bool TryPopulateStorage(LootTableDefinition lootTable, GameDatabase database, out string error)
        {
            error = null;
            if (lootTable.entries == null)
                return true;

            ItemStorage.StateSnapshot storageSnapshot = storage.CaptureState();
            using (ItemInstanceIdRegistry.ItemInstanceIdReservationScope identityScope =
                   ItemInstanceIdRegistry.Instance.BeginReservationScope())
            {
                try
                {
                    GridInventoryBackend backend = GetGridRuntime().Backend;
                    for (int entryIndex = 0; entryIndex < lootTable.entries.Length; entryIndex++)
                    {
                        LootTableEntryDefinition entry = lootTable.entries[entryIndex];
                        ItemDefinition definition = database.GetItem(entry.item_id);
                        InventoryMutationResult result = backend.Add(definition, entry.count);
                        if (!result.Success || result.AffectedQuantity != entry.count)
                        {
                            throw new InvalidOperationException(
                                $"entry[{entryIndex}] '{entry.item_id}' x{entry.count} failed: " +
                                (result.Message ?? result.Failure.ToString()));
                        }
                    }

                    ItemOwnedStorageRegistry.Instance.BindEntries(storage.Entries, this);
                    identityScope.Commit();
                    return true;
                }
                catch (Exception exception)
                {
                    storage.RestoreState(storageSnapshot);
                    error = exception.Message;
                    return false;
                }
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

        private static void MarkContainerSearched(WorldObjectTags targetTags, DebugActionExecutionContext executionContext, ActionDefinition action)
        {
            bool removedUnsearched = targetTags.RemoveTag(UnsearchedContainerTag);
            bool addedStorageAccessible = targetTags.AddTag(StorageAccessibleTag);

            Debug.Log(
                "[ContainerLootComponent] First container search completed." +
                $"\n  Target: {targetTags.name}" +
                $"\n  Runtime tags: {FormatTags(targetTags.RuntimeTags)}");

            RecordContainerSearched(executionContext, action, addedStorageAccessible, removedUnsearched);
        }

        private void MarkContainerLootedIfEmpty(WorldObjectTags targetTags, DebugActionExecutionContext executionContext, ActionDefinition action)
        {
            if (targetTags == null || !storage.IsEmpty)
                return;

            MarkContainerLooted(targetTags, executionContext, action);
        }

        private void RestoreContainerContentState(WorldObjectTags targetTags)
        {
            if (targetTags == null || storage.IsEmpty)
                return;

            bool removedLooted = targetTags.RemoveTag(LootedContainerTag);
            bool addedLootable = targetTags.AddTag(LootableContainerTag);
            if (!removedLooted && !addedLootable)
                return;

            Debug.Log(
                "[ContainerLootComponent] Container content state restored after deposit." +
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

        private static void RecordContainerSearched(DebugActionExecutionContext executionContext, ActionDefinition action, bool addedStorageAccessible, bool removedUnsearched)
        {
            if (!addedStorageAccessible && !removedUnsearched)
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
                addedTags: addedStorageAccessible ? new[] { StorageAccessibleTag } : null,
                removedTags: removedUnsearched ? new[] { UnsearchedContainerTag } : null,
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

        private void LogStorageInitializationFailure(
            string failureCode,
            bool databaseReady,
            bool lootTableFound,
            string failure,
            string actionTaken)
        {
            PersistentSceneObjectId persistentIdentity = GetComponent<PersistentSceneObjectId>();
            Debug.LogError(
                "[ContainerLootComponent][INITIALIZATION_FAILED]" +
                $"\n  Operation: InitializeStorage\n  Scene: {DiagnosticText(gameObject.scene.name)}\n  Container: {name}" +
                $"\n  PersistentSceneObjectId: {DiagnosticText(persistentIdentity != null ? persistentIdentity.PersistentId : null)}" +
                $"\n  LootTable: {DiagnosticText(lootTableId)}\n  DatabaseReady: {databaseReady}\n  LootTableFound: {lootTableFound}" +
                $"\n  MutationCommitted: false\n  RollbackAttempted: {failureCode == "StoragePopulationFailed"}" +
                $"\n  RollbackSucceeded: {failureCode == "StoragePopulationFailed"}" +
                $"\n  FailureCode: {failureCode}\n  Failure: {DiagnosticText(failure)}" +
                $"\n  ActionTaken: {actionTaken}",
                this);
        }

        private static string DiagnosticText(string value)
        {
            return value == null ? "<NONE>" : string.IsNullOrWhiteSpace(value) ? "<EMPTY>" : value;
        }
    }
}
