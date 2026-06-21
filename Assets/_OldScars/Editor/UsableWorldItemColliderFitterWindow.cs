using System.Collections.Generic;
using OldScars.Core.Items;
using OldScars.Core.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OldScars.EditorTools
{
    public sealed class UsableWorldItemColliderFitterWindow : EditorWindow
    {
        private static readonly string[] M30WorldItemPrefabPaths =
        {
            "Assets/_OldScars/Resources/PFB_WorldItem_lee_enfield_rifle_01.prefab",
            "Assets/_OldScars/Resources/PFB_WorldItem_rusted_crowbar_01.prefab",
            "Assets/_OldScars/Resources/PFB_WorldItem_ammo_303_british_01.prefab"
        };

        private static readonly Vector3 DefaultPaddingPerSide = new Vector3(0.03f, 0.03f, 0.03f);
        private static readonly Vector3 DefaultMinimumSize = new Vector3(0.12f, 0.12f, 0.12f);

        private Vector3 paddingPerSide = DefaultPaddingPerSide;
        private Vector3 minimumSize = DefaultMinimumSize;

        [MenuItem("Old Scars/World Items/Fit Usable Item Colliders")]
        private static void Open()
        {
            GetWindow<UsableWorldItemColliderFitterWindow>("Fit World Item Colliders");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(
                "Fits root BoxColliders to every Renderer under the authored Visual child. Only WorldItemPickup roots are accepted.",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();
            paddingPerSide = EditorGUILayout.Vector3Field("Padding Per Side", paddingPerSide);
            minimumSize = EditorGUILayout.Vector3Field("Minimum Size", minimumSize);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Fit M30.3 World Item Prefabs"))
                    FitKnownPrefabAssets();

                if (GUILayout.Button("Fit Selected World Items / Prefab Assets"))
                    FitSelection();
            }
        }

        private void FitKnownPrefabAssets()
        {
            Vector3 safePadding = ClampNonNegative(paddingPerSide);
            Vector3 safeMinimumSize = ClampNonNegative(minimumSize);
            int fittedCount = 0;

            for (int index = 0; index < M30WorldItemPrefabPaths.Length; index++)
            {
                if (FitPrefabAsset(M30WorldItemPrefabPaths[index], safePadding, safeMinimumSize))
                    fittedCount++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[UsableWorldItemColliderFitterWindow] Fitted {fittedCount}/{M30WorldItemPrefabPaths.Length} M30.3 prefab colliders." +
                $" Padding per side={safePadding}; minimum size={safeMinimumSize}.");
        }

        private void FitSelection()
        {
            Vector3 safePadding = ClampNonNegative(paddingPerSide);
            Vector3 safeMinimumSize = ClampNonNegative(minimumSize);
            var prefabPaths = new HashSet<string>();
            var sceneRoots = new HashSet<GameObject>();

            Object[] selectedObjects = Selection.objects;
            for (int index = 0; index < selectedObjects.Length; index++)
            {
                Object selected = selectedObjects[index];
                string assetPath = AssetDatabase.GetAssetPath(selected);
                if (!string.IsNullOrWhiteSpace(assetPath) && assetPath.EndsWith(".prefab"))
                {
                    prefabPaths.Add(assetPath);
                    continue;
                }

                if (selected is GameObject selectedGameObject)
                {
                    WorldItemPickup pickup = selectedGameObject.GetComponentInParent<WorldItemPickup>();
                    if (pickup != null && pickup.gameObject.scene.IsValid())
                        sceneRoots.Add(pickup.gameObject);
                }
            }

            int fittedPrefabCount = 0;
            foreach (string prefabPath in prefabPaths)
            {
                if (FitPrefabAsset(prefabPath, safePadding, safeMinimumSize))
                    fittedPrefabCount++;
            }

            int fittedSceneCount = 0;
            foreach (GameObject root in sceneRoots)
            {
                if (!TryFitRoot(root, safePadding, safeMinimumSize, true, out _))
                    continue;

                EditorSceneManager.MarkSceneDirty(root.scene);
                fittedSceneCount++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[UsableWorldItemColliderFitterWindow] Fitted {fittedPrefabCount} prefab asset(s) and {fittedSceneCount} scene item(s)." +
                $" Padding per side={safePadding}; minimum size={safeMinimumSize}.");
        }

        private static bool FitPrefabAsset(string prefabPath, Vector3 padding, Vector3 minimumColliderSize)
        {
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null)
            {
                Debug.LogWarning($"[UsableWorldItemColliderFitterWindow] Prefab was not found: {prefabPath}");
                return false;
            }

            if (prefabAsset.GetComponent<WorldItemPickup>() == null)
            {
                Debug.LogWarning($"[UsableWorldItemColliderFitterWindow] Skipped non-world-item prefab: {prefabPath}");
                return false;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (!TryFitRoot(prefabRoot, padding, minimumColliderSize, false, out string error))
                {
                    Debug.LogWarning($"[UsableWorldItemColliderFitterWindow] Skipped {prefabPath}: {error}");
                    return false;
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static bool TryFitRoot(
            GameObject root,
            Vector3 padding,
            Vector3 minimumColliderSize,
            bool recordUndo,
            out string error)
        {
            error = null;
            if (root == null || root.GetComponent<WorldItemPickup>() == null)
            {
                error = "root requires WorldItemPickup";
                return false;
            }

            Transform visual = root.transform.Find(WorldObjectVisualBinder.VisualRootName);
            if (visual == null)
            {
                error = "authored Visual child was not found";
                return false;
            }

            BoxCollider[] colliders = root.GetComponents<BoxCollider>();
            if (colliders.Length != 1)
            {
                error = "root requires exactly one BoxCollider";
                return false;
            }

            if (!TryGetVisualBoundsInRootSpace(root.transform, visual, out Bounds visualBounds))
            {
                error = "Visual has no Renderer bounds";
                return false;
            }

            BoxCollider collider = colliders[0];
            if (recordUndo)
                Undo.RecordObject(collider, "Fit Usable World Item Collider");

            collider.center = visualBounds.center;
            collider.size = MaxPerAxis(visualBounds.size + padding * 2f, minimumColliderSize);
            EditorUtility.SetDirty(collider);
            return true;
        }

        private static bool TryGetVisualBoundsInRootSpace(Transform root, Transform visual, out Bounds bounds)
        {
            bounds = default;
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            bool hasPoint = false;

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null)
                    continue;

                Bounds rendererLocalBounds = GetRendererLocalBounds(renderer);
                Vector3 center = rendererLocalBounds.center;
                Vector3 extents = rendererLocalBounds.extents;

                for (int cornerIndex = 0; cornerIndex < 8; cornerIndex++)
                {
                    Vector3 rendererLocalCorner = center + new Vector3(
                        (cornerIndex & 1) == 0 ? -extents.x : extents.x,
                        (cornerIndex & 2) == 0 ? -extents.y : extents.y,
                        (cornerIndex & 4) == 0 ? -extents.z : extents.z);
                    Vector3 rootLocalPoint = root.InverseTransformPoint(
                        renderer.transform.TransformPoint(rendererLocalCorner));

                    if (!hasPoint)
                    {
                        bounds = new Bounds(rootLocalPoint, Vector3.zero);
                        hasPoint = true;
                    }
                    else
                    {
                        bounds.Encapsulate(rootLocalPoint);
                    }
                }
            }

            return hasPoint;
        }

        private static Bounds GetRendererLocalBounds(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
                return skinnedMeshRenderer.localBounds;

            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
                return meshFilter.sharedMesh.bounds;

            return renderer.localBounds;
        }

        private static Vector3 ClampNonNegative(Vector3 value)
        {
            return new Vector3(
                Mathf.Max(0f, value.x),
                Mathf.Max(0f, value.y),
                Mathf.Max(0f, value.z));
        }

        private static Vector3 MaxPerAxis(Vector3 left, Vector3 right)
        {
            return new Vector3(
                Mathf.Max(left.x, right.x),
                Mathf.Max(left.y, right.y),
                Mathf.Max(left.z, right.z));
        }
    }
}
