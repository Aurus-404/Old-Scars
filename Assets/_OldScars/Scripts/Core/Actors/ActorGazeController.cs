using UnityEngine;

namespace OldScars.Core.Actors
{
    public enum ActorAttentionMode
    {
        Ambient,
        Candidate,
        Encounter,
        LostContact,
        Inactive
    }

    /// <summary>
    /// Logical visual-attention authority. It owns no perception, target discovery,
    /// navigation, combat, body rotation or presentation transform.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorBehaviorController))]
    public sealed class ActorGazeController : MonoBehaviour
    {
        private const float MaximumBodyRelativeYawDegrees = 65f;
        private const float AngularSpeedDegreesPerSecond = 90f;
        private const float MinimumAmbientYawDegrees = 18f;
        private const float MaximumAmbientYawDegrees = 52f;
        private const float MinimumAmbientHoldSeconds = 1.25f;
        private const float MaximumAmbientHoldSeconds = 2.4f;
        private const float CandidateHoldSeconds = 0.75f;
        private const float InitialAmbientDelaySeconds = 0.35f;
        private const float MinimumObservationDeltaSeconds = 0.04f;
        private const float MaximumObservationDeltaSeconds = 0.75f;
        private const float MaximumEstimatedObservedSpeed = 8f;
        private const float MaximumAcceptedRawObservedSpeed = 16f;
        private const float PredictionLookAheadSeconds = 0.12f;
        private const float MaximumPredictionHorizonSeconds = 0.35f;
        private const float MaximumPredictionLeadDistance = 1.5f;

        private ActorRuntimeIdentity identity;
        private ActorConditionComponent condition;
        private ActorBehaviorController behavior;
        private bool configured;
        private long seed;
        private long ambientSequence;
        private float nextAmbientDecisionTime;
        private float candidateExpiresAt;
        private string trackedTargetId;
        private Vector3 lastObservedPosition;
        private Vector3 estimatedObservedVelocity;
        private double lastObservationTime = double.NegativeInfinity;
        private bool hasObservedVelocity;
        private int trackedObservationSampleCount;

        public ActorAttentionMode Mode { get; private set; } = ActorAttentionMode.Inactive;
        public Vector3 CurrentGazeDirection { get; private set; } = Vector3.forward;
        public Vector3 DesiredGazeDirection { get; private set; } = Vector3.forward;
        public float MaximumBodyRelativeYaw => MaximumBodyRelativeYawDegrees;
        public float AngularSpeed => AngularSpeedDegreesPerSecond;
        public float AngularError => Vector3.Angle(CurrentGazeDirection, DesiredGazeDirection);
        public float CurrentBodyRelativeYaw => SignedBodyYaw(CurrentGazeDirection);
        public float LastAngularStepDegrees { get; private set; }
        public int AttentionRevision { get; private set; }
        public int AmbientDecisionCount { get; private set; }
        public bool IsConfigured => configured;
        public string TrackedTargetId => trackedTargetId;
        public Vector3 LastObservedPosition => lastObservedPosition;
        public Vector3 EstimatedObservedVelocity => estimatedObservedVelocity;
        public Vector3 PredictedAttentionPoint { get; private set; }
        public bool HasObservedVelocity => hasObservedVelocity;
        public bool IsPredictionActive { get; private set; }
        public double LastObservationTime => lastObservationTime;
        public float LastObservationDeltaSeconds { get; private set; }
        public float CurrentPredictionHorizonSeconds { get; private set; }
        public float CurrentPredictionLeadDistance { get; private set; }
        public int TrackedObservationSampleCount => trackedObservationSampleCount;
        public int TrackingRevision { get; private set; }
        public float MinimumValidObservationDelta => MinimumObservationDeltaSeconds;
        public float MaximumValidObservationDelta => MaximumObservationDeltaSeconds;
        public float MaximumEstimatedSpeed => MaximumEstimatedObservedSpeed;
        public float MaximumAcceptedRawSpeed => MaximumAcceptedRawObservedSpeed;
        public float PredictionLookAhead => PredictionLookAheadSeconds;
        public float MaximumPredictionHorizon => MaximumPredictionHorizonSeconds;
        public float MaximumPredictionLead => MaximumPredictionLeadDistance;

        private void Awake()
        {
            ResolveReferences();
            Vector3 forward = FlatForward();
            CurrentGazeDirection = forward;
            DesiredGazeDirection = forward;
        }

