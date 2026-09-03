using System;
using System.Collections.Generic;
using System.Globalization;
using OldScars.Core.Actors;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Items;
using UnityEngine;

namespace OldScars.Core.Combat
{
    public enum CombatAttackKind { Firearm, Melee }
    public enum CombatResolutionCode { Miss, InvalidTarget, Rejected, ResolvedNoWound, WoundApplied, TargetKilled }
    public enum ArmorCoverageStatus { NoArmorEquipped, RegionNotCovered, Covered }
    public enum ArmorResolutionOutcome { Unarmored, Stopped, Penetrated }
    public enum PenetrationOutcome { Stopped, Penetrated }

    /// <summary>
    /// A receiver-agnostic layer on Old Scars' internal penetration scale.
    /// SurfaceId may identify wearable armor, world cover, or a future machine
    /// or vehicle adapter. This contract has no medical dependency.
    /// </summary>
    public readonly struct PenetrationLayer
    {
        public PenetrationLayer(
            string surfaceId,
            string profileId,
            int layerPriority,
            float resistance)
        {
            SurfaceId = surfaceId;
            ProfileId = profileId;
            LayerPriority = layerPriority;
            Resistance = resistance;
        }

        public string SurfaceId { get; }
        public string ProfileId { get; }
        public int LayerPriority { get; }
        public float Resistance { get; }
    }

    public readonly struct PenetrationResolution
    {
        public PenetrationResolution(
            PenetrationOutcome outcome,
            string decisiveSurfaceId,
            string decisiveProfileId,
            float incomingPower,
            float appliedResistance,
            float residualPower,
            float powerAtDecisiveLayer,
            int layerCount,
            int appliedLayerCount)
        {
            Outcome = outcome;
            DecisiveSurfaceId = decisiveSurfaceId;
            DecisiveProfileId = decisiveProfileId;
            IncomingPower = incomingPower;
            AppliedResistance = appliedResistance;
            ResidualPower = residualPower;
            PowerAtDecisiveLayer = powerAtDecisiveLayer;
            LayerCount = layerCount;
            AppliedLayerCount = appliedLayerCount;
        }

        public PenetrationOutcome Outcome { get; }
        public string DecisiveSurfaceId { get; }
        public string DecisiveProfileId { get; }
        public float IncomingPower { get; }
        public float AppliedResistance { get; }
        public float ResidualPower { get; }
        public float PowerAtDecisiveLayer { get; }
        public int LayerCount { get; }
        public int AppliedLayerCount { get; }
    }

    public static class PenetrationResolutionService
    {
        public static PenetrationResolution Resolve(float incomingPower, IReadOnlyList<PenetrationLayer> layers)
        {
            if (!FiniteNonNegative(incomingPower))
                throw new ArgumentOutOfRangeException(nameof(incomingPower), incomingPower, "Penetration power must be finite and >= 0.");
            if (layers == null || layers.Count == 0)
                throw new ArgumentException("Penetration resolution requires at least one layer.", nameof(layers));

            var ordered = new List<PenetrationLayer>(layers.Count);
            for (int index = 0; index < layers.Count; index++)
            {
                PenetrationLayer layer = layers[index];
                if (!FiniteNonNegative(layer.Resistance) || layer.LayerPriority < 0)
                    throw new ArgumentException($"Penetration layer '{layer.ProfileId ?? "<NONE>"}' contains invalid values.", nameof(layers));
                ordered.Add(layer);
            }
            ordered.Sort(CompareLayers);

            float remaining = incomingPower;
            float appliedResistance = 0f;
            float powerAtLastLayer = incomingPower;
            for (int index = 0; index < ordered.Count; index++)
            {
                PenetrationLayer layer = ordered[index];
                float powerAtLayer = remaining;
                powerAtLastLayer = powerAtLayer;
                appliedResistance += layer.Resistance;
                if (remaining <= layer.Resistance)
                {
                    return new PenetrationResolution(
                        PenetrationOutcome.Stopped,
                        layer.SurfaceId,
                        layer.ProfileId,
                        incomingPower,
                        appliedResistance,
                        0f,
                        powerAtLayer,
                        ordered.Count,
                        index + 1);
                }
                remaining = Math.Max(0f, remaining - layer.Resistance);
            }

            PenetrationLayer last = ordered[ordered.Count - 1];
            return new PenetrationResolution(
                PenetrationOutcome.Penetrated,
                last.SurfaceId,
                last.ProfileId,
                incomingPower,
                appliedResistance,
                remaining,
                powerAtLastLayer,
                ordered.Count,
                ordered.Count);
        }

