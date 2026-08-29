using System;
using System.Collections.Generic;
using OldScars.Core.Actions;
using OldScars.Core.Actors;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Combat;
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
        private const string EffectTargetTarget = "target";
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
            ValidateVisualRigCapabilities();
            ValidateVisualRigProfiles();
            ValidateVisualAssets();
            ValidateItemVisualProfiles();
            ValidateAttachmentPoses();
            ValidateActions();
            ValidateWeaponProfiles();
            ValidateFirearmProfiles();
            ValidateAmmoProfiles();
            ValidatePenetrationProfiles();
            ValidateArmorProfiles();
            ValidateItemStorageProfiles();
            ValidateItems();
            ValidateLootTables();
            ValidateActorLoadoutProfiles();
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
                RequireGlobalContentId(slot.id, "id", ctx);
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
                RequireGlobalContentId(layout.id, "id", ctx);
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

                        RequireLocalId(group.id, "id", groupCtx);
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

                    RequireGlobalContentId(slot.slot_id, "slot_id", slotCtx);
                    RequireLocalId(slot.group_id, "group_id", slotCtx);
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

        private void ValidateVisualRigCapabilities()
        {
            foreach (VisualRigCapabilityDefinition capability in database.GetAllVisualRigCapabilities())
            {
                string ctx = $"VisualRigCapability '{SafeId(capability != null ? capability.id : null)}'";
                if (capability == null)
                {
                    report.Error("VisualRigCapability: null definition loaded.");
                    continue;
                }
                RequireType(capability.type, "visual_rig_capability", ctx);
                RequireGlobalContentId(capability.id, "id", ctx);
                if (string.IsNullOrWhiteSpace(capability.display_name))
                    report.Error($"{ctx}: 'display_name' is required.");
            }
        }

        private void ValidateVisualRigProfiles()
        {
            foreach (VisualRigProfileDefinition profile in database.GetAllVisualRigProfiles())
            {
                string ctx = $"VisualRigProfile '{SafeId(profile != null ? profile.id : null)}'";
                if (profile == null)
                {
                    report.Error("VisualRigProfile: null definition loaded.");
                    continue;
                }

                RequireType(profile.type, "visual_rig_profile", ctx);
                RequireGlobalContentId(profile.id, "id", ctx);
                RequireLocalId(profile.family_id, "family_id", ctx);
                if (string.IsNullOrWhiteSpace(profile.display_name))
                    report.Error($"{ctx}: 'display_name' is required.");

                var parts = new Dictionary<string, VisualPartDefinition>();
                if (profile.parts == null || profile.parts.Length == 0)
                {
                    report.Error($"{ctx}: 'parts' is required and must not be empty.");
                }
                else
                {
                    for (int index = 0; index < profile.parts.Length; index++)
                    {
                        VisualPartDefinition part = profile.parts[index];
                        string partCtx = $"{ctx}: parts[{index}]";
                        if (part == null)
                        {
                            report.Error($"{partCtx} must not be null.");
                            continue;
                        }
                        RequireLocalId(part.id, "id", partCtx);
                        if (!string.IsNullOrWhiteSpace(part.parent_part_id))
                            RequireLocalId(part.parent_part_id, "parent_part_id", partCtx);
                        if (!string.IsNullOrWhiteSpace(part.damage_region_id))
                            RequireLocalId(part.damage_region_id, "damage_region_id", partCtx);
                        if (!parts.ContainsKey(part.id ?? string.Empty))
                            parts[part.id ?? string.Empty] = part;
                        else
                            report.Error($"{ctx}: duplicate part id '{SafeId(part.id)}'.");
                    }

                    foreach (VisualPartDefinition part in parts.Values)
                    {
                        if (!string.IsNullOrWhiteSpace(part.parent_part_id) && !parts.ContainsKey(part.parent_part_id))
                            report.Error($"{ctx}: part '{part.id}' references missing parent_part_id '{part.parent_part_id}'.");
                    }
                    ValidateVisualPartCycles(parts, ctx);
                }

                var socketIds = new HashSet<string>();
                var socketRoles = new HashSet<string>();
                if (profile.sockets == null || profile.sockets.Length == 0)
                {
                    report.Error($"{ctx}: 'sockets' is required and must not be empty.");
                }
                else
                {
                    for (int index = 0; index < profile.sockets.Length; index++)
                    {
                        VisualSocketDefinition socket = profile.sockets[index];
                        string socketCtx = $"{ctx}: sockets[{index}]";
                        if (socket == null)
                        {
                            report.Error($"{socketCtx} must not be null.");
                            continue;
                        }
                        RequireLocalId(socket.id, "id", socketCtx);
                        RequireLocalId(socket.part_id, "part_id", socketCtx);
                        RequireLocalId(socket.role, "role", socketCtx);
                        if (!socketIds.Add(socket.id ?? string.Empty))
                            report.Error($"{ctx}: duplicate socket id '{SafeId(socket.id)}'.");
                        socketRoles.Add(socket.role ?? string.Empty);
                        if (!parts.ContainsKey(socket.part_id ?? string.Empty))
                            report.Error($"{socketCtx}: part_id references '{SafeId(socket.part_id)}' which is not declared by this rig.");
                        if (socket.capabilities == null || socket.capabilities.Length == 0)
                            report.Error($"{socketCtx}: 'capabilities' is required and must not be empty.");
                        else
                        {
                            var seenCapabilities = new HashSet<string>();
                            for (int capabilityIndex = 0; capabilityIndex < socket.capabilities.Length; capabilityIndex++)
                            {
                                string capabilityId = socket.capabilities[capabilityIndex];
                                RequireGlobalContentId(capabilityId, "capability", socketCtx);
                                if (!seenCapabilities.Add(capabilityId ?? string.Empty))
                                    report.Error($"{socketCtx}: duplicate capability '{SafeId(capabilityId)}'.");
                                if (database.GetVisualRigCapability(capabilityId) == null)
                                    report.Error($"{socketCtx}: capability '{SafeId(capabilityId)}' was not loaded.");
                            }
                        }
                    }
                }

                var mappedEquipmentSlots = new HashSet<string>();
                VisualEquipmentSocketMappingDefinition[] mappings = profile.equipment_slot_mappings ?? Array.Empty<VisualEquipmentSocketMappingDefinition>();
                for (int index = 0; index < mappings.Length; index++)
                {
                    VisualEquipmentSocketMappingDefinition mapping = mappings[index];
                    string mappingCtx = $"{ctx}: equipment_slot_mappings[{index}]";
                    if (mapping == null)
                    {
                        report.Error($"{mappingCtx} must not be null.");
                        continue;
                    }
                    RequireGlobalContentId(mapping.equipment_slot_id, "equipment_slot_id", mappingCtx);
                    RequireLocalId(mapping.socket_role, "socket_role", mappingCtx);
                    if (!mappedEquipmentSlots.Add(mapping.equipment_slot_id ?? string.Empty))
                        report.Error($"{mappingCtx}: duplicate mapping for equipment slot '{SafeId(mapping.equipment_slot_id)}'.");
                    if (database.GetEquipmentSlot(mapping.equipment_slot_id) == null)
                        report.Error($"{mappingCtx}: equipment slot '{SafeId(mapping.equipment_slot_id)}' was not loaded.");
                    if (!socketRoles.Contains(mapping.socket_role ?? string.Empty))
                        report.Error($"{mappingCtx}: socket role '{SafeId(mapping.socket_role)}' is not declared by this rig.");
                }
            }
        }

        private void ValidateVisualAssets()
        {
            foreach (VisualAssetDefinition asset in database.GetAllVisualAssets())
            {
                string ctx = $"VisualAsset '{SafeId(asset != null ? asset.id : null)}'";
                if (asset == null)
                {
                    report.Error("VisualAsset: null definition loaded.");
                    continue;
                }
                RequireType(asset.type, "visual_asset", ctx);
                RequireGlobalContentId(asset.id, "id", ctx);
                RequireAssetKey(asset.asset_key, "asset_key", ctx);
                if (asset.provider_id != "builtin")
                    report.Error($"{ctx}: unsupported provider_id '{SafeId(asset.provider_id)}'. M35.0 supports only 'builtin'.");
                if (string.IsNullOrWhiteSpace(asset.provider_asset_id))
                    report.Error($"{ctx}: 'provider_asset_id' is required.");
            }
        }

        private void ValidateItemVisualProfiles()
        {
            foreach (ItemVisualProfileDefinition profile in database.GetAllItemVisualProfiles())
            {
                string ctx = $"ItemVisualProfile '{SafeId(profile != null ? profile.id : null)}'";
                if (profile == null)
                {
                    report.Error("ItemVisualProfile: null definition loaded.");
                    continue;
                }
                RequireType(profile.type, "item_visual_profile", ctx);
                RequireGlobalContentId(profile.id, "id", ctx);
                RequireGlobalContentId(profile.item_definition_id, "item_definition_id", ctx);
                ItemDefinition item = database.GetItem(profile.item_definition_id);
                if (item == null)
                    report.Error($"{ctx}: item_definition_id '{SafeId(profile.item_definition_id)}' was not loaded.");
                ValidateVisualAssetReference(profile.world_asset_key, "world_asset_key", ctx);
                ValidateVisualAssetReference(profile.equipped_asset_key, "equipped_asset_key", ctx);

                if (profile.socket_policy != ItemVisualSocketPolicy.EquipmentSlot &&
                    profile.socket_policy != ItemVisualSocketPolicy.PreferredRoleThenCapability)
                {
                    report.Error($"{ctx}: socket_policy must be '{ItemVisualSocketPolicy.EquipmentSlot}' or '{ItemVisualSocketPolicy.PreferredRoleThenCapability}'.");
                }
                if (!string.IsNullOrWhiteSpace(profile.primary_socket_role))
                    RequireLocalId(profile.primary_socket_role, "primary_socket_role", ctx);
                if (profile.required_socket_capabilities == null || profile.required_socket_capabilities.Length == 0)
                {
                    report.Error($"{ctx}: 'required_socket_capabilities' is required and must not be empty.");
                }
                else
                {
                    var seen = new HashSet<string>();
                    for (int index = 0; index < profile.required_socket_capabilities.Length; index++)
                    {
                        string capability = profile.required_socket_capabilities[index];
                        RequireGlobalContentId(capability, "required_socket_capability", ctx);
                        if (!seen.Add(capability ?? string.Empty))
                            report.Error($"{ctx}: duplicate required capability '{SafeId(capability)}'.");
                        if (database.GetVisualRigCapability(capability) == null)
                            report.Error($"{ctx}: required capability '{SafeId(capability)}' was not loaded.");
                    }
                }

                if (profile.fallback_visual != ItemVisualFallback.None && profile.fallback_visual != ItemVisualFallback.DebugBox)
                    report.Error($"{ctx}: fallback_visual must be '{ItemVisualFallback.None}' or '{ItemVisualFallback.DebugBox}'.");
                if (!string.IsNullOrWhiteSpace(profile.persistent_pose_id))
                {
                    RequireGlobalContentId(profile.persistent_pose_id, "persistent_pose_id", ctx);
                    if (database.GetAttachmentPose(profile.persistent_pose_id) == null)
                        report.Error($"{ctx}: persistent_pose_id references '{profile.persistent_pose_id}' which was not loaded.");
                }

                if (HasMultiSlotAlternative(item) && string.IsNullOrWhiteSpace(profile.primary_socket_role))
                    report.Error($"{ctx}: a multi-slot item requires 'primary_socket_role' so it produces one primary visual.");
            }
        }

        private void ValidateAttachmentPoses()
        {
            var resolutionKeys = new HashSet<string>();
            foreach (AttachmentPoseDefinition pose in database.GetAllAttachmentPoses())
            {
                string ctx = $"AttachmentPose '{SafeId(pose != null ? pose.id : null)}'";
                if (pose == null)
                {
                    report.Error("AttachmentPose: null definition loaded.");
                    continue;
                }
                RequireType(pose.type, "attachment_pose", ctx);
                RequireGlobalContentId(pose.id, "id", ctx);
                RequireGlobalContentId(pose.visual_profile_id, "visual_profile_id", ctx);
                if (database.GetItemVisualProfile(pose.visual_profile_id) == null)
                    report.Error($"{ctx}: visual_profile_id references '{SafeId(pose.visual_profile_id)}' which was not loaded.");

                if (!string.IsNullOrWhiteSpace(pose.rig_profile_id) && !string.IsNullOrWhiteSpace(pose.rig_family_id))
                    report.Error($"{ctx}: use either rig_profile_id or rig_family_id, not both.");
                if (!string.IsNullOrWhiteSpace(pose.rig_profile_id))
                {
                    RequireGlobalContentId(pose.rig_profile_id, "rig_profile_id", ctx);
                    if (database.GetVisualRigProfile(pose.rig_profile_id) == null)
                        report.Error($"{ctx}: rig_profile_id references '{pose.rig_profile_id}' which was not loaded.");
                }
                if (!string.IsNullOrWhiteSpace(pose.rig_family_id))
                {
                    RequireLocalId(pose.rig_family_id, "rig_family_id", ctx);
                    if (!VisualRigFamilyExists(pose.rig_family_id))
                        report.Error($"{ctx}: rig_family_id '{pose.rig_family_id}' is not used by a loaded rig profile.");
                }

                if (!string.IsNullOrWhiteSpace(pose.socket_id) && !string.IsNullOrWhiteSpace(pose.socket_role))
                    report.Error($"{ctx}: use either socket_id or socket_role, not both.");
                if (!string.IsNullOrWhiteSpace(pose.socket_id))
                    RequireLocalId(pose.socket_id, "socket_id", ctx);
                if (!string.IsNullOrWhiteSpace(pose.socket_role))
                    RequireLocalId(pose.socket_role, "socket_role", ctx);
                if (!PoseSocketExists(pose))
                    report.Error($"{ctx}: socket selector does not exist on the referenced rig profile or family.");

                ValidateFiniteVector(pose.local_position, "local_position", ctx, false);
                ValidateFiniteVector(pose.local_rotation, "local_rotation", ctx, false);
                ValidateFiniteVector(pose.local_scale, "local_scale", ctx, true);

                string key = string.Join("|", pose.visual_profile_id, pose.rig_profile_id, pose.rig_family_id, pose.socket_id, pose.socket_role);
                if (!resolutionKeys.Add(key))
                    report.Error($"{ctx}: duplicate attachment pose resolution key '{key}'.");
            }
        }

        private void ValidateVisualPartCycles(Dictionary<string, VisualPartDefinition> parts, string context)
        {
            foreach (string partId in parts.Keys)
            {
                var visited = new HashSet<string>();
                string current = partId;
                while (!string.IsNullOrWhiteSpace(current) && parts.TryGetValue(current, out VisualPartDefinition part))
                {
                    if (!visited.Add(current))
                    {
                        report.Error($"{context}: cycle detected in visual parts at '{current}'.");
                        break;
                    }
                    current = part.parent_part_id;
                }
            }
        }

        private void ValidateVisualAssetReference(string assetKey, string fieldName, string context)
        {
            RequireAssetKey(assetKey, fieldName, context);
            if (database.GetVisualAssetByKey(assetKey) == null)
                report.Error($"{context}: {fieldName} references '{SafeId(assetKey)}' which was not loaded.");
        }

        private void RequireAssetKey(string value, string fieldName, string context)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                report.Error($"{context}: '{fieldName}' is required.");
                return;
            }
            if (!ContentId.TryParse(value, out _, out string error))
                report.Error($"{context}: '{fieldName}' asset key '{value}' is invalid: {error}.");
        }

        private bool VisualRigFamilyExists(string familyId)
        {
            foreach (VisualRigProfileDefinition profile in database.GetAllVisualRigProfiles())
            {
                if (profile != null && profile.family_id == familyId)
                    return true;
            }
            return false;
        }

        private bool PoseSocketExists(AttachmentPoseDefinition pose)
        {
            if (string.IsNullOrWhiteSpace(pose.socket_id) && string.IsNullOrWhiteSpace(pose.socket_role))
                return true;
            foreach (VisualRigProfileDefinition profile in database.GetAllVisualRigProfiles())
            {
                if (profile == null ||
                    (!string.IsNullOrWhiteSpace(pose.rig_profile_id) && profile.id != pose.rig_profile_id) ||
                    (!string.IsNullOrWhiteSpace(pose.rig_family_id) && profile.family_id != pose.rig_family_id) ||
                    profile.sockets == null)
                    continue;
                for (int index = 0; index < profile.sockets.Length; index++)
                {
                    VisualSocketDefinition socket = profile.sockets[index];
                    if (socket != null &&
                        (string.IsNullOrWhiteSpace(pose.socket_id) || socket.id == pose.socket_id) &&
                        (string.IsNullOrWhiteSpace(pose.socket_role) || socket.role == pose.socket_role))
                        return true;
                }
            }
            return false;
        }

        private void ValidateFiniteVector(Float3Definition value, string fieldName, string context, bool requirePositive)
        {
            if (value == null)
            {
                report.Error($"{context}: '{fieldName}' is required.");
                return;
            }
            float[] components = { value.x, value.y, value.z };
            for (int index = 0; index < components.Length; index++)
            {
                float component = components[index];
                if (float.IsNaN(component) || float.IsInfinity(component))
                    report.Error($"{context}: '{fieldName}' must contain finite values.");
                if (requirePositive && component <= 0f)
                    report.Error($"{context}: '{fieldName}' components must be > 0.");
            }
        }

        private static bool HasMultiSlotAlternative(ItemDefinition item)
        {
            if (item?.equip?.slot_sets == null)
                return false;
            for (int index = 0; index < item.equip.slot_sets.Length; index++)
            {
                if (item.equip.slot_sets[index] != null && item.equip.slot_sets[index].Length > 1)
                    return true;
            }
            return false;
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
                RequireGlobalContentId(worldObjectProfile.id, "id", ctx);

                if (string.IsNullOrWhiteSpace(worldObjectProfile.display_name))
                    report.Error($"{ctx}: 'display_name' is required.");

                ValidateWorldObjectProfileInitialTags(worldObjectProfile.initial_tags, $"{ctx}: initial_tags");

                if (!string.IsNullOrWhiteSpace(worldObjectProfile.penetration_profile_id))
                {
                    RequireGlobalContentId(worldObjectProfile.penetration_profile_id, "penetration_profile_id", ctx);
                    if (database.GetPenetrationProfile(worldObjectProfile.penetration_profile_id) == null)
                    {
                        report.Error($"{ctx}: 'penetration_profile_id' references " +
                                     $"'{worldObjectProfile.penetration_profile_id}' which was not loaded.");
                    }
                }

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

                if (!ContentId.TryValidateLocalId(tag, out _))
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
                RequireGlobalContentId(actorProfile.id, "id", ctx);

                if (string.IsNullOrWhiteSpace(actorProfile.display_name))
                    report.Error($"{ctx}: 'display_name' is required.");

                if (!string.IsNullOrWhiteSpace(actorProfile.inventory_seed_actor_tag))
                {
                    RequireLocalId(actorProfile.inventory_seed_actor_tag, "inventory_seed_actor_tag", ctx);
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
                ValidateActorProfileNavigation(actorProfile.navigation, $"{ctx}: navigation");
                ValidateActorProfileVisualPerception(actorProfile.visual_perception, $"{ctx}: visual_perception");
                ValidateActorProfileEncounterAI(actorProfile, $"{ctx}: encounter_ai");
                ValidateActorProfileInventory(actorProfile.initial_inventory, $"{ctx}: initial_inventory");

                if (!string.IsNullOrWhiteSpace(actorProfile.loadout_profile_id))
                {
                    RequireGlobalContentId(actorProfile.loadout_profile_id, "loadout_profile_id", ctx);
                    if (database.GetActorLoadoutProfile(actorProfile.loadout_profile_id) == null)
                        report.Error($"{ctx}: loadout_profile_id references '{actorProfile.loadout_profile_id}' which was not loaded.");
                    if (actorProfile.initial_inventory != null && actorProfile.initial_inventory.Length > 0 ||
                        actorProfile.initial_equipment != null && actorProfile.initial_equipment.Length > 0)
                        report.Error($"{ctx}: loadout_profile_id cannot coexist with deterministic initial_inventory or initial_equipment.");
                }

                if (!string.IsNullOrWhiteSpace(actorProfile.equipment_layout_id))
                {
                    RequireGlobalContentId(actorProfile.equipment_layout_id, "equipment_layout_id", ctx);
                    if (database.GetEquipmentLayout(actorProfile.equipment_layout_id) == null)
                        report.Error($"{ctx}: equipment_layout_id references '{actorProfile.equipment_layout_id}' which was not loaded.");
                }

                if (!string.IsNullOrWhiteSpace(actorProfile.loadout_profile_id))
                    ValidateActorLoadoutCompatibility(actorProfile, ctx);

                ValidateActorProfileEquipment(
                    actorProfile.initial_equipment,
                    actorProfile.equipment_layout_id,
                    $"{ctx}: initial_equipment");

                if (!string.IsNullOrWhiteSpace(actorProfile.visual_rig_profile_id))
                {
                    RequireGlobalContentId(actorProfile.visual_rig_profile_id, "visual_rig_profile_id", ctx);
                    if (database.GetVisualRigProfile(actorProfile.visual_rig_profile_id) == null)
                        report.Error($"{ctx}: visual_rig_profile_id references '{actorProfile.visual_rig_profile_id}' which was not loaded.");
                }

                if (actorProfile.equipped != null)
                    report.Error($"{ctx}: 'equipped' is not supported yet in Milestone 24.2.");
            }
        }

        private void ValidateActorProfileNavigation(ActorProfileNavigation navigation, string context)
        {
            if (navigation == null)
                return;
            RequireFinitePositive(navigation.speed, "speed", context);
            RequireFinitePositive(navigation.acceleration, "acceleration", context);
            RequireFinitePositive(navigation.angular_speed, "angular_speed", context);
            RequireFinitePositive(navigation.stopping_distance, "stopping_distance", context);
        }

        private void ValidateActorProfileVisualPerception(ActorProfileVisualPerception perception, string context)
        {
            if (perception == null)
                return;
            RequireFinitePositive(perception.visual_range, "visual_range", context);
            RequireFinitePositive(perception.eye_height, "eye_height", context);
            if (!FinitePositive(perception.horizontal_fov_degrees) || perception.horizontal_fov_degrees > 360f)
                report.Error($"{context}: 'horizontal_fov_degrees' must be finite and within (0, 360] (got {perception.horizontal_fov_degrees}).");
        }

        private void RequireFinitePositive(float value, string field, string context)
        {
            if (!FinitePositive(value))
                report.Error($"{context}: '{field}' must be finite and > 0 (got {value}).");
        }

        private void ValidateActorProfileEncounterAI(ActorProfileDefinition actorProfile, string context)
        {
            ActorProfileEncounterAI ai = actorProfile.encounter_ai;
            if (ai == null)
                return;
            if (ai.response_policy != "avoid" && ai.response_policy != "flee" && ai.response_policy != "fight")
                report.Error($"{context}: 'response_policy' must be exactly 'avoid', 'flee' or 'fight'.");
            RequireFinitePositive(ai.alert_duration_seconds, "alert_duration_seconds", context);
            RequireFinitePositive(ai.lost_contact_timeout_seconds, "lost_contact_timeout_seconds", context);
            RequireFinitePositive(ai.avoid_distance, "avoid_distance", context);
            RequireFinitePositive(ai.flee_distance, "flee_distance", context);
            RequireFinitePositive(ai.preferred_combat_distance, "preferred_combat_distance", context);
            RequireFinitePositive(ai.decision_interval_seconds, "decision_interval_seconds", context);
            RequireFinitePositive(ai.replan_distance, "replan_distance", context);
            if (FinitePositive(ai.flee_distance) && FinitePositive(ai.avoid_distance) && ai.flee_distance <= ai.avoid_distance)
                report.Error($"{context}: 'flee_distance' must be greater than 'avoid_distance'.");
            if (actorProfile.navigation == null || actorProfile.visual_perception == null)
                report.Error($"{context}: encounter_ai requires both navigation and visual_perception blocks.");
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

                if (!ContentId.TryValidateLocalId(tag, out _))
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
                RequireGlobalContentId(entry.item_id, "item_id", context);

                if (database.GetItem(entry.item_id) == null)
                    report.Error($"{context}: item_id '{entry.item_id}' references an item that was not loaded.");
            }

            if (entry.quantity <= 0)
                report.Error($"{context}: 'quantity' must be > 0 (got {entry.quantity}).");
        }

        private void ValidateActorProfileEquipment(
            ActorProfileInitialEquipmentEntry[] initialEquipment,
            string layoutId,
            string context)
        {
            if (initialEquipment == null || initialEquipment.Length == 0)
                return;

            if (string.IsNullOrWhiteSpace(layoutId))
            {
                report.Error($"{context}: equipment_layout_id is required when initial_equipment is present.");
                return;
            }

            EquipmentLayoutDefinition layout = database.GetEquipmentLayout(layoutId);
            if (layout == null)
                return;

            var occupiedSlots = new HashSet<string>();
            for (int index = 0; index < initialEquipment.Length; index++)
            {
                ActorProfileInitialEquipmentEntry entry = initialEquipment[index];
                string entryContext = $"{context}[{index}]";
                if (entry == null)
                {
                    report.Error($"{entryContext}: entry must not be null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.item_id))
                {
                    report.Error($"{entryContext}: 'item_id' is required.");
                    continue;
                }

                RequireGlobalContentId(entry.item_id, "item_id", entryContext);
                ItemDefinition item = database.GetItem(entry.item_id);
                if (item == null)
                {
                    report.Error($"{entryContext}: item_id '{entry.item_id}' references an item that was not loaded.");
                    continue;
                }

                bool isEquippable = item.equip != null && item.equip.equippable.GetValueOrDefault(
                    item.equippable.GetValueOrDefault(false));
                string[][] declaredSlotSets = ResolveActorProfileSlotSets(item);
                if (!isEquippable || item.max_stack != 1 || declaredSlotSets == null || declaredSlotSets.Length == 0)
                {
                    report.Error($"{entryContext}: item '{entry.item_id}' is not a quantity-1 equipable item with declared slots.");
                    continue;
                }

                string[] selectedSlots = null;
                if (entry.slot_ids != null)
                {
                    if (entry.slot_ids.Length == 0)
                    {
                        report.Error($"{entryContext}: slot_ids must be omitted or contain a complete slot set.");
                        continue;
                    }

                    var requestedSlots = new HashSet<string>();
                    for (int slotIndex = 0; slotIndex < entry.slot_ids.Length; slotIndex++)
                    {
                        string slotId = entry.slot_ids[slotIndex];
                        RequireGlobalContentId(slotId, $"slot_ids[{slotIndex}]", entryContext);
                        if (!requestedSlots.Add(slotId))
                            report.Error($"{entryContext}: duplicate slot_id '{SafeId(slotId)}'.");
                    }

                    for (int setIndex = 0; setIndex < declaredSlotSets.Length; setIndex++)
                    {
                        if (SameSlotSet(declaredSlotSets[setIndex], entry.slot_ids))
                        {
                            selectedSlots = entry.slot_ids;
                            break;
                        }
                    }

                    if (selectedSlots == null)
                    {
                        report.Error($"{entryContext}: slot_ids are not a declared complete alternative for item '{entry.item_id}'.");
                        continue;
                    }
                }
                else
                {
                    int availableCount = 0;
                    for (int setIndex = 0; setIndex < declaredSlotSets.Length; setIndex++)
                    {
                        string[] candidate = declaredSlotSets[setIndex];
                        if (!AreProfileSlotsAvailable(layout, candidate, occupiedSlots))
                            continue;
                        selectedSlots = candidate;
                        availableCount++;
                    }

                    if (availableCount != 1)
                    {
                        report.Error(
                            availableCount == 0
                                ? $"{entryContext}: item '{entry.item_id}' has no compatible free slot set in layout '{layoutId}'."
                                : $"{entryContext}: item '{entry.item_id}' has multiple free slot alternatives; slot_ids must select one.");
                        continue;
                    }
                }

                if (!AreProfileSlotsAvailable(layout, selectedSlots, occupiedSlots))
                {
                    report.Error($"{entryContext}: one or more selected slots are absent or already occupied in layout '{layoutId}'.");
                    continue;
                }

                for (int slotIndex = 0; slotIndex < selectedSlots.Length; slotIndex++)
                    occupiedSlots.Add(selectedSlots[slotIndex]);
            }
        }

        private static string[][] ResolveActorProfileSlotSets(ItemDefinition item)
        {
            if (item?.equip == null)
                return null;
            if (item.equip.slot_sets != null && item.equip.slot_sets.Length > 0)
                return item.equip.slot_sets;
            if (item.equip.occupied_slots != null && item.equip.occupied_slots.Length > 0)
                return new[] { MapActorProfileSlots(item.equip.occupied_slots) };
            if (item.equip.allowed_slots == null || item.equip.allowed_slots.Length == 0)
                return null;

            var alternatives = new string[item.equip.allowed_slots.Length][];
            for (int index = 0; index < alternatives.Length; index++)
                alternatives[index] = MapActorProfileSlots(new[] { item.equip.allowed_slots[index] });
            return alternatives;
        }

        private static string[] MapActorProfileSlots(string[] slots)
        {
            return (string[])slots.Clone();
        }

        private static bool SameSlotSet(string[] left, string[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;
            var expected = new HashSet<string>(left);
            for (int index = 0; index < right.Length; index++)
            {
                if (!expected.Contains(right[index]))
                    return false;
            }
            return true;
        }

        private static bool AreProfileSlotsAvailable(
            EquipmentLayoutDefinition layout,
            string[] slotIds,
            HashSet<string> occupiedSlots)
        {
            if (layout?.slots == null || slotIds == null || slotIds.Length == 0)
                return false;
            for (int slotIndex = 0; slotIndex < slotIds.Length; slotIndex++)
            {
                string slotId = slotIds[slotIndex];
                if (occupiedSlots.Contains(slotId))
                    return false;

                bool found = false;
                for (int layoutIndex = 0; layoutIndex < layout.slots.Length; layoutIndex++)
                {
                    EquipmentLayoutSlotDefinition slot = layout.slots[layoutIndex];
                    if (slot != null && slot.slot_id == slotId)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    return false;
            }
            return true;
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
                RequireGlobalContentId(lootTable.id, "id", ctx);

                if (lootTable.entries == null || lootTable.entries.Length == 0)
                {
                    report.Error($"{ctx}: 'entries' array is required and must not be empty.");
                    continue;
                }

                for (int index = 0; index < lootTable.entries.Length; index++)
                    ValidateLootTableEntry(lootTable.entries[index], $"{ctx}: entries[{index}]");
            }
        }

        private void ValidateActorLoadoutProfiles()
        {
            foreach (ActorLoadoutProfileDefinition profile in database.GetAllActorLoadoutProfiles())
            {
                string ctx = $"ActorLoadoutProfile '{SafeId(profile != null ? profile.id : null)}'";
                if (profile == null)
                {
                    report.Error("ActorLoadoutProfile: null definition loaded.");
                    continue;
                }
                RequireType(profile.type, "actor_loadout_profile", ctx);
                RequireGlobalContentId(profile.id, "id", ctx);
                if (profile.groups == null || profile.groups.Length == 0)
                {
                    report.Error($"{ctx}: 'groups' is required and must not be empty.");
                    continue;
                }

                var groupIds = new HashSet<string>();
                var possibleSlotsByGroup = new Dictionary<string, HashSet<string>>();
                for (int groupIndex = 0; groupIndex < profile.groups.Length; groupIndex++)
                {
                    ActorLoadoutGroupDefinition group = profile.groups[groupIndex];
                    string groupCtx = $"{ctx}: groups[{groupIndex}]";
                    if (group == null)
                    {
                        report.Error($"{groupCtx}: group must not be null.");
                        continue;
                    }
                    RequireLocalId(group.id, "id", groupCtx);
                    if (!groupIds.Add(group.id)) report.Error($"{ctx}: duplicate group id '{SafeId(group.id)}'.");
                    if (group.choices == null || group.choices.Length == 0)
                    {
                        report.Error($"{groupCtx}: 'choices' is required and must not be empty.");
                        continue;
                    }

                    long totalWeight = 0;
                    var possibleSlots = new HashSet<string>();
                    for (int choiceIndex = 0; choiceIndex < group.choices.Length; choiceIndex++)
                    {
                        ActorLoadoutChoiceDefinition choice = group.choices[choiceIndex];
                        string choiceCtx = $"{groupCtx}: choices[{choiceIndex}]";
                        if (choice == null) { report.Error($"{choiceCtx}: choice must not be null."); continue; }
                        if (choice.weight < 0) report.Error($"{choiceCtx}: 'weight' must be >= 0.");
                        totalWeight += choice.weight;
                        int inventoryCount = choice.inventory?.Length ?? 0;
                        int equipmentCount = choice.equipment?.Length ?? 0;
                        if (choice.none && (inventoryCount > 0 || equipmentCount > 0))
                            report.Error($"{choiceCtx}: explicit NONE cannot declare inventory or equipment.");
                        if (!choice.none && inventoryCount == 0 && equipmentCount == 0)
                            report.Error($"{choiceCtx}: a non-NONE choice must declare inventory and/or equipment.");

                        for (int itemIndex = 0; itemIndex < inventoryCount; itemIndex++)
                        {
                            ActorLoadoutInventoryEntry entry = choice.inventory[itemIndex];
                            string itemCtx = $"{choiceCtx}: inventory[{itemIndex}]";
                            if (entry == null) { report.Error($"{itemCtx}: entry must not be null."); continue; }
                            RequireGlobalContentId(entry.item_id, "item_id", itemCtx);
                            if (database.GetItem(entry.item_id) == null) report.Error($"{itemCtx}: item_id '{entry.item_id}' was not loaded.");
                            if (entry.quantity_min <= 0 || entry.quantity_max < entry.quantity_min)
                                report.Error($"{itemCtx}: quantity range must satisfy 0 < quantity_min <= quantity_max.");
                        }

                        for (int itemIndex = 0; itemIndex < equipmentCount; itemIndex++)
                        {
                            ItemDefinition item = database.GetItem(choice.equipment[itemIndex]?.item_id);
                            string[][] sets = ResolveActorProfileSlotSets(item);
                            string[] selected = choice.equipment[itemIndex]?.slot_ids;
                            if (item == null || sets == null || sets.Length == 0 || item.max_stack != 1)
                            {
                                report.Error($"{choiceCtx}: equipment[{itemIndex}] must reference a quantity-1 equippable item with declared slots.");
                                continue;
                            }
                            if (selected != null)
                            {
                                bool declared = false;
                                for (int setIndex = 0; setIndex < sets.Length; setIndex++)
                                    if (SameSlotSet(sets[setIndex], selected)) declared = true;
                                if (!declared)
                                    report.Error($"{choiceCtx}: equipment[{itemIndex}].slot_ids is not a declared complete slot set for '{item.id}'.");
                            }
                            if (selected == null && sets != null && sets.Length == 1) selected = sets[0];
                            if (selected == null)
                                report.Error($"{choiceCtx}: equipment[{itemIndex}] has multiple slot alternatives; slot_ids must select one.");
                            if (selected != null) for (int slotIndex = 0; slotIndex < selected.Length; slotIndex++) possibleSlots.Add(selected[slotIndex]);
                        }
                        ValidateLoadoutWeaponAmmoPackage(choice, choiceCtx);
                    }
                    if (totalWeight <= 0) report.Error($"{groupCtx}: total choice weight must be > 0.");
                    possibleSlotsByGroup[group.id ?? string.Empty] = possibleSlots;
                }

                string[] keys = new List<string>(possibleSlotsByGroup.Keys).ToArray();
                for (int left = 0; left < keys.Length; left++)
                    for (int right = left + 1; right < keys.Length; right++)
                        foreach (string slot in possibleSlotsByGroup[keys[left]])
                            if (possibleSlotsByGroup[keys[right]].Contains(slot))
                                report.Error($"{ctx}: groups '{keys[left]}' and '{keys[right]}' can both occupy '{slot}', making a weighted result non-atomic.");
            }
        }

        private void ValidateActorLoadoutCompatibility(ActorProfileDefinition actorProfile, string context)
        {
            ActorLoadoutProfileDefinition loadout = database.GetActorLoadoutProfile(actorProfile.loadout_profile_id);
            if (loadout?.groups == null) return;
            if (string.IsNullOrWhiteSpace(actorProfile.equipment_layout_id))
            {
                report.Error($"{context}: equipment_layout_id is required when loadout_profile_id is present.");
                return;
            }
            for (int groupIndex = 0; groupIndex < loadout.groups.Length; groupIndex++)
            {
                ActorLoadoutChoiceDefinition[] choices = loadout.groups[groupIndex]?.choices;
                if (choices == null) continue;
                for (int choiceIndex = 0; choiceIndex < choices.Length; choiceIndex++)
                    ValidateActorProfileEquipment(
                        choices[choiceIndex]?.equipment,
                        actorProfile.equipment_layout_id,
                        $"{context}: loadout '{loadout.id}' groups[{groupIndex}].choices[{choiceIndex}].equipment");
            }
        }

        private void ValidateLoadoutWeaponAmmoPackage(ActorLoadoutChoiceDefinition choice, string context)
        {
            if (choice?.equipment == null) return;
            for (int equipmentIndex = 0; equipmentIndex < choice.equipment.Length; equipmentIndex++)
            {
                ItemDefinition weaponItem = database.GetItem(choice.equipment[equipmentIndex]?.item_id);
                if (weaponItem == null || string.IsNullOrWhiteSpace(weaponItem.firearm_profile_id)) continue;
                FirearmProfileDefinition firearm = database.GetFirearmProfile(weaponItem.firearm_profile_id);
                bool compatibleAmmo = false;
                ActorLoadoutInventoryEntry[] inventory = choice.inventory ?? new ActorLoadoutInventoryEntry[0];
                for (int inventoryIndex = 0; inventoryIndex < inventory.Length; inventoryIndex++)
                {
                    ItemDefinition ammo = database.GetItem(inventory[inventoryIndex]?.item_id);
                    if (ammo == null || string.IsNullOrWhiteSpace(ammo.ammo_profile_id) || firearm?.accepted_ammo_profile_ids == null) continue;
                    for (int accepted = 0; accepted < firearm.accepted_ammo_profile_ids.Length; accepted++)
                        if (firearm.accepted_ammo_profile_ids[accepted] == ammo.ammo_profile_id) compatibleAmmo = true;
                }
                if (!compatibleAmmo)
                    report.Error($"{context}: firearm '{weaponItem.id}' requires inventory ammo whose ammo_profile_id is accepted by '{firearm?.id ?? weaponItem.firearm_profile_id}'.");
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
                RequireGlobalContentId(entry.item_id, "item_id", ctx);

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
                RequireGlobalContentId(item.id, "id", ctx);

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

                ValidateItemProfileReferences(item, ctx);
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
                RequireGlobalContentId(profile.id, "id", ctx);
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

            RequireGlobalContentId(item.owned_storage_profile_id, "owned_storage_profile_id", ctx);
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
                else if (!ContentId.TryValidateLocalId(iconId, out _))
                    report.Warning($"{ctx}: optional 'inventory.icon_id' should use snake_case (got '{inventory.icon_id}'); inventory UI will use its visual fallback if the sprite cannot be resolved.");
            }
        }

        private void ValidateItemProfileReferences(ItemDefinition item, string ctx)
        {
            bool hasFirearmProfile = !string.IsNullOrWhiteSpace(item.firearm_profile_id);
            bool hasAmmoProfile = !string.IsNullOrWhiteSpace(item.ammo_profile_id);
            bool hasArmorProfile = !string.IsNullOrWhiteSpace(item.armor_profile_id);

            if (hasFirearmProfile && hasAmmoProfile)
                report.Error($"{ctx}: an item cannot reference both 'firearm_profile_id' and 'ammo_profile_id'.");

            if (hasArmorProfile && (hasFirearmProfile || hasAmmoProfile))
                report.Error($"{ctx}: an armor item cannot also reference a firearm or ammo profile in M40.1.");

            if (hasFirearmProfile)
            {
                RequireGlobalContentId(item.firearm_profile_id, "firearm_profile_id", ctx);

                if (database.GetFirearmProfile(item.firearm_profile_id) == null)
                    report.Error($"{ctx}: 'firearm_profile_id' references '{item.firearm_profile_id}' which was not loaded.");

                if (!IsItemEquipEnabled(item))
                    report.Error($"{ctx}: items with 'firearm_profile_id' must be equipable.");
            }

            if (hasAmmoProfile)
            {
                RequireGlobalContentId(item.ammo_profile_id, "ammo_profile_id", ctx);

                if (database.GetAmmoProfile(item.ammo_profile_id) == null)
                    report.Error($"{ctx}: 'ammo_profile_id' references '{item.ammo_profile_id}' which was not loaded.");

                if (item.max_stack <= 1)
                    report.Error($"{ctx}: ammo items must be stackable with 'max_stack' > 1.");

                if (IsItemEquipEnabled(item))
                    report.Error($"{ctx}: ammo items must not be equipable in Milestone 29.");
            }

            if (hasArmorProfile)
            {
                RequireGlobalContentId(item.armor_profile_id, "armor_profile_id", ctx);

                if (database.GetArmorProfile(item.armor_profile_id) == null)
                    report.Error($"{ctx}: 'armor_profile_id' references '{item.armor_profile_id}' which was not loaded.");

                if (!IsItemEquipEnabled(item))
                    report.Error($"{ctx}: items with 'armor_profile_id' must be equipable.");

                if (item.max_stack != 1)
                    report.Error($"{ctx}: armor items must declare 'max_stack' exactly 1.");
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
                    RequireGlobalContentId(slotId, $"slot[{slotIndex}]", setContext);
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

            for (int index = 0; index < slots.Length; index++)
            {
                string slot = slots[index];
                RequireGlobalContentId(slot, $"slot[{index}]", context);
                if (string.IsNullOrWhiteSpace(slot))
                    continue;

                if (database.GetEquipmentSlot(slot) == null)
                    report.Error($"{context}: slot '{slot}' was not loaded.");
            }
        }

        private void ValidateItemConsumable(ItemDefinition item, string ctx)
        {
            bool hasRestoreNeeds = item.consumable.restore_needs != null && item.consumable.restore_needs.Length > 0;
            bool hasRestoreHealth = item.consumable.restore_health != null && item.consumable.restore_health.amount > 0f;
            bool hasWoundTreatment = item.consumable.wound_treatment != null;

            if (!hasRestoreNeeds && !hasRestoreHealth && !hasWoundTreatment)
            {
                report.Error($"{ctx}: 'consumable' must declare 'restore_needs', 'restore_health.amount' or 'wound_treatment'.");
                return;
            }

            if (item.consumable.restore_health != null && item.consumable.restore_health.amount <= 0f)
                report.Error($"{ctx}: 'consumable.restore_health.amount' must be > 0 when 'restore_health' is present.");

            if (hasWoundTreatment)
            {
                ItemWoundTreatment treatment = item.consumable.wound_treatment;
                if (treatment.type != ItemWoundTreatmentTypes.Bandage)
                    report.Error($"{ctx}: 'consumable.wound_treatment.type' must be '{ItemWoundTreatmentTypes.Bandage}'.");
                if (float.IsNaN(treatment.bleeding_multiplier) || float.IsInfinity(treatment.bleeding_multiplier) ||
                    treatment.bleeding_multiplier < 0f || treatment.bleeding_multiplier >= 1f)
                {
                    report.Error($"{ctx}: 'consumable.wound_treatment.bleeding_multiplier' must be finite and in [0, 1).");
                }
                if (hasRestoreNeeds || item.consumable.restore_health != null)
                    report.Error($"{ctx}: 'wound_treatment' cannot be combined with restore effects in V1.");
            }

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

                RequireLocalId(restoreNeed.need_id, "need_id", restoreCtx);

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
            else
            {
                RequireGlobalContentId(item.combat.weapon_profile, "combat.weapon_profile", ctx);
                if (database.GetWeaponProfile(item.combat.weapon_profile) == null)
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

                    RequireGlobalContentId(actionId, "combat.actions entry", ctx);

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
                RequireGlobalContentId(profile.id, "id", ctx);

                if (string.IsNullOrWhiteSpace(profile.damage_type))
                    report.Error($"{ctx}: 'damage_type' is required.");
                else
                    RequireLocalId(profile.damage_type, "damage_type", ctx);

                if (profile.scales_with != null)
                    ValidateLocalIdList(profile.scales_with, $"{ctx}: scales_with");

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

                        RequireGlobalContentId(actionId, "default_actions entry", ctx);

                        if (database.GetAction(actionId) == null)
                            report.Error($"{ctx}: 'default_actions' references '{actionId}' which was not loaded.");
                    }
                }

                if (profile.melee_range <= 0f || float.IsNaN(profile.melee_range) || float.IsInfinity(profile.melee_range))
                    report.Error($"{ctx}: 'melee_range' must be finite and > 0 (got {profile.melee_range}).");
                if (profile.attack_duration < 0f || float.IsNaN(profile.attack_duration) || float.IsInfinity(profile.attack_duration))
                    report.Error($"{ctx}: 'attack_duration' must be finite and >= 0 (got {profile.attack_duration}).");
                if (profile.attack_cooldown < 0f || float.IsNaN(profile.attack_cooldown) || float.IsInfinity(profile.attack_cooldown))
                    report.Error($"{ctx}: 'attack_cooldown' must be finite and >= 0 (got {profile.attack_cooldown}).");
                ValidateMedicalImpact(profile.wound_type, profile.wound_severity,
                    profile.bleeding_rate_per_game_hour, profile.pain_contribution, ctx);
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
                RequireGlobalContentId(profile.id, "id", ctx);

                if (string.IsNullOrWhiteSpace(profile.display_name))
                    report.Error($"{ctx}: 'display_name' is required.");

                if (!FirearmActionModes.IsDefined(profile.fire_mode))
                {
                    report.Error($"{ctx}: 'fire_mode' must be one of " +
                                 $"'{FirearmActionModes.ManualCycle}', '{FirearmActionModes.SemiAutomatic}' or " +
                                 $"'{FirearmActionModes.Automatic}' (got '{SafeId(profile.fire_mode)}').");
                }

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
                        RequireGlobalContentId(ammoProfileId, $"accepted_ammo_profile_ids[{index}]", ctx);

                        if (!string.IsNullOrWhiteSpace(ammoProfileId) && database.GetAmmoProfile(ammoProfileId) == null)
                            report.Error($"{ctx}: accepted ammo profile '{ammoProfileId}' was not loaded.");

                        if (!string.IsNullOrWhiteSpace(ammoProfileId) && !seenAmmoProfiles.Add(ammoProfileId))
                            report.Error($"{ctx}: duplicate accepted ammo profile '{ammoProfileId}'.");
                    }
                }

                if (profile.magazine_capacity <= 0)
                    report.Error($"{ctx}: 'magazine_capacity' must be > 0 (got {profile.magazine_capacity}).");

                if (profile.reload_duration <= 0f || float.IsNaN(profile.reload_duration) || float.IsInfinity(profile.reload_duration))
                    report.Error($"{ctx}: 'reload_duration' must be finite and > 0 (got {profile.reload_duration}).");

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
                RequireGlobalContentId(profile.id, "id", ctx);

                if (string.IsNullOrWhiteSpace(profile.display_name))
                    report.Error($"{ctx}: 'display_name' is required.");

                RequireLocalId(profile.caliber_tag, "caliber_tag", ctx);
                if (!string.IsNullOrWhiteSpace(profile.caliber_tag) && !tags.IsValid(profile.caliber_tag))
                    report.Error($"{ctx}: caliber_tag '{profile.caliber_tag}' is not registered in tags.json.");

                ValidateMedicalImpact(profile.wound_type, profile.wound_severity,
                    profile.bleeding_rate_per_game_hour, profile.pain_contribution, ctx);

                if (float.IsNaN(profile.penetration_power) || float.IsInfinity(profile.penetration_power) ||
                    profile.penetration_power <= 0f)
                {
                    report.Error($"{ctx}: projectile 'penetration_power' must be finite and > 0 (got {profile.penetration_power}).");
                }

                ValidateTagList(profile.tags, $"{ctx}: tags");
                if (profile.tags == null || !ContainsValue(profile.tags, profile.caliber_tag))
                    report.Error($"{ctx}: 'tags' must contain caliber_tag '{SafeId(profile.caliber_tag)}'.");
            }
        }

        private void ValidateArmorProfiles()
        {
            foreach (ArmorProfileDefinition profile in database.GetAllArmorProfiles())
            {
                string ctx = $"ArmorProfile '{SafeId(profile != null ? profile.id : null)}'";
                if (profile == null)
                {
                    report.Error("ArmorProfile: null definition loaded.");
                    continue;
                }

                RequireType(profile.type, "armor_profile", ctx);
                RequireGlobalContentId(profile.id, "id", ctx);
                if (string.IsNullOrWhiteSpace(profile.display_name))
                    report.Error($"{ctx}: 'display_name' is required.");

                if (profile.covered_regions == null || profile.covered_regions.Length == 0)
                {
                    report.Error($"{ctx}: 'covered_regions' is required and must not be empty.");
                }
                else
                {
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    for (int index = 0; index < profile.covered_regions.Length; index++)
                    {
                        string region = profile.covered_regions[index];
                        if (!Enum.TryParse(region, false, out BodyRegion parsed) ||
                            !Enum.IsDefined(typeof(BodyRegion), parsed) ||
                            !string.Equals(region, parsed.ToString(), StringComparison.Ordinal))
                        {
                            report.Error($"{ctx}: covered_regions[{index}] '{SafeId(region)}' is not a canonical BodyRegion.");
                        }
                        else if (!seen.Add(region))
                        {
                            report.Error($"{ctx}: duplicate covered region '{region}'.");
                        }
                    }
                }

                RequireGlobalContentId(profile.penetration_profile_id, "penetration_profile_id", ctx);
                if (!string.IsNullOrWhiteSpace(profile.penetration_profile_id) &&
                    database.GetPenetrationProfile(profile.penetration_profile_id) == null)
                {
                    report.Error($"{ctx}: 'penetration_profile_id' references '{profile.penetration_profile_id}' which was not loaded.");
                }
                if (!FiniteNonNegative(profile.impact_resistance))
                    report.Error($"{ctx}: 'impact_resistance' must be finite and >= 0 (got {profile.impact_resistance}).");
                if (!FiniteUnit(profile.stopped_blunt_transfer))
                    report.Error($"{ctx}: 'stopped_blunt_transfer' must be finite and in [0, 1] (got {profile.stopped_blunt_transfer}).");
                if (!FiniteUnit(profile.blunt_wound_threshold))
                    report.Error($"{ctx}: 'blunt_wound_threshold' must be finite and in [0, 1] (got {profile.blunt_wound_threshold}).");
                if (profile.layer_priority < 0)
                    report.Error($"{ctx}: 'layer_priority' must be >= 0 (got {profile.layer_priority}).");
            }
        }

        private void ValidatePenetrationProfiles()
        {
            foreach (PenetrationProfileDefinition profile in database.GetAllPenetrationProfiles())
            {
                string ctx = $"PenetrationProfile '{SafeId(profile != null ? profile.id : null)}'";
                if (profile == null)
                {
                    report.Error("PenetrationProfile: null definition loaded.");
                    continue;
                }

                RequireType(profile.type, "penetration_profile", ctx);
                RequireGlobalContentId(profile.id, "id", ctx);
                if (string.IsNullOrWhiteSpace(profile.display_name))
                    report.Error($"{ctx}: 'display_name' is required.");
                if (!FiniteNonNegative(profile.resistance))
                    report.Error($"{ctx}: 'resistance' must be finite and >= 0 (got {profile.resistance}).");
            }
        }

        private static bool FiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

        private static bool FinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

        private static bool FiniteUnit(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;

        private void ValidateMedicalImpact(
            string woundType,
            float severity,
            float bleedingRatePerGameHour,
            float painContribution,
            string context)
        {
            if (woundType != "Laceration" && woundType != "Puncture" && woundType != "Blunt")
                report.Error($"{context}: 'wound_type' must be canonical Laceration, Puncture or Blunt.");
            if (float.IsNaN(severity) || float.IsInfinity(severity) || severity <= 0f || severity > 1f)
                report.Error($"{context}: 'wound_severity' must be finite and in (0, 1].");
            if (float.IsNaN(bleedingRatePerGameHour) || float.IsInfinity(bleedingRatePerGameHour) || bleedingRatePerGameHour < 0f || bleedingRatePerGameHour > 1f)
                report.Error($"{context}: 'bleeding_rate_per_game_hour' must be finite and in [0, 1].");
            if (float.IsNaN(painContribution) || float.IsInfinity(painContribution) || painContribution < 0f || painContribution > 1f)
                report.Error($"{context}: 'pain_contribution' must be finite and in [0, 1].");
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
                RequireGlobalContentId(action.id, "id", ctx);

                if (action.contexts == null || action.contexts.Length == 0)
                    report.Error($"{ctx}: 'contexts' array is required and must not be empty.");
                else
                    ValidateLocalIdList(action.contexts, $"{ctx}: contexts");

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
                            if (string.IsNullOrWhiteSpace(stat.Key) || !ContentId.TryValidateLocalId(stat.Key, out _))
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

                if (!ContentId.TryValidateLocalId(tag, out _))
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
                    if (!ContentId.TryValidateLocalId(effect.type, out _))
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

            if (!ContentId.TryValidateLocalId(tag, out _))
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

                if (!ContentId.TryValidateLocalId(tag, out _))
                    report.Error($"{context}: tag '{tag}' must use snake_case.");

                if (!tags.IsValid(tag))
                    report.Error($"{context}: tag '{tag}' is not registered in tags.json.");
            }
        }

        private void ValidateLocalIdList(string[] values, string context)
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

                if (!ContentId.TryValidateLocalId(value, out _))
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

        private void RequireLocalId(string value, string fieldName, string context)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                report.Error($"{context}: '{fieldName}' is required.");
                return;
            }

            if (!ContentId.TryValidateLocalId(value, out string error))
                report.Error($"{context}: Local ID '{fieldName}' value '{value}' is invalid: {error}.");
        }

        private void RequireGlobalContentId(string value, string fieldName, string context)
        {
            if (!ContentId.TryParse(value, out _, out string error))
                report.Error($"{context}: Global Content ID '{fieldName}' value '{SafeId(value)}' is invalid: {error}.");
        }

        private static string SafeId(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? "<missing_id>" : id;
        }
    }
}
