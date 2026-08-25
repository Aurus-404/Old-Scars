using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace OldScars.Core.World
{
    public sealed class TerrainMaterializationResult
    {
        internal TerrainMaterializationResult(
            TerrainMaterializationPlan plan,
            Terrain terrain,
            TerrainCollider terrainCollider,
            NavMeshSurface navMeshSurface,
            Vector3 spawnPosition,
            Vector3 pathDestination,
            Vector3[] pathCorners,
            int navMeshVertexCount,
            int oceanCellCount,
            int generatedObjectCount,
            long approximateRuntimeBytes,
            long projectionElapsedMilliseconds,
            long terrainElapsedMilliseconds,
            long navMeshElapsedMilliseconds,
            long totalElapsedMilliseconds)
        {
            Plan = plan;
            Terrain = terrain;
            TerrainCollider = terrainCollider;
            NavMeshSurface = navMeshSurface;
            SpawnPosition = spawnPosition;
            PathDestination = pathDestination;
            PathCorners = pathCorners;
            NavMeshVertexCount = navMeshVertexCount;
            OceanCellCount = oceanCellCount;
            GeneratedObjectCount = generatedObjectCount;
            ApproximateRuntimeBytes = approximateRuntimeBytes;
            ProjectionElapsedMilliseconds = projectionElapsedMilliseconds;
            TerrainElapsedMilliseconds = terrainElapsedMilliseconds;
            NavMeshElapsedMilliseconds = navMeshElapsedMilliseconds;
            TotalElapsedMilliseconds = totalElapsedMilliseconds;
        }

        public TerrainMaterializationPlan Plan { get; }
        public Terrain Terrain { get; }
        public TerrainCollider TerrainCollider { get; }
        public NavMeshSurface NavMeshSurface { get; }
        public Vector3 SpawnPosition { get; }
        public Vector3 PathDestination { get; }
        public IReadOnlyList<Vector3> PathCorners { get; }
        public int NavMeshVertexCount { get; }
        public int OceanCellCount { get; }
        public int GeneratedObjectCount { get; }
        public long ApproximateRuntimeBytes { get; }
        public long ProjectionElapsedMilliseconds { get; }
        public long TerrainElapsedMilliseconds { get; }
        public long NavMeshElapsedMilliseconds { get; }
        public long TotalElapsedMilliseconds { get; }
    }

    /// <summary>
    /// Scene-local owner of the technical spike representation. It consumes a
    /// committed logical plan once and owns only transient Unity objects.
    /// Sector identity, persistence, generation, and gameplay state remain in
    /// their existing authorities.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldTerrainMaterializationController : MonoBehaviour
    {
        private const int GroundLayer = 3;
        private const int WaterLayer = 4;
        private const int NavigationSourceLayer = 0;
        private const int SpawnSearchResolution = 21;

        [SerializeField] private TerrainMaterializationConfiguration configuration =
            TerrainMaterializationConfiguration.CreateProvisionalBaseline();

        private readonly List<UnityEngine.Object> ownedAssets = new List<UnityEngine.Object>();
        private GameObject generatedRoot;
        private NavMeshSurface navMeshSurface;

        public TerrainMaterializationConfiguration Configuration => configuration;
        public TerrainMaterializationResult Result { get; private set; }
        public string Failure { get; private set; }
        public bool IsReady => Result != null && string.IsNullOrEmpty(Failure);
        public GameObject GeneratedRoot => generatedRoot;

        public bool TryMaterializeActiveSession(WorldSession session)
        {
            return TryMaterializeActiveSession(session, configuration);
        }

        public bool TryMaterializeActiveSession(
            WorldSession session,
            TerrainMaterializationConfiguration selectedConfiguration)
        {
            Stopwatch total = Stopwatch.StartNew();
            Stopwatch projection = Stopwatch.StartNew();
            if (!TerrainMaterializationPlanner.TryBuildActiveRegion(
                    session, selectedConfiguration,
                    out TerrainMaterializationPlan plan, out string error))
            {
                projection.Stop();
                return Fail(session, error);
            }
            projection.Stop();
            return TryMaterializePlan(plan, projection.ElapsedMilliseconds, total);
        }

        /// <summary>
        /// Tooling-only representative-region seam. Runtime product flow uses
        /// the active-sector overload above.
        /// </summary>
        public bool TryMaterializeAt(
            WorldSession session,
            TerrainMaterializationConfiguration selectedConfiguration,
            MacroPoint2D logicalCenter)
        {
            Stopwatch total = Stopwatch.StartNew();
            Stopwatch projection = Stopwatch.StartNew();
            if (!TerrainMaterializationPlanner.TryBuildAt(
                    session, selectedConfiguration, logicalCenter,
                    out TerrainMaterializationPlan plan, out string error))
            {
                projection.Stop();
                return Fail(session, error);
            }
            projection.Stop();
            return TryMaterializePlan(plan, projection.ElapsedMilliseconds, total);
        }

        public void ClearMaterialization()
        {
            Result = null;
            Failure = null;
            if (navMeshSurface != null)
            {
                navMeshSurface.RemoveData();
                navMeshSurface = null;
            }
            DestroyOwnedObject(generatedRoot);
            generatedRoot = null;
            for (int index = ownedAssets.Count - 1; index >= 0; index--)
                DestroyOwnedObject(ownedAssets[index]);
            ownedAssets.Clear();
        }

        private void OnDestroy()
        {
            ClearMaterialization();
        }

        private bool TryMaterializePlan(
            TerrainMaterializationPlan plan,
            long projectionElapsedMilliseconds,
            Stopwatch total)
        {
            ClearMaterialization();
            try
            {
                generatedRoot = new GameObject("Generated Active Region [Terrain Spike]");
                generatedRoot.transform.SetParent(transform, false);

                Stopwatch terrainWatch = Stopwatch.StartNew();
                Terrain terrain = CreateTerrain(plan, out TerrainCollider terrainCollider);
                int oceanCells = CreateWater(plan);
                CreateRoads(plan);
                MeshCollider navigationSource = CreateNavigationLandProxy(plan);
                terrainWatch.Stop();

                Stopwatch navWatch = Stopwatch.StartNew();
                navMeshSurface = BuildLocalNavMesh(plan, navigationSource);
                NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
                if (navMeshSurface.navMeshData == null || triangulation.vertices.Length == 0)
                    throw new InvalidOperationException("local NavMesh build produced no active triangulation");
                if (!TryFindSpawnAndPath(
                        plan, terrain, out Vector3 spawn, out Vector3 destination,
                        out Vector3[] pathCorners, out string pathFailure))
                {
                    throw new InvalidOperationException(pathFailure);
                }
                navWatch.Stop();

                Physics.SyncTransforms();
                total.Stop();

                int objectCount = generatedRoot.GetComponentsInChildren<Transform>(true).Length;
                long approximateBytes = Profiler.GetRuntimeMemorySizeLong(terrain.terrainData);
                for (int index = 0; index < ownedAssets.Count; index++)
                    approximateBytes += Profiler.GetRuntimeMemorySizeLong(ownedAssets[index]);
                Result = new TerrainMaterializationResult(
                    plan, terrain, terrainCollider, navMeshSurface,
                    spawn, destination, pathCorners, triangulation.vertices.Length,
                    oceanCells, objectCount, approximateBytes,
                    projectionElapsedMilliseconds, terrainWatch.ElapsedMilliseconds,
                    navWatch.ElapsedMilliseconds, total.ElapsedMilliseconds);
                LogReady(Result);
                return true;
            }
            catch (Exception exception)
            {
                return FailPlan(plan, exception.Message);
            }
        }

        private Terrain CreateTerrain(
            TerrainMaterializationPlan plan,
            out TerrainCollider terrainCollider)
        {
            var terrainData = new TerrainData
            {
                name = "Transient Active Region TerrainData",
                heightmapResolution = plan.Configuration.HeightmapResolution,
                size = new Vector3(
                    plan.Configuration.PhysicalWidth,
                    plan.Configuration.VerticalRelief,
                    plan.Configuration.PhysicalLength),
                alphamapResolution = plan.Configuration.AlphamapResolution
            };
            ownedAssets.Add(terrainData);
            terrainData.SetHeights(0, 0, plan.CopyHeights());
            ApplyDiagnosticLandformLayers(terrainData, plan);

            GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            terrainObject.name = "Local Unity Terrain (not a Sector)";
            terrainObject.layer = GroundLayer;
            terrainObject.transform.SetParent(generatedRoot.transform, false);
            terrainObject.transform.localPosition = new Vector3(
                -plan.Configuration.PhysicalWidth * 0.5f,
                0f,
                -plan.Configuration.PhysicalLength * 0.5f);
            Terrain terrain = terrainObject.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 4f;
            terrainCollider = terrainObject.GetComponent<TerrainCollider>();
            if (terrainCollider == null || terrainCollider.terrainData != terrainData)
                throw new InvalidOperationException("Unity Terrain did not create a usable TerrainCollider");
            return terrain;
        }

        private void ApplyDiagnosticLandformLayers(
            TerrainData terrainData,
            TerrainMaterializationPlan plan)
        {
            Color[] colors =
            {
                new Color(0.29f, 0.43f, 0.22f, 1f),
                new Color(0.42f, 0.43f, 0.25f, 1f),
                new Color(0.46f, 0.38f, 0.29f, 1f),
                new Color(0.58f, 0.54f, 0.48f, 1f)
            };
            var layers = new TerrainLayer[colors.Length];
            for (int index = 0; index < layers.Length; index++)
            {
                Texture2D texture = CreateSolidTexture(colors[index], "Landform Tint " + index);
                var layer = new TerrainLayer
                {
                    name = "Diagnostic " + ((MacroLandform)index),
                    diffuseTexture = texture,
                    tileSize = new Vector2(48f, 48f),
                    metallic = 0f,
                    smoothness = 0.05f
                };
                layers[index] = layer;
                ownedAssets.Add(layer);
            }
            terrainData.terrainLayers = layers;

            int resolution = terrainData.alphamapResolution;
            var weights = new float[resolution, resolution, colors.Length];
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                float nx = resolution == 1 ? 0f : x / (float)(resolution - 1);
                float nz = resolution == 1 ? 0f : z / (float)(resolution - 1);
                weights[z, x, (int)plan.LandformAtNormalized(nx, nz)] = 1f;
            }
            terrainData.SetAlphamaps(0, 0, weights);
        }

        private int CreateWater(TerrainMaterializationPlan plan)
        {
            int resolution = plan.WaterMaskResolution;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            int oceanCells = 0;
            float width = plan.Configuration.PhysicalWidth;
            float length = plan.Configuration.PhysicalLength;
            float y = plan.PhysicalWaterLevel + 0.12f;
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                if (!plan.IsOceanCell(x, z)) continue;
                oceanCells++;
                float x0 = -width * 0.5f + width * x / resolution;
                float x1 = -width * 0.5f + width * (x + 1) / resolution;
                float z0 = -length * 0.5f + length * z / resolution;
                float z1 = -length * 0.5f + length * (z + 1) / resolution;
                int start = vertices.Count;
                vertices.Add(new Vector3(x0, y, z0));
                vertices.Add(new Vector3(x1, y, z0));
                vertices.Add(new Vector3(x1, y, z1));
                vertices.Add(new Vector3(x0, y, z1));
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 1);
                triangles.Add(start);
                triangles.Add(start + 3);
                triangles.Add(start + 2);
            }
            if (oceanCells == 0)
                return 0;

            var waterObject = new GameObject("Committed Ocean Mask Visualization");
            waterObject.layer = WaterLayer;
            waterObject.transform.SetParent(generatedRoot.transform, false);
            MeshFilter filter = waterObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = waterObject.AddComponent<MeshRenderer>();
            var mesh = new Mesh { name = "Transient Ocean Mask Mesh" };
            if (vertices.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            var normals = new Vector3[vertices.Count];
            for (int index = 0; index < normals.Length; index++) normals[index] = Vector3.up;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            filter.sharedMesh = mesh;
            ownedAssets.Add(mesh);
            renderer.sharedMaterial = CreateUnlitMaterial(
                "Diagnostic Ocean", new Color(0.08f, 0.30f, 0.52f, 0.82f));
            return oceanCells;
        }

        private void CreateRoads(TerrainMaterializationPlan plan)
        {
            if (plan.Roads.Count == 0)
                return;
            Material primary = CreateUnlitMaterial(
                "Diagnostic Primary Road", new Color(0.92f, 0.70f, 0.24f, 1f));
            Material secondary = CreateUnlitMaterial(
                "Diagnostic Secondary Road", new Color(0.76f, 0.58f, 0.30f, 1f));
            for (int roadIndex = 0; roadIndex < plan.Roads.Count; roadIndex++)
            {
                TerrainProjectedRoad road = plan.Roads[roadIndex];
                var roadObject = new GameObject(
                    "Persisted " + road.RoadClass + " " + road.RoadId.Canonical);
                roadObject.transform.SetParent(generatedRoot.transform, false);
                LineRenderer line = roadObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.alignment = LineAlignment.View;
                line.textureMode = LineTextureMode.Stretch;
                line.numCapVertices = 2;
                line.numCornerVertices = 2;
                line.sharedMaterial = road.RoadClass == MacroRoadClass.Primary ? primary : secondary;
                float width = plan.Configuration.PrimaryRoadWidth *
                              (road.RoadClass == MacroRoadClass.Primary ? 1f : 0.62f);
                line.startWidth = width;
                line.endWidth = width;
                line.positionCount = road.Points.Count;
                for (int pointIndex = 0; pointIndex < road.Points.Count; pointIndex++)
                {
                    Vector2 point = road.Points[pointIndex];
                    float height = plan.HeightNormalizedAtLocal(point.x, point.y) *
                                   plan.Configuration.VerticalRelief;
                    line.SetPosition(pointIndex, new Vector3(point.x, height + 0.45f, point.y));
                }
            }
        }

        private MeshCollider CreateNavigationLandProxy(TerrainMaterializationPlan plan)
        {
            int cells = plan.WaterMaskResolution;
            int verticesPerAxis = cells + 1;
            var vertices = new List<Vector3>(verticesPerAxis * verticesPerAxis);
            var triangles = new List<int>(cells * cells * 6);
            float width = plan.Configuration.PhysicalWidth;
            float length = plan.Configuration.PhysicalLength;
            for (int z = 0; z <= cells; z++)
            for (int x = 0; x <= cells; x++)
            {
                float nx = x / (float)cells;
                float nz = z / (float)cells;
                float localX = -width * 0.5f + width * nx;
                float localZ = -length * 0.5f + length * nz;
                float height = plan.HeightNormalizedAtLocal(localX, localZ) *
                               plan.Configuration.VerticalRelief;
                vertices.Add(new Vector3(localX, height, localZ));
            }
            for (int z = 0; z < cells; z++)
            for (int x = 0; x < cells; x++)
            {
                if (plan.IsOceanCell(x, z))
                    continue;
                int lowerLeft = z * verticesPerAxis + x;
                int lowerRight = lowerLeft + 1;
                int upperLeft = lowerLeft + verticesPerAxis;
                int upperRight = upperLeft + 1;
                triangles.Add(lowerLeft);
                triangles.Add(upperRight);
                triangles.Add(lowerRight);
                triangles.Add(lowerLeft);
                triangles.Add(upperLeft);
                triangles.Add(upperRight);
            }
            if (triangles.Count == 0)
                throw new InvalidOperationException("local materialization window contains no terrestrial navigation surface");

            var mesh = new Mesh { name = "Transient Land Navigation Proxy Mesh" };
            if (vertices.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            ownedAssets.Add(mesh);

            var proxy = new GameObject("Local Land Navigation Proxy [internal partition]");
            proxy.layer = NavigationSourceLayer;
            proxy.transform.SetParent(generatedRoot.transform, false);
            MeshCollider collider = proxy.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            return collider;
        }

        private NavMeshSurface BuildLocalNavMesh(
            TerrainMaterializationPlan plan,
            MeshCollider navigationSource)
        {
            NavMeshSurface surface = generatedRoot.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = 1 << NavigationSourceLayer;
            surface.overrideTileSize = true;
            surface.tileSize = plan.Configuration.NavMeshTileSize;
            surface.minRegionArea = 4f;
            surface.BuildNavMesh();
            navigationSource.enabled = false;
            return surface;
        }

        private static bool TryFindSpawnAndPath(
            TerrainMaterializationPlan plan,
            Terrain terrain,
            out Vector3 spawn,
            out Vector3 destination,
            out Vector3[] pathCorners,
            out string failure)
        {
            spawn = default;
            destination = default;
            pathCorners = Array.Empty<Vector3>();
            failure = null;
            List<SpawnCandidate> candidates = BuildSpawnCandidates();
            var resolved = new List<Vector3>();
            for (int index = 0; index < candidates.Count; index++)
            {
                SpawnCandidate candidate = candidates[index];
                if (plan.IsOceanAtNormalized(candidate.NormalizedX, candidate.NormalizedZ))
                    continue;
                float steepness = terrain.terrainData.GetSteepness(
                    candidate.NormalizedX, candidate.NormalizedZ);
                if (steepness > plan.Configuration.MaximumSpawnSlopeDegrees)
                    continue;
                Vector3 world = new Vector3(
                    terrain.transform.position.x + candidate.NormalizedX * plan.Configuration.PhysicalWidth,
                    0f,
                    terrain.transform.position.z + candidate.NormalizedZ * plan.Configuration.PhysicalLength);
                world.y = terrain.SampleHeight(world) + terrain.transform.position.y;
                if (NavMesh.SamplePosition(world, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                    resolved.Add(hit.position);
            }
            if (resolved.Count < 2)
            {
                failure = "local terrain did not yield two safe terrestrial NavMesh positions";
                return false;
            }

            spawn = resolved[0];
            float bestDistance = 0f;
            Vector3 bestDestination = default;
            Vector3[] bestCorners = null;
            for (int index = 1; index < resolved.Count; index++)
            {
                float distance = Vector3.ProjectOnPlane(resolved[index] - spawn, Vector3.up).sqrMagnitude;
                if (distance <= bestDistance) continue;
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(spawn, resolved[index], NavMesh.AllAreas, path) ||
                    path.status != NavMeshPathStatus.PathComplete || path.corners.Length < 2)
                    continue;
                bestDistance = distance;
                bestDestination = resolved[index];
                bestCorners = (Vector3[])path.corners.Clone();
            }
            if (bestCorners == null)
            {
                failure = "local NavMesh contained safe samples but no representative complete path";
                return false;
            }
            destination = bestDestination;
            pathCorners = bestCorners;
            return true;
        }

        private Material CreateUnlitMaterial(string materialName, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                            Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader == null)
                throw new InvalidOperationException("No supported unlit shader is available for terrain-spike visualization");
            var material = new Material(shader) { name = materialName };
            SetMaterialColor(material, color);
            ownedAssets.Add(material);
            return material;
        }

        private Texture2D CreateSolidTexture(Color color, string textureName)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply(false, true);
            ownedAssets.Add(texture);
            return texture;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        private static List<SpawnCandidate> BuildSpawnCandidates()
        {
            var candidates = new List<SpawnCandidate>(SpawnSearchResolution * SpawnSearchResolution);
            for (int z = 1; z < SpawnSearchResolution - 1; z++)
            for (int x = 1; x < SpawnSearchResolution - 1; x++)
            {
                float nx = x / (float)(SpawnSearchResolution - 1);
                float nz = z / (float)(SpawnSearchResolution - 1);
                candidates.Add(new SpawnCandidate(nx, nz));
            }
            candidates.Sort((left, right) =>
            {
                int comparison = left.DistanceToCenterSquared.CompareTo(right.DistanceToCenterSquared);
                if (comparison != 0) return comparison;
                comparison = left.NormalizedZ.CompareTo(right.NormalizedZ);
                return comparison != 0 ? comparison : left.NormalizedX.CompareTo(right.NormalizedX);
            });
            return candidates;
        }

        private bool Fail(WorldSession session, string failure)
        {
            ClearMaterialization();
            Failure = string.IsNullOrEmpty(failure) ? "unknown materialization failure" : failure;
            Debug.LogError(
                "[WorldMaterialization][FAIL]\n" +
                "WorldId: " + (session?.WorldId.Canonical ?? "<NONE>") + "\n" +
                "SectorId: " + (session?.ActiveSectorId.Canonical ?? "<NONE>") + "\n" +
                "Reason: " + Failure);
            return false;
        }

        private bool FailPlan(TerrainMaterializationPlan plan, string failure)
        {
            WorldId worldId = plan?.WorldId ?? default;
            SectorId sectorId = plan?.SectorId ?? default;
            ClearMaterialization();
            Failure = string.IsNullOrEmpty(failure) ? "unknown materialization failure" : failure;
            Debug.LogError(
                "[WorldMaterialization][FAIL]\n" +
                "WorldId: " + (worldId.IsValid ? worldId.Canonical : "<NONE>") + "\n" +
                "SectorId: " + (sectorId.IsValid ? sectorId.Canonical : "<NONE>") + "\n" +
                "Reason: " + Failure);
            return false;
        }

        private static void LogReady(TerrainMaterializationResult result)
        {
            TerrainMaterializationPlan plan = result.Plan;
            Debug.Log(
                "[WorldMaterialization][READY]\n" +
                "WorldId: " + plan.WorldId.Canonical + "\n" +
                "SectorId: " + plan.SectorId.Canonical + "\n" +
                "LogicalSampleBounds: " + plan.Window + "\n" +
                "PhysicalTerrain: " + plan.Configuration.PhysicalWidth + "x" +
                plan.Configuration.PhysicalLength + " / vertical " +
                plan.Configuration.VerticalRelief + " Unity units\n" +
                "HeightmapResolution: " + plan.HeightmapResolution + "\n" +
                "PhysicalElevationRange: " +
                (plan.MinimumElevation / 65535f * plan.Configuration.VerticalRelief).ToString("0.###") +
                ".." + (plan.MaximumElevation / 65535f * plan.Configuration.VerticalRelief).ToString("0.###") + "\n" +
                "WaterLevel: " + plan.PhysicalWaterLevel.ToString("0.###") +
                " / ocean cells " + result.OceanCellCount + "\n" +
                "RoadsIntersecting: " + plan.IntersectingRoadCount +
                " / projected fragments " + plan.Roads.Count + "\n" +
                "NavMesh: vertices " + result.NavMeshVertexCount +
                " / complete path corners " + result.PathCorners.Count + "\n" +
                "ElapsedMs: projection " + result.ProjectionElapsedMilliseconds +
                " / terrain " + result.TerrainElapsedMilliseconds +
                " / NavMesh " + result.NavMeshElapsedMilliseconds +
                " / total " + result.TotalElapsedMilliseconds);
        }

        private static void DestroyOwnedObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }

        private readonly struct SpawnCandidate
        {
            public SpawnCandidate(float normalizedX, float normalizedZ)
            {
                NormalizedX = normalizedX;
                NormalizedZ = normalizedZ;
                float dx = normalizedX - 0.5f;
                float dz = normalizedZ - 0.5f;
                DistanceToCenterSquared = dx * dx + dz * dz;
            }

            public float NormalizedX { get; }
            public float NormalizedZ { get; }
            public float DistanceToCenterSquared { get; }
        }
    }
}
