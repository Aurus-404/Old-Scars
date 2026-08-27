using System;
using UnityEngine;

namespace OldScars.Core.Actors
{
    [Serializable]
    public sealed class ActorStaminaState
    {
        public float currentStamina;
        public bool exhaustedLockout;

        public ActorStaminaState()
        {
        }

        public ActorStaminaState(float currentStamina, bool exhaustedLockout)
        {
            this.currentStamina = currentStamina;
            this.exhaustedLockout = exhaustedLockout;
        }
    }

    /// <summary>
    /// Small actor-local stamina authority. It uses active real gameplay time;
    /// baseline needs continue to be advanced only by WorldClock game time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActorStaminaComponent : MonoBehaviour
    {
        [Header("Stamina")]
        [SerializeField, Min(0.01f)] private float maximumStamina = 100f;
        [SerializeField, Min(0f)] private float initialStamina = 100f;
        [SerializeField, Min(0f)] private float sprintDrainPerRealSecond = 16f;
        [SerializeField, Min(0f)] private float recoveryPerRealSecond = 20f;
        [SerializeField, Min(0f)] private float sprintRecoveryThreshold = 20f;

        [Header("Needs Interaction")]
        [SerializeField, Range(0.01f, 1f)] private float minimumRecoveryFactor = 0.25f;
        [SerializeField, Min(0f)] private float extraSprintHungerCostPerRealSecond = 0.05f;
        [SerializeField, Min(0f)] private float extraSprintThirstCostPerRealSecond = 0.08f;
        [SerializeField, Min(1f)] private float lowStaminaExertionMultiplier = 2f;

        [SerializeField] private float currentStamina;
        [SerializeField] private bool exhaustedLockout;

        private ActorNeedsComponent needs;
        private ActorHealthComponent health;
        private ActorRuntimeIdentity identity;
        private bool initialized;

        public float MaximumStamina => Mathf.Max(0.01f, maximumStamina);
        public float CurrentStamina => currentStamina;
        public float SprintRecoveryThreshold => Mathf.Clamp(sprintRecoveryThreshold, 0f, MaximumStamina);
        public bool IsExhausted => exhaustedLockout;
        public bool CanSprint => !IsDead() && !exhaustedLockout && currentStamina > 0f;
        public float LastRecoveryFactor { get; private set; } = 1f;

        public event Action<ActorStaminaState> StaminaChanged;

        private void Awake()
        {
            ResolveDependencies();
            InitializeRuntimeState();
        }

        private void OnEnable()
        {
            ResolveDependencies();
            InitializeRuntimeState();
        }

        public bool Advance(float elapsedRealSeconds, bool actuallySprinting)
        {
            if (!IsFinite(elapsedRealSeconds) || elapsedRealSeconds <= 0f || IsDead())
                return false;

            float previousStamina = currentStamina;
            bool previousLockout = exhaustedLockout;
            if (actuallySprinting && CanSprint)
            {
                float fatigue01 = 1f - Mathf.Clamp01(currentStamina / MaximumStamina);
                float exertionMultiplier = Mathf.Lerp(1f, Mathf.Max(1f, lowStaminaExertionMultiplier), fatigue01);
                currentStamina = Mathf.Max(0f, currentStamina - Mathf.Max(0f, sprintDrainPerRealSecond) * elapsedRealSeconds);
                needs?.TryConsumeNeed("hunger", Mathf.Max(0f, extraSprintHungerCostPerRealSecond) * exertionMultiplier * elapsedRealSeconds);
                needs?.TryConsumeNeed("thirst", Mathf.Max(0f, extraSprintThirstCostPerRealSecond) * exertionMultiplier * elapsedRealSeconds);
                if (currentStamina <= 0f)
                    exhaustedLockout = true;
            }
            else
            {
                LastRecoveryFactor = CalculateRecoveryFactor();
                currentStamina = Mathf.Min(
                    MaximumStamina,
                    currentStamina + Mathf.Max(0f, recoveryPerRealSecond) * LastRecoveryFactor * elapsedRealSeconds);
                if (exhaustedLockout &&
                    (currentStamina >= SprintRecoveryThreshold ||
                     Mathf.Approximately(currentStamina, SprintRecoveryThreshold)))
                    exhaustedLockout = false;
            }

            if (Mathf.Approximately(previousStamina, currentStamina) && previousLockout == exhaustedLockout)
                return false;

            NotifyChanged();
            return true;
        }

        public bool TrySetCurrentStamina(float value)
        {
            if (!IsFinite(value) || value < 0f || value > MaximumStamina)
                return false;

            bool previousLockout = exhaustedLockout;
            float previousStamina = currentStamina;
            currentStamina = value;
            if (currentStamina <= 0f)
                exhaustedLockout = true;
            else if (currentStamina >= SprintRecoveryThreshold)
                exhaustedLockout = false;

            if (Mathf.Approximately(previousStamina, currentStamina) && previousLockout == exhaustedLockout)
                return false;

            NotifyChanged();
            return true;
        }

        public ActorStaminaState CapturePersistenceState()
        {
            return new ActorStaminaState(currentStamina, exhaustedLockout);
        }

        internal bool TryApplyPersistenceState(ActorStaminaState state, out string error)
        {
            error = null;
            if (state == null || !IsFinite(state.currentStamina) ||
                state.currentStamina < 0f || state.currentStamina > MaximumStamina)
            {
                error = "Persistence stamina state is missing or outside its configured range.";
                return false;
            }

            if (state.currentStamina <= 0f && !state.exhaustedLockout)
            {
                error = "Persistence stamina at zero must retain exhaustion lockout.";
                return false;
            }

            currentStamina = state.currentStamina;
            exhaustedLockout = state.exhaustedLockout;
            NotifyChanged();
            return true;
        }

        internal ActorStaminaState CreateDefaultPersistenceState()
        {
            return new ActorStaminaState(MaximumStamina, false);
        }

        private float CalculateRecoveryFactor()
        {
            ResolveDependencies();
            float hungerReserve = GetReserveRatio("hunger");
            float thirstReserve = GetReserveRatio("thirst");
            float limitingReserve = Mathf.Min(hungerReserve, thirstReserve);
            return Mathf.Lerp(Mathf.Clamp01(minimumRecoveryFactor), 1f, limitingReserve);
        }

        private float GetReserveRatio(string needId)
        {
            if (needs == null || !needs.HasNeed(needId))
                return 1f;

            float maximum = needs.GetNeedMaxValue(needId);
            return maximum <= 0f ? 1f : Mathf.Clamp01(needs.GetNeedValue(needId) / maximum);
        }

        private bool IsDead()
        {
            ResolveDependencies();
            return health != null && health.IsDead ||
                   identity != null && identity.LifecycleState == ActorLifecycleState.Dead;
        }

        private void ResolveDependencies()
        {
            if (needs == null)
                needs = GetComponent<ActorNeedsComponent>();
            if (health == null)
                health = GetComponent<ActorHealthComponent>();
            if (identity == null)
                identity = GetComponent<ActorRuntimeIdentity>();
        }

        private void InitializeRuntimeState()
        {
            if (initialized)
                return;

            currentStamina = Mathf.Clamp(initialStamina, 0f, MaximumStamina);
            exhaustedLockout = currentStamina <= 0f;
            initialized = true;
        }

        private void NotifyChanged()
        {
            StaminaChanged?.Invoke(CapturePersistenceState());
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
