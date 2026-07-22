using System;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;

namespace OldScars.Core.Items
{
    /// <summary>
    /// Runtime representative of one durable item identity. Stack quantity lives in ItemStorageEntry.
    /// The public constructor and CreateNew both create a new runtime item; M37 must use Rehydrate.
    /// </summary>
    public sealed class ItemInstance
    {
        public string InstanceId { get; }
        public string DefinitionId { get; }
        public int Condition { get; }
        public int MaxStack { get; }
        public string OwnedStorageProfileId { get; }
        public ItemOwnedStorageRuntime OwnedStorage { get; private set; }
        public bool HasOwnedStorage => OwnedStorage != null;

        /// <summary>
        /// Compatibility path for a new runtime item. Loaded items must use Rehydrate.
        /// </summary>
        public ItemInstance(ItemDefinition definition)
            : this(PrepareNew(definition), ResolveOwnedStorageProfile)
        {
        }

        private ItemInstance(NewItemState state, Func<string, ItemStorageProfileDefinition> profileResolver)
            : this(state.InstanceId, state.DefinitionId, state.Condition, state.MaxStack, state.OwnedStorageProfileId)
        {
            ItemOwnedStorageRuntime storage = null;
            bool storageRegistered = false;
            try
            {
                if (!string.IsNullOrWhiteSpace(OwnedStorageProfileId))
                {
                    ItemStorageProfileDefinition profile = profileResolver?.Invoke(OwnedStorageProfileId);
                    if (profile == null)
                        throw new InvalidOperationException($"Item-owned storage profile '{OwnedStorageProfileId}' was not found.");

                    storage = new ItemOwnedStorageRuntime(this, profile);
                    ItemOwnedStorageRegistry.Instance.RegisterStorage(storage);
                    storageRegistered = true;
                    OwnedStorage = storage;
                }
            }
            catch
            {
                if (storageRegistered)
                    ItemOwnedStorageRegistry.Instance.UnregisterStorage(InstanceId, storage);
                ItemOwnedStorageRegistry.Instance.UnbindItem(InstanceId);
                ItemInstanceIdRegistry.Instance.ReleaseFailedReservation(InstanceId);
                throw;
            }
        }

        private ItemInstance(
            string instanceId,
            string definitionId,
            int condition,
            int maxStack,
            string ownedStorageProfileId)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            Condition = condition;
            MaxStack = maxStack;
            OwnedStorageProfileId = ownedStorageProfileId;
        }

        public static ItemInstance CreateNew(ItemDefinition definition)
        {
            return new ItemInstance(definition);
        }

        internal static ItemInstance CreateNew(
            ItemDefinition definition,
            Func<string, ItemStorageProfileDefinition> profileResolver)
        {
            return new ItemInstance(PrepareNew(definition), profileResolver);
        }

        /// <summary>
        /// Reserves and restores an existing durable identity without creating storage or ownership.
        /// M37 will attach and hydrate item-owned storage explicitly after restoring its content.
        /// </summary>
        public static ItemInstance Rehydrate(ItemDefinition definition, string instanceId, int condition)
        {
            ValidateDefinition(definition);
            if (!ItemInstanceIdRegistry.IsValidFormat(instanceId))
                throw new ArgumentException($"Item instance id '{instanceId}' does not match the durable item ID format.", nameof(instanceId));

            ItemInstanceIdRegistry.Instance.ReserveExact(instanceId);
            try
            {
                ValidateCondition(definition, condition);
                return new ItemInstance(
                    instanceId,
                    definition.id,
                    condition,
                    Math.Max(1, definition.max_stack),
                    definition.owned_storage_profile_id);
            }
            catch
            {
                ItemInstanceIdRegistry.Instance.ReleaseFailedReservation(instanceId);
                throw;
            }
        }

