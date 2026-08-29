using System;
using System.Collections.Generic;
using System.Linq;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Items;
using UnityEngine;
using UnityEngine.AI;

namespace OldScars.Core.Actors
{
    [DisallowMultipleComponent]
    public sealed class SandboxNpcController : MonoBehaviour
    {
        public const string SandboxActorProfileId = "core:debug_sandbox_npc_01";
        public const long DefaultBaseSeed = 41303L;
        private const float MinimumSpawnRadius = 4f;
        private const float MaximumSpawnRadius = 10f;
        private const int CandidateCount = 24;

        private Transform player;
        private long baseSeed = DefaultBaseSeed;
        private long spawnSequence;
        private readonly List<SandboxNpcMetadata> spawned = new List<SandboxNpcMetadata>();

        public long BaseSeed => baseSeed;
        public long SpawnSequence => spawnSequence;
        public SandboxNpcMetadata LastSpawn { get; private set; }
        public string LastFeedback { get; private set; } = "No sandbox NPC spawned yet.";
        public IReadOnlyList<SandboxNpcMetadata> Spawned => spawned;

        public void BindRuntime(Transform playerTransform)
        {
            player = playerTransform;
        }

        public bool TrySetBaseSeed(string text, out string error)
        {
            if (!long.TryParse(text, out long parsed))
            {
                error = "Sandbox base seed must be a signed 64-bit integer.";
                return false;
            }
            if (parsed != baseSeed)
            {
                baseSeed = parsed;
                spawnSequence = 0;
            }
            error = null;
            return true;
        }

        public bool TrySpawnRandomNpc(out SandboxNpcMetadata metadata, out string error)
        {
            metadata = null;
            error = null;
            if (player == null)
            {
                error = "Sandbox NPC spawn requires the shared runtime player transform.";
                return Fail(error);
            }
            GameDatabase database = GameDataManager.Instance?.Database;
            ActorProfileDefinition actorProfile = database?.GetActorProfile(SandboxActorProfileId);
            if (actorProfile == null || string.IsNullOrWhiteSpace(actorProfile.loadout_profile_id))
            {
                error = $"Sandbox actor profile '{SandboxActorProfileId}' or its loadout profile is unavailable.";
                return Fail(error);
            }

            long sequence = spawnSequence;
            long derivedSeed = ActorLoadoutService.DeriveSandboxSpawnSeed(
                baseSeed, sequence, actorProfile.id, actorProfile.loadout_profile_id);
            if (!TryResolveSpawnPosition(player.position, derivedSeed, out Vector3 position, out error))
                return Fail(error);

            if (!ActorSpawnService.TrySpawnWithLoadoutSeed(
                    actorProfile.id, position, Quaternion.identity, derivedSeed,
                    out ActorRuntimeIdentity identity, out ActorLoadoutResult loadout, out error))
                return Fail(error);

            metadata = identity.gameObject.AddComponent<SandboxNpcMetadata>();
            metadata.Configure(baseSeed, sequence, derivedSeed, loadout);
            SandboxActorRoamingController roaming = identity.gameObject.AddComponent<SandboxActorRoamingController>();
            roaming.Configure(derivedSeed);
            spawned.RemoveAll(value => value == null);
            spawned.Add(metadata);
            LastSpawn = metadata;
            spawnSequence++;
            LastFeedback = "Spawned " + identity.ActorInstanceId + " with " + loadout.Signature;

            Debug.Log("[Actors][SANDBOX_NPC_SPAWNED]" +
                      $"\n  ActorInstanceId: {identity.ActorInstanceId}" +
                      $"\n  SandboxBaseSeed: {baseSeed}" +
                      $"\n  SpawnSequence: {sequence}" +
                      $"\n  DerivedSpawnSeed: {derivedSeed}" +
                      $"\n  LoadoutProfileId: {loadout.ProfileId}" +
                      $"\n  LoadoutSignature: {loadout.Signature}" +
                      $"\n  Position: {position}");
            return true;
        }