        private void Update()
        {
            ResolveReferences();
            if (!configured)
                return;
            if (!CanAct() || behavior?.Owner == ActorBehaviorOwner.Inactive)
            {
                EnterInactive();
                return;
            }

            if (Mode == ActorAttentionMode.Inactive)
                EnterAmbient(true);

            if (behavior != null && behavior.Owner == ActorBehaviorOwner.Ambient)
            {
                if (Mode == ActorAttentionMode.Encounter || Mode == ActorAttentionMode.LostContact ||
                    Mode == ActorAttentionMode.Candidate && Time.time >= candidateExpiresAt)
                    EnterAmbient(true);
                if (Mode == ActorAttentionMode.Ambient && Time.time >= nextAmbientDecisionTime)
                    SelectNextAmbientDirection();
            }

            if (Mode == ActorAttentionMode.Candidate || Mode == ActorAttentionMode.Encounter ||
                Mode == ActorAttentionMode.LostContact)
                UpdatePredictedAttention(Time.timeAsDouble);

            DesiredGazeDirection = ClampToBody(DesiredGazeDirection);
            CurrentGazeDirection = ClampToBody(CurrentGazeDirection);
            Vector3 previous = CurrentGazeDirection;
            float maximumRadians = AngularSpeedDegreesPerSecond * Mathf.Deg2Rad * Time.deltaTime;
            CurrentGazeDirection = Vector3.RotateTowards(
                CurrentGazeDirection, DesiredGazeDirection, maximumRadians, 0f).normalized;
            LastAngularStepDegrees = Vector3.Angle(previous, CurrentGazeDirection);
        }

        public void Configure(long deterministicSeed)
        {
            ResolveReferences();
            seed = deterministicSeed;
            ambientSequence = 0L;
            AmbientDecisionCount = 0;
            configured = true;
            Vector3 forward = FlatForward();
            CurrentGazeDirection = forward;
            DesiredGazeDirection = forward;
            candidateExpiresAt = 0f;
            nextAmbientDecisionTime = Time.time + InitialAmbientDelaySeconds;
            ClearTrackingHistory();
            SetMode(CanAct() ? ActorAttentionMode.Ambient : ActorAttentionMode.Inactive);
        }

        public void ConfigureFromIdentity()
        {
            ResolveReferences();
            Configure(StableSeed(identity?.ActorInstanceId));
        }

        public bool TryAttendCandidate(ActorVisualPerceptionResult observation)
        {
            ResolveReferences();
            if (!CanAct() || behavior?.Owner != ActorBehaviorOwner.Ambient ||
                !IsOwnPerceivedObservation(observation))
                return false;
            if (!AcceptObservedTrackingSample(observation, Time.timeAsDouble) ||
                !TrySetAttentionPoint(PredictedAttentionPoint, ActorAttentionMode.Candidate))
                return false;
            candidateExpiresAt = Time.time + CandidateHoldSeconds;
            return true;
        }

        public bool TryAttendEncounter(ActorVisualPerceptionResult observation)
        {
            ResolveReferences();
            return CanAct() && behavior?.Owner == ActorBehaviorOwner.Encounter &&
                   IsOwnPerceivedObservation(observation) &&
                   AcceptObservedTrackingSample(observation, Time.timeAsDouble) &&
                   TrySetAttentionPoint(PredictedAttentionPoint, ActorAttentionMode.Encounter);
        }

        public bool TryAttendLostContact(string targetId, Vector3 lastKnownPosition)
        {
            ResolveReferences();
            if (!CanAct() || behavior?.Owner != ActorBehaviorOwner.Encounter ||
                string.IsNullOrWhiteSpace(targetId) || !Finite(lastKnownPosition))
                return false;
            if (!string.Equals(trackedTargetId, targetId, System.StringComparison.Ordinal))
                ResetTrackingToRetainedPosition(targetId, lastKnownPosition);
            else
                lastObservedPosition = lastKnownPosition;
            UpdatePredictedAttention(Time.timeAsDouble);
            return TrySetAttentionPoint(PredictedAttentionPoint, ActorAttentionMode.LostContact);
        }

        public void ReleaseEncounterAttention()
        {
            ResolveReferences();
            if (!CanAct())
            {
                EnterInactive();
                return;
            }
            if (behavior?.Owner == ActorBehaviorOwner.Ambient)
                EnterAmbient(true);
        }

        public void EnterInactive()
        {
            if (Mode == ActorAttentionMode.Inactive)
                return;
            ClearTrackingHistory();
            SetMode(ActorAttentionMode.Inactive);
            LastAngularStepDegrees = 0f;
        }

        private bool TrySetAttentionPoint(Vector3 attentionPoint, ActorAttentionMode mode)
        {
            if (!Finite(attentionPoint))
                return false;
            Vector3 direction = Vector3.ProjectOnPlane(attentionPoint - transform.position, Vector3.up);
            if (direction.sqrMagnitude <= 0.000001f)
                return false;
            DesiredGazeDirection = ClampToBody(direction.normalized);
            SetMode(mode);
            return true;
        }

