using System.Collections.Generic;
using System.Text.RegularExpressions;
using OldScars.Core.Data.Definitions;

namespace OldScars.Core.Data.Loading
{
    /// <summary>
    /// Central registry of valid tags.
    ///
    /// All tags used by item/action JSON must be declared in tags.json. This
    /// prevents silent typos such as "metal", "Metal" and "metall" becoming
    /// three different concepts.
    /// </summary>
    public sealed class TagRegistry
    {
        private static readonly Regex SnakeCasePattern = new Regex("^[a-z0-9_]+$", RegexOptions.Compiled);
        private readonly HashSet<string> validTags = new HashSet<string>();

        public int Count => validTags.Count;

        public void Register(TagDefinition tag, DataLoadReport report)
        {
            if (tag == null)
            {
                report.Error("Tag: tried to register a null tag definition.");
                return;
            }

            if (string.IsNullOrWhiteSpace(tag.id))
            {
                report.Error("Tag: 'id' is required.");
                return;
            }

            if (!SnakeCasePattern.IsMatch(tag.id))
            {
                report.Error($"Tag '{tag.id}': id must use snake_case only: lowercase letters, digits and underscores.");
                return;
            }

            if (validTags.Contains(tag.id))
            {
                report.Error($"Duplicate tag id '{tag.id}'.");
                return;
            }

            validTags.Add(tag.id);
        }

        public bool IsValid(string tagId)
        {
            return !string.IsNullOrWhiteSpace(tagId) && validTags.Contains(tagId);
        }

        public List<string> FindInvalidTags(IEnumerable<string> tags)
        {
            var invalid = new List<string>();
            if (tags == null)
                return invalid;

            foreach (string tag in tags)
            {
                if (!IsValid(tag))
                    invalid.Add(tag);
            }

            return invalid;
        }
    }
}
