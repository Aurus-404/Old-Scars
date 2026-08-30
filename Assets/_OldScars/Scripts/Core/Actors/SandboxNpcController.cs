using System;
using System.Collections.Generic;
using System.Linq;
using OldScars.Core.Combat;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Items;
using UnityEngine;
using UnityEngine.AI;

namespace OldScars.Core.Actors
{
    public enum SandboxCombatAffiliation
    {
        Blue,
        Red
    }

    [DisallowMultipleComponent]
    public sealed class SandboxNpcController : MonoBehaviour
    {
        public const string SandboxActorProfileId = "core:debug_sandbox_npc_01";
        public const string CombatSandboxActorProfileId = "core:debug_combat_sandbox_npc_01";
        public const string PlayerAffiliationId = "debug_player";
        public const string BlueAffiliationId = "debug_blue";
        public const string RedAffiliationId = "debug_red";
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
            if (player == null)
                return;
            ActorAffiliationComponent playerAffiliation = player.GetComponent<ActorAffiliationComponent>() ??
                                                          player.gameObject.AddComponent<ActorAffiliationComponent>();
            if (!playerAffiliation.TryConfigure(
                    PlayerAffiliationId, "Player", Array.Empty<string>(), out string error))
                Debug.LogError("[Actors][PLAYER_AFFILIATION_REJECTED]\n  Failure: " + error);
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

        public bool TrySpawnBlueNpc(out SandboxNpcMetadata metadata, out string error) =>
            TrySpawnCombatNpc(SandboxCombatAffiliation.Blue, out metadata, out error);

        public bool TrySpawnRedNpc(out SandboxNpcMetadata metadata, out string error) =>
            TrySpawnCombatNpc(SandboxCombatAffiliation.Red, out metadata, out error);

        private bool TrySpawnCombatNpc(
            SandboxCombatAffiliation requestedAffiliation,
            out SandboxNpcMetadata metadata,
            out string error)
        {
            metadata = null;
            error = null;
            if (player == null)
            {
                error = "Combat sandbox spawn requires the shared runtime player transform.";
                return Fail(error);
            }

            GameDatabase database = GameDataManager.Instance?.Database;
            ActorProfileDefinition actorProfile = database?.GetActorProfile(CombatSandboxActorProfileId);
            if (actorProfile == null || string.IsNullOrWhiteSpace(actorProfile.loadout_profile_id))
            {
                error = $"Combat sandbox actor profile '{CombatSandboxActorProfileId}' or its loadout profile is unavailable.";
                return Fail(error);
            }

            const int maximumWeaponRollAttempts = 8;
            for (int attempt = 0; attempt < maximumWeaponRollAttempts; attempt++)
            {
                long sequence = spawnSequence + attempt;
                long derivedSeed = ActorLoadoutService.DeriveSandboxSpawnSeed(
                    baseSeed, sequence, actorProfile.id, actorProfile.loadout_profile_id);
                if (!TryResolveSpawnPosition(player.position, derivedSeed, out Vector3 position, out error))
                    continue;
                if (!ActorSpawnService.TrySpawnWithLoadoutSeed(
                        actorProfile.id, position, Quaternion.identity, derivedSeed,
                        out ActorRuntimeIdentity identity, out ActorLoadoutResult loadout, out error))
                    continue;

                ActorItemOwnershipComponent ownership = identity.GetComponent<ActorItemOwnershipComponent>();
                if (!WeaponCombatService.TryGetEquippedWeapon(ownership, out _, out _, out _, out _))
                {
                    ActorSpawnService.TryRemoveRuntimeRepresentationForRestore(identity.ActorInstanceId, out _);
                    continue;
                }

                string affiliationId = requestedAffiliation == SandboxCombatAffiliation.Blue
                    ? BlueAffiliationId
                    : RedAffiliationId;
                string[] hostileAffiliations = requestedAffiliation == SandboxCombatAffiliation.Red
                    ? new[] { BlueAffiliationId, PlayerAffiliationId }
                    : Array.Empty<string>();
                ActorAffiliationComponent affiliation = identity.gameObject.AddComponent<ActorAffiliationComponent>();
                if (!affiliation.TryConfigure(
                        affiliationId, requestedAffiliation.ToString(), hostileAffiliations, out error))
                {
                    ActorSpawnService.TryRemoveRuntimeRepresentationForRestore(identity.ActorInstanceId, out _);
                    return Fail(error);
                }

                HumanEncounterAIController encounter = identity.GetComponent<HumanEncounterAIController>();
                encounter.ConfigureDeterministicAimSeed(derivedSeed);
                ActorThreatAcquisitionController acquisition =
                    identity.gameObject.AddComponent<ActorThreatAcquisitionController>();
                if (!acquisition.TryConfigure(derivedSeed, out error))
                {
                    ActorSpawnService.TryRemoveRuntimeRepresentationForRestore(identity.ActorInstanceId, out _);
                    return Fail(error);
                }

                metadata = identity.gameObject.AddComponent<SandboxNpcMetadata>();
                metadata.Configure(baseSeed, sequence, derivedSeed, loadout, requestedAffiliation.ToString());
                ApplyDebugColor(identity.gameObject, requestedAffiliation);
                spawned.RemoveAll(value => value == null);
                spawned.Add(metadata);
                LastSpawn = metadata;
                spawnSequence = sequence + 1;
                LastFeedback = "Spawned " + requestedAffiliation + " " + identity.ActorInstanceId +
                               " with " + loadout.Signature;
                Debug.Log("[Actors][COMBAT_SANDBOX_NPC_SPAWNED]" +
                          $"\n  ActorInstanceId: {identity.ActorInstanceId}" +
                          $"\n  Affiliation: {requestedAffiliation}" +
                          $"\n  SandboxBaseSeed: {baseSeed}" +
                          $"\n  SpawnSequence: {sequence}" +
                          $"\n  DerivedSpawnSeed: {derivedSeed}" +
                          $"\n  LoadoutProfileId: {loadout.ProfileId}" +
                          $"\n  LoadoutSignature: {loadout.Signature}" +
                          $"\n  Position: {position}");
                return true;
            }

            error = $"No armed combat sandbox loadout was produced within {maximumWeaponRollAttempts} deterministic attempts.";
            return Fail(error);
        }

