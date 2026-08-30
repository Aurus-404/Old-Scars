using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;
using OldScars.Core.Data.Loading;
using OldScars.Core.Data.Validation;
using OldScars.Core.Persistence;
using OldScars.Core.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Profiling;

namespace OldScars.EditorTools
{
    public static class DeformableTerrainSpikeDiagnostics
    {
        private const long GoldenSeed = 8675309123456789L;
        private const string GoldenPlanHash =
            "3f300ba2129962493d2ab8f2ad6ec0863e96aa0ceeb400f9899f91889a34e91a";
        private const string GoldenGeographyHash =
            "c2d412fcdcb1b0e1b41f4fdbda2df01258758e6db9c6b93aac59b446be7dbd3e";
        private const string GoldenWaterHash =
            "ec29f501e4f36ae3b2313d3da6089f2fe6e92b052f18079c649e21ce8faabfc0";
        private const string GoldenClimateHash =
            "a4b7869a7d8deab093eb9b9c5f7a2da118156f22c61ac466fbd0a9e64958eec1";
        private const string GoldenEnvironmentHash =
            "f8081c040da64ccce5e5eb5ffed941c2c2c44cd7ac5442582ee5d331c3abd1c5";
        private const string GoldenHumanHash =
            "a786f018ce3bdea44aeb066c80e38cb1f5dc8e114c65bd7eb352489628245ba6";
        private const string MenuPath =
            "Old Scars/Diagnostics/Worldgen/Run Deformable Volumetric Terrain Spike";

        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("Deformable Volumetric Terrain Spike Diagnostics: CANCELLED");
                return;
            }

            SceneSetup[] originalSceneSetup = EditorSceneManager.GetSceneManagerSetup();
            var failures = new List<string>();
            var evidence = new List<string>();
            string persistenceRoot = Path.Combine(
                Path.GetTempPath(), "OldScars_DeformableTerrain_Save_" + Guid.NewGuid().ToString("N"));
            string captureRoot = Path.Combine(
                Path.GetTempPath(), "OldScars_DeformableTerrain_Evidence");
            WorldDeformableTerrainSpikeController controller = null;
            try
            {
                Directory.CreateDirectory(captureRoot);
                LoadedContentSet content = LoadValidatedCore(failures);
                WorldSession session = CreateFixture(content, persistenceRoot, failures);
                if (session == null)
                    throw new InvalidOperationException("current-schema WorldSession fixture was unavailable");
                ValidateUpstreamTruth(session, failures);

                TerrainMaterializationConfiguration projection =
                    TerrainMaterializationConfiguration.CreateProvisionalBaseline();
                if (!TerrainMaterializationPlanner.TryBuildActiveRegion(
                        session, projection, out TerrainMaterializationPlan plan, out string planError))
                    throw new InvalidOperationException("projection failed: " + planError);

                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                CreateLighting();
                controller = new GameObject("Deformable Terrain Spike Diagnostics")
                    .AddComponent<WorldDeformableTerrainSpikeController>();
                DeformableTerrainSpikeConfiguration baseline =
                    DeformableTerrainSpikeConfiguration.CreateBaseline();
                if (!controller.TryMaterializePlan(plan, baseline))
                    throw new InvalidOperationException("baseline materialization failed: " + controller.Failure);

                ValidateBaseline(controller, failures);
                ValidateSharedBoundaries(controller.Volume, failures);
                DeformableTerrainSpikeMetrics initialMetrics = controller.Metrics;
                evidence.Add(
                    "baseline runtime: density=" + initialMetrics.DensityGenerationMilliseconds +
                    "ms, mesh=" + initialMetrics.InitialMeshingMilliseconds +
                    "ms, assignment=" + initialMetrics.MeshAssignmentMilliseconds +
                    "ms, collider=" + initialMetrics.ColliderUpdateMilliseconds +
                    "ms, meshApprox=" + initialMetrics.ApproximateMeshBytes + "B");
                ValidateLocalNavigationProbe(controller, "baseline", evidence, failures);
                Capture(controller, captureRoot, "01_near_surface", new Vector3(-20f, 10f, -20f),
                    controller.SpawnPosition, failures, evidence);

                ValidateLocalizedMutations(controller, failures, evidence);
                controller.TryReset(out _, out _);
                ValidateRepeatedMutationResetCycles(
                    controller, plan, "MarchingTetrahedra", failures, evidence);
                controller.TryReset(out _, out _);

                const float tunnelZ = -12f;
                float surface = plan.HeightNormalizedAtLocal(0f, tunnelZ) *
                                plan.Configuration.VerticalRelief;
                Vector3 craterCenter = new Vector3(0f, surface - 1.5f, tunnelZ);
                Check(controller.TrySubtractSphere(
                          craterCenter, 6.5f, out DeformableTerrainMutationResult crater, out string craterError),
                    "crater failed: " + Safe(craterError), failures);
                Capture(controller, captureRoot, "02_crater", new Vector3(-24f, 15f, -22f),
                    craterCenter, failures, evidence);

                Vector3 tunnelStart = new Vector3(0f, surface - 8f, tunnelZ);
                Vector3 tunnelEnd = new Vector3(28f, surface - 8f, tunnelZ);
                Check(controller.TrySubtractCapsule(
                          tunnelStart, tunnelEnd, 3.75f,
                          out DeformableTerrainMutationResult tunnel, out string tunnelError),
                    "tunnel failed: " + Safe(tunnelError), failures);
                evidence.Add(
                    "MarchingTetrahedra crater+tunnel dirty rebuild: craterChunks=" +
                    (crater?.AffectedChunks.Count ?? -1) + ", tunnelChunks=" +
                    (tunnel?.AffectedChunks.Count ?? -1) + ", tunnelMutation=" +
                    (tunnel?.MutationMilliseconds ?? -1) + "ms, mesh=" +
                    (tunnel?.MeshingMilliseconds ?? -1) + "ms, upload=" +
                    (tunnel?.MeshAssignmentMilliseconds ?? -1) + "ms, collider=" +
                    (tunnel?.ColliderUpdateMilliseconds ?? -1) + "ms");
                ValidateCavity(controller, tunnelStart, tunnelEnd, 3.75f, failures);
                ValidateLocalNavigationProbe(controller, "deformed", evidence, failures);
                Capture(controller, captureRoot, "03_cavity_side",
                    new Vector3(-10f, 3f, -10f), tunnelStart + Vector3.right * 10f,
                    failures, evidence);
                Capture(controller, captureRoot, "04_boundary_tunnel",
                    new Vector3(-15f, 0f, 0f), tunnelStart + Vector3.right * 20f,
                    failures, evidence);

                ValidatePersistenceReplay(
                    controller, session, plan, persistenceRoot, failures, evidence);
                ValidateConfigurationComparison(plan, evidence, failures);
                ValidateIndexedMarchingCubesRuntime(
                    controller, plan, baseline, captureRoot, failures, evidence);

                Check(crater != null && crater.AffectedChunks.Count > 0 &&
                      crater.AffectedChunks.Count < baseline.ChunkCountX * baseline.ChunkCountY * baseline.ChunkCountZ,
                    "border crater should rebuild a strict subset of the 3D chunk volume", failures);
                Check(tunnel != null && tunnel.AffectedChunks.Count >= 2 &&
                      tunnel.AffectedChunks.Count < baseline.ChunkCountX * baseline.ChunkCountY * baseline.ChunkCountZ,
                    "tunnel should rebuild intersected chunks without rebuilding the entire volume", failures);
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

            string report = failures.Count == 0
                ? "Deformable Volumetric Terrain Spike Diagnostics: PASS\n- " +
                  string.Join("\n- ", evidence)
                : "Deformable Volumetric Terrain Spike Diagnostics: FAIL\n- " +
                  string.Join("\n- ", failures);
            if (failures.Count > 0)
            {
                Debug.LogError(report);
                throw new InvalidOperationException(report);
            }
            Debug.Log(report);
        }

