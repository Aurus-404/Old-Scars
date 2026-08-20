using System;
using System.Linq;
using OldScars.Core.Combat;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Items;
using UnityEngine;
using UnityEngine.AI;

namespace OldScars.Core.Actors
{
    public enum HumanEncounterAIState
    {
        Idle,
        Alerted,
        Avoiding,
        Fleeing,
        Fighting,
        LostContact,
        Inactive
    }

    public enum HumanEncounterResponse
    {
        Avoid,
        Flee,
        Fight
    }

    [DisallowMultipleComponent]
    public sealed class HumanEncounterAIController : MonoBehaviour
    {
        private const float DestinationProjectionDistance = 3f;

        private ActorRuntimeIdentity identity;
        private ActorHealthComponent health;
        private ActorNavigationController navigation;
        private ActorVisualPerceptionService perception;
        private ActorItemOwnershipComponent ownership;
        private ActorRuntimeIdentity threat;
        private HumanEncounterResponse configuredResponse;
        private HumanEncounterResponse? responseOverride;
        private bool configured;
        private bool hasLastKnownPosition;
        private Vector3 lastKnownPosition;
        private double lastSeenTime;
        private double stateEnteredTime;
        private double nextDecisionTime;
        private double nextAttackTime;
        private bool reloadPending;
        private double reloadCompletionTime;
        private string reloadWeaponInstanceId;
        private bool navigationFailureLatched;
        private Vector3 failedPlanActorPosition;
        private Vector3 failedPlanThreatPosition;
        private HumanEncounterResponse failedPlanResponse;
        private Vector3 lastPlannedThreatPosition;
        private bool hasPlan;

        private float alertDuration;
        private float lostContactTimeout;
        private float avoidDistance;
        private float fleeDistance;
        private float preferredCombatDistance;
        private float decisionInterval;
        private float replanDistance;

        public HumanEncounterAIState State { get; private set; } = HumanEncounterAIState.Idle;
        public HumanEncounterResponse Response => responseOverride ?? configuredResponse;
        public ActorRuntimeIdentity Threat => threat;
        public string ThreatActorInstanceId => threat != null ? threat.ActorInstanceId : null;
        public bool HasLastKnownPosition => hasLastKnownPosition;
        public Vector3 LastKnownPosition => lastKnownPosition;
        public double LastSeenTime => lastSeenTime;
        public bool IsConfigured => configured;
        public bool IsReloadPending => reloadPending;
        public bool NavigationFailureLatched => navigationFailureLatched;
        public int TransitionRevision { get; private set; }
        public int AttackCount { get; private set; }
        public int ReloadCount { get; private set; }
        public int NavigationPlanAttemptCount { get; private set; }
        public ActorVisualPerceptionResult LastPerception { get; private set; }
        public WeaponCombatResult LastCombatResult { get; private set; }

        private void Awake() => ResolveReferences();

        private void Update()
        {
            ResolveReferences();
            if (!configured)
                return;
            if (identity == null || health == null || identity.LifecycleState == ActorLifecycleState.Dead || health.IsDead)
            {
                EnterInactive("Actor lifecycle is Dead");
                return;
            }

            double now = Time.timeAsDouble;
            if (now < nextDecisionTime)
                return;
            nextDecisionTime = now + decisionInterval;
            EvaluateEncounter(now);
        }

        private void OnDisable()
        {
            CancelActiveAction();
            navigation?.Stop();
            threat = null;
            responseOverride = null;
            ClearEncounterMemory();
            ResetPlanLatch();
            State = HumanEncounterAIState.Inactive;
        }

        public bool TryConfigure(ActorProfileEncounterAI profile, out string error)
        {
            ResolveReferences();
            error = null;
            if (profile == null || !TryParseResponse(profile.response_policy, out HumanEncounterResponse response) ||
                !FinitePositive(profile.alert_duration_seconds) || !FinitePositive(profile.lost_contact_timeout_seconds) ||
                !FinitePositive(profile.avoid_distance) || !FinitePositive(profile.flee_distance) ||
                profile.flee_distance <= profile.avoid_distance || !FinitePositive(profile.preferred_combat_distance) ||
                !FinitePositive(profile.decision_interval_seconds) || !FinitePositive(profile.replan_distance))
            {
                error = "Encounter AI requires a canonical response policy and finite positive tuning; flee_distance must exceed avoid_distance.";
                return false;
            }
            if (identity == null || health == null || navigation?.IsConfigured != true || perception?.IsConfigured != true || ownership == null)
            {
                error = "Encounter AI requires identity, health, configured Navigation and Perception, and item ownership.";
                return false;
            }

            configuredResponse = response;
            alertDuration = profile.alert_duration_seconds;
            lostContactTimeout = profile.lost_contact_timeout_seconds;
            avoidDistance = profile.avoid_distance;
            fleeDistance = profile.flee_distance;
            preferredCombatDistance = profile.preferred_combat_distance;
            decisionInterval = profile.decision_interval_seconds;
            replanDistance = profile.replan_distance;
            configured = true;
            ResetEncounter("Profile configured");
            return true;
        }

