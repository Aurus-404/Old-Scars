using System.Collections.Generic;
using UnityEngine;

namespace OldScars.Core.Feedback
{
    public sealed class GameplayFeedbackLog : MonoBehaviour
    {
        [SerializeField] private int maxEntries = 50;

        private static GameplayFeedbackLog activeLog;
        private readonly List<GameplayFeedbackEntry> entries = new List<GameplayFeedbackEntry>();

        public IReadOnlyList<GameplayFeedbackEntry> Entries => entries;

        public static bool TryRecord(GameplayFeedbackEntry entry)
        {
            if (activeLog == null || entry == null)
                return false;

            activeLog.Record(entry);
            return true;
        }

        public void Record(GameplayFeedbackEntry entry)
        {
            if (entry == null)
                return;

            entries.Add(entry);
            TrimToMaxEntries();
        }

        public void Clear()
        {
            entries.Clear();
        }

        private void OnEnable()
        {
            activeLog = this;
            TrimToMaxEntries();
        }

        private void OnDisable()
        {
            if (activeLog == this)
                activeLog = null;
        }

        private void OnValidate()
        {
            maxEntries = Mathf.Max(1, maxEntries);
            TrimToMaxEntries();
        }

        private void TrimToMaxEntries()
        {
            int clampedMaxEntries = Mathf.Max(1, maxEntries);
            int overflow = entries.Count - clampedMaxEntries;
            if (overflow <= 0)
                return;

            entries.RemoveRange(0, overflow);
        }
    }
}
