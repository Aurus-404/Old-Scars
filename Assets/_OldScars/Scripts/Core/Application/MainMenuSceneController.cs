using System;
using OldScars.Core.Persistence;
using OldScars.Core.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldScars.Core.ApplicationShell
{
    /// <summary>
    /// Minimal functional product menu. Existing OnGUI is used deliberately for
    /// this application shell; final production UI remains a later milestone.
    /// </summary>
    public sealed class MainMenuSceneController : MonoBehaviour
    {
        private enum MenuScreen
        {
            Home,
            NewGame,
            LoadGame
        }

        private MenuScreen screen;
        private string worldName = "New World";
        private string seedText = string.Empty;
        private string selectedSlotId;
        private string statusMessage;
        private Vector2 saveScroll;
        private WorldSaveCatalogResult catalog;

        public string StatusMessage => statusMessage;

        private void Awake()
        {
            // The Main Menu is never an owner of an opened world. This also
            // protects against stale static state after an unexpected scene route.
            WorldSessionService.Close();
        }

        private void OnGUI()
        {
            float width = Mathf.Min(620f, Mathf.Max(360f, Screen.width - 40f));
            float height = Mathf.Min(620f, Mathf.Max(420f, Screen.height - 40f));
            var area = new Rect(
                Mathf.Max(20f, (Screen.width - width) * 0.5f),
                Mathf.Max(20f, (Screen.height - height) * 0.5f),
                width,
                height);

            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Space(18f);
            GUILayout.Label("OLD SCARS", CenteredTitleStyle());
            GUILayout.Space(28f);

            switch (screen)
            {
                case MenuScreen.Home:
                    DrawHome();
                    break;
                case MenuScreen.NewGame:
                    DrawNewGame();
                    break;
                case MenuScreen.LoadGame:
                    DrawLoadGame();
                    break;
            }

            GUILayout.FlexibleSpace();
            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUILayout.Space(12f);
                GUILayout.Label(statusMessage, WrappedLabelStyle());
            }
            GUILayout.Space(12f);
            GUILayout.EndArea();
        }

        public bool TryCreateWorld(
            string requestedDisplayName,
            string requestedSeedText,
            PersistenceFileStore store = null)
        {
            if (!TryRequireValidatedContent(out string contentFailure))
            {
                statusMessage = contentFailure;
                return false;
            }

            WorldSeed seed;
            if (string.IsNullOrWhiteSpace(requestedSeedText))
            {
                seed = WorldSessionBootstrap.CreateRandomSeed();
            }
            else if (!WorldSeed.TryParse(requestedSeedText, out seed, out string seedFailure))
            {
                statusMessage = "Invalid seed: " + seedFailure + ".";
                return false;
            }

            WorldSessionOperationResult result = WorldSessionService.Create(
                requestedDisplayName,
                seed,
                GameDataManager.Instance.LoadedContentSet,
                store);
            if (!result.Success)
            {
                statusMessage = $"New Game failed during {result.Phase}: {result.Failure}";
                return false;
            }

            statusMessage = $"Created '{result.Session.DisplayName}' with seed {seed.Canonical}.";
            return TryEnterWorldRuntime("New Game");
        }

        public bool TryLoadWorld(string slotId, PersistenceFileStore store = null)
        {
            if (!TryRequireValidatedContent(out string contentFailure))
            {
                statusMessage = contentFailure;
                return false;
            }

            WorldSessionOperationResult result = WorldSessionService.Load(slotId, store);
            if (!result.Success)
            {
                statusMessage = $"Load Game failed during {result.Phase}: {result.Failure}";
                return false;
            }

            statusMessage = $"Loaded '{result.Session.DisplayName}'.";
            return TryEnterWorldRuntime("Load Game");
        }

        public WorldSaveCatalogResult RefreshSaveCatalog(PersistenceFileStore store = null)
        {
            catalog = WorldSaveCatalog.Discover(store);
            if (!catalog.Success)
                statusMessage = "Save discovery failed: " + catalog.DiscoveryFailure;
            else if (catalog.Issues.Count > 0)
                statusMessage = $"Ignored {catalog.Issues.Count} invalid world save(s); valid saves remain available.";
            else
                statusMessage = catalog.Entries.Count == 0 ? "No world saves found." : null;

            if (!string.IsNullOrEmpty(selectedSlotId) && !ContainsSlot(catalog, selectedSlotId))
                selectedSlotId = null;
            return catalog;
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

        private void DrawHome()
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("NEW GAME", GUILayout.Height(52f)))
            {
                screen = MenuScreen.NewGame;
                statusMessage = null;
            }
            GUILayout.Space(14f);
            if (GUILayout.Button("LOAD GAME", GUILayout.Height(52f)))
            {
                screen = MenuScreen.LoadGame;
                selectedSlotId = null;
                RefreshSaveCatalog();
            }
            GUILayout.Space(14f);
            if (GUILayout.Button("EXIT", GUILayout.Height(52f)))
                ExitApplication();
            GUILayout.FlexibleSpace();
        }

        private void DrawNewGame()
        {
            GUILayout.Label("NEW GAME", CenteredHeadingStyle());
            GUILayout.Space(18f);
            GUILayout.Label("World name");
            worldName = GUILayout.TextField(worldName, WorldSession.MaximumDisplayNameLength);
            GUILayout.Space(14f);
            GUILayout.Label("Seed (optional signed 64-bit integer)");
            seedText = GUILayout.TextField(seedText, 32);
            GUILayout.Space(24f);

            bool ready = IsValidatedContentReady();
            bool previousEnabled = GUI.enabled;
            GUI.enabled = ready;
            if (GUILayout.Button("CREATE", GUILayout.Height(46f)))
                TryCreateWorld(worldName, seedText);
            GUI.enabled = previousEnabled;
            GUILayout.Space(12f);
            if (GUILayout.Button("CANCEL", GUILayout.Height(40f)))
            {
                screen = MenuScreen.Home;
                statusMessage = null;
            }
            if (!ready)
            {
                GUILayout.Space(12f);
                GUILayout.Label("Waiting for validated game content.", WrappedLabelStyle());
            }
        }

        private void DrawLoadGame()
        {
            GUILayout.Label("LOAD GAME", CenteredHeadingStyle());
            GUILayout.Space(12f);
            if (catalog == null)
                RefreshSaveCatalog();

            saveScroll = GUILayout.BeginScrollView(saveScroll, GUI.skin.box, GUILayout.Height(330f));
            if (catalog != null)
            {
                for (int index = 0; index < catalog.Entries.Count; index++)
                {
                    WorldSaveCatalogEntry entry = catalog.Entries[index];
                    bool selected = entry.SlotId == selectedSlotId;
                    string label = (selected ? "> " : string.Empty) + entry.DisplayName + "\n" +
                                   entry.WorldId.Canonical + "  |  seed " + entry.WorldSeed.Canonical;
                    if (GUILayout.Button(label, GUILayout.Height(54f)))
                        selectedSlotId = entry.SlotId;
                }
            }
            GUILayout.EndScrollView();

            GUILayout.Space(12f);
            bool canLoad = IsValidatedContentReady() && !string.IsNullOrEmpty(selectedSlotId);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = canLoad;
            if (GUILayout.Button("LOAD", GUILayout.Height(44f)))
                TryLoadWorld(selectedSlotId);
            GUI.enabled = previousEnabled;
            GUILayout.Space(8f);
            if (GUILayout.Button("REFRESH", GUILayout.Height(36f)))
                RefreshSaveCatalog();
            if (GUILayout.Button("BACK", GUILayout.Height(36f)))
            {
                screen = MenuScreen.Home;
                statusMessage = null;
            }
        }

        private bool TryEnterWorldRuntime(string operation)
        {
            try
            {
                SceneManager.LoadScene(WorldApplicationScenes.WorldRuntimeSceneName, LoadSceneMode.Single);
                return true;
            }
            catch (Exception exception)
            {
                WorldSessionService.Close();
                statusMessage = $"{operation} saved/loaded the world but runtime scene entry failed: {exception.Message}";
                Debug.LogError("[WorldApplication] " + statusMessage);
                return false;
            }
        }

        private static bool TryRequireValidatedContent(out string failure)
        {
            failure = null;
            if (IsValidatedContentReady())
                return true;
            failure = "Validated game content is not ready; no WorldSession was published.";
            return false;
        }

        private static bool IsValidatedContentReady()
        {
            return GameDataManager.Instance != null &&
                   GameDataManager.Instance.IsReady &&
                   GameDataManager.Instance.LoadedContentSet != null;
        }

        private static bool ContainsSlot(WorldSaveCatalogResult result, string slotId)
        {
            if (result == null)
                return false;
            for (int index = 0; index < result.Entries.Count; index++)
            {
                if (result.Entries[index].SlotId == slotId)
                    return true;
            }
            return false;
        }

        private static GUIStyle CenteredTitleStyle()
        {
            return new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 34,
                fontStyle = FontStyle.Bold
            };
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
