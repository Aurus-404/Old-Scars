using OldScars.Core.Persistence;
using OldScars.Core.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace OldScars.Core.ApplicationShell
{
    /// <summary>
    /// Minimal runtime placeholder proving that a validated WorldSession is open.
    /// It intentionally contains no world materialization or gameplay simulation.
    /// </summary>
    public sealed class WorldRuntimeSceneController : MonoBehaviour
    {
        private bool menuOpen;
        private string statusMessage;

        public bool IsMenuOpen => menuOpen;
        public string StatusMessage => statusMessage;

        private void Start()
        {
            if (WorldSessionService.HasActiveSession)
                return;

            Debug.LogError("[WorldApplication] World Runtime opened without an active WorldSession; returning to Main Menu.");
            WorldSessionService.Close();
            SceneManager.LoadScene(WorldApplicationScenes.MainMenuSceneName, LoadSceneMode.Single);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                SetMenuOpen(!menuOpen);
        }

        private void OnDisable()
        {
            if (menuOpen)
                Time.timeScale = 1f;
        }

        private void OnGUI()
        {
            WorldSession session = WorldSessionService.ActiveSession;
            if (session == null)
                return;

            GUILayout.BeginArea(new Rect(18f, 18f, Mathf.Min(620f, Screen.width - 36f), 150f), GUI.skin.box);
            GUILayout.Label(session.DisplayName, HeadingStyle());
            GUILayout.Label("World: " + session.WorldId.Canonical);
            GUILayout.Label("Seed: " + session.GenerationContext.WorldSeed.Canonical);
            GUILayout.Label(session.HasMacroWorldPlan
                ? "World size: " + session.MacroWorldPlan.GenerationSettings.WorldSizePreset +
                  "  |  sectors: " + session.MacroWorldPlan.SectorPlacements.Count
                : "World size: legacy schema 1 (no macro plan)");
            GUILayout.Label("Active sector: " + session.ActiveSectorId.Canonical);
            GUILayout.Label("Press Escape for menu");
            GUILayout.EndArea();

            if (!menuOpen)
                return;

            float width = 390f;
            float height = 390f;
            var menuArea = new Rect(
                Mathf.Max(20f, (Screen.width - width) * 0.5f),
                Mathf.Max(20f, (Screen.height - height) * 0.5f),
                width,
                height);
            GUILayout.BeginArea(menuArea, GUI.skin.window);
            GUILayout.Space(12f);
            GUILayout.Label("WORLD MENU", CenteredHeadingStyle());
            GUILayout.Space(22f);
            if (GUILayout.Button("CONTINUE", GUILayout.Height(44f)))
                ContinueGame();
            GUILayout.Space(10f);
            if (GUILayout.Button("SAVE GAME", GUILayout.Height(44f)))
                SaveGame();
            GUILayout.Space(10f);
            if (GUILayout.Button("RETURN TO MAIN MENU", GUILayout.Height(44f)))
                ReturnToMainMenu();
            GUILayout.Space(10f);
            if (GUILayout.Button("EXIT", GUILayout.Height(44f)))
                ExitApplication();
            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUILayout.Space(12f);
                GUILayout.Label(statusMessage, WrappedLabelStyle());
            }
            GUILayout.EndArea();
        }

        public void OpenMenu()
        {
            SetMenuOpen(true);
        }

        public void ContinueGame()
        {
            SetMenuOpen(false);
        }

        public bool SaveGame(PersistenceFileStore store = null)
        {
            WorldSessionOperationResult result = WorldSessionService.Save(store);
            statusMessage = result.Success
                ? "World saved."
                : $"Save failed during {result.Phase}: {result.Failure}";
            return result.Success;
        }

        public void ReturnToMainMenu()
        {
            SetMenuOpen(false);
            WorldSessionService.Close();
            SceneManager.LoadScene(WorldApplicationScenes.MainMenuSceneName, LoadSceneMode.Single);
        }

        public void ExitApplication()
        {
            if (Application.isEditor)
            {
                statusMessage = "Exit is disabled inside the Unity Editor.";
                Debug.Log("[WorldApplication] Exit requested in Editor; the Editor remains open.");
                return;
            }
            Application.Quit();
        }

        private void SetMenuOpen(bool value)
        {
            menuOpen = value;
            Time.timeScale = menuOpen ? 0f : 1f;
        }

        private static GUIStyle HeadingStyle()
        {
            return new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
        }

        private static GUIStyle CenteredHeadingStyle()
        {
            return new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 22,
                fontStyle = FontStyle.Bold
            };
        }

        private static GUIStyle WrappedLabelStyle()
        {
            return new GUIStyle(GUI.skin.label) { wordWrap = true };
        }
    }
}
