using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using OldScars.Core.Data.Definitions;
using UnityEngine;

namespace OldScars.Core.Data.Loading
{
    /// <summary>
    /// Reads JSON definition files from StreamingAssets/Mods and registers them
    /// into TagRegistry and GameDatabase.
    ///
    /// Current definition families:
    /// - tags
    /// - weapon_profiles
    /// - actions
    /// - items
    /// - item_storage_profiles
    /// - loot_tables
    /// - actor_profiles
    /// - world_object_profiles
    /// - firearm_profiles
    /// - ammo_profiles
    /// - visual_capabilities / visual_rig_profiles
    /// - visual_assets / item_visual_profiles / attachment_poses
    ///
    /// Global Content IDs are canonicalized with source context before
    /// registration. Core is the only source allowed to qualify legacy IDs;
    /// external sources require explicit namespace:local_id until manifests
    /// provide authoritative mod identity and dependency context.
    /// </summary>
    public sealed class GameDataLoader
    {
        public GameDatabase Database { get; }
        public TagRegistry Tags { get; }

        private readonly string modsRootPath;
        private readonly DataLoadReport report;

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };

        public GameDataLoader(string modsRootPath, DataLoadReport report)
        {
            this.modsRootPath = modsRootPath;
            this.report = report;
            Database = new GameDatabase(report);
            Tags = new TagRegistry();
        }

        public void LoadAll()
        {
            if (!Directory.Exists(modsRootPath))
            {
                report.Error($"Mods root folder not found: '{modsRootPath}'. Expected Assets/StreamingAssets/Mods.");
                return;
            }

            List<string> modDirectories = GetOrderedModDirectories();

            if (modDirectories.Count == 0)
            {
                report.Error($"No mod folders found under '{modsRootPath}'. Expected at least Mods/Core.");
                return;
            }

            foreach (string modDirectory in modDirectories)
            {
                string modName = Path.GetFileName(modDirectory);
                bool isCore = string.Equals(modName, "Core", StringComparison.OrdinalIgnoreCase);
                var context = new ContentLoadContext(modName, modDirectory, isCore);
                Debug.Log($"[GameDataLoader] Loading mod source: {modName}" +
                          (isCore
                              ? $" (namespace '{ContentId.CoreNamespace}', Core legacy compatibility enabled)"
                              : " (canonical Global Content IDs required; manifest ownership pending)"));
                LoadMod(context);
                context.ReportLegacyUsage(report);
            }

            Database.LogStats();
        }

        private List<string> GetOrderedModDirectories()
        {
            var result = new List<string>();
            string[] directories = Directory.GetDirectories(modsRootPath);
            Array.Sort(directories, StringComparer.OrdinalIgnoreCase);

            string coreDirectory = null;

            foreach (string directory in directories)
            {
                string name = Path.GetFileName(directory);
                if (string.Equals(name, "Core", StringComparison.OrdinalIgnoreCase))
                {
                    coreDirectory = directory;
                    break;
                }
            }

            if (coreDirectory != null)
            {
                result.Add(coreDirectory);
            }
            else
            {
                report.Error("Required mod folder 'Core' was not found. Create Assets/StreamingAssets/Mods/Core.");
            }

            foreach (string directory in directories)
            {
                if (string.Equals(Path.GetFileName(directory), "Core", StringComparison.OrdinalIgnoreCase))
                    continue;

                result.Add(directory);
            }

            return result;
        }

        private void LoadMod(ContentLoadContext context)
        {
            string modDirectory = context.ModDirectory;
            LoadTagsFrom(Path.Combine(modDirectory, "tags"));
            LoadWeaponProfilesFrom(Path.Combine(modDirectory, "profiles"), context);
            LoadFirearmProfilesFrom(Path.Combine(modDirectory, "firearm_profiles"), context);
            LoadAmmoProfilesFrom(Path.Combine(modDirectory, "ammo_profiles"), context);
            LoadActionsFrom(Path.Combine(modDirectory, "actions"), context);
            LoadItemsFrom(Path.Combine(modDirectory, "items"), context);
            LoadItemStorageProfilesFrom(Path.Combine(modDirectory, "item_storage_profiles"), context);
            LoadEquipmentSlotsFrom(Path.Combine(modDirectory, "equipment_slots"), context);
            LoadEquipmentLayoutsFrom(Path.Combine(modDirectory, "equipment_layouts"), context);
            LoadVisualRigCapabilitiesFrom(Path.Combine(modDirectory, "visual_capabilities"), context);
            LoadVisualRigProfilesFrom(Path.Combine(modDirectory, "visual_rig_profiles"), context);
            LoadVisualAssetsFrom(Path.Combine(modDirectory, "visual_assets"), context);
            LoadItemVisualProfilesFrom(Path.Combine(modDirectory, "item_visual_profiles"), context);
            LoadAttachmentPosesFrom(Path.Combine(modDirectory, "attachment_poses"), context);
            LoadLootTablesFrom(Path.Combine(modDirectory, "loot_tables"), context);
            LoadActorProfilesFrom(Path.Combine(modDirectory, "actor_profiles"), context);
            LoadWorldObjectProfilesFrom(Path.Combine(modDirectory, "world_object_profiles"), context);
        }

