using OldScars.Core.Actors;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Feedback;
using System.Collections.Generic;

namespace OldScars.Core.Items
{
    public static class InventoryItemUseService
    {
        public static bool IsConsumable(ItemStorageEntry entry)
        {
            ItemDefinition item = GetItemDefinition(entry);
            return HasConsumableEffects(item);
        }

        public static InventoryItemUseResult TryUseItem(InventoryComponent inventory, int itemIndex, ActorNeedsComponent actorNeeds)
        {
            return TryUseItem(inventory, itemIndex, actorNeeds, null);
        }

        public static InventoryItemUseResult TryUseItem(InventoryComponent inventory, int itemIndex, ActorNeedsComponent actorNeeds, ActorHealthComponent actorHealth)
        {
            if (inventory == null)
            {
                return InventoryItemUseResult.Failed("No inventory available.");
            }

            ItemStorageEntry entry = inventory.GetEntry(itemIndex);
            if (entry == null || entry.Item == null)
            {
                return InventoryItemUseResult.Failed("No item at that inventory slot.");
            }

            return TryUseEntry(
                entry,
                () => inventory.TryRemoveItemAt(itemIndex, 1),
                inventory,
                actorNeeds,
                actorHealth);
        }

        public static InventoryItemUseResult TryUseItem(
            IGridStorageOwner sourceOwner,
            string instanceId,
            InventoryComponent actorInventory,
            ActorNeedsComponent actorNeeds,
            ActorHealthComponent actorHealth)
        {
            if (sourceOwner == null || !(sourceOwner is IGridStorageTransferEndpoint endpoint) ||
                !sourceOwner.TryGetEntryByInstanceId(instanceId, out _, out ItemStorageEntry entry) ||
                entry?.Item == null)
            {
                return InventoryItemUseResult.Failed("No item available in that personal compartment.");
            }

            if (!ItemOwnedStorageRegistry.Instance.ShareRootOwner(sourceOwner, actorInventory))
                return InventoryItemUseResult.Failed("El objeto ya no pertenece al actor.");

            return TryUseEntry(
                entry,
                () => RemoveOne(endpoint, sourceOwner, instanceId),
                actorInventory,
                actorNeeds,
                actorHealth);
        }

        public static InventoryItemUseResult TryUseExternalItem(
            IGridStorageOwner sourceOwner,
            string instanceId,
            InventoryComponent actorInventory,
            ActorNeedsComponent actorNeeds,
            ActorHealthComponent actorHealth,
            GridStorageTransferContext context)
        {
            if (sourceOwner == null || !(sourceOwner is IGridStorageTransferEndpoint endpoint) ||
                !sourceOwner.TryGetEntryByInstanceId(instanceId, out _, out ItemStorageEntry entry) ||
                entry?.Item == null)
            {
                return InventoryItemUseResult.Failed("No item available in that external storage.");
            }

            return TryUseEntry(
                entry,
                () => RemoveOne(endpoint, sourceOwner, instanceId, entry.DefinitionId, context),
                actorInventory,
                actorNeeds,
                actorHealth);
        }

        public static int GetAvailableWoundTreatmentQuantity(ActorItemOwnershipComponent ownership)
        {
            if (ownership == null || ownership.PersonalInventory == null)
                return 0;

            int total = 0;
            IReadOnlyList<ItemStorageEntry> entries = ownership.GetAllOwnedEntries();
            for (int index = 0; index < entries.Count; index++)
            {
                ItemStorageEntry entry = entries[index];
                if (!TryResolveOwnedTreatment(ownership, entry, out _, out _, out _))
                    continue;
                total += entry.Quantity;
            }
            return total;
        }

