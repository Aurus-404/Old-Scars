using System.Collections.Generic;
using System.Text.RegularExpressions;
using OldScars.Core.Actions;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Data.Loading;

namespace OldScars.Core.Data.Validation
{
    /// <summary>
    /// Validates loaded data after GameDataLoader finishes.
    ///
    /// This protects the project from broken references, typos and invalid data
    /// before gameplay systems start using definitions.
    /// </summary>
    public sealed class DataValidator
    {
        private static readonly Regex SnakeCasePattern = new Regex("^[a-z0-9_]+$", RegexOptions.Compiled);
        private const string EffectTargetTarget = "target";
        private const string LegacyRightHandSlotId = "right_hand";
        private const string HandRightSlotId = "hand_right";
        private const int MaxItemStorageDimension = 64;
        private static readonly HashSet<string> RuntimeHealthTags = new HashSet<string>
        {
            "alive_actor",
            "damaged_actor",
            "low_health_actor",
            "dead_actor",
            "lootable_actor"
        };
        private static readonly HashSet<string> RuntimeWorldObjectProfileTags = new HashSet<string>
        {
            "looted_container",
            "opened_container",
            "forced_open",
            "dead_actor",
            "lootable_actor",
            "damaged_actor",
            "low_health_actor"
        };

        private readonly GameDatabase database;
        private readonly TagRegistry tags;
        private readonly DataLoadReport report;

        public DataValidator(GameDatabase database, TagRegistry tags, DataLoadReport report)
        {
            this.database = database;
            this.tags = tags;
            this.report = report;
        }

        public void Validate()
        {
            ValidateEquipmentSlots();
            ValidateEquipmentLayouts();
            ValidateActions();
            ValidateWeaponProfiles();
            ValidateFirearmProfiles();
            ValidateAmmoProfiles();
            ValidateItemStorageProfiles();
            ValidateItems();
            ValidateLootTables();
            ValidateActorProfiles();
            ValidateWorldObjectProfiles();
        }

        private void ValidateEquipmentSlots()
        {
            foreach (EquipmentSlotDefinition slot in database.GetAllEquipmentSlots())
            {
                string ctx = $"EquipmentSlot '{SafeId(slot != null ? slot.id : null)}'";
                if (slot == null)
                {
                    report.Error("EquipmentSlot: null equipment slot definition loaded.");
                    continue;
                }

                RequireType(slot.type, "equipment_slot", ctx);
                RequireSnakeCase(slot.id, "id", ctx);
                if (string.IsNullOrWhiteSpace(slot.display_name))
                    report.Error($"{ctx}: 'display_name' is required.");
            }
        }

        private void ValidateEquipmentLayouts()
        {
            foreach (EquipmentLayoutDefinition layout in database.GetAllEquipmentLayouts())
            {
                string ctx = $"EquipmentLayout '{SafeId(layout != null ? layout.id : null)}'";
                if (layout == null)
                {
                    report.Error("EquipmentLayout: null equipment layout definition loaded.");
                    continue;
                }

                RequireType(layout.type, "equipment_layout", ctx);
                RequireSnakeCase(layout.id, "id", ctx);
                if (string.IsNullOrWhiteSpace(layout.display_name))
                    report.Error($"{ctx}: 'display_name' is required.");

                var groupIds = new HashSet<string>();
                var groupOrders = new HashSet<int>();
                if (layout.groups == null || layout.groups.Length == 0)
                {
                    report.Error($"{ctx}: 'groups' is required and must not be empty.");
                }
                else
                {
                    for (int index = 0; index < layout.groups.Length; index++)
                    {
                        EquipmentLayoutGroupDefinition group = layout.groups[index];
                        string groupCtx = $"{ctx}: groups[{index}]";
                        if (group == null)
                        {
                            report.Error($"{groupCtx} must not be null.");
                            continue;
                        }

                        RequireSnakeCase(group.id, "id", groupCtx);
                        if (!groupIds.Add(group.id))
                            report.Error($"{ctx}: duplicate group id '{SafeId(group.id)}'.");
                        if (string.IsNullOrWhiteSpace(group.display_name))
                            report.Error($"{groupCtx}: 'display_name' is required.");
                        if (group.display_order < 0)
                            report.Error($"{groupCtx}: 'display_order' must be >= 0.");
                        if (!groupOrders.Add(group.display_order))
                            report.Error($"{ctx}: duplicate group display_order '{group.display_order}'.");
                    }
                }

                var slotIds = new HashSet<string>();
                var slotOrdersByGroup = new Dictionary<string, HashSet<int>>();
                if (layout.slots == null || layout.slots.Length == 0)
                {
                    report.Error($"{ctx}: 'slots' is required and must not be empty.");
                    continue;
                }

                for (int index = 0; index < layout.slots.Length; index++)
                {
                    EquipmentLayoutSlotDefinition slot = layout.slots[index];
                    string slotCtx = $"{ctx}: slots[{index}]";
                    if (slot == null)
                    {
                        report.Error($"{slotCtx} must not be null.");
                        continue;
                    }

                    RequireSnakeCase(slot.slot_id, "slot_id", slotCtx);
                    RequireSnakeCase(slot.group_id, "group_id", slotCtx);
                    if (database.GetEquipmentSlot(slot.slot_id) == null)
                        report.Error($"{slotCtx}: slot_id references '{SafeId(slot.slot_id)}' which was not loaded.");
                    if (!groupIds.Contains(slot.group_id))
                        report.Error($"{slotCtx}: group_id references '{SafeId(slot.group_id)}' which is not declared by this layout.");
                    if (!slotIds.Add(slot.slot_id))
                        report.Error($"{ctx}: duplicate slot_id '{SafeId(slot.slot_id)}'.");
                    if (slot.display_order < 0)
                        report.Error($"{slotCtx}: 'display_order' must be >= 0.");

                    if (!slotOrdersByGroup.TryGetValue(slot.group_id ?? string.Empty, out HashSet<int> orders))
                    {
                        orders = new HashSet<int>();
                        slotOrdersByGroup[slot.group_id ?? string.Empty] = orders;
                    }
                    if (!orders.Add(slot.display_order))
                        report.Error($"{slotCtx}: duplicate display_order '{slot.display_order}' inside group '{SafeId(slot.group_id)}'.");
                }
            }
        }

