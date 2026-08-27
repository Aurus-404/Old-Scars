using OldScars.Core.Actors;
using UnityEngine;

namespace OldScars.Core.Interactions
{
    [RequireComponent(typeof(CharacterController), typeof(ActorStaminaComponent))]
    public sealed class PlayerMovementController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField, Min(1f)] private float sprintMultiplier = 1.65f;
        [SerializeField] private bool rotateTowardsMovement = true;
        [SerializeField] private float rotationSpeedDegrees = 720f;

        [Header("Gravity")]
        [SerializeField] private float gravity = 20f;

        private const float GroundedVerticalVelocity = -2f;

        private CharacterController characterController;
        private ActorStaminaComponent stamina;
        private Vector3 requestedMovementDirection;
        private float verticalVelocity;
        private bool sprintRequested;
        private float debugMovementMultiplier = 1f;

        public Vector3 RequestedMovementDirection => requestedMovementDirection;
        public ActorStaminaComponent Stamina => stamina;
        public bool IsSprinting { get; private set; }
        public float DebugMovementMultiplier => debugMovementMultiplier;
        public float EffectiveMovementSpeed => Mathf.Max(0f, moveSpeed) *
                                               (IsSprinting ? Mathf.Max(1f, sprintMultiplier) : 1f) *
                                               debugMovementMultiplier;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            stamina = GetComponent<ActorStaminaComponent>();
            debugMovementMultiplier = 1f;
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
            sprintRequested = false;
            IsSprinting = false;
        }

        public void SetSprintRequested(bool requested)
        {
            sprintRequested = requested;
        }

        public void SetDebugMovementMultiplier(float multiplier)
        {
            debugMovementMultiplier = Mathf.Clamp(multiplier, 0.25f, 8f);
        }

        public void ResetDebugMovementMultiplier()
        {
            debugMovementMultiplier = 1f;
        }

        public bool TryTeleportTo(Vector3 rootPosition, Collider surfaceCollider, out string failure)
        {
            failure = null;
            if (characterController == null)
                characterController = GetComponent<CharacterController>();
            if (characterController == null)
            {
                failure = "Player CharacterController is missing.";
                return false;
            }

            if (!IsFinite(rootPosition))
            {
                failure = "Teleport position must be finite.";
                return false;
            }

            bool wasEnabled = characterController.enabled;
            if (wasEnabled)
                characterController.enabled = false;

            try
            {
                Physics.SyncTransforms();
                if (HasTeleportOverlap(rootPosition, surfaceCollider))
                {
                    failure = "Teleport destination overlaps blocking geometry.";
                    return false;
                }

                transform.position = rootPosition;
                verticalVelocity = GroundedVerticalVelocity;
                ClearMovement();
                Physics.SyncTransforms();
                return true;
            }
            finally
            {
                if (wasEnabled)
                    characterController.enabled = true;
            }
        }

        private void Update()
        {
            if (characterController == null)
            {
                Debug.LogError("[PlayerMovementController] CharacterController was not found. Add one to the player actor.");
                enabled = false;
                return;
            }

            if (stamina == null)
                stamina = GetComponent<ActorStaminaComponent>();

            ApplyGravity();
            IsSprinting = sprintRequested && requestedMovementDirection.sqrMagnitude > 0f &&
                          stamina != null && stamina.CanSprint;
            Vector3 positionBeforeMove = transform.position;
            Vector3 displacement = requestedMovementDirection * EffectiveMovementSpeed * Time.deltaTime;
            displacement.y = verticalVelocity * Time.deltaTime;
            characterController.Move(displacement);

            Vector3 actualPlanarDisplacement = transform.position - positionBeforeMove;
            actualPlanarDisplacement.y = 0f;
            stamina?.Advance(Time.deltaTime, IsSprinting && actualPlanarDisplacement.sqrMagnitude > 0.000001f);

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

        private bool HasTeleportOverlap(Vector3 rootPosition, Collider surfaceCollider)
        {
            float radius = Mathf.Max(0.001f, characterController.radius - characterController.skinWidth * 0.5f);
            float height = Mathf.Max(radius * 2f, characterController.height - characterController.skinWidth);
            Vector3 center = rootPosition + transform.rotation * characterController.center;
            Vector3 up = transform.up;
            float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
            Collider[] overlaps = Physics.OverlapCapsule(
                center - up * halfSegment,
                center + up * halfSegment,
                radius,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            for (int index = 0; index < overlaps.Length; index++)
            {
                Collider overlap = overlaps[index];
                if (overlap == null || overlap == surfaceCollider || overlap.transform.IsChildOf(transform))
                    continue;
                return true;
            }

            return false;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
