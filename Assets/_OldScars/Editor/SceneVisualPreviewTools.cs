using System.Collections.Generic;
using OldScars.Core.Items;
using OldScars.Core.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldScars.EditorTools
{
    public static class SceneVisualPreviewTools
    {
        private const string ApplyMenuPath = "Old Scars/Visuals/Apply Scene Visual Preview";
        private const string ClearMenuPath = "Old Scars/Visuals/Clear Scene Visual Preview";

        private static void ApplySceneVisualPreview()
        {
            if (!TryGetEditableActiveScene(out Scene activeScene))
                return;

            int appliedCount = 0;
            int skippedCount = 0;
            List<GameObject> roots = SceneVisualEditorUtility.GetRelevantRoots(activeScene);

            for (int index = 0; index < roots.Count; index++)
            {
                GameObject root = roots[index];
                string visualPrefabId = SceneVisualEditorUtility.GetVisualPrefabId(root);
                if (string.IsNullOrWhiteSpace(visualPrefabId))
                {
                    skippedCount++;
                    continue;
                }

                Transform existingVisual = root.transform.Find(WorldObjectVisualBinder.VisualRootName);
                SceneVisualPreviewMarker existingMarker = existingVisual != null
                    ? existingVisual.GetComponent<SceneVisualPreviewMarker>()
                    : null;

                if (existingMarker != null)
                {
                    ClearPreview(existingMarker);
                    existingVisual = null;
                }
                else if (existingVisual != null)
                {
                    Debug.LogWarning(
                        $"[SceneVisualPreviewTools] Skipped {root.name}: existing Visual is not an editor preview.");
                    skippedCount++;
                    continue;
                }

                GameObject prefab = Resources.Load<GameObject>(WorldVisualPrefabRegistry.ResourcePrefix + visualPrefabId);
                if (prefab == null)
                {
                    Debug.LogWarning(
                        $"[SceneVisualPreviewTools] Missing visual prefab id={visualPrefabId} for {root.name}.");
                    skippedCount++;
                    continue;
                }

                Renderer[] placeholderRenderers = root.GetComponentsInChildren<Renderer>(true);
                var visualRoot = new GameObject(WorldObjectVisualBinder.VisualRootName);
                visualRoot.layer = root.layer;
                visualRoot.transform.SetParent(root.transform, false);
                Undo.RegisterCreatedObjectUndo(visualRoot, "Apply Scene Visual Preview");

                SceneVisualPreviewMarker marker = Undo.AddComponent<SceneVisualPreviewMarker>(visualRoot);
                Undo.RecordObject(marker, "Capture Visual Placeholder State");
                marker.CapturePlaceholderStates(placeholderRenderers);
                EditorUtility.SetDirty(marker);

                GameObject visualInstance = PrefabUtility.InstantiatePrefab(prefab, visualRoot.transform) as GameObject;
                if (visualInstance == null || visualInstance.GetComponentInChildren<Renderer>(true) == null)
                {
                    Debug.LogWarning(
                        $"[SceneVisualPreviewTools] Visual prefab id={visualPrefabId} has no usable Renderer for {root.name}.");
                    Undo.DestroyObjectImmediate(visualRoot);
                    skippedCount++;
                    continue;
                }

                Undo.RegisterCreatedObjectUndo(visualInstance, "Apply Scene Visual Preview");
                visualInstance.name = "Visual Model";
                SceneVisualEditorUtility.SetLayerRecursively(visualInstance, root.layer);

                for (int rendererIndex = 0; rendererIndex < placeholderRenderers.Length; rendererIndex++)
                {
                    Renderer placeholder = placeholderRenderers[rendererIndex];
                    if (placeholder == null)
                        continue;

                    Undo.RecordObject(placeholder, "Disable Visual Placeholder");
                    placeholder.enabled = false;
                }

                appliedCount++;
            }

            if (appliedCount > 0)
                EditorSceneManager.MarkSceneDirty(activeScene);

            Debug.Log(
                $"[SceneVisualPreviewTools] Applied {appliedCount} scene visual previews; skipped {skippedCount}.");
        }

        private static void ClearSceneVisualPreview()
        {
            if (!TryGetEditableActiveScene(out Scene activeScene))
                return;

            SceneVisualPreviewMarker[] markers = Resources.FindObjectsOfTypeAll<SceneVisualPreviewMarker>();
            int clearedCount = 0;

            for (int index = 0; index < markers.Length; index++)
            {
                SceneVisualPreviewMarker marker = markers[index];
                if (marker == null || marker.gameObject.scene != activeScene)
                    continue;

                ClearPreview(marker);
                clearedCount++;
            }

            if (clearedCount > 0)
                EditorSceneManager.MarkSceneDirty(activeScene);

            Debug.Log($"[SceneVisualPreviewTools] Cleared {clearedCount} scene visual previews.");
        }

        private static void ClearPreview(SceneVisualPreviewMarker marker)
        {
            if (marker == null)
                return;

            Transform parent = marker.transform.parent;
            if (parent != null)
            {
                Renderer[] renderers = parent.GetComponentsInChildren<Renderer>(true);
                Undo.RecordObjects(renderers, "Restore Visual Placeholders");
            }

            marker.RestorePlaceholderStates();
            Undo.DestroyObjectImmediate(marker.gameObject);
        }

        private static bool TryGetEditableActiveScene(out Scene activeScene)
        {
            activeScene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[SceneVisualPreviewTools] Scene visual previews cannot be edited in Play Mode.");
                return false;
            }

            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                Debug.LogWarning("[SceneVisualPreviewTools] No editable active scene was found.");
                return false;
            }

            return true;
        }
    }

    internal static class SceneVisualEditorUtility
    {
        public static List<GameObject> GetRelevantRoots(Scene scene)
        {
            var roots = new HashSet<GameObject>();

            WorldItemPickup[] pickups = Resources.FindObjectsOfTypeAll<WorldItemPickup>();
            for (int index = 0; index < pickups.Length; index++)
            {
                WorldItemPickup pickup = pickups[index];
                if (pickup != null && pickup.gameObject.scene == scene)
                    roots.Add(pickup.gameObject);
            }

            WorldObjectVisualBinder[] binders = Resources.FindObjectsOfTypeAll<WorldObjectVisualBinder>();
            for (int index = 0; index < binders.Length; index++)
            {
                WorldObjectVisualBinder binder = binders[index];
                if (binder != null && binder.gameObject.scene == scene)
                    roots.Add(binder.gameObject);
            }

            return new List<GameObject>(roots);
        }

        public static string GetVisualPrefabId(GameObject root)
        {
            if (root == null)
                return null;

            WorldObjectVisualBinder binder = root.GetComponent<WorldObjectVisualBinder>();
            if (binder != null && !string.IsNullOrWhiteSpace(binder.VisualPrefabId))
                return binder.VisualPrefabId;

            WorldItemPickup pickup = root.GetComponent<WorldItemPickup>();
            return pickup != null ? WorldItemVisualResolver.GetVisualPrefabId(pickup.ItemDefinitionId) : null;
        }

        public static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null)
                return;

            root.layer = layer;
            for (int index = 0; index < root.transform.childCount; index++)
                SetLayerRecursively(root.transform.GetChild(index).gameObject, layer);
        }
    }
}