        private static void ValidateBaseline(
            WorldDeformableTerrainSpikeController controller,
            ICollection<string> failures)
        {
            DeformableTerrainSpikeMetrics metrics = controller.Metrics;
            Check(controller.IsReady && metrics != null, "controller was not ready", failures);
            Check(metrics.ChunkCount == 8, "baseline must materialize a real 2x2x2 chunk volume", failures);
            Check(metrics.DensitySamples == 79233,
                "baseline density sample cardinality changed unexpectedly", failures);
            Check(metrics.ApproximateFieldBytes == metrics.DensitySamples * 9L,
                "field memory estimate must include mutable density, baseline density, and material samples",
                failures);
            Check(metrics.Vertices > 0 && metrics.Triangles > 0,
                "baseline mesh was empty", failures);
            Check(controller.SpawnPosition.y > controller.Volume.Origin.y,
                "collider-backed spawn did not resolve above volume floor", failures);

            int nonEmptyChunks = 0;
            foreach (DeformableTerrainChunkId chunkId in controller.Volume.EnumerateChunks())
            {
                DeformableTerrainChunkMeshData mesh =
                    DeformableTerrainMesher.Build(
                        controller.Volume, chunkId, controller.MesherBackend);
                if (mesh.Vertices.Count > 0 && mesh.TriangleCount > 0)
                    nonEmptyChunks++;
                Check(mesh.Vertices.Count == mesh.Normals.Count &&
                      mesh.Vertices.Count == mesh.UVs.Count,
                    chunkId + " vertex/normal/UV cardinality differs", failures);
                for (int index = 0; index < mesh.Vertices.Count; index++)
                {
                    Vector3 vertex = mesh.Vertices[index];
                    Vector3 normal = mesh.Normals[index];
                    if (!Finite(vertex) || !Finite(normal) || normal.sqrMagnitude < 0.5f)
                    {
                        failures.Add(chunkId + " contains a non-finite/invalid vertex normal");
                        break;
                    }
                }
            }
            Check(nonEmptyChunks >= 2,
                "the procedural surface did not span multiple technical chunks", failures);
        }

        private static void ValidateSharedBoundaries(
            DeformableTerrainVolume volume,
            ICollection<string> failures)
        {
            ValidateSharedBoundaries(volume, failures, true);
        }

