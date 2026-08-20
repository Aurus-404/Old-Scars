using OldScars.Core.Actors;
using OldScars.Core.Combat;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Items;
using UnityEditor;
using UnityEngine;

namespace OldScars.Editor
{
    /// <summary>Editor-only live readout for the named M41.1 manual fixture actors.</summary>
    public sealed class M41ManualEncounterStatusWindow : EditorWindow
    {
        private const string ManualPrefix = "M41.1 Manual ";

        [InitializeOnLoadMethod]
        private static void RegisterRepaint() => EditorApplication.update += RepaintOpenWindows;

        public static void ShowWindow()
        {
            M41ManualEncounterStatusWindow window = GetWindow<M41ManualEncounterStatusWindow>("M41.1 Manual Status");
            window.minSize = new Vector2(330f, 180f);
            window.Show();
        }

        private static void RepaintOpenWindows()
        {
            foreach (M41ManualEncounterStatusWindow window in Resources.FindObjectsOfTypeAll<M41ManualEncounterStatusWindow>())
                window.Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("M41.1 Manual Fixture", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Scenario", M41HumanEncounterAIDiagnostics.CurrentManualScenario);

            HumanEncounterAIController ai = FindManualAI();
            if (!EditorApplication.isPlaying || ai == null)
            {
                EditorGUILayout.HelpBox("Prepare a scenario in Play Mode to inspect its live state.", MessageType.Info);
                return;
            }

            ActorVisualPerceptionResult perception = ai.LastPerception;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Current AI State", ai.State.ToString());
            EditorGUILayout.LabelField("Perceived / LOS", $"{perception.Perceived} / {perception.Reason}");
            EditorGUILayout.LabelField("LOS Blocker", perception.Blocker != null ? perception.Blocker.name : "<NONE>");

            if (M41HumanEncounterAIDiagnostics.CurrentManualScenario == "Fight")
                DrawFightStatus(ai);
        }

        private static HumanEncounterAIController FindManualAI()
        {
            foreach (HumanEncounterAIController candidate in Object.FindObjectsByType<HumanEncounterAIController>(FindObjectsInactive.Exclude))
                if (candidate != null && candidate.name.StartsWith(ManualPrefix))
                    return candidate;
            return null;
        }

        private static void DrawFightStatus(HumanEncounterAIController ai)
        {
            ActorItemOwnershipComponent ownership = ai.GetComponent<ActorItemOwnershipComponent>();
            if (!WeaponCombatService.TryGetEquippedWeapon(ownership, out ItemInstance weapon, out _, out FirearmProfileDefinition firearm, out _)
                || firearm == null)
            {
                EditorGUILayout.LabelField("Weapon / Ammo / Reload", "<NONE>");
                return;
            }

            int compatibleAmmo = WeaponCombatService.GetCompatibleAmmoQuantity(ownership, weapon);
            EditorGUILayout.LabelField("Weapon", firearm.display_name);
            EditorGUILayout.LabelField("Ammo", $"{weapon.LoadedRounds}/{firearm.magazine_capacity} loaded; {compatibleAmmo} reserve");
            EditorGUILayout.LabelField("Reload", ai.IsReloadPending ? "Pending" : "Ready");
        }
    }
}
