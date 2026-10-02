using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OldScars.Core;
using OldScars.Core.ApplicationShell;
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
    /// <summary>One combined identity/seam gate plus the real WorldRuntime consumer, in one Play session.</summary>
    [InitializeOnLoad]
    public static class TerrainChunkIdentityDiagnostics
    {
        private const string Prefix = "OldScars.IMPL0063.Stage1.";
        private const string FixtureWorld = "world_00630000000000000000000000000001";
        private static WorldRuntimeSceneController runtime;
        private static Vector3 walkStart;
        private static double deadline;

        static TerrainChunkIdentityDiagnostics() => EditorApplication.update += Continue;

        [MenuItem("Old Scars/Diagnostics/Terrain/Run Chunk Identity Stage 1")]
        public static void Run()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling &&
                !WorldSessionService.HasActiveSession, "Requires idle compiled Edit Mode without an active WorldSession.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                Require(!SceneManager.GetSceneAt(i).isDirty, "Unsaved scenes must be preserved; gate will not save them.");
            SessionState.SetString(Prefix + "SceneSetup", JsonConvert.SerializeObject(EditorSceneManager.GetSceneManagerSetup()));
            SessionState.SetString(Prefix + "Root", Path.Combine(Path.GetTempPath(), "OldScars_IMPL0063_" + Guid.NewGuid().ToString("N")));
            SessionState.SetString(Prefix + "Evidence", "");
            SessionState.SetString(Prefix + "Result", "PENDING");
            SessionState.SetFloat(Prefix + "BootDeadline", (float)(EditorApplication.timeSinceStartup + 45));
            SessionState.SetString(Prefix + "Phase", "boot");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Stage 1 content bootstrap").AddComponent<GameDataManager>();
            EditorApplication.EnterPlaymode();
        }

        private static void Continue()
        {
            string phase = SessionState.GetString(Prefix + "Phase", "");
            if (phase.Length == 0) return;
            try
            {
                if (phase == "finish" && !EditorApplication.isPlayingOrWillChangePlaymode) { Finish(); return; }
                if (!EditorApplication.isPlaying)
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                    SessionState.SetString(Prefix + "Result", "INCONCLUSIVE: Play Mode interrupted before completion.");
                    Exit();
                    Finish();
                    return;
                }
                if (phase == "boot")
                {
                    Require(GameDataManager.Instance?.Report?.HasErrors != true, "Content bootstrap reported validation errors.");
                    Require(EditorApplication.timeSinceStartup < SessionState.GetFloat(Prefix + "BootDeadline", 0),
                        "Content bootstrap readiness timeout.");
                    if (Time.frameCount < 5 || GameDataManager.Instance?.IsReady != true) return;
                    Require(WorldSessionBootstrap.TryBuildNew("Terrain identity fixture", new WorldSeed(8675309123456789L),
                        WorldGenerationSettings.ResolvePreset(WorldSizePreset.Small), LandCoveragePreset.High,
                        GameDataManager.Instance.LoadedContentSet, out WorldSession generated, out string error), error);
                    // Fix save lineage while preserving all generated committed fields, using the real semantic preflight.
                    var payload = (JObject)WorldSessionPersistenceService.ToPayload(generated);
                    payload["worldId"] = FixtureWorld;
                    WorldSessionPersistenceResult fixedSession = WorldSessionPersistenceService.FromPayload(payload);
                    Require(fixedSession.Success, fixedSession.Failure);
                    var store = new PersistenceFileStore(SessionState.GetString(Prefix + "Root", ""));
                    Require(WorldSessionPersistenceService.Save(fixedSession.Session, store).Success, "Fixture save failed.");
                    WorldSessionOperationResult loaded = WorldSessionService.Load(FixtureWorld, store);
                    Require(loaded.Success, loaded.Failure);
                    WorldRuntimeTerrainDevelopmentSettings.SetDiagnosticSelectionOverride(
                        WorldRuntimeTerrainDevelopmentSelection.VolumetricIndexedMarchingCubes);
                    SessionState.SetString(Prefix + "Phase", "runtime");
                    deadline = EditorApplication.timeSinceStartup + 45;
                    SceneManager.LoadScene(WorldApplicationScenes.WorldRuntimeSceneName, LoadSceneMode.Single);
                }
                else if (phase == "runtime")
                {
                    runtime = UnityEngine.Object.FindAnyObjectByType<WorldRuntimeSceneController>();
                    if (runtime?.GameplayStateReady != true)
                    {
                        Require(EditorApplication.timeSinceStartup < deadline, "WorldRuntime readiness timeout.");
                        return;
                    }
                    ValidateCombined(runtime);
                    var terrain = runtime.VolumetricTerrainController;
                    Require(terrain.TryFindSurfacePoint(new Vector3(-10, 0, 12), out Vector3 start), "Traversal start missing.");
                    runtime.PlayerComposition.MovementInput.enabled = false;
                    runtime.PlayerComposition.PlacePlayerAtSurface(start, Quaternion.LookRotation(Vector3.right));
                    walkStart = runtime.PlayerComposition.PlayerTransform.position;
                    runtime.PlayerComposition.MovementController.SetMovementDirection(Vector3.right);
                    deadline = EditorApplication.timeSinceStartup + 10;
                    SessionState.SetString(Prefix + "Phase", "walk");
                }
                else if (phase == "walk")
                {
                    Vector3 position = runtime.PlayerComposition.PlayerTransform.position;
                    if (position.x - walkStart.x < 4f)
                    {
                        Require(EditorApplication.timeSinceStartup < deadline, "Real Player traversal did not progress.");
                        return;
                    }
                    Require(Physics.Raycast(position + Vector3.up, Vector3.down, out RaycastHit hit, 4f,
                        1 << 3, QueryTriggerInteraction.Ignore) &&
                        hit.transform.IsChildOf(runtime.VolumetricTerrainController.GeneratedRoot.transform),
                        "Player traversal lacks collider-backed ground.");
                    runtime.PlayerComposition.MovementController.ClearMovement();
                    Evidence("Runtime Player traversed >=4 Unity units on existing volumetric collider; shared movement authority.");
                    SessionState.SetString(Prefix + "Result", "PASS");
                    Exit();
                }
            }
            catch (Exception exception)
            {
                SessionState.SetString(Prefix + "Result", "FAIL: " + exception);
                Debug.LogException(exception);
                Exit();
            }
        }

        private static void ValidateCombined(WorldRuntimeSceneController worldRuntime)
        {
            WorldSession session = WorldSessionService.ActiveSession;
            var terrain = worldRuntime.VolumetricTerrainController;
            Require(terrain?.IsReady == true && worldRuntime.PlayerComposition != null &&
                worldRuntime.MaterializationController == null &&
                worldRuntime.TerrainSelection == WorldRuntimeTerrainDevelopmentSelection.VolumetricIndexedMarchingCubes,
                "Existing WorldRuntime integration / single terrain authority failed.");
            Require(terrain.SourcePlan.WorldId == session.WorldId &&
                terrain.SourcePlan.GeographyHash == session.MacroGeography.CanonicalHash &&
                session.GenerationContext.GeneratorVersion.Canonical == WorldSessionBootstrap.CurrentGeneratorVersion,
                "Runtime projection does not consume committed world truth.");
            Require(UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Exclude).Length == 1 &&
                terrain.NavMeshSurface.navMeshData != null, "Expected one existing local NavMesh contribution.");
            var plan = terrain.SourcePlan;
            var volume = terrain.Volume;
            var ids = volume.EnumerateChunks().ToArray();
            var original = Mapping(volume, ids);
            Require(ids.Length == 8 && original.Count == ids.Length && original.Keys.All(k => k.StartsWith("terrain_chunk_")),
                "Current 2x2x2 fixture identities missing or colliding.");
            Require(terrain.GeneratedRoot.GetComponentsInChildren<MeshCollider>().Length == ids.Length,
                "Runtime did not create multiple technical chunk colliders.");
            Evidence("Phase 0: committed schema-7 fixture -> active-region plan -> eight chunks -> Indexed MC -> collider/local NavMesh -> real Player/runtime ready.");
            Evidence("Layout SHA-256: " + volume.BaselineLayoutEvidence);
            foreach (var pair in original) Evidence(pair.Key + " = " + pair.Value);

            WorldSessionPersistenceResult duplicateSession = WorldSessionPersistenceService.FromPayload(
                WorldSessionPersistenceService.ToPayload(session));
            Require(duplicateSession.Success, duplicateSession.Failure);
            Require(TerrainMaterializationPlanner.TryBuildActiveRegion(duplicateSession.Session, plan.Configuration,
                out TerrainMaterializationPlan duplicatePlan, out string error), error);
            Require(DeformableTerrainVolume.TryCreate(duplicatePlan, volume.Configuration, out var duplicate, out error), error);
            Same(original, Mapping(duplicate, ids.Reverse()), "Committed reconstruction / reverse order");
            var evensThenOdds = ids.Where((_, i) => i % 2 == 0).Concat(ids.Where((_, i) => i % 2 != 0));
            Same(original, Mapping(volume, evensThenOdds), "Different resolution order");
            ValidateSeams(volume, duplicate);

            var foreignPayload = (JObject)WorldSessionPersistenceService.ToPayload(session);
            foreignPayload["worldId"] = "world_00630000000000000000000000000002";
            WorldSessionPersistenceResult foreign = WorldSessionPersistenceService.FromPayload(foreignPayload);
            Require(foreign.Success, foreign.Failure);
            Require(TerrainMaterializationPlanner.TryBuildActiveRegion(foreign.Session, plan.Configuration,
                out var foreignPlan, out error), error);
            Require(DeformableTerrainVolume.TryCreate(foreignPlan, volume.Configuration, out var foreignVolume, out error), error);
            foreach (var id in ids)
            {
                Require(volume.GetChunkKey(id) != foreignVolume.GetChunkKey(id), "Distinct WorldIds alias.");
                Require(volume.ComputeChunkBaselineEvidence(id) == foreignVolume.ComputeChunkBaselineEvidence(id),
                    "WorldId incorrectly changed procedural baseline data.");
            }
            ValidateCompatibility(plan, volume);

            foreach (var backend in (DeformableTerrainMesherBackend[])Enum.GetValues(typeof(DeformableTerrainMesherBackend)))
            {
                int triangles = 0;
                foreach (var id in ids.Reverse())
                {
                    var mesh = DeformableTerrainMesher.Build(volume, id, backend);
                    Require(mesh.ChunkId == id, "Mesher changed local address.");
                    triangles += mesh.TriangleCount;
                }
                Require(triangles > 0, "Backend did not consume the baseline.");
                Same(original, Mapping(volume, ids), backend + " identity / baseline");
            }

            Vector3 position = terrain.GeneratedRoot.transform.position;
            terrain.GeneratedRoot.name = "Representation name is not an address";
            terrain.GeneratedRoot.transform.position = new Vector3(1000, 2000, -3000);
            foreach (Transform child in terrain.GeneratedRoot.transform) child.SetAsFirstSibling();
            Same(original, Mapping(volume, ids), "Transform / name / sibling order");
            terrain.GeneratedRoot.transform.position = position;
            // Recreate the real existing representation with both backends, without a second controller/NavMesh authority.
            foreach (var backend in new[] { DeformableTerrainMesherBackend.MarchingTetrahedra,
                DeformableTerrainMesherBackend.IndexedMarchingCubes })
            {
                terrain.GeneratedRoot.SetActive(false);
                Require(terrain.TryMaterializePlan(plan, volume.Configuration, backend), terrain.Failure);
                Same(original, Mapping(terrain.Volume, ids.Reverse()), "GameObject recreation / " + backend);
            }
            volume = terrain.Volume;
            string mutable = volume.ComputeDensityEvidence();
            float surface = plan.HeightNormalizedAtLocal(0, -12) * plan.Configuration.VerticalRelief;
            MutateAndCheck(terrain, original, ids, () =>
            {
                Require(terrain.TrySubtractSphere(new Vector3(0, surface - 1.5f, -12), 6.5f,
                    out var result, out string failure), failure);
                return result;
            });
            MutateAndCheck(terrain, original, ids, () =>
            {
                Require(terrain.TrySubtractCapsule(new Vector3(0, surface - 8, -12), new Vector3(28, surface - 8, -12),
                    3.75f, out var result, out string failure), failure);
                return result;
            });
            Require(volume.ComputeDensityEvidence() != mutable, "Runtime mutations did not change current state.");
            Require(terrain.TryReset(out _, out error) && volume.ComputeDensityEvidence() == mutable, "Baseline reset failed: " + error);
            Same(original, Mapping(volume, ids), "Reset preserves baseline identity");
            Require(UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Exclude).Length == 1 &&
                terrain.NavMeshVertexCount > 0, "Terrain rebuilt a parallel navigation authority.");
            Require(terrain.TryFindSurfacePoint(new Vector3(-10, 0, 12), out var start), "NavMesh start lacks physical surface.");
            Require(terrain.TryFindSurfacePoint(new Vector3(10, 0, 12), out var end), "NavMesh end lacks physical surface.");
            var path = new NavMeshPath();
            Require(NavMesh.SamplePosition(start, out var a, 4, NavMesh.AllAreas) &&
                NavMesh.SamplePosition(end, out var b, 4, NavMesh.AllAreas) &&
                NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
                "Existing local NavMesh failed the post-reset path.");
            Require(WorldSessionPersistenceService.ToPayload(session).ToString(Formatting.None) ==
                WorldSessionPersistenceService.ToPayload(duplicateSession.Session).ToString(Formatting.None),
                "Runtime identity/meshing/mutation changed committed truth.");
            Evidence("A-H PASS: reconstruction/order, world separation, layout safety, both meshers, representation/Transform independence, sphere/capsule baseline independence and selective rebuild. Upstream truth unchanged; single local NavMesh complete path.");
        }

        private static void ValidateCompatibility(TerrainMaterializationPlan plan, DeformableTerrainVolume original)
        {
            var c = original.Configuration;
            var changed = new DeformableTerrainSpikeConfiguration(c.ChunkCountX, c.ChunkCountZ, c.CellsPerChunkX,
                c.CellsPerChunkZ, c.VerticalCells, c.HorizontalCellSize, c.UndergroundDepth, c.AirHeadroom,
                c.SurfaceLayerDepth + 0.25f, c.SoilLayerDepth);
            Require(DeformableTerrainVolume.TryCreate(plan, changed, out var different, out string error), error);
            Require(original.BaselineLayoutEvidence != different.BaselineLayoutEvidence &&
                original.GetChunkKey(default) != different.GetChunkKey(default), "Material-layout change aliases old identity.");
            var p = plan.Configuration;
            var renderingOnly = new TerrainMaterializationConfiguration(p.PhysicalWidth, p.PhysicalLength, p.VerticalRelief,
                p.LogicalWidth, p.LogicalLength, p.HeightmapResolution, p.WaterMaskResolution, p.AlphamapResolution,
                p.NavMeshTileSize, p.PrimaryRoadWidth + 1, p.MaximumSpawnSlopeDegrees);
            Require(TerrainMaterializationPlanner.TryBuildActiveRegion(WorldSessionService.ActiveSession, renderingOnly,
                out var visualPlan, out error), error);
            Require(DeformableTerrainVolume.TryCreate(visualPlan, c, out var visualVolume, out error), error);
            Require(original.BaselineLayoutEvidence == visualVolume.BaselineLayoutEvidence,
                "Rendering-only road tuning redefined terrain compatibility.");
            Require(!default(TerrainChunkKey).IsValid, "Default key must be invalid.");
            bool rejected = false;
            try { original.GetChunkKey(new DeformableTerrainChunkId(-1, 0, 0)); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Require(rejected, "Invalid chunk address was accepted.");
        }

        private static void MutateAndCheck(WorldDeformableTerrainSpikeController terrain, Dictionary<string, string> expected,
            DeformableTerrainChunkId[] ids, Func<DeformableTerrainMutationResult> apply)
        {
            string before = terrain.Volume.ComputeDensityEvidence();
            int navBefore = terrain.NavigationRebuildCount;
            var counts = ids.ToDictionary(id => id, terrain.GetChunkRebuildCount);
            var result = apply();
            Require(result.AffectedChunks.Count > 0 && result.AffectedChunks.Count < ids.Length,
                "Mutation must rebuild a strict subset of the current fixture.");
            foreach (var id in ids)
                Require(terrain.GetChunkRebuildCount(id) == counts[id] + (result.AffectedChunks.Contains(id) ? 1 : 0),
                    "Rebuild count mismatch for " + id);
            Require(terrain.Volume.ComputeDensityEvidence() != before && terrain.NavigationRebuildCount == navBefore + 1,
                "Mutation did not change current terrain / existing navigation contribution.");
            Same(expected, Mapping(terrain.Volume, ids.Reverse()), "Mutation preserves baseline mapping");
        }

        private static void ValidateSeams(DeformableTerrainVolume first, DeformableTerrainVolume reconstructed)
        {
            var c = first.Configuration;
            int[] sizes = { c.CellsPerChunkX, c.CellsPerChunkY, c.CellsPerChunkZ };
            int[] counts = { c.ChunkCountX, c.ChunkCountY, c.ChunkCountZ };
            for (int axis = 0; axis < 3; axis++)
            {
                int compared = 0;
                foreach (var id in first.EnumerateChunks())
                {
                    int[] address = { id.X, id.Y, id.Z };
                    if (address[axis] + 1 >= counts[axis]) continue;
                    address[axis]++;
                    var neighbor = new DeformableTerrainChunkId(address[0], address[1], address[2]);
                    for (int v = 0; v <= sizes[(axis + 2) % 3]; v++)
                    for (int u = 0; u <= sizes[(axis + 1) % 3]; u++)
                    {
                        int[] left = new int[3];
                        left[axis] = sizes[axis]; left[(axis + 1) % 3] = u; left[(axis + 2) % 3] = v;
                        int[] right = (int[])left.Clone(); right[axis] = 0;
                        float density = first.BaselineDensityAtChunkSample(id, left[0], left[1], left[2]);
                        var material = first.BaselineMaterialAtChunkSample(id, left[0], left[1], left[2]);
                        Require(density == first.BaselineDensityAtChunkSample(neighbor, right[0], right[1], right[2]) &&
                            density == reconstructed.BaselineDensityAtChunkSample(neighbor, right[0], right[1], right[2]) &&
                            material == first.BaselineMaterialAtChunkSample(neighbor, right[0], right[1], right[2]) &&
                            material == reconstructed.BaselineMaterialAtChunkSample(neighbor, right[0], right[1], right[2]),
                            "Exact shared baseline density/material seam mismatch, axis " + axis);
                        compared++;
                    }
                }
                Require(compared > 0, "Missing boundary coverage.");
                Evidence("XYZ"[axis] + " seam: " + compared + " shared baseline density/material samples EXACT PASS.");
            }
        }

        private static Dictionary<string, string> Mapping(DeformableTerrainVolume volume, IEnumerable<DeformableTerrainChunkId> order)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var id in order) result.Add(volume.GetChunkKey(id).Canonical, volume.ComputeChunkBaselineEvidence(id));
            return result;
        }
        private static void Same(Dictionary<string, string> expected, Dictionary<string, string> actual, string label) =>
            Require(expected.Count == actual.Count && expected.All(p => actual.TryGetValue(p.Key, out string value) && value == p.Value), label);
        private static void Evidence(string text) => SessionState.SetString(Prefix + "Evidence",
            SessionState.GetString(Prefix + "Evidence", "") + text + "\n");
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Exit()
        {
            WorldSessionService.Close();
            WorldRuntimeTerrainDevelopmentSettings.ClearDiagnosticSelectionOverride();
            SessionState.SetString(Prefix + "Phase", "finish");
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
        }
        private static void Finish()
        {
            WorldRuntimeTerrainDevelopmentSettings.ClearDiagnosticSelectionOverride();
            var setup = JsonConvert.DeserializeObject<SceneSetup[]>(SessionState.GetString(Prefix + "SceneSetup", ""));
            EditorSceneManager.RestoreSceneManagerSetup(setup);
            string root = Path.GetFullPath(SessionState.GetString(Prefix + "Root", ""));
            Require(root.StartsWith(Path.Combine(Path.GetTempPath(), "OldScars_IMPL0063_"), StringComparison.OrdinalIgnoreCase),
                "Fixture cleanup path must remain within task temp scope.");
            if (Directory.Exists(root)) Directory.Delete(root, true);
            string result = SessionState.GetString(Prefix + "Result", "INCONCLUSIVE");
            string evidence = SessionState.GetString(Prefix + "Evidence", "");
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/IMPL0063"));
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "stage1-result.txt"), result + "\n" + evidence);
            SessionState.EraseString(Prefix + "Phase");
            runtime = null;
            if (result == "PASS") Debug.Log("[IMPL0063][STAGE1] COMBINED PASS\n" + evidence);
            else Debug.LogError("[IMPL0063][STAGE1] " + result + "\n" + evidence);
        }
    }
}