        private void ValidateWorldObjectProfiles()
        {
            foreach (WorldObjectProfileDefinition worldObjectProfile in database.GetAllWorldObjectProfiles())
            {
                string ctx = $"WorldObjectProfile '{SafeId(worldObjectProfile != null ? worldObjectProfile.id : null)}'";

                if (worldObjectProfile == null)
                {
                    report.Error("WorldObjectProfile: null world object profile definition loaded.");
                    continue;
                }

                RequireType(worldObjectProfile.type, "world_object_profile", ctx);
                RequireSnakeCase(worldObjectProfile.id, "id", ctx);

                if (string.IsNullOrWhiteSpace(worldObjectProfile.display_name))
                    report.Error($"{ctx}: 'display_name' is required.");

                ValidateWorldObjectProfileInitialTags(worldObjectProfile.initial_tags, $"{ctx}: initial_tags");

                if (worldObjectProfile.loot_table_id != null)
                    report.Error($"{ctx}: 'loot_table_id' is not supported in World Object Profile v0.");
            }
        }

        private void ValidateWorldObjectProfileInitialTags(string[] initialTags, string context)
        {
            if (initialTags == null || initialTags.Length == 0)
            {
                report.Error($"{context} array is required and must not be empty.");
                return;
            }

            var seenTags = new HashSet<string>();

            for (int index = 0; index < initialTags.Length; index++)
            {
                string tag = initialTags[index];

                if (string.IsNullOrWhiteSpace(tag))
                {
                    report.Error($"{context}: contains an empty tag.");
                    continue;
                }

                if (!SnakeCasePattern.IsMatch(tag))
                    report.Error($"{context}: tag '{tag}' must use snake_case.");

                if (!tags.IsValid(tag))
                    report.Error($"{context}: tag '{tag}' is not registered in tags.json.");

                if (!seenTags.Add(tag))
                    report.Error($"{context}: duplicate tag '{tag}'.");

                if (RuntimeWorldObjectProfileTags.Contains(tag))
                    report.Error($"{context}: runtime state tag '{tag}' must not be declared in world object profile JSON.");
            }
        }

        private void ValidateActorProfiles()
        {
            var inventorySeedTags = new HashSet<string>();
            foreach (ActorProfileDefinition actorProfile in database.GetAllActorProfiles())
            {
                string ctx = $"ActorProfile '{SafeId(actorProfile != null ? actorProfile.id : null)}'";

                if (actorProfile == null)
                {
                    report.Error("ActorProfile: null actor profile definition loaded.");
                    continue;
                }

                RequireType(actorProfile.type, "actor_profile", ctx);
                RequireSnakeCase(actorProfile.id, "id", ctx);

                if (string.IsNullOrWhiteSpace(actorProfile.display_name))
                    report.Error($"{ctx}: 'display_name' is required.");

                if (!string.IsNullOrWhiteSpace(actorProfile.inventory_seed_actor_tag))
                {
                    RequireSnakeCase(actorProfile.inventory_seed_actor_tag, "inventory_seed_actor_tag", ctx);
                    if (!tags.IsValid(actorProfile.inventory_seed_actor_tag))
                        report.Error($"{ctx}: inventory_seed_actor_tag '{actorProfile.inventory_seed_actor_tag}' is not registered in tags.json.");
                    if (!inventorySeedTags.Add(actorProfile.inventory_seed_actor_tag))
                        report.Error($"{ctx}: inventory_seed_actor_tag '{actorProfile.inventory_seed_actor_tag}' is already used by another actor profile.");
                    if (!ContainsValue(actorProfile.initial_tags, actorProfile.inventory_seed_actor_tag))
                        report.Error($"{ctx}: inventory_seed_actor_tag '{actorProfile.inventory_seed_actor_tag}' must also appear in initial_tags.");
                    if (actorProfile.initial_inventory == null || actorProfile.initial_inventory.Length == 0)
                        report.Error($"{ctx}: inventory_seed_actor_tag requires a non-empty initial_inventory.");
                }

                ValidateActorProfileInitialTags(actorProfile.initial_tags, $"{ctx}: initial_tags");
                ValidateActorProfileHealth(actorProfile.health, $"{ctx}: health");
                ValidateActorProfileInventory(actorProfile.initial_inventory, $"{ctx}: initial_inventory");

                if (!string.IsNullOrWhiteSpace(actorProfile.equipment_layout_id))
                {
                    RequireSnakeCase(actorProfile.equipment_layout_id, "equipment_layout_id", ctx);
                    if (database.GetEquipmentLayout(actorProfile.equipment_layout_id) == null)
                        report.Error($"{ctx}: equipment_layout_id references '{actorProfile.equipment_layout_id}' which was not loaded.");
                }

                if (actorProfile.equipped != null)
                    report.Error($"{ctx}: 'equipped' is not supported yet in Milestone 24.2.");
            }
        }

