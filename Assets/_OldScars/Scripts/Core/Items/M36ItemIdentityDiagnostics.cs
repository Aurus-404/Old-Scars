using System;
using System.Collections.Generic;
using OldScars.Core.Data.Definitions;
using UnityEngine;

namespace OldScars.Core.Items
{
    public sealed class M36ItemIdentityDiagnosticReport
    {
        internal M36ItemIdentityDiagnosticReport(List<string> failures)
        {
            Failures = failures != null ? failures.ToArray() : Array.Empty<string>();
        }

        public bool Passed => Failures.Count == 0;
        public IReadOnlyList<string> Failures { get; }

        public override string ToString()
        {
            return Passed
                ? "M36.1 Checkpoint A Item Identity Diagnostics: PASS"
                : "M36.1 Checkpoint A Item Identity Diagnostics: FAIL\n- " + string.Join("\n- ", Failures);
        }
    }

    /// <summary>
    /// Synthetic, asset-free invariant checks for M36.1 Checkpoint A.
    /// The diagnostic resets both runtime registries before and after execution.
    /// </summary>
    public static class M36ItemIdentityDiagnostics
    {
        private const string RehydratedId = "item_11111111111111111111111111111111";
        private const string ReleasedFailureId = "item_22222222222222222222222222222222";
        private const string ConditionAId = "item_33333333333333333333333333333333";
        private const string ConditionBId = "item_44444444444444444444444444444444";
        private const string OwnedRehydratedId = "item_55555555555555555555555555555555";
        private const string FailedOwnedHydrationId = "item_66666666666666666666666666666666";
        private const string TransitionOwnedId = "item_77777777777777777777777777777777";

