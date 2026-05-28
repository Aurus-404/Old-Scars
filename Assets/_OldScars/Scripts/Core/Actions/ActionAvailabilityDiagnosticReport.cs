using System.Collections.Generic;

namespace OldScars.Core.Actions
{
    public sealed class ActionAvailabilityDiagnosticReport
    {
        private readonly List<ActionAvailabilityDiagnosticEntry> entries = new List<ActionAvailabilityDiagnosticEntry>();

        public readonly string TargetName;
        public readonly string TargetDisplayName;
        public readonly string RequiredContext;
        public readonly string[] ActorTagsSnapshot;
        public readonly string[] TargetTagsSnapshot;
        public readonly string EquippedItemId;
        public readonly string[] EquippedItemTagsSnapshot;

        public IReadOnlyList<ActionAvailabilityDiagnosticEntry> Entries => entries;

        public ActionAvailabilityDiagnosticReport(
            string targetName,
            string targetDisplayName,
            string requiredContext,
            string[] actorTagsSnapshot,
            string[] targetTagsSnapshot,
            string equippedItemId,
            string[] equippedItemTagsSnapshot)
        {
            TargetName = targetName;
            TargetDisplayName = targetDisplayName;
            RequiredContext = requiredContext;
            ActorTagsSnapshot = Copy(actorTagsSnapshot);
            TargetTagsSnapshot = Copy(targetTagsSnapshot);
            EquippedItemId = equippedItemId;
            EquippedItemTagsSnapshot = Copy(equippedItemTagsSnapshot);
        }

        internal void AddEntry(ActionAvailabilityDiagnosticEntry entry)
        {
            if (entry != null)
                entries.Add(entry);
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
