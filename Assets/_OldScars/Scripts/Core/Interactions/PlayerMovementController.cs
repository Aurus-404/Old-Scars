using UnityEngine;

namespace OldScars.Core.Interactions
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovementController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private bool rotateTowardsMovement = true;
        [SerializeField] private float rotationSpeedDegrees = 720f;

        [Header("Gravity")]
        [SerializeField] private float gravity = 20f;

        private const float GroundedVerticalVelocity = -2f;

        private CharacterController characterController;
        private Vector3 requestedMovementDirection;
        private float verticalVelocity;

        public Vector3 RequestedMovementDirection => requestedMovementDirection;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        public void SetMovementDirection(Vector3 worldDirection)
        {
            worldDirection.y = 0f;
            requestedMovementDirection = worldDirection.sqrMagnitude > 0f
                ? worldDirection.normalized
                : Vector3.zero;
        }

        public void ClearMovement()
        {
            requestedMovementDirection = Vector3.zero;
        }

        private void Update()
        {
            if (characterController == null)
            {
                Debug.LogError("[PlayerMovementController] CharacterController was not found. Add one to the player actor.");
                enabled = false;
                return;
            }

            ApplyGravity();
            Vector3 displacement = requestedMovementDirection * moveSpeed * Time.deltaTime;
            displacement.y = verticalVelocity * Time.deltaTime;
            characterController.Move(displacement);

            if (rotateTowardsMovement && requestedMovementDirection.sqrMagnitude > 0f)
                RotateTowards(requestedMovementDirection);
        }

        private void ApplyGravity()
        {
            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = GroundedVerticalVelocity;
                return;
            }

            verticalVelocity -= Mathf.Abs(gravity) * Time.deltaTime;
        }

        private void RotateTowards(Vector3 direction)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeedDegrees * Time.deltaTime);
        }
    }
}
