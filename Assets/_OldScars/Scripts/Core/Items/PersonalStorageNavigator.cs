using System.Collections.Generic;
using OldScars.Core.Actors;

namespace OldScars.Core.Items
{
    public readonly struct PersonalStorageOption
    {
        public PersonalStorageOption(string containerInstanceId, string label, IGridStorageOwner owner)
        {
            ContainerInstanceId = containerInstanceId;
            Label = label;
            Owner = owner;
        }

        public string ContainerInstanceId { get; }
        public string Label { get; }
        public IGridStorageOwner Owner { get; }
        public bool IsPersonalInventory => string.IsNullOrWhiteSpace(ContainerInstanceId);
    }

    /// <summary>
    /// UI-only navigation over actor-accessible personal storage owners.
    /// It never owns content or placements.
    /// </summary>
    public sealed class PersonalStorageNavigator
    {
        private readonly InventoryComponent inventory;
        private readonly ActorEquipmentComponent equipment;
        private readonly List<PersonalStorageOption> options = new List<PersonalStorageOption>();
        private readonly HashSet<string> seenContainerIds = new HashSet<string>();
        private string selectedContainerInstanceId;

        public PersonalStorageNavigator(InventoryComponent inventory)
        {
            this.inventory = inventory;
            equipment = inventory != null ? inventory.GetComponent<ActorEquipmentComponent>() : null;
        }

        public InventoryComponent PersonalInventory => inventory;
        public string SelectedContainerInstanceId => selectedContainerInstanceId;

        public IGridStorageOwner SelectedOwner
        {
            get
            {
                Refresh();
                if (!string.IsNullOrWhiteSpace(selectedContainerInstanceId) &&
                    TryGetOwnedStorage(selectedContainerInstanceId, out ItemOwnedStorageRuntime storage))
                {
                    return storage;
                }

                return inventory;
            }
        }

        public IReadOnlyList<PersonalStorageOption> GetOptions()
        {
            Refresh();
            return options;
        }

        public void SelectPersonalInventory()
        {
            selectedContainerInstanceId = null;
        }

        public bool TrySelectContainer(string containerInstanceId)
        {
            Refresh();
            if (!TryGetOwnedStorage(containerInstanceId, out _))
                return false;

            selectedContainerInstanceId = containerInstanceId;
            return true;
        }

        public bool TryGetOwnedStorage(string containerInstanceId, out ItemOwnedStorageRuntime storage)
        {
            storage = null;
            return IsAccessible(containerInstanceId) &&
                   ItemOwnedStorageRegistry.Instance.TryResolveOwnedStorage(containerInstanceId, out storage);
        }

        public bool TryGetPersonalInventoryOwnedStorage(
            string containerInstanceId,
            out ItemOwnedStorageRuntime storage,
            out ItemStorageEntry entry)
        {
            storage = null;
            entry = FindEntry(inventory != null ? inventory.Entries : null, containerInstanceId);
            return entry?.Item?.HasOwnedStorage == true &&
                   ItemOwnedStorageRegistry.Instance.TryResolveOwnedStorage(containerInstanceId, out storage);
        }

        public bool IsEquippedOwnedStorage(string containerInstanceId)
        {
            return FindEntry(equipment != null ? equipment.Entries : null, containerInstanceId)?.Item?.HasOwnedStorage == true;
        }

        public bool TryGetContainerEntry(string containerInstanceId, out ItemStorageEntry entry)
        {
            entry = FindEntry(inventory != null ? inventory.Entries : null, containerInstanceId);
            if (entry != null)
                return true;

            entry = FindEntry(equipment != null ? equipment.Entries : null, containerInstanceId);
            return entry != null;
        }

        public void Refresh()
        {
            options.Clear();
            seenContainerIds.Clear();
            options.Add(new PersonalStorageOption(null, "Inventario personal", inventory));
            AppendOptions(equipment != null ? equipment.Entries : null);

            if (!string.IsNullOrWhiteSpace(selectedContainerInstanceId) && !IsAccessibleFromOptions(selectedContainerInstanceId))
                selectedContainerInstanceId = null;
        }

        private bool IsAccessible(string containerInstanceId)
        {
            if (string.IsNullOrWhiteSpace(containerInstanceId))
                return false;

            Refresh();
            return IsAccessibleFromOptions(containerInstanceId);
        }

        private bool IsAccessibleFromOptions(string containerInstanceId)
        {
            for (int index = 1; index < options.Count; index++)
            {
                if (options[index].ContainerInstanceId == containerInstanceId)
                    return true;
            }
            return false;
        }

        private void AppendOptions(IReadOnlyList<ItemStorageEntry> entries)
        {
            if (entries == null)
                return;

            for (int index = 0; index < entries.Count; index++)
            {
                ItemInstance item = entries[index] != null ? entries[index].Item : null;
                if (item == null || !item.HasOwnedStorage || !seenContainerIds.Add(item.InstanceId))
                    continue;

                string suffix = item.InstanceId.Length > 4
                    ? item.InstanceId.Substring(item.InstanceId.Length - 4)
                    : item.InstanceId;
                options.Add(new PersonalStorageOption(
                    item.InstanceId,
                    $"{item.OwnedStorage.GridStorageDisplayName} [{suffix}]",
                    item.OwnedStorage));
            }
        }

        private static ItemStorageEntry FindEntry(IReadOnlyList<ItemStorageEntry> entries, string instanceId)
        {
            if (entries == null || string.IsNullOrWhiteSpace(instanceId))
                return null;

            for (int index = 0; index < entries.Count; index++)
            {
                ItemStorageEntry entry = entries[index];
                if (entry?.Item?.InstanceId == instanceId && entry.Item.HasOwnedStorage)
                    return entry;
            }
            return null;
        }
    }
}
