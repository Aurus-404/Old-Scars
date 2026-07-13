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
        private int mutationVersion;

        public IReadOnlyList<ItemStorageEntry> Entries => entries;
        public bool IsEmpty => entries.Count == 0;
        public int EntryCount => entries.Count;
        internal int Version => mutationVersion;

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

            if (firstChangedEntry != null)
                mutationVersion++;

            return firstChangedEntry;
        }

        internal ItemStorageEntry AddItemAsSeparateEntry(ItemInstance item, int quantity)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            if (quantity < 1 || quantity > Math.Max(1, item.MaxStack))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantity),
                    quantity,
                    "Quantity must fit in one stack.");
            }

            if (GetEntryByInstanceId(item.InstanceId) != null)
                throw new InvalidOperationException($"Item instance '{item.InstanceId}' already exists in the target storage.");

            var entry = new ItemStorageEntry(item, quantity);
            entries.Add(entry);
            mutationVersion++;
            return entry;
        }

        internal bool TryAddQuantityToEntry(string instanceId, int quantity)
        {
            if (quantity < 1)
                return false;

            ItemStorageEntry entry = GetEntryByInstanceId(instanceId);
            if (entry == null || entry.AvailableStackSpace < quantity)
                return false;

            entry.AddQuantity(quantity);
            mutationVersion++;
            return true;
        }

        public ItemStorageEntry GetEntry(int index)
        {
            return IsIndexValid(index) ? entries[index] : null;
        }

        public ItemStorageEntry GetEntryByInstanceId(string instanceId)
        {
            int index = GetEntryIndexByInstanceId(instanceId);
            return index >= 0 ? entries[index] : null;
        }

        public int GetEntryIndexByInstanceId(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
                return -1;

            string trimmedInstanceId = instanceId.Trim();
            for (int index = 0; index < entries.Count; index++)
            {
                ItemStorageEntry entry = entries[index];
                ItemInstance item = entry != null ? entry.Item : null;
                if (item != null && item.InstanceId == trimmedInstanceId)
                    return index;
            }

            return -1;
        }

        public bool RemoveAt(int index)
        {
            if (!IsIndexValid(index))
                return false;

            entries.RemoveAt(index);
            mutationVersion++;
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
            mutationVersion++;
            return true;
        }

        public void Clear()
        {
            if (entries.Count == 0)
                return;

            entries.Clear();
            mutationVersion++;
        }

        internal StateSnapshot CaptureState()
        {
            var states = new EntryState[entries.Count];
            for (int index = 0; index < entries.Count; index++)
            {
                ItemStorageEntry entry = entries[index];
                states[index] = new EntryState(entry.Item, entry.Quantity);
            }

            return new StateSnapshot(states, mutationVersion);
        }

        internal void RestoreState(StateSnapshot snapshot)
        {
            entries.Clear();
            EntryState[] states = snapshot.Entries;
            if (states != null)
            {
                for (int index = 0; index < states.Length; index++)
                {
                    EntryState state = states[index];
                    if (state.Item != null && state.Quantity > 0)
                        entries.Add(new ItemStorageEntry(state.Item, state.Quantity));
                }
            }

            mutationVersion = snapshot.Version;
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
            bool splitsSourceEntry = transferredQuantity < entry.Quantity;
            bool fullyMergesIntoTarget = target.GetAvailableMergeCapacity(entry.Item) >= transferredQuantity;

            // A split needs a sibling only when the target must keep a separate
            // representative entry. Fully merged quantities can reuse the source instance safely.
            ItemInstance transferredItem = splitsSourceEntry && !fullyMergesIntoTarget
                ? entry.Item.CreateStackSibling()
                : entry.Item;

            target.AddItem(transferredItem, transferredQuantity);
            RemoveAt(index, transferredQuantity);
            return transferredQuantity;
        }

        private bool IsIndexValid(int index)
        {
            return index >= 0 && index < entries.Count;
        }

        private int GetAvailableMergeCapacity(ItemInstance item)
        {
            if (item == null || item.MaxStack <= 1)
                return 0;

            int totalCapacity = 0;
            for (int index = 0; index < entries.Count; index++)
            {
                ItemStorageEntry entry = entries[index];
                if (entry == null || entry.DefinitionId != item.DefinitionId)
                    continue;

                totalCapacity += entry.AvailableStackSpace;
            }

            return totalCapacity;
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

        internal readonly struct EntryState
        {
            public EntryState(ItemInstance item, int quantity)
            {
                Item = item;
                Quantity = quantity;
            }

            public readonly ItemInstance Item;
            public readonly int Quantity;
        }

        internal readonly struct StateSnapshot
        {
            public StateSnapshot(EntryState[] entries, int version)
            {
                Entries = entries;
                Version = version;
            }

            public readonly EntryState[] Entries;
            public readonly int Version;
        }
    }
}
