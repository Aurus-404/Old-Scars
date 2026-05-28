using System.Collections.Generic;
using OldScars.Core.Data.Definitions;

namespace OldScars.Core.Actions
{
    public sealed class ActionAvailabilityDiagnosticEntry
    {
        public readonly string ActionId;
        public readonly string ActionDisplayName;
        public readonly bool IsAvailable;
        public readonly string[] SuccessReasons;
        public readonly string[] BlockReasons;
        public readonly string[] RequiredActorTags;
        public readonly string[] RequiredTargetTags;
        public readonly string[] RequiredItemTags;
        public readonly string[] MissingActorTags;
        public readonly string[] MissingTargetTags;
        public readonly string[] MissingItemTags;
        public readonly string[] MatchedItemTags;

        public ActionAvailabilityDiagnosticEntry(ActionDefinition action, ActionAvailabilityResult result)
        {
            ActionId = action != null ? action.id : null;
            ActionDisplayName = GetActionDisplayName(action);
            IsAvailable = result != null && result.IsAvailable;
            SuccessReasons = Copy(result != null ? result.SuccessReasons : null);
            BlockReasons = Copy(result != null ? result.BlockReasons : null);
            RequiredActorTags = Copy(result != null ? result.RequiredActorTags : null);
            RequiredTargetTags = Copy(result != null ? result.RequiredTargetTags : null);
            RequiredItemTags = Copy(result != null ? result.RequiredItemTags : null);
            MissingActorTags = Copy(result != null ? result.MissingActorTags : null);
            MissingTargetTags = Copy(result != null ? result.MissingTargetTags : null);
            MissingItemTags = Copy(result != null ? result.MissingItemTags : null);
            MatchedItemTags = Copy(result != null ? result.MatchedItemTags : null);
        }

        private static string GetActionDisplayName(ActionDefinition action)
        {
            if (action == null)
                return null;

            if (action.display != null && !string.IsNullOrWhiteSpace(action.display.name))
                return action.display.name;

            return action.id;
        }

        private static string[] Copy(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
                return new string[0];

            var copy = new string[values.Count];

            for (int index = 0; index < values.Count; index++)
            {
                copy[index] = values[index];
            }

            return copy;
        }
    }
}
