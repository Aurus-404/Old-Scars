using System.Collections.Generic;

namespace OldScars.Core.Actions
{
    public sealed class ActionAvailabilityContext
    {
        public string[] ActorTags;
        public Dictionary<string, float> ActorStats;
        public string[] TargetTags;
        public string[] ItemTags;
        public string EquippedItemId;
        public bool HasEquippedItem;
    }
}
