using OldScars.Core.Items;
using UnityEngine;

namespace OldScars.Core.Actors
{
    public sealed class DebugActorInventorySeeder : MonoBehaviour
    {
        [SerializeField] private InventoryComponent inventory;
        [SerializeField] private DebugItemStack[] startingItems;

        private bool seeded;

        private void Start()
        {
            SeedIfNeeded();
        }

        public void SeedIfNeeded()
        {
            if (seeded)
                return;

            seeded = true;

            if (inventory == null)
                inventory = GetComponent<InventoryComponent>();

            if (inventory == null || startingItems == null)
                return;

            for (int index = 0; index < startingItems.Length; index++)
            {
                DebugItemStack stack = startingItems[index];
                if (stack == null || string.IsNullOrWhiteSpace(stack.itemDefinitionId) || stack.quantity < 1)
                    continue;

                inventory.AddItemByDefinitionId(stack.itemDefinitionId, stack.quantity);
            }
        }
    }

    [System.Serializable]
    public sealed class DebugItemStack
    {
        public string itemDefinitionId;
        public int quantity = 1;
    }
}
