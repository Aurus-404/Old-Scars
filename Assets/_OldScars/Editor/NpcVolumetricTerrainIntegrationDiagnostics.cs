using System;
using System.Collections.Generic;
using System.IO;
using OldScars.Core;
using OldScars.Core.Actors;
using OldScars.Core.ApplicationShell;
using OldScars.Core.Data;
using OldScars.Core.Persistence;
using OldScars.Core.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace OldScars.Editor
{
    [InitializeOnLoad]
    public static class NpcVolumetricTerrainIntegrationDiagnostics
    {
        private const string PendingKey = "OldScars.NpcVolumetricTerrain.Pending";
        private const string StageKey = "OldScars.NpcVolumetricTerrain.Stage";
        private const string FailureKey = "OldScars.NpcVolumetricTerrain.Failure";
        private const string RootKey = "OldScars.NpcVolumetricTerrain.Root";
        private const int InitialNpcCount = 12;
        private const int PostDeformationNpcCount = 3;
        private const int RequiredMovingNpcCount = 3;
        private const float InitialNavigationTimeout = 8f;
        private const float PostNavigationTimeout = 10f;
        private const long Seed = 8912345678901234L;

        private sealed class NpcSnapshot
        {
            public string ActorInstanceId;
            public ActorRuntimeIdentity Identity;
            public ActorLifecycleState Lifecycle;
            public float Health;
            public Vector3 SpawnPosition;
            public Vector3 OrderedPosition;
            public int AcceptedOrdersBefore;
        }

        private static readonly List<NpcSnapshot> existingNpcs = new List<NpcSnapshot>(InitialNpcCount);
        private static readonly List<NpcSnapshot> postDeformationNpcs = new List<NpcSnapshot>(PostDeformationNpcCount);
        private static readonly Dictionary<DeformableTerrainChunkId, Mesh> chunkMeshesBeforeMutation =
            new Dictionary<DeformableTerrainChunkId, Mesh>();

        private static WorldRuntimeSceneController runtime;
        private static WorldDeformableTerrainSpikeController terrain;
        private static SandboxNpcController sandbox;
        private static float stageStartedAt;
        private static int stableFrame;
        private static int navMeshRevisionBeforeMutation;

        static NpcVolumetricTerrainIntegrationDiagnostics()
        {
            EditorApplication.update -= Continue;
            EditorApplication.update += Continue;
        }

        public static void RunBatchWorldRuntime()
        {
            if (!Application.isBatchMode || EditorApplication.isCompiling)
                throw new InvalidOperationException("NPC volumetric terrain integration diagnostics require compiled Unity batchmode.");

            string root = Path.Combine(Path.GetTempPath(), "OldScars_NpcVolumetricTerrain_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            SessionState.SetString(RootKey, root);
            SessionState.SetBool(PendingKey, true);
            SessionState.SetInt(StageKey, 0);
            SessionState.EraseString(FailureKey);
            WorldRuntimeTerrainDevelopmentSettings.SetDiagnosticSelectionOverride(
                WorldRuntimeTerrainDevelopmentSelection.VolumetricIndexedMarchingCubes);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("GameDataManager").AddComponent<GameDataManager>();
            EditorApplication.EnterPlaymode();
        }

        private static void Continue()
        {
            if (!SessionState.GetBool(PendingKey, false))
                return;
            try
            {
                WorldRuntimeTerrainDevelopmentSettings.SetDiagnosticSelectionOverride(
                    WorldRuntimeTerrainDevelopmentSelection.VolumetricIndexedMarchingCubes);
                if (EditorApplication.isPlaying)
                {
                    RunPlayStage();
                    return;
                }
                if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                    SessionState.GetInt(StageKey, 0) == 99)
                    Finish(0);
                else if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    Finish(1, "Play Mode integration diagnostic was interrupted before completion.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetString(FailureKey, exception.Message);
                SessionState.SetInt(StageKey, 99);
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorApplication.ExitPlaymode();
                else
                    Finish(1, exception.Message);
            }
        }

        private static void RunPlayStage()
        {
            int stage = SessionState.GetInt(StageKey, 0);
            if (stage == 0)
            {
                if (Time.frameCount < 5 || GameDataManager.Instance?.IsReady != true)
                    return;
                CreateWorldSessionAndLoadRuntime();
                SessionState.SetInt(StageKey, 1);
                stageStartedAt = Time.time;
                return;
            }

            runtime = UnityEngine.Object.FindAnyObjectByType<WorldRuntimeSceneController>();
            if (runtime == null || !runtime.GameplayStateReady || runtime.GameplayRuntimeComposition == null)
            {
                if (Time.time - stageStartedAt > 30f)
                    throw new InvalidOperationException("real WorldRuntime did not reach gameplay readiness for volumetric NPC integration.");
                return;
            }

            if (stage == 1)
            {
                SetupInitialRuntimeEvidence();
                SessionState.SetInt(StageKey, 2);
                stageStartedAt = Time.time;
                return;
            }

            if (stage == 2)
            {
                if (Time.time - stageStartedAt < InitialNavigationTimeout)
                    return;
                Require(CountMovedOrMoving(existingNpcs, true) >= RequiredMovingNpcCount,
                    "Initial sandbox NPCs were spawned but did not navigate over the volumetric NavMesh.");
                CaptureAndApplyDeformation();
                stableFrame = Time.frameCount + 4;
                SessionState.SetInt(StageKey, 3);
                return;
            }

            if (stage == 3)
            {
                if (Time.frameCount < stableFrame)
                    return;
                VerifyExistingActorsAfterRebuild();
                Require(IssueNavigationOrders(existingNpcs) >= RequiredMovingNpcCount,
                    "Existing NPCs did not accept enough navigation orders after volumetric NavMesh rebuild.");
                SessionState.SetInt(StageKey, 4);
                stageStartedAt = Time.time;
                return;
            }

            if (stage == 4)
            {
                if (Time.time - stageStartedAt < PostNavigationTimeout)
                    return;
                Require(CountMovedOrMoving(existingNpcs, false) >= RequiredMovingNpcCount,
                    "Existing NPCs accepted post-deformation orders but did not move on the rebuilt NavMesh.");
                SpawnPostDeformationNpcs();
                Require(IssueNavigationOrders(postDeformationNpcs) >= 2,
                    "New sandbox NPCs did not accept navigation on the rebuilt volumetric NavMesh.");
                SessionState.SetInt(StageKey, 5);
                stageStartedAt = Time.time;
                return;
            }

            if (stage == 5 && Time.time - stageStartedAt >= PostNavigationTimeout)
            {
                Require(CountMovedOrMoving(postDeformationNpcs, false) >= 2,
                    "New sandbox NPCs did not move on the rebuilt volumetric NavMesh.");
                Debug.Log(
                    "NPC + Volumetric Terrain Integration Diagnostics: PASS\n" +
                    "- Runtime: real WorldSession -> MacroGeography -> VolumetricIndexedMarchingCubes\n" +
                    "- ExistingNPCs: " + existingNpcs.Count + " retained identity/state and navigated before/after rebuild\n" +
                    "- NewNPCs: " + postDeformationNpcs.Count + " spawned and navigated after rebuild\n" +
                    "- NavMeshRebuilds: " + terrain.NavigationRebuildCount + "\n" +
                    "- NavMeshVertices: " + terrain.NavMeshVertexCount + "\n" +
                    "- No Unity Terrain or second runtime terrain/navigation authority was created");
                SessionState.SetInt(StageKey, 99);
                EditorApplication.ExitPlaymode();
            }
        }

        private static void CreateWorldSessionAndLoadRuntime()
        {
            WorldSessionService.Close();
            var store = new PersistenceFileStore(SessionState.GetString(RootKey, string.Empty));
            WorldSessionOperationResult created = WorldSessionService.Create(
                "NPC Volumetric Terrain Integration", new WorldSeed(Seed),
                WorldGenerationSettings.ResolvePreset(WorldSizePreset.Small),
                LandCoveragePreset.High, GameDataManager.Instance.LoadedContentSet, store);
            Require(created.Success, "Could not create procedural WorldSession: " + created.Failure);
            SceneManager.LoadScene(WorldApplicationScenes.WorldRuntimeSceneName, LoadSceneMode.Single);
        }

        private static void SetupInitialRuntimeEvidence()
        {
            terrain = runtime.VolumetricTerrainController;
            Require(terrain != null && terrain.IsReady &&
                    terrain.MesherBackend == DeformableTerrainMesherBackend.IndexedMarchingCubes,
                "WorldRuntime did not publish ready indexed marching-cubes terrain.");
            Require(runtime.TerrainSelection == WorldRuntimeTerrainDevelopmentSelection.VolumetricIndexedMarchingCubes &&
                    (runtime.MaterializationController == null || !runtime.MaterializationController.IsReady),
                "WorldRuntime created a second heightmap terrain authority alongside the volumetric terrain.");
            Require(UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include).Length == 0,
                "A Unity Terrain physical representation exists alongside the volumetric terrain.");
            NavMeshSurface[] navSurfaces = UnityEngine.Object.FindObjectsByType<NavMeshSurface>(
                FindObjectsInactive.Include);
            Require(navSurfaces.Length == 1 && ReferenceEquals(navSurfaces[0], terrain.NavMeshSurface),
                "A second navigation authority exists alongside the volumetric terrain NavMeshSurface.");

            WorldSession session = WorldSessionService.ActiveSession;
            Require(session != null && session.HasMacroGeography &&
                    terrain.SourcePlan.GeographyHash == session.MacroGeography.CanonicalHash,
                "Volumetric terrain was not derived from the active WorldSession MacroGeography.");
            Require(terrain.NavMeshSurface != null && terrain.NavMeshSurface.navMeshData != null &&
                    terrain.NavMeshVertexCount > 0 && terrain.NavigationRebuildCount == 1,
                "Initial volumetric NavMesh is not valid.");

            sandbox = runtime.GameplayRuntimeComposition.SandboxNpcController;
            Require(sandbox != null, "WorldRuntime sandbox NPC controller is unavailable.");
            Require(sandbox.TrySetBaseSeed("84521", out string seedError),
                "Could not configure the sandbox NPC seed: " + seedError);
            existingNpcs.Clear();
            for (int index = 0; index < InitialNpcCount; index++)
            {
                Require(sandbox.TrySpawnRandomNpc(out SandboxNpcMetadata metadata, out string spawnError),
                    "Initial sandbox NPC spawn " + index + " failed: " + spawnError);
                existingNpcs.Add(Snapshot(metadata.GetComponent<ActorRuntimeIdentity>()));
            }
            Require(existingNpcs.Count == InitialNpcCount, "Initial sandbox NPC count is incorrect.");
            Debug.Log("[NPC_VOLUMETRIC][INITIAL] Actors=" + existingNpcs.Count +
                      " NavMeshVertices=" + terrain.NavMeshVertexCount +
                      " GeographyHash=" + terrain.SourcePlan.GeographyHash);
        }

        private static void CaptureAndApplyDeformation()
        {
            chunkMeshesBeforeMutation.Clear();
            foreach (DeformableTerrainChunkId chunkId in terrain.Volume.EnumerateChunks())
            {
                if (TryGetChunkRepresentation(chunkId, out Mesh mesh, out MeshCollider collider) &&
                    mesh != null && collider.sharedMesh == mesh)
                    chunkMeshesBeforeMutation.Add(chunkId, mesh);
            }

            for (int index = 0; index < existingNpcs.Count; index++)
            {
                NpcSnapshot snapshot = existingNpcs[index];
                ActorRuntimeIdentity identity = ResolveSnapshot(snapshot);
                snapshot.Lifecycle = identity.LifecycleState;
                snapshot.Health = identity.GetComponent<ActorHealthComponent>().CurrentHealth;
                snapshot.SpawnPosition = identity.transform.position;
                identity.GetComponent<ActorNavigationController>().Stop();
            }

            navMeshRevisionBeforeMutation = terrain.NavigationRebuildCount;
            Require(terrain.TryFindSurfacePoint(new Vector3(-20f, 0f, 12f), out Vector3 deformationSurface),
                "Controlled deformation surface was unavailable on volumetric terrain.");
            Require(terrain.TrySubtractSphere(
                        deformationSurface - Vector3.up * 1.5f, 3.5f,
                        out DeformableTerrainMutationResult mutation, out string deformationError),
                "Localized volumetric deformation failed: " + deformationError);
            Require(mutation.AffectedChunks.Count > 0 &&
                    terrain.NavigationRebuildCount == navMeshRevisionBeforeMutation + 1 &&
                    terrain.NavMeshSurface != null && terrain.NavMeshSurface.navMeshData != null &&
                    terrain.NavMeshVertexCount > 0,
                "Localized deformation did not rebuild the volumetric NavMesh.");

            int observableMeshColliderRebuilds = 0;
            for (int index = 0; index < mutation.AffectedChunks.Count; index++)
            {
                DeformableTerrainChunkId chunkId = mutation.AffectedChunks[index];
                bool hasRepresentation = TryGetChunkRepresentation(
                    chunkId, out Mesh mesh, out MeshCollider collider);
                Require(terrain.GetChunkRebuildCount(chunkId) > 0 && hasRepresentation,
                    "Localized deformation did not rebuild the affected chunk representation: " + chunkId);
                if (!HasColliderGeometry(mesh))
                {
                    Require(collider.sharedMesh == null,
                        "An affected terrain chunk without collision geometry retained a stale MeshCollider: " + chunkId);
                    continue;
                }

                Require(collider.sharedMesh == mesh,
                    "Localized deformation did not refresh the affected MeshCollider: " + chunkId);
                if (chunkMeshesBeforeMutation.TryGetValue(chunkId, out Mesh previousMesh))
                    Require(!ReferenceEquals(mesh, previousMesh),
                        "Localized deformation did not replace the affected chunk mesh: " + chunkId);
                observableMeshColliderRebuilds++;
            }
            Require(observableMeshColliderRebuilds > 0,
                "Localized deformation rebuilt no observable mesh/collider surface chunks.");
            Debug.Log("[NPC_VOLUMETRIC][DEFORMATION] AffectedChunks=" + mutation.AffectedChunks.Count +
                      " NavMeshRebuild=" + terrain.NavigationRebuildCount +
                      " NavMeshVertices=" + terrain.NavMeshVertexCount);
        }

        private static void VerifyExistingActorsAfterRebuild()
        {
            for (int index = 0; index < existingNpcs.Count; index++)
            {
                NpcSnapshot snapshot = existingNpcs[index];
                ActorRuntimeIdentity identity = ResolveSnapshot(snapshot);
                ActorHealthComponent health = identity.GetComponent<ActorHealthComponent>();
                ActorNavigationController navigation = identity.GetComponent<ActorNavigationController>();
                Require(identity.LifecycleState == snapshot.Lifecycle &&
                        Mathf.Approximately(health.CurrentHealth, snapshot.Health) &&
                        navigation != null && navigation.IsConfigured && navigation.Agent.isOnNavMesh,
                    "Existing NPC identity/state/navigation was not preserved after terrain rebuild: " + snapshot.ActorInstanceId);
            }
        }

        private static void SpawnPostDeformationNpcs()
        {
            postDeformationNpcs.Clear();
            for (int index = 0; index < PostDeformationNpcCount; index++)
            {
                Require(sandbox.TrySpawnRandomNpc(out SandboxNpcMetadata metadata, out string spawnError),
                    "Post-deformation sandbox NPC spawn " + index + " failed: " + spawnError);
                NpcSnapshot snapshot = Snapshot(metadata.GetComponent<ActorRuntimeIdentity>());
                Require(snapshot.Identity.GetComponent<ActorNavigationController>().Agent.isOnNavMesh,
                    "New sandbox NPC did not bind to the rebuilt NavMesh: " + snapshot.ActorInstanceId);
                postDeformationNpcs.Add(snapshot);
            }
        }

        private static int IssueNavigationOrders(List<NpcSnapshot> snapshots)
        {
            int accepted = 0;
            for (int index = 0; index < snapshots.Count; index++)
            {
                NpcSnapshot snapshot = snapshots[index];
                ActorRuntimeIdentity identity = ResolveSnapshot(snapshot);
                ActorNavigationController navigation = identity.GetComponent<ActorNavigationController>();
                navigation.Stop();
                snapshot.OrderedPosition = identity.transform.position;
                if (TryIssueNearbyOrder(navigation))
                    accepted++;
            }
            return accepted;
        }

        private static bool TryIssueNearbyOrder(ActorNavigationController navigation)
        {
            Vector3 origin = navigation.transform.position;
            Vector3[] offsets =
            {
                new Vector3(6f, 0f, 0f), new Vector3(-6f, 0f, 0f),
                new Vector3(0f, 0f, 6f), new Vector3(0f, 0f, -6f),
                new Vector3(4f, 0f, 4f), new Vector3(-4f, 0f, -4f)
            };
            for (int index = 0; index < offsets.Length; index++)
            {
                if (!NavMesh.SamplePosition(origin + offsets[index], out NavMeshHit hit, 4f, navigation.Agent.areaMask))
                    continue;
                if (navigation.TryNavigate(hit.position, out _))
                    return true;
            }
            return false;
        }

        private static int CountMovedOrMoving(List<NpcSnapshot> snapshots, bool fromSpawn)
        {
            int count = 0;
            for (int index = 0; index < snapshots.Count; index++)
            {
                NpcSnapshot snapshot = snapshots[index];
                ActorRuntimeIdentity identity = ResolveSnapshot(snapshot);
                ActorNavigationController navigation = identity.GetComponent<ActorNavigationController>();
                SandboxActorRoamingController roaming = identity.GetComponent<SandboxActorRoamingController>();
                Vector3 origin = fromSpawn ? snapshot.SpawnPosition : snapshot.OrderedPosition;
                bool moved = Vector3.Distance(origin, identity.transform.position) > 0.2f;
                bool accepted = fromSpawn
                    ? roaming != null && roaming.AcceptedOrderCount > snapshot.AcceptedOrdersBefore
                    : navigation.State == ActorNavigationState.Moving || navigation.State == ActorNavigationState.Reached;
                if (accepted && (moved || navigation.State == ActorNavigationState.Moving))
                    count++;
            }
            return count;
        }

        private static NpcSnapshot Snapshot(ActorRuntimeIdentity identity)
        {
            Require(identity != null && identity.IsRegistered &&
                    identity.GetComponent<ActorNavigationController>() != null &&
                    identity.GetComponent<SandboxActorRoamingController>() != null,
                "Sandbox spawn did not use the existing actor/navigation/roaming authorities.");
            return new NpcSnapshot
            {
                ActorInstanceId = identity.ActorInstanceId,
                Identity = identity,
                Lifecycle = identity.LifecycleState,
                Health = identity.GetComponent<ActorHealthComponent>().CurrentHealth,
                SpawnPosition = identity.transform.position,
                AcceptedOrdersBefore = identity.GetComponent<SandboxActorRoamingController>().AcceptedOrderCount
            };
        }

        private static ActorRuntimeIdentity ResolveSnapshot(NpcSnapshot snapshot)
        {
            Require(ActorRuntimeRegistry.TryGet(snapshot.ActorInstanceId, out ActorRuntimeIdentity identity) &&
                    ReferenceEquals(identity, snapshot.Identity),
                "Existing sandbox NPC was recreated or lost after terrain mutation: " + snapshot.ActorInstanceId);
            return identity;
        }

        private static bool TryGetChunkRepresentation(
            DeformableTerrainChunkId chunkId,
            out Mesh mesh,
            out MeshCollider collider)
        {
            mesh = null;
            collider = null;
            Transform chunk = terrain.GeneratedRoot.transform.Find("Volumetric " + chunkId);
            if (chunk == null)
                return false;
            mesh = chunk.GetComponent<MeshFilter>()?.sharedMesh;
            collider = chunk.GetComponent<MeshCollider>();
            return collider != null;
        }

        private static bool HasColliderGeometry(Mesh mesh)
        {
            if (mesh == null || mesh.vertexCount == 0)
                return false;
            for (int index = 0; index < mesh.subMeshCount; index++)
                if (mesh.GetIndexCount(index) > 0)
                    return true;
            return false;
        }

        private static void Finish(int exitCode, string immediateFailure = null)
        {
            string failure = string.IsNullOrWhiteSpace(immediateFailure)
                ? SessionState.GetString(FailureKey, string.Empty)
                : immediateFailure;
            SessionState.SetBool(PendingKey, false);
            SessionState.SetInt(StageKey, 0);
            SessionState.EraseString(FailureKey);
            WorldRuntimeTerrainDevelopmentSettings.ClearDiagnosticSelectionOverride();
            WorldSessionService.Close();
            string root = SessionState.GetString(RootKey, string.Empty);
            SessionState.EraseString(RootKey);
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
                Directory.Delete(root, true);
            runtime = null;
            terrain = null;
            sandbox = null;
            existingNpcs.Clear();
            postDeformationNpcs.Clear();
            chunkMeshesBeforeMutation.Clear();
            if (!string.IsNullOrWhiteSpace(failure))
                Debug.LogError("NPC + Volumetric Terrain Integration Diagnostics: FAIL\n" + failure);
            EditorApplication.Exit(string.IsNullOrWhiteSpace(failure) && exitCode == 0 ? 0 : 1);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
