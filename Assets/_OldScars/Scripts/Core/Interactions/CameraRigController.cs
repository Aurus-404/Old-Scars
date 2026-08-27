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

        [Header("Pitch")]
        [SerializeField] private float minimumPitchDegrees = -35f;
        [SerializeField] private float maximumPitchDegrees = 35f;
        [SerializeField] private float pitchDegreesPerMousePixel = 0.15f;

        [Header("Camera Collision")]
        [SerializeField, Min(0.01f)] private float collisionRadius = 0.3f;
        [SerializeField, Min(0f)] private float collisionSafetyMargin = 0.12f;
        [SerializeField, Min(0.01f)] private float collisionRecoverySpeed = 18f;
        [SerializeField] private LayerMask collisionLayers = ~0;

        [Header("Rotation")]
        [SerializeField] private float rightDragThresholdPixels = 8f;
        [SerializeField] private float yawDegreesPerMousePixel = 0.2f;

        [Header("Recenter")]
        [SerializeField] private bool enableRecenterInput = true;

        private Vector2 rightMouseDownPosition;
        private bool rightMouseIsDown;
        private bool isRotating;
        private Camera mainCamera;
        private Vector3 desiredCameraLocalDirection = Vector3.back;
        private float desiredZoomDistance;
        private float actualZoomDistance;
        private float yawDegrees;
        private float pitchDegrees;
        private float authoredYawDegrees;
        private float authoredPitchDegrees;
        private float authoredZoomDistance;
        private bool cameraStateInitialized;

        public bool HasContinuousFollow => recenterTarget != null;
        public bool AllowsIndependentPan => false;
        public float DesiredZoomDistance => desiredZoomDistance;
        public float ActualZoomDistance => actualZoomDistance;
        public float PitchDegrees => pitchDegrees;

        private void Start()
        {
            mainCamera = Camera.main;
            if (inventorySessionController == null)
                inventorySessionController = FindAnyObjectByType<InventoryUISessionController>();
            if (uiInputBlocker == null)
                uiInputBlocker = FindAnyObjectByType<DebugWorldUiInputBlocker>();
            InitializeCameraState();
            if (snapToTargetOnStart)
                FollowTargetNow();
            ResolveCameraCollisionNow();
        }

        private void Update()
        {
            if (inventorySessionController != null && inventorySessionController.BlocksWorldInput)
            {
                ClearRotationInput();
                return;
            }

            bool pointerOverUi = Mouse.current != null && uiInputBlocker != null &&
                uiInputBlocker.IsPointerOverBlockingPanel(Mouse.current.position.ReadValue());
            if (pointerOverUi)
            {
                ClearRotationInput();
                return;
            }

            HandleZoomInput();
            HandleRightMouseRotation();
            HandleRecenterInput();
        }

        private void LateUpdate()
        {
            FollowTargetNow();
            UpdateCameraCollision(Time.deltaTime, false);
        }

        public void SetFollowTarget(Transform target)
        {
            recenterTarget = target;
            FollowTargetNow();
        }

        public void BindRuntime(
            Transform target,
            InventoryUISessionController inventorySession,
            DebugWorldUiInputBlocker inputBlocker)
        {
            recenterTarget = target;
            inventorySessionController = inventorySession;
            uiInputBlocker = inputBlocker;
            InitializeCameraState();
            FollowTargetNow();
        }

        public void RecenterOnTarget()
        {
            FollowTargetNow();
        }

        public void ResetCamera()
        {
            InitializeCameraState();
            yawDegrees = authoredYawDegrees;
            pitchDegrees = authoredPitchDegrees;
            desiredZoomDistance = authoredZoomDistance;
            actualZoomDistance = authoredZoomDistance;
            ApplyRigRotation();
            FollowTargetNow();
            ResolveCameraCollisionNow();
        }

        public void FollowTargetNow()
        {
            if (recenterTarget != null)
                transform.position = recenterTarget.position + targetOffset;
        }

        public void OrbitAroundTarget(float yawDelta)
        {
            InitializeCameraState();
            if (Mathf.Approximately(yawDelta, 0f))
                return;

            yawDegrees = Mathf.Repeat(yawDegrees + yawDelta, 360f);
            ApplyRigRotation();
        }

        public void PitchAroundTarget(float pitchDelta)
        {
            InitializeCameraState();
            if (Mathf.Approximately(pitchDelta, 0f))
                return;

            float lower = Mathf.Min(minimumPitchDegrees, maximumPitchDegrees);
            float upper = Mathf.Max(minimumPitchDegrees, maximumPitchDegrees);
            pitchDegrees = Mathf.Clamp(pitchDegrees + pitchDelta, lower, upper);
            ApplyRigRotation();
        }

        public void ApplyZoom(float scrollDelta)
        {
            InitializeCameraState();
            if (Mathf.Approximately(scrollDelta, 0f))
                return;

            desiredZoomDistance = Mathf.Clamp(
                desiredZoomDistance - scrollDelta * zoomSpeed,
                MinimumZoomDistance,
                MaximumZoomDistance);
            UpdateCameraCollision(0f, true);
        }

        /// <summary>
        /// Deterministic immediate query hook for diagnostics. Normal runtime
        /// recovery uses smoothing only when returning outward after a block.
        /// </summary>
        public void ResolveCameraCollisionNow()
        {
            UpdateCameraCollision(0f, true);
        }

        private float MinimumZoomDistance => Mathf.Max(0.05f, Mathf.Min(minZoomDistance, maxZoomDistance));
        private float MaximumZoomDistance => Mathf.Max(MinimumZoomDistance, Mathf.Max(minZoomDistance, maxZoomDistance));

        private void HandleZoomInput()
        {
            if (Mouse.current == null)
                return;

            ApplyZoom(Mouse.current.scroll.ReadValue().y);
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
                    Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                    OrbitAroundTarget(mouseDelta.x * yawDegreesPerMousePixel);
                    PitchAroundTarget(-mouseDelta.y * pitchDegreesPerMousePixel);
                }
            }

            if (Mouse.current.rightButton.wasReleasedThisFrame)
                ClearRotationInput();
        }

        private void HandleRecenterInput()
        {
            if (enableRecenterInput && Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame)
                RecenterOnTarget();
        }

        private void InitializeCameraState()
        {
            if (cameraStateInitialized)
                return;

            Camera cameraToPosition = GetMainCamera();
            if (cameraToPosition == null || !cameraToPosition.transform.IsChildOf(transform))
                return;

            Vector3 localPosition = cameraToPosition.transform.localPosition;
            desiredCameraLocalDirection = localPosition.sqrMagnitude > 0.0001f
                ? localPosition.normalized
                : Vector3.back;
            desiredZoomDistance = Mathf.Clamp(localPosition.magnitude, MinimumZoomDistance, MaximumZoomDistance);
            actualZoomDistance = desiredZoomDistance;
            yawDegrees = Mathf.Repeat(transform.eulerAngles.y, 360f);
            pitchDegrees = Mathf.Clamp(
                NormalizeSignedAngle(transform.eulerAngles.x),
                Mathf.Min(minimumPitchDegrees, maximumPitchDegrees),
                Mathf.Max(minimumPitchDegrees, maximumPitchDegrees));
            authoredYawDegrees = yawDegrees;
            authoredPitchDegrees = pitchDegrees;
            authoredZoomDistance = desiredZoomDistance;
            ApplyRigRotation();
            cameraStateInitialized = true;
        }

        private void ApplyRigRotation()
        {
            transform.rotation = Quaternion.Euler(pitchDegrees, yawDegrees, 0f);
        }

        private void UpdateCameraCollision(float deltaTime, bool immediateRecovery)
        {
            InitializeCameraState();
            Camera cameraToPosition = GetMainCamera();
            if (cameraToPosition == null || !cameraToPosition.transform.IsChildOf(transform))
                return;

            Vector3 origin = transform.position;
            Vector3 desiredWorldPosition = transform.TransformPoint(desiredCameraLocalDirection * desiredZoomDistance);
            Vector3 toDesired = desiredWorldPosition - origin;
            float requestedDistance = toDesired.magnitude;
            float targetDistance = desiredZoomDistance;
            if (requestedDistance > 0.0001f && TryGetCameraBlocker(origin, toDesired / requestedDistance, requestedDistance, out RaycastHit hit))
                targetDistance = Mathf.Max(0.05f, Mathf.Min(desiredZoomDistance, hit.distance - Mathf.Max(0f, collisionSafetyMargin)));

            if (immediateRecovery || targetDistance <= actualZoomDistance)
                actualZoomDistance = targetDistance;
            else
                actualZoomDistance = Mathf.MoveTowards(
                    actualZoomDistance,
                    targetDistance,
                    Mathf.Max(0.01f, collisionRecoverySpeed) * Mathf.Max(0f, deltaTime));

            cameraToPosition.transform.localPosition = desiredCameraLocalDirection * actualZoomDistance;
        }

        private bool TryGetCameraBlocker(Vector3 origin, Vector3 direction, float distance, out RaycastHit closestHit)
        {
            closestHit = default;
            RaycastHit[] hits = Physics.SphereCastAll(
                origin,
                Mathf.Max(0.01f, collisionRadius),
                direction,
                distance,
                collisionLayers,
                QueryTriggerInteraction.Ignore);
            bool found = false;
            for (int index = 0; index < hits.Length; index++)
            {
                RaycastHit hit = hits[index];
                if (hit.collider == null || IsSelfCollider(hit.collider))
                    continue;
                if (!found || hit.distance < closestHit.distance)
                {
                    closestHit = hit;
                    found = true;
                }
            }

            return found;
        }

        private bool IsSelfCollider(Collider collider)
        {
            if (recenterTarget == null || collider == null)
                return false;

            Transform colliderTransform = collider.transform;
            return colliderTransform == recenterTarget || colliderTransform.IsChildOf(recenterTarget);
        }

        private void ClearRotationInput()
        {
            rightMouseIsDown = false;
            isRotating = false;
        }

        private static float NormalizeSignedAngle(float degrees)
        {
            float normalized = Mathf.Repeat(degrees + 180f, 360f) - 180f;
            return Mathf.Approximately(normalized, -180f) ? 180f : normalized;
        }
    }
}
