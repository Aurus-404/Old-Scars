using System;
using System.Collections.Generic;

namespace OldScars.Core.Items
{
    /// <summary>
    /// Runtime-only item storage shared by debug inventory and world
    /// containers. This is not save data, slots, weight, stack limits, or UI.
    /// </summary>
    public sealed class ItemStorage
    {
        private readonly List<ItemStorageEntry> entries = new List<ItemStorageEntry>();

        public IReadOnlyList<ItemStorageEntry> Entries => entries;
        public bool IsEmpty => entries.Count == 0;
        public int EntryCount => entries.Count;

        public int TotalQuantity
        {
            get
            {
                int total = 0;
                for (int index = 0; index < entries.Count; index++)
                    total += entries[index].Quantity;

                return total;
            }
        }

        public ItemStorageEntry AddItem(ItemInstance item, int quantity = 1)
        {
            var entry = new ItemStorageEntry(item, quantity);
            entries.Add(entry);
            return entry;
        }

        public ItemStorageEntry GetEntry(int index)
        {
            return IsIndexValid(index) ? entries[index] : null;
        }

        public bool RemoveAt(int index)
        {
            if (!IsIndexValid(index))
                return false;

            entries.RemoveAt(index);
            return true;
        }

        public bool RemoveAt(int index, int quantity)
        {
            if (!IsIndexValid(index) || quantity < 1)
                return false;

            ItemStorageEntry entry = entries[index];
            if (quantity >= entry.Quantity)
                return RemoveAt(index);

            entry.RemoveQuantity(quantity);
            return true;
        }

        public void Clear()
        {
            entries.Clear();
        }

        public int TransferAllTo(ItemStorage target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (ReferenceEquals(target, this))
                return 0;

            int transferredQuantity = 0;
            for (int index = 0; index < entries.Count; index++)
            {
                ItemStorageEntry entry = entries[index];
                target.AddItem(entry.Item, entry.Quantity);
                transferredQuantity += entry.Quantity;
            }

            Clear();
            return transferredQuantity;
        }

        private bool IsIndexValid(int index)
        {
            return index >= 0 && index < entries.Count;
        }
    }
}
