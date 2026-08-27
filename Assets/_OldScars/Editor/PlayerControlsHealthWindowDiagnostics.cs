using System;
using OldScars.Core.Actors;
using OldScars.Core.Interactions;
using OldScars.Core.Items;
using UnityEditor;
using UnityEngine;

namespace OldScars.Editor
{
    public static class PlayerControlsHealthWindowDiagnostics
    {
        private const string Menu = "Old Scars/Diagnostics/Player Controls & Health Window/Run Foundation";

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Player Controls & Health Window diagnostics require idle Edit Mode.");

            GameObject targetObject = new GameObject("Player Controls Diagnostic Target");
            GameObject rigObject = new GameObject("Player Controls Diagnostic Camera Rig");
            GameObject cameraObject = new GameObject("Player Controls Diagnostic Camera");
            GameObject healthObject = new GameObject("Player Controls Diagnostic Health");
            GameObject inventoryObject = new GameObject("Player Controls Diagnostic Inventory");
            GameObject inventoryPanelObject = new GameObject("Player Controls Diagnostic Inventory Panel");
            GameObject blockerObject = new GameObject("Player Controls Diagnostic UI Blocker");
            GameObject debugPanelObject = new GameObject("Player Controls Diagnostic Runtime Debug Tools");
            GameObject obstructionObject = null;
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetParent(rigObject.transform, false);
                cameraObject.transform.localPosition = new Vector3(0f, 10f, -10f);
                CameraRigController rig = rigObject.AddComponent<CameraRigController>();
                rig.SetFollowTarget(targetObject.transform);
                targetObject.transform.position = new Vector3(4f, 0f, -3f);
                rig.FollowTargetNow();
                Require(Near(rig.transform.position, targetObject.transform.position), "Moving the follow target did not move CameraRig.");
                Require(!rig.AllowsIndependentPan, "CameraRig still allows independent pan.");

                Quaternion rotationBeforeOrbit = rig.transform.rotation;
                rig.OrbitAroundTarget(30f);
                Require(rig.transform.rotation != rotationBeforeOrbit, "RMB orbit contract is unavailable.");
                rig.PitchAroundTarget(999f);
                Require(rig.PitchDegrees <= 35f, "Camera pitch did not respect its configured upper clamp.");
                rig.PitchAroundTarget(-1998f);
                Require(rig.PitchDegrees >= -35f, "Camera pitch did not respect its configured lower clamp.");
                float zoomBefore = camera.transform.localPosition.magnitude;
                rig.ApplyZoom(1f);
                Require(!Mathf.Approximately(camera.transform.localPosition.magnitude, zoomBefore), "Mouse-wheel zoom did not change camera distance.");
                rig.RecenterOnTarget();
                Require(Near(rig.transform.position, targetObject.transform.position), "Recenter did not preserve follow target alignment.");

                targetObject.AddComponent<SphereCollider>().radius = 1f;
                rig.ResolveCameraCollisionNow();
                Require(Mathf.Abs(rig.ActualZoomDistance - rig.DesiredZoomDistance) <= 0.0001f,
                    "Player self collider incorrectly blocked camera collision.");
                obstructionObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstructionObject.name = "Player Controls Diagnostic Camera Obstruction";
                obstructionObject.transform.position = rig.transform.TransformPoint(camera.transform.localPosition.normalized * 4f);
                obstructionObject.transform.localScale = Vector3.one * 2f;
                Physics.SyncTransforms();
                rig.ResolveCameraCollisionNow();
                Require(rig.ActualZoomDistance < rig.DesiredZoomDistance,
                    "Camera collision did not shorten the actual camera distance.");
                UnityEngine.Object.DestroyImmediate(obstructionObject);
                obstructionObject = null;
                Physics.SyncTransforms();
                rig.ResolveCameraCollisionNow();
                Require(Mathf.Abs(rig.ActualZoomDistance - rig.DesiredZoomDistance) <= 0.0001f,
                    "Camera desired zoom did not survive a removed obstruction.");