        public string DescribeLastSpawn()
        {
            if (LastSpawn == null) return LastFeedback;
            ActorRuntimeIdentity identity = LastSpawn.GetComponent<ActorRuntimeIdentity>();
            ActorNavigationController navigation = LastSpawn.GetComponent<ActorNavigationController>();
            ActorEquipmentComponent equipment = LastSpawn.GetComponent<ActorEquipmentComponent>();
            InventoryComponent inventory = LastSpawn.GetComponent<InventoryComponent>();
            GameDatabase database = GameDataManager.Instance?.Database;
            var lines = new List<string>
            {
                "Actor: " + (identity?.ActorInstanceId ?? "<NONE>"),
                "Seed: " + LastSpawn.DerivedSpawnSeed,
                "Loadout: " + LastSpawn.LoadoutProfileId,
                "Signature: " + LastSpawn.LoadoutSignature,
                "Navigation: " + (navigation != null ? navigation.State.ToString() : "<NONE>"),
                "Equipment:"
            };
            ItemStorageEntry[] equipped = equipment?.Entries.Where(entry => entry?.Item != null)
                .OrderBy(entry => entry.Item.InstanceId, StringComparer.Ordinal).ToArray() ?? Array.Empty<ItemStorageEntry>();
            for (int index = 0; index < equipped.Length; index++)
            {
                ItemStorageEntry entry = equipped[index];
                lines.Add("  " + string.Join("+", equipment.GetSlotsOccupiedBy(entry.Item.InstanceId)) + " -> " +
                          Display(database, entry.DefinitionId) + " [" + entry.Item.InstanceId + "]");
            }
            if (equipped.Length == 0) lines.Add("  <EMPTY>");
            lines.Add("Inventory:");
            ItemStorageEntry[] carried = inventory?.Entries.Where(entry => entry?.Item != null)
                .OrderBy(entry => entry.Item.InstanceId, StringComparer.Ordinal).ToArray() ?? Array.Empty<ItemStorageEntry>();
            for (int index = 0; index < carried.Length; index++)
                lines.Add("  " + Display(database, carried[index].DefinitionId) + " x" + carried[index].Quantity +
                          " [" + carried[index].Item.InstanceId + "]");
            if (carried.Length == 0) lines.Add("  <EMPTY>");
            lines.Add("Weapon: " + Category(equipped, database, item =>
                item.combat != null || !string.IsNullOrWhiteSpace(item.firearm_profile_id)));
            lines.Add("Backpack: " + Category(equipped, database, item => !string.IsNullOrWhiteSpace(item.owned_storage_profile_id)));
            lines.Add("Armor: " + Category(equipped, database, item => !string.IsNullOrWhiteSpace(item.armor_profile_id)));
            return string.Join("\n", lines);
        }

