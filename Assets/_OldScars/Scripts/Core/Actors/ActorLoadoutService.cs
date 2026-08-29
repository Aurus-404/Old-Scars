using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Items;

namespace OldScars.Core.Actors
{
    public sealed class ActorLoadoutResult
    {
        internal ActorLoadoutResult(string profileId, long seed, string signature, string[] selections)
        {
            ProfileId = profileId;
            Seed = seed;
            Signature = signature;
            Selections = selections ?? Array.Empty<string>();
        }

        public string ProfileId { get; }
        public long Seed { get; }
        public string Signature { get; }
        public IReadOnlyList<string> Selections { get; }
    }

    /// <summary>
    /// Materializes a weighted actor loadout once as normal inventory/equipment state.
    /// Persistence and corpse access operate on those ItemInstances and never call this service.
    /// </summary>
    public static class ActorLoadoutService
    {
        public static long DeriveSandboxSpawnSeed(long baseSeed, long spawnSequence, string actorProfileId, string loadoutProfileId)
        {
            return DeriveInt64("old_scars|sandbox_actor_spawn_v1|" + baseSeed + "|" + spawnSequence + "|" +
                               Safe(actorProfileId) + "|" + Safe(loadoutProfileId));
        }

        public static int ResolveWeightedChoiceIndex(IReadOnlyList<int> weights, long roll)
        {
            if (weights == null || weights.Count == 0) throw new ArgumentException("Weights are required.", nameof(weights));
            long total = 0;
            for (int index = 0; index < weights.Count; index++)
            {
                if (weights[index] < 0) throw new ArgumentOutOfRangeException(nameof(weights), "Weights must be non-negative.");
                total += weights[index];
            }
            if (total <= 0 || roll < 0 || roll >= total) throw new ArgumentOutOfRangeException(nameof(roll));
            long cursor = 0;
            for (int index = 0; index < weights.Count; index++)
            {
                cursor += weights[index];
                if (roll < cursor) return index;
            }
            throw new InvalidOperationException("Weighted choice resolution did not select an interval.");
        }

