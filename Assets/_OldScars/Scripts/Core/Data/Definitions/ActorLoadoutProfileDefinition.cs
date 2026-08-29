namespace OldScars.Core.Data.Definitions
{
    /// <summary>
    /// Immutable weighted initial-state recipe for a newly spawned actor.
    /// The result is materialized once as real ItemInstances; it is not corpse loot.
    /// </summary>
    [System.Serializable]
    public sealed class ActorLoadoutProfileDefinition
    {
        public string type; // must be "actor_loadout_profile"
        public string id;
        public ActorLoadoutGroupDefinition[] groups;
    }

    [System.Serializable]
    public sealed class ActorLoadoutGroupDefinition
    {
        public string id; // local stable group key
        public ActorLoadoutChoiceDefinition[] choices;
    }

    [System.Serializable]
    public sealed class ActorLoadoutChoiceDefinition
    {
        public int weight;
        public bool none;
        public ActorLoadoutInventoryEntry[] inventory;
        public ActorProfileInitialEquipmentEntry[] equipment;
    }

    [System.Serializable]
    public sealed class ActorLoadoutInventoryEntry
    {
        public string item_id;
        public int quantity_min = 1;
        public int quantity_max = 1;
    }
}
