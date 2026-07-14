using OldScars.Core.Data.Definitions;
using OldScars.Core.Data;
using System;

namespace OldScars.Core.Items
{
    /// <summary>
    /// Runtime-only item instance used by debug gameplay.
    ///
    /// Definitions live in JSON. Instances live in runtime now, and may later
    /// be owned by save data. This class does not implement inventory,
    /// equipment, stacks, root ownership, location, or durability logic. M34.2
    /// allows the instance to own one spatial storage without making it the
    /// authority for where the instance itself is located.
    /// </summary>
    public sealed class ItemInstance
    {
        private static int nextInstanceNumber = 1;

        public string InstanceId { get; }
        public string DefinitionId { get; }
        public int Condition { get; }
        public int MaxStack { get; }
        public string OwnedStorageProfileId { get; }
        public ItemOwnedStorageRuntime OwnedStorage { get; }
        public bool HasOwnedStorage => OwnedStorage != null;

        public ItemInstance(ItemDefinition definition)
        {
            InstanceId = CreateRuntimeInstanceId();
            DefinitionId = definition != null ? definition.id : null;
            Condition = GetInitialCondition(definition);
            MaxStack = GetMaxStack(definition);
            OwnedStorageProfileId = definition != null ? definition.owned_storage_profile_id : null;
            OwnedStorage = CreateOwnedStorage(OwnedStorageProfileId);
        }

        private ItemInstance(string definitionId, int condition, int maxStack, string ownedStorageProfileId)
        {
            InstanceId = CreateRuntimeInstanceId();
            DefinitionId = definitionId;
            Condition = Math.Max(1, condition);
            MaxStack = Math.Max(1, maxStack);
            OwnedStorageProfileId = ownedStorageProfileId;
            OwnedStorage = CreateOwnedStorage(OwnedStorageProfileId);
        }

        public ItemInstance CreateStackSibling()
        {
            return new ItemInstance(DefinitionId, Condition, MaxStack, OwnedStorageProfileId);
        }

        internal static int CaptureIdSequence()
        {
            return nextInstanceNumber;
        }

        internal static void RestoreIdSequence(int nextNumber)
        {
            nextInstanceNumber = Math.Max(1, nextNumber);
        }

        private static string CreateRuntimeInstanceId()
        {
            string instanceId = $"item_instance_{nextInstanceNumber:0000}";
            nextInstanceNumber++;
            return instanceId;
        }

        private static int GetInitialCondition(ItemDefinition definition)
        {
            if (definition == null || definition.physical == null || definition.physical.condition_max <= 0)
                return 1;

            return definition.physical.condition_max;
        }

        private static int GetMaxStack(ItemDefinition definition)
        {
            return definition != null ? Math.Max(1, definition.max_stack) : 1;
        }

        private ItemOwnedStorageRuntime CreateOwnedStorage(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId) || GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
                return null;

            ItemStorageProfileDefinition profile = GameDataManager.Instance.Database?.GetItemStorageProfile(profileId);
            return profile != null ? new ItemOwnedStorageRuntime(this, profile) : null;
        }
    }
}
