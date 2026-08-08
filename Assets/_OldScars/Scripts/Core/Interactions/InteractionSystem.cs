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

            if (!TryBuildAvailabilityContext(query, query != null && query.LogAvailabilityDetails, out ActionAvailabilityContext context, out string[] itemTags))
                return availableActions;

            List<EvaluatedAction> evaluatedActions = EvaluateCandidateActions(query, context, query.LogAvailabilityDetails);
            for (int index = 0; index < evaluatedActions.Count; index++)
            {
                if (evaluatedActions[index].Result.IsAvailable)
                    availableActions.Add(evaluatedActions[index].Action);
            }

            if (query.LogAvailabilityDetails)
            {
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
            }

            return availableActions;
        }

        public ActionAvailabilityDiagnosticReport GetAvailabilityDiagnostics(InteractionQuery query)
        {
            if (!TryBuildAvailabilityContext(query, false, out ActionAvailabilityContext context, out string[] itemTags))
                return null;

            var report = new ActionAvailabilityDiagnosticReport(
                query.Target.name,
                GetTargetDisplayName(query.Target),
                query.RequiredContext,
                query.ActorTags,
                query.Target.Tags,
                FormatEquippedItemId(query.EquippedItemId),
                itemTags);

            List<EvaluatedAction> evaluatedActions = EvaluateCandidateActions(query, context, false);
            for (int index = 0; index < evaluatedActions.Count; index++)
            {
                EvaluatedAction evaluatedAction = evaluatedActions[index];
                report.AddEntry(new ActionAvailabilityDiagnosticEntry(evaluatedAction.Action, evaluatedAction.Result));
            }

            return report;
        }

        private bool TryBuildAvailabilityContext(
            InteractionQuery query,
            bool logEquippedItemDetails,
            out ActionAvailabilityContext context,
            out string[] itemTags)
        {
            context = null;
            itemTags = new string[0];

            if (query == null)
            {
                Debug.LogError("[InteractionSystem] Cannot evaluate a null interaction query.");
                return false;
            }

            if (query.Database == null)
            {
                Debug.LogError("[InteractionSystem] Cannot evaluate actions without a GameDatabase.");
                return false;
            }

            if (query.Target == null)
            {
                Debug.LogError("[InteractionSystem] Cannot evaluate actions without a target.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(query.RequiredContext))
            {
                Debug.LogError("[InteractionSystem] Cannot evaluate actions without a required context.");
                return false;
            }

            bool hasEquippedItem = !IsNoEquippedItemId(query.EquippedItemId);
            itemTags = GetEquippedItemTags(query.Database, query.EquippedItemId, logEquippedItemDetails);

            context = new ActionAvailabilityContext
            {
                ActorTags = query.ActorTags,
                ActorStats = query.ActorStats,
                TargetTags = query.Target.Tags,
                ItemTags = itemTags,
                EquippedItemId = query.EquippedItemId,
                HasEquippedItem = hasEquippedItem
            };

            return true;
        }

        private List<EvaluatedAction> EvaluateCandidateActions(InteractionQuery query, ActionAvailabilityContext context, bool logAvailabilityDetails)
        {
            var evaluatedActions = new List<EvaluatedAction>();

            foreach (ActionDefinition action in query.Database.GetAllActions())
            {
                if (action == null)
                    continue;

                if (!HasContext(action, query.RequiredContext))
                    continue;

                ActionAvailabilityResult availabilityResult = evaluator.Evaluate(action, context);

                if (logAvailabilityDetails)
                    LogAvailabilityDetail(action, availabilityResult, query);

                evaluatedActions.Add(new EvaluatedAction(action, availabilityResult));
            }

            return evaluatedActions;
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

        private static string[] GetEquippedItemTags(GameDatabase database, string equippedItemId, bool logDetails)
        {
            if (IsNoEquippedItemId(equippedItemId))
            {
                if (logDetails)
                    Debug.Log("[InteractionSystem] No equipped item.");

                return new string[0];
            }

            ItemDefinition item = database.GetItem(equippedItemId);
            if (item == null)
            {
                if (logDetails)
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

        private static string GetTargetDisplayName(WorldObjectTags target)
        {
            if (target == null)
                return null;

            WorldObjectDebugInfo debugInfo = target.GetComponent<WorldObjectDebugInfo>();
            return debugInfo != null ? debugInfo.GetDisplayNameOrFallback(target.name) : target.name;
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

        private readonly struct EvaluatedAction
        {
            public readonly ActionDefinition Action;
            public readonly ActionAvailabilityResult Result;

            public EvaluatedAction(ActionDefinition action, ActionAvailabilityResult result)
            {
                Action = action;
                Result = result;
            }
        }
    }
}