        private static int CompareLayers(PenetrationLayer left, PenetrationLayer right)
        {
            int priority = right.LayerPriority.CompareTo(left.LayerPriority);
            if (priority != 0)
                return priority;
            int profile = string.Compare(left.ProfileId, right.ProfileId, StringComparison.Ordinal);
            return profile != 0
                ? profile
                : string.Compare(left.SurfaceId, right.SurfaceId, StringComparison.Ordinal);
        }

        private static bool FiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }

    public enum PhysicalShotTermination
    {
        Miss,
        Impact,
        SurfaceStopped,
        SurfaceLimitStopped
    }

    public readonly struct PhysicalShotResolution
    {
        public PhysicalShotResolution(
            PhysicalShotTermination termination,
            Collider hitCollider,
            Vector3 endPoint,
            float originalPower,
            float remainingPower,
            int penetratedSurfaceCount,
            PenetrationResolution lastSurfaceResolution,
            string terminalSurfaceProfileId = null)
        {
            IsResolved = true;
            Termination = termination;
            HitCollider = hitCollider;
            EndPoint = endPoint;
            OriginalPower = originalPower;
            RemainingPower = remainingPower;
            PenetratedSurfaceCount = penetratedSurfaceCount;
            LastSurfaceResolution = lastSurfaceResolution;
            TerminalSurfaceProfileId = terminalSurfaceProfileId;
        }

        public bool IsResolved { get; }
        public PhysicalShotTermination Termination { get; }
        public Collider HitCollider { get; }
        public Vector3 EndPoint { get; }
        public float OriginalPower { get; }
        public float RemainingPower { get; }
        public int PenetratedSurfaceCount { get; }
        public PenetrationResolution LastSurfaceResolution { get; }
        public string TerminalSurfaceProfileId { get; }
    }

    public delegate PhysicalShotResolution PhysicalShotResolver(float penetrationPower);

    public readonly struct ArmorLayerInput
    {
        public ArmorLayerInput(
            string itemInstanceId,
            string armorProfileId,
            string penetrationProfileId,
            int layerPriority,
            float ballisticResistance,
            float impactResistance,
            float stoppedBluntTransfer,
            float bluntWoundThreshold)
        {
            ItemInstanceId = itemInstanceId;
            ArmorProfileId = armorProfileId;
            PenetrationProfileId = penetrationProfileId;
            LayerPriority = layerPriority;
            BallisticResistance = ballisticResistance;
            ImpactResistance = impactResistance;
            StoppedBluntTransfer = stoppedBluntTransfer;
            BluntWoundThreshold = bluntWoundThreshold;
        }

        public string ItemInstanceId { get; }
        public string ArmorProfileId { get; }
        public string PenetrationProfileId { get; }
        public int LayerPriority { get; }
        public float BallisticResistance { get; }
        public float ImpactResistance { get; }
        public float StoppedBluntTransfer { get; }
        public float BluntWoundThreshold { get; }
    }

