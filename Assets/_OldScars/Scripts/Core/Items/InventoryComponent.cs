using System.Collections.Generic;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Feedback;
using UnityEngine;

namespace OldScars.Core.Items
{
    /// <summary>
    /// Runtime-only inventory v0 for the playable debug loop.
    ///
    /// This is not the final inventory or equipment system. The optional grid
    /// backend adds debug spatial capacity only; there is no save data,
    /// final equipment model, pickup/drop rules, or final UI. M33.1 exposes
    /// closed placement movement for the temporary OnGUI drag interface.
    /// </summary>
    public sealed class InventoryComponent : MonoBehaviour, IGridStorageOwner, IGridStorageTransferEndpoint
    {
        private const string NoItemId = "none";
        public const string RightHandSlotId = "right_hand";

        [SerializeField] private string rightHandItemInstanceId;
        [SerializeField] private bool useGridLayout;
        [SerializeField] private int gridWidth = 6;
        [SerializeField] private int gridHeight = 8;

        private readonly ItemStorage storage = new ItemStorage();
        private readonly List<ItemInstance> itemInstancesView = new List<ItemInstance>();
        private GridStorageRuntime gridStorageRuntime;

        public string RightHandItemInstanceId => rightHandItemInstanceId;
        public int EquippedItemIndex => GetRightHandItemIndex();
        public IReadOnlyList<ItemStorageEntry> Entries => storage.Entries;
        public bool IsEmpty => storage.IsEmpty;
        public bool UsesGridLayout => GetGridBackend().UsesGridLayout;
        public int GridWidth => GetGridBackend().GridWidth;
        public int GridHeight => GetGridBackend().GridHeight;
        public int ConfiguredGridWidth => gridWidth;
        public int ConfiguredGridHeight => gridHeight;
        public GridStorageInitializationState GridInitializationState => GetGridRuntime().InitializationState;
        public string GridInitializationError => GetGridRuntime().InitializationError;
        public string GridStorageDisplayName => name;
        public IReadOnlyList<ItemStorageEntry> GridStorageEntries => storage.Entries;

        GridInventoryBackend IGridStorageTransferEndpoint.TransferBackend => GetGridBackend();

        private void Awake()
        {
            InitializeGridBackend();
        }

        public ItemInstance AddItemByDefinitionId(string definitionId)
        {
            return AddItemByDefinitionId(definitionId, 1);
        }

        public ItemInstance AddItemByDefinitionId(string definitionId, int quantity)
        {
            string normalizedDefinitionId = NormalizeItemId(definitionId);
            if (IsNoItemId(normalizedDefinitionId))
            {
                Debug.LogWarning("[InventoryComponent] Cannot add an empty item definition id.");
                return null;
            }

            if (quantity < 1)
            {
                Debug.LogWarning($"[InventoryComponent] Cannot add '{normalizedDefinitionId}' with quantity {quantity}. Quantity must be >= 1.");
                return null;
            }

            if (GameDataManager.Instance == null)
            {
                Debug.LogWarning($"[InventoryComponent] Cannot add '{normalizedDefinitionId}' because GameDataManager.Instance was not found.");
                return null;
            }

            if (!GameDataManager.Instance.IsReady)
            {
                Debug.LogWarning($"[InventoryComponent] Cannot add '{normalizedDefinitionId}' because GameDataManager is not ready.");
                return null;
            }

            GameDatabase database = GameDataManager.Instance.Database;
            ItemDefinition definition = database != null ? database.GetItem(normalizedDefinitionId) : null;
            if (definition == null)
            {
                Debug.LogWarning($"[InventoryComponent] Item definition '{normalizedDefinitionId}' was not found.");
                return null;
            }

            InventoryMutationResult result = GetGridBackend().Add(definition, quantity);
            if (!result.Success)
            {
                Debug.LogWarning($"[InventoryComponent] Cannot add '{normalizedDefinitionId}' x{quantity}: {SafeText(result.Message)}");
                return null;
            }

            ItemStorageEntry addedEntry = storage.GetEntryByInstanceId(result.DestinationInstanceId);
            ItemInstance storedItem = addedEntry != null ? addedEntry.Item : null;
            if (storedItem == null)
            {
                Debug.LogError($"[InventoryComponent] Add succeeded but destination instance '{SafeText(result.DestinationInstanceId)}' was not found.");
                return null;
            }

            Debug.Log(
                "[InventoryComponent] Added runtime item instance." +
                $"\n  Definition: {storedItem.DefinitionId}" +
                $"\n  Instance: {storedItem.InstanceId}" +
                $"\n  Condition: {storedItem.Condition}" +
                $"\n  Quantity: {quantity}");

            return storedItem;
        }

