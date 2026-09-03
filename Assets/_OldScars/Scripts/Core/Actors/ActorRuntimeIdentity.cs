using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Identity;
using OldScars.Core.Interactions;
using OldScars.Core.Items;
using UnityEngine;

namespace OldScars.Core.Actors
{
    public enum ActorOriginKind
    {
        Authored,
        Runtime
    }

    public enum ActorLifecycleState
    {
        Alive,
        Dead
    }

    public enum ActorSpawnInitialization
    {
        Bootstrap,
        PersistenceRestore
    }

    [DisallowMultipleComponent]
    public sealed class ActorRuntimeIdentity : MonoBehaviour
    {
        private const string AuthoredHashSalt = "old_scars:actor-authored:v1|";
        private static readonly Regex ValidId = new Regex(
            "^actor_[0-9a-f]{32}$",
            RegexOptions.CultureInvariant);

        [SerializeField] private string authoredActorInstanceId;

        private string actorInstanceId;
        private string actorProfileId;
        private ActorOriginKind originKind;
        private ActorLifecycleState lifecycleState = ActorLifecycleState.Alive;
        private bool registered;

        public string ActorInstanceId => actorInstanceId;
        public string ActorProfileId => actorProfileId;
        public string AuthoredActorInstanceId => authoredActorInstanceId;
        public ActorOriginKind OriginKind => originKind;
        public ActorLifecycleState LifecycleState => lifecycleState;
        public bool IsRegistered => registered;

        public static bool IsValidFormat(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && ValidId.IsMatch(value);
        }