        public static bool TryApply(
            ActorRuntimeIdentity actor,
            ActorProfileDefinition actorProfile,
            long loadoutSeed,
            out ActorLoadoutResult result,
            out string error)
        {
            result = null;
            error = null;
            if (actor == null || actorProfile == null || string.IsNullOrWhiteSpace(actorProfile.loadout_profile_id))
            {
                error = "Actor, actor profile and loadout_profile_id are required.";
                return false;
            }
            GameDatabase database = GameDataManager.Instance?.Database;
            ActorLoadoutProfileDefinition profile = database?.GetActorLoadoutProfile(actorProfile.loadout_profile_id);
            InventoryComponent inventory = actor.GetComponent<InventoryComponent>();
            ActorEquipmentComponent equipment = actor.GetComponent<ActorEquipmentComponent>();
            if (profile?.groups == null || inventory == null || equipment == null)
            {
                error = $"Loadout '{actorProfile.loadout_profile_id}' or required actor storage authorities are unavailable.";
                return false;
            }

            try
            {
                var inventoryEntries = new List<ActorLoadoutInventoryEntry>();
                var equipmentEntries = new List<ActorProfileInitialEquipmentEntry>();
                var selections = new List<string>();
                ActorLoadoutGroupDefinition[] orderedGroups = profile.groups
                    .Where(group => group != null)
                    .OrderBy(group => group.id, StringComparer.Ordinal)
                    .ToArray();

                for (int groupIndex = 0; groupIndex < orderedGroups.Length; groupIndex++)
                {
                    ActorLoadoutGroupDefinition group = orderedGroups[groupIndex];
                    long groupSeed = DeriveInt64("old_scars|actor_loadout_group_v1|" + loadoutSeed + "|" + profile.id + "|" + group.id);
                    var random = new StableRandom(unchecked((ulong)groupSeed));
                    ActorLoadoutChoiceDefinition choice = Select(group, ref random);
                    int choiceIndex = Array.IndexOf(group.choices, choice);
                    selections.Add(group.id + "=" + (choice.none ? "NONE" : choiceIndex.ToString()));
                    if (choice.none) continue;

                    ActorLoadoutInventoryEntry[] declaredInventory = choice.inventory ?? Array.Empty<ActorLoadoutInventoryEntry>();
                    for (int index = 0; index < declaredInventory.Length; index++)
                    {
                        ActorLoadoutInventoryEntry declared = declaredInventory[index];
                        int quantity = random.NextInclusive(declared.quantity_min, declared.quantity_max);
                        inventoryEntries.Add(new ActorLoadoutInventoryEntry
                        {
                            item_id = declared.item_id,
                            quantity_min = quantity,
                            quantity_max = quantity
                        });
                        selections.Add(group.id + ":inventory=" + declared.item_id + "x" + quantity);
                    }
                    ActorProfileInitialEquipmentEntry[] declaredEquipment = choice.equipment ?? Array.Empty<ActorProfileInitialEquipmentEntry>();
                    for (int index = 0; index < declaredEquipment.Length; index++)
                    {
                        equipmentEntries.Add(declaredEquipment[index]);
                        selections.Add(group.id + ":equipment=" + declaredEquipment[index].item_id + "@" +
                                       string.Join("+", declaredEquipment[index].slot_ids ?? Array.Empty<string>()));
                    }
                }

                for (int index = 0; index < inventoryEntries.Count; index++)
                {
                    ActorLoadoutInventoryEntry entry = inventoryEntries[index];
                    if (inventory.AddItemByDefinitionId(entry.item_id, entry.quantity_min) == null)
                        throw new InvalidOperationException($"Could not create loadout inventory item '{entry.item_id}' x{entry.quantity_min}.");
                }
                if (!EquipmentTransactionService.TryEquipInitialItems(equipment, equipmentEntries, out string equipmentError))
                    throw new InvalidOperationException(equipmentError ?? "Loadout equipment transaction failed.");

                string signature = BuildSignature(profile.id, selections);
                result = new ActorLoadoutResult(profile.id, loadoutSeed, signature, selections.ToArray());
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static ActorLoadoutChoiceDefinition Select(ActorLoadoutGroupDefinition group, ref StableRandom random)
        {
            long total = 0;
            for (int index = 0; index < group.choices.Length; index++) total += Math.Max(0, group.choices[index]?.weight ?? 0);
            long roll = random.NextInt64(total);
            int selected = ResolveWeightedChoiceIndex(
                group.choices.Select(choice => Math.Max(0, choice?.weight ?? 0)).ToArray(), roll);
            return group.choices[selected];
        }

        private static string BuildSignature(string profileId, List<string> selections)
        {
            string canonical = profileId + "|" + string.Join("|", selections);
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            var builder = new StringBuilder(hash.Length * 2);
            for (int index = 0; index < hash.Length; index++) builder.Append(hash[index].ToString("x2"));
            return builder.ToString();
        }

        private static long DeriveInt64(string canonical)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            ulong value = 0;
            for (int index = 0; index < 8; index++) value |= (ulong)hash[index] << (index * 8);
            return unchecked((long)value);
        }

        private static string Safe(string value) => value ?? "<NONE>";

        private struct StableRandom
        {
            private ulong state;
            internal StableRandom(ulong seed) { state = seed; }
            private ulong Next()
            {
                state += 0x9E3779B97F4A7C15UL;
                ulong value = state;
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                return value ^ (value >> 31);
            }
            internal long NextInt64(long exclusiveMaximum) => (long)(Next() % (ulong)exclusiveMaximum);
            internal int NextInclusive(int minimum, int maximum) => minimum + (int)(Next() % (uint)(maximum - minimum + 1));
        }
    }
}
