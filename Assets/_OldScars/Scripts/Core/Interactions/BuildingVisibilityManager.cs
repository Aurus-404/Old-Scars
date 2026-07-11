using System;
using System.Collections.Generic;
using UnityEngine;

namespace OldScars.Core.Interactions
{
    public sealed class BuildingVisibilityManager : MonoBehaviour
    {
        private static readonly Vector3[] DebugSphereAxes =
        {
            Vector3.right,
            Vector3.up,
            Vector3.forward
        };

        [SerializeField] private Camera mainCamera;
        [SerializeField] private ActorInteractionContext playerContext;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private BuildingInteriorVolume[] interiorVolumes;
        [SerializeField] private BuildingOccluderTarget[] occluderTargets;
        [SerializeField] private LayerMask occluderRaycastMask = Physics.DefaultRaycastLayers;
        [SerializeField] private float restoreDelay = 0.15f;
        [SerializeField] private bool useSphereCasts = true;
        [SerializeField] private float sphereCastRadius = 0.35f;
        [SerializeField] private bool useCameraOverlap = true;
        [SerializeField] private float cameraOverlapRadius = 0.45f;
        [SerializeField] private bool drawDebugCasts = true;
        [SerializeField] private bool logHitChanges;
        [SerializeField] private float debugDrawDuration = 0.05f;
        [SerializeField] private float[] raycastVerticalOffsets = { 1.6f, 0.9f, 0.25f };

        private readonly Dictionary<BuildingOccluderTarget, float> lastOccludedTimes =
            new Dictionary<BuildingOccluderTarget, float>();

        private readonly HashSet<BuildingOccluderTarget> hitTargets =
            new HashSet<BuildingOccluderTarget>();

        private readonly HashSet<BuildingOccluderTarget> previousHitTargets =
            new HashSet<BuildingOccluderTarget>();

        private BuildingInteriorVolume currentVolume;
        private string currentBuildingId;

        private void Awake()
        {
            CacheReferences();
        }

        private void OnDisable()
        {
            RestoreAllManagedTargets();
        }

        private void LateUpdate()
        {
            if (!HasRequiredReferences())
                return;

            ResolveCurrentVolumeByFallback();

            if (currentVolume == null || string.IsNullOrWhiteSpace(currentBuildingId))
                return;

            UpdateCastHits();
            ApplyVisibilityForCurrentBuilding();
        }

        public void NotifyPlayerEntered(BuildingInteriorVolume volume, ActorInteractionContext actor)
        {
            if (volume == null || actor == null || actor != playerContext)
                return;

            SetCurrentVolume(volume);
        }

        public void NotifyPlayerExited(BuildingInteriorVolume volume, ActorInteractionContext actor)
        {
            if (volume == null || actor == null || actor != playerContext || volume != currentVolume)
                return;

            if (!volume.ContainsPlayer(playerContext))
                ClearCurrentVolume();
        }

        private void CacheReferences()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            if (playerContext == null)
                playerContext = FindAnyObjectByType<ActorInteractionContext>();

            if (playerTransform == null && playerContext != null)
                playerTransform = playerContext.transform;

            if (interiorVolumes == null || interiorVolumes.Length == 0)
                interiorVolumes = FindObjectsByType<BuildingInteriorVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (occluderTargets == null || occluderTargets.Length == 0)
                occluderTargets = FindObjectsByType<BuildingOccluderTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (occluderRaycastMask.value == 0)
                occluderRaycastMask = Physics.DefaultRaycastLayers;

            EnsureVerticalOffsets();
        }

        private bool HasRequiredReferences()
        {
            return mainCamera != null
                && playerContext != null
                && playerTransform != null
                && interiorVolumes != null
                && occluderTargets != null;
        }

        private void ResolveCurrentVolumeByFallback()
        {
            if (currentVolume != null)
            {
                if (currentVolume.ContainsPlayer(playerContext))
                    return;

                ClearCurrentVolume();
            }

            for (int index = 0; index < interiorVolumes.Length; index++)
            {
                BuildingInteriorVolume volume = interiorVolumes[index];
                if (volume == null || !volume.ContainsPlayer(playerContext))
                    continue;

                SetCurrentVolume(volume);
                return;
            }
        }

        private void SetCurrentVolume(BuildingInteriorVolume volume)
        {
            if (currentVolume == volume)
                return;

            RestoreAllManagedTargets();
            currentVolume = volume;
            currentBuildingId = volume != null ? volume.BuildingId : null;
            lastOccludedTimes.Clear();
            previousHitTargets.Clear();
        }

