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
        public const string RightHandSlotId = "right_hand";

        [SerializeField] private string rightHandItemInstanceId;

        private readonly ItemStorage storage = new ItemStorage();
        private readonly List<ItemInstance> itemInstancesView = new List<ItemInstance>();

        public string RightHandItemInstanceId => rightHandItemInstanceId;
        public int EquippedItemIndex => GetRightHandItemIndex();
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
            string removedInstanceId = removesEntry && entry.Item != null ? entry.Item.InstanceId : null;
            if (!storage.RemoveAt(index, quantity))
            {
                Debug.LogWarning($"[InventoryComponent] Failed to remove quantity {quantity} from item index {index}.");
                return false;
            }

            if (removesEntry)
                ClearRightHandIfInstanceId(removedInstanceId);

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
