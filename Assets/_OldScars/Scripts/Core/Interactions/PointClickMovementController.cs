using UnityEngine;

namespace OldScars.Core.Interactions
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PointClickMovementController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float stoppingDistance = 0.05f;
        [SerializeField] private bool rotateTowardsMovement = true;
        [SerializeField] private float rotationSpeedDegrees = 720f;

        [Header("Gravity")]
        [SerializeField] private float gravity = 20f;

        private const float GroundedVerticalVelocity = -2f;

        private CharacterController characterController;
        private Vector3 targetPosition;
        private bool hasTarget;
        private float verticalVelocity;

        public bool HasTarget => hasTarget;
        public Vector3 TargetPosition => targetPosition;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        public void SetTarget(Vector3 worldPosition)
        {
            targetPosition = new Vector3(worldPosition.x, transform.position.y, worldPosition.z);
            hasTarget = true;
        }

        public void ClearTarget()
        {
            hasTarget = false;
        }

        private void Update()
        {
            if (characterController == null)
            {
                Debug.LogError("[PointClickMovementController] CharacterController was not found. Add one to the debug actor.");
                enabled = false;
                return;
            }

            Vector3 horizontalDisplacement = GetHorizontalDisplacement(out Vector3 movementDirection);
            ApplyGravity();

            Vector3 displacement = horizontalDisplacement;
            displacement.y = verticalVelocity * Time.deltaTime;

            characterController.Move(displacement);

            if (rotateTowardsMovement && movementDirection.sqrMagnitude > 0f)
                RotateTowards(movementDirection);
        }

        private Vector3 GetHorizontalDisplacement(out Vector3 movementDirection)
        {
            movementDirection = Vector3.zero;

            if (!hasTarget)
                return Vector3.zero;

            Vector3 currentPosition = transform.position;
            Vector3 toTarget = targetPosition - currentPosition;
            toTarget.y = 0f;

            float distanceToTarget = toTarget.magnitude;
            if (distanceToTarget <= stoppingDistance)
            {
                hasTarget = false;
                return Vector3.zero;
            }

            movementDirection = toTarget / distanceToTarget;
            float stepDistance = moveSpeed * Time.deltaTime;

            if (stepDistance >= distanceToTarget)
            {
                hasTarget = false;
                stepDistance = distanceToTarget;
            }

            return movementDirection * stepDistance;
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
