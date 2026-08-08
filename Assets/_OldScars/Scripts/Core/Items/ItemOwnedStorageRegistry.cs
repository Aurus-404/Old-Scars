using System;
using System.Collections.Generic;

namespace OldScars.Core.Items
{
    internal interface IGridStorageDirectOwnerProvider
    {
        object DirectItemOwner { get; }
    }

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

                throw new InvalidOperationException(
                    $"OwnershipMismatch: item instance '{item.InstanceId}' is bound to " +
                    $"'{DescribeOwner(registeredOwner)}'; expected '{DescribeOwner(directOwner)}'.");
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

        internal bool TryGetDirectOwner(string instanceId, out object directOwner)
        {
            directOwner = null;
            return !string.IsNullOrWhiteSpace(instanceId) &&
                   directOwnersByInstanceId.TryGetValue(instanceId, out directOwner) &&
                   directOwner != null;
        }

        internal void ValidateBinding(string instanceId, object expectedOwner)
        {
            if (string.IsNullOrWhiteSpace(instanceId) || expectedOwner == null)
                throw new ArgumentException("Item ownership validation requires an InstanceId and expected owner.");

            if (!directOwnersByInstanceId.TryGetValue(instanceId, out object registeredOwner) || registeredOwner == null)
                throw new InvalidOperationException($"Item instance '{instanceId}' has no registered direct owner.");
            if (!ReferenceEquals(registeredOwner, expectedOwner))
                throw new InvalidOperationException(
                    $"OwnershipMismatch: item instance '{instanceId}' is bound to " +
                    $"'{DescribeOwner(registeredOwner)}'; expected '{DescribeOwner(expectedOwner)}'.");
        }

        internal void ValidateEntries(IReadOnlyList<ItemStorageEntry> entries, object expectedOwner)
        {
            if (entries == null || expectedOwner == null)
                return;

            for (int index = 0; index < entries.Count; index++)
            {
                ItemInstance item = entries[index]?.Item;
                if (item != null)
                    ValidateBinding(item.InstanceId, expectedOwner);
            }
        }

        internal void TransferBinding(string instanceId, object expectedSource, object target)
        {
            if (string.IsNullOrWhiteSpace(instanceId) || expectedSource == null || target == null)
                throw new ArgumentException("Item ownership transition requires an InstanceId, expected source and target.");
            if (ReferenceEquals(expectedSource, target))
            {
                ValidateBinding(instanceId, target);
                return;
            }

            if (!directOwnersByInstanceId.TryGetValue(instanceId, out object registeredOwner) || registeredOwner == null)
                throw new InvalidOperationException($"Item instance '{instanceId}' has no registered direct owner to transfer.");
            if (ReferenceEquals(registeredOwner, target))
                return;
            if (!ReferenceEquals(registeredOwner, expectedSource))
                throw new InvalidOperationException(
                    $"OwnershipTransitionFailed: item instance '{instanceId}' actual owner is " +
                    $"'{DescribeOwner(registeredOwner)}'; expected source '{DescribeOwner(expectedSource)}'; " +
                    $"target '{DescribeOwner(target)}'.");

            directOwnersByInstanceId[instanceId] = target;
        }

        internal void RemoveBinding(string instanceId, object expectedOwner)
        {
            if (string.IsNullOrWhiteSpace(instanceId) || expectedOwner == null)
                throw new ArgumentException("Item ownership cleanup requires an InstanceId and expected owner.");
            if (!directOwnersByInstanceId.TryGetValue(instanceId, out object registeredOwner) || registeredOwner == null)
                return;
            if (!ReferenceEquals(registeredOwner, expectedOwner))
                throw new InvalidOperationException(
                    $"OwnershipMismatch: item instance '{instanceId}' cannot be unbound from " +
                    $"'{DescribeOwner(expectedOwner)}'; actual owner is '{DescribeOwner(registeredOwner)}'.");

            directOwnersByInstanceId.Remove(instanceId);
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
            object directOwner = ResolveDirectOwner(owner);
            if (directOwner is ItemOwnedStorageRuntime itemStorage &&
                TryResolveRootOwner(itemStorage.ContainerInstanceId, out object rootOwner, out _))
            {
                return rootOwner;
            }

            return directOwner;
        }

