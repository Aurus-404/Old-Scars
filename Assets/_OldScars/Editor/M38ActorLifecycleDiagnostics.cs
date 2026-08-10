using System;
using System.IO;
using System.Linq;
using OldScars.Core;
using OldScars.Core.Actors;
using OldScars.Core.Data;
using OldScars.Core.Identity;
using OldScars.Core.Interactions;
using OldScars.Core.Items;
using OldScars.Core.Persistence;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OldScars.Editor
{
    [InitializeOnLoad]
    public static class M38ActorLifecycleDiagnostics
    {
        private const string Menu = "Old Scars/Diagnostics/Actors/Run M38.0 Lifecycle";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string PhaseKey = "OldScars.M38.Lifecycle.Phase";
        private const string RootKey = "OldScars.M38.Lifecycle.Root";
        private const string ErrorKey = "OldScars.M38.Lifecycle.Error";
        private const string AuthoredIdKey = "OldScars.M38.Lifecycle.AuthoredId";
        private const string ProfileIdKey = "OldScars.M38.Lifecycle.ProfileId";
        private const string RuntimeIdKey = "OldScars.M38.Lifecycle.RuntimeId";
        private const string EnterA = "enter_a";
        private const string ExitA = "exit_a";
        private const string EnterB = "enter_b";
        private const string Finish = "finish";
        private const string InitialSlot = "m38_initial";
        private const string AliveSlot = "m38_authored_alive";
        private const string TargetSlot = "m38_dead_and_runtime";

        static M38ActorLifecycleDiagnostics()
        {
            EditorApplication.update += Continue;
        }

        [MenuItem(Menu)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("M38.0 lifecycle diagnostics require idle Edit Mode.");

            ClearSession();
            string root = Path.Combine(Path.GetTempPath(), "OldScars_M38_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            SessionState.SetString(RootKey, root);
            SessionState.SetString(ErrorKey, string.Empty);
            SessionState.SetString(PhaseKey, EnterA);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem(Menu, true)]
        private static bool ValidateRun() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;

        private static void Continue()
        {
            string phase = SessionState.GetString(PhaseKey, string.Empty);
            if (string.IsNullOrWhiteSpace(phase))
                return;

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
            return EditorApplication.isPlaying && Time.frameCount >= 5 &&
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
            var store = Store();
            CurrentSliceSaveData initial = Capture("initial authored bootstrap");
            Write(store, InitialSlot, initial);

            ActorRuntimeIdentity authored = ActorRuntimeRegistry.ActiveRepresentations
                .Where(identity => identity != null && identity.OriginKind == ActorOriginKind.Authored &&
                                   identity.GetComponent<PersistentSceneObjectId>() != null &&
                                   identity.GetComponent<ActorProfileComponent>() != null &&
                                   identity.GetComponent<ActorEquipmentComponent>()?.Entries.Count > 0)
                .OrderBy(identity => identity.ActorInstanceId, StringComparer.Ordinal).FirstOrDefault();
            Require(authored != null, "No equipped authored NPC is available for lifecycle diagnostics.");
            Require(!authored.GetComponent<ActorHealthComponent>().IsDead,
                $"Authored actor '{authored.ActorInstanceId}' did not bootstrap Alive.");

            string actorId = authored.ActorInstanceId;
            string profileId = authored.ActorProfileId;
            Require(ActorRuntimeIdentity.IsValidFormat(actorId), "Authored ActorInstanceId format is invalid.");
            Require(actorId != authored.GetComponent<PersistentSceneObjectId>().PersistentId,
                "ActorInstanceId was conflated with PersistentSceneObjectId.");
            SessionState.SetString(AuthoredIdKey, actorId);
            SessionState.SetString(ProfileIdKey, profileId);

            authored.transform.SetPositionAndRotation(
                authored.transform.position + new Vector3(0.85f, 0f, -0.45f), Quaternion.Euler(0f, 53f, 0f));
            Physics.SyncTransforms();
            CurrentSliceSaveData alive = Capture("authored Alive state");
            ActorState aliveState = Actor(alive, actorId);
            Require(aliveState.lifecycleState == "Alive" && aliveState.actorProfileId == profileId,
                "Authored Alive state lost lifecycle or profile identity.");
            Write(store, AliveSlot, alive);

            ActorHealthComponent health = authored.GetComponent<ActorHealthComponent>();
            health.Kill();
            authored.GetComponent<LootableActorInventoryComponent>()?.RefreshLootableState();
            WorldObjectTags tags = authored.GetComponent<WorldObjectTags>();
            Require(authored.ActorInstanceId == actorId && authored.LifecycleState == ActorLifecycleState.Dead,
                "Death changed ActorInstanceId or failed to commit logical Dead state.");
            Require(tags != null && tags.HasTag(ActorHealthComponent.DeadActorTag) &&
                    tags.HasTag(ActorHealthComponent.LootableActorTag),
                "Dead authored actor did not expose corpse/lootable tags.");

            Vector3 runtimePosition = authored.transform.position + new Vector3(2.5f, 0f, 1.25f);
            Quaternion runtimeRotation = Quaternion.Euler(0f, 127f, 0f);
            Require(ActorSpawnService.TrySpawn(profileId, runtimePosition, runtimeRotation,
                    out ActorRuntimeIdentity runtime, out string spawnError),
                "Runtime actor bootstrap failed: " + spawnError);
            Require(runtime.ActorProfileId == profileId && runtime.OriginKind == ActorOriginKind.Runtime &&
                    runtime.GetComponent<InventoryComponent>() != null &&
                    runtime.GetComponent<ActorEquipmentComponent>() != null,
                "Runtime spawn lacks canonical profile or required storage components.");
            string runtimeId = runtime.ActorInstanceId;
            SessionState.SetString(RuntimeIdKey, runtimeId);
            Require(!ActorSpawnService.TrySpawn(profileId, runtimePosition, runtimeRotation, runtimeId,
                    ActorSpawnInitialization.PersistenceRestore, out _, out string duplicateError) &&
                    duplicateError.Contains("active representation"),
                "Duplicate ActorInstanceId was not rejected explicitly.");

            CurrentSliceSaveData target = Capture("dead authored plus runtime actor state");
            ActorState deadState = Actor(target, actorId);
            ActorState runtimeState = Actor(target, runtimeId);
            Require(deadState.lifecycleState == "Dead" && deadState.currentHealth == 0f,
                "Dead authored actor state is inconsistent.");
            Require(runtimeState.originKind == "Runtime" && runtimeState.lifecycleState == "Alive",
                "Runtime actor state is not persistable as an Alive runtime origin.");
            Require(!string.IsNullOrWhiteSpace(deadState.inventoryStorageId) &&
                    !string.IsNullOrWhiteSpace(deadState.equipmentStorageId),
                "Dead actor did not retain inventory/equipment storage references.");
            Write(store, TargetSlot, target);

            Require(ActorSpawnService.TryRemoveRuntimeRepresentationForRestore(runtimeId, out string removeError),
                "Runtime representation removal failed: " + removeError);
            Require(!ActorRuntimeRegistry.TryGet(runtimeId, out _),
                "Runtime representation removal left ActorInstanceId registered.");
        }

        private static void RunSessionB()
        {
            var store = Store();
            string authoredId = SessionState.GetString(AuthoredIdKey, string.Empty);
            string profileId = SessionState.GetString(ProfileIdKey, string.Empty);
            string runtimeId = SessionState.GetString(RuntimeIdKey, string.Empty);
            Require(ActorRuntimeRegistry.TryGet(authoredId, out ActorRuntimeIdentity bootstrapActor),
                "Fresh session did not recreate the authored actor identity.");
            Require(bootstrapActor.ActorProfileId == profileId &&
                    bootstrapActor.LifecycleState == ActorLifecycleState.Alive &&
                    !bootstrapActor.GetComponent<ActorHealthComponent>().IsDead,
                "Fresh authored bootstrap was not Alive before load.");
            Require(!ActorRuntimeRegistry.TryGet(runtimeId, out _),
                "Fresh session unexpectedly retained the prior runtime representation.");

            CurrentSliceSaveData target = Read(store, TargetSlot);
            CurrentSliceLoadResult deadLoad = CurrentSliceLoadService.Load(TargetSlot, store);
            Require(deadLoad.Success, "Fresh-session dead/runtime load failed: " + deadLoad.Failure);
            Require(ActorRuntimeRegistry.TryGet(authoredId, out ActorRuntimeIdentity deadActor) &&
                    deadActor.LifecycleState == ActorLifecycleState.Dead &&
                    deadActor.GetComponent<ActorHealthComponent>().CurrentHealth == 0f,
                "Authored actor did not replace fresh Alive bootstrap with persisted Dead state.");
            WorldObjectTags deadTags = deadActor.GetComponent<WorldObjectTags>();
            Require(deadTags.HasTag(ActorHealthComponent.DeadActorTag) &&
                    deadTags.HasTag(ActorHealthComponent.LootableActorTag),
                "Restored authored corpse is not dead/lootable.");
            Require(ActorRuntimeRegistry.TryGet(runtimeId, out ActorRuntimeIdentity restoredRuntime) &&
                    restoredRuntime.ActorProfileId == profileId && restoredRuntime.OriginKind == ActorOriginKind.Runtime,
                "Runtime actor was not recreated with exact identity/profile.");
            AssertPose(Actor(target, runtimeId), restoredRuntime.transform, "restored runtime actor");
            AssertEquivalent(target, Capture("post fresh-session target load"), "dead/runtime fresh-session round-trip");

            CurrentSliceSaveData alive = Read(store, AliveSlot);
            CurrentSliceLoadResult aliveLoad = CurrentSliceLoadService.Load(AliveSlot, store);
            Require(aliveLoad.Success, "Authored Alive load failed: " + aliveLoad.Failure);
            Require(ActorRuntimeRegistry.TryGet(authoredId, out ActorRuntimeIdentity aliveActor) &&
                    aliveActor.LifecycleState == ActorLifecycleState.Alive &&
                    aliveActor.ActorProfileId == profileId,
                "Authored actor did not restore Alive with the same identity/profile.");
            Require(!ActorRuntimeRegistry.TryGet(runtimeId, out _),
                "Selective Alive load failed to remove the target-only runtime representation.");
            AssertPose(Actor(alive, authoredId), aliveActor.transform, "restored authored Alive actor");
            AssertEquivalent(alive, Capture("post Alive load"), "authored Alive round-trip and M37 regression");

            CurrentSliceSaveData beforeFault = Capture("pre actor-reconciliation fault");
            CurrentSliceLoadService.DiagnosticInjectFailureAfterActorReconciliation = true;
            CurrentSliceLoadResult fault = CurrentSliceLoadService.Load(TargetSlot, store);
            Require(fault.FailureCode == CurrentSliceLoadFailureCode.ApplyFailed &&
                    fault.RollbackAttempted && fault.RollbackSucceeded,
                "Actor-reconciliation fault did not report successful rollback: " + fault.Failure);
            AssertEquivalent(beforeFault, Capture("post actor-reconciliation rollback"),
                "actor existence/lifecycle/storage rollback");
            Require(!ActorRuntimeRegistry.TryGet(runtimeId, out _),
                "Rollback left the fault-spawned runtime actor registered.");

            CurrentSliceSaveData initial = Read(store, InitialSlot);
            CurrentSliceLoadResult cleanup = CurrentSliceLoadService.Load(InitialSlot, store);
            Require(cleanup.Success, "Initial-state cleanup load failed: " + cleanup.Failure);
            AssertEquivalent(initial, Capture("initial cleanup"), "initial cleanup and M37 state preservation");
            CurrentSliceLoadService.DiagnosticInjectFailureAfterActorReconciliation = false;
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

        private static void Write(PersistenceFileStore store, string slot, CurrentSliceSaveData snapshot)
        {
            PersistenceWriteResult result = store.Write(slot, CurrentSliceSnapshotService.ToPayload(snapshot));
            Require(result.Success, $"Slot '{slot}' write failed: {result.Failure}");
        }

        private static CurrentSliceSaveData Read(PersistenceFileStore store, string slot)
        {
            CurrentSliceResult result = CurrentSliceSnapshotService.Read(slot, store);
            Require(result.Success, $"Slot '{slot}' read/preflight failed: {result.Failure}");
            return result.Snapshot;
        }

        private static ActorState Actor(CurrentSliceSaveData snapshot, string actorId)
        {
            ActorState state = (snapshot.actors ?? Array.Empty<ActorState>())
                .SingleOrDefault(actor => actor != null && actor.actorInstanceId == actorId);
            Require(state != null, $"Snapshot does not contain actor '{actorId}'.");
            return state;
        }

        private static void AssertEquivalent(CurrentSliceSaveData expected, CurrentSliceSaveData actual, string label)
        {
            CurrentSliceComparisonResult comparison = CurrentSliceSnapshotService.Compare(expected, actual);
            Require(comparison.Equivalent, label + " differs: " + comparison.Difference);
        }

        private static void AssertPose(ActorState state, Transform transform, string label)
        {
            Vector3 expectedPosition = new Vector3(state.pose.position.x, state.pose.position.y, state.pose.position.z);
            Quaternion expectedRotation = new Quaternion(
                state.pose.rotation.x, state.pose.rotation.y, state.pose.rotation.z, state.pose.rotation.w);
            Require(Vector3.Distance(expectedPosition, transform.position) <= 0.0001f &&
                    Quaternion.Angle(expectedRotation, transform.rotation) <= 0.01f,
                label + " pose differs from persisted state.");
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
                Debug.Log("M38.0 Actor Runtime & Lifecycle Diagnostics: PASS");
            else
                Debug.LogError("M38.0 Actor Runtime & Lifecycle Diagnostics: FAIL\n- " + failure);
            if (Application.isBatchMode)
                EditorApplication.Exit(success ? 0 : 1);
        }

        private static string Append(string current, string value)
        {
            return string.IsNullOrWhiteSpace(current) ? value : current + "\n- " + value;
        }

        private static void ClearSession()
        {
            SessionState.EraseString(PhaseKey);
            SessionState.EraseString(RootKey);
            SessionState.EraseString(ErrorKey);
            SessionState.EraseString(AuthoredIdKey);
            SessionState.EraseString(ProfileIdKey);
            SessionState.EraseString(RuntimeIdKey);
        }
    }
}