        private void LoadTagsFrom(string directory)
        {
            foreach (string file in JsonFilesIn(directory))
            {
                TagsWrapper wrapper = Parse<TagsWrapper>(file);
                if (wrapper == null || wrapper.tags == null)
                {
                    report.Warning($"No 'tags' array found in {FileName(file)}.");
                    continue;
                }

                foreach (TagDefinition tag in wrapper.tags)
                    Tags.Register(tag, report);

                Debug.Log($"[GameDataLoader] Tags: {wrapper.tags.Length} entries from {FileName(file)}");
            }
        }

        private void LoadWeaponProfilesFrom(string directory, ContentLoadContext context)
        {
            foreach (string file in JsonFilesIn(directory))
            {
                WeaponProfilesWrapper wrapper = Parse<WeaponProfilesWrapper>(file);
                if (wrapper == null || wrapper.weapon_profiles == null)
                {
                    report.Warning($"No 'weapon_profiles' array found in {FileName(file)}.");
                    continue;
                }

                foreach (WeaponProfileDefinition profile in wrapper.weapon_profiles)
                    if (profile == null || DefinitionContentIdNormalizer.Normalize(profile, context, FileName(file), report))
                        Database.RegisterWeaponProfile(profile, report);

                Debug.Log($"[GameDataLoader] WeaponProfiles: {wrapper.weapon_profiles.Length} entries from {FileName(file)}");
            }
        }

        private void LoadFirearmProfilesFrom(string directory, ContentLoadContext context)
        {
            foreach (string file in JsonFilesIn(directory))
            {
                FirearmProfilesWrapper wrapper = Parse<FirearmProfilesWrapper>(file);
                if (wrapper == null || wrapper.firearm_profiles == null)
                {
                    report.Warning($"No 'firearm_profiles' array found in {FileName(file)}.");
                    continue;
                }

                foreach (FirearmProfileDefinition profile in wrapper.firearm_profiles)
                    if (profile == null || DefinitionContentIdNormalizer.Normalize(profile, context, FileName(file), report))
                        Database.RegisterFirearmProfile(profile, report);

                Debug.Log($"[GameDataLoader] FirearmProfiles: {wrapper.firearm_profiles.Length} entries from {FileName(file)}");
            }
        }

        private void LoadAmmoProfilesFrom(string directory, ContentLoadContext context)
        {
            foreach (string file in JsonFilesIn(directory))
            {
                AmmoProfilesWrapper wrapper = Parse<AmmoProfilesWrapper>(file);
                if (wrapper == null || wrapper.ammo_profiles == null)
                {
                    report.Warning($"No 'ammo_profiles' array found in {FileName(file)}.");
                    continue;
                }

                foreach (AmmoProfileDefinition profile in wrapper.ammo_profiles)
                    if (profile == null || DefinitionContentIdNormalizer.Normalize(profile, context, FileName(file), report))
                        Database.RegisterAmmoProfile(profile, report);

                Debug.Log($"[GameDataLoader] AmmoProfiles: {wrapper.ammo_profiles.Length} entries from {FileName(file)}");
            }
        }

        private void LoadActionsFrom(string directory, ContentLoadContext context)
        {
            foreach (string file in JsonFilesIn(directory))
            {
                ActionsWrapper wrapper = Parse<ActionsWrapper>(file);
                if (wrapper == null || wrapper.actions == null)
                {
                    report.Warning($"No 'actions' array found in {FileName(file)}.");
                    continue;
                }

                foreach (ActionDefinition action in wrapper.actions)
                    if (action == null || DefinitionContentIdNormalizer.Normalize(action, context, FileName(file), report))
                        Database.RegisterAction(action, report);

                Debug.Log($"[GameDataLoader] Actions: {wrapper.actions.Length} entries from {FileName(file)}");
            }
        }

