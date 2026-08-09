using System;
using System.Collections.Generic;
using OldScars.Core.Data.Definitions;

namespace OldScars.Core.Data.Loading
{
    /// <summary>
    /// Transitional source context at the JSON loading boundary.
    /// A future manifest can supply the namespace and richer provenance here.
    /// </summary>
    internal sealed class ContentLoadContext
    {
        private readonly HashSet<string> legacyExamples = new HashSet<string>(StringComparer.Ordinal);
        private int legacyResolutionCount;

        internal ContentLoadContext(string modName, string modDirectory, bool isCore)
        {
            ModName = modName;
            ModDirectory = modDirectory;
            IsCore = isCore;
        }

        internal string ModName { get; }
        internal string ModDirectory { get; }
        internal bool IsCore { get; }

        internal void RecordLegacy(string raw, string canonical, string fieldContext)
        {
            legacyResolutionCount++;
            if (legacyExamples.Count < 5)
                legacyExamples.Add($"{fieldContext}: '{raw}' -> '{canonical}'");
        }

        internal void ReportLegacyUsage(DataLoadReport report)
        {
            if (legacyResolutionCount == 0)
                return;

            report.Warning(
                $"Mod source '{ModName}' used {legacyResolutionCount} unqualified legacy Global Content ID reference(s). " +
                $"They were resolved against '{ContentId.CoreNamespace}' by temporary Core-only compatibility. " +
                "Migrate them to namespace:local_id; this compatibility path is removable. " +
                "Examples: " + string.Join("; ", legacyExamples));
        }
    }

    /// <summary>
    /// Canonicalizes only IDs that target global GameDatabase registries.
    /// Local IDs, tags, runtime IDs, persistent IDs and asset provider keys are
    /// deliberately outside this visitor.
    /// </summary>
    internal static class DefinitionContentIdNormalizer
    {
        internal static bool Normalize(
            ActionDefinition definition,
            ContentLoadContext context,
            string sourceFile,
            DataLoadReport report)
        {
            return ResolveDefinitionId(ref definition.id, "ActionDefinition", context, sourceFile, report);
        }

        internal static bool Normalize(
            ActorProfileDefinition definition,
            ContentLoadContext context,
            string sourceFile,
            DataLoadReport report)
        {
            if (!ResolveDefinitionId(ref definition.id, "ActorProfileDefinition", context, sourceFile, report))
                return false;

            bool valid = true;
            ResolveOptionalReference(ref definition.equipment_layout_id, "ActorProfileDefinition", definition.id,
                "equipment_layout_id", context, sourceFile, report, ref valid);
            ResolveOptionalReference(ref definition.visual_rig_profile_id, "ActorProfileDefinition", definition.id,
                "visual_rig_profile_id", context, sourceFile, report, ref valid);

            ActorProfileInventoryEntry[] inventory = definition.initial_inventory ?? Array.Empty<ActorProfileInventoryEntry>();
            for (int index = 0; index < inventory.Length; index++)
            {
                if (inventory[index] == null)
                    continue;
                ResolveReference(ref inventory[index].item_id, "ActorProfileDefinition", definition.id,
                    $"initial_inventory[{index}].item_id", context, sourceFile, report, ref valid);
            }

            ActorProfileInitialEquipmentEntry[] equipment = definition.initial_equipment ?? Array.Empty<ActorProfileInitialEquipmentEntry>();
            for (int index = 0; index < equipment.Length; index++)
            {
                ActorProfileInitialEquipmentEntry entry = equipment[index];
                if (entry == null)
                    continue;
                ResolveReference(ref entry.item_id, "ActorProfileDefinition", definition.id,
                    $"initial_equipment[{index}].item_id", context, sourceFile, report, ref valid);
                ResolveEquipmentSlots(entry.slot_ids, "ActorProfileDefinition", definition.id,
                    $"initial_equipment[{index}].slot_ids", context, sourceFile, report, ref valid);
            }

            return valid;
        }

        internal static bool Normalize(
            AmmoProfileDefinition definition,
            ContentLoadContext context,
            string sourceFile,
            DataLoadReport report)
        {
            return ResolveDefinitionId(ref definition.id, "AmmoProfileDefinition", context, sourceFile, report);
        }

