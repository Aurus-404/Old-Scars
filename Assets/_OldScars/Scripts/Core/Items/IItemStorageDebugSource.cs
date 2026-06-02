using System.Collections.Generic;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Interactions;

namespace OldScars.Core.Items
{
    public interface IItemStorageDebugSource
    {
        bool HasStoredItems { get; }
        IReadOnlyList<ItemStorageEntry> StorageEntries { get; }

        string GetStorageDebugTitle(WorldObjectTags target);
        int TakeItem(int storageIndex, int quantity, InventoryComponent targetInventory, DebugActionExecutionContext executionContext, ActionDefinition action, out string message);
    }
}
