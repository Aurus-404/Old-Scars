using UnityEngine;

namespace OldScars.Core.Interactions
{
    public sealed class ContextualActionDebugProgressPanel : MonoBehaviour
    {
        private const float PanelWidth = 420f;
        private const float PanelHeight = 150f;

        [SerializeField] private DebugActionProgressController progressController;

        private void Awake()
        {
            if (progressController == null)
                progressController = FindAnyObjectByType<DebugActionProgressController>();
        }

        private void OnGUI()
        {
            if (progressController == null)
                progressController = FindAnyObjectByType<DebugActionProgressController>();

            if (progressController == null || !progressController.IsActionInProgress)
                return;

            GUILayout.BeginArea(GetPanelRect(), GUI.skin.box);
            GUILayout.Label("Action In Progress (Debug)");
            GUILayout.Label($"Action: {SafeText(progressController.ActiveActionName)}");
            GUILayout.Label($"Target: {SafeText(progressController.ActiveTargetName)}");
            GUILayout.Label($"Time: {progressController.ElapsedTime:0.0}s / {progressController.Duration:0.0}s");
            GUILayout.Label($"Remaining: {progressController.RemainingTime:0.0}s");

            Rect progressRect = GUILayoutUtility.GetRect(1f, 16f, GUILayout.ExpandWidth(true));
            GUI.Box(progressRect, string.Empty);

            Rect fillRect = progressRect;
            fillRect.width *= progressController.Progress01;
            GUI.Box(fillRect, string.Empty);

            GUILayout.EndArea();
        }

        private static Rect GetPanelRect()
        {
            float x = Mathf.Max(0f, (Screen.width - PanelWidth) * 0.5f);
            float y = 24f;
            return new Rect(x, y, PanelWidth, PanelHeight);
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
        }
    }
}