        private void ClearCurrentVolume()
        {
            RestoreAllManagedTargets();
            currentVolume = null;
            currentBuildingId = null;
            lastOccludedTimes.Clear();
            hitTargets.Clear();
            previousHitTargets.Clear();
        }

        private void UpdateCastHits()
        {
            hitTargets.Clear();

            Vector3 cameraPosition = mainCamera.transform.position;
            for (int index = 0; index < raycastVerticalOffsets.Length; index++)
            {
                Vector3 origin = playerTransform.position + Vector3.up * raycastVerticalOffsets[index];
                Vector3 toCamera = cameraPosition - origin;
                float distance = toCamera.magnitude;
                if (distance <= 0.01f)
                    continue;

                Vector3 direction = toCamera / distance;
                bool castHit = useSphereCasts
                    ? RegisterSphereCastHits(origin, direction, distance)
                    : RegisterRaycastHits(origin, direction, distance);

                DrawDebugCast(origin, toCamera, castHit);
            }

            if (useCameraOverlap)
                RegisterCameraOverlapHits(cameraPosition);

            LogHitChangesIfNeeded();
        }

        private bool RegisterSphereCastHits(Vector3 origin, Vector3 direction, float distance)
        {
            float safeRadius = Mathf.Max(0f, sphereCastRadius);
            if (safeRadius <= 0f)
                return RegisterRaycastHits(origin, direction, distance);

            RaycastHit[] hits = Physics.SphereCastAll(
                origin,
                safeRadius,
                direction,
                distance,
                occluderRaycastMask,
                QueryTriggerInteraction.Ignore);

            return RegisterHitTargets(hits);
        }

        private bool RegisterRaycastHits(Vector3 origin, Vector3 direction, float distance)
        {
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction,
                distance,
                occluderRaycastMask,
                QueryTriggerInteraction.Ignore);

