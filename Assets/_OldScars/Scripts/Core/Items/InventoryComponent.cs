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
    /// data, capacity, stacks, slots, drag/drop, pickup/drop rules, or UI.
    /// </summary>
    public sealed class InventoryComponent : MonoBehaviour
    {
        private const string NoItemId = "none";

        [SerializeField] private int equippedItemIndex = -1;

        private readonly List<ItemInstance> itemInstances = new List<ItemInstance>();

        public int EquippedItemIndex => equippedItemIndex;

        public ItemInstance AddItemByDefinitionId(string definitionId)
        {
            string normalizedDefinitionId = NormalizeItemId(definitionId);
            if (IsNoItemId(normalizedDefinitionId))
            {
                Debug.LogWarning("[InventoryComponent] Cannot add an empty item definition id.");
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
            itemInstances.Add(instance);

            Debug.Log(
                "[InventoryComponent] Added runtime item instance." +
                $"\n  Definition: {instance.DefinitionId}" +
                $"\n  Instance: {instance.InstanceId}" +
                $"\n  Condition: {instance.Condition}");

            return instance;
        }

        public IReadOnlyList<ItemInstance> GetItems()
        {
            return itemInstances;
        }

        public ItemInstance GetEquippedItemInstance()
        {
            if (!IsEquippedItemIndexValid())
                return null;

            return itemInstances[equippedItemIndex];
        }

        public string GetEquippedItemDefinitionId()
        {
            ItemInstance equippedItem = GetEquippedItemInstance();
            return equippedItem != null ? equippedItem.DefinitionId : null;
        }

        public bool EquipIndex(int index)
        {
            if (index < 0 || index >= itemInstances.Count || itemInstances[index] == null)
            {
                Debug.LogWarning($"[InventoryComponent] Cannot equip invalid item index {index}.");
                return false;
            }

            equippedItemIndex = index;
            ItemInstance equippedItem = itemInstances[equippedItemIndex];
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
            return equippedItemIndex >= 0 && equippedItemIndex < itemInstances.Count;
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