    public readonly struct ArmorResolution
    {
        public ArmorResolution(
            ArmorResolutionOutcome outcome,
            ArmorCoverageStatus coverage,
            string armorItemInstanceId,
            string armorProfileId,
            float attackPower,
            float resistance,
            float residualPower,
            float transferredTrauma,
            float traumaFactor,
            int layerCount,
            int appliedLayerCount,
            PenetrationResolution penetration)
        {
            Outcome = outcome;
            Coverage = coverage;
            ArmorItemInstanceId = armorItemInstanceId;
            ArmorProfileId = armorProfileId;
            AttackPower = attackPower;
            Resistance = resistance;
            ResidualPower = residualPower;
            TransferredTrauma = transferredTrauma;
            TraumaFactor = traumaFactor;
            LayerCount = layerCount;
            AppliedLayerCount = appliedLayerCount;
            Penetration = penetration;
        }

        public ArmorResolutionOutcome Outcome { get; }
        public ArmorCoverageStatus Coverage { get; }
        public string ArmorItemInstanceId { get; }
        public string ArmorProfileId { get; }
        public float AttackPower { get; }
        public float Resistance { get; }
        public float ResidualPower { get; }
        public float TransferredTrauma { get; }
        public float TraumaFactor { get; }
        public int LayerCount { get; }
        public int AppliedLayerCount { get; }
        public PenetrationResolution Penetration { get; }
        public bool ArmorFound => Coverage == ArmorCoverageStatus.Covered;
    }

    public readonly struct CombatImpact
    {
        public CombatImpact(
            GameObject attacker,
            ItemInstance weapon,
            CombatAttackKind attackKind,
            WoundType woundType,
            float severity,
            float bleedingRatePerGameHour,
            float painContribution,
            float originalProtectionPower,
            float remainingProtectionPower)
        {
            Attacker = attacker;
            Weapon = weapon;
            AttackKind = attackKind;
            WoundType = woundType;
            Severity = severity;
            BleedingRatePerGameHour = bleedingRatePerGameHour;
            PainContribution = painContribution;
            OriginalProtectionPower = originalProtectionPower;
            RemainingProtectionPower = remainingProtectionPower;
        }

        public GameObject Attacker { get; }
        public ItemInstance Weapon { get; }
        public CombatAttackKind AttackKind { get; }
        public WoundType WoundType { get; }
        public float Severity { get; }
        public float BleedingRatePerGameHour { get; }
        public float PainContribution { get; }
        public float OriginalProtectionPower { get; }
        public float RemainingProtectionPower { get; }
    }

    public readonly struct CombatResolutionResult
    {
        public CombatResolutionResult(
            CombatResolutionCode code,
            string message,
            BodyRegion? region = null,
            string woundId = null,
            ArmorResolution armor = default,
            WoundType? finalWoundType = null,
            float finalSeverity = 0f,
            float vitalDamage = 0f,
            float vitalIntegrityBefore = 0f,
            float vitalIntegrityAfter = 0f)
        {
            Code = code;
            Message = message;
            Region = region;
            WoundId = woundId;
            Armor = armor;
            FinalWoundType = finalWoundType;
            FinalSeverity = finalSeverity;
            VitalDamage = vitalDamage;
            VitalIntegrityBefore = vitalIntegrityBefore;
            VitalIntegrityAfter = vitalIntegrityAfter;
        }

        public CombatResolutionCode Code { get; }
        public string Message { get; }
        public BodyRegion? Region { get; }
        public string WoundId { get; }
        public ArmorResolution Armor { get; }
        public WoundType? FinalWoundType { get; }
        public float FinalSeverity { get; }
        public float VitalDamage { get; }
        public float VitalIntegrityBefore { get; }
        public float VitalIntegrityAfter { get; }
        public bool WoundApplied => Code == CombatResolutionCode.WoundApplied || Code == CombatResolutionCode.TargetKilled;
        public bool Resolved => Code == CombatResolutionCode.ResolvedNoWound || WoundApplied;
    }

    internal static class ActorMedicalCombatReceiver
    {
        public static bool CanResolve(Collider hitCollider) => hitCollider != null &&
            hitCollider.GetComponentInParent<ActorMedicalStateComponent>() != null && hitCollider.GetComponentInParent<ActorHealthComponent>() != null;

