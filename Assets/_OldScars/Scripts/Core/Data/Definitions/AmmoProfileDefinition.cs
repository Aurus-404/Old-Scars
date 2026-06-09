namespace OldScars.Core.Data.Definitions
{
    /// <summary>
    /// Immutable ammunition behavior loaded from JSON.
    /// Item storage owns quantities; this profile owns caliber and base damage.
    /// </summary>
    [System.Serializable]
    public sealed class AmmoProfileDefinition
    {
        public string type; // must be "ammo_profile"
        public string id;
        public string display_name;
        public string caliber_tag;
        public float damage;
        public string[] tags;
    }
}
