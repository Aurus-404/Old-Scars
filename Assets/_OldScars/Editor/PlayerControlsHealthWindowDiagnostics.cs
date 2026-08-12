using System;
using OldScars.Core.Actors;
using OldScars.Core.Interactions;
using UnityEditor;
using UnityEngine;

namespace OldScars.Editor
{
    public static class PlayerControlsHealthWindowDiagnostics
    {
        private const string Menu = "Old Scars/Diagnostics/Player Controls & Health Window/Run Foundation";

        [MenuItem(Menu)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Player Controls & Health Window diagnostics require idle Edit Mode.");

            GameObject cameraObject = new GameObject("Player Controls Diagnostic Camera");
            GameObject healthObject = new GameObject("Player Controls Diagnostic Health");
            GameObject blockerObject = new GameObject("Player Controls Diagnostic UI Blocker");
            try
            {
                cameraObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                Vector3 forward = PlayerMovementInputController.CalculateCameraRelativeDirection(Vector2.up, cameraObject.transform);
                Vector3 right = PlayerMovementInputController.CalculateCameraRelativeDirection(Vector2.right, cameraObject.transform);
                Vector3 diagonal = PlayerMovementInputController.CalculateCameraRelativeDirection(new Vector2(1f, 1f), cameraObject.transform);
                Require(Near(forward, cameraObject.transform.forward), "W is not camera-relative after a 90 degree camera yaw.");
                Require(Near(right, cameraObject.transform.right), "D is not camera-relative after a 90 degree camera yaw.");
                Require(Mathf.Abs(diagonal.magnitude - 1f) <= 0.0001f, "Diagonal movement is not normalized.");
                Require(PlayerMovementInputController.CalculateCameraRelativeDirection(Vector2.zero, cameraObject.transform) == Vector3.zero,
                    "Zero WASD input produced movement.");
                Require(typeof(PlayerMovementController).Assembly.GetType("OldScars.Core.Interactions.PointClickMovementController") == null &&
                        typeof(PlayerMovementController).Assembly.GetType("OldScars.Core.Interactions.PointClickMovementInputController") == null,
                    "A legacy PointClick movement type is still compiled.");

                ActorHealthComponent health = healthObject.AddComponent<ActorHealthComponent>();
                health.ApplyInitialHealth(100f, 100f);
                ActorHealthDebugWindow window = healthObject.AddComponent<ActorHealthDebugWindow>();
                window.SetActorHealth(health);
                DebugWorldUiInputBlocker blocker = blockerObject.AddComponent<DebugWorldUiInputBlocker>();
                Require(!window.IsOpen, "Health Window did not bootstrap closed.");
                window.Open();
                Require(window.IsOpen && window.GetQualitativeStatus() == "Healthy", "Health Window did not expose the real ActorHealthComponent.");
                Vector2 windowPoint = new Vector2(260f, Screen.height - 32f);
                Require(window.ContainsScreenPosition(windowPoint), "Health Window did not recognize a click inside its bounds.");
                Require(blocker.ConsumeLeftClickIfNeeded(windowPoint), "Health Window click was not consumed before reaching world input.");
                Require(!blocker.BlocksWorldInput, "Opening Health Window incorrectly enabled global world-input blocking.");
                window.Close();
                Require(!window.IsOpen, "Health Window did not close.");

                Debug.Log("Player Controls & Health Window Diagnostics: PASS");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(blockerObject);
                UnityEngine.Object.DestroyImmediate(healthObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [MenuItem(Menu, true)]
        private static bool ValidateRun()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;
        }

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
            actual.y = 0f;
            expected.y = 0f;
            return Vector3.Distance(actual.normalized, expected.normalized) <= 0.0001f;
        }

        private static void Require(bool condition, string failure)
        {
            if (!condition)
                throw new InvalidOperationException(failure);
        }
    }
}
