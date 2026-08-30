using System;
using System.Collections.Generic;
using UnityEngine;

namespace OldScars.Core.Actors
{
    public enum ActorDisposition
    {
        Neutral,
        Hostile
    }

    /// <summary>
    /// Ephemeral, generic relationship identity for the development combat sandbox.
    /// It is intentionally separate from actor/profile/save identity and owns no persistence.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActorAffiliationComponent : MonoBehaviour
    {
        private readonly HashSet<string> hostileAffiliations =
            new HashSet<string>(StringComparer.Ordinal);
        private string affiliationId;
        private string debugDisplayName;

        public string AffiliationId => affiliationId;
        public string DebugDisplayName => debugDisplayName;
        public bool IsConfigured => !string.IsNullOrWhiteSpace(affiliationId);

        public bool TryConfigure(
            string requestedAffiliationId,
            string requestedDebugDisplayName,
            IReadOnlyList<string> hostileAffiliationIds,
            out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(requestedAffiliationId))
            {
                error = "Affiliation requires a non-empty runtime id.";
                return false;
            }

            hostileAffiliations.Clear();
            if (hostileAffiliationIds != null)
            {
                for (int index = 0; index < hostileAffiliationIds.Count; index++)
                {
                    string hostileId = hostileAffiliationIds[index];
                    if (string.IsNullOrWhiteSpace(hostileId))
                    {
                        error = "Hostile affiliation ids cannot be empty.";
                        hostileAffiliations.Clear();
                        return false;
                    }
                    if (hostileId == requestedAffiliationId)
                    {
                        error = "An affiliation cannot be hostile to itself in the M41.4 baseline.";
                        hostileAffiliations.Clear();
                        return false;
                    }
                    hostileAffiliations.Add(hostileId);
                }
            }

            affiliationId = requestedAffiliationId;
            debugDisplayName = string.IsNullOrWhiteSpace(requestedDebugDisplayName)
                ? requestedAffiliationId
                : requestedDebugDisplayName;
            return true;
        }

        public ActorDisposition GetDispositionToward(ActorAffiliationComponent other)
        {
            return IsConfigured && other != null && other.IsConfigured &&
                   hostileAffiliations.Contains(other.affiliationId)
                ? ActorDisposition.Hostile
                : ActorDisposition.Neutral;
        }