        internal object ResolveDirectOwner(IGridStorageOwner owner)
        {
            if (owner is IGridStorageDirectOwnerProvider provider)
            {
                object directOwner = provider.DirectItemOwner;
                if (directOwner != null)
                    return directOwner;
            }
            return owner;
        }

        internal bool ShareRootOwner(IGridStorageOwner first, IGridStorageOwner second)
        {
            object firstRoot = ResolveRootOwner(first);
            object secondRoot = ResolveRootOwner(second);
            return firstRoot != null && ReferenceEquals(firstRoot, secondRoot);
        }

        internal bool TryReconcileCommittedTransfer(
            IGridStorageOwner source,
            IGridStorageOwner target,
            GridStorageTransferReceipt receipt,
            out string error)
        {
            error = null;
            object sourceDirectOwner = ResolveDirectOwner(source);
            object targetDirectOwner = ResolveDirectOwner(target);
            if (source == null || target == null || ReferenceEquals(source, target) ||
                sourceDirectOwner == null || targetDirectOwner == null ||
                ReferenceEquals(sourceDirectOwner, targetDirectOwner) ||
                receipt.Result == null || !receipt.Result.Success || string.IsNullOrWhiteSpace(receipt.SourceInstanceId))
            {
                error = "Committed ownership reconciliation requires a successful transfer between distinct owners.";
                return false;
            }

            if (!TryCollectFinalInstanceIds(source, out HashSet<string> sourceIds, out error) ||
                !TryCollectFinalInstanceIds(target, out HashSet<string> targetIds, out error))
            {
                return false;
            }

            foreach (string instanceId in sourceIds)
            {
                if (targetIds.Contains(instanceId))
                {
                    error = $"Item instance '{instanceId}' exists in both committed transfer owners.";
                    return false;
                }
            }

            if (!TryCollectReceiptIds(receipt.Result.CreatedInstanceIds, "created", out HashSet<string> createdIds, out error) ||
                !TryCollectReceiptIds(receipt.Result.RemovedInstanceIds, "removed", out HashSet<string> removedIds, out error))
            {
                return false;
            }

            string sourceInstanceId = receipt.SourceInstanceId;
            bool sourceStillPresent = sourceIds.Contains(sourceInstanceId);
            bool sourceMovedToTarget = targetIds.Contains(sourceInstanceId);
            if (receipt.SourceWasRemoved == sourceStillPresent)
            {
                error = $"Committed transfer receipt disagrees with final source state for '{sourceInstanceId}'.";
                return false;
            }
            if (receipt.SourceWasRemoved != removedIds.Contains(sourceInstanceId))
            {
                error = $"Committed transfer receipt has inconsistent removal metadata for '{sourceInstanceId}'.";
                return false;
            }
            if (!string.IsNullOrWhiteSpace(receipt.DestinationInstanceId) &&
                !targetIds.Contains(receipt.DestinationInstanceId))
            {
                error = $"Committed transfer destination '{receipt.DestinationInstanceId}' is missing from the target.";
                return false;
            }

            foreach (string createdId in createdIds)
            {
                if (!targetIds.Contains(createdId))
                {
                    error = $"Committed transfer created instance '{createdId}' is missing from the target.";
                    return false;
                }
            }

            foreach (string removedId in removedIds)
            {
                if (sourceIds.Contains(removedId) || (targetIds.Contains(removedId) && removedId != sourceInstanceId))
                {
                    error = $"Committed transfer removed instance '{removedId}' remains in an unexpected final storage.";
                    return false;
                }
            }

            var bindToTarget = new HashSet<string>(StringComparer.Ordinal);
            var transferToTarget = new HashSet<string>(StringComparer.Ordinal);
            var removeFromSource = new HashSet<string>(StringComparer.Ordinal);

            foreach (string instanceId in sourceIds)
            {
                if (!HasExpectedOwner(instanceId, sourceDirectOwner, out error))
                    return false;
            }

            if (sourceMovedToTarget)
            {
                if (!CanTransferOwner(sourceInstanceId, sourceDirectOwner, targetDirectOwner, out bool needsTransfer, out error))
                    return false;
                if (needsTransfer)
                    transferToTarget.Add(sourceInstanceId);
            }
            else if (!sourceStillPresent)
            {
                if (!CanRemoveOwner(sourceInstanceId, sourceDirectOwner, out bool needsRemoval, out error))
                    return false;
                if (needsRemoval)
                    removeFromSource.Add(sourceInstanceId);
            }

            foreach (string instanceId in targetIds)
            {
                if (instanceId == sourceInstanceId)
                    continue;

                if (directOwnersByInstanceId.TryGetValue(instanceId, out object registeredOwner) && registeredOwner != null)
                {
                    if (!ReferenceEquals(registeredOwner, targetDirectOwner))
                    {
                        error = $"OwnershipMismatch: committed target instance '{instanceId}' actual owner is " +
                                $"'{DescribeOwner(registeredOwner)}'; expected '{DescribeOwner(targetDirectOwner)}'.";
                        return false;
                    }
                    continue;
                }

                if (!createdIds.Contains(instanceId))
                {
                    error = $"Committed target instance '{instanceId}' has no registered direct owner.";
                    return false;
                }

                bindToTarget.Add(instanceId);
            }

            foreach (string removedId in removedIds)
            {
                if (removedId == sourceInstanceId || targetIds.Contains(removedId))
                    continue;
                if (!CanRemoveOwner(removedId, sourceDirectOwner, out bool needsRemoval, out error))
                    return false;
                if (needsRemoval)
                    removeFromSource.Add(removedId);
            }

            foreach (string instanceId in transferToTarget)
                directOwnersByInstanceId[instanceId] = targetDirectOwner;
            foreach (string instanceId in bindToTarget)
                directOwnersByInstanceId.Add(instanceId, targetDirectOwner);
            foreach (string instanceId in removeFromSource)
                directOwnersByInstanceId.Remove(instanceId);

            return true;
        }

