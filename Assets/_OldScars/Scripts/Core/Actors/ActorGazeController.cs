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

        private ActorRuntimeIdentity identity;
        private ActorConditionComponent condition;
        private ActorBehaviorController behavior;
        private bool configured;
        private long seed;
        private long ambientSequence;
        private float nextAmbientDecisionTime;
        private float candidateExpiresAt;

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
            if (!TrySetObservedDirection(observation.ObservedPosition, ActorAttentionMode.Candidate))
                return false;
            candidateExpiresAt = Time.time + CandidateHoldSeconds;
            return true;
        }

        public bool TryAttendEncounter(ActorVisualPerceptionResult observation)
        {
            ResolveReferences();
            return CanAct() && behavior?.Owner == ActorBehaviorOwner.Encounter &&
                   IsOwnPerceivedObservation(observation) &&
                   TrySetObservedDirection(observation.ObservedPosition, ActorAttentionMode.Encounter);
        }

        public bool TryAttendLostContact(Vector3 lastKnownPosition)
        {
            ResolveReferences();
            return CanAct() && behavior?.Owner == ActorBehaviorOwner.Encounter &&
                   TrySetObservedDirection(lastKnownPosition, ActorAttentionMode.LostContact);
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
            SetMode(ActorAttentionMode.Inactive);
            LastAngularStepDegrees = 0f;
        }

        private bool TrySetObservedDirection(Vector3 observedPosition, ActorAttentionMode mode)
        {
            Vector3 direction = Vector3.ProjectOnPlane(observedPosition - transform.position, Vector3.up);
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
                   Finite(observation.ObservedPosition);
        }

        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private void EnterAmbient(bool delayNextDecision)
        {
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
