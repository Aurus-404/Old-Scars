namespace OldScars.Core.Data.Definitions
{
    [System.Serializable]
    public sealed class WorldObjectProfileDefinition
    {
        public string type; // must be "world_object_profile"
        public string id;
        public string display_name;
        public string[] initial_tags;
        public string penetration_profile_id; // optional PenetrationProfileDefinition.id; absent means opaque
        public object loot_table_id; // Unsupported schema shim for explicit validation.
    }
}