        public void BeginInitialContentLoad()
        {
            GetGridRuntime().BeginInitialContentLoad();
        }

        public bool CompleteInitialContentLoad()
        {
            bool initialized = GetGridRuntime().CompleteInitialContentLoad(out string error);
            if (!initialized)
            {
                Debug.LogError(
                    "[InventoryComponent] Grid layout initialization failed after initial content load; " +
                    "inventory remains linear and no items were changed." +
                    $"\n  Actor: {name}" +
                    $"\n  Requested grid: {gridWidth}x{gridHeight}" +
                    $"\n  Reason: {SafeText(error)}");
            }

            return initialized;
        }

        public IReadOnlyList<ItemInstance> GetItems()
        {
            itemInstancesView.Clear();
            IReadOnlyList<ItemStorageEntry> entries = storage.Entries;
            for (int index = 0; index < entries.Count; index++)
                itemInstancesView.Add(entries[index].Item);

            return itemInstancesView;
        }

        public IReadOnlyList<ItemStorageEntry> GetStorageEntries()
        {
            return storage.Entries;
        }

        public ItemStorageEntry GetEntry(int index)
        {
            return storage.GetEntry(index);
        }

        public bool TryGetEntryByInstanceId(string instanceId, out int index, out ItemStorageEntry entry)
        {
            index = storage.GetEntryIndexByInstanceId(instanceId);
            entry = index >= 0 ? storage.GetEntry(index) : null;
            return entry != null && entry.Item != null;
        }

        public bool TryRemoveItemAt(int index, int quantity)
        {
            ItemStorageEntry entry = storage.GetEntry(index);
            if (entry == null || quantity < 1)
            {
                Debug.LogWarning($"[InventoryComponent] Cannot remove quantity {quantity} from invalid item index {index}.");
                return false;
            }

            string sourceInstanceId = entry.Item != null ? entry.Item.InstanceId : null;
            InventoryMutationResult result = GetGridBackend().Remove(sourceInstanceId, quantity);
            if (!result.Success)
            {
                Debug.LogWarning($"[InventoryComponent] Failed to remove quantity {quantity} from item index {index}: {SafeText(result.Message)}");
                return false;
            }

            if (ContainsInstanceId(result.RemovedInstanceIds, sourceInstanceId))
                ClearRightHandIfInstanceId(sourceInstanceId);

            return true;
        }

        public int TransferItemsFrom(ItemStorage source)
        {
            if (source == null)
            {
                Debug.LogWarning("[InventoryComponent] Cannot transfer items from a null storage.");
                return 0;
            }

            if (UsesGridLayout)
            {
                Debug.LogWarning("[InventoryComponent] Batch transfer is pending for grid inventory. Use individual or stack transfers.");
                return 0;
            }

            return source.TransferAllTo(storage);
        }

        public int TransferItemFrom(ItemStorage source, int sourceIndex, int quantity)
        {
            if (source == null)
            {
                Debug.LogWarning("[InventoryComponent] Cannot transfer an item from a null storage.");
                return 0;
            }

            if (quantity < 1)
            {
                Debug.LogWarning($"[InventoryComponent] Cannot transfer quantity {quantity}. Quantity must be >= 1.");
                return 0;
            }

            ItemStorageEntry sourceEntry = source.GetEntry(sourceIndex);
            ItemInstance sourceItem = sourceEntry != null ? sourceEntry.Item : null;
            if (sourceItem == null)
            {
                Debug.LogWarning($"[InventoryComponent] Cannot transfer an invalid source item index {sourceIndex}.");
                return 0;
            }

            InventoryMutationResult result = GetGridBackend().TransferFrom(source, sourceItem.InstanceId, quantity);
            if (!result.Success)
            {
                Debug.LogWarning($"[InventoryComponent] Transfer from storage failed: {SafeText(result.Message)}");
                return 0;
            }

            return result.AffectedQuantity;
        }

