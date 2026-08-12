using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using OldScars.Core;
using OldScars.Core.Actors;
using OldScars.Core.Data;
using OldScars.Core.Interactions;
using OldScars.Core.Items;
using OldScars.Core.Persistence;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OldScars.Editor
{
    [InitializeOnLoad]
    public static class M39LocalizedHealthMedicineDiagnostics
    {
        private const string Menu = "Old Scars/Diagnostics/Actors/Run M39.0 Localized Health & Medicine";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string PhaseKey = "OldScars.M39.Medicine.Phase";
        private const string RootKey = "OldScars.M39.Medicine.Root";
        private const string ErrorKey = "OldScars.M39.Medicine.Error";
        private const string EnterA = "enter_a";
        private const string ExitA = "exit_a";
        private const string EnterB = "enter_b";
        private const string Finish = "finish";
        private const string InitialSlot = "m39_initial";
        private const string TargetSlot = "m39_localized_target";
        private const string LegacySlot = "m39_legacy_without_medical";
        private const string InvalidSlot = "m39_invalid_medical";
        private const string NullSlot = "m39_null_medical";
        private const string NumericEnumSlot = "m39_numeric_enum_medical";
        private const string CaseNullSlot = "m39_case_null_medical";
        private const string LeftWoundId = "wound_11111111111111111111111111111111";
        private const string TorsoWoundId = "wound_22222222222222222222222222222222";
        private const string FatalWoundId = "wound_33333333333333333333333333333333";
        private const string RollbackWoundId = "wound_44444444444444444444444444444444";

        static M39LocalizedHealthMedicineDiagnostics()
        {
            EditorApplication.update += Continue;
        }

        [MenuItem(Menu)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("M39.0 diagnostics require idle Edit Mode.");

            ClearSession();
            string root = Path.Combine(Path.GetTempPath(), "OldScars_M39_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            SessionState.SetString(RootKey, root);
            SessionState.SetString(ErrorKey, string.Empty);
            SessionState.SetString(PhaseKey, EnterA);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem(Menu, true)]
        private static bool ValidateRun()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;
        }

        private static void Continue()
        {
            string phase = SessionState.GetString(PhaseKey, string.Empty);
            if (string.IsNullOrWhiteSpace(phase))
                return;

            if ((phase == EnterA || phase == EnterB) && EditorApplication.isPlaying && WorldClock.Current != null)
                WorldClock.Current.AdvanceDuringGameplay = false;

            if (phase == EnterA && Ready())
            {
                ExecutePlayPhase(RunSessionA, ExitA);
                return;
            }
            if (phase == ExitA && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                SessionState.SetString(PhaseKey, EnterB);
                EditorApplication.EnterPlaymode();
                return;
            }
            if (phase == EnterB && Ready())
            {
                ExecutePlayPhase(RunSessionB, Finish);
                return;
            }
            if (phase == Finish && !EditorApplication.isPlayingOrWillChangePlaymode)
                FinalizeRun();
        }

        private static bool Ready()
        {
            return EditorApplication.isPlaying && Time.frameCount >= 5 && WorldClock.Current != null &&
                   GameDataManager.Instance != null && GameDataManager.Instance.IsReady;
        }

        private static void ExecutePlayPhase(Action action, string nextPhase)
        {
            try
            {
                action();
                SessionState.SetString(PhaseKey, nextPhase);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetString(ErrorKey, exception.Message);
                SessionState.SetString(PhaseKey, Finish);
            }
            EditorApplication.ExitPlaymode();
        }

        private static void RunSessionA()
        {
            WorldClock clock = Clock();
            ActorHealthComponent health = PlayerHealth();
            ActorMedicalStateComponent medical = health.GetComponent<ActorMedicalStateComponent>();
            ActorNeedsComponent needs = health.GetComponent<ActorNeedsComponent>();
            ActorItemOwnershipComponent ownership = health.GetComponent<ActorItemOwnershipComponent>();
            Require(medical != null && needs != null && ownership != null && !health.IsDead,
                "Player medical/needs/ownership baseline is incomplete.");

            CurrentSliceSaveData initial = Capture("initial M39 bootstrap");
            Write(InitialSlot, initial);

            Require(medical.WoundCount == 0 && ActorMedicalStateComponent.HumanRegions.Count == 6 &&
                    Near(medical.VitalFraction, 1f),
                "A. Healthy baseline did not expose six uninjured regions and full vital state.");

            Require(medical.TryApplyWound(
                    LeftWoundId, BodyRegion.LeftArm, WoundType.Laceration, 0.5f, 0.09f, 0.35f, out string failure),
                "B. Left-arm laceration failed: " + failure);
            ActorMedicalWoundState left = medical.GetWound(LeftWoundId);
            Require(left != null && left.region == BodyRegion.LeftArm.ToString() && Near(left.severity, 0.5f) &&
                    medical.GetWounds(BodyRegion.LeftArm).Length == 1 && medical.GetWounds(BodyRegion.RightArm).Length == 0,
                "B. Localized wound identity/severity or regional isolation failed.");
            Require(medical.TryApplyWound(
                    TorsoWoundId, BodyRegion.Torso, WoundType.Puncture, 0.4f, 0.048f, 0.32f, out failure),
                "G. Torso isolation fixture failed: " + failure);

            RestoreClock(clock, 0d);
            float beforeBleed = health.CurrentHealth;
            float rateBeforeTreatment = medical.EffectiveBleedingRatePerGameHour;
            int events = 0;
            Action<double> countAdvance = _ => events++;
            clock.GameTimeAdvanced += countAdvance;
            Require(clock.TryAdvanceGameTime(WorldClock.SecondsPerHour * 0.25d, out failure),
                "C. Known bleeding advance failed: " + failure);
            clock.GameTimeAdvanced -= countAdvance;
            float expectedAfterBleed = beforeBleed - rateBeforeTreatment * 0.25f * health.MaxHealth;
            Require(events == 1 && Near(health.CurrentHealth, expectedAfterBleed),
                "C. Bleeding did not change vital state exactly once for the WorldClock delta.");

            float beforeRest = health.CurrentHealth;
            events = 0;
            clock.GameTimeAdvanced += countAdvance;
            ActorRestResult rest = ActorRestService.TryRest(needs, WorldClock.SecondsPerHour * 0.5d);
            clock.GameTimeAdvanced -= countAdvance;
            float expectedAfterRest = beforeRest - rateBeforeTreatment * 0.5f * health.MaxHealth;
            Require(rest.Success && events == 1 && Near(health.CurrentHealth, expectedAfterRest) && medical.WoundCount == 2,
                "D. Rest did not process one shared clock delta or removed/healed a wound.");
            Require(Near(medical.TotalPain, 0.67f), "E. Pain was not derived from both durable wounds.");

            InventoryComponent inventory = ownership.PersonalInventory;
            int treatmentsBeforeAdd = InventoryItemUseService.GetAvailableWoundTreatmentQuantity(ownership);
            ItemInstance addedBandage = inventory.AddItemByDefinitionId("core:bandage_01", 2);
            Require(addedBandage != null &&
                    InventoryItemUseService.GetAvailableWoundTreatmentQuantity(ownership) == treatmentsBeforeAdd + 2,
                "F. Could not add two data-driven bandage treatments to player ownership.");
            int treatmentBefore = InventoryItemUseService.GetAvailableWoundTreatmentQuantity(ownership);
            ActorMedicalWoundState torsoBefore = medical.GetWound(TorsoWoundId);
            InventoryItemUseResult treatment = InventoryItemUseService.TryApplyWoundTreatment(ownership, medical, LeftWoundId);
            left = medical.GetWound(LeftWoundId);
            Require(treatment.Success && InventoryItemUseService.GetAvailableWoundTreatmentQuantity(ownership) == treatmentBefore - 1 &&
                    left != null && left.treatmentState == WoundTreatmentState.Bandaged.ToString() &&
                    Near(ActorMedicalStateComponent.EffectiveBleedingRate(left), 0.009f) && medical.WoundCount == 2,
                "F. Bandage did not consume exactly one item, retain the wound and reduce its bleeding: " + treatment.Message);
            int afterFirstTreatment = InventoryItemUseService.GetAvailableWoundTreatmentQuantity(ownership);
            InventoryItemUseResult secondTreatment = InventoryItemUseService.TryApplyWoundTreatment(ownership, medical, LeftWoundId);
            ActorMedicalWoundState torsoAfter = medical.GetWound(TorsoWoundId);
            Require(!secondTreatment.Success &&
                    InventoryItemUseService.GetAvailableWoundTreatmentQuantity(ownership) == afterFirstTreatment &&
                    EquivalentWound(torsoBefore, torsoAfter),
                "F/G. Repeat treatment was not rejected atomically or altered the torso wound.");

            RunHealthWindowRegression(health);
            RunDeathRegression(clock, health);

            CurrentSliceSaveData target = Capture("M39 localized target");
            Require(target.player.medicalState.wounds.Length == 2 &&
                    target.player.medicalState.wounds.Any(wound => wound.woundId == LeftWoundId &&
                        wound.treatmentState == WoundTreatmentState.Bandaged.ToString()),
                "I. Target DTO did not capture localized wounds and treatment.");
            Write(TargetSlot, target);

            JObject legacy = (JObject)CurrentSliceSnapshotService.ToPayload(target).DeepClone();
            Require(((JObject)legacy["player"]).Remove("medicalState"),
                "K. Could not construct legacy player payload.");
            foreach (JObject actor in legacy["actors"].Children<JObject>())
                Require(actor.Remove("medicalState"), "K. Could not construct legacy actor payload.");
            WritePayload(LegacySlot, legacy);

            JObject invalid = (JObject)CurrentSliceSnapshotService.ToPayload(target).DeepClone();
            invalid["player"]["medicalState"]["wounds"][0]["severity"] = 1.5f;
            WritePayload(InvalidSlot, invalid);
            JObject presentNull = (JObject)CurrentSliceSnapshotService.ToPayload(target).DeepClone();
            presentNull["player"]["medicalState"] = JValue.CreateNull();
            WritePayload(NullSlot, presentNull);
            JObject numericEnum = (JObject)CurrentSliceSnapshotService.ToPayload(target).DeepClone();
            numericEnum["player"]["medicalState"]["wounds"][0]["region"] = "0";
            WritePayload(NumericEnumSlot, numericEnum);
            JObject caseNull = (JObject)CurrentSliceSnapshotService.ToPayload(target).DeepClone();
            JObject caseNullPlayer = (JObject)caseNull["player"];
            caseNullPlayer.Remove("medicalState");
            caseNullPlayer["MedicalState"] = JValue.CreateNull();
            WritePayload(CaseNullSlot, caseNull);
        }

        private static void RunDeathRegression(WorldClock clock, ActorHealthComponent playerHealth)
        {
            ActorRuntimeIdentity source = ActorRuntimeRegistry.ActiveRepresentations.FirstOrDefault(identity =>
                identity != null && identity.GetComponent<ActorHealthComponent>() != playerHealth &&
                identity.OriginKind == ActorOriginKind.Authored && identity.LifecycleState == ActorLifecycleState.Alive);
            Require(source != null, "H. No living authored profile exists for runtime victim bootstrap.");
            Require(ActorSpawnService.TrySpawn(
                    source.ActorProfileId,
                    source.transform.position + new Vector3(3f, 0f, 2f),
                    Quaternion.Euler(0f, 37f, 0f),
                    out ActorRuntimeIdentity victim,
                    out string spawnFailure),
                "H. Runtime victim bootstrap failed: " + spawnFailure);
            Require(victim.OriginKind == ActorOriginKind.Runtime,
                "H. Lethal medical fixture did not use a runtime-origin actor.");
            ActorMedicalStateComponent medical = victim.GetComponent<ActorMedicalStateComponent>();
            ActorHealthComponent health = victim.GetComponent<ActorHealthComponent>();
            Require(medical != null && health != null, "H. Victim lacks medical or lifecycle health state.");
            Require(medical.TryApplyWound(
                    FatalWoundId, BodyRegion.Torso, WoundType.Puncture, 1f, 1f, 1f, out string failure),
                "H. Fatal bleeding fixture failed: " + failure);
            Require(clock.TryAdvanceGameTime(WorldClock.SecondsPerHour, out failure),
                "H. Lethal bleeding clock advance failed: " + failure);
            Require(health.IsDead && victim.LifecycleState == ActorLifecycleState.Dead &&
                    victim.GetComponent<WorldObjectTags>()?.HasTag(ActorHealthComponent.DeadActorTag) == true &&
                    victim.GetComponent<WorldObjectTags>()?.HasTag(ActorHealthComponent.LootableActorTag) == true,
                "H. Vital depletion did not preserve M38 Dead/corpse continuity.");
            ActorMedicalStateData deadMedical = medical.CaptureState();
            int revision = medical.Revision;
            Require(clock.TryAdvanceGameTime(WorldClock.SecondsPerHour, out failure),
                "H. Post-death clock advance failed: " + failure);
            Require(health.CurrentHealth == 0f && medical.Revision == revision &&
                    EquivalentMedical(deadMedical, medical.CaptureState()),
                "H. Dead actor medical state continued progressing after death.");
        }

        private static void RunHealthWindowRegression(ActorHealthComponent health)
        {
            ActorHealthDebugWindow window = UnityEngine.Object.FindAnyObjectByType<ActorHealthDebugWindow>();
            InventoryUISessionController inventory = UnityEngine.Object.FindAnyObjectByType<InventoryUISessionController>();
            DebugWorldUiInputBlocker blocker = UnityEngine.Object.FindAnyObjectByType<DebugWorldUiInputBlocker>();
            Require(window != null && inventory != null && blocker != null,
                "N. Health Window foundation runtime objects are missing.");
            window.SetActorHealth(health);
            window.Open();
            Require(window.IsOpen && window.GetRegionAssessment(BodyRegion.LeftArm).Contains("herida") &&
                    !blocker.BlocksWorldInput,
                "E/N. Health Window did not expose qualitative regional state or incorrectly blocked WASD globally.");
            Vector2 point = new Vector2(260f, Screen.height - 32f);
            Require(window.ContainsScreenPosition(point) && blocker.ConsumeLeftClickIfNeeded(point),
                "N. Health Window internal click leaked to world input.");
            inventory.OpenPersonal();
            Require(inventory.IsOpen && !window.IsOpen, "N. Inventory did not close Health Window.");
            window.Open();
            Require(window.IsOpen && !inventory.IsOpen && !blocker.BlocksWorldInput,
                "N. Health did not close Inventory while preserving movement input.");
            window.Close();
        }

        private static void RunSessionB()
        {
            WorldClock clock = Clock();
            ActorHealthComponent health = PlayerHealth();
            ActorMedicalStateComponent medical = health.GetComponent<ActorMedicalStateComponent>();
            Require(medical != null && medical.WoundCount == 0,
                "J. Fresh Play session did not bootstrap a healthy localized state.");

            CurrentSliceSaveData target = Read(TargetSlot);
            CurrentSliceLoadResult targetLoad = CurrentSliceLoadService.Load(TargetSlot, Store());
            Require(targetLoad.Success, "I/J. Fresh-session localized load failed: " + targetLoad.Failure);
            AssertEquivalent(target, Capture("post M39 fresh-session load"), "I/J. localized round-trip");
            Require(EquivalentMedical(target.player.medicalState, medical.CaptureState()) &&
                    Near(target.player.currentHealth, health.CurrentHealth) &&
                    Near(medical.TotalPain, target.player.medicalState.wounds.Sum(wound => wound.painContribution)),
                "I/J. Wound IDs/regions/treatment/pain/vital state did not restore exactly.");
            ActorState deadTarget = target.actors.Single(actor => actor.medicalState.wounds.Any(wound => wound.woundId == FatalWoundId));
            Require(deadTarget.originKind == "Runtime" &&
                    ActorRuntimeRegistry.TryGet(deadTarget.actorInstanceId, out ActorRuntimeIdentity deadIdentity) &&
                    deadIdentity.OriginKind == ActorOriginKind.Runtime &&
                    deadIdentity.LifecycleState == ActorLifecycleState.Dead &&
                    deadIdentity.GetComponent<ActorMedicalStateComponent>().GetWound(FatalWoundId) != null,
                "H/I. Dead actor medical/corpse continuity did not survive fresh-session load.");

            CurrentSliceSaveData legacy = Read(LegacySlot);
            Require(legacy.player.medicalState != null && legacy.player.medicalState.wounds.Length == 0 &&
                    legacy.actors.All(actor => actor.medicalState != null && actor.medicalState.wounds.Length == 0),
                "K. Omitted localized state did not normalize to an etiology-free baseline.");
            CurrentSliceLoadResult legacyLoad = CurrentSliceLoadService.Load(LegacySlot, Store());
            Require(legacyLoad.Success && medical.WoundCount == 0 && Near(health.CurrentHealth, legacy.player.currentHealth),
                "K. Legacy schema-v1 scalar health baseline did not load safely: " + legacyLoad.Failure);

            CurrentSliceSaveData beforeInvalid = Capture("pre invalid medical preflight");
            CurrentSliceLoadResult invalidLoad = CurrentSliceLoadService.Load(InvalidSlot, Store());
            Require(invalidLoad.FailureCode == CurrentSliceLoadFailureCode.SemanticPreflightFailed &&
                    !invalidLoad.MutationStarted,
                "L. Invalid medical payload was not rejected before mutation.");
            AssertEquivalent(beforeInvalid, Capture("post invalid medical preflight"), "L. medical no-mutation preflight");
            CurrentSliceLoadResult nullLoad = CurrentSliceLoadService.Load(NullSlot, Store());
            Require(nullLoad.FailureCode == CurrentSliceLoadFailureCode.SemanticPreflightFailed &&
                    !nullLoad.MutationStarted,
                "L. Explicit-null medical state was not distinguished from legacy omission.");
            AssertEquivalent(beforeInvalid, Capture("post null medical preflight"), "L. null medical no-mutation preflight");
            CurrentSliceLoadResult numericLoad = CurrentSliceLoadService.Load(NumericEnumSlot, Store());
            Require(numericLoad.FailureCode == CurrentSliceLoadFailureCode.SemanticPreflightFailed &&
                    !numericLoad.MutationStarted,
                "L. Numeric enum text was not rejected as non-canonical medical state.");
            AssertEquivalent(beforeInvalid, Capture("post numeric enum preflight"), "L. numeric enum no-mutation preflight");
            CurrentSliceLoadResult caseNullLoad = CurrentSliceLoadService.Load(CaseNullSlot, Store());
            Require(caseNullLoad.FailureCode == CurrentSliceLoadFailureCode.SemanticPreflightFailed &&
                    !caseNullLoad.MutationStarted,
                "L. Case-variant explicit-null medical state was misclassified as legacy omission.");
            AssertEquivalent(beforeInvalid, Capture("post case null preflight"), "L. case null no-mutation preflight");

            Require(medical.TryApplyWound(
                    RollbackWoundId, BodyRegion.RightLeg, WoundType.Blunt, 0.3f, 0f, 0.27f, out string failure),
                "M. Rollback fixture wound failed: " + failure);
            RestoreClock(clock, WorldClock.SecondsPerDay * 4d + 17d);
            CurrentSliceSaveData beforeFault = Capture("pre post-medical fault");
            CurrentSliceLoadService.DiagnosticInjectFailureAfterRuntimeStateRestore = true;
            CurrentSliceLoadResult fault = CurrentSliceLoadService.Load(TargetSlot, Store());
            Require(fault.FailureCode == CurrentSliceLoadFailureCode.ApplyFailed &&
                    fault.RollbackAttempted && fault.RollbackSucceeded,
                "M. Post-medical fault did not report successful rollback: " + fault.Failure);
            AssertEquivalent(beforeFault, Capture("post medical rollback"), "M. exact medical rollback");

            CurrentSliceSaveData initial = Read(InitialSlot);
            CurrentSliceLoadResult cleanup = CurrentSliceLoadService.Load(InitialSlot, Store());
            Require(cleanup.Success, "M39 initial-state cleanup failed: " + cleanup.Failure);
            AssertEquivalent(initial, Capture("M39 initial cleanup"), "M39 diagnostic cleanup");
            CurrentSliceLoadService.DiagnosticInjectFailureAfterRuntimeStateRestore = false;
        }

        private static ActorHealthComponent PlayerHealth()
        {
            ActorInteractionContext[] actors = UnityEngine.Object.FindObjectsByType<ActorInteractionContext>(FindObjectsInactive.Exclude);
            ActorInteractionContext player = actors.SingleOrDefault(candidate => candidate.ActorTags.Contains("player"));
            Require(player != null, $"Expected exactly one player ActorInteractionContext; found {actors.Length} actors.");
            ActorHealthComponent health = player.GetComponent<ActorHealthComponent>();
            Require(health != null, "Player ActorHealthComponent is unavailable.");
            return health;
        }

        private static WorldClock Clock()
        {
            WorldClock clock = WorldClock.Current;
            Require(clock != null, "WorldClock authority is unavailable.");
            clock.AdvanceDuringGameplay = false;
            return clock;
        }

        private static PersistenceFileStore Store()
        {
            string root = SessionState.GetString(RootKey, string.Empty);
            Require(!string.IsNullOrWhiteSpace(root), "Temporary persistence root is missing.");
            return new PersistenceFileStore(root);
        }

        private static CurrentSliceSaveData Capture(string label)
        {
            CurrentSliceResult result = CurrentSliceSnapshotService.Capture();
            Require(result.Success, label + " capture failed: " + result.Failure);
            return result.Snapshot;
        }

        private static void Write(string slot, CurrentSliceSaveData snapshot)
        {
            WritePayload(slot, CurrentSliceSnapshotService.ToPayload(snapshot));
        }

        private static void WritePayload(string slot, JToken payload)
        {
            PersistenceWriteResult result = Store().Write(slot, payload);
            Require(result.Success, $"Slot '{slot}' write failed: {result.Failure}");
        }

        private static CurrentSliceSaveData Read(string slot)
        {
            CurrentSliceResult result = CurrentSliceSnapshotService.Read(slot, Store());
            Require(result.Success, $"Slot '{slot}' read/preflight failed: {result.Failure}");
            return result.Snapshot;
        }

        private static void RestoreClock(WorldClock clock, double elapsed)
        {
            Require(clock.TryRestoreElapsedGameSeconds(elapsed, out string failure),
                "WorldClock setup failed: " + failure);
        }

        private static void AssertEquivalent(CurrentSliceSaveData expected, CurrentSliceSaveData actual, string label)
        {
            CurrentSliceComparisonResult comparison = CurrentSliceSnapshotService.Compare(expected, actual);
            Require(comparison.Equivalent, label + " differs: " + comparison.Difference);
        }

        private static bool EquivalentMedical(ActorMedicalStateData left, ActorMedicalStateData right)
        {
            if (left?.wounds == null || right?.wounds == null || left.wounds.Length != right.wounds.Length)
                return false;
            return left.wounds.OrderBy(wound => wound.woundId).Zip(
                right.wounds.OrderBy(wound => wound.woundId), EquivalentWound).All(value => value);
        }

        private static bool EquivalentWound(ActorMedicalWoundState left, ActorMedicalWoundState right)
        {
            return left != null && right != null && left.woundId == right.woundId && left.region == right.region &&
                   left.woundType == right.woundType && Near(left.severity, right.severity) &&
                   Near(left.bleedingRatePerGameHour, right.bleedingRatePerGameHour) &&
                   Near(left.painContribution, right.painContribution) && left.treatmentState == right.treatmentState &&
                   Near(left.treatmentBleedingMultiplier, right.treatmentBleedingMultiplier);
        }

        private static bool Near(float left, float right, float tolerance = 0.001f)
        {
            return Mathf.Abs(left - right) <= tolerance;
        }

        private static void Require(bool condition, string failure)
        {
            if (!condition)
                throw new InvalidOperationException(failure);
        }

        private static void FinalizeRun()
        {
            string failure = SessionState.GetString(ErrorKey, string.Empty);
            string root = SessionState.GetString(RootKey, string.Empty);
            if (EditorSceneManager.GetActiveScene().isDirty)
                failure = Append(failure, "Diagnostics left SampleScene dirty; it was not saved.");
            try
            {
                if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
                    Directory.Delete(root, true);
                if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
                    failure = Append(failure, "Temporary persistence root still exists after cleanup.");
            }
            catch (Exception exception)
            {
                failure = Append(failure, "Temporary cleanup failed: " + exception.Message);
            }

            bool success = string.IsNullOrWhiteSpace(failure);
            ClearSession();
            if (success)
                Debug.Log("M39.0 Localized Health & Medicine Diagnostics: PASS");
            else
                Debug.LogError("M39.0 Localized Health & Medicine Diagnostics: FAIL\n- " + failure);
            if (Application.isBatchMode)
                EditorApplication.Exit(success ? 0 : 1);
        }

        private static string Append(string current, string value)
        {
            return string.IsNullOrWhiteSpace(current) ? value : current + "\n- " + value;
        }

        private static void ClearSession()
        {
            CurrentSliceLoadService.DiagnosticInjectFailureAfterRuntimeStateRestore = false;
            SessionState.EraseString(PhaseKey);
            SessionState.EraseString(RootKey);
            SessionState.EraseString(ErrorKey);
        }
    }
}
