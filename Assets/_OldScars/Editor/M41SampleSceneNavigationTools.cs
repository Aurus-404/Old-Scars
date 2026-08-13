using System;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace OldScars.Editor
{
    public static class M41SampleSceneNavigationTools
    {
        public const string ScenePath = "Assets/Scenes/SampleScene.unity";
        public const string FixtureRootName = "M41_NavigationFixture";
        public const string FloorName = "Navigation Floor";
        public const string BarrierName = "Navigation Perception Barrier";
        public const string StartName = "Nav Start";
        public const string GoalName = "Nav Goal";
        public const string ObserverName = "Perception Observer";
        public const string TargetName = "Perception Target";

        private const string NavMeshAssetPath = "Assets/Scenes/SampleScene/NavMesh-M41_NavigationFixture.asset";

        [MenuItem("Old Scars/Diagnostics/AI/Prepare M41.0 SampleScene Navigation")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("M41.0 SampleScene navigation preparation requires idle Edit Mode.");

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject root = GameObject.Find(FixtureRootName);
            if (root == null)
            {
                root = new GameObject(FixtureRootName);
                root.transform.position = new Vector3(30f, 0f, 30f);
            }
            else
            {
                root.transform.SetPositionAndRotation(new Vector3(30f, 0f, 30f), Quaternion.identity);
                root.transform.localScale = Vector3.one;
            }

            GameObject floor = EnsureCube(root.transform, FloorName, new Vector3(0f, -0.1f, 0f), new Vector3(16f, 0.2f, 12f));
            GameObject barrier = EnsureCube(root.transform, BarrierName, new Vector3(0f, 1.25f, 0f), new Vector3(1f, 2.5f, 6f));
            NavMeshModifier modifier = barrier.GetComponent<NavMeshModifier>();
            if (modifier == null)
                modifier = barrier.AddComponent<NavMeshModifier>();
            modifier.overrideArea = true;
            modifier.area = NavMesh.GetAreaFromName("Not Walkable");
            modifier.applyToChildren = true;

            EnsureMarker(root.transform, StartName, new Vector3(-5f, 0f, 0f), Quaternion.LookRotation(Vector3.right));
            EnsureMarker(root.transform, GoalName, new Vector3(5f, 0f, 0f), Quaternion.LookRotation(Vector3.left));
            EnsureMarker(root.transform, ObserverName, new Vector3(-4f, 0f, 0f), Quaternion.LookRotation(Vector3.right));
            EnsureMarker(root.transform, TargetName, new Vector3(4f, 0f, 0f), Quaternion.LookRotation(Vector3.left));

            NavMeshSurface surface = root.GetComponent<NavMeshSurface>();
            if (surface == null)
                surface = root.AddComponent<NavMeshSurface>();
            surface.agentTypeID = 0;
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.defaultArea = 0;
            surface.ignoreNavMeshAgent = true;
            surface.ignoreNavMeshObstacle = true;

            BakeStableAsset(surface);
            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(floor);
            EditorUtility.SetDirty(barrier);
            EditorSceneManager.MarkSceneDirty(scene);
            ValidatePreparedFixture(root, surface);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log(
                "M41.0 SampleScene Navigation Preparation: PASS" +
                $"\n  Scene: {ScenePath}" +
                $"\n  Fixture: {FixtureRootName}" +
                $"\n  NavMeshData: {NavMeshAssetPath}" +
                "\n  Contract: isolated baked floor, blocking barrier and reproducible markers");
        }

        public static void PrepareBatch()
        {
            try
            {
                Prepare();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("M41.0 SampleScene Navigation Preparation: FAIL\n- " + exception.Message);
                EditorApplication.Exit(1);
            }
        }

        public static Transform FindMarker(string markerName)
        {
            GameObject root = GameObject.Find(FixtureRootName);
            return root != null ? root.transform.Find(markerName) : null;
        }

        public static GameObject FindBarrier()
        {
            Transform marker = FindMarker(BarrierName);
            return marker != null ? marker.gameObject : null;
        }

        private static void BakeStableAsset(NavMeshSurface surface)
        {
            NavMeshData existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshAssetPath);
            surface.RemoveData();
            surface.navMeshData = null;
            surface.BuildNavMesh();
            NavMeshData built = surface.navMeshData;
            if (built == null)
                throw new InvalidOperationException("NavMeshSurface produced no NavMeshData.");
            surface.RemoveData();

            if (existing == null)
            {
                string directory = Path.GetDirectoryName(NavMeshAssetPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory ?? "Assets/Scenes/SampleScene");
                AssetDatabase.CreateAsset(built, NavMeshAssetPath);
                existing = built;
            }
            else
            {
                EditorUtility.CopySerialized(built, existing);
                UnityEngine.Object.DestroyImmediate(built);
                EditorUtility.SetDirty(existing);
            }

            surface.navMeshData = existing;
            if (surface.isActiveAndEnabled)
                surface.AddData();
            EditorUtility.SetDirty(surface);
        }

        private static void ValidatePreparedFixture(GameObject root, NavMeshSurface surface)
        {
            if (surface.navMeshData == null)
                throw new InvalidOperationException("Prepared NavMeshSurface has no persisted NavMeshData.");
            Transform start = root.transform.Find(StartName);
            Transform goal = root.transform.Find(GoalName);
            if (start == null || goal == null)
                throw new InvalidOperationException("Navigation markers are missing after preparation.");
            if (!NavMesh.SamplePosition(start.position, out NavMeshHit startHit, 2f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(goal.position, out NavMeshHit goalHit, 2f, NavMesh.AllAreas))
                throw new InvalidOperationException("Start or goal marker does not resolve to the baked NavMesh.");
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(startHit.position, goalHit.position, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete)
                throw new InvalidOperationException("Prepared start-to-goal path is not complete.");
            if (NavMesh.CalculateTriangulation().vertices.Length == 0)
                throw new InvalidOperationException("Prepared NavMesh triangulation is empty.");
        }

        private static GameObject EnsureCube(Transform parent, string objectName, Vector3 localPosition, Vector3 localScale)
        {
            Transform existing = parent.Find(objectName);
            GameObject result = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
            result.name = objectName;
            result.transform.SetParent(parent, false);
            result.transform.localPosition = localPosition;
            result.transform.localRotation = Quaternion.identity;
            result.transform.localScale = localScale;
            if (result.GetComponent<BoxCollider>() == null)
                result.AddComponent<BoxCollider>();
            return result;
        }

        private static void EnsureMarker(Transform parent, string markerName, Vector3 localPosition, Quaternion localRotation)
        {
            Transform marker = parent.Find(markerName);
            if (marker == null)
            {
                marker = new GameObject(markerName).transform;
                marker.SetParent(parent, false);
            }
            marker.localPosition = localPosition;
            marker.localRotation = localRotation;
            marker.localScale = Vector3.one;
        }
    }
}
