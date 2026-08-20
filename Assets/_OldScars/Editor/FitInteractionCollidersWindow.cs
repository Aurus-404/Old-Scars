using System.Collections.Generic;
using OldScars.Core.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldScars.EditorTools
{
    public sealed class FitInteractionCollidersWindow : EditorWindow
    {
        private static readonly Vector3 DefaultPadding = new Vector3(0.08f, 0.05f, 0.08f);

        private Vector3 padding = DefaultPadding;

        private static void Open()
        {
            GetWindow<FitInteractionCollidersWindow>("Fit Visual Colliders");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Fit root BoxColliders to real child Visual bounds.", EditorStyles.wordWrappedLabel);
            padding = EditorGUILayout.Vector3Field("Padding Per Side", padding);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Fit Active Scene"))
                    FitActiveScene();
            }
        }

        private void FitActiveScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                Debug.LogWarning("[FitInteractionCollidersWindow] No editable active scene was found.");
                return;
            }

            Vector3 safePadding = new Vector3(
                Mathf.Max(0f, padding.x),
                Mathf.Max(0f, padding.y),
                Mathf.Max(0f, padding.z));
            List<GameObject> roots = SceneVisualEditorUtility.GetRelevantRoots(activeScene);
            int fittedCount = 0;
            int skippedCount = 0;

            for (int index = 0; index < roots.Count; index++)
            {
                GameObject root = roots[index];
                Transform visual = root.transform.Find(WorldObjectVisualBinder.VisualRootName);
                BoxCollider[] colliders = root.GetComponents<BoxCollider>();

                if (visual == null || colliders.Length != 1 || !TryGetLocalVisualBounds(root.transform, visual, out Bounds bounds))
                {
                    Debug.LogWarning(
                        $"[FitInteractionCollidersWindow] Skipped {root.name}: requires one root BoxCollider and a real Visual with Renderer bounds.");
                    skippedCount++;
                    continue;
                }

                BoxCollider collider = colliders[0];
                Undo.RecordObject(collider, "Fit Interaction Collider To Visual");
                collider.center = bounds.center;
                collider.size = bounds.size + safePadding * 2f;
                EditorUtility.SetDirty(collider);
                fittedCount++;
            }

            if (fittedCount > 0)
                EditorSceneManager.MarkSceneDirty(activeScene);

            Debug.Log(
                $"[FitInteractionCollidersWindow] Fitted {fittedCount} interaction colliders; skipped {skippedCount}; padding={safePadding}.");
        }

        private static bool TryGetLocalVisualBounds(Transform root, Transform visual, out Bounds bounds)
        {
            bounds = default;
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            bool hasPoint = false;

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null)
                    continue;

                Bounds worldBounds = renderer.bounds;
                Vector3 min = worldBounds.min;
                Vector3 max = worldBounds.max;

                for (int cornerIndex = 0; cornerIndex < 8; cornerIndex++)
                {
                    Vector3 worldCorner = new Vector3(
                        (cornerIndex & 1) == 0 ? min.x : max.x,
                        (cornerIndex & 2) == 0 ? min.y : max.y,
                        (cornerIndex & 4) == 0 ? min.z : max.z);
                    Vector3 localPoint = root.InverseTransformPoint(worldCorner);

                    if (!hasPoint)
                    {
                        bounds = new Bounds(localPoint, Vector3.zero);
                        hasPoint = true;
                    }
                    else
                    {
                        bounds.Encapsulate(localPoint);
                    }
                }
            }

            return hasPoint;
        }
    }
}
