using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using OldScars.Core;
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
        private const string DiagnosticMenu = "Old Scars/Diagnostics/M37.1/Run Snapshot & Semantic Preflight";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string PendingKey = "OldScars.M37.1.SnapshotDiagnostics.Pending";
        private const string RunningKey = "OldScars.M37.1.SnapshotDiagnostics.Running";
        private const string ResultKey = "OldScars.M37.1.SnapshotDiagnostics.Result";

        static M37CurrentSliceSnapshotTools()
        {
            EditorApplication.update += ContinueBatchDiagnostic;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem(SaveMenu)]
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

        [MenuItem(SaveMenu, true)]
        private static bool ValidateSaveDebugSlot() => EditorApplication.isPlaying && !EditorApplication.isCompiling;

        [MenuItem(DiagnosticMenu)]
        public static void RunSnapshotAndSemanticPreflightDiagnostics()
        {
            if (EditorApplication.isPlaying)
            {
                RunRuntimeDiagnostics();
                return;
            }
            if (EditorApplication.isCompiling)
                throw new InvalidOperationException("M37.1 diagnostics cannot start while Unity is compiling.");
            SessionState.SetBool(PendingKey, true);
            SessionState.SetBool(RunningKey, false);
            SessionState.SetString(ResultKey, string.Empty);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem(DiagnosticMenu, true)]
        private static bool ValidateDiagnostics() => !EditorApplication.isCompiling;

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
