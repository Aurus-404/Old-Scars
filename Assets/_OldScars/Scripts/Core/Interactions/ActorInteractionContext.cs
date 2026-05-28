using System.Collections.Generic;
using OldScars.Core.Items;
using UnityEngine;

namespace OldScars.Core.Interactions
{
    public sealed class ActorInteractionContext : MonoBehaviour
    {
        [SerializeField] private string[] actorTags = { "player", "human" };
        [SerializeField] private ActorInteractionStat[] actorStats = { new ActorInteractionStat("strength", 4f) };
        [SerializeField] private string equippedItemDefinitionId = "rusted_crowbar_01";
        [SerializeField] private InventoryComponent inventoryComponent;
        [SerializeField] private DebugInventory debugInventory;

        public string[] ActorTags => actorTags;
        public string EquippedItemDefinitionId => equippedItemDefinitionId;

        private void Awake()
        {
            if (inventoryComponent == null)
                inventoryComponent = GetComponent<InventoryComponent>();

            if (debugInventory == null)
                debugInventory = GetComponent<DebugInventory>();
        }

        public string GetEquippedItemDefinitionId()
        {
            InventoryComponent inventory = GetInventoryComponent();
            if (inventory != null)
                return inventory.GetEquippedItemDefinitionId();

            if (debugInventory != null)
                return debugInventory.GetEquippedItemDefinitionId();

            return equippedItemDefinitionId;
        }

        public InventoryComponent GetInventoryComponent()
        {
            if (inventoryComponent == null)
                inventoryComponent = GetComponent<InventoryComponent>();

            return inventoryComponent;
        }

        public Dictionary<string, float> BuildActorStatsDictionary()
        {
            var result = new Dictionary<string, float>();

            if (actorStats == null)
                return result;

            for (int index = 0; index < actorStats.Length; index++)
            {
                ActorInteractionStat stat = actorStats[index];
                if (stat == null || string.IsNullOrWhiteSpace(stat.id))
                    continue;

                result[stat.id] = stat.value;
            }

            return result;
        }
    }

    [System.Serializable]
    public sealed class ActorInteractionStat
    {
        public string id;
        public float value;

        public ActorInteractionStat()
        {
        }

        public ActorInteractionStat(string id, float value)
        {
            this.id = id;
            this.value = value;
        }
    }
}