        public bool TryAssignThreat(ActorRuntimeIdentity target, out string error)
        {
            error = null;
            if (!configured)
            {
                error = "Encounter AI is not configured.";
                return false;
            }
            if (target == null || !target.IsRegistered || target == identity || target.LifecycleState == ActorLifecycleState.Dead)
            {
                error = "Threat must be a distinct registered living actor.";
                return false;
            }
            if (threat == target)
                return true;
            threat = target;
            responseOverride = null;
            ClearEncounterMemory();
            CancelActiveAction();
            navigation.Stop();
            Transition(HumanEncounterAIState.Idle, "Threat assigned; awaiting perception");
            nextDecisionTime = 0d;
            Debug.Log($"[AI][THREAT_ASSIGNED]\n  Actor: {identity.ActorInstanceId}\n  Threat: {target.ActorInstanceId}");
            return true;
        }

        public void ClearThreat(string reason)
        {
            threat = null;
            responseOverride = null;
            ResetEncounter(string.IsNullOrWhiteSpace(reason) ? "Threat cleared" : reason);
        }

        public bool TryOverrideResponse(HumanEncounterResponse response, out string error)
        {
            error = null;
            if (!configured || !Enum.IsDefined(typeof(HumanEncounterResponse), response))
            {
                error = "Encounter AI is not configured or the response is invalid.";
                return false;
            }
            responseOverride = response;
            CancelActiveAction();
            navigation.Stop();
            ResetPlanLatch();
            if (hasLastKnownPosition)
                Transition(HumanEncounterAIState.Alerted, "Encounter response changed");
            nextDecisionTime = 0d;
            return true;
        }

        public void ClearResponseOverride()
        {
            responseOverride = null;
            CancelActiveAction();
            navigation?.Stop();
            ResetPlanLatch();
            if (hasLastKnownPosition)
                Transition(HumanEncounterAIState.Alerted, "Encounter response override cleared");
            nextDecisionTime = 0d;
        }

        private void EvaluateEncounter(double now)
        {
            if (threat == null || !threat.IsRegistered || threat.LifecycleState == ActorLifecycleState.Dead)
            {
                ClearThreat("Threat became unavailable");
                return;
            }

            LastPerception = perception.Evaluate(threat);
            if (LastPerception.Perceived)
            {
                lastKnownPosition = LastPerception.ObservedPosition;
                hasLastKnownPosition = true;
                lastSeenTime = now;
                if (State == HumanEncounterAIState.Idle || State == HumanEncounterAIState.LostContact)
                    Transition(HumanEncounterAIState.Alerted, "Threat perceived");
            }
            else
            {
                if (!hasLastKnownPosition)
                    return;
                if (State != HumanEncounterAIState.LostContact)
                {
                    CancelActiveAction();
                    if (State != HumanEncounterAIState.Avoiding && State != HumanEncounterAIState.Fleeing)
                        navigation.Stop();
                    Transition(HumanEncounterAIState.LostContact, $"Perception lost: {LastPerception.Reason}");
                }
                if (now - lastSeenTime >= lostContactTimeout)
                {
                    threat = null;
                    ResetEncounter("Lost-contact timeout elapsed");
                }
                return;
            }

            if (State == HumanEncounterAIState.Alerted)
            {
                if (now - stateEnteredTime < alertDuration)
                    return;
                Transition(ResponseState(), $"Alert completed with {Response} policy");
            }

            switch (State)
            {
                case HumanEncounterAIState.Avoiding:
                    ExecuteRetreat(avoidDistance, false);
                    break;
                case HumanEncounterAIState.Fleeing:
                    ExecuteRetreat(fleeDistance, true);
                    break;
                case HumanEncounterAIState.Fighting:
                    ExecuteFight(now);
                    break;
            }
        }

