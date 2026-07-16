using System;
using OldScars.Core.Data.Definitions;
using UnityEngine;

namespace OldScars.Core.Interactions
{
    public sealed class DebugActionProgressController : MonoBehaviour
    {
        [SerializeField] private ContextualActionDebugResultPanel resultPanel;

        private ActionDefinition activeAction;
        private DebugActionExecutionContext activeExecutionContext;
        private string activeActionName;
        private string activeTargetName;
        private float duration;
        private float elapsed;
        private bool isActionInProgress;
        private Func<DebugActionExecutionResult> activeCompletion;

        public bool IsActionInProgress => isActionInProgress;
        public float Progress01 => duration > 0f ? Mathf.Clamp01(elapsed / duration) : 0f;
        public float ElapsedTime => elapsed;
        public float Duration => duration;
        public float RemainingTime => Mathf.Max(0f, duration - elapsed);
        public string ActiveActionName => activeActionName;
        public string ActiveTargetName => activeTargetName;

        private void Awake()
        {
            if (resultPanel == null)
                resultPanel = FindAnyObjectByType<ContextualActionDebugResultPanel>();
        }

        private void Update()
        {
            if (!isActionInProgress)
                return;

            if (activeExecutionContext.Target == null && activeCompletion == null)
            {
                AbortActiveAction("target became invalid before completion");
                return;
            }

            elapsed += Time.deltaTime;

            if (elapsed >= duration)
                CompleteActiveAction();
        }

        public bool TryStartAction(ActionDefinition action, WorldObjectTags target, string equippedItemId)
        {
            return TryStartAction(action, new DebugActionExecutionContext(null, target, equippedItemId));
        }

        public bool TryStartAction(ActionDefinition action, DebugActionExecutionContext executionContext)
        {
            if (isActionInProgress)
            {
                Debug.LogWarning(
                    "[DebugActionProgressController] Cannot start action while another action is in progress." +
                    $"\n  Active action: {SafeText(activeActionName)}" +
                    $"\n  Requested action: {SafeText(GetActionDisplayName(action))}");
                return false;
            }

            if (action == null)
            {
                Debug.LogError("[DebugActionProgressController] Cannot start a null action.");
                return false;
            }

            if (executionContext.Target == null)
            {
                Debug.LogWarning($"[DebugActionProgressController] Cannot start action '{SafeText(GetActionDisplayName(action))}' without a valid target.");
                return false;
            }

            float actionDuration = GetActionDuration(action);

            if (actionDuration <= 0f)
            {
                Debug.Log($"[DebugActionProgressController] Executing action '{SafeText(GetActionDisplayName(action))}' immediately because duration is {actionDuration:0.00}s.");
                ExecuteAction(action, executionContext);
                return true;
            }

            activeAction = action;
            activeExecutionContext = executionContext;
            activeActionName = GetActionDisplayName(action);
            activeTargetName = executionContext.Target.name;
            duration = actionDuration;
            elapsed = 0f;
            isActionInProgress = true;

            Debug.Log(
                "[DebugActionProgressController] Started debug action." +
                $"\n  Action: {SafeText(activeActionName)}" +
                $"\n  Target: {SafeText(activeTargetName)}" +
                $"\n  Duration: {duration:0.00}s");

            return true;
        }

        internal bool TryStartAction(
            ActionDefinition timingAction,
            DebugActionExecutionContext executionContext,
            string displayName,
            Func<DebugActionExecutionResult> completion)
        {
            if (isActionInProgress)
            {
                Debug.LogWarning("[DebugActionProgressController] Cannot start a world quick action while another action is in progress.");
                return false;
            }

            if (timingAction == null || executionContext.Target == null || completion == null)
            {
                Debug.LogWarning("[DebugActionProgressController] World quick action requires timing, target, and completion contracts.");
                return false;
            }

            float actionDuration = GetActionDuration(timingAction);
            if (actionDuration <= 0f)
            {
                ShowResult(completion());
                return true;
            }

            activeAction = timingAction;
            activeExecutionContext = executionContext;
            activeActionName = string.IsNullOrWhiteSpace(displayName)
                ? GetActionDisplayName(timingAction)
                : displayName;
            activeTargetName = executionContext.Target.name;
            duration = actionDuration;
            elapsed = 0f;
            activeCompletion = completion;
            isActionInProgress = true;

            Debug.Log(
                "[DebugActionProgressController] Started world quick action." +
                $"\n  Action: {SafeText(activeActionName)}" +
                $"\n  Target: {SafeText(activeTargetName)}" +
                $"\n  Duration: {duration:0.00}s");
            return true;
        }