        private bool IsOwnPerceivedObservation(ActorVisualPerceptionResult observation)
        {
            return observation.Perceived && identity != null &&
                   observation.ObserverId == identity.ActorInstanceId &&
                   !string.IsNullOrWhiteSpace(observation.TargetId) &&
                   Finite(observation.ObservedPosition);
        }

        private bool AcceptObservedTrackingSample(ActorVisualPerceptionResult observation, double receivedTime)
        {
            if (!Finite(receivedTime))
                return false;
            if (!string.Equals(trackedTargetId, observation.TargetId, System.StringComparison.Ordinal))
            {
                ResetTrackingToObservation(observation.TargetId, observation.ObservedPosition, receivedTime);
                return true;
            }

            double elapsed = receivedTime - lastObservationTime;
            if (elapsed < MinimumObservationDeltaSeconds)
            {
                if ((observation.ObservedPosition - lastObservedPosition).sqrMagnitude > 0.000001f)
                    ResetTrackingToObservation(observation.TargetId, observation.ObservedPosition, receivedTime);
                else
                    UpdatePredictedAttention(receivedTime);
                return true;
            }
            if (elapsed > MaximumObservationDeltaSeconds || double.IsNaN(elapsed) || double.IsInfinity(elapsed))
            {
                ResetTrackingToObservation(observation.TargetId, observation.ObservedPosition, receivedTime);
                return true;
            }

            Vector3 displacement = Vector3.ProjectOnPlane(
                observation.ObservedPosition - lastObservedPosition, Vector3.up);
            float deltaSeconds = (float)elapsed;
            Vector3 rawVelocity = displacement / deltaSeconds;
            if (!Finite(rawVelocity) || rawVelocity.magnitude > MaximumAcceptedRawObservedSpeed)
            {
                ResetTrackingToObservation(observation.TargetId, observation.ObservedPosition, receivedTime);
                return true;
            }

            estimatedObservedVelocity = Vector3.ClampMagnitude(rawVelocity, MaximumEstimatedObservedSpeed);
            hasObservedVelocity = true;
            lastObservedPosition = observation.ObservedPosition;
            lastObservationTime = receivedTime;
            LastObservationDeltaSeconds = deltaSeconds;
            trackedObservationSampleCount++;
            TrackingRevision++;
            UpdatePredictedAttention(receivedTime);
            return true;
        }

        private void UpdatePredictedAttention(double now)
        {
            if (string.IsNullOrWhiteSpace(trackedTargetId))
                return;
            if (!hasObservedVelocity || double.IsNegativeInfinity(lastObservationTime))
            {
                PredictedAttentionPoint = lastObservedPosition;
                CurrentPredictionHorizonSeconds = 0f;
                CurrentPredictionLeadDistance = 0f;
                IsPredictionActive = false;
                SetDesiredDirectionFromAttentionPoint(PredictedAttentionPoint);
                return;
            }

            float observationAge = Mathf.Max(0f, (float)(now - lastObservationTime));
            float requestedHorizon = observationAge + PredictionLookAheadSeconds;
            float horizon = Mathf.Min(requestedHorizon, MaximumPredictionHorizonSeconds);
            Vector3 lead = Vector3.ClampMagnitude(
                estimatedObservedVelocity * horizon, MaximumPredictionLeadDistance);
            PredictedAttentionPoint = lastObservedPosition + lead;
            CurrentPredictionHorizonSeconds = horizon;
            CurrentPredictionLeadDistance = lead.magnitude;
            IsPredictionActive = requestedHorizon < MaximumPredictionHorizonSeconds &&
                                 lead.sqrMagnitude > 0.000001f;
            SetDesiredDirectionFromAttentionPoint(PredictedAttentionPoint);
        }

        private void SetDesiredDirectionFromAttentionPoint(Vector3 attentionPoint)
        {
            Vector3 direction = Vector3.ProjectOnPlane(attentionPoint - transform.position, Vector3.up);
            if (direction.sqrMagnitude > 0.000001f)
                DesiredGazeDirection = ClampToBody(direction.normalized);
        }

        private void ResetTrackingToObservation(string targetId, Vector3 observedPosition, double receivedTime)
        {
            trackedTargetId = targetId;
            lastObservedPosition = observedPosition;
            estimatedObservedVelocity = Vector3.zero;
            lastObservationTime = receivedTime;
            hasObservedVelocity = false;
            trackedObservationSampleCount = 1;
            LastObservationDeltaSeconds = 0f;
            PredictedAttentionPoint = observedPosition;
            CurrentPredictionHorizonSeconds = 0f;
            CurrentPredictionLeadDistance = 0f;
            IsPredictionActive = false;
            TrackingRevision++;
        }

