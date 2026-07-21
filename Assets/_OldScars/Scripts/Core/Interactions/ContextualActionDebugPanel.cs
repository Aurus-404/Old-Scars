using System;
using System.Collections.Generic;
using OldScars.Core.Actions;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OldScars.Core.Interactions
{
    public sealed class ContextualActionDebugPanel : MonoBehaviour
    {
        private const string DefaultRequiredContext = "world_interaction";
        private const float PanelWidth = 420f;
        private const float MinimumPanelHeight = 150f;
        private const float MaximumPanelHeight = 260f;
        private const float HeaderHeight = 68f;
        private const float FooterHeight = 34f;
        private const float ActionRowHeight = 28f;
        private const float MinimumActionListHeight = 28f;
        private const float MaximumActionListHeight = 116f;

        private readonly List<ActionDefinition> actions = new List<ActionDefinition>();
        private readonly List<InventoryContextAction> worldQuickActions = new List<InventoryContextAction>();
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
        private Func<IReadOnlyList<InventoryContextAction>> worldQuickActionsProvider;
        private Func<InventoryContextAction, bool> worldQuickActionHandler;

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
            ShowActions(
                availableActions,
                target,
                debugItemId,
                mousePosition,
                actorContext,
                actionContext,
                null,
                null);
        }

        public void ShowActions(
            IReadOnlyList<ActionDefinition> availableActions,
            WorldObjectTags target,
            string debugItemId,
            Vector2 mousePosition,
            ActorInteractionContext actorContext,
            string actionContext,
            Func<IReadOnlyList<InventoryContextAction>> quickActionsProvider,
            Func<InventoryContextAction, bool> quickActionHandler)
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
            worldQuickActionsProvider = quickActionsProvider;
            worldQuickActionHandler = quickActionHandler;
            RefreshWorldQuickActions();
            isVisible = true;
        }

        public void Hide()
        {
            isVisible = false;
            actions.Clear();
            worldQuickActions.Clear();
            currentTarget = null;
            currentActorContext = null;
            requiredContext = null;
            itemId = null;
            lastObservedEquippedItemDefinitionId = null;
            worldQuickActionsProvider = null;
            worldQuickActionHandler = null;
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
            GUILayout.Label($"Equipado: {SafeText(itemId)}");

            GUILayout.Space(8f);

            int actionCount = actions.Count + worldQuickActions.Count;
            float actionListHeight = GetActionListHeight(actionCount);
            if (actionCount == 0)
            {
                GUILayout.Label("No hay acciones disponibles");
            }
            else
            {
                var buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    wordWrap = true
                };
                scrollPosition.x = 0f;
                scrollPosition = GUILayout.BeginScrollView(
                    scrollPosition,
                    false,
                    false,
                    GUIStyle.none,
                    GUI.skin.verticalScrollbar,
                    GUILayout.Height(actionListHeight));

                for (int index = 0; index < actions.Count; index++)
                {
                    ActionDefinition action = actions[index];
                    string label = GetActionLabel(action);

                    if (GUILayout.Button(label, buttonStyle, GUILayout.Height(28f), GUILayout.ExpandWidth(true)))
                    {
                        TryStartAction(action);
                    }
                }

                for (int index = 0; index < worldQuickActions.Count; index++)
                {
                    InventoryContextAction action = worldQuickActions[index];
                    bool previousEnabled = GUI.enabled;
                    GUI.enabled = previousEnabled && action.Enabled;
                    if (GUILayout.Button(action.Label, buttonStyle, GUILayout.Height(28f), GUILayout.ExpandWidth(true)))
                        TryStartWorldQuickAction(action);
                    GUI.enabled = previousEnabled;
                }

                GUILayout.EndScrollView();
                scrollPosition.x = 0f;
            }

            GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(34f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(120f), GUILayout.Height(24f)))
                Hide();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

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

        private Vector2 ClampGuiPosition(Vector2 position)
        {
            Vector2 panelSize = GetPanelSize();
            float maxX = Mathf.Max(0f, Screen.width - panelSize.x);
            float maxY = Mathf.Max(0f, Screen.height - panelSize.y);

            return new Vector2(
                Mathf.Clamp(position.x, 0f, maxX),
                Mathf.Clamp(position.y, 0f, maxY));
        }

        private Rect GetPanelRect()
        {
            Vector2 clampedPosition = ClampGuiPosition(guiPosition);
            Vector2 panelSize = GetPanelSize();
            return new Rect(clampedPosition.x, clampedPosition.y, panelSize.x, panelSize.y);
        }

        private Vector2 GetPanelSize()
        {
            return new Vector2(
                Mathf.Min(PanelWidth, Mathf.Max(0f, Screen.width)),
                Mathf.Min(GetPanelHeight(), Mathf.Max(0f, Screen.height)));
        }

        private float GetPanelHeight()
        {
            int actionCount = actions.Count + worldQuickActions.Count;
            float bodyHeight = actionCount == 0
                ? MinimumActionListHeight
                : GetActionListHeight(actionCount);
            return Mathf.Clamp(
                HeaderHeight + bodyHeight + FooterHeight,
                MinimumPanelHeight,
                MaximumPanelHeight);
        }

        private static float GetActionListHeight(int actionCount)
        {
            return Mathf.Clamp(
                Mathf.Max(1, actionCount) * ActionRowHeight,
                MinimumActionListHeight,
                MaximumActionListHeight);
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

        private void TryStartWorldQuickAction(InventoryContextAction action)
        {
            if (action == null || !action.Enabled || worldQuickActionHandler == null)
                return;

            if (worldQuickActionHandler(action))
            {
                Hide();
                return;
            }

            RefreshWorldQuickActions();
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

        private void RefreshWorldQuickActions()
        {
            worldQuickActions.Clear();
            IReadOnlyList<InventoryContextAction> refreshed = worldQuickActionsProvider?.Invoke();
            if (refreshed == null)
                return;

            for (int index = 0; index < refreshed.Count; index++)
            {
                if (refreshed[index] != null)
                    worldQuickActions.Add(refreshed[index]);
            }
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
