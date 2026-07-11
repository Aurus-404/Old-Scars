using System;
using System.Collections.Generic;
using UnityEngine;

namespace OldScars.Core.Items
{
    public static class InventoryIconResolver
    {
        public const string ResourcePrefix = "OldScars/InventoryIcons/";

        private static readonly Dictionary<string, Sprite> ResolvedIcons = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> MissingIcons = new HashSet<string>();

        public static bool VerboseMissingIconLogging { get; set; }

        public static bool TryResolve(string iconId, out Sprite sprite)
        {
            sprite = null;
            string normalizedIconId = NormalizeIconId(iconId);
            if (normalizedIconId == null)
                return false;

            if (ResolvedIcons.TryGetValue(normalizedIconId, out sprite))
                return sprite != null;

            if (MissingIcons.Contains(normalizedIconId))
                return false;

            try
            {
                sprite = Resources.Load<Sprite>(ResourcePrefix + normalizedIconId);
            }
            catch (Exception exception)
            {
                RememberMissing(normalizedIconId, $"{exception.GetType().Name}: {exception.Message}");
                return false;
            }

            if (sprite == null)
            {
                RememberMissing(normalizedIconId, "sprite was not found");
                return false;
            }

            ResolvedIcons[normalizedIconId] = sprite;
            return true;
        }

        private static void RememberMissing(string iconId, string reason)
        {
            if (!MissingIcons.Add(iconId) || !VerboseMissingIconLogging)
                return;

            Debug.LogWarning(
                $"[InventoryIconResolver] Optional inventory icon '{iconId}' could not be resolved at " +
                $"Resources/{ResourcePrefix}{iconId}: {reason}. The inventory fallback will be used.");
        }

        private static string NormalizeIconId(string iconId)
        {
            return string.IsNullOrWhiteSpace(iconId) ? null : iconId.Trim();
        }
    }
}
