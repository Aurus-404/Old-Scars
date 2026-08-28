namespace OldScars.Core.Data.Definitions
{
    /// <summary>
    /// Immutable firearm behavior loaded from JSON.
    /// Runtime aiming, ammo consumption and raycasts stay in C#.
    /// </summary>
    [System.Serializable]
    public sealed class FirearmProfileDefinition
    {
        public string type; // must be "firearm_profile"
        public string id;
        public string display_name;
        // Canonical trigger/action policy consumed by player input adapters.
        // Ammo, cycle state and physical resolution remain runtime service concerns.
        public string fire_mode;
        public string[] accepted_ammo_profile_ids;
        public int magazine_capacity;
        public float reload_duration;
        public float range;
        public float cycle_time;
        public float muzzle_offset;
        public float debug_accuracy_spread;
    }
}
