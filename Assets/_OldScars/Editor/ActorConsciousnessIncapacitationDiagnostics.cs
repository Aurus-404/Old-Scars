using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OldScars.Core;
using OldScars.Core.Actors;
using OldScars.Core.Combat;
using OldScars.Core.Interactions;
using OldScars.Core.Items;
using OldScars.Core.Persistence;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OldScars.Editor
{
    [InitializeOnLoad]
    public static class ActorConsciousnessIncapacitationDiagnostics
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string PendingKey = "OldScars.Consciousness.Pending";
        private const string StageKey = "OldScars.Consciousness.Stage";
        private const string FailureKey = "OldScars.Consciousness.Failure";
        private const string RootKey = "OldScars.Consciousness.Root";
        private const string Slot = "actor_consciousness_state";
        private const string ProfileId = "core:debug_encounter_fight_01";
        private const string LimbWound = "wound_a1111111111111111111111111111111";
        private const string HeadWound = "wound_b2222222222222222222222222222222";
        private const string KnockoutWound = "wound_c3333333333333333333333333333333";
        private const string PlayerWound = "wound_d4444444444444444444444444444444";
        private const string BleedingWound = "wound_e5555555555555555555555555555555";

        private static ActorRuntimeIdentity limbActor;
        private static ActorRuntimeIdentity headActor;
        private static string headActorId;
        private static string[] headBelongingIds;
        private static float limbStability;
        private static float headStability;
        private static float unconsciousStability;
        private static float recoveredStability;
        private static float persistedBlood;
        private static float persistedTrauma;
        private static float bleedingUnconsciousBlood;
        private static float bleedingAfterBandage;

        static ActorConsciousnessIncapacitationDiagnostics()
        {
            EditorApplication.update -= Continue;
            EditorApplication.update += Continue;
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Actor Consciousness diagnostics require idle compiled Edit Mode.");

            ClearSession();
            string root = Path.Combine(Path.GetTempPath(), "OldScars_Consciousness_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            SessionState.SetString(RootKey, root);
            SessionState.SetBool(PendingKey, true);
            SessionState.SetInt(StageKey, 0);
            SessionState.EraseString(FailureKey);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void Continue()
        {
            if (!SessionState.GetBool(PendingKey, false))
                return;
            try
            {
                if (EditorApplication.isPlaying)
                {
                    if (!Ready())
                        return;
                    WorldClock.Current.AdvanceDuringGameplay = false;
                    int stage = SessionState.GetInt(StageKey, 0);
                    if (stage == 0)
                    {
                        SetupAndProveImmediateTrauma();
                        SessionState.SetInt(StageKey, 1);
                        return;
                    }
                    if (stage == 1)
                    {
                        ProveIncapacitationRecoveryPersistenceAndBloodCollapse();
                        SessionState.SetInt(StageKey, 99);
                        EditorApplication.ExitPlaymode();
                    }
                    return;
                }

                if (!EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetInt(StageKey, 0) == 99)
                    Finish();
                else if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    FailAndFinish("Actor Consciousness diagnostic was interrupted before completion.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetString(FailureKey, exception.Message);
                SessionState.SetInt(StageKey, 99);
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorApplication.ExitPlaymode();
                else
                    Finish();
            }
        }

        private static bool Ready()
        {
            return Time.frameCount >= 5 && WorldClock.Current != null &&
                   GameDataManager.Instance != null && GameDataManager.Instance.IsReady;
        }

        private static void SetupAndProveImmediateTrauma()
        {
            ActorInteractionContext player = FindPlayer();
            ActorHealthComponent playerHealth = player.GetComponent<ActorHealthComponent>();
            ActorMedicalStateComponent playerMedical = player.GetComponent<ActorMedicalStateComponent>();
            ActorConditionComponent playerCondition = player.GetComponent<ActorConditionComponent>();
            Require(playerHealth != null && playerMedical != null && playerCondition != null &&
                    playerCondition.IsConfigured && Near(playerCondition.ConsciousnessStability, 1f) &&
                    playerCondition.FunctionalState == ActorFunctionalState.Conscious,
                "Healthy Player did not start fully conscious through the shared condition authority.");

            Vector3 origin = player.transform.position + new Vector3(4f, 0f, 4f);
            limbActor = Spawn(ProfileId, origin);
            headActor = Spawn(ProfileId, origin + new Vector3(3f, 0f, 0f));
            DisableAcquisition(limbActor);
            DisableAcquisition(headActor);

            ActorConditionComponent limbCondition = limbActor.GetComponent<ActorConditionComponent>();
            ActorConditionComponent headCondition = headActor.GetComponent<ActorConditionComponent>();
            ActorMedicalStateComponent limbMedical = limbActor.GetComponent<ActorMedicalStateComponent>();
            ActorMedicalStateComponent headMedical = headActor.GetComponent<ActorMedicalStateComponent>();
            Require(limbCondition != null && headCondition != null && limbCondition.GetType() == playerCondition.GetType() &&
                    limbActor.GetComponents<ActorConditionComponent>().Length == 1 &&
                    headActor.GetComponents<ActorConditionComponent>().Length == 1,
                "Player/NPC did not share exactly one ActorConditionComponent authority.");

            Require(limbMedical.TryApplyWound(
                    LimbWound, BodyRegion.LeftArm, WoundType.Blunt, 0.4f, 0f, 0.3f, out string failure),
                "Equivalent limb blunt wound failed: " + failure);
            Require(headMedical.TryApplyWound(
                    HeadWound, BodyRegion.Head, WoundType.Blunt, 0.4f, 0f, 0.3f, out failure),
                "Equivalent Head blunt wound failed: " + failure);
            limbStability = limbCondition.ConsciousnessStability;
            headStability = headCondition.ConsciousnessStability;
            Require(limbStability < 1f && limbStability > 0.75f &&
                    limbCondition.FunctionalState == ActorFunctionalState.Conscious,
                "Moderate limb pain/trauma did not affect stability without automatic KO.");
            Require(headStability < limbStability - 0.2f && !headCondition.IsUnconscious,
                "Equivalent Head blunt trauma was not materially stronger than limb trauma.");

            Require(headMedical.TryApplyWound(
                    KnockoutWound, BodyRegion.Head, WoundType.Blunt, 0.5f, 0f, 0.05f, out failure),
                "Accumulated Head trauma failed: " + failure);
            unconsciousStability = headCondition.ConsciousnessStability;
            Require(headCondition.IsUnconscious && !headActor.GetComponent<ActorHealthComponent>().IsDead &&
                    headActor.LifecycleState == ActorLifecycleState.Alive,
                "Accumulated trauma did not produce Unconscious while preserving Alive lifecycle.");

            headActorId = headActor.ActorInstanceId;
            headBelongingIds = BelongingIds(headActor);
            Require(headBelongingIds.Length > 0, "Unconscious NPC fixture has no real belongings to preserve.");
            WorldObjectTags tags = headActor.GetComponent<WorldObjectTags>();
            Require(tags != null && tags.HasTag(ActorHealthComponent.AliveActorTag) &&
                    !tags.HasTag(ActorHealthComponent.DeadActorTag) &&
                    !tags.HasTag(ActorHealthComponent.LootableActorTag),
                "Unconscious living actor was incorrectly converted to corpse/dead tags.");

            Require(playerMedical.TryApplyWound(
                    PlayerWound, BodyRegion.RightArm, WoundType.Blunt, 0.2f, 0f, 0.1f, out failure) &&
                    playerCondition.ConsciousnessStability < 1f && playerCondition.CanPerformActiveActions,
                "Player did not use the same physiological condition path as NPCs: " + failure);

            persistedBlood = headCondition.BloodFraction;
            persistedTrauma = headCondition.TransientTrauma;
            CurrentSliceSaveResult save = CurrentSliceSnapshotService.Save(Slot, Store());
            Require(save.Success && save.Snapshot.actors.Single(actor => actor.actorInstanceId == headActorId)
                    .conditionState.transientTrauma > 0f,
                "Current Slice did not capture durable unconscious actor condition: " + save.Failure);
        }

        private static void ProveIncapacitationRecoveryPersistenceAndBloodCollapse()
        {
            Require(headActor != null && headActor.GetComponent<HumanEncounterAIController>().State == HumanEncounterAIState.Inactive,
                "Unconscious NPC encounter AI did not become Inactive.");
            ActorNavigationController navigation = headActor.GetComponent<ActorNavigationController>();
            Require(navigation != null && !navigation.TryNavigate(headActor.transform.position, out ActorNavigationResult navigationResult) &&
                    navigationResult.Failure == ActorNavigationFailure.Incapacitated,
                "Unconscious NPC accepted normal navigation.");
            ActorItemOwnershipComponent ownership = headActor.GetComponent<ActorItemOwnershipComponent>();
            Require(WeaponCombatService.TryGetEquippedWeapon(
                    ownership, out ItemInstance weapon, out _, out _, out _),
                "Unconscious NPC fixture lost its equipped weapon.");
            WeaponCombatResult attack = WeaponCombatService.FireEquipped(
                ownership, weapon.InstanceId, (Collider)null, headActor.transform.position);
            Require(attack.Code == WeaponCombatCode.Incapacitated,
                "Unconscious NPC was allowed to attack normally.");
            Require(headActor.ActorInstanceId == headActorId &&
                    headBelongingIds.SequenceEqual(BelongingIds(headActor), StringComparer.Ordinal),
                "Unconscious NPC lost identity or belongings.");

            Require(WorldClock.Current.TryAdvanceGameTime(WorldClock.SecondsPerHour * 2d, out string failure),
                "Transient trauma recovery clock advance failed: " + failure);
            ActorConditionComponent recovered = headActor.GetComponent<ActorConditionComponent>();
            recoveredStability = recovered.ConsciousnessStability;
            Require(recovered.FunctionalState == ActorFunctionalState.Conscious &&
                    recoveredStability > unconsciousStability + 0.5f &&
                    headActor.LifecycleState == ActorLifecycleState.Alive,
                "Transient non-bleeding trauma did not recover conditionally.");

            CurrentSliceLoadResult load = CurrentSliceLoadService.Load(Slot, Store());
            Require(load.Success && ActorRuntimeRegistry.TryGet(headActorId, out headActor),
                "Current Slice could not restore the unconscious NPC: " + load.Failure);
            ActorConditionComponent restored = headActor.GetComponent<ActorConditionComponent>();
            Require(restored.IsUnconscious && Near(restored.BloodFraction, persistedBlood) &&
                    Near(restored.TransientTrauma, persistedTrauma) &&
                    headActor.LifecycleState == ActorLifecycleState.Alive &&
                    headBelongingIds.SequenceEqual(BelongingIds(headActor), StringComparer.Ordinal),
                "Current Slice did not preserve unconscious state, identity and belongings exactly.");

            ActorRuntimeIdentity bleedingActor = Spawn(ProfileId, headActor.transform.position + new Vector3(6f, 0f, 0f));
            DisableAcquisition(bleedingActor);
            ActorMedicalStateComponent bleedingMedical = bleedingActor.GetComponent<ActorMedicalStateComponent>();
            ActorConditionComponent bleedingCondition = bleedingActor.GetComponent<ActorConditionComponent>();
            ActorHealthComponent bleedingHealth = bleedingActor.GetComponent<ActorHealthComponent>();
            Require(bleedingMedical.TryApplyWound(
                    BleedingWound, BodyRegion.Torso, WoundType.Puncture, 0.4f, 0.4f, 0.2f, out failure),
                "Bleeding deterioration wound failed: " + failure);
            float healthBeforeBleeding = bleedingHealth.CurrentHealth;
            Require(WorldClock.Current.TryAdvanceGameTime(WorldClock.SecondsPerHour * 2d, out failure),
                "Bleeding-to-unconscious advance failed: " + failure);
            bleedingUnconsciousBlood = bleedingCondition.BloodFraction;
            float stabilityBeforeFurtherBleeding = bleedingCondition.ConsciousnessStability;
            Require(bleedingCondition.IsUnconscious && !bleedingHealth.IsDead &&
                    Near(bleedingUnconsciousBlood, 0.2f) && Near(bleedingHealth.CurrentHealth, healthBeforeBleeding),
                "Blood loss did not produce unconscious-but-alive state without parallel HP drain.");

            Require(WorldClock.Current.TryAdvanceGameTime(WorldClock.SecondsPerHour * 0.15d, out failure) &&
                    bleedingCondition.BloodFraction < bleedingUnconsciousBlood &&
                    bleedingCondition.ConsciousnessStability < stabilityBeforeFurtherBleeding,
                "Continuing bleeding did not worsen the unconscious actor.");
            float bloodBeforeBandage = bleedingCondition.BloodFraction;
            float rateBeforeBandage = bleedingMedical.EffectiveBleedingRatePerGameHour;
            Require(bleedingMedical.TryApplyBandage(BleedingWound, 0.1f, out failure),
                "Bandaging deterioration fixture failed: " + failure);
            float rateAfterBandage = bleedingMedical.EffectiveBleedingRatePerGameHour;
            Require(rateAfterBandage < rateBeforeBandage &&
                    WorldClock.Current.TryAdvanceGameTime(WorldClock.SecondsPerHour * 0.5d, out failure),
                "Bandaging did not reduce future bleeding flow: " + failure);
            bleedingAfterBandage = bleedingCondition.BloodFraction;
            Require(Near(bloodBeforeBandage - bleedingAfterBandage, rateAfterBandage * 0.5f) &&
                    bloodBeforeBandage - bleedingAfterBandage < rateBeforeBandage * 0.5f,
                "Post-bandage blood progression did not use reduced EffectiveBleedingRate.");

            Require(WorldClock.Current.TryAdvanceGameTime(WorldClock.SecondsPerHour * 1.1d, out failure),
                "Terminal circulatory collapse advance failed: " + failure);
            WorldObjectTags deathTags = bleedingActor.GetComponent<WorldObjectTags>();
            Require(bleedingHealth.IsDead && bleedingActor.LifecycleState == ActorLifecycleState.Dead &&
                    deathTags.HasTag(ActorHealthComponent.DeadActorTag) &&
                    deathTags.HasTag(ActorHealthComponent.LootableActorTag),
                "Severe blood loss did not terminate through existing health/lifecycle/corpse authority.");

            Require(UnityEngine.Object.FindObjectsByType<ActorConditionComponent>(FindObjectsInactive.Exclude)
                    .All(component => component.GetComponents<ActorConditionComponent>().Length == 1) &&
                    typeof(ActorConditionComponent).Assembly.GetType("OldScars.Core.Actors.PlayerConsciousnessComponent") == null &&
                    typeof(ActorConditionComponent).Assembly.GetType("OldScars.Core.Actors.NpcConsciousnessComponent") == null,
                "Duplicate or player/NPC-specific consciousness authorities were introduced.");

            Debug.Log(
                "Actor Consciousness & Incapacitation Diagnostics: PASS\n" +
                "- Shared authority: Player/NPC ActorConditionComponent; Unconscious remained Alive with identity/belongings\n" +
                "- Trauma: limb stability=" + limbStability.ToString("0.###") +
                " Head=" + headStability.ToString("0.###") +
                " accumulated=" + unconsciousStability.ToString("0.###") +
                " recovered=" + recoveredStability.ToString("0.###") + "\n" +
                "- Blood: unconscious=" + bleedingUnconsciousBlood.ToString("0.###") +
                " post-bandage=" + bleedingAfterBandage.ToString("0.###") +
                " terminal lifecycle=Dead\n" +
                "- Current Slice: unconscious state, ActorInstanceId and belongings restored exactly");
        }

        private static ActorRuntimeIdentity Spawn(string profileId, Vector3 position)
        {
            Require(ActorSpawnService.TrySpawn(profileId, position, Quaternion.identity,
                    out ActorRuntimeIdentity actor, out string failure),
                "Runtime actor spawn failed: " + failure);
            return actor;
        }

        private static ActorInteractionContext FindPlayer()
        {
            ActorInteractionContext[] players = UnityEngine.Object.FindObjectsByType<ActorInteractionContext>(FindObjectsInactive.Exclude)
                .Where(candidate => candidate.ActorTags.Contains("player")).ToArray();
            Require(players.Length == 1, "Expected one Player ActorInteractionContext; found " + players.Length + ".");
            return players[0];
        }

        private static void DisableAcquisition(ActorRuntimeIdentity actor)
        {
            ActorThreatAcquisitionController acquisition = actor.GetComponent<ActorThreatAcquisitionController>();
            if (acquisition != null)
                acquisition.enabled = false;
        }

        private static string[] BelongingIds(ActorRuntimeIdentity actor)
        {
            ActorItemOwnershipComponent ownership = actor.GetComponent<ActorItemOwnershipComponent>();
            return (ownership?.GetAllOwnedEntries() ?? Array.Empty<ItemStorageEntry>())
                .Where(entry => entry?.Item != null)
                .Select(entry => entry.Item.InstanceId + "x" + entry.Quantity)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private static PersistenceFileStore Store()
        {
            string root = SessionState.GetString(RootKey, string.Empty);
            Require(!string.IsNullOrWhiteSpace(root), "Diagnostic persistence root is missing.");
            return new PersistenceFileStore(root);
        }

        private static bool Near(float left, float right, float tolerance = 0.002f) =>
            Mathf.Abs(left - right) <= tolerance;

        private static void Require(bool condition, string failure)
        {
            if (!condition)
                throw new InvalidOperationException(failure);
        }

        private static void Finish()
        {
            string failure = SessionState.GetString(FailureKey, string.Empty);
            string root = SessionState.GetString(RootKey, string.Empty);
            if (EditorSceneManager.GetActiveScene().isDirty)
                failure = Append(failure, "Diagnostics left SampleScene dirty; it was not saved.");
            try
            {
                if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
                    Directory.Delete(root, true);
            }
            catch (Exception exception)
            {
                failure = Append(failure, "Temporary cleanup failed: " + exception.Message);
            }

            bool success = string.IsNullOrWhiteSpace(failure);
            ClearSession();
            if (!success)
                Debug.LogError("Actor Consciousness & Incapacitation Diagnostics: FAIL\n- " + failure);
            if (Application.isBatchMode)
                EditorApplication.Exit(success ? 0 : 1);
        }

        private static void FailAndFinish(string failure)
        {
            SessionState.SetString(FailureKey, failure);
            Finish();
        }

        private static string Append(string current, string value) =>
            string.IsNullOrWhiteSpace(current) ? value : current + "\n- " + value;

        private static void ClearSession()
        {
            SessionState.SetBool(PendingKey, false);
            SessionState.EraseString(StageKey);
            SessionState.EraseString(FailureKey);
            SessionState.EraseString(RootKey);
            limbActor = null;
            headActor = null;
            headActorId = null;
            headBelongingIds = null;
        }
    }
}