        public static InventoryItemUseResult TryApplyWoundTreatment(
            ActorItemOwnershipComponent ownership,
            ActorMedicalStateComponent medicalState,
            string woundId)
        {
            if (ownership == null || ownership.PersonalInventory == null)
                return InventoryItemUseResult.Failed("Actor item ownership is unavailable.");
            if (medicalState == null)
                return InventoryItemUseResult.Failed("Actor medical state is unavailable.");

            IReadOnlyList<ItemStorageEntry> entries = ownership.GetAllOwnedEntries();
            for (int index = 0; index < entries.Count; index++)
            {
                ItemStorageEntry entry = entries[index];
                if (!TryResolveOwnedTreatment(
                        ownership,
                        entry,
                        out ItemDefinition item,
                        out IGridStorageOwner owner,
                        out IGridStorageTransferEndpoint endpoint))
                {
                    continue;
                }

                ItemWoundTreatment treatment = item.consumable.wound_treatment;
                if (!medicalState.CanApplyBandage(woundId, treatment.bleeding_multiplier, out string failure))
                    return InventoryItemUseResult.Failed(failure);

                ActorMedicalStateData rollback = medicalState.CaptureState();
                int rollbackRevision = medicalState.Revision;
                if (!medicalState.TryApplyBandage(woundId, treatment.bleeding_multiplier, out failure))
                    return InventoryItemUseResult.Failed(failure);

                if (!RemoveOne(endpoint, owner, entry.Item.InstanceId, entry.DefinitionId, default))
                {
                    if (!medicalState.TryRestoreTransactionState(rollback, rollbackRevision, out string rollbackFailure))
                    {
                        UnityEngine.Debug.LogError(
                            $"[Medicine][ROLLBACK_FAILED] Actor: {medicalState.name}; WoundId: {woundId}; " +
                            $"ItemInstanceId: {entry.Item.InstanceId}; Failure: {rollbackFailure ?? "<UNKNOWN>"}");
                    }
                    return InventoryItemUseResult.Failed("Could not consume one treatment item; wound treatment was rolled back.");
                }

                string displayName = GetItemDisplayName(item);
                GameplayFeedbackLog.TryRecord(new GameplayFeedbackEntry(
                    GameplayFeedbackEntryType.ItemUsed,
                    $"Aplicaste {displayName} a una herida.",
                    actorId: ownership.name,
                    actorDisplayName: ownership.name,
                    itemId: item.id,
                    itemDisplayName: displayName,
                    quantity: 1,
                    debugOnly: false));
                return InventoryItemUseResult.Succeeded($"Applied {displayName}; bleeding reduced.");
            }

            return InventoryItemUseResult.Failed("No compatible wound-treatment item is owned by the actor.");
        }

        private static bool RemoveOne(
            IGridStorageTransferEndpoint endpoint,
            IGridStorageOwner owner,
            string instanceId)
        {
            return RemoveOne(endpoint, owner, instanceId, null, default);
        }

        private static bool RemoveOne(
            IGridStorageTransferEndpoint endpoint,
            IGridStorageOwner owner,
            string instanceId,
            string definitionId,
            GridStorageTransferContext context)
        {
            InventoryMutationResult result = endpoint.TransferBackend.Remove(instanceId, 1);
            if (!result.Success)
                return false;

            if (!owner.TryGetEntryByInstanceId(instanceId, out _, out _))
                ItemOwnedStorageRegistry.Instance.UnbindItem(instanceId);

            if (!string.IsNullOrWhiteSpace(definitionId))
                endpoint.OnTransferCommittedOut(new GridStorageTransferReceipt(definitionId, result), context);
            return true;
        }

