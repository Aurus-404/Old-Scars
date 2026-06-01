using System.Collections.Generic;
using OldScars.Core.Actions;
using OldScars.Core.Data.Definitions;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OldScars.Core.Interactions
{
    public sealed class ContextualActionDebugPanel : MonoBehaviour
    {
        private const string DefaultRequiredContext = "world_interaction";
        private const float PanelWidth = 360f;
        private const float PanelHeight = 260f;

        private readonly List<ActionDefinition> actions = new List<ActionDefinition>();
        private readonly InteractionSystem interactionSystem = new InteractionSystem();

        [SerializeField] private DebugActionProgressController progressController;
        [SerializeField] private ContextualActionDebugResultPanel resultPanel;

        private bool isVisible;
        private WorldObjectTags currentTarget;
        private ActorInteractionContext currentActorContext;
        private string requiredContext;
        private string itemId;
        private string lastObservedEquippedItemDefinitionId;
        private Vector2 guiPosition;
        private Vector2 scrollPosition;

        public bool IsVisible => isVisible;

        private void Awake()
        {
            if (progressController == null)
                progressController = FindAnyObjectByType<DebugActionProgressController>();

            if (resultPanel == null)
                resultPanel = FindAnyObjectByType<ContextualActionDebugResultPanel>();
        }

        public void ShowActions(IReadOnlyList<ActionDefinition> availableActions, WorldObjectTags target, string debugItemId, Vector2 mousePosition)
        {
            ShowActions(availableActions, target, debugItemId, mousePosition, null, DefaultRequiredContext);
        }

        public void ShowActions(IReadOnlyList<ActionDefinition> availableActions, WorldObjectTags target, string debugItemId, Vector2 mousePosition, ActorInteractionContext actorContext)
        {
            ShowActions(availableActions, target, debugItemId, mousePosition, actorContext, DefaultRequiredContext);
        }

        public void ShowActions(IReadOnlyList<ActionDefinition> availableActions, WorldObjectTags target, string debugItemId, Vector2 mousePosition, ActorInteractionContext actorContext, string actionContext)
        {
            actions.Clear();

            if (availableActions != null)
            {
                for (int index = 0; index < availableActions.Count; index++)
                {
                    if (availableActions[index] != null)
                        actions.Add(availableActions[index]);
                }
            }

            currentTarget = target;
            currentActorContext = actorContext;
            requiredContext = string.IsNullOrWhiteSpace(actionContext) ? DefaultRequiredContext : actionContext;
            itemId = debugItemId;
            lastObservedEquippedItemDefinitionId = debugItemId;
            guiPosition = ToGuiPosition(mousePosition);
            scrollPosition = Vector2.zero;
            isVisible = true;
        }

        public void Hide()
        {
            isVisible = false;
            actions.Clear();
            currentTarget = null;
            currentActorContext = null;
            requiredContext = null;
            itemId = null;
            lastObservedEquippedItemDefinitionId = null;
        }

        public bool ContainsScreenPosition(Vector2 screenPosition)
        {
            if (!isVisible)
                return false;

            Vector2 guiPoint = ToGuiPosition(screenPosition);
            return GetPanelRect().Contains(guiPoint);
        }

        private void Update()
        {
            if (!isVisible)
                return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Hide();
                return;
            }

            RefreshActionsIfEquippedItemChanged();
        }

        private void OnGUI()
        {
            if (!isVisible)
                return;

            guiPosition = ClampGuiPosition(guiPosition);

            GUILayout.BeginArea(GetPanelRect(), GUI.skin.box);
            GUILayout.Label("Contextual Actions (Debug)");
            GUILayout.Label($"Target: {SafeText(GetTargetName())}");
            GUILayout.Label($"Item: {SafeText(itemId)}");

            GUILayout.Space(8f);

            if (actions.Count == 0)
            {
                GUILayout.Label("No hay acciones disponibles");
            }
            else
            {
                scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(150f));

                for (int index = 0; index < actions.Count; index++)
                {
                    ActionDefinition action = actions[index];
                    string label = GetActionLabel(action);

                    if (GUILayout.Button(label, GUILayout.Height(28f)))
                    {
                        TryStartAction(action);
                    }
                }

                GUILayout.EndScrollView();
            }

            GUILayout.Space(8f);

            if (GUILayout.Button("Close", GUILayout.Height(24f)))
                Hide();

            GUILayout.EndArea();
        }

        private static string GetActionLabel(ActionDefinition action)
        {
            if (action == null)
                return "(null action)";

            string displayName = action.display != null && !string.IsNullOrWhiteSpace(action.display.name)
                ? action.display.name
                : action.id;

            if (string.IsNullOrWhiteSpace(action.id) || displayName == action.id)
                return displayName;

            return $"{displayName} ({action.id})";
        }

        private static Vector2 ToGuiPosition(Vector2 mousePosition)
        {
            return new Vector2(mousePosition.x, Screen.height - mousePosition.y);
        }

        private static Vector2 ClampGuiPosition(Vector2 position)
        {
            float maxX = Mathf.Max(0f, Screen.width - PanelWidth);
            float maxY = Mathf.Max(0f, Screen.height - PanelHeight);

            return new Vector2(
                Mathf.Clamp(position.x, 0f, maxX),
                Mathf.Clamp(position.y, 0f, maxY));
        }

        private Rect GetPanelRect()
        {
            Vector2 clampedPosition = ClampGuiPosition(guiPosition);
            return new Rect(clampedPosition.x, clampedPosition.y, PanelWidth, PanelHeight);
        }

        private string GetTargetName()
        {
            return currentTarget != null ? currentTarget.name : null;
        }

        private void TryStartAction(ActionDefinition action)
        {
            if (progressController == null)
                progressController = FindAnyObjectByType<DebugActionProgressController>();

            if (progressController == null)
            {
                Debug.LogError("[ContextualActionDebugPanel] DebugActionProgressController was not found in the scene. Add one before executing contextual actions.");
                return;
            }

            if (!TryRevalidateAction(action, out DebugActionExecutionContext executionContext))
                return;

            bool started = progressController.TryStartAction(action, executionContext);

            if (started)
                Hide();
        }

        private bool TryRevalidateAction(ActionDefinition action, out DebugActionExecutionContext executionContext)
        {
            executionContext = default;

            if (action == null)
            {
                ShowUnavailableActionFeedback(action, null, null);
                return false;
            }

            if (!TryBuildCurrentQuery(out InteractionQuery query, out string currentItemId, out string unavailableReason))
            {
                ShowUnavailableActionFeedback(action, null, unavailableReason);
                return false;
            }

            List<ActionDefinition> currentAvailableActions = interactionSystem.GetAvailableActions(query);
            RefreshAvailableActions(currentAvailableActions, currentItemId);

            if (!ContainsAction(currentAvailableActions, action))
            {
                ActionAvailabilityDiagnosticReport diagnosticReport = interactionSystem.GetAvailabilityDiagnostics(query);
                ShowUnavailableActionFeedback(action, diagnosticReport, null);
                return false;
            }

            executionContext = new DebugActionExecutionContext(currentActorContext, currentTarget, currentItemId);
            return true;
        }

        private void RefreshActionsIfEquippedItemChanged()
        {
            if (currentActorContext == null)
                return;

            string currentItemId = currentActorContext.GetEquippedItemDefinitionId();
            if (currentItemId == lastObservedEquippedItemDefinitionId)
                return;

            if (!TryBuildCurrentQuery(out InteractionQuery query, out string queryItemId, out string unavailableReason))
            {
                RefreshAvailableActions(null, currentItemId);
                Debug.LogWarning($"[ContextualActionDebugPanel] Contextual action refresh skipped: {unavailableReason}");
                return;
            }

            List<ActionDefinition> currentAvailableActions = interactionSystem.GetAvailableActions(query);
            RefreshAvailableActions(currentAvailableActions, queryItemId);
        }

        private bool TryBuildCurrentQuery(out InteractionQuery query, out string currentItemId, out string unavailableReason)
        {
            query = default;
            currentItemId = null;
            unavailableReason = null;

            if (currentActorContext == null)
            {
                unavailableReason = "Actor context is missing.";
                return false;
            }

            if (currentTarget == null)
            {
                unavailableReason = "Target is missing.";
                return false;
            }

            if (GameDataManager.Instance == null || !GameDataManager.Instance.IsReady || GameDataManager.Instance.Database == null)
            {
                unavailableReason = "Game data is not ready.";
                return false;
            }

            currentItemId = currentActorContext.GetEquippedItemDefinitionId();
            query = new InteractionQuery
            {
                Database = GameDataManager.Instance.Database,
                ActorTags = currentActorContext.ActorTags,
                ActorStats = currentActorContext.BuildActorStatsDictionary(),
                EquippedItemId = currentItemId,
                Target = currentTarget,
                RequiredContext = string.IsNullOrWhiteSpace(requiredContext) ? DefaultRequiredContext : requiredContext,
                LogAvailabilityDetails = false
            };

            return true;
        }

        private void RefreshAvailableActions(IReadOnlyList<ActionDefinition> availableActions, string currentItemId)
        {
            actions.Clear();
            if (availableActions != null)
            {
                for (int index = 0; index < availableActions.Count; index++)
                {
                    if (availableActions[index] != null)
                        actions.Add(availableActions[index]);
                }
            }

            itemId = currentItemId;
            lastObservedEquippedItemDefinitionId = currentItemId;
        }

        private void ShowUnavailableActionFeedback(ActionDefinition action, ActionAvailabilityDiagnosticReport diagnosticReport, string explicitReason)
        {
            string actionLabel = GetActionLabel(action);
            string currentItemId = currentActorContext != null ? currentActorContext.GetEquippedItemDefinitionId() : null;
            ActionAvailabilityDiagnosticEntry entry = FindDiagnosticEntry(diagnosticReport, action);
            string blockReasons = explicitReason;
            if (string.IsNullOrWhiteSpace(blockReasons))
                blockReasons = entry != null ? FormatStrings(entry.BlockReasons) : null;

            string message =
                "Requirements changed before the action started." +
                $"\nAction: {SafeText(actionLabel)}" +
                $"\nTarget: {SafeText(GetTargetName())}" +
                $"\nEquipped item now: {SafeText(currentItemId)}" +
                $"\nBlock reasons: {SafeText(blockReasons)}";

            Debug.LogWarning("[ContextualActionDebugPanel] Action no longer available.\n" + message);

            if (resultPanel == null)
                resultPanel = FindAnyObjectByType<ContextualActionDebugResultPanel>();

            if (resultPanel != null)
                resultPanel.Show(DebugActionExecutionResult.Info("Action no longer available", message));
        }

        private static bool ContainsAction(IReadOnlyList<ActionDefinition> availableActions, ActionDefinition action)
        {
            if (availableActions == null || action == null)
                return false;

            for (int index = 0; index < availableActions.Count; index++)
            {
                ActionDefinition availableAction = availableActions[index];
                if (availableAction == null)
                    continue;

                if (ReferenceEquals(availableAction, action))
                    return true;

                if (!string.IsNullOrWhiteSpace(action.id) && availableAction.id == action.id)
                    return true;
            }

            return false;
        }

        private static ActionAvailabilityDiagnosticEntry FindDiagnosticEntry(ActionAvailabilityDiagnosticReport report, ActionDefinition action)
        {
            if (report == null || action == null)
                return null;

            IReadOnlyList<ActionAvailabilityDiagnosticEntry> entries = report.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                ActionAvailabilityDiagnosticEntry entry = entries[index];
                if (entry == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(action.id) && entry.ActionId == action.id)
                    return entry;
            }

            return null;
        }

        private static string FormatStrings(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
                return null;

            return string.Join(", ", values);
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
        }
    }
}
