namespace OldScars.Core.Data.Definitions
{
    /// <summary>
    /// Shared combat behavior for a family of weapons.
    ///
    /// Example: a crowbar, a metal pipe and a wrench may share an
    /// improvised_blunt profile while keeping different item stats.
    /// Complex combat logic stays in C# systems; JSON only points to data.
    /// </summary>
    [System.Serializable]
    public sealed class WeaponProfileDefinition
    {
        public string type; // must be "weapon_profile"
        public string id;
        public string damage_type;
        public string[] scales_with;
        public bool condition_affects_damage;
        public bool condition_affects_accuracy;
        public string[] default_actions; // references ActionDefinition.id[]
        public string armor_interaction;
        public float melee_range;
        public float attack_duration;
        public float attack_cooldown;
        public string wound_type;
        public float wound_severity;
        public float bleeding_rate_per_game_hour;
        public float pain_contribution;
    }
}