        internal static bool Normalize(
            AttachmentPoseDefinition definition,
            ContentLoadContext context,
            string sourceFile,
            DataLoadReport report)
        {
            if (!ResolveDefinitionId(ref definition.id, "AttachmentPoseDefinition", context, sourceFile, report))
                return false;

            bool valid = true;
            ResolveReference(ref definition.visual_profile_id, "AttachmentPoseDefinition", definition.id,
                "visual_profile_id", context, sourceFile, report, ref valid);
            ResolveOptionalReference(ref definition.rig_profile_id, "AttachmentPoseDefinition", definition.id,
                "rig_profile_id", context, sourceFile, report, ref valid);
            return valid;
        }

        internal static bool Normalize(
            EquipmentLayoutDefinition definition,
            ContentLoadContext context,
            string sourceFile,
            DataLoadReport report)
        {
            if (!ResolveDefinitionId(ref definition.id, "EquipmentLayoutDefinition", context, sourceFile, report))
                return false;

            bool valid = true;
            EquipmentLayoutSlotDefinition[] slots = definition.slots ?? Array.Empty<EquipmentLayoutSlotDefinition>();
            for (int index = 0; index < slots.Length; index++)
            {
                if (slots[index] == null)
                    continue;
                ResolveEquipmentSlot(ref slots[index].slot_id, "EquipmentLayoutDefinition", definition.id,
                    $"slots[{index}].slot_id", context, sourceFile, report, ref valid);
            }
            return valid;
        }

        internal static bool Normalize(
            EquipmentSlotDefinition definition,
            ContentLoadContext context,
            string sourceFile,
            DataLoadReport report)
        {
            return ResolveDefinitionId(ref definition.id, "EquipmentSlotDefinition", context, sourceFile, report);
        }

        internal static bool Normalize(
            FirearmProfileDefinition definition,
            ContentLoadContext context,
            string sourceFile,
            DataLoadReport report)
        {
            if (!ResolveDefinitionId(ref definition.id, "FirearmProfileDefinition", context, sourceFile, report))
                return false;

            bool valid = true;
            ResolveReferences(definition.accepted_ammo_profile_ids, "FirearmProfileDefinition", definition.id,
                "accepted_ammo_profile_ids", context, sourceFile, report, ref valid);
            return valid;
        }

        internal static bool Normalize(
            ItemDefinition definition,
            ContentLoadContext context,
            string sourceFile,
            DataLoadReport report)
        {
            if (!ResolveDefinitionId(ref definition.id, "ItemDefinition", context, sourceFile, report))
                return false;

            bool valid = true;
            ResolveOptionalReference(ref definition.owned_storage_profile_id, "ItemDefinition", definition.id,
                "owned_storage_profile_id", context, sourceFile, report, ref valid);
            ResolveOptionalReference(ref definition.firearm_profile_id, "ItemDefinition", definition.id,
                "firearm_profile_id", context, sourceFile, report, ref valid);
            ResolveOptionalReference(ref definition.ammo_profile_id, "ItemDefinition", definition.id,
                "ammo_profile_id", context, sourceFile, report, ref valid);

            if (definition.equip != null)
            {
                string[][] slotSets = definition.equip.slot_sets ?? Array.Empty<string[]>();
                for (int index = 0; index < slotSets.Length; index++)
                    ResolveEquipmentSlots(slotSets[index], "ItemDefinition", definition.id,
                        $"equip.slot_sets[{index}]", context, sourceFile, report, ref valid);
                ResolveEquipmentSlots(definition.equip.allowed_slots, "ItemDefinition", definition.id,
                    "equip.allowed_slots", context, sourceFile, report, ref valid);
                ResolveEquipmentSlots(definition.equip.occupied_slots, "ItemDefinition", definition.id,
                    "equip.occupied_slots", context, sourceFile, report, ref valid);
            }

            if (definition.combat != null)
            {
                ResolveReference(ref definition.combat.weapon_profile, "ItemDefinition", definition.id,
                    "combat.weapon_profile", context, sourceFile, report, ref valid);
                ResolveReferences(definition.combat.actions, "ItemDefinition", definition.id,
                    "combat.actions", context, sourceFile, report, ref valid);
            }

            return valid;
        }

        internal static bool Normalize(
            ItemStorageProfileDefinition definition,
            ContentLoadContext context,
            string sourceFile,
            DataLoadReport report)
        {
            return ResolveDefinitionId(ref definition.id, "ItemStorageProfileDefinition", context, sourceFile, report);
        }

