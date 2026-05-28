namespace OldScars.Core.Data.Definitions
{
    /// <summary>
    /// Immutable loot table definition loaded from JSON.
    ///
    /// Milestone 15 scope: deterministic entries only. No rarity, chances,
    /// weights, random rolls, conditions, economy, containers, or save data.
    /// </summary>
    [System.Serializable]
    public sealed class LootTableDefinition
    {
        public string type; // must be "loot_table"
        public string id;
        public LootTableEntryDefinition[] entries;
    }

    [System.Serializable]
    public sealed class LootTableEntryDefinition
    {
        public string item_id;
        public int count;
    }
}
