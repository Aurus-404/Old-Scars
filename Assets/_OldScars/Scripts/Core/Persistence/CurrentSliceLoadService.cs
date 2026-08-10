using System;
using System.Collections.Generic;
using System.Linq;
using OldScars.Core.Actors;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Identity;
using OldScars.Core.Interactions;
using OldScars.Core.Items;
using UnityEngine;

namespace OldScars.Core.Persistence
{
    public enum CurrentSliceLoadFailureCode
    {
        Success,
        ReadFailed,
        SemanticPreflightFailed,
        SceneResolutionFailed,
        ApplyFailed,
        RollbackFailed
    }

    public sealed class CurrentSliceLoadResult
    {
        internal CurrentSliceLoadResult(
            CurrentSliceLoadFailureCode failureCode,
            string phase,
            string failure,
            bool mutationStarted,
            bool rollbackAttempted,
            bool rollbackSucceeded)
        {
            FailureCode = failureCode;
            Phase = phase;
            Failure = failure;
            MutationStarted = mutationStarted;
            RollbackAttempted = rollbackAttempted;
            RollbackSucceeded = rollbackSucceeded;
        }

        public bool Success => FailureCode == CurrentSliceLoadFailureCode.Success;
        public CurrentSliceLoadFailureCode FailureCode { get; }
        public string Phase { get; }
        public string Failure { get; }
        public bool MutationStarted { get; }
        public bool RollbackAttempted { get; }
        public bool RollbackSucceeded { get; }
    }

    public static class CurrentSliceLoadService
    {
        private const string InventoryKind = "inventory";
        private const string EquipmentKind = "equipment";
        private const string ContainerKind = "container";
        private const string ItemOwnedKind = "item_owned";
        private const string AuthoredWorldKind = "authored";
        private const string RuntimeWorldKind = "runtime";
        private static readonly string[] ContainerTags =
        {
            "opened_container", "sealed_container", "unsearched_container",
            "storage_accessible", "lootable_container", "looted_container"
        };
        private static readonly string[] DoorStates = { "opened_door", "closed_door", "locked_door" };

#if UNITY_EDITOR
        public static bool DiagnosticInjectFailureAfterStorageRestore { get; set; }
        public static bool DiagnosticInjectFailureAfterActorReconciliation { get; set; }
#endif

        public static CurrentSliceLoadResult Load(string slotId, PersistenceFileStore store = null)
        {
            if (!Application.isPlaying)
                return Failure(CurrentSliceLoadFailureCode.SceneResolutionFailed, "SceneResolution", "Load requires Play Mode.");

            PersistenceLoadResult read = (store ?? new PersistenceFileStore()).Read(slotId);
            if (!read.Success)
                return Failure(CurrentSliceLoadFailureCode.ReadFailed, "Read", $"{read.FailureCode}: {read.Failure}");

            CurrentSliceResult preflight = CurrentSliceSnapshotService.FromPayload(read.Payload);
            if (!preflight.Success)
                return Failure(CurrentSliceLoadFailureCode.SemanticPreflightFailed, "SemanticPreflight", preflight.Failure);

            if (!ResolvedScene.TryCreate(preflight.Snapshot, true, out ResolvedScene targetScene, out string resolutionError))
                return Failure(CurrentSliceLoadFailureCode.SceneResolutionFailed, "SceneResolution", resolutionError);

            CurrentSliceResult rollbackCapture = CurrentSliceSnapshotService.Capture();
            if (!rollbackCapture.Success)
                return Failure(CurrentSliceLoadFailureCode.SceneResolutionFailed, "RollbackCapture", rollbackCapture.Failure);
            if (!ResolvedScene.TryCreate(rollbackCapture.Snapshot, false, out ResolvedScene rollbackScene, out resolutionError))
                return Failure(CurrentSliceLoadFailureCode.SceneResolutionFailed, "RollbackResolution", resolutionError);

            var rollbackIds = new HashSet<string>(Items(rollbackCapture.Snapshot.items).Select(item => item.instanceId), StringComparer.Ordinal);
            foreach (ItemState item in Items(preflight.Snapshot.items))
            {
                if (ItemInstanceIdRegistry.Instance.IsActive(item.instanceId) && !rollbackIds.Contains(item.instanceId))
                    return Failure(CurrentSliceLoadFailureCode.SceneResolutionFailed, "IdentityCollision",
                        $"Target item identity '{item.instanceId}' is active outside the captured Current Slice.");
            }

            var rollbackActorIds = new HashSet<string>(Items(rollbackCapture.Snapshot.actors)
                .Where(actor => actor != null).Select(actor => actor.actorInstanceId), StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(rollbackCapture.Snapshot.player?.actorInstanceId))
                rollbackActorIds.Add(rollbackCapture.Snapshot.player.actorInstanceId);
            foreach (ActorState actor in Items(preflight.Snapshot.actors))
            {
                if (actor != null && ActorRuntimeRegistry.TryGet(actor.actorInstanceId, out _) &&
                    !rollbackActorIds.Contains(actor.actorInstanceId))
                    return Failure(CurrentSliceLoadFailureCode.SceneResolutionFailed, "ActorIdentityCollision",
                        $"Target ActorInstanceId '{actor.actorInstanceId}' is active outside the captured Current Slice.");
            }

            var sliceActorIds = new HashSet<string>(StringComparer.Ordinal) { preflight.Snapshot.player.persistentId };
            sliceActorIds.Add(rollbackCapture.Snapshot.player.persistentId);
            if (!string.IsNullOrWhiteSpace(preflight.Snapshot.player.actorInstanceId))
                sliceActorIds.Add(preflight.Snapshot.player.actorInstanceId);
            if (!string.IsNullOrWhiteSpace(rollbackCapture.Snapshot.player.actorInstanceId))
                sliceActorIds.Add(rollbackCapture.Snapshot.player.actorInstanceId);
            foreach (ActorState actor in Items(preflight.Snapshot.actors)) sliceActorIds.Add(actor.actorInstanceId);
            foreach (ActorState actor in Items(rollbackCapture.Snapshot.actors)) sliceActorIds.Add(actor.actorInstanceId);
            foreach (CorpseState corpse in Items(preflight.Snapshot.corpses)) sliceActorIds.Add(corpse.persistentId);
            foreach (CorpseState corpse in Items(rollbackCapture.Snapshot.corpses)) sliceActorIds.Add(corpse.persistentId);
            if (!ExternalBindings.TryCapture(sliceActorIds, out ExternalBindings external, out resolutionError))
                return Failure(CurrentSliceLoadFailureCode.SceneResolutionFailed, "ExternalBindingCapture", resolutionError);

            var teardownIds = new HashSet<string>(rollbackIds, StringComparer.Ordinal);
            teardownIds.UnionWith(Items(preflight.Snapshot.items).Select(item => item.instanceId));
            bool injectActorFailure = ShouldInjectActorDiagnosticFailure();
            bool injectStorageFailure = ShouldInjectStorageDiagnosticFailure();
            bool mutationStarted;
            if (TryApplyCore(preflight.Snapshot, targetScene, rollbackScene, teardownIds, external,
                    injectActorFailure, injectStorageFailure, out mutationStarted, out string applyFailure))
            {
                CurrentSliceLoadResult success = new CurrentSliceLoadResult(
                    CurrentSliceLoadFailureCode.Success, "Complete", null, mutationStarted, false, false);
                Log(slotId, success, preflight.Snapshot);
                return success;
            }

            if (!mutationStarted)
            {
                CurrentSliceLoadResult rejected = Failure(CurrentSliceLoadFailureCode.ApplyFailed, "Apply", applyFailure);
                Log(slotId, rejected, null);
                return rejected;
            }

            bool rollbackMutationStarted;
            bool rollbackSucceeded = TryApplyCore(
                rollbackCapture.Snapshot,
                rollbackScene,
                targetScene,
                teardownIds,
                external,
                false,
                false,
                out rollbackMutationStarted,
                out string rollbackFailure);
            CurrentSliceLoadResult result = rollbackSucceeded
                ? new CurrentSliceLoadResult(CurrentSliceLoadFailureCode.ApplyFailed, "Apply", applyFailure, true, true, true)
                : new CurrentSliceLoadResult(CurrentSliceLoadFailureCode.RollbackFailed, "Rollback",
                    $"Apply failed: {applyFailure}\nRollback failed: {rollbackFailure}", true, true, false);
            Log(slotId, result, null);
            return result;
        }