        public int TransferItemTo(ItemStorage targetStorage, int sourceIndex, int quantity)
        {
            if (targetStorage == null)
            {
                Debug.LogWarning("[InventoryComponent] Cannot transfer an item to a null storage.");
                return 0;
            }

            if (ReferenceEquals(targetStorage, storage))
            {
                Debug.LogWarning("[InventoryComponent] Cannot transfer an item to the same storage.");
                return 0;
            }

            if (quantity < 1)
            {
                Debug.LogWarning($"[InventoryComponent] Cannot transfer quantity {quantity}. Quantity must be >= 1.");
                return 0;
            }

            ItemStorageEntry sourceEntry = storage.GetEntry(sourceIndex);
            if (sourceEntry == null || sourceEntry.Item == null)
            {
                Debug.LogWarning($"[InventoryComponent] Cannot transfer an invalid item index {sourceIndex}.");
                return 0;
            }

            string sourceInstanceId = sourceEntry.Item.InstanceId;
            InventoryMutationResult result = GetGridBackend().TransferTo(targetStorage, sourceInstanceId, quantity);
            if (!result.Success)
            {
                Debug.LogWarning($"[InventoryComponent] Transfer to storage failed: {SafeText(result.Message)}");
                return 0;
            }

            if (ContainsInstanceId(result.RemovedInstanceIds, sourceInstanceId))
                ClearRightHandIfInstanceId(sourceInstanceId);

            return result.AffectedQuantity;
        }

        public int TransferItemTo(InventoryComponent targetInventory, int sourceIndex, int quantity)
        {
            if (targetInventory == null)
            {
                Debug.LogWarning("[InventoryComponent] Cannot transfer an item to a null inventory.");
                return 0;
            }

            if (ReferenceEquals(targetInventory, this))
            {
                Debug.LogWarning("[InventoryComponent] Cannot transfer an item to the same inventory.");
                return 0;
            }

            if (quantity < 1)
            {
                Debug.LogWarning($"[InventoryComponent] Cannot transfer quantity {quantity}. Quantity must be >= 1.");
                return 0;
            }

            ItemStorageEntry sourceEntry = storage.GetEntry(sourceIndex);
            if (sourceEntry == null || sourceEntry.Item == null)
            {
                Debug.LogWarning($"[InventoryComponent] Cannot transfer an invalid item index {sourceIndex}.");
                return 0;
            }

            string sourceInstanceId = sourceEntry.Item.InstanceId;
            InventoryMutationResult result = GetGridBackend().TransferTo(targetInventory.GetGridBackend(), sourceInstanceId, quantity);
            if (!result.Success)
            {
                Debug.LogWarning($"[InventoryComponent] Inventory-to-inventory transfer failed: {SafeText(result.Message)}");
                return 0;
            }

            if (ContainsInstanceId(result.RemovedInstanceIds, sourceInstanceId))
                ClearRightHandIfInstanceId(sourceInstanceId);

            return result.AffectedQuantity;
        }

        public bool TryGetGridPlacement(string instanceId, out GridPlacement placement)
        {
            return GetGridBackend().TryGetPlacement(instanceId, out placement);
        }

        public bool TryGetGridFootprint(string definitionId, out GridFootprint footprint, out bool usedFallback)
        {
            return GetGridBackend().TryResolveFootprint(definitionId, out footprint, out usedFallback, out _);
        }

        public GridPlacementValidationResult PreviewGridPlacementMove(
            string instanceId,
            int x,
            int y,
            bool isRotated)
        {
            return GetGridBackend().PreviewMovePlacement(instanceId, x, y, isRotated);
        }

        public InventoryMutationResult MoveGridPlacement(
            string instanceId,
            int x,
            int y,
            bool isRotated)
        {
            InventoryMutationResult result = GetGridBackend().MovePlacement(instanceId, x, y, isRotated);
            if (!result.Success)
            {
                Debug.LogWarning(
                    $"[InventoryComponent] Grid placement move failed for '{SafeText(instanceId)}': {SafeText(result.Message)}");
            }

            return result;
        }

        public ItemInstance GetEquippedItemInstance()
        {
            return GetRightHandItemInstance();
        }

        public ItemStorageEntry GetEquippedStorageEntry()
        {
            return GetRightHandStorageEntry();
        }

        public string GetEquippedItemDefinitionId()
        {
            return GetRightHandItemDefinitionId();
        }

        public ItemInstance GetRightHandItemInstance()
        {
            ItemStorageEntry entry = GetRightHandStorageEntry();
            return entry != null ? entry.Item : null;
        }

        public ItemStorageEntry GetRightHandStorageEntry()
        {
            if (IsNoItemId(rightHandItemInstanceId))
                return null;

            ItemStorageEntry entry = storage.GetEntryByInstanceId(rightHandItemInstanceId);
            if (entry == null)
                rightHandItemInstanceId = null;

            return entry;
        }