        private void ValidateActorProfileInitialTags(string[] initialTags, string context)
        {
            if (initialTags == null || initialTags.Length == 0)
            {
                report.Error($"{context} array is required and must not be empty.");
                return;
            }

            var seenTags = new HashSet<string>();

            for (int index = 0; index < initialTags.Length; index++)
            {
                string tag = initialTags[index];

                if (string.IsNullOrWhiteSpace(tag))
                {
                    report.Error($"{context}: contains an empty tag.");
                    continue;
                }

                if (!SnakeCasePattern.IsMatch(tag))
                    report.Error($"{context}: tag '{tag}' must use snake_case.");

                if (!tags.IsValid(tag))
                    report.Error($"{context}: tag '{tag}' is not registered in tags.json.");

                if (!seenTags.Add(tag))
                    report.Error($"{context}: duplicate tag '{tag}'.");

                if (RuntimeHealthTags.Contains(tag))
                    report.Error($"{context}: runtime health tag '{tag}' must not be declared in actor profile JSON.");
            }
        }

        private void ValidateActorProfileHealth(ActorProfileHealth health, string context)
        {
            if (health == null)
            {
                report.Error($"{context} block is required.");
                return;
            }

            if (health.max_health <= 0f)
                report.Error($"{context}: 'max_health' must be > 0 (got {health.max_health}).");

            if (health.current_health < 0f)
                report.Error($"{context}: 'current_health' must be >= 0 (got {health.current_health}).");

            if (health.current_health > health.max_health)
                report.Error($"{context}: 'current_health' ({health.current_health}) must be <= 'max_health' ({health.max_health}).");
        }

        private void ValidateActorProfileInventory(ActorProfileInventoryEntry[] initialInventory, string context)
        {
            if (initialInventory == null || initialInventory.Length == 0)
                return;

            for (int index = 0; index < initialInventory.Length; index++)
                ValidateActorProfileInventoryEntry(initialInventory[index], $"{context}[{index}]");
        }

        private void ValidateActorProfileInventoryEntry(ActorProfileInventoryEntry entry, string context)
        {
            if (entry == null)
            {
                report.Error($"{context}: entry must not be null.");
                return;
            }

            if (string.IsNullOrWhiteSpace(entry.item_id))
            {
                report.Error($"{context}: 'item_id' is required.");
            }
            else
            {
                RequireSnakeCase(entry.item_id, "item_id", context);

                if (database.GetItem(entry.item_id) == null)
                    report.Error($"{context}: item_id '{entry.item_id}' references an item that was not loaded.");
            }

            if (entry.quantity <= 0)
                report.Error($"{context}: 'quantity' must be > 0 (got {entry.quantity}).");
        }

        private void ValidateLootTables()
        {
            foreach (LootTableDefinition lootTable in database.GetAllLootTables())
            {
                string ctx = $"LootTable '{SafeId(lootTable != null ? lootTable.id : null)}'";

                if (lootTable == null)
                {
                    report.Error("LootTable: null loot table definition loaded.");
                    continue;
                }

                RequireType(lootTable.type, "loot_table", ctx);
                RequireSnakeCase(lootTable.id, "id", ctx);

                if (lootTable.entries == null || lootTable.entries.Length == 0)
                {
                    report.Error($"{ctx}: 'entries' array is required and must not be empty.");
                    continue;
                }

                for (int index = 0; index < lootTable.entries.Length; index++)
                    ValidateLootTableEntry(lootTable.entries[index], $"{ctx}: entries[{index}]");
            }
        }

        private void ValidateLootTableEntry(LootTableEntryDefinition entry, string ctx)
        {
            if (entry == null)
            {
                report.Error($"{ctx}: entry must not be null.");
                return;
            }

            if (string.IsNullOrWhiteSpace(entry.item_id))
            {
                report.Error($"{ctx}: 'item_id' is required.");
            }
            else
            {
                RequireSnakeCase(entry.item_id, "item_id", ctx);

                if (database.GetItem(entry.item_id) == null)
                    report.Error($"{ctx}: item_id '{entry.item_id}' references an item that was not loaded.");
            }

            if (entry.count <= 0)
                report.Error($"{ctx}: 'count' must be > 0 (got {entry.count}).");
        }

