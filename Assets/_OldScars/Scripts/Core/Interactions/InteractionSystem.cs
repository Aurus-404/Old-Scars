using System.Collections.Generic;
using OldScars.Core.Actions;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using UnityEngine;

namespace OldScars.Core.Interactions
{
    public sealed class InteractionQuery
    {
        public GameDatabase Database;
        public string[] ActorTags;
        public Dictionary<string, float> ActorStats;
        public string EquippedItemId;
        public WorldObjectTags Target;
        public string RequiredContext;
        public bool LogAvailabilityDetails;
    }

    public sealed class InteractionSystem
    {
        private const string NoEquippedItemId = "none";

        private readonly ActionAvailabilityEvaluator evaluator = new ActionAvailabilityEvaluator();

        public List<ActionDefinition> GetAvailableActions(InteractionQuery query)
        {
            var availableActions = new List<ActionDefinition>();

            if (query == null)
            {
                Debug.LogError("[InteractionSystem] Cannot evaluate a null interaction query.");
                return availableActions;
            }

            if (query.Database == null)
            {
                Debug.LogError("[InteractionSystem] Cannot evaluate actions without a GameDatabase.");
                return availableActions;
            }

            if (query.Target == null)
            {
                Debug.LogError("[InteractionSystem] Cannot evaluate actions without a target.");
                return availableActions;
            }

            if (string.IsNullOrWhiteSpace(query.RequiredContext))
            {
                Debug.LogError("[InteractionSystem] Cannot evaluate actions without a required context.");
                return availableActions;
            }

            bool hasEquippedItem = !IsNoEquippedItemId(query.EquippedItemId);
            string[] itemTags = GetEquippedItemTags(query.Database, query.EquippedItemId);

            var context = new ActionAvailabilityContext
            {
                ActorTags = query.ActorTags,
                ActorStats = query.ActorStats,
                TargetTags = query.Target.Tags,
                ItemTags = itemTags,
                EquippedItemId = query.EquippedItemId,
                HasEquippedItem = hasEquippedItem
            };

            foreach (ActionDefinition action in query.Database.GetAllActions())
            {
                if (action == null)
                    continue;

                if (!HasContext(action, query.RequiredContext))
                    continue;

                ActionAvailabilityResult availabilityResult = evaluator.Evaluate(action, context);

                if (query.LogAvailabilityDetails)
                    LogAvailabilityDetail(action, availabilityResult, query);

                if (availabilityResult.IsAvailable)
                    availableActions.Add(action);
            }

            Debug.Log(
                "[InteractionSystem] Available actions:" +
                $"\n  Target: {query.Target.name}" +
                $"\n  Target tags: {FormatStrings(query.Target.Tags)}" +
                $"\n  Actor tags: {FormatStrings(query.ActorTags)}" +
                $"\n  Actor stats: {FormatStats(query.ActorStats)}" +
                $"\n  Equipped item: {FormatEquippedItemId(query.EquippedItemId)}" +
                $"\n  Equipped item tags: {FormatStrings(itemTags)}" +
                $"\n  Required context: {query.RequiredContext}" +
                $"\n  Actions: {FormatActionIds(availableActions)}");

            return availableActions;
        }

        private static void LogAvailabilityDetail(ActionDefinition action, ActionAvailabilityResult result, InteractionQuery query)
        {
            if (action == null || result == null || query == null)
                return;

            Debug.Log(
                "[InteractionSystem] Action availability detail:" +
                $"\n  Action: {SafeActionId(action)}" +
                $"\n  Result: {(result.IsAvailable ? "available" : "blocked")}" +
                $"\n  Target: {(query.Target != null ? query.Target.name : "(none)")}" +
                $"\n  Equipped item: {FormatEquippedItemId(query.EquippedItemId)}" +
                $"\n  Required equipped item tags (weapon_tags): {FormatStrings(result.RequiredItemTags)}" +
                $"\n  Matched equipped item tags: {FormatStrings(result.MatchedItemTags)}" +
                $"\n  Missing equipped item tags: {FormatStrings(result.MissingItemTags)}" +
                $"\n  Missing actor tags: {FormatStrings(result.MissingActorTags)}" +
                $"\n  Missing target tags: {FormatStrings(result.MissingTargetTags)}" +
                $"\n  Success reasons: {FormatStrings(result.SuccessReasons)}" +
                $"\n  Block reasons: {FormatStrings(result.BlockReasons)}");
        }

        private static string[] GetEquippedItemTags(GameDatabase database, string equippedItemId)
        {
            if (IsNoEquippedItemId(equippedItemId))
            {
                Debug.Log("[InteractionSystem] No equipped item.");
                return new string[0];
            }

            ItemDefinition item = database.GetItem(equippedItemId);
            if (item == null)
            {
                Debug.LogWarning($"[InteractionSystem] Equipped item '{equippedItemId}' was not found in GameDatabase. Continuing with empty item tags.");
                return new string[0];
            }

            return item.tags != null ? item.tags : new string[0];
        }

        private static bool IsNoEquippedItemId(string itemId)
        {
            return string.IsNullOrWhiteSpace(itemId) || itemId.Trim().ToLowerInvariant() == NoEquippedItemId;
        }

        private static bool HasContext(ActionDefinition action, string requiredContext)
        {
            if (action == null || action.contexts == null || action.contexts.Length == 0)
                return false;

            for (int index = 0; index < action.contexts.Length; index++)
            {
                if (action.contexts[index] == requiredContext)
                    return true;
            }

            return false;
        }

        private static string FormatActionIds(IReadOnlyList<ActionDefinition> actions)
        {
            if (actions == null || actions.Count == 0)
                return "(none)";

            var ids = new List<string>();

            for (int index = 0; index < actions.Count; index++)
            {
                if (actions[index] != null)
                    ids.Add(actions[index].id);
            }

            return FormatStrings(ids);
        }

        private static string FormatStrings(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
                return "(none)";

            return string.Join(", ", values);
        }

        private static string FormatStats(Dictionary<string, float> values)
        {
            if (values == null || values.Count == 0)
                return "(none)";

            var parts = new List<string>();

            foreach (KeyValuePair<string, float> value in values)
            {
                parts.Add($"{value.Key}: {value.Value}");
            }

            return string.Join(", ", parts);
        }

        private static string FormatEquippedItemId(string itemId)
        {
            return IsNoEquippedItemId(itemId) ? "(none)" : itemId;
        }

        private static string SafeActionId(ActionDefinition action)
        {
            if (action == null || string.IsNullOrWhiteSpace(action.id))
                return "(none)";

            return action.id;
        }
    }
}