        public string GetRightHandItemDefinitionId()
        {
            ItemInstance item = GetRightHandItemInstance();
            return item != null ? item.DefinitionId : null;
        }

        public int GetRightHandItemIndex()
        {
            if (IsNoItemId(rightHandItemInstanceId))
                return -1;

            int index = storage.GetEntryIndexByInstanceId(rightHandItemInstanceId);
            if (index < 0)
                rightHandItemInstanceId = null;

            return index;
        }

        public bool IsRightHandEquippedIndex(int index)
        {
            return IsRightHandStorageEntry(storage.GetEntry(index));
        }

        public bool IsRightHandStorageEntry(ItemStorageEntry entry)
        {
            ItemInstance item = entry != null ? entry.Item : null;
            return item != null && !IsNoItemId(rightHandItemInstanceId) && item.InstanceId == rightHandItemInstanceId;
        }

        public bool IsInstanceEquipped(string instanceId)
        {
            return !IsNoItemId(instanceId) && instanceId == rightHandItemInstanceId;
        }

        public bool CanEquipIndexToRightHand(int index)
        {
            return CanEquipIndexToSlot(index, RightHandSlotId, out _);
        }

        public bool EquipIndex(int index)
        {
            return TryEquipIndexToRightHand(index);
        }

        public bool TryEquipIndexToRightHand(int index)
        {
            return TryEquipIndexToSlot(index, RightHandSlotId);
        }

        public void Unequip()
        {
            UnequipRightHand();
        }

        public void UnequipRightHand()
        {
            ItemInstance unequippedItem = GetRightHandItemInstance();
            rightHandItemInstanceId = null;
            Debug.Log("[InventoryComponent] Right hand slot cleared.");

            if (unequippedItem != null)
                RecordItemUnequipped(unequippedItem);
        }

        private bool TryEquipIndexToSlot(int index, string slotId)
        {
            if (!CanEquipIndexToSlot(index, slotId, out string reason))
            {
                Debug.LogWarning($"[InventoryComponent] Cannot equip item index {index} to '{SafeText(slotId)}': {reason}");
                return false;
            }

            ItemStorageEntry entry = storage.GetEntry(index);
            ItemInstance item = entry.Item;
            if (item.InstanceId == rightHandItemInstanceId)
                return true;

            ItemInstance previousItem = GetRightHandItemInstance();
            rightHandItemInstanceId = item.InstanceId;

            Debug.Log($"[InventoryComponent] Equipped item {item.DefinitionId} [{item.InstanceId}] to {slotId}.");

            if (previousItem != null)
                RecordItemUnequipped(previousItem);

            RecordItemEquipped(item);
            return true;
        }

        private bool CanEquipIndexToSlot(int index, string slotId, out string reason)
        {
            reason = null;

            if (slotId != RightHandSlotId)
            {
                reason = $"slot '{SafeText(slotId)}' is not supported in Milestone 23.";
                return false;
            }

            ItemStorageEntry entry = storage.GetEntry(index);
            if (entry == null || entry.Item == null)
            {
                reason = "invalid storage entry.";
                return false;
            }

            if (entry.Quantity > 1)
            {
                reason = "stacked entries cannot be equipped in Milestone 23.";
                return false;
            }

            ItemDefinition definition = GetItemDefinition(entry.DefinitionId);
            if (definition == null)
            {
                reason = $"item definition '{SafeText(entry.DefinitionId)}' was not found.";
                return false;
            }

            if (!IsDefinitionEquipEnabled(definition))
            {
                reason = $"item '{SafeText(entry.DefinitionId)}' is not equipable.";
                return false;
            }

            if (definition.equip == null)
            {
                reason = $"item '{SafeText(entry.DefinitionId)}' has no equip block.";
                return false;
            }

            if (!ContainsSlot(definition.equip.allowed_slots, slotId))
            {
                reason = $"item '{SafeText(entry.DefinitionId)}' is not allowed in slot '{slotId}'.";
                return false;
            }

            if (!ContainsSlot(definition.equip.occupied_slots, slotId))
            {
                reason = $"item '{SafeText(entry.DefinitionId)}' does not occupy slot '{slotId}'.";
                return false;
            }

            return true;
        }

        private void ClearRightHandIfInstanceId(string instanceId)
        {
            if (IsNoItemId(instanceId) || instanceId != rightHandItemInstanceId)
                return;

            rightHandItemInstanceId = null;
        }

        private GridInventoryBackend GetGridBackend()
        {
            return GetGridRuntime().Backend;
        }