        private bool HasExpectedOwner(string instanceId, object expectedOwner, out string error)
        {
            error = null;
            if (directOwnersByInstanceId.TryGetValue(instanceId, out object registeredOwner) &&
                registeredOwner != null && ReferenceEquals(registeredOwner, expectedOwner))
            {
                return true;
            }

            error = $"OwnershipMismatch: item instance '{instanceId}' actual owner is " +
                    $"'{DescribeOwner(registeredOwner)}'; expected committed owner '{DescribeOwner(expectedOwner)}'.";
            return false;
        }

        private bool CanTransferOwner(
            string instanceId,
            object expectedSource,
            object target,
            out bool needsTransfer,
            out string error)
        {
            needsTransfer = false;
            error = null;
            if (!directOwnersByInstanceId.TryGetValue(instanceId, out object registeredOwner) || registeredOwner == null)
            {
                error = $"Item instance '{instanceId}' has no registered direct owner to transfer.";
                return false;
            }
            if (ReferenceEquals(registeredOwner, target))
                return true;
            if (!ReferenceEquals(registeredOwner, expectedSource))
            {
                error = $"OwnershipTransitionFailed: item instance '{instanceId}' actual owner is " +
                        $"'{DescribeOwner(registeredOwner)}'; expected source '{DescribeOwner(expectedSource)}'; " +
                        $"target '{DescribeOwner(target)}'.";
                return false;
            }

            needsTransfer = true;
            return true;
        }

        private bool CanRemoveOwner(string instanceId, object expectedOwner, out bool needsRemoval, out string error)
        {
            needsRemoval = false;
            error = null;
            if (!directOwnersByInstanceId.TryGetValue(instanceId, out object registeredOwner) || registeredOwner == null)
            {
                error = $"Item instance '{instanceId}' has no registered direct owner to retire.";
                return false;
            }
            if (!ReferenceEquals(registeredOwner, expectedOwner))
            {
                error = $"OwnershipMismatch: item instance '{instanceId}' actual owner is " +
                        $"'{DescribeOwner(registeredOwner)}'; expected retiring owner '{DescribeOwner(expectedOwner)}'.";
                return false;
            }

            needsRemoval = true;
            return true;
        }

