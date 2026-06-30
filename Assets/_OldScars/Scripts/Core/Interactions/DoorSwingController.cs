using UnityEngine;

namespace OldScars.Core.Interactions
{
    public sealed class DoorSwingController : MonoBehaviour
    {
        private const string LockedDoorTag = "locked_door";
        private const string ClosedDoorTag = "closed_door";
        private const string OpenedDoorTag = "opened_door";

        [SerializeField] private WorldObjectTags worldObjectTags;
        [SerializeField] private Transform doorPivot;
        [SerializeField] private float closedLocalYAngle = 0f;
        [SerializeField] private float openedLocalYAngle = 90f;
        [SerializeField] private float swingSpeed = 8f;
        [SerializeField] private bool snapOnStart = true;
        [SerializeField] private bool invertSwingDirection;

        private bool warnedMissingTags;
        private bool warnedMissingPivot;
        private bool warnedInvalidState;

        private void Awake()
        {
            ResolveReferences();

            if (snapOnStart)
                ApplyTargetAngle(true);
        }

        private void Start()
        {
            ResolveReferences();

            if (snapOnStart)
                ApplyTargetAngle(true);
        }

        private void Update()
        {
            ApplyTargetAngle(false);
        }

        private void ResolveReferences()
        {
            if (worldObjectTags == null)
                worldObjectTags = GetComponent<WorldObjectTags>();

            if (doorPivot == null)
            {
                Transform foundPivot = transform.Find("DoorVisualPivot");
                if (foundPivot != null)
                    doorPivot = foundPivot;
            }
        }

        private void ApplyTargetAngle(bool snap)
        {
            ResolveReferences();

            if (worldObjectTags == null)
            {
                WarnMissingTagsOnce();
                return;
            }

            if (doorPivot == null)
            {
                WarnMissingPivotOnce();
                return;
            }

            float targetYAngle = GetTargetYAngle();
            Vector3 currentEulerAngles = doorPivot.localEulerAngles;

            if (snap)
            {
                doorPivot.localEulerAngles = new Vector3(currentEulerAngles.x, targetYAngle, currentEulerAngles.z);
                return;
            }

            float speed = Mathf.Max(0f, swingSpeed);
            float nextYAngle = speed <= 0f
                ? targetYAngle
                : Mathf.LerpAngle(currentEulerAngles.y, targetYAngle, Time.deltaTime * speed);

            doorPivot.localEulerAngles = new Vector3(currentEulerAngles.x, nextYAngle, currentEulerAngles.z);
        }

        private float GetTargetYAngle()
        {
            if (worldObjectTags.HasTag(OpenedDoorTag))
                return GetOpenedAngle();

            if (worldObjectTags.HasTag(ClosedDoorTag) || worldObjectTags.HasTag(LockedDoorTag))
                return closedLocalYAngle;

            if (worldObjectTags.RuntimeTags.Length > 0)
                WarnInvalidStateOnce();

            return closedLocalYAngle;
        }

        private float GetOpenedAngle()
        {
            float direction = invertSwingDirection ? -1f : 1f;
            return openedLocalYAngle * direction;
        }

        private void WarnMissingTagsOnce()
        {
            if (warnedMissingTags)
                return;

            warnedMissingTags = true;
            Debug.LogWarning($"[DoorSwingController] '{name}' has no WorldObjectTags reference and none was found on the same GameObject.");
        }

        private void WarnMissingPivotOnce()
        {
            if (warnedMissingPivot)
                return;

            warnedMissingPivot = true;
            Debug.LogWarning($"[DoorSwingController] '{name}' has no doorPivot configured and no child named DoorVisualPivot was found.");
        }

        private void WarnInvalidStateOnce()
        {
            if (warnedInvalidState)
                return;

            warnedInvalidState = true;
            Debug.LogWarning($"[DoorSwingController] '{name}' has no door state tag. Falling back to closed.");
        }
    }
}
