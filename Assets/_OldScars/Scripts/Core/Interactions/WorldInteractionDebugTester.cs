using System;
using System.Collections.Generic;
using OldScars.Core.Actions;
using OldScars.Core.Actors;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Items;
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
        [SerializeField] private float interactionRange = 2.5f;
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
            {
                HideDebugPanel();
                Debug.LogWarning("[WorldInteractionDebugTester] Context menu rejected: objetivo fuera de alcance.");
                return;
            }

            HideDebugPanel();
            EvaluateAvailableActions(targetTags, hit.collider, mousePosition);
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

        private void EvaluateAvailableActions(WorldObjectTags targetTags, Collider targetCollider, Vector2 mousePosition)
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

            Func<IReadOnlyList<InventoryContextAction>> quickActionsProvider = null;
            Func<InventoryContextAction, bool> quickActionHandler = null;
            ActionDefinition pickupAction = FindAction(availableActions, "pick_up_item");
            WorldItemPickup worldSource = targetTags.GetComponent<WorldItemPickup>() ??
                                          targetTags.GetComponentInChildren<WorldItemPickup>();
            if (pickupAction != null && worldSource != null &&
                worldSource.TryPrepareTransactionSource(out _, out _))
            {
                quickActionsProvider = () => ResolveWorldQuickActions(targetTags, targetCollider, worldSource);
                quickActionHandler = action => TryStartWorldQuickAction(targetTags, targetCollider, worldSource, action);
            }

            debugPanel.ShowActions(
                availableActions,
                targetTags,
                equippedItemDefinitionId,
                mousePosition,
                actorInteractionContext,
                RequiredActionContext,
                quickActionsProvider,
                quickActionHandler);
        }

        private IReadOnlyList<InventoryContextAction> ResolveWorldQuickActions(
            WorldObjectTags targetTags,
            Collider targetCollider,
            WorldItemPickup source)
        {
            if (targetTags == null || source == null ||
                !IsTargetWithinInteractionRange(targetTags, targetCollider) ||
                !TryBuildAvailableActions(targetTags, out List<ActionDefinition> availableActions, out string equippedItemId))
            {
                return Array.Empty<InventoryContextAction>();
            }

            ActionDefinition pickupAction = FindAction(availableActions, "pick_up_item");
            if (pickupAction == null || !source.TryPrepareTransactionSource(out ItemStorageEntry entry, out _))
                return Array.Empty<InventoryContextAction>();

            InventoryComponent inventory = actorInteractionContext.GetInventoryComponent();
            if (inventory == null)
                return Array.Empty<InventoryContextAction>();

            ActorEquipmentComponent equipment = actorInteractionContext.GetComponent<ActorEquipmentComponent>();
            var navigator = new PersonalStorageNavigator(inventory);
            var executionContext = new DebugActionExecutionContext(actorInteractionContext, targetTags, equippedItemId);
            var transferContext = new GridStorageTransferContext(executionContext, pickupAction);
            return InventoryContextActionResolver.ResolveWorldItem(
                source,
                equipment,
                navigator,
                entry.Item.InstanceId,
                transferContext);
        }

        private bool TryStartWorldQuickAction(
            WorldObjectTags targetTags,
            Collider targetCollider,
            WorldItemPickup source,
            InventoryContextAction requestedAction)
        {
            if (targetTags == null || source == null || requestedAction == null)
                return false;

            if (!IsTargetWithinInteractionRange(targetTags, targetCollider))
            {
                Debug.LogWarning("[WorldInteractionDebugTester] World quick action rejected before progress: objetivo fuera de alcance.");
                return false;
            }

            IReadOnlyList<InventoryContextAction> currentActions = ResolveWorldQuickActions(targetTags, targetCollider, source);
            InventoryContextAction currentAction = FindMatchingAction(currentActions, requestedAction);
            if (currentAction == null ||
                !source.TryPrepareTransactionSource(out ItemStorageEntry entry, out _) ||
                !TryBuildAvailableActions(targetTags, out List<ActionDefinition> availableActions, out string equippedItemId))
            {
                Debug.LogWarning("[WorldInteractionDebugTester] World quick action became unavailable before progress started.");
                return false;
            }

            ActionDefinition pickupAction = FindAction(availableActions, "pick_up_item");
            if (pickupAction == null)
                return false;

            InventoryComponent inventory = actorInteractionContext.GetInventoryComponent();
            ActorEquipmentComponent equipment = actorInteractionContext.GetComponent<ActorEquipmentComponent>();
            EquipmentPreview equipPreview = null;
            EquipmentReplacementPlan replacementPlan = null;
            int destinationStorageVersion = 0;
            int destinationLayoutVersion = 0;
            if (currentAction.Kind == InventoryContextActionKind.Equip)
            {
                if (equipment == null)
                    return false;
                equipPreview = WorldItemEquipmentTransactionService.PreviewEquip(
                    equipment,
                    source,
                    entry.Item.InstanceId,
                    currentAction.EquipmentSlotIds);
                if (!equipPreview.Success || equipPreview.RequiresChoice)
                    return false;
            }
            else if (currentAction.Kind == InventoryContextActionKind.EquipReplacing)
            {
                if (equipment == null)
                    return false;
                replacementPlan = WorldItemEquipmentTransactionService.PreviewEquipReplacing(
                    equipment,
                    source,
                    entry.Item.InstanceId,
                    currentAction.EquipmentSlotIds);
                if (!replacementPlan.Success)
                    return false;
            }
            else if (currentAction.Kind == InventoryContextActionKind.MoveToOwnedStorageStack)
            {
                var navigator = new PersonalStorageNavigator(inventory);
                if (!navigator.TryGetOwnedStorage(currentAction.TargetContainerInstanceId, out ItemOwnedStorageRuntime destination) ||
                    !(destination is IGridStorageTransferEndpoint destinationEndpoint))
                {
                    return false;
                }
                destinationStorageVersion = destinationEndpoint.TransferBackend.StorageVersion;
                destinationLayoutVersion = destinationEndpoint.TransferBackend.LayoutVersion;
            }

            if (progressController == null)
                progressController = FindAnyObjectByType<DebugActionProgressController>();
            if (progressController == null)
                return false;

            var selection = new WorldQuickActionSelection(
                targetTags,
                targetCollider,
                source,
                currentAction,
                entry.Item.InstanceId,
                entry.DefinitionId,
                entry.Quantity,
                source.ContentVersion,
                entry.Item.Condition,
                entry.Item.HasOwnedStorage ? entry.Item.OwnedStorage.ContentVersion : 0,
                entry.Item.HasOwnedStorage ? entry.Item.OwnedStorage.LayoutVersion : 0,
                equipPreview,
                replacementPlan,
                destinationStorageVersion,
                destinationLayoutVersion);
            var executionContext = new DebugActionExecutionContext(actorInteractionContext, targetTags, equippedItemId);
            return progressController.TryStartAction(
                pickupAction,
                executionContext,
                currentAction.Label,
                () => ExecuteWorldQuickAction(selection));
        }

        private DebugActionExecutionResult ExecuteWorldQuickAction(WorldQuickActionSelection selection)
        {
            if (selection.Target == null || selection.Source == null)
                return QuickActionFailure("El objeto mundial ya no existe.");

            Collider collider = selection.TargetCollider != null
                ? selection.TargetCollider
                : selection.Source.GetComponentInChildren<Collider>();
            if (!IsTargetWithinInteractionRange(selection.Target, collider))
                return QuickActionFailure("El objeto quedo fuera de alcance.");

            if (!TryBuildAvailableActions(selection.Target, out List<ActionDefinition> availableActions, out string equippedItemId))
                return QuickActionFailure("No se pudo reconstruir el contexto de interacciÃ³n.");

            ActionDefinition pickupAction = FindAction(availableActions, "pick_up_item");
            if (pickupAction == null)
                return QuickActionFailure("Recoger ya no estÃ¡ disponible para este objeto.");

            if (!selection.Source.TryValidateTransactionSource(
                    selection.InstanceId,
                    selection.DefinitionId,
                    selection.Quantity,
                    selection.SourceContentVersion,
                    out ItemStorageEntry currentEntry,
                    out string sourceError))
            {
                return QuickActionFailure(sourceError);
            }

            if (currentEntry.Item.Condition != selection.Condition ||
                (currentEntry.Item.HasOwnedStorage &&
                 (currentEntry.Item.OwnedStorage.ContentVersion != selection.OwnedStorageContentVersion ||
                  currentEntry.Item.OwnedStorage.LayoutVersion != selection.OwnedStorageLayoutVersion)))
            {
                return QuickActionFailure("La condiciÃ³n o el contenido interno del objeto mundial cambiÃ³.");
            }

            IReadOnlyList<InventoryContextAction> currentActions = ResolveWorldQuickActions(selection.Target, collider, selection.Source);
            InventoryContextAction currentAction = FindMatchingAction(currentActions, selection.Action);
            if (currentAction == null)
                return QuickActionFailure("El destino de la acciÃ³n cambiÃ³. IntentÃ¡ nuevamente.");

            if (!IsTargetWithinInteractionRange(selection.Target, collider))
                return QuickActionFailure("El objeto quedo fuera de alcance antes de confirmar la accion.");

            InventoryComponent inventory = actorInteractionContext != null
                ? actorInteractionContext.GetInventoryComponent()
                : null;
            ActorEquipmentComponent equipment = actorInteractionContext != null
                ? actorInteractionContext.GetComponent<ActorEquipmentComponent>()
                : null;
            var executionContext = new DebugActionExecutionContext(actorInteractionContext, selection.Target, equippedItemId);
            var transferContext = new GridStorageTransferContext(executionContext, pickupAction);

            if (currentAction.Kind == InventoryContextActionKind.Equip)
            {
                if (equipment == null)
                    return QuickActionFailure("El actor no tiene equipment disponible.");

                EquipmentMutationResult result = WorldItemEquipmentTransactionService.Equip(
                    equipment,
                    selection.Source,
                    selection.EquipPreview,
                    actorInteractionContext,
                    selection.Target);
                return EquipmentResult(currentAction.Label, result);
            }

            if (currentAction.Kind == InventoryContextActionKind.EquipReplacing)
            {
                if (equipment == null)
                    return QuickActionFailure("El actor no tiene equipment disponible.");

                EquipmentMutationResult result = WorldItemEquipmentTransactionService.EquipReplacing(
                    equipment,
                    selection.Source,
                    selection.ReplacementPlan,
                    actorInteractionContext,
                    selection.Target);
                return EquipmentResult(currentAction.Label, result);
            }

            if (currentAction.Kind == InventoryContextActionKind.MoveToOwnedStorageStack)
            {
                if (inventory == null)
                    return QuickActionFailure("El actor no tiene inventario personal disponible.");

                var navigator = new PersonalStorageNavigator(inventory);
                if (!navigator.TryGetOwnedStorage(currentAction.TargetContainerInstanceId, out ItemOwnedStorageRuntime destination) ||
                    !(selection.Source is IGridStorageTransferEndpoint sourceEndpoint) ||
                    !(destination is IGridStorageTransferEndpoint destinationEndpoint))
                {
                    return QuickActionFailure("El storage destino ya no estÃ¡ accesible.");
                }

                if (destinationEndpoint.TransferBackend.StorageVersion != selection.DestinationStorageVersion ||
                    destinationEndpoint.TransferBackend.LayoutVersion != selection.DestinationLayoutVersion)
                {
                    return QuickActionFailure("El contenido o layout del storage destino cambiÃ³.");
                }

                GridStorageAutoTransferPreview preview = GridStorageTransferService.PreviewTransferQuantityAuto(
                    selection.Source,
                    destination,
                    selection.InstanceId,
                    currentEntry.Quantity,
                    GridStorageTransferQuantityPolicy.Exact,
                    transferContext);
                if (!preview.IsValid || preview.EffectiveQuantity != currentEntry.Quantity)
                    return QuickActionFailure(preview.Message ?? "El stack completo ya no cabe en el storage destino.");

                GridInventoryBackend.BackendStateSnapshot sourceSnapshot = sourceEndpoint.TransferBackend.CaptureBackendState();
                GridInventoryBackend.BackendStateSnapshot destinationSnapshot = destinationEndpoint.TransferBackend.CaptureBackendState();
                ItemInstance sourceItem = currentEntry.Item;
                InventoryMutationResult transfer;
                try
                {
                    transfer = GridStorageTransferService.TransferQuantityAuto(
                        selection.Source,
                        destination,
                        selection.InstanceId,
                        currentEntry.Quantity,
                        true,
                        GridStorageTransferQuantityPolicy.Exact,
                        transferContext);
                }
                catch (Exception exception)
                {
                    RestoreWorldStorageTransfer(
                        selection.Source,
                        destination,
                        sourceEndpoint,
                        destinationEndpoint,
                        sourceSnapshot,
                        destinationSnapshot);
                    return QuickActionFailure($"La transferencia fue revertida: {exception.Message}");
                }

                bool complete = transfer.Success &&
                                transfer.AffectedQuantity == currentEntry.Quantity &&
                                transfer.SourceRemainingQuantity == 0 &&
                                selection.Source.IsTransactionSourceEmpty(selection.InstanceId);
                bool nonStackIdentityPreserved = sourceItem.MaxStack > 1 ||
                                                 destination.TryGetEntryByInstanceId(selection.InstanceId, out _, out _);
                if (!complete || !nonStackIdentityPreserved)
                {
                    RestoreWorldStorageTransfer(
                        selection.Source,
                        destination,
                        sourceEndpoint,
                        destinationEndpoint,
                        sourceSnapshot,
                        destinationSnapshot);
                    return QuickActionFailure(transfer.Message ?? "La transferencia completa fue revertida.");
                }

                return selection.Source.FinalizeCommittedPickup(
                    actorInteractionContext,
                    selection.Target,
                    sourceItem,
                    transfer.AffectedQuantity,
                    currentAction.Label,
                    "Recogiste y guardaste");
            }

            return QuickActionFailure("La acciÃ³n mundial seleccionada no estÃ¡ soportada.");
        }

        private bool TryBuildAvailableActions(
            WorldObjectTags targetTags,
            out List<ActionDefinition> availableActions,
            out string equippedItemDefinitionId)
        {
            availableActions = null;
            equippedItemDefinitionId = null;
            if (targetTags == null || GameDataManager.Instance == null || !GameDataManager.Instance.IsReady ||
                GameDataManager.Instance.Database == null)
            {
                return false;
            }

            if (actorInteractionContext == null)
                actorInteractionContext = FindAnyObjectByType<ActorInteractionContext>();
            if (actorInteractionContext == null)
                return false;

            equippedItemDefinitionId = actorInteractionContext.GetEquippedItemDefinitionId();
            var query = new InteractionQuery
            {
                Database = GameDataManager.Instance.Database,
                ActorTags = actorInteractionContext.ActorTags,
                ActorStats = actorInteractionContext.BuildActorStatsDictionary(),
                EquippedItemId = equippedItemDefinitionId,
                Target = targetTags,
                RequiredContext = RequiredActionContext,
                LogAvailabilityDetails = false
            };
            availableActions = interactionSystem.GetAvailableActions(query);
            return true;
        }

        private static ActionDefinition FindAction(IReadOnlyList<ActionDefinition> actions, string actionId)
        {
            if (actions == null || string.IsNullOrWhiteSpace(actionId))
                return null;

            for (int index = 0; index < actions.Count; index++)
            {
                if (actions[index] != null && actions[index].id == actionId)
                    return actions[index];
            }

            return null;
        }

        private static InventoryContextAction FindMatchingAction(
            IReadOnlyList<InventoryContextAction> actions,
            InventoryContextAction expected)
        {
            if (actions == null || expected == null)
                return null;

            for (int index = 0; index < actions.Count; index++)
            {
                InventoryContextAction candidate = actions[index];
                if (candidate != null && candidate.Kind == expected.Kind &&
                    candidate.TargetContainerInstanceId == expected.TargetContainerInstanceId &&
                    SameValues(candidate.EquipmentSlotIds, expected.EquipmentSlotIds))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool SameValues(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            if ((left?.Count ?? 0) != (right?.Count ?? 0))
                return false;
            for (int index = 0; index < left.Count; index++)
            {
                if (left[index] != right[index])
                    return false;
            }
            return true;
        }

        private static DebugActionExecutionResult EquipmentResult(string title, EquipmentMutationResult result)
        {
            return result.Success
                ? DebugActionExecutionResult.Info(title, result.Message ?? "Equipment actualizado.")
                : QuickActionFailure(result.Message ?? "La transacciÃ³n de equipment fue rechazada.");
        }

        private static DebugActionExecutionResult QuickActionFailure(string message)
        {
            return DebugActionExecutionResult.Info("AcciÃ³n no disponible", message);
        }

        private static void RestoreWorldStorageTransfer(
            WorldItemPickup source,
            ItemOwnedStorageRuntime destination,
            IGridStorageTransferEndpoint sourceEndpoint,
            IGridStorageTransferEndpoint destinationEndpoint,
            GridInventoryBackend.BackendStateSnapshot sourceSnapshot,
            GridInventoryBackend.BackendStateSnapshot destinationSnapshot)
        {
            sourceEndpoint.TransferBackend.RestoreBackendState(sourceSnapshot);
            destinationEndpoint.TransferBackend.RestoreBackendState(destinationSnapshot);
            ItemOwnedStorageRegistry.Instance.ReconcileRestoredOwners(source, destination);
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

        private readonly struct WorldQuickActionSelection
        {
            internal WorldQuickActionSelection(
                WorldObjectTags target,
                Collider targetCollider,
                WorldItemPickup source,
                InventoryContextAction action,
                string instanceId,
                string definitionId,
                int quantity,
                int sourceContentVersion,
                int condition,
                int ownedStorageContentVersion,
                int ownedStorageLayoutVersion,
                EquipmentPreview equipPreview,
                EquipmentReplacementPlan replacementPlan,
                int destinationStorageVersion,
                int destinationLayoutVersion)
            {
                Target = target;
                TargetCollider = targetCollider;
                Source = source;
                Action = action;
                InstanceId = instanceId;
                DefinitionId = definitionId;
                Quantity = quantity;
                SourceContentVersion = sourceContentVersion;
                Condition = condition;
                OwnedStorageContentVersion = ownedStorageContentVersion;
                OwnedStorageLayoutVersion = ownedStorageLayoutVersion;
                EquipPreview = equipPreview;
                ReplacementPlan = replacementPlan;
                DestinationStorageVersion = destinationStorageVersion;
                DestinationLayoutVersion = destinationLayoutVersion;
            }

            internal WorldObjectTags Target { get; }
            internal Collider TargetCollider { get; }
            internal WorldItemPickup Source { get; }
            internal InventoryContextAction Action { get; }
            internal string InstanceId { get; }
            internal string DefinitionId { get; }
            internal int Quantity { get; }
            internal int SourceContentVersion { get; }
            internal int Condition { get; }
            internal int OwnedStorageContentVersion { get; }
            internal int OwnedStorageLayoutVersion { get; }
            internal EquipmentPreview EquipPreview { get; }
            internal EquipmentReplacementPlan ReplacementPlan { get; }
            internal int DestinationStorageVersion { get; }
            internal int DestinationLayoutVersion { get; }
        }
    }
}
