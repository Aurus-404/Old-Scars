namespace OldScars.Core.Data.Definitions
{
    /// <summary>
    /// Declares a valid tag. Tags are lightweight identifiers used by items,
    /// actions and future entities/interactions to connect systems without
    /// hardcoding specific object classes.
    /// </summary>
    [System.Serializable]
    public sealed class TagDefinition
    {
        public string id;
        public string description;
    }
}
