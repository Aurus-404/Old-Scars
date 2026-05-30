using System;

namespace OldScars.Core.Items
{
    /// <summary>
    /// Runtime-only storage entry for a representative item instance and a
    /// simple quantity. Quantity belongs to storage, not to ItemInstance.
    /// </summary>
    public sealed class ItemStorageEntry
    {
        public ItemInstance Item { get; }
        public int Quantity { get; private set; }
        public string DefinitionId => Item != null ? Item.DefinitionId : null;

        public ItemStorageEntry(ItemInstance item, int quantity)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            if (quantity < 1)
                throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be >= 1.");

            Item = item;
            Quantity = quantity;
        }

        internal void AddQuantity(int quantity)
        {
            if (quantity < 1)
                throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be >= 1.");

            Quantity += quantity;
        }

        internal void RemoveQuantity(int quantity)
        {
            if (quantity < 1)
                throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be >= 1.");

            if (quantity >= Quantity)
                throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity removal must leave the entry quantity >= 1.");

            Quantity -= quantity;
        }
    }
}