                rig.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                Vector3 forward = PlayerMovementInputController.CalculateCameraRelativeDirection(Vector2.up, camera.transform);
                Vector3 right = PlayerMovementInputController.CalculateCameraRelativeDirection(Vector2.right, camera.transform);
                Vector3 diagonal = PlayerMovementInputController.CalculateCameraRelativeDirection(new Vector2(1f, 1f), camera.transform);
                Require(Near(forward, camera.transform.forward), "W is not camera-relative after a 90 degree camera yaw.");
                Require(Near(right, camera.transform.right), "D is not camera-relative after a 90 degree camera yaw.");
                Require(Mathf.Abs(diagonal.magnitude - 1f) <= 0.0001f, "Diagonal movement is not normalized.");
                Require(PlayerMovementInputController.CalculateCameraRelativeDirection(Vector2.zero, camera.transform) == Vector3.zero,
                    "Zero WASD input produced movement.");
                Require(typeof(PlayerMovementController).Assembly.GetType("OldScars.Core.Interactions.PointClickMovementController") == null &&
                        typeof(PlayerMovementController).Assembly.GetType("OldScars.Core.Interactions.PointClickMovementInputController") == null,
                    "A legacy PointClick movement type is still compiled.");

                ActorHealthComponent health = healthObject.AddComponent<ActorHealthComponent>();
                health.ApplyInitialHealth(100f, 100f);
                ActorHealthDebugWindow window = healthObject.AddComponent<ActorHealthDebugWindow>();
                window.SetActorHealth(health);
                InventoryUISessionController inventorySession = inventoryObject.AddComponent<InventoryUISessionController>();
                inventoryPanelObject.AddComponent<InventoryDebugPanel>();
                DebugWorldUiInputBlocker blocker = blockerObject.AddComponent<DebugWorldUiInputBlocker>();
                ActorNeedsDebugPanel debugPanel = debugPanelObject.AddComponent<ActorNeedsDebugPanel>();
                Require(!debugPanel.IsVisible, "Runtime Debug Tools did not bootstrap hidden.");
                debugPanel.ToggleVisibility();
                Require(debugPanel.IsVisible && debugPanel.ContainsScreenPosition(new Vector2(20f, Screen.height - 20f)),
                    "Runtime Debug Tools did not expose a blocking development panel when toggled.");
                debugPanel.ToggleVisibility();
                Require(!debugPanel.IsVisible, "Runtime Debug Tools did not close when toggled.");

                Require(!window.IsOpen, "Health Window did not bootstrap closed.");
                window.Open();
                Require(window.IsOpen && window.GetQualitativeStatus() == "Healthy", "Health Window did not expose the real ActorHealthComponent.");
                Vector2 windowPoint = new Vector2(260f, Screen.height - 32f);
                Require(window.ContainsScreenPosition(windowPoint), "Health Window did not recognize a click inside its bounds.");
                Require(blocker.ConsumeLeftClickIfNeeded(windowPoint), "Health Window click was not consumed before reaching world input.");
                Require(!blocker.BlocksWorldInput, "Opening Health Window incorrectly enabled global world-input blocking.");

                inventorySession.OpenPersonal();
                Require(inventorySession.IsOpen && !window.IsOpen, "Opening Inventory did not close Health Window.");
                Require(inventorySession.BlocksWorldInput, "Inventory did not retain movement blocking.");
                window.Open();
                Require(window.IsOpen && !inventorySession.IsOpen, "Opening Health did not close Inventory.");
                Require(!blocker.BlocksWorldInput, "Health Window incorrectly retained inventory world-input blocking.");
                window.Close();

                Debug.Log("Player Controls & Health Window Diagnostics: PASS");
            }
            finally
            {
                if (obstructionObject != null)
                    UnityEngine.Object.DestroyImmediate(obstructionObject);
                UnityEngine.Object.DestroyImmediate(debugPanelObject);
                UnityEngine.Object.DestroyImmediate(blockerObject);
                UnityEngine.Object.DestroyImmediate(inventoryPanelObject);
                UnityEngine.Object.DestroyImmediate(inventoryObject);
                UnityEngine.Object.DestroyImmediate(healthObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(rigObject);
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        private static bool ValidateRun() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;

        public static void RunBatch()
        {
            try
            {
                Run();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static bool Near(Vector3 actual, Vector3 expected)
        {
            return Vector3.Distance(actual, expected) <= 0.0001f;
        }

        private static void Require(bool condition, string failure)
        {
            if (!condition)
                throw new InvalidOperationException(failure);
        }
    }
}
