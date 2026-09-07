using System;
using System.Linq;
using Newtonsoft.Json;
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
        [JsonProperty(Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
        public double? unconsciousDwellRemainingSeconds;
        public bool unconsciousRecoveryPending;
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
        private float bloodRecoveryPerGameHour = 0.02f;
        private float recoveryHysteresis = 0.05f;
        private float minimumUnconsciousRealSeconds = 5f;
        private double unconsciousDwellRemainingSeconds;
        private double dwellLastRealTime;

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
        public float BloodRecoveryPerGameHour => bloodRecoveryPerGameHour;
        public float RecoveryHysteresis => recoveryHysteresis;
        public double UnconsciousDwellRemainingSeconds => unconsciousDwellRemainingSeconds;
        public bool IsUnconsciousDwellActive => IsUnconscious && unconsciousDwellRemainingSeconds > 0d;
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

        private void Update()
        {
            if (!IsUnconsciousDwellActive)
                return;
            double now = Time.realtimeSinceStartupAsDouble;
            unconsciousDwellRemainingSeconds = Math.Max(0d,
                unconsciousDwellRemainingSeconds - Math.Max(0d, now - dwellLastRealTime));
            dwellLastRealTime = now;
            if (unconsciousDwellRemainingSeconds == 0d)
            {
                Revision++;
                RecalculateConsciousness();
            }
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
            bloodRecoveryPerGameHour = profile.blood_recovery_per_game_hour;
            recoveryHysteresis = profile.recovery_hysteresis;
            minimumUnconsciousRealSeconds = profile.minimum_unconscious_real_seconds;
            configured = true;
            RecalculateConsciousness(false);
            return true;
        }

        internal bool ApplyImmediateTraumaForNewWound(BodyRegion region, WoundType woundType, float severity)
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
            else if (bloodFraction > fatalBloodFraction && bloodFraction < 1f)
                bloodFraction = Mathf.Min(1f, bloodFraction + bloodRecoveryPerGameHour * elapsedHours);
            transientTrauma = Mathf.Max(0f, transientTrauma - traumaRecoveryPerGameHour * elapsedHours);

            bool changed = !Mathf.Approximately(previousBlood, bloodFraction) ||
                           !Mathf.Approximately(previousTrauma, transientTrauma);
            if (changed)
                Revision++;
            RecalculateConsciousness();

            if (bloodFraction <= fatalBloodFraction && health != null && !health.IsDead)
                health.Kill();
            return changed;
        }

        public ActorConditionStateData CaptureState()
        {
            return new ActorConditionStateData
            {
                bloodFraction = bloodFraction,
                transientTrauma = transientTrauma,
                // Capture the last evaluated frame atomically: synchronous load/rollback
                // comparisons must not consume wall time between individual actors.
                unconsciousDwellRemainingSeconds = unconsciousDwellRemainingSeconds,
                unconsciousRecoveryPending = IsUnconscious
            };
        }

        public bool TryApplyPersistenceState(ActorConditionStateData state, out string failure)
        {
            if (!TryValidatePersistenceState(state, out failure))
                return false;
            bloodFraction = state.bloodFraction;
            transientTrauma = state.transientTrauma;
            bool legacy = !state.unconsciousDwellRemainingSeconds.HasValue;
            unconsciousDwellRemainingSeconds = state.unconsciousDwellRemainingSeconds ?? 0d;
            dwellLastRealTime = Time.realtimeSinceStartupAsDouble;
            Revision++;
            RecalculateConsciousness(false, legacy ? (bool?)null : state.unconsciousRecoveryPending, legacy);
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
            if (state.unconsciousDwellRemainingSeconds.HasValue &&
                (!Finite(state.unconsciousDwellRemainingSeconds.Value) ||
                 state.unconsciousDwellRemainingSeconds.Value < 0d ||
                 state.unconsciousDwellRemainingSeconds.Value > 0d && !state.unconsciousRecoveryPending) ||
                !state.unconsciousDwellRemainingSeconds.HasValue && state.unconsciousRecoveryPending)
            {
                failure = "Unconscious dwell must be finite, nonnegative and belong to an unconscious episode.";
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
                !FinitePositive(profile.trauma_recovery_per_game_hour) ||
                !FinitePositive(profile.blood_recovery_per_game_hour) ||
                !FiniteUnitPositive(profile.recovery_hysteresis) ||
                !FinitePositive(profile.minimum_unconscious_real_seconds))
            {
                failure = "Consciousness tuning must be finite and ordered; recovery hysteresis, blood/trauma recovery rates and minimum unconscious real seconds must be positive.";
                return false;
            }
            failure = null;
            return true;
        }

        internal void ResetForHealthInitialization(bool alive)
        {
            unconsciousDwellRemainingSeconds = 0d;
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

        private void RecalculateConsciousness(bool applyRecoveryHysteresis = true,
            bool? restoredUnconscious = null, bool legacyRestore = false)
        {
            ResolveReferences();
            ClampState();
            float totalPain = medical != null ? medical.TotalPain : 0f;
            ConsciousnessStability = IsDead() ? 0f : CalculateStability(bloodFraction, transientTrauma,
                totalPain, painTolerance, bloodPressureStartFraction, fatalBloodFraction, consciousnessResilience);

            ActorFunctionalState next = IsDead()
                ? ActorFunctionalState.Unconscious
                : restoredUnconscious == true
                    ? ResolveFunctionalState(ConsciousnessStability, ActorFunctionalState.Unconscious)
                    : applyRecoveryHysteresis
                        ? ResolveFunctionalState(ConsciousnessStability, FunctionalState)
                        : ResolveThresholdState(ConsciousnessStability);
            if (IsDead())
                unconsciousDwellRemainingSeconds = 0d;
            else if (unconsciousDwellRemainingSeconds > 0d)
                next = ActorFunctionalState.Unconscious;
            else if (next == ActorFunctionalState.Unconscious && !restoredUnconscious.HasValue &&
                     (FunctionalState != ActorFunctionalState.Unconscious || legacyRestore))
            {
                unconsciousDwellRemainingSeconds = minimumUnconsciousRealSeconds;
                dwellLastRealTime = Time.realtimeSinceStartupAsDouble;
            }
            if (next == FunctionalState)
                return;
            ActorFunctionalState previous = FunctionalState;
            FunctionalState = next;
            FunctionalStateChanged?.Invoke(previous, next);
        }

        private static float CalculateStability(float blood, float trauma, float totalPain,
            float painTolerance, float bloodPressureStartFraction, float fatalBloodFraction, float resilience)
        {
            float painPressure = Mathf.Clamp01(
                (totalPain - painTolerance) / Mathf.Max(0.001f, 1f - painTolerance));
            float circulatoryPressure = Mathf.Clamp01(
                (bloodPressureStartFraction - blood) /
                Mathf.Max(0.001f, bloodPressureStartFraction - fatalBloodFraction));
            float totalPressure = trauma + painPressure * PainPressureWeight +
                                  circulatoryPressure * CirculatoryPressureWeight;
            return Mathf.Clamp01(1f - totalPressure / Mathf.Max(0.01f, resilience));
        }

        // Schema-v1 additive normalization before transactional apply/comparison.
        internal static void NormalizeLegacyDwell(ActorConditionStateData state,
            ActorMedicalStateData medicalState, ActorProfileConsciousness profile, bool alive)
        {
            if (state == null || state.unconsciousDwellRemainingSeconds.HasValue || profile == null)
                return;
            float pain = Mathf.Clamp01(medicalState.wounds.Sum(wound => wound.painContribution));
            bool unconscious = alive && CalculateStability(state.bloodFraction, state.transientTrauma,
                pain, profile.pain_tolerance, profile.blood_pressure_start_fraction,
                profile.fatal_blood_fraction, profile.consciousness_resilience) < profile.unconscious_threshold;
            state.unconsciousRecoveryPending = unconscious;
            state.unconsciousDwellRemainingSeconds = unconscious ? profile.minimum_unconscious_real_seconds : 0d;
        }

        private ActorFunctionalState ResolveFunctionalState(float stability, ActorFunctionalState startingState)
        {
            ActorFunctionalState thresholdState = ResolveThresholdState(stability);
            if (thresholdState >= startingState)
                return thresholdState;

            ActorFunctionalState recovered = startingState;
            while (recovered > thresholdState && stability >= RecoveryThreshold(recovered))
                recovered = (ActorFunctionalState)((int)recovered - 1);
            return recovered;
        }

        private ActorFunctionalState ResolveThresholdState(float stability)
        {
            return stability < unconsciousThreshold
                ? ActorFunctionalState.Unconscious
                : stability < incapacitatedThreshold
                    ? ActorFunctionalState.Incapacitated
                    : stability < dazedThreshold
                        ? ActorFunctionalState.Dazed
                        : ActorFunctionalState.Conscious;
        }

        private float RecoveryThreshold(ActorFunctionalState state)
        {
            return state switch
            {
                ActorFunctionalState.Unconscious => Mathf.Min(1f, unconsciousThreshold + recoveryHysteresis),
                ActorFunctionalState.Incapacitated => Mathf.Min(1f, incapacitatedThreshold + recoveryHysteresis),
                ActorFunctionalState.Dazed => Mathf.Min(1f, dazedThreshold + recoveryHysteresis),
                _ => 1f
            };
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
