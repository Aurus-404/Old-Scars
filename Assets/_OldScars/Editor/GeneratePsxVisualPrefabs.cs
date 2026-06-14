using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldScars.EditorTools
{
    public static class GeneratePsxVisualPrefabs
    {
        private const string MenuPath = "Old Scars/Visuals/Generate PSX Visual Prefabs";

        private static readonly VisualPrefabSpec[] Specs =
        {
            new VisualPrefabSpec(
                "PFB_VIS_Rusted_Crowbar_PSX",
                "Assets/_OldScars/Art/External/Sketchfab/Survival_PSX/Source/Survival/Models/Survival.fbx",
                "Axe", // TEMP visual placeholder; gameplay remains rusted_crowbar_01.
                "Assets/_OldScars/Art/External/Sketchfab/Crowbar_PSX_LowPoly/Prefabs/Resources/OldScarsVisuals/PFB_VIS_Rusted_Crowbar_PSX.prefab",
                0.90f,
                0.04f),
            new VisualPrefabSpec(
                "PFB_VIS_Lee_Enfield_PSX",
                "Assets/_OldScars/Art/External/Sketchfab/Survival_PSX/Source/Survival/Models/Survival.fbx",
                "hunting_rifle",
                "Assets/_OldScars/Art/External/Sketchfab/Survival_PSX/Prefabs/Resources/OldScarsVisuals/PFB_VIS_Lee_Enfield_PSX.prefab",
                1.20f,
                0.04f),
            new VisualPrefabSpec(
                "PFB_VIS_Ammo_303_PSX",
                "Assets/_OldScars/Art/External/Sketchfab/Survival_PSX/Source/Survival/Models/Survival.fbx",
                "Ammunition",
                "Assets/_OldScars/Art/External/Sketchfab/Survival_PSX/Prefabs/Resources/OldScarsVisuals/PFB_VIS_Ammo_303_PSX.prefab",
                0.30f,
                0.05f),
            new VisualPrefabSpec(
                "PFB_VIS_Crate_PSX",
                "Assets/_OldScars/Art/External/Sketchfab/PSX_Industrial_Pack/Source/industrial.fbx",
                "Crate",
                "Assets/_OldScars/Art/External/Sketchfab/PSX_Industrial_Pack/Prefabs/Resources/OldScarsVisuals/PFB_VIS_Crate_PSX.prefab",
                1.20f,
                0f),
            new VisualPrefabSpec(
                "PFB_VIS_Crate_Wood_PSX",
                "Assets/_OldScars/Art/External/Sketchfab/PSX_Industrial_Pack/Source/industrial.fbx",
                "Crate",
                "Assets/_OldScars/Art/External/Sketchfab/PSX_Industrial_Pack/Prefabs/Resources/OldScarsVisuals/PFB_VIS_Crate_Wood_PSX.prefab",
                1.20f,
                0f),
            new VisualPrefabSpec(
                "PFB_VIS_Crate_Metal_PSX",
                "Assets/_OldScars/Art/External/Sketchfab/PSX_Industrial_Pack/Source/industrial.fbx",
                "Crate",
                "Assets/_OldScars/Art/External/Sketchfab/PSX_Industrial_Pack/Prefabs/Resources/OldScarsVisuals/PFB_VIS_Crate_Metal_PSX.prefab",
                1.20f,
                0f)
        };

        [MenuItem(MenuPath)]
        private static void GenerateAll()
        {
            var failures = new List<string>();

            for (int index = 0; index < Specs.Length; index++)
            {
                VisualPrefabSpec spec = Specs[index];
                try
                {
                    Generate(spec);
                    Debug.Log($"[GeneratePsxVisualPrefabs] Generated {spec.PrefabName}.");
                }
                catch (Exception exception)
                {
                    failures.Add($"{spec.PrefabName}: {exception.Message}");
                    Debug.LogError($"[GeneratePsxVisualPrefabs] Failed {spec.PrefabName}: {exception}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (failures.Count > 0)
            {
                Debug.LogWarning(
                    $"[GeneratePsxVisualPrefabs] Generated {Specs.Length - failures.Count}/{Specs.Length} prefabs. Failures: {string.Join(" | ", failures)}");
                return;
            }

            Debug.Log($"[GeneratePsxVisualPrefabs] Generated all {Specs.Length} real visual prefabs.");
        }

        private static void Generate(VisualPrefabSpec spec)
        {
            GameObject sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(spec.SourceAssetPath);
            if (sourceAsset == null)
                throw new InvalidOperationException($"Source model not found or not a GameObject: {spec.SourceAssetPath}");

            Scene previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject sourceInstance = PrefabUtility.InstantiatePrefab(sourceAsset, previewScene) as GameObject;
                if (sourceInstance == null)
                    throw new InvalidOperationException($"Could not instantiate source model: {spec.SourceAssetPath}");

                GameObject prefabRoot = new GameObject(spec.PrefabName);
                SceneManager.MoveGameObjectToScene(prefabRoot, previewScene);

                Transform sourceChild = FindDescendant(sourceInstance.transform, spec.SourceChildName);
                if (sourceChild == null)
                    throw new InvalidOperationException($"Source child not found: {spec.SourceChildName}");

                if (sourceChild.GetComponentInChildren<Renderer>(true) == null)
                    throw new InvalidOperationException($"Source child has no Renderer: {spec.SourceChildName}");

                GameObject model = UnityEngine.Object.Instantiate(sourceChild.gameObject);
                model.name = "Model";
                model.transform.SetParent(prefabRoot.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = sourceChild.rotation;
                model.transform.localScale = sourceChild.lossyScale;
                model.SetActive(true);

                UnpackIfNeeded(model);
                StripImportedRuntimeComponents(model);
                NormalizeVisual(prefabRoot.transform, model.transform, spec.TargetMaxDimension, spec.GroundClearance);

                if (prefabRoot.GetComponentInChildren<Renderer>(true) == null)
                    throw new InvalidOperationException($"Generated prefab has no Renderer: {spec.PrefabName}");

                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, spec.OutputPath);
                if (savedPrefab == null)
                    throw new InvalidOperationException($"PrefabUtility could not save: {spec.OutputPath}");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static void NormalizeVisual(
            Transform prefabRoot,
            Transform model,
            float targetMaxDimension,
            float groundClearance)
        {
            if (targetMaxDimension <= 0.0001f)
                throw new InvalidOperationException($"Target dimension must be positive: {targetMaxDimension}");

            if (!TryGetCombinedBounds(prefabRoot, out Bounds bounds))
                throw new InvalidOperationException($"Generated model has no Renderer bounds: {model.name}");

            float maxDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxDimension <= 0.0001f)
                throw new InvalidOperationException($"Generated model has invalid Renderer bounds: {model.name}");

            model.localScale *= targetMaxDimension / maxDimension;

            if (!TryGetCombinedBounds(prefabRoot, out bounds))
                throw new InvalidOperationException($"Normalized model has no Renderer bounds: {model.name}");

            Vector3 worldOffset = new Vector3(
                -bounds.center.x,
                Mathf.Max(0f, groundClearance) - bounds.min.y,
                -bounds.center.z);
            model.position += worldOffset;
        }

        private static bool TryGetCombinedBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;

            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private static Transform FindDescendant(Transform root, string childName)
        {
            if (root.name == childName)
                return root;

            for (int index = 0; index < root.childCount; index++)
            {
                Transform found = FindDescendant(root.GetChild(index), childName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static void UnpackIfNeeded(GameObject model)
        {
            GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(model);
            if (prefabRoot != null)
            {
                PrefabUtility.UnpackPrefabInstance(
                    prefabRoot,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
            }
        }

        private static void StripImportedRuntimeComponents(GameObject root)
        {
            DestroyComponents<Collider>(root);
            DestroyComponents<Rigidbody>(root);
            DestroyComponents<Joint>(root);
            DestroyComponents<CharacterController>(root);
        }

        private static void DestroyComponents<T>(GameObject root)
            where T : Component
        {
            T[] components = root.GetComponentsInChildren<T>(true);
            for (int index = 0; index < components.Length; index++)
                UnityEngine.Object.DestroyImmediate(components[index]);
        }

        private sealed class VisualPrefabSpec
        {
            public VisualPrefabSpec(
                string prefabName,
                string sourceAssetPath,
                string sourceChildName,
                string outputPath,
                float targetMaxDimension,
                float groundClearance)
            {
                PrefabName = prefabName;
                SourceAssetPath = sourceAssetPath;
                SourceChildName = sourceChildName;
                OutputPath = outputPath;
                TargetMaxDimension = targetMaxDimension;
                GroundClearance = groundClearance;
            }

            public string PrefabName { get; }
            public string SourceAssetPath { get; }
            public string SourceChildName { get; }
            public string OutputPath { get; }
            public float TargetMaxDimension { get; }
            public float GroundClearance { get; }
        }
    }
}