        private static bool TryApplyCore(
            CurrentSliceSaveData snapshot,
            ResolvedScene scene,
            ResolvedScene alternateScene,
            HashSet<string> teardownIds,
            ExternalBindings external,
            bool injectActorFailure,
            bool injectStorageFailure,
            out bool mutationStarted,
            out string failure)
        {
            mutationStarted = false;
            failure = null;
            try
            {
                GameDatabase database = GameDataManager.Instance?.Database;
                if (database == null)
                    throw new InvalidOperationException("GameDatabase is unavailable during apply.");

                mutationStarted = true;
                TeardownSlice(snapshot, scene, alternateScene, teardownIds);
                ReconcileActorRepresentations(snapshot, scene);
                if (injectActorFailure)
                    throw new InvalidOperationException("Injected diagnostic failure after actor reconciliation.");
                if (!ResolvedScene.TryCreate(snapshot, false, out scene, out string resolutionError))
                    throw new InvalidOperationException("Post-reconciliation scene resolution failed: " + resolutionError);
                Dictionary<string, ItemInstance> items = RehydrateItems(snapshot, database);
                RestoreOwnedStorages(snapshot, items, database);
                RestoreEquipmentLayouts(snapshot, scene);
                RestoreRootStorages(snapshot, scene, items);
                BindStorageOwnership(snapshot, scene, items);

                if (injectStorageFailure)
                    throw new InvalidOperationException("Injected diagnostic failure after storage restore.");

                RestoreWorld(snapshot, scene, items);
                RestoreRuntimeState(snapshot, scene);
                ValidateOwnership(scene, external);
                RestorePlayerPose(snapshot.player.pose, scene.Player.transform);

                CurrentSliceResult captured = CurrentSliceSnapshotService.Capture();
                if (!captured.Success)
                    throw new InvalidOperationException($"Post-apply capture failed: {captured.Failure}");
                if (Items(snapshot.actors).Length > 0)
                {
                    CurrentSliceComparisonResult comparison = CurrentSliceSnapshotService.Compare(snapshot, captured.Snapshot);
                    if (!comparison.Equivalent)
                        throw new InvalidOperationException($"Post-apply snapshot differs: {comparison.Difference}");
                }
                return true;
            }
            catch (Exception exception)
            {
                failure = $"{exception.GetType().Name}: {exception.Message}";
                return false;
            }
        }

        private static void TeardownSlice(
            CurrentSliceSaveData snapshot,
            ResolvedScene first,
            ResolvedScene second,
            HashSet<string> ids)
        {
            var retireIds = new HashSet<string>(ids, StringComparer.Ordinal);
            string[] actorOwners = Items(snapshot.storages)
                .Where(state => state != null && (state.kind == InventoryKind || state.kind == EquipmentKind))
                .Select(state => state.ownerId).Distinct(StringComparer.Ordinal).ToArray();
            InventoryComponent[] inventories = actorOwners.Select(owner =>
                    first.Actors.TryGetValue(owner, out ActorRuntime primary) ? primary.Inventory :
                    second.Actors.TryGetValue(owner, out ActorRuntime alternate) ? alternate.Inventory : null)
                .Where(value => value != null).Distinct().ToArray();
            ActorEquipmentComponent[] equipmentComponents = actorOwners.Select(owner =>
                    first.Actors.TryGetValue(owner, out ActorRuntime primary) ? primary.Equipment :
                    second.Actors.TryGetValue(owner, out ActorRuntime alternate) ? alternate.Equipment : null)
                .Where(value => value != null).Distinct().ToArray();
            ContainerLootComponent[] containers = first.Containers.Values.Concat(second.Containers.Values)
                .Where(value => value != null).Distinct().ToArray();
            foreach (InventoryComponent inventory in inventories) CollectEntryIds(inventory.Entries, retireIds);
            foreach (ActorEquipmentComponent equipment in equipmentComponents) CollectEntryIds(equipment.Entries, retireIds);
            foreach (ContainerLootComponent container in containers) CollectEntryIds(container.StorageEntries, retireIds);

            foreach (WorldItemPickup pickup in FindScene<WorldItemPickup>())
            {
                if (!string.IsNullOrWhiteSpace(pickup.AuthoredItemInstanceId))
                {
                    if (ids.Contains(pickup.AuthoredItemInstanceId))
                    {
                        CollectEntryIds(pickup.GridStorageEntries, retireIds);
                        pickup.SetPersistenceAuthoredAbsent();
                    }
                    continue;
                }

                ItemStorageEntry entry = pickup.GridStorageEntries.Count > 0 ? pickup.GridStorageEntries[0] : null;
                if (entry?.Item != null && ids.Contains(entry.Item.InstanceId))
                {
                    CollectEntryIds(pickup.GridStorageEntries, retireIds);
                    pickup.RemovePersistenceRuntimeRepresentation();
                }
            }

            foreach (InventoryComponent inventory in inventories)
                ReplaceEmpty(inventory.InternalGridBackend);
            foreach (ActorEquipmentComponent equipment in equipmentComponents)
            {
                ReplaceEmpty(equipment.Backend);
                equipment.RestoreEquipmentState(new ActorEquipmentComponent.EquipmentStateSnapshot(
                    new Dictionary<string, string>(), new Dictionary<string, string[]>(), equipment.Version + 1));
            }
            foreach (ContainerLootComponent container in containers)
            {
                ReplaceEmpty(((IGridStorageTransferEndpoint)container).TransferBackend);
                container.MarkPersistenceStorageInitialized();
            }

            foreach (string instanceId in retireIds.OrderBy(value => value, StringComparer.Ordinal))
            {
                if (ItemInstanceIdRegistry.Instance.IsActive(instanceId))
                    ItemInstanceIdRegistry.Instance.RetireAfterCommit(instanceId);
            }
        }

