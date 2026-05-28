using System.Collections.Generic;
using OldScars.Core.Data.Definitions;
using UnityEngine;

namespace OldScars.Core.Actions
{
    public sealed class ActionAvailabilityDebugTester : MonoBehaviour
    {
        private const string TestItemId = "rusted_crowbar_01";
        private const string ExpectedActionId = "force_door";

        private void Start()
        {
            if (GameDataManager.Instance == null)
            {
                Debug.LogError("[ActionAvailabilityDebugTester] GameDataManager.Instance was not found in the scene.");
                return;
            }

            if (!GameDataManager.Instance.IsReady)
            {
                Debug.LogError("[ActionAvailabilityDebugTester] GameDataManager is not ready. CoreDataSystem did not finish loading successfully.");
                return;
            }

            ItemDefinition item = GameDataManager.Instance.Database.GetItem(TestItemId);
            if (item == null)
            {
                Debug.LogError($"[ActionAvailabilityDebugTester] Item '{TestItemId}' was not found in GameDatabase.");
                return;
            }

            var context = new ActionAvailabilityContext
            {
                ActorStats = new Dictionary<string, float>
                {
                    { "strength", 4f }
                },
                TargetTags = new[] { "locked_door" },
                ItemTags = item.tags
            };

            var evaluator = new ActionAvailabilityEvaluator();
            List<ActionDefinition> availableActions = evaluator.GetAvailableActions(
                GameDataManager.Instance.Database.GetAllActions(),
                context);

            bool foundExpectedAction = false;
            var availableActionIds = new List<string>();

            foreach (ActionDefinition action in availableActions)
            {
                if (action == null)
                    continue;

                availableActionIds.Add(action.id);

                if (action.id == ExpectedActionId)
                    foundExpectedAction = true;
            }

            Debug.Log(
                "[ActionAvailabilityDebugTester] Available actions:" +
                $"\n  Actor strength: 4" +
                $"\n  Target tags: locked_door" +
                $"\n  Item: {TestItemId}" +
                $"\n  Actions: {FormatActionIds(availableActionIds)}");

            if (!foundExpectedAction)
            {
                Debug.LogError($"[ActionAvailabilityDebugTester] Expected action '{ExpectedActionId}' was not available.");
                return;
            }

            Debug.Log($"[ActionAvailabilityDebugTester] Expected action '{ExpectedActionId}' is available.");
        }

        private static string FormatActionIds(List<string> actionIds)
        {
            return actionIds != null && actionIds.Count > 0 ? string.Join(", ", actionIds) : "(none)";
        }
    }
}
