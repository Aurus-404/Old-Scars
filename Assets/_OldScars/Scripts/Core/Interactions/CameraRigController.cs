using OldScars.Core.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OldScars.Core.Interactions
{
    public sealed class CameraRigController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform recenterTarget;
        [SerializeField] private InventoryUISessionController inventorySessionController;
        [SerializeField] private DebugWorldUiInputBlocker uiInputBlocker;

        [Header("Follow")]
        [SerializeField] private bool snapToTargetOnStart = true;
        [SerializeField] private Vector3 targetOffset;

        [Header("Zoom")]
        [SerializeField] private float zoomSpeed = 0.02f;
        [SerializeField] private float minZoomDistance = 2.5f;
        [SerializeField] private float maxZoomDistance = 28f;

        [Header("Rotation")]
        [SerializeField] private float rightDragThresholdPixels = 8f;
        [SerializeField] private float yawDegreesPerMousePixel = 0.2f;

        [Header("Recenter")]
        [SerializeField] private bool enableRecenterInput = true;

        private Vector2 rightMouseDownPosition;
        private bool rightMouseIsDown;
        private bool isRotating;
        private Camera mainCamera;

        public bool HasContinuousFollow => recenterTarget != null;
        public bool AllowsIndependentPan => false;

        private void Start()
        {
            mainCamera = Camera.main;
            if (inventorySessionController == null)
                inventorySessionController = FindAnyObjectByType<InventoryUISessionController>();
            if (uiInputBlocker == null)
                uiInputBlocker = FindAnyObjectByType<DebugWorldUiInputBlocker>();
            if (snapToTargetOnStart)
                FollowTargetNow();
        }

        private void Update()
        {
            if (inventorySessionController != null && inventorySessionController.BlocksWorldInput)
            {
                rightMouseIsDown = false;
                isRotating = false;
                return;
            }

            bool pointerOverUi = Mouse.current != null && uiInputBlocker != null &&
                uiInputBlocker.IsPointerOverBlockingPanel(Mouse.current.position.ReadValue());
            if (pointerOverUi)
            {
                rightMouseIsDown = false;
                isRotating = false;
            }
            else
            {
                HandleZoomInput();
                HandleRightMouseRotation();
                HandleRecenterInput();
            }
        }

        private void LateUpdate()
        {
            FollowTargetNow();
        }

        public void SetFollowTarget(Transform target)
        {
            recenterTarget = target;
            FollowTargetNow();
        }

        public void RecenterOnTarget()
        {
            FollowTargetNow();
        }

        public void FollowTargetNow()
        {
            if (recenterTarget != null)
                transform.position = recenterTarget.position + targetOffset;
        }

        public void OrbitAroundTarget(float yawDelta)
        {
            if (!Mathf.Approximately(yawDelta, 0f))
                transform.Rotate(Vector3.up, yawDelta, Space.World);
        }

        public void ApplyZoom(float scrollDelta)
        {
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
            float targetDistance = Mathf.Clamp(localPosition.magnitude - scrollDelta * zoomSpeed, minDistance, maxDistance);
            cameraTransform.localPosition = localPosition.normalized * targetDistance;
        }

        private void HandleZoomInput()
        {
            if (Mouse.current == null)
                return;

            float scrollDelta = Mouse.current.scroll.ReadValue().y;
            if (!Mathf.Approximately(scrollDelta, 0f))
                ApplyZoom(scrollDelta);
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
                    OrbitAroundTarget(Mouse.current.delta.ReadValue().x * yawDegreesPerMousePixel);
            }

            if (Mouse.current.rightButton.wasReleasedThisFrame)
            {
                rightMouseIsDown = false;
                isRotating = false;
            }
        }

        private void HandleRecenterInput()
        {
            if (enableRecenterInput && Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame)
                RecenterOnTarget();
        }
    }
}
