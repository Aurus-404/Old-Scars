using System;
using System.Collections.Generic;
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
        Searching,
        Inactive
    }

    public enum HumanEncounterSearchOutcome
    {
        None,
        Navigating,
        Inspecting,
        Reacquired,
        Released,
        Failed,
        Aborted
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
        private const float MinimumNpcSpreadDegrees = 1.25f;
        private const float MaximumNpcSpreadDegrees = 4.5f;
        private const float FocusBuildSeconds = 1.5f;
        private const float FocusDecaySeconds = 0.75f;
        private const float BurstSpreadRecoveryPerSecond = 1.25f;
        private const int InitialColliderBufferCapacity = 8;

        private readonly List<Collider> colliderBuffer = new List<Collider>(InitialColliderBufferCapacity);

        private ActorRuntimeIdentity identity;
        private ActorHealthComponent health;
        private ActorConditionComponent condition;
        private ActorNavigationController navigation;
        private ActorBehaviorController behavior;
        private ActorGazeController gaze;
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
        private bool hasSearchAnchor;
        private Vector3 searchObservedPosition;
        private Vector3 searchAnchor;
        private double searchStartedTime;
        private double searchNavigationDeadline;
        private double searchInspectionStartedTime;
        private bool deterministicAimSeedConfigured;
        private long deterministicAimSeed;
        private ulong aimSampleSequence;
        private double lastAimUpdateTime;
        private bool hasPreviousObservedPosition;
        private Vector3 previousObservedPosition;
        private double previousObservedTime;
        private float observedTargetSpeed;
        private float burstSpreadDegrees;

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
        public bool HasSearchAnchor => hasSearchAnchor;
        public Vector3 SearchObservedPosition => searchObservedPosition;
        public Vector3 SearchAnchor => searchAnchor;
        public double SearchStartedTime => searchStartedTime;
        public bool IsSearchInspecting => State == HumanEncounterAIState.Searching &&
                                          !double.IsNaN(searchInspectionStartedTime);
        public float SearchInspectionDurationSeconds => lostContactTimeout;
        public float SearchInspectionRemainingSeconds => IsSearchInspecting
            ? Mathf.Max(0f, lostContactTimeout - (float)(Time.timeAsDouble - searchInspectionStartedTime))
            : 0f;
        public int SearchCount { get; private set; }
        public int SearchRevision { get; private set; }
        public HumanEncounterSearchOutcome LastSearchOutcome { get; private set; }
        public ActorVisualPerceptionResult LastPerception { get; private set; }
        public WeaponCombatResult LastCombatResult { get; private set; }
        public float CurrentFocus { get; private set; }
        public float CurrentSpreadDegrees { get; private set; } = MaximumNpcSpreadDegrees;
        public float CurrentDefocusedSpreadDegrees { get; private set; } = MaximumNpcSpreadDegrees;
        public float CurrentTargetDistance { get; private set; }
        public float CurrentWeaponRange { get; private set; }
        public bool IsClosingDistance { get; private set; }
        public Vector3 CurrentAimPoint { get; private set; }
        public Vector3 CurrentShotDirection { get; private set; }
        public int PhysicalActorHitCount { get; private set; }
        public int PhysicalMissCount { get; private set; }
        public int PhysicalObstacleImpactCount { get; private set; }
        public int ArmoredActorHitCount { get; private set; }
        public ulong AimSampleSequence => aimSampleSequence;
        public double LastShotTime { get; private set; } = double.NegativeInfinity;
        public Vector3 LastShotOrigin { get; private set; }
        public Vector3 LastShotDirection { get; private set; }
        public string LastShotIntentTargetActorInstanceId { get; private set; }

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
            if (condition != null && !condition.CanPerformActiveActions)
            {
                EnterInactive("Actor is functionally incapacitated");
                return;
            }
            if (State == HumanEncounterAIState.Inactive)
                ReleaseEncounter("Actor recovered functional capacity");

            double now = Time.timeAsDouble;
            if (now >= nextDecisionTime)
            {
                nextDecisionTime = now + decisionInterval;
                EvaluateEncounter(now);
            }
            UpdateAimState(now);
            if (State == HumanEncounterAIState.Fighting && HasFreshPerception(now))
                ExecuteWeaponCycle(now);
        }

        private void OnDisable()
        {
            CancelActiveAction();
            behavior?.EnterInactive("Encounter controller disabled");
            gaze?.ReleaseEncounterAttention();
            threat = null;
            responseOverride = null;
            ClearEncounterMemory();
            ClearSearchMemory(false);
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
            if (identity == null || health == null || navigation?.IsConfigured != true || behavior == null ||
                gaze?.IsConfigured != true ||
                perception?.IsConfigured != true || ownership == null)
            {
                error = "Encounter AI requires identity, health, Behavior and Gaze ownership, configured Navigation and Perception, and item ownership.";
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
            if (!deterministicAimSeedConfigured)
                deterministicAimSeed = StableHash(identity.ActorProfileId);
            configured = true;
            ReleaseEncounter("Profile configured");
            return true;
        }

        public void ConfigureDeterministicAimSeed(long seed)
        {
            deterministicAimSeed = seed;
            deterministicAimSeedConfigured = true;
            aimSampleSequence = 0UL;
        }

        public bool TryAssignThreat(ActorRuntimeIdentity target, out string error)
        {
            error = null;
            if (!configured)
            {
                error = "Encounter AI is not configured.";
                return false;
            }
            if (condition != null && !condition.CanPerformActiveActions)
            {
                error = "Functionally incapacitated actors cannot acquire threats.";
                return false;
            }
            if (target == null || !target.IsRegistered || target == identity || target.LifecycleState == ActorLifecycleState.Dead)
            {
                error = "Threat must be a distinct registered living actor.";
                return false;
            }
            if (threat == target)
                return true;
            if (!behavior.EnterEncounter("Threat assigned"))
            {
                error = "Behavior ownership rejected Encounter while the actor cannot act.";
                return false;
            }
            threat = target;
            responseOverride = null;
            ClearEncounterMemory();
            ClearSearchMemory(true);
            ResetAimTracking();
            CancelActiveAction();
            Transition(HumanEncounterAIState.Idle, "Threat assigned; awaiting perception");
            nextDecisionTime = 0d;
            Debug.Log($"[AI][THREAT_ASSIGNED]\n  Actor: {identity.ActorInstanceId}\n  Threat: {target.ActorInstanceId}");
            return true;
        }

        public void ClearThreat(string reason)
        {
            ReleaseEncounter(string.IsNullOrWhiteSpace(reason) ? "Threat cleared" : reason);
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
            if (State == HumanEncounterAIState.Searching)
            {
                if (AbortSearchToEncounter("Encounter response changed during Search"))
                    return true;
                error = "Search could not restore Encounter ownership for the response change.";
                return false;
            }
            behavior.StopEncounterNavigation();
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
            if (State == HumanEncounterAIState.Searching)
            {
                if (!AbortSearchToEncounter("Encounter response override cleared during Search"))
                    ReleaseEncounter("Search could not restore Encounter ownership after clearing response override");
                return;
            }
            behavior?.StopEncounterNavigation();
            ResetPlanLatch();
            if (hasLastKnownPosition)
                Transition(HumanEncounterAIState.Alerted, "Encounter response override cleared");
            nextDecisionTime = 0d;
        }

        private void EvaluateEncounter(double now)
        {
            if (threat == null)
                return;
            if (!ThreatRemainsValid())
            {
                if (State == HumanEncounterAIState.Searching)
                    FinishSearch(HumanEncounterSearchOutcome.Aborted, "Search threat became unavailable");
                else
                    ClearThreat("Threat became unavailable");
                return;
            }

            LastPerception = perception.Evaluate(threat);
            if (LastPerception.Perceived)
            {
                if (State == HumanEncounterAIState.Searching)
                {
                    if (!behavior.ReturnSearchToEncounter("Search reacquired perceived threat"))
                    {
                        ReleaseEncounter("Search could not restore Encounter ownership");
                        return;
                    }
                    LastSearchOutcome = HumanEncounterSearchOutcome.Reacquired;
                    SearchRevision++;
                    ClearSearchMemory(false);
                    ResetPlanLatch();
                    Transition(HumanEncounterAIState.Alerted, "Threat reacquired during Search");
                }
                gaze.TryAttendEncounter(LastPerception);
                UpdateObservedMotion(LastPerception.ObservedPosition, now);
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
                gaze.TryAttendLostContact(threat.ActorInstanceId, lastKnownPosition);
                if (State == HumanEncounterAIState.Searching)
                {
                    UpdateSearch(now);
                    return;
                }
                if (State != HumanEncounterAIState.LostContact)
                {
                    IsClosingDistance = false;
                    CancelActiveAction();
                    if (State != HumanEncounterAIState.Avoiding && State != HumanEncounterAIState.Fleeing)
                        behavior.StopEncounterNavigation();
                    Transition(HumanEncounterAIState.LostContact, $"Perception lost: {LastPerception.Reason}");
                    return;
                }
                if (Response == HumanEncounterResponse.Fight)
                {
                    BeginSearch(now);
                    return;
                }
                if (now - lastSeenTime >= lostContactTimeout)
                    ClearThreat("Lost-contact timeout elapsed");
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
                    EvaluateFightTactics();
                    break;
            }
        }

        private void BeginSearch(double now)
        {
            CancelActiveAction();
            ResetPlanLatch();
            if (!behavior.EnterSearch("Fight lost contact with observed threat"))
            {
                ReleaseEncounter("Behavior ownership rejected Search");
                return;
            }

            searchObservedPosition = lastKnownPosition;
            hasSearchAnchor = false;
            searchStartedTime = now;
            searchInspectionStartedTime = double.NaN;
            SearchCount++;
            SearchRevision++;
            Transition(HumanEncounterAIState.Searching, "Investigating frozen last-known information");

            int areaMask = navigation.Agent != null ? navigation.Agent.areaMask : NavMesh.AllAreas;
            NavigationPlanAttemptCount++;
            if (!NavMesh.SamplePosition(searchObservedPosition, out NavMeshHit resolved,
                    DestinationProjectionDistance, areaMask))
            {
                FinishSearch(HumanEncounterSearchOutcome.Failed,
                    "Search anchor could not be projected onto NavMesh");
                return;
            }

            searchAnchor = resolved.position;
            hasSearchAnchor = true;
            float distance = FlatDistance(transform.position, searchAnchor);
            float speed = navigation.Agent != null ? Mathf.Max(0.1f, navigation.Agent.speed) : 0.1f;
            searchNavigationDeadline = now + Math.Max(lostContactTimeout, distance / speed * 2f + lostContactTimeout);
            if (!behavior.TryNavigateSearch(searchAnchor))
            {
                FinishSearch(HumanEncounterSearchOutcome.Failed,
                    "Search navigation rejected the frozen anchor");
                return;
            }

            LastSearchOutcome = HumanEncounterSearchOutcome.Navigating;
            SearchRevision++;
        }

        private void UpdateSearch(double now)
        {
            if (behavior.Owner != ActorBehaviorOwner.Search || !hasSearchAnchor)
            {
                FinishSearch(HumanEncounterSearchOutcome.Aborted,
                    "Search lost its owner, anchor or navigation order");
                return;
            }

            if (!IsSearchInspecting && now >= searchNavigationDeadline)
            {
                FinishSearch(HumanEncounterSearchOutcome.Failed,
                    "Search navigation exceeded its bounded travel deadline");
                return;
            }

            if (!IsSearchInspecting)
            {
                if (navigation.State == ActorNavigationState.Moving)
                    return;
                if (navigation.State == ActorNavigationState.Reached)
                {
                    behavior.StopSearchNavigation();
                    searchInspectionStartedTime = now;
                    LastSearchOutcome = HumanEncounterSearchOutcome.Inspecting;
                    SearchRevision++;
                    return;
                }

                FinishSearch(HumanEncounterSearchOutcome.Failed,
                    "Search navigation stopped before reaching its anchor");
                return;
            }

            if (now - searchInspectionStartedTime >= lostContactTimeout)
                FinishSearch(HumanEncounterSearchOutcome.Released,
                    "Search inspection elapsed without reacquisition");
        }

        private void FinishSearch(HumanEncounterSearchOutcome outcome, string reason)
        {
            LastSearchOutcome = outcome;
            SearchRevision++;
            ReleaseEncounter(reason);
        }

        private bool AbortSearchToEncounter(string reason)
        {
            if (!behavior.ReturnSearchToEncounter(reason))
                return false;
            LastSearchOutcome = HumanEncounterSearchOutcome.Aborted;
            SearchRevision++;
            ClearSearchMemory(false);
            ResetPlanLatch();
            Transition(HumanEncounterAIState.Alerted, reason);
            nextDecisionTime = 0d;
            return true;
        }

        private bool ThreatRemainsValid()
        {
            if (threat == null || !threat.IsRegistered || threat.LifecycleState == ActorLifecycleState.Dead)
                return false;
            ActorConditionComponent threatCondition = threat.GetComponent<ActorConditionComponent>();
            if (threatCondition != null && !threatCondition.CanPerformActiveActions)
                return false;
            if (State != HumanEncounterAIState.Searching)
                return true;
            ActorAffiliationComponent observerAffiliation = GetComponent<ActorAffiliationComponent>();
            ActorAffiliationComponent threatAffiliation = threat.GetComponent<ActorAffiliationComponent>();
            return observerAffiliation?.IsConfigured != true || threatAffiliation?.IsConfigured != true ||
                   observerAffiliation.IsHostileToward(threat);
        }

        private void ExecuteRetreat(float desiredDistance, bool decisive)
        {
            float currentDistance = FlatDistance(transform.position, lastKnownPosition);
            if (currentDistance >= desiredDistance)
            {
                behavior.StopEncounterNavigation();
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
                    behavior.TryNavigateEncounter(resolved.position))
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

        private void EvaluateFightTactics()
        {
            if (!WeaponCombatService.TryGetEquippedWeapon(ownership, out ItemInstance weapon, out _,
                    out FirearmProfileDefinition firearm, out WeaponProfileDefinition melee))
            {
                CancelActiveAction();
                behavior.StopEncounterNavigation();
                IsClosingDistance = false;
                CurrentWeaponRange = 0f;
                return;
            }

            Collider targetCollider = SelectTargetCollider(threat);
            if (targetCollider == null)
                return;
            Vector3 attackPoint = targetCollider.bounds.center;
            float distance = FlatDistance(transform.position, attackPoint);
            float engagementRange = firearm != null ? Mathf.Min(firearm.range, preferredCombatDistance) : melee.melee_range;
            float physicalDistance = firearm != null
                ? Vector3.Distance(PhysicalOrigin(), attackPoint)
                : Vector3.Distance(transform.position, attackPoint);
            CurrentTargetDistance = physicalDistance;
            CurrentWeaponRange = firearm != null ? firearm.range : melee.melee_range;
            float engagementTolerance = navigation.Agent != null ? navigation.Agent.stoppingDistance : 0.2f;
            if (physicalDistance > engagementRange + engagementTolerance)
            {
                CancelActiveAction();
                float desiredFlatDistance = firearm != null
                    ? Mathf.Max(0.5f, engagementRange * 0.9f)
                    : Mathf.Max(0.25f, melee.melee_range * 0.6f);
                NavigateTowardEngagement(distance, desiredFlatDistance);
                IsClosingDistance = true;
                return;
            }
            behavior.StopEncounterNavigation();
            IsClosingDistance = false;
        }

        private void ExecuteWeaponCycle(double now)
        {
            if (!WeaponCombatService.TryGetEquippedWeapon(ownership, out ItemInstance weapon, out _,
                    out FirearmProfileDefinition firearm, out WeaponProfileDefinition melee))
                return;

            Collider targetCollider = SelectTargetCollider(threat);
            if (targetCollider == null)
                return;
            Vector3 aimPoint = targetCollider.bounds.center;

            if (firearm != null)
            {
                Vector3 origin = PhysicalOrigin();
                float distance = Vector3.Distance(origin, aimPoint);
                float engagementRange = Mathf.Min(firearm.range, preferredCombatDistance);
                float engagementTolerance = navigation.Agent != null ? navigation.Agent.stoppingDistance : 0.2f;
                CurrentTargetDistance = distance;
                CurrentWeaponRange = firearm.range;
                if (distance > engagementRange + engagementTolerance || distance > firearm.range + 0.01f)
                    return;
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

                Vector3 direction = BuildImperfectShotDirection(origin, aimPoint, CurrentSpreadDegrees);
                if (direction.sqrMagnitude <= 0.0001f)
                    return;
                CurrentAimPoint = aimPoint;
                CurrentShotDirection = direction;
                LastCombatResult = WeaponCombatService.FireEquipped(
                    ownership,
                    weapon.InstanceId,
                    penetrationPower => PhysicalShotPathResolver.Resolve(
                        transform, origin, direction, firearm.range, penetrationPower));
                if (LastCombatResult.Quantity == 1)
                {
                    LastShotTime = now;
                    LastShotOrigin = origin;
                    LastShotDirection = direction;
                    LastShotIntentTargetActorInstanceId = threat != null ? threat.ActorInstanceId : null;
                    AttackCount++;
                    nextAttackTime = now + firearm.cycle_time;
                    if (firearm.fire_mode == "automatic")
                        burstSpreadDegrees = Mathf.Min(2f, burstSpreadDegrees + 0.22f);
                    RecordPhysicalShot(LastCombatResult);
                }
                return;
            }

            if (now < nextAttackTime)
                return;
            CurrentTargetDistance = Vector3.Distance(transform.position, aimPoint);
            CurrentWeaponRange = melee.melee_range;
            if (CurrentTargetDistance > melee.melee_range + 0.05f)
                return;
            LastCombatResult = WeaponCombatService.StrikeEquipped(ownership, weapon.InstanceId, targetCollider, aimPoint);
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
                behavior.TryNavigateEncounter(resolved.position))
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
            colliderBuffer.Clear();
            GetComponentsInChildren(false, colliderBuffer);
            Collider body = null;
            for (int index = 0; index < colliderBuffer.Count; index++)
            {
                Collider candidate = colliderBuffer[index];
                if (candidate == null || !candidate.enabled || candidate.isTrigger ||
                    candidate.GetComponent<ActorCombatHitRegion>() != null)
                    continue;
                if (candidate.GetComponent<ActorLocomotionCollider>() != null)
                    return candidate.bounds.center;
                if (body == null)
                    body = candidate;
            }
            return body != null ? body.bounds.center : transform.position + Vector3.up * perception.EyeHeight;
        }

        private Collider SelectTargetCollider(ActorRuntimeIdentity target)
        {
            if (target == null)
                return null;
            colliderBuffer.Clear();
            target.GetComponentsInChildren(false, colliderBuffer);
            Collider selected = null;
            float preferredHeight = target.transform.position.y + 1f;
            float closestHeight = float.PositiveInfinity;
            for (int index = 0; index < colliderBuffer.Count; index++)
            {
                Collider candidate = colliderBuffer[index];
                if (candidate == null || !candidate.enabled || candidate.isTrigger ||
                    candidate.GetComponent<ActorCombatHitRegion>() != null)
                    continue;
                if (candidate.GetComponent<ActorLocomotionCollider>() != null)
                    return candidate;
                float heightDistance = Mathf.Abs(candidate.bounds.center.y - preferredHeight);
                if (heightDistance >= closestHeight)
                    continue;
                selected = candidate;
                closestHeight = heightDistance;
            }
            return selected;
        }

        private void EnterInactive(string reason)
        {
            if (State == HumanEncounterAIState.Inactive)
                return;
            if (State == HumanEncounterAIState.Searching)
            {
                LastSearchOutcome = HumanEncounterSearchOutcome.Aborted;
                SearchRevision++;
            }
            CancelActiveAction();
            behavior?.EnterInactive(reason);
            gaze?.EnterInactive();
            threat = null;
            ClearEncounterMemory();
            ClearSearchMemory(false);
            ResetAimTracking();
            Transition(HumanEncounterAIState.Inactive, reason);
        }

        private void ReleaseEncounter(string reason)
        {
            bool wasSearching = behavior?.Owner == ActorBehaviorOwner.Search ||
                                State == HumanEncounterAIState.Searching;
            if (wasSearching && (LastSearchOutcome == HumanEncounterSearchOutcome.Navigating ||
                                 LastSearchOutcome == HumanEncounterSearchOutcome.Inspecting))
            {
                LastSearchOutcome = HumanEncounterSearchOutcome.Aborted;
                SearchRevision++;
            }
            CancelActiveAction();
            threat = null;
            responseOverride = null;
            ClearEncounterMemory();
            ClearSearchMemory(false);
            ResetPlanLatch();
            nextDecisionTime = 0d;
            nextAttackTime = 0d;
            ResetAimTracking();
            if (behavior?.Owner == ActorBehaviorOwner.Search)
                behavior.ExitSearchToAmbient(reason);
            else
                behavior?.ExitEncounter(reason);
            gaze?.ReleaseEncounterAttention();
            Transition(identity != null && (identity.LifecycleState == ActorLifecycleState.Dead ||
                                            condition != null && !condition.CanPerformActiveActions)
                ? HumanEncounterAIState.Inactive : HumanEncounterAIState.Idle, reason);
        }

        private void ClearEncounterMemory()
        {
            hasLastKnownPosition = false;
            lastKnownPosition = default;
            lastSeenTime = double.NaN;
            LastPerception = default;
            IsClosingDistance = false;
        }

        private void ClearSearchMemory(bool resetOutcome)
        {
            hasSearchAnchor = false;
            searchObservedPosition = default;
            searchAnchor = default;
            searchStartedTime = double.NaN;
            searchNavigationDeadline = double.NaN;
            searchInspectionStartedTime = double.NaN;
            if (resetOutcome)
                LastSearchOutcome = HumanEncounterSearchOutcome.None;
        }

        private void CancelActiveAction()
        {
            reloadPending = false;
            reloadCompletionTime = 0d;
            reloadWeaponInstanceId = null;
        }

        private void UpdateObservedMotion(Vector3 observedPosition, double now)
        {
            if (hasPreviousObservedPosition)
            {
                double elapsed = now - previousObservedTime;
                if (elapsed > 0.0001d)
                    observedTargetSpeed = (observedPosition - previousObservedPosition).magnitude / (float)elapsed;
            }
            previousObservedPosition = observedPosition;
            previousObservedTime = now;
            hasPreviousObservedPosition = true;
        }

        private void UpdateAimState(double now)
        {
            if (double.IsNaN(lastAimUpdateTime) || lastAimUpdateTime <= 0d)
                lastAimUpdateTime = now;
            float elapsed = Mathf.Clamp((float)(now - lastAimUpdateTime), 0f, 0.25f);
            lastAimUpdateTime = now;
            if (HasFreshPerception(now))
                CurrentFocus = Mathf.Clamp01(CurrentFocus + elapsed / FocusBuildSeconds);
            else
                CurrentFocus = Mathf.Clamp01(CurrentFocus - elapsed / FocusDecaySeconds);
            burstSpreadDegrees = Mathf.Max(0f, burstSpreadDegrees - elapsed * BurstSpreadRecoveryPerSecond);

            if (!WeaponCombatService.TryGetEquippedWeapon(ownership, out _, out _,
                    out FirearmProfileDefinition firearm, out _ ) || firearm == null)
            {
                CurrentSpreadDegrees = 0f;
                return;
            }

            float distancePenalty = Mathf.Clamp01(CurrentTargetDistance / Mathf.Max(0.01f, firearm.range)) * 2.25f;
            float targetMotionPenalty = Mathf.Min(2f, observedTargetSpeed * 0.18f);
            float shooterSpeed = navigation?.Agent != null ? navigation.Agent.velocity.magnitude : 0f;
            float shooterMotionPenalty = Mathf.Min(1.5f, shooterSpeed * 0.22f);
            float focusSpread = Mathf.Lerp(MaximumNpcSpreadDegrees, MinimumNpcSpreadDegrees, CurrentFocus);
            float contextualSpread = firearm.debug_accuracy_spread + distancePenalty +
                                     targetMotionPenalty + shooterMotionPenalty + burstSpreadDegrees;
            CurrentDefocusedSpreadDegrees = Mathf.Max(
                MinimumNpcSpreadDegrees,
                MaximumNpcSpreadDegrees + contextualSpread);
            CurrentSpreadDegrees = Mathf.Max(MinimumNpcSpreadDegrees, focusSpread + contextualSpread);
        }

        private bool HasFreshPerception(double now)
        {
            if (!LastPerception.Perceived || threat == null)
                return false;
            double freshness = Math.Max(0.2d, decisionInterval * 1.5d);
            return now - lastSeenTime <= freshness;
        }

        private Vector3 BuildImperfectShotDirection(Vector3 origin, Vector3 aimPoint, float spreadDegrees)
        {
            Vector3 forward = aimPoint - origin;
            if (forward.sqrMagnitude <= 0.000001f)
                return Vector3.zero;
            forward.Normalize();

            ulong sample = Mix(unchecked((ulong)deterministicAimSeed) +
                               aimSampleSequence++ * 0x9E3779B97F4A7C15UL);
            float radialSample = (sample & 0x00ffffffUL) / 16777216f;
            sample = Mix(sample + 0xD1B54A32D192ED03UL);
            float angularSample = (sample & 0x00ffffffUL) / 16777216f;
            float radius = Mathf.Sqrt(radialSample) * Mathf.Tan(spreadDegrees * Mathf.Deg2Rad);
            float angle = angularSample * Mathf.PI * 2f;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude <= 0.000001f)
                right = transform.right;
            right.Normalize();
            Vector3 up = Vector3.Cross(forward, right).normalized;
            return (forward + right * (Mathf.Cos(angle) * radius) + up * (Mathf.Sin(angle) * radius)).normalized;
        }

        private void RecordPhysicalShot(WeaponCombatResult result)
        {
            if (result.Code == WeaponCombatCode.Miss ||
                result.PhysicalShot.Termination == PhysicalShotTermination.Miss)
            {
                PhysicalMissCount++;
                return;
            }
            if (result.Combat.Resolved)
            {
                PhysicalActorHitCount++;
                if (result.Combat.Armor.ArmorFound)
                    ArmoredActorHitCount++;
            }
            else
                PhysicalObstacleImpactCount++;
        }

        private void ResetAimTracking()
        {
            CurrentFocus = 0f;
            CurrentSpreadDegrees = MaximumNpcSpreadDegrees;
            CurrentDefocusedSpreadDegrees = MaximumNpcSpreadDegrees;
            CurrentTargetDistance = 0f;
            CurrentWeaponRange = 0f;
            CurrentAimPoint = default;
            CurrentShotDirection = default;
            LastShotTime = double.NegativeInfinity;
            LastShotOrigin = default;
            LastShotDirection = default;
            LastShotIntentTargetActorInstanceId = null;
            IsClosingDistance = false;
            hasPreviousObservedPosition = false;
            observedTargetSpeed = 0f;
            burstSpreadDegrees = 0f;
            aimSampleSequence = 0UL;
            lastAimUpdateTime = Time.timeAsDouble;
        }

        private static long StableHash(string value)
        {
            ulong hash = 1469598103934665603UL;
            string text = value ?? string.Empty;
            for (int index = 0; index < text.Length; index++)
            {
                hash ^= text[index];
                hash *= 1099511628211UL;
            }
            return unchecked((long)hash);
        }

        private static ulong Mix(ulong value)
        {
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
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
            if (condition == null) condition = GetComponent<ActorConditionComponent>();
            if (navigation == null) navigation = GetComponent<ActorNavigationController>();
            if (behavior == null) behavior = GetComponent<ActorBehaviorController>();
            if (gaze == null) gaze = GetComponent<ActorGazeController>();
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
