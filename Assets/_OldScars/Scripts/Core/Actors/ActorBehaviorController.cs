using UnityEngine;
using UnityEngine.AI;

namespace OldScars.Core.Actors
{
    public enum ActorBehaviorOwner
    {
        Ambient,
        Encounter,
        Search,
        Inactive
    }

    /// <summary>
    /// Single high-level owner of normal NPC navigation. Ambient selects bounded sandbox
    /// destinations here; encounter code may calculate movement but must submit it here.
    /// Lifecycle collapse and persistence remain authoritative technical interrupters.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorNavigationController))]
    public sealed class ActorBehaviorController : MonoBehaviour
    {
        private const float MinimumRadius = 2.5f;
        private const float MaximumRadius = 14f;
        private const float MinimumPauseSeconds = 1.25f;
        private const float MaximumPauseSeconds = 3f;
        private const float RetryDelay = 1.5f;
        private const int CandidateCount = 6;
        private const float CandidateSampleDistance = 3f;

        private ActorRuntimeIdentity identity;
        private ActorConditionComponent condition;
        private ActorNavigationController navigation;
        private bool ambientConfigured;
        private long ambientSeed;
        private long ambientDecisionSequence;
        private float nextAmbientDecisionTime;
        private Vector3 homeAnchor;
        private Vector3 lastTrackedPosition;

        public ActorBehaviorOwner Owner { get; private set; } = ActorBehaviorOwner.Ambient;
        public int OwnerRevision { get; private set; }
        public int AmbientAcceptedOrderCount { get; private set; }
        public int AmbientFailedDecisionCount { get; private set; }
        public string LastAmbientDecisionFailure { get; private set; }
        public Vector3 HomeAnchor => homeAnchor;
        public float MaximumRoamRadius => MaximumRadius;
        public float AmbientDistanceTravelled { get; private set; }
        public bool IsAmbientConfigured => ambientConfigured;

        private void Awake()
        {
            ResolveReferences();
            homeAnchor = transform.position;
            lastTrackedPosition = transform.position;
        }

        private void Update()
        {
            ResolveReferences();
            TrackAmbientTravel();

            if (!CanAct())
            {
                EnterInactive("Lifecycle or functional capacity prevents active behavior");
                return;
            }
            if (Owner == ActorBehaviorOwner.Inactive)
                SetOwner(ActorBehaviorOwner.Ambient, "Actor recovered active behavior capacity");
            if (Owner == ActorBehaviorOwner.Ambient && ambientConfigured)
                UpdateAmbient();
        }

        private void OnDisable()
        {
            if (navigation != null)
                navigation.Stop();
            Owner = ActorBehaviorOwner.Inactive;
        }

        public void ConfigureAmbient(long derivedSpawnSeed)
        {
            ResolveReferences();
            ambientSeed = derivedSpawnSeed;
            ambientDecisionSequence = 0L;
            ambientConfigured = true;
            homeAnchor = transform.position;
            lastTrackedPosition = transform.position;
            AmbientDistanceTravelled = 0f;
            nextAmbientDecisionTime = Time.time + 0.5f;
            if (CanAct() && Owner == ActorBehaviorOwner.Inactive)
                SetOwner(ActorBehaviorOwner.Ambient, "Ambient behavior configured");
        }

        public bool EnterEncounter(string reason)
        {
            ResolveReferences();
            if (!CanAct())
            {
                EnterInactive(reason);
                return false;
            }
            SetOwner(ActorBehaviorOwner.Encounter, reason);
            return true;
        }

        public void ExitEncounter(string reason)
        {
            ResolveReferences();
            if (!CanAct())
            {
                EnterInactive(reason);
                return;
            }
            if (Owner != ActorBehaviorOwner.Encounter)
                return;
            SetOwner(ActorBehaviorOwner.Ambient, reason);
            nextAmbientDecisionTime = Time.time + MinimumPauseSeconds;
        }

        public bool EnterSearch(string reason)
        {
            ResolveReferences();
            if (!CanAct())
            {
                EnterInactive(reason);
                return false;
            }
            if (Owner != ActorBehaviorOwner.Encounter)
                return false;
            SetOwner(ActorBehaviorOwner.Search, reason);
            return true;
        }

        public bool ReturnSearchToEncounter(string reason)
        {
            ResolveReferences();
            if (!CanAct())
            {
                EnterInactive(reason);
                return false;
            }
            if (Owner != ActorBehaviorOwner.Search)
                return false;
            SetOwner(ActorBehaviorOwner.Encounter, reason);
            return true;
        }

        public void ExitSearchToAmbient(string reason)
        {
            ResolveReferences();
            if (!CanAct())
            {
                EnterInactive(reason);
                return;
            }
            if (Owner != ActorBehaviorOwner.Search)
                return;
            SetOwner(ActorBehaviorOwner.Ambient, reason);
            nextAmbientDecisionTime = Time.time + MinimumPauseSeconds;
        }

