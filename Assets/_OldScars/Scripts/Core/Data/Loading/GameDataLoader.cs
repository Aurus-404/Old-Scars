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
    /// Milestone 1 scope:
    /// - tags
    /// - weapon_profiles
    /// - actions
    /// - items
    /// - loot_tables
    ///
    /// No entities, save system, IA, firearms or protection profiles yet.
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
            Database = new GameDatabase();
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
                Debug.Log($"[GameDataLoader] Loading mod: {modName}");
                LoadMod(modDirectory);
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

        private void LoadMod(string modDirectory)
        {
            LoadTagsFrom(Path.Combine(modDirectory, "tags"));
            LoadWeaponProfilesFrom(Path.Combine(modDirectory, "profiles"));
            LoadActionsFrom(Path.Combine(modDirectory, "actions"));
            LoadItemsFrom(Path.Combine(modDirectory, "items"));
            LoadLootTablesFrom(Path.Combine(modDirectory, "loot_tables"));
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

        private void LoadWeaponProfilesFrom(string directory)
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
                    Database.RegisterWeaponProfile(profile, report);

                Debug.Log($"[GameDataLoader] WeaponProfiles: {wrapper.weapon_profiles.Length} entries from {FileName(file)}");
            }
        }

        private void LoadActionsFrom(string directory)
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
                    Database.RegisterAction(action, report);

                Debug.Log($"[GameDataLoader] Actions: {wrapper.actions.Length} entries from {FileName(file)}");
            }
        }

        private void LoadItemsFrom(string directory)
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
                    Database.RegisterItem(item, report);

                Debug.Log($"[GameDataLoader] Items: {wrapper.items.Length} entries from {FileName(file)}");
            }
        }

        private void LoadLootTablesFrom(string directory)
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
                    Database.RegisterLootTable(lootTable, report);

                Debug.Log($"[GameDataLoader] LootTables: {wrapper.loot_tables.Length} entries from {FileName(file)}");
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
        [Serializable] private sealed class ActionsWrapper { public ActionDefinition[] actions; }
        [Serializable] private sealed class ItemsWrapper { public ItemDefinition[] items; }
        [Serializable] private sealed class LootTablesWrapper { public LootTableDefinition[] loot_tables; }
    }
}
