using System.Collections.Generic;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using UnityEngine;

namespace OldScars.Core.Items
{
    /// <summary>
    /// Temporary debug inventory for Milestone 12.
    ///
    /// This is not the final inventory or equipment system. It only creates a
    /// small runtime list of item instances so interaction tests can stop
    /// depending directly on a flat equipped item definition id.
    /// </summary>
    public sealed class DebugInventory : MonoBehaviour
    {
        private const string NoItemId = "none";

        [SerializeField] private string[] initialItemDefinitionIds = { "rusted_crowbar_01" };
        [SerializeField] private int equippedItemIndex = 0;

        private readonly List<ItemInstance> itemInstances = new List<ItemInstance>();
        private bool isInitialized;

        public ItemInstance GetEquippedItemInstance()
        {
            EnsureInitialized();

            if (!IsEquippedItemIndexValid())
                return null;

            return itemInstances[equippedItemIndex];
        }

        public string GetEquippedItemDefinitionId()
        {
            ItemInstance equippedItem = GetEquippedItemInstance();
            return equippedItem != null ? equippedItem.DefinitionId : null;
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (isInitialized)
                return;

            if (GameDataManager.Instance == null)
            {
                Debug.LogWarning("[DebugInventory] GameDataManager.Instance was not found. Runtime debug inventory will retry later.");
                return;
            }

            if (!GameDataManager.Instance.IsReady)
            {
                Debug.LogWarning("[DebugInventory] GameDataManager is not ready. Runtime debug inventory will retry later.");
                return;
            }

            BuildRuntimeItems(GameDataManager.Instance.Database);
            isInitialized = true;
            LogInitialState();
        }

        private void BuildRuntimeItems(GameDatabase database)
        {
            itemInstances.Clear();

            if (initialItemDefinitionIds == null)
                return;

            for (int index = 0; index < initialItemDefinitionIds.Length; index++)
            {
                string definitionId = NormalizeItemId(initialItemDefinitionIds[index]);

                if (IsNoItemId(definitionId))
                {
                    itemInstances.Add(null);
                    continue;
                }

                ItemDefinition definition = database != null ? database.GetItem(definitionId) : null;
                if (definition == null)
                {
                    Debug.LogWarning($"[DebugInventory] Item definition '{definitionId}' was not found. Slot {index} will be empty.");
                    itemInstances.Add(null);
                    continue;
                }

                itemInstances.Add(new ItemInstance(definition));
            }
        }

        private bool IsEquippedItemIndexValid()
        {
            return equippedItemIndex >= 0 && equippedItemIndex < itemInstances.Count;
        }

        private void LogInitialState()
        {
            Debug.Log(
                "[DebugInventory] Built runtime debug inventory." +
                $"\n  Items: {FormatItems()}" +
                $"\n  Equipped index: {equippedItemIndex}" +
                $"\n  Equipped item: {FormatItem(GetEquippedItemInstance())}");
        }

        private string FormatItems()
        {
            if (itemInstances.Count == 0)
                return "(empty)";

            var parts = new List<string>();

            for (int index = 0; index < itemInstances.Count; index++)
            {
                parts.Add($"{index}: {FormatItem(itemInstances[index])}");
            }

            return string.Join(", ", parts);
        }

        private static string FormatItem(ItemInstance item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.DefinitionId))
                return "(none)";

            return $"{item.DefinitionId} [{item.InstanceId}] condition {item.Condition}";
        }

        private static string NormalizeItemId(string itemId)
        {
            return string.IsNullOrWhiteSpace(itemId) ? null : itemId.Trim();
        }

        private static bool IsNoItemId(string itemId)
        {
            return string.IsNullOrWhiteSpace(itemId) || itemId.ToLowerInvariant() == NoItemId;
        }
    }
}