        private void ResetTrackingToRetainedPosition(string targetId, Vector3 retainedPosition)
        {
            trackedTargetId = targetId;
            lastObservedPosition = retainedPosition;
            estimatedObservedVelocity = Vector3.zero;
            lastObservationTime = double.NegativeInfinity;
            hasObservedVelocity = false;
            trackedObservationSampleCount = 0;
            LastObservationDeltaSeconds = 0f;
            PredictedAttentionPoint = retainedPosition;
            CurrentPredictionHorizonSeconds = 0f;
            CurrentPredictionLeadDistance = 0f;
            IsPredictionActive = false;
            TrackingRevision++;
        }

        private void ClearTrackingHistory()
        {
            trackedTargetId = null;
            lastObservedPosition = default;
            estimatedObservedVelocity = Vector3.zero;
            lastObservationTime = double.NegativeInfinity;
            hasObservedVelocity = false;
            trackedObservationSampleCount = 0;
            LastObservationDeltaSeconds = 0f;
            PredictedAttentionPoint = default;
            CurrentPredictionHorizonSeconds = 0f;
            CurrentPredictionLeadDistance = 0f;
            IsPredictionActive = false;
            TrackingRevision++;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private void EnterAmbient(bool delayNextDecision)
        {
            ClearTrackingHistory();
            SetMode(ActorAttentionMode.Ambient);
            candidateExpiresAt = 0f;
            if (delayNextDecision)
                nextAmbientDecisionTime = Time.time + InitialAmbientDelaySeconds;
        }

        private void SelectNextAmbientDirection()
        {
            ulong mixed = Mix(unchecked((ulong)seed) +
                              unchecked((ulong)ambientSequence) * 0x9E3779B97F4A7C15UL);
            bool lookLeft = ((mixed >> 32) & 1UL) == 0UL;
            if ((ambientSequence & 1L) != 0L)
                lookLeft = !lookLeft;
            float magnitude = Mathf.Lerp(
                MinimumAmbientYawDegrees, MaximumAmbientYawDegrees,
                (mixed & 0xffffUL) / 65535f);
            float yaw = lookLeft ? -magnitude : magnitude;
            DesiredGazeDirection = Quaternion.AngleAxis(yaw, Vector3.up) * FlatForward();
            float hold = Mathf.Lerp(
                MinimumAmbientHoldSeconds, MaximumAmbientHoldSeconds,
                ((mixed >> 16) & 0xffffUL) / 65535f);
            ambientSequence++;
            AmbientDecisionCount++;
            nextAmbientDecisionTime = Time.time + hold;
        }

        private Vector3 ClampToBody(Vector3 direction)
        {
            Vector3 body = FlatForward();
            Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (flat.sqrMagnitude <= 0.000001f)
                return body;
            float signedYaw = Vector3.SignedAngle(body, flat.normalized, Vector3.up);
            return Quaternion.AngleAxis(
                Mathf.Clamp(signedYaw, -MaximumBodyRelativeYawDegrees, MaximumBodyRelativeYawDegrees),
                Vector3.up) * body;
        }

        private float SignedBodyYaw(Vector3 direction)
        {
            Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up);
            return flat.sqrMagnitude <= 0.000001f
                ? 0f
                : Vector3.SignedAngle(FlatForward(), flat.normalized, Vector3.up);
        }

        private Vector3 FlatForward()
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            return forward.sqrMagnitude <= 0.000001f ? Vector3.forward : forward.normalized;
        }

        private bool CanAct()
        {
            return identity != null && identity.IsRegistered &&
                   identity.LifecycleState == ActorLifecycleState.Alive &&
                   (condition == null || condition.CanPerformActiveActions);
        }

        private void SetMode(ActorAttentionMode next)
        {
            if (Mode == next)
                return;
            Mode = next;
            AttentionRevision++;
        }

        private void ResolveReferences()
        {
            if (identity == null) identity = GetComponent<ActorRuntimeIdentity>();
            if (condition == null) condition = GetComponent<ActorConditionComponent>();
            if (behavior == null) behavior = GetComponent<ActorBehaviorController>();
        }

        private static long StableSeed(string value)
        {
            unchecked
            {
                ulong hash = 1469598103934665603UL;
                string safe = value ?? string.Empty;
                for (int index = 0; index < safe.Length; index++)
                {
                    hash ^= safe[index];
                    hash *= 1099511628211UL;
                }
                return (long)hash;
            }
        }

        private static ulong Mix(ulong value)
        {
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }
}