        private void ExecuteRetreat(float desiredDistance, bool decisive)
        {
            float currentDistance = FlatDistance(transform.position, lastKnownPosition);
            if (currentDistance >= desiredDistance)
            {
                navigation.Stop();
                hasPlan = false;
                return;
            }

            if (navigation.State == ActorNavigationState.Moving && hasPlan &&
                FlatDistance(lastKnownPosition, lastPlannedThreatPosition) < replanDistance)
                return;
            if (navigationFailureLatched &&
                FlatDistance(transform.position, failedPlanActorPosition) < replanDistance &&
                FlatDistance(lastKnownPosition, failedPlanThreatPosition) < replanDistance &&
                failedPlanResponse == Response)
                return;

            Vector3 away = Vector3.ProjectOnPlane(transform.position - lastKnownPosition, Vector3.up);
            if (away.sqrMagnitude <= 0.0001f)
                away = -Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            away.Normalize();
            float travel = Mathf.Max(decisive ? fleeDistance : avoidDistance, desiredDistance - currentDistance + replanDistance);
            Vector3[] directions = { away, Quaternion.Euler(0f, 45f, 0f) * away, Quaternion.Euler(0f, -45f, 0f) * away };
            NavigationPlanAttemptCount++;
            for (int index = 0; index < directions.Length; index++)
            {
                Vector3 candidate = transform.position + directions[index] * travel;
                if (NavMesh.SamplePosition(candidate, out NavMeshHit resolved, DestinationProjectionDistance, NavMesh.AllAreas) &&
                    navigation.TryNavigate(resolved.position, out _))
                {
                    navigationFailureLatched = false;
                    hasPlan = true;
                    lastPlannedThreatPosition = lastKnownPosition;
                    return;
                }
            }

            navigationFailureLatched = true;
            failedPlanActorPosition = transform.position;
            failedPlanThreatPosition = lastKnownPosition;
            failedPlanResponse = Response;
            hasPlan = false;
            Debug.LogWarning(
                "[AI][NAVIGATION_PLAN_FAILED]" +
                $"\n  Actor: {identity?.ActorInstanceId ?? "<NONE>"}" +
                $"\n  Response: {Response}" +
                $"\n  ActorPosition: {transform.position}" +
                $"\n  LastKnownPosition: {lastKnownPosition}" +
                $"\n  DesiredDistance: {desiredDistance:0.###}" +
                "\n  ActionTaken: failure latched until actor or threat context changes");
        }

        private void ExecuteFight(double now)
        {
            if (!WeaponCombatService.TryGetEquippedWeapon(ownership, out ItemInstance weapon, out _,
                    out FirearmProfileDefinition firearm, out WeaponProfileDefinition melee))
            {
                CancelActiveAction();
                navigation.Stop();
                return;
            }

            float distance = FlatDistance(transform.position, lastKnownPosition);
            float engagementRange = firearm != null ? Mathf.Min(firearm.range, preferredCombatDistance) : melee.melee_range;
            float engagementTolerance = navigation.Agent != null ? navigation.Agent.stoppingDistance : 0.2f;
            if (distance > engagementRange + engagementTolerance)
            {
                CancelActiveAction();
                NavigateTowardEngagement(distance, engagementRange);
                return;
            }
            navigation.Stop();

            if (firearm != null)
            {
                if (reloadPending)
                {
                    if (weapon.InstanceId != reloadWeaponInstanceId)
                    {
                        CancelActiveAction();
                        return;
                    }
                    if (now < reloadCompletionTime)
                        return;
                    reloadPending = false;
                    LastCombatResult = WeaponCombatService.ReloadEquipped(ownership, reloadWeaponInstanceId);
                    if (LastCombatResult.Success)
                        ReloadCount++;
                    return;
                }

                if (weapon.LoadedRounds <= 0)
                {
                    if (WeaponCombatService.GetCompatibleAmmoQuantity(ownership, weapon) <= 0)
                        return;
                    reloadPending = true;
                    reloadWeaponInstanceId = weapon.InstanceId;
                    reloadCompletionTime = now + firearm.reload_duration;
                    return;
                }
                if (now < nextAttackTime)
                    return;

                Collider targetCollider = SelectTargetCollider(threat);
                if (targetCollider == null)
                    return;
                Vector3 origin = PhysicalOrigin();
                Vector3 direction = lastKnownPosition - origin;
                if (direction.sqrMagnitude <= 0.0001f)
                    return;
                LastCombatResult = WeaponCombatService.FireEquipped(
                    ownership,
                    weapon.InstanceId,
                    penetrationPower => PhysicalShotPathResolver.Resolve(
                        transform, origin, direction, firearm.range, penetrationPower));
                if (LastCombatResult.Quantity == 1)
                {
                    AttackCount++;
                    nextAttackTime = now + firearm.cycle_time;
                }
                return;
            }

            if (now < nextAttackTime)
                return;
            Collider meleeTarget = SelectTargetCollider(threat);
            if (meleeTarget == null)
                return;
            LastCombatResult = WeaponCombatService.StrikeEquipped(ownership, weapon.InstanceId, meleeTarget, lastKnownPosition);
            if (LastCombatResult.Success)
            {
                AttackCount++;
                nextAttackTime = now + melee.attack_duration + melee.attack_cooldown;
            }
        }