        private GridStorageRuntime GetGridRuntime()
        {
            if (gridStorageRuntime == null)
                InitializeGridBackend();

            return gridStorageRuntime;
        }

        private void InitializeGridBackend()
        {
            if (gridStorageRuntime != null)
                return;

            gridStorageRuntime = new GridStorageRuntime(
                storage,
                GetItemDefinition,
                useGridLayout,
                gridWidth,
                gridHeight,
                true);
            if (gridStorageRuntime.InitializationState != GridStorageInitializationState.LinearFallback)
                return;

            Debug.LogError(
                "[InventoryComponent] Grid layout initialization failed; inventory remains linear and no items were changed." +
                $"\n  Actor: {name}" +
                $"\n  Requested grid: {gridWidth}x{gridHeight}" +
                $"\n  Reason: {SafeText(gridStorageRuntime.InitializationError)}");
        }

        bool IGridStorageTransferEndpoint.CanTransferOut(GridStorageTransferContext context, out string reason)
        {
            reason = null;
            return true;
        }

        bool IGridStorageTransferEndpoint.CanTransferIn(GridStorageTransferContext context, out string reason)
        {
            reason = null;
            return true;
        }

        void IGridStorageTransferEndpoint.OnTransferCommittedOut(
            GridStorageTransferReceipt receipt,
            GridStorageTransferContext context)
        {
            if (receipt.Result != null && receipt.Result.Success && receipt.SourceWasRemoved)
                ClearRightHandIfInstanceId(receipt.SourceInstanceId);
        }

        void IGridStorageTransferEndpoint.OnTransferCommittedIn(
            GridStorageTransferReceipt receipt,
            GridStorageTransferContext context)
        {
        }

        private static bool ContainsInstanceId(string[] instanceIds, string expected)
        {
            if (instanceIds == null || string.IsNullOrWhiteSpace(expected))
                return false;

            for (int index = 0; index < instanceIds.Length; index++)
            {
                if (instanceIds[index] == expected)
                    return true;
            }

            return false;
        }

        private static string NormalizeItemId(string itemId)
        {
            return string.IsNullOrWhiteSpace(itemId) ? null : itemId.Trim();
        }

        private static bool IsNoItemId(string itemId)
        {
            return string.IsNullOrWhiteSpace(itemId) || itemId.ToLowerInvariant() == NoItemId;
        }

        private static ItemDefinition GetItemDefinition(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
                return null;

            if (GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
                return null;

            GameDatabase database = GameDataManager.Instance.Database;
            return database != null ? database.GetItem(definitionId) : null;
        }

        private static bool IsDefinitionEquipEnabled(ItemDefinition definition)
        {
            if (definition == null)
                return false;

            if (definition.equip != null && definition.equip.equippable.HasValue)
                return definition.equip.equippable.Value;

            return definition.equippable.GetValueOrDefault(false);
        }

        private static bool ContainsSlot(string[] slots, string slotId)
        {
            if (slots == null || string.IsNullOrWhiteSpace(slotId))
                return false;

            for (int index = 0; index < slots.Length; index++)
            {
                if (slots[index] == slotId)
                    return true;
            }

            return false;
        }

        private void RecordItemEquipped(ItemInstance item)
        {
            if (item == null)
                return;

            string displayName = GetItemDisplayName(item.DefinitionId);
            GameplayFeedbackLog.TryRecord(new GameplayFeedbackEntry(
                GameplayFeedbackEntryType.ItemEquipped,
                $"Equipaste {displayName}.",
                actorId: name,
                actorDisplayName: name,
                itemId: item.DefinitionId,
                itemDisplayName: displayName,
                quantity: 1));
        }

        private void RecordItemUnequipped(ItemInstance item)
        {
            if (item == null)
                return;

            string displayName = GetItemDisplayName(item.DefinitionId);
            GameplayFeedbackLog.TryRecord(new GameplayFeedbackEntry(
                GameplayFeedbackEntryType.ItemUnequipped,
                $"Desequipaste {displayName}.",
                actorId: name,
                actorDisplayName: name,
                itemId: item.DefinitionId,
                itemDisplayName: displayName,
                quantity: 1));
        }

        private static string GetItemDisplayName(string definitionId)
        {
            if (GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
                return SafeText(definitionId);

            GameDatabase database = GameDataManager.Instance.Database;
            ItemDefinition definition = database != null ? database.GetItem(definitionId) : null;
            if (definition == null || definition.display == null || string.IsNullOrWhiteSpace(definition.display.name))
                return SafeText(definitionId);

            return definition.display.name;
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
        }
    }
}
