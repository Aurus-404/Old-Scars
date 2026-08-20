namespace OldScars.Core.Data.Definitions
{
    /// <summary>
    /// Immutable actor profile definition loaded from JSON.
    ///
    /// Immutable content parameters are applied by ActorProfileComponent;
    /// runtime orders and observations remain ephemeral actor state.
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
        public ActorProfileNavigation navigation;
        public ActorProfileVisualPerception visual_perception;
        public ActorProfileEncounterAI encounter_ai;
        public ActorProfileEquipped equipped; // Unsupported until a later M24 pass.
    }

    [System.Serializable]
    public sealed class ActorProfileHealth
    {
        public float max_health;
        public float current_health;
    }

    [System.Serializable]
    public sealed class ActorProfileNavigation
    {
        public float speed;
        public float acceleration;
        public float angular_speed;
        public float stopping_distance;
    }

    [System.Serializable]
    public sealed class ActorProfileVisualPerception
    {
        public float visual_range;
        public float horizontal_fov_degrees;
        public float eye_height;
    }

    [System.Serializable]
    public sealed class ActorProfileEncounterAI
    {
        public string response_policy;
        public float alert_duration_seconds;
        public float lost_contact_timeout_seconds;
        public float avoid_distance;
        public float flee_distance;
        public float preferred_combat_distance;
        public float decision_interval_seconds;
        public float replan_distance;
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