        private void NavigateTowardEngagement(float distance, float desiredDistance)
        {
            if (navigation.State == ActorNavigationState.Moving && hasPlan &&
                FlatDistance(lastKnownPosition, lastPlannedThreatPosition) < replanDistance)
                return;
            if (navigationFailureLatched &&
                FlatDistance(transform.position, failedPlanActorPosition) < replanDistance &&
                FlatDistance(lastKnownPosition, failedPlanThreatPosition) < replanDistance &&
                failedPlanResponse == Response)
                return;
            Vector3 toward = Vector3.ProjectOnPlane(lastKnownPosition - transform.position, Vector3.up).normalized;
            Vector3 candidate = transform.position + toward * Mathf.Max(0f, distance - desiredDistance);
            NavigationPlanAttemptCount++;
            if (NavMesh.SamplePosition(candidate, out NavMeshHit resolved, DestinationProjectionDistance, NavMesh.AllAreas) &&
                navigation.TryNavigate(resolved.position, out _))
            {
                navigationFailureLatched = false;
                hasPlan = true;
                lastPlannedThreatPosition = lastKnownPosition;
                return;
            }
            navigationFailureLatched = true;
            failedPlanActorPosition = transform.position;
            failedPlanThreatPosition = lastKnownPosition;
            failedPlanResponse = Response;
            hasPlan = false;
        }

        private Vector3 PhysicalOrigin()
        {
            Collider body = GetComponentsInChildren<Collider>(false)
                .FirstOrDefault(collider => collider != null && collider.enabled && !collider.isTrigger);
            return body != null ? body.bounds.center : transform.position + Vector3.up * perception.EyeHeight;
        }

        private static Collider SelectTargetCollider(ActorRuntimeIdentity target) => target == null ? null :
            target.GetComponentsInChildren<Collider>(false)
                .Where(collider => collider != null && collider.enabled && !collider.isTrigger)
                .OrderBy(collider => collider.bounds.center.y)
                .FirstOrDefault();

        private void EnterInactive(string reason)
        {
            if (State == HumanEncounterAIState.Inactive)
                return;
            CancelActiveAction();
            navigation?.Stop();
            threat = null;
            ClearEncounterMemory();
            Transition(HumanEncounterAIState.Inactive, reason);
        }

        private void ResetEncounter(string reason)
        {
            CancelActiveAction();
            navigation?.Stop();
            threat = null;
            responseOverride = null;
            ClearEncounterMemory();
            ResetPlanLatch();
            nextDecisionTime = 0d;
            nextAttackTime = 0d;
            Transition(identity != null && identity.LifecycleState == ActorLifecycleState.Dead
                ? HumanEncounterAIState.Inactive : HumanEncounterAIState.Idle, reason);
        }

        private void ClearEncounterMemory()
        {
            hasLastKnownPosition = false;
            lastKnownPosition = default;
            lastSeenTime = double.NaN;
            LastPerception = default;
        }

        private void CancelActiveAction()
        {
            reloadPending = false;
            reloadCompletionTime = 0d;
            reloadWeaponInstanceId = null;
        }

        private void ResetPlanLatch()
        {
            navigationFailureLatched = false;
            hasPlan = false;
        }

        private HumanEncounterAIState ResponseState() => Response == HumanEncounterResponse.Avoid
            ? HumanEncounterAIState.Avoiding
            : Response == HumanEncounterResponse.Flee ? HumanEncounterAIState.Fleeing : HumanEncounterAIState.Fighting;

        private void Transition(HumanEncounterAIState next, string reason)
        {
            HumanEncounterAIState previous = State;
            if (previous == next)
                return;
            State = next;
            stateEnteredTime = Time.timeAsDouble;
            TransitionRevision++;
            Debug.Log($"[AI][STATE]\n  Actor: {identity?.ActorInstanceId ?? "<NONE>"}\n  From: {previous}\n  To: {next}\n  Reason: {reason ?? "<UNKNOWN>"}");
        }

        private void ResolveReferences()
        {
            if (identity == null) identity = GetComponent<ActorRuntimeIdentity>();
            if (health == null) health = GetComponent<ActorHealthComponent>();
            if (navigation == null) navigation = GetComponent<ActorNavigationController>();
            if (perception == null) perception = GetComponent<ActorVisualPerceptionService>();
            if (ownership == null) ownership = GetComponent<ActorItemOwnershipComponent>();
        }

        private static bool TryParseResponse(string value, out HumanEncounterResponse response)
        {
            response = default;
            if (value == "avoid") response = HumanEncounterResponse.Avoid;
            else if (value == "flee") response = HumanEncounterResponse.Flee;
            else if (value == "fight") response = HumanEncounterResponse.Fight;
            else return false;
            return true;
        }

        private static bool FinitePositive(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        private static float FlatDistance(Vector3 left, Vector3 right) =>
            Vector3.ProjectOnPlane(left - right, Vector3.up).magnitude;
    }
}