        public ItemInstance CreateStackSibling()
        {
            if (HasOwnedStorage || !string.IsNullOrWhiteSpace(OwnedStorageProfileId) || MaxStack <= 1)
                throw new InvalidOperationException($"Item instance '{InstanceId}' cannot create a stack sibling.");

            string siblingId = ItemInstanceIdRegistry.Instance.ReserveNewId();
            try
            {
                return new ItemInstance(siblingId, DefinitionId, Condition, MaxStack, null);
            }
            catch
            {
                ItemInstanceIdRegistry.Instance.ReleaseFailedReservation(siblingId);
                throw;
            }
        }

        public static bool CanStackWith(ItemInstance first, ItemInstance second)
        {
            return first != null && second != null &&
                   first.MaxStack > 1 && first.MaxStack == second.MaxStack &&
                   first.DefinitionId == second.DefinitionId &&
                   first.Condition == second.Condition &&
                   !first.HasOwnedStorage && !second.HasOwnedStorage &&
                   string.IsNullOrWhiteSpace(first.OwnedStorageProfileId) &&
                   string.IsNullOrWhiteSpace(second.OwnedStorageProfileId);
        }

        internal void AttachOwnedStorage(ItemStorageProfileDefinition profile)
        {
            if (OwnedStorage != null)
                throw new InvalidOperationException($"Item instance '{InstanceId}' already has item-owned storage.");
            if (profile == null || string.IsNullOrWhiteSpace(OwnedStorageProfileId) || profile.id != OwnedStorageProfileId)
                throw new InvalidOperationException($"Item-owned storage profile does not match item instance '{InstanceId}'.");

            var storage = new ItemOwnedStorageRuntime(this, profile);
            ItemOwnedStorageRegistry.Instance.RegisterStorage(storage);
            OwnedStorage = storage;
        }

        private static NewItemState PrepareNew(ItemDefinition definition)
        {
            ValidateDefinition(definition);
            string instanceId = ItemInstanceIdRegistry.Instance.ReserveNewId();
            return new NewItemState(
                instanceId,
                definition.id,
                definition.physical.condition_max,
                Math.Max(1, definition.max_stack),
                definition.owned_storage_profile_id);
        }

        private static void ValidateDefinition(ItemDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (string.IsNullOrWhiteSpace(definition.id))
                throw new ArgumentException("Item definition requires an id.", nameof(definition));
            if (definition.physical == null || definition.physical.condition_max < 1)
                throw new ArgumentException($"Item definition '{definition.id}' requires condition_max >= 1.", nameof(definition));
            if (!string.IsNullOrWhiteSpace(definition.owned_storage_profile_id) && Math.Max(1, definition.max_stack) > 1)
                throw new ArgumentException($"Item definition '{definition.id}' cannot stack while owning storage.", nameof(definition));
        }

        private static void ValidateCondition(ItemDefinition definition, int condition)
        {
            if (condition < 1 || condition > definition.physical.condition_max)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(condition),
                    condition,
                    $"Condition for '{definition.id}' must be between 1 and {definition.physical.condition_max}.");
            }
        }

        private static ItemStorageProfileDefinition ResolveOwnedStorageProfile(string profileId)
        {
            if (GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
                throw new InvalidOperationException($"Cannot resolve item-owned storage profile '{profileId}' before game data is ready.");

            return GameDataManager.Instance.Database?.GetItemStorageProfile(profileId);
        }

        private readonly struct NewItemState
        {
            internal NewItemState(
                string instanceId,
                string definitionId,
                int condition,
                int maxStack,
                string ownedStorageProfileId)
            {
                InstanceId = instanceId;
                DefinitionId = definitionId;
                Condition = condition;
                MaxStack = maxStack;
                OwnedStorageProfileId = ownedStorageProfileId;
            }

            internal string InstanceId { get; }
            internal string DefinitionId { get; }
            internal int Condition { get; }
            internal int MaxStack { get; }
            internal string OwnedStorageProfileId { get; }
        }
    }
}
