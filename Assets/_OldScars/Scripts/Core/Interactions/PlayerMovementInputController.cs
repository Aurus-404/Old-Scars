using OldScars.Core.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OldScars.Core.Interactions
{
    public sealed class PlayerMovementInputController : MonoBehaviour
    {
        [SerializeField] private Camera inputCamera;
        [SerializeField] private PlayerMovementController movementController;
        [SerializeField] private DebugWorldUiInputBlocker uiInputBlocker;
        [SerializeField] private DebugActionProgressController progressController;
        [SerializeField] private InventoryUISessionController inventorySessionController;

        private bool wasMoving;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnDisable()
        {
            movementController?.ClearMovement();
            wasMoving = false;
        }

        private void Update()
        {
            ResolveReferences();
            if (movementController == null)
                return;

            if (inventorySessionController != null && inventorySessionController.BlocksWorldInput)
            {
                movementController.ClearMovement();
                wasMoving = false;
                return;
            }

            Vector2 input = ReadMovementInput();
            Vector3 direction = CalculateCameraRelativeDirection(input, inputCamera != null ? inputCamera.transform : null);
            bool hasValidMovement = direction.sqrMagnitude > 0f;
            movementController.SetMovementDirection(direction);

            if (hasValidMovement && !wasMoving)
                CancelActiveActionForMovement();

            wasMoving = hasValidMovement;
        }

        public static Vector3 CalculateCameraRelativeDirection(Vector2 input, Transform cameraTransform)
        {
            if (cameraTransform == null || input.sqrMagnitude <= 0f)
                return Vector3.zero;

            Vector3 forward = cameraTransform.forward;
            forward.y = 0f;
            Vector3 right = cameraTransform.right;
            right.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f || right.sqrMagnitude <= 0.0001f)
                return Vector3.zero;

            Vector3 direction = forward.normalized * input.y + right.normalized * input.x;
            return direction.sqrMagnitude > 0f ? direction.normalized : Vector3.zero;
        }

        public void BindRuntime(
            Camera camera,
            PlayerMovementController movement,
            DebugWorldUiInputBlocker inputBlocker,
            DebugActionProgressController progress,
            InventoryUISessionController inventorySession)
        {
            inputCamera = camera;
            movementController = movement;
            uiInputBlocker = inputBlocker;
            progressController = progress;
            inventorySessionController = inventorySession;
        }

        private static Vector2 ReadMovementInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return Vector2.zero;

            float horizontal = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            float vertical = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            return Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
        }

        private void CancelActiveActionForMovement()
        {
            if (progressController == null)
                progressController = FindAnyObjectByType<DebugActionProgressController>();

            progressController?.TryCancelActiveAction("valid WASD movement input received");
        }

        private void ResolveReferences()
        {
            if (inputCamera == null)
                inputCamera = Camera.main;
            if (movementController == null)
                movementController = FindAnyObjectByType<PlayerMovementController>();
            if (uiInputBlocker == null)
                uiInputBlocker = FindAnyObjectByType<DebugWorldUiInputBlocker>();
            if (progressController == null)
                progressController = FindAnyObjectByType<DebugActionProgressController>();
            if (inventorySessionController == null)
                inventorySessionController = FindAnyObjectByType<InventoryUISessionController>();
        }
    }
}
