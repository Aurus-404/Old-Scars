using UnityEngine;
using UnityEngine.InputSystem;

namespace OldScars.Core.Interactions
{
    public sealed class CameraRigController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform recenterTarget;

        [Header("Movement")]
        [SerializeField] private float panSpeed = 10f;
        [SerializeField] private bool snapToTargetOnStart = true;
        [SerializeField] private Vector3 targetOffset;

        [Header("Screen Edge Pan")]
        [SerializeField] private bool enableScreenEdgePan;
        [SerializeField] private float screenEdgeSize = 20f;

        [Header("Zoom")]
        [SerializeField] private float zoomSpeed = 0.02f;
        [SerializeField] private float minZoomDistance = 4f;
        [SerializeField] private float maxZoomDistance = 18f;

        [Header("Rotation")]
        [SerializeField] private float rightDragThresholdPixels = 8f;
        [SerializeField] private float yawDegreesPerMousePixel = 0.2f;

        [Header("Recenter")]
        [SerializeField] private bool enableRecenterInput = true;

        private Vector2 rightMouseDownPosition;
        private bool rightMouseIsDown;
        private bool isRotating;
        private Camera mainCamera;

        private void Start()
        {
            mainCamera = Camera.main;

            if (snapToTargetOnStart)
                RecenterOnTarget();
        }

        private void Update()
        {
            HandlePanInput();
            HandleZoomInput();
            HandleRightMouseRotation();
            HandleRecenterInput();
        }

        public void RecenterOnTarget()
        {
            if (recenterTarget == null)
                return;

            transform.position = recenterTarget.position + targetOffset;
        }

        private void HandlePanInput()
        {
            if (Keyboard.current == null)
                return;

            Vector3 move = Vector3.zero;

            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 right = transform.right;
            right.y = 0f;
            right.Normalize();

            if (Keyboard.current.wKey.isPressed)
                move += forward;
            if (Keyboard.current.sKey.isPressed)
                move -= forward;
            if (Keyboard.current.dKey.isPressed)
                move += right;
            if (Keyboard.current.aKey.isPressed)
                move -= right;

            if (enableScreenEdgePan && Mouse.current != null)
                move += GetScreenEdgeMove(forward, right);

            if (move.sqrMagnitude <= 0f)
                return;

            transform.position += move.normalized * panSpeed * Time.deltaTime;
        }

        private Vector3 GetScreenEdgeMove(Vector3 forward, Vector3 right)
        {
            Vector2 position = Mouse.current.position.ReadValue();
            Vector3 move = Vector3.zero;

            if (position.x <= screenEdgeSize)
                move -= right;
            else if (position.x >= Screen.width - screenEdgeSize)
                move += right;

            if (position.y <= screenEdgeSize)
                move -= forward;
            else if (position.y >= Screen.height - screenEdgeSize)
                move += forward;

            return move;
        }

        private void HandleZoomInput()
        {
            if (Mouse.current == null)
                return;

            float scrollDelta = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Approximately(scrollDelta, 0f))
                return;

            Camera cameraToZoom = GetMainCamera();
            if (cameraToZoom == null)
            {
                Debug.LogWarning("[CameraRigController] Camera.main was not found, so zoom input cannot be applied.");
                return;
            }

            Transform cameraTransform = cameraToZoom.transform;
            if (!cameraTransform.IsChildOf(transform))
            {
                Debug.LogWarning("[CameraRigController] Camera.main must be a child of the camera rig to zoom by localPosition.");
                return;
            }

            Vector3 localPosition = cameraTransform.localPosition;
            if (localPosition.sqrMagnitude <= 0.0001f)
                localPosition = Vector3.back;

            float minDistance = Mathf.Max(0.01f, Mathf.Min(minZoomDistance, maxZoomDistance));
            float maxDistance = Mathf.Max(minDistance, Mathf.Max(minZoomDistance, maxZoomDistance));
            float currentDistance = localPosition.magnitude;
            float targetDistance = Mathf.Clamp(
                currentDistance - scrollDelta * zoomSpeed,
                minDistance,
                maxDistance);

            cameraTransform.localPosition = localPosition.normalized * targetDistance;
        }

        private Camera GetMainCamera()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            return mainCamera;
        }

        private void HandleRightMouseRotation()
        {
            if (Mouse.current == null)
                return;

            Vector2 mousePosition = Mouse.current.position.ReadValue();

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                rightMouseDownPosition = mousePosition;
                rightMouseIsDown = true;
                isRotating = false;
            }

            if (rightMouseIsDown && Mouse.current.rightButton.isPressed)
            {
                if (!isRotating && (mousePosition - rightMouseDownPosition).sqrMagnitude >= rightDragThresholdPixels * rightDragThresholdPixels)
                    isRotating = true;

                if (isRotating)
                {
                    float yawDelta = Mouse.current.delta.ReadValue().x * yawDegreesPerMousePixel;
                    transform.Rotate(Vector3.up, yawDelta, Space.World);
                }
            }

            if (Mouse.current.rightButton.wasReleasedThisFrame)
            {
                rightMouseIsDown = false;
                isRotating = false;
            }
        }

        private void HandleRecenterInput()
        {
            if (!enableRecenterInput || Mouse.current == null)
                return;

            if (Mouse.current.middleButton.wasPressedThisFrame)
                RecenterOnTarget();
        }
    }
}
