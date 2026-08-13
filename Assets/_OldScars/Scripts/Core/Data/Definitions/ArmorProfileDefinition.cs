namespace OldScars.Core.Data.Definitions
{
    /// <summary>
    /// Receiver-agnostic resistance on Old Scars' internal penetration scale.
    /// Wearable armor and penetrable world surfaces reference this same data.
    /// </summary>
    [System.Serializable]
    public sealed class PenetrationProfileDefinition
    {
        public string type; // must be "penetration_profile"
        public string id;
        public string display_name;
        public float resistance = float.NaN;
    }

    /// <summary>
    /// Immutable regional protection loaded from JSON.
    /// Values use Old Scars' shared internal relative protection scale; they are not
    /// physical material or armor-thickness units.
    /// </summary>
    [System.Serializable]
    public sealed class ArmorProfileDefinition
    {
        public string type; // must be "armor_profile"
        public string id;
        public string display_name;
        public string[] covered_regions;
        public string penetration_profile_id;
        public float impact_resistance = float.NaN;
        public float stopped_blunt_transfer = float.NaN;
        public float blunt_wound_threshold = float.NaN;
        public int layer_priority = -1;
    }
}