        public static CombatResolutionResult ResolveImpact(Collider hitCollider, Vector3 hitPoint, CombatImpact impact)
        {
            if (hitCollider == null)
                return new CombatResolutionResult(CombatResolutionCode.Miss, "Attack did not hit a collider.");

            if (!FiniteUnitPositive(impact.Severity) || !FiniteUnit(impact.BleedingRatePerGameHour) ||
                !FiniteUnit(impact.PainContribution) || !FiniteNonNegative(impact.OriginalProtectionPower) ||
                !FiniteNonNegative(impact.RemainingProtectionPower) ||
                impact.RemainingProtectionPower > impact.OriginalProtectionPower)
            {
                return new CombatResolutionResult(
                    CombatResolutionCode.Rejected,
                    $"Combat impact contains invalid medical/protection values " +
                    $"(severity={Value(impact.Severity)}, bleeding={Value(impact.BleedingRatePerGameHour)}, " +
                    $"pain={Value(impact.PainContribution)}, originalPower={Value(impact.OriginalProtectionPower)}, " +
                    $"remainingPower={Value(impact.RemainingProtectionPower)}).");
            }

            ActorMedicalStateComponent medical = hitCollider.GetComponentInParent<ActorMedicalStateComponent>();
            ActorHealthComponent health = hitCollider.GetComponentInParent<ActorHealthComponent>();
            if (medical == null || health == null)
                return new CombatResolutionResult(CombatResolutionCode.InvalidTarget, "Impact hit world geometry, not an actor.");
            if (impact.Attacker != null && (health.gameObject == impact.Attacker || health.transform.IsChildOf(impact.Attacker.transform)))
                return new CombatResolutionResult(CombatResolutionCode.InvalidTarget, "Attacker cannot resolve combat against itself.");
            if (health.IsDead)
                return new CombatResolutionResult(CombatResolutionCode.Rejected, "Dead actors cannot receive new M40 wounds.");

            BodyRegion region = ResolveBodyRegion(health.transform, hitCollider, hitPoint);
            ArmorDiscovery discovery = DiscoverArmor(health, region);
            ArmorResolution armor = ResolveArmorLayers(
                impact.AttackKind,
                impact.RemainingProtectionPower,
                discovery.Coverage,
                discovery.Layers);

            if (armor.Outcome == ArmorResolutionOutcome.Stopped && armor.TraumaFactor <= 0f)
            {
                return new CombatResolutionResult(
                    CombatResolutionCode.ResolvedNoWound,
                    BuildNoWoundMessage(region, impact.AttackKind, armor),
                    region,
                    armor: armor);
            }

            WoundType finalType = armor.Outcome == ArmorResolutionOutcome.Stopped
                ? WoundType.Blunt
                : impact.WoundType;
            float powerAfterArmor = armor.Outcome == ArmorResolutionOutcome.Penetrated
                ? armor.ResidualPower
                : impact.RemainingProtectionPower;
            float receiverFactor = impact.OriginalProtectionPower > Mathf.Epsilon
                ? Mathf.Clamp01(powerAfterArmor / impact.OriginalProtectionPower)
                : 1f;
            float consequenceFactor = armor.Outcome switch
            {
                ArmorResolutionOutcome.Stopped =>
                    (impact.OriginalProtectionPower > Mathf.Epsilon
                        ? Mathf.Clamp01(impact.RemainingProtectionPower / impact.OriginalProtectionPower)
                        : 1f) * armor.TraumaFactor,
                _ => receiverFactor
            };
            float finalSeverity = Mathf.Clamp(impact.Severity * consequenceFactor, float.Epsilon, 1f);
            string woundId;
            string failure;
            bool applied;
            if (armor.Outcome == ArmorResolutionOutcome.Stopped)
            {
                applied = medical.ApplyWound(region, finalType, finalSeverity, out woundId, out failure);
            }
            else
            {
                woundId = "wound_" + Guid.NewGuid().ToString("N");
                applied = medical.TryApplyWound(
                    woundId,
                    region,
                    finalType,
                    finalSeverity,
                    Mathf.Clamp01(impact.BleedingRatePerGameHour * consequenceFactor),
                    Mathf.Clamp01(impact.PainContribution * consequenceFactor),
                    out failure);
            }

            if (!applied)
                return new CombatResolutionResult(
                    CombatResolutionCode.Rejected,
                    failure,
                    region,
                    armor: armor,
                    finalWoundType: finalType,
                    finalSeverity: finalSeverity);

            float vitalIntegrityBefore = health.VitalIntegrity;
            float vitalDamage = health.CalculateVitalDamage(region, finalType, finalSeverity);
            health.ApplyVitalDamage(vitalDamage);

            return new CombatResolutionResult(
                health.IsDead ? CombatResolutionCode.TargetKilled : CombatResolutionCode.WoundApplied,
                BuildWoundMessage(region, impact.AttackKind, armor, finalType, woundId),
                region,
                woundId,
                armor,
                finalType,
                finalSeverity,
                vitalDamage,
                vitalIntegrityBefore,
                health.VitalIntegrity);
        }