        public static string DeriveAuthoredActorInstanceId(string persistentSceneObjectId)
        {
            if (!PersistentSceneObjectId.IsValidFormat(persistentSceneObjectId))
                throw new ArgumentException("A valid PersistentSceneObjectId is required.", nameof(persistentSceneObjectId));

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(AuthoredHashSalt + persistentSceneObjectId));
                var builder = new StringBuilder(32);
                for (int index = 0; index < 16; index++)
                    builder.Append(hash[index].ToString("x2"));
                return "actor_" + builder;
            }
        }

        public static bool TryEnsureAuthored(
            GameObject root,
            string canonicalProfileId,
            out ActorRuntimeIdentity identity,
            out string error)
        {
            identity = null;
            error = null;
            if (root == null)
            {
                error = "Actor root is missing.";
                return false;
            }

            PersistentSceneObjectId sceneIdentity = root.GetComponent<PersistentSceneObjectId>();
            if (sceneIdentity == null || !sceneIdentity.enabled ||
                !PersistentSceneObjectId.IsValidFormat(sceneIdentity.PersistentId))
            {
                error = $"Authored actor '{root.name}' has no valid enabled PersistentSceneObjectId.";
                return false;
            }

            identity = root.GetComponent<ActorRuntimeIdentity>();
            if (identity == null)
                identity = root.AddComponent<ActorRuntimeIdentity>();

            string resolvedId = string.IsNullOrWhiteSpace(identity.authoredActorInstanceId)
                ? DeriveAuthoredActorInstanceId(sceneIdentity.PersistentId)
                : identity.authoredActorInstanceId;
            ActorLifecycleState lifecycle = root.GetComponent<ActorHealthComponent>()?.IsDead == true
                ? ActorLifecycleState.Dead
                : ActorLifecycleState.Alive;
            return identity.TryConfigure(
                resolvedId, canonicalProfileId, ActorOriginKind.Authored, lifecycle, out error);
        }

        internal bool TryConfigure(
            string instanceId,
            string canonicalProfileId,
            ActorOriginKind origin,
            ActorLifecycleState lifecycle,
            out string error)
        {
            error = null;
            if (!IsValidFormat(instanceId))
            {
                error = $"ActorInstanceId '{Safe(instanceId)}' must match actor_<32 hex lowercase>.";
                return false;
            }
            if (!ContentId.TryParse(canonicalProfileId, out ContentId _, out string profileError))
            {
                error = $"ActorProfileId '{Safe(canonicalProfileId)}' is not canonical: {profileError}";
                return false;
            }
            if (registered)
            {
                if (actorInstanceId == instanceId && actorProfileId == canonicalProfileId && originKind == origin)
                {
                    lifecycleState = lifecycle;
                    return true;
                }
                error = $"Actor identity '{actorInstanceId}' is immutable after registration.";
                return false;
            }
            if (!ActorRuntimeRegistry.TryRegister(instanceId, this, out error))
                return false;

            actorInstanceId = instanceId;
            actorProfileId = canonicalProfileId;
            originKind = origin;
            lifecycleState = lifecycle;
            registered = true;
            return true;
        }

        internal void SetLifecycle(ActorLifecycleState state)
        {
            lifecycleState = state;
        }

        internal void ReleaseRepresentation()
        {
            if (!registered)
                return;
            ActorRuntimeRegistry.Unregister(actorInstanceId, this);
            registered = false;
        }

        private void OnDestroy()
        {
            ReleaseRepresentation();
        }

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<EMPTY>" : value;
        }
    }

    public static class ActorRuntimeRegistry
    {
        private static readonly Dictionary<string, ActorRuntimeIdentity> Active =
            new Dictionary<string, ActorRuntimeIdentity>(StringComparer.Ordinal);

        public static int ActiveCount => Active.Count;
        public static IReadOnlyCollection<ActorRuntimeIdentity> ActiveRepresentations => Active.Values.ToArray();

        public static int CopyActiveRepresentationsTo(List<ActorRuntimeIdentity> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            destination.Clear();
            foreach (ActorRuntimeIdentity identity in Active.Values)
                destination.Add(identity);
            return destination.Count;
        }

        public static bool TryGet(string actorInstanceId, out ActorRuntimeIdentity identity)
        {
            return Active.TryGetValue(actorInstanceId ?? string.Empty, out identity) && identity != null;
        }

        internal static bool TryRegister(string actorInstanceId, ActorRuntimeIdentity identity, out string error)
        {
            error = null;
            if (Active.TryGetValue(actorInstanceId, out ActorRuntimeIdentity existing))
            {
                if (ReferenceEquals(existing, identity))
                    return true;
                error = $"Duplicate active ActorInstanceId '{actorInstanceId}' on '{identity?.name ?? "<UNKNOWN>"}'; " +
                        $"already registered by '{existing?.name ?? "<UNKNOWN>"}'.";
                return false;
            }
            Active.Add(actorInstanceId, identity);
            return true;
        }

        internal static void Unregister(string actorInstanceId, ActorRuntimeIdentity identity)
        {
            if (Active.TryGetValue(actorInstanceId ?? string.Empty, out ActorRuntimeIdentity existing) &&
                ReferenceEquals(existing, identity))
                Active.Remove(actorInstanceId);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeSession()
        {
            Active.Clear();
        }
    }

    public static class ActorSpawnService
    {
        public static bool CanSpawn(string actorProfileId, out string canonicalProfileId, out string error)
        {
            canonicalProfileId = null;
            error = null;
            GameDatabase database = GameDataManager.Instance?.Database;
            if (database == null || GameDataManager.Instance?.IsReady != true)
            {
                error = "GameDatabase is not ready.";
                return false;
            }
            ActorProfileDefinition profile = database.GetActorProfile(actorProfileId);
            if (profile == null)
            {
                error = $"Actor profile '{Safe(actorProfileId)}' was not found.";
                return false;
            }
            canonicalProfileId = profile.id;
            return true;
        }

        public static bool TrySpawn(
            string actorProfileId,
            Vector3 position,
            Quaternion rotation,
            out ActorRuntimeIdentity identity,
            out string error)
        {
            return TrySpawnInternal(
                actorProfileId, position, rotation, null,
                ActorSpawnInitialization.Bootstrap, null, out identity, out _, out error);
        }

        public static bool TrySpawnWithLoadoutSeed(
            string actorProfileId,
            Vector3 position,
            Quaternion rotation,
            long loadoutSeed,
            out ActorRuntimeIdentity identity,
            out ActorLoadoutResult loadout,
            out string error)
        {
            return TrySpawnInternal(
                actorProfileId, position, rotation, null,
                ActorSpawnInitialization.Bootstrap, loadoutSeed, out identity, out loadout, out error);
        }

        public static bool TrySpawn(
            string actorProfileId,
            Vector3 position,
            Quaternion rotation,
            string existingActorInstanceId,
            ActorSpawnInitialization initialization,
            out ActorRuntimeIdentity identity,
            out string error)
        {
            return TrySpawnInternal(
                actorProfileId, position, rotation, existingActorInstanceId,
                initialization, null, out identity, out _, out error);
        }

        private static bool TrySpawnInternal(
            string actorProfileId,
            Vector3 position,
            Quaternion rotation,
            string existingActorInstanceId,
            ActorSpawnInitialization initialization,
            long? loadoutSeed,
            out ActorRuntimeIdentity identity,
            out ActorLoadoutResult loadout,
            out string error)
        {
            identity = null;
            loadout = null;
            error = null;
            if (!CanSpawn(actorProfileId, out string canonicalProfileId, out error))
                return false;
            ActorProfileDefinition actorProfile = GameDataManager.Instance.Database.GetActorProfile(canonicalProfileId);

            string actorInstanceId = existingActorInstanceId;
            if (initialization == ActorSpawnInitialization.Bootstrap)
            {
                if (!string.IsNullOrWhiteSpace(actorInstanceId))
                {
                    error = "A new runtime actor cannot receive an existing ActorInstanceId.";
                    return false;
                }
                actorInstanceId = "actor_" + Guid.NewGuid().ToString("N");
            }
            else if (!ActorRuntimeIdentity.IsValidFormat(actorInstanceId))
            {
                error = $"Restore ActorInstanceId '{Safe(actorInstanceId)}' is invalid.";
                return false;
            }
            if (ActorRuntimeRegistry.TryGet(actorInstanceId, out _))
            {
                error = $"ActorInstanceId '{actorInstanceId}' already has an active representation.";
                return false;
            }

            GameObject root = null;
            try
            {
                root = ActorRuntimeRepresentationFactory.Create(actorProfile, position, rotation);
                root.name = "Runtime Actor " + actorInstanceId;
                int interactableLayer = LayerMask.NameToLayer("Interactable");
                if (interactableLayer >= 0)
                    SetLayerRecursively(root, interactableLayer);

                root.AddComponent<WorldObjectTags>();
                root.AddComponent<WorldObjectDebugInfo>();
                identity = root.AddComponent<ActorRuntimeIdentity>();
                if (!identity.TryConfigure(
                        actorInstanceId, canonicalProfileId, ActorOriginKind.Runtime,
                        ActorLifecycleState.Alive, out error))
                    throw new InvalidOperationException(error);

                root.AddComponent<InventoryComponent>();
                root.AddComponent<ActorItemOwnershipComponent>();
                root.AddComponent<ActorEquipmentComponent>();
                root.AddComponent<ActorHealthComponent>();
                root.AddComponent<LootableActorInventoryComponent>();
                ActorProfileComponent profileComponent = root.AddComponent<ActorProfileComponent>();
                bool configured = initialization == ActorSpawnInitialization.Bootstrap
                    ? profileComponent.TryApplyRuntimeBootstrap(canonicalProfileId, out error)
                    : profileComponent.TryPreparePersistenceRestore(canonicalProfileId, out error);
                if (!configured)
                    throw new InvalidOperationException(error);

                if (initialization == ActorSpawnInitialization.Bootstrap &&
                    !string.IsNullOrWhiteSpace(actorProfile.loadout_profile_id))
                {
                    if (!loadoutSeed.HasValue)
                        throw new InvalidOperationException(
                            $"Actor profile '{canonicalProfileId}' requires an explicit loadout seed for a new runtime spawn.");
                    if (!ActorLoadoutService.TryApply(identity, actorProfile, loadoutSeed.Value, out loadout, out error))
                        throw new InvalidOperationException(error);
                }

                Debug.Log(
                    "[Actors][SPAWN_COMMITTED]" +
                    $"\n  ActorInstanceId: {actorInstanceId}" +
                    $"\n  ActorProfileId: {canonicalProfileId}" +
                    $"\n  Initialization: {initialization}" +
                    $"\n  LoadoutProfileId: {loadout?.ProfileId ?? "<ABSENT>"}" +
                    $"\n  LoadoutSignature: {loadout?.Signature ?? "<ABSENT>"}" +
                    "\n  Origin: Runtime");
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                bool removed = identity != null && identity.IsRegistered &&
                               TryRemoveRuntimeRepresentationForRestore(identity.ActorInstanceId, out _);
                if (!removed && root != null)
                {
                    identity?.ReleaseRepresentation();
                    root.SetActive(false);
                    UnityEngine.Object.Destroy(root);
                }
                identity = null;
                loadout = null;
                return false;
            }
        }

        public static bool TryRemoveRuntimeRepresentationForRestore(string actorInstanceId, out string error)
        {
            error = null;
            if (!ActorRuntimeRegistry.TryGet(actorInstanceId, out ActorRuntimeIdentity identity) || identity == null)
            {
                error = $"Runtime actor representation '{Safe(actorInstanceId)}' was not found.";
                return false;
            }
            if (identity.OriginKind != ActorOriginKind.Runtime)
            {
                error = $"Authored actor '{actorInstanceId}' cannot be despawned as a runtime representation.";
                return false;
            }

            try
            {
                var itemIds = new HashSet<string>(StringComparer.Ordinal);
                InventoryComponent inventory = identity.GetComponent<InventoryComponent>();
                ActorEquipmentComponent equipment = identity.GetComponent<ActorEquipmentComponent>();
                CollectItemIds(inventory?.Entries, itemIds);
                CollectItemIds(equipment?.Entries, itemIds);

                if (inventory != null)
                    ReplaceEmpty(inventory.InternalGridBackend);
                if (equipment != null)
                {
                    ReplaceEmpty(equipment.Backend);
                    equipment.RestoreEquipmentState(new ActorEquipmentComponent.EquipmentStateSnapshot(
                        new Dictionary<string, string>(), new Dictionary<string, string[]>(), equipment.Version + 1));
                }
                foreach (string itemId in itemIds.OrderBy(value => value, StringComparer.Ordinal))
                    if (ItemInstanceIdRegistry.Instance.IsActive(itemId))
                        ItemInstanceIdRegistry.Instance.RetireAfterCommit(itemId);

                identity.ReleaseRepresentation();
                identity.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(identity.gameObject);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            for (int index = 0; index < root.transform.childCount; index++)
                SetLayerRecursively(root.transform.GetChild(index).gameObject, layer);
        }

        private static void CollectItemIds(IReadOnlyList<ItemStorageEntry> entries, HashSet<string> result)
        {
            if (entries == null)
                return;
            for (int index = 0; index < entries.Count; index++)
            {
                ItemInstance item = entries[index]?.Item;
                if (item == null || !result.Add(item.InstanceId) || !item.HasOwnedStorage)
                    continue;
                CollectItemIds(item.OwnedStorage.GridStorageEntries, result);
            }
        }

        private static void ReplaceEmpty(GridInventoryBackend backend)
        {
            if (!backend.TryReplaceWithExactEntries(
                    Array.Empty<ItemStorageEntry>(), false, 0, 0,
                    Array.Empty<GridPlacement>(), out string error))
                throw new InvalidOperationException("Actor representation teardown failed: " + error);
        }

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<EMPTY>" : value;
        }
    }
}