        private void ValidateItems()
        {
            foreach (ItemDefinition item in database.GetAllItems())
            {
                string ctx = $"Item '{SafeId(item != null ? item.id : null)}'";

                if (item == null)
                {
                    report.Error("Item: null item definition loaded.");
                    continue;
                }

                RequireType(item.type, "item", ctx);
                RequireSnakeCase(item.id, "id", ctx);

                if (item.display == null)
                    report.Error($"{ctx}: 'display' block is required.");
                else if (string.IsNullOrWhiteSpace(item.display.name))
                    report.Error($"{ctx}: 'display.name' is required.");

                if (item.tags == null || item.tags.Length == 0)
                {
                    report.Error($"{ctx}: 'tags' array is required and must not be empty.");
                }
                else
                {
                    ValidateTagList(item.tags, $"{ctx}: tags");
                }

                if (item.max_stack < 1)
                    report.Error($"{ctx}: 'max_stack' must be >= 1 (got {item.max_stack}).");

                if (item.physical == null)
                {
                    report.Error($"{ctx}: 'physical' block is required.");
                }
                else
                {
                    if (!item.physical.weight_kg.HasValue)
                    {
                        report.Error($"{ctx}: required 'physical.weight_kg' is missing for item '{item.id}'.");
                    }
                    else
                    {
                        float weightKg = item.physical.weight_kg.Value;
                        if (float.IsNaN(weightKg) || float.IsInfinity(weightKg) || weightKg < 0f)
                            report.Error($"{ctx}: 'physical.weight_kg' must be finite and >= 0 for item '{item.id}' (got {weightKg}).");
                    }
                    if (item.physical.volume_l < 0)
                        report.Error($"{ctx}: 'physical.volume_l' must be >= 0 (got {item.physical.volume_l}).");
                    if (item.physical.condition_max <= 0)
                        report.Error($"{ctx}: 'physical.condition_max' must be > 0 (got {item.physical.condition_max}).");
                }

                if (item.economy != null)
                {
                    if (item.economy.base_buy_value < 0)
                        report.Error($"{ctx}: 'economy.base_buy_value' must be >= 0.");
                    if (item.economy.base_sell_value < 0)
                        report.Error($"{ctx}: 'economy.base_sell_value' must be >= 0.");
                }

                ValidateItemEquip(item, ctx);
                ValidateItemInventoryMetadata(item, ctx);
                ValidateItemOwnedStorage(item, ctx);

                if (item.combat != null)
                    ValidateItemCombat(item, ctx);

                if (item.consumable != null)
                    ValidateItemConsumable(item, ctx);

                ValidateItemFirearmAndAmmoProfiles(item, ctx);
            }
        }

        private void ValidateItemStorageProfiles()
        {
            foreach (ItemStorageProfileDefinition profile in database.GetAllItemStorageProfiles())
            {
                string ctx = $"ItemStorageProfile '{SafeId(profile != null ? profile.id : null)}'";
                if (profile == null)
                {
                    report.Error("ItemStorageProfile: null definition loaded.");
                    continue;
                }

                RequireType(profile.type, "item_storage_profile", ctx);
                RequireSnakeCase(profile.id, "id", ctx);
                if (string.IsNullOrWhiteSpace(profile.display_name))
                    report.Error($"{ctx}: 'display_name' is required.");
                if (profile.width <= 0 || profile.width > MaxItemStorageDimension)
                    report.Error($"{ctx}: 'width' must be between 1 and {MaxItemStorageDimension} (got {profile.width}).");
                if (profile.height <= 0 || profile.height > MaxItemStorageDimension)
                    report.Error($"{ctx}: 'height' must be between 1 and {MaxItemStorageDimension} (got {profile.height}).");
            }
        }

        private void ValidateItemOwnedStorage(ItemDefinition item, string ctx)
        {
            if (string.IsNullOrWhiteSpace(item.owned_storage_profile_id))
                return;

            RequireSnakeCase(item.owned_storage_profile_id, "owned_storage_profile_id", ctx);
            if (database.GetItemStorageProfile(item.owned_storage_profile_id) == null)
                report.Error($"{ctx}: 'owned_storage_profile_id' references '{item.owned_storage_profile_id}' which was not loaded.");
            if (item.max_stack != 1)
                report.Error($"{ctx}: items with 'owned_storage_profile_id' must declare 'max_stack' exactly 1.");
        }

        private void ValidateItemInventoryMetadata(ItemDefinition item, string ctx)
        {
            if (!item.inventory.HasValue)
            {
                report.Warning($"{ctx}: optional 'inventory' metadata is missing; grid inventory will use fallback footprint 1x1.");
                return;
            }

            ItemInventoryMetadata inventory = item.inventory.Value;
            if (inventory.initial_orientation != null &&
                inventory.initial_orientation != ItemInitialOrientation.Original &&
                inventory.initial_orientation != ItemInitialOrientation.Rotated)
            {
                report.Error($"{ctx}: optional 'inventory.initial_orientation' must be 'original' or 'rotated' (got '{inventory.initial_orientation}').");
            }

            if (!inventory.footprint.HasValue)
            {
                report.Warning($"{ctx}: optional 'inventory.footprint' is missing; grid inventory will use fallback footprint 1x1.");
                return;
            }

            ItemFootprintDefinition footprint = inventory.footprint.Value;
            if (footprint.width <= 0)
                report.Error($"{ctx}: 'inventory.footprint.width' must be > 0 (got {footprint.width}).");

            if (footprint.height <= 0)
                report.Error($"{ctx}: 'inventory.footprint.height' must be > 0 (got {footprint.height}).");

            if (inventory.icon_id != null)
            {
                string iconId = inventory.icon_id.Trim();
                if (iconId.Length == 0)
                    report.Warning($"{ctx}: optional 'inventory.icon_id' is empty; inventory UI will use its visual fallback.");
                else if (!SnakeCasePattern.IsMatch(iconId))
                    report.Warning($"{ctx}: optional 'inventory.icon_id' should use snake_case (got '{inventory.icon_id}'); inventory UI will use its visual fallback if the sprite cannot be resolved.");
            }
        }

