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
        private const float PanelPadding = 8f;
        private const float HeaderLineHeight = 20f;
        private const int HeaderLineCount = 3;
        private const float HeaderBottomSpacing = 8f;
        private const float FooterTopSpacing = 8f;
        private const float FooterHeight = 34f;
        private const float ActionRowHeight = 28f;
        private const float ActionRowSpacing = 4f;
        private const float EmptyBodyHeight = 20f;
        private const float MaximumActionBodyHeight = 124f;
        private const float ScrollbarWidth = 16f;
        private const float CloseButtonWidth = 120f;
        private const float CloseButtonHeight = 24f;

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
            Rect panelRect = GetPanelRect();
            int actionCount = actions.Count + worldQuickActions.Count;
            ActionPanelLayout layout = CalculateActionPanelLayout(actionCount, panelRect.height);

            GUI.Box(panelRect, GUIContent.none);
            GUI.BeginGroup(panelRect);
            float contentWidth = Mathf.Max(1f, panelRect.width - PanelPadding * 2f);
            float y = PanelPadding;
            GUI.Label(new Rect(PanelPadding, y, contentWidth, HeaderLineHeight), "Contextual Actions (Debug)");
            y += HeaderLineHeight;
            GUI.Label(new Rect(PanelPadding, y, contentWidth, HeaderLineHeight), $"Target: {SafeText(GetTargetName())}");
            y += HeaderLineHeight;
            GUI.Label(new Rect(PanelPadding, y, contentWidth, HeaderLineHeight), $"Equipado: {SafeText(itemId)}");
            y += HeaderLineHeight + HeaderBottomSpacing;

            var bodyRect = new Rect(PanelPadding, y, contentWidth, layout.BodyViewportHeight);
            DrawActionBody(bodyRect, layout);
            y = bodyRect.yMax + FooterTopSpacing;

            var footerRect = new Rect(PanelPadding, y, contentWidth, FooterHeight);
            GUI.Box(footerRect, GUIContent.none);
            var closeRect = new Rect(
                footerRect.x + Mathf.Max(0f, (footerRect.width - CloseButtonWidth) * 0.5f),
                footerRect.y + Mathf.Max(0f, (footerRect.height - CloseButtonHeight) * 0.5f),
                Mathf.Min(CloseButtonWidth, footerRect.width),
                CloseButtonHeight);
            if (GUI.Button(closeRect, "Close"))
                Hide();
            GUI.EndGroup();
        }

        private void DrawActionBody(Rect bodyRect, ActionPanelLayout layout)
        {
            int actionCount = actions.Count + worldQuickActions.Count;
            if (actionCount == 0)
            {
                GUI.Label(bodyRect, "No hay acciones disponibles");
                return;
            }

            var buttonStyle = new GUIStyle(GUI.skin.button)
            {
                wordWrap = true
            };
            if (!layout.HasOverflow)
            {
                scrollPosition = Vector2.zero;
                DrawActionButtons(bodyRect, buttonStyle);
                return;
            }

            float viewWidth = Mathf.Max(1f, bodyRect.width - ScrollbarWidth);
            var viewRect = new Rect(0f, 0f, viewWidth, layout.BodyContentHeight);
            scrollPosition.x = 0f;
            scrollPosition = GUI.BeginScrollView(bodyRect, scrollPosition, viewRect, false, true);
            DrawActionButtons(viewRect, buttonStyle);
            GUI.EndScrollView();
            scrollPosition.x = 0f;
        }

        private void DrawActionButtons(Rect contentRect, GUIStyle buttonStyle)
        {
            float y = contentRect.y;
            for (int index = 0; index < actions.Count; index++)
            {
                ActionDefinition action = actions[index];
                if (GUI.Button(new Rect(contentRect.x, y, contentRect.width, ActionRowHeight), GetActionLabel(action), buttonStyle))
                    TryStartAction(action);
                y += ActionRowHeight + ActionRowSpacing;
            }

            for (int index = 0; index < worldQuickActions.Count; index++)
            {
                InventoryContextAction action = worldQuickActions[index];
                bool previousEnabled = GUI.enabled;
                GUI.enabled = previousEnabled && action.Enabled;
                if (GUI.Button(new Rect(contentRect.x, y, contentRect.width, ActionRowHeight), action.Label, buttonStyle))
                    TryStartWorldQuickAction(action);
                GUI.enabled = previousEnabled;
                y += ActionRowHeight + ActionRowSpacing;
            }
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
            ActionPanelLayout layout = CalculateActionPanelLayout(
                actions.Count + worldQuickActions.Count,
                Mathf.Max(1f, Screen.height));
            return new Vector2(
                Mathf.Min(PanelWidth, Mathf.Max(0f, Screen.width)),
                Mathf.Min(layout.PanelHeight, Mathf.Max(0f, Screen.height)));
        }

        private static ActionPanelLayout CalculateActionPanelLayout(int actionCount, float availableHeight)
        {
            float bodyContentHeight = GetActionContentHeight(actionCount);
            float fixedHeight = PanelPadding * 2f +
                                HeaderLineHeight * HeaderLineCount +
                                HeaderBottomSpacing +
                                FooterTopSpacing +
                                FooterHeight;
            float availableBodyHeight = Mathf.Max(1f, availableHeight - fixedHeight);
            float bodyViewportHeight = Mathf.Min(
                bodyContentHeight,
                Mathf.Min(MaximumActionBodyHeight, availableBodyHeight));
            return new ActionPanelLayout(
                bodyContentHeight,
                bodyViewportHeight,
                Mathf.Min(availableHeight, fixedHeight + bodyViewportHeight));
        }

        private static float GetActionContentHeight(int actionCount)
        {
            if (actionCount <= 0)
                return EmptyBodyHeight;

            return actionCount * ActionRowHeight + Mathf.Max(0, actionCount - 1) * ActionRowSpacing;
        }

        private readonly struct ActionPanelLayout
        {
            internal ActionPanelLayout(float bodyContentHeight, float bodyViewportHeight, float panelHeight)
            {
                BodyContentHeight = bodyContentHeight;
                BodyViewportHeight = bodyViewportHeight;
                PanelHeight = panelHeight;
            }

            internal float BodyContentHeight { get; }
            internal float BodyViewportHeight { get; }
            internal float PanelHeight { get; }
            internal bool HasOverflow => BodyContentHeight > BodyViewportHeight;
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