        private static void ReconcileActorRepresentations(CurrentSliceSaveData snapshot, ResolvedScene scene)
        {
            if (Items(snapshot.actors).Length == 0)
                return;

            foreach (ActorRuntimeIdentity runtime in ActorRuntimeRegistry.ActiveRepresentations
                         .Where(identity => identity != null && identity.OriginKind == ActorOriginKind.Runtime)
                         .OrderBy(identity => identity.ActorInstanceId, StringComparer.Ordinal).ToArray())
            {
                if (!ActorSpawnService.TryRemoveRuntimeRepresentationForRestore(runtime.ActorInstanceId, out string removeError))
                    throw new InvalidOperationException(
                        $"Runtime actor '{runtime.ActorInstanceId}' representation teardown failed: {removeError}");
            }

            scene.Actors[snapshot.player.persistentId].Inventory.PreparePersistenceRestore();
            foreach (ActorState state in Items(snapshot.actors).OrderBy(value => value.actorInstanceId, StringComparer.Ordinal))
            {
                if (state.originKind == CurrentSliceSnapshotService.AuthoredActorOrigin)
                {
                    if (!scene.Identities.TryGetValue(state.authoredSceneObjectId, out PersistentSceneObjectId locator))
                        throw new InvalidOperationException(
                            $"Authored actor locator '{state.authoredSceneObjectId}' disappeared during reconciliation.");
                    ActorProfileComponent profile = locator.GetComponent<ActorProfileComponent>();
                    string profileError = profile == null ? "ActorProfileComponent is missing." : null;
                    if (profile == null || !profile.TryPreparePersistenceRestore(state.actorProfileId, out profileError))
                        throw new InvalidOperationException(
                            $"Authored actor '{state.actorInstanceId}' restore preparation failed: {profileError}");
                    if (!ActorRuntimeIdentity.TryEnsureAuthored(
                            locator.gameObject, state.actorProfileId,
                            out ActorRuntimeIdentity identity, out string identityError) ||
                        identity.ActorInstanceId != state.actorInstanceId)
                        throw new InvalidOperationException(
                            $"Authored actor '{state.authoredSceneObjectId}' identity reconciliation failed: " +
                            (identityError ?? $"expected '{state.actorInstanceId}', got '{identity?.ActorInstanceId ?? "<NONE>"}'."));
                    continue;
                }

                if (!ActorSpawnService.TrySpawn(
                        state.actorProfileId, Position(state.pose), Rotation(state.pose),
                        state.actorInstanceId, ActorSpawnInitialization.PersistenceRestore,
                        out _, out string spawnError))
                    throw new InvalidOperationException(
                        $"Runtime actor '{state.actorInstanceId}' representation restore failed: {spawnError}");
            }
        }

        private static void CollectEntryIds(IReadOnlyList<ItemStorageEntry> entries, HashSet<string> result)
        {
            if (entries == null)
                return;
            for (int index = 0; index < entries.Count; index++)
            {
                ItemInstance item = entries[index]?.Item;
                if (item == null || !result.Add(item.InstanceId) || !item.HasOwnedStorage)
                    continue;
                CollectEntryIds(item.OwnedStorage.GridStorageEntries, result);
            }
        }

        private static Dictionary<string, ItemInstance> RehydrateItems(CurrentSliceSaveData snapshot, GameDatabase database)
        {
            var result = new Dictionary<string, ItemInstance>(StringComparer.Ordinal);
            try
            {
                foreach (ItemState state in Items(snapshot.items).OrderBy(value => value.instanceId, StringComparer.Ordinal))
                {
                    ItemDefinition definition = database.GetItem(state.definitionId) ??
                        throw new InvalidOperationException($"Item definition '{state.definitionId}' disappeared after preflight.");
                    result.Add(state.instanceId, ItemInstance.Rehydrate(definition, state.instanceId, state.condition));
                }
                return result;
            }
            catch
            {
                foreach (string instanceId in result.Keys)
                    if (ItemInstanceIdRegistry.Instance.IsActive(instanceId))
                        ItemInstanceIdRegistry.Instance.RetireAfterCommit(instanceId);
                throw;
            }
        }

        private static void RestoreOwnedStorages(
            CurrentSliceSaveData snapshot,
            Dictionary<string, ItemInstance> items,
            GameDatabase database)
        {
            Dictionary<string, StorageState> storages = Items(snapshot.storages)
                .ToDictionary(storage => storage.storageId, StringComparer.Ordinal);
            foreach (ItemInstance item in items.Values.Where(value => !string.IsNullOrWhiteSpace(value.OwnedStorageProfileId)))
            {
                ItemStorageProfileDefinition profile = database.GetItemStorageProfile(item.OwnedStorageProfileId) ??
                    throw new InvalidOperationException($"Owned-storage profile '{item.OwnedStorageProfileId}' disappeared after preflight.");
                string storageId = ItemOwnedKind + ":" + item.InstanceId;
                if (!storages.TryGetValue(storageId, out StorageState state))
                    throw new InvalidOperationException($"Owned storage '{storageId}' disappeared after preflight.");

                item.AttachOwnedStorageUnregistered(profile, database.GetItem);
                IReadOnlyList<ItemStorageEntry> entries = BuildEntries(state, items);
                IReadOnlyList<GridPlacement> placements = BuildPlacements(state);
                if (!item.OwnedStorage.CompleteInitialContentLoadExact(entries, placements, out string error))
                {
                    item.DetachUnregisteredOwnedStorage();
                    throw new InvalidOperationException($"Owned storage '{storageId}' failed exact initialization: {error}");
                }
                item.RegisterAttachedOwnedStorage();
            }
        }

        private static void RestoreEquipmentLayouts(CurrentSliceSaveData snapshot, ResolvedScene scene)
        {
            foreach (EquipmentState state in Items(snapshot.equipment))
            {
                ActorEquipmentComponent equipment = scene.Actors[state.ownerPersistentId].Equipment;
                if (!equipment.TrySetLayout(state.layoutId, out string error))
                    throw new InvalidOperationException($"Equipment layout restore failed for '{state.ownerPersistentId}': {error}");
            }
        }

        private static void RestoreRootStorages(
            CurrentSliceSaveData snapshot,
            ResolvedScene scene,
            Dictionary<string, ItemInstance> items)
        {
            foreach (StorageState state in Items(snapshot.storages).Where(value => value.kind != ItemOwnedKind))
            {
                GridInventoryBackend backend = ResolveBackend(state, scene);
                if (!backend.TryReplaceWithExactEntries(
                        BuildEntries(state, items), state.usesGrid, state.width, state.height,
                        BuildPlacements(state), out string error))
                {
                    throw new InvalidOperationException($"Storage '{state.storageId}' restore failed: {error}");
                }
                if (state.kind == ContainerKind)
                    scene.Containers[state.ownerId].MarkPersistenceStorageInitialized();
            }

            foreach (EquipmentState state in Items(snapshot.equipment))
            {
                ActorEquipmentComponent equipment = scene.Actors[state.ownerPersistentId].Equipment;
                foreach (EquippedItemState item in Items(state.items))
                    equipment.AssignSlots(item.instanceId, item.slots);
            }
        }

