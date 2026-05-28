using System.Collections.Generic;

namespace OldScars.Core.Actions
{
    public sealed class ActionAvailabilityResult
    {
        public bool IsAvailable;

        public readonly List<string> SuccessReasons = new List<string>();
        public readonly List<string> BlockReasons = new List<string>();

        public readonly List<string> RequiredActorTags = new List<string>();
        public readonly List<string> RequiredTargetTags = new List<string>();

        // requirements.weapon_tags is the legacy-compatible JSON field name.
        // It currently means required equipped item tags.
        public readonly List<string> RequiredItemTags = new List<string>();

        public readonly List<string> MissingActorTags = new List<string>();
        public readonly List<string> MissingTargetTags = new List<string>();
        public readonly List<string> MissingItemTags = new List<string>();
        public readonly List<string> MatchedItemTags = new List<string>();
    }
}
