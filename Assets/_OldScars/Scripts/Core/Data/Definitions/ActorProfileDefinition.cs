namespace OldScars.Core.Data.Definitions
{
    /// <summary>
    /// Immutable actor profile definition loaded from JSON.
    ///
    /// Milestone 24.1 scope: data load and registration only. Runtime
    /// application to scene actors belongs to a later pass.
    /// </summary>
    [System.Serializable]
    public sealed class ActorProfileDefinition
    {
        public string type; // must be "actor_profile"
        public string id;
        public string display_name;
        public string inventory_seed_actor_tag; // Optional debug bootstrap selector for actors without ActorProfileComponent.
        public string[] initial_tags;
        public ActorProfileHealth health;
        public ActorProfileInventoryEntry[] initial_inventory;
        public ActorProfileInitialEquipmentEntry[] initial_equipment;
        public string equipment_layout_id;
        public string visual_rig_profile_id;
        public ActorProfileEquipped equipped; // Unsupported until a later M24 pass.
    }

    [System.Serializable]
    public sealed class ActorProfileHealth
    {
        public float max_health;
        public float current_health;
    }

    [System.Serializable]
    public sealed class ActorProfileInventoryEntry
    {
        public string item_id;
        public int quantity;
    }

    [System.Serializable]
    public sealed class ActorProfileInitialEquipmentEntry
    {
        public string item_id;
        public string[] slot_ids;
    }

    [System.Serializable]
    public sealed class ActorProfileEquipped
    {
        public string right_hand;
    }
}