        private static void ValidateSharedBoundaries(
            DeformableTerrainVolume volume,
            ICollection<string> failures,
            bool includeCarvedFixture)
        {
            foreach (DeformableTerrainMesherBackend backend in
                     (DeformableTerrainMesherBackend[])Enum.GetValues(
                         typeof(DeformableTerrainMesherBackend)))
            {
                float boundaryX = volume.Origin.x + volume.Configuration.CellsPerChunkX *
                                  volume.Configuration.HorizontalCellSize;
                float boundaryY = volume.Origin.y + volume.Configuration.CellsPerChunkY *
                                  volume.VerticalCellSize;
                float boundaryZ = volume.Origin.z + volume.Configuration.CellsPerChunkZ *
                                  volume.Configuration.HorizontalCellSize;
                for (int y = 0; y < volume.Configuration.ChunkCountY; y++)
                for (int z = 0; z < volume.Configuration.ChunkCountZ; z++)
                    ValidateBoundaryPair(
                        DeformableTerrainMesher.Build(
                            volume, new DeformableTerrainChunkId(0, y, z), backend),
                        DeformableTerrainMesher.Build(
                            volume, new DeformableTerrainChunkId(1, y, z), backend),
                        0, boundaryX, backend + " X boundary y" + y + " z" + z, failures);
                for (int x = 0; x < volume.Configuration.ChunkCountX; x++)
                for (int z = 0; z < volume.Configuration.ChunkCountZ; z++)
                    ValidateBoundaryPair(
                        DeformableTerrainMesher.Build(
                            volume, new DeformableTerrainChunkId(x, 0, z), backend),
                        DeformableTerrainMesher.Build(
                            volume, new DeformableTerrainChunkId(x, 1, z), backend),
                        1, boundaryY, backend + " Y boundary x" + x + " z" + z, failures);
                for (int x = 0; x < volume.Configuration.ChunkCountX; x++)
                for (int y = 0; y < volume.Configuration.ChunkCountY; y++)
                    ValidateBoundaryPair(
                        DeformableTerrainMesher.Build(
                            volume, new DeformableTerrainChunkId(x, y, 0), backend),
                        DeformableTerrainMesher.Build(
                            volume, new DeformableTerrainChunkId(x, y, 1), backend),
                        2, boundaryZ, backend + " Z boundary x" + x + " y" + y, failures);
            }

            if (!includeCarvedFixture)
                return;

            string baselineEvidence = volume.ComputeDensityEvidence();
            Bounds lowerChunk = volume.ChunkBounds(new DeformableTerrainChunkId(0, 0, 0));
            var mutationService = new DeformableTerrainMutationService(volume);
            mutationService.SubtractSphere(
                new Vector3(lowerChunk.center.x, lowerChunk.max.y, lowerChunk.center.z), 3f);
            foreach (DeformableTerrainMesherBackend backend in
                     (DeformableTerrainMesherBackend[])Enum.GetValues(
                         typeof(DeformableTerrainMesherBackend)))
            {
                ValidateBoundaryPair(
                    DeformableTerrainMesher.Build(
                        volume, new DeformableTerrainChunkId(0, 0, 0), backend),
                    DeformableTerrainMesher.Build(
                        volume, new DeformableTerrainChunkId(0, 1, 0), backend),
                    1, lowerChunk.max.y, backend + " carved Y boundary", failures);
            }
            mutationService.Reset();
            Check(volume.ComputeDensityEvidence() == baselineEvidence,
                "boundary seam fixture did not reset the shared density field", failures);
        }

        private static void ValidateBoundaryPair(
            DeformableTerrainChunkMeshData first,
            DeformableTerrainChunkMeshData second,
            int axisIndex,
            float boundary,
            string label,
            ICollection<string> failures)
        {
            HashSet<string> firstPoints = BoundaryPoints(first, axisIndex, boundary);
            HashSet<string> secondPoints = BoundaryPoints(second, axisIndex, boundary);
            if (!firstPoints.SetEquals(secondPoints))
            {
                string firstOnly = FirstDifference(firstPoints, secondPoints);
                string secondOnly = FirstDifference(secondPoints, firstPoints);
                failures.Add(label + " mesh vertices do not agree exactly across shared density samples" +
                             " (first=" + firstPoints.Count + ", second=" + secondPoints.Count +
                             ", firstOnly=" + Safe(firstOnly) + ", secondOnly=" + Safe(secondOnly) + ")");
            }
        }