        private static void BindStorageOwnership(
            CurrentSliceSaveData snapshot,
            ResolvedScene scene,
            Dictionary<string, ItemInstance> items)
        {
            foreach (StorageState state in Items(snapshot.storages))
            {
                object owner;
                if (state.kind == ItemOwnedKind)
                    owner = items[state.ownerId].OwnedStorage;
                else if (state.kind == ContainerKind)
                    owner = scene.Containers[state.ownerId];
                else
                    owner = scene.Actors[state.ownerId].Inventory;
                ItemOwnedStorageRegistry.Instance.BindEntries(BuildEntries(state, items), owner);
            }
        }

        private static void RestoreWorld(
            CurrentSliceSaveData snapshot,
            ResolvedScene scene,
            Dictionary<string, ItemInstance> items)
        {
            foreach (WorldItemState state in Items(snapshot.worldItems))
            {
                if (state.kind == AuthoredWorldKind)
                {
                    WorldItemPickup pickup = scene.AuthoredWorld[state.instanceId];
                    if (!state.present)
                    {
                        pickup.SetPersistenceAuthoredAbsent();
                        continue;
                    }
                    ApplyPose(state.pose, pickup.transform);
                    if (!pickup.RestorePersistenceRepresentation(items[state.instanceId], state.quantity, true, out string error))
                        throw new InvalidOperationException($"Authored world item '{state.instanceId}' failed restore: {error}");
                    ItemOwnedStorageRegistry.Instance.BindItem(items[state.instanceId], pickup);
                }
                else if (state.kind == RuntimeWorldKind)
                {
                    WorldItemPickup pickup = DroppedWorldItemSpawner.RestorePersistenceDrop(
                        items[state.instanceId], state.quantity, Position(state.pose), Rotation(state.pose), out string error);
                    if (pickup == null)
                        throw new InvalidOperationException($"Runtime drop '{state.instanceId}' failed restore: {error}");
                    ItemOwnedStorageRegistry.Instance.BindItem(items[state.instanceId], pickup);
                }
            }
        }

        private static void RestoreRuntimeState(CurrentSliceSaveData snapshot, ResolvedScene scene)
        {
            ActorRuntime player = scene.Actors[snapshot.player.persistentId];
            player.Health.ApplyInitialHealth(player.Health.MaxHealth, snapshot.player.currentHealth);
            var needs = Items(snapshot.player.needs).ToDictionary(value => value.needId, value => value.currentValue, StringComparer.Ordinal);
            if (!player.Needs.TryApplyPersistenceState(needs, out string needsError))
                throw new InvalidOperationException($"Player needs restore failed: {needsError}");

            foreach (ActorState state in Items(snapshot.actors))
            {
                ActorRuntime actor = scene.Actors[state.actorInstanceId];
                ApplyPose(state.pose, actor.Root);
                actor.Health.ApplyInitialHealth(actor.Health.MaxHealth, state.currentHealth);
                actor.Lootable?.RefreshLootableState();
                bool expectedDead = state.lifecycleState == CurrentSliceSnapshotService.DeadLifecycle;
                if (actor.Health.IsDead != expectedDead ||
                    (actor.ActorIdentity?.LifecycleState == ActorLifecycleState.Dead) != expectedDead)
                    throw new InvalidOperationException(
                        $"Actor '{state.actorInstanceId}' lifecycle restore did not reach '{state.lifecycleState}'.");
            }

            foreach (CorpseState corpse in Items(snapshot.corpses))
            {
                ActorRuntime actor = scene.Actors[corpse.persistentId];
                actor.Health.ApplyInitialHealth(actor.Health.MaxHealth, corpse.currentHealth);
                actor.Lootable?.RefreshLootableState();
            }

            foreach (ContainerState container in Items(snapshot.containers))
                ReplaceAllowedTags(scene.Containers[container.persistentId].GetComponent<WorldObjectTags>(), ContainerTags, container.mutableTags);
            foreach (DoorState door in Items(snapshot.doors))
            {
                PersistentSceneObjectId identity = scene.Identities[door.persistentId];
                ReplaceAllowedTags(identity.GetComponent<WorldObjectTags>(), DoorStates, new[] { door.state });
                identity.GetComponent<DoorSwingController>()?.SyncPersistenceState();
            }
            foreach (ActorRuntime actor in scene.Actors.Values)
                actor.Equipment?.CommitVisualState(EquipmentVisualCommitKind.Replacement);
            Physics.SyncTransforms();
        }

        private static void ValidateOwnership(ResolvedScene scene, ExternalBindings external)
        {
            foreach (ActorRuntime actor in scene.Actors.Values)
            {
                if (actor.Ownership != null && !actor.Ownership.ValidateUniqueOwnership(out string error))
                    throw new InvalidOperationException($"Actor '{actor.DisplayId}' ownership failed: {error}");
                actor.Equipment?.ValidateActorOwnedItems();
            }
            external.Validate();
        }

        private static void RestorePlayerPose(PoseState pose, Transform player)
        {
            PointClickMovementController movement = player.GetComponent<PointClickMovementController>();
            movement?.ClearTarget();
            CharacterController controller = player.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (wasEnabled) controller.enabled = false;
            player.SetPositionAndRotation(Position(pose), Rotation(pose));
            if (wasEnabled) controller.enabled = true;
            Physics.SyncTransforms();
        }

        private static GridInventoryBackend ResolveBackend(StorageState state, ResolvedScene scene)
        {
            if (state.kind == ContainerKind)
                return ((IGridStorageTransferEndpoint)scene.Containers[state.ownerId]).TransferBackend;
            ActorRuntime actor = scene.Actors[state.ownerId];
            return state.kind == EquipmentKind ? actor.Equipment.Backend : actor.Inventory.InternalGridBackend;
        }

        private static IReadOnlyList<ItemStorageEntry> BuildEntries(StorageState state, Dictionary<string, ItemInstance> items)
        {
            return Items(state.entries).Select(entry => new ItemStorageEntry(items[entry.instanceId], entry.quantity)).ToArray();
        }

        private static IReadOnlyList<GridPlacement> BuildPlacements(StorageState state)
        {
            if (!state.usesGrid)
                return Array.Empty<GridPlacement>();
            return Items(state.entries).Select(entry => new GridPlacement(
                entry.instanceId, entry.placement.x, entry.placement.y, entry.placement.rotated,
                entry.placement.width, entry.placement.height)).ToArray();
        }

