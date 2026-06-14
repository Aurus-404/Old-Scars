using System;
using System.Collections.Generic;
using UnityEngine;

namespace OldScars.Core.Visuals
{
    /// <summary>
    /// Resolves curated visual-only prefabs without exposing imported assets to gameplay code.
    /// </summary>
    public static class WorldVisualPrefabRegistry
    {
        public const string ResourcePrefix = "OldScarsVisuals/";
        private static readonly HashSet<string> LoggedVisualIds = new HashSet<string>();
        private static readonly HashSet<string> WarnedVisualIds = new HashSet<string>();

        public static bool TryCreate(
            Transform parent,
            string visualPrefabId,
            out Renderer[] renderers)
        {
            renderers = null;

            if (parent == null || string.IsNullOrWhiteSpace(visualPrefabId))
                return false;

            LogLoadOnce(visualPrefabId);

            GameObject prefab;
            try
            {
                prefab = Resources.Load<GameObject>(ResourcePrefix + visualPrefabId);
            }
            catch (Exception exception)
            {
                WarnOnce(visualPrefabId, $"Could not load visual prefab id={visualPrefabId}: {exception.GetType().Name}: {exception.Message}; using fallback.");
                return false;
            }

            if (prefab == null)
            {
                WarnOnce(visualPrefabId, $"Missing visual prefab id={visualPrefabId}, using fallback.");
                return false;
            }

            GameObject instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(prefab, parent, false);
                if (instance == null)
                {
                    WarnOnce(visualPrefabId, $"Visual prefab id={visualPrefabId} did not instantiate; using fallback.");
                    return false;
                }

                instance.name = "Visual Model";
                SetLayerRecursively(instance, parent.gameObject.layer);

                renderers = instance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                    return true;
            }
            catch (Exception exception)
            {
                DestroyFailedInstance(instance);
                renderers = null;
                WarnOnce(visualPrefabId, $"Could not use visual prefab id={visualPrefabId}: {exception.GetType().Name}: {exception.Message}; using fallback.");
                return false;
            }

            WarnOnce(visualPrefabId, $"Visual prefab id={visualPrefabId} produced no Renderers; using fallback.");
            DestroyFailedInstance(instance);
            renderers = null;
            return false;
        }

        private static void LogLoadOnce(string visualPrefabId)
        {
            if (LoggedVisualIds.Add(visualPrefabId))
                Debug.Log($"[WorldVisualPrefabRegistry] Loading visual prefab id={visualPrefabId}.");
        }

        private static void WarnOnce(string visualPrefabId, string message)
        {
            if (!WarnedVisualIds.Add(visualPrefabId))
                return;

            Debug.LogWarning($"[WorldVisualPrefabRegistry] {message}");
        }

        private static void DestroyFailedInstance(GameObject instance)
        {
            if (instance == null)
                return;

            instance.SetActive(false);
            UnityEngine.Object.Destroy(instance);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null)
                return;

            root.layer = layer;
            for (int index = 0; index < root.transform.childCount; index++)
                SetLayerRecursively(root.transform.GetChild(index).gameObject, layer);
        }
    }
}
