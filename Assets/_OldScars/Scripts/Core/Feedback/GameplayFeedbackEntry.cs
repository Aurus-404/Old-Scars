using UnityEngine;

namespace OldScars.Core.Feedback
{
    public sealed class GameplayFeedbackEntry
    {
        public GameplayFeedbackEntry(
            GameplayFeedbackEntryType type,
            string fallbackMessage,
            float time = -1f,
            string actorId = null,
            string actorDisplayName = null,
            string targetId = null,
            string targetDisplayName = null,
            string itemId = null,
            string itemDisplayName = null,
            string actionId = null,
            string actionDisplayName = null,
            int quantity = 0,
            string[] addedTags = null,
            string[] removedTags = null,
            bool debugOnly = false)
        {
            this.type = type;
            this.fallbackMessage = fallbackMessage;
            this.time = time >= 0f ? time : Time.time;
            this.actorId = actorId;
            this.actorDisplayName = actorDisplayName;
            this.targetId = targetId;
            this.targetDisplayName = targetDisplayName;
            this.itemId = itemId;
            this.itemDisplayName = itemDisplayName;
            this.actionId = actionId;
            this.actionDisplayName = actionDisplayName;
            this.quantity = quantity;
            this.addedTags = CloneTags(addedTags);
            this.removedTags = CloneTags(removedTags);
            this.debugOnly = debugOnly;
        }

        public readonly GameplayFeedbackEntryType type;
        public readonly string fallbackMessage;
        public readonly float time;
        public readonly string actorId;
        public readonly string actorDisplayName;
        public readonly string targetId;
        public readonly string targetDisplayName;
        public readonly string itemId;
        public readonly string itemDisplayName;
        public readonly string actionId;
        public readonly string actionDisplayName;
        public readonly int quantity;
        public readonly string[] addedTags;
        public readonly string[] removedTags;
        public readonly bool debugOnly;

        private static string[] CloneTags(string[] tags)
        {
            if (tags == null || tags.Length == 0)
                return new string[0];

            string[] copy = new string[tags.Length];
            tags.CopyTo(copy, 0);
            return copy;
        }
    }
}
