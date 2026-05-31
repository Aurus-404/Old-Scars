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
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            if (quantity < 1)
                throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be >= 1.");

            int remainingQuantity = quantity;
            int maxStack = Math.Max(1, item.MaxStack);
            ItemStorageEntry firstChangedEntry = null;

            if (maxStack > 1)
                MergeIntoExistingStacks(item, ref remainingQuantity, ref firstChangedEntry);

            bool hasUsedOriginalInstance = false;
            while (remainingQuantity > 0)
            {
                int stackQuantity = Math.Min(remainingQuantity, maxStack);
                ItemInstance stackItem = hasUsedOriginalInstance ? item.CreateStackSibling() : item;
                var entry = new ItemStorageEntry(stackItem, stackQuantity);
                entries.Add(entry);

                if (firstChangedEntry == null)
                    firstChangedEntry = entry;

                hasUsedOriginalInstance = true;
                remainingQuantity -= stackQuantity;
            }

            return firstChangedEntry;
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

        public int TransferTo(ItemStorage target, int index, int quantity)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (ReferenceEquals(target, this) || !IsIndexValid(index) || quantity < 1)
                return 0;

            ItemStorageEntry entry = entries[index];
            int transferredQuantity = Math.Min(quantity, entry.Quantity);
            ItemInstance transferredItem = transferredQuantity < entry.Quantity ? entry.Item.CreateStackSibling() : entry.Item;
            target.AddItem(transferredItem, transferredQuantity);
            RemoveAt(index, transferredQuantity);
            return transferredQuantity;
        }

        private bool IsIndexValid(int index)
        {
            return index >= 0 && index < entries.Count;
        }

        private void MergeIntoExistingStacks(ItemInstance item, ref int remainingQuantity, ref ItemStorageEntry firstChangedEntry)
        {
            if (item == null || remainingQuantity <= 0)
                return;

            for (int index = 0; index < entries.Count && remainingQuantity > 0; index++)
            {
                ItemStorageEntry entry = entries[index];
                if (entry == null || entry.DefinitionId != item.DefinitionId || entry.MaxStack <= 1 || entry.AvailableStackSpace <= 0)
                    continue;

                int acceptedQuantity = entry.AddQuantityUpTo(remainingQuantity);
                if (acceptedQuantity <= 0)
                    continue;

                if (firstChangedEntry == null)
                    firstChangedEntry = entry;

                remainingQuantity -= acceptedQuantity;
            }
        }
    }
}