        public static M36ItemIdentityDiagnosticReport Run()
        {
            var failures = new List<string>();
            ItemInstanceIdRegistry.ResetRuntimeSession();

            try
            {
                ItemDefinition stackDefinition = CreateDefinition("m36_diag_stack", 10, 10, null);
                ItemDefinition ownedDefinition = CreateDefinition("m36_diag_owned_storage", 100, 1, "m36_diag_storage");
                var storageProfile = new ItemStorageProfileDefinition
                {
                    type = "item_storage_profile",
                    id = "m36_diag_storage",
                    display_name = "M36 diagnostic storage",
                    width = 2,
                    height = 2
                };

                ItemInstance first = ItemInstance.CreateNew(stackDefinition);
                ItemInstance second = ItemInstance.CreateNew(stackDefinition);
                Check(first.InstanceId != second.InstanceId, "CreateNew must generate different IDs.", failures);
                Check(
                    ItemInstanceIdRegistry.IsValidFormat(first.InstanceId) &&
                    ItemInstanceIdRegistry.IsValidFormat(second.InstanceId),
                    "CreateNew IDs must match item_<guid N lowercase>.",
                    failures);

                ItemInstance rehydrated = ItemInstance.Rehydrate(stackDefinition, RehydratedId, 7);
                Check(rehydrated.InstanceId == RehydratedId && rehydrated.Condition == 7,
                    "Rehydrate must preserve the exact ID and Condition.", failures);

                bool duplicateRejected = Throws(() => ItemInstance.Rehydrate(stackDefinition, RehydratedId, 7));
                Check(duplicateRejected, "Rehydrate must reject an already-active ID.", failures);

                bool invalidConditionRejected = Throws(() => ItemInstance.Rehydrate(stackDefinition, ReleasedFailureId, 0));
                Check(invalidConditionRejected && !ItemInstanceIdRegistry.Instance.IsActive(ReleasedFailureId),
                    "A failed rehydration must release its new reservation.", failures);
                ItemInstance releasedRetry = ItemInstance.Rehydrate(stackDefinition, ReleasedFailureId, 5);
                Check(releasedRetry.InstanceId == ReleasedFailureId,
                    "A released failed reservation must be reservable by a valid operation.", failures);

                int activeBeforeFailedCreation = ItemInstanceIdRegistry.Instance.ActiveCount;
                int storagesBeforeFailedCreation = ItemOwnedStorageRegistry.Instance.RegisteredStorageCount;
                bool failedCreationRejected = Throws(() => ItemInstance.CreateNew(ownedDefinition, _ => null));
                Check(
                    failedCreationRejected &&
                    ItemInstanceIdRegistry.Instance.ActiveCount == activeBeforeFailedCreation &&
                    ItemOwnedStorageRegistry.Instance.RegisteredStorageCount == storagesBeforeFailedCreation,
                    "A failed CreateNew must release its generated ID and partial owned-storage state.",
                    failures);

                string nestedRollbackId;
                using (ItemInstanceIdRegistry.ItemInstanceIdReservationScope outerScope =
                       ItemInstanceIdRegistry.Instance.BeginReservationScope())
                {
                    using (ItemInstanceIdRegistry.ItemInstanceIdReservationScope innerScope =
                           ItemInstanceIdRegistry.Instance.BeginReservationScope())
                    {
                        nestedRollbackId = ItemInstance.CreateNew(stackDefinition).InstanceId;
                        innerScope.Commit();
                    }
                }
                Check(!ItemInstanceIdRegistry.Instance.IsActive(nestedRollbackId),
                    "A nested committed reservation must transfer to its parent and release on parent rollback.", failures);

                RunCommittedOwnershipTransitionChecks(stackDefinition, failures);

                Func<string, ItemDefinition> definitionResolver = id => id == stackDefinition.id ? stackDefinition : null;
                var splitSourceStorage = new ItemStorage();
                var splitTargetStorage = new ItemStorage();
                var splitSource = new GridInventoryBackend(splitSourceStorage, definitionResolver);
                var splitTarget = new GridInventoryBackend(splitTargetStorage, definitionResolver);
                InventoryMutationResult splitAdd = splitSource.Add(stackDefinition, 2);
                string originalSplitId = splitAdd.DestinationInstanceId;
                InventoryMutationResult split = splitSource.TransferTo(splitTarget, originalSplitId, 1);
                ItemStorageEntry splitTargetEntry = splitTargetStorage.GetEntry(0);
                Check(
                    split.Success && splitSourceStorage.GetEntryByInstanceId(originalSplitId) != null &&
                    splitTargetEntry?.Item != null && splitTargetEntry.Item.InstanceId != originalSplitId,
                    "Split must preserve the source ID and create a different active ID.",
                    failures);

                var mergeSourceStorage = new ItemStorage();
                var mergeTargetStorage = new ItemStorage();
                var mergeSource = new GridInventoryBackend(mergeSourceStorage, definitionResolver);
                var mergeTarget = new GridInventoryBackend(mergeTargetStorage, definitionResolver);
                InventoryMutationResult mergeSourceAdd = mergeSource.Add(stackDefinition, 1);
                InventoryMutationResult mergeTargetAdd = mergeTarget.Add(stackDefinition, 9);
                string mergeSourceId = mergeSourceAdd.DestinationInstanceId;
                string mergeTargetId = mergeTargetAdd.DestinationInstanceId;
                InventoryMutationResult merge = mergeSource.MergeIntoTarget(mergeTarget, mergeSourceId, mergeTargetId);
                Check(
                    merge.Success && merge.DestinationInstanceId == mergeTargetId &&
                    mergeTargetStorage.GetEntryByInstanceId(mergeTargetId)?.Quantity == 10 &&
                    !ItemInstanceIdRegistry.Instance.IsActive(mergeSourceId) &&
                    ItemInstanceIdRegistry.Instance.IsActive(mergeTargetId),
                    "Compatible full merge must preserve the target and retire the consumed source.",
                    failures);

                var repeatedAddStorage = new ItemStorage();
                var repeatedAddBackend = new GridInventoryBackend(repeatedAddStorage, definitionResolver);
                InventoryMutationResult repeatedAddFirst = repeatedAddBackend.Add(stackDefinition, 9);
                string repeatedAddTargetId = repeatedAddFirst.DestinationInstanceId;
                int activeBeforeRepeatedAddMerge = ItemInstanceIdRegistry.Instance.ActiveCount;
                InventoryMutationResult repeatedAddSecond = repeatedAddBackend.Add(stackDefinition, 1);
                Check(
                    repeatedAddFirst.Success && repeatedAddSecond.Success &&
                    repeatedAddStorage.EntryCount == 1 &&
                    repeatedAddStorage.GetEntryByInstanceId(repeatedAddTargetId)?.Quantity == 10 &&
                    repeatedAddSecond.DestinationInstanceId == repeatedAddTargetId &&
                    ItemInstanceIdRegistry.Instance.ActiveCount == activeBeforeRepeatedAddMerge,
                    "Repeated backend Add full-merge must preserve quantity without leaving a candidate ID active.",
                    failures);

                ItemInstance conditionA = ItemInstance.Rehydrate(stackDefinition, ConditionAId, 10);
                ItemInstance conditionB = ItemInstance.Rehydrate(stackDefinition, ConditionBId, 9);
                var conditionSourceStorage = new ItemStorage();
                var conditionTargetStorage = new ItemStorage();
                conditionSourceStorage.AddItemAsSeparateEntry(conditionA, 1);
                conditionTargetStorage.AddItemAsSeparateEntry(conditionB, 1);
                var conditionSource = new GridInventoryBackend(conditionSourceStorage, definitionResolver);
                var conditionTarget = new GridInventoryBackend(conditionTargetStorage, definitionResolver);
                InventoryMutationResult incompatible = conditionSource.MergeIntoTarget(
                    conditionTarget,
                    conditionA.InstanceId,
                    conditionB.InstanceId);
                Check(!incompatible.Success && incompatible.Failure == InventoryMutationResult.MutationFailure.IncompatibleStack,
                    "Stacks with different Condition must reject merge.", failures);

                ItemInstance ownedItem = ItemInstance.CreateNew(
                    ownedDefinition,
                    profileId => profileId == storageProfile.id ? storageProfile : null);
                Check(
                    ownedItem.OwnedStorage != null &&
                    ownedItem.OwnedStorage.ContainerInstanceId == ownedItem.InstanceId &&
                    ItemOwnedStorageRegistry.Instance.TryResolveOwnedStorage(ownedItem.InstanceId, out ItemOwnedStorageRuntime resolved) &&
                    ReferenceEquals(resolved, ownedItem.OwnedStorage),
                    "Item-owned storage must use and register exactly its owner item ID.",
                    failures);

                var duplicateStorage = new ItemOwnedStorageRuntime(ownedItem, storageProfile);
                Check(Throws(() => ItemOwnedStorageRegistry.Instance.RegisterStorage(duplicateStorage)),
                    "Duplicate item-owned storage registration must be rejected.", failures);

                object owner = new object();
                bool firstBindSucceeded = !Throws(() => ItemOwnedStorageRegistry.Instance.BindItem(ownedItem, owner));
                bool idempotentBindSucceeded = !Throws(() => ItemOwnedStorageRegistry.Instance.BindItem(ownedItem, owner));
                Check(firstBindSucceeded && idempotentBindSucceeded,
                    "Binding the same item to the same owner must be idempotent.", failures);
                Check(Throws(() => ItemOwnedStorageRegistry.Instance.BindItem(ownedItem, new object())),
                    "Binding the same item to another owner must be rejected.", failures);

                Func<string, ItemDefinition> ownedContentResolver = id =>
                    id == stackDefinition.id ? stackDefinition :
                    id == ownedDefinition.id ? ownedDefinition : null;
                ItemInstance detachedOwnedItem = ItemInstance.Rehydrate(ownedDefinition, OwnedRehydratedId, 82);
                Check(
                    detachedOwnedItem.OwnedStorage == null &&
                    !ItemOwnedStorageRegistry.Instance.TryResolveOwnedStorage(OwnedRehydratedId, out _),
                    "Rehydrate must leave item-owned storage detached and unpublished.",
                    failures);

                detachedOwnedItem.AttachOwnedStorageUnregistered(storageProfile, ownedContentResolver);
                InventoryMutationResult hydratedContentAdd = detachedOwnedItem.OwnedStorage.Backend.Add(stackDefinition, 1);
                string hydratedContentId = hydratedContentAdd.DestinationInstanceId;
                Check(
                    hydratedContentAdd.Success &&
                    detachedOwnedItem.OwnedStorage.GridInitializationState == GridStorageInitializationState.Pending &&
                    !ItemOwnedStorageRegistry.Instance.TryResolveOwnedStorage(OwnedRehydratedId, out _),
                    "Detached hydration must allow content population without publishing the storage.",
                    failures);

                bool hydratedLayoutValid = detachedOwnedItem.OwnedStorage.CompleteInitialContentLoad(out string hydrationError);
                Check(
                    hydratedLayoutValid && string.IsNullOrWhiteSpace(hydrationError) &&
                    detachedOwnedItem.OwnedStorage.GridInitializationState == GridStorageInitializationState.Active &&
                    !ItemOwnedStorageRegistry.Instance.TryResolveOwnedStorage(OwnedRehydratedId, out _),
                    "Detached hydration must validate layout before explicit registration.",
                    failures);

                detachedOwnedItem.RegisterAttachedOwnedStorage();
                Check(
                    ItemOwnedStorageRegistry.Instance.TryResolveOwnedStorage(
                        OwnedRehydratedId,
                        out ItemOwnedStorageRuntime hydratedResolvedStorage) &&
                    ReferenceEquals(hydratedResolvedStorage, detachedOwnedItem.OwnedStorage),
                    "A validated detached item-owned storage must publish only through explicit registration.",
                    failures);

                var terminalParentStorage = new ItemStorage();
                terminalParentStorage.AddItemAsSeparateEntry(detachedOwnedItem, 1);
                var terminalParentBackend = new GridInventoryBackend(terminalParentStorage, ownedContentResolver);
                object terminalOwner = new object();
                ItemOwnedStorageRegistry.Instance.BindItem(detachedOwnedItem, terminalOwner);
                ItemOwnedStorageRegistry.Instance.BindEntries(
                    detachedOwnedItem.OwnedStorage.GridStorageEntries,
                    detachedOwnedItem.OwnedStorage);

                int activeBeforeRejectedRemove = ItemInstanceIdRegistry.Instance.ActiveCount;
                int storagesBeforeRejectedRemove = ItemOwnedStorageRegistry.Instance.RegisteredStorageCount;
                int ownersBeforeRejectedRemove = ItemOwnedStorageRegistry.Instance.BoundItemCount;
                int parentVersionBeforeRejectedRemove = terminalParentStorage.Version;
                int contentVersionBeforeRejectedRemove = detachedOwnedItem.OwnedStorage.ContentVersion;
                InventoryMutationResult rejectedOwnedRemove = terminalParentBackend.Remove(OwnedRehydratedId, 1);
                Check(
                    !rejectedOwnedRemove.Success &&
                    rejectedOwnedRemove.Failure == InventoryMutationResult.MutationFailure.OwnedStorageNotEmpty &&
                    !string.IsNullOrWhiteSpace(rejectedOwnedRemove.Message) &&
                    terminalParentStorage.GetEntryByInstanceId(OwnedRehydratedId)?.Quantity == 1 &&
                    detachedOwnedItem.OwnedStorage.GridStorageEntries.Count == 1 &&
                    ItemInstanceIdRegistry.Instance.IsActive(OwnedRehydratedId) &&
                    ItemInstanceIdRegistry.Instance.IsActive(hydratedContentId) &&
                    ItemInstanceIdRegistry.Instance.ActiveCount == activeBeforeRejectedRemove &&
                    ItemOwnedStorageRegistry.Instance.RegisteredStorageCount == storagesBeforeRejectedRemove &&
                    ItemOwnedStorageRegistry.Instance.BoundItemCount == ownersBeforeRejectedRemove &&
                    terminalParentStorage.Version == parentVersionBeforeRejectedRemove &&
                    detachedOwnedItem.OwnedStorage.ContentVersion == contentVersionBeforeRejectedRemove,
                    "Terminal remove must reject a non-empty item-owned storage without mutating identities, owners or storage.",
                    failures);

                InventoryMutationResult emptyOwnedStorage = detachedOwnedItem.OwnedStorage.Backend.Remove(hydratedContentId, 1);
                InventoryMutationResult removeEmptiedOwner = terminalParentBackend.Remove(OwnedRehydratedId, 1);
                Check(
                    emptyOwnedStorage.Success && removeEmptiedOwner.Success &&
                    terminalParentStorage.IsEmpty && detachedOwnedItem.OwnedStorage.IsEmpty &&
                    !ItemInstanceIdRegistry.Instance.IsActive(hydratedContentId) &&
                    !ItemInstanceIdRegistry.Instance.IsActive(OwnedRehydratedId) &&
                    !ItemOwnedStorageRegistry.Instance.TryResolveOwnedStorage(OwnedRehydratedId, out _) &&
                    ItemInstanceIdRegistry.Instance.ActiveCount == activeBeforeRejectedRemove - 2 &&
                    ItemOwnedStorageRegistry.Instance.RegisteredStorageCount == storagesBeforeRejectedRemove - 1 &&
                    ItemOwnedStorageRegistry.Instance.BoundItemCount == ownersBeforeRejectedRemove - 2,
                    "Terminal remove must succeed after the item-owned storage is emptied and retire its runtime state.",
                    failures);

                int activeBeforeFailedOwnedHydration = ItemInstanceIdRegistry.Instance.ActiveCount;
                int storagesBeforeFailedOwnedHydration = ItemOwnedStorageRegistry.Instance.RegisteredStorageCount;
                int ownersBeforeFailedOwnedHydration = ItemOwnedStorageRegistry.Instance.BoundItemCount;
                using (ItemInstanceIdRegistry.ItemInstanceIdReservationScope failedHydrationScope =
                       ItemInstanceIdRegistry.Instance.BeginReservationScope())
                {
                    ItemInstance failedHydration = ItemInstance.Rehydrate(
                        ownedDefinition,
                        FailedOwnedHydrationId,
                        91);
                    failedHydration.AttachOwnedStorageUnregistered(storageProfile, ownedContentResolver);
                    InventoryMutationResult oversizedContent = failedHydration.OwnedStorage.Backend.Add(stackDefinition, 50);
                    bool invalidLayoutRejected =
                        !failedHydration.OwnedStorage.CompleteInitialContentLoad(out _) &&
                        Throws(failedHydration.RegisterAttachedOwnedStorage);
                    failedHydration.DetachUnregisteredOwnedStorage();
                    Check(
                        oversizedContent.Success && invalidLayoutRejected && failedHydration.OwnedStorage == null,
                        "Failed detached hydration must reject publication and allow attachment cleanup.",
                        failures);
                }

                Check(
                    !ItemInstanceIdRegistry.Instance.IsActive(FailedOwnedHydrationId) &&
                    ItemInstanceIdRegistry.Instance.ActiveCount == activeBeforeFailedOwnedHydration &&
                    ItemOwnedStorageRegistry.Instance.RegisteredStorageCount == storagesBeforeFailedOwnedHydration &&
                    ItemOwnedStorageRegistry.Instance.BoundItemCount == ownersBeforeFailedOwnedHydration,
                    "Failed detached hydration must roll back every reserved ID without partial registry state.",
                    failures);

                string resetProbeId = ownedItem.InstanceId;
                Check(ItemInstanceIdRegistry.Instance.ActiveCount > 0 &&
                      ItemOwnedStorageRegistry.Instance.RegisteredStorageCount > 0 &&
                      ItemOwnedStorageRegistry.Instance.BoundItemCount > 0,
                    "Reset precondition must contain active identity, storage and owner state.", failures);
                ItemInstanceIdRegistry.ResetRuntimeSession();
                Check(
                    !ItemInstanceIdRegistry.Instance.IsActive(resetProbeId) &&
                    ItemInstanceIdRegistry.Instance.ActiveCount == 0 &&
                    ItemOwnedStorageRegistry.Instance.RegisteredStorageCount == 0 &&
                    ItemOwnedStorageRegistry.Instance.BoundItemCount == 0,
                    "Runtime session reset must clear both item registries.",
                    failures);
            }
            catch (Exception exception)
            {
                failures.Add($"Unexpected {exception.GetType().Name}: {exception.Message}");
            }
            finally
            {
                ItemInstanceIdRegistry.ResetRuntimeSession();
                if (ItemInstanceIdRegistry.Instance.ActiveCount != 0 ||
                    ItemOwnedStorageRegistry.Instance.RegisteredStorageCount != 0 ||
                    ItemOwnedStorageRegistry.Instance.BoundItemCount != 0)
                {
                    failures.Add("Diagnostic cleanup left runtime identity state registered.");
                }
            }

            return new M36ItemIdentityDiagnosticReport(failures);
        }

