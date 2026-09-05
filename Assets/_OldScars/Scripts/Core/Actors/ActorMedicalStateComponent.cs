using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OldScars.Core.Actors
{
    public enum BodyRegion
    {
        Head,
        Torso,
        LeftArm,
        RightArm,
        LeftLeg,
        RightLeg
    }

    public enum WoundType
    {
        Laceration,
        Puncture,
        Blunt
    }

    public enum WoundTreatmentState
    {
        Unbandaged,
        Bandaged
    }

    [Serializable]
    public sealed class ActorMedicalWoundState
    {
        public string woundId;
        public string region;
        public string woundType;
        public float severity = float.NaN;
        public float bleedingRatePerGameHour = float.NaN;
        public float painContribution = float.NaN;
        public string treatmentState;
        public float treatmentBleedingMultiplier = float.NaN;
    }

    [Serializable]
    public sealed class ActorMedicalStateData
    {
        public ActorMedicalWoundState[] wounds;
    }

    [DisallowMultipleComponent]
    public sealed class ActorMedicalStateComponent : MonoBehaviour
    {
        public const float MaxSeverity = 1f;
        public const float MaxBleedingRatePerGameHour = 1f;
        public const float MaxPainContribution = 1f;
        private const string WoundPrefix = "wound_";

        private static readonly BodyRegion[] HumanRegionValues =
        {
            BodyRegion.Head,
            BodyRegion.Torso,
            BodyRegion.LeftArm,
            BodyRegion.RightArm,
            BodyRegion.LeftLeg,
            BodyRegion.RightLeg
        };

        private readonly List<ActorMedicalWoundState> wounds = new List<ActorMedicalWoundState>();
        private ActorHealthComponent actorHealth;
        private ActorRuntimeIdentity actorIdentity;
        private ActorConditionComponent actorCondition;
        private int revision;

        public static IReadOnlyList<BodyRegion> HumanRegions => HumanRegionValues;
        public int WoundCount => wounds.Count;
        public int Revision => revision;
        public float VitalFraction
        {
            get
            {
                ResolveActor();
                return actorHealth != null && actorHealth.MaxHealth > 0f
                    ? Mathf.Clamp01(actorHealth.CurrentHealth / actorHealth.MaxHealth)
                    : 0f;
            }
        }
        public float TotalPain => Mathf.Clamp01(wounds.Sum(wound => wound.painContribution));
        public float EffectiveBleedingRatePerGameHour => Mathf.Clamp(
            wounds.Sum(EffectiveBleedingRate), 0f, MaxBleedingRatePerGameHour);

        private void Awake()
        {
            ResolveActor();
        }

        public bool ApplyWound(
            BodyRegion region,
            WoundType woundType,
            float severity,
            out string woundId,
            out string failure)
        {
            float bleeding = DefaultBleedingRate(woundType, severity);
            float pain = DefaultPainContribution(woundType, severity);
            woundId = WoundPrefix + Guid.NewGuid().ToString("N");
            if (TryApplyWound(woundId, region, woundType, severity, bleeding, pain, out failure))
                return true;
            woundId = null;
            return false;
        }

        public bool TryApplyWound(
            string woundId,
            BodyRegion region,
            WoundType woundType,
            float severity,
            float bleedingRatePerGameHour,
            float painContribution,
            out string failure)
        {
            var candidate = new ActorMedicalWoundState
            {
                woundId = woundId,
                region = region.ToString(),
                woundType = woundType.ToString(),
                severity = severity,
                bleedingRatePerGameHour = bleedingRatePerGameHour,
                painContribution = painContribution,
                treatmentState = WoundTreatmentState.Unbandaged.ToString(),
                treatmentBleedingMultiplier = 1f
            };
            if (!TryValidateWound(candidate, out failure))
                return false;
            if (wounds.Any(wound => string.Equals(wound.woundId, woundId, StringComparison.Ordinal)))
            {
                failure = $"WoundId '{woundId}' already exists on actor '{ActorId()}'.";
                return false;
            }

            ResolveActor();
            if (IsDead())
            {
                failure = $"Dead actor '{ActorId()}' cannot receive new wound progression.";
                return false;
            }

            wounds.Add(Clone(candidate));
            revision++;
            ResolveActor();
            actorCondition?.ApplyImmediateTraumaForNewWound(region, woundType, severity);
            failure = null;
            return true;
        }

        public bool CanApplyBandage(string woundId, float bleedingMultiplier, out string failure)
        {
            ActorMedicalWoundState wound = FindWound(woundId);
            if (wound == null)
            {
                failure = $"Wound '{Value(woundId)}' was not found on actor '{ActorId()}'.";
                return false;
            }
            if (!Finite(bleedingMultiplier) || bleedingMultiplier < 0f || bleedingMultiplier >= 1f)
            {
                failure = $"Bandage bleeding multiplier must be finite and within 0..<1; received {bleedingMultiplier}.";
                return false;
            }
            if (wound.bleedingRatePerGameHour <= 0f)
            {
                failure = $"Wound '{woundId}' is not bleeding and does not accept a bandage.";
                return false;
            }
            if (wound.treatmentState != WoundTreatmentState.Unbandaged.ToString())
            {
                failure = $"Wound '{woundId}' is already bandaged.";
                return false;
            }
            if (IsDead())
            {
                failure = $"Dead actor '{ActorId()}' cannot receive treatment.";
                return false;
            }
            failure = null;
            return true;
        }

        public bool TryApplyBandage(string woundId, float bleedingMultiplier, out string failure)
        {
            if (!CanApplyBandage(woundId, bleedingMultiplier, out failure))
                return false;

            ActorMedicalWoundState wound = FindWound(woundId);
            wound.treatmentState = WoundTreatmentState.Bandaged.ToString();
            wound.treatmentBleedingMultiplier = bleedingMultiplier;
            revision++;
            return true;
        }

        public ActorMedicalWoundState GetWound(string woundId)
        {
            ActorMedicalWoundState wound = FindWound(woundId);
            return wound != null ? Clone(wound) : null;
        }

        public ActorMedicalWoundState[] GetWounds(BodyRegion region)
        {
            string regionId = region.ToString();
            return wounds.Where(wound => wound.region == regionId).Select(Clone).ToArray();
        }

        public ActorMedicalStateData CaptureState()
        {
            return new ActorMedicalStateData
            {
                wounds = wounds.OrderBy(wound => wound.woundId, StringComparer.Ordinal).Select(Clone).ToArray()
            };
        }

        public bool TryApplyPersistenceState(ActorMedicalStateData state, out string failure)
        {
            if (!TryValidatePersistenceState(state, out failure))
                return false;
            wounds.Clear();
            wounds.AddRange((state.wounds ?? Array.Empty<ActorMedicalWoundState>()).Select(Clone));
            revision++;
            ResolveActor();
            actorCondition?.RecalculateFromMedicalState();
            return true;
        }

        internal bool TryRestoreTransactionState(ActorMedicalStateData state, int restoredRevision, out string failure)
        {
            if (!TryValidatePersistenceState(state, out failure) || restoredRevision < 0)
            {
                if (string.IsNullOrWhiteSpace(failure))
                    failure = "Medical transaction revision is invalid.";
                return false;
            }
            wounds.Clear();
            wounds.AddRange((state.wounds ?? Array.Empty<ActorMedicalWoundState>()).Select(Clone));
            revision = restoredRevision;
            return true;
        }

        public static bool TryValidatePersistenceState(ActorMedicalStateData state, out string failure)
        {
            if (state == null)
            {
                failure = "Localized medical state is missing.";
                return false;
            }
            if (state.wounds == null)
            {
                failure = "Localized medical wounds must not be null.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < state.wounds.Length; index++)
            {
                ActorMedicalWoundState wound = state.wounds[index];
                if (!TryValidateWound(wound, out failure))
                {
                    failure = $"wounds[{index}]: {failure}";
                    return false;
                }
                if (!ids.Add(wound.woundId))
                {
                    failure = $"wounds[{index}]: duplicate WoundId '{wound.woundId}'.";
                    return false;
                }
            }
            failure = null;
            return true;
        }

        public static ActorMedicalStateData HealthyBaseline()
        {
            return new ActorMedicalStateData { wounds = Array.Empty<ActorMedicalWoundState>() };
        }

        public static bool IsValidWoundId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(WoundPrefix, StringComparison.Ordinal) ||
                value.Length != WoundPrefix.Length + 32)
                return false;
            for (int index = WoundPrefix.Length; index < value.Length; index++)
            {
                char character = value[index];
                if (!char.IsDigit(character) && (character < 'a' || character > 'f'))
                    return false;
            }
            return true;
        }

        public static float EffectiveBleedingRate(ActorMedicalWoundState wound)
        {
            if (wound == null)
                return 0f;
            return wound.bleedingRatePerGameHour * wound.treatmentBleedingMultiplier;
        }

        public static string SeverityLabel(float severity)
        {
            if (severity < 0.25f) return "Minor";
            if (severity < 0.5f) return "Moderate";
            if (severity < 0.75f) return "Severe";
            return "Critical";
        }

        private bool IsDead()
        {
            ResolveActor();
            return actorHealth != null && actorHealth.IsDead ||
                   actorIdentity != null && actorIdentity.LifecycleState == ActorLifecycleState.Dead;
        }

        private void ResolveActor()
        {
            if (actorHealth == null)
                actorHealth = GetComponent<ActorHealthComponent>();
            if (actorIdentity == null)
                actorIdentity = GetComponent<ActorRuntimeIdentity>();
            if (actorCondition == null)
                actorCondition = GetComponent<ActorConditionComponent>();
        }

        private ActorMedicalWoundState FindWound(string woundId)
        {
            return wounds.FirstOrDefault(wound => string.Equals(wound.woundId, woundId, StringComparison.Ordinal));
        }

        private string ActorId()
        {
            ResolveActor();
            return actorIdentity != null && !string.IsNullOrWhiteSpace(actorIdentity.ActorInstanceId)
                ? actorIdentity.ActorInstanceId
                : name;
        }

        private static float DefaultBleedingRate(WoundType woundType, float severity)
        {
            if (!Finite(severity) || severity <= 0f)
                return 0f;
            return woundType switch
            {
                WoundType.Laceration => severity * 0.18f,
                WoundType.Puncture => severity * 0.12f,
                _ => 0f
            };
        }

        private static float DefaultPainContribution(WoundType woundType, float severity)
        {
            if (!Finite(severity) || severity <= 0f)
                return 0f;
            float multiplier = woundType switch
            {
                WoundType.Laceration => 0.7f,
                WoundType.Puncture => 0.8f,
                _ => 0.9f
            };
            return Mathf.Clamp01(severity * multiplier);
        }

        private static bool TryValidateWound(ActorMedicalWoundState wound, out string failure)
        {
            if (wound == null)
            {
                failure = "Wound is null.";
                return false;
            }
            if (!IsValidWoundId(wound.woundId))
            {
                failure = $"WoundId '{Value(wound.woundId)}' is invalid.";
                return false;
            }
            if (!Enum.TryParse(wound.region, false, out BodyRegion region) || !HumanRegionValues.Contains(region) ||
                !string.Equals(wound.region, region.ToString(), StringComparison.Ordinal))
            {
                failure = $"Body region '{Value(wound.region)}' is unsupported.";
                return false;
            }
            if (!Enum.TryParse(wound.woundType, false, out WoundType woundType) ||
                !Enum.IsDefined(typeof(WoundType), woundType) ||
                !string.Equals(wound.woundType, woundType.ToString(), StringComparison.Ordinal))
            {
                failure = $"Wound type '{Value(wound.woundType)}' is unsupported.";
                return false;
            }
            if (!Finite(wound.severity) || wound.severity <= 0f || wound.severity > MaxSeverity)
            {
                failure = $"Severity '{wound.severity}' must be finite and within 0..{MaxSeverity}.";
                return false;
            }
            if (!Finite(wound.bleedingRatePerGameHour) || wound.bleedingRatePerGameHour < 0f ||
                wound.bleedingRatePerGameHour > MaxBleedingRatePerGameHour)
            {
                failure = $"Bleeding rate '{wound.bleedingRatePerGameHour}' is outside its supported range.";
                return false;
            }
            if (!Finite(wound.painContribution) || wound.painContribution < 0f ||
                wound.painContribution > MaxPainContribution)
            {
                failure = $"Pain contribution '{wound.painContribution}' is outside its supported range.";
                return false;
            }
            if (!Enum.TryParse(wound.treatmentState, false, out WoundTreatmentState treatment) ||
                !Enum.IsDefined(typeof(WoundTreatmentState), treatment) ||
                !string.Equals(wound.treatmentState, treatment.ToString(), StringComparison.Ordinal))
            {
                failure = $"Treatment state '{Value(wound.treatmentState)}' is unsupported.";
                return false;
            }
            if (!Finite(wound.treatmentBleedingMultiplier) || wound.treatmentBleedingMultiplier < 0f ||
                wound.treatmentBleedingMultiplier > 1f ||
                treatment == WoundTreatmentState.Unbandaged && wound.treatmentBleedingMultiplier != 1f ||
                treatment == WoundTreatmentState.Bandaged && wound.treatmentBleedingMultiplier >= 1f)
            {
                failure = $"Treatment bleeding multiplier '{wound.treatmentBleedingMultiplier}' contradicts '{treatment}'.";
                return false;
            }
            failure = null;
            return true;
        }

        private static ActorMedicalWoundState Clone(ActorMedicalWoundState wound)
        {
            return wound == null ? null : new ActorMedicalWoundState
            {
                woundId = wound.woundId,
                region = wound.region,
                woundType = wound.woundType,
                severity = wound.severity,
                bleedingRatePerGameHour = wound.bleedingRatePerGameHour,
                painContribution = wound.painContribution,
                treatmentState = wound.treatmentState,
                treatmentBleedingMultiplier = wound.treatmentBleedingMultiplier
            };
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static string Value(string value) => string.IsNullOrWhiteSpace(value) ? "<EMPTY>" : value;
    }
}