        private static InventoryItemUseResult TryUseEntry(
            ItemStorageEntry entry,
            System.Func<bool> removeOne,
            InventoryComponent actorInventory,
            ActorNeedsComponent actorNeeds,
            ActorHealthComponent actorHealth)
        {

            ItemDefinition item = GetItemDefinition(entry);
            if (!HasConsumableEffects(item))
            {
                return InventoryItemUseResult.Failed("Item is not consumable.");
            }

            ItemNeedRestore[] restoreNeeds = item.consumable != null ? item.consumable.restore_needs : null;
            ItemHealthRestore restoreHealth = item.consumable != null ? item.consumable.restore_health : null;
            ItemWoundTreatment woundTreatment = item.consumable != null ? item.consumable.wound_treatment : null;
            if (woundTreatment != null)
                return InventoryItemUseResult.Failed("Select a bleeding wound in the Health window before applying this treatment.");
            var restorableEffects = new List<RestorableNeedEffect>();

            if (actorNeeds != null && restoreNeeds != null)
            {
                for (int i = 0; i < restoreNeeds.Length; i++)
                {
                    ItemNeedRestore restore = restoreNeeds[i];
                    if (restore == null || string.IsNullOrWhiteSpace(restore.need_id) || restore.amount <= 0f)
                    {
                        continue;
                    }

                    if (!actorNeeds.HasNeed(restore.need_id))
                    {
                        continue;
                    }

                    if (actorNeeds.CanRestoreNeed(restore.need_id, restore.amount))
                    {
                        restorableEffects.Add(new RestorableNeedEffect(
                            restore.need_id,
                            actorNeeds.GetNeedDisplayName(restore.need_id),
                            restore.amount,
                            actorNeeds.GetNeedValue(restore.need_id),
                            actorNeeds.GetNeedMaxValue(restore.need_id)));
                    }
                }
            }

            bool hasHealthRestore = restoreHealth != null && restoreHealth.amount > 0f;
            bool canRestoreHealth = hasHealthRestore && actorHealth != null && actorHealth.CanHeal(restoreHealth.amount);

            if (restorableEffects.Count == 0 && !canRestoreHealth && !HasAnyMatchingEffect(actorNeeds, restoreNeeds, actorHealth, hasHealthRestore))
            {
                return InventoryItemUseResult.Failed("Actor has no matching need or health target for this item.");
            }

            if (restorableEffects.Count == 0 && !canRestoreHealth)
            {
                return InventoryItemUseResult.Failed("Matching needs or health are already full.");
            }

            if (removeOne == null || !removeOne())
            {
                return InventoryItemUseResult.Failed("Could not consume item quantity.");
            }

            int appliedCount = 0;
            for (int i = 0; i < restorableEffects.Count; i++)
            {
                RestorableNeedEffect restore = restorableEffects[i];
                if (actorNeeds.TryRestoreNeed(restore.NeedId, restore.Amount))
                {
                    float afterValue = actorNeeds.GetNeedValue(restore.NeedId);
                    RecordItemUsed(actorInventory, item, restore, afterValue);
                    appliedCount++;
                }
            }

            if (canRestoreHealth)
            {
                float beforeValue = actorHealth.CurrentHealth;
                if (actorHealth.Heal(restoreHealth.amount))
                {
                    RecordHealthItemUsed(actorInventory, item, beforeValue, actorHealth.CurrentHealth, actorHealth.MaxHealth);
                    appliedCount++;
                }
            }

            if (appliedCount == 0)
            {
                return InventoryItemUseResult.Failed("No need or health was restored.");
            }

            string displayName = item.display != null && !string.IsNullOrWhiteSpace(item.display.name)
                ? item.display.name
                : entry.DefinitionId;
            return InventoryItemUseResult.Succeeded($"Used {displayName}.");
        }