        public static M36ItemIdentityDiagnosticReport RunAndLog()
        {
            M36ItemIdentityDiagnosticReport report = Run();
            if (report.Passed)
                Debug.Log(report.ToString());
            else
                Debug.LogError(report.ToString());
            return report;
        }

        private static void RunCommittedOwnershipTransitionChecks(
            ItemDefinition stackDefinition,
            List<string> failures)
        {
            ItemDefinition identityDefinition = CreateDefinition("m36_diag_identity", 100, 1, null);
            ItemDefinition ownedDefinition = CreateDefinition(
                "m36_diag_transition_owned",
                100,
                1,
                "m36_diag_transition_storage");
            var storageProfile = new ItemStorageProfileDefinition
            {
                type = "item_storage_profile",
                id = "m36_diag_transition_storage",
                display_name = "M36 transition storage",
                width = 2,
                height = 2
            };
            Func<string, ItemDefinition> resolver = id =>
                id == stackDefinition.id ? stackDefinition :
                id == identityDefinition.id ? identityDefinition :
                id == ownedDefinition.id ? ownedDefinition : null;

            var explicitSource = new DiagnosticGridOwner("explicit source", resolver);
            var explicitTarget = new DiagnosticGridOwner("explicit target", resolver);
            var unexpectedOwner = new DiagnosticGridOwner("unexpected owner", resolver);
            ItemInstance explicitItem = ItemInstance.CreateNew(identityDefinition);
            explicitSource.Seed(explicitItem, 1);
            ItemOwnedStorageRegistry.Instance.TransferBinding(explicitItem.InstanceId, explicitSource, explicitTarget);
            ItemOwnedStorageRegistry.Instance.TransferBinding(explicitItem.InstanceId, explicitSource, explicitTarget);
            bool wrongExpectedRejected = Throws(() =>
                ItemOwnedStorageRegistry.Instance.TransferBinding(explicitItem.InstanceId, explicitSource, unexpectedOwner));
            Check(
                wrongExpectedRejected && IsBoundTo(explicitItem.InstanceId, explicitTarget),
                "Explicit ownership transition must be target-idempotent and reject a wrong expected source without overwrite.",
                failures);

            var world = new DiagnosticGridOwner("world", resolver);
            var personal = new DiagnosticGridOwner("personal", resolver);
            ItemInstance worldItem = ItemInstance.CreateNew(identityDefinition);
            world.Seed(worldItem, 1);
            InventoryMutationResult pickup = GridStorageTransferService.TransferStackAuto(
                world,
                personal,
                worldItem.InstanceId,
                GridStorageTransferQuantityPolicy.Exact,
                default);
            bool pickupPresentationCanFinalize = pickup.Success && world.GridStorageEntries.Count == 0;
            Check(
                pickup.Success && pickup.DestinationInstanceId == worldItem.InstanceId &&
                pickupPresentationCanFinalize && personal.Contains(worldItem.InstanceId) &&
                IsBoundTo(worldItem.InstanceId, personal),
                "World-to-personal commit must preserve the ID, empty the world source and transfer direct ownership before presentation finalization.",
                failures);

            var equipmentSurface = new DiagnosticGridOwner("equipment surface", resolver);
            var diagnosticSlots = new HashSet<string>(StringComparer.Ordinal);
            InventoryMutationResult equipMove = personal.Backend.TransferTo(
                equipmentSurface.Backend,
                worldItem.InstanceId,
                1);
            if (equipMove.Success && equipMove.DestinationInstanceId == worldItem.InstanceId)
                diagnosticSlots.Add("hand_right");
            ItemOwnedStorageRegistry.Instance.TransferBinding(worldItem.InstanceId, personal, personal);
            Check(
                equipMove.Success && !personal.Contains(worldItem.InstanceId) &&
                equipmentSurface.Contains(worldItem.InstanceId) && diagnosticSlots.Contains("hand_right") &&
                IsBoundTo(worldItem.InstanceId, personal),
                "Personal-to-equipment storage must preserve the actor personal inventory as direct owner while slots are occupied.",
                failures);
            InventoryMutationResult unequipMove = equipmentSurface.Backend.TransferTo(
                personal.Backend,
                worldItem.InstanceId,
                1);
            if (unequipMove.Success)
                diagnosticSlots.Remove("hand_right");
            ItemOwnedStorageRegistry.Instance.TransferBinding(worldItem.InstanceId, personal, personal);
            Check(
                unequipMove.Success && diagnosticSlots.Count == 0 &&
                !equipmentSurface.Contains(worldItem.InstanceId) && personal.Contains(worldItem.InstanceId) &&
                IsBoundTo(worldItem.InstanceId, personal),
                "Equipment-to-personal storage must preserve the ID and actor owner while releasing slots.",
                failures);

            ItemInstance ownedContainer = ItemInstance.Rehydrate(ownedDefinition, TransitionOwnedId, 100);
            ownedContainer.AttachOwnedStorageUnregistered(storageProfile, resolver);
            bool ownedLayoutReady = ownedContainer.OwnedStorage.CompleteInitialContentLoad(out string ownedLayoutError);
            if (ownedLayoutReady)
                ownedContainer.RegisterAttachedOwnedStorage();
            personal.Seed(ownedContainer, 1);

            var container = new DiagnosticGridOwner("container", resolver);
            ItemInstance routedItem = ItemInstance.CreateNew(identityDefinition);
            container.Seed(routedItem, 1);
            InventoryMutationResult containerToPersonal = GridStorageTransferService.TransferStackAuto(
                container,
                personal,
                routedItem.InstanceId,
                default);
            InventoryMutationResult personalToOwned = GridStorageTransferService.TransferStackAuto(
                personal,
                ownedContainer.OwnedStorage,
                routedItem.InstanceId,
                default);
            bool ownedBound = IsBoundTo(routedItem.InstanceId, ownedContainer.OwnedStorage);
            InventoryMutationResult ownedToPersonal = GridStorageTransferService.TransferStackAuto(
                ownedContainer.OwnedStorage,
                personal,
                routedItem.InstanceId,
                default);
            Check(
                ownedLayoutReady && string.IsNullOrWhiteSpace(ownedLayoutError) &&
                containerToPersonal.Success && personalToOwned.Success && ownedBound &&
                ownedToPersonal.Success && personal.Contains(routedItem.InstanceId) &&
                IsBoundTo(routedItem.InstanceId, personal),
                "Container-to-personal-to-item-owned-to-personal commits must update direct ownership at every boundary.",
                failures);

            var canonicalProxyOwner = new DiagnosticGridOwner("canonical proxy owner", resolver);
            var proxySurface = new DiagnosticGridOwner("proxy surface", resolver, canonicalProxyOwner);
            var proxyTarget = new DiagnosticGridOwner("proxy target", resolver);
            ItemInstance proxyItem = ItemInstance.CreateNew(identityDefinition);
            proxySurface.Seed(proxyItem, 1, canonicalProxyOwner);
            InventoryMutationResult proxyTransfer = GridStorageTransferService.TransferStackAuto(
                proxySurface,
                proxyTarget,
                proxyItem.InstanceId,
                default);
            Check(
                proxyTransfer.Success && proxySurface.GridStorageEntries.Count == 0 &&
                proxyTarget.Contains(proxyItem.InstanceId) && IsBoundTo(proxyItem.InstanceId, proxyTarget),
                "A delegated storage surface must reconcile from its canonical direct owner without creating mixed bindings.",
                failures);

            var mergeSource = new DiagnosticGridOwner("merge source", resolver);
            var mergeTarget = new DiagnosticGridOwner("merge target", resolver);
            ItemInstance consumedMergeItem = ItemInstance.CreateNew(stackDefinition);
            ItemInstance survivingMergeItem = ItemInstance.CreateNew(stackDefinition);
            mergeSource.Seed(consumedMergeItem, 1);
            mergeTarget.Seed(survivingMergeItem, 9);
            InventoryMutationResult committedMerge = GridStorageTransferService.MergeIntoTarget(
                mergeSource,
                consumedMergeItem.InstanceId,
                mergeTarget,
                survivingMergeItem.InstanceId,
                default);
            Check(
                committedMerge.Success && mergeSource.GridStorageEntries.Count == 0 &&
                mergeTarget.QuantityOf(survivingMergeItem.InstanceId) == 10 &&
                IsBoundTo(survivingMergeItem.InstanceId, mergeTarget) &&
                !ItemOwnedStorageRegistry.Instance.TryGetDirectOwner(consumedMergeItem.InstanceId, out _) &&
                !ItemInstanceIdRegistry.Instance.IsActive(consumedMergeItem.InstanceId),
                "Full merge must preserve the destination owner and retire the consumed source identity.",
                failures);

            var splitSource = new DiagnosticGridOwner("split source", resolver);
            var splitTarget = new DiagnosticGridOwner("split target", resolver);
            ItemInstance splitItem = ItemInstance.CreateNew(stackDefinition);
            splitSource.Seed(splitItem, 2);
            InventoryMutationResult committedSplit = GridStorageTransferService.TransferQuantityAuto(
                splitSource,
                splitTarget,
                splitItem.InstanceId,
                1,
                true,
                default);
            Check(
                committedSplit.Success && splitSource.QuantityOf(splitItem.InstanceId) == 1 &&
                committedSplit.DestinationInstanceId != splitItem.InstanceId &&
                IsBoundTo(splitItem.InstanceId, splitSource) &&
                IsBoundTo(committedSplit.DestinationInstanceId, splitTarget),
                "Split must retain the source owner and bind the new sibling only to the target.",
                failures);

            var rollbackSource = new DiagnosticGridOwner("rollback source", resolver);
            var rollbackTarget = new DiagnosticGridOwner("rollback target", resolver);
            var thirdOwner = new DiagnosticGridOwner("third owner", resolver);
            ItemInstance rollbackItem = ItemInstance.CreateNew(identityDefinition);
            rollbackSource.Seed(rollbackItem, 1, thirdOwner);
            bool rollbackPresentationFinalized = false;
            InventoryMutationResult rolledBack = GridStorageTransferService.TransferStackAuto(
                rollbackSource,
                rollbackTarget,
                rollbackItem.InstanceId,
                default);
            if (rolledBack.Success)
                rollbackPresentationFinalized = true;
            Check(
                rolledBack.Status == InventoryMutationResult.MutationStatus.RolledBack &&
                rollbackSource.Contains(rollbackItem.InstanceId) && rollbackTarget.GridStorageEntries.Count == 0 &&
                IsBoundTo(rollbackItem.InstanceId, thirdOwner) &&
                ItemInstanceIdRegistry.Instance.IsActive(rollbackItem.InstanceId) &&
                !rollbackPresentationFinalized,
                "Ownership rejection must roll back both storages and IDs without finalizing source presentation.",
                failures);

            var missingOwnerSource = new DiagnosticGridOwner("missing-owner source", resolver);
            var missingOwnerTarget = new DiagnosticGridOwner("missing-owner target", resolver);
            ItemInstance missingOwnerItem = ItemInstance.CreateNew(identityDefinition);
            missingOwnerSource.Seed(missingOwnerItem, 1);
            ItemOwnedStorageRegistry.Instance.UnbindItem(missingOwnerItem.InstanceId);
            InventoryMutationResult missingOwnerRollback = GridStorageTransferService.TransferStackAuto(
                missingOwnerSource,
                missingOwnerTarget,
                missingOwnerItem.InstanceId,
                default);
            Check(
                missingOwnerRollback.Status == InventoryMutationResult.MutationStatus.RolledBack &&
                missingOwnerSource.Contains(missingOwnerItem.InstanceId) &&
                missingOwnerTarget.GridStorageEntries.Count == 0 &&
                !ItemOwnedStorageRegistry.Instance.TryGetDirectOwner(missingOwnerItem.InstanceId, out _),
                "A missing source binding must reject and roll back instead of being silently consumed.",
                failures);

            var equipmentRollbackSource = new DiagnosticGridOwner("equipment rollback source", resolver);
            var equipmentRollbackTarget = new DiagnosticGridOwner("equipment rollback target", resolver);
            ItemInstance equipmentRollbackItem = ItemInstance.CreateNew(identityDefinition);
            equipmentRollbackSource.Seed(equipmentRollbackItem, 1);
            GridInventoryBackend.BackendStateSnapshot rollbackSourceSnapshot =
                equipmentRollbackSource.Backend.CaptureBackendState();
            GridInventoryBackend.BackendStateSnapshot rollbackTargetSnapshot =
                equipmentRollbackTarget.Backend.CaptureBackendState();
            bool rollbackSlotOccupied = false;
            bool equipmentRollbackRejected = false;
            bool rollbackOwnershipTransferred = false;
            using (ItemInstanceIdRegistry.ItemInstanceIdReservationScope rollbackIdentityScope =
                   ItemInstanceIdRegistry.Instance.BeginReservationScope())
            {
                try
                {
                    InventoryMutationResult equipmentMove = equipmentRollbackSource.Backend.TransferTo(
                        equipmentRollbackTarget.Backend,
                        equipmentRollbackItem.InstanceId,
                        1);
                    if (!equipmentMove.Success)
                        throw new InvalidOperationException(equipmentMove.Message ?? "Diagnostic equipment move failed.");

                    rollbackSlotOccupied = true;
                    ItemOwnedStorageRegistry.Instance.TransferBinding(
                        equipmentRollbackItem.InstanceId,
                        equipmentRollbackSource,
                        equipmentRollbackTarget);
                    rollbackOwnershipTransferred = true;
                    throw new InvalidOperationException("Forced diagnostic failure after ownership transition.");
                }
                catch
                {
                    equipmentRollbackSource.Backend.RestoreBackendState(rollbackSourceSnapshot);
                    equipmentRollbackTarget.Backend.RestoreBackendState(rollbackTargetSnapshot);
                    if (rollbackOwnershipTransferred)
                    {
                        ItemOwnedStorageRegistry.Instance.TransferBinding(
                            equipmentRollbackItem.InstanceId,
                            equipmentRollbackTarget,
                            equipmentRollbackSource);
                    }
                    rollbackSlotOccupied = false;
                    equipmentRollbackRejected = true;
                }
            }
            Check(
                equipmentRollbackRejected && rollbackOwnershipTransferred && !rollbackSlotOccupied &&
                equipmentRollbackSource.Contains(equipmentRollbackItem.InstanceId) &&
                equipmentRollbackTarget.GridStorageEntries.Count == 0 &&
                IsBoundTo(equipmentRollbackItem.InstanceId, equipmentRollbackSource),
                "Forced equipment-transition rollback must restore storages, slots and the previous direct owner.",
                failures);
        }

