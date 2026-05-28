using System.Collections.Generic;
using UnityEngine;

namespace OldScars.Core.Interactions
{
    public sealed class WorldObjectTags : MonoBehaviour
    {
        [Header("Initial Tags")]
        [Tooltip("Tags configured in the Inspector. They are copied into runtime tags on Awake; gameplay mutations never modify this serialized array.")]
        [SerializeField] private string[] tags;

        private readonly List<string> runtimeTags = new List<string>();
        private bool runtimeTagsInitialized;

        public string[] InitialTags => GetInitialTags();
        public string[] RuntimeTags => GetRuntimeTags();
        public string[] Tags => RuntimeTags;

        private void Awake()
        {
            InitializeRuntimeTags();
        }

        public bool HasTag(string tag)
        {
            EnsureRuntimeTags();

            if (string.IsNullOrWhiteSpace(tag))
                return false;

            return runtimeTags.Contains(tag);
        }

        public bool AddTag(string tag)
        {
            EnsureRuntimeTags();

            if (string.IsNullOrWhiteSpace(tag))
                return false;

            if (runtimeTags.Contains(tag))
                return false;

            runtimeTags.Add(tag);
            return true;
        }

        public bool RemoveTag(string tag)
        {
            EnsureRuntimeTags();

            if (string.IsNullOrWhiteSpace(tag))
                return false;

            return runtimeTags.Remove(tag);
        }

        public void ResetRuntimeTagsFromInitial()
        {
            InitializeRuntimeTags();
        }

        public string[] GetTags()
        {
            return GetRuntimeTags();
        }

        public string[] GetRuntimeTags()
        {
            EnsureRuntimeTags();
            return runtimeTags.ToArray();
        }

        public string[] GetInitialTags()
        {
            if (tags == null || tags.Length == 0)
                return new string[0];

            string[] initialTagsCopy = new string[tags.Length];
            tags.CopyTo(initialTagsCopy, 0);
            return initialTagsCopy;
        }

        private void EnsureRuntimeTags()
        {
            if (!runtimeTagsInitialized)
                InitializeRuntimeTags();
        }

        private void InitializeRuntimeTags()
        {
            runtimeTags.Clear();

            if (tags != null)
            {
                for (int index = 0; index < tags.Length; index++)
                {
                    string tag = tags[index];
                    if (!string.IsNullOrWhiteSpace(tag) && !runtimeTags.Contains(tag))
                        runtimeTags.Add(tag);
                }
            }

            runtimeTagsInitialized = true;
        }
    }
}
