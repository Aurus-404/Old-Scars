using System;
using OldScars.Core.Actors;
using OldScars.Core.Items;
using UnityEngine;

namespace OldScars.Core.Combat
{
    public enum CombatAttackKind { Firearm, Melee }
    public enum CombatResolutionCode { Miss, InvalidTarget, Rejected, WoundApplied, TargetKilled }

    public readonly struct CombatImpact
    {
        public CombatImpact(
            GameObject attacker,
            ItemInstance weapon,
            CombatAttackKind attackKind,
            WoundType woundType,
            float severity,
            float bleedingRatePerGameHour,
            float painContribution)
        {
            Attacker = attacker;
            Weapon = weapon;
            AttackKind = attackKind;
            WoundType = woundType;
            Severity = severity;
            BleedingRatePerGameHour = bleedingRatePerGameHour;
            PainContribution = painContribution;
        }

        public GameObject Attacker { get; }
        public ItemInstance Weapon { get; }
        public CombatAttackKind AttackKind { get; }
        public WoundType WoundType { get; }
        public float Severity { get; }
        public float BleedingRatePerGameHour { get; }
        public float PainContribution { get; }
    }

    public readonly struct CombatResolutionResult
    {
        public CombatResolutionResult(
            CombatResolutionCode code,
            string message,
            BodyRegion? region = null,
            string woundId = null)
        {
            Code = code;
            Message = message;
            Region = region;
            WoundId = woundId;
        }

        public CombatResolutionCode Code { get; }
        public string Message { get; }
        public BodyRegion? Region { get; }
        public string WoundId { get; }
        public bool WoundApplied => Code == CombatResolutionCode.WoundApplied || Code == CombatResolutionCode.TargetKilled;
    }

    public static class CombatResolutionService
    {
        public static CombatResolutionResult ResolveImpact(Collider hitCollider, Vector3 hitPoint, CombatImpact impact)
        {
            if (hitCollider == null)
                return new CombatResolutionResult(CombatResolutionCode.Miss, "Attack did not hit a collider.");

            ActorMedicalStateComponent medical = hitCollider.GetComponentInParent<ActorMedicalStateComponent>();
            ActorHealthComponent health = hitCollider.GetComponentInParent<ActorHealthComponent>();
            if (medical == null || health == null)
                return new CombatResolutionResult(CombatResolutionCode.InvalidTarget, "Impact hit world geometry, not an actor.");
            if (impact.Attacker != null && (health.gameObject == impact.Attacker || health.transform.IsChildOf(impact.Attacker.transform)))
                return new CombatResolutionResult(CombatResolutionCode.InvalidTarget, "Attacker cannot resolve combat against itself.");
            if (health.IsDead)
                return new CombatResolutionResult(CombatResolutionCode.Rejected, "Dead actors cannot receive new M40 wounds.");

            BodyRegion region = ResolveBodyRegion(health.transform, hitCollider, hitPoint);
            string woundId = "wound_" + Guid.NewGuid().ToString("N");
            if (!medical.TryApplyWound(
                    woundId,
                    region,
                    impact.WoundType,
                    impact.Severity,
                    impact.BleedingRatePerGameHour,
                    impact.PainContribution,
                    out string failure))
            {
                return new CombatResolutionResult(CombatResolutionCode.Rejected, failure, region);
            }

            return new CombatResolutionResult(
                health.IsDead ? CombatResolutionCode.TargetKilled : CombatResolutionCode.WoundApplied,
                $"{impact.AttackKind} applied {impact.WoundType} wound '{woundId}' to {region}.",
                region,
                woundId);
        }

        public static BodyRegion ResolveBodyRegion(Transform actorRoot, Collider hitCollider, Vector3 hitPoint)
        {
            if (actorRoot == null || hitCollider == null)
                return BodyRegion.Torso;

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
}
