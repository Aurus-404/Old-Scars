using System;
using System.Collections.Generic;
using OldScars.Core.Actors;
using OldScars.Core.Identity;
using OldScars.Core.Interactions;
using OldScars.Core.Items;
using OldScars.Core.World;
using UnityEngine;
using UnityEngine.AI;

namespace OldScars.Core.ApplicationShell
{
    /// <summary>
    /// Development-only authored integration content. It is neither generated
    /// world truth nor a production settlement/materialization authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DevelopmentGameplayIntegrationFixture : MonoBehaviour
    {
        public const string ResourcePath = "Development/PFB_IntegratedGameplayFixture";
        private const float FootprintHalfExtent = 10f;
        private const float MaximumFootprintHeightRange = 3f;
        private const float MaximumFixtureSlope = 22f;
        private const float MinimumPlayerDistance = 24f;

        public Vector3 PlacementPosition { get; private set; }
        public float PlacementHeightRange { get; private set; }

        public void BindRuntime(PlayerGameplayComposition player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            BuildingVisibilityManager[] managers = GetComponentsInChildren<BuildingVisibilityManager>(true);
            for (int index = 0; index < managers.Length; index++)
                managers[index].BindRuntime(player.GameplayCamera, player.PlayerContext);
        }

        public bool TryValidate(out string failure)
        {
            failure = null;
            if (GetComponentsInChildren<DoorSwingController>(true).Length < 2)
                return Fail("Development fixture requires open/locked door coverage.", out failure);
            if (GetComponentsInChildren<ContainerLootComponent>(true).Length < 5)
                return Fail("Development fixture requires representative container/loot coverage.", out failure);
            if (GetComponentsInChildren<WorldItemPickup>(true).Length < 2)
                return Fail("Development fixture requires representative authored world items.", out failure);
            if (GetComponentsInChildren<ActorProfileComponent>(true).Length < 1)
                return Fail("Development fixture requires a representative authored actor.", out failure);

            var durableIds = new HashSet<string>(StringComparer.Ordinal);
            PersistentSceneObjectId[] identities = GetComponentsInChildren<PersistentSceneObjectId>(true);
            for (int index = 0; index < identities.Length; index++)
            {
                string id = identities[index].PersistentId;
                if (!PersistentSceneObjectId.IsValidFormat(id) || !durableIds.Add(id))
                    return Fail("Development fixture contains an invalid or duplicate PersistentSceneObjectId.", out failure);
            }
            return true;
        }

        public static bool TryInstantiateOnMaterializedLand(
            TerrainMaterializationResult materialization,
            Transform parent,
            PlayerGameplayComposition player,
            out DevelopmentGameplayIntegrationFixture fixture,
            out string failure)
        {
            fixture = null;
            failure = null;
            DevelopmentGameplayIntegrationFixture prefab =
                Resources.Load<DevelopmentGameplayIntegrationFixture>(ResourcePath);
            if (prefab == null)
                return Fail("Development gameplay fixture resource was not found.", out failure);
            if (materialization == null || materialization.Terrain == null || materialization.Plan == null)
                return Fail("Development fixture requires completed terrain materialization.", out failure);

            if (!TryFindPlacement(materialization, out Vector3 position, out float heightRange))
                return Fail("No safe terrestrial low-slope footprint was found for the development fixture.", out failure);

            DevelopmentGameplayIntegrationFixture instance = null;
            try
            {
                instance = Instantiate(prefab, position, Quaternion.identity, parent);
                instance.name = "Development Integrated Gameplay Fixture";
                instance.PlacementPosition = position;
                instance.PlacementHeightRange = heightRange;
                instance.BindRuntime(player);
                if (!instance.TryValidate(out failure))
                    throw new InvalidOperationException(failure);
                Physics.SyncTransforms();
                fixture = instance;
                return true;
            }
            catch (Exception exception)
            {
                failure = string.IsNullOrWhiteSpace(exception.Message)
                    ? exception.GetType().Name
                    : exception.Message;
                if (instance != null)
                {
                    if (Application.isPlaying) Destroy(instance.gameObject);
                    else DestroyImmediate(instance.gameObject);
                }
                return false;
            }
        }

        private static bool TryFindPlacement(
            TerrainMaterializationResult materialization,
            out Vector3 position,
            out float heightRange)
        {
            position = default;
            heightRange = 0f;
            Terrain terrain = materialization.Terrain;
            TerrainData data = terrain.terrainData;
            Vector3 origin = terrain.transform.position;
            float width = data.size.x;
            float length = data.size.z;
            float bestDistance = float.MaxValue;
            bool found = false;

            for (int zIndex = 2; zIndex <= 18; zIndex++)
            for (int xIndex = 2; xIndex <= 18; xIndex++)
            {
                float nx = xIndex / 20f;
                float nz = zIndex / 20f;
                if (materialization.Plan.IsOceanAtNormalized(nx, nz)) continue;
                Vector3 candidate = new Vector3(origin.x + nx * width, 0f, origin.z + nz * length);
                float playerDistance = Vector3.ProjectOnPlane(
                    candidate - materialization.SpawnPosition, Vector3.up).magnitude;
                if (playerDistance < MinimumPlayerDistance) continue;
                if (!TryMeasureFootprint(terrain, materialization.Plan, candidate,
                        out float groundY, out float range)) continue;
                if (!NavMesh.SamplePosition(
                        new Vector3(candidate.x, groundY, candidate.z), out NavMeshHit hit,
                        8f, NavMesh.AllAreas)) continue;

                float distance = Vector3.ProjectOnPlane(
                    hit.position - materialization.PathDestination, Vector3.up).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                position = new Vector3(candidate.x, groundY + 0.05f, candidate.z);
                heightRange = range;
                found = true;
            }
            return found;
        }

        private static bool TryMeasureFootprint(
            Terrain terrain,
            TerrainMaterializationPlan plan,
            Vector3 center,
            out float groundY,
            out float heightRange)
        {
            groundY = 0f;
            heightRange = 0f;
            Vector2[] offsets =
            {
                Vector2.zero,
                new Vector2(-FootprintHalfExtent, -FootprintHalfExtent),
                new Vector2(-FootprintHalfExtent, FootprintHalfExtent),
                new Vector2(FootprintHalfExtent, -FootprintHalfExtent),
                new Vector2(FootprintHalfExtent, FootprintHalfExtent)
            };
            float min = float.MaxValue;
            float max = float.MinValue;
            Vector3 terrainOrigin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            for (int index = 0; index < offsets.Length; index++)
            {
                Vector3 sample = new Vector3(center.x + offsets[index].x, 0f, center.z + offsets[index].y);
                float nx = (sample.x - terrainOrigin.x) / size.x;
                float nz = (sample.z - terrainOrigin.z) / size.z;
                if (nx <= 0f || nx >= 1f || nz <= 0f || nz >= 1f || plan.IsOceanAtNormalized(nx, nz))
                    return false;
                if (terrain.terrainData.GetSteepness(nx, nz) > MaximumFixtureSlope)
                    return false;
                float height = terrain.SampleHeight(sample) + terrainOrigin.y;
                min = Mathf.Min(min, height);
                max = Mathf.Max(max, height);
            }
            heightRange = max - min;
            groundY = max;
            return heightRange <= MaximumFootprintHeightRange;
        }

        private static bool Fail(string message, out string failure)
        {
            failure = message;
            return false;
        }
    }
}
