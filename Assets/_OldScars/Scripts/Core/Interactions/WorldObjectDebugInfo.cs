using UnityEngine;

namespace OldScars.Core.Interactions
{
    public sealed class WorldObjectDebugInfo : MonoBehaviour
    {
        [System.Serializable]
        private sealed class ConditionalInspectionText
        {
            public int priority;
            public string[] requiredTags;
            public string[] forbiddenTags;
            public string displayName;
            [TextArea(2, 6)] public string inspectText;
        }

        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 6)] private string inspectText;
        [SerializeField] private ConditionalInspectionText[] conditionalInspectionTexts;

        public string DisplayName => displayName;
        public string InspectText => inspectText;

        public void SetRuntimeDisplayName(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                displayName = value;
        }

        public void SetRuntimeInspectText(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                inspectText = value;
        }

        public string GetDisplayNameOrFallback(string fallbackName)
        {
            return !string.IsNullOrWhiteSpace(displayName) ? displayName : fallbackName;
        }

        public string GetDisplayNameOrFallback(string fallbackName, WorldObjectTags tags)
        {
            ConditionalInspectionText conditionalText = GetBestConditionalText(tags);
            if (conditionalText != null && !string.IsNullOrWhiteSpace(conditionalText.displayName))
                return conditionalText.displayName;

            return GetDisplayNameOrFallback(fallbackName);
        }

        public string GetInspectTextOrFallback()
        {
            return !string.IsNullOrWhiteSpace(inspectText)
                ? inspectText
                : "No hay texto de inspeccion configurado para este objeto.";
        }

        public string GetInspectTextOrFallback(WorldObjectTags tags)
        {
            ConditionalInspectionText conditionalText = GetBestConditionalText(tags);
            if (conditionalText != null && !string.IsNullOrWhiteSpace(conditionalText.inspectText))
                return conditionalText.inspectText;

            return GetInspectTextOrFallback();
        }

        private ConditionalInspectionText GetBestConditionalText(WorldObjectTags tags)
        {
            if (tags == null || conditionalInspectionTexts == null || conditionalInspectionTexts.Length == 0)
                return null;

            string[] runtimeTags = tags.RuntimeTags;
            ConditionalInspectionText bestText = null;
            int bestPriority = int.MinValue;

            for (int index = 0; index < conditionalInspectionTexts.Length; index++)
            {
                ConditionalInspectionText candidate = conditionalInspectionTexts[index];
                if (candidate == null || !MatchesTags(candidate, runtimeTags))
                    continue;

                if (bestText == null || candidate.priority > bestPriority)
                {
                    bestText = candidate;
                    bestPriority = candidate.priority;
                }
            }

            return bestText;
        }

        private static bool MatchesTags(ConditionalInspectionText candidate, string[] runtimeTags)
        {
            if (!HasRequiredTags(candidate.requiredTags, runtimeTags))
                return false;

            return !HasForbiddenTags(candidate.forbiddenTags, runtimeTags);
        }

        private static bool HasRequiredTags(string[] requiredTags, string[] runtimeTags)
        {
            if (requiredTags == null || requiredTags.Length == 0)
                return true;

            for (int index = 0; index < requiredTags.Length; index++)
            {
                string requiredTag = requiredTags[index];
                if (!string.IsNullOrWhiteSpace(requiredTag) && !ContainsTag(runtimeTags, requiredTag))
                    return false;
            }

            return true;
        }

        private static bool HasForbiddenTags(string[] forbiddenTags, string[] runtimeTags)
        {
            if (forbiddenTags == null || forbiddenTags.Length == 0)
                return false;

            for (int index = 0; index < forbiddenTags.Length; index++)
            {
                string forbiddenTag = forbiddenTags[index];
                if (!string.IsNullOrWhiteSpace(forbiddenTag) && ContainsTag(runtimeTags, forbiddenTag))
                    return true;
            }

            return false;
        }

        private static bool ContainsTag(string[] runtimeTags, string tag)
        {
            if (runtimeTags == null || runtimeTags.Length == 0 || string.IsNullOrWhiteSpace(tag))
                return false;

            for (int index = 0; index < runtimeTags.Length; index++)
            {
                if (runtimeTags[index] == tag)
                    return true;
            }

            return false;
        }
    }
}
