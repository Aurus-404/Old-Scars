using System;
using System.IO;
using OldScars.Core;
using OldScars.Core.ApplicationShell;
using OldScars.Core.Interactions;
using OldScars.Core.Persistence;
using OldScars.Core.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OldScars.EditorTools
{
    [InitializeOnLoad]
    public static class DeformableTerrainSpikePlayModeDiagnostics
    {
        private const string PendingKey = "OldScars.DeformableTerrain.Play.Pending";
        private const string StageKey = "OldScars.DeformableTerrain.Play.Stage";
        private const string FailureKey = "OldScars.DeformableTerrain.Play.Failure";
        private const long Seed = 8675309123456789L;

        private static WorldDeformableTerrainSpikeController terrain;
        private static PlayerGameplayComposition player;
        private static PlayerMovementController movement;
        private static string persistenceRoot;
        private static Vector3 stageStartPosition;
        private static Vector3 surfaceStartForLog;
        private static float stageStartedAt;
        private static int stableFrames;

        static DeformableTerrainSpikePlayModeDiagnostics()
        {
            EditorApplication.update -= Continue;
            EditorApplication.update += Continue;
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Deformable terrain Play Mode diagnostics require idle Edit Mode.");
            SessionState.SetBool(PendingKey, true);
            SessionState.SetInt(StageKey, 0);
            SessionState.EraseString(FailureKey);
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
                if (EditorApplication.isPlaying)
                {
                    RunPlayStage();
                    return;
                }
                if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                    SessionState.GetInt(StageKey, 0) == 99)
                    Finish();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetString(FailureKey, exception.Message);
                SessionState.SetInt(StageKey, 99);
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorApplication.ExitPlaymode();
            }
        }

        private static void RunPlayStage()
        {
            int stage = SessionState.GetInt(StageKey, 0);
            if (stage == 0)
            {
                if (Time.frameCount < 5 || GameDataManager.Instance?.IsReady != true ||
                    GameDataManager.Instance.LoadedContentSet == null)
                    return;
                Setup();
                SessionState.SetInt(StageKey, 1);
                stageStartedAt = Time.time;
                return;
            }

            if (player == null || terrain == null || movement == null)
                throw new InvalidOperationException("Play Mode terrain/player fixture disappeared.");
            if (stage == 1)
            {
                if (!GroundedOnSpike(player.PlayerTransform.position, out _))
                {
                    if (++stableFrames > 30)
                        throw new InvalidOperationException("real player did not settle on the volumetric MeshCollider.");
                    return;
                }
                stableFrames = 0;
                stageStartPosition = player.PlayerTransform.position;
                surfaceStartForLog = stageStartPosition;
                movement.SetMovementDirection(Vector3.right);
                SessionState.SetInt(StageKey, 2);
                stageStartedAt = Time.time;
                return;
            }
            if (stage == 2)
            {
                if (player.PlayerTransform.position.y < terrain.Volume.Origin.y - 2f)
                    throw new InvalidOperationException("player fell through untouched volumetric terrain.");
                Vector3 surfaceEnd = player.PlayerTransform.position;
                if (surfaceEnd.x <= 5f)
                {
                    if (Time.time - stageStartedAt > 20f)
                        throw new InvalidOperationException(
                            "existing PlayerMovementController did not cross a chunk boundary on the volumetric surface " +
                            "(start=" + stageStartPosition + ", end=" + surfaceEnd +
                            ", requested=" + movement.RequestedMovementDirection +
                            ", speed=" + movement.EffectiveMovementSpeed +
                            ", boundaryProfile=" + BoundaryProfile() + ").");
                    return;
                }
                movement.ClearMovement();
                surfaceEndForLog = surfaceEnd;
                if (surfaceEnd.x - stageStartPosition.x < 20f || !GroundedOnSpike(surfaceEnd, out _))
                    throw new InvalidOperationException(
                        "existing PlayerMovementController did not cross a chunk boundary on the volumetric surface " +
                        "(start=" + stageStartPosition + ", end=" + surfaceEnd +
                        ", requested=" + movement.RequestedMovementDirection +
                        ", speed=" + movement.EffectiveMovementSpeed +
                        ", boundaryProfile=" + BoundaryProfile() + ").");

                if (!terrain.TryFindSurfacePoint(
                        new Vector3(-9f, 0f, -12f), out Vector3 lipSurface))
                    throw new InvalidOperationException("crater approach surface was unavailable.");
                player.PlacePlayerAtSurface(lipSurface, Quaternion.LookRotation(Vector3.right));
                Physics.SyncTransforms();
                stageStartPosition = player.PlayerTransform.position;
                movement.SetMovementDirection(Vector3.right);
                SessionState.SetInt(StageKey, 3);
                stageStartedAt = Time.time;
                return;
            }
            if (stage == 3)
            {
                if (player.PlayerTransform.position.y < terrain.Volume.Origin.y - 2f)
                    throw new InvalidOperationException("player fell through deformed terrain collider.");
                Vector3 end = player.PlayerTransform.position;
                float outsideSurface = terrain.SourcePlan.HeightNormalizedAtLocal(end.x, end.z) *
                                       terrain.SourcePlan.Configuration.VerticalRelief;
                bool underRoof = Physics.Raycast(
                    end + Vector3.up * 0.2f, Vector3.up, out RaycastHit roof, 8f,
                    1 << 3, QueryTriggerInteraction.Ignore);
                bool onFloor = GroundedOnSpike(end, out RaycastHit floor);
                bool inside = end.x > 4f && end.x - stageStartPosition.x > 10f &&
                              end.y < outsideSurface - 2f && underRoof && onFloor;
                if (!inside)
                {
                    if (Time.time - stageStartedAt <= 20f)
                        return;
                    throw new InvalidOperationException(
                        "player did not descend through the crater and enter the roofed volumetric tunnel " +
                        "(start=" + stageStartPosition + ", end=" + end +
                        ", outsideSurface=" + outsideSurface + ", roof=" + roof.distance +
                        ", floor=" + floor.distance + ").");
                }
                movement.ClearMovement();

                Debug.Log(
                    "Deformable Volumetric Terrain Play Mode Diagnostics: PASS\n" +
                    "- PlayerAuthority: PFB_PlayerGameplayComposition / PlayerMovementController / CharacterController\n" +
                    "- SurfaceChunkBoundaryTraversal: " + surfaceStartForLog + " -> " + surfaceEndForLog + "\n" +
                    "- CavityTraversal: " + stageStartPosition + " -> " + end + "\n" +
                    "- RoofDistance: " + roof.distance + "\n" +
                    "- FloorDistance: " + floor.distance + "\n" +
                    "- No parallel player or navigation authority was created");
                SessionState.SetInt(StageKey, 99);
                EditorApplication.ExitPlaymode();
            }
        }

        private static Vector3 surfaceEndForLog;

        private static void Setup()
        {
            persistenceRoot = Path.Combine(
                Path.GetTempPath(), "OldScars_DeformableTerrain_Play_" + Guid.NewGuid().ToString("N"));
            var store = new PersistenceFileStore(persistenceRoot);
            WorldSessionOperationResult created = WorldSessionService.Create(
                "Deformable Terrain Play Probe", new WorldSeed(Seed),
                WorldGenerationSettings.ResolvePreset(WorldSizePreset.Small),
                LandCoveragePreset.High, GameDataManager.Instance.LoadedContentSet, store);
            if (!created.Success)
                throw new InvalidOperationException("WorldSession fixture failed: " + created.Failure);

            terrain = new GameObject("Deformable Terrain Play Probe")
                .AddComponent<WorldDeformableTerrainSpikeController>();
            if (!terrain.TryMaterializeActiveSession(
                    created.Session,
                    TerrainMaterializationConfiguration.CreateProvisionalBaseline(),
                    DeformableTerrainSpikeConfiguration.CreateBaseline()))
                throw new InvalidOperationException("terrain fixture failed: " + terrain.Failure);
            float surface = terrain.SourcePlan.HeightNormalizedAtLocal(0f, -12f) *
                            terrain.SourcePlan.Configuration.VerticalRelief;
            bool craterSucceeded = terrain.TrySubtractSphere(
                new Vector3(0f, surface - 1.5f, -12f), 6.5f, out _, out string craterError);
            bool tunnelSucceeded = terrain.TrySubtractCapsule(
                new Vector3(0f, surface - 8f, -12f),
                new Vector3(28f, surface - 8f, -12f),
                3.75f, out _, out string tunnelError);
            if (!craterSucceeded || !tunnelSucceeded)
                throw new InvalidOperationException(
                    "deformation fixture failed: " + craterError + " / " + tunnelError);
            if (!terrain.TryFindSurfacePoint(
                    new Vector3(-20f, 0f, 12f), out Vector3 spawnSurface))
                throw new InvalidOperationException("safe volumetric player spawn was unavailable.");
            if (!PlayerGameplayComposition.TryInstantiateAtSurface(
                    spawnSurface, terrain.transform, out player, out string playerError))
                throw new InvalidOperationException("shared player composition failed: " + playerError);
            player.MovementInput.enabled = false;
            movement = player.MovementController;
            movement.SetDebugMovementMultiplier(1f);
            player.BindCameraToPlayer();
            Physics.SyncTransforms();
        }

        private static bool GroundedOnSpike(Vector3 position, out RaycastHit hit)
        {
            if (!Physics.Raycast(
                    position + Vector3.up * 1f, Vector3.down, out hit, 4f,
                    1 << 3, QueryTriggerInteraction.Ignore))
                return false;
            return terrain != null && terrain.GeneratedRoot != null &&
                   hit.transform.IsChildOf(terrain.GeneratedRoot.transform);
        }

        private static string BoundaryProfile()
        {
            string value = string.Empty;
            for (int x = -3; x <= 3; x++)
            {
                Vector3 probe = new Vector3(x, 0f, 12f);
                float logical = terrain.SourcePlan.HeightNormalizedAtLocal(probe.x, probe.z) *
                                terrain.SourcePlan.Configuration.VerticalRelief;
                bool hit = terrain.TryFindSurfacePoint(probe, out Vector3 physical);
                if (value.Length > 0) value += ";";
                value += x + ":logical=" + logical.ToString("0.000") +
                         ",collider=" + (hit ? physical.y.ToString("0.000") : "MISS");
            }
            return value;
        }

        private static void Finish()
        {
            string failure = SessionState.GetString(FailureKey, string.Empty);
            SessionState.SetBool(PendingKey, false);
            SessionState.SetInt(StageKey, 0);
            SessionState.EraseString(FailureKey);
            WorldSessionService.Close();
            if (!string.IsNullOrWhiteSpace(persistenceRoot) && Directory.Exists(persistenceRoot))
                Directory.Delete(persistenceRoot, true);
            terrain = null;
            player = null;
            movement = null;
            stableFrames = 0;
            if (!string.IsNullOrEmpty(failure))
            {
                Debug.LogError("Deformable Volumetric Terrain Play Mode Diagnostics: FAIL\n" + failure);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