        private static void ReplaceEmpty(GridInventoryBackend backend)
        {
            if (!backend.TryReplaceWithExactEntries(Array.Empty<ItemStorageEntry>(), false, 0, 0,
                    Array.Empty<GridPlacement>(), out string error))
                throw new InvalidOperationException($"Selective storage teardown failed: {error}");
        }

        private static void ReplaceAllowedTags(WorldObjectTags tags, string[] allowed, string[] target)
        {
            if (tags == null)
                throw new InvalidOperationException("Persistent mutable tag owner is missing WorldObjectTags.");
            foreach (string tag in allowed) tags.RemoveTag(tag);
            foreach (string tag in Items(target)) tags.AddTag(tag);
        }

        private static void ApplyPose(PoseState pose, Transform transform)
        {
            transform.SetPositionAndRotation(Position(pose), Rotation(pose));
        }

        private static Vector3 Position(PoseState pose) => new Vector3(pose.position.x, pose.position.y, pose.position.z);
        private static Quaternion Rotation(PoseState pose) => new Quaternion(pose.rotation.x, pose.rotation.y, pose.rotation.z, pose.rotation.w);
        private static T[] Items<T>(T[] values) => values ?? Array.Empty<T>();

        private static T[] FindScene<T>() where T : Component
        {
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include)
                .Where(component => component != null && component.gameObject.scene.IsValid() && component.gameObject.scene.isLoaded)
                .ToArray();
        }

        private static CurrentSliceLoadResult Failure(CurrentSliceLoadFailureCode code, string phase, string failure)
        {
            return new CurrentSliceLoadResult(code, phase, failure ?? "Unknown failure.", false, false, false);
        }

        private static void Log(string slot, CurrentSliceLoadResult result, CurrentSliceSaveData snapshot)
        {
            string message = "[Persistence][CURRENT_SLICE_LOAD]" +
                $"\nSlot: {slot}" +
                (snapshot == null ? string.Empty :
                    $"\nItems: {Items(snapshot.items).Length}\nStorages: {Items(snapshot.storages).Length}" +
                    $"\nWorldItems: {Items(snapshot.worldItems).Length}\nContainers: {Items(snapshot.containers).Length}" +
                    $"\nActors: {Items(snapshot.actors).Length}\nCorpsesLegacy: {Items(snapshot.corpses).Length}" +
                    $"\nDoors: {Items(snapshot.doors).Length}") +
                $"\nPhase: {result.Phase}\nFailureCode: {result.FailureCode}" +
                $"\nMutationStarted: {result.MutationStarted}\nRollbackAttempted: {result.RollbackAttempted}" +
                $"\nRollbackSucceeded: {result.RollbackSucceeded}\nResult: {(result.Success ? "Success" : "Failure")}" +
                (string.IsNullOrWhiteSpace(result.Failure) ? string.Empty : $"\nFailure: {result.Failure}");
            if (result.Success) Debug.Log(message); else Debug.LogError(message);
        }

        private static bool ShouldInjectStorageDiagnosticFailure()
        {
#if UNITY_EDITOR
            bool inject = DiagnosticInjectFailureAfterStorageRestore;
            DiagnosticInjectFailureAfterStorageRestore = false;
            return inject;
#else
            return false;
#endif
        }

        private static bool ShouldInjectActorDiagnosticFailure()
        {
#if UNITY_EDITOR
            bool inject = DiagnosticInjectFailureAfterActorReconciliation;
            DiagnosticInjectFailureAfterActorReconciliation = false;
            return inject;
#else
            return false;
#endif
        }

        private sealed class ActorRuntime
        {
            internal PersistentSceneObjectId Identity;
            internal ActorRuntimeIdentity ActorIdentity;
            internal Transform Root;
            internal string DisplayId;
            internal InventoryComponent Inventory;
            internal ActorEquipmentComponent Equipment;
            internal ActorItemOwnershipComponent Ownership;
            internal ActorHealthComponent Health;
            internal ActorNeedsComponent Needs;
            internal LootableActorInventoryComponent Lootable;
        }