        private static bool HasAnyMatchingEffect(ActorNeedsComponent actorNeeds, ItemNeedRestore[] restoreNeeds, ActorHealthComponent actorHealth, bool hasHealthRestore)
        {
            if (hasHealthRestore && actorHealth != null)
                return true;

            if (actorNeeds != null && restoreNeeds != null)
            {
                for (int i = 0; i < restoreNeeds.Length; i++)
                {
                    ItemNeedRestore restore = restoreNeeds[i];
                    if (restore != null && actorNeeds.HasNeed(restore.need_id))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static ItemDefinition GetItemDefinition(ItemStorageEntry entry)
        {
            if (entry == null || entry.Item == null || GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
            {
                return null;
            }

            GameDatabase database = GameDataManager.Instance.Database;
            return database != null ? database.GetItem(entry.DefinitionId) : null;
        }

        private static bool TryResolveOwnedTreatment(
            ActorItemOwnershipComponent ownership,
            ItemStorageEntry entry,
            out ItemDefinition item,
            out IGridStorageOwner owner,
            out IGridStorageTransferEndpoint endpoint)
        {
            item = GetItemDefinition(entry);
            owner = null;
            endpoint = null;
            if (entry?.Item == null || entry.Quantity < 1 ||
                item?.consumable?.wound_treatment == null ||
                item.consumable.wound_treatment.type != ItemWoundTreatmentTypes.Bandage ||
                !ItemOwnedStorageRegistry.Instance.TryGetDirectOwner(entry.Item.InstanceId, out object directOwner) ||
                !(directOwner is IGridStorageOwner directStorage) ||
                !(directOwner is IGridStorageTransferEndpoint directEndpoint) ||
                !directStorage.TryGetEntryByInstanceId(entry.Item.InstanceId, out _, out ItemStorageEntry ownedEntry) ||
                !ReferenceEquals(ownedEntry, entry) ||
                !ItemOwnedStorageRegistry.Instance.ShareRootOwner(directStorage, ownership.PersonalInventory))
            {
                return false;
            }

            owner = directStorage;
            endpoint = directEndpoint;
            return true;
        }

        private static bool HasConsumableEffects(ItemDefinition item)
        {
            if (item?.consumable == null)
                return false;

            bool hasNeedRestore = item.consumable.restore_needs != null && item.consumable.restore_needs.Length > 0;
            bool hasHealthRestore = item.consumable.restore_health != null && item.consumable.restore_health.amount > 0f;
            bool hasWoundTreatment = item.consumable.wound_treatment != null;
            return hasNeedRestore || hasHealthRestore || hasWoundTreatment;
        }

        private static void RecordItemUsed(InventoryComponent inventory, ItemDefinition item, RestorableNeedEffect restore, float afterValue)
        {
            string itemDisplayName = GetItemDisplayName(item);
            GameplayFeedbackLog.TryRecord(new GameplayFeedbackEntry(
                GameplayFeedbackEntryType.ItemUsed,
                $"Usaste {itemDisplayName}.",
                actorId: inventory != null ? inventory.name : null,
                actorDisplayName: inventory != null ? inventory.name : null,
                itemId: item != null ? item.id : null,
                itemDisplayName: itemDisplayName,
                quantity: 1,
                needId: restore.NeedId,
                needDisplayName: restore.NeedDisplayName,
                needAmount: afterValue - restore.BeforeValue,
                needValueBefore: restore.BeforeValue,
                needValueAfter: afterValue,
                needMaxValue: restore.MaxValue));
        }

        private static void RecordHealthItemUsed(InventoryComponent inventory, ItemDefinition item, float beforeValue, float afterValue, float maxValue)
        {
            string itemDisplayName = GetItemDisplayName(item);
            GameplayFeedbackLog.TryRecord(new GameplayFeedbackEntry(
                GameplayFeedbackEntryType.ItemUsed,
                $"Usaste {itemDisplayName}.",
                actorId: inventory != null ? inventory.name : null,
                actorDisplayName: inventory != null ? inventory.name : null,
                itemId: item != null ? item.id : null,
                itemDisplayName: itemDisplayName,
                quantity: 1,
                needId: "health",
                needDisplayName: "Health",
                needAmount: afterValue - beforeValue,
                needValueBefore: beforeValue,
                needValueAfter: afterValue,
                needMaxValue: maxValue));
        }

        private static string GetItemDisplayName(ItemDefinition item)
        {
            if (item == null)
            {
                return "(none)";
            }

            return item.display != null && !string.IsNullOrWhiteSpace(item.display.name)
                ? item.display.name
                : item.id;
        }

        private readonly struct RestorableNeedEffect
        {
            public RestorableNeedEffect(string needId, string needDisplayName, float amount, float beforeValue, float maxValue)
            {
                NeedId = needId;
                NeedDisplayName = needDisplayName;
                Amount = amount;
                BeforeValue = beforeValue;
                MaxValue = maxValue;
            }

            public readonly string NeedId;
            public readonly string NeedDisplayName;
            public readonly float Amount;
            public readonly float BeforeValue;
            public readonly float MaxValue;
        }
    }
}
