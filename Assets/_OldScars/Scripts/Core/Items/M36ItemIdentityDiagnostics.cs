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
    }
}
