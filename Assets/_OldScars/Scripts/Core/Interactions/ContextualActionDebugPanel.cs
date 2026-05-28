using System.Collections.Generic;
using OldScars.Core.Data.Definitions;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OldScars.Core.Interactions
{
    public sealed class ContextualActionDebugPanel : MonoBehaviour
    {
        private const float PanelWidth = 360f;
        private const float PanelHeight = 260f;

        private readonly List<ActionDefinition> actions = new List<ActionDefinition>();

        [SerializeField] private DebugActionProgressController progressController;

        private bool isVisible;
        private WorldObjectTags currentTarget;
        private ActorInteractionContext currentActorContext;
        private string itemId;
        private Vector2 guiPosition;
        private Vector2 scrollPosition;

        public bool IsVisible => isVisible;

        private void Awake()
        {
            if (progressController == null)
                progressController = FindAnyObjectByType<DebugActionProgressController>();
        }

        public void ShowActions(IReadOnlyList<ActionDefinition> availableActions, WorldObjectTags target, string debugItemId, Vector2 mousePosition)
        {
            ShowActions(availableActions, target, debugItemId, mousePosition, null);
        }

        public void ShowActions(IReadOnlyList<ActionDefinition> availableActions, WorldObjectTags target, string debugItemId, Vector2 mousePosition, ActorInteractionContext actorContext)
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
            itemId = debugItemId;
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
            if (!isVisible || Keyboard.current == null)
                return;

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
                Hide();
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

            var executionContext = new DebugActionExecutionContext(currentActorContext, currentTarget, itemId);
            bool started = progressController.TryStartAction(action, executionContext);

            if (started)
                Hide();
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
        }
    }
}
