using System.Collections.Generic;
using OldScars.Core.Actions;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OldScars.Core.Interactions
{
    public sealed class DebugActionAvailabilityPanel : MonoBehaviour
    {
        private const float PanelWidth = 560f;
        private const float PanelHeight = 420f;

        [SerializeField] private bool visibleOnStart = false;
        [SerializeField] private Key toggleKey = Key.F8;
        [SerializeField] private bool showAvailableActions = true;
        [SerializeField] private bool showBlockedActions = true;
        [SerializeField] private int maxVisibleEntries = 24;

        private bool isVisible;
        private ActionAvailabilityDiagnosticReport report;
        private Vector2 scrollPosition;

        private void Awake()
        {
            isVisible = visibleOnStart;
        }

        private void Update()
        {
            if (!WasTogglePressed())
                return;

            isVisible = !isVisible;
            scrollPosition = Vector2.zero;
        }

        public void SetReport(ActionAvailabilityDiagnosticReport diagnosticReport)
        {
            report = diagnosticReport;
        }

        public void Hide()
        {
            isVisible = false;
        }

        private void OnGUI()
        {
            if (!isVisible)
                return;

            GUILayout.BeginArea(GetPanelRect(), GUI.skin.box);
            GUILayout.Label("Action Availability Diagnostics (Debug)");

            if (report == null)
            {
                GUILayout.Label("No diagnostic report.");
                GUILayout.EndArea();
                return;
            }

            DrawSnapshot(report);

            GUILayout.Space(6f);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(270f));
            DrawEntries(report.Entries);
            GUILayout.EndScrollView();

            GUILayout.Space(6f);
            if (GUILayout.Button("Close", GUILayout.Height(24f)))
                Hide();

            GUILayout.EndArea();
        }

        private void DrawSnapshot(ActionAvailabilityDiagnosticReport diagnosticReport)
        {
            GUILayout.Label($"Target: {FormatDisplayWithId(diagnosticReport.TargetDisplayName, diagnosticReport.TargetName)}");
            GUILayout.Label($"Context: {SafeText(diagnosticReport.RequiredContext)}");
            GUILayout.Label($"Actor tags: {FormatStrings(diagnosticReport.ActorTagsSnapshot)}");
            GUILayout.Label($"Target tags: {FormatStrings(diagnosticReport.TargetTagsSnapshot)}");
            GUILayout.Label($"Equipped item: {SafeText(diagnosticReport.EquippedItemId)}");
            GUILayout.Label($"Equipped item tags: {FormatStrings(diagnosticReport.EquippedItemTagsSnapshot)}");
        }

        private void DrawEntries(IReadOnlyList<ActionAvailabilityDiagnosticEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                GUILayout.Label("No candidate actions.");
                return;
            }

            int drawnCount = 0;
            int visibleLimit = Mathf.Max(1, maxVisibleEntries);

            for (int index = 0; index < entries.Count && drawnCount < visibleLimit; index++)
            {
                ActionAvailabilityDiagnosticEntry entry = entries[index];
                if (entry == null)
                    continue;

                if (entry.IsAvailable && !showAvailableActions)
                    continue;

                if (!entry.IsAvailable && !showBlockedActions)
                    continue;

                DrawEntry(entry);
                drawnCount++;
            }

            if (drawnCount == 0)
                GUILayout.Label("No visible actions with current panel filters.");
        }

        private static void DrawEntry(ActionAvailabilityDiagnosticEntry entry)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"{(entry.IsAvailable ? "AVAILABLE" : "BLOCKED")}: {FormatDisplayWithId(entry.ActionDisplayName, entry.ActionId)}");

            if (entry.IsAvailable)
            {
                DrawLine("Success", entry.SuccessReasons);
            }
            else
            {
                DrawLine("Block reasons", entry.BlockReasons);
                DrawLine("Missing target tags", entry.MissingTargetTags);
                DrawLine("Missing equipped item tags", entry.MissingItemTags);
                DrawLine("Missing actor tags", entry.MissingActorTags);
            }

            DrawLine("Required target tags", entry.RequiredTargetTags);
            DrawLine("Required equipped item tags", entry.RequiredItemTags);
            DrawLine("Matched equipped item tags", entry.MatchedItemTags);
            GUILayout.EndVertical();
        }

        private static void DrawLine(string label, IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
                return;

            GUILayout.Label($"{label}: {FormatStrings(values)}");
        }

        private static Rect GetPanelRect()
        {
            float x = Mathf.Max(0f, Screen.width - PanelWidth - 24f);
            return new Rect(x, 24f, PanelWidth, PanelHeight);
        }

        private bool WasTogglePressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || toggleKey == Key.None)
                return false;

            var keyControl = keyboard[toggleKey];
            return keyControl != null && keyControl.wasPressedThisFrame;
        }

        private static string FormatDisplayWithId(string displayName, string id)
        {
            if (!string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(id) && displayName != id)
                return $"{displayName} ({id})";

            return SafeText(displayName, id);
        }

        private static string FormatStrings(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
                return "(none)";

            return string.Join(", ", values);
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
        }

        private static string SafeText(string primary, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(primary))
                return primary;

            return !string.IsNullOrWhiteSpace(fallback) ? fallback : "(none)";
        }
    }
}