        public static ArmorResolution ResolveArmorLayers(
            CombatAttackKind attackKind,
            float attackPower,
            ArmorCoverageStatus coverage,
            IReadOnlyList<ArmorLayerInput> layers)
        {
            if (!FiniteNonNegative(attackPower))
                throw new ArgumentOutOfRangeException(nameof(attackPower), attackPower, "Armor resolution power must be finite and >= 0.");

            int layerCount = layers != null ? layers.Count : 0;
            if (layerCount == 0)
            {
                ArmorCoverageStatus unarmoredCoverage = coverage == ArmorCoverageStatus.Covered
                    ? ArmorCoverageStatus.RegionNotCovered
                    : coverage;
                return new ArmorResolution(
                    ArmorResolutionOutcome.Unarmored,
                    unarmoredCoverage,
                    null,
                    null,
                    attackPower,
                    0f,
                    attackPower,
                    0f,
                    0f,
                    0,
                    0,
                    default);
            }

            var penetrationLayers = new List<PenetrationLayer>(layerCount);
            for (int index = 0; index < layerCount; index++)
            {
                ArmorLayerInput layer = layers[index];
                float resistance = attackKind == CombatAttackKind.Firearm
                    ? layer.BallisticResistance
                    : layer.ImpactResistance;
                if (!FiniteNonNegative(resistance) || !FiniteUnit(layer.StoppedBluntTransfer) ||
                    !FiniteUnit(layer.BluntWoundThreshold) || layer.LayerPriority < 0)
                {
                    throw new ArgumentException($"Armor layer '{layer.ArmorProfileId ?? "<NONE>"}' contains invalid values.", nameof(layers));
                }
                string profileId = attackKind == CombatAttackKind.Firearm
                    ? layer.PenetrationProfileId
                    : layer.ArmorProfileId;
                penetrationLayers.Add(new PenetrationLayer(
                    layer.ItemInstanceId,
                    profileId,
                    layer.LayerPriority,
                    resistance));
            }

            PenetrationResolution penetration = PenetrationResolutionService.Resolve(attackPower, penetrationLayers);
            ArmorLayerInput decisive = default;
            for (int index = 0; index < layerCount; index++)
            {
                if (string.Equals(layers[index].ItemInstanceId, penetration.DecisiveSurfaceId, StringComparison.Ordinal))
                {
                    decisive = layers[index];
                    break;
                }
            }

            float transferred = 0f;
            float traumaFactor = 0f;
            if (penetration.Outcome == PenetrationOutcome.Stopped)
            {
                float decisiveResistance = attackKind == CombatAttackKind.Firearm
                    ? decisive.BallisticResistance
                    : decisive.ImpactResistance;
                float impactRatio = decisiveResistance > Mathf.Epsilon
                    ? Mathf.Clamp01(penetration.PowerAtDecisiveLayer / decisiveResistance)
                    : 0f;
                transferred = Mathf.Clamp01(impactRatio * decisive.StoppedBluntTransfer);
                traumaFactor = transferred > decisive.BluntWoundThreshold && decisive.BluntWoundThreshold < 1f
                    ? Mathf.Clamp01((transferred - decisive.BluntWoundThreshold) / (1f - decisive.BluntWoundThreshold))
                    : 0f;
            }

            return new ArmorResolution(
                penetration.Outcome == PenetrationOutcome.Stopped
                    ? ArmorResolutionOutcome.Stopped
                    : ArmorResolutionOutcome.Penetrated,
                ArmorCoverageStatus.Covered,
                decisive.ItemInstanceId,
                decisive.ArmorProfileId,
                attackPower,
                penetration.AppliedResistance,
                penetration.ResidualPower,
                transferred,
                traumaFactor,
                penetration.LayerCount,
                penetration.AppliedLayerCount,
                penetration);
        }

