using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OldScars.Core.Actors;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Identity;
using OldScars.Core.Interactions;
using OldScars.Core.Items;
using UnityEngine;

namespace OldScars.Core.Persistence
{
    [Serializable] public sealed class Float3State { public float x; public float y; public float z; }
    [Serializable] public sealed class Float4State { public float x; public float y; public float z; public float w; }
    [Serializable] public sealed class PoseState { public Float3State position; public Float4State rotation; }
    [Serializable] public sealed class NeedState { public string needId; public float currentValue; }
    [Serializable] public sealed class WorldClockState { public double elapsedGameSeconds; }
    [Serializable] public sealed class FirearmItemState { public string ammoProfileId; public int loadedRounds; }
    [Serializable]
    public sealed class ItemState
    {
        public string instanceId;
        public string definitionId;
        public int condition;
        public FirearmItemState firearmState;
    }
    [Serializable] public sealed class GridPlacementState { public int x; public int y; public bool rotated; public int width; public int height; }
    [Serializable] public sealed class StorageEntryState { public string instanceId; public int quantity; public GridPlacementState placement; }
    [Serializable]
    public sealed class StorageState
    {
        public string storageId;
        public string kind;
        public string ownerId;
        public bool usesGrid;
        public int width;
        public int height;
        public StorageEntryState[] entries = Array.Empty<StorageEntryState>();
    }
    [Serializable] public sealed class EquippedItemState { public string instanceId; public string[] slots = Array.Empty<string>(); }
    [Serializable]
    public sealed class EquipmentState
    {
        public string ownerPersistentId;
        public string layoutId;
        public string storageId;
        public EquippedItemState[] items = Array.Empty<EquippedItemState>();
    }
    [Serializable]
    public sealed class PlayerState
    {
        public string persistentId;
        public string actorInstanceId;
        public string actorProfileId;
        public PoseState pose;
        public float currentHealth;
        public ActorMedicalStateData medicalState;
        public NeedState[] needs = Array.Empty<NeedState>();
        public string inventoryStorageId;
        public string equipmentStorageId;
    }
    [Serializable]
    public sealed class ContainerState
    {
        public string persistentId;
        public string storageId;
        public bool authoritativeStorage;
        public string[] mutableTags = Array.Empty<string>();
    }
    [Serializable]
    public sealed class CorpseState
    {
        public string persistentId;
        public float currentHealth;
        public string inventoryStorageId;
        public string equipmentStorageId;
    }
    [Serializable]
    public sealed class ActorState
    {
        public string actorInstanceId;
        public string actorProfileId;
        public string authoredSceneObjectId;
        public string originKind;
        public string lifecycleState;
        public PoseState pose;
        public float currentHealth;
        public ActorMedicalStateData medicalState;
        public string inventoryStorageId;
        public string equipmentStorageId;
    }
    [Serializable] public sealed class DoorState { public string persistentId; public string state; }
    [Serializable]
    public sealed class WorldItemState
    {
        public string instanceId;
        public string kind;
        public bool present;
        public int quantity;
        public PoseState pose;
    }
    [Serializable]
    public sealed class CurrentSliceSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public string snapshotType = "current_slice_v1";
        public int schemaVersion = CurrentSchemaVersion;
        public WorldClockState worldClock;
        public PlayerState player;
        public ItemState[] items = Array.Empty<ItemState>();
        public StorageState[] storages = Array.Empty<StorageState>();
        public EquipmentState[] equipment = Array.Empty<EquipmentState>();
        public ContainerState[] containers = Array.Empty<ContainerState>();
        public ActorState[] actors = Array.Empty<ActorState>();
        // Read-only compatibility with pre-M38 schema-v1 saves. New captures use actors exclusively.
        public CorpseState[] corpses = Array.Empty<CorpseState>();
        public DoorState[] doors = Array.Empty<DoorState>();
        public WorldItemState[] worldItems = Array.Empty<WorldItemState>();
    }

    /// <summary>
    /// In-memory migration for the three Global Content ID reference surfaces
    /// persisted by schema v1. Runtime/instance/persistent IDs and tags are not
    /// touched, and the on-disk schema version remains unchanged.
    /// </summary>
    internal static class CurrentSliceContentIdCompatibility
    {
        internal static bool TryNormalizeLegacyCoreReferences(
            CurrentSliceSaveData snapshot,
            GameDatabase database,
            out string error)
        {
            error = null;
            if (snapshot == null || database == null)
                return true;

            ItemState[] itemStates = snapshot.items ?? Array.Empty<ItemState>();
            for (int index = 0; index < itemStates.Length; index++)
            {
                ItemState item = itemStates[index];
                if (item == null)
                    continue;
                string rawDefinitionId = item.definitionId;
                if (!TryNormalize(rawDefinitionId, database, out item.definitionId, out string reason))
                {
                    error = $"items[{index}].definitionId '{rawDefinitionId}' is not a valid Global Content ID: {reason}.";
                    return false;
                }
            }

            if (snapshot.player != null && !string.IsNullOrWhiteSpace(snapshot.player.actorProfileId))
            {
                string rawProfileId = snapshot.player.actorProfileId;
                if (!TryNormalize(rawProfileId, database, out snapshot.player.actorProfileId, out string reason) ||
                    database.GetActorProfile(snapshot.player.actorProfileId) == null)
                {
                    error = $"player.actorProfileId '{rawProfileId}' is not a valid Actor Profile Global Content ID: {reason}.";
                    return false;
                }
            }

            ActorState[] actorStates = snapshot.actors ?? Array.Empty<ActorState>();
            for (int index = 0; index < actorStates.Length; index++)
            {
                ActorState actor = actorStates[index];
                if (actor == null)
                    continue;
                string rawProfileId = actor.actorProfileId;
                if (!TryNormalize(rawProfileId, database, out actor.actorProfileId, out string reason) ||
                    database.GetActorProfile(actor.actorProfileId) == null)
                {
                    error = $"actors[{index}].actorProfileId '{rawProfileId}' is not a valid Actor Profile Global Content ID: {reason}.";
                    return false;
                }
            }

            EquipmentState[] equipmentStates = snapshot.equipment ?? Array.Empty<EquipmentState>();
            for (int equipmentIndex = 0; equipmentIndex < equipmentStates.Length; equipmentIndex++)
            {
                EquipmentState equipment = equipmentStates[equipmentIndex];
                if (equipment == null)
                    continue;
                string rawLayoutId = equipment.layoutId;
                if (!TryNormalize(rawLayoutId, database, out equipment.layoutId, out string layoutReason))
                {
                    error = $"equipment[{equipmentIndex}].layoutId '{rawLayoutId}' is not a valid Global Content ID: {layoutReason}.";
                    return false;
                }

                EquippedItemState[] equippedItems = equipment.items ?? Array.Empty<EquippedItemState>();
                for (int itemIndex = 0; itemIndex < equippedItems.Length; itemIndex++)
                {
                    EquippedItemState equipped = equippedItems[itemIndex];
                    if (equipped?.slots == null)
                        continue;
                    for (int slotIndex = 0; slotIndex < equipped.slots.Length; slotIndex++)
                    {
                        string raw = equipped.slots[slotIndex];
                        if (!database.TryResolveEquipmentSlotId(
                                raw, out equipped.slots[slotIndex], out string slotReason))
                        {
                            error = $"equipment[{equipmentIndex}].items[{itemIndex}].slots[{slotIndex}] " +
                                    $"'{raw}' is not a valid Global Content ID: {slotReason}.";
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private static bool TryNormalize(
            string raw,
            GameDatabase database,
            out string canonical,
            out string error)
        {
            return database.TryResolveGlobalContentId(raw, out canonical, out error);
        }
    }

    public sealed class CurrentSliceResult
    {
        internal CurrentSliceResult(CurrentSliceSaveData snapshot, string failure)
        {
            Snapshot = snapshot;
            Failure = failure;
        }
        public bool Success => Snapshot != null && string.IsNullOrEmpty(Failure);
        public CurrentSliceSaveData Snapshot { get; }
        public string Failure { get; }
    }

    public sealed class CurrentSliceValidationResult
    {
        internal CurrentSliceValidationResult(List<string> errors) { Errors = errors.ToArray(); }
        public bool Success => Errors.Length == 0;
        public string[] Errors { get; }
        public string Failure => Success ? null : string.Join("\n- ", Errors);
    }

    public sealed class CurrentSliceComparisonResult
    {
        internal CurrentSliceComparisonResult(string difference) { Difference = difference; }
        public bool Equivalent => string.IsNullOrEmpty(Difference);
        public string Difference { get; }
    }

    public sealed class CurrentSliceSaveResult
    {
        internal CurrentSliceSaveResult(CurrentSliceSaveData snapshot, string failure)
        {
            Snapshot = snapshot;
            Failure = failure;
        }
        public bool Success => Snapshot != null && string.IsNullOrEmpty(Failure);
        public CurrentSliceSaveData Snapshot { get; }
        public string Failure { get; }
    }

    public static class CurrentSliceSnapshotService
    {
        public const string DebugSlotId = "m37_current_slice_debug";
        private const string InventoryKind = "inventory";
        private const string EquipmentKind = "equipment";
        private const string ContainerKind = "container";
        private const string ItemOwnedKind = "item_owned";
        private const string AuthoredWorldKind = "authored";
        private const string RuntimeWorldKind = "runtime";
        internal const string AuthoredActorOrigin = "Authored";
        internal const string RuntimeActorOrigin = "Runtime";
        internal const string AliveLifecycle = "Alive";
        internal const string DeadLifecycle = "Dead";
        private const float PoseTolerance = 0.0001f;
        private static readonly string[] ContainerTags =
        {
            "opened_container", "sealed_container", "unsearched_container",
            "storage_accessible", "lootable_container", "looted_container"
        };
        private static readonly string[] DoorStates = { "opened_door", "closed_door", "locked_door" };
        private static readonly JsonSerializer PayloadSerializer = JsonSerializer.Create(new JsonSerializerSettings
        {
            Culture = CultureInfo.InvariantCulture,
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Double,
            NullValueHandling = NullValueHandling.Include,
            ReferenceLoopHandling = ReferenceLoopHandling.Error,
            TypeNameHandling = TypeNameHandling.None
        });

        public static CurrentSliceResult Capture()
        {
            if (!Application.isPlaying)
                return Failed("Capture requires Play Mode runtime state.");
            if (GameDataManager.Instance == null || !GameDataManager.Instance.IsReady || GameDataManager.Instance.Database == null)
                return Failed("Capture requires a ready GameDatabase.");

            try
            {
                var context = new CaptureContext(GameDataManager.Instance.Database);
                CurrentSliceSaveData snapshot = context.Capture();
                if (snapshot == null)
                    return Failed(context.Failure);
                CurrentSliceValidationResult validation = Validate(snapshot);
                return validation.Success ? new CurrentSliceResult(snapshot, null) : Failed(validation.Failure);
            }
            catch (Exception exception)
            {
                return Failed($"Capture threw {exception.GetType().Name}: {exception.Message}");
            }
        }

        public static CurrentSliceValidationResult Validate(CurrentSliceSaveData snapshot)
        {
            var validator = new SemanticValidator(snapshot, GameDataManager.Instance != null ? GameDataManager.Instance.Database : null);
            return validator.Run();
        }

        public static JToken ToPayload(CurrentSliceSaveData snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            return JToken.FromObject(snapshot, PayloadSerializer);
        }

        public static CurrentSliceResult FromPayload(JToken payload)
        {
            if (payload == null || payload.Type == JTokenType.Null)
                return Failed("Current Slice payload is missing.");
            try
            {
                JObject payloadObject = payload as JObject;
                bool legacyWorldClockMissing = payloadObject != null &&
                    !HasProperty(payloadObject, "worldClock");
                bool legacyPlayerMedicalMissing = payloadObject?["player"] is JObject playerObject &&
                    !HasProperty(playerObject, "medicalState");
                var legacyActorMedicalMissing = new HashSet<int>();
                var legacyFirearmStateMissing = new HashSet<int>();
                if (payloadObject?["actors"] is JArray actorArray)
                {
                    for (int index = 0; index < actorArray.Count; index++)
                    {
                        if (actorArray[index] is JObject actorObject &&
                            !HasProperty(actorObject, "medicalState"))
                        {
                            legacyActorMedicalMissing.Add(index);
                        }
                    }
                }
                if (payloadObject?["items"] is JArray itemArray)
                {
                    for (int index = 0; index < itemArray.Count; index++)
                    {
                        if (itemArray[index] is JObject itemObject && !HasProperty(itemObject, "firearmState"))
                            legacyFirearmStateMissing.Add(index);
                    }
                }
                CurrentSliceSaveData snapshot = payload.ToObject<CurrentSliceSaveData>(PayloadSerializer);
                if (legacyWorldClockMissing && snapshot != null)
                {
                    snapshot.worldClock = new WorldClockState
                    {
                        elapsedGameSeconds = WorldClock.DefaultElapsedGameSeconds
                    };
                }
                if (legacyPlayerMedicalMissing && snapshot?.player != null)
                    snapshot.player.medicalState = ActorMedicalStateComponent.HealthyBaseline();
                ActorState[] actors = snapshot?.actors ?? Array.Empty<ActorState>();
                foreach (int index in legacyActorMedicalMissing)
                {
                    if (index >= 0 && index < actors.Length && actors[index] != null)
                        actors[index].medicalState = ActorMedicalStateComponent.HealthyBaseline();
                }
                GameDatabase database = GameDataManager.Instance != null ? GameDataManager.Instance.Database : null;
                if (!CurrentSliceContentIdCompatibility.TryNormalizeLegacyCoreReferences(snapshot, database, out string migrationError))
                    return Failed("Current Slice legacy Content ID migration failed: " + migrationError);
                ItemState[] itemStates = snapshot?.items ?? Array.Empty<ItemState>();
                foreach (int index in legacyFirearmStateMissing)
                {
                    if (index < 0 || index >= itemStates.Length || itemStates[index] == null)
                        continue;
                    ItemDefinition definition = database?.GetItem(itemStates[index].definitionId);
                    if (definition != null && !string.IsNullOrWhiteSpace(definition.firearm_profile_id))
                        itemStates[index].firearmState = new FirearmItemState { ammoProfileId = null, loadedRounds = 0 };
                }
                CurrentSliceValidationResult validation = Validate(snapshot);
                return validation.Success ? new CurrentSliceResult(snapshot, null) : Failed(validation.Failure);
            }
            catch (Exception exception) when (exception is JsonException || exception is InvalidOperationException)
            {
                return Failed($"Current Slice payload deserialization failed: {exception.Message}");
            }
        }

        public static CurrentSliceSaveResult Save(string slotId, PersistenceFileStore store = null)
        {
            CurrentSliceResult capture = Capture();
            if (!capture.Success)
                return new CurrentSliceSaveResult(null, capture.Failure);
            PersistenceWriteResult write = (store ?? new PersistenceFileStore()).Write(slotId, ToPayload(capture.Snapshot));
            return write.Success
                ? new CurrentSliceSaveResult(capture.Snapshot, null)
                : new CurrentSliceSaveResult(null, $"{write.FailureCode}: {write.Failure}");
        }

        public static CurrentSliceResult Read(string slotId, PersistenceFileStore store = null)
        {
            PersistenceLoadResult read = (store ?? new PersistenceFileStore()).Read(slotId);
            return read.Success ? FromPayload(read.Payload) : Failed($"{read.FailureCode}: {read.Failure}");
        }

        public static CurrentSliceComparisonResult Compare(CurrentSliceSaveData left, CurrentSliceSaveData right)
        {
            if (left == null || right == null)
                return new CurrentSliceComparisonResult("One or both snapshots are null.");
            JToken first = Canonicalize(ToPayload(left));
            JToken second = Canonicalize(ToPayload(right));
            return new CurrentSliceComparisonResult(FindDifference(first, second, "$", false));
        }

        public static string BuildSuccessSummary(string slot, CurrentSliceSaveData snapshot)
        {
            return "[Persistence][CURRENT_SLICE_SAVE]" +
                   $"\nSlot: {slot}\nItems: {snapshot.items.Length}\nStorages: {snapshot.storages.Length}" +
                   $"\nWorldItems: {snapshot.worldItems.Length}\nContainers: {snapshot.containers.Length}" +
                   $"\nActors: {Items(snapshot.actors).Length}\nCorpsesLegacy: {Items(snapshot.corpses).Length}" +
                   $"\nDoors: {snapshot.doors.Length}" +
                   $"\nElapsedGameSeconds: {snapshot.worldClock?.elapsedGameSeconds.ToString("R", CultureInfo.InvariantCulture) ?? "<NONE>"}" +
                   "\nResult: Success";
        }

        private static CurrentSliceResult Failed(string failure) => new CurrentSliceResult(null, failure ?? "Unknown failure.");
        private static bool HasProperty(JObject value, string propertyName) => value != null &&
            value.Properties().Any(property => string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase));
        private static T[] Items<T>(T[] source) => source ?? Array.Empty<T>();
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static string StorageId(string kind, string ownerId) => kind + ":" + ownerId;

        private static T[] FindScene<T>() where T : Component
        {
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include)
                .Where(component => component != null && component.gameObject.scene.IsValid() && component.gameObject.scene.isLoaded)
                .ToArray();
        }

        private static PersistentSceneObjectId Identity(Component component)
        {
            PersistentSceneObjectId identity = component != null ? component.GetComponent<PersistentSceneObjectId>() : null;
            return identity != null && identity.enabled ? identity : null;
        }
        private static PoseState Pose(Transform transform) => new PoseState
        {
            position = new Float3State { x = transform.position.x, y = transform.position.y, z = transform.position.z },
            rotation = new Float4State { x = transform.rotation.x, y = transform.rotation.y, z = transform.rotation.z, w = transform.rotation.w }
        };

        private static JToken Canonicalize(JToken token)
        {
            if (token is JObject obj)
            {
                var result = new JObject();
                foreach (JProperty property in obj.Properties().OrderBy(property => property.Name, StringComparer.Ordinal))
                    result[property.Name] = Canonicalize(property.Value);
                return result;
            }
            if (token is JArray array)
            {
                JToken[] values = array.Select(Canonicalize).ToArray();
                return new JArray(values.OrderBy(value => value.ToString(Formatting.None), StringComparer.Ordinal));
            }
            return token.DeepClone();
        }

        private static string FindDifference(JToken left, JToken right, string path, bool pose)
        {
            if (left == null || right == null)
                return $"{path}: {left?.ToString(Formatting.None) ?? "<null>"} != {right?.ToString(Formatting.None) ?? "<null>"}";
            if (pose && (left.Type == JTokenType.Float || left.Type == JTokenType.Integer) &&
                (right.Type == JTokenType.Float || right.Type == JTokenType.Integer) &&
                Math.Abs(left.Value<double>() - right.Value<double>()) <= PoseTolerance)
                return null;
            if (left.Type != right.Type)
                return $"{path}: {left.ToString(Formatting.None)} != {right.ToString(Formatting.None)}";
            if (left is JObject firstObject && right is JObject secondObject)
            {
                string[] names = firstObject.Properties().Select(p => p.Name).Union(secondObject.Properties().Select(p => p.Name)).OrderBy(n => n).ToArray();
                foreach (string name in names)
                {
                    JToken firstValue = firstObject[name];
                    JToken secondValue = secondObject[name];
                    string difference = FindDifference(firstValue, secondValue, path + "." + name,
                        pose || name == "position" || name == "rotation");
                    if (difference != null) return difference;
                }
            }
            else if (left is JArray firstArray && right is JArray secondArray)
            {
                if (firstArray.Count != secondArray.Count)
                    return $"{path}: array count {firstArray.Count} != {secondArray.Count}";
                for (int index = 0; index < firstArray.Count; index++)
                {
                    string difference = FindDifference(firstArray[index], secondArray[index], $"{path}[{index}]", pose);
                    if (difference != null) return difference;
                }
            }
            else if (!JToken.DeepEquals(left, right))
                return $"{path}: {left.ToString(Formatting.None)} != {right.ToString(Formatting.None)}";
            return null;
        }

        private sealed class CaptureContext
        {
            private readonly GameDatabase database;
            private readonly Dictionary<string, ItemState> items = new Dictionary<string, ItemState>(StringComparer.Ordinal);
            private readonly Dictionary<string, string> locations = new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly List<StorageState> storages = new List<StorageState>();
            private readonly List<EquipmentState> equipment = new List<EquipmentState>();
            internal string Failure { get; private set; }

            internal CaptureContext(GameDatabase database) { this.database = database; }

            internal CurrentSliceSaveData Capture()
            {
                Dictionary<string, PersistentSceneObjectId> identities = IndexIdentities();
                if (identities == null)
                    return null;
                WorldClock worldClock = WorldClock.Current;
                if (worldClock == null || !WorldClock.IsValidElapsedGameSeconds(worldClock.ElapsedGameSeconds))
                    return Fail("World Clock authority is missing or outside its supported range.");
                ActorInteractionContext[] players = FindScene<ActorInteractionContext>()
                    .Where(actor => Identity(actor) != null && Items(actor.ActorTags).Contains("player"))
                    .ToArray();
                if (players.Length != 1)
                    return Fail($"Expected exactly one persistent player ActorInteractionContext; found {players.Length}.");
                ActorInteractionContext player = players[0];
                string playerId = Identity(player).PersistentId;
                ActorProfileDefinition playerProfile = ResolveTaggedPlayerProfile(player);
                if (playerProfile == null)
                    return Fail($"Player '{playerId}' does not resolve exactly one actor profile bootstrap tag.");
                if (!ActorRuntimeIdentity.TryEnsureAuthored(
                        player.gameObject, playerProfile.id, out ActorRuntimeIdentity playerIdentity, out string playerIdentityError))
                    return Fail($"Player '{playerId}' actor identity failed: {playerIdentityError}");
                InventoryComponent playerInventory = player.GetInventoryComponent();
                ActorHealthComponent playerHealth = player.GetComponent<ActorHealthComponent>();
                ActorMedicalStateComponent playerMedical = player.GetComponent<ActorMedicalStateComponent>();
                ActorNeedsComponent playerNeeds = player.GetComponent<ActorNeedsComponent>();
                if (playerInventory == null || playerHealth == null || playerMedical == null || playerNeeds == null)
                    return Fail($"Player '{playerId}' is missing Inventory, Health, Medical or Needs runtime state.");

                var snapshot = new CurrentSliceSaveData
                {
                    worldClock = new WorldClockState
                    {
                        elapsedGameSeconds = worldClock.ElapsedGameSeconds
                    }
                };
                string playerInventoryId = CaptureStorage(InventoryKind, playerId, playerInventory);
                if (Failure != null) return null;
                string playerEquipmentId = CaptureEquipment(playerId, player.GetComponent<ActorEquipmentComponent>());
                if (Failure != null) return null;
                snapshot.player = new PlayerState
                {
                    persistentId = playerId,
                    actorInstanceId = playerIdentity.ActorInstanceId,
                    actorProfileId = playerProfile.id,
                    pose = CurrentSliceSnapshotService.Pose(player.transform),
                    currentHealth = playerHealth.CurrentHealth,
                    medicalState = playerMedical.CaptureState(),
                    needs = playerNeeds.RuntimeStates.Where(state => state != null)
                        .Select(state => new NeedState { needId = state.needId, currentValue = state.currentValue })
                        .OrderBy(state => state.needId, StringComparer.Ordinal).ToArray(),
                    inventoryStorageId = playerInventoryId,
                    equipmentStorageId = playerEquipmentId
                };

                foreach (ActorProfileComponent profileComponent in FindScene<ActorProfileComponent>())
                {
                    if (!profileComponent.gameObject.activeInHierarchy)
                        continue;
                    PersistentSceneObjectId authored = Identity(profileComponent);
                    if (authored != null && !ActorRuntimeIdentity.TryEnsureAuthored(
                            profileComponent.gameObject, profileComponent.ActorProfileId,
                            out _, out string identityError))
                        return Fail($"Authored actor '{authored.PersistentId}' identity failed: {identityError}");
                }

                snapshot.actors = FindScene<ActorRuntimeIdentity>()
                    .Where(identity => identity.IsRegistered && identity.gameObject.activeInHierarchy &&
                                       identity.ActorInstanceId != playerIdentity.ActorInstanceId)
                    .Select(CaptureActor).Where(state => state != null)
                    .OrderBy(state => state.actorInstanceId, StringComparer.Ordinal).ToArray();
                if (Failure != null) return null;

                snapshot.containers = FindScene<ContainerLootComponent>().Select(container => CaptureContainer(container)).Where(state => state != null)
                    .OrderBy(state => state.persistentId, StringComparer.Ordinal).ToArray();
                if (Failure != null) return null;
                snapshot.corpses = Array.Empty<CorpseState>();
                snapshot.doors = identities.Values.Select(CaptureDoor).Where(state => state != null)
                    .OrderBy(state => state.persistentId, StringComparer.Ordinal).ToArray();
                if (Failure != null) return null;
                snapshot.worldItems = FindScene<WorldItemPickup>().Select(CaptureWorldItem).Where(state => state != null)
                    .OrderBy(state => state.kind, StringComparer.Ordinal).ThenBy(state => state.instanceId, StringComparer.Ordinal).ToArray();
                if (Failure != null) return null;

                snapshot.items = items.Values.OrderBy(item => item.instanceId, StringComparer.Ordinal).ToArray();
                snapshot.storages = storages.OrderBy(storage => storage.storageId, StringComparer.Ordinal).ToArray();
                snapshot.equipment = equipment.OrderBy(state => state.ownerPersistentId, StringComparer.Ordinal).ToArray();
                return snapshot;
            }

            private ActorProfileDefinition ResolveTaggedPlayerProfile(ActorInteractionContext player)
            {
                string[] tags = Items(player?.ActorTags);
                ActorProfileDefinition[] matches = database.GetAllActorProfiles()
                    .Where(profile => profile != null && !string.IsNullOrWhiteSpace(profile.inventory_seed_actor_tag) &&
                                      tags.Contains(profile.inventory_seed_actor_tag))
                    .ToArray();
                return matches.Length == 1 ? matches[0] : null;
            }

            private ActorState CaptureActor(ActorRuntimeIdentity identity)
            {
                if (identity == null || !identity.IsRegistered)
                    return null;
                ActorProfileComponent profile = identity.GetComponent<ActorProfileComponent>();
                ActorHealthComponent health = identity.GetComponent<ActorHealthComponent>();
                ActorMedicalStateComponent medical = identity.GetComponent<ActorMedicalStateComponent>();
                InventoryComponent inventory = identity.GetComponent<InventoryComponent>();
                if (profile == null || health == null || medical == null || inventory == null)
                {
                    Failure = $"Actor '{identity.ActorInstanceId}' lacks Profile, Health, Medical or Inventory runtime state.";
                    return null;
                }
                if (identity.LifecycleState == ActorLifecycleState.Dead != health.IsDead)
                {
                    Failure = $"Actor '{identity.ActorInstanceId}' lifecycle contradicts health {health.CurrentHealth}.";
                    return null;
                }

                PersistentSceneObjectId authored = Identity(identity);
                string ownerId = identity.ActorInstanceId;
                return new ActorState
                {
                    actorInstanceId = ownerId,
                    actorProfileId = identity.ActorProfileId,
                    authoredSceneObjectId = identity.OriginKind == ActorOriginKind.Authored ? authored?.PersistentId : null,
                    originKind = identity.OriginKind == ActorOriginKind.Authored ? AuthoredActorOrigin : RuntimeActorOrigin,
                    lifecycleState = identity.LifecycleState == ActorLifecycleState.Dead ? DeadLifecycle : AliveLifecycle,
                    pose = CurrentSliceSnapshotService.Pose(identity.transform),
                    currentHealth = health.CurrentHealth,
                    medicalState = medical.CaptureState(),
                    inventoryStorageId = CaptureStorage(InventoryKind, ownerId, inventory),
                    equipmentStorageId = CaptureEquipment(ownerId, identity.GetComponent<ActorEquipmentComponent>())
                };
            }

            private Dictionary<string, PersistentSceneObjectId> IndexIdentities()
            {
                var result = new Dictionary<string, PersistentSceneObjectId>(StringComparer.Ordinal);
                foreach (PersistentSceneObjectId identity in FindScene<PersistentSceneObjectId>())
                {
                    if (!identity.enabled)
                        continue;
                    if (!PersistentSceneObjectId.IsValidFormat(identity.PersistentId))
                    {
                        Failure = $"Invalid PersistentSceneObjectId on '{identity.name}'.";
                        return null;
                    }
                    if (!result.TryAdd(identity.PersistentId, identity))
                    {
                        Failure = $"Duplicate PersistentSceneObjectId '{identity.PersistentId}'.";
                        return null;
                    }
                }
                return result;
            }

            private ContainerState CaptureContainer(ContainerLootComponent container)
            {
                PersistentSceneObjectId identity = Identity(container);
                if (identity == null || !container.HasInitializedStorage)
                {
                    Failure = $"Container '{container.name}' lacks persistent identity or initialized authoritative runtime storage.";
                    return null;
                }
                string storageId = CaptureStorage(ContainerKind, identity.PersistentId, container);
                WorldObjectTags tags = container.GetComponent<WorldObjectTags>();
                return new ContainerState
                {
                    persistentId = identity.PersistentId,
                    storageId = storageId,
                    authoritativeStorage = true,
                    mutableTags = tags == null ? Array.Empty<string>() : tags.RuntimeTags.Intersect(ContainerTags).OrderBy(tag => tag).ToArray()
                };
            }

            private CorpseState CaptureCorpse(PersistentSceneObjectId identity, string playerId)
            {
                if (identity.PersistentId == playerId)
                    return null;
                ActorHealthComponent health = identity.GetComponent<ActorHealthComponent>();
                InventoryComponent inventory = identity.GetComponent<InventoryComponent>();
                if (health == null || inventory == null || !health.IsDead)
                    return null;
                return new CorpseState
                {
                    persistentId = identity.PersistentId,
                    currentHealth = health.CurrentHealth,
                    inventoryStorageId = CaptureStorage(InventoryKind, identity.PersistentId, inventory),
                    equipmentStorageId = CaptureEquipment(identity.PersistentId, identity.GetComponent<ActorEquipmentComponent>())
                };
            }

            private DoorState CaptureDoor(PersistentSceneObjectId identity)
            {
                WorldObjectTags tags = identity.GetComponent<WorldObjectTags>();
                if (tags == null)
                    return null;
                string[] states = DoorStates.Where(tags.HasTag).ToArray();
                if (states.Length == 0)
                    return null;
                if (states.Length != 1)
                {
                    Failure = $"Door '{identity.PersistentId}' has {states.Length} canonical door state tags.";
                    return null;
                }
                return new DoorState { persistentId = identity.PersistentId, state = states[0] };
            }

            private WorldItemState CaptureWorldItem(WorldItemPickup pickup)
            {
                bool authored = !string.IsNullOrWhiteSpace(pickup.AuthoredItemInstanceId);
                ItemStorageEntry entry = pickup.GridStorageEntries.Count > 0 ? pickup.GridStorageEntries[0] : null;
                if (!authored && entry?.Item == null)
                    return null;
                string instanceId = authored ? pickup.AuthoredItemInstanceId : entry.Item.InstanceId;
                bool present = entry?.Item != null || authored && !pickup.HasInitializedSource;
                int quantity = present ? entry?.Quantity ?? 1 : 0;
                if (entry?.Item != null)
                    AddItem(entry.Item);
                else if (present)
                {
                    ItemDefinition definition = database.GetItem(pickup.ItemDefinitionId);
                    if (definition == null || definition.physical == null)
                    {
                        Failure = $"Authored world item '{instanceId}' references missing definition '{pickup.ItemDefinitionId}'.";
                        return null;
                    }
                    AddItem(new ItemState
                    {
                        instanceId = instanceId,
                        definitionId = definition.id,
                        condition = definition.physical.condition_max,
                        firearmState = !string.IsNullOrWhiteSpace(definition.firearm_profile_id)
                            ? new FirearmItemState { ammoProfileId = null, loadedRounds = 0 }
                            : null
                    });
                }
                if (Failure != null) return null;
                if (present && !AddLocation(instanceId, "world:" + (authored ? AuthoredWorldKind : RuntimeWorldKind)))
                    return null;
                return new WorldItemState
                {
                    instanceId = instanceId,
                    kind = authored ? AuthoredWorldKind : RuntimeWorldKind,
                    present = present,
                    quantity = quantity,
                    pose = present ? CurrentSliceSnapshotService.Pose(pickup.transform) : null
                };
            }

            private string CaptureStorage(string kind, string ownerId, IGridStorageOwner source)
            {
                string id = StorageId(kind, ownerId);
                if (storages.Any(storage => storage.storageId == id))
                    return id;
                var entries = new List<StorageEntryState>();
                var state = new StorageState
                {
                    storageId = id,
                    kind = kind,
                    ownerId = ownerId,
                    usesGrid = source.UsesGridLayout,
                    width = source.UsesGridLayout ? source.GridWidth : 0,
                    height = source.UsesGridLayout ? source.GridHeight : 0
                };
                storages.Add(state);
                foreach (ItemStorageEntry entry in source.GridStorageEntries)
                {
                    if (entry?.Item == null || !AddItem(entry.Item) || !AddLocation(entry.Item.InstanceId, id))
                        return id;
                    GridPlacementState placement = null;
                    if (state.usesGrid)
                    {
                        if (!source.TryGetGridPlacement(entry.Item.InstanceId, out GridPlacement runtimePlacement))
                        {
                            Failure = $"Storage '{id}' has no placement for '{entry.Item.InstanceId}'.";
                            return id;
                        }
                        placement = new GridPlacementState
                        {
                            x = runtimePlacement.X, y = runtimePlacement.Y, rotated = runtimePlacement.IsRotated,
                            width = runtimePlacement.EffectiveWidth, height = runtimePlacement.EffectiveHeight
                        };
                    }
                    entries.Add(new StorageEntryState { instanceId = entry.Item.InstanceId, quantity = entry.Quantity, placement = placement });
                    if (entry.Item.HasOwnedStorage)
                    {
                        if (kind == ItemOwnedKind)
                        {
                            Failure = $"Illegal nested item-owned storage at '{entry.Item.InstanceId}' in '{id}'.";
                            return id;
                        }
                        CaptureStorage(ItemOwnedKind, entry.Item.InstanceId, entry.Item.OwnedStorage);
                    }
                    if (Failure != null) return id;
                }
                state.entries = entries.OrderBy(entry => entry.instanceId, StringComparer.Ordinal).ToArray();
                return id;
            }

            private string CaptureEquipment(string ownerId, ActorEquipmentComponent component)
            {
                if (component == null)
                    return null;
                string id = StorageId(EquipmentKind, ownerId);
                var storage = new StorageState { storageId = id, kind = EquipmentKind, ownerId = ownerId };
                var storageEntries = new List<StorageEntryState>();
                var equippedItems = new List<EquippedItemState>();
                storages.Add(storage);
                foreach (ItemStorageEntry entry in component.Entries)
                {
                    if (entry?.Item == null || !AddItem(entry.Item) || !AddLocation(entry.Item.InstanceId, id))
                        return id;
                    storageEntries.Add(new StorageEntryState { instanceId = entry.Item.InstanceId, quantity = entry.Quantity });
                    equippedItems.Add(new EquippedItemState
                    {
                        instanceId = entry.Item.InstanceId,
                        slots = component.GetSlotsOccupiedBy(entry.Item.InstanceId).OrderBy(slot => slot, StringComparer.Ordinal).ToArray()
                    });
                    if (entry.Item.HasOwnedStorage)
                        CaptureStorage(ItemOwnedKind, entry.Item.InstanceId, entry.Item.OwnedStorage);
                    if (Failure != null) return id;
                }
                storage.entries = storageEntries.OrderBy(entry => entry.instanceId, StringComparer.Ordinal).ToArray();
                equipment.Add(new EquipmentState
                {
                    ownerPersistentId = ownerId,
                    layoutId = component.EquipmentLayoutId,
                    storageId = id,
                    items = equippedItems.OrderBy(item => item.instanceId, StringComparer.Ordinal).ToArray()
                });
                return id;
            }

            private bool AddItem(ItemInstance item) => AddItem(new ItemState
            {
                instanceId = item.InstanceId,
                definitionId = item.DefinitionId,
                condition = item.Condition,
                firearmState = item.HasFirearmState
                    ? new FirearmItemState
                    {
                        ammoProfileId = item.LoadedAmmoProfileId,
                        loadedRounds = item.LoadedRounds
                    }
                    : null
            });

            private bool AddItem(ItemState item)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.instanceId))
                {
                    Failure = "Capture encountered an item without InstanceId.";
                    return false;
                }
                if (items.TryGetValue(item.instanceId, out ItemState previous))
                {
                    if (previous.definitionId == item.definitionId && previous.condition == item.condition &&
                        EquivalentFirearmState(previous.firearmState, item.firearmState))
                        return true;
                    Failure = $"InstanceId '{item.instanceId}' resolves to conflicting item state.";
                    return false;
                }
                items.Add(item.instanceId, item);
                return true;
            }

            private static bool EquivalentFirearmState(FirearmItemState left, FirearmItemState right)
            {
                if (left == null || right == null)
                    return left == null && right == null;
                return left.ammoProfileId == right.ammoProfileId && left.loadedRounds == right.loadedRounds;
            }

            private bool AddLocation(string instanceId, string location)
            {
                if (locations.TryGetValue(instanceId, out string previous))
                {
                    Failure = $"InstanceId '{instanceId}' appears in incompatible locations '{previous}' and '{location}'.";
                    return false;
                }
                locations.Add(instanceId, location);
                return true;
            }

            private CurrentSliceSaveData Fail(string failure) { Failure = failure; return null; }
        }

        private sealed class SemanticValidator
        {
            private readonly CurrentSliceSaveData snapshot;
            private readonly GameDatabase database;
            private readonly List<string> errors = new List<string>();
            private readonly Dictionary<string, ItemState> items = new Dictionary<string, ItemState>(StringComparer.Ordinal);
            private readonly Dictionary<string, StorageState> storages = new Dictionary<string, StorageState>(StringComparer.Ordinal);
            private readonly Dictionary<string, string> locations = new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly Dictionary<string, ActorState> actorStates = new Dictionary<string, ActorState>(StringComparer.Ordinal);
            private Dictionary<string, PersistentSceneObjectId> sceneIds;

            internal SemanticValidator(CurrentSliceSaveData snapshot, GameDatabase database)
            {
                this.snapshot = snapshot;
                this.database = database;
            }

            internal CurrentSliceValidationResult Run()
            {
                if (snapshot == null) return Error("Snapshot is null.");
                if (snapshot.snapshotType != "current_slice_v1" || snapshot.schemaVersion != CurrentSliceSaveData.CurrentSchemaVersion)
                    errors.Add($"Unsupported Current Slice contract '{snapshot.snapshotType}' schema {snapshot.schemaVersion}.");
                if (database == null) errors.Add("Semantic preflight requires a ready GameDatabase.");
                ValidateWorldClock();
                IndexScene();
                ValidateItems();
                ValidateTopLevelEntities();
                ValidateStorages();
                ValidateEquipment();
                ValidateWorldItems();
                foreach (string instanceId in items.Keys)
                    if (!locations.ContainsKey(instanceId)) errors.Add($"Item '{instanceId}' has no authoritative location.");
                return new CurrentSliceValidationResult(errors);
            }

            private void ValidateWorldClock()
            {
                if (snapshot.worldClock == null)
                {
                    errors.Add("World Clock state is missing. Legacy saves must omit the field rather than store null.");
                    return;
                }

                double elapsed = snapshot.worldClock.elapsedGameSeconds;
                if (!Finite(elapsed) || !WorldClock.IsValidElapsedGameSeconds(elapsed))
                    errors.Add($"World Clock elapsedGameSeconds '{elapsed:R}' is outside the supported range.");
            }

            private void IndexScene()
            {
                sceneIds = new Dictionary<string, PersistentSceneObjectId>(StringComparer.Ordinal);
                foreach (PersistentSceneObjectId identity in FindScene<PersistentSceneObjectId>())
                {
                    if (!identity.enabled)
                        continue;
                    if (!PersistentSceneObjectId.IsValidFormat(identity.PersistentId))
                        errors.Add($"Scene object '{identity.name}' has invalid persistent identity.");
                    else if (!sceneIds.TryAdd(identity.PersistentId, identity))
                        errors.Add($"Scene contains duplicate PersistentSceneObjectId '{identity.PersistentId}'.");
                }
            }

            private void ValidateItems()
            {
                foreach (ItemState item in Items(snapshot.items))
                {
                    if (item == null) { errors.Add("Items contains null state."); continue; }
                    if (!ItemInstanceIdRegistry.IsValidFormat(item.instanceId))
                        errors.Add($"Item InstanceId '{item.instanceId}' has invalid format.");
                    else if (!items.TryAdd(item.instanceId, item))
                        errors.Add($"Duplicate ItemState '{item.instanceId}'.");
                    ItemDefinition definition = database?.GetItem(item.definitionId);
                    if (definition == null)
                        errors.Add($"Item '{item.instanceId}' references missing DefinitionId '{item.definitionId}'.");
                    else if (definition.physical == null || item.condition < 1 || item.condition > definition.physical.condition_max)
                        errors.Add($"Item '{item.instanceId}' has invalid Condition {item.condition}.");
                    if (definition != null)
                        ValidateFirearmItemState(item, definition);
                }
            }

            private void ValidateFirearmItemState(ItemState item, ItemDefinition definition)
            {
                bool isFirearm = !string.IsNullOrWhiteSpace(definition.firearm_profile_id);
                if (!isFirearm)
                {
                    if (item.firearmState != null)
                        errors.Add($"Non-firearm item '{item.instanceId}' carries firearm state.");
                    return;
                }
                if (item.firearmState == null)
                {
                    errors.Add($"Firearm item '{item.instanceId}' is missing required firearm state.");
                    return;
                }

                FirearmProfileDefinition firearm = database?.GetFirearmProfile(definition.firearm_profile_id);
                if (firearm == null)
                {
                    errors.Add($"Firearm item '{item.instanceId}' references unavailable profile '{definition.firearm_profile_id}'.");
                    return;
                }
                FirearmItemState state = item.firearmState;
                if (state.loadedRounds < 0 || state.loadedRounds > firearm.magazine_capacity)
                    errors.Add($"Firearm item '{item.instanceId}' has loadedRounds {state.loadedRounds} outside 0..{firearm.magazine_capacity}.");
                if (state.loadedRounds == 0)
                {
                    if (state.ammoProfileId != null)
                        errors.Add($"Unloaded firearm item '{item.instanceId}' must use null ammoProfileId.");
                    return;
                }
                if (!ContentId.TryParse(state.ammoProfileId, out ContentId ammoId, out string idError) ||
                    ammoId.ToString() != state.ammoProfileId)
                {
                    errors.Add($"Firearm item '{item.instanceId}' has non-canonical ammoProfileId '{state.ammoProfileId ?? "<NONE>"}': {idError}.");
                    return;
                }
                if (database.GetAmmoProfile(state.ammoProfileId) == null)
                    errors.Add($"Firearm item '{item.instanceId}' references missing ammo profile '{state.ammoProfileId}'.");
                if (firearm.accepted_ammo_profile_ids == null ||
                    !firearm.accepted_ammo_profile_ids.Contains(state.ammoProfileId, StringComparer.Ordinal))
                    errors.Add($"Firearm item '{item.instanceId}' contains ammo '{state.ammoProfileId}' incompatible with '{firearm.id}'.");
            }

            private void ValidateTopLevelEntities()
            {
                var entityKinds = new Dictionary<string, string>(StringComparer.Ordinal);
                if (snapshot.player == null)
                    errors.Add("Snapshot requires exactly one player state.");
                else
                {
                    AddEntity(entityKinds, snapshot.player.persistentId, "player", typeof(ActorInteractionContext));
                    if (!Items(SceneComponent<ActorInteractionContext>(snapshot.player.persistentId)?.ActorTags).Contains("player"))
                        errors.Add($"Player '{snapshot.player.persistentId}' does not resolve the current player role.");
                    if (snapshot.player.pose == null || !ValidPose(snapshot.player.pose)) errors.Add("Player pose is missing or non-finite.");
                    ActorHealthComponent health = SceneComponent<ActorHealthComponent>(snapshot.player.persistentId);
                    if (!Finite(snapshot.player.currentHealth) || health == null || snapshot.player.currentHealth < 0f || snapshot.player.currentHealth > health.MaxHealth)
                        errors.Add($"Player '{snapshot.player.persistentId}' has invalid health {snapshot.player.currentHealth}.");
                    ValidateMedical(snapshot.player.medicalState, $"Player '{snapshot.player.persistentId}'");
                    ActorNeedsComponent needs = SceneComponent<ActorNeedsComponent>(snapshot.player.persistentId);
                    var needIds = new HashSet<string>(StringComparer.Ordinal);
                    foreach (NeedState need in Items(snapshot.player.needs))
                    {
                        if (need == null || !needIds.Add(need.needId)) errors.Add("Player needs contain null or duplicate need id.");
                        else if (needs == null || !needs.HasNeed(need.needId) || !Finite(need.currentValue) || need.currentValue < 0f || need.currentValue > needs.GetNeedMaxValue(need.needId))
                            errors.Add($"Player need '{need.needId}' has invalid value {need.currentValue}.");
                    }
                    string[] runtimeNeedIds = needs == null ? Array.Empty<string>() : Items(needs.RuntimeStates?.ToArray())
                        .Where(state => state != null).Select(state => state.needId).ToArray();
                    if (!needIds.SetEquals(runtimeNeedIds)) errors.Add($"Player '{snapshot.player.persistentId}' needs do not match current runtime state.");

                    bool hasPlayerActorId = !string.IsNullOrWhiteSpace(snapshot.player.actorInstanceId);
                    bool hasPlayerProfileId = !string.IsNullOrWhiteSpace(snapshot.player.actorProfileId);
                    if (hasPlayerActorId != hasPlayerProfileId)
                        errors.Add("Player actor identity and profile must both be present or both be absent for legacy schema-v1 compatibility.");
                    if (hasPlayerActorId)
                    {
                        if (!ActorRuntimeIdentity.IsValidFormat(snapshot.player.actorInstanceId))
                            errors.Add($"Player ActorInstanceId '{snapshot.player.actorInstanceId}' is invalid.");
                        if (database?.GetActorProfile(snapshot.player.actorProfileId) == null)
                            errors.Add($"Player ActorProfileId '{snapshot.player.actorProfileId}' does not resolve.");
                        if (sceneIds.TryGetValue(snapshot.player.persistentId ?? string.Empty, out PersistentSceneObjectId playerRoot))
                        {
                            ActorRuntimeIdentity runtimeIdentity = playerRoot.GetComponent<ActorRuntimeIdentity>();
                            string expectedId = !string.IsNullOrWhiteSpace(runtimeIdentity?.AuthoredActorInstanceId)
                                ? runtimeIdentity.AuthoredActorInstanceId
                                : ActorRuntimeIdentity.DeriveAuthoredActorInstanceId(snapshot.player.persistentId);
                            if (snapshot.player.actorInstanceId != expectedId)
                                errors.Add($"Player '{snapshot.player.persistentId}' expects ActorInstanceId '{expectedId}'.");
                            ActorInteractionContext context = playerRoot.GetComponent<ActorInteractionContext>();
                            ActorProfileDefinition[] profiles = database?.GetAllActorProfiles()
                                .Where(profile => profile != null && !string.IsNullOrWhiteSpace(profile.inventory_seed_actor_tag) &&
                                                  Items(context?.ActorTags).Contains(profile.inventory_seed_actor_tag)).ToArray()
                                ?? Array.Empty<ActorProfileDefinition>();
                            if (profiles.Length != 1 || profiles[0].id != snapshot.player.actorProfileId)
                                errors.Add($"Player '{snapshot.player.persistentId}' ActorProfileId does not match its unique bootstrap profile.");
                        }
                    }
                }
                var actorIds = new HashSet<string>(StringComparer.Ordinal);
                if (!string.IsNullOrWhiteSpace(snapshot.player?.actorInstanceId))
                    actorIds.Add(snapshot.player.actorInstanceId);
                var authoredActorLocators = new HashSet<string>(StringComparer.Ordinal);
                foreach (ActorState actor in Items(snapshot.actors))
                {
                    if (actor == null || !actorIds.Add(actor?.actorInstanceId))
                    {
                        errors.Add("Actors contain null or duplicate ActorInstanceId.");
                        continue;
                    }
                    actorStates[actor.actorInstanceId] = actor;
                    if (!ActorRuntimeIdentity.IsValidFormat(actor.actorInstanceId))
                        errors.Add($"ActorInstanceId '{actor.actorInstanceId}' is invalid.");
                    ActorProfileDefinition profile = database?.GetActorProfile(actor.actorProfileId);
                    if (profile == null)
                        errors.Add($"Actor '{actor.actorInstanceId}' references missing ActorProfileId '{actor.actorProfileId}'.");
                    if (actor.originKind != AuthoredActorOrigin && actor.originKind != RuntimeActorOrigin)
                        errors.Add($"Actor '{actor.actorInstanceId}' has unsupported origin '{actor.originKind}'.");
                    if (actor.lifecycleState != AliveLifecycle && actor.lifecycleState != DeadLifecycle)
                        errors.Add($"Actor '{actor.actorInstanceId}' has unsupported lifecycle '{actor.lifecycleState}'.");
                    if (!ValidPose(actor.pose))
                        errors.Add($"Actor '{actor.actorInstanceId}' has invalid pose.");
                    float maxHealth = profile?.health?.max_health ?? 0f;
                    bool healthValid = Finite(actor.currentHealth) && actor.currentHealth >= 0f && actor.currentHealth <= maxHealth;
                    if (!healthValid || actor.lifecycleState == AliveLifecycle && actor.currentHealth <= 0f ||
                        actor.lifecycleState == DeadLifecycle && actor.currentHealth != 0f)
                        errors.Add($"Actor '{actor.actorInstanceId}' lifecycle '{actor.lifecycleState}' contradicts health {actor.currentHealth}.");
                    ValidateMedical(actor.medicalState, $"Actor '{actor.actorInstanceId}'");

                    if (actor.originKind == AuthoredActorOrigin)
                    {
                        if (!authoredActorLocators.Add(actor.authoredSceneObjectId))
                            errors.Add($"Authored actor locator '{actor.authoredSceneObjectId}' is duplicated.");
                        AddEntity(entityKinds, actor.authoredSceneObjectId, "actor", typeof(ActorProfileComponent));
                        if (actor.authoredSceneObjectId == snapshot.player?.persistentId)
                            errors.Add($"Actor '{actor.actorInstanceId}' duplicates the player authored root.");
                        if (sceneIds.TryGetValue(actor.authoredSceneObjectId ?? string.Empty, out PersistentSceneObjectId root))
                        {
                            ActorProfileComponent component = root.GetComponent<ActorProfileComponent>();
                            ActorRuntimeIdentity runtimeIdentity = root.GetComponent<ActorRuntimeIdentity>();
                            string expectedId = !string.IsNullOrWhiteSpace(runtimeIdentity?.AuthoredActorInstanceId)
                                ? runtimeIdentity.AuthoredActorInstanceId
                                : ActorRuntimeIdentity.DeriveAuthoredActorInstanceId(actor.authoredSceneObjectId);
                            if (expectedId != actor.actorInstanceId)
                                errors.Add($"Authored actor '{actor.authoredSceneObjectId}' expects ActorInstanceId '{expectedId}', not '{actor.actorInstanceId}'.");
                            ActorProfileDefinition resolved = database?.GetActorProfile(component?.ActorProfileId);
                            if (resolved == null || resolved.id != actor.actorProfileId)
                                errors.Add($"Authored actor '{actor.authoredSceneObjectId}' profile does not match '{actor.actorProfileId}'.");
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(actor.authoredSceneObjectId))
                            errors.Add($"Runtime actor '{actor.actorInstanceId}' cannot carry an authored locator.");
                        if (!ActorSpawnService.CanSpawn(actor.actorProfileId, out _, out string spawnError))
                            errors.Add($"Runtime actor '{actor.actorInstanceId}' cannot be recreated: {spawnError}");
                        if (ActorRuntimeRegistry.TryGet(actor.actorInstanceId, out ActorRuntimeIdentity existing) &&
                            (existing.OriginKind != ActorOriginKind.Runtime || existing.ActorProfileId != actor.actorProfileId))
                            errors.Add($"Runtime actor '{actor.actorInstanceId}' conflicts with its active representation.");
                    }
                }
                if (Items(snapshot.actors).Length > 0)
                {
                    string[] expectedAuthored = FindScene<ActorProfileComponent>()
                        .Where(component => component.gameObject.activeInHierarchy && Identity(component) != null &&
                                            Identity(component).PersistentId != snapshot.player?.persistentId)
                        .Select(component => Identity(component).PersistentId).Distinct(StringComparer.Ordinal).ToArray();
                    if (!authoredActorLocators.SetEquals(expectedAuthored))
                        errors.Add("Actor records do not cover every non-player authored actor exactly once.");
                }
                var containerIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (ContainerState container in Items(snapshot.containers))
                {
                    if (container == null || !containerIds.Add(container?.persistentId)) { errors.Add("Containers contain null or duplicate identity."); continue; }
                    AddEntity(entityKinds, container.persistentId, "container", typeof(ContainerLootComponent));
                    if (!container.authoritativeStorage) errors.Add($"Container '{container.persistentId}' does not mark its storage authoritative.");
                    ValidateTagState(container.persistentId, container.mutableTags, ContainerTags,
                        new[] { "opened_container", "sealed_container" }, new[] { "unsearched_container", "storage_accessible" });
                    if (container.mutableTags != null && container.mutableTags.Contains("lootable_container") && container.mutableTags.Contains("looted_container"))
                        errors.Add($"Entity '{container.persistentId}' cannot be both lootable and looted.");
                }
                var corpseIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (CorpseState corpse in Items(snapshot.corpses))
                {
                    if (corpse == null || !corpseIds.Add(corpse?.persistentId)) { errors.Add("Corpses contain null or duplicate identity."); continue; }
                    AddEntity(entityKinds, corpse.persistentId, "corpse", typeof(ActorHealthComponent));
                    if (SceneComponent<InventoryComponent>(corpse.persistentId) == null) errors.Add($"Corpse '{corpse.persistentId}' has no InventoryComponent.");
                    if (!Finite(corpse.currentHealth) || corpse.currentHealth != 0f) errors.Add($"Corpse '{corpse.persistentId}' has invalid health {corpse.currentHealth}.");
                }
                var doorIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (DoorState door in Items(snapshot.doors))
                {
                    if (door == null || !doorIds.Add(door?.persistentId)) { errors.Add("Doors contain null or duplicate identity."); continue; }
                    AddEntity(entityKinds, door.persistentId, "door", typeof(WorldObjectTags));
                    if (!DoorStates.Contains(door.state)) errors.Add($"Door '{door.persistentId}' has unsupported state '{door.state}'.");
                    WorldObjectTags tags = SceneComponent<WorldObjectTags>(door.persistentId);
                    if (tags == null || !DoorStates.Any(tags.HasTag)) errors.Add($"Door '{door.persistentId}' does not resolve a current authored door.");
                }
                int liveContainerCount = FindScene<ContainerLootComponent>().Length;
                if (containerIds.Count != liveContainerCount) errors.Add($"Snapshot has {containerIds.Count} containers; current slice exposes {liveContainerCount}.");
                int liveDoorCount = sceneIds.Values.Count(identity =>
                    identity.GetComponent<WorldObjectTags>() is WorldObjectTags tags && DoorStates.Any(tags.HasTag));
                if (doorIds.Count != liveDoorCount) errors.Add($"Snapshot has {doorIds.Count} doors; current slice exposes {liveDoorCount}.");
                if (FindScene<ActorInteractionContext>().Count(actor =>
                        Identity(actor) != null && Items(actor.ActorTags).Contains("player")) != 1)
                    errors.Add("Current scene must expose exactly one persistent player.");
            }

            private void ValidateMedical(ActorMedicalStateData medicalState, string context)
            {
                if (!ActorMedicalStateComponent.TryValidatePersistenceState(medicalState, out string failure))
                    errors.Add($"{context} has invalid localized medical state: {failure}");
            }

            private void ValidateStorages()
            {
                foreach (StorageState storage in Items(snapshot.storages))
                {
                    if (storage == null || string.IsNullOrWhiteSpace(storage.storageId)) { errors.Add("Storages contain null or missing storageId."); continue; }
                    if (!storages.TryAdd(storage.storageId, storage)) { errors.Add($"Duplicate storage '{storage.storageId}'."); continue; }
                    if (storage.storageId != StorageId(storage.kind, storage.ownerId)) errors.Add($"Storage '{storage.storageId}' has inconsistent owner key.");
                    if (storage.usesGrid && (storage.width <= 0 || storage.height <= 0)) errors.Add($"Storage '{storage.storageId}' has invalid grid dimensions.");
                    if (!storage.usesGrid && (storage.width != 0 || storage.height != 0)) errors.Add($"Linear storage '{storage.storageId}' carries grid dimensions.");
                    ValidateStorageOwner(storage);
                    var placements = new List<GridPlacementState>();
                    foreach (StorageEntryState entry in Items(storage.entries))
                    {
                        if (entry == null || !items.TryGetValue(entry?.instanceId, out ItemState item))
                        {
                            errors.Add($"Storage '{storage.storageId}' has dangling item reference '{entry?.instanceId}'.");
                            continue;
                        }
                        ItemDefinition definition = database?.GetItem(item.definitionId);
                        int maxStack = definition != null ? Math.Max(1, definition.max_stack) : 1;
                        if (entry.quantity < 1 || entry.quantity > maxStack) errors.Add($"Storage '{storage.storageId}' item '{entry.instanceId}' has invalid quantity {entry.quantity}.");
                        AddLocation(entry.instanceId, storage.storageId);
                        if (storage.usesGrid) ValidatePlacement(storage, entry, definition, placements);
                        else if (entry.placement != null) errors.Add($"Linear storage '{storage.storageId}' item '{entry.instanceId}' has a grid placement.");
                    }
                }
                ValidateStorageReferences();
            }

            private void ValidateStorageOwner(StorageState storage)
            {
                if (storage.kind == InventoryKind || storage.kind == EquipmentKind)
                {
                    bool actor = snapshot.player?.persistentId == storage.ownerId ||
                                 actorStates.ContainsKey(storage.ownerId ?? string.Empty) ||
                                 Items(snapshot.corpses).Any(corpse => corpse?.persistentId == storage.ownerId);
                    if (!actor) errors.Add($"Storage '{storage.storageId}' references unsupported actor owner '{storage.ownerId}'.");
                }
                else if (storage.kind == ContainerKind)
                {
                    if (!Items(snapshot.containers).Any(container => container?.persistentId == storage.ownerId))
                        errors.Add($"Storage '{storage.storageId}' references missing container '{storage.ownerId}'.");
                }
                else if (storage.kind == ItemOwnedKind)
                {
                    if (!items.TryGetValue(storage.ownerId, out ItemState owner)) errors.Add($"Item-owned storage '{storage.storageId}' has missing owner item.");
                    else
                    {
                        ItemDefinition definition = database?.GetItem(owner.definitionId);
                        if (definition == null || string.IsNullOrWhiteSpace(definition.owned_storage_profile_id))
                            errors.Add($"Item '{owner.instanceId}' cannot own storage '{storage.storageId}'.");
                    }
                }
                else errors.Add($"Storage '{storage.storageId}' has unsupported kind '{storage.kind}'.");
            }

            private void ValidatePlacement(StorageState storage, StorageEntryState entry, ItemDefinition definition, List<GridPlacementState> previous)
            {
                GridPlacementState placement = entry.placement;
                if (placement == null) { errors.Add($"Grid storage '{storage.storageId}' item '{entry.instanceId}' has no placement."); return; }
                if (!GridFootprint.TryResolve(definition, out GridFootprint footprint, out _, out string footprintError))
                {
                    errors.Add($"Item '{entry.instanceId}' footprint failed: {footprintError}"); return;
                }
                int width = footprint.GetWidth(placement.rotated);
                int height = footprint.GetHeight(placement.rotated);
                if (placement.width != width || placement.height != height || placement.x < 0 || placement.y < 0 ||
                    placement.x + width > storage.width || placement.y + height > storage.height)
                    errors.Add($"Storage '{storage.storageId}' item '{entry.instanceId}' has invalid placement {placement.x},{placement.y} {placement.width}x{placement.height}.");
                foreach (GridPlacementState other in previous)
                    if (placement.x < other.x + other.width && placement.x + placement.width > other.x &&
                        placement.y < other.y + other.height && placement.y + placement.height > other.y)
                        errors.Add($"Storage '{storage.storageId}' has overlapping placements at {placement.x},{placement.y}.");
                previous.Add(placement);
            }

            private void ValidateStorageReferences()
            {
                if (snapshot.player != null)
                {
                    RequireStorage(snapshot.player.inventoryStorageId, InventoryKind, snapshot.player.persistentId);
                    if (!string.IsNullOrWhiteSpace(snapshot.player.equipmentStorageId)) RequireStorage(snapshot.player.equipmentStorageId, EquipmentKind, snapshot.player.persistentId);
                }
                foreach (ActorState actor in Items(snapshot.actors)) if (actor != null)
                {
                    RequireStorage(actor.inventoryStorageId, InventoryKind, actor.actorInstanceId);
                    if (!string.IsNullOrWhiteSpace(actor.equipmentStorageId))
                        RequireStorage(actor.equipmentStorageId, EquipmentKind, actor.actorInstanceId);
                }
                foreach (ContainerState container in Items(snapshot.containers)) if (container != null) RequireStorage(container.storageId, ContainerKind, container.persistentId);
                foreach (CorpseState corpse in Items(snapshot.corpses)) if (corpse != null)
                {
                    RequireStorage(corpse.inventoryStorageId, InventoryKind, corpse.persistentId);
                    if (!string.IsNullOrWhiteSpace(corpse.equipmentStorageId)) RequireStorage(corpse.equipmentStorageId, EquipmentKind, corpse.persistentId);
                }
                foreach (StorageState storage in storages.Values.Where(state => state.kind == ItemOwnedKind))
                {
                    if (locations.TryGetValue(storage.ownerId, out string ownerLocation) && ownerLocation.StartsWith(ItemOwnedKind + ":", StringComparison.Ordinal))
                        errors.Add($"Illegal nested owned storage: owner '{storage.ownerId}' is located in '{ownerLocation}'.");
                }
                foreach (ItemState item in items.Values)
                {
                    ItemDefinition definition = database?.GetItem(item.definitionId);
                    if (definition != null && !string.IsNullOrWhiteSpace(definition.owned_storage_profile_id) &&
                        !storages.ContainsKey(StorageId(ItemOwnedKind, item.instanceId)))
                        errors.Add($"Item '{item.instanceId}' is missing its required item-owned storage.");
                }
            }

            private void ValidateEquipment()
            {
                var owners = new HashSet<string>(StringComparer.Ordinal);
                foreach (EquipmentState state in Items(snapshot.equipment))
                {
                    if (state == null || !owners.Add(state?.ownerPersistentId)) { errors.Add("Equipment contains null or duplicate owner."); continue; }
                    RequireStorage(state.storageId, EquipmentKind, state.ownerPersistentId);
                    bool referencedByOwner = snapshot.player?.persistentId == state.ownerPersistentId && snapshot.player.equipmentStorageId == state.storageId ||
                        actorStates.TryGetValue(state.ownerPersistentId ?? string.Empty, out ActorState actor) && actor.equipmentStorageId == state.storageId ||
                        Items(snapshot.corpses).Any(corpse => corpse?.persistentId == state.ownerPersistentId && corpse.equipmentStorageId == state.storageId);
                    if (!referencedByOwner) errors.Add($"Equipment '{state.ownerPersistentId}' is not referenced by its actor state.");
                    if (ActorEquipmentComponent(state.ownerPersistentId) == null &&
                        (!actorStates.TryGetValue(state.ownerPersistentId ?? string.Empty, out ActorState equipmentActor) ||
                         equipmentActor.originKind == AuthoredActorOrigin))
                        errors.Add($"Equipment owner '{state.ownerPersistentId}' has no ActorEquipmentComponent.");
                    StorageState storage = storages.TryGetValue(state.storageId ?? string.Empty, out StorageState found) ? found : null;
                    EquipmentLayoutDefinition layout = database?.GetEquipmentLayout(state.layoutId);
                    if (layout == null) errors.Add($"Equipment owner '{state.ownerPersistentId}' references missing layout '{state.layoutId}'.");
                    var slots = new HashSet<string>(StringComparer.Ordinal);
                    var equippedIds = new HashSet<string>(StringComparer.Ordinal);
                    foreach (EquippedItemState equipped in Items(state.items))
                    {
                        if (equipped == null || !equippedIds.Add(equipped?.instanceId) || !items.TryGetValue(equipped?.instanceId ?? string.Empty, out ItemState item))
                        {
                            errors.Add($"Equipment '{state.ownerPersistentId}' has duplicate or dangling item '{equipped?.instanceId}'."); continue;
                        }
                        if (storage == null || !Items(storage.entries).Any(entry => entry?.instanceId == equipped.instanceId && entry.quantity == 1))
                            errors.Add($"Equipped item '{equipped.instanceId}' is missing quantity-one equipment storage entry.");
                        foreach (string slot in Items(equipped.slots))
                        {
                            if (!slots.Add(slot)) errors.Add($"Equipment '{state.ownerPersistentId}' duplicates slot '{slot}'.");
                            if (layout == null || layout.slots == null || !layout.slots.Any(candidate => candidate != null && candidate.slot_id == slot))
                                errors.Add($"Equipment '{state.ownerPersistentId}' references unavailable slot '{slot}'.");
                        }
                        string[][] alternatives = EquipmentOwnedStorageTransactionService.ResolveSlotSets(database?.GetItem(item.definitionId));
                        if (alternatives == null || !alternatives.Any(alternative => SetEquals(alternative, equipped.slots)))
                            errors.Add($"Equipped item '{equipped.instanceId}' does not occupy a complete compatible slot set.");
                    }
                    if (storage != null)
                        foreach (StorageEntryState entry in Items(storage.entries))
                            if (entry != null && !equippedIds.Contains(entry.instanceId)) errors.Add($"Equipment storage '{storage.storageId}' has unreferenced item '{entry.instanceId}'.");
                }
                if (!string.IsNullOrWhiteSpace(snapshot.player?.equipmentStorageId) && !owners.Contains(snapshot.player.persistentId))
                    errors.Add($"Player '{snapshot.player.persistentId}' is missing EquipmentState.");
                foreach (ActorState actor in Items(snapshot.actors))
                    if (actor != null && !string.IsNullOrWhiteSpace(actor.equipmentStorageId) && !owners.Contains(actor.actorInstanceId))
                        errors.Add($"Actor '{actor.actorInstanceId}' is missing EquipmentState.");
                foreach (CorpseState corpse in Items(snapshot.corpses))
                    if (corpse != null && !string.IsNullOrWhiteSpace(corpse.equipmentStorageId) && !owners.Contains(corpse.persistentId))
                        errors.Add($"Corpse '{corpse.persistentId}' is missing EquipmentState.");
            }

            private void ValidateWorldItems()
            {
                var authored = new HashSet<string>(StringComparer.Ordinal);
                var present = new HashSet<string>(StringComparer.Ordinal);
                foreach (WorldItemState world in Items(snapshot.worldItems))
                {
                    if (world == null) { errors.Add("World items contain null state."); continue; }
                    if (world.kind != AuthoredWorldKind && world.kind != RuntimeWorldKind) errors.Add($"World item '{world.instanceId}' has invalid kind '{world.kind}'.");
                    if (world.kind == AuthoredWorldKind && !authored.Add(world.instanceId)) errors.Add($"Duplicate authored world marker '{world.instanceId}'.");
                    if (world.kind == RuntimeWorldKind && !world.present) errors.Add($"Runtime world item '{world.instanceId}' cannot be absent.");
                    if (world.present)
                    {
                        if (!items.ContainsKey(world.instanceId)) errors.Add($"World representation '{world.instanceId}' has dangling item reference.");
                        if (world.quantity < 1) errors.Add($"World representation '{world.instanceId}' has invalid quantity {world.quantity}.");
                        if (items.TryGetValue(world.instanceId, out ItemState worldItem) && database?.GetItem(worldItem.definitionId) is ItemDefinition definition && world.quantity > Math.Max(1, definition.max_stack))
                            errors.Add($"World representation '{world.instanceId}' exceeds max stack with quantity {world.quantity}.");
                        if (!ValidPose(world.pose)) errors.Add($"World representation '{world.instanceId}' has invalid pose.");
                        if (!present.Add(world.instanceId)) errors.Add($"Duplicate present world representation '{world.instanceId}'.");
                        AddLocation(world.instanceId, "world:" + world.kind);
                    }
                    else if (world.quantity != 0 || world.pose != null) errors.Add($"Absent authored world marker '{world.instanceId}' carries runtime representation state.");
                }
                string[] liveAuthoredRaw = FindScene<WorldItemPickup>().Select(pickup => pickup.AuthoredItemInstanceId)
                    .Where(id => !string.IsNullOrWhiteSpace(id)).ToArray();
                foreach (IGrouping<string, string> duplicate in liveAuthoredRaw.GroupBy(id => id, StringComparer.Ordinal).Where(group => group.Count() > 1))
                    errors.Add($"Scene contains duplicate authored world item identity '{duplicate.Key}'.");
                string[] liveAuthored = liveAuthoredRaw.Distinct(StringComparer.Ordinal).ToArray();
                foreach (string authoredId in liveAuthored)
                    if (!authored.Contains(authoredId)) errors.Add($"Snapshot omits authored world item marker '{authoredId}'.");
                foreach (string authoredId in authored)
                    if (!liveAuthored.Contains(authoredId, StringComparer.Ordinal)) errors.Add($"Authored world item '{authoredId}' is not referencable in the current scene.");
                foreach (WorldItemState absent in Items(snapshot.worldItems).Where(world => world != null && world.kind == AuthoredWorldKind && !world.present))
                    if (!items.ContainsKey(absent.instanceId)) errors.Add($"Absent authored item '{absent.instanceId}' is not represented by the item table and another owner.");
            }

            private void AddEntity(Dictionary<string, string> kinds, string id, string kind, Type requiredComponent)
            {
                if (!PersistentSceneObjectId.IsValidFormat(id)) errors.Add($"{kind} has invalid PersistentSceneObjectId '{id}'.");
                else if (kinds.TryGetValue(id, out string previous)) errors.Add($"Persistent entity '{id}' appears as both {previous} and {kind}.");
                else kinds.Add(id, kind);
                if (!sceneIds.TryGetValue(id ?? string.Empty, out PersistentSceneObjectId identity) || identity.GetComponent(requiredComponent) == null)
                    errors.Add($"{kind} '{id}' does not resolve exactly once with {requiredComponent.Name}.");
            }

            private T SceneComponent<T>(string id) where T : Component
            {
                return sceneIds.TryGetValue(id ?? string.Empty, out PersistentSceneObjectId identity) ? identity.GetComponent<T>() : null;
            }

            private ActorEquipmentComponent ActorEquipmentComponent(string ownerId)
            {
                if (snapshot.player?.persistentId == ownerId)
                    return SceneComponent<ActorEquipmentComponent>(ownerId);
                if (actorStates.TryGetValue(ownerId ?? string.Empty, out ActorState actor))
                {
                    if (actor.originKind == AuthoredActorOrigin)
                        return SceneComponent<ActorEquipmentComponent>(actor.authoredSceneObjectId);
                    return ActorRuntimeRegistry.TryGet(ownerId, out ActorRuntimeIdentity identity)
                        ? identity.GetComponent<ActorEquipmentComponent>()
                        : null;
                }
                return SceneComponent<ActorEquipmentComponent>(ownerId);
            }

            private void RequireStorage(string storageId, string kind, string owner)
            {
                if (!storages.TryGetValue(storageId ?? string.Empty, out StorageState storage) || storage.kind != kind || storage.ownerId != owner)
                    errors.Add($"Reference '{storageId}' does not resolve {kind} storage for '{owner}'.");
            }

            private void AddLocation(string instanceId, string location)
            {
                if (locations.TryGetValue(instanceId ?? string.Empty, out string previous))
                    errors.Add($"Item '{instanceId}' appears in incompatible locations '{previous}' and '{location}'.");
                else locations[instanceId ?? string.Empty] = location;
            }

            private void ValidateTagState(string id, string[] tags, string[] allowed, params string[][] exclusivePairs)
            {
                string[] values = Items(tags);
                if (values.Distinct(StringComparer.Ordinal).Count() != values.Length) errors.Add($"Entity '{id}' has duplicate mutable tags.");
                foreach (string tag in values) if (!allowed.Contains(tag)) errors.Add($"Entity '{id}' has unsupported mutable tag '{tag}'.");
                foreach (string[] pair in exclusivePairs) if (pair.Count(values.Contains) != 1) errors.Add($"Entity '{id}' must contain exactly one of [{string.Join(", ", pair)}].");
            }

            private CurrentSliceValidationResult Error(string error) { errors.Add(error); return new CurrentSliceValidationResult(errors); }
            private static bool SetEquals(string[] first, string[] second) => new HashSet<string>(Items(first)).SetEquals(Items(second));
            private static bool ValidPose(PoseState pose) => pose != null && pose.position != null && pose.rotation != null &&
                Finite(pose.position.x) && Finite(pose.position.y) && Finite(pose.position.z) &&
                Finite(pose.rotation.x) && Finite(pose.rotation.y) && Finite(pose.rotation.z) && Finite(pose.rotation.w) &&
                Math.Abs(pose.rotation.x * pose.rotation.x + pose.rotation.y * pose.rotation.y +
                    pose.rotation.z * pose.rotation.z + pose.rotation.w * pose.rotation.w - 1f) <= 0.01f;
        }
    }
}