        public bool IsHostileToward(ActorRuntimeIdentity other)
        {
            return other != null &&
                   GetDispositionToward(other.GetComponent<ActorAffiliationComponent>()) == ActorDisposition.Hostile;
        }
    }

    /// <summary>
    /// Minimal acquisition adapter from affiliation to the existing perception and encounter seams.
    /// Candidate buffers are caller-owned and reused; LOS is evaluated only after cheap filters.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActorThreatAcquisitionController : MonoBehaviour
    {
        private const float AcquisitionIntervalSeconds = 0.25f;
        private const int InitialRegistryCapacity = 32;
        private const int InitialCandidateCapacity = 16;

        private readonly List<ActorRuntimeIdentity> registryBuffer =
            new List<ActorRuntimeIdentity>(InitialRegistryCapacity);
        private readonly List<ActorRuntimeIdentity> candidateBuffer =
            new List<ActorRuntimeIdentity>(InitialCandidateCapacity);

        private ActorRuntimeIdentity identity;
        private ActorHealthComponent health;
        private ActorAffiliationComponent affiliation;
        private ActorVisualPerceptionService perception;
        private HumanEncounterAIController encounter;
        private float nextAcquisitionTime;
        private bool configured;

        public bool IsConfigured => configured;
        public int AcquisitionScanCount { get; private set; }
        public int RegistryCandidateVisitCount { get; private set; }
        public int PerceptionEvaluationCount { get; private set; }
        public int RegistryBufferExpansionCount { get; private set; }
        public int CandidateBufferExpansionCount { get; private set; }
        public ActorVisualPerceptionResult LastAcquisitionPerception { get; private set; }

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (!configured)
                return;
            if (identity == null || health == null || identity.LifecycleState == ActorLifecycleState.Dead || health.IsDead)
            {
                encounter?.ClearThreat("Acquirer became inactive");
                enabled = false;
                return;
            }

            ActorRuntimeIdentity current = encounter.Threat;
            if (current != null)
            {
                if (current.IsRegistered && current.LifecycleState == ActorLifecycleState.Alive &&
                    affiliation.IsHostileToward(current))
                    return;
                encounter.ClearThreat("Assigned actor is no longer a living hostile candidate");
            }

            float now = Time.time;
            if (now < nextAcquisitionTime)
                return;
            nextAcquisitionTime = now + AcquisitionIntervalSeconds;
            AcquireNearestPerceivedHostile();
        }

        public bool TryConfigure(long deterministicPhaseSeed, out string error)
        {
            ResolveReferences();
            error = null;
            if (identity == null || health == null || affiliation?.IsConfigured != true ||
                perception?.IsConfigured != true || encounter?.IsConfigured != true)
            {
                error = "Threat acquisition requires living actor identity, affiliation, configured perception and encounter AI.";
                return false;
            }

            configured = true;
            float phase = (Mix(unchecked((ulong)deterministicPhaseSeed)) & 0xffffUL) / 65535f;
            nextAcquisitionTime = Time.time + AcquisitionIntervalSeconds * (0.5f + phase * 0.5f);
            return true;
        }

        private void AcquireNearestPerceivedHostile()
        {
            AcquisitionScanCount++;
            int registryCapacity = registryBuffer.Capacity;
            ActorRuntimeRegistry.CopyActiveRepresentationsTo(registryBuffer);
            if (registryBuffer.Capacity > registryCapacity)
                RegistryBufferExpansionCount++;

            int candidateCapacity = candidateBuffer.Capacity;
            candidateBuffer.Clear();
            float visualRangeSquared = perception.VisualRange * perception.VisualRange;
            for (int index = 0; index < registryBuffer.Count; index++)
            {
                ActorRuntimeIdentity candidate = registryBuffer[index];
                RegistryCandidateVisitCount++;
                if (candidate == null || candidate == identity || !candidate.IsRegistered ||
                    candidate.LifecycleState == ActorLifecycleState.Dead)
                    continue;
                if (!affiliation.IsHostileToward(candidate))
                    continue;
                if ((candidate.transform.position - transform.position).sqrMagnitude > visualRangeSquared)
                    continue;
                candidateBuffer.Add(candidate);
            }
            if (candidateBuffer.Capacity > candidateCapacity)
                CandidateBufferExpansionCount++;

            SortCandidatesByApproximateDistance();
            for (int index = 0; index < candidateBuffer.Count; index++)
            {
                LastAcquisitionPerception = perception.Evaluate(candidateBuffer[index]);
                PerceptionEvaluationCount++;
                if (!LastAcquisitionPerception.Perceived)
                    continue;
                if (encounter.TryAssignThreat(candidateBuffer[index], out string error))
                    return;
                Debug.LogWarning(
                    "[AI][THREAT_ACQUISITION_REJECTED]" +
                    $"\n  Actor: {identity.ActorInstanceId}" +
                    $"\n  Candidate: {candidateBuffer[index].ActorInstanceId}" +
                    $"\n  Failure: {error ?? "<UNKNOWN>"}");
            }
        }

        private void SortCandidatesByApproximateDistance()
        {
            for (int index = 1; index < candidateBuffer.Count; index++)
            {
                ActorRuntimeIdentity value = candidateBuffer[index];
                float valueDistance = (value.transform.position - transform.position).sqrMagnitude;
                int insertion = index - 1;
                while (insertion >= 0 &&
                       (candidateBuffer[insertion].transform.position - transform.position).sqrMagnitude > valueDistance)
                {
                    candidateBuffer[insertion + 1] = candidateBuffer[insertion];
                    insertion--;
                }
                candidateBuffer[insertion + 1] = value;
            }
        }

        private void ResolveReferences()
        {
            if (identity == null) identity = GetComponent<ActorRuntimeIdentity>();
            if (health == null) health = GetComponent<ActorHealthComponent>();
            if (affiliation == null) affiliation = GetComponent<ActorAffiliationComponent>();
            if (perception == null) perception = GetComponent<ActorVisualPerceptionService>();
            if (encounter == null) encounter = GetComponent<HumanEncounterAIController>();
        }

        private static ulong Mix(ulong value)
        {
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }
}