        private void LoadItemsFrom(string directory, ContentLoadContext context)
        {
            foreach (string file in JsonFilesIn(directory))
            {
                ItemsWrapper wrapper = Parse<ItemsWrapper>(file);
                if (wrapper == null || wrapper.items == null)
                {
                    report.Warning($"No 'items' array found in {FileName(file)}.");
                    continue;
                }

                foreach (ItemDefinition item in wrapper.items)
                    if (item == null || DefinitionContentIdNormalizer.Normalize(item, context, FileName(file), report))
                        Database.RegisterItem(item, report);

                Debug.Log($"[GameDataLoader] Items: {wrapper.items.Length} entries from {FileName(file)}");
            }
        }

        private void LoadItemStorageProfilesFrom(string directory, ContentLoadContext context)
        {
            foreach (string file in JsonFilesIn(directory))
            {
                ItemStorageProfilesWrapper wrapper = Parse<ItemStorageProfilesWrapper>(file);
                if (wrapper == null || wrapper.item_storage_profiles == null)
                {
                    report.Warning($"No 'item_storage_profiles' array found in {FileName(file)}.");
                    continue;
                }

                foreach (ItemStorageProfileDefinition profile in wrapper.item_storage_profiles)
                    if (profile == null || DefinitionContentIdNormalizer.Normalize(profile, context, FileName(file), report))
                        Database.RegisterItemStorageProfile(profile, report);

                Debug.Log($"[GameDataLoader] ItemStorageProfiles: {wrapper.item_storage_profiles.Length} entries from {FileName(file)}");
            }
        }

        private void LoadEquipmentSlotsFrom(string directory, ContentLoadContext context)
        {
            foreach (string file in JsonFilesIn(directory))
            {
                EquipmentSlotsWrapper wrapper = Parse<EquipmentSlotsWrapper>(file);
                if (wrapper == null || wrapper.equipment_slots == null)
                {
                    report.Warning($"No 'equipment_slots' array found in {FileName(file)}.");
                    continue;
                }

                foreach (EquipmentSlotDefinition slot in wrapper.equipment_slots)
                    if (slot == null || DefinitionContentIdNormalizer.Normalize(slot, context, FileName(file), report))
                        Database.RegisterEquipmentSlot(slot, report);

                Debug.Log($"[GameDataLoader] EquipmentSlots: {wrapper.equipment_slots.Length} entries from {FileName(file)}");
            }
        }

        private void LoadEquipmentLayoutsFrom(string directory, ContentLoadContext context)
        {
            foreach (string file in JsonFilesIn(directory))
            {
                EquipmentLayoutsWrapper wrapper = Parse<EquipmentLayoutsWrapper>(file);
                if (wrapper == null || wrapper.equipment_layouts == null)
                {
                    report.Warning($"No 'equipment_layouts' array found in {FileName(file)}.");
                    continue;
                }

                foreach (EquipmentLayoutDefinition layout in wrapper.equipment_layouts)
                    if (layout == null || DefinitionContentIdNormalizer.Normalize(layout, context, FileName(file), report))
                        Database.RegisterEquipmentLayout(layout, report);

                Debug.Log($"[GameDataLoader] EquipmentLayouts: {wrapper.equipment_layouts.Length} entries from {FileName(file)}");
            }
        }

        private void LoadVisualRigCapabilitiesFrom(string directory, ContentLoadContext context)
        {
            foreach (string file in JsonFilesIn(directory))
            {
                VisualRigCapabilitiesWrapper wrapper = Parse<VisualRigCapabilitiesWrapper>(file);
                if (wrapper == null || wrapper.visual_rig_capabilities == null)
                {
                    report.Warning($"No 'visual_rig_capabilities' array found in {FileName(file)}.");
                    continue;
                }

                foreach (VisualRigCapabilityDefinition capability in wrapper.visual_rig_capabilities)
                    if (capability == null || DefinitionContentIdNormalizer.Normalize(capability, context, FileName(file), report))
                        Database.RegisterVisualRigCapability(capability, report);

                Debug.Log($"[GameDataLoader] VisualRigCapabilities: {wrapper.visual_rig_capabilities.Length} entries from {FileName(file)}");
            }
        }