        private void ValidateItemFirearmAndAmmoProfiles(ItemDefinition item, string ctx)
        {
            bool hasFirearmProfile = !string.IsNullOrWhiteSpace(item.firearm_profile_id);
            bool hasAmmoProfile = !string.IsNullOrWhiteSpace(item.ammo_profile_id);

            if (hasFirearmProfile && hasAmmoProfile)
                report.Error($"{ctx}: an item cannot reference both 'firearm_profile_id' and 'ammo_profile_id'.");

            if (hasFirearmProfile)
            {
                RequireSnakeCase(item.firearm_profile_id, "firearm_profile_id", ctx);

                if (database.GetFirearmProfile(item.firearm_profile_id) == null)
                    report.Error($"{ctx}: 'firearm_profile_id' references '{item.firearm_profile_id}' which was not loaded.");

                if (!IsItemEquipEnabled(item))
                    report.Error($"{ctx}: items with 'firearm_profile_id' must be equipable.");
            }

            if (hasAmmoProfile)
            {
                RequireSnakeCase(item.ammo_profile_id, "ammo_profile_id", ctx);

                if (database.GetAmmoProfile(item.ammo_profile_id) == null)
                    report.Error($"{ctx}: 'ammo_profile_id' references '{item.ammo_profile_id}' which was not loaded.");

                if (item.max_stack <= 1)
                    report.Error($"{ctx}: ammo items must be stackable with 'max_stack' > 1.");

                if (IsItemEquipEnabled(item))
                    report.Error($"{ctx}: ammo items must not be equipable in Milestone 29.");
            }
        }

        private void ValidateItemEquip(ItemDefinition item, string ctx)
        {
            bool hasFlatEquippable = item.equippable.HasValue;
            bool flatEquippable = item.equippable.GetValueOrDefault();
            bool hasNestedEquippable = item.equip != null && item.equip.equippable.HasValue;
            bool nestedEquippable = hasNestedEquippable && item.equip.equippable.Value;

            if (hasFlatEquippable && hasNestedEquippable && flatEquippable != nestedEquippable)
                report.Error($"{ctx}: 'equippable' and 'equip.equippable' must not contradict each other.");

            bool isEquippable = hasNestedEquippable ? nestedEquippable : hasFlatEquippable && flatEquippable;
            if (isEquippable && item.equip == null)
            {
                report.Error($"{ctx}: equipable items must declare an 'equip' block.");
                return;
            }

            if (item.equip == null)
                return;

            bool hasSlotSets = item.equip.slot_sets != null && item.equip.slot_sets.Length > 0;
            bool hasLegacySlots = (item.equip.allowed_slots != null && item.equip.allowed_slots.Length > 0) ||
                                  (item.equip.occupied_slots != null && item.equip.occupied_slots.Length > 0);

            if (hasSlotSets && hasLegacySlots)
                report.Error($"{ctx}: equip.slot_sets cannot be combined with legacy allowed_slots/occupied_slots.");

            if (!isEquippable)
                return;

            if (item.max_stack != 1)
                report.Error($"{ctx}: equipable items must use max_stack = 1 (got {item.max_stack}).");

            if (hasSlotSets)
            {
                ValidateEquipSlotSets(item.equip.slot_sets, $"{ctx}: equip.slot_sets");
                return;
            }

            ValidateEquipSlotArray(item.equip.allowed_slots, $"{ctx}: equip.allowed_slots", true);
            ValidateEquipSlotArray(item.equip.occupied_slots, $"{ctx}: equip.occupied_slots", true);
        }

        private void ValidateEquipSlotSets(string[][] slotSets, string context)
        {
            if (slotSets == null || slotSets.Length == 0)
            {
                report.Error($"{context} must not be empty for equipable items.");
                return;
            }

            for (int setIndex = 0; setIndex < slotSets.Length; setIndex++)
            {
                string[] slotSet = slotSets[setIndex];
                string setContext = $"{context}[{setIndex}]";
                if (slotSet == null || slotSet.Length == 0)
                {
                    report.Error($"{setContext} must be a non-empty complete slot alternative.");
                    continue;
                }

                var seenSlots = new HashSet<string>();
                for (int slotIndex = 0; slotIndex < slotSet.Length; slotIndex++)
                {
                    string slotId = slotSet[slotIndex];
                    RequireSnakeCase(slotId, $"slot[{slotIndex}]", setContext);
                    if (!seenSlots.Add(slotId))
                        report.Error($"{setContext}: duplicate slot '{SafeId(slotId)}'.");
                    if (database.GetEquipmentSlot(slotId) == null)
                        report.Error($"{setContext}: slot '{SafeId(slotId)}' was not loaded.");
                }
            }
        }

        private void ValidateEquipSlotArray(string[] slots, string context, bool isRequired)
        {
            if (slots == null || slots.Length == 0)
            {
                if (isRequired)
                    report.Error($"{context} must not be empty for equipable items.");

                return;
            }

            ValidateSnakeCaseList(slots, context);

            foreach (string slot in slots)
            {
                if (string.IsNullOrWhiteSpace(slot))
                    continue;

                string mappedSlot = slot == LegacyRightHandSlotId ? HandRightSlotId : slot;
                if (database.GetEquipmentSlot(mappedSlot) == null)
                    report.Error($"{context}: slot '{slot}' maps to '{mappedSlot}', which was not loaded.");
            }
        }

        private void ValidateItemConsumable(ItemDefinition item, string ctx)
        {
            bool hasRestoreNeeds = item.consumable.restore_needs != null && item.consumable.restore_needs.Length > 0;
            bool hasRestoreHealth = item.consumable.restore_health != null && item.consumable.restore_health.amount > 0f;

            if (!hasRestoreNeeds && !hasRestoreHealth)
            {
                report.Error($"{ctx}: 'consumable' must declare 'restore_needs' or 'restore_health.amount'.");
                return;
            }

            if (item.consumable.restore_health != null && item.consumable.restore_health.amount <= 0f)
                report.Error($"{ctx}: 'consumable.restore_health.amount' must be > 0 when 'restore_health' is present.");

            if (item.consumable.restore_needs == null)
                return;

            for (int index = 0; index < item.consumable.restore_needs.Length; index++)
            {
                string restoreCtx = $"{ctx}: consumable.restore_needs[{index}]";
                ItemNeedRestore restoreNeed = item.consumable.restore_needs[index];

                if (restoreNeed == null)
                {
                    report.Error($"{restoreCtx}: restore need entry must not be null.");
                    continue;
                }

                RequireSnakeCase(restoreNeed.need_id, "need_id", restoreCtx);

                if (restoreNeed.amount <= 0f)
                    report.Error($"{restoreCtx}: 'amount' must be > 0 (got {restoreNeed.amount}).");
            }
        }

