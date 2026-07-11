using OldScars.Core.Interactions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldScars.EditorTools
{
    internal static class RepairM32DoorPivotsTool
    {
        private const string ValidateMenuPath = "Old Scars/Debug/Validate M32 Door Pivots";
        private const string RepairMenuPath = "Old Scars/Debug/Repair M32 Door Pivots";
        private const string HouseRootName = "M32_DebugTestHouse";
        private const string DoorsRootName = "Doors";
        private const string DoorVisualPivotName = "DoorVisualPivot";
        private const string DoorVisualName = "DoorVisual";
        private const string DoorPivotPropertyName = "doorPivot";
        private const float ScaleEpsilon = 0.001f;
        private const float NearZeroScaleThreshold = 0.01f;
        private const float AbsurdLocalPositionThreshold = 2f;
        private const float FallbackHalfWidth = 0.575f;

        [MenuItem(ValidateMenuPath)]
        private static void ValidateM32DoorPivots()
        {
            if (!TryGetDoorsRoot(out Transform doorsRoot))
                return;

            DoorSwingController[] controllers = doorsRoot.GetComponentsInChildren<DoorSwingController>(true);
            int issueCount = 0;

            for (int index = 0; index < controllers.Length; index++)
                issueCount += ValidateDoor(controllers[index]);

            if (controllers.Length == 0)
                Debug.LogWarning($"[RepairM32DoorPivotsTool] No DoorSwingController found under {HouseRootName}/{DoorsRootName}.");
            else if (issueCount == 0)
                Debug.Log($"[RepairM32DoorPivotsTool] Validated {controllers.Length} M32 door pivot(s): no issues found.");
            else
                Debug.LogWarning($"[RepairM32DoorPivotsTool] Validated {controllers.Length} M32 door pivot(s): {issueCount} issue(s) found.");
        }

        [MenuItem(RepairMenuPath)]
        private static void RepairM32DoorPivots()
        {
            if (!TryGetDoorsRoot(out Transform doorsRoot))
                return;

            Scene scene = doorsRoot.gameObject.scene;
            DoorSwingController[] controllers = doorsRoot.GetComponentsInChildren<DoorSwingController>(true);
            int repairedCount = 0;
            int skippedCount = 0;

            for (int index = 0; index < controllers.Length; index++)
            {
                if (TryRepairDoor(controllers[index]))
                    repairedCount++;
                else
                    skippedCount++;
            }

            if (repairedCount > 0)
                EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[RepairM32DoorPivotsTool] Repaired {repairedCount} M32 door pivot(s); skipped {skippedCount}.");
        }

        private static int ValidateDoor(DoorSwingController controller)
        {
            if (controller == null)
                return 0;

            int issueCount = 0;
            Transform root = controller.transform;
            Transform serializedPivot = GetSerializedDoorPivot(controller);
            Transform pivot = ResolvePivot(controller);
            Transform visual = pivot != null ? FindChildRecursive(pivot, DoorVisualName) : null;

            if (!IsApproximatelyOne(root.localScale))
                issueCount += LogIssue(root.name, $"root localScale is {FormatVector(root.localScale)}, expected (1, 1, 1).");

            if (serializedPivot == null)
                issueCount += LogIssue(root.name, "DoorSwingController has no serialized doorPivot reference.");
            else if (!serializedPivot.IsChildOf(root))
                issueCount += LogIssue(root.name, $"serialized doorPivot '{serializedPivot.name}' is outside the door root.");

            if (pivot == null)
            {
                issueCount += LogIssue(root.name, "DoorVisualPivot was not found by serialized reference or child name.");
                return issueCount;
            }

            if (!IsApproximatelyOne(pivot.localScale))
                issueCount += LogIssue(root.name, $"DoorVisualPivot localScale is {FormatVector(pivot.localScale)}, expected (1, 1, 1).");

            if (HasAbsurdLocalPosition(pivot.localPosition))
                issueCount += LogIssue(root.name, $"DoorVisualPivot localPosition looks invalid: {FormatVector(pivot.localPosition)}.");

            if (visual == null)
            {
                issueCount += LogIssue(root.name, "DoorVisualPivot has no DoorVisual child.");
                return issueCount;
            }

            if (HasNearZeroScale(visual.localScale))
                issueCount += LogIssue(root.name, $"DoorVisual localScale is invalid or near zero: {FormatVector(visual.localScale)}.");

            if (HasAbsurdLocalPosition(visual.localPosition))
                issueCount += LogIssue(root.name, $"DoorVisual localPosition looks invalid: {FormatVector(visual.localPosition)}.");

            return issueCount;
        }

        private static bool TryRepairDoor(DoorSwingController controller)
        {
            if (controller == null)
                return false;

            Transform root = controller.transform;
            Transform pivot = ResolvePivot(controller);
            if (pivot == null || !pivot.IsChildOf(root))
            {
                Debug.LogWarning($"[RepairM32DoorPivotsTool] Skipped '{root.name}': DoorVisualPivot is missing or outside the door root.");
                return false;
            }

            Transform visual = FindChildRecursive(pivot, DoorVisualName);
            if (visual == null)
            {
                Debug.LogWarning($"[RepairM32DoorPivotsTool] Skipped '{root.name}': DoorVisual child was not found under DoorVisualPivot.");
                return false;
            }

            Undo.RecordObjects(
                new UnityEngine.Object[] { root, pivot, visual },
                "Repair M32 Door Pivot");

            Vector3 originalRootScale = root.localScale;
            Vector3 originalPivotPosition = pivot.localPosition;

            if (!IsApproximatelyOne(originalRootScale) && IsApproximatelyOne(visual.localScale))
                visual.localScale = originalRootScale;

            root.localScale = Vector3.one;
            pivot.localScale = Vector3.one;
            pivot.localRotation = Quaternion.identity;
            visual.localRotation = Quaternion.identity;

            float halfWidth = Mathf.Abs(visual.localScale.x) * 0.5f;
            if (halfWidth <= NearZeroScaleThreshold)
                halfWidth = FallbackHalfWidth;

            float hingeSide = originalPivotPosition.x < 0f ? -1f : 1f;
            if (Mathf.Approximately(originalPivotPosition.x, 0f))
                hingeSide = -1f;

            Vector3 pivotLocalPosition = new Vector3(hingeSide * halfWidth, 0f, 0f);
            pivot.localPosition = pivotLocalPosition;
            visual.localPosition = -pivotLocalPosition;

            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(pivot);
            EditorUtility.SetDirty(visual);

            Debug.Log(
                $"[RepairM32DoorPivotsTool] Repaired '{root.name}': rootScale {FormatVector(originalRootScale)} -> {FormatVector(root.localScale)}, " +
                $"pivotLocalPosition={FormatVector(pivot.localPosition)}, visualLocalPosition={FormatVector(visual.localPosition)}, visualScale={FormatVector(visual.localScale)}.");
            return true;
        }

        private static bool TryGetDoorsRoot(out Transform doorsRoot)
        {
            doorsRoot = null;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[RepairM32DoorPivotsTool] Exit Play Mode before validating or repairing M32 door pivots.");
                return false;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[RepairM32DoorPivotsTool] No loaded active scene was found.");
                return false;
            }

            Transform houseRoot = FindSceneTransform(scene, HouseRootName);
            if (houseRoot == null)
            {
                Debug.LogWarning($"[RepairM32DoorPivotsTool] Scene object '{HouseRootName}' was not found.");
                return false;
            }

            doorsRoot = houseRoot.Find(DoorsRootName);
            if (doorsRoot == null)
            {
                Debug.LogWarning($"[RepairM32DoorPivotsTool] Scene object '{HouseRootName}/{DoorsRootName}' was not found.");
                return false;
            }

            return true;
        }

        private static Transform ResolvePivot(DoorSwingController controller)
        {
            Transform serializedPivot = GetSerializedDoorPivot(controller);
            if (serializedPivot != null)
                return serializedPivot;

            return controller != null ? controller.transform.Find(DoorVisualPivotName) : null;
        }

        private static Transform GetSerializedDoorPivot(DoorSwingController controller)
        {
            if (controller == null)
                return null;

            SerializedObject serializedController = new SerializedObject(controller);
            SerializedProperty doorPivot = serializedController.FindProperty(DoorPivotPropertyName);
            return doorPivot != null ? doorPivot.objectReferenceValue as Transform : null;
        }

        private static Transform FindSceneTransform(Scene scene, string objectName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                Transform match = FindChildRecursive(roots[index].transform, objectName);
                if (match != null)
                    return match;
            }

            return null;
        }

        private static Transform FindChildRecursive(Transform root, string objectName)
        {
            if (root == null)
                return null;

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

        private static bool IsApproximatelyOne(Vector3 value)
        {
            return Mathf.Abs(value.x - 1f) <= ScaleEpsilon
                && Mathf.Abs(value.y - 1f) <= ScaleEpsilon
                && Mathf.Abs(value.z - 1f) <= ScaleEpsilon;
        }

        private static bool HasNearZeroScale(Vector3 scale)
        {
            return Mathf.Abs(scale.x) <= NearZeroScaleThreshold
                || Mathf.Abs(scale.y) <= NearZeroScaleThreshold
                || Mathf.Abs(scale.z) <= NearZeroScaleThreshold;
        }

        private static bool HasAbsurdLocalPosition(Vector3 position)
        {
            return Mathf.Abs(position.x) > AbsurdLocalPositionThreshold
                || Mathf.Abs(position.y) > AbsurdLocalPositionThreshold
                || Mathf.Abs(position.z) > AbsurdLocalPositionThreshold;
        }

        private static int LogIssue(string doorName, string message)
        {
            Debug.LogWarning($"[RepairM32DoorPivotsTool] {doorName}: {message}");
            return 1;
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
        }
    }
}
