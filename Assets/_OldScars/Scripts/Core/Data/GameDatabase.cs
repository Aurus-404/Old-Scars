using System.Collections.Generic;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Data.Loading;
using UnityEngine;

namespace OldScars.Core.Data
{
    /// <summary>
    /// In-memory database of immutable JSON definitions.
    ///
    /// Gameplay systems should not read JSON files directly. They should query
    /// this database by ID after GameDataManager reports IsReady == true.
    ///
    /// Runtime mutable state belongs to future instance classes and save data,
    /// not to these definitions.
    /// </summary>
    public sealed class GameDatabase
    {
        private readonly Dictionary<string, ItemDefinition> _items = new Dictionary<string, ItemDefinition>();
        private readonly Dictionary<string, WeaponProfileDefinition> _weaponProfiles = new Dictionary<string, WeaponProfileDefinition>();
        private readonly Dictionary<string, ActionDefinition> _actions = new Dictionary<string, ActionDefinition>();
        private readonly Dictionary<string, LootTableDefinition> _lootTables = new Dictionary<string, LootTableDefinition>();
        private readonly Dictionary<string, ActorProfileDefinition> _actorProfiles = new Dictionary<string, ActorProfileDefinition>();
        private readonly Dictionary<string, WorldObjectProfileDefinition> _worldObjectProfiles = new Dictionary<string, WorldObjectProfileDefinition>();

        public int ItemCount => _items.Count;
        public int WeaponProfileCount => _weaponProfiles.Count;
        public int ActionCount => _actions.Count;
        public int LootTableCount => _lootTables.Count;
        public int ActorProfileCount => _actorProfiles.Count;
        public int WorldObjectProfileCount => _worldObjectProfiles.Count;

        // Registration --------------------------------------------------------

        public void RegisterItem(ItemDefinition definition, DataLoadReport report)
        {
            Register(_items, definition != null ? definition.id : null, definition, "Item", report);
        }

        public void RegisterWeaponProfile(WeaponProfileDefinition definition, DataLoadReport report)
        {
            Register(_weaponProfiles, definition != null ? definition.id : null, definition, "WeaponProfile", report);
        }

        public void RegisterAction(ActionDefinition definition, DataLoadReport report)
        {
            Register(_actions, definition != null ? definition.id : null, definition, "Action", report);
        }

        public void RegisterLootTable(LootTableDefinition definition, DataLoadReport report)
        {
            Register(_lootTables, definition != null ? definition.id : null, definition, "LootTable", report);
        }

        public void RegisterActorProfile(ActorProfileDefinition definition, DataLoadReport report)
        {
            Register(_actorProfiles, definition != null ? definition.id : null, definition, "ActorProfile", report);
        }

        public void RegisterWorldObjectProfile(WorldObjectProfileDefinition definition, DataLoadReport report)
        {
            Register(_worldObjectProfiles, definition != null ? definition.id : null, definition, "WorldObjectProfile", report);
        }

        private static void Register<T>(Dictionary<string, T> dictionary, string id, T definition, string typeName, DataLoadReport report) where T : class
        {
            if (definition == null)
            {
                report.Error($"{typeName}: tried to register a null definition.");
                return;
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                report.Error($"{typeName}: tried to register a definition with null/empty id.");
                return;
            }

            if (dictionary.ContainsKey(id))
            {
                report.Error($"Duplicate {typeName} id '{id}'. The second definition was rejected.");
                return;
            }

            dictionary[id] = definition;
        }

        // Queries -------------------------------------------------------------

        public ItemDefinition GetItem(string id)
        {
            return Lookup(_items, id);
        }

        public WeaponProfileDefinition GetWeaponProfile(string id)
        {
            return Lookup(_weaponProfiles, id);
        }

        public ActionDefinition GetAction(string id)
        {
            return Lookup(_actions, id);
        }

        public LootTableDefinition GetLootTable(string id)
        {
            return Lookup(_lootTables, id);
        }

        public ActorProfileDefinition GetActorProfile(string id)
        {
            return Lookup(_actorProfiles, id);
        }

        public WorldObjectProfileDefinition GetWorldObjectProfile(string id)
        {
            return Lookup(_worldObjectProfiles, id);
        }

        private static T Lookup<T>(Dictionary<string, T> dictionary, string id) where T : class
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            T result;
            return dictionary.TryGetValue(id, out result) ? result : null;
        }

        // Bulk queries used by DataValidator ----------------------------------

        public IEnumerable<ItemDefinition> GetAllItems()
        {
            return _items.Values;
        }

        public IEnumerable<WeaponProfileDefinition> GetAllWeaponProfiles()
        {
            return _weaponProfiles.Values;
        }

        public IEnumerable<ActionDefinition> GetAllActions()
        {
            return _actions.Values;
        }

        public IEnumerable<LootTableDefinition> GetAllLootTables()
        {
            return _lootTables.Values;
        }

        public IEnumerable<ActorProfileDefinition> GetAllActorProfiles()
        {
            return _actorProfiles.Values;
        }

        public IEnumerable<WorldObjectProfileDefinition> GetAllWorldObjectProfiles()
        {
            return _worldObjectProfiles.Values;
        }

        public void LogStats()
        {
            Debug.Log("[GameDatabase] Loaded definitions:" +
                      $"\n  Items:           {_items.Count}" +
                      $"\n  WeaponProfiles:  {_weaponProfiles.Count}" +
                      $"\n  Actions:         {_actions.Count}" +
                      $"\n  LootTables:      {_lootTables.Count}" +
                      $"\n  ActorProfiles:   {_actorProfiles.Count}" +
                      $"\n  WorldObjectProfiles: {_worldObjectProfiles.Count}");
        }
    }
}