        private void LoadVisualRigProfilesFrom(string directory, ContentLoadContext context)
        {
            foreach (string file in JsonFilesIn(directory))
            {
                VisualRigProfilesWrapper wrapper = Parse<VisualRigProfilesWrapper>(file);
                if (wrapper == null || wrapper.visual_rig_profiles == null)
                {
                    report.Warning($"No 'visual_rig_profiles' array found in {FileName(file)}.");
                    continue;
                }

                foreach (VisualRigProfileDefinition profile in wrapper.visual_rig_profiles)
                    if (profile == null || DefinitionContentIdNormalizer.Normalize(profile, context, FileName(file), report))
                        Database.RegisterVisualRigProfile(profile, report);

                Debug.Log($"[GameDataLoader] VisualRigProfiles: {wrapper.visual_rig_profiles.Length} entries from {FileName(file)}");
            }
        }

        private void LoadVisualAssetsFrom(string directory, ContentLoadContext context)
        {
            foreach (string file in JsonFilesIn(directory))
            {
                VisualAssetsWrapper wrapper = Parse<VisualAssetsWrapper>(file);
                if (wrapper == null || wrapper.visual_assets == null)
                {
                    report.Warning($"No 'visual_assets' array found in {FileName(file)}.");
                    continue;
                }

                foreach (VisualAssetDefinition asset in wrapper.visual_assets)
                    if (asset == null || DefinitionContentIdNormalizer.Normalize(asset, context, FileName(file), report))
                        Database.RegisterVisualAsset(asset, report);

                Debug.Log($"[GameDataLoader] VisualAssets: {wrapper.visual_assets.Length} entries from {FileName(file)}");
            }
        }

        private void LoadItemVisualProfilesFrom(string directory, ContentLoadContext context)
        {
            foreach (string file in JsonFilesIn(directory))
            {
                ItemVisualProfilesWrapper wrapper = Parse<ItemVisualProfilesWrapper>(file);
                if (wrapper == null || wrapper.item_visual_profiles == null)
                {
                    report.Warning($"No 'item_visual_profiles' array found in {FileName(file)}.");
                    continue;
                }

                foreach (ItemVisualProfileDefinition profile in wrapper.item_visual_profiles)
                    if (profile == null || DefinitionContentIdNormalizer.Normalize(profile, context, FileName(file), report))
                        Database.RegisterItemVisualProfile(profile, report);

                Debug.Log($"[GameDataLoader] ItemVisualProfiles: {wrapper.item_visual_profiles.Length} entries from {FileName(file)}");
            }
        }

        private void LoadAttachmentPosesFrom(string directory, ContentLoadContext context)
        {
            foreach (string file in JsonFilesIn(directory))
            {
                AttachmentPosesWrapper wrapper = Parse<AttachmentPosesWrapper>(file);
                if (wrapper == null || wrapper.attachment_poses == null)
                {
                    report.Warning($"No 'attachment_poses' array found in {FileName(file)}.");
                    continue;
                }

                foreach (AttachmentPoseDefinition pose in wrapper.attachment_poses)
                    if (pose == null || DefinitionContentIdNormalizer.Normalize(pose, context, FileName(file), report))
                        Database.RegisterAttachmentPose(pose, report);

                Debug.Log($"[GameDataLoader] AttachmentPoses: {wrapper.attachment_poses.Length} entries from {FileName(file)}");
            }
        }

        private void LoadLootTablesFrom(string directory, ContentLoadContext context)
        {
            foreach (string file in JsonFilesIn(directory))
            {
                LootTablesWrapper wrapper = Parse<LootTablesWrapper>(file);
                if (wrapper == null || wrapper.loot_tables == null)
                {
                    report.Warning($"No 'loot_tables' array found in {FileName(file)}.");
                    continue;
                }

                foreach (LootTableDefinition lootTable in wrapper.loot_tables)
                    if (lootTable == null || DefinitionContentIdNormalizer.Normalize(lootTable, context, FileName(file), report))
                        Database.RegisterLootTable(lootTable, report);

                Debug.Log($"[GameDataLoader] LootTables: {wrapper.loot_tables.Length} entries from {FileName(file)}");
            }
        }

