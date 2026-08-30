using System.Collections;
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
        // World information is a development aid, not a modal gameplay menu.
        // Keep it collapsed by default so the shared debug surfaces remain usable.
        private bool worldInfoVisible;
        private string statusMessage;
        private WorldTerrainMaterializationController materializationController;
        private WorldDeformableTerrainSpikeController volumetricTerrainController;
        private WorldRuntimeTerrainDevelopmentSelection terrainSelection;
        private PlayerGameplayComposition playerComposition;
        private GameplayRuntimeComposition gameplayRuntimeComposition;
        private DevelopmentGameplayIntegrationFixture developmentFixture;
        private GameObject lightingRoot;
        private bool gameplayRestoreAttempted;
        private bool compositionReadyBeforeRestore;
        private bool gameplayStateReady;
        private WorldGameplayLoadResult gameplayLoadResult;
        private WorldRuntimePlayerBindSource playerBindSource;

        public bool IsMenuOpen => menuOpen;
        public bool IsWorldInfoVisible => worldInfoVisible;
        public string StatusMessage => statusMessage;
        public WorldTerrainMaterializationController MaterializationController => materializationController;
        public WorldDeformableTerrainSpikeController VolumetricTerrainController => volumetricTerrainController;
        public WorldRuntimeTerrainDevelopmentSelection TerrainSelection => terrainSelection;
        public PlayerGameplayComposition PlayerComposition => playerComposition;
        public GameplayRuntimeComposition GameplayRuntimeComposition => gameplayRuntimeComposition;
        public DevelopmentGameplayIntegrationFixture DevelopmentFixture => developmentFixture;
        public bool GameplayRestoreAttempted => gameplayRestoreAttempted;
        public bool CompositionReadyBeforeRestore => compositionReadyBeforeRestore;
        public bool GameplayStateReady => gameplayStateReady;
        public WorldGameplayLoadResult GameplayLoadResult => gameplayLoadResult;
        public WorldRuntimePlayerBindSource PlayerBindSource => playerBindSource;
        private bool IsTerrainReady =>
            materializationController != null && materializationController.IsReady ||
            volumetricTerrainController != null && volumetricTerrainController.IsReady;

        private IEnumerator Start()
        {
            if (WorldSessionService.HasActiveSession)
            {
                WorldSession session = WorldSessionService.ActiveSession;
                WorldSessionObservability.LogRuntimeReady(session);
                if (terrainMaterialization == null)
                    terrainMaterialization = TerrainMaterializationConfiguration.CreateProvisionalBaseline();
                terrainSelection = WorldRuntimeTerrainDevelopmentSettings.CurrentSelection;
                Vector3 spawnPosition;
                if (terrainSelection == WorldRuntimeTerrainDevelopmentSelection.UnityTerrain)
                {
                    materializationController = GetComponent<WorldTerrainMaterializationController>();
                    if (materializationController == null)
                        materializationController = gameObject.AddComponent<WorldTerrainMaterializationController>();
                    if (!materializationController.TryMaterializeActiveSession(
                            session, terrainMaterialization))
                    {
                        statusMessage = "Terrain materialization failed: " + materializationController.Failure;
                        yield break;
                    }
                    spawnPosition = materializationController.Result.SpawnPosition;
                }
                else
                {
                    volumetricTerrainController = GetComponent<WorldDeformableTerrainSpikeController>();
                    if (volumetricTerrainController == null)
                        volumetricTerrainController = gameObject.AddComponent<WorldDeformableTerrainSpikeController>();
                    if (!volumetricTerrainController.TryMaterializeActiveSession(
                            session,
                            terrainMaterialization,
                            DeformableTerrainSpikeConfiguration.CreateBaseline(),
                            WorldRuntimeTerrainDevelopmentSettings.SelectedMesher))
                    {
                        statusMessage = "Volumetric terrain materialization failed: " +
                                        volumetricTerrainController.Failure;
                        yield break;
                    }
                    spawnPosition = volumetricTerrainController.SpawnPosition;
                }

                EnsureWorldLighting();
                if (!PlayerGameplayComposition.TryInstantiateAtSurface(
                        spawnPosition,
                        transform,
                        out playerComposition,
                        out string playerFailure))
                {
                    statusMessage = "Player gameplay composition failed: " + playerFailure;
                    Debug.LogError("[WorldRuntime][PLAYER_BIND_FAIL]\nWorldId: " +
                                   session.WorldId.Canonical + "\nFailure: " + playerFailure);
                    yield break;
                }

                playerComposition.SetGameplayInputEnabled(false);
                if (!GameplayRuntimeComposition.TryCreateAndBind(
                        transform, playerComposition, out gameplayRuntimeComposition,
                        out string runtimeFailure))
                {
                    FailGameplayInitialization("Shared gameplay runtime failed: " + runtimeFailure);
                    yield break;
                }

                bool fixtureExpected = (Application.isEditor || Debug.isDebugBuild) &&
                                       terrainSelection == WorldRuntimeTerrainDevelopmentSelection.UnityTerrain;
                if (fixtureExpected && !DevelopmentGameplayIntegrationFixture.TryInstantiateOnMaterializedLand(
                        materializationController.Result, transform, playerComposition,
                        out developmentFixture, out string fixtureFailure))
                {
                    FailGameplayInitialization("Development gameplay fixture failed: " + fixtureFailure);
                    yield break;
                }

                // Existing authored profile/container components initialize in Start.
                // Keep input disabled and let those persistence representations finish
                // before Current Slice semantic preflight/application begins.
                yield return null;

                if (WorldSessionService.ActiveSessionSource == WorldSessionActivationSource.Loaded)
                {
                    gameplayRestoreAttempted = true;
                    compositionReadyBeforeRestore = IsTerrainReady &&
                                                    playerComposition.TryValidateRuntime(out _);
                    if (!compositionReadyBeforeRestore)
                    {
                        FailGameplayInitialization(
                            "Gameplay restore was rejected because terrain/player composition was not ready.");
                        yield break;
                    }

                    gameplayLoadResult = WorldGameplayPersistenceService.LoadAndApply(
                        session,
                        WorldSessionService.ActivePersistenceStore);
                    if (!gameplayLoadResult.Success)
                    {
                        FailGameplayInitialization(
                            "Gameplay load failed during " + gameplayLoadResult.Phase + ": " +
                            gameplayLoadResult.Failure);
                        yield break;
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

                if (!gameplayRuntimeComposition.TryValidate(out string readinessFailure))
                {
                    FailGameplayInitialization("Integrated gameplay runtime validation failed: " + readinessFailure);
                    yield break;
                }

                playerComposition.BindCameraToPlayer();
                playerComposition.SetGameplayInputEnabled(true);
                gameplayStateReady = true;
                LogPlayerBound(session, playerComposition, playerBindSource);
                LogGameplayRuntimeReady(session, fixtureExpected);
                yield break;
            }

            Debug.LogError("[WorldApplication] World Runtime opened without an active WorldSession; returning to Main Menu.");
            WorldSessionService.Close();
            SceneManager.LoadScene(WorldApplicationScenes.MainMenuSceneName, LoadSceneMode.Single);
        }

        private void LogGameplayRuntimeReady(WorldSession session, bool fixtureExpected)
        {
            bool fixtureReady = developmentFixture != null && developmentFixture.TryValidate(out _);
            Debug.Log("[WorldRuntime][GAMEPLAY_RUNTIME_READY]\n" +
                      "WorldId: " + session.WorldId.Canonical + "\n" +
                      "TerrainRepresentation: " + terrainSelection + "\n" +
                      gameplayRuntimeComposition.DescribeReadiness(fixtureReady) + "\n" +
                      "DevelopmentFixtureExpected: " + fixtureExpected +
                      (fixtureReady
                          ? "\nFixturePosition: " + developmentFixture.PlacementPosition.ToString("F2") +
                            "\nFixtureHeightRange: " + developmentFixture.PlacementHeightRange.ToString("F2", CultureInfo.InvariantCulture)
                          : string.Empty));
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

            float infoWidth = Mathf.Min(720f, Mathf.Max(1f, Screen.width - 36f));
            float infoButtonWidth = 112f;
            Rect infoButtonRect = new Rect(
                Mathf.Max(18f, (Screen.width - infoButtonWidth) * 0.5f),
                18f,
                Mathf.Min(infoButtonWidth, Mathf.Max(1f, Screen.width - 36f)),
                28f);

            if (!worldInfoVisible)
            {
                if (GUI.Button(infoButtonRect, "WORLD INFO"))
                    OpenWorldInfo();
            }
            else
            {
                float infoHeight = volumetricTerrainController != null ? 420f : 292f;
                Rect infoRect = new Rect(
                    Mathf.Max(18f, (Screen.width - infoWidth) * 0.5f),
                    18f,
                    infoWidth,
                    Mathf.Min(infoHeight, Mathf.Max(150f, Screen.height - 36f)));
                GUILayout.BeginArea(infoRect, GUI.skin.window);
                GUILayout.BeginHorizontal();
                GUILayout.Label("WORLD INFORMATION", HeadingStyle());
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("X", GUILayout.Width(28f), GUILayout.Height(24f)))
                    CloseWorldInfo();
                GUILayout.EndHorizontal();

                DrawWorldInformation(session);
                GUILayout.EndArea();
            }

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

        private void DrawWorldInformation(WorldSession session)
        {
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
            else if (volumetricTerrainController != null && volumetricTerrainController.IsReady)
            {
                DeformableTerrainSpikeMetrics metrics = volumetricTerrainController.Metrics;
                GUILayout.Label("Terrain: VOLUMETRIC DEVELOPMENT OPT-IN  |  " + metrics.MesherBackend);
                GUILayout.Label("Technical chunks: " + metrics.ChunkCount + " (" +
                                volumetricTerrainController.Volume.Configuration.ChunkCountX + "x" +
                                volumetricTerrainController.Volume.Configuration.ChunkCountY + "x" +
                                volumetricTerrainController.Volume.Configuration.ChunkCountZ + ")" +
                                "  |  vertices " + metrics.Vertices + "  |  triangles " + metrics.Triangles);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("DEV CRATER")) ApplyVolumetricCrater();
                if (GUILayout.Button("DEV TUNNEL")) ApplyVolumetricTunnel();
                if (GUILayout.Button("RESET VOLUME")) ResetVolumetricTerrain();
                GUILayout.EndHorizontal();
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
            else if (volumetricTerrainController != null &&
                     !string.IsNullOrEmpty(volumetricTerrainController.Failure))
            {
                GUILayout.Label("Volumetric terrain: FAILED — " + volumetricTerrainController.Failure);
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
        }

        public void OpenWorldInfo()
        {
            worldInfoVisible = true;
        }

        public void CloseWorldInfo()
        {
            worldInfoVisible = false;
        }

        public void OpenMenu()
        {
            SetMenuOpen(true);
        }

        public void ContinueGame()
        {
            SetMenuOpen(false);
        }

        private void ApplyVolumetricCrater()
        {
            if (volumetricTerrainController == null || !volumetricTerrainController.IsReady)
                return;
            const float z = -12f;
            float surface = volumetricTerrainController.SourcePlan.HeightNormalizedAtLocal(0f, z) *
                            volumetricTerrainController.SourcePlan.Configuration.VerticalRelief;
            if (volumetricTerrainController.TrySubtractSphere(
                    new Vector3(0f, surface - 1.5f, z), 6.5f,
                    out DeformableTerrainMutationResult result, out string error))
            {
                statusMessage = "Development crater rebuilt " + result.AffectedChunks.Count + " chunks.";
                return;
            }
            statusMessage = "Development crater failed: " + error;
        }

        private void ApplyVolumetricTunnel()
        {
            if (volumetricTerrainController == null || !volumetricTerrainController.IsReady)
                return;
            const float z = -12f;
            float surface = volumetricTerrainController.SourcePlan.HeightNormalizedAtLocal(0f, z) *
                            volumetricTerrainController.SourcePlan.Configuration.VerticalRelief;
            if (volumetricTerrainController.TrySubtractCapsule(
                    new Vector3(0f, surface - 8f, z),
                    new Vector3(28f, surface - 8f, z),
                    3.75f, out DeformableTerrainMutationResult result, out string error))
            {
                statusMessage = "Development tunnel rebuilt " + result.AffectedChunks.Count + " chunks.";
                return;
            }
            statusMessage = "Development tunnel failed: " + error;
        }

        private void ResetVolumetricTerrain()
        {
            if (volumetricTerrainController == null || !volumetricTerrainController.IsReady)
                return;
            statusMessage = volumetricTerrainController.TryReset(out _, out string error)
                ? "Development volumetric terrain reset to committed MacroGeography baseline."
                : "Development terrain reset failed: " + error;
        }

        public bool SaveGame(PersistenceFileStore store = null)
        {
            if (!gameplayStateReady || playerComposition == null)
            {
                statusMessage = "Save failed: gameplay player/state is not ready.";
                return false;
            }

            if (volumetricTerrainController != null && volumetricTerrainController.HasMutations)
            {
                statusMessage =
                    "Save blocked: volumetric spike mutations are non-persistent in Stage 1. " +
                    "Reset the development volume before saving.";
                Debug.LogWarning("[WorldRuntime][SAVE_BLOCKED]\n" + statusMessage);
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
