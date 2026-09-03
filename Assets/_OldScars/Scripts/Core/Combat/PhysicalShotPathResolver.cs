using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OldScars.Core.Actors;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Interactions;
using UnityEngine;

namespace OldScars.Core.Combat
{
    /// <summary>
    /// Shared physical hitscan path used by player and AI firearm adapters.
    /// WeaponCombatService remains authoritative for ammo and consequences.
    /// </summary>
    public static class PhysicalShotPathResolver
    {
        private const float SurfaceContinuationEpsilon = 0.001f;
        private const int MaxPenetratedSurfaces = 4;

        public static PhysicalShotResolution Resolve(
            Transform shooter,
            Vector3 origin,
            Vector3 direction,
            float range,
            float penetrationPower,
            int layerMask = 0)
        {
            if (shooter == null || !Finite(origin) || !Finite(direction) || direction.sqrMagnitude <= 0.000001f ||
                !FiniteNonNegative(range) || !FiniteNonNegative(penetrationPower))
            {
                throw new ArgumentException("Physical shot path requires a shooter and finite non-negative values.");
            }

            direction.Normalize();
            var ignoredColliders = new HashSet<Collider>();
            var penetratedSurfaceOwners = new HashSet<WorldObjectProfileComponent>();
            Vector3 currentOrigin = origin;
            float remainingRange = range;
            float remainingPower = penetrationPower;
            int penetratedSurfaces = 0;
            PenetrationResolution lastSurface = default;

            while (remainingRange > 0f)
            {
                if (!TryNextHit(shooter, currentOrigin, direction, remainingRange, layerMask,
                        ignoredColliders, out RaycastHit hit))
                {
                    return new PhysicalShotResolution(
                        PhysicalShotTermination.Miss, null, currentOrigin + direction * remainingRange,
                        penetrationPower, remainingPower, penetratedSurfaces, lastSurface);
                }

                Collider collider = hit.collider;
                ActorHealthComponent actor = collider.GetComponentInParent<ActorHealthComponent>();
                if (actor != null)
                {
                    return new PhysicalShotResolution(
                        PhysicalShotTermination.Impact, collider, hit.point,
                        penetrationPower, remainingPower, penetratedSurfaces, lastSurface);
                }

                WorldObjectProfileComponent surface = collider.GetComponentInParent<WorldObjectProfileComponent>();
                if (surface == null || !surface.TryGetPenetrationProfile(out PenetrationProfileDefinition profile))
                {
                    return new PhysicalShotResolution(
                        PhysicalShotTermination.Impact, collider, hit.point,
                        penetrationPower, remainingPower, penetratedSurfaces, lastSurface);
                }

                if (penetratedSurfaceOwners.Contains(surface))
                {
                    ignoredColliders.Add(collider);
                    Advance(hit, direction, ref currentOrigin, ref remainingRange);
                    continue;
                }

                if (penetratedSurfaces >= MaxPenetratedSurfaces)
                {
                    return new PhysicalShotResolution(
                        PhysicalShotTermination.SurfaceLimitStopped, null, hit.point,
                        penetrationPower, remainingPower, penetratedSurfaces, lastSurface, profile.id);
                }

                lastSurface = PenetrationResolutionService.Resolve(
                    remainingPower,
                    new[]
                    {
                        new PenetrationLayer(
                            "world_surface_" + RuntimeHelpers.GetHashCode(surface),
                            profile.id,
                            0,
                            profile.resistance)
                    });
                if (lastSurface.Outcome == PenetrationOutcome.Stopped)
                {
                    return new PhysicalShotResolution(
                        PhysicalShotTermination.SurfaceStopped, null, hit.point,
                        penetrationPower, 0f, penetratedSurfaces, lastSurface, profile.id);
                }

                remainingPower = lastSurface.ResidualPower;
                penetratedSurfaces++;
                ignoredColliders.Add(collider);
                penetratedSurfaceOwners.Add(surface);
                Advance(hit, direction, ref currentOrigin, ref remainingRange);
            }

            return new PhysicalShotResolution(
                PhysicalShotTermination.Miss, null, currentOrigin,
                penetrationPower, remainingPower, penetratedSurfaces, lastSurface);
        }

        private static bool TryNextHit(
            Transform shooter,
            Vector3 origin,
            Vector3 direction,
            float range,
            int layerMask,
            ISet<Collider> ignoredColliders,
            out RaycastHit result)
        {
            int mask = layerMask != 0 ? layerMask : Physics.DefaultRaycastLayers;
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, range, mask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                Collider collider = hit.collider;
                if (collider == null || collider.transform == shooter || collider.transform.IsChildOf(shooter) ||
                    ignoredColliders.Contains(collider))
                    continue;
                ActorLocomotionCollider locomotion = collider.GetComponent<ActorLocomotionCollider>();
                if (locomotion != null && locomotion.HasExplicitCombatHitboxes)
                    continue;
                result = hit;
                return true;
            }

            result = default;
            return false;
        }

        private static void Advance(
            RaycastHit hit,
            Vector3 direction,
            ref Vector3 currentOrigin,
            ref float remainingRange)
        {
            float advance = hit.distance + SurfaceContinuationEpsilon;
            remainingRange = Mathf.Max(0f, remainingRange - advance);
            currentOrigin = hit.point + direction * SurfaceContinuationEpsilon;
        }

        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private static bool FiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }
}