        private void LoadActorProfilesFrom(string directory, ContentLoadContext context)
        {
            foreach (string file in JsonFilesIn(directory))
            {
                ActorProfilesWrapper wrapper = Parse<ActorProfilesWrapper>(file);
                if (wrapper == null || wrapper.actor_profiles == null)
                {
                    report.Warning($"No 'actor_profiles' array found in {FileName(file)}.");
                    continue;
                }

                foreach (ActorProfileDefinition actorProfile in wrapper.actor_profiles)
                    if (actorProfile == null || DefinitionContentIdNormalizer.Normalize(actorProfile, context, FileName(file), report))
                        Database.RegisterActorProfile(actorProfile, report);

                Debug.Log($"[GameDataLoader] ActorProfiles: {wrapper.actor_profiles.Length} entries from {FileName(file)}");
            }
        }

        private void LoadWorldObjectProfilesFrom(string directory, ContentLoadContext context)
        {
            foreach (string file in JsonFilesIn(directory))
            {
                WorldObjectProfilesWrapper wrapper = Parse<WorldObjectProfilesWrapper>(file);
                if (wrapper == null || wrapper.world_object_profiles == null)
                {
                    report.Warning($"No 'world_object_profiles' array found in {FileName(file)}.");
                    continue;
                }

                foreach (WorldObjectProfileDefinition worldObjectProfile in wrapper.world_object_profiles)
                    if (worldObjectProfile == null || DefinitionContentIdNormalizer.Normalize(worldObjectProfile, context, FileName(file), report))
                        Database.RegisterWorldObjectProfile(worldObjectProfile, report);

                Debug.Log($"[GameDataLoader] WorldObjectProfiles: {wrapper.world_object_profiles.Length} entries from {FileName(file)}");
            }
        }

        private IEnumerable<string> JsonFilesIn(string directory)
        {
            if (!Directory.Exists(directory))
                yield break;

            string[] files = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            foreach (string file in files)
                yield return file;
        }

        private T Parse<T>(string path) where T : class
        {
            try
            {
                string json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<T>(json, JsonSettings);
            }
            catch (Exception ex)
            {
                report.Error($"Failed to parse '{path}': {ex.Message}");
                return null;
            }
        }

        private static string FileName(string path)
        {
            return Path.GetFileName(path);
        }

        [Serializable] private sealed class TagsWrapper { public TagDefinition[] tags; }
        [Serializable] private sealed class WeaponProfilesWrapper { public WeaponProfileDefinition[] weapon_profiles; }
        [Serializable] private sealed class FirearmProfilesWrapper { public FirearmProfileDefinition[] firearm_profiles; }
        [Serializable] private sealed class AmmoProfilesWrapper { public AmmoProfileDefinition[] ammo_profiles; }
        [Serializable] private sealed class ActionsWrapper { public ActionDefinition[] actions; }
        [Serializable] private sealed class ItemsWrapper { public ItemDefinition[] items; }
        [Serializable] private sealed class ItemStorageProfilesWrapper { public ItemStorageProfileDefinition[] item_storage_profiles; }
        [Serializable] private sealed class EquipmentSlotsWrapper { public EquipmentSlotDefinition[] equipment_slots; }
        [Serializable] private sealed class EquipmentLayoutsWrapper { public EquipmentLayoutDefinition[] equipment_layouts; }
        [Serializable] private sealed class VisualRigCapabilitiesWrapper { public VisualRigCapabilityDefinition[] visual_rig_capabilities; }
        [Serializable] private sealed class VisualRigProfilesWrapper { public VisualRigProfileDefinition[] visual_rig_profiles; }
        [Serializable] private sealed class VisualAssetsWrapper { public VisualAssetDefinition[] visual_assets; }
        [Serializable] private sealed class ItemVisualProfilesWrapper { public ItemVisualProfileDefinition[] item_visual_profiles; }
        [Serializable] private sealed class AttachmentPosesWrapper { public AttachmentPoseDefinition[] attachment_poses; }
        [Serializable] private sealed class LootTablesWrapper { public LootTableDefinition[] loot_tables; }
        [Serializable] private sealed class ActorProfilesWrapper { public ActorProfileDefinition[] actor_profiles; }
        [Serializable] private sealed class WorldObjectProfilesWrapper { public WorldObjectProfileDefinition[] world_object_profiles; }
    }
}
