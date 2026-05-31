using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OldScars.Core.Feedback
{
    public sealed class DebugFeedbackLogPanel : MonoBehaviour
    {
        private const float PanelWidth = 520f;
        private const float PanelHeight = 260f;

        [SerializeField] private GameplayFeedbackLog feedbackLog;
        [SerializeField] private bool visibleOnStart = false;
        [SerializeField] private Key toggleKey = Key.F7;
        [SerializeField] private bool showDebugOnly = true;
        [SerializeField] private int maxVisibleEntries = 8;

        private bool isVisible;
        private Vector2 scrollPosition;

        private void Awake()
        {
            isVisible = visibleOnStart;

            if (feedbackLog == null)
                feedbackLog = FindAnyObjectByType<GameplayFeedbackLog>();
        }

        private void Update()
        {
            if (!WasTogglePressed())
                return;

            isVisible = !isVisible;
            scrollPosition = Vector2.zero;
        }

        private void OnGUI()
        {
            if (!isVisible)
                return;

            if (feedbackLog == null)
                feedbackLog = FindAnyObjectByType<GameplayFeedbackLog>();

            if (feedbackLog == null)
                return;

            GUILayout.BeginArea(GetPanelRect(), GUI.skin.box);
            GUILayout.Label("Gameplay Feedback Log (Debug)");

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(210f));
            DrawEntries(feedbackLog.Entries);
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private void DrawEntries(IReadOnlyList<GameplayFeedbackEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                GUILayout.Label("No feedback entries.");
                return;
            }

            int drawnCount = 0;
            int visibleLimit = Mathf.Max(1, maxVisibleEntries);

            for (int index = entries.Count - 1; index >= 0 && drawnCount < visibleLimit; index--)
            {
                GameplayFeedbackEntry entry = entries[index];
                if (entry == null)
                    continue;

                if (entry.debugOnly && !showDebugOnly)
                    continue;

                DrawEntry(entry);
                drawnCount++;
            }

            if (drawnCount == 0)
                GUILayout.Label("No visible feedback entries.");
        }

        private static void DrawEntry(GameplayFeedbackEntry entry)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(FormatHeadline(entry));

            string details = FormatDetails(entry);
            if (!string.IsNullOrWhiteSpace(details))
                GUILayout.Label(details);

            GUILayout.EndVertical();
        }

        private static string FormatHeadline(GameplayFeedbackEntry entry)
        {
            string debugPrefix = entry.debugOnly ? "[debug] " : string.Empty;
            string message = !string.IsNullOrWhiteSpace(entry.fallbackMessage)
                ? entry.fallbackMessage
                : entry.type.ToString();

            return $"{entry.time:0.0}s {debugPrefix}{entry.type}: {message}";
        }

        private static string FormatDetails(GameplayFeedbackEntry entry)
        {
            var parts = new List<string>();

            AddPart(parts, "actor", entry.actorDisplayName, entry.actorId);
            AddPart(parts, "target", entry.targetDisplayName, entry.targetId);
            AddPart(parts, "item", entry.itemDisplayName, entry.itemId);
            AddPart(parts, "action", entry.actionDisplayName, entry.actionId);

            if (entry.quantity > 0)
                parts.Add($"qty={entry.quantity}");

            if (!string.IsNullOrWhiteSpace(entry.needId))
            {
                string needName = !string.IsNullOrWhiteSpace(entry.needDisplayName) ? entry.needDisplayName : entry.needId;
                parts.Add($"need={needName} ({entry.needId}) {entry.needValueBefore:0.#}->{entry.needValueAfter:0.#}/{entry.needMaxValue:0.#} (+{entry.needAmount:0.#})");
            }

            if (entry.addedTags != null && entry.addedTags.Length > 0)
                parts.Add($"added=[{string.Join(", ", entry.addedTags)}]");

            if (entry.removedTags != null && entry.removedTags.Length > 0)
                parts.Add($"removed=[{string.Join(", ", entry.removedTags)}]");

            return string.Join(" | ", parts);
        }

        private static void AddPart(List<string> parts, string label, string displayName, string id)
        {
            if (string.IsNullOrWhiteSpace(displayName) && string.IsNullOrWhiteSpace(id))
                return;

            if (!string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(id) && displayName != id)
            {
                parts.Add($"{label}={displayName} ({id})");
                return;
            }

            parts.Add($"{label}={SafeText(displayName, id)}");
        }

        private static Rect GetPanelRect()
        {
            float x = 24f;
            float y = Mathf.Max(0f, Screen.height - PanelHeight - 24f);
            return new Rect(x, y, PanelWidth, PanelHeight);
        }

        private bool WasTogglePressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || toggleKey == Key.None)
                return false;

            var keyControl = keyboard[toggleKey];
            return keyControl != null && keyControl.wasPressedThisFrame;
        }

        private static string SafeText(string primary, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(primary))
                return primary;

            return !string.IsNullOrWhiteSpace(fallback) ? fallback : "(none)";
        }
    }
}
