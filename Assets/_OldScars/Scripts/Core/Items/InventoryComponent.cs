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
    /// This is not the final inventory or equipment system. It has no save
    /// data, capacity, slots, drag/drop, pickup/drop rules, or UI.
    /// </summary>
    public sealed class InventoryComponent : MonoBehaviour
    {
        private const string NoItemId = "none";

        [SerializeField] private int equippedItemIndex = -1;

        private readonly ItemStorage storage = new ItemStorage();
        private readonly List<ItemInstance> itemInstancesView = new List<ItemInstance>();

        public int EquippedItemIndex => equippedItemIndex;
        public IReadOnlyList<ItemStorageEntry> Entries => storage.Entries;
        public bool IsEmpty => storage.IsEmpty;

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

            var instance = new ItemInstance(definition);
            ItemStorageEntry addedEntry = storage.AddItem(instance, quantity);
            ItemInstance storedItem = addedEntry != null ? addedEntry.Item : instance;

            Debug.Log(
                "[InventoryComponent] Added runtime item instance." +
                $"\n  Definition: {storedItem.DefinitionId}" +
                $"\n  Instance: {storedItem.InstanceId}" +
                $"\n  Condition: {storedItem.Condition}" +
                $"\n  Quantity: {quantity}");

            return storedItem;
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

        public bool TryRemoveItemAt(int index, int quantity)
        {
            ItemStorageEntry entry = storage.GetEntry(index);
            if (entry == null || quantity < 1)
            {
                Debug.LogWarning($"[InventoryComponent] Cannot remove quantity {quantity} from invalid item index {index}.");
                return false;
            }

            bool removesEntry = quantity >= entry.Quantity;
            if (!storage.RemoveAt(index, quantity))
            {
                Debug.LogWarning($"[InventoryComponent] Failed to remove quantity {quantity} from item index {index}.");
                return false;
            }

            if (removesEntry)
                AdjustEquippedIndexAfterEntryRemoval(index);

            return true;
        }

        public int TransferItemsFrom(ItemStorage source)
        {
            if (source == null)
            {
                Debug.LogWarning("[InventoryComponent] Cannot transfer items from a null storage.");
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

            return source.TransferTo(storage, sourceIndex, quantity);
        }

        public ItemInstance GetEquippedItemInstance()
        {
            ItemStorageEntry equippedEntry = GetEquippedStorageEntry();
            return equippedEntry != null ? equippedEntry.Item : null;
        }

        public ItemStorageEntry GetEquippedStorageEntry()
        {
            return IsEquippedItemIndexValid() ? storage.GetEntry(equippedItemIndex) : null;
        }

        public string GetEquippedItemDefinitionId()
        {
            ItemInstance equippedItem = GetEquippedItemInstance();
            return equippedItem != null ? equippedItem.DefinitionId : null;
        }

        public bool EquipIndex(int index)
        {
            ItemStorageEntry entry = storage.GetEntry(index);
            if (entry == null || entry.Item == null)
            {
                Debug.LogWarning($"[InventoryComponent] Cannot equip invalid item index {index}.");
                return false;
            }

            equippedItemIndex = index;
            ItemInstance equippedItem = entry.Item;
            Debug.Log($"[InventoryComponent] Equipped item {equippedItem.DefinitionId} [{equippedItem.InstanceId}] at index {equippedItemIndex}.");
            RecordItemEquipped(equippedItem);
            return true;
        }

        public void Unequip()
        {
            ItemInstance unequippedItem = GetEquippedItemInstance();
            equippedItemIndex = -1;
            Debug.Log("[InventoryComponent] Equipped item cleared.");

            if (unequippedItem != null)
                RecordItemUnequipped(unequippedItem);
        }

        private bool IsEquippedItemIndexValid()
        {
            return equippedItemIndex >= 0 && equippedItemIndex < storage.EntryCount;
        }

        private void AdjustEquippedIndexAfterEntryRemoval(int removedIndex)
        {
            if (equippedItemIndex == removedIndex)
            {
                equippedItemIndex = -1;
            }
            else if (equippedItemIndex > removedIndex)
            {
                equippedItemIndex--;
            }
        }

        private static string NormalizeItemId(string itemId)
        {
            return string.IsNullOrWhiteSpace(itemId) ? null : itemId.Trim();
        }

        private static bool IsNoItemId(string itemId)
        {
            return string.IsNullOrWhiteSpace(itemId) || itemId.ToLowerInvariant() == NoItemId;
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
