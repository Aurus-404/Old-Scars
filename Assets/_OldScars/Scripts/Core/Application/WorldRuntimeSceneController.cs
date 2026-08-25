using System.Globalization;
using OldScars.Core.Actors;
using OldScars.Core.Persistence;
using OldScars.Core.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace OldScars.Core.ApplicationShell
{
    public enum WorldRuntimePlayerBindSource
    {
        None,
        NewGameSafeSpawn,
        LegacySafeSpawn,
        SaveRestore
    }

    /// <summary>
    /// Product runtime shell for one validated WorldSession. The terrain spike
    /// is an explicit derived consumer at this scene boundary; session and
    /// logical world authorities remain outside the scene.
    /// </summary>
    public sealed class WorldRuntimeSceneController : MonoBehaviour
    {
        [SerializeField] private TerrainMaterializationConfiguration terrainMaterialization =
            TerrainMaterializationConfiguration.CreateProvisionalBaseline();

        private bool menuOpen;
        private string statusMessage;
        private WorldTerrainMaterializationController materializationController;
        private PlayerGameplayComposition playerComposition;
        private GameObject lightingRoot;
        private bool gameplayRestoreAttempted;
        private bool compositionReadyBeforeRestore;
        private bool gameplayStateReady;
        private WorldGameplayLoadResult gameplayLoadResult;
        private WorldRuntimePlayerBindSource playerBindSource;

        public bool IsMenuOpen => menuOpen;
        public string StatusMessage => statusMessage;
        public WorldTerrainMaterializationController MaterializationController => materializationController;
        public PlayerGameplayComposition PlayerComposition => playerComposition;
        public bool GameplayRestoreAttempted => gameplayRestoreAttempted;
        public bool CompositionReadyBeforeRestore => compositionReadyBeforeRestore;
        public bool GameplayStateReady => gameplayStateReady;
        public WorldGameplayLoadResult GameplayLoadResult => gameplayLoadResult;
        public WorldRuntimePlayerBindSource PlayerBindSource => playerBindSource;

        private void Start()
        {
            if (WorldSessionService.HasActiveSession)
            {
                WorldSession session = WorldSessionService.ActiveSession;
                WorldSessionObservability.LogRuntimeReady(session);
                if (terrainMaterialization == null)
                    terrainMaterialization = TerrainMaterializationConfiguration.CreateProvisionalBaseline();
                materializationController = GetComponent<WorldTerrainMaterializationController>();
                if (materializationController == null)
                    materializationController = gameObject.AddComponent<WorldTerrainMaterializationController>();
                if (!materializationController.TryMaterializeActiveSession(
                        session, terrainMaterialization))
                {
                    statusMessage = "Terrain materialization failed: " + materializationController.Failure;
                    return;
                }

                EnsureWorldLighting();
                if (!PlayerGameplayComposition.TryInstantiateAtSurface(
                        materializationController.Result.SpawnPosition,
                        transform,
                        out playerComposition,
                        out string playerFailure))
                {
                    statusMessage = "Player gameplay composition failed: " + playerFailure;
                    Debug.LogError("[WorldRuntime][PLAYER_BIND_FAIL]\nWorldId: " +
                                   session.WorldId.Canonical + "\nFailure: " + playerFailure);
                    return;
                }

                if (WorldSessionService.ActiveSessionSource == WorldSessionActivationSource.Loaded)
                {
                    gameplayRestoreAttempted = true;
                    compositionReadyBeforeRestore = materializationController.IsReady &&
                                                    playerComposition.TryValidateRuntime(out _);
                    if (!compositionReadyBeforeRestore)
                    {
                        FailGameplayInitialization(
                            "Gameplay restore was rejected because terrain/player composition was not ready.");
                        return;
                    }

                    gameplayLoadResult = WorldGameplayPersistenceService.LoadAndApply(
                        session,
                        WorldSessionService.ActivePersistenceStore);
                    if (!gameplayLoadResult.Success)
                    {
                        FailGameplayInitialization(
                            "Gameplay load failed during " + gameplayLoadResult.Phase + ": " +
                            gameplayLoadResult.Failure);
                        return;
                    }

                    playerBindSource = gameplayLoadResult.Disposition == WorldGameplayLoadDisposition.Restored
                        ? WorldRuntimePlayerBindSource.SaveRestore
                        : WorldRuntimePlayerBindSource.LegacySafeSpawn;
                }
                else
                {
                    ResetGameplayClockForBootstrap();
                    playerBindSource = WorldRuntimePlayerBindSource.NewGameSafeSpawn;
                }

                if (playerBindSource == WorldRuntimePlayerBindSource.LegacySafeSpawn)
                    ResetGameplayClockForBootstrap();

                playerComposition.BindCameraToPlayer();
                gameplayStateReady = true;
                LogPlayerBound(session, playerComposition, playerBindSource);
                return;
            }

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

            GUILayout.BeginArea(new Rect(18f, 18f, Mathf.Min(720f, Screen.width - 36f), 250f), GUI.skin.box);
            GUILayout.Label(session.DisplayName, HeadingStyle());
            GUILayout.Label("World: " + session.WorldId.Canonical);
            GUILayout.Label("Seed: " + session.GenerationContext.WorldSeed.Canonical);
            GUILayout.Label(session.HasMacroWorldPlan
                ? "World size: " + session.MacroWorldPlan.GenerationSettings.WorldSizePreset +
                  "  |  sectors: " + session.MacroWorldPlan.SectorPlacements.Count
                : "World size: legacy schema 1 (no macro plan)");
            GUILayout.Label("Active sector: " + session.ActiveSectorId.Canonical);
            if (materializationController != null && materializationController.IsReady)
            {
                TerrainMaterializationResult result = materializationController.Result;
                GUILayout.Label("Terrain spike: " + result.Plan.Configuration.PhysicalWidth + "x" +
                                result.Plan.Configuration.PhysicalLength +
                                "  |  NavMesh vertices " + result.NavMeshVertexCount +
                                "  |  roads " + result.Plan.IntersectingRoadCount);
            }
            if (playerComposition != null && gameplayStateReady)
            {
                GUILayout.Label("Player: " + playerComposition.PlayerIdentity.ActorInstanceId +
                                "  |  " + playerBindSource);
            }
            else if (materializationController != null && !string.IsNullOrEmpty(materializationController.Failure))
            {
                GUILayout.Label("Terrain spike: FAILED — " + materializationController.Failure);
            }
            if (TryGetActiveSectorMacroSample(out MacroGeographySample geographySample))
            {
                GUILayout.Label("Macro geography: " + geographySample.Landform +
                                "  |  elevation " + geographySample.Elevation + "/65535");
            }
            else if (session.HasMacroWorldPlan)
            {
                GUILayout.Label("Macro geography: legacy schema 2 (not fabricated)");
            }
            if (TryGetActiveSectorWaterSample(out MacroWaterSample waterSample))
            {
                GUILayout.Label("Macro water: " +
                                (waterSample.IsOcean ? "Ocean" : waterSample.IsCoastline ? "Coast" : "Land") +
                                "  |  coverage " + session.MacroWater.GenerationSettings.LandCoverage);
            }
            else if (session.HasMacroGeography)
            {
                GUILayout.Label("Macro water: legacy schema 3 (not fabricated)");
            }
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
            if (!gameplayStateReady || playerComposition == null)
            {
                statusMessage = "Save failed: gameplay player/state is not ready.";
                return false;
            }

            WorldRuntimeSaveResult result = WorldRuntimeSaveService.SaveActive(store);
            statusMessage = result.Success
                ? "World and gameplay state saved."
                : $"Save failed during {result.Phase}: {result.Failure}";
            return result.Success;
        }

        public bool TryGetActiveSectorMacroSample(out MacroGeographySample sample)
        {
            sample = default;
            WorldSession session = WorldSessionService.ActiveSession;
            if (session == null || !session.HasMacroWorldPlan || !session.HasMacroGeography ||
                !session.MacroWorldPlan.TryGetSectorPlacement(
                    session.ActiveSectorId, out MacroSectorPlacement placement))
            {
                return false;
            }
            return session.MacroGeography.TrySampleAt(placement.Position, out sample);
        }

        public bool TryGetActiveSectorWaterSample(out MacroWaterSample sample)
        {
            sample = default;
            WorldSession session = WorldSessionService.ActiveSession;
            if (session == null || !session.HasMacroWorldPlan || !session.HasMacroWater ||
                !session.MacroWorldPlan.TryGetSectorPlacement(
                    session.ActiveSectorId, out MacroSectorPlacement placement))
                return false;
            sample = session.MacroWater.SampleAt(placement.Position);
            return true;
        }

        public void ReturnToMainMenu()
        {
            SetMenuOpen(false);
            if (playerComposition != null &&
                !CurrentSliceLoadService.TryReleaseCurrentSceneRepresentations(out string releaseFailure))
            {
                statusMessage = "Return to Main Menu failed during gameplay teardown: " + releaseFailure;
                SetMenuOpen(true);
                return;
            }
            ResetGameplayClockForBootstrap();
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

        private void FailGameplayInitialization(string failure)
        {
            gameplayStateReady = false;
            statusMessage = failure;
            playerComposition?.SetGameplayInputEnabled(false);
            Debug.LogError("[WorldRuntime][GAMEPLAY_INIT_FAIL]\n" + failure +
                           "\nActionTaken: gameplay input disabled; no fallback snapshot was fabricated");
            SetMenuOpen(true);
        }

        private void EnsureWorldLighting()
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            for (int index = 0; index < lights.Length; index++)
            {
                if (lights[index] != null && lights[index].type == LightType.Directional)
                {
                    RenderSettings.sun = lights[index];
                    return;
                }
            }

            lightingRoot = new GameObject("World Runtime Directional Light");
            lightingRoot.transform.SetParent(transform, false);
            lightingRoot.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            Light light = lightingRoot.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.96f, 0.88f, 1f);
            RenderSettings.sun = light;
        }

        private static void ResetGameplayClockForBootstrap()
        {
            WorldClock clock = WorldClock.Current;
            if (clock != null &&
                !clock.TryRestoreElapsedGameSeconds(
                    WorldClock.DefaultElapsedGameSeconds,
                    out string clockFailure))
            {
                Debug.LogError(
                    "[WorldRuntime][CLOCK_RESET_FAIL]\nFailure: " + clockFailure);
            }
        }

        private static void LogPlayerBound(
            WorldSession session,
            PlayerGameplayComposition composition,
            WorldRuntimePlayerBindSource source)
        {
            Vector3 position = composition.PlayerTransform.position;
            Debug.Log(
                "[WorldRuntime][PLAYER_BOUND]\n" +
                "WorldId: " + session.WorldId.Canonical + "\n" +
                "ActorInstanceId: " + composition.PlayerIdentity.ActorInstanceId + "\n" +
                "PersistentSceneObjectId: " + composition.PersistentIdentity.PersistentId + "\n" +
                "ActorProfile: " + composition.PlayerProfile.ActorProfileId + "\n" +
                "Source: " + source + "\n" +
                "SectorId: " + session.ActiveSectorId.Canonical + "\n" +
                "LocalPosition: (" + position.x.ToString("R", CultureInfo.InvariantCulture) + ", " +
                position.y.ToString("R", CultureInfo.InvariantCulture) + ", " +
                position.z.ToString("R", CultureInfo.InvariantCulture) + ")");
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
