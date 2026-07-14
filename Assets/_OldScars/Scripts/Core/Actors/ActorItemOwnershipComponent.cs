using System.Collections.Generic;
using OldScars.Core.Items;
using UnityEngine;

namespace OldScars.Core.Actors
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InventoryComponent))]
    public sealed class ActorItemOwnershipComponent : MonoBehaviour
    {
        [SerializeField] private InventoryComponent inventoryComponent;
        [SerializeField] private ActorEquipmentComponent equipmentComponent;

        private readonly List<ItemStorageEntry> entriesView = new List<ItemStorageEntry>();
        private readonly List<ItemStorageEntry> ownedEntriesView = new List<ItemStorageEntry>();

        public InventoryComponent PersonalInventory
        {
            get
            {
                ResolveReferences();
                return inventoryComponent;
            }
        }

        public ActorEquipmentComponent Equipment
        {
            get
            {
                ResolveReferences();
                return equipmentComponent;
            }
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        public bool TryLocateInstance(
            string instanceId,
            out ActorItemStorageNodeKind nodeKind,
            out ItemStorageEntry entry)
        {
            ResolveReferences();
            nodeKind = ActorItemStorageNodeKind.Personal;
            entry = null;
            if (string.IsNullOrWhiteSpace(instanceId))
                return false;

            if (inventoryComponent != null &&
                inventoryComponent.TryGetEntryByInstanceId(instanceId, out _, out entry))
            {
                nodeKind = ActorItemStorageNodeKind.Personal;
                return true;
            }

            if (equipmentComponent != null &&
                equipmentComponent.TryGetEntryByInstanceId(instanceId, out entry))
            {
                nodeKind = ActorItemStorageNodeKind.Equipment;
                return true;
            }

            entry = null;
            return false;
        }

        public IReadOnlyList<ItemStorageEntry> GetAllDirectEntries()
        {
            ResolveReferences();
            entriesView.Clear();
            if (inventoryComponent != null)
                Append(entriesView, inventoryComponent.Entries);
            if (equipmentComponent != null)
                Append(entriesView, equipmentComponent.Entries);
            return entriesView;
        }

        public IReadOnlyList<ItemStorageEntry> GetAllOwnedEntries()
        {
            ownedEntriesView.Clear();
            IReadOnlyList<ItemStorageEntry> directEntries = GetAllDirectEntries();
            for (int index = 0; index < directEntries.Count; index++)
                AppendOwnedEntryTree(ownedEntriesView, directEntries[index], new HashSet<string>());
            return ownedEntriesView;
        }

        public bool ValidateUniqueOwnership(out string error)
        {
            ResolveReferences();
            error = null;
            var seen = new HashSet<string>();
            if (!AddUniqueTree(seen, inventoryComponent != null ? inventoryComponent.Entries : null, "personal", out error))
                return false;
            if (!AddUniqueTree(seen, equipmentComponent != null ? equipmentComponent.Entries : null, "equipment", out error))
                return false;
            return true;
        }

        public ActorItemOwnershipSnapshot CaptureSnapshot()
        {
            ResolveReferences();
            bool valid = ValidateUniqueOwnership(out string error);
            int instanceCount = GetAllOwnedEntries().Count;
            return new ActorItemOwnershipSnapshot(
                inventoryComponent != null ? inventoryComponent.InternalGridBackend.StorageVersion : 0,
                inventoryComponent != null ? inventoryComponent.InternalGridBackend.LayoutVersion : 0,
                equipmentComponent != null ? equipmentComponent.StorageVersion : 0,
                equipmentComponent != null ? equipmentComponent.Version : 0,
                instanceCount,
                valid,
                error);
        }

        internal void BindEquipment(ActorEquipmentComponent equipment)
        {
            equipmentComponent = equipment;
        }

        private void ResolveReferences()
        {
            if (inventoryComponent == null)
                inventoryComponent = GetComponent<InventoryComponent>();
            if (equipmentComponent == null)
                equipmentComponent = GetComponent<ActorEquipmentComponent>();
        }

        private static void Append(List<ItemStorageEntry> target, IReadOnlyList<ItemStorageEntry> source)
        {
            if (source == null)
                return;
            for (int index = 0; index < source.Count; index++)
                target.Add(source[index]);
        }

        private static bool AddUniqueTree(
            HashSet<string> seen,
            IReadOnlyList<ItemStorageEntry> entries,
            string nodeName,
            out string error)
        {
            error = null;
            if (entries == null)
                return true;

            for (int index = 0; index < entries.Count; index++)
            {
                ItemInstance item = entries[index] != null ? entries[index].Item : null;
                if (item == null || string.IsNullOrWhiteSpace(item.InstanceId))
                {
                    error = $"Actor ownership node '{nodeName}' contains an invalid entry at index {index}.";
                    return false;
                }

                if (!seen.Add(item.InstanceId))
                {
                    error = $"Item instance '{item.InstanceId}' exists in more than one actor ownership node.";
                    return false;
                }

                if (item.HasOwnedStorage &&
                    !AddUniqueTree(seen, item.OwnedStorage.GridStorageEntries, $"item_storage:{item.InstanceId}", out error))
                {
                    return false;
                }
            }

            return true;
        }

        private static void AppendOwnedEntryTree(
            List<ItemStorageEntry> target,
            ItemStorageEntry entry,
            HashSet<string> visited)
        {
            ItemInstance item = entry != null ? entry.Item : null;
            if (item == null || !visited.Add(item.InstanceId))
                return;

            target.Add(entry);
            if (!item.HasOwnedStorage)
                return;

            IReadOnlyList<ItemStorageEntry> contents = item.OwnedStorage.GridStorageEntries;
            for (int index = 0; index < contents.Count; index++)
                AppendOwnedEntryTree(target, contents[index], visited);
        }
    }
}
