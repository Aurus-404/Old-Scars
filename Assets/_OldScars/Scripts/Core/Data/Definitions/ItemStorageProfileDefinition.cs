namespace OldScars.Core.Data.Definitions
{
    /// <summary>
    /// Data-driven spatial storage profile owned by a runtime ItemInstance.
    /// </summary>
    [System.Serializable]
    public sealed class ItemStorageProfileDefinition
    {
        public string type; // must be "item_storage_profile"
        public string id;
        public string display_name;
        public int width;
        public int height;
        public float? max_content_weight_kg; // Absent = unlimited physical content mass.
        public float? carried_weight_multiplier; // Absent = 1; applies to contents only while equipped.
    }
}