        private static bool IsBoundTo(string instanceId, object expectedOwner)
        {
            return ItemOwnedStorageRegistry.Instance.TryGetDirectOwner(instanceId, out object directOwner) &&
                   ReferenceEquals(directOwner, expectedOwner);
        }

        private static ItemDefinition CreateDefinition(
            string id,
            int conditionMax,
            int maxStack,
            string ownedStorageProfileId)
        {
            return new ItemDefinition
            {
                type = "item",
                id = id,
                max_stack = maxStack,
                physical = new ItemPhysical
                {
                    condition_max = conditionMax,
                    weight_kg = 0f
                },
                owned_storage_profile_id = ownedStorageProfileId
            };
        }

        private static bool Throws(Action action)
        {
            try
            {
                action();
                return false;
            }
            catch
            {
                return true;
            }
        }

        private static void Check(bool condition, string failure, List<string> failures)
        {
            if (!condition)
                failures.Add(failure);
        }

        private sealed class DiagnosticGridOwner : IGridStorageOwner, IGridStorageTransferEndpoint,
            IGridStorageDirectOwnerProvider
        {
            private readonly ItemStorage storage = new ItemStorage();
            private readonly Func<string, ItemDefinition> definitionResolver;
            private readonly object directItemOwner;

            internal DiagnosticGridOwner(
                string displayName,
                Func<string, ItemDefinition> definitionResolver,
                object directItemOwner = null)
            {
                GridStorageDisplayName = displayName;
                this.definitionResolver = definitionResolver;
                this.directItemOwner = directItemOwner;
                Backend = new GridInventoryBackend(storage, definitionResolver);
            }

            internal GridInventoryBackend Backend { get; }
            public string GridStorageDisplayName { get; }
            public IReadOnlyList<ItemStorageEntry> GridStorageEntries => storage.Entries;
            public bool UsesGridLayout => false;
            public int GridWidth => 0;
            public int GridHeight => 0;
            public int ConfiguredGridWidth => 0;
            public int ConfiguredGridHeight => 0;
            public GridStorageInitializationState GridInitializationState => GridStorageInitializationState.Disabled;
            public string GridInitializationError => null;
            GridInventoryBackend IGridStorageTransferEndpoint.TransferBackend => Backend;
            object IGridStorageDirectOwnerProvider.DirectItemOwner => directItemOwner ?? this;

            internal void Seed(ItemInstance item, int quantity, object bindingOwner = null)
            {
                storage.AddItemAsSeparateEntry(item, quantity);
                ItemOwnedStorageRegistry.Instance.BindItem(item, bindingOwner ?? this);
            }

            internal bool Contains(string instanceId)
            {
                return storage.GetEntryByInstanceId(instanceId)?.Item != null;
            }

            internal int QuantityOf(string instanceId)
            {
                return storage.GetEntryByInstanceId(instanceId)?.Quantity ?? 0;
            }

            public bool TryGetEntryByInstanceId(string instanceId, out int index, out ItemStorageEntry entry)
            {
                index = storage.GetEntryIndexByInstanceId(instanceId);
                entry = index >= 0 ? storage.GetEntry(index) : null;
                return entry?.Item != null;
            }

            public bool TryGetGridPlacement(string instanceId, out GridPlacement placement)
            {
                placement = null;
                return false;
            }

            public bool TryGetGridFootprint(string definitionId, out GridFootprint footprint, out bool usedFallback)
            {
                return GridFootprint.TryResolve(definitionResolver?.Invoke(definitionId), out footprint, out usedFallback, out _);
            }

            public GridPlacementValidationResult PreviewGridPlacementMove(string instanceId, int x, int y, bool isRotated)
            {
                return GridPlacementValidationResult.Invalid(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    "Diagnostic storage is linear.");
            }

            public InventoryMutationResult MoveGridPlacement(string instanceId, int x, int y, bool isRotated)
            {
                return InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.InvalidArguments,
                    "Diagnostic storage is linear.",
                    0,
                    instanceId);
            }

            public bool IsInstanceEquipped(string instanceId)
            {
                return false;
            }

            bool IGridStorageTransferEndpoint.CanTransferOut(GridStorageTransferContext context, out string reason)
            {
                reason = null;
                return true;
            }

            bool IGridStorageTransferEndpoint.CanTransferIn(GridStorageTransferContext context, out string reason)
            {
                reason = null;
                return true;
            }

            void IGridStorageTransferEndpoint.OnTransferCommittedOut(
                GridStorageTransferReceipt receipt,
                GridStorageTransferContext context)
            {
            }

            void IGridStorageTransferEndpoint.OnTransferCommittedIn(
                GridStorageTransferReceipt receipt,
                GridStorageTransferContext context)
            {
            }
        }
    }
}
