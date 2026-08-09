using System;
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
        private readonly DataLoadReport report;
        private readonly HashSet<string> reportedLegacyLookups = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, ItemDefinition> _items = new Dictionary<string, ItemDefinition>();
        private readonly Dictionary<string, ItemStorageProfileDefinition> _itemStorageProfiles = new Dictionary<string, ItemStorageProfileDefinition>();
        private readonly Dictionary<string, EquipmentSlotDefinition> _equipmentSlots = new Dictionary<string, EquipmentSlotDefinition>();
        private readonly Dictionary<string, EquipmentLayoutDefinition> _equipmentLayouts = new Dictionary<string, EquipmentLayoutDefinition>();
        private readonly Dictionary<string, WeaponProfileDefinition> _weaponProfiles = new Dictionary<string, WeaponProfileDefinition>();
        private readonly Dictionary<string, FirearmProfileDefinition> _firearmProfiles = new Dictionary<string, FirearmProfileDefinition>();
        private readonly Dictionary<string, AmmoProfileDefinition> _ammoProfiles = new Dictionary<string, AmmoProfileDefinition>();
        private readonly Dictionary<string, ActionDefinition> _actions = new Dictionary<string, ActionDefinition>();
        private readonly Dictionary<string, LootTableDefinition> _lootTables = new Dictionary<string, LootTableDefinition>();
        private readonly Dictionary<string, ActorProfileDefinition> _actorProfiles = new Dictionary<string, ActorProfileDefinition>();
        private readonly Dictionary<string, WorldObjectProfileDefinition> _worldObjectProfiles = new Dictionary<string, WorldObjectProfileDefinition>();
        private readonly Dictionary<string, VisualRigCapabilityDefinition> _visualRigCapabilities = new Dictionary<string, VisualRigCapabilityDefinition>();
        private readonly Dictionary<string, VisualRigProfileDefinition> _visualRigProfiles = new Dictionary<string, VisualRigProfileDefinition>();
        private readonly Dictionary<string, VisualAssetDefinition> _visualAssets = new Dictionary<string, VisualAssetDefinition>();
        private readonly Dictionary<string, VisualAssetDefinition> _visualAssetsByKey = new Dictionary<string, VisualAssetDefinition>();
        private readonly Dictionary<string, ItemVisualProfileDefinition> _itemVisualProfiles = new Dictionary<string, ItemVisualProfileDefinition>();
        private readonly Dictionary<string, ItemVisualProfileDefinition> _itemVisualProfilesByItem = new Dictionary<string, ItemVisualProfileDefinition>();
        private readonly Dictionary<string, AttachmentPoseDefinition> _attachmentPoses = new Dictionary<string, AttachmentPoseDefinition>();

        public int ItemCount => _items.Count;
        public int ItemStorageProfileCount => _itemStorageProfiles.Count;
        public int EquipmentSlotCount => _equipmentSlots.Count;
        public int EquipmentLayoutCount => _equipmentLayouts.Count;
        public int WeaponProfileCount => _weaponProfiles.Count;
        public int FirearmProfileCount => _firearmProfiles.Count;
        public int AmmoProfileCount => _ammoProfiles.Count;
        public int ActionCount => _actions.Count;
        public int LootTableCount => _lootTables.Count;
        public int ActorProfileCount => _actorProfiles.Count;
        public int WorldObjectProfileCount => _worldObjectProfiles.Count;
        public int VisualRigCapabilityCount => _visualRigCapabilities.Count;
        public int VisualRigProfileCount => _visualRigProfiles.Count;
        public int VisualAssetCount => _visualAssets.Count;
        public int ItemVisualProfileCount => _itemVisualProfiles.Count;
        public int AttachmentPoseCount => _attachmentPoses.Count;

        public GameDatabase(DataLoadReport report = null)
        {
            this.report = report;
        }

        // Registration --------------------------------------------------------

        public void RegisterItem(ItemDefinition definition, DataLoadReport report)
        {
            Register(_items, definition != null ? definition.id : null, definition, "Item", report);
        }

        public void RegisterItemStorageProfile(ItemStorageProfileDefinition definition, DataLoadReport report)
        {
            Register(_itemStorageProfiles, definition != null ? definition.id : null, definition, "ItemStorageProfile", report);
        }

        public void RegisterEquipmentSlot(EquipmentSlotDefinition definition, DataLoadReport report)
        {
            Register(_equipmentSlots, definition != null ? definition.id : null, definition, "EquipmentSlot", report);
        }

        public void RegisterEquipmentLayout(EquipmentLayoutDefinition definition, DataLoadReport report)
        {
            Register(_equipmentLayouts, definition != null ? definition.id : null, definition, "EquipmentLayout", report);
        }

        public void RegisterWeaponProfile(WeaponProfileDefinition definition, DataLoadReport report)
        {
            Register(_weaponProfiles, definition != null ? definition.id : null, definition, "WeaponProfile", report);
        }

        public void RegisterFirearmProfile(FirearmProfileDefinition definition, DataLoadReport report)
        {
            Register(_firearmProfiles, definition != null ? definition.id : null, definition, "FirearmProfile", report);
        }

        public void RegisterAmmoProfile(AmmoProfileDefinition definition, DataLoadReport report)
        {
            Register(_ammoProfiles, definition != null ? definition.id : null, definition, "AmmoProfile", report);
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

        public void RegisterVisualRigCapability(VisualRigCapabilityDefinition definition, DataLoadReport report)
        {
            Register(_visualRigCapabilities, definition != null ? definition.id : null, definition, "VisualRigCapability", report);
        }

        public void RegisterVisualRigProfile(VisualRigProfileDefinition definition, DataLoadReport report)
        {
            Register(_visualRigProfiles, definition != null ? definition.id : null, definition, "VisualRigProfile", report);
        }

        public void RegisterVisualAsset(VisualAssetDefinition definition, DataLoadReport report)
        {
            int before = _visualAssets.Count;
            Register(_visualAssets, definition != null ? definition.id : null, definition, "VisualAsset", report);
            if (_visualAssets.Count == before || definition == null || string.IsNullOrWhiteSpace(definition.asset_key))
                return;

            if (_visualAssetsByKey.ContainsKey(definition.asset_key))
            {
                report.Error($"Duplicate VisualAsset asset_key '{definition.asset_key}'. The second definition was rejected.");
                return;
            }
            _visualAssetsByKey[definition.asset_key] = definition;
        }

        public void RegisterItemVisualProfile(ItemVisualProfileDefinition definition, DataLoadReport report)
        {
            int before = _itemVisualProfiles.Count;
            Register(_itemVisualProfiles, definition != null ? definition.id : null, definition, "ItemVisualProfile", report);
            if (_itemVisualProfiles.Count == before || definition == null || string.IsNullOrWhiteSpace(definition.item_definition_id))
                return;

            if (_itemVisualProfilesByItem.ContainsKey(definition.item_definition_id))
            {
                report.Error($"Duplicate ItemVisualProfile item_definition_id '{definition.item_definition_id}'. The second definition was rejected.");
                return;
            }
            _itemVisualProfilesByItem[definition.item_definition_id] = definition;
        }

        public void RegisterAttachmentPose(AttachmentPoseDefinition definition, DataLoadReport report)
        {
            Register(_attachmentPoses, definition != null ? definition.id : null, definition, "AttachmentPose", report);
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

            if (!ContentId.TryParse(id, out ContentId contentId, out string idError))
            {
                report.Error($"{typeName}: Global Content ID '{id}' is not canonical and was rejected: {idError}.");
                return;
            }

            if (!string.Equals(id, contentId.Canonical, StringComparison.Ordinal))
            {
                report.Error($"{typeName}: Global Content ID '{id}' must be stored as canonical '{contentId.Canonical}'.");
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
            return LookupContent(_items, id, "Item");
        }

        public ItemStorageProfileDefinition GetItemStorageProfile(string id)
        {
            return LookupContent(_itemStorageProfiles, id, "ItemStorageProfile");
        }

        public EquipmentSlotDefinition GetEquipmentSlot(string id)
        {
            if (!TryResolveEquipmentSlotId(id, out string canonical, out _))
                return null;
            return _equipmentSlots.TryGetValue(canonical, out EquipmentSlotDefinition result) ? result : null;
        }

        public EquipmentLayoutDefinition GetEquipmentLayout(string id)
        {
            return LookupContent(_equipmentLayouts, id, "EquipmentLayout");
        }

        public WeaponProfileDefinition GetWeaponProfile(string id)
        {
            return LookupContent(_weaponProfiles, id, "WeaponProfile");
        }

        public FirearmProfileDefinition GetFirearmProfile(string id)
        {
            return LookupContent(_firearmProfiles, id, "FirearmProfile");
        }

        public AmmoProfileDefinition GetAmmoProfile(string id)
        {
            return LookupContent(_ammoProfiles, id, "AmmoProfile");
        }

        public ActionDefinition GetAction(string id)
        {
            return LookupContent(_actions, id, "Action");
        }

        public LootTableDefinition GetLootTable(string id)
        {
            return LookupContent(_lootTables, id, "LootTable");
        }

        public ActorProfileDefinition GetActorProfile(string id)
        {
            return LookupContent(_actorProfiles, id, "ActorProfile");
        }

        public WorldObjectProfileDefinition GetWorldObjectProfile(string id)
        {
            return LookupContent(_worldObjectProfiles, id, "WorldObjectProfile");
        }

        public VisualRigCapabilityDefinition GetVisualRigCapability(string id)
        {
            return LookupContent(_visualRigCapabilities, id, "VisualRigCapability");
        }

        public VisualRigProfileDefinition GetVisualRigProfile(string id)
        {
            return LookupContent(_visualRigProfiles, id, "VisualRigProfile");
        }

        public VisualAssetDefinition GetVisualAsset(string id)
        {
            return LookupContent(_visualAssets, id, "VisualAsset");
        }

        public VisualAssetDefinition GetVisualAssetByKey(string assetKey)
        {
            return LookupExact(_visualAssetsByKey, assetKey);
        }

        public ItemVisualProfileDefinition GetItemVisualProfile(string id)
        {
            return LookupContent(_itemVisualProfiles, id, "ItemVisualProfile");
        }

        public ItemVisualProfileDefinition GetItemVisualProfileByItemDefinitionId(string itemDefinitionId)
        {
            return LookupContent(_itemVisualProfilesByItem, itemDefinitionId, "ItemVisualProfile.item_definition_id");
        }

        public AttachmentPoseDefinition GetAttachmentPose(string id)
        {
            return LookupContent(_attachmentPoses, id, "AttachmentPose");
        }

        /// <summary>
        /// Temporary compatibility for authored scene fields and schema-v1 saves.
        /// Unqualified lookups resolve only to Core and never create alias keys.
        /// </summary>
        public bool TryResolveGlobalContentId(string raw, out string canonical, out string error)
        {
            canonical = null;
            if (!ContentId.TryResolveLegacyCore(
                    raw,
                    out ContentId contentId,
                    out bool usedLegacyCompatibility,
                    out error))
                return false;

            canonical = contentId.Canonical;
            if (usedLegacyCompatibility && report != null && reportedLegacyLookups.Add(raw))
            {
                report.Warning(
                    $"Legacy unqualified Global Content ID lookup '{raw}' resolved to '{canonical}'. " +
                    "This temporary Core-only compatibility exists for authored scene data and schema-v1 saves; migrate the reference.");
            }
            return true;
        }

        public bool TryResolveEquipmentSlotId(string raw, out string canonical, out string error)
        {
            canonical = null;
            if (!ContentId.TryResolveLegacyCoreEquipmentSlot(
                    raw,
                    out ContentId contentId,
                    out bool usedLegacyCompatibility,
                    out error))
                return false;

            canonical = contentId.Canonical;
            if (usedLegacyCompatibility && report != null &&
                reportedLegacyLookups.Add("legacy-equipment-slot:" + raw))
            {
                report.Warning(
                    $"Legacy EquipmentSlot reference '{raw}' resolved to '{canonical}'. " +
                    "This narrow Core compatibility exists for authored data and schema-v1 saves; migrate the reference.");
            }
            return true;
        }

        private T LookupContent<T>(Dictionary<string, T> dictionary, string id, string typeName) where T : class
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            if (!TryResolveGlobalContentId(id, out string canonical, out string error))
            {
                if (report != null)
                {
                    string warningKey = "invalid:" + typeName + ":" + id;
                    if (reportedLegacyLookups.Add(warningKey))
                        report.Warning($"{typeName} lookup rejected invalid Global Content ID '{id}': {error}.");
                }
                return null;
            }

            return dictionary.TryGetValue(canonical, out T result) ? result : null;
        }

        private static T LookupExact<T>(Dictionary<string, T> dictionary, string id) where T : class
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;
            return dictionary.TryGetValue(id, out T result) ? result : null;
        }

        // Bulk queries used by DataValidator ----------------------------------

        public IEnumerable<ItemDefinition> GetAllItems()
        {
            return _items.Values;
        }

        public IEnumerable<ItemStorageProfileDefinition> GetAllItemStorageProfiles()
        {
            return _itemStorageProfiles.Values;
        }

        public IEnumerable<EquipmentSlotDefinition> GetAllEquipmentSlots()
        {
            return _equipmentSlots.Values;
        }

        public IEnumerable<EquipmentLayoutDefinition> GetAllEquipmentLayouts()
        {
            return _equipmentLayouts.Values;
        }

        public IEnumerable<WeaponProfileDefinition> GetAllWeaponProfiles()
        {
            return _weaponProfiles.Values;
        }

        public IEnumerable<FirearmProfileDefinition> GetAllFirearmProfiles()
        {
            return _firearmProfiles.Values;
        }

        public IEnumerable<AmmoProfileDefinition> GetAllAmmoProfiles()
        {
            return _ammoProfiles.Values;
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

        public IEnumerable<VisualRigCapabilityDefinition> GetAllVisualRigCapabilities()
        {
            return _visualRigCapabilities.Values;
        }

        public IEnumerable<VisualRigProfileDefinition> GetAllVisualRigProfiles()
        {
            return _visualRigProfiles.Values;
        }

        public IEnumerable<VisualAssetDefinition> GetAllVisualAssets()
        {
            return _visualAssets.Values;
        }

        public IEnumerable<ItemVisualProfileDefinition> GetAllItemVisualProfiles()
        {
            return _itemVisualProfiles.Values;
        }

        public IEnumerable<AttachmentPoseDefinition> GetAllAttachmentPoses()
        {
            return _attachmentPoses.Values;
        }

        public void LogStats()
        {
            Debug.Log("[GameDatabase] Loaded definitions:" +
                      $"\n  Items:           {_items.Count}" +
                      $"\n  ItemStorageProfiles: {_itemStorageProfiles.Count}" +
                      $"\n  EquipmentSlots:  {_equipmentSlots.Count}" +
                      $"\n  EquipmentLayouts:{_equipmentLayouts.Count}" +
                      $"\n  WeaponProfiles:  {_weaponProfiles.Count}" +
                      $"\n  FirearmProfiles: {_firearmProfiles.Count}" +
                      $"\n  AmmoProfiles:    {_ammoProfiles.Count}" +
                      $"\n  Actions:         {_actions.Count}" +
                      $"\n  LootTables:      {_lootTables.Count}" +
                      $"\n  ActorProfiles:   {_actorProfiles.Count}" +
                      $"\n  WorldObjectProfiles: {_worldObjectProfiles.Count}" +
                      $"\n  VisualRigCapabilities: {_visualRigCapabilities.Count}" +
                      $"\n  VisualRigProfiles: {_visualRigProfiles.Count}" +
                      $"\n  VisualAssets: {_visualAssets.Count}" +
                      $"\n  ItemVisualProfiles: {_itemVisualProfiles.Count}" +
                      $"\n  AttachmentPoses: {_attachmentPoses.Count}");
        }
    }
}