        private static bool TryResolveSpawnPosition(Vector3 origin, long seed, out Vector3 position, out string error)
        {
            for (int index = 0; index < CandidateCount; index++)
            {
                ulong mixed = Mix(unchecked((ulong)seed) + (ulong)index * 0x9E3779B97F4A7C15UL);
                float angle = (mixed & 0xffffUL) / 65535f * Mathf.PI * 2f;
                float radius = Mathf.Lerp(MinimumSpawnRadius, MaximumSpawnRadius, ((mixed >> 16) & 0xffffUL) / 65535f);
                Vector3 candidate = origin + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 12f, NavMesh.AllAreas)) continue;
                if ((hit.position - origin).sqrMagnitude < MinimumSpawnRadius * MinimumSpawnRadius) continue;
                Vector3 lower = hit.position + Vector3.up * 0.55f;
                Vector3 upper = hit.position + Vector3.up * 1.75f;
                if (Physics.CheckCapsule(lower, upper, 0.35f, ~0, QueryTriggerInteraction.Ignore)) continue;
                position = hit.position;
                error = null;
                return true;
            }
            position = default;
            error = "No valid nearby NavMesh position with actor clearance was found; no actor was created.";
            return false;
        }

        private bool Fail(string error)
        {
            LastFeedback = error;
            Debug.LogWarning("[Actors][SANDBOX_NPC_SPAWN_REJECTED]\n  Failure: " + error +
                             "\n  ActionTaken: no actor or loadout ItemInstances were committed");
            return false;
        }

        private static string Display(GameDatabase database, string definitionId)
        {
            ItemDefinition item = database?.GetItem(definitionId);
            return item?.display != null && !string.IsNullOrWhiteSpace(item.display.name)
                ? item.display.name + " (" + definitionId + ")"
                : definitionId;
        }

        private static string Category(IEnumerable<ItemStorageEntry> entries, GameDatabase database, Func<ItemDefinition, bool> match)
        {
            ItemStorageEntry entry = entries.FirstOrDefault(value =>
            {
                ItemDefinition item = database?.GetItem(value.DefinitionId);
                return item != null && match(item);
            });
            return entry?.Item != null ? Display(database, entry.DefinitionId) + " [" + entry.Item.InstanceId + "]" : "<NONE>";
        }

        private static ulong Mix(ulong value)
        {
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }

    [DisallowMultipleComponent]
    public sealed class SandboxNpcMetadata : MonoBehaviour
    {
        public long SandboxBaseSeed { get; private set; }
        public long SpawnSequence { get; private set; }
        public long DerivedSpawnSeed { get; private set; }
        public string LoadoutProfileId { get; private set; }
        public string LoadoutSignature { get; private set; }

        internal void Configure(long baseSeed, long sequence, long derivedSeed, ActorLoadoutResult loadout)
        {
            SandboxBaseSeed = baseSeed;
            SpawnSequence = sequence;
            DerivedSpawnSeed = derivedSeed;
            LoadoutProfileId = loadout.ProfileId;
            LoadoutSignature = loadout.Signature;
        }
    }

    [DisallowMultipleComponent]
    public sealed class SandboxActorRoamingController : MonoBehaviour
    {
        private const float MinimumRadius = 2.5f;
        private const float MaximumRadius = 14f;
        private const float RetryDelay = 1.5f;
        private ActorRuntimeIdentity identity;
        private ActorNavigationController navigation;
        private long seed;
        private long decisionSequence;
        private float nextDecisionTime;

        public int AcceptedOrderCount { get; private set; }
        public int FailedDecisionCount { get; private set; }
        public string LastDecisionFailure { get; private set; }

        internal void Configure(long derivedSpawnSeed)
        {
            seed = derivedSpawnSeed;
            identity = GetComponent<ActorRuntimeIdentity>();
            navigation = GetComponent<ActorNavigationController>();
            nextDecisionTime = Time.time + 0.5f;
        }

        private void Update()
        {
            if (identity == null || identity.LifecycleState == ActorLifecycleState.Dead)
            {
                navigation?.Stop();
                enabled = false;
                return;
            }
            if (navigation == null || Time.time < nextDecisionTime || navigation.State == ActorNavigationState.Moving) return;

            long currentDecision = decisionSequence++;
            bool accepted = false;
            NavMeshAgent agent = navigation.Agent;
            Vector3 navOrigin = agent.nextPosition;
            string failure = "No local NavMesh candidate was accepted. NavOrigin=" + navOrigin +
                             "; Transform=" + transform.position + "; BaseOffset=" + agent.baseOffset.ToString("0.###") + ".";
            for (int candidateIndex = 0; candidateIndex < 8 && !accepted; candidateIndex++)
            {
                long domain = ActorLoadoutService.DeriveSandboxSpawnSeed(
                    seed, currentDecision * 8 + candidateIndex, identity.ActorProfileId, "roam");
                ulong mixed = unchecked((ulong)domain);
                float angle = (mixed & 0xffffUL) / 65535f * Mathf.PI * 2f;
                float radius = Mathf.Lerp(MinimumRadius, MaximumRadius, ((mixed >> 16) & 0xffffUL) / 65535f);
                Vector3 candidate = navOrigin + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 12f, NavMesh.AllAreas)) continue;
                accepted = navigation.TryNavigate(hit.position, out ActorNavigationResult result);
                failure = result.Failure + ": " + result.Detail;
            }
            if (accepted)
            {
                AcceptedOrderCount++;
                LastDecisionFailure = null;
            }
            else
            {
                FailedDecisionCount++;
                LastDecisionFailure = failure;
            }
            nextDecisionTime = Time.time + RetryDelay;
        }
    }
}
