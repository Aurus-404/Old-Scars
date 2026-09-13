using System;
using OldScars.Core.Actors;
using OldScars.Core.Interactions;
using UnityEditor;
using UnityEngine;

namespace OldScars.Editor
{
    public static class M41PlayerDebugInvincibleDiagnostics
    {
        private const string BloodWoundId = "wound_41200000000000000000000000000001";
        private const string VitalWoundId = "wound_41200000000000000000000000000002";

        [MenuItem("Old Scars/Diagnostics/Run M41 Player Debug Invincible")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Player Debug Invincible diagnostics require idle compiled Edit Mode.");

            GameObject offControl = null;
            GameObject protectedVital = null;
            GameObject protectedBlood = null;
            GameObject unprotectedNpc = null;
            try
            {
                offControl = CreateActor("Invincible OFF Player", true, false);
                AssertFatal(offControl, "OFF Player control");
                ActorDebugInvincible deadMarker = offControl.GetComponent<ActorDebugInvincible>();
                deadMarker.SetInvincible(true);
                offControl.GetComponent<ActorHealthComponent>().Kill();
                AssertDead(offControl, "already-Dead Player with Invincible enabled");

                protectedVital = CreateActor("Invincible ON Vital Player", true, true);
                ActorHealthComponent vitalHealth = protectedVital.GetComponent<ActorHealthComponent>();
                ActorMedicalStateComponent vitalMedical = protectedVital.GetComponent<ActorMedicalStateComponent>();
                Require(vitalMedical.TryApplyWound(VitalWoundId, BodyRegion.Head, WoundType.Puncture,
                        1f, 0.5f, 1f, out string failure), "Protected vital wound failed: " + failure);
                Require(vitalHealth.ApplyVitalDamage(vitalHealth.MaxVitalIntegrity * 10f),
                    "Protected fatal vital damage did not traverse ApplyVitalDamage.");
                AssertProtectedAlive(protectedVital, "direct fatal vital damage");
                Require(vitalMedical.WoundCount == 1 && vitalMedical.TotalPain > 0f &&
                        vitalMedical.EffectiveBleedingRatePerGameHour > 0f,
                    "Protected vital actor lost real wound, pain, or bleeding state.");

                protectedBlood = CreateActor("Invincible ON Blood Player", true, true);
                ActorHealthComponent bloodHealth = protectedBlood.GetComponent<ActorHealthComponent>();
                ActorMedicalStateComponent bloodMedical = protectedBlood.GetComponent<ActorMedicalStateComponent>();
                ActorConditionComponent bloodCondition = protectedBlood.GetComponent<ActorConditionComponent>();
                Require(bloodMedical.TryApplyWound(BloodWoundId, BodyRegion.Torso, WoundType.Laceration,
                        1f, 1f, 1f, out failure), "Protected bleeding wound failed: " + failure);
                float bloodBefore = bloodCondition.BloodFraction;
                Require(bloodCondition.AdvancePhysiology(WorldClock.SecondsPerHour * 2d),
                    "Protected blood loss did not advance through physiology.");
                Require(bloodCondition.BloodFraction < bloodBefore &&
                        bloodCondition.BloodFraction > bloodCondition.FatalBloodFraction &&
                        bloodMedical.EffectiveBleedingRatePerGameHour > 0f,
                    "Protected blood state did not preserve active bleeding at the nonterminal floor.");
                Require(bloodCondition.IsUnconscious && bloodCondition.IsUnconsciousDwellActive,
                    "Invincible prevented real Unconscious state or its minimum real-time dwell.");
                AssertProtectedAlive(protectedBlood, "fatal blood loss");
                Require(bloodHealth.VitalIntegrity < bloodHealth.MaxVitalIntegrity,
                    "Fatal blood-loss protection did not preserve the terminal vital consequence at its floor.");
                Require(bloodMedical.TryApplyBandage(BloodWoundId, 0.5f, out failure),
                    "Invincible blocked treatment of the critical bleeding wound: " + failure);

                ActorDebugInvincible marker = protectedBlood.GetComponent<ActorDebugInvincible>();
                float criticalVital = bloodHealth.VitalIntegrity;
                float criticalBlood = bloodCondition.BloodFraction;
                int woundCount = bloodMedical.WoundCount;
                marker.SetInvincible(false);
                Require(!bloodHealth.IsDead && Near(bloodHealth.VitalIntegrity, criticalVital) &&
                        Near(bloodCondition.BloodFraction, criticalBlood) && bloodMedical.WoundCount == woundCount,
                    "Turning Invincible OFF healed, reset, or killed immediately.");
                Require(bloodCondition.AdvancePhysiology(WorldClock.SecondsPerHour),
                    "The next production physiology evaluation did not run after Invincible OFF.");
                AssertDead(protectedBlood, "critical Player after OFF and next physiology evaluation");

                unprotectedNpc = CreateActor("Unprotected NPC", false, false);
                Require(unprotectedNpc.GetComponent<ActorDebugInvincible>() == null,
                    "NPC control unexpectedly received the Player debug marker.");
                AssertFatal(unprotectedNpc, "unprotected NPC control");

                Debug.Log(
                    "M41 Player Debug Invincible Diagnostics: PASS\n" +
                    "- OFF: fatal vital damage produced coherent Health/Lifecycle/Dead/Lootable state\n" +
                    "- ON vital: wound, pain and bleeding persisted; Vital Integrity remained minimally positive\n" +
                    "- ON blood/KO: bleeding reached the nonterminal floor; Unconscious+dwell and treatment remained active\n" +
                    "- OFF after critical: no immediate heal/reset/death; next physiology evaluation produced Dead\n" +
                    "- NPC: no marker and normal terminal death semantics");
            }
            finally
            {
                Destroy(offControl);
                Destroy(protectedVital);
                Destroy(protectedBlood);
                Destroy(unprotectedNpc);
            }
        }