        public void EnterInactive(string reason)
        {
            SetOwner(ActorBehaviorOwner.Inactive, reason);
        }

        public bool TryNavigateEncounter(Vector3 destination)
        {
            ResolveReferences();
            return Owner == ActorBehaviorOwner.Encounter && CanAct() &&
                   navigation != null && navigation.TryNavigate(destination, out _);
        }

        public void StopEncounterNavigation()
        {
            ResolveReferences();
            if (Owner == ActorBehaviorOwner.Encounter)
                navigation?.Stop();
        }

        public bool TryNavigateSearch(Vector3 destination)
        {
            ResolveReferences();
            return Owner == ActorBehaviorOwner.Search && CanAct() &&
                   navigation != null && navigation.TryNavigate(destination, out _);
        }

        public void StopSearchNavigation()
        {
            ResolveReferences();
            if (Owner == ActorBehaviorOwner.Search)
                navigation?.Stop();
        }

        private void UpdateAmbient()
        {
            if (navigation == null || Time.time < nextAmbientDecisionTime ||
                navigation.State == ActorNavigationState.Moving)
                return;

            long currentDecision = ambientDecisionSequence++;
            bool accepted = false;
            NavMeshAgent agent = navigation.Agent;
            string failure = "No bounded home-anchor NavMesh candidate was accepted. Home=" + homeAnchor +
                             "; Transform=" + transform.position +
                             "; BaseOffset=" + agent.baseOffset.ToString("0.###") + ".";
            for (int candidateIndex = 0; candidateIndex < CandidateCount && !accepted; candidateIndex++)
            {
                long domain = ActorLoadoutService.DeriveSandboxSpawnSeed(
                    ambientSeed, currentDecision * CandidateCount + candidateIndex,
                    identity.ActorProfileId, "roam");
                ulong mixed = unchecked((ulong)domain);
                float angle = (mixed & 0xffffUL) / 65535f * Mathf.PI * 2f;
                float radius = Mathf.Lerp(MinimumRadius, MaximumRadius,
                    ((mixed >> 16) & 0xffffUL) / 65535f);
                Vector3 candidate = homeAnchor +
                                    new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit,
                        CandidateSampleDistance, agent.areaMask))
                    continue;
                accepted = navigation.TryNavigate(hit.position, out ActorNavigationResult result);
                failure = result.Failure + ": " + result.Detail;
            }

            if (accepted)
            {
                AmbientAcceptedOrderCount++;
                LastAmbientDecisionFailure = null;
            }
            else
            {
                AmbientFailedDecisionCount++;
                LastAmbientDecisionFailure = failure;
            }

            ulong pauseSample = unchecked((ulong)ActorLoadoutService.DeriveSandboxSpawnSeed(
                ambientSeed, currentDecision, identity.ActorProfileId, "roam_pause"));
            float pause = Mathf.Lerp(MinimumPauseSeconds, MaximumPauseSeconds,
                (pauseSample & 0xffffUL) / 65535f);
            nextAmbientDecisionTime = Time.time + (accepted ? pause : RetryDelay);
        }

        private void TrackAmbientTravel()
        {
            Vector3 current = transform.position;
            if (ambientConfigured && Owner == ActorBehaviorOwner.Ambient && CanAct())
                AmbientDistanceTravelled += Vector3.ProjectOnPlane(current - lastTrackedPosition, Vector3.up).magnitude;
            lastTrackedPosition = current;
        }

        private void SetOwner(ActorBehaviorOwner next, string reason)
        {
            if (Owner == next)
                return;
            ActorBehaviorOwner previous = Owner;
            if (navigation != null &&
                (navigation.State != ActorNavigationState.Idle || navigation.HasDestination))
                navigation.Stop();
            Owner = next;
            OwnerRevision++;
            Debug.Log(
                "[AI][BEHAVIOR_OWNER]" +
                $"\n  Actor: {identity?.ActorInstanceId ?? "<NONE>"}" +
                $"\n  From: {previous}" +
                $"\n  To: {next}" +
                $"\n  Reason: {reason ?? "<UNKNOWN>"}");
        }

        private bool CanAct()
        {
            return identity != null && identity.IsRegistered &&
                   identity.LifecycleState == ActorLifecycleState.Alive &&
                   (condition == null || condition.CanPerformActiveActions);
        }

        private void ResolveReferences()
        {
            if (identity == null) identity = GetComponent<ActorRuntimeIdentity>();
            if (condition == null) condition = GetComponent<ActorConditionComponent>();
            if (navigation == null) navigation = GetComponent<ActorNavigationController>();
        }
    }
}
