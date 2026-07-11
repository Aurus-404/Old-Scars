using OldScars.Core.Data.Definitions;
using System;

namespace OldScars.Core.Items
{
    /// <summary>
    /// Runtime-only item instance used by debug gameplay.
    ///
    /// Definitions live in JSON. Instances live in runtime now, and may later
    /// be owned by save data. This class does not implement inventory,
    /// equipment, stacks, ownership, location, or durability logic.
    /// </summary>
    public sealed class ItemInstance
    {
        private static int nextInstanceNumber = 1;

        public string InstanceId { get; }
        public string DefinitionId { get; }
        public int Condition { get; }
        public int MaxStack { get; }

        public ItemInstance(ItemDefinition definition)
        {
            InstanceId = CreateRuntimeInstanceId();
            DefinitionId = definition != null ? definition.id : null;
            Condition = GetInitialCondition(definition);
            MaxStack = GetMaxStack(definition);
        }

        private ItemInstance(string definitionId, int condition, int maxStack)
        {
            InstanceId = CreateRuntimeInstanceId();
            DefinitionId = definitionId;
            Condition = Math.Max(1, condition);
            MaxStack = Math.Max(1, maxStack);
        }

        public ItemInstance CreateStackSibling()
        {
            return new ItemInstance(DefinitionId, Condition, MaxStack);
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
    }
}
