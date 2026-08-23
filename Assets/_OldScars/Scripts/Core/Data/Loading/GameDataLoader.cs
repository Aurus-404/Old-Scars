using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
    /// - armor_profiles
    /// - penetration_profiles
    /// - visual_capabilities / visual_rig_profiles
    /// - visual_assets / item_visual_profiles / attachment_poses
    ///
    /// Every source is identified by a root manifest before any definition is
    /// registered. Global Content IDs are canonicalized with that source context;
    /// official Core alone may qualify legacy IDs. Manifests establish identity,
    /// namespace ownership and provenance metadata, not dependency or compatibility
    /// policy.
    /// </summary>
    public sealed class GameDataLoader
    {
        public const string ManifestFileName = "manifest.json";
        public const string OfficialCoreSourceId = "old_scars_core";

        public GameDatabase Database { get; }
        public TagRegistry Tags { get; }

        private readonly string modsRootPath;
        private readonly DataLoadReport report;
        private List<ContentLoadContext> loadedSourceContexts;

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };

        private static readonly JsonSerializerSettings ManifestJsonSettings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
            NullValueHandling = NullValueHandling.Include
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
            loadedSourceContexts = null;
            if (!Directory.Exists(modsRootPath))
            {
                report.Error($"Mods root folder not found: '{modsRootPath}'. Expected Assets/StreamingAssets/Mods.");
                return;
            }

            List<ContentLoadContext> sources = DiscoverAndValidateSources();
            if (report.HasErrors)
                return;

            if (sources.Count == 0)
            {
                report.Error($"No content-source folders found under '{modsRootPath}'.");
                return;
            }

            foreach (ContentLoadContext context in sources)
            {
                Debug.Log($"[GameDataLoader] Loading content source '{context.SourceId}' " +
                          $"version '{context.Version}' owning namespace '{context.OwnedNamespace}'" +
                          (context.IsOfficialCore ? " (official Core legacy compatibility enabled)" : string.Empty));
                LoadMod(context);
                context.ReportLegacyUsage(report);
            }

            loadedSourceContexts = sources;
            Database.LogStats();
        }

        public bool TryBuildLoadedContentSet(out LoadedContentSet loadedContentSet)
        {
            loadedContentSet = null;
            if (report.HasErrors || loadedSourceContexts == null)
                return false;

            var sources = new List<LoadedContentSource>(loadedSourceContexts.Count);
            foreach (ContentLoadContext context in loadedSourceContexts)
            {
                try
                {
                    sources.Add(ContentProvenance.BuildSource(context));
                }
                catch (Exception exception)
                {
                    report.Error(
                        $"Content source '{context.SourceId}' provenance could not be built from its recognized inputs: " +
                        exception.Message);
                    return false;
                }
            }

            loadedContentSet = ContentProvenance.BuildSet(sources);
            return true;
        }

        private List<ContentLoadContext> DiscoverAndValidateSources()
        {
            var discovered = new List<ContentLoadContext>();
            string[] directories = Directory.GetDirectories(modsRootPath);
            Array.Sort(directories, StringComparer.Ordinal);
            if (directories.Length == 0)
            {
                report.Error($"No content-source folders found under '{modsRootPath}'. Expected official Core content.");
                return discovered;
            }

            foreach (string directory in directories)
            {
                ContentSourceManifest manifest = ParseManifest(directory);
                if (manifest == null)
                    continue;

                bool valid = ValidateManifest(manifest, directory);
                if (!valid)
                    continue;

                bool isOfficialCore =
                    string.Equals(manifest.SourceId, OfficialCoreSourceId, StringComparison.Ordinal) &&
                    string.Equals(manifest.OwnedNamespace, ContentId.CoreNamespace, StringComparison.Ordinal);
                discovered.Add(new ContentLoadContext(
                    manifest.SourceId,
                    manifest.OwnedNamespace,
                    manifest.Version,
                    directory,
                    isOfficialCore));
            }

            ValidateUniqueSourceIdentity(discovered);
            if (report.HasErrors)
                return new List<ContentLoadContext>();

            discovered.Sort(CompareSources);
            return discovered;
        }

        private ContentSourceManifest ParseManifest(string sourceDirectory)
        {
            string manifestPath = Path.Combine(sourceDirectory, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                report.Error(
                    $"Content source folder '{sourceDirectory}' is missing required root manifest '{ManifestFileName}'. " +
                    "Source identity is never inferred from the folder name.");
                return null;
            }

            try
            {
                string json = File.ReadAllText(manifestPath);
                ContentSourceManifest manifest =
                    JsonConvert.DeserializeObject<ContentSourceManifest>(json, ManifestJsonSettings);
                if (manifest == null)
                    report.Error($"Content source manifest '{manifestPath}' is empty or deserialized to null.");
                return manifest;
            }
            catch (Exception exception)
            {
                report.Error($"Failed to parse content source manifest '{manifestPath}': {exception.Message}");
                return null;
            }
        }

        private bool ValidateManifest(ContentSourceManifest manifest, string sourceDirectory)
        {
            bool valid = true;
            string context = $"Content source manifest in '{sourceDirectory}'";

            if (!ContentId.TryValidateLocalId(manifest.SourceId, out string sourceIdError))
            {
                report.Error($"{context}: required 'source_id' is invalid: {sourceIdError}.");
                valid = false;
            }

            if (!ContentId.TryValidateNamespace(manifest.OwnedNamespace, out string namespaceError))
            {
                report.Error($"{context}: required 'namespace' is invalid: {namespaceError}.");
                valid = false;
            }

            if (!TryValidateVersion(manifest.Version, out string versionError))
            {
                report.Error($"{context}: required 'version' is invalid: {versionError}.");
                valid = false;
            }

            if (!valid)
                return false;

            bool claimsCoreSourceId =
                string.Equals(manifest.SourceId, OfficialCoreSourceId, StringComparison.Ordinal);
            bool claimsCoreNamespace =
                string.Equals(manifest.OwnedNamespace, ContentId.CoreNamespace, StringComparison.Ordinal);
            if (claimsCoreSourceId != claimsCoreNamespace)
            {
                report.Error(
                    $"Content source '{manifest.SourceId}' cannot claim the reserved official Core identity or " +
                    $"namespace independently. Official Core requires source_id '{OfficialCoreSourceId}' and " +
                    $"namespace '{ContentId.CoreNamespace}'; external sources may claim neither.");
                return false;
            }

            return true;
        }

        private void ValidateUniqueSourceIdentity(List<ContentLoadContext> sources)
        {
            var sourceIds = new Dictionary<string, ContentLoadContext>(StringComparer.Ordinal);
            var namespaces = new Dictionary<string, ContentLoadContext>(StringComparer.Ordinal);
            int officialCoreCount = 0;

            foreach (ContentLoadContext source in sources)
            {
                if (source.IsOfficialCore)
                    officialCoreCount++;

                if (sourceIds.TryGetValue(source.SourceId, out ContentLoadContext sourceIdOwner))
                {
                    report.Error(
                        $"Duplicate content source_id '{source.SourceId}' in source roots " +
                        $"'{sourceIdOwner.SourceRootPath}' and '{source.SourceRootPath}'.");
                }
                else
                {
                    sourceIds[source.SourceId] = source;
                }

                if (namespaces.TryGetValue(source.OwnedNamespace, out ContentLoadContext namespaceOwner))
                {
                    report.Error(
                        $"Duplicate owned namespace '{source.OwnedNamespace}' in content sources " +
                        $"'{namespaceOwner.SourceId}' and '{source.SourceId}'. Exactly one source may own a namespace.");
                }
                else
                {
                    namespaces[source.OwnedNamespace] = source;
                }
            }

            if (officialCoreCount != 1)
            {
                report.Error(
                    $"Exactly one official Core content source is required with source_id '{OfficialCoreSourceId}' " +
                    $"and namespace '{ContentId.CoreNamespace}' (found {officialCoreCount}).");
            }
        }

        private static int CompareSources(ContentLoadContext left, ContentLoadContext right)
        {
            if (left.IsOfficialCore != right.IsOfficialCore)
                return left.IsOfficialCore ? -1 : 1;
            return string.CompareOrdinal(left.SourceId, right.SourceId);
        }

        private static bool TryValidateVersion(string version, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(version))
            {
                error = "value is null, empty or whitespace";
                return false;
            }

            if (!string.Equals(version, version.Trim(), StringComparison.Ordinal))
            {
                error = "leading or trailing whitespace is not allowed";
                return false;
            }

            for (int index = 0; index < version.Length; index++)
            {
                if (char.IsControl(version[index]))
                {
                    error = $"control character at position {index} is not allowed";
                    return false;
                }
            }

            return true;
        }

        private void LoadMod(ContentLoadContext context)
        {
            string modDirectory = context.SourceRootPath;
            LoadTagsFrom(Path.Combine(modDirectory, "tags"), context);
            LoadWeaponProfilesFrom(Path.Combine(modDirectory, "profiles"), context);
            LoadFirearmProfilesFrom(Path.Combine(modDirectory, "firearm_profiles"), context);
            LoadAmmoProfilesFrom(Path.Combine(modDirectory, "ammo_profiles"), context);
            LoadPenetrationProfilesFrom(Path.Combine(modDirectory, "penetration_profiles"), context);
            LoadArmorProfilesFrom(Path.Combine(modDirectory, "armor_profiles"), context);
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

        private void LoadTagsFrom(string directory, ContentLoadContext context)
        {
            foreach (string file in JsonFilesIn(directory, context))
            {
                TagsWrapper wrapper = Parse<TagsWrapper>(file, context);
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
            foreach (string file in JsonFilesIn(directory, context))
            {
                WeaponProfilesWrapper wrapper = Parse<WeaponProfilesWrapper>(file, context);
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
            foreach (string file in JsonFilesIn(directory, context))
            {
                FirearmProfilesWrapper wrapper = Parse<FirearmProfilesWrapper>(file, context);
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
            foreach (string file in JsonFilesIn(directory, context))
            {
                AmmoProfilesWrapper wrapper = Parse<AmmoProfilesWrapper>(file, context);
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

        private void LoadArmorProfilesFrom(string directory, ContentLoadContext context)
        {
            foreach (string file in JsonFilesIn(directory, context))
            {
                ArmorProfilesWrapper wrapper = Parse<ArmorProfilesWrapper>(file, context);
                if (wrapper == null || wrapper.armor_profiles == null)
                {
                    report.Warning($"No 'armor_profiles' array found in {FileName(file)}.");
                    continue;
                }

                foreach (ArmorProfileDefinition profile in wrapper.armor_profiles)
                    if (profile == null || DefinitionContentIdNormalizer.Normalize(profile, context, FileName(file), report))
                        Database.RegisterArmorProfile(profile, report);

                Debug.Log($"[GameDataLoader] ArmorProfiles: {wrapper.armor_profiles.Length} entries from {FileName(file)}");
            }
        }

        private void LoadPenetrationProfilesFrom(string directory, ContentLoadContext context)
        {
            foreach (string file in JsonFilesIn(directory, context))
            {
                PenetrationProfilesWrapper wrapper = Parse<PenetrationProfilesWrapper>(file, context);
                if (wrapper == null || wrapper.penetration_profiles == null)
                {
                    report.Warning($"No 'penetration_profiles' array found in {FileName(file)}.");
                    continue;
                }

                foreach (PenetrationProfileDefinition profile in wrapper.penetration_profiles)
                    if (profile == null || DefinitionContentIdNormalizer.Normalize(profile, context, FileName(file), report))
                        Database.RegisterPenetrationProfile(profile, report);

                Debug.Log($"[GameDataLoader] PenetrationProfiles: {wrapper.penetration_profiles.Length} entries from {FileName(file)}");
            }
        }

        private void LoadActionsFrom(string directory, ContentLoadContext context)
        {
            foreach (string file in JsonFilesIn(directory, context))
            {
                ActionsWrapper wrapper = Parse<ActionsWrapper>(file, context);
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
            foreach (string file in JsonFilesIn(directory, context))
            {
                ItemsWrapper wrapper = Parse<ItemsWrapper>(file, context);
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
            foreach (string file in JsonFilesIn(directory, context))
            {
                ItemStorageProfilesWrapper wrapper = Parse<ItemStorageProfilesWrapper>(file, context);
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
            foreach (string file in JsonFilesIn(directory, context))
            {
                EquipmentSlotsWrapper wrapper = Parse<EquipmentSlotsWrapper>(file, context);
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
            foreach (string file in JsonFilesIn(directory, context))
            {
                EquipmentLayoutsWrapper wrapper = Parse<EquipmentLayoutsWrapper>(file, context);
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
            foreach (string file in JsonFilesIn(directory, context))
            {
                VisualRigCapabilitiesWrapper wrapper = Parse<VisualRigCapabilitiesWrapper>(file, context);
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
            foreach (string file in JsonFilesIn(directory, context))
            {
                VisualRigProfilesWrapper wrapper = Parse<VisualRigProfilesWrapper>(file, context);
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
            foreach (string file in JsonFilesIn(directory, context))
            {
                VisualAssetsWrapper wrapper = Parse<VisualAssetsWrapper>(file, context);
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
            foreach (string file in JsonFilesIn(directory, context))
            {
                ItemVisualProfilesWrapper wrapper = Parse<ItemVisualProfilesWrapper>(file, context);
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
            foreach (string file in JsonFilesIn(directory, context))
            {
                AttachmentPosesWrapper wrapper = Parse<AttachmentPosesWrapper>(file, context);
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
            foreach (string file in JsonFilesIn(directory, context))
            {
                LootTablesWrapper wrapper = Parse<LootTablesWrapper>(file, context);
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
            foreach (string file in JsonFilesIn(directory, context))
            {
                ActorProfilesWrapper wrapper = Parse<ActorProfilesWrapper>(file, context);
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
            foreach (string file in JsonFilesIn(directory, context))
            {
                WorldObjectProfilesWrapper wrapper = Parse<WorldObjectProfilesWrapper>(file, context);
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

        private IEnumerable<string> JsonFilesIn(string directory, ContentLoadContext context)
        {
            if (!Directory.Exists(directory))
                yield break;

            string[] files = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.Ordinal);

            foreach (string file in files)
                yield return file;
        }

        private T Parse<T>(string path, ContentLoadContext context) where T : class
        {
            try
            {
                byte[] exactBytes = File.ReadAllBytes(path);
                context.RecordRecognizedInput(path, exactBytes);
                string json;
                using (var reader = new StreamReader(
                           new MemoryStream(exactBytes),
                           Encoding.UTF8,
                           true))
                {
                    json = reader.ReadToEnd();
                }
                return JsonConvert.DeserializeObject<T>(json, JsonSettings);
            }
            catch (Exception ex)
            {
                string relativePath = Path.GetRelativePath(context.SourceRootPath, path).Replace('\\', '/');
                report.Error(
                    $"Content source '{context.SourceId}' failed to parse recognized input '{relativePath}': " +
                    ex.Message);
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
        [Serializable] private sealed class ArmorProfilesWrapper { public ArmorProfileDefinition[] armor_profiles; }
        [Serializable] private sealed class PenetrationProfilesWrapper { public PenetrationProfileDefinition[] penetration_profiles; }
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
