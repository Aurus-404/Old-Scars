using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OldScars.Core;
using OldScars.Core.Actors;
using OldScars.Core.Combat;
using OldScars.Core.Data.Definitions;
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
        private const string FatalWound = "wound_f6666666666666666666666666666666";
        private const string HysteresisWoundA = "wound_a7777777777777777777777777777777";
        private const string HysteresisWoundB = "wound_b8888888888888888888888888888888";

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
        private static float recoveredTrauma;
        private static float immediateTraumaContribution;
        private static float combinedBloodBefore;
        private static float combinedBloodAfter;
        private static float combinedTraumaBefore;
        private static float combinedTraumaAfter;
        private static float bleedingUnconsciousBlood;
        private static float recoveredBlood;

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
                    playerCondition.IsConfigured && Near(playerCondition.BloodFraction, 1f) &&
                    Near(playerCondition.TransientTrauma, 0f) && Near(playerCondition.ConsciousnessStability, 1f) &&
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

            float limbTraumaBefore = limbCondition.TransientTrauma;
            int limbConditionRevisionBefore = limbCondition.Revision;
            Require(limbMedical.TryApplyWound(
                    LimbWound, BodyRegion.LeftArm, WoundType.Blunt, 0.4f, 0f, 0.3f, out string failure),
                "Equivalent limb blunt wound failed: " + failure);
            immediateTraumaContribution = limbCondition.TransientTrauma - limbTraumaBefore;
            Require(Near(immediateTraumaContribution, 0.4f * 0.65f * 0.65f) &&
                    limbCondition.Revision == limbConditionRevisionBefore + 1,
                "One durable wound did not produce exactly one immediate trauma contribution.");
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

            ActorMedicalStateData limbWounds = limbMedical.CaptureState();
            ActorConditionStateData limbConditionAfterInjury = limbCondition.CaptureState();
            Require(limbCondition.TryApplyPersistenceState(ActorConditionComponent.HealthyBaseline(), out failure) &&
                    limbMedical.TryApplyPersistenceState(limbWounds, out failure) &&
                    Near(limbCondition.TransientTrauma, 0f),
                "Restoring an existing wound reapplied its immediate trauma consequence: " + failure);
            Require(limbCondition.TryApplyPersistenceState(limbConditionAfterInjury, out failure) &&
                    Near(limbCondition.TransientTrauma, immediateTraumaContribution),
                "Condition persistence did not restore the separately persisted trauma consequence: " + failure);

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
            recoveredTrauma = recovered.TransientTrauma;
            Require(recovered.FunctionalState == ActorFunctionalState.Conscious &&
                    recoveredStability > unconsciousStability + 0.5f && recoveredTrauma < persistedTrauma &&
                    headActor.LifecycleState == ActorLifecycleState.Alive,
                "Transient non-bleeding trauma did not recover conditionally.");

            CurrentSliceLoadResult load = CurrentSliceLoadService.Load(Slot, Store());
            Require(load.Success && ActorRuntimeRegistry.TryGet(headActorId, out headActor),
                "Current Slice could not restore the unconscious NPC: " + load.Failure);
            ActorConditionComponent restored = headActor.GetComponent<ActorConditionComponent>();
            Require(restored.IsUnconscious && Near(restored.BloodFraction, persistedBlood) &&
                    Near(restored.TransientTrauma, persistedTrauma) &&
                    restored.TransientTrauma > recoveredTrauma &&
                    headActor.LifecycleState == ActorLifecycleState.Alive &&
                    headBelongingIds.SequenceEqual(BelongingIds(headActor), StringComparer.Ordinal),
                "Current Slice did not intentionally restore the older traumatic state, identity and belongings exactly.");

            ProveFunctionalStateHysteresis(headActor.transform.position + new Vector3(9f, 0f, 0f));

            ActorRuntimeIdentity bleedingActor = Spawn(ProfileId, headActor.transform.position + new Vector3(6f, 0f, 0f));
            DisableAcquisition(bleedingActor);
            ActorMedicalStateComponent bleedingMedical = bleedingActor.GetComponent<ActorMedicalStateComponent>();
            ActorConditionComponent bleedingCondition = bleedingActor.GetComponent<ActorConditionComponent>();
            ActorHealthComponent bleedingHealth = bleedingActor.GetComponent<ActorHealthComponent>();
            Require(bleedingMedical.TryApplyWound(
                    BleedingWound, BodyRegion.Torso, WoundType.Puncture, 0.4f, 0.4f, 0.2f, out failure),
                "Bleeding deterioration wound failed: " + failure);
            float healthBeforeBleeding = bleedingHealth.CurrentHealth;
            combinedBloodBefore = bleedingCondition.BloodFraction;
            combinedTraumaBefore = bleedingCondition.TransientTrauma;
            Require(WorldClock.Current.TryAdvanceGameTime(WorldClock.SecondsPerHour * 0.1d, out failure),
                "Combined blood/trauma advance failed: " + failure);
            combinedBloodAfter = bleedingCondition.BloodFraction;
            combinedTraumaAfter = bleedingCondition.TransientTrauma;
            Require(combinedBloodAfter < combinedBloodBefore && combinedTraumaAfter < combinedTraumaBefore,
                "Blood loss and transient trauma recovery did not progress independently in the same WorldClock advance.");
            Require(WorldClock.Current.TryAdvanceGameTime(WorldClock.SecondsPerHour * 2.1875d, out failure),
                "Bleeding-to-unconscious advance failed: " + failure);
            bleedingUnconsciousBlood = bleedingCondition.BloodFraction;
            float stabilityBeforeFurtherBleeding = bleedingCondition.ConsciousnessStability;
            Require(bleedingCondition.IsUnconscious && !bleedingHealth.IsDead &&
                    bleedingUnconsciousBlood > bleedingCondition.FatalBloodFraction &&
                    Near(bleedingHealth.CurrentHealth, healthBeforeBleeding),
                "Blood loss did not produce unconscious-but-alive state without parallel HP drain.");

            Require(WorldClock.Current.TryAdvanceGameTime(WorldClock.SecondsPerHour * 0.005d, out failure) &&
                    bleedingCondition.BloodFraction < bleedingUnconsciousBlood &&
                    bleedingCondition.ConsciousnessStability < stabilityBeforeFurtherBleeding,
                "Continuing bleeding did not worsen the unconscious actor.");
            float bloodBeforeRecovery = bleedingCondition.BloodFraction;
            Require(bleedingMedical.TryApplyBandage(BleedingWound, 0f, out failure) &&
                    Near(bleedingMedical.EffectiveBleedingRatePerGameHour, 0f) &&
                    WorldClock.Current.TryAdvanceGameTime(WorldClock.SecondsPerHour * 0.5d, out failure),
                "Stabilizing the wound did not stop future bleeding: " + failure);
            recoveredBlood = bleedingCondition.BloodFraction;
            Require(recoveredBlood > bloodBeforeRecovery && recoveredBlood <= 1f &&
                    Near(recoveredBlood - bloodBeforeRecovery, bleedingCondition.BloodRecoveryPerGameHour * 0.5f),
                "Blood did not recover slowly after bleeding reached zero.");

            ActorRuntimeIdentity fatalActor = Spawn(ProfileId, headActor.transform.position + new Vector3(12f, 0f, 0f));
            DisableAcquisition(fatalActor);
            ActorMedicalStateComponent fatalMedical = fatalActor.GetComponent<ActorMedicalStateComponent>();
            ActorConditionComponent fatalCondition = fatalActor.GetComponent<ActorConditionComponent>();
            ActorHealthComponent fatalHealth = fatalActor.GetComponent<ActorHealthComponent>();
            Require(fatalMedical.TryApplyWound(
                    FatalWound, BodyRegion.Torso, WoundType.Puncture, 0.2f, 1f, 0f, out failure) &&
                    WorldClock.Current.TryAdvanceGameTime(WorldClock.SecondsPerHour * 0.95d, out failure),
                "Terminal circulatory collapse advance failed: " + failure);
            WorldObjectTags deathTags = fatalActor.GetComponent<WorldObjectTags>();
            int fatalRevision = fatalCondition.Revision;
            Require(fatalHealth.IsDead && fatalActor.LifecycleState == ActorLifecycleState.Dead &&
                    deathTags.HasTag(ActorHealthComponent.DeadActorTag) &&
                    deathTags.HasTag(ActorHealthComponent.LootableActorTag),
                "Severe blood loss did not terminate through existing health/lifecycle/corpse authority.");
            Require(WorldClock.Current.TryAdvanceGameTime(WorldClock.SecondsPerHour, out failure) &&
                    fatalCondition.Revision == fatalRevision && fatalHealth.IsDead && fatalHealth.CurrentHealth == 0f,
                "Fatal blood loss repeated condition/death mutation after Health/Lifecycle reached Dead.");

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
                " immediate=" + immediateTraumaContribution.ToString("0.###") +
                " recovered=" + recoveredTrauma.ToString("0.###") +
                " restored=" + persistedTrauma.ToString("0.###") + "\n" +
                "- Combined progression: blood " + combinedBloodBefore.ToString("0.###") + "->" + combinedBloodAfter.ToString("0.###") +
                " trauma " + combinedTraumaBefore.ToString("0.###") + "->" + combinedTraumaAfter.ToString("0.###") + "\n" +
                "- Blood: unconscious=" + bleedingUnconsciousBlood.ToString("0.###") +
                " recovered=" + recoveredBlood.ToString("0.###") +
                " terminal lifecycle=Dead\n" +
                "- Hysteresis: threshold jitter stable; large recovery crossed multiple states\n" +
                "- Current Slice: recovered trauma stayed low until the older traumatic snapshot was intentionally loaded");
        }

        private static void ProveFunctionalStateHysteresis(Vector3 position)
        {
            var legacyCloseThresholds = new ActorProfileConsciousness
            {
                consciousness_resilience = 1f,
                pain_tolerance = 0.35f,
                blunt_trauma_resistance = 1f,
                dazed_threshold = 0.75f,
                incapacitated_threshold = 0.23f,
                unconscious_threshold = 0.2f,
                blood_pressure_start_fraction = 0.65f,
                fatal_blood_fraction = 0.08f,
                trauma_recovery_per_game_hour = 0.6f
            };
            Require(ActorConditionComponent.TryValidateProfile(legacyCloseThresholds, out string legacyFailure),
                "A legacy consciousness profile with close thresholds was rejected by default hysteresis: " + legacyFailure);

            ActorRuntimeIdentity actor = Spawn(ProfileId, position);
            DisableAcquisition(actor);
            ActorConditionComponent condition = actor.GetComponent<ActorConditionComponent>();
            ActorMedicalStateComponent medical = actor.GetComponent<ActorMedicalStateComponent>();
            Require(condition.TryApplyPersistenceState(StateForStability(0.19f), out string failure) &&
                    condition.FunctionalState == ActorFunctionalState.Unconscious,
                "Hysteresis fixture did not enter Unconscious: " + failure);
            Require(AdvanceToStability(condition, 0.201f) &&
                    condition.FunctionalState == ActorFunctionalState.Unconscious &&
                    medical.TryApplyWound(HysteresisWoundA, BodyRegion.LeftArm, WoundType.Laceration,
                        0.002f / (0.25f * 0.65f), 0f, 0f, out failure) &&
                    Near(condition.ConsciousnessStability, 0.199f) &&
                    condition.FunctionalState == ActorFunctionalState.Unconscious,
                "Unconscious state flapped around its deterioration threshold.");
            Require(AdvanceToStability(condition, 0.26f) &&
                    condition.FunctionalState == ActorFunctionalState.Incapacitated &&
                    AdvanceToStability(condition, 0.451f) &&
                    condition.FunctionalState == ActorFunctionalState.Incapacitated &&
                    medical.TryApplyWound(HysteresisWoundB, BodyRegion.LeftArm, WoundType.Laceration,
                        0.002f / (0.25f * 0.65f), 0f, 0f, out failure) &&
                    Near(condition.ConsciousnessStability, 0.449f) &&
                    condition.FunctionalState == ActorFunctionalState.Incapacitated,
                "Incapacitated state flapped around its deterioration threshold.");
            Require(AdvanceToStability(condition, 0.51f) &&
                    condition.FunctionalState == ActorFunctionalState.Dazed &&
                    AdvanceToStability(condition, 0.81f) &&
                    condition.FunctionalState == ActorFunctionalState.Conscious,
                "Recovery did not cross the explicit hysteresis boundaries or traverse multiple states.");
            Require(condition.TryApplyPersistenceState(StateForStability(0.19f), out failure) &&
                    condition.FunctionalState == ActorFunctionalState.Unconscious &&
                    condition.TryApplyPersistenceState(StateForStability(0.21f), out failure) &&
                    condition.FunctionalState == ActorFunctionalState.Incapacitated,
                "Persistence restore inherited the worse pre-load state through runtime hysteresis.");
        }

        private static bool AdvanceToStability(ActorConditionComponent condition, float targetStability)
        {
            const float CoreTraumaRecoveryPerGameHour = 0.6f;
            float stabilityDelta = targetStability - condition.ConsciousnessStability;
            return stabilityDelta > 0f &&
                   condition.AdvancePhysiology(
                       WorldClock.SecondsPerHour * stabilityDelta / CoreTraumaRecoveryPerGameHour) &&
                   Near(condition.ConsciousnessStability, targetStability);
        }

        private static ActorConditionStateData StateForStability(float stability) => new ActorConditionStateData
        {
            bloodFraction = 1f,
            transientTrauma = 1f - stability
        };

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
