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
        private const string RightHandSlotId = "right_hand";
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
            ValidateActions();
            ValidateWeaponProfiles();
            ValidateItems();
            ValidateLootTables();
            ValidateActorProfiles();
            ValidateWorldObjectProfiles();
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

                ValidateActorProfileInitialTags(actorProfile.initial_tags, $"{ctx}: initial_tags");
                ValidateActorProfileHealth(actorProfile.health, $"{ctx}: health");
                ValidateActorProfileInventory(actorProfile.initial_inventory, $"{ctx}: initial_inventory");

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
                    if (item.physical.weight_kg < 0)
                        report.Error($"{ctx}: 'physical.weight_kg' must be >= 0 (got {item.physical.weight_kg}).");
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

                if (item.combat != null)
                    ValidateItemCombat(item, ctx);

                if (item.consumable != null)
                    ValidateItemConsumable(item, ctx);
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

            ValidateEquipSlotArray(item.equip.allowed_slots, $"{ctx}: equip.allowed_slots", isEquippable);
            ValidateEquipSlotArray(item.equip.occupied_slots, $"{ctx}: equip.occupied_slots", isEquippable);

            if (!isEquippable)
                return;

            if (!ContainsValue(item.equip.allowed_slots, RightHandSlotId))
                report.Error($"{ctx}: equip.allowed_slots must contain '{RightHandSlotId}' for Milestone 23.");

            if (!ContainsValue(item.equip.occupied_slots, RightHandSlotId))
                report.Error($"{ctx}: equip.occupied_slots must contain '{RightHandSlotId}' for Milestone 23.");
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

                if (slot != RightHandSlotId)
                    report.Error($"{context}: slot '{slot}' is not supported in Milestone 23. Allowed value: '{RightHandSlotId}'.");
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
