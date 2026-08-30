using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace OldScars.Core.World
{
    public sealed class DeformableTerrainSpikeMetrics
    {
        internal DeformableTerrainSpikeMetrics(
            DeformableTerrainMesherBackend mesherBackend,
            int chunkCount,
            int densitySamples,
            long fieldBytes,
            int vertices,
            int triangles,
            int indices,
            int reusedVertexReferences,
            long meshBytes,
            long meshingAllocatedBytes,
            long densityMilliseconds,
            long meshingMilliseconds,
            long meshAssignmentMilliseconds,
            long colliderMilliseconds)
        {
            MesherBackend = mesherBackend;
            ChunkCount = chunkCount;
            DensitySamples = densitySamples;
            ApproximateFieldBytes = fieldBytes;
            Vertices = vertices;
            Triangles = triangles;
            Indices = indices;
            ReusedVertexReferences = reusedVertexReferences;
            ApproximateMeshBytes = meshBytes;
            MeshingAllocatedBytes = meshingAllocatedBytes;
            DensityGenerationMilliseconds = densityMilliseconds;
            InitialMeshingMilliseconds = meshingMilliseconds;
            MeshAssignmentMilliseconds = meshAssignmentMilliseconds;
            ColliderUpdateMilliseconds = colliderMilliseconds;
        }

        public DeformableTerrainMesherBackend MesherBackend { get; }
        public int ChunkCount { get; }
        public int DensitySamples { get; }
        public long ApproximateFieldBytes { get; }
        public int Vertices { get; }
        public int Triangles { get; }
        public int Indices { get; }
        public int ReusedVertexReferences { get; }
        public long ApproximateMeshBytes { get; }
        public long MeshingAllocatedBytes { get; }
        public long DensityGenerationMilliseconds { get; }
        public long InitialMeshingMilliseconds { get; }
        public long MeshAssignmentMilliseconds { get; }
        public long ColliderUpdateMilliseconds { get; }
    }

    public sealed class DeformableTerrainMutationResult
    {
        internal DeformableTerrainMutationResult(
            IReadOnlyList<DeformableTerrainChunkId> affectedChunks,
            long mutationMilliseconds,
            long meshingMilliseconds,
            long meshAssignmentMilliseconds,
            long colliderMilliseconds)
        {
            AffectedChunks = affectedChunks;
            MutationMilliseconds = mutationMilliseconds;
            MeshingMilliseconds = meshingMilliseconds;
            MeshAssignmentMilliseconds = meshAssignmentMilliseconds;
            ColliderUpdateMilliseconds = colliderMilliseconds;
        }

        public IReadOnlyList<DeformableTerrainChunkId> AffectedChunks { get; }
        public long MutationMilliseconds { get; }
        public long MeshingMilliseconds { get; }
        public long MeshAssignmentMilliseconds { get; }
        public long ColliderUpdateMilliseconds { get; }
    }

    /// <summary>
    /// Development-only scene representation for the volumetric terrain
    /// foundation spike. It consumes the existing projection and owns only a
    /// bounded local density volume, transient chunk meshes and colliders.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldDeformableTerrainSpikeController : MonoBehaviour
    {
        private const int GroundLayer = 3;

        private sealed class ChunkRepresentation
        {
            public GameObject GameObject;
            public MeshFilter MeshFilter;
            public MeshCollider MeshCollider;
            public Mesh Mesh;
            public int RebuildCount;
        }

        private readonly Dictionary<DeformableTerrainChunkId, ChunkRepresentation> chunks =
            new Dictionary<DeformableTerrainChunkId, ChunkRepresentation>();
        private readonly List<UnityEngine.Object> ownedAssets = new List<UnityEngine.Object>();
        private GameObject generatedRoot;
        private Material[] materials;
        private NavMeshSurface navMeshSurface;

        public TerrainMaterializationPlan SourcePlan { get; private set; }
        public DeformableTerrainMesherBackend MesherBackend { get; private set; }
        public DeformableTerrainVolume Volume { get; private set; }
        public DeformableTerrainMutationService MutationService { get; private set; }
        public DeformableTerrainSpikeMetrics Metrics { get; private set; }
        public Vector3 SpawnPosition { get; private set; }
        public string Failure { get; private set; }
        public bool IsReady => Volume != null && chunks.Count > 1 && string.IsNullOrEmpty(Failure);
        public GameObject GeneratedRoot => generatedRoot;
        public NavMeshSurface NavMeshSurface => navMeshSurface;
        public int NavMeshVertexCount { get; private set; }
        public long NavigationBuildMilliseconds { get; private set; }
        public bool HasMutations => MutationService?.Mutations.Count > 0;

        public bool TryMaterializeActiveSession(
            WorldSession session,
            TerrainMaterializationConfiguration projectionConfiguration,
            DeformableTerrainSpikeConfiguration spikeConfiguration)
        {
            return TryMaterializeActiveSession(
                session, projectionConfiguration, spikeConfiguration,
                DeformableTerrainMesherBackend.MarchingTetrahedra);
        }

        public bool TryMaterializeActiveSession(
            WorldSession session,
            TerrainMaterializationConfiguration projectionConfiguration,
            DeformableTerrainSpikeConfiguration spikeConfiguration,
            DeformableTerrainMesherBackend mesherBackend)
        {
            if (!TerrainMaterializationPlanner.TryBuildActiveRegion(
                    session, projectionConfiguration, out TerrainMaterializationPlan plan, out string error))
                return Fail(error);
            return TryMaterializePlan(plan, spikeConfiguration, mesherBackend);
        }

        public bool TryMaterializePlan(
            TerrainMaterializationPlan plan,
            DeformableTerrainSpikeConfiguration spikeConfiguration)
        {
            return TryMaterializePlan(
                plan, spikeConfiguration, DeformableTerrainMesherBackend.MarchingTetrahedra);
        }

        public bool TryMaterializePlan(
            TerrainMaterializationPlan plan,
            DeformableTerrainSpikeConfiguration spikeConfiguration,
            DeformableTerrainMesherBackend mesherBackend)
        {
            ClearMaterialization();
            try
            {
                if (!Enum.IsDefined(typeof(DeformableTerrainMesherBackend), mesherBackend))
                    throw new ArgumentOutOfRangeException(nameof(mesherBackend));
                SourcePlan = plan ?? throw new ArgumentNullException(nameof(plan));
                MesherBackend = mesherBackend;
                Stopwatch densityWatch = Stopwatch.StartNew();
                if (!DeformableTerrainVolume.TryCreate(
                        plan, spikeConfiguration, out DeformableTerrainVolume volume, out string error))
                    return Fail(error);
                densityWatch.Stop();
                Volume = volume;
                MutationService = new DeformableTerrainMutationService(volume);

                generatedRoot = new GameObject(
                    "Generated Local Volume [" + mesherBackend + "]");
                generatedRoot.transform.SetParent(transform, false);
                materials = CreateMaterials();
                foreach (DeformableTerrainChunkId chunkId in volume.EnumerateChunks())
                    CreateChunk(chunkId);

                Stopwatch meshingWatch = Stopwatch.StartNew();
                long allocationStart = Profiler.GetMonoUsedSizeLong();
                var meshData = new List<DeformableTerrainChunkMeshData>();
                foreach (DeformableTerrainChunkId chunkId in volume.EnumerateChunks())
                    meshData.Add(DeformableTerrainMesher.Build(volume, chunkId, mesherBackend));
                long meshingAllocatedBytes = Math.Max(
                    0L, Profiler.GetMonoUsedSizeLong() - allocationStart);
                meshingWatch.Stop();

                Stopwatch assignmentWatch = Stopwatch.StartNew();
                for (int index = 0; index < meshData.Count; index++)
                    AssignMeshData(meshData[index], false);
                assignmentWatch.Stop();

                Stopwatch colliderWatch = Stopwatch.StartNew();
                for (int index = 0; index < meshData.Count; index++)
                    AssignCollider(meshData[index].ChunkId);
                colliderWatch.Stop();
                Physics.SyncTransforms();

                RebuildLocalNavigation();

                if (!TryFindSurfacePoint(Vector3.zero, out Vector3 spawn))
                    throw new InvalidOperationException("no collider-backed surface was found near the volume center");
                SpawnPosition = spawn + Vector3.up * 0.2f;

                int vertices = 0;
                int triangles = 0;
                int indices = 0;
                int reusedVertexReferences = 0;
                for (int index = 0; index < meshData.Count; index++)
                {
                    vertices += meshData[index].Vertices.Count;
                    triangles += meshData[index].TriangleCount;
                    indices += meshData[index].IndexCount;
                    reusedVertexReferences += meshData[index].ReusedVertexReferenceCount;
                }
                long meshBytes = vertices * (3L * sizeof(float) + 3L * sizeof(float) + 2L * sizeof(float)) +
                                 indices * sizeof(int);
                Metrics = new DeformableTerrainSpikeMetrics(
                    mesherBackend,
                    chunks.Count, volume.DensitySampleCount, volume.ApproximateFieldBytes,
                    vertices, triangles, indices, reusedVertexReferences, meshBytes,
                    meshingAllocatedBytes, densityWatch.ElapsedMilliseconds,
                    meshingWatch.ElapsedMilliseconds, assignmentWatch.ElapsedMilliseconds,
                    colliderWatch.ElapsedMilliseconds);
                Debug.Log(
                    "[DeformableTerrainSpike][READY]\n" +
                    "Contract: " + DeformableTerrainSpikeConfiguration.Contract + "\n" +
                    "Mesher: " + Metrics.MesherBackend + "\n" +
                    "Chunks: " + Metrics.ChunkCount + "\n" +
                    "DensitySamples: " + Metrics.DensitySamples + "\n" +
                    "Vertices: " + Metrics.Vertices + "\n" +
                    "Triangles: " + Metrics.Triangles + "\n" +
                    "ReusedVertexReferences: " + Metrics.ReusedVertexReferences + "\n" +
                    "NavMeshVertices: " + NavMeshVertexCount + "\n" +
                    "NavigationBuildMs: " + NavigationBuildMilliseconds + "\n" +
                    "MeshingManagedAllocationBytes: " + Metrics.MeshingAllocatedBytes + "\n" +
                    "FieldBytesApprox: " + Metrics.ApproximateFieldBytes + "\n" +
                    "MeshBytesApprox: " + Metrics.ApproximateMeshBytes);
                return true;
            }
            catch (Exception exception)
            {
                return Fail(exception.Message);
            }
        }

        public bool TrySubtractSphere(
            Vector3 center,
            float radius,
            out DeformableTerrainMutationResult result,
            out string error)
        {
            return TryMutate(
                () => MutationService.SubtractSphere(center, radius), out result, out error);
        }

        public bool TrySubtractCapsule(
            Vector3 start,
            Vector3 end,
            float radius,
            out DeformableTerrainMutationResult result,
            out string error)
        {
            return TryMutate(
                () => MutationService.SubtractCapsule(start, end, radius), out result, out error);
        }

        public bool TryReplay(
            IEnumerable<DeformableTerrainMutation> mutations,
            out DeformableTerrainMutationResult result,
            out string error)
        {
            return TryMutate(
                () => MutationService.Replay(mutations), out result, out error);
        }

        public bool TryReset(out DeformableTerrainMutationResult result, out string error)
        {
            return TryMutate(() => MutationService.Reset(), out result, out error);
        }

        public bool TryFindSurfacePoint(Vector3 horizontalLocalPosition, out Vector3 point)
        {
            point = default;
            if (Volume == null || generatedRoot == null)
                return false;
            float top = Volume.Origin.y + Volume.VerticalCellSize * Volume.Configuration.VerticalCells + 10f;
            var ray = new Ray(
                new Vector3(horizontalLocalPosition.x, top, horizontalLocalPosition.z), Vector3.down);
            RaycastHit[] hits = Physics.RaycastAll(
                ray, top - Volume.Origin.y + 20f, 1 << GroundLayer,
                QueryTriggerInteraction.Ignore);
            float best = float.MinValue;
            bool found = false;
            for (int index = 0; index < hits.Length; index++)
            {
                if (!hits[index].transform.IsChildOf(generatedRoot.transform) || hits[index].point.y <= best)
                    continue;
                best = hits[index].point.y;
                point = hits[index].point;
                found = true;
            }
            return found;
        }

        public int GetChunkRebuildCount(DeformableTerrainChunkId chunkId)
        {
            return chunks.TryGetValue(chunkId, out ChunkRepresentation representation)
                ? representation.RebuildCount
                : 0;
        }

        public void ClearMaterialization()
        {
            Metrics = null;
            if (navMeshSurface != null)
                navMeshSurface.RemoveData();
            navMeshSurface = null;
            NavMeshVertexCount = 0;
            NavigationBuildMilliseconds = 0L;
            MutationService = null;
            Volume = null;
            SourcePlan = null;
            MesherBackend = DeformableTerrainMesherBackend.MarchingTetrahedra;
            SpawnPosition = default;
            Failure = null;
            chunks.Clear();
            DestroyOwnedObject(generatedRoot);
            generatedRoot = null;
            for (int index = ownedAssets.Count - 1; index >= 0; index--)
                DestroyOwnedObject(ownedAssets[index]);
            ownedAssets.Clear();
            materials = null;
        }

        private void OnDestroy()
        {
            ClearMaterialization();
        }

        private bool TryMutate(
            Func<IReadOnlyList<DeformableTerrainChunkId>> operation,
            out DeformableTerrainMutationResult result,
            out string error)
        {
            result = null;
            error = null;
            if (!IsReady || MutationService == null)
            {
                error = "deformable terrain spike is not ready";
                return false;
            }
            try
            {
                Stopwatch mutationWatch = Stopwatch.StartNew();
                IReadOnlyList<DeformableTerrainChunkId> affected = operation();
                mutationWatch.Stop();

                Stopwatch meshingWatch = Stopwatch.StartNew();
                var meshData = new List<DeformableTerrainChunkMeshData>(affected.Count);
                for (int index = 0; index < affected.Count; index++)
                    meshData.Add(DeformableTerrainMesher.Build(
                        Volume, affected[index], MesherBackend));
                meshingWatch.Stop();

                Stopwatch assignmentWatch = Stopwatch.StartNew();
                for (int index = 0; index < meshData.Count; index++)
                    AssignMeshData(meshData[index], true);
                assignmentWatch.Stop();

                Stopwatch colliderWatch = Stopwatch.StartNew();
                for (int index = 0; index < affected.Count; index++)
                    AssignCollider(affected[index]);
                colliderWatch.Stop();
                Physics.SyncTransforms();
                // Stage 1 keeps navigation deliberately local and synchronous. This
                // preserves the existing ActorNavigationController consumer after a
                // development deformation without claiming a production dynamic-nav system.
                RebuildLocalNavigation();

                result = new DeformableTerrainMutationResult(
                    new ReadOnlyCollection<DeformableTerrainChunkId>(
                        new List<DeformableTerrainChunkId>(affected)),
                    mutationWatch.ElapsedMilliseconds, meshingWatch.ElapsedMilliseconds,
                    assignmentWatch.ElapsedMilliseconds, colliderWatch.ElapsedMilliseconds);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private void RebuildLocalNavigation()
        {
            if (generatedRoot == null)
                throw new InvalidOperationException("volumetric terrain root is unavailable for navigation");

            if (navMeshSurface == null)
            {
                navMeshSurface = generatedRoot.AddComponent<NavMeshSurface>();
                navMeshSurface.collectObjects = CollectObjects.Children;
                navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                navMeshSurface.layerMask = 1 << GroundLayer;
                navMeshSurface.overrideTileSize = true;
                navMeshSurface.tileSize = 64;
            }
            else
            {
                navMeshSurface.RemoveData();
            }

            Stopwatch watch = Stopwatch.StartNew();
            navMeshSurface.BuildNavMesh();
            watch.Stop();
            NavigationBuildMilliseconds = watch.ElapsedMilliseconds;
            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            NavMeshVertexCount = triangulation.vertices.Length;
            if (navMeshSurface.navMeshData == null || NavMeshVertexCount < 1)
                throw new InvalidOperationException("volumetric terrain local NavMesh build produced no data");
        }

        private void CreateChunk(DeformableTerrainChunkId chunkId)
        {
            var chunkObject = new GameObject("Volumetric " + chunkId);
            chunkObject.layer = GroundLayer;
            chunkObject.transform.SetParent(generatedRoot.transform, false);
            var meshFilter = chunkObject.AddComponent<MeshFilter>();
            var renderer = chunkObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            var collider = chunkObject.AddComponent<MeshCollider>();
            collider.cookingOptions =
                MeshColliderCookingOptions.CookForFasterSimulation |
                MeshColliderCookingOptions.EnableMeshCleaning |
                MeshColliderCookingOptions.WeldColocatedVertices |
                MeshColliderCookingOptions.UseFastMidphase;
            chunks.Add(chunkId, new ChunkRepresentation
            {
                GameObject = chunkObject,
                MeshFilter = meshFilter,
                MeshCollider = collider
            });
        }

        private void AssignMeshData(DeformableTerrainChunkMeshData data, bool countRebuild)
        {
            ChunkRepresentation representation = chunks[data.ChunkId];
            if (representation.Mesh != null)
            {
                representation.MeshCollider.sharedMesh = null;
                ownedAssets.Remove(representation.Mesh);
                DestroyOwnedObject(representation.Mesh);
            }
            var mesh = new Mesh
            {
                name = "Transient " + data.ChunkId + " Mesh",
                indexFormat = data.Vertices.Count > ushort.MaxValue
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16,
                subMeshCount = data.SubMeshCount
            };
            mesh.SetVertices(new List<Vector3>(data.Vertices));
            mesh.SetNormals(new List<Vector3>(data.Normals));
            mesh.SetUVs(0, new List<Vector2>(data.UVs));
            for (int subMesh = 0; subMesh < data.SubMeshCount; subMesh++)
                mesh.SetTriangles(new List<int>(data.Triangles(subMesh)), subMesh, false);
            mesh.RecalculateBounds();
            representation.Mesh = mesh;
            representation.MeshFilter.sharedMesh = mesh;
            if (countRebuild)
                representation.RebuildCount++;
            ownedAssets.Add(mesh);
        }

        private void AssignCollider(DeformableTerrainChunkId chunkId)
        {
            ChunkRepresentation representation = chunks[chunkId];
            representation.MeshCollider.sharedMesh = null;
            if (representation.Mesh != null && representation.Mesh.vertexCount > 0 &&
                representation.Mesh.GetIndexCount(0) + representation.Mesh.GetIndexCount(1) +
                representation.Mesh.GetIndexCount(2) > 0)
                representation.MeshCollider.sharedMesh = representation.Mesh;
        }

        private Material[] CreateMaterials()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException("no supported lit shader was found for spike materials");
            Color[] colors =
            {
                new Color(0.25f, 0.33f, 0.16f, 1f),
                new Color(0.31f, 0.20f, 0.11f, 1f),
                new Color(0.34f, 0.34f, 0.32f, 1f)
            };
            string[] names = { "Topsoil", "Soil", "Rock" };
            var resolved = new Material[3];
            for (int index = 0; index < resolved.Length; index++)
            {
                Texture2D texture = CreateProceduralTexture(colors[index], names[index]);
                var material = new Material(shader) { name = "Spike " + names[index] + " Matte Material" };
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
                if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.05f);
                material.enableInstancing = true;
                ownedAssets.Add(material);
                resolved[index] = material;
            }
            return resolved;
        }

        private Texture2D CreateProceduralTexture(Color baseColor, string label)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "Transient " + label + " Technical Texture",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var colors = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int checker = ((x / 8) + (y / 8)) & 1;
                int fine = ((x * 17 + y * 31 + (x ^ y) * 7) & 15) - 7;
                float variation = (checker == 0 ? -0.035f : 0.035f) + fine / 500f;
                colors[x + y * size] = new Color(
                    Mathf.Clamp01(baseColor.r + variation),
                    Mathf.Clamp01(baseColor.g + variation),
                    Mathf.Clamp01(baseColor.b + variation), 1f);
            }
            texture.SetPixels(colors);
            texture.Apply(true, true);
            ownedAssets.Add(texture);
            return texture;
        }

        private bool Fail(string failure)
        {
            Failure = string.IsNullOrWhiteSpace(failure) ? "unknown deformable terrain spike failure" : failure;
            Debug.LogError("[DeformableTerrainSpike][FAILURE]\nFailure: " + Failure);
            return false;
        }

        private static void DestroyOwnedObject(UnityEngine.Object ownedObject)
        {
            if (ownedObject == null)
                return;
            if (Application.isPlaying)
                Destroy(ownedObject);
            else
                DestroyImmediate(ownedObject);
        }
    }
}
