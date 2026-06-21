using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldScars.Editor
{
    internal static class RealUsableWorldItemMigrationTools
    {
        private const string MenuPath = "Old Scars/World Items/Migrate Sample Scene Pickups To Usable Prefabs";

        private static readonly MigrationSpec[] MigrationSpecs =
        {
            new MigrationSpec(
                "Debug World Crowbar",
                "Assets/_OldScars/Resources/PFB_WorldItem_rusted_crowbar_01.prefab"),
            new MigrationSpec(
                "Debug World Lee-Enfield Rifle",
                "Assets/_OldScars/Resources/PFB_WorldItem_lee_enfield_rifle_01.prefab")
        };

        [MenuItem(MenuPath)]
        private static void MigrateSampleScenePickups()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[RealUsableWorldItemMigrationTools] Exit Play Mode before migrating scene pickups.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[RealUsableWorldItemMigrationTools] No loaded active scene was found.");
                return;
            }

            int migratedCount = 0;
            for (int index = 0; index < MigrationSpecs.Length; index++)
            {
                if (TryMigrate(scene, MigrationSpecs[index]))
                    migratedCount++;
            }

            if (migratedCount > 0)
                EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[RealUsableWorldItemMigrationTools] Migrated {migratedCount} world item pickup(s) in '{scene.name}'.");
        }

        private static bool TryMigrate(Scene scene, MigrationSpec spec)
        {
            GameObject existing = FindSceneObject(scene, spec.SceneObjectName);
            if (existing == null)
            {
                Debug.LogWarning($"[RealUsableWorldItemMigrationTools] Scene object '{spec.SceneObjectName}' was not found.");
                return false;
            }

            string currentPrefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(existing);
            if (currentPrefabPath == spec.PrefabPath)
                return false;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[RealUsableWorldItemMigrationTools] Prefab was not found: {spec.PrefabPath}");
                return false;
            }

            Transform existingTransform = existing.transform;
            Transform parent = existingTransform.parent;
            int siblingIndex = existingTransform.GetSiblingIndex();
            Vector3 localPosition = existingTransform.localPosition;
            Quaternion localRotation = existingTransform.localRotation;
            Vector3 localScale = existingTransform.localScale;

            GameObject replacement = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (replacement == null)
            {
                Debug.LogError($"[RealUsableWorldItemMigrationTools] Could not instantiate: {spec.PrefabPath}");
                return false;
            }

            Undo.RegisterCreatedObjectUndo(replacement, "Migrate usable world item prefab");
            replacement.name = spec.SceneObjectName;
            replacement.transform.SetParent(parent, false);
            replacement.transform.SetSiblingIndex(siblingIndex);
            replacement.transform.localPosition = localPosition;
            replacement.transform.localRotation = localRotation;
            replacement.transform.localScale = localScale;
            Undo.DestroyObjectImmediate(existing);
            return true;
        }

        private static GameObject FindSceneObject(Scene scene, string objectName)
        {
            GameObject[] rootObjects = scene.GetRootGameObjects();
            for (int index = 0; index < rootObjects.Length; index++)
            {
                Transform match = FindChildRecursive(rootObjects[index].transform, objectName);
                if (match != null)
                    return match.gameObject;
            }

            return null;
        }

        private static Transform FindChildRecursive(Transform root, string objectName)
        {
            if (root.name == objectName)
                return root;

            for (int index = 0; index < root.childCount; index++)
            {
                Transform match = FindChildRecursive(root.GetChild(index), objectName);
                if (match != null)
                    return match;
            }

            return null;
        }

        private readonly struct MigrationSpec
        {
            public MigrationSpec(string sceneObjectName, string prefabPath)
            {
                SceneObjectName = sceneObjectName;
                PrefabPath = prefabPath;
            }

            public string SceneObjectName { get; }
            public string PrefabPath { get; }
        }
    }
}
