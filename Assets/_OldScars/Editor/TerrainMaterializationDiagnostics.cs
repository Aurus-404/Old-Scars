using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using OldScars.Core;
using OldScars.Core.Actors;
using OldScars.Core.ApplicationShell;
using OldScars.Core.Data.Loading;
using OldScars.Core.Data.Validation;
using OldScars.Core.Interactions;
using OldScars.Core.Persistence;
using OldScars.Core.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace OldScars.EditorTools
{
    public static class TerrainMaterializationDiagnostics
    {
        private const long GoldenSeed = 8675309123456789L;
        private const string GoldenPlanHash =
            "3f300ba2129962493d2ab8f2ad6ec0863e96aa0ceeb400f9899f91889a34e91a";
        private const string GoldenGeographyHash =
            "c2d412fcdcb1b0e1b41f4fdbda2df01258758e6db9c6b93aac59b446be7dbd3e";
        private const string GoldenWaterHash =
            "ec29f501e4f36ae3b2313d3da6089f2fe6e92b052f18079c649e21ce8faabfc0";
        private const string GoldenHumanHash =
            "a786f018ce3bdea44aeb066c80e38cb1f5dc8e114c65bd7eb352489628245ba6";
        private const string MenuPath =
            "Old Scars/Diagnostics/Worldgen/Run Terrain Materialization Technical Spike";

        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("Terrain Materialization Technical Spike Diagnostics: CANCELLED — current scene setup was not changed.");
                return;
            }

            SceneSetup[] originalSceneSetup = EditorSceneManager.GetSceneManagerSetup();
            var failures = new List<string>();
            var measurements = new List<string>();
            var captures = new List<string>();
            string persistenceRoot = Path.Combine(
                Path.GetTempPath(), "OldScars_TerrainMaterialization_Save_" + Guid.NewGuid().ToString("N"));
            string captureRoot = Path.Combine(Path.GetTempPath(), "OldScars_TerrainMaterialization_Evidence");
            WorldTerrainMaterializationController controller = null;
            try
            {
                Directory.CreateDirectory(captureRoot);
                LoadedContentSet content = LoadValidatedCore(failures);
                if (content == null)
                    throw new InvalidOperationException("real Core content could not create a current-schema world fixture");

                var store = new PersistenceFileStore(persistenceRoot);
                WorldSession golden = CreatePersistedFixture(
                    GoldenSeed, "Terrain Spike Golden", content, store, failures);
                if (golden == null)
                    throw new InvalidOperationException("golden persisted WorldSession fixture was unavailable");
                ValidateLogicalAuthority(golden, failures);
                ValidateProjectionDeterminism(golden, failures);

                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                CreateDiagnosticCamera();
                controller = new GameObject("Terrain Materialization Diagnostic Orchestrator")
                    .AddComponent<WorldTerrainMaterializationController>();

                TerrainMaterializationConfiguration[] configurations = CreateMeasuredConfigurations();
                for (int index = 0; index < configurations.Length; index++)
                {
                    TerrainMaterializationConfiguration selected = configurations[index];
                    if (!controller.TryMaterializeActiveSession(golden, selected))
                    {
                        failures.Add("scale candidate " + index + " failed: " + controller.Failure);
                        continue;
                    }
                    ValidatePhysicalMaterialization(
                        controller, golden, "scale candidate " + index, failures);
                    TerrainMaterializationResult result = controller.Result;
                    measurements.Add(
                        selected.PhysicalWidth.ToString("0", CultureInfo.InvariantCulture) + "x" +
                        selected.PhysicalLength.ToString("0", CultureInfo.InvariantCulture) +
                        " / logical " + selected.LogicalWidth + "x" + selected.LogicalLength +
                        " / h" + selected.HeightmapResolution +
                        ": projection=" + result.ProjectionElapsedMilliseconds + "ms" +
                        ", terrain=" + result.TerrainElapsedMilliseconds + "ms" +
                        ", navmesh=" + result.NavMeshElapsedMilliseconds + "ms" +
                        ", total=" + result.TotalElapsedMilliseconds + "ms" +
                        ", approx=" + result.ApproximateRuntimeBytes + "B" +
                        ", objects=" + result.GeneratedObjectCount);
                    controller.ClearMaterialization();
                }

                TerrainMaterializationConfiguration baseline =
                    TerrainMaterializationConfiguration.CreateProvisionalBaseline();
                ValidateRepresentativeCapture(
                    golden, RepresentativeKind.InlandPlains, "inland_plain", baseline,
                    controller, captureRoot, captures, failures);

                WorldSession rugged = CreatePersistedFixture(
                    GoldenSeed + 104729L, "Terrain Spike Rugged", content, store, failures);
                ValidateRuggedNavigationProbe(rugged, controller, measurements, failures);
                ValidateRepresentativeCapture(
                    rugged, RepresentativeKind.RuggedLand, "rugged", baseline,
                    controller, captureRoot, captures, failures);

                WorldSession coastal = CreatePersistedFixture(
                    GoldenSeed + 209759L, "Terrain Spike Coastal", content, store, failures);
                ValidateRepresentativeCapture(
                    coastal, RepresentativeKind.Coast, "coastal", baseline,
                    controller, captureRoot, captures, failures);
            }
            catch (Exception exception)
            {
                failures.Add("diagnostic fixture threw " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                if (controller != null)
                    UnityEngine.Object.DestroyImmediate(controller.gameObject);
                WorldSessionService.Close();
                NavMesh.RemoveAllNavMeshData();
                if (Directory.Exists(persistenceRoot))
                    Directory.Delete(persistenceRoot, true);
                if (originalSceneSetup.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(originalSceneSetup);
            }

            Complete(
                failures,
                "- physical scale candidates: " + string.Join("; ", measurements),
                "- visual captures: " + string.Join("; ", captures),
                "- committed Plan/Geography/Water/Human truth consumed without Climate coupling or logical-hash changes",
                "- local Unity Terrain/TerrainCollider, masked ocean, projected roads, technical player, and one local NavMesh",
                "- no sector-to-terrain identity mapping and no whole-world/inactive-world materialization");
        }

        private static TerrainMaterializationConfiguration[] CreateMeasuredConfigurations()
        {
            return new[]
            {
                new TerrainMaterializationConfiguration(
                    512f, 512f, 180f, 1400L, 1400L,
                    129, 48, 64, 128, 3f, 38f),
                TerrainMaterializationConfiguration.CreateProvisionalBaseline(),
                new TerrainMaterializationConfiguration(
                    1024f, 1024f, 320f, 2400L, 2400L,
                    257, 96, 128, 256, 5f, 38f)
            };
        }

        private static void ValidateLogicalAuthority(WorldSession session, ICollection<string> failures)
        {
            Check(WorldSessionPersistenceService.CurrentSchemaVersion == 6,
                "current New Game schema must include Climate without changing terrain inputs", failures);
            Check(session.HasMacroWorldPlan && session.HasMacroGeography && session.HasMacroWater &&
                  session.HasMacroHumanGeography,
                "persisted terrain fixture must expose all committed terrain-input truth", failures);
            Check(session.MacroWorldPlan.CanonicalHash == GoldenPlanHash,
                "MacroWorldPlan golden drifted", failures);
            Check(session.MacroGeography.CanonicalHash == GoldenGeographyHash,
                "MacroGeography golden drifted", failures);
            Check(session.MacroWater.CanonicalHash == GoldenWaterHash,
                "MacroWater golden drifted", failures);
            Check(session.MacroHumanGeography.CanonicalHash == GoldenHumanHash,
                "MacroHumanGeography golden drifted", failures);
        }

        private static void ValidateProjectionDeterminism(
            WorldSession session,
            ICollection<string> failures)
        {
            TerrainMaterializationConfiguration configuration =
                TerrainMaterializationConfiguration.CreateProvisionalBaseline();
            bool firstBuilt = TerrainMaterializationPlanner.TryBuildActiveRegion(
                session, configuration, out TerrainMaterializationPlan first, out string firstError);
            bool secondBuilt = TerrainMaterializationPlanner.TryBuildActiveRegion(
                session, configuration, out TerrainMaterializationPlan second, out string secondError);
            Check(firstBuilt && secondBuilt && first.HasEquivalentProjection(second),
                "same committed world truth/configuration must produce equivalent physical projection: " +
                Safe(firstError) + " / " + Safe(secondError), failures);

            TerrainMaterializationConfiguration alternate = CreateMeasuredConfigurations()[0];
            Check(TerrainMaterializationPlanner.TryBuildActiveRegion(
                      session, alternate, out TerrainMaterializationPlan changedScale, out string alternateError) &&
                  changedScale.MacroPlanHash == session.MacroWorldPlan.CanonicalHash &&
                  changedScale.GeographyHash == session.MacroGeography.CanonicalHash &&
                  changedScale.WaterHash == session.MacroWater.CanonicalHash &&
                  changedScale.HumanGeographyHash == session.MacroHumanGeography.CanonicalHash &&
                  (changedScale.Configuration.PhysicalWidth != first.Configuration.PhysicalWidth ||
                   changedScale.Configuration.VerticalRelief != first.Configuration.VerticalRelief),
                "physical scale configuration must not change logical world evidence: " + Safe(alternateError),
                failures);
        }

        private static void ValidateRepresentativeCapture(
            WorldSession session,
            RepresentativeKind kind,
            string label,
            TerrainMaterializationConfiguration configuration,
            WorldTerrainMaterializationController controller,
            string captureRoot,
            ICollection<string> captures,
            ICollection<string> failures)
        {
            if (session == null)
            {
                failures.Add(label + " persisted session was unavailable");
                return;
            }
            if (!TryFindRepresentativeCenter(session, kind, out MacroPoint2D center, out string centerError))
            {
                failures.Add(label + " representative center failed: " + centerError);
                return;
            }
            if (!controller.TryMaterializeAt(session, configuration, center))
            {
                failures.Add(label + " materialization failed: " + controller.Failure);
                return;
            }
            ValidatePhysicalMaterialization(controller, session, label, failures);
            string path = Path.Combine(captureRoot, "terrain_materialization_" + label + ".png");
            if (!TryCapture(controller.Result, path, out string captureError))
            {
                failures.Add(label + " visual capture failed: " + captureError);
            }
            else
            {
                captures.Add(path + " (" + new FileInfo(path).Length + " bytes)");
            }
            controller.ClearMaterialization();
        }

        private static void ValidateRuggedNavigationProbe(
            WorldSession session,
            WorldTerrainMaterializationController controller,
            ICollection<string> measurements,
            ICollection<string> failures)
        {
            if (session == null)
            {
                failures.Add("rugged navigation probe session was unavailable");
                return;
            }
            if (!TryFindRepresentativeCenter(
                    session, RepresentativeKind.RuggedLand,
                    out MacroPoint2D center, out string centerError))
            {
                failures.Add("rugged navigation probe center failed: " + Safe(centerError));
                return;
            }
            var configuration = new TerrainMaterializationConfiguration(
                512f, 512f, 1200f, 1800L, 1800L,
                257, 64, 128, 128, 3f, 38f);
            if (!controller.TryMaterializeAt(session, configuration, center))
            {
                failures.Add("rugged navigation scale probe failed: " + controller.Failure);
                return;
            }
            TerrainMaterializationResult result = controller.Result;
            ValidatePhysicalMaterialization(
                controller, session, "rugged navigation scale probe", failures);
            ValidateSteepNavigationFiltering(result, "rugged navigation scale probe", failures);
            measurements.Add(
                "rugged-nav 512x512 / vertical 1200 / logical 1800x1800 / h257" +
                ": navmesh=" + result.NavMeshElapsedMilliseconds + "ms" +
                ", total=" + result.TotalElapsedMilliseconds + "ms");
            controller.ClearMaterialization();
        }

        private static void ValidatePhysicalMaterialization(
            WorldTerrainMaterializationController controller,
            WorldSession session,
            string label,
            ICollection<string> failures)
        {
            TerrainMaterializationResult result = controller.Result;
            Check(controller.IsReady && result != null, label + " did not publish READY result", failures);
            if (result == null) return;
            TerrainMaterializationPlan plan = result.Plan;
            Terrain terrain = result.Terrain;
            Check(terrain != null && result.TerrainCollider != null &&
                  result.TerrainCollider.terrainData == terrain.terrainData,
                label + " lacks a usable Terrain/TerrainCollider pair", failures);
            Check(terrain.terrainData.heightmapResolution == plan.HeightmapResolution,
                label + " Unity Terrain heightmap resolution differs from the projection", failures);
            ValidateHeightSamples(plan, terrain, label, failures);

            Ray ray = new Ray(result.SpawnPosition + Vector3.up * 800f, Vector3.down);
            Check(result.TerrainCollider.Raycast(ray, out RaycastHit hit, 1600f) &&
                  Mathf.Abs(hit.point.y - terrain.SampleHeight(hit.point)) < 0.25f,
                label + " TerrainCollider did not resolve the safe player spawn surface", failures);
            Check(controller.GeneratedRoot.GetComponentInChildren<PlayerGameplayComposition>(true) == null &&
                  controller.GeneratedRoot.GetComponentInChildren<ActorInteractionContext>(true) == null &&
                  controller.GeneratedRoot.GetComponentInChildren<CameraRigController>(true) == null &&
                  controller.GeneratedRoot.GetComponentInChildren<Camera>(true) == null,
                label + " terrain materialization incorrectly acquired player/camera composition authority", failures);
            ValidateSafeSpawn(plan, terrain, result.SpawnPosition, label, failures);
            Check(result.NavMeshSurface != null && result.NavMeshSurface.navMeshData != null &&
                  result.NavMeshVertexCount > 0 && result.PathCorners.Count >= 2,
                label + " local NavMesh/path evidence is incomplete", failures);
            Check(UnityEngine.Object.FindObjectsByType<NavMeshSurface>().Length == 1,
                label + " created more than one/whole-world NavMesh surface", failures);
            Check(UnityEngine.Object.FindObjectsByType<Terrain>().Length == 1 &&
                  session.MacroWorldPlan.SectorPlacements.Count > 1 &&
                  !terrain.name.Contains(session.ActiveSectorId.Canonical),
                label + " conflated product SectorId/count with Unity Terrain tiles", failures);
            ValidateWater(plan, controller.GeneratedRoot, label, failures);
            ValidateRoads(plan, controller.GeneratedRoot, label, failures);
            Check(plan.MacroPlanHash == session.MacroWorldPlan.CanonicalHash &&
                  plan.GeographyHash == session.MacroGeography.CanonicalHash &&
                  plan.WaterHash == session.MacroWater.CanonicalHash &&
                  plan.HumanGeographyHash == session.MacroHumanGeography.CanonicalHash,
                label + " materialization did not retain source logical evidence", failures);
        }

        private static void ValidateSafeSpawn(
            TerrainMaterializationPlan plan,
            Terrain terrain,
            Vector3 spawn,
            string label,
            ICollection<string> failures)
        {
            Vector3 terrainOrigin = terrain.transform.position;
            float normalizedX = Mathf.Clamp01(
                (spawn.x - terrainOrigin.x) / terrain.terrainData.size.x);
            float normalizedZ = Mathf.Clamp01(
                (spawn.z - terrainOrigin.z) / terrain.terrainData.size.z);
            float slope = terrain.terrainData.GetSteepness(normalizedX, normalizedZ);
            Check(!plan.IsOceanAtNormalized(normalizedX, normalizedZ),
                label + " safe player spawn resolved inside committed ocean", failures);
            Check(slope <= plan.Configuration.MaximumSpawnSlopeDegrees + 0.25f,
                label + " safe player spawn exceeded the configured slope limit", failures);
        }

        private static void ValidateSteepNavigationFiltering(
            TerrainMaterializationResult result,
            string label,
            ICollection<string> failures)
        {
            const int samplesPerAxis = 65;
            Terrain terrain = result.Terrain;
            TerrainData data = terrain.terrainData;
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(result.NavMeshSurface.agentTypeID);
            float walkableSlope = settings.agentSlope;
            float maxSlope = 0f;
            int steepSamples = 0;
            int rejectedSamples = 0;
            for (int z = 1; z < samplesPerAxis - 1; z++)
            for (int x = 1; x < samplesPerAxis - 1; x++)
            {
                float nx = x / (float)(samplesPerAxis - 1);
                float nz = z / (float)(samplesPerAxis - 1);
                float slope = data.GetSteepness(nx, nz);
                maxSlope = Mathf.Max(maxSlope, slope);
                if (slope <= walkableSlope + 1f)
                    continue;
                steepSamples++;
                Vector3 point = terrain.transform.position +
                    new Vector3(nx * data.size.x, 0f, nz * data.size.z);
                point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
                if (!NavMesh.SamplePosition(point, out _, 0.2f, NavMesh.AllAreas))
                    rejectedSamples++;
            }
            Check(steepSamples > 0 && maxSlope > walkableSlope + 1f,
                label + " did not expose terrain steeper than the local NavMesh agent contract", failures);
            Check(rejectedSamples > 0,
                label + " local NavMesh did not reject any objectively steep terrain samples", failures);
            Debug.Log(
                "[TerrainMaterialization][SLOPE_EVIDENCE]" +
                "\n  AgentWalkableSlope: " + walkableSlope.ToString("0.##", CultureInfo.InvariantCulture) +
                "\n  MaxSampledSlope: " + maxSlope.ToString("0.##", CultureInfo.InvariantCulture) +
                "\n  SteepSamples: " + steepSamples +
                "\n  RejectedByLocalNavMesh: " + rejectedSamples);
        }

        private static void ValidateHeightSamples(
            TerrainMaterializationPlan plan,
            Terrain terrain,
            string label,
            ICollection<string> failures)
        {
            int last = plan.HeightmapResolution - 1;
            int middle = last / 2;
            int[] indices = { 0, middle, last };
            for (int zIndex = 0; zIndex < indices.Length; zIndex++)
            for (int xIndex = 0; xIndex < indices.Length; xIndex++)
            {
                int x = indices[xIndex];
                int z = indices[zIndex];
                float expected = plan.HeightAt(x, z) * plan.Configuration.VerticalRelief;
                float actual = terrain.terrainData.GetHeight(x, z);
                Check(Mathf.Abs(expected - actual) <= 0.02f,
                    label + " physical heightmap differs from MacroGeography projection at " + x + "," + z,
                    failures);
            }
        }

        private static void ValidateWater(
            TerrainMaterializationPlan plan,
            GameObject generatedRoot,
            string label,
            ICollection<string> failures)
        {
            Transform water = generatedRoot != null
                ? generatedRoot.transform.Find("Committed Ocean Mask Visualization")
                : null;
            int expectedOceanCells = 0;
            for (int z = 0; z < plan.WaterMaskResolution; z++)
            for (int x = 0; x < plan.WaterMaskResolution; x++)
                if (plan.IsOceanCell(x, z)) expectedOceanCells++;
            Check(expectedOceanCells == 0 ? water == null : water != null,
                label + " water mesh presence does not match committed ocean mask", failures);
            if (water == null) return;
            Mesh mesh = water.GetComponent<MeshFilter>()?.sharedMesh;
            Check(mesh != null && mesh.vertexCount == expectedOceanCells * 4,
                label + " water mesh cell count does not match committed ocean mask", failures);
            if (mesh == null) return;
            Vector3[] vertices = mesh.vertices;
            for (int index = 0; index < vertices.Length; index += Math.Max(1, vertices.Length / 17))
                Check(Mathf.Abs(vertices[index].y - (plan.PhysicalWaterLevel + 0.12f)) <= 0.001f,
                    label + " water vertex is not aligned to committed sea level", failures);

            int oceanNavMeshSamples = 0;
            for (int z = 0; z < plan.WaterMaskResolution; z++)
            for (int x = 0; x < plan.WaterMaskResolution; x++)
            {
                if (!plan.IsOceanCell(x, z)) continue;
                float localX = -plan.Configuration.PhysicalWidth * 0.5f +
                               plan.Configuration.PhysicalWidth * (x + 0.5f) /
                               plan.WaterMaskResolution;
                float localZ = -plan.Configuration.PhysicalLength * 0.5f +
                               plan.Configuration.PhysicalLength * (z + 0.5f) /
                               plan.WaterMaskResolution;
                var point = new Vector3(
                    localX,
                    plan.HeightNormalizedAtLocal(localX, localZ) *
                    plan.Configuration.VerticalRelief,
                    localZ);
                if (NavMesh.SamplePosition(point, out _, 0.2f, NavMesh.AllAreas))
                    oceanNavMeshSamples++;
            }
            Check(oceanNavMeshSamples == 0,
                label + " local terrestrial NavMesh leaked into committed ocean cells", failures);
        }

        private static void ValidateRoads(
            TerrainMaterializationPlan plan,
            GameObject generatedRoot,
            string label,
            ICollection<string> failures)
        {
            LineRenderer[] lines = generatedRoot != null
                ? generatedRoot.GetComponentsInChildren<LineRenderer>(true)
                : Array.Empty<LineRenderer>();
            Check(lines.Length == plan.Roads.Count,
                label + " projected road fragment count differs from persisted-road projection", failures);
            var matched = new bool[plan.Roads.Count];
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                LineRenderer line = lines[lineIndex];
                bool found = false;
                for (int roadIndex = 0; roadIndex < plan.Roads.Count && !found; roadIndex++)
                {
                    if (matched[roadIndex]) continue;
                    TerrainProjectedRoad road = plan.Roads[roadIndex];
                    if (!line.name.Contains(road.RoadId.Canonical) || line.positionCount != road.Points.Count)
                        continue;
                    bool pointsMatch = true;
                    for (int pointIndex = 0; pointIndex < road.Points.Count; pointIndex++)
                    {
                        Vector3 physical = line.GetPosition(pointIndex);
                        Vector2 projected = road.Points[pointIndex];
                        float expectedY = plan.HeightNormalizedAtLocal(projected.x, projected.y) *
                                          plan.Configuration.VerticalRelief + 0.45f;
                        if (Mathf.Abs(physical.x - projected.x) > 0.001f ||
                            Mathf.Abs(physical.z - projected.y) > 0.001f ||
                            Mathf.Abs(physical.y - expectedY) > 0.02f)
                        {
                            pointsMatch = false;
                            break;
                        }
                    }
                    if (!pointsMatch) continue;
                    matched[roadIndex] = true;
                    found = true;
                }
                Check(found, label + " contains road geometry not aligned to the shared macro-to-local frame", failures);
            }
        }

        private static bool TryCapture(
            TerrainMaterializationResult result,
            string path,
            out string error)
        {
            error = null;
            GameObject cameraObject = null;
            Camera camera = null;
            RenderTexture renderTexture = null;
            Texture2D pixels = null;
            try
            {
                Terrain terrain = result.Terrain;
                Vector3 size = terrain.terrainData.size;
                Vector3 center = terrain.transform.position +
                                 new Vector3(size.x * 0.5f, size.y * 0.45f, size.z * 0.5f);
                cameraObject = new GameObject("Terrain Evidence Camera");
                camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.075f, 0.09f, 0.12f, 1f);
                camera.fieldOfView = 42f;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = Math.Max(3000f, size.x * 4f);
                camera.transform.position = center +
                    new Vector3(-size.x * 0.58f, Math.Max(380f, size.y * 2.2f), -size.z * 0.58f);
                camera.transform.LookAt(center);
                renderTexture = new RenderTexture(960, 720, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = renderTexture;
                pixels = new Texture2D(960, 720, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, 960, 720), 0, 0);
                pixels.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                if (!File.Exists(path) || new FileInfo(path).Length < 4096)
                {
                    error = "rendered PNG was missing or unexpectedly small";
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
            finally
            {
                if (camera != null) camera.targetTexture = null;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                if (renderTexture != null) UnityEngine.Object.DestroyImmediate(renderTexture);
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static bool TryFindRepresentativeCenter(
            WorldSession session,
            RepresentativeKind kind,
            out MacroPoint2D center,
            out string error)
        {
            center = default;
            error = null;
            MacroGeographyPlan geography = session.MacroGeography;
            MacroWaterPlan water = session.MacroWater;
            bool found = false;
            long bestDistance = long.MaxValue;
            long worldCenterX = geography.WorldBounds.MinX + geography.WorldBounds.Width / 2L;
            long worldCenterY = geography.WorldBounds.MinY + geography.WorldBounds.Height / 2L;
            for (int row = 0; row < geography.SampleRows; row++)
            for (int column = 0; column < geography.SampleColumns; column++)
            {
                MacroPoint2D point = SamplePoint(geography.WorldBounds, column, row,
                    geography.SampleColumns, geography.SampleRows);
                MacroWaterSample waterSample = water.SampleAt(point);
                MacroLandform landform = geography.LandformAt(point);
                bool matches;
                switch (kind)
                {
                    case RepresentativeKind.InlandPlains:
                        matches = waterSample.IsLand && !waterSample.IsCoastline &&
                                  landform == MacroLandform.Plains;
                        break;
                    case RepresentativeKind.RuggedLand:
                        matches = waterSample.IsLand &&
                                  (landform == MacroLandform.Mountains ||
                                   landform == MacroLandform.Highlands);
                        break;
                    case RepresentativeKind.Coast:
                        matches = waterSample.IsLand && waterSample.IsCoastline;
                        break;
                    default:
                        matches = false;
                        break;
                }
                if (!matches) continue;
                long dx = point.X - worldCenterX;
                long dy = point.Y - worldCenterY;
                long distance = dx * dx + dy * dy;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    center = point;
                    found = true;
                }
            }
            if (!found)
                error = "no committed sample matched " + kind;
            return found;
        }

        private static MacroPoint2D SamplePoint(
            FiniteMacroWorldBounds bounds,
            int column,
            int row,
            int columns,
            int rows)
        {
            return new MacroPoint2D(
                SampleAxis(bounds.MinX, bounds.Width, column, columns),
                SampleAxis(bounds.MinY, bounds.Height, row, rows));
        }

        private static long SampleAxis(long minimum, long extent, int index, int count)
        {
            long denominator = count - 1L;
            long numerator = (long)index * (extent - 1L);
            return minimum + (numerator + denominator / 2L) / denominator;
        }

        private static WorldSession CreatePersistedFixture(
            long seed,
            string displayName,
            LoadedContentSet content,
            PersistenceFileStore store,
            ICollection<string> failures)
        {
            if (!WorldSessionBootstrap.TryBuildNew(
                    displayName, new WorldSeed(seed),
                    WorldGenerationSettings.ResolvePreset(WorldSizePreset.Large),
                    LandCoveragePreset.High, content,
                    out WorldSession generated, out string buildError))
            {
                failures.Add(displayName + " generation failed: " + Safe(buildError));
                return null;
            }
            WorldSessionPersistenceResult save = WorldSessionPersistenceService.Save(generated, store);
            if (!save.Success)
            {
                failures.Add(displayName + " persistence failed: " + save.Failure);
                return null;
            }
            WorldSessionPersistenceResult load =
                WorldSessionPersistenceService.Read(generated.WorldId.Canonical, store);
            if (!load.Success)
            {
                failures.Add(displayName + " reload failed: " + load.Failure);
                return null;
            }
            if (load.Session.MacroWorldPlan.CanonicalHash != generated.MacroWorldPlan.CanonicalHash ||
                load.Session.MacroGeography.CanonicalHash != generated.MacroGeography.CanonicalHash ||
                load.Session.MacroWater.CanonicalHash != generated.MacroWater.CanonicalHash ||
                load.Session.MacroHumanGeography.CanonicalHash != generated.MacroHumanGeography.CanonicalHash)
            {
                failures.Add(displayName + " committed logical evidence changed across current-schema reload");
                return null;
            }
            return load.Session;
        }

        private static LoadedContentSet LoadValidatedCore(ICollection<string> failures)
        {
            string modsRoot = Path.Combine(Application.streamingAssetsPath, "Mods");
            var report = new DataLoadReport();
            var loader = new GameDataLoader(modsRoot, report);
            loader.LoadAll();
            var validator = new DataValidator(loader.Database, loader.Tags, report);
            validator.Validate();
            if (report.HasErrors || !loader.TryBuildLoadedContentSet(out LoadedContentSet content))
            {
                failures.Add("real Core loader/DataValidator/provenance validation failed");
                return null;
            }
            return content;
        }

        private static void CreateDiagnosticCamera()
        {
            var cameraObject = new GameObject("Diagnostic Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>();
        }

        private static void Complete(IList<string> failures, params string[] evidence)
        {
            if (failures.Count > 0)
            {
                string message = "Terrain Materialization Technical Spike Diagnostics: FAIL\n- " +
                                 string.Join("\n- ", failures);
                Debug.LogError(message);
                throw new InvalidOperationException(message);
            }
            Debug.Log("Terrain Materialization Technical Spike Diagnostics: PASS\n" +
                      string.Join("\n", evidence));
        }

        private static void Check(bool condition, string failure, ICollection<string> failures)
        {
            if (!condition) failures.Add(failure);
        }

        private static string Safe(string value) => string.IsNullOrEmpty(value) ? "<NONE>" : value;

        private enum RepresentativeKind
        {
            InlandPlains,
            RuggedLand,
            Coast
        }
    }
}
