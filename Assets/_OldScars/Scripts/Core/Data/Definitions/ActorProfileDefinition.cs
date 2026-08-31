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
        public ActorProfileConsciousness consciousness;
        public ActorProfileInventoryEntry[] initial_inventory;
        public ActorProfileInitialEquipmentEntry[] initial_equipment;
        public string loadout_profile_id; // Optional ActorLoadoutProfileDefinition used only for new runtime spawns.
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
        public ActorProfileVitalIntegrity vital_integrity = new ActorProfileVitalIntegrity();
    }

    [System.Serializable]
    public sealed class ActorProfileVitalIntegrity
    {
        public float damage_scale = 1f;
        public float blunt_factor = 0.35f;
        public float puncture_factor = 1f;
        public float laceration_factor = 0.6f;
        public float head_factor = 1.8f;
        public float torso_factor = 1f;
        public float limb_factor = 0.25f;
    }

    [System.Serializable]
    public sealed class ActorProfileConsciousness
    {
        public float consciousness_resilience;
        public float pain_tolerance;
        public float blunt_trauma_resistance;
        public float dazed_threshold;
        public float incapacitated_threshold;
        public float unconscious_threshold;
        public float blood_pressure_start_fraction;
        public float fatal_blood_fraction;
        public float trauma_recovery_per_game_hour;
        public float blood_recovery_per_game_hour = 0.02f;
        public float recovery_hysteresis = 0.05f;
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
        public float recognition_near_seconds = 0.2f;
        public float recognition_far_seconds = 1.0f;
        public float recognition_decay_seconds = 0.5f;
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