        private sealed class ResolvedScene
        {
            internal readonly Dictionary<string, PersistentSceneObjectId> Identities = new Dictionary<string, PersistentSceneObjectId>(StringComparer.Ordinal);
            internal readonly Dictionary<string, ActorRuntime> Actors = new Dictionary<string, ActorRuntime>(StringComparer.Ordinal);
            internal readonly Dictionary<string, ContainerLootComponent> Containers = new Dictionary<string, ContainerLootComponent>(StringComparer.Ordinal);
            internal readonly Dictionary<string, WorldItemPickup> AuthoredWorld = new Dictionary<string, WorldItemPickup>(StringComparer.Ordinal);
            internal ActorInteractionContext Player;
            internal static bool TryCreate(
                CurrentSliceSaveData snapshot,
                bool allowMissingRuntimeActors,
                out ResolvedScene scene,
                out string error)
            {
                scene = new ResolvedScene();
                error = null;
                foreach (PersistentSceneObjectId identity in FindScene<PersistentSceneObjectId>())
                {
                    if (!identity.enabled)
                        continue;
                    if (!scene.Identities.TryAdd(identity.PersistentId, identity))
                        return Fail(out scene, out error, $"Persistent scene identity '{identity.PersistentId}' does not resolve exactly once.");
                }
                if (!scene.TrySceneActor(snapshot.player.persistentId, true, true, false, out ActorRuntime player, out error))
                    return false;
                scene.Player = player.Identity.GetComponent<ActorInteractionContext>();
                if (scene.Player == null || !Items(scene.Player.ActorTags).Contains("player"))
                    return Fail(out scene, out error, $"Player '{snapshot.player.persistentId}' does not resolve the current player role.");

                foreach (ActorState state in Items(snapshot.actors))
                {
                    if (state.originKind == CurrentSliceSnapshotService.AuthoredActorOrigin)
                    {
                        if (!scene.TrySceneActor(
                                state.authoredSceneObjectId, false, false, false,
                                out ActorRuntime actor, out error))
                            return false;
                        if (actor.ActorIdentity == null ||
                            actor.ActorIdentity.ActorInstanceId != state.actorInstanceId ||
                            actor.ActorIdentity.ActorProfileId != state.actorProfileId ||
                            actor.ActorIdentity.OriginKind != ActorOriginKind.Authored)
                            return Fail(out scene, out error,
                                $"Authored actor '{state.authoredSceneObjectId}' does not expose expected runtime identity '{state.actorInstanceId}'.");
                        scene.Actors.Remove(state.authoredSceneObjectId);
                        scene.Actors.Add(state.actorInstanceId, actor);
                    }
                    else if (ActorRuntimeRegistry.TryGet(state.actorInstanceId, out ActorRuntimeIdentity runtimeIdentity))
                    {
                        if (!scene.TryRuntimeActor(runtimeIdentity, state, out ActorRuntime actor, out error))
                            return false;
                        scene.Actors.Add(state.actorInstanceId, actor);
                    }
                    else if (!allowMissingRuntimeActors)
                    {
                        return Fail(out scene, out error,
                            $"Runtime actor '{state.actorInstanceId}' has no active representation after reconciliation.");
                    }
                }

                foreach (CorpseState corpse in Items(snapshot.corpses))
                    if (!scene.TrySceneActor(corpse.persistentId, false, false, true, out _, out error)) return false;
                foreach (ContainerState state in Items(snapshot.containers))
                {
                    if (!scene.Identities.TryGetValue(state.persistentId, out PersistentSceneObjectId identity) ||
                        identity.GetComponent<ContainerLootComponent>() is not ContainerLootComponent container)
                        return Fail(out scene, out error, $"Container '{state.persistentId}' does not resolve exactly once.");
                    scene.Containers.Add(state.persistentId, container);
                }
                foreach (DoorState state in Items(snapshot.doors))
                {
                    if (!scene.Identities.TryGetValue(state.persistentId, out PersistentSceneObjectId identity) ||
                        identity.GetComponent<WorldObjectTags>() == null)
                        return Fail(out scene, out error, $"Door '{state.persistentId}' lacks logical WorldObjectTags.");
                }

                var authored = FindScene<WorldItemPickup>().Where(pickup => !string.IsNullOrWhiteSpace(pickup.AuthoredItemInstanceId))
                    .GroupBy(pickup => pickup.AuthoredItemInstanceId, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
                foreach (WorldItemState state in Items(snapshot.worldItems).Where(value => value.kind == AuthoredWorldKind))
                {
                    if (!authored.TryGetValue(state.instanceId, out WorldItemPickup[] pickups) || pickups.Length != 1)
                        return Fail(out scene, out error, $"Authored world marker '{state.instanceId}' does not resolve exactly once.");
                    scene.AuthoredWorld.Add(state.instanceId, pickups[0]);
                }
                return true;
            }

            private bool TrySceneActor(
                string id,
                bool requireNeeds,
                bool requireOwnership,
                bool requireDead,
                out ActorRuntime actor,
                out string error)
            {
                actor = null;
                error = null;
                if (Actors.TryGetValue(id, out actor))
                    return true;
                if (!Identities.TryGetValue(id, out PersistentSceneObjectId identity))
                    return FailActor(out actor, out error, $"Actor '{id}' does not resolve exactly once.");
                actor = new ActorRuntime
                {
                    Identity = identity,
                    ActorIdentity = identity.GetComponent<ActorRuntimeIdentity>(),
                    Root = identity.transform,
                    DisplayId = id,
                    Inventory = identity.GetComponent<InventoryComponent>(),
                    Equipment = identity.GetComponent<ActorEquipmentComponent>(),
                    Ownership = identity.GetComponent<ActorItemOwnershipComponent>(),
                    Health = identity.GetComponent<ActorHealthComponent>(),
                    Needs = identity.GetComponent<ActorNeedsComponent>(),
                    Lootable = identity.GetComponent<LootableActorInventoryComponent>()
                };
                if (actor.Inventory == null || actor.Health == null ||
                    requireOwnership && actor.Ownership == null || requireNeeds && actor.Needs == null)
                    return FailActor(out actor, out error, $"Actor '{id}' lacks required Current Slice runtime components.");
                if (requireDead && !actor.Health.IsDead)
                    return FailActor(out actor, out error,
                        $"Legacy corpse '{id}' is not currently dead; its schema-v1 compatibility record cannot restore general lifecycle.");
                Actors.Add(id, actor);
                return true;
            }

            private bool TryRuntimeActor(
                ActorRuntimeIdentity identity,
                ActorState state,
                out ActorRuntime actor,
                out string error)
            {
                actor = null;
                error = null;
                if (identity == null || identity.OriginKind != ActorOriginKind.Runtime ||
                    identity.ActorProfileId != state.actorProfileId)
                    return FailActor(out actor, out error,
                        $"Runtime actor '{state.actorInstanceId}' representation is incompatible with its snapshot state.");
                actor = new ActorRuntime
                {
                    ActorIdentity = identity,
                    Root = identity.transform,
                    DisplayId = state.actorInstanceId,
                    Inventory = identity.GetComponent<InventoryComponent>(),
                    Equipment = identity.GetComponent<ActorEquipmentComponent>(),
                    Ownership = identity.GetComponent<ActorItemOwnershipComponent>(),
                    Health = identity.GetComponent<ActorHealthComponent>(),
                    Needs = identity.GetComponent<ActorNeedsComponent>(),
                    Lootable = identity.GetComponent<LootableActorInventoryComponent>()
                };
                if (actor.Inventory == null || actor.Health == null || actor.Ownership == null)
                    return FailActor(out actor, out error,
                        $"Runtime actor '{state.actorInstanceId}' lacks required lifecycle runtime components.");
                return true;
            }

            private static bool Fail(out ResolvedScene scene, out string error, string failure)
            {
                scene = null;
                error = failure;
                return false;
            }

            private static bool FailActor(out ActorRuntime actor, out string error, string failure)
            {
                actor = null;
                error = failure;
                return false;
            }
        }

        private sealed class ExternalBindings
        {
            private readonly List<Binding> bindings = new List<Binding>();

            internal static bool TryCapture(HashSet<string> sliceActorIds, out ExternalBindings result, out string error)
            {
                result = new ExternalBindings();
                error = null;
                foreach (ActorItemOwnershipComponent ownership in FindScene<ActorItemOwnershipComponent>())
                {
                    PersistentSceneObjectId identity = ownership.GetComponent<PersistentSceneObjectId>();
                    ActorRuntimeIdentity actorIdentity = ownership.GetComponent<ActorRuntimeIdentity>();
                    string ownerId = actorIdentity != null && actorIdentity.IsRegistered
                        ? actorIdentity.ActorInstanceId
                        : identity != null && identity.enabled ? identity.PersistentId : null;
                    if (!string.IsNullOrWhiteSpace(ownerId) && sliceActorIds.Contains(ownerId))
                        continue;
                    foreach (ItemStorageEntry entry in ownership.GetAllOwnedEntries())
                    {
                        ItemInstance item = entry?.Item;
                        if (item == null || !ItemInstanceIdRegistry.Instance.IsActive(item.InstanceId) ||
                            !ItemOwnedStorageRegistry.Instance.TryGetDirectOwner(item.InstanceId, out object owner))
                        {
                            result = null;
                            error = $"Out-of-slice actor binding for '{item?.InstanceId ?? "<missing>"}' is incomplete before load.";
                            return false;
                        }
                        ItemOwnedStorageRegistry.Instance.TryResolveOwnedStorage(item.InstanceId, out ItemOwnedStorageRuntime storage);
                        result.bindings.Add(new Binding(item.InstanceId, owner, storage));
                    }
                }
                return true;
            }

