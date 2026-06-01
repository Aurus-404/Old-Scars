using System.Collections.Generic;
using OldScars.Core.Actions;
using OldScars.Core.Data.Definitions;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OldScars.Core.Interactions
{
    public sealed class WorldInteractionDebugTester : MonoBehaviour
    {
        private const string RequiredActionContext = "world_interaction";

        [SerializeField] private ContextualActionDebugPanel debugPanel;
        [SerializeField] private DebugActionProgressController progressController;
        [SerializeField] private DebugActionAvailabilityPanel availabilityPanel;
        [SerializeField] private DebugWorldUiInputBlocker uiInputBlocker;
        [SerializeField] private ActorInteractionContext actorInteractionContext;
        [SerializeField] private LayerMask interactableLayerMask;
        [SerializeField] private float maxRayDistance = 1000f;
        [SerializeField] private float interactionRange = 3f;
        [SerializeField] private float rightDragThresholdPixels = 8f;
        [SerializeField] private bool logAvailabilityDetails = false;
        [SerializeField] private bool showAvailabilityDiagnostics = true;

        private readonly InteractionSystem interactionSystem = new InteractionSystem();

        private Vector2 rightMouseDownPosition;
        private bool rightMouseIsDown;
        private bool rightMouseDragged;

        private void Awake()
        {
            if (debugPanel == null)
                debugPanel = FindAnyObjectByType<ContextualActionDebugPanel>();

            if (progressController == null)
                progressController = FindAnyObjectByType<DebugActionProgressController>();

            if (availabilityPanel == null)
                availabilityPanel = FindAnyObjectByType<DebugActionAvailabilityPanel>();

            if (uiInputBlocker == null)
                uiInputBlocker = FindAnyObjectByType<DebugWorldUiInputBlocker>();

            if (actorInteractionContext == null)
                actorInteractionContext = FindAnyObjectByType<ActorInteractionContext>();

            if (interactableLayerMask.value == 0)
                interactableLayerMask = LayerMask.GetMask("Interactable");
        }

        private void Update()
        {
            if (Mouse.current == null)
                return;

            HandleRightMouseInput();
        }

        private void HandleRightMouseInput()
        {
            Vector2 mousePosition = Mouse.current.position.ReadValue();

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                rightMouseDownPosition = mousePosition;
                rightMouseIsDown = true;
                rightMouseDragged = false;
                return;
            }

            if (rightMouseIsDown && Mouse.current.rightButton.isPressed)
            {
                if ((mousePosition - rightMouseDownPosition).sqrMagnitude >= rightDragThresholdPixels * rightDragThresholdPixels)
                    rightMouseDragged = true;
            }

            if (!Mouse.current.rightButton.wasReleasedThisFrame)
                return;

            bool shouldOpenContextMenu = rightMouseIsDown && !rightMouseDragged;
            rightMouseIsDown = false;
            rightMouseDragged = false;

            if (shouldOpenContextMenu)
                TryOpenContextMenu(mousePosition);
        }

        private void TryOpenContextMenu(Vector2 mousePosition)
        {
            if (uiInputBlocker == null)
                uiInputBlocker = FindAnyObjectByType<DebugWorldUiInputBlocker>();

            if (uiInputBlocker != null && uiInputBlocker.IsPointerOverBlockingPanel(mousePosition))
                return;

            if (IsActionInProgress())
            {
                Debug.Log("[WorldInteractionDebugTester] Context menu blocked because a debug action is in progress.");
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("[WorldInteractionDebugTester] Camera.main was not found.");
                return;
            }

            if (interactableLayerMask.value == 0)
            {
                Debug.LogWarning("[WorldInteractionDebugTester] Interactable layer mask is not configured. Assign the Interactable layer to contextual interaction raycasts.");
                return;
            }

            Ray ray = mainCamera.ScreenPointToRay(new Vector3(mousePosition.x, mousePosition.y, 0f));

            if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, interactableLayerMask, QueryTriggerInteraction.Ignore))
                return;

            WorldObjectTags targetTags = hit.collider.GetComponentInParent<WorldObjectTags>();
            if (targetTags == null)
                return;

            if (!IsTargetWithinInteractionRange(targetTags, hit.collider))
                return;

            HideDebugPanel();
            EvaluateAvailableActions(targetTags, mousePosition);
        }

        private bool IsTargetWithinInteractionRange(WorldObjectTags targetTags, Collider targetCollider)
        {
            if (actorInteractionContext == null)
                actorInteractionContext = FindAnyObjectByType<ActorInteractionContext>();

            if (actorInteractionContext == null)
            {
                Debug.LogError("[WorldInteractionDebugTester] ActorInteractionContext was not found in the scene. Add ActorInteractionContext to the debug actor before evaluating world interactions.");
                return false;
            }

            Vector3 actorPosition = actorInteractionContext.transform.position;
            Vector3 targetPoint = targetCollider != null
                ? targetCollider.ClosestPoint(actorPosition)
                : targetTags.transform.position;

            float range = Mathf.Max(0f, interactionRange);
            return (targetPoint - actorPosition).sqrMagnitude <= range * range;
        }

        private void EvaluateAvailableActions(WorldObjectTags targetTags, Vector2 mousePosition)
        {
            if (GameDataManager.Instance == null)
            {
                Debug.LogError("[WorldInteractionDebugTester] GameDataManager.Instance was not found in the scene.");
                return;
            }

            if (!GameDataManager.Instance.IsReady)
            {
                Debug.LogError("[WorldInteractionDebugTester] GameDataManager is not ready. CoreDataSystem did not finish loading successfully.");
                return;
            }

            if (actorInteractionContext == null)
                actorInteractionContext = FindAnyObjectByType<ActorInteractionContext>();

            if (actorInteractionContext == null)
            {
                Debug.LogError("[WorldInteractionDebugTester] ActorInteractionContext was not found in the scene. Add ActorInteractionContext to the debug actor before evaluating world interactions.");
                return;
            }

            string equippedItemDefinitionId = actorInteractionContext.GetEquippedItemDefinitionId();
            Dictionary<string, float> actorStats = actorInteractionContext.BuildActorStatsDictionary();

            var query = new InteractionQuery
            {
                Database = GameDataManager.Instance.Database,
                ActorTags = actorInteractionContext.ActorTags,
                ActorStats = actorStats,
                EquippedItemId = equippedItemDefinitionId,
                Target = targetTags,
                RequiredContext = RequiredActionContext,
                LogAvailabilityDetails = logAvailabilityDetails
            };

            List<ActionDefinition> availableActions = interactionSystem.GetAvailableActions(query);

            if (showAvailabilityDiagnostics)
                ShowAvailabilityDiagnostics(interactionSystem.GetAvailabilityDiagnostics(query));

            if (debugPanel == null)
                debugPanel = FindAnyObjectByType<ContextualActionDebugPanel>();

            if (debugPanel == null)
            {
                Debug.LogError("[WorldInteractionDebugTester] ContextualActionDebugPanel was not found in the scene.");
                return;
            }

            debugPanel.ShowActions(availableActions, targetTags, equippedItemDefinitionId, mousePosition, actorInteractionContext, RequiredActionContext);
        }

        private void ShowAvailabilityDiagnostics(ActionAvailabilityDiagnosticReport report)
        {
            if (availabilityPanel == null)
                availabilityPanel = FindAnyObjectByType<DebugActionAvailabilityPanel>();

            if (availabilityPanel != null)
                availabilityPanel.SetReport(report);
        }

        private void HideDebugPanel()
        {
            if (debugPanel == null)
                debugPanel = FindAnyObjectByType<ContextualActionDebugPanel>();

            if (debugPanel != null)
                debugPanel.Hide();
        }

        private bool IsActionInProgress()
        {
            if (progressController == null)
                progressController = FindAnyObjectByType<DebugActionProgressController>();

            return progressController != null && progressController.IsActionInProgress;
        }
    }
}