        private void ValidateItemCombat(ItemDefinition item, string ctx)
        {
            if (string.IsNullOrWhiteSpace(item.combat.weapon_profile))
            {
                report.Error($"{ctx}: 'combat.weapon_profile' is required when 'combat' block is present.");
            }
            else if (database.GetWeaponProfile(item.combat.weapon_profile) == null)
            {
                report.Error($"{ctx}: 'combat.weapon_profile' references '{item.combat.weapon_profile}' which was not loaded.");
            }

            if (item.combat.damage == null)
            {
                report.Error($"{ctx}: 'combat.damage' is required when 'combat' block is present.");
            }
            else
            {
                if (item.combat.damage.min < 0 || item.combat.damage.max < 0)
                    report.Error($"{ctx}: combat damage values must be >= 0.");
                if (item.combat.damage.min > item.combat.damage.max)
                    report.Error($"{ctx}: 'combat.damage.min' ({item.combat.damage.min}) must be <= 'combat.damage.max' ({item.combat.damage.max}).");
            }

            if (item.combat.actions == null || item.combat.actions.Length == 0)
            {
                report.Error($"{ctx}: 'combat.actions' must not be empty when 'combat' block is present.");
            }
            else
            {
                foreach (string actionId in item.combat.actions)
                {
                    if (string.IsNullOrWhiteSpace(actionId))
                    {
                        report.Error($"{ctx}: 'combat.actions' contains an empty action id.");
                        continue;
                    }

                    if (!SnakeCasePattern.IsMatch(actionId))
                        report.Error($"{ctx}: combat action id '{actionId}' must use snake_case.");

                    if (database.GetAction(actionId) == null)
                        report.Error($"{ctx}: 'combat.actions' references '{actionId}' which was not loaded.");
                }
            }
        }

        private void ValidateWeaponProfiles()
        {
            foreach (WeaponProfileDefinition profile in database.GetAllWeaponProfiles())
            {
                string ctx = $"WeaponProfile '{SafeId(profile != null ? profile.id : null)}'";

                if (profile == null)
                {
                    report.Error("WeaponProfile: null definition loaded.");
                    continue;
                }

                RequireType(profile.type, "weapon_profile", ctx);
                RequireSnakeCase(profile.id, "id", ctx);

                if (string.IsNullOrWhiteSpace(profile.damage_type))
                    report.Error($"{ctx}: 'damage_type' is required.");
                else
                    RequireSnakeCase(profile.damage_type, "damage_type", ctx);

                if (profile.scales_with != null)
                    ValidateSnakeCaseList(profile.scales_with, $"{ctx}: scales_with");

                if (profile.default_actions == null || profile.default_actions.Length == 0)
                {
                    report.Warning($"{ctx}: 'default_actions' is empty. This may be valid later, but the current profile will not provide default combat actions.");
                }
                else
                {
                    foreach (string actionId in profile.default_actions)
                    {
                        if (string.IsNullOrWhiteSpace(actionId))
                        {
                            report.Error($"{ctx}: 'default_actions' contains an empty action id.");
                            continue;
                        }

                        if (!SnakeCasePattern.IsMatch(actionId))
                            report.Error($"{ctx}: default action id '{actionId}' must use snake_case.");

                        if (database.GetAction(actionId) == null)
                            report.Error($"{ctx}: 'default_actions' references '{actionId}' which was not loaded.");
                    }
                }
            }
        }

        private void ValidateFirearmProfiles()
        {
            foreach (FirearmProfileDefinition profile in database.GetAllFirearmProfiles())
            {
                string ctx = $"FirearmProfile '{SafeId(profile != null ? profile.id : null)}'";

                if (profile == null)
                {
                    report.Error("FirearmProfile: null definition loaded.");
                    continue;
                }

                RequireType(profile.type, "firearm_profile", ctx);
                RequireSnakeCase(profile.id, "id", ctx);

                if (string.IsNullOrWhiteSpace(profile.display_name))
                    report.Error($"{ctx}: 'display_name' is required.");

                if (profile.accepted_ammo_profile_ids == null || profile.accepted_ammo_profile_ids.Length == 0)
                {
                    report.Error($"{ctx}: 'accepted_ammo_profile_ids' is required and must not be empty.");
                }
                else
                {
                    var seenAmmoProfiles = new HashSet<string>();
                    for (int index = 0; index < profile.accepted_ammo_profile_ids.Length; index++)
                    {
                        string ammoProfileId = profile.accepted_ammo_profile_ids[index];
                        RequireSnakeCase(ammoProfileId, $"accepted_ammo_profile_ids[{index}]", ctx);

                        if (!string.IsNullOrWhiteSpace(ammoProfileId) && database.GetAmmoProfile(ammoProfileId) == null)
                            report.Error($"{ctx}: accepted ammo profile '{ammoProfileId}' was not loaded.");

                        if (!string.IsNullOrWhiteSpace(ammoProfileId) && !seenAmmoProfiles.Add(ammoProfileId))
                            report.Error($"{ctx}: duplicate accepted ammo profile '{ammoProfileId}'.");
                    }
                }

                if (profile.magazine_capacity != 1)
                    report.Error($"{ctx}: 'magazine_capacity' must be 1 for Milestone 29 single-shot v0 (got {profile.magazine_capacity}).");

                if (profile.range <= 0f)
                    report.Error($"{ctx}: 'range' must be > 0 (got {profile.range}).");

                if (profile.cycle_time < 0f)
                    report.Error($"{ctx}: 'cycle_time' must be >= 0 (got {profile.cycle_time}).");

                if (profile.muzzle_offset <= 0f)
                    report.Error($"{ctx}: 'muzzle_offset' must be > 0 (got {profile.muzzle_offset}).");

                if (profile.debug_accuracy_spread < 0f)
                    report.Error($"{ctx}: 'debug_accuracy_spread' must be >= 0 (got {profile.debug_accuracy_spread}).");
            }
        }