            return RegisterHitTargets(hits);
        }

        private bool RegisterHitTargets(RaycastHit[] hits)
        {
            bool hasValidHit = false;

            if (hits == null)
                return false;

            for (int index = 0; index < hits.Length; index++)
            {
                Collider hitCollider = hits[index].collider;
                BuildingOccluderTarget target = hitCollider != null
                    ? hitCollider.GetComponentInParent<BuildingOccluderTarget>()
                    : null;

                if (!RegisterHitTarget(target))
                    continue;

                hasValidHit = true;
            }

            return hasValidHit;
        }

        private void RegisterCameraOverlapHits(Vector3 cameraPosition)
        {
            float safeRadius = Mathf.Max(0f, cameraOverlapRadius);
            if (safeRadius <= 0f)
                return;

            Collider[] overlappingColliders = Physics.OverlapSphere(
                cameraPosition,
                safeRadius,
                occluderRaycastMask,
                QueryTriggerInteraction.Ignore);

            bool hasValidOverlap = false;
            for (int index = 0; index < overlappingColliders.Length; index++)
            {
                Collider overlapCollider = overlappingColliders[index];
                BuildingOccluderTarget target = overlapCollider != null
                    ? overlapCollider.GetComponentInParent<BuildingOccluderTarget>()
                    : null;

                if (!RegisterHitTarget(target))
                    continue;

                hasValidOverlap = true;
                DrawDebugOverlapHit(cameraPosition, overlapCollider);
            }

            DrawDebugOverlap(cameraPosition, safeRadius, hasValidOverlap);
        }

        private bool RegisterHitTarget(BuildingOccluderTarget target)
        {
            if (!IsValidCastTarget(target))
                return false;

            hitTargets.Add(target);
            lastOccludedTimes[target] = Time.time;
            return true;
        }

        private void ApplyVisibilityForCurrentBuilding()
        {
            float now = Time.time;

            for (int index = 0; index < occluderTargets.Length; index++)
            {
                BuildingOccluderTarget target = occluderTargets[index];
                if (target == null)
                    continue;

                if (!IsSameBuilding(target))
                {
                    target.RestoreInitialState();
                    continue;
                }

                bool hideAlways = target.HideAlwaysWhenInside;
                bool hideByCast = ShouldStayHiddenByCast(target, now);

                if (hideAlways || hideByCast)
                    target.Hide();
                else
                    target.RestoreInitialState();
            }
        }

        private bool ShouldStayHiddenByCast(BuildingOccluderTarget target, float now)
        {
            if (!target.HideByCameraRaycast)
                return false;

            if (hitTargets.Contains(target))
                return true;

            if (!lastOccludedTimes.TryGetValue(target, out float lastOccludedTime))
                return false;

            bool withinDelay = now - lastOccludedTime <= Mathf.Max(0f, restoreDelay);
            if (!withinDelay)
                lastOccludedTimes.Remove(target);

            return withinDelay;
        }

        private bool IsValidCastTarget(BuildingOccluderTarget target)
        {
            if (target == null || !target.HideByCameraRaycast || !IsSameBuilding(target))
                return false;

            return target.GetComponentInParent<WorldObjectTags>() == null;
        }

        private bool IsSameBuilding(BuildingOccluderTarget target)
        {
            return target != null
                && string.Equals(target.BuildingId, currentBuildingId, StringComparison.Ordinal);
        }

        private void RestoreAllManagedTargets()
        {
            if (occluderTargets == null)
                return;

            for (int index = 0; index < occluderTargets.Length; index++)
            {
                BuildingOccluderTarget target = occluderTargets[index];
                if (target != null)
                    target.RestoreInitialState();
            }
        }

        private void DrawDebugCast(Vector3 origin, Vector3 ray, bool hasValidHit)
        {
            if (!drawDebugCasts || !Application.isPlaying)
                return;

            Debug.DrawRay(origin, ray, hasValidHit ? Color.red : Color.green, Mathf.Max(0f, debugDrawDuration));
        }

        private void DrawDebugOverlap(Vector3 center, float radius, bool hasValidOverlap)
        {
            if (!drawDebugCasts || !Application.isPlaying)
                return;

            Color color = hasValidOverlap ? Color.cyan : Color.blue;
            float duration = Mathf.Max(0f, debugDrawDuration);
            for (int index = 0; index < DebugSphereAxes.Length; index++)
            {
                Vector3 axis = DebugSphereAxes[index] * radius;
                Debug.DrawLine(center - axis, center + axis, color, duration);
            }
        }

        private void DrawDebugOverlapHit(Vector3 cameraPosition, Collider overlapCollider)
        {
            if (!drawDebugCasts || !Application.isPlaying || overlapCollider == null)
                return;

            Debug.DrawLine(cameraPosition, overlapCollider.bounds.center, Color.cyan, Mathf.Max(0f, debugDrawDuration));
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebugCasts || mainCamera == null || playerTransform == null)
                return;

            EnsureVerticalOffsets();

            Vector3 cameraPosition = mainCamera.transform.position;
            Gizmos.color = Color.green;
            for (int index = 0; index < raycastVerticalOffsets.Length; index++)
            {
                Vector3 origin = playerTransform.position + Vector3.up * raycastVerticalOffsets[index];
                Gizmos.DrawLine(origin, cameraPosition);

                if (useSphereCasts)
                    Gizmos.DrawWireSphere(origin, Mathf.Max(0f, sphereCastRadius));
            }

            if (useCameraOverlap)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(cameraPosition, Mathf.Max(0f, cameraOverlapRadius));
            }
        }

        private void LogHitChangesIfNeeded()
        {
            if (!logHitChanges)
            {
                CopyHitTargetsToPrevious();
                return;
            }

            for (int index = 0; index < occluderTargets.Length; index++)
            {
                BuildingOccluderTarget target = occluderTargets[index];
                if (target == null || !IsSameBuilding(target) || !target.HideByCameraRaycast)
                    continue;

                bool wasHit = previousHitTargets.Contains(target);
                bool isHit = hitTargets.Contains(target);
                if (wasHit == isHit)
                    continue;

                string state = isHit ? "hit" : "clear";
                Debug.Log($"[BuildingVisibilityManager] {state}: {target.name}");
            }

            CopyHitTargetsToPrevious();
        }

        private void CopyHitTargetsToPrevious()
        {
            previousHitTargets.Clear();
            foreach (BuildingOccluderTarget target in hitTargets)
                previousHitTargets.Add(target);
        }

        private void EnsureVerticalOffsets()
        {
            if (raycastVerticalOffsets == null || raycastVerticalOffsets.Length == 0)
                raycastVerticalOffsets = new[] { 1.6f, 0.9f, 0.25f };
        }

        private void OnValidate()
        {
            if (restoreDelay < 0f)
                restoreDelay = 0f;

            if (sphereCastRadius < 0f)
                sphereCastRadius = 0f;

            if (cameraOverlapRadius < 0f)
                cameraOverlapRadius = 0f;

            if (debugDrawDuration < 0f)
                debugDrawDuration = 0f;

            EnsureVerticalOffsets();
        }
    }
}
