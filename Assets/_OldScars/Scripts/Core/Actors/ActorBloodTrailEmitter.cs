using System;
using UnityEngine;

namespace OldScars.Core.Actors
{
    /// <summary>
    /// Converts the existing medical bleeding state into distance-spaced visual
    /// requests. It never changes wounds, treatment, AI, perception or saves.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorMedicalStateComponent))]
    public sealed class ActorBloodTrailEmitter : MonoBehaviour
    {
        public const float MinBleedingThreshold = .01f;
        public const float MinSpacingMeters = .60f;
        public const float MaxSpacingMeters = 3f;
        public const float FullDensityBleedingRate = .25f;

        private const float RaycastHeight = 1.5f;
        private const float RaycastDistance = 4f;
        private const int SurfaceQueryBufferSize = 8;
        private const int MaximumMarksPerObservation = 8;

        private ActorMedicalStateComponent medical;
        private ActorHealthComponent health;
        private ActorRuntimeIdentity identity;
        private int observedMedicalRevision = int.MinValue;
        private float cachedBleeding;
        private float cachedSpacing = MaxSpacingMeters;
        private Vector3 lastPosition;
        private bool hasLastPosition;
        private float pendingDistance;
        private ulong emissionSequence;
        private readonly RaycastHit[] surfaceHits = new RaycastHit[SurfaceQueryBufferSize];

        public float CachedEffectiveBleedingRatePerGameHour => cachedBleeding;
        public float CurrentSpacingMeters => cachedSpacing;
        public int EmittedCount { get; private set; }
        public int SurfaceQuerySaturationCount { get; private set; }

        private void Awake()
        {
            medical = GetComponent<ActorMedicalStateComponent>();
            health = GetComponent<ActorHealthComponent>();
            identity = GetComponent<ActorRuntimeIdentity>();
        }

        private void Update() => ObservePosition(transform.position);

        public void ObservePositionForDiagnostics(Vector3 position) => ObservePosition(position);

        private void ObservePosition(Vector3 position)
        {
            RefreshMedicalCache();
            if (IsDead() || cachedBleeding < MinBleedingThreshold)
            {
                hasLastPosition = false;
                pendingDistance = 0f;
                return;
            }

            if (!hasLastPosition)
            {
                lastPosition = position;
                hasLastPosition = true;
                return;
            }

            Vector3 segment = position - lastPosition;
            float remaining = segment.magnitude;
            if (remaining <= .0001f)
                return;

            Vector3 cursor = lastPosition;
            int placedThisObservation = 0;
            while (pendingDistance + remaining >= cachedSpacing && placedThisObservation < MaximumMarksPerObservation)
            {
                float required = cachedSpacing - pendingDistance;
                float fraction = Mathf.Clamp01(required / remaining);
                cursor = Vector3.Lerp(cursor, position, fraction);
                remaining -= required;
                pendingDistance = 0f;
                TryEmit(cursor);
                placedThisObservation++;
            }
            pendingDistance += Mathf.Max(0f, remaining);
            lastPosition = position;
        }

        private void RefreshMedicalCache()
        {
            if (medical == null) medical = GetComponent<ActorMedicalStateComponent>();
            if (medical == null || observedMedicalRevision == medical.Revision) return;
            observedMedicalRevision = medical.Revision;
            cachedBleeding = medical.EffectiveBleedingRatePerGameHour;
            cachedSpacing = SpacingForBleeding(cachedBleeding);
        }

        private void TryEmit(Vector3 candidate)
        {
            if (!TryResolveSurface(candidate, out RaycastHit hit)) return;
            ulong seed = StableHash(ActorKey()) ^ (++emissionSequence * 0x9E3779B97F4A7C15UL);
            float scale = Mathf.Lerp(.85f, 1.15f, Unit(seed));
            float rotation = Unit(seed >> 17) * 360f;
            if (WorldBloodMarkPool.Ensure().TryPlace(hit.point, hit.normal, scale, rotation))
                EmittedCount++;
        }

        private bool TryResolveSurface(Vector3 candidate, out RaycastHit resolved)
        {
            int hitCount = Physics.RaycastNonAlloc(candidate + Vector3.up * RaycastHeight, Vector3.down,
                surfaceHits, RaycastDistance, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            if (hitCount == surfaceHits.Length) SurfaceQuerySaturationCount++;
            float nearestDistance = float.PositiveInfinity;
            resolved = default;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = surfaceHits[index];
                if (hit.collider == null || hit.collider.isTrigger || hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.distance >= nearestDistance) continue;
                nearestDistance = hit.distance;
                resolved = hit;
            }
            return nearestDistance < float.PositiveInfinity;
        }

        private bool IsDead()
        {
            if (health == null) health = GetComponent<ActorHealthComponent>();
            if (identity == null) identity = GetComponent<ActorRuntimeIdentity>();
            return health != null && health.IsDead || identity != null && identity.LifecycleState == ActorLifecycleState.Dead;
        }

        private string ActorKey() => identity != null && !string.IsNullOrEmpty(identity.ActorInstanceId)
            ? identity.ActorInstanceId : gameObject.name;

        public static float SpacingForBleeding(float effectiveBleedingRatePerGameHour)
        {
            if (float.IsNaN(effectiveBleedingRatePerGameHour) || float.IsInfinity(effectiveBleedingRatePerGameHour) ||
                effectiveBleedingRatePerGameHour < MinBleedingThreshold)
                return MaxSpacingMeters;
            float normalized = Mathf.InverseLerp(MinBleedingThreshold, FullDensityBleedingRate, effectiveBleedingRatePerGameHour);
            return Mathf.Lerp(MaxSpacingMeters, MinSpacingMeters, normalized);
        }

        private static ulong StableHash(string value)
        {
            ulong hash = 1469598103934665603UL;
            foreach (char character in value ?? string.Empty) { hash ^= character; hash *= 1099511628211UL; }
            return hash;
        }

        private static float Unit(ulong value) => (value & 0xFFFFFFUL) / 16777215f;
    }
}
