using System;
using OldScars.Core.Data.Definitions;
using UnityEngine;

namespace OldScars.Core.Actors
{
    public enum ActorFunctionalState
    {
        Conscious,
        Dazed,
        Incapacitated,
        Unconscious
    }

    [Serializable]
    public sealed class ActorConditionStateData
    {
        public float bloodFraction = float.NaN;
        public float transientTrauma = float.NaN;
    }

    /// <summary>
    /// Shared player/NPC authority for circulatory reserve, transient trauma,
    /// consciousness stability and functional capacity. Death remains owned by
    /// ActorHealthComponent and ActorRuntimeIdentity lifecycle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActorConditionComponent : MonoBehaviour
    {
        private const float PainPressureWeight = 0.45f;
        private const float CirculatoryPressureWeight = 0.9f;
        private const float BluntTraumaFactor = 0.65f;
        private const float PunctureTraumaFactor = 0.35f;
        private const float LacerationTraumaFactor = 0.25f;
        private const float HeadTraumaMultiplier = 1.8f;
        private const float TorsoTraumaMultiplier = 1f;
        private const float LimbTraumaMultiplier = 0.65f;

        [SerializeField] private float bloodFraction = 1f;
        [SerializeField] private float transientTrauma;

        private float consciousnessResilience = 1f;
        private float painTolerance = 0.35f;
        private float bluntTraumaResistance = 1f;
        private float dazedThreshold = 0.75f;
        private float incapacitatedThreshold = 0.45f;
        private float unconsciousThreshold = 0.2f;
        private float bloodPressureStartFraction = 0.65f;
        private float fatalBloodFraction = 0.08f;
        private float traumaRecoveryPerGameHour = 0.6f;

        private ActorHealthComponent health;
        private ActorMedicalStateComponent medical;
        private ActorRuntimeIdentity identity;
        private WorldClock connectedClock;
        private bool configured;

        public bool IsConfigured => configured;
        public float BloodFraction => bloodFraction;
        public float TransientTrauma => transientTrauma;
        public float ConsciousnessStability { get; private set; } = 1f;
        public ActorFunctionalState FunctionalState { get; private set; } = ActorFunctionalState.Conscious;
        public bool CanPerformActiveActions => !IsDead() &&
            (FunctionalState == ActorFunctionalState.Conscious || FunctionalState == ActorFunctionalState.Dazed);
        public bool IsUnconscious => !IsDead() && FunctionalState == ActorFunctionalState.Unconscious;
        public string ObservableState => IsDead() ? "Dead" : FunctionalState.ToString();
        public float FatalBloodFraction => fatalBloodFraction;
        public int Revision { get; private set; }

        public event Action<ActorFunctionalState, ActorFunctionalState> FunctionalStateChanged;

        private void Awake()
        {
            ResolveReferences();
            ClampState();
            RecalculateConsciousness();
        }

        private void OnEnable()
        {
            ConnectWorldClock(WorldClock.Current);
        }

        private void Start()
        {
            ConnectWorldClock(WorldClock.Current);
        }

        private void OnDisable()
        {
            ConnectWorldClock(null);
        }

        public bool TryConfigure(ActorProfileConsciousness profile, out string failure)
        {
            failure = null;
            if (!TryValidateProfile(profile, out failure))
                return false;

            consciousnessResilience = profile.consciousness_resilience;
            painTolerance = profile.pain_tolerance;
            bluntTraumaResistance = profile.blunt_trauma_resistance;
            dazedThreshold = profile.dazed_threshold;
            incapacitatedThreshold = profile.incapacitated_threshold;
            unconsciousThreshold = profile.unconscious_threshold;
            bloodPressureStartFraction = profile.blood_pressure_start_fraction;
            fatalBloodFraction = profile.fatal_blood_fraction;
            traumaRecoveryPerGameHour = profile.trauma_recovery_per_game_hour;
            configured = true;
            RecalculateConsciousness();
            return true;
        }

        public bool ApplyImmediateTrauma(BodyRegion region, WoundType woundType, float severity)
        {
            ResolveReferences();
            if (IsDead() || !Finite(severity) || severity <= 0f)
                return false;

            float typeFactor = woundType switch
            {
                WoundType.Blunt => BluntTraumaFactor / Mathf.Max(0.01f, bluntTraumaResistance),
                WoundType.Puncture => PunctureTraumaFactor,
                _ => LacerationTraumaFactor
            };
            float regionFactor = region == BodyRegion.Head
                ? HeadTraumaMultiplier
                : region == BodyRegion.Torso ? TorsoTraumaMultiplier : LimbTraumaMultiplier;
            float previous = transientTrauma;
            transientTrauma = Mathf.Clamp01(transientTrauma + severity * typeFactor * regionFactor);
            if (Mathf.Approximately(previous, transientTrauma))
                return false;

            Revision++;
            RecalculateConsciousness();
            return true;
        }

        public bool AdvancePhysiology(double elapsedGameSeconds)
        {
            ResolveReferences();
            if (!Finite(elapsedGameSeconds) || elapsedGameSeconds <= 0d || IsDead() || medical == null)
                return false;

            float elapsedHours = (float)(elapsedGameSeconds / WorldClock.SecondsPerHour);
            float bleedingRate = medical.EffectiveBleedingRatePerGameHour;
            float previousBlood = bloodFraction;
            float previousTrauma = transientTrauma;
            if (bleedingRate > 0f)
                bloodFraction = Mathf.Clamp01(bloodFraction - bleedingRate * elapsedHours);
            else if (bloodFraction > fatalBloodFraction)
                transientTrauma = Mathf.Max(0f, transientTrauma - traumaRecoveryPerGameHour * elapsedHours);

            bool changed = !Mathf.Approximately(previousBlood, bloodFraction) ||
                           !Mathf.Approximately(previousTrauma, transientTrauma);
            if (changed)
                Revision++;
            RecalculateConsciousness();

            if (bloodFraction <= fatalBloodFraction && health != null && !health.IsDead)
                health.ApplyDamage(health.CurrentHealth);
            return changed;
        }

        public ActorConditionStateData CaptureState()
        {
            return new ActorConditionStateData
            {
                bloodFraction = bloodFraction,
                transientTrauma = transientTrauma
            };
        }

        public bool TryApplyPersistenceState(ActorConditionStateData state, out string failure)
        {
            if (!TryValidatePersistenceState(state, out failure))
                return false;
            bloodFraction = state.bloodFraction;
            transientTrauma = state.transientTrauma;
            Revision++;
            RecalculateConsciousness();
            return true;
        }

        public static ActorConditionStateData HealthyBaseline()
        {
            return new ActorConditionStateData { bloodFraction = 1f, transientTrauma = 0f };
        }

        public static bool TryValidatePersistenceState(ActorConditionStateData state, out string failure)
        {
            if (state == null)
            {
                failure = "Actor condition state is missing.";
                return false;
            }
            if (!Finite(state.bloodFraction) || state.bloodFraction < 0f || state.bloodFraction > 1f)
            {
                failure = $"Blood fraction '{state.bloodFraction}' must be finite and within 0..1.";
                return false;
            }
            if (!Finite(state.transientTrauma) || state.transientTrauma < 0f || state.transientTrauma > 1f)
            {
                failure = $"Transient trauma '{state.transientTrauma}' must be finite and within 0..1.";
                return false;
            }
            failure = null;
            return true;
        }

        public static bool TryValidateProfile(ActorProfileConsciousness profile, out string failure)
        {
            if (profile == null)
            {
                failure = "Consciousness profile is missing.";
                return false;
            }
            if (!FinitePositive(profile.consciousness_resilience) ||
                !FiniteUnit(profile.pain_tolerance) ||
                !FinitePositive(profile.blunt_trauma_resistance) ||
                !FiniteUnitPositive(profile.dazed_threshold) ||
                !FiniteUnitPositive(profile.incapacitated_threshold) ||
                !FiniteUnitPositive(profile.unconscious_threshold) ||
                !(profile.unconscious_threshold < profile.incapacitated_threshold &&
                  profile.incapacitated_threshold < profile.dazed_threshold) ||
                !FiniteUnitPositive(profile.blood_pressure_start_fraction) ||
                !FiniteUnit(profile.fatal_blood_fraction) ||
                profile.fatal_blood_fraction >= profile.blood_pressure_start_fraction ||
                !FinitePositive(profile.trauma_recovery_per_game_hour))
            {
                failure = "Consciousness tuning must be finite and ordered: unconscious < incapacitated < dazed, and fatal blood < blood-pressure start.";
                return false;
            }
            failure = null;
            return true;
        }

        internal void ResetForHealthInitialization(bool alive)
        {
            if (alive)
            {
                bloodFraction = 1f;
                transientTrauma = 0f;
                Revision++;
            }
            RecalculateConsciousness();
        }

        internal void RecalculateFromMedicalState()
        {
            RecalculateConsciousness();
        }

        private void ConnectWorldClock(WorldClock clock)
        {
            if (connectedClock != clock)
            {
                if (connectedClock != null)
                    connectedClock.GameTimeAdvanced -= OnGameTimeAdvanced;
                connectedClock = clock;
            }
            if (connectedClock != null && isActiveAndEnabled)
            {
                connectedClock.GameTimeAdvanced -= OnGameTimeAdvanced;
                connectedClock.GameTimeAdvanced += OnGameTimeAdvanced;
            }
        }

        private void OnGameTimeAdvanced(double elapsedGameSeconds)
        {
            AdvancePhysiology(elapsedGameSeconds);
        }

        private void RecalculateConsciousness()
        {
            ResolveReferences();
            ClampState();
            float totalPain = medical != null ? medical.TotalPain : 0f;
            float painPressure = Mathf.Clamp01(
                (totalPain - painTolerance) / Mathf.Max(0.001f, 1f - painTolerance));
            float circulatoryPressure = Mathf.Clamp01(
                (bloodPressureStartFraction - bloodFraction) /
                Mathf.Max(0.001f, bloodPressureStartFraction - fatalBloodFraction));
            float totalPressure = transientTrauma + painPressure * PainPressureWeight +
                                  circulatoryPressure * CirculatoryPressureWeight;
            ConsciousnessStability = IsDead()
                ? 0f
                : Mathf.Clamp01(1f - totalPressure / Mathf.Max(0.01f, consciousnessResilience));

            ActorFunctionalState next = ConsciousnessStability < unconsciousThreshold
                ? ActorFunctionalState.Unconscious
                : ConsciousnessStability < incapacitatedThreshold
                    ? ActorFunctionalState.Incapacitated
                    : ConsciousnessStability < dazedThreshold
                        ? ActorFunctionalState.Dazed
                        : ActorFunctionalState.Conscious;
            if (next == FunctionalState)
                return;
            ActorFunctionalState previous = FunctionalState;
            FunctionalState = next;
            FunctionalStateChanged?.Invoke(previous, next);
        }

        private bool IsDead()
        {
            ResolveReferences();
            return health != null && health.IsDead ||
                   identity != null && identity.LifecycleState == ActorLifecycleState.Dead;
        }

        private void ResolveReferences()
        {
            if (health == null) health = GetComponent<ActorHealthComponent>();
            if (medical == null) medical = GetComponent<ActorMedicalStateComponent>();
            if (identity == null) identity = GetComponent<ActorRuntimeIdentity>();
        }

        private void ClampState()
        {
            bloodFraction = Mathf.Clamp01(bloodFraction);
            transientTrauma = Mathf.Clamp01(transientTrauma);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool FinitePositive(float value) => Finite(value) && value > 0f;
        private static bool FiniteUnit(float value) => Finite(value) && value >= 0f && value < 1f;
        private static bool FiniteUnitPositive(float value) => Finite(value) && value > 0f && value < 1f;
    }
}