        public string DescribeLastSpawn()
        {
            if (LastSpawn == null) return LastFeedback;
            ActorRuntimeIdentity identity = LastSpawn.GetComponent<ActorRuntimeIdentity>();
            ActorNavigationController navigation = LastSpawn.GetComponent<ActorNavigationController>();
            ActorEquipmentComponent equipment = LastSpawn.GetComponent<ActorEquipmentComponent>();
            InventoryComponent inventory = LastSpawn.GetComponent<InventoryComponent>();
            ActorAffiliationComponent affiliation = LastSpawn.GetComponent<ActorAffiliationComponent>();
            ActorConditionComponent condition = LastSpawn.GetComponent<ActorConditionComponent>();
            HumanEncounterAIController encounter = LastSpawn.GetComponent<HumanEncounterAIController>();
            ActorThreatAcquisitionController acquisition = LastSpawn.GetComponent<ActorThreatAcquisitionController>();
            GameDatabase database = GameDataManager.Instance?.Database;
            var lines = new List<string>
            {
                "Actor: " + (identity?.ActorInstanceId ?? "<NONE>"),
                "Seed: " + LastSpawn.DerivedSpawnSeed,
                "Loadout: " + LastSpawn.LoadoutProfileId,
                "Signature: " + LastSpawn.LoadoutSignature,
                "Affiliation: " + (affiliation?.DebugDisplayName ?? "<NONE>"),
                "Condition: " + (condition != null
                    ? condition.ObservableState + " / Stability " + condition.ConsciousnessStability.ToString("0.###") +
                      " / Blood " + condition.BloodFraction.ToString("0.###")
                    : "<NONE>"),
                "Target: " + (encounter?.ThreatActorInstanceId ?? "<NONE>"),
                "Perception: " + DescribePerception(encounter),
                "Recognition: " + (acquisition != null
                    ? acquisition.HighestRecognitionProgress.ToString("0.###") +
                      " / " + (acquisition.HighestRecognitionTargetActorInstanceId ?? "<NONE>")
                    : "<NONE>"),
                "Distance: " + DescribeDistance(identity, encounter),
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
            if (WeaponCombatService.TryGetEquippedWeapon(
                    LastSpawn.GetComponent<ActorItemOwnershipComponent>(), out _, out _,
                    out FirearmProfileDefinition firearm, out WeaponProfileDefinition melee))
                lines.Add("Weapon range: " + (firearm != null ? firearm.range : melee.melee_range).ToString("0.###"));
            else
                lines.Add("Weapon range: <NONE>");
            lines.Add("AI State: " + (encounter != null
                ? encounter.IsClosingDistance ? "Closing Distance" : encounter.State.ToString()
                : "<NONE>"));
            lines.Add("Focus: " + (encounter != null ? encounter.CurrentFocus.ToString("0.###") : "<NONE>"));
            lines.Add("Spread: " + (encounter != null ? encounter.CurrentSpreadDegrees.ToString("0.###") + " deg" : "<NONE>"));
            lines.Add("Backpack: " + Category(equipped, database, item => !string.IsNullOrWhiteSpace(item.owned_storage_profile_id)));
            lines.Add("Armor: " + Category(equipped, database, item => !string.IsNullOrWhiteSpace(item.armor_profile_id)));
            return string.Join("\n", lines);
        }

        private static string DescribePerception(HumanEncounterAIController encounter)
        {
            if (encounter == null || encounter.Threat == null)
                return "<NONE>";
            return encounter.LastPerception.Perceived
                ? "Perceived"
                : encounter.LastPerception.Reason.ToString();
        }

        private static string DescribeDistance(ActorRuntimeIdentity identity, HumanEncounterAIController encounter)
        {
            return identity != null && encounter?.Threat != null
                ? Vector3.Distance(identity.transform.position, encounter.Threat.transform.position).ToString("0.###")
                : "<NONE>";
        }

        private static void ApplyDebugColor(GameObject actor, SandboxCombatAffiliation affiliation)
        {
            Renderer renderer = actor != null ? actor.GetComponentInChildren<Renderer>() : null;
            if (renderer != null)
                renderer.material.color = affiliation == SandboxCombatAffiliation.Blue
                    ? new Color(0.16f, 0.42f, 1f)
                    : new Color(0.9f, 0.12f, 0.08f);
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
        public string DebugAffiliation { get; private set; }

        internal void Configure(
            long baseSeed,
            long sequence,
            long derivedSeed,
            ActorLoadoutResult loadout,
            string debugAffiliation = null)
        {
            SandboxBaseSeed = baseSeed;
            SpawnSequence = sequence;
            DerivedSpawnSeed = derivedSeed;
            LoadoutProfileId = loadout.ProfileId;
            LoadoutSignature = loadout.Signature;
            DebugAffiliation = debugAffiliation;
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
