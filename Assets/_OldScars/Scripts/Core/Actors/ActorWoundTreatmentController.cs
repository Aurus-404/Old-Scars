using OldScars.Core.Data.Definitions;
using OldScars.Core.Interactions;
using OldScars.Core.Items;
using UnityEngine;

namespace OldScars.Core.Actors
{
    public enum ActorWoundTreatmentPurpose
    {
        Manual,
        RoutineSelfTreatment,
        EmergencySelfTreatment
    }

    public enum ActorWoundTreatmentOutcome
    {
        None,
        InProgress,
        Completed,
        Cancelled,
        Failed
    }

    /// <summary>
    /// Per-actor authority for one real-time wound-treatment operation. It owns only temporal
    /// continuity; InventoryItemUseService retains the exact-instance transactional commit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActorWoundTreatmentController : MonoBehaviour
    {
        private ActorRuntimeIdentity identity;
        private ActorHealthComponent health;
        private ActorConditionComponent condition;
        private ActorMedicalStateComponent medical;
        private ActorItemOwnershipComponent ownership;
        private PlayerMovementController playerMovement;
        private DebugActionProgressController playerActionProgress;
        private double startedAtRealTime;
        private float durationSeconds;
        private string woundId;
        private string woundRegion;
        private string treatmentItemInstanceId;
        private string treatmentDefinitionId;

        public bool IsTreating { get; private set; }
        public ActorWoundTreatmentPurpose Purpose { get; private set; }
        public ActorWoundTreatmentOutcome LastOutcome { get; private set; }
        public string LastMessage { get; private set; }
        public string WoundId => woundId;
        public string WoundRegion => woundRegion;
        public string TreatmentItemInstanceId => treatmentItemInstanceId;
        public string TreatmentDefinitionId => treatmentDefinitionId;
        public float DurationSeconds => durationSeconds;
        public float ElapsedSeconds => IsTreating
            ? Mathf.Clamp((float)(Time.realtimeSinceStartupAsDouble - startedAtRealTime), 0f, durationSeconds)
            : 0f;
        public float RemainingSeconds => IsTreating ? Mathf.Max(0f, durationSeconds - ElapsedSeconds) : 0f;
        public float Progress => IsTreating && durationSeconds > 0f
            ? Mathf.Clamp01(ElapsedSeconds / durationSeconds)
            : LastOutcome == ActorWoundTreatmentOutcome.Completed ? 1f : 0f;
        public int StartedCount { get; private set; }
        public int CompletedCount { get; private set; }
        public int CancelledCount { get; private set; }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnDisable()
        {
            if (IsTreating)
                Cancel("Treatment controller disabled");
        }

        private void Update()
        {
            if (!IsTreating)
                return;
            ResolveReferences();
            if (!CanActorContinue(out string failure))
            {
                Cancel(failure);
                return;
            }
            if (playerMovement != null && playerMovement.IsSprinting)
            {
                Cancel("Sprint started during wound treatment");
                return;
            }
            if (Time.realtimeSinceStartupAsDouble - startedAtRealTime < durationSeconds)
                return;
            Complete();
        }

        public bool TryStart(
            string targetWoundId,
            ActorWoundTreatmentPurpose purpose,
            out string failure)
        {
            ResolveReferences();
            if (!InventoryItemUseService.TryFindOwnedWoundTreatment(
                    ownership, out string itemInstanceId, out _, out failure))
                return FailStart(failure);
            return TryStart(targetWoundId, itemInstanceId, purpose, out failure);
        }

        public bool TryStart(
            string targetWoundId,
            string exactTreatmentItemInstanceId,
            ActorWoundTreatmentPurpose purpose,
            out string failure)
        {
            ResolveReferences();
            if (IsTreating)
            {
                failure = "This actor already has a wound treatment in progress.";
                return FailStart(failure);
            }
            if (!CanActorContinue(out failure))
                return FailStart(failure);
            if (playerMovement != null && playerMovement.IsSprinting)
            {
                failure = "Cannot start wound treatment while sprinting.";
                return FailStart(failure);
            }
            if (playerActionProgress != null && playerActionProgress.IsActionInProgress)
            {
                failure = "Cannot start wound treatment while another timed action is active.";
                return FailStart(failure);
            }
            if (!InventoryItemUseService.TryGetOwnedWoundTreatment(
                    ownership,
                    exactTreatmentItemInstanceId,
                    out ItemWoundTreatment treatment,
                    out string definitionId,
                    out failure))
                return FailStart(failure);
            if (!FinitePositive(treatment.application_seconds))
            {
                failure = "Wound treatment application duration must be finite and positive.";
                return FailStart(failure);
            }
            if (medical == null || !medical.CanApplyBandage(targetWoundId, treatment.bleeding_multiplier, out failure))
                return FailStart(failure ?? "Actor medical state is unavailable.");

            ActorMedicalWoundState wound = medical.GetWound(targetWoundId);
            woundId = targetWoundId;
            woundRegion = wound?.region;
            treatmentItemInstanceId = exactTreatmentItemInstanceId;
            treatmentDefinitionId = definitionId;
            durationSeconds = treatment.application_seconds;
            startedAtRealTime = Time.realtimeSinceStartupAsDouble;
            Purpose = purpose;
            IsTreating = true;
            LastOutcome = ActorWoundTreatmentOutcome.InProgress;
            LastMessage = "Wound treatment started.";
            StartedCount++;
            failure = null;
            return true;
        }

        public bool Cancel(string reason)
        {
            if (!IsTreating)
                return false;
            IsTreating = false;
            LastOutcome = ActorWoundTreatmentOutcome.Cancelled;
            LastMessage = string.IsNullOrWhiteSpace(reason) ? "Wound treatment cancelled." : reason;
            CancelledCount++;
            ClearActiveOperation();
            return true;
        }

        private void Complete()
        {
            InventoryItemUseResult result = InventoryItemUseService.TryCommitWoundTreatment(
                ownership, medical, woundId, treatmentItemInstanceId);
            IsTreating = false;
            LastOutcome = result.Success
                ? ActorWoundTreatmentOutcome.Completed
                : ActorWoundTreatmentOutcome.Failed;
            LastMessage = result.Message;
            if (result.Success)
                CompletedCount++;
            ClearActiveOperation();
        }

        private bool CanActorContinue(out string failure)
        {
            if (health == null || health.IsDead ||
                identity != null && identity.LifecycleState == ActorLifecycleState.Dead)
            {
                failure = "Dead actor cannot perform wound treatment.";
                return false;
            }
            if (condition == null)
            {
                failure = "Actor condition authority is unavailable.";
                return false;
            }
            if (!condition.CanPerformActiveActions)
            {
                failure = "Functionally incapacitated actor cannot perform wound treatment.";
                return false;
            }
            failure = null;
            return true;
        }

        private bool FailStart(string failure)
        {
            LastOutcome = ActorWoundTreatmentOutcome.Failed;
            LastMessage = failure;
            return false;
        }

        private void ClearActiveOperation()
        {
            startedAtRealTime = 0d;
            durationSeconds = 0f;
            woundId = null;
            woundRegion = null;
            treatmentItemInstanceId = null;
            treatmentDefinitionId = null;
        }

        private void ResolveReferences()
        {
            if (identity == null) identity = GetComponent<ActorRuntimeIdentity>();
            if (health == null) health = GetComponent<ActorHealthComponent>();
            if (condition == null) condition = GetComponent<ActorConditionComponent>();
            if (medical == null) medical = GetComponent<ActorMedicalStateComponent>();
            if (ownership == null) ownership = GetComponent<ActorItemOwnershipComponent>();
            if (playerMovement == null) playerMovement = GetComponent<PlayerMovementController>();
            if (playerActionProgress == null) playerActionProgress = GetComponent<DebugActionProgressController>();
        }

        private static bool FinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }
}