        private static HashSet<string> BoundaryPoints(
            DeformableTerrainChunkMeshData data,
            int axisIndex,
            float boundary)
        {
            var points = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < data.Vertices.Count; index++)
            {
                Vector3 point = data.Vertices[index];
                float axis = axisIndex == 0 ? point.x : axisIndex == 1 ? point.y : point.z;
                if (Mathf.Abs(axis - boundary) > 0.0001f)
                    continue;
                points.Add(
                    Quantize(axisIndex == 0 ? point.y : point.x) + ":" +
                    Quantize(axisIndex == 2 ? point.y : point.z));
            }
            return points;
        }

        private static void ValidateLocalizedMutations(
            WorldDeformableTerrainSpikeController controller,
            ICollection<string> failures,
            ICollection<string> evidence)
        {
            Bounds containedBounds = controller.Volume.ChunkBounds(
                new DeformableTerrainChunkId(0, 0, 0));
            Check(controller.TrySubtractSphere(
                      containedBounds.center, 3f,
                      out DeformableTerrainMutationResult contained, out string containedError) &&
                  contained.AffectedChunks.Count == 1,
                "contained mutation must rebuild exactly one chunk: " + Safe(containedError), failures);
            Check(controller.GetChunkRebuildCount(new DeformableTerrainChunkId(0, 0, 0)) == 1 &&
                  AllOtherChunksUnchanged(controller, new DeformableTerrainChunkId(0, 0, 0)),
                "contained mutation rebuilt an unrelated chunk", failures);
            controller.TryReset(out _, out _);

            float xBoundary = controller.Volume.ChunkBounds(
                new DeformableTerrainChunkId(0, 0, 0)).max.x;
            Check(controller.TrySubtractSphere(
                      new Vector3(xBoundary - 5.01f, containedBounds.center.y, containedBounds.center.z), 3f,
                      out DeformableTerrainMutationResult normalHalo, out string normalHaloError) &&
                  normalHalo.AffectedChunks.Count == 2,
                "near-border normal halo must rebuild the neighboring chunk: " +
                Safe(normalHaloError), failures);
            controller.TryReset(out _, out _);

            Check(controller.TrySubtractSphere(
                      new Vector3(xBoundary, containedBounds.center.y, containedBounds.center.z), 3f,
                      out DeformableTerrainMutationResult xBorder, out string xError) &&
                  xBorder.AffectedChunks.Count == 2,
                "X-border mutation must rebuild two chunks: " + Safe(xError), failures);
            controller.TryReset(out _, out _);

            float zBoundary = containedBounds.max.z;
            Check(controller.TrySubtractSphere(
                      new Vector3(containedBounds.center.x, containedBounds.center.y, zBoundary), 3f,
                      out DeformableTerrainMutationResult zBorder, out string zError) &&
                  zBorder.AffectedChunks.Count == 2,
                "Z-border mutation must rebuild two chunks: " + Safe(zError), failures);
            controller.TryReset(out _, out _);

            float yBoundary = containedBounds.max.y;
            Check(controller.TrySubtractSphere(
                      new Vector3(containedBounds.center.x, yBoundary, containedBounds.center.z), 3f,
                      out DeformableTerrainMutationResult yBorder, out string yError) &&
                  yBorder.AffectedChunks.Count == 2,
                "Y-border mutation must rebuild two chunks: " + Safe(yError), failures);
            controller.TryReset(out _, out _);

            Check(controller.TrySubtractSphere(
                      new Vector3(xBoundary, yBoundary, zBoundary), 3f,
                      out DeformableTerrainMutationResult corner, out string cornerError) &&
                  corner.AffectedChunks.Count == 8,
                "XYZ corner mutation must rebuild eight chunks: " + Safe(cornerError), failures);
            evidence.Add(
                "mutation locality: contained=1, normal-halo=2, X-border=2, Y-border=2, " +
                "Z-border=2, XYZ-corner=8 chunks; " +
                "corner mutation=" + (corner?.MutationMilliseconds ?? -1) + "ms, mesh=" +
                (corner?.MeshingMilliseconds ?? -1) + "ms, collider=" +
                (corner?.ColliderUpdateMilliseconds ?? -1) + "ms");
        }

        private static void ValidateRepeatedMutationResetCycles(
            WorldDeformableTerrainSpikeController controller,
            TerrainMaterializationPlan plan,
            string backendLabel,
            ICollection<string> failures,
            ICollection<string> evidence)
        {
            string baselineDensity = controller.Volume.ComputeDensityEvidence();
            long totalMutationMilliseconds = 0L;
            long totalMeshingMilliseconds = 0L;
            long totalColliderMilliseconds = 0L;
            int totalAffectedChunks = 0;
            const int cycles = 3;
            for (int cycle = 0; cycle < cycles; cycle++)
            {
                float firstZ = -4f + cycle * 2.5f;
                float secondZ = firstZ + 3f;
                float firstSurface = plan.HeightNormalizedAtLocal(-4f, firstZ) *
                                     plan.Configuration.VerticalRelief;
                float secondSurface = plan.HeightNormalizedAtLocal(4f, secondZ) *
                                      plan.Configuration.VerticalRelief;
                Check(controller.TrySubtractSphere(
                          new Vector3(-4f, firstSurface - 1.5f, firstZ), 2.5f,
                          out DeformableTerrainMutationResult first, out string firstError),
                    backendLabel + " repeated mutation first sphere failed: " + Safe(firstError), failures);
                Check(controller.TrySubtractSphere(
                          new Vector3(4f, secondSurface - 1.5f, secondZ), 2.5f,
                          out DeformableTerrainMutationResult second, out string secondError),
                    backendLabel + " repeated mutation second sphere failed: " + Safe(secondError), failures);
                totalAffectedChunks += (first?.AffectedChunks.Count ?? 0) +
                                       (second?.AffectedChunks.Count ?? 0);
                totalMutationMilliseconds += (first?.MutationMilliseconds ?? 0L) +
                                             (second?.MutationMilliseconds ?? 0L);
                totalMeshingMilliseconds += (first?.MeshingMilliseconds ?? 0L) +
                                             (second?.MeshingMilliseconds ?? 0L);
                totalColliderMilliseconds += (first?.ColliderUpdateMilliseconds ?? 0L) +
                                              (second?.ColliderUpdateMilliseconds ?? 0L);
                Check(controller.MutationService.Mutations.Count == 2,
                    backendLabel + " repeated mutation operation count drifted", failures);
                // The current mutation authority rebuilds each operation immediately;
                // validate the shared lattice in its current state without invoking
                // the separate carved-fixture reset helper.
                ValidateSharedBoundaries(controller.Volume, failures, false);

                Check(controller.TryReset(
                          out DeformableTerrainMutationResult reset, out string resetError),
                    backendLabel + " repeated mutation reset failed: " + Safe(resetError), failures);
                Check(controller.Volume.ComputeDensityEvidence() == baselineDensity &&
                      controller.MutationService.Mutations.Count == 0,
                    backendLabel + " reset did not restore exact baseline density/mutation state", failures);
                totalMutationMilliseconds += reset?.MutationMilliseconds ?? 0L;
                totalMeshingMilliseconds += reset?.MeshingMilliseconds ?? 0L;
                totalColliderMilliseconds += reset?.ColliderUpdateMilliseconds ?? 0L;
            }

            evidence.Add(
                backendLabel + " repeated mutate/reset: cycles=" + cycles +
                ", operations=" + (cycles * 2) +
                ", affectedChunksTotal=" + totalAffectedChunks +
                ", mutation=" + totalMutationMilliseconds + "ms, mesh=" +
                totalMeshingMilliseconds + "ms, collider=" + totalColliderMilliseconds +
                "ms, exact baseline restored each cycle; batch mutation unsupported by current boundary");
        }

        private static bool AllOtherChunksUnchanged(
            WorldDeformableTerrainSpikeController controller,
            DeformableTerrainChunkId expected)
        {
            foreach (DeformableTerrainChunkId chunkId in controller.Volume.EnumerateChunks())
            {
                if (chunkId != expected && controller.GetChunkRebuildCount(chunkId) != 0)
                    return false;
            }
            return true;
        }

        private static void ValidateCavity(
            WorldDeformableTerrainSpikeController controller,
            Vector3 start,
            Vector3 end,
            float radius,
            ICollection<string> failures)
        {
            Vector3 center = Vector3.Lerp(start, end, 0.72f);
            Check(controller.Volume.DensityAtLocal(center) < 0f,
                "tunnel center is not air", failures);
            Check(controller.Volume.DensityAtLocal(center + Vector3.up * (radius + 1.5f)) > 0f,
                "heightmap-impossible cavity lacks a solid roof", failures);
            Check(controller.Volume.DensityAtLocal(center - Vector3.up * (radius + 1.5f)) > 0f,
                "heightmap-impossible cavity lacks a solid floor", failures);
            int layerMask = 1 << 3;
            bool roofHit = Physics.Raycast(
                center, Vector3.up, out RaycastHit roof, radius * 2f, layerMask,
                QueryTriggerInteraction.Ignore);
            bool floorHit = Physics.Raycast(
                center, Vector3.down, out RaycastHit floor, radius * 2f, layerMask,
                QueryTriggerInteraction.Ignore);
            Check(roofHit && floorHit && roof.distance > 0.25f && floor.distance > 0.25f,
                "cavity MeshCollider must expose both roof and floor around traversable air", failures);
        }

        private static void ValidatePersistenceReplay(
            WorldDeformableTerrainSpikeController controller,
            WorldSession session,
            TerrainMaterializationPlan plan,
            string persistenceRoot,
            ICollection<string> failures,
            ICollection<string> evidence)
        {
            string expectedDensity = controller.Volume.ComputeDensityEvidence();
            Vector3 cavityProbe = new Vector3(
                20f,
                plan.HeightNormalizedAtLocal(0f, -12f) * plan.Configuration.VerticalRelief - 8f,
                -12f);
            DeformableTerrainSpikeState captured = controller.MutationService.CaptureState(session);
            var store = new PersistenceFileStore(persistenceRoot);
            PersistenceWriteResult write = store.Write(
                "deformable_terrain_spike", DeformableTerrainSpikePersistence.ToPayload(captured));
            PersistenceLoadResult read = store.Read("deformable_terrain_spike");
            Check(write.Success && read.Success,
                "M37 spike payload write/read failed: " + Safe(write.Failure) + " / " + Safe(read.Failure),
                failures);
            DeformableTerrainSpikeState restored = null;
            string restoreError = null;
            bool restoredPayload = read.Success && DeformableTerrainSpikePersistence.TryFromPayload(
                read.Payload, session, plan, out restored, out restoreError);
            Check(restoredPayload,
                "spike payload semantic preflight failed: " + Safe(restoreError), failures);
            if (!read.Success || restored == null)
                return;
            Check(restored.Configuration.HasEquivalentLayout(captured.Configuration) &&
                  restored.Origin == captured.Origin &&
                  restored.VerticalCellSize == captured.VerticalCellSize,
                "persisted spike baseline contract changed", failures);

            store.TryGetPaths("deformable_terrain_spike", out string primary, out _, out _);
            long payloadBytes = File.Exists(primary) ? new FileInfo(primary).Length : -1L;
            controller.ClearMaterialization();
            Check(controller.TryMaterializePlan(plan, restored.Configuration),
                "teardown/reconstruction baseline failed: " + controller.Failure, failures);
            Check(controller.TryReplay(
                      restored.Mutations, out DeformableTerrainMutationResult replay, out string replayError),
                "persisted terrain mutation replay failed: " + Safe(replayError), failures);
            Check(controller.Volume.ComputeDensityEvidence() == expectedDensity,
                "teardown/reconstruction did not reproduce exact deformed density evidence", failures);
            Check(controller.Volume.DensityAtLocal(cavityProbe) < 0f,
                "heightmap-impossible cavity disappeared after persistence replay", failures);
            evidence.Add(
                "SPIKE_NON_PRODUCTION persistence: operations=" + restored.Mutations.Count +
                ", payload=" + payloadBytes + "B, replayChunks=" +
                (replay?.AffectedChunks.Count ?? -1) + ", densityEvidence=" + expectedDensity);

            JObject malformed = (JObject)read.Payload.DeepClone();
            malformed["worldId"] = WorldId.CreateNew().Canonical;
            Check(!DeformableTerrainSpikePersistence.TryFromPayload(
                      malformed, session, plan, out _, out _),
                "foreign-world spike payload must fail before replay/publication", failures);

            JObject undefinedKind = (JObject)read.Payload.DeepClone();
            ((JObject)((JArray)undefinedKind["mutations"])[0])["kind"] = "999";
            Check(!DeformableTerrainSpikePersistence.TryFromPayload(
                      undefinedKind, session, plan, out _, out _),
                "undefined numeric mutation kinds must fail semantic preflight", failures);

            string replayedDensity = controller.Volume.ComputeDensityEvidence();
            int replayedOperationCount = controller.MutationService.Mutations.Count;
            Check(controller.TryReplay(
                      controller.MutationService.Mutations, out _, out string selfReplayError) &&
                  controller.Volume.ComputeDensityEvidence() == replayedDensity &&
                  controller.MutationService.Mutations.Count == replayedOperationCount,
                "replaying the service's immutable mutation view changed or erased state: " +
                Safe(selfReplayError), failures);

            var invalidReplay = new List<DeformableTerrainMutation>(
                controller.MutationService.Mutations)
            {
                new DeformableTerrainMutation(
                    DeformableTerrainMutationKind.SubtractSphere,
                    new Vector3(100000f, 0f, 0f),
                    new Vector3(100000f, 0f, 0f),
                    3f)
            };
            Check(!controller.TryReplay(
                      invalidReplay, out _, out string invalidReplayError) &&
                  controller.Volume.ComputeDensityEvidence() == replayedDensity &&
                  controller.MutationService.Mutations.Count == replayedOperationCount,
                "failed replay must preserve the previously committed spike state: " +
                Safe(invalidReplayError), failures);
        }

        private static void ValidateConfigurationComparison(
            TerrainMaterializationPlan plan,
            ICollection<string> evidence,
            ICollection<string> failures)
        {
            DeformableTerrainSpikeConfiguration[] configurations =
            {
                DeformableTerrainSpikeConfiguration.CreateCoarseComparison(),
                DeformableTerrainSpikeConfiguration.CreateBaseline()
            };
            for (int configurationIndex = 0; configurationIndex < configurations.Length; configurationIndex++)
            {
                System.Diagnostics.Stopwatch density = System.Diagnostics.Stopwatch.StartNew();
                Check(DeformableTerrainVolume.TryCreate(
                          plan, configurations[configurationIndex],
                          out DeformableTerrainVolume volume, out string error),
                    "comparison volume failed: " + Safe(error), failures);
                density.Stop();
                if (volume == null) continue;
                string densityEvidence = volume.ComputeDensityEvidence();
                foreach (DeformableTerrainMesherBackend backend in
                         (DeformableTerrainMesherBackend[])Enum.GetValues(
                             typeof(DeformableTerrainMesherBackend)))
                {
                    long allocationStart = Profiler.GetMonoUsedSizeLong();
                    System.Diagnostics.Stopwatch mesh = System.Diagnostics.Stopwatch.StartNew();
                    int vertices = 0;
                    int triangles = 0;
                    int indices = 0;
                    int reused = 0;
                    foreach (DeformableTerrainChunkId chunkId in volume.EnumerateChunks())
                    {
                        DeformableTerrainChunkMeshData data =
                            DeformableTerrainMesher.Build(volume, chunkId, backend);
                        vertices += data.Vertices.Count;
                        triangles += data.TriangleCount;
                        indices += data.IndexCount;
                        reused += data.ReusedVertexReferenceCount;
                    }
                    mesh.Stop();
                    long allocated = Math.Max(
                        0L, Profiler.GetMonoUsedSizeLong() - allocationStart);
                    long meshBytes = vertices * 32L + indices * sizeof(int);
                    Check(volume.ComputeDensityEvidence() == densityEvidence,
                        backend + " meshing changed shared density truth", failures);
                    if (backend == DeformableTerrainMesherBackend.IndexedMarchingCubes)
                        Check(reused > 0 && vertices < indices,
                            "indexed Marching Cubes did not demonstrate vertex reuse", failures);
                    evidence.Add(
                        (configurationIndex == 0 ? "coarse" : "baseline") + " " + backend +
                        ": chunks=" + (configurations[configurationIndex].ChunkCountX *
                                        configurations[configurationIndex].ChunkCountY *
                                        configurations[configurationIndex].ChunkCountZ) +
                        ", cells/chunk=" + configurations[configurationIndex].CellsPerChunkX +
                        "x" + configurations[configurationIndex].CellsPerChunkY + "x" +
                        configurations[configurationIndex].CellsPerChunkZ +
                        ", horizontalCell=" + configurations[configurationIndex].HorizontalCellSize.ToString(
                            "0.###", CultureInfo.InvariantCulture) +
                        ", verticalCell=" + volume.VerticalCellSize.ToString("0.###", CultureInfo.InvariantCulture) +
                        ", samples=" + volume.DensitySampleCount + ", field=" + volume.ApproximateFieldBytes +
                        "B, vertices=" + vertices + ", triangles=" + triangles +
                        ", reusedRefs=" + reused + ", meshApprox=" + meshBytes +
                        "B, allocated=" + allocated + "B, density=" + density.ElapsedMilliseconds +
                        "ms, mesh=" + mesh.ElapsedMilliseconds + "ms");
                }
            }
        }

        private static void ValidateIndexedMarchingCubesRuntime(
            WorldDeformableTerrainSpikeController controller,
            TerrainMaterializationPlan plan,
            DeformableTerrainSpikeConfiguration configuration,
            string captureRoot,
            ICollection<string> failures,
            ICollection<string> evidence)
        {
            Check(controller.TryMaterializePlan(
                      plan, configuration, DeformableTerrainMesherBackend.IndexedMarchingCubes),
                "indexed MC runtime materialization failed: " + Safe(controller.Failure), failures);
            if (!controller.IsReady) return;
            ValidateBaseline(controller, failures);
            Check(controller.Metrics.ReusedVertexReferences > 0,
                "indexed MC runtime did not publish indexed vertex reuse", failures);
            Capture(controller, captureRoot, "05_mc_near_surface",
                new Vector3(-20f, 10f, -20f), controller.SpawnPosition, failures, evidence);

            const float z = -12f;
            float surface = plan.HeightNormalizedAtLocal(0f, z) * plan.Configuration.VerticalRelief;
            Vector3 tunnelStart = new Vector3(0f, surface - 8f, z);
            Vector3 tunnelEnd = new Vector3(28f, surface - 8f, z);
            Check(controller.TrySubtractSphere(
                      new Vector3(0f, surface - 1.5f, z), 6.5f, out _, out string craterError),
                "indexed MC crater failed: " + Safe(craterError), failures);
            Check(controller.TrySubtractCapsule(
                      tunnelStart, tunnelEnd, 3.75f, out DeformableTerrainMutationResult tunnel,
                      out string tunnelError),
                "indexed MC tunnel failed: " + Safe(tunnelError), failures);
            ValidateCavity(controller, tunnelStart, tunnelEnd, 3.75f, failures);
            Capture(controller, captureRoot, "06_mc_cavity",
                new Vector3(-15f, 0f, 0f), tunnelStart + Vector3.right * 20f,
                failures, evidence);
            evidence.Add(
                "indexed MC Unity publication: vertices=" + controller.Metrics.Vertices +
                ", triangles=" + controller.Metrics.Triangles +
                ", reusedRefs=" + controller.Metrics.ReusedVertexReferences +
                ", dirtyTunnelChunks=" + (tunnel?.AffectedChunks.Count ?? -1) +
                ", mutation=" + (tunnel?.MutationMilliseconds ?? -1) + "ms, mesh=" +
                (tunnel?.MeshingMilliseconds ?? -1) + "ms, upload=" +
                (tunnel?.MeshAssignmentMilliseconds ?? -1) + "ms, collider=" +
                (tunnel?.ColliderUpdateMilliseconds ?? -1) + "ms");
            controller.TryReset(out _, out _);
            ValidateRepeatedMutationResetCycles(
                controller, plan, "IndexedMarchingCubes", failures, evidence);
        }

        private static void ValidateLocalNavigationProbe(
            WorldDeformableTerrainSpikeController controller,
            string label,
            ICollection<string> evidence,
            ICollection<string> failures)
        {
            NavMeshSurface surface = controller.NavMeshSurface;
            bool ownsSurface = false;
            try
            {
                if (surface == null)
                {
                    surface = controller.GeneratedRoot.AddComponent<NavMeshSurface>();
                    ownsSurface = true;
                    surface.collectObjects = CollectObjects.Children;
                    surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                    surface.layerMask = 1 << 3;
                    surface.overrideTileSize = true;
                    surface.tileSize = 64;
                    System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
                    surface.BuildNavMesh();
                    watch.Stop();
                }
                NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
                Check(surface.navMeshData != null && triangulation.vertices.Length > 0,
                    label + " local NavMesh probe produced no data", failures);
                string buildEvidence = ownsSurface
                    ? "diagnostic build"
                    : "controller build " + controller.NavigationBuildMilliseconds + "ms";
                evidence.Add(
                    label + " local NavMesh probe: " + buildEvidence +
                    ", vertices=" + triangulation.vertices.Length +
                    "; local volumetric surface, ActorNavigationController unchanged");
            }
            catch (Exception exception)
            {
                failures.Add(label + " local NavMesh probe failed: " + exception.Message);
            }
            finally
            {
                if (ownsSurface && surface != null)
                {
                    surface.RemoveData();
                    UnityEngine.Object.DestroyImmediate(surface);
                }
            }
        }

        private static void ValidateUpstreamTruth(WorldSession session, ICollection<string> failures)
        {
            Check(WorldSessionPersistenceService.CurrentSchemaVersion == 7,
                "terrain spike must not bump world_session_v1 schema 7", failures);
            Check(session.MacroWorldPlan.CanonicalHash == GoldenPlanHash, "Plan golden drifted", failures);
            Check(session.MacroGeography.CanonicalHash == GoldenGeographyHash,
                "Geography golden drifted", failures);
            Check(session.MacroWater.CanonicalHash == GoldenWaterHash, "Water golden drifted", failures);
            Check(session.MacroClimate.CanonicalHash == GoldenClimateHash, "Climate golden drifted", failures);
            Check(session.MacroEnvironment.CanonicalHash == GoldenEnvironmentHash,
                "Environment golden drifted", failures);
            Check(session.MacroHumanGeography.CanonicalHash == GoldenHumanHash,
                "Human Geography golden drifted", failures);
        }

        private static WorldSession CreateFixture(
            LoadedContentSet content,
            string persistenceRoot,
            ICollection<string> failures)
        {
            if (content == null)
                return null;
            if (!WorldSessionBootstrap.TryBuildNew(
                    "Deformable Terrain Spike", new WorldSeed(GoldenSeed),
                    WorldGenerationSettings.ResolvePreset(WorldSizePreset.Large),
                    LandCoveragePreset.High, content,
                    out WorldSession generated, out string buildError))
            {
                failures.Add("world fixture generation failed: " + Safe(buildError));
                return null;
            }
            var store = new PersistenceFileStore(persistenceRoot);
            WorldSessionPersistenceResult save = WorldSessionPersistenceService.Save(generated, store);
            WorldSessionPersistenceResult load = WorldSessionPersistenceService.Read(
                generated.WorldId.Canonical, store);
            if (!save.Success || !load.Success)
            {
                failures.Add("world fixture persistence failed: " + Safe(save.Failure) + " / " + Safe(load.Failure));
                return null;
            }
            return load.Session;
        }

        private static LoadedContentSet LoadValidatedCore(ICollection<string> failures)
        {
            var report = new DataLoadReport();
            var loader = new GameDataLoader(
                Path.Combine(Application.streamingAssetsPath, "Mods"), report);
            loader.LoadAll();
            new DataValidator(loader.Database, loader.Tags, report).Validate();
            if (report.HasErrors || !loader.TryBuildLoadedContentSet(out LoadedContentSet content))
            {
                failures.Add("real Core content validation/provenance failed");
                return null;
            }
            return content;
        }

        private static void CreateLighting()
        {
            RenderSettings.ambientLight = new Color(0.32f, 0.35f, 0.4f, 1f);
            GameObject lightObject = new GameObject("Spike Evidence Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        }

        private static void Capture(
            WorldDeformableTerrainSpikeController controller,
            string root,
            string label,
            Vector3 cameraOffset,
            Vector3 target,
            ICollection<string> failures,
            ICollection<string> evidence)
        {
            string path = Path.Combine(root, "deformable_terrain_" + label + ".png");
            GameObject cameraObject = null;
            RenderTexture renderTexture = null;
            Texture2D pixels = null;
            try
            {
                cameraObject = new GameObject("Deformable Terrain Evidence Camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.075f, 0.09f, 0.12f, 1f);
                camera.fieldOfView = 58f;
                camera.nearClipPlane = 0.15f;
                camera.farClipPlane = 500f;
                camera.transform.position = target + cameraOffset;
                camera.transform.LookAt(target);
                renderTexture = new RenderTexture(960, 720, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = renderTexture;
                camera.Render();
                camera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = renderTexture;
                pixels = new Texture2D(960, 720, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, 960, 720), 0, 0);
                pixels.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                Check(File.Exists(path) && new FileInfo(path).Length > 4096,
                    label + " capture was missing or unexpectedly small", failures);
                if (File.Exists(path))
                    evidence.Add("capture " + path + " (" + new FileInfo(path).Length + "B)");
                camera.targetTexture = null;
            }
            catch (Exception exception)
            {
                failures.Add(label + " capture failed: " + exception.Message);
            }
            finally
            {
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                if (renderTexture != null) UnityEngine.Object.DestroyImmediate(renderTexture);
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static bool Finite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static string Quantize(float value)
        {
            return Math.Round(value, 4).ToString("0.0000", CultureInfo.InvariantCulture);
        }

        private static string FirstDifference(HashSet<string> source, HashSet<string> other)
        {
            foreach (string value in source)
                if (!other.Contains(value))
                    return value;
            return null;
        }

        private static void Check(bool condition, string failure, ICollection<string> failures)
        {
            if (!condition) failures.Add(failure);
        }

        private static string Safe(string value) => string.IsNullOrEmpty(value) ? "<NONE>" : value;
    }
}
