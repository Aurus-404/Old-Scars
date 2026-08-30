using System.Collections.Generic;
using UnityEngine;

namespace OldScars.Core.Actors
{
    public enum ActorVisualPerceptionReason
    {
        Perceived,
        NotConfigured,
        InvalidObserver,
        InvalidTarget,
        Self,
        ObserverDead,
        TargetDead,
        MissingTargetCollider,
        OutOfRange,
        OutsideFov,
        LineOfSightMiss,
        Occluded
    }

    public readonly struct ActorVisualPerceptionResult
    {
        public ActorVisualPerceptionResult(
            bool perceived,
            ActorVisualPerceptionReason reason,
            string observerId,
            string targetId,
            Vector3 observedPosition,
            float distance,
            float horizontalAngle,
            Collider blocker,
            double worldTimeSeconds)
        {
            Perceived = perceived;
            Reason = reason;
            ObserverId = observerId;
            TargetId = targetId;
            ObservedPosition = observedPosition;
            Distance = distance;
            HorizontalAngle = horizontalAngle;
            Blocker = blocker;
            WorldTimeSeconds = worldTimeSeconds;
        }

        public bool Perceived { get; }
        public ActorVisualPerceptionReason Reason { get; }
        public string ObserverId { get; }
        public string TargetId { get; }
        public Vector3 ObservedPosition { get; }
        public float Distance { get; }
        public float HorizontalAngle { get; }
        public Collider Blocker { get; }
        public double WorldTimeSeconds { get; }
        public bool HasWorldTime => !double.IsNaN(WorldTimeSeconds);
    }

    [DisallowMultipleComponent]
    public sealed class ActorVisualPerceptionService : MonoBehaviour
    {
        private const int InitialColliderBufferCapacity = 8;
        private const int InitialLineOfSightHitBufferCapacity = 16;

        private ActorRuntimeIdentity observer;
        private readonly List<Collider> targetColliderBuffer = new List<Collider>(InitialColliderBufferCapacity);
        private RaycastHit[] lineOfSightHitBuffer = new RaycastHit[InitialLineOfSightHitBufferCapacity];
        private bool configured;
        private float visualRange;
        private float horizontalFovDegrees;
        private float eyeHeight;
        private float recognitionNearSeconds;
        private float recognitionFarSeconds;
        private float recognitionDecaySeconds;

        public bool IsConfigured => configured;
        public float VisualRange => visualRange;
        public float HorizontalFovDegrees => horizontalFovDegrees;
        public float EyeHeight => eyeHeight;
        public float RecognitionNearSeconds => recognitionNearSeconds;
        public float RecognitionFarSeconds => recognitionFarSeconds;
        public float RecognitionDecaySeconds => recognitionDecaySeconds;
        public int TargetColliderBufferExpansionCount { get; private set; }
        public int LineOfSightFallbackCount { get; private set; }

        private void Awake()
        {
            observer = GetComponent<ActorRuntimeIdentity>();
        }

        public bool TryConfigure(
            float range,
            float horizontalFov,
            float observerEyeHeight,
            float nearRecognitionSeconds,
            float farRecognitionSeconds,
            float decaySeconds,
            out string error)
        {
            error = null;
            if (!FinitePositive(range) || !FinitePositive(horizontalFov) || horizontalFov > 360f ||
                !FinitePositive(observerEyeHeight) || !FinitePositive(nearRecognitionSeconds) ||
                !FinitePositive(farRecognitionSeconds) || farRecognitionSeconds <= nearRecognitionSeconds ||
                !FinitePositive(decaySeconds))
            {
                error = "Visual range, eye height and recognition times must be finite and positive; horizontal FOV must be within (0, 360] and far recognition must exceed near recognition.";
                return false;
            }
            visualRange = range;
            horizontalFovDegrees = horizontalFov;
            eyeHeight = observerEyeHeight;
            recognitionNearSeconds = nearRecognitionSeconds;
            recognitionFarSeconds = farRecognitionSeconds;
            recognitionDecaySeconds = decaySeconds;
            configured = true;
            return true;
        }

        public float RecognitionSecondsAtDistance(float distance)
        {
            float normalizedDistance = Mathf.Clamp01(distance / Mathf.Max(visualRange, 0.001f));
            return Mathf.Lerp(recognitionNearSeconds, recognitionFarSeconds, normalizedDistance);
        }