        public bool TryCancelActiveAction(string reason)
        {
            if (!isActionInProgress)
                return false;

            string cancelledActionName = activeActionName;
            string cancelledTargetName = activeTargetName;
            ClearActiveAction();

            Debug.Log(
                "[DebugActionProgressController] Cancelled debug action." +
                $"\n  Action: {SafeText(cancelledActionName)}" +
                $"\n  Target: {SafeText(cancelledTargetName)}" +
                $"\n  Reason: {SafeText(reason)}");
            return true;
        }

        private void CompleteActiveAction()
        {
            ActionDefinition completedAction = activeAction;
            DebugActionExecutionContext completedExecutionContext = activeExecutionContext;
            WorldObjectTags completedTarget = completedExecutionContext.Target;
            string completedActionName = activeActionName;
            string completedTargetName = activeTargetName;
            Func<DebugActionExecutionResult> completedCallback = activeCompletion;

            ClearActiveAction();

            if (completedTarget == null && completedCallback == null)
            {
                Debug.LogWarning($"[DebugActionProgressController] Action '{SafeText(completedActionName)}' aborted because target became invalid at completion.");
                return;
            }

            Debug.Log(
                "[DebugActionProgressController] Completed debug action." +
                $"\n  Action: {SafeText(completedActionName)}" +
                $"\n  Target: {SafeText(completedTargetName)}");

            if (completedCallback != null)
                ShowResult(completedCallback());
            else
                ExecuteAction(completedAction, completedExecutionContext);
        }

        private void AbortActiveAction(string reason)
        {
            string abortedActionName = activeActionName;
            string abortedTargetName = activeTargetName;

            ClearActiveAction();

            Debug.LogWarning(
                "[DebugActionProgressController] Aborted debug action." +
                $"\n  Action: {SafeText(abortedActionName)}" +
                $"\n  Target: {SafeText(abortedTargetName)}" +
                $"\n  Reason: {SafeText(reason)}");
        }

        private void ExecuteAction(ActionDefinition action, DebugActionExecutionContext executionContext)
        {
            DebugActionExecutionResult result = DebugActionExecutor.Execute(action, executionContext);
            ShowResult(result);
        }

        private void ShowResult(DebugActionExecutionResult result)
        {
            if (!result.hasResult)
                return;

            if (resultPanel == null)
                resultPanel = FindAnyObjectByType<ContextualActionDebugResultPanel>();

            if (resultPanel == null)
            {
                Debug.LogWarning("[DebugActionProgressController] ContextualActionDebugResultPanel was not found in the scene.");
                return;
            }

            resultPanel.Show(result);
        }

        private void ClearActiveAction()
        {
            activeAction = null;
            activeExecutionContext = default;
            activeActionName = null;
            activeTargetName = null;
            activeCompletion = null;
            duration = 0f;
            elapsed = 0f;
            isActionInProgress = false;
        }

        private static float GetActionDuration(ActionDefinition action)
        {
            if (action == null || action.cost == null)
                return 0f;

            return Mathf.Max(0f, action.cost.time);
        }

        private static string GetActionDisplayName(ActionDefinition action)
        {
            if (action == null)
                return null;

            if (action.display != null && !string.IsNullOrWhiteSpace(action.display.name))
                return action.display.name;

            return action.id;
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
        }
    }
}
