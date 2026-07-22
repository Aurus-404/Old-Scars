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

            if (storagesByContainerInstanceId.ContainsKey(storage.ContainerInstanceId))
                throw new InvalidOperationException($"Item-owned storage '{storage.ContainerInstanceId}' is already registered.");

            storagesByContainerInstanceId.Add(storage.ContainerInstanceId, storage);
        }

        internal bool UnregisterStorage(string containerInstanceId, ItemOwnedStorageRuntime expectedStorage)
        {
            if (string.IsNullOrWhiteSpace(containerInstanceId) || expectedStorage == null)
                throw new ArgumentException("Item-owned storage unregister requires an id and expected runtime.");
            if (!storagesByContainerInstanceId.TryGetValue(containerInstanceId, out ItemOwnedStorageRuntime registered))
                return false;
            if (!ReferenceEquals(registered, expectedStorage))
                throw new InvalidOperationException($"Item-owned storage '{containerInstanceId}' does not match the expected runtime.");

            return storagesByContainerInstanceId.Remove(containerInstanceId);
        }

        internal void BindItem(ItemInstance item, object directOwner)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.InstanceId) || directOwner == null)
                throw new ArgumentException("Item ownership binding requires an item and direct owner.");

            if (directOwnersByInstanceId.TryGetValue(item.InstanceId, out object registeredOwner))
            {
                if (ReferenceEquals(registeredOwner, directOwner))
                    return;

                throw new InvalidOperationException($"Item instance '{item.InstanceId}' is already bound to a different owner.");
            }

            directOwnersByInstanceId.Add(item.InstanceId, directOwner);
        }

        internal void BindEntries(IReadOnlyList<ItemStorageEntry> entries, object directOwner)
        {
            if (entries == null || directOwner == null)
                return;

            for (int index = 0; index < entries.Count; index++)
            {
                ItemInstance item = entries[index] != null ? entries[index].Item : null;
                if (item != null)
                    BindItem(item, directOwner);
            }
        }

        internal void UnbindItem(string instanceId)
        {
            if (!string.IsNullOrWhiteSpace(instanceId))
                directOwnersByInstanceId.Remove(instanceId);
        }

        internal void RemoveRuntimeStateForInstance(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
                return;

            directOwnersByInstanceId.Remove(instanceId);
            if (storagesByContainerInstanceId.TryGetValue(instanceId, out ItemOwnedStorageRuntime storage))
                UnregisterStorage(instanceId, storage);
        }

        internal void ResetRuntimeSession()
        {
            directOwnersByInstanceId.Clear();
            storagesByContainerInstanceId.Clear();
        }

        internal int RegisteredStorageCount => storagesByContainerInstanceId.Count;
        internal int BoundItemCount => directOwnersByInstanceId.Count;

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
            string sourceInstanceId = receipt.SourceInstanceId;
            if (receipt.SourceWasRemoved && !string.IsNullOrWhiteSpace(sourceInstanceId))
                directOwnersByInstanceId.Remove(sourceInstanceId);

            if (source != null)
                BindEntries(source.GridStorageEntries, source);
            if (target != null)
                BindEntries(target.GridStorageEntries, target);

            if (receipt.SourceWasRemoved && !string.IsNullOrWhiteSpace(sourceInstanceId) &&
                !ContainsInstance(source, sourceInstanceId) && !ContainsInstance(target, sourceInstanceId))
            {
                directOwnersByInstanceId.Remove(sourceInstanceId);
            }
        }

        internal void ReconcileRestoredOwners(IGridStorageOwner first, IGridStorageOwner second)
        {
            UnbindEntries(first != null ? first.GridStorageEntries : null);
            UnbindEntries(second != null ? second.GridStorageEntries : null);
            if (first != null)
                BindEntries(first.GridStorageEntries, first);
            if (second != null)
                BindEntries(second.GridStorageEntries, second);
        }

        private void UnbindEntries(IReadOnlyList<ItemStorageEntry> entries)
        {
            if (entries == null)
                return;

            for (int index = 0; index < entries.Count; index++)
            {
                ItemInstance item = entries[index] != null ? entries[index].Item : null;
                if (item != null)
                    directOwnersByInstanceId.Remove(item.InstanceId);
            }
        }

        private static bool ContainsInstance(IGridStorageOwner owner, string instanceId)
        {
            return owner != null && owner.TryGetEntryByInstanceId(instanceId, out _, out ItemStorageEntry entry) &&
                   entry != null && entry.Item != null;
        }
    }
}
