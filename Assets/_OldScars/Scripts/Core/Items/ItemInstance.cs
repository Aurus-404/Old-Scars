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
        public bool HasFirearmState { get; }
        public string LoadedAmmoProfileId { get; private set; }
        public int LoadedRounds { get; private set; }

        private bool ownedStorageRegistered;

        /// <summary>
        /// Compatibility path for a new runtime item. Loaded items must use Rehydrate.
        /// </summary>
        public ItemInstance(ItemDefinition definition)
            : this(PrepareNew(definition), ResolveOwnedStorageProfile)
        {
        }

        private ItemInstance(NewItemState state, Func<string, ItemStorageProfileDefinition> profileResolver)
            : this(state.InstanceId, state.DefinitionId, state.Condition, state.MaxStack, state.OwnedStorageProfileId, state.HasFirearmState)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(OwnedStorageProfileId))
                {
                    ItemStorageProfileDefinition profile = profileResolver?.Invoke(OwnedStorageProfileId);
                    if (profile == null)
                        throw new InvalidOperationException($"Item-owned storage profile '{OwnedStorageProfileId}' was not found.");

                    AttachOwnedStorageUnregistered(profile, null, true);
                    RegisterAttachedOwnedStorage();
                }
            }
            catch
            {
                CleanupOwnedStorageAfterFailure();
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
            string ownedStorageProfileId,
            bool hasFirearmState)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            Condition = condition;
            MaxStack = maxStack;
            OwnedStorageProfileId = ownedStorageProfileId;
            HasFirearmState = hasFirearmState;
        }

        public static ItemInstance CreateNew(ItemDefinition definition)
        {
            return new ItemInstance(definition);
        }

        /// <summary>
        /// Creates a new scene-authored gameplay item with an exact preassigned identity.
        /// Save loading must use Rehydrate instead.
        /// </summary>
        public static ItemInstance CreateAuthored(ItemDefinition definition, string authoredInstanceId)
        {
            return new ItemInstance(PrepareAuthored(definition, authoredInstanceId), ResolveOwnedStorageProfile);
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
                    definition.owned_storage_profile_id,
                    !string.IsNullOrWhiteSpace(definition.firearm_profile_id));
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
                return new ItemInstance(siblingId, DefinitionId, Condition, MaxStack, null, false);
            }
            catch
            {
                ItemInstanceIdRegistry.Instance.ReleaseFailedReservation(siblingId);
                throw;
            }
        }

        public bool TrySetFirearmState(string ammoProfileId, int loadedRounds, out string failure)
        {
            if (!HasFirearmState)
            {
                failure = $"Item instance '{InstanceId}' is not a firearm.";
                return false;
            }
            if (loadedRounds < 0 || loadedRounds == 0 && !string.IsNullOrEmpty(ammoProfileId) ||
                loadedRounds > 0 && string.IsNullOrWhiteSpace(ammoProfileId))
            {
                failure = $"Firearm state for '{InstanceId}' has inconsistent ammo '{ammoProfileId ?? "<NONE>"}' and loaded rounds {loadedRounds}.";
                return false;
            }

            LoadedAmmoProfileId = loadedRounds > 0 ? ammoProfileId : null;
            LoadedRounds = loadedRounds;
            failure = null;
            return true;
        }

        public bool TryConsumeLoadedRound(out string failure)
        {
            if (!HasFirearmState || LoadedRounds <= 0)
            {
                failure = $"Firearm '{InstanceId}' is unloaded.";
                return false;
            }

            LoadedRounds--;
            if (LoadedRounds == 0)
                LoadedAmmoProfileId = null;
            failure = null;
            return true;
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

        internal void AttachOwnedStorageUnregistered(ItemStorageProfileDefinition profile)
        {
            AttachOwnedStorageUnregistered(profile, null, false);
        }

        internal void AttachOwnedStorageUnregistered(
            ItemStorageProfileDefinition profile,
            Func<string, ItemDefinition> definitionResolver)
        {
            AttachOwnedStorageUnregistered(profile, definitionResolver, false);
        }

        internal void RegisterAttachedOwnedStorage()
        {
            if (OwnedStorage == null)
                throw new InvalidOperationException($"Item instance '{InstanceId}' has no item-owned storage to register.");
            if (ownedStorageRegistered)
                throw new InvalidOperationException($"Item-owned storage '{InstanceId}' is already registered by its item.");
            if (OwnedStorage.GridInitializationState != GridStorageInitializationState.Active)
            {
                throw new InvalidOperationException(
                    $"Item-owned storage '{InstanceId}' must complete layout validation before registration.");
            }

            ItemOwnedStorageRegistry.Instance.RegisterStorage(OwnedStorage);
            ownedStorageRegistered = true;
        }

        internal void DetachUnregisteredOwnedStorage()
        {
            if (ownedStorageRegistered)
                throw new InvalidOperationException($"Registered item-owned storage '{InstanceId}' cannot be detached.");

            OwnedStorage = null;
        }

        private void AttachOwnedStorageUnregistered(
            ItemStorageProfileDefinition profile,
            Func<string, ItemDefinition> definitionResolver,
            bool initializeLayoutImmediately)
        {
            if (OwnedStorage != null)
                throw new InvalidOperationException($"Item instance '{InstanceId}' already has item-owned storage.");
            if (profile == null || string.IsNullOrWhiteSpace(OwnedStorageProfileId) || profile.id != OwnedStorageProfileId)
                throw new InvalidOperationException($"Item-owned storage profile does not match item instance '{InstanceId}'.");

            OwnedStorage = new ItemOwnedStorageRuntime(
                this,
                profile,
                definitionResolver,
                initializeLayoutImmediately);
            ownedStorageRegistered = false;
        }

        private void CleanupOwnedStorageAfterFailure()
        {
            if (OwnedStorage != null && ownedStorageRegistered)
                ItemOwnedStorageRegistry.Instance.UnregisterStorage(InstanceId, OwnedStorage);

            OwnedStorage = null;
            ownedStorageRegistered = false;
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
                definition.owned_storage_profile_id,
                !string.IsNullOrWhiteSpace(definition.firearm_profile_id));
        }

        private static NewItemState PrepareAuthored(ItemDefinition definition, string authoredInstanceId)
        {
            ValidateDefinition(definition);
            if (!ItemInstanceIdRegistry.IsValidFormat(authoredInstanceId))
                throw new ArgumentException($"Authored item instance id '{authoredInstanceId}' does not match the durable item ID format.", nameof(authoredInstanceId));

            ItemInstanceIdRegistry.Instance.ReserveExact(authoredInstanceId);
            return new NewItemState(
                authoredInstanceId,
                definition.id,
                definition.physical.condition_max,
                Math.Max(1, definition.max_stack),
                definition.owned_storage_profile_id,
                !string.IsNullOrWhiteSpace(definition.firearm_profile_id));
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
                string ownedStorageProfileId,
                bool hasFirearmState)
            {
                InstanceId = instanceId;
                DefinitionId = definitionId;
                Condition = condition;
                MaxStack = maxStack;
                OwnedStorageProfileId = ownedStorageProfileId;
                HasFirearmState = hasFirearmState;
            }

            internal string InstanceId { get; }
            internal string DefinitionId { get; }
            internal int Condition { get; }
            internal int MaxStack { get; }
            internal string OwnedStorageProfileId { get; }
            internal bool HasFirearmState { get; }
        }
    }
}
