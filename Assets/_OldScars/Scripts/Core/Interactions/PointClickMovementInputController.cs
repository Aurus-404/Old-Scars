using UnityEngine;
using UnityEngine.InputSystem;

namespace OldScars.Core.Interactions
{
    public sealed class PointClickMovementInputController : MonoBehaviour
    {
        [SerializeField] private Camera inputCamera;
        [SerializeField] private PointClickMovementController movementController;
        [SerializeField] private DebugWorldUiInputBlocker uiInputBlocker;
        [SerializeField] private DebugActionProgressController progressController;
        [SerializeField] private LayerMask groundLayerMask;
        [SerializeField] private float maxRayDistance = 1000f;

        private void Awake()
        {
            if (inputCamera == null)
                inputCamera = Camera.main;

            if (movementController == null)
                movementController = FindAnyObjectByType<PointClickMovementController>();

            if (uiInputBlocker == null)
                uiInputBlocker = FindAnyObjectByType<DebugWorldUiInputBlocker>();

            if (progressController == null)
                progressController = FindAnyObjectByType<DebugActionProgressController>();

            if (groundLayerMask.value == 0)
                groundLayerMask = LayerMask.GetMask("Ground");
        }

        private void Update()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
                return;

            Vector2 mousePosition = Mouse.current.position.ReadValue();

            if (uiInputBlocker != null && uiInputBlocker.ConsumeLeftClickIfNeeded(mousePosition))
                return;

            TrySetMovementTarget(mousePosition);
        }

        private void CancelActiveActionForMovement()
        {
            if (progressController == null)
                progressController = FindAnyObjectByType<DebugActionProgressController>();

            if (progressController != null)
                progressController.TryCancelActiveAction("valid movement order received");
        }

        private void TrySetMovementTarget(Vector2 mousePosition)
        {
            if (inputCamera == null)
            {
                Debug.LogError("[PointClickMovementInputController] Input camera was not found.");
                return;
            }

            if (movementController == null)
            {
                Debug.LogError("[PointClickMovementInputController] PointClickMovementController was not found.");
                return;
            }

            if (groundLayerMask.value == 0)
            {
                Debug.LogWarning("[PointClickMovementInputController] Ground layer mask is not configured. Assign the Ground layer to the movement raycast.");
                return;
            }

            Ray ray = inputCamera.ScreenPointToRay(new Vector3(mousePosition.x, mousePosition.y, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                CancelActiveActionForMovement();
                movementController.SetTarget(hit.point);
            }
        }
    }
}