        private void ValidateAmmoProfiles()
        {
            foreach (AmmoProfileDefinition profile in database.GetAllAmmoProfiles())
            {
                string ctx = $"AmmoProfile '{SafeId(profile != null ? profile.id : null)}'";

                if (profile == null)
                {
                    report.Error("AmmoProfile: null definition loaded.");
                    continue;
                }

                RequireType(profile.type, "ammo_profile", ctx);
                RequireSnakeCase(profile.id, "id", ctx);

                if (string.IsNullOrWhiteSpace(profile.display_name))
                    report.Error($"{ctx}: 'display_name' is required.");

                RequireSnakeCase(profile.caliber_tag, "caliber_tag", ctx);
                if (!string.IsNullOrWhiteSpace(profile.caliber_tag) && !tags.IsValid(profile.caliber_tag))
                    report.Error($"{ctx}: caliber_tag '{profile.caliber_tag}' is not registered in tags.json.");

                if (profile.damage <= 0f)
                    report.Error($"{ctx}: 'damage' must be > 0 (got {profile.damage}).");

                ValidateTagList(profile.tags, $"{ctx}: tags");
                if (profile.tags == null || !ContainsValue(profile.tags, profile.caliber_tag))
                    report.Error($"{ctx}: 'tags' must contain caliber_tag '{SafeId(profile.caliber_tag)}'.");
            }
        }

        private void ValidateActions()
        {
            HashSet<string> loadedItemTags = BuildLoadedItemTagSet();

            foreach (ActionDefinition action in database.GetAllActions())
            {
                string ctx = $"Action '{SafeId(action != null ? action.id : null)}'";

                if (action == null)
                {
                    report.Error("Action: null definition loaded.");
                    continue;
                }

                RequireType(action.type, "action", ctx);
                RequireSnakeCase(action.id, "id", ctx);

                if (action.contexts == null || action.contexts.Length == 0)
                    report.Error($"{ctx}: 'contexts' array is required and must not be empty.");
                else
                    ValidateSnakeCaseList(action.contexts, $"{ctx}: contexts");

                if (action.display == null)
                    report.Error($"{ctx}: 'display' block is required.");
                else if (string.IsNullOrWhiteSpace(action.display.name))
                    report.Error($"{ctx}: 'display.name' is required.");

                if (action.cost == null)
                {
                    report.Error($"{ctx}: 'cost' block is required.");
                }
                else
                {
                    if (action.cost.stamina < 0)
                        report.Error($"{ctx}: 'cost.stamina' must be >= 0 (got {action.cost.stamina}).");
                    if (action.cost.time < 0)
                        report.Error($"{ctx}: 'cost.time' must be >= 0 (got {action.cost.time}).");
                }

                if (action.requirements != null)
                {
                    ValidateTagList(action.requirements.actor_tags, $"{ctx}: requirements.actor_tags");
                    ValidateTagList(action.requirements.target_tags, $"{ctx}: requirements.target_tags");
                    ValidateTagList(action.requirements.weapon_tags, $"{ctx}: requirements.weapon_tags");
                    WarnIfWeaponTagsHaveNoLoadedItem(action.requirements.weapon_tags, loadedItemTags, ctx);

                    if (action.requirements.actor_min_stats != null)
                    {
                        foreach (KeyValuePair<string, float> stat in action.requirements.actor_min_stats)
                        {
                            if (string.IsNullOrWhiteSpace(stat.Key) || !SnakeCasePattern.IsMatch(stat.Key))
                                report.Error($"{ctx}: actor_min_stats key '{stat.Key}' must use snake_case.");
                        }
                    }
                }

                ValidateActionEffects(action, ctx);
            }
        }

        private HashSet<string> BuildLoadedItemTagSet()
        {
            var loadedItemTags = new HashSet<string>();

            foreach (ItemDefinition item in database.GetAllItems())
            {
                if (item == null || item.tags == null)
                    continue;

                foreach (string tag in item.tags)
                {
                    if (string.IsNullOrWhiteSpace(tag))
                        continue;

                    loadedItemTags.Add(tag);
                }
            }

            return loadedItemTags;
        }

        private void WarnIfWeaponTagsHaveNoLoadedItem(string[] weaponTags, HashSet<string> loadedItemTags, string context)
        {
            if (weaponTags == null)
                return;

            foreach (string tag in weaponTags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                    continue;

                if (!SnakeCasePattern.IsMatch(tag))
                    continue;

                if (!tags.IsValid(tag))
                    continue;

                if (!loadedItemTags.Contains(tag))
                {
                    report.Warning(
                        $"{context}: requirements.weapon_tags uses '{tag}', but no loaded item currently contains that tag. " +
                        "weapon_tags is the legacy-compatible field for required equipped item tags.");
                }
            }
        }

