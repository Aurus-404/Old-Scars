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

            ItemDefinition item = GetItemDefinition(entry);
            if (!HasConsumableEffects(item))
            {
                return InventoryItemUseResult.Failed("Item is not consumable.");
            }

            ItemNeedRestore[] restoreNeeds = item.consumable != null ? item.consumable.restore_needs : null;
            ItemHealthRestore restoreHealth = item.consumable != null ? item.consumable.restore_health : null;
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

            if (!inventory.TryRemoveItemAt(itemIndex, 1))
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
                    RecordItemUsed(inventory, item, restore, afterValue);
                    appliedCount++;
                }
            }

            if (canRestoreHealth)
            {
                float beforeValue = actorHealth.CurrentHealth;
                if (actorHealth.Heal(restoreHealth.amount))
                {
                    RecordHealthItemUsed(inventory, item, beforeValue, actorHealth.CurrentHealth, actorHealth.MaxHealth);
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

        private static bool HasConsumableEffects(ItemDefinition item)
        {
            if (item?.consumable == null)
                return false;

            bool hasNeedRestore = item.consumable.restore_needs != null && item.consumable.restore_needs.Length > 0;
            bool hasHealthRestore = item.consumable.restore_health != null && item.consumable.restore_health.amount > 0f;
            return hasNeedRestore || hasHealthRestore;
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
