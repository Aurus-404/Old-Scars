using System.Collections.Generic;
using UnityEngine;

namespace OldScars.Core.Interactions
{
    public sealed class DebugPlayerContext : MonoBehaviour
    {
        private const string NoEquippedItemId = "none";

        [SerializeField] private string[] actorTags = { "player", "human" };
        [SerializeField] private DebugActorStat[] actorStats = { new DebugActorStat("strength", 4f) };
        [SerializeField] private string debugEquippedItemId = "rusted_crowbar_01";

        public string[] ActorTags => actorTags;
        public string DebugEquippedItemId => debugEquippedItemId;

        public bool HasEquippedItem()
        {
            return !IsNoEquippedItemId(debugEquippedItemId);
        }

        public static bool IsNoEquippedItemId(string itemId)
        {
            return string.IsNullOrWhiteSpace(itemId) || itemId.Trim().ToLowerInvariant() == NoEquippedItemId;
        }

        public Dictionary<string, float> BuildActorStatsDictionary()
        {
            var result = new Dictionary<string, float>();

            if (actorStats == null)
                return result;

            for (int index = 0; index < actorStats.Length; index++)
            {
                DebugActorStat stat = actorStats[index];
                if (stat == null || string.IsNullOrWhiteSpace(stat.id))
                    continue;

                result[stat.id] = stat.value;
            }

            return result;
        }
    }

    [System.Serializable]
    public sealed class DebugActorStat
    {
        public string id;
        public float value;

        public DebugActorStat()
        {
        }

        public DebugActorStat(string id, float value)
        {
            this.id = id;
            this.value = value;
        }
    }
}