        private static ArmorDiscovery DiscoverArmor(ActorHealthComponent health, BodyRegion region)
        {
            ActorEquipmentComponent equipment = health != null ? health.GetComponent<ActorEquipmentComponent>() : null;
            GameDatabase database = GameDataManager.Instance != null && GameDataManager.Instance.IsReady
                ? GameDataManager.Instance.Database
                : null;
            if (equipment == null || database == null)
                return new ArmorDiscovery(ArmorCoverageStatus.NoArmorEquipped, Array.Empty<ArmorLayerInput>());

            bool hasEquippedArmor = false;
            var layers = new List<ArmorLayerInput>();
            IReadOnlyList<ItemStorageEntry> entries = equipment.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                ItemStorageEntry entry = entries[index];
                if (entry?.Item == null || !equipment.IsEquipped(entry.Item.InstanceId))
                    continue;
                ItemDefinition item = database.GetItem(entry.Item.DefinitionId);
                if (item == null || string.IsNullOrWhiteSpace(item.armor_profile_id))
                    continue;

                hasEquippedArmor = true;
                ArmorProfileDefinition profile = database.GetArmorProfile(item.armor_profile_id);
                if (profile == null || !Covers(profile, region))
                    continue;
                PenetrationProfileDefinition penetration = database.GetPenetrationProfile(profile.penetration_profile_id);
                if (penetration == null)
                    continue;

                layers.Add(new ArmorLayerInput(
                    entry.Item.InstanceId,
                    profile.id,
                    penetration.id,
                    profile.layer_priority,
                    EffectiveResistance(entry.Item, penetration.resistance),
                    EffectiveResistance(entry.Item, profile.impact_resistance),
                    profile.stopped_blunt_transfer,
                    profile.blunt_wound_threshold));
            }

            return new ArmorDiscovery(
                layers.Count > 0
                    ? ArmorCoverageStatus.Covered
                    : hasEquippedArmor ? ArmorCoverageStatus.RegionNotCovered : ArmorCoverageStatus.NoArmorEquipped,
                layers);
        }

        private static float EffectiveResistance(ItemInstance armorItem, float baseResistance)
        {
            // M43 seam: armorItem is intentionally available here for a future
            // baseResistance * conditionFactor calculation. M40.1 neither reads
            // nor mutates ItemInstance.Condition.
            _ = armorItem;
            return baseResistance;
        }