        private static bool TryCollectFinalInstanceIds(
            IGridStorageOwner owner,
            out HashSet<string> instanceIds,
            out string error)
        {
            instanceIds = new HashSet<string>(StringComparer.Ordinal);
            error = null;
            IReadOnlyList<ItemStorageEntry> entries = owner?.GridStorageEntries;
            if (entries == null)
                return true;

            for (int index = 0; index < entries.Count; index++)
            {
                ItemInstance item = entries[index]?.Item;
                if (item == null)
                    continue;
                if (string.IsNullOrWhiteSpace(item.InstanceId) || !instanceIds.Add(item.InstanceId))
                {
                    error = $"Committed owner contains an invalid or duplicate InstanceId at entry {index}.";
                    return false;
                }
            }

            return true;
        }

        private static bool TryCollectReceiptIds(
            IReadOnlyList<string> values,
            string label,
            out HashSet<string> instanceIds,
            out string error)
        {
            instanceIds = new HashSet<string>(StringComparer.Ordinal);
            error = null;
            if (values == null)
                return true;

            for (int index = 0; index < values.Count; index++)
            {
                string instanceId = values[index];
                if (string.IsNullOrWhiteSpace(instanceId) || !instanceIds.Add(instanceId))
                {
                    error = $"Committed transfer contains an invalid or duplicate {label} InstanceId.";
                    return false;
                }
            }

            return true;
        }

        internal void ReconcileRestoredOwners(IGridStorageOwner first, IGridStorageOwner second)
        {
            object firstDirectOwner = ResolveDirectOwner(first);
            object secondDirectOwner = ResolveDirectOwner(second);
            if (first == null || second == null || ReferenceEquals(first, second) ||
                firstDirectOwner == null || secondDirectOwner == null ||
                ReferenceEquals(firstDirectOwner, secondDirectOwner))
                throw new ArgumentException("Ownership rollback requires two distinct restored owners.");
            if (!TryCollectFinalInstanceIds(first, out HashSet<string> firstIds, out string error) ||
                !TryCollectFinalInstanceIds(second, out HashSet<string> secondIds, out error))
            {
                throw new InvalidOperationException(error);
            }

            foreach (string instanceId in firstIds)
            {
                if (secondIds.Contains(instanceId))
                    throw new InvalidOperationException($"Restored item instance '{instanceId}' exists in both owners.");
                ValidateRestoredOwner(instanceId, firstDirectOwner, secondDirectOwner);
            }
            foreach (string instanceId in secondIds)
                ValidateRestoredOwner(instanceId, firstDirectOwner, secondDirectOwner);

            foreach (string instanceId in firstIds)
                directOwnersByInstanceId[instanceId] = firstDirectOwner;
            foreach (string instanceId in secondIds)
                directOwnersByInstanceId[instanceId] = secondDirectOwner;
        }

        private void ValidateRestoredOwner(string instanceId, object first, object second)
        {
            if (!directOwnersByInstanceId.TryGetValue(instanceId, out object registeredOwner) || registeredOwner == null)
            {
                if (!ItemInstanceIdRegistry.Instance.IsActive(instanceId))
                    throw new InvalidOperationException($"Restored item instance '{instanceId}' is not active.");
                return;
            }
            if (!ReferenceEquals(registeredOwner, first) && !ReferenceEquals(registeredOwner, second))
                throw new InvalidOperationException(
                    $"OwnershipMismatch: restored item instance '{instanceId}' actual owner is " +
                    $"'{DescribeOwner(registeredOwner)}'; expected '{DescribeOwner(first)}' or '{DescribeOwner(second)}'.");
        }

        private static string DescribeOwner(object owner)
        {
            if (owner == null)
                return "<NONE>";
            if (owner is UnityEngine.Object unityObject)
                return $"{owner.GetType().Name}({(string.IsNullOrWhiteSpace(unityObject.name) ? "<EMPTY>" : unityObject.name)})";
            if (owner is IGridStorageOwner storageOwner)
            {
                string displayName = storageOwner.GridStorageDisplayName;
                return $"{owner.GetType().Name}({(string.IsNullOrWhiteSpace(displayName) ? "<EMPTY>" : displayName)})";
            }
            return owner.GetType().Name;
        }

    }
}
