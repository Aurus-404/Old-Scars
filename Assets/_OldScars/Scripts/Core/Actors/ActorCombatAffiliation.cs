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
        private const float AcquisitionIntervalSeconds = 0.1f;
        private const int InitialRegistryCapacity = 32;
        private const int InitialCandidateCapacity = 16;
        private const int InitialRecognitionCapacity = 16;

        private struct RecognitionState
        {
            public ActorRuntimeIdentity Candidate;
            public float Progress;
            public float LastUpdateTime;
            public int LastScanRevision;
        }

        private readonly List<ActorRuntimeIdentity> registryBuffer =
            new List<ActorRuntimeIdentity>(InitialRegistryCapacity);
        private readonly List<ActorRuntimeIdentity> candidateBuffer =
            new List<ActorRuntimeIdentity>(InitialCandidateCapacity);
        private readonly List<RecognitionState> recognitionStates =
            new List<RecognitionState>(InitialRecognitionCapacity);

        private ActorRuntimeIdentity identity;
        private ActorHealthComponent health;
        private ActorAffiliationComponent affiliation;
        private ActorVisualPerceptionService perception;
        private HumanEncounterAIController encounter;
        private float nextAcquisitionTime;
        private int recognitionScanRevision;
        private bool configured;

        public bool IsConfigured => configured;
        public int AcquisitionScanCount { get; private set; }
        public int RegistryCandidateVisitCount { get; private set; }
        public int PerceptionEvaluationCount { get; private set; }
        public int RegistryBufferExpansionCount { get; private set; }
        public int CandidateBufferExpansionCount { get; private set; }
        public int RecognitionStateBufferExpansionCount { get; private set; }
        public int PeakRecognitionStateCount { get; private set; }
        public int RecognitionStateCount => recognitionStates.Count;
        public float RecognitionScanIntervalSeconds => AcquisitionIntervalSeconds;
        public float HighestRecognitionProgress { get; private set; }
        public string HighestRecognitionTargetActorInstanceId { get; private set; }
        public ActorVisualPerceptionResult LastAcquisitionPerception { get; private set; }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnDisable()
        {
            ClearRecognitionStates();
        }

        private void Update()
        {
            if (!configured)
                return;
            if (identity == null || health == null || identity.LifecycleState == ActorLifecycleState.Dead || health.IsDead)
            {
                encounter?.ClearThreat("Acquirer became inactive");
                ClearRecognitionStates();
                enabled = false;
                return;
            }

            ActorRuntimeIdentity current = encounter.Threat;
            if (current != null)
            {
                if (current.IsRegistered && current.LifecycleState == ActorLifecycleState.Alive &&
                    affiliation.IsHostileToward(current))
                {
                    ClearRecognitionStates();
                    return;
                }
                encounter.ClearThreat("Assigned actor is no longer a living hostile candidate");
            }

            float now = Time.time;
            if (now < nextAcquisitionTime)
                return;
            nextAcquisitionTime = now + AcquisitionIntervalSeconds;
            AcquireNearestRecognizedHostile(now);
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
            ClearRecognitionStates();
            float phase = (Mix(unchecked((ulong)deterministicPhaseSeed)) & 0xffffUL) / 65535f;
            nextAcquisitionTime = Time.time + AcquisitionIntervalSeconds * (0.5f + phase * 0.5f);
            return true;
        }

        public bool TryGetRecognitionProgress(ActorRuntimeIdentity candidate, out float progress)
        {
            int index = FindRecognitionState(candidate);
            progress = index >= 0 ? recognitionStates[index].Progress : 0f;
            return index >= 0;
        }

        private void AcquireNearestRecognizedHostile(float now)
        {
            AcquisitionScanCount++;
            recognitionScanRevision++;
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
                ActorRuntimeIdentity candidate = candidateBuffer[index];
                int stateIndex = FindRecognitionState(candidate);
                if (stateIndex < 0)
                {
                    int previousCapacity = recognitionStates.Capacity;
                    recognitionStates.Add(new RecognitionState
                    {
                        Candidate = candidate,
                        Progress = 0f,
                        LastUpdateTime = now,
                        LastScanRevision = recognitionScanRevision
                    });
                    if (recognitionStates.Capacity > previousCapacity)
                        RecognitionStateBufferExpansionCount++;
                    stateIndex = recognitionStates.Count - 1;
                    PeakRecognitionStateCount = Mathf.Max(PeakRecognitionStateCount, recognitionStates.Count);
                }

                RecognitionState state = recognitionStates[stateIndex];
                float elapsed = Mathf.Max(0f, now - state.LastUpdateTime);
                LastAcquisitionPerception = perception.Evaluate(candidate);
                PerceptionEvaluationCount++;
                state.LastUpdateTime = now;
                state.LastScanRevision = recognitionScanRevision;
                if (LastAcquisitionPerception.Perceived)
                {
                    float recognitionSeconds = perception.RecognitionSecondsAtDistance(LastAcquisitionPerception.Distance);
                    state.Progress = Mathf.Clamp01(state.Progress + elapsed / recognitionSeconds);
                }
                else
                {
                    state.Progress = Mathf.Clamp01(state.Progress - elapsed / perception.RecognitionDecaySeconds);
                }
                recognitionStates[stateIndex] = state;

                if (state.Progress >= 1f)
                {
                    if (encounter.TryAssignThreat(candidate, out string error))
                    {
                        ClearRecognitionStates();
                        return;
                    }
                    Debug.LogWarning(
                        "[AI][THREAT_ACQUISITION_REJECTED]" +
                        $"\n  Actor: {identity.ActorInstanceId}" +
                        $"\n  Candidate: {candidate.ActorInstanceId}" +
                        $"\n  Failure: {error ?? "<UNKNOWN>"}");
                }
                else if (state.Progress <= 0f && !LastAcquisitionPerception.Perceived)
                {
                    recognitionStates.RemoveAt(stateIndex);
                }
            }

            DecayOrRemoveUnscannedStates(now);
            UpdateRecognitionObservability();
        }

        private void DecayOrRemoveUnscannedStates(float now)
        {
            float visualRangeSquared = perception.VisualRange * perception.VisualRange;
            for (int index = recognitionStates.Count - 1; index >= 0; index--)
            {
                RecognitionState state = recognitionStates[index];
                if (state.LastScanRevision == recognitionScanRevision)
                    continue;
                ActorRuntimeIdentity candidate = state.Candidate;
                if (candidate == null || !candidate.IsRegistered ||
                    candidate.LifecycleState == ActorLifecycleState.Dead || !affiliation.IsHostileToward(candidate))
                {
                    recognitionStates.RemoveAt(index);
                    continue;
                }

                float elapsed = Mathf.Max(0f, now - state.LastUpdateTime);
                state.LastUpdateTime = now;
                state.Progress = Mathf.Clamp01(state.Progress - elapsed / perception.RecognitionDecaySeconds);
                bool outsideBroadRange = (candidate.transform.position - transform.position).sqrMagnitude > visualRangeSquared;
                if (state.Progress <= 0f || outsideBroadRange && elapsed >= perception.RecognitionDecaySeconds)
                    recognitionStates.RemoveAt(index);
                else
                    recognitionStates[index] = state;
            }
        }

        private int FindRecognitionState(ActorRuntimeIdentity candidate)
        {
            for (int index = 0; index < recognitionStates.Count; index++)
            {
                if (recognitionStates[index].Candidate == candidate)
                    return index;
            }
            return -1;
        }

        private void UpdateRecognitionObservability()
        {
            HighestRecognitionProgress = 0f;
            HighestRecognitionTargetActorInstanceId = null;
            for (int index = 0; index < recognitionStates.Count; index++)
            {
                RecognitionState state = recognitionStates[index];
                if (state.Candidate == null || state.Progress <= HighestRecognitionProgress)
                    continue;
                HighestRecognitionProgress = state.Progress;
                HighestRecognitionTargetActorInstanceId = state.Candidate.ActorInstanceId;
            }
        }

        private void ClearRecognitionStates()
        {
            recognitionStates.Clear();
            HighestRecognitionProgress = 0f;
            HighestRecognitionTargetActorInstanceId = null;
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
