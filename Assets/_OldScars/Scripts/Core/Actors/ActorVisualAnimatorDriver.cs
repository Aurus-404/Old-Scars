using UnityEngine;

namespace OldScars.Core.Actors
{
    public sealed class ActorVisualAnimatorDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField] private float movingThreshold = 0.05f;
        [SerializeField] private float speedScale = 1f;
        [SerializeField] private float damping = 0.1f;
        [SerializeField] private bool usePlanarVelocityOnly = true;

        private Vector3 previousPosition;
        private float currentAnimatorSpeed;
        private float smoothingVelocity;
        private bool canUpdateAnimator;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);

            canUpdateAnimator = ValidateAnimatorSetup();
        }

        private void OnEnable()
        {
            previousPosition = transform.position;
            currentAnimatorSpeed = 0f;
            smoothingVelocity = 0f;
        }

        private void LateUpdate()
        {
            Vector3 currentPosition = transform.position;
            Vector3 displacement = currentPosition - previousPosition;
            previousPosition = currentPosition;

            if (!canUpdateAnimator || Time.deltaTime <= 0f)
                return;

            if (usePlanarVelocityOnly)
                displacement.y = 0f;

            float speed = displacement.magnitude / Time.deltaTime;
            float targetAnimatorSpeed = speed < movingThreshold
                ? 0f
                : Mathf.Clamp01(speed * speedScale);

            currentAnimatorSpeed = damping > 0f
                ? Mathf.SmoothDamp(
                    currentAnimatorSpeed,
                    targetAnimatorSpeed,
                    ref smoothingVelocity,
                    damping,
                    Mathf.Infinity,
                    Time.deltaTime)
                : targetAnimatorSpeed;

            animator.SetFloat(speedParameter, currentAnimatorSpeed);
        }

        private bool ValidateAnimatorSetup()
        {
            if (animator == null)
            {
                Debug.LogWarning($"[ActorVisualAnimatorDriver] '{name}' has no Animator assigned or available in its children.", this);
                return false;
            }

            if (string.IsNullOrWhiteSpace(speedParameter))
            {
                Debug.LogWarning($"[ActorVisualAnimatorDriver] '{name}' has no speed parameter configured.", this);
                return false;
            }

            for (int index = 0; index < animator.parameterCount; index++)
            {
                AnimatorControllerParameter parameter = animator.parameters[index];
                if (parameter.name == speedParameter && parameter.type == AnimatorControllerParameterType.Float)
                    return true;
            }

            Debug.LogWarning(
                $"[ActorVisualAnimatorDriver] Animator on '{name}' has no float parameter named '{speedParameter}'.",
                this);
            return false;
        }

        private void OnValidate()
        {
            movingThreshold = Mathf.Max(0f, movingThreshold);
            speedScale = Mathf.Max(0f, speedScale);
            damping = Mathf.Max(0f, damping);
        }
    }
}