        private void ValidateActionEffects(ActionDefinition action, string ctx)
        {
            if (action.effects == null)
                return;

            for (int index = 0; index < action.effects.Length; index++)
            {
                string effectCtx = $"{ctx}: effects[{index}]";
                ActionEffectDefinition effect = action.effects[index];

                if (effect == null)
                {
                    report.Error($"{effectCtx}: effect must not be null.");
                    continue;
                }

                bool requiresTag = false;
                bool disallowsTag = false;

                if (string.IsNullOrWhiteSpace(effect.type))
                {
                    report.Error($"{effectCtx}: 'type' is required.");
                }
                else
                {
                    if (!SnakeCasePattern.IsMatch(effect.type))
                        report.Error($"{effectCtx}: type '{effect.type}' must use snake_case.");

                    if (effect.type == ActionEffectTypes.AddTag || effect.type == ActionEffectTypes.RemoveTag)
                    {
                        requiresTag = true;
                    }
                    else if (
                        effect.type == ActionEffectTypes.ShowTargetInfo ||
                        effect.type == ActionEffectTypes.PickUpItem ||
                        effect.type == ActionEffectTypes.SearchContainer ||
                        effect.type == ActionEffectTypes.OpenStorage ||
                        effect.type == ActionEffectTypes.KillActor ||
                        effect.type == ActionEffectTypes.SearchActorInventory)
                    {
                        disallowsTag = true;
                    }
                    else if (effect.type == ActionEffectTypes.ApplyDamage)
                    {
                        disallowsTag = true;
                        if (effect.amount <= 0f)
                            report.Error($"{effectCtx}: 'amount' must be > 0 for '{ActionEffectTypes.ApplyDamage}'.");
                    }
                    else
                    {
                        report.Error($"{effectCtx}: unsupported effect type '{effect.type}'. Allowed values: '{ActionEffectTypes.AddTag}', '{ActionEffectTypes.RemoveTag}', '{ActionEffectTypes.ShowTargetInfo}', '{ActionEffectTypes.PickUpItem}', '{ActionEffectTypes.SearchContainer}', '{ActionEffectTypes.OpenStorage}', '{ActionEffectTypes.ApplyDamage}', '{ActionEffectTypes.KillActor}', '{ActionEffectTypes.SearchActorInventory}'.");
                    }
                }

                if (string.IsNullOrWhiteSpace(effect.target))
                {
                    report.Error($"{effectCtx}: 'target' is required.");
                }
                else if (effect.target != EffectTargetTarget)
                {
                    report.Error($"{effectCtx}: unsupported target '{effect.target}'. Allowed value: '{EffectTargetTarget}'.");
                }

                if (requiresTag)
                    ValidateRequiredEffectTag(effect.tag, effectCtx);
                else if (disallowsTag && !string.IsNullOrWhiteSpace(effect.tag))
                    report.Error($"{effectCtx}: '{effect.type}' does not use 'tag'.");
            }
        }

        private void ValidateRequiredEffectTag(string tag, string effectCtx)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                report.Error($"{effectCtx}: 'tag' is required.");
                return;
            }

            if (!SnakeCasePattern.IsMatch(tag))
            {
                report.Error($"{effectCtx}: tag '{tag}' must use snake_case.");
            }
            else if (!tags.IsValid(tag))
            {
                report.Error($"{effectCtx}: tag '{tag}' is not registered in tags.json.");
            }
        }

        private void ValidateTagList(string[] tagList, string context)
        {
            if (tagList == null)
                return;

            foreach (string tag in tagList)
            {
                if (string.IsNullOrWhiteSpace(tag))
                {
                    report.Error($"{context}: contains an empty tag.");
                    continue;
                }

                if (!SnakeCasePattern.IsMatch(tag))
                    report.Error($"{context}: tag '{tag}' must use snake_case.");

                if (!tags.IsValid(tag))
                    report.Error($"{context}: tag '{tag}' is not registered in tags.json.");
            }
        }

        private void ValidateSnakeCaseList(string[] values, string context)
        {
            if (values == null)
                return;

            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    report.Error($"{context}: contains an empty value.");
                    continue;
                }

                if (!SnakeCasePattern.IsMatch(value))
                    report.Error($"{context}: value '{value}' must use snake_case.");
            }
        }

        private static bool ContainsValue(string[] values, string expected)
        {
            if (values == null)
                return false;

            foreach (string value in values)
            {
                if (value == expected)
                    return true;
            }

            return false;
        }

        private static bool IsItemEquipEnabled(ItemDefinition item)
        {
            if (item == null)
                return false;

            if (item.equip != null && item.equip.equippable.HasValue)
                return item.equip.equippable.Value;

            return item.equippable.GetValueOrDefault(false);
        }

        private void RequireType(string actual, string expected, string context)
        {
            if (string.IsNullOrWhiteSpace(actual))
                report.Error($"{context}: 'type' field is missing. Expected '{expected}'.");
            else if (actual != expected)
                report.Error($"{context}: 'type' is '{actual}' but must be '{expected}'.");
        }

        private void RequireSnakeCase(string value, string fieldName, string context)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                report.Error($"{context}: '{fieldName}' is required.");
                return;
            }

            if (!SnakeCasePattern.IsMatch(value))
                report.Error($"{context}: '{fieldName}' value '{value}' must use snake_case only.");
        }

        private static string SafeId(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? "<missing_id>" : id;
        }
    }
}