        internal static bool Normalize(
            ItemVisualProfileDefinition definition,
            ContentLoadContext context,
            string sourceFile,
            DataLoadReport report)
        {
            if (!ResolveDefinitionId(ref definition.id, "ItemVisualProfileDefinition", context, sourceFile, report))
                return false;

            bool valid = true;
            ResolveReference(ref definition.item_definition_id, "ItemVisualProfileDefinition", definition.id,
                "item_definition_id", context, sourceFile, report, ref valid);
            ResolveReferences(definition.required_socket_capabilities, "ItemVisualProfileDefinition", definition.id,
                "required_socket_capabilities", context, sourceFile, report, ref valid);
            ResolveOptionalReference(ref definition.persistent_pose_id, "ItemVisualProfileDefinition", definition.id,
                "persistent_pose_id", context, sourceFile, report, ref valid);
            return valid;
        }

        internal static bool Normalize(
            LootTableDefinition definition,
            ContentLoadContext context,
            string sourceFile,
            DataLoadReport report)
        {
            if (!ResolveDefinitionId(ref definition.id, "LootTableDefinition", context, sourceFile, report))
                return false;

            bool valid = true;
            LootTableEntryDefinition[] entries = definition.entries ?? Array.Empty<LootTableEntryDefinition>();
            for (int index = 0; index < entries.Length; index++)
            {
                if (entries[index] == null)
                    continue;
                ResolveReference(ref entries[index].item_id, "LootTableDefinition", definition.id,
                    $"entries[{index}].item_id", context, sourceFile, report, ref valid);
            }
            return valid;
        }

        internal static bool Normalize(
            VisualAssetDefinition definition,
            ContentLoadContext context,
            string sourceFile,
            DataLoadReport report)
        {
            return ResolveDefinitionId(ref definition.id, "VisualAssetDefinition", context, sourceFile, report);
        }

        internal static bool Normalize(
            VisualRigCapabilityDefinition definition,
            ContentLoadContext context,
            string sourceFile,
            DataLoadReport report)
        {
            return ResolveDefinitionId(ref definition.id, "VisualRigCapabilityDefinition", context, sourceFile, report);
        }

        internal static bool Normalize(
            VisualRigProfileDefinition definition,
            ContentLoadContext context,
            string sourceFile,
            DataLoadReport report)
        {
            if (!ResolveDefinitionId(ref definition.id, "VisualRigProfileDefinition", context, sourceFile, report))
                return false;

            bool valid = true;
            VisualSocketDefinition[] sockets = definition.sockets ?? Array.Empty<VisualSocketDefinition>();
            for (int socketIndex = 0; socketIndex < sockets.Length; socketIndex++)
            {
                if (sockets[socketIndex] == null)
                    continue;
                ResolveReferences(sockets[socketIndex].capabilities, "VisualRigProfileDefinition", definition.id,
                    $"sockets[{socketIndex}].capabilities", context, sourceFile, report, ref valid);
            }

            VisualEquipmentSocketMappingDefinition[] mappings =
                definition.equipment_slot_mappings ?? Array.Empty<VisualEquipmentSocketMappingDefinition>();
            for (int index = 0; index < mappings.Length; index++)
            {
                if (mappings[index] == null)
                    continue;
                ResolveEquipmentSlot(ref mappings[index].equipment_slot_id, "VisualRigProfileDefinition", definition.id,
                    $"equipment_slot_mappings[{index}].equipment_slot_id", context, sourceFile, report, ref valid);
            }
            return valid;
        }

        internal static bool Normalize(
            WeaponProfileDefinition definition,
            ContentLoadContext context,
            string sourceFile,
            DataLoadReport report)
        {
            if (!ResolveDefinitionId(ref definition.id, "WeaponProfileDefinition", context, sourceFile, report))
                return false;

            bool valid = true;
            ResolveReferences(definition.default_actions, "WeaponProfileDefinition", definition.id,
                "default_actions", context, sourceFile, report, ref valid);
            return valid;
        }

        internal static bool Normalize(
            WorldObjectProfileDefinition definition,
            ContentLoadContext context,
            string sourceFile,
            DataLoadReport report)
        {
            // loot_table_id is currently an unsupported object shim and remains untouched.
            return ResolveDefinitionId(ref definition.id, "WorldObjectProfileDefinition", context, sourceFile, report);
        }

