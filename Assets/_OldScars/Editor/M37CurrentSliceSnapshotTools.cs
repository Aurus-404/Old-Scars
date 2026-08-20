using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using OldScars.Core;
using OldScars.Core.Data;
using OldScars.Core.Persistence;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OldScars.Editor
{
    [InitializeOnLoad]
    public static class M37CurrentSliceSnapshotTools
    {
        private const string SaveMenu = "Old Scars/Persistence/M37.1/Save Debug Slot";
        private const string LoadMenu = "Old Scars/Persistence/M37.1/Load Debug Slot";
        private const string DiagnosticMenu = "Old Scars/Diagnostics/M37.1/Run Snapshot & Semantic Preflight";
        private const string RoundTripDiagnosticMenu = "Old Scars/Diagnostics/M37.1/Run Current Slice Persistent Round-Trip";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string PendingKey = "OldScars.M37.1.SnapshotDiagnostics.Pending";
        private const string RunningKey = "OldScars.M37.1.SnapshotDiagnostics.Running";
        private const string ResultKey = "OldScars.M37.1.SnapshotDiagnostics.Result";
        private const string ModeKey = "OldScars.M37.1.SnapshotDiagnostics.Mode";

        static M37CurrentSliceSnapshotTools()
        {
            EditorApplication.update += ContinueBatchDiagnostic;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static void SaveDebugSlot()
        {
            CurrentSliceSaveResult result = CurrentSliceSnapshotService.Save(CurrentSliceSnapshotService.DebugSlotId);
            if (!result.Success)
            {
                Debug.LogError("[Persistence][CURRENT_SLICE_SAVE_FAILURE]" +
                    $"\nSlot: {CurrentSliceSnapshotService.DebugSlotId}\nFailureCode: SnapshotOrWriteFailed" +
                    $"\nFailure: {result.Failure}\nActionTaken: no snapshot was committed");
                return;
            }
            Debug.Log(CurrentSliceSnapshotService.BuildSuccessSummary(CurrentSliceSnapshotService.DebugSlotId, result.Snapshot));
        }

        private static bool ValidateSaveDebugSlot() => EditorApplication.isPlaying && !EditorApplication.isCompiling;

        public static void LoadDebugSlot()
        {
            CurrentSliceLoadResult result = CurrentSliceLoadService.Load(CurrentSliceSnapshotService.DebugSlotId);
            if (!result.Success)
            {
                Debug.LogError("[Persistence][CURRENT_SLICE_LOAD_FAILURE]" +
                    $"\nSlot: {CurrentSliceSnapshotService.DebugSlotId}\nPhase: {result.Phase}" +
                    $"\nFailureCode: {result.FailureCode}\nRollbackAttempted: {result.RollbackAttempted}" +
                    $"\nRollbackSucceeded: {result.RollbackSucceeded}\nFailure: {result.Failure}");
            }
        }

        private static bool ValidateLoadDebugSlot() => EditorApplication.isPlaying && !EditorApplication.isCompiling;

        public static void RunSnapshotAndSemanticPreflightDiagnostics()
        {
            StartDiagnostic("snapshot");
        }

        private static bool ValidateDiagnostics() => !EditorApplication.isCompiling;

        public static void RunCurrentSlicePersistentRoundTripDiagnostics()
        {
            StartDiagnostic("roundtrip");
        }

        private static bool ValidateRoundTripDiagnostics() => !EditorApplication.isCompiling;

        private static void StartDiagnostic(string mode)
        {
            if (EditorApplication.isPlaying)
            {
                if (mode == "roundtrip") RunRoundTripRuntimeDiagnostics(); else RunRuntimeDiagnostics();
                return;
            }
            if (EditorApplication.isCompiling)
                throw new InvalidOperationException("M37.1 diagnostics cannot start while Unity is compiling.");
            SessionState.SetBool(PendingKey, true);
            SessionState.SetBool(RunningKey, false);
            SessionState.SetString(ResultKey, string.Empty);
            SessionState.SetString(ModeKey, mode);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void ContinueBatchDiagnostic()
        {
            if (!EditorApplication.isPlaying && SessionState.GetBool(RunningKey, false))
            {
                FinishBatchDiagnostic();
                return;
            }
            if (!SessionState.GetBool(PendingKey, false) || !EditorApplication.isPlaying || Time.frameCount < 5 ||
                GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
                return;
            SessionState.SetBool(PendingKey, false);
            SessionState.SetBool(RunningKey, true);
            try
            {
                if (SessionState.GetString(ModeKey, "snapshot") == "roundtrip")
                    RunRoundTripRuntimeDiagnostics();
                else
                    RunRuntimeDiagnostics();
                SessionState.SetString(ResultKey, "PASS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetString(ResultKey, exception.Message);
            }
            EditorApplication.ExitPlaymode();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(RunningKey, false))
                return;
            FinishBatchDiagnostic();
        }

        private static void FinishBatchDiagnostic()
        {
            string result = SessionState.GetString(ResultKey, "Diagnostic did not produce a result.");
            SessionState.EraseBool(PendingKey);
            SessionState.EraseBool(RunningKey);
            SessionState.EraseString(ResultKey);
            SessionState.EraseString(ModeKey);
            bool cleanScene = !EditorSceneManager.GetActiveScene().isDirty;
            if (!cleanScene)
                Debug.LogError("M37.1 diagnostics left SampleScene dirty; the scene was not saved.");
            if (Application.isBatchMode)
                EditorApplication.Exit(result == "PASS" && cleanScene ? 0 : 1);
        }

        private static void RunRuntimeDiagnostics()
        {
            var errors = new List<string>();
            string root = Path.Combine(Path.GetTempPath(), "OldScars_M37_1_" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new PersistenceFileStore(root);
                CurrentSliceSaveResult save = CurrentSliceSnapshotService.Save("snapshot_round_trip", store);
                Require(save.Success, "real capture and temporary M37.0 write", save.Failure, errors);
                if (!save.Success) throw new InvalidOperationException(string.Join("\n", errors));
                CurrentSliceSaveData snapshot = save.Snapshot;
                Require(CurrentSliceSnapshotService.Validate(snapshot).Success, "captured semantic preflight", null, errors);

                CurrentSliceResult read = CurrentSliceSnapshotService.Read("snapshot_round_trip", store);
                Require(read.Success, "read and post-read preflight", read.Failure, errors);
                if (read.Success)
                {
                    CurrentSliceComparisonResult comparison = CurrentSliceSnapshotService.Compare(snapshot, read.Snapshot);
                    Require(comparison.Equivalent, "canonical save/read comparison", comparison.Difference, errors);
                }

                CurrentSliceSaveData legacyContentIds = Clone(snapshot);
                foreach (ItemState item in legacyContentIds.items ?? Array.Empty<ItemState>())
                    if (item != null) item.definitionId = LegacyCoreId(item.definitionId);
                foreach (EquipmentState equipment in legacyContentIds.equipment ?? Array.Empty<EquipmentState>())
                {
                    if (equipment == null) continue;
                    equipment.layoutId = LegacyCoreId(equipment.layoutId);
                    foreach (EquippedItemState item in equipment.items ?? Array.Empty<EquippedItemState>())
                    {
                        if (item?.slots == null) continue;
                        for (int slotIndex = 0; slotIndex < item.slots.Length; slotIndex++)
                            item.slots[slotIndex] = LegacyCoreId(item.slots[slotIndex]);
                    }
                }
                CurrentSliceResult legacyRead = CurrentSliceSnapshotService.FromPayload(
                    CurrentSliceSnapshotService.ToPayload(legacyContentIds));
                Require(legacyRead.Success, "schema-v1 legacy Core Content ID migration", legacyRead.Failure, errors);
                if (legacyRead.Success)
                {
                    CurrentSliceComparisonResult migrated = CurrentSliceSnapshotService.Compare(snapshot, legacyRead.Snapshot);
                    Require(migrated.Equivalent, "legacy save references become canonical in memory", migrated.Difference, errors);
                }

                CurrentSliceSaveData withinPoseTolerance = Clone(snapshot);
                withinPoseTolerance.player.pose.position.x += 0.00005f;
                CurrentSliceComparisonResult tolerantComparison = CurrentSliceSnapshotService.Compare(snapshot, withinPoseTolerance);
                Require(tolerantComparison.Equivalent, "pose comparison within tolerance", tolerantComparison.Difference, errors);

                CurrentSliceSaveData outsidePoseTolerance = Clone(snapshot);
                outsidePoseTolerance.player.pose.position.x += 0.001f;
                Require(!CurrentSliceSnapshotService.Compare(snapshot, outsidePoseTolerance).Equivalent,
                    "pose comparison outside tolerance", null, errors);

                ExpectInvalid(snapshot, "duplicate InstanceId", copy =>
                    copy.items = copy.items.Concat(new[] { Clone(copy.items[0]) }).ToArray(), errors);
                ExpectInvalid(snapshot, "dangling item reference", copy =>
                    copy.storages[0].entries = copy.storages[0].entries.Concat(new[]
                    {
                        new StorageEntryState { instanceId = "item_ffffffffffffffffffffffffffffffff", quantity = 1 }
                    }).ToArray(), errors);
                ExpectInvalid(snapshot, "invalid quantity", copy => FirstEntry(copy).quantity = 0, errors);
                ExpectInvalid(snapshot, "invalid placement", copy => FirstPlacedEntry(copy).placement.x = -1, errors);
                ExpectInvalid(snapshot, "invalid Equipment reference", copy =>
                {
                    EquipmentState equipment = copy.equipment[0];
                    equipment.items = new[] { new EquippedItemState
                        { instanceId = "item_eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee", slots = new[] { "hand_right" } } };
                }, errors);
                ExpectInvalid(snapshot, "illegal item-owned relationship", copy =>
                {
                    ItemState ordinary = copy.items.First(item =>
                        string.IsNullOrWhiteSpace(GameDataManager.Instance.Database.GetItem(item.definitionId).owned_storage_profile_id));
                    copy.storages = copy.storages.Concat(new[] { new StorageState
                    {
                        storageId = "item_owned:" + ordinary.instanceId, kind = "item_owned", ownerId = ordinary.instanceId
                    }}).ToArray();
                }, errors);
                ExpectInvalid(snapshot, "duplicate world representation", copy =>
                {
                    WorldItemState world = copy.worldItems.First(item => item.present);
                    copy.worldItems = copy.worldItems.Concat(new[] { new WorldItemState
                    {
                        instanceId = world.instanceId, kind = "runtime", present = true,
                        quantity = world.quantity, pose = world.pose
                    }}).ToArray();
                }, errors);

                CurrentSliceSaveData emptyContainer = Clone(snapshot);
                MakeContainerEmpty(emptyContainer);
                CurrentSliceValidationResult emptyValidation = CurrentSliceSnapshotService.Validate(emptyContainer);
                Require(emptyValidation.Success, "explicit empty container preflight", emptyValidation.Failure, errors);
                if (emptyValidation.Success)
                {
                    var emptyWrite = store.Write("empty_container", CurrentSliceSnapshotService.ToPayload(emptyContainer));
                    CurrentSliceResult emptyRead = CurrentSliceSnapshotService.Read("empty_container", store);
                    Require(emptyWrite.Success && emptyRead.Success, "empty container save/read", emptyRead.Failure ?? emptyWrite.Failure, errors);
                    if (emptyRead.Success)
                        Require(CurrentSliceSnapshotService.Compare(emptyContainer, emptyRead.Snapshot).Equivalent,
                            "empty container remains represented", null, errors);
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(root)) Directory.Delete(root, true);
                    if (Directory.Exists(root)) errors.Add("Temporary diagnostic root still exists after cleanup.");
                }
                catch (Exception exception) { errors.Add("Temporary diagnostic cleanup failed: " + exception.Message); }
            }

            if (errors.Count > 0)
            {
                string failure = "M37.1 Snapshot & Semantic Preflight Diagnostics: FAIL\n- " + string.Join("\n- ", errors);
                Debug.LogError(failure);
                throw new InvalidOperationException(failure);
            }
            Debug.Log("M37.1 Snapshot & Semantic Preflight Diagnostics: PASS");
        }

        private static void RunRoundTripRuntimeDiagnostics()
        {
            var errors = new List<string>();
            string root = Path.Combine(Path.GetTempPath(), "OldScars_M37_1_RoundTrip_" + Guid.NewGuid().ToString("N"));
            var store = new PersistenceFileStore(root);
            CurrentSliceSaveData initial = null;
            bool initialWritten = false;
            try
            {
                CurrentSliceResult initialCapture = CurrentSliceSnapshotService.Capture();
                Require(initialCapture.Success, "initial cleanup snapshot", initialCapture.Failure, errors);
                if (!initialCapture.Success) throw new InvalidOperationException(string.Join("\n", errors));
                initial = initialCapture.Snapshot;
                PersistenceWriteResult initialWrite = store.Write("diagnostic_initial", CurrentSliceSnapshotService.ToPayload(initial));
                initialWritten = initialWrite.Success;
                Require(initialWritten, "initial cleanup snapshot write", initialWrite.Failure, errors);

                Require(CurrentSliceRoundTripDiagnosticScenario.TryPrepareStateA(out string setupFailure),
                    "runtime State A setup", setupFailure, errors);
                if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));

                CurrentSliceResult stateAResult = CurrentSliceSnapshotService.Capture();
                Require(stateAResult.Success, "State A capture", stateAResult.Failure, errors);
                if (!stateAResult.Success) throw new InvalidOperationException(string.Join("\n", errors));
                CurrentSliceSaveData stateA = stateAResult.Snapshot;
                Require(string.IsNullOrWhiteSpace(CurrentSliceRoundTripDiagnosticScenario.ValidateCoverage(stateA)),
                    "State A slice coverage", CurrentSliceRoundTripDiagnosticScenario.ValidateCoverage(stateA), errors);
                PersistenceWriteResult stateAWrite = store.Write("state_a", CurrentSliceSnapshotService.ToPayload(stateA));
                Require(stateAWrite.Success, "State A M37.0 write", stateAWrite.Failure, errors);

                Require(CurrentSliceRoundTripDiagnosticScenario.TryMutateStateB(out string mutationFailure),
                    "runtime State B mutation", mutationFailure, errors);
                CurrentSliceResult stateBResult = CurrentSliceSnapshotService.Capture();
                Require(stateBResult.Success, "State B capture", stateBResult.Failure, errors);
                if (stateBResult.Success)
                    Require(!CurrentSliceSnapshotService.Compare(stateA, stateBResult.Snapshot).Equivalent,
                        "State B differs from State A", null, errors);

                CurrentSliceLoadResult load = CurrentSliceLoadService.Load("state_a", store);
                Require(load.Success, "transactional State A load", load.Failure, errors);
                CurrentSliceResult stateCResult = CurrentSliceSnapshotService.Capture();
                Require(stateCResult.Success, "State C capture", stateCResult.Failure, errors);
                if (stateCResult.Success)
                    Require(CurrentSliceSnapshotService.Compare(stateA, stateCResult.Snapshot).Equivalent,
                        "canonical A/C equivalence", CurrentSliceSnapshotService.Compare(stateA, stateCResult.Snapshot).Difference, errors);

                CurrentSliceResult beforeFault = CurrentSliceSnapshotService.Capture();
                Require(beforeFault.Success, "pre-fault snapshot", beforeFault.Failure, errors);
                CurrentSliceLoadService.DiagnosticInjectFailureAfterStorageRestore = true;
                CurrentSliceLoadResult fault = CurrentSliceLoadService.Load("state_a", store);
                Require(fault.FailureCode == CurrentSliceLoadFailureCode.ApplyFailed &&
                        fault.RollbackAttempted && fault.RollbackSucceeded,
                    "faulted apply reports successful rollback", fault.Failure, errors);
                CurrentSliceResult afterFault = CurrentSliceSnapshotService.Capture();
                Require(afterFault.Success, "post-rollback snapshot", afterFault.Failure, errors);
                if (beforeFault.Success && afterFault.Success)
                    Require(CurrentSliceSnapshotService.Compare(beforeFault.Snapshot, afterFault.Snapshot).Equivalent,
                        "rollback restores pre-load runtime", CurrentSliceSnapshotService.Compare(beforeFault.Snapshot, afterFault.Snapshot).Difference, errors);
            }
            catch (Exception exception)
            {
                if (errors.Count == 0) errors.Add(exception.Message);
            }
            finally
            {
                CurrentSliceLoadService.DiagnosticInjectFailureAfterStorageRestore = false;
                if (!CurrentSliceRoundTripDiagnosticScenario.TryRestoreDiagnosticCorpseAlive(out string corpseCleanupFailure))
                    errors.Add("Diagnostic corpse cleanup failed: " + corpseCleanupFailure);
                if (initialWritten && initial != null)
                {
                    CurrentSliceLoadResult cleanupLoad = CurrentSliceLoadService.Load("diagnostic_initial", store);
                    if (!cleanupLoad.Success) errors.Add("Initial-state cleanup load failed: " + cleanupLoad.Failure);
                    else
                    {
                        CurrentSliceResult cleanupCapture = CurrentSliceSnapshotService.Capture();
                        if (!cleanupCapture.Success) errors.Add("Initial-state cleanup capture failed: " + cleanupCapture.Failure);
                        else
                        {
                            CurrentSliceComparisonResult cleanupComparison = CurrentSliceSnapshotService.Compare(initial, cleanupCapture.Snapshot);
                            if (!cleanupComparison.Equivalent) errors.Add("Initial-state cleanup differs: " + cleanupComparison.Difference);
                        }
                    }
                }
                try
                {
                    if (Directory.Exists(root)) Directory.Delete(root, true);
                    if (Directory.Exists(root)) errors.Add("Temporary round-trip diagnostic root still exists after cleanup.");
                }
                catch (Exception exception) { errors.Add("Temporary round-trip cleanup failed: " + exception.Message); }
            }

            if (errors.Count > 0)
            {
                string failure = "M37.1 Current Slice Persistent Round-Trip Diagnostics: FAIL\n- " + string.Join("\n- ", errors);
                Debug.LogError(failure);
                throw new InvalidOperationException(failure);
            }
            Debug.Log("M37.1 Current Slice Persistent Round-Trip Diagnostics: PASS");
        }

        private static void ExpectInvalid(CurrentSliceSaveData source, string label, Action<CurrentSliceSaveData> mutation, List<string> errors)
        {
            CurrentSliceSaveData copy = Clone(source);
            try { mutation(copy); }
            catch (Exception exception) { errors.Add(label + " setup failed: " + exception.Message); return; }
            if (CurrentSliceSnapshotService.Validate(copy).Success) errors.Add(label + " was not rejected.");
        }

        private static CurrentSliceSaveData Clone(CurrentSliceSaveData source) =>
            CurrentSliceSnapshotService.ToPayload(source).ToObject<CurrentSliceSaveData>();
        private static T Clone<T>(T source) => JToken.FromObject(source).ToObject<T>();
        private static string LegacyCoreId(string value)
        {
            return ContentId.TryParse(value, out ContentId contentId, out _) &&
                   contentId.Namespace == ContentId.CoreNamespace
                ? contentId.LocalId
                : value;
        }
        private static StorageEntryState FirstEntry(CurrentSliceSaveData data) => data.storages.SelectMany(storage => storage.entries).First();
        private static StorageEntryState FirstPlacedEntry(CurrentSliceSaveData data) => data.storages.SelectMany(storage => storage.entries).First(entry => entry.placement != null);

        private static void MakeContainerEmpty(CurrentSliceSaveData data)
        {
            ContainerState container = data.containers.First(state => data.storages.First(storage => storage.storageId == state.storageId).entries.Length > 0);
            StorageState storage = data.storages.First(state => state.storageId == container.storageId);
            var removed = new HashSet<string>(storage.entries.Select(entry => entry.instanceId));
            storage.entries = Array.Empty<StorageEntryState>();
            bool changed;
            do
            {
                changed = false;
                foreach (StorageState owned in data.storages.Where(state => state.kind == "item_owned" && removed.Contains(state.ownerId)).ToArray())
                {
                    foreach (StorageEntryState entry in owned.entries) removed.Add(entry.instanceId);
                    data.storages = data.storages.Where(state => !ReferenceEquals(state, owned)).ToArray();
                    changed = true;
                }
            } while (changed);
            data.items = data.items.Where(item => !removed.Contains(item.instanceId)).ToArray();
        }

        private static void Require(bool condition, string label, string detail, List<string> errors)
        {
            if (!condition) errors.Add(label + (string.IsNullOrWhiteSpace(detail) ? " failed." : ": " + detail));
        }
    }
}