        public ActorVisualPerceptionResult Evaluate(ActorRuntimeIdentity target)
        {
            if (observer == null)
                observer = GetComponent<ActorRuntimeIdentity>();

            string observerId = observer != null ? observer.ActorInstanceId : null;
            string targetId = target != null ? target.ActorInstanceId : null;
            double worldTime = WorldClock.Current != null ? WorldClock.Current.ElapsedGameSeconds : double.NaN;
            if (!configured)
                return Result(false, ActorVisualPerceptionReason.NotConfigured, observerId, targetId, default, 0f, 0f, null, worldTime);
            if (observer == null || !observer.IsRegistered || !ActorRuntimeIdentity.IsValidFormat(observerId))
                return Result(false, ActorVisualPerceptionReason.InvalidObserver, observerId, targetId, default, 0f, 0f, null, worldTime);
            if (target == null || !target.IsRegistered || !ActorRuntimeIdentity.IsValidFormat(targetId))
                return Result(false, ActorVisualPerceptionReason.InvalidTarget, observerId, targetId, default, 0f, 0f, null, worldTime);
            if (ReferenceEquals(observer, target) || observerId == targetId)
                return Result(false, ActorVisualPerceptionReason.Self, observerId, targetId, transform.position, 0f, 0f, null, worldTime);
            if (observer.LifecycleState == ActorLifecycleState.Dead)
                return Result(false, ActorVisualPerceptionReason.ObserverDead, observerId, targetId, default, 0f, 0f, null, worldTime);
            if (target.LifecycleState == ActorLifecycleState.Dead)
                return Result(false, ActorVisualPerceptionReason.TargetDead, observerId, targetId, target.transform.position, 0f, 0f, null, worldTime);

            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Collider targetCollider = SelectTargetCollider(target, eye);
            if (targetCollider == null)
                return Result(false, ActorVisualPerceptionReason.MissingTargetCollider, observerId, targetId, target.transform.position, 0f, 0f, null, worldTime);

            Vector3 observed = targetCollider.bounds.center;
            Vector3 toTarget = observed - eye;
            float distance = toTarget.magnitude;
            Vector3 flatDirection = Vector3.ProjectOnPlane(toTarget, Vector3.up);
            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            float angle = flatDirection.sqrMagnitude <= 0.000001f || flatForward.sqrMagnitude <= 0.000001f
                ? 0f
                : Vector3.Angle(flatForward, flatDirection);
            if (distance > visualRange)
                return Result(false, ActorVisualPerceptionReason.OutOfRange, observerId, targetId, observed, distance, angle, null, worldTime);
            if (angle > horizontalFovDegrees * 0.5f)
                return Result(false, ActorVisualPerceptionReason.OutsideFov, observerId, targetId, observed, distance, angle, null, worldTime);
            if (distance <= 0.0001f)
                return Result(true, ActorVisualPerceptionReason.Perceived, observerId, targetId, observed, distance, angle, null, worldTime);

            int hitCount = Physics.RaycastNonAlloc(
                eye, toTarget / distance, lineOfSightHitBuffer, distance + 0.01f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            RaycastHit[] hits = lineOfSightHitBuffer;
            if (hitCount >= lineOfSightHitBuffer.Length)
            {
                // NonAlloc cannot prove it captured the nearest relevant hit when full.
                // Preserve LOS semantics by taking the exceptional allocating path instead.
                hits = Physics.RaycastAll(
                    eye, toTarget / distance, distance + 0.01f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                hitCount = hits.Length;
                LineOfSightFallbackCount++;
            }

            if (TryGetNearestRelevantHit(hits, hitCount, out RaycastHit nearestHit))
            {
                Collider hit = nearestHit.collider;
                ActorRuntimeIdentity hitActor = hit.GetComponentInParent<ActorRuntimeIdentity>();
                if (ReferenceEquals(hitActor, target))
                    return Result(true, ActorVisualPerceptionReason.Perceived, observerId, targetId, observed, distance, angle, null, worldTime);
                return Result(false, ActorVisualPerceptionReason.Occluded, observerId, targetId, observed, distance, angle, hit, worldTime);
            }

            return Result(false, ActorVisualPerceptionReason.LineOfSightMiss, observerId, targetId, observed, distance, angle, null, worldTime);
        }

        private Collider SelectTargetCollider(ActorRuntimeIdentity target, Vector3 observerEye)
        {
            int previousCapacity = targetColliderBuffer.Capacity;
            targetColliderBuffer.Clear();
            target.GetComponentsInChildren(false, targetColliderBuffer);
            if (targetColliderBuffer.Capacity > previousCapacity)
                TargetColliderBufferExpansionCount++;

            Collider selected = null;
            float closestDistanceSquared = float.PositiveInfinity;
            for (int index = 0; index < targetColliderBuffer.Count; index++)
            {
                Collider collider = targetColliderBuffer[index];
                if (collider == null || !collider.enabled || collider.isTrigger)
                    continue;

                float distanceSquared = (collider.bounds.center - observerEye).sqrMagnitude;
                if (distanceSquared >= closestDistanceSquared)
                    continue;
                selected = collider;
                closestDistanceSquared = distanceSquared;
            }
            return selected;
        }

        private bool TryGetNearestRelevantHit(RaycastHit[] hits, int hitCount, out RaycastHit nearestHit)
        {
            nearestHit = default;
            float nearestDistance = float.PositiveInfinity;
            bool found = false;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit candidate = hits[index];
                Collider collider = candidate.collider;
                if (collider == null || collider.transform == transform || collider.transform.IsChildOf(transform) ||
                    candidate.distance >= nearestDistance)
                    continue;
                nearestHit = candidate;
                nearestDistance = candidate.distance;
                found = true;
            }
            return found;
        }

        private static ActorVisualPerceptionResult Result(
            bool perceived,
            ActorVisualPerceptionReason reason,
            string observerId,
            string targetId,
            Vector3 observedPosition,
            float distance,
            float angle,
            Collider blocker,
            double worldTime)
        {
            return new ActorVisualPerceptionResult(
                perceived, reason, observerId, targetId, observedPosition, distance, angle, blocker, worldTime);
        }

        private static bool FinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }
}