        private static bool ResolveDefinitionId(
            ref string value,
            string definitionType,
            ContentLoadContext context,
            string sourceFile,
            DataLoadReport report)
        {
            string raw = value;
            if (!TryResolve(raw, context, out ContentId contentId,
                    out bool usedLegacy, out string error))
            {
                report.Error($"{Source(context, sourceFile)}: {definitionType} has invalid Global Content ID " +
                             $"'{Safe(raw)}' in 'id': {error}.");
                return false;
            }

            if (context.IsCore && contentId.Namespace != ContentId.CoreNamespace)
            {
                report.Error($"{Source(context, sourceFile)}: {definitionType} id '{contentId.Canonical}' cannot be " +
                             $"declared by Core; Core definitions must use namespace '{ContentId.CoreNamespace}'.");
                return false;
            }

            if (!context.IsCore && contentId.Namespace == ContentId.CoreNamespace)
            {
                report.Error($"{Source(context, sourceFile)}: {definitionType} id '{contentId.Canonical}' cannot be " +
                             "declared by a non-Core source because namespace 'core' is reserved for official content.");
                return false;
            }

            value = contentId.Canonical;
            if (usedLegacy)
                context.RecordLegacy(raw, value, $"{definitionType}.id");
            return true;
        }

        private static void ResolveOptionalReference(
            ref string value,
            string definitionType,
            string definitionId,
            string fieldPath,
            ContentLoadContext context,
            string sourceFile,
            DataLoadReport report,
            ref bool valid)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            ResolveReference(ref value, definitionType, definitionId, fieldPath, context, sourceFile, report, ref valid);
        }

        private static void ResolveReference(
            ref string value,
            string definitionType,
            string definitionId,
            string fieldPath,
            ContentLoadContext context,
            string sourceFile,
            DataLoadReport report,
            ref bool valid)
        {
            string raw = value;
            if (!TryResolve(raw, context, out ContentId contentId,
                    out bool usedLegacy, out string error))
            {
                report.Error($"{Source(context, sourceFile)}: {definitionType} '{Safe(definitionId)}' has invalid " +
                             $"Global Content ID reference '{Safe(raw)}' in '{fieldPath}': {error}.");
                valid = false;
                return;
            }

            value = contentId.Canonical;
            if (usedLegacy)
                context.RecordLegacy(raw, value, $"{definitionType} '{definitionId}'.{fieldPath}");
        }

        private static void ResolveEquipmentSlot(
            ref string value,
            string definitionType,
            string definitionId,
            string fieldPath,
            ContentLoadContext context,
            string sourceFile,
            DataLoadReport report,
            ref bool valid)
        {
            if (!context.IsCore)
            {
                ResolveReference(ref value, definitionType, definitionId, fieldPath,
                    context, sourceFile, report, ref valid);
                return;
            }

            string raw = value;
            if (!ContentId.TryResolveLegacyCoreEquipmentSlot(
                    raw, out ContentId contentId, out bool usedLegacy, out string error))
            {
                report.Error($"{Source(context, sourceFile)}: {definitionType} '{Safe(definitionId)}' has invalid " +
                             $"Global Content ID reference '{Safe(raw)}' in '{fieldPath}': {error}.");
                valid = false;
                return;
            }

            value = contentId.Canonical;
            if (usedLegacy)
                context.RecordLegacy(raw, value, $"{definitionType} '{definitionId}'.{fieldPath}");
        }

        private static void ResolveReferences(
            string[] values,
            string definitionType,
            string definitionId,
            string fieldPath,
            ContentLoadContext context,
            string sourceFile,
            DataLoadReport report,
            ref bool valid)
        {
            if (values == null)
                return;
            for (int index = 0; index < values.Length; index++)
                ResolveReference(ref values[index], definitionType, definitionId, $"{fieldPath}[{index}]",
                    context, sourceFile, report, ref valid);
        }

        private static void ResolveEquipmentSlots(
            string[] values,
            string definitionType,
            string definitionId,
            string fieldPath,
            ContentLoadContext context,
            string sourceFile,
            DataLoadReport report,
            ref bool valid)
        {
            if (values == null)
                return;
            for (int index = 0; index < values.Length; index++)
                ResolveEquipmentSlot(ref values[index], definitionType, definitionId, $"{fieldPath}[{index}]",
                    context, sourceFile, report, ref valid);
        }

        private static bool TryResolve(
            string raw,
            ContentLoadContext context,
            out ContentId contentId,
            out bool usedLegacy,
            out string error)
        {
            return ContentId.TryResolve(
                raw,
                ContentId.CoreNamespace,
                context.IsCore,
                out contentId,
                out usedLegacy,
                out error);
        }

        private static string Source(ContentLoadContext context, string sourceFile)
        {
            return $"Mod source '{context.ModName}', file '{sourceFile}'";
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "<missing>" : value;
        }
    }
}
