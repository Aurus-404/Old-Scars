using System;
using System.Collections.Generic;

namespace OldScars.Core.Items
{
    /// <summary>
    /// Runtime-session identity and ownership index for item-owned storages.
    /// Identity is always an ItemInstance.InstanceId; DefinitionId is never used.
    /// </summary>
    public sealed class ItemOwnedStorageRegistry : IItemOwnedStorageResolver
    {
        private static readonly ItemOwnedStorageRegistry instance = new ItemOwnedStorageRegistry();

        private readonly Dictionary<string, ItemOwnedStorageRuntime> storagesByContainerInstanceId =
            new Dictionary<string, ItemOwnedStorageRuntime>();
        private readonly Dictionary<string, object> directOwnersByInstanceId =
            new Dictionary<string, object>();

        public static ItemOwnedStorageRegistry Instance => instance;

        private ItemOwnedStorageRegistry()
        {
        }

        public bool TryResolveOwnedStorage(string containerInstanceId, out ItemOwnedStorageRuntime storage)
        {
            storage = null;
            if (string.IsNullOrWhiteSpace(containerInstanceId))
                return false;

            return storagesByContainerInstanceId.TryGetValue(containerInstanceId, out storage) && storage != null;
        }

        public bool TryResolveRootOwner(string instanceId, out object rootOwner, out string error)
        {
            rootOwner = null;
            error = null;
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                error = "Cannot resolve an item owner without an InstanceId.";
                return false;
            }

            string currentInstanceId = instanceId;
            var visited = new HashSet<string>();
            while (true)
            {
                if (!visited.Add(currentInstanceId))
                {
                    error = $"Item-owned storage cycle detected at '{currentInstanceId}'.";
                    return false;
                }

                if (!directOwnersByInstanceId.TryGetValue(currentInstanceId, out object directOwner) || directOwner == null)
                {
                    error = $"Runtime owner for item instance '{currentInstanceId}' is not registered.";
                    return false;
                }

                if (directOwner is ItemOwnedStorageRuntime itemStorage)
                {
                    currentInstanceId = itemStorage.ContainerInstanceId;
                    continue;
                }

                rootOwner = directOwner;
                return true;
            }
        }

        internal void RegisterStorage(ItemOwnedStorageRuntime storage)
        {
            if (storage == null || string.IsNullOrWhiteSpace(storage.ContainerInstanceId))
                throw new ArgumentException("Item-owned storage requires a container InstanceId.", nameof(storage));

            storagesByContainerInstanceId[storage.ContainerInstanceId] = storage;
        }

        internal void BindItem(ItemInstance item, object directOwner)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.InstanceId) || directOwner == null)
                return;

            directOwnersByInstanceId[item.InstanceId] = directOwner;
        }

        internal void BindEntries(IReadOnlyList<ItemStorageEntry> entries, object directOwner)
        {
            if (entries == null || directOwner == null)
                return;

            for (int index = 0; index < entries.Count; index++)
            {
                ItemInstance item = entries[index] != null ? entries[index].Item : null;
                BindItem(item, directOwner);
            }
        }

        internal void UnbindItem(string instanceId)
        {
            if (!string.IsNullOrWhiteSpace(instanceId))
                directOwnersByInstanceId.Remove(instanceId);
        }

        internal object ResolveRootOwner(IGridStorageOwner owner)
        {
            if (owner is ItemOwnedStorageRuntime itemStorage &&
                TryResolveRootOwner(itemStorage.ContainerInstanceId, out object rootOwner, out _))
            {
                return rootOwner;
            }

            return owner;
        }

        internal bool ShareRootOwner(IGridStorageOwner first, IGridStorageOwner second)
        {
            object firstRoot = ResolveRootOwner(first);
            object secondRoot = ResolveRootOwner(second);
            return firstRoot != null && ReferenceEquals(firstRoot, secondRoot);
        }

        internal void ReconcileCommittedTransfer(
            IGridStorageOwner source,
            IGridStorageOwner target,
            GridStorageTransferReceipt receipt)
        {
            if (source != null)
                BindEntries(source.GridStorageEntries, source);
            if (target != null)
                BindEntries(target.GridStorageEntries, target);

            string sourceInstanceId = receipt.SourceInstanceId;
            if (!receipt.SourceWasRemoved || string.IsNullOrWhiteSpace(sourceInstanceId))
                return;

            bool stillExists = ContainsInstance(source, sourceInstanceId) || ContainsInstance(target, sourceInstanceId);
            if (!stillExists)
                directOwnersByInstanceId.Remove(sourceInstanceId);
        }

        private static bool ContainsInstance(IGridStorageOwner owner, string instanceId)
        {
            return owner != null && owner.TryGetEntryByInstanceId(instanceId, out _, out ItemStorageEntry entry) &&
                   entry != null && entry.Item != null;
        }
    }
}