            internal void Validate()
            {
                foreach (Binding binding in bindings)
                {
                    if (!ItemInstanceIdRegistry.Instance.IsActive(binding.InstanceId) ||
                        !ItemOwnedStorageRegistry.Instance.TryGetDirectOwner(binding.InstanceId, out object owner) ||
                        !ReferenceEquals(owner, binding.Owner))
                        throw new InvalidOperationException($"Out-of-slice ownership binding '{binding.InstanceId}' changed during load.");
                    bool hasStorage = ItemOwnedStorageRegistry.Instance.TryResolveOwnedStorage(binding.InstanceId, out ItemOwnedStorageRuntime storage);
                    if ((binding.Storage != null) != hasStorage || binding.Storage != null && !ReferenceEquals(binding.Storage, storage))
                        throw new InvalidOperationException($"Out-of-slice owned storage '{binding.InstanceId}' changed during load.");
                }
            }

            private readonly struct Binding
            {
                internal Binding(string instanceId, object owner, ItemOwnedStorageRuntime storage)
                {
                    InstanceId = instanceId;
                    Owner = owner;
                    Storage = storage;
                }
                internal string InstanceId { get; }
                internal object Owner { get; }
                internal ItemOwnedStorageRuntime Storage { get; }
            }
        }
    }

#if UNITY_EDITOR
    public static class CurrentSliceRoundTripDiagnosticScenario
    {
        private const string BackpackId = "core:small_backpack_01";
        private const string CrowbarId = "core:rusted_crowbar_01";
        private const string RifleId = "core:lee_enfield_rifle_01";
        private static readonly Vector3 DropOffset = new Vector3(1.75f, 0.35f, -0.8f);
        private static string diagnosticCorpseId;

        public static bool TryPrepareStateA(out string failure)
        {
            failure = null;
            try
            {
                ActorInteractionContext player = Player();
                InventoryComponent inventory = player.GetInventoryComponent();
                ActorEquipmentComponent equipment = player.GetComponent<ActorEquipmentComponent>();
                if (inventory == null || equipment == null)
                    throw new InvalidOperationException("Diagnostic player lacks inventory or Equipment.");

                ItemStorageEntry backpack = inventory.Entries.FirstOrDefault(entry => entry?.DefinitionId == BackpackId);
                if (backpack?.Item == null)
                    throw new InvalidOperationException("Diagnostic player has no small backpack seed.");
                Equip(equipment, backpack.Item.InstanceId, new[] { ActorEquipmentComponent.BackSlotId });

                PickUpAuthored(player, CrowbarId);
                PickUpAuthored(player, RifleId);
                ItemStorageEntry rifle = inventory.Entries.FirstOrDefault(entry => entry?.DefinitionId == RifleId);
                if (rifle?.Item == null)
                    throw new InvalidOperationException("Lee-Enfield was not transferred to the player inventory.");
                Equip(equipment, rifle.Item.InstanceId, null);

                ItemStorageEntry equippedBackpack = equipment.Entries.FirstOrDefault(entry => entry?.DefinitionId == BackpackId);
                ItemOwnedStorageRuntime backpackStorage = equippedBackpack?.Item?.OwnedStorage;
                if (backpackStorage == null)
                    throw new InvalidOperationException("Equipped backpack has no registered owned storage.");
                ContainerLootComponent source = FindScene<ContainerLootComponent>()
                    .FirstOrDefault(container => container.StorageEntries.Any(entry => entry?.Item != null && entry.Quantity >= 2));
                if (source == null)
                    throw new InvalidOperationException("No initialized container exposes a transferable stack.");
                MakeContainerAccessible(source.GetComponent<WorldObjectTags>());
                ItemStorageEntry stack = source.StorageEntries.First(entry => entry?.Item != null && entry.Quantity >= 2);
                var context = new GridStorageTransferContext(
                    new DebugActionExecutionContext(player, source.GetComponent<WorldObjectTags>(), null), null);
                InventoryMutationResult transfer = GridStorageTransferService.TransferQuantityAuto(
                    source, backpackStorage, stack.Item.InstanceId, 2, true, context);
                if (!transfer.Success)
                    throw new InvalidOperationException("Stack transfer into backpack failed: " + transfer.Message);

                if (!inventory.TryGetEntryByInstanceId(
                        inventory.Entries.First(entry => entry?.DefinitionId == CrowbarId).Item.InstanceId,
                        out int crowbarIndex, out ItemStorageEntry crowbar))
                    throw new InvalidOperationException("Crowbar is missing before runtime drop.");
                if (!DroppedWorldItemSpawner.TryDrop(inventory, crowbarIndex, 1,
                        "m37_diagnostic_drop", "M37 diagnostic drop", out string dropError))
                    throw new InvalidOperationException(dropError);
                if (!ItemOwnedStorageRegistry.Instance.TryGetDirectOwner(crowbar.Item.InstanceId, out object dropOwner) ||
                    dropOwner is not WorldItemPickup runtimeDrop)
                    throw new InvalidOperationException("Runtime crowbar drop did not publish its WorldItemPickup owner.");
                runtimeDrop.transform.SetPositionAndRotation(player.transform.position + DropOffset, Quaternion.Euler(0f, 37f, 0f));

                SetPlayerPose(player.transform, player.transform.position + new Vector3(0.45f, 0f, 0.3f), Quaternion.Euler(0f, 23f, 0f));
                player.GetComponent<ActorHealthComponent>()?.ApplyDamage(7f);
                ActorNeedsComponent needs = player.GetComponent<ActorNeedsComponent>();
                ActorNeedState firstNeed = needs?.RuntimeStates?.FirstOrDefault(state => state != null);
                if (firstNeed != null) needs.TryRestoreNeed(firstNeed.needId, 0.25f);

                PersistentSceneObjectId door = FindScene<PersistentSceneObjectId>()
                    .FirstOrDefault(identity => identity.GetComponent<DoorSwingController>() != null);
                if (door == null) throw new InvalidOperationException("Diagnostic scene has no visual door controller.");
                SetDoorState(door, door.GetComponent<WorldObjectTags>().HasTag("opened_door") ? "closed_door" : "opened_door");

                ActorEquipmentComponent corpseEquipment = FindScene<ActorEquipmentComponent>().FirstOrDefault(candidate =>
                    candidate.GetComponent<PersistentSceneObjectId>() != null &&
                    candidate.GetComponent<ActorInteractionContext>()?.ActorTags.Contains("player") != true &&
                    candidate.GetComponent<ActorHealthComponent>() != null && candidate.Entries.Count > 0);
                if (corpseEquipment == null) throw new InvalidOperationException("Diagnostic scene has no equipped NPC for corpse coverage.");
                diagnosticCorpseId = corpseEquipment.GetComponent<PersistentSceneObjectId>().PersistentId;
                corpseEquipment.GetComponent<ActorHealthComponent>().Kill();
                return true;
            }
            catch (Exception exception)
            {
                failure = exception.Message;
                return false;
            }
        }