        private static GameObject CreateActor(string name, bool player, bool invincible)
        {
            var root = new GameObject(name);
            WorldObjectTags tags = root.AddComponent<WorldObjectTags>();
            tags.ApplyInitialTags(player ? new[] { "player" } : new[] { "npc" });
            root.AddComponent<ActorRuntimeIdentity>();
            root.AddComponent<ActorMedicalStateComponent>();
            ActorHealthComponent health = root.AddComponent<ActorHealthComponent>();
            root.AddComponent<ActorConditionComponent>();
            ActorDebugInvincible marker = player ? root.AddComponent<ActorDebugInvincible>() : null;
            marker?.SetInvincible(invincible);
            health.ApplyInitialHealth(100f, 100f);
            return root;
        }

        private static void AssertFatal(GameObject actor, string label)
        {
            ActorHealthComponent health = actor.GetComponent<ActorHealthComponent>();
            Require(health.ApplyVitalDamage(health.MaxVitalIntegrity * 10f), label + " rejected fatal damage.");
            AssertDead(actor, label);
        }

        private static void AssertProtectedAlive(GameObject actor, string label)
        {
            ActorHealthComponent health = actor.GetComponent<ActorHealthComponent>();
            ActorRuntimeIdentity identity = actor.GetComponent<ActorRuntimeIdentity>();
            WorldObjectTags tags = actor.GetComponent<WorldObjectTags>();
            Require(health.VitalIntegrity > 0f && !health.IsDead &&
                    identity.LifecycleState != ActorLifecycleState.Dead &&
                    tags.HasTag(ActorHealthComponent.AliveActorTag) &&
                    !tags.HasTag(ActorHealthComponent.DeadActorTag) &&
                    !tags.HasTag(ActorHealthComponent.LootableActorTag),
                label + " produced contradictory protected terminal state.");
        }

        private static void AssertDead(GameObject actor, string label)
        {
            ActorHealthComponent health = actor.GetComponent<ActorHealthComponent>();
            ActorRuntimeIdentity identity = actor.GetComponent<ActorRuntimeIdentity>();
            WorldObjectTags tags = actor.GetComponent<WorldObjectTags>();
            Require(health.IsDead && Near(health.VitalIntegrity, 0f) &&
                    identity.LifecycleState == ActorLifecycleState.Dead &&
                    !tags.HasTag(ActorHealthComponent.AliveActorTag) &&
                    tags.HasTag(ActorHealthComponent.DeadActorTag) &&
                    tags.HasTag(ActorHealthComponent.LootableActorTag),
                label + " did not publish coherent terminal state.");
        }

        private static bool Near(float left, float right) => Mathf.Abs(left - right) <= 0.00001f;

        private static void Require(bool condition, string failure)
        {
            if (!condition)
                throw new InvalidOperationException(failure);
        }

        private static void Destroy(GameObject value)
        {
            if (value != null)
                UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