        private static bool Covers(ArmorProfileDefinition profile, BodyRegion region)
        {
            string expected = region.ToString();
            string[] covered = profile?.covered_regions ?? Array.Empty<string>();
            for (int index = 0; index < covered.Length; index++)
                if (string.Equals(covered[index], expected, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static string BuildNoWoundMessage(BodyRegion region, CombatAttackKind kind, ArmorResolution armor) =>
            $"{region} - armor '{armor.ArmorProfileId}' stopped {AttackLabel(kind)} " +
            $"(power {Value(armor.AttackPower)} <= resistance {Value(armor.Resistance)}) - no medical wound.";

        private static string BuildWoundMessage(
            BodyRegion region,
            CombatAttackKind kind,
            ArmorResolution armor,
            WoundType woundType,
            string woundId)
        {
            if (armor.Outcome == ArmorResolutionOutcome.Unarmored)
            {
                string state = armor.Coverage == ArmorCoverageStatus.RegionNotCovered
                    ? "equipped armor did not cover region"
                    : "unarmored";
                return $"{region} - {state} - {woundType} wound '{woundId}'.";
            }

            if (armor.Outcome == ArmorResolutionOutcome.Stopped)
            {
                return $"{region} - armor '{armor.ArmorProfileId}' stopped {AttackLabel(kind)} " +
                       $"(power {Value(armor.AttackPower)} <= resistance {Value(armor.Resistance)}) - " +
                       $"Blunt trauma '{woundId}'.";
            }

            string penetration = kind == CombatAttackKind.Firearm ? "armor penetrated" : "armor protection exceeded";
            return $"{region} - {penetration} '{armor.ArmorProfileId}' " +
                   $"(power {Value(armor.AttackPower)}, resistance {Value(armor.Resistance)}, " +
                   $"residual {Value(armor.ResidualPower)}) - {woundType} wound '{woundId}'.";
        }

        private static string AttackLabel(CombatAttackKind kind) =>
            kind == CombatAttackKind.Firearm ? "projectile" : "melee impact";

        private static string Value(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        private static bool FiniteNonNegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        private static bool FiniteUnit(float value) => FiniteNonNegative(value) && value <= 1f;
        private static bool FiniteUnitPositive(float value) => FiniteUnit(value) && value > 0f;

        private readonly struct ArmorDiscovery
        {
            public ArmorDiscovery(ArmorCoverageStatus coverage, IReadOnlyList<ArmorLayerInput> layers)
            {
                Coverage = coverage;
                Layers = layers;
            }

            public ArmorCoverageStatus Coverage { get; }
            public IReadOnlyList<ArmorLayerInput> Layers { get; }
        }

        public static BodyRegion ResolveBodyRegion(Transform actorRoot, Collider hitCollider, Vector3 hitPoint)
        {
            if (actorRoot == null || hitCollider == null)
                return BodyRegion.Torso;

            ActorCombatHitRegion explicitRegion = hitCollider.GetComponent<ActorCombatHitRegion>();
            if (explicitRegion != null && explicitRegion.BelongsTo(actorRoot))
                return explicitRegion.Region;

            Bounds bounds = hitCollider.bounds;
            float height = Mathf.Max(0.001f, bounds.size.y);
            float normalizedY = Mathf.Clamp01((hitPoint.y - bounds.min.y) / height);
            Vector3 localPoint = actorRoot.InverseTransformPoint(hitPoint);
            Vector3 localCenter = actorRoot.InverseTransformPoint(bounds.center);
            float localHalfWidth = Mathf.Max(0.001f,
                Mathf.Abs(actorRoot.InverseTransformVector(bounds.extents).x));
            float normalizedX = Mathf.Clamp((localPoint.x - localCenter.x) / localHalfWidth, -1f, 1f);

            if (normalizedY >= 0.82f)
                return BodyRegion.Head;
            if (normalizedY >= 0.48f)
            {
                if (Mathf.Abs(normalizedX) < 0.42f)
                    return BodyRegion.Torso;
                return normalizedX < 0f ? BodyRegion.LeftArm : BodyRegion.RightArm;
            }
            return normalizedX < 0f ? BodyRegion.LeftLeg : BodyRegion.RightLeg;
        }
    }

    // Terminal dispatch seam. M40.1 registers only the medical-actor adapter;
    // future receivers do not change physical or penetration resolution.
    public static class CombatResolutionService
    {
        public static CombatResolutionResult ResolveImpact(Collider hitCollider, Vector3 hitPoint, CombatImpact impact) =>
            ActorMedicalCombatReceiver.CanResolve(hitCollider)
                ? ActorMedicalCombatReceiver.ResolveImpact(hitCollider, hitPoint, impact)
                : new CombatResolutionResult(CombatResolutionCode.InvalidTarget,
                    "Impact reached a collider without a supported combat consequence receiver.");

        public static ArmorResolution ResolveArmorLayers(CombatAttackKind attackKind, float attackPower,
            ArmorCoverageStatus coverage, IReadOnlyList<ArmorLayerInput> layers) =>
            ActorMedicalCombatReceiver.ResolveArmorLayers(attackKind, attackPower, coverage, layers);

        public static BodyRegion ResolveBodyRegion(Transform actorRoot, Collider hitCollider, Vector3 hitPoint) =>
            ActorMedicalCombatReceiver.ResolveBodyRegion(actorRoot, hitCollider, hitPoint);
    }
}
