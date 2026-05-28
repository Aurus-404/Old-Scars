using System.Collections.Generic;
using OldScars.Core.Data.Definitions;

namespace OldScars.Core.Actions
{
    public sealed class ActionAvailabilityEvaluator
    {
        public bool IsAvailable(ActionDefinition action, ActionAvailabilityContext context)
        {
            return Evaluate(action, context).IsAvailable;
        }

        public ActionAvailabilityResult Evaluate(ActionDefinition action, ActionAvailabilityContext context)
        {
            var result = new ActionAvailabilityResult();

            if (action == null)
            {
                result.BlockReasons.Add("action is null");
                return result;
            }

            if (context == null)
            {
                result.BlockReasons.Add("availability context is null");
                return result;
            }

            if (action.requirements == null)
            {
                result.IsAvailable = true;
                result.SuccessReasons.Add("action has no requirements");
                return result;
            }

            bool isAvailable = true;

            if (!EvaluateAllTags(context.ActorTags, action.requirements.actor_tags, result.RequiredActorTags, result.MissingActorTags))
            {
                isAvailable = false;
                result.BlockReasons.Add($"missing actor tags: {FormatStrings(result.MissingActorTags)}");
            }
            else if (result.RequiredActorTags.Count > 0)
            {
                result.SuccessReasons.Add($"actor tags satisfied: {FormatStrings(result.RequiredActorTags)}");
            }

            if (!EvaluateActorStats(context.ActorStats, action.requirements.actor_min_stats, result))
                isAvailable = false;

            if (!EvaluateAllTags(context.TargetTags, action.requirements.target_tags, result.RequiredTargetTags, result.MissingTargetTags))
            {
                isAvailable = false;
                result.BlockReasons.Add($"missing target tags: {FormatStrings(result.MissingTargetTags)}");
            }
            else if (result.RequiredTargetTags.Count > 0)
            {
                result.SuccessReasons.Add($"target tags satisfied: {FormatStrings(result.RequiredTargetTags)}");
            }

            if (!EvaluateRequiredItemTags(context, action.requirements.weapon_tags, result))
                isAvailable = false;

            result.IsAvailable = isAvailable;

            if (result.IsAvailable && result.SuccessReasons.Count == 0)
                result.SuccessReasons.Add("all requirements satisfied");

            return result;
        }

        public List<ActionDefinition> GetAvailableActions(IEnumerable<ActionDefinition> actions, ActionAvailabilityContext context)
        {
            var availableActions = new List<ActionDefinition>();

            if (actions == null)
                return availableActions;

            foreach (ActionDefinition action in actions)
            {
                if (IsAvailable(action, context))
                    availableActions.Add(action);
            }

            return availableActions;
        }

        private static bool EvaluateAllTags(string[] availableTags, string[] requiredTags, List<string> requiredOutput, List<string> missingOutput)
        {
            AddRequiredTags(requiredTags, requiredOutput);

            if (requiredOutput.Count == 0)
                return true;

            if (availableTags == null || availableTags.Length == 0)
            {
                AddRange(requiredOutput, missingOutput);
                return false;
            }

            for (int requiredIndex = 0; requiredIndex < requiredOutput.Count; requiredIndex++)
            {
                string requiredTag = requiredOutput[requiredIndex];

                if (!ContainsTag(availableTags, requiredTag))
                    missingOutput.Add(requiredTag);
            }

            return missingOutput.Count == 0;
        }

        private static bool EvaluateRequiredItemTags(ActionAvailabilityContext context, string[] requiredTags, ActionAvailabilityResult result)
        {
            AddRequiredTags(requiredTags, result.RequiredItemTags);

            if (result.RequiredItemTags.Count == 0)
                return true;

            if (context.ItemTags == null || context.ItemTags.Length == 0)
            {
                AddRange(result.RequiredItemTags, result.MissingItemTags);
                AddMissingItemBlockReason(context, result);
                return false;
            }

            for (int requiredIndex = 0; requiredIndex < result.RequiredItemTags.Count; requiredIndex++)
            {
                string requiredTag = result.RequiredItemTags[requiredIndex];

                if (ContainsTag(context.ItemTags, requiredTag))
                    result.MatchedItemTags.Add(requiredTag);
            }

            if (result.MatchedItemTags.Count > 0)
            {
                result.SuccessReasons.Add($"matched required equipped item tags: {FormatStrings(result.MatchedItemTags)}");
                return true;
            }

            AddRange(result.RequiredItemTags, result.MissingItemTags);
            AddMissingItemBlockReason(context, result);
            return false;
        }

        private static bool EvaluateActorStats(Dictionary<string, float> actorStats, Dictionary<string, float> minimumStats, ActionAvailabilityResult result)
        {
            if (minimumStats == null || minimumStats.Count == 0)
                return true;

            var satisfiedStats = new List<string>();
            var missingStats = new List<string>();

            foreach (KeyValuePair<string, float> minimumStat in minimumStats)
            {
                if (string.IsNullOrWhiteSpace(minimumStat.Key))
                    continue;

                if (actorStats == null || !actorStats.TryGetValue(minimumStat.Key, out float actorValue))
                {
                    missingStats.Add($"{minimumStat.Key} >= {FormatFloat(minimumStat.Value)} (missing)");
                    continue;
                }

                if (actorValue < minimumStat.Value)
                {
                    missingStats.Add($"{minimumStat.Key} >= {FormatFloat(minimumStat.Value)} (actual {FormatFloat(actorValue)})");
                    continue;
                }

                satisfiedStats.Add($"{minimumStat.Key} >= {FormatFloat(minimumStat.Value)}");
            }

            if (missingStats.Count > 0)
            {
                result.BlockReasons.Add($"missing actor stats: {FormatStrings(missingStats)}");
                return false;
            }

            if (satisfiedStats.Count > 0)
                result.SuccessReasons.Add($"actor stats satisfied: {FormatStrings(satisfiedStats)}");

            return true;
        }

        private static void AddRequiredTags(string[] requiredTags, List<string> output)
        {
            if (requiredTags == null || output == null)
                return;

            for (int index = 0; index < requiredTags.Length; index++)
            {
                string requiredTag = requiredTags[index];
                if (string.IsNullOrWhiteSpace(requiredTag))
                    continue;

                output.Add(requiredTag);
            }
        }

        private static void AddRange(List<string> source, List<string> destination)
        {
            if (source == null || destination == null)
                return;

            for (int index = 0; index < source.Count; index++)
            {
                destination.Add(source[index]);
            }
        }

        private static void AddMissingItemBlockReason(ActionAvailabilityContext context, ActionAvailabilityResult result)
        {
            string requiredTags = FormatStrings(result.RequiredItemTags);

            if (context.HasEquippedItem)
            {
                result.BlockReasons.Add($"equipped item {SafeText(context.EquippedItemId)} missing required equipped item tags: {requiredTags}");
                return;
            }

            result.BlockReasons.Add($"no equipped item; requires one of: {requiredTags}");
        }

        private static bool ContainsTag(string[] tags, string tag)
        {
            if (tags == null || string.IsNullOrWhiteSpace(tag))
                return false;

            for (int index = 0; index < tags.Length; index++)
            {
                if (tags[index] == tag)
                    return true;
            }

            return false;
        }

        private static string FormatStrings(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
                return "(none)";

            return string.Join(", ", values);
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.##");
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
        }
    }
}