        public static bool TryMutateStateB(out string failure)
        {
            failure = null;
            try
            {
                ActorInteractionContext player = Player();
                InventoryComponent inventory = player.GetInventoryComponent();
                ActorEquipmentComponent equipment = player.GetComponent<ActorEquipmentComponent>();
                SetPlayerPose(player.transform, player.transform.position + new Vector3(-1.1f, 0f, 0.65f), Quaternion.Euler(0f, 141f, 0f));
                player.GetComponent<ActorHealthComponent>()?.ApplyDamage(11f);

                ItemStorageEntry rifle = equipment.Entries.FirstOrDefault(entry => entry?.DefinitionId == RifleId);
                if (rifle?.Item != null && !DroppedWorldItemSpawner.TryDrop(
                        equipment, rifle.Item.InstanceId, inventory, "m37_diagnostic_mutation", "M37 mutation", out string dropError))
                    throw new InvalidOperationException(dropError);

                WorldItemPickup existingDrop = FindScene<WorldItemPickup>()
                    .FirstOrDefault(pickup => string.IsNullOrWhiteSpace(pickup.AuthoredItemInstanceId) && pickup.GridStorageEntries.Count > 0);
                if (existingDrop != null)
                    existingDrop.transform.position += new Vector3(2f, 0f, 1f);

                PersistentSceneObjectId door = FindScene<PersistentSceneObjectId>()
                    .First(identity => identity.GetComponent<DoorSwingController>() != null);
                SetDoorState(door, door.GetComponent<WorldObjectTags>().HasTag("locked_door") ? "opened_door" : "locked_door");
                ContainerLootComponent container = FindScene<ContainerLootComponent>().First();
                WorldObjectTags tags = container.GetComponent<WorldObjectTags>();
                tags.RemoveTag("storage_accessible");
                tags.RemoveTag("opened_container");
                tags.RemoveTag("lootable_container");
                tags.AddTag("sealed_container");
                tags.AddTag("unsearched_container");
                return true;
            }
            catch (Exception exception)
            {
                failure = exception.Message;
                return false;
            }
        }

        public static string ValidateCoverage(CurrentSliceSaveData snapshot)
        {
            var missing = new List<string>();
            EquipmentState playerEquipment = snapshot.equipment.FirstOrDefault(state => state.ownerPersistentId == snapshot.player.persistentId);
            if (playerEquipment == null || !playerEquipment.items.Any(item =>
                    snapshot.items.Any(value => value.instanceId == item.instanceId && value.definitionId == RifleId)))
                missing.Add("Lee-Enfield Equipment");
            StorageState backpack = snapshot.storages.FirstOrDefault(state => state.kind == "item_owned" && state.entries.Length > 0);
            if (backpack == null) missing.Add("backpack content / stack transfer");
            if (!snapshot.worldItems.Any(item => item.kind == "authored" && !item.present)) missing.Add("authored pickup absent marker");
            if (!snapshot.worldItems.Any(item => item.kind == "runtime" && item.present)) missing.Add("runtime dropped item");
            if (snapshot.containers.Length == 0) missing.Add("authored containers");
            if (snapshot.actors == null || snapshot.actors.Length == 0 || !snapshot.actors.Any(actor =>
                    actor != null && actor.lifecycleState == CurrentSliceSnapshotService.DeadLifecycle &&
                    !string.IsNullOrWhiteSpace(actor.inventoryStorageId)))
                missing.Add("current corpse storage");
            if (snapshot.doors.Length == 0) missing.Add("door state");
            if (snapshot.player.currentHealth >= 100f || snapshot.player.needs.Length == 0) missing.Add("health / needs");
            return missing.Count == 0 ? null : "State A lacks: " + string.Join(", ", missing);
        }

        public static bool TryRestoreDiagnosticCorpseAlive(out string failure)
        {
            failure = null;
            if (string.IsNullOrWhiteSpace(diagnosticCorpseId))
                return true;
            try
            {
                PersistentSceneObjectId identity = FindScene<PersistentSceneObjectId>()
                    .Single(value => value.PersistentId == diagnosticCorpseId);
                ActorHealthComponent health = identity.GetComponent<ActorHealthComponent>();
                health.ApplyInitialHealth(health.MaxHealth, health.MaxHealth);
                identity.GetComponent<LootableActorInventoryComponent>()?.RefreshLootableState();
                diagnosticCorpseId = null;
                return true;
            }
            catch (Exception exception)
            {
                failure = exception.Message;
                return false;
            }
        }

        private static void PickUpAuthored(ActorInteractionContext player, string definitionId)
        {
            GameDatabase database = GameDataManager.Instance.Database;
            WorldItemPickup pickup = FindScene<WorldItemPickup>().Single(value =>
                database.GetItem(value.ItemDefinitionId)?.id == definitionId &&
                !string.IsNullOrWhiteSpace(value.AuthoredItemInstanceId));
            string instanceId = pickup.AuthoredItemInstanceId;
            pickup.PickUp(player, pickup.GetComponent<WorldObjectTags>());
            if (!player.GetInventoryComponent().TryGetEntryByInstanceId(instanceId, out _, out _))
                throw new InvalidOperationException($"Authored pickup '{definitionId}' did not commit.");
        }

        private static void Equip(ActorEquipmentComponent equipment, string instanceId, string[] slots)
        {
            EquipmentPreview preview = equipment.PreviewEquip(instanceId, slots);
            if (!preview.Success && preview.RequiresChoice)
            {
                EquipmentSlotSet choice = equipment.GetAvailableSlotSets(instanceId).First();
                preview = equipment.PreviewEquip(instanceId, choice.SlotIds);
            }
            EquipmentMutationResult result = equipment.Equip(preview);
            if (!result.Success)
                throw new InvalidOperationException($"Equip '{instanceId}' failed: {result.FailureCode}: {result.Message}");
        }

        private static ActorInteractionContext Player() => FindScene<ActorInteractionContext>()
            .Single(actor => actor.GetComponent<PersistentSceneObjectId>() != null && actor.ActorTags.Contains("player"));

        private static void MakeContainerAccessible(WorldObjectTags tags)
        {
            tags.RemoveTag("sealed_container");
            tags.RemoveTag("looted_container");
            tags.RemoveTag("unsearched_container");
            tags.AddTag("opened_container");
            tags.AddTag("storage_accessible");
            tags.AddTag("lootable_container");
        }

        private static void SetDoorState(PersistentSceneObjectId identity, string state)
        {
            WorldObjectTags tags = identity.GetComponent<WorldObjectTags>();
            tags.RemoveTag("opened_door");
            tags.RemoveTag("closed_door");
            tags.RemoveTag("locked_door");
            tags.AddTag(state);
            identity.GetComponent<DoorSwingController>().SyncPersistenceState();
        }

        private static void SetPlayerPose(Transform transform, Vector3 position, Quaternion rotation)
        {
            transform.GetComponent<PointClickMovementController>()?.ClearTarget();
            CharacterController controller = transform.GetComponent<CharacterController>();
            bool enabled = controller != null && controller.enabled;
            if (enabled) controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            if (enabled) controller.enabled = true;
            Physics.SyncTransforms();
        }

        private static T[] FindScene<T>() where T : Component => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include)
            .Where(component => component != null && component.gameObject.scene.IsValid() && component.gameObject.scene.isLoaded).ToArray();
    }
#endif
}
