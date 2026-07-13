using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OldScars.Core.Items
{
    public sealed class InventoryGridDragController
    {
        private const float DragThreshold = 4f;

        private readonly List<EndpointView> endpoints = new List<EndpointView>(2);
        private GridStorageTransferContext transferContext;
        private EndpointView sourceEndpoint;
        private EndpointView destinationEndpoint;
        private string sourceInstanceId;
        private GridPlacement sourcePlacement;
        private Vector2 pressMousePosition;
        private Vector2 lastMousePosition;
        private int grabbedCellX;
        private int grabbedCellY;
        private bool requestedRotated;
        private bool isDragging;
        private bool hasCandidateCoordinates;
        private int candidateX;
        private int candidateY;
        private GridPlacementValidationResult candidatePreview;
        private GridStorageMergePreview mergePreview;
        private string destinationInstanceId;
        private DropIntent dropIntent;
        private string pendingStatusMessage;
        private InventoryToastSeverity pendingStatusSeverity;

        public bool IsDragging => isDragging;
        public IGridStorageOwner ActiveOwner { get; private set; }

        public void BeginFrame(GridStorageTransferContext context)
        {
            endpoints.Clear();
            transferContext = context;
        }

        public void RegisterEndpoint(IGridStorageOwner owner, InventoryGridDebugView view, Rect rect)
        {
            if (owner == null || view == null)
                return;

            endpoints.Add(new EndpointView(owner, view, rect));
        }

        public void ProcessOnGUI()
        {
            HandlePointerEvent();
            DrawOverlay();
        }

        public void HandleRotationInput()
        {
            if (!isDragging || sourceEndpoint == null || Keyboard.current == null ||
                !Keyboard.current.rKey.wasPressedThisFrame)
            {
                return;
            }

            if (!sourceEndpoint.Owner.TryGetEntryByInstanceId(sourceInstanceId, out _, out ItemStorageEntry entry) ||
                entry == null || entry.Item == null ||
                !sourceEndpoint.Owner.TryGetGridFootprint(entry.DefinitionId, out GridFootprint footprint, out _))
            {
                SetStatus("Cannot rotate: item or footprint is unavailable.", InventoryToastSeverity.Error);
                return;
            }

            if (footprint.Width == footprint.Height)
            {
                SetStatus("Rotation preview: square footprint unchanged.", InventoryToastSeverity.Warning);
                RefreshCandidate();
                return;
            }

            requestedRotated = !requestedRotated;
            grabbedCellX = Mathf.Clamp(grabbedCellX, 0, footprint.GetWidth(requestedRotated) - 1);
            grabbedCellY = Mathf.Clamp(grabbedCellY, 0, footprint.GetHeight(requestedRotated) - 1);
            SetStatus(
                requestedRotated ? "Rotation preview: rotated." : "Rotation preview: original.",
                InventoryToastSeverity.Warning);
            RefreshCandidate();
        }

        public bool CancelDrag()
        {
            if (!isDragging && sourceEndpoint == null)
                return false;

            ResetPointerState();
            SetStatus("Grid move cancelled.", InventoryToastSeverity.Warning);
            return true;
        }

        public void Reset()
        {
            endpoints.Clear();
            ActiveOwner = null;
            pendingStatusMessage = null;
            ResetPointerState();
        }

        internal bool TryConsumeStatus(out string message, out InventoryToastSeverity severity)
        {
            message = pendingStatusMessage;
            severity = pendingStatusSeverity;
            pendingStatusMessage = null;
            return !string.IsNullOrWhiteSpace(message);
        }

        public bool IsDraggedSource(IGridStorageOwner owner, string instanceId)
        {
            return isDragging && sourceEndpoint != null && ReferenceEquals(sourceEndpoint.Owner, owner) &&
                   sourceInstanceId == instanceId;
        }

        public void SetActiveOwner(IGridStorageOwner owner)
        {
            ActiveOwner = owner;
        }

        private void HandlePointerEvent()
        {
            Event guiEvent = Event.current;
            if (guiEvent == null)
                return;

            if (guiEvent.type == EventType.MouseDown && guiEvent.button == 0)
            {
                EndpointView endpoint = FindEndpoint(guiEvent.mousePosition);
                if (endpoint == null)
                    return;

                string instanceId = endpoint.View.FindInstanceAtPosition(
                    endpoint.Owner,
                    endpoint.Rect,
                    guiEvent.mousePosition,
                    out GridPlacement placement);
                if (string.IsNullOrWhiteSpace(instanceId) || placement == null)
                    return;

                endpoint.View.SelectInstance(instanceId);
                ActiveOwner = endpoint.Owner;
                if (IsShiftPressed() && endpoints.Count == 2)
                {
                    TransferQuick(endpoint, instanceId);
                    guiEvent.Use();
                    return;
                }

                sourceEndpoint = endpoint;
                sourceInstanceId = instanceId;
                sourcePlacement = placement;
                pressMousePosition = guiEvent.mousePosition;
                lastMousePosition = guiEvent.mousePosition;
                requestedRotated = placement.IsRotated;
                Rect placementRect = InventoryGridDebugView.GetPlacementRect(endpoint.Rect, placement);
                grabbedCellX = Mathf.Clamp(
                    Mathf.FloorToInt((guiEvent.mousePosition.x - placementRect.x) / InventoryGridDebugView.CellPitch),
                    0,
                    placement.EffectiveWidth - 1);
                grabbedCellY = Mathf.Clamp(
                    Mathf.FloorToInt((guiEvent.mousePosition.y - placementRect.y) / InventoryGridDebugView.CellPitch),
                    0,
                    placement.EffectiveHeight - 1);
                guiEvent.Use();
                return;
            }

            if (guiEvent.type == EventType.MouseDrag && guiEvent.button == 0 && sourceEndpoint != null)
            {
                lastMousePosition = guiEvent.mousePosition;
                if (!isDragging && (lastMousePosition - pressMousePosition).sqrMagnitude >= DragThreshold * DragThreshold)
                    isDragging = true;

                if (isDragging)
                    UpdateCandidate(lastMousePosition);

                guiEvent.Use();
                return;
            }

            if (guiEvent.type != EventType.MouseUp || guiEvent.button != 0 || sourceEndpoint == null)
                return;

            lastMousePosition = guiEvent.mousePosition;
            if (isDragging)
            {
                UpdateCandidate(lastMousePosition);
                CommitCandidate();
            }

            ResetPointerState();
            guiEvent.Use();
        }

        private void TransferQuick(EndpointView source, string instanceId)
        {
            EndpointView target = FindOtherEndpoint(source);
            if (target == null)
                return;

            InventoryMutationResult result = GridStorageTransferService.TransferStackAuto(
                source.Owner,
                target.Owner,
                instanceId,
                transferContext);
            SetStatus(
                result.Success
                    ? $"Transferred stack x{result.AffectedQuantity}."
                    : result.Message ?? "Stack transfer failed.",
                result.Success ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
            ReconcileAfterTransfer(source, target, result);
        }

        private void UpdateCandidate(Vector2 mousePosition)
        {
            destinationEndpoint = FindEndpoint(mousePosition);
            hasCandidateCoordinates = false;
            candidatePreview = default;
            mergePreview = default;
            destinationInstanceId = null;
            dropIntent = DropIntent.None;
            if (destinationEndpoint == null)
                return;

            if (!destinationEndpoint.View.TryGetCellAtPosition(
                    destinationEndpoint.Owner,
                    destinationEndpoint.Rect,
                    mousePosition,
                    out int hoveredX,
                    out int hoveredY))
            {
                return;
            }

            candidateX = hoveredX - grabbedCellX;
            candidateY = hoveredY - grabbedCellY;
            hasCandidateCoordinates = true;

            if (ReferenceEquals(sourceEndpoint.Owner, destinationEndpoint.Owner))
            {
                dropIntent = DropIntent.Placement;
                candidatePreview = sourceEndpoint.Owner.PreviewGridPlacementMove(
                    sourceInstanceId,
                    candidateX,
                    candidateY,
                    requestedRotated);
                return;
            }

            destinationInstanceId = destinationEndpoint.View.FindInstanceAtCell(
                destinationEndpoint.Owner,
                hoveredX,
                hoveredY,
                out _);
            if (!string.IsNullOrWhiteSpace(destinationInstanceId))
            {
                dropIntent = DropIntent.DirectedMerge;
                mergePreview = GridStorageTransferService.PreviewMergeIntoTarget(
                    sourceEndpoint.Owner,
                    sourceInstanceId,
                    destinationEndpoint.Owner,
                    destinationInstanceId,
                    transferContext);
                return;
            }

            dropIntent = DropIntent.Placement;
            candidatePreview = GridStorageTransferService.PreviewTransferExact(
                sourceEndpoint.Owner,
                destinationEndpoint.Owner,
                sourceInstanceId,
                candidateX,
                candidateY,
                requestedRotated,
                transferContext);
        }

        private void RefreshCandidate()
        {
            if (isDragging)
                UpdateCandidate(lastMousePosition);
        }

        private void CommitCandidate()
        {
            if (!hasCandidateCoordinates || destinationEndpoint == null || dropIntent == DropIntent.None)
            {
                SetStatus("Grid move cancelled: invalid destination.", InventoryToastSeverity.Error);
                return;
            }

            if (dropIntent == DropIntent.DirectedMerge)
            {
                if (!mergePreview.IsValid)
                {
                    SetStatus(
                        mergePreview.Message ?? "Directed merge failed.",
                        InventoryToastSeverity.Error);
                    return;
                }

                InventoryMutationResult mergeResult = GridStorageTransferService.MergeIntoTarget(
                    sourceEndpoint.Owner,
                    sourceInstanceId,
                    destinationEndpoint.Owner,
                    destinationInstanceId,
                    transferContext);
                SetStatus(
                    mergeResult.Success
                        ? $"Merged +{mergeResult.AffectedQuantity}."
                        : mergeResult.Message ?? "Directed merge failed.",
                    mergeResult.Success ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
                ReconcileAfterTransfer(sourceEndpoint, destinationEndpoint, mergeResult);
                return;
            }

            if (!candidatePreview.IsValid)
            {
                SetStatus(
                    candidatePreview.Message ?? "Grid move cancelled: invalid destination.",
                    InventoryToastSeverity.Error);
                return;
            }

            if (ReferenceEquals(sourceEndpoint.Owner, destinationEndpoint.Owner))
            {
                InventoryMutationResult moveResult = sourceEndpoint.Owner.MoveGridPlacement(
                    sourceInstanceId,
                    candidateX,
                    candidateY,
                    requestedRotated);
                SetStatus(
                    moveResult.Success
                        ? $"Moved item to ({candidateX},{candidateY}){(requestedRotated ? " rotated" : string.Empty)}."
                        : moveResult.Message ?? "Grid move failed.",
                    moveResult.Success ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
                sourceEndpoint.View.ReconcileSelection(sourceEndpoint.Owner);
                return;
            }

            InventoryMutationResult transferResult = GridStorageTransferService.TransferExact(
                sourceEndpoint.Owner,
                destinationEndpoint.Owner,
                sourceInstanceId,
                candidateX,
                candidateY,
                requestedRotated,
                transferContext);
            SetStatus(
                transferResult.Success
                    ? $"Transferred stack x{transferResult.AffectedQuantity} to ({candidateX},{candidateY})."
                    : transferResult.Message ?? "Exact grid transfer failed.",
                transferResult.Success ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
            ReconcileAfterTransfer(sourceEndpoint, destinationEndpoint, transferResult);
        }

        private void ReconcileAfterTransfer(
            EndpointView source,
            EndpointView target,
            InventoryMutationResult result)
        {
            source.View.ReconcileSelection(source.Owner);
            target.View.ReconcileSelection(target.Owner);
            if (result.Success && !string.IsNullOrWhiteSpace(result.DestinationInstanceId))
            {
                target.View.SelectInstance(result.DestinationInstanceId);
                ActiveOwner = target.Owner;
            }
        }

        private void DrawOverlay()
        {
            if (!isDragging || sourceEndpoint == null)
                return;

            EndpointView drawingEndpoint = destinationEndpoint ?? sourceEndpoint;
            if (dropIntent == DropIntent.DirectedMerge && destinationEndpoint != null)
            {
                drawingEndpoint.View.DrawMergePreview(
                    destinationEndpoint.Owner,
                    destinationEndpoint.Rect,
                    destinationInstanceId,
                    mergePreview);
                return;
            }

            drawingEndpoint.View.DrawDragPreview(
                sourceEndpoint.Owner,
                drawingEndpoint.Rect,
                sourceInstanceId,
                lastMousePosition,
                grabbedCellX,
                grabbedCellY,
                requestedRotated,
                hasCandidateCoordinates,
                candidateX,
                candidateY,
                candidatePreview);
        }

        private EndpointView FindEndpoint(Vector2 mousePosition)
        {
            for (int index = 0; index < endpoints.Count; index++)
            {
                if (endpoints[index].Rect.Contains(mousePosition))
                    return endpoints[index];
            }

            return null;
        }

        private EndpointView FindOtherEndpoint(EndpointView source)
        {
            for (int index = 0; index < endpoints.Count; index++)
            {
                if (!ReferenceEquals(endpoints[index], source))
                    return endpoints[index];
            }

            return null;
        }

        private void ResetPointerState()
        {
            sourceEndpoint = null;
            destinationEndpoint = null;
            sourceInstanceId = null;
            sourcePlacement = null;
            isDragging = false;
            hasCandidateCoordinates = false;
            candidatePreview = default;
            mergePreview = default;
            destinationInstanceId = null;
            dropIntent = DropIntent.None;
        }

        private void SetStatus(string message, InventoryToastSeverity severity)
        {
            pendingStatusMessage = message;
            pendingStatusSeverity = severity;
        }

        private static bool IsShiftPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        }

        private sealed class EndpointView
        {
            internal EndpointView(IGridStorageOwner owner, InventoryGridDebugView view, Rect rect)
            {
                Owner = owner;
                View = view;
                Rect = rect;
            }

            internal IGridStorageOwner Owner { get; }
            internal InventoryGridDebugView View { get; }
            internal Rect Rect { get; }
        }

        private enum DropIntent
        {
            None,
            Placement,
            DirectedMerge
        }
    }

    internal enum InventoryToastSeverity
    {
        Success,
        Warning,
        Error
    }

    internal sealed class InventoryDebugToast
    {
        private const float DurationSeconds = 1.75f;
        private const float ToastHeight = 34f;
        private const float BottomInset = 12f;
        private const float HorizontalInset = 12f;
        private const float MaxToastWidth = 440f;

        private string message;
        private InventoryToastSeverity severity;
        private float expiresAt;

        internal void Show(string value, InventoryToastSeverity valueSeverity)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            float now = Time.unscaledTime;
            if (message == value && severity == valueSeverity && now < expiresAt)
                return;

            message = value;
            severity = valueSeverity;
            expiresAt = now + DurationSeconds;
        }

        internal void Clear()
        {
            message = null;
            expiresAt = 0f;
        }

        internal void Draw(Rect windowRect)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            if (Time.unscaledTime >= expiresAt)
            {
                Clear();
                return;
            }

            float width = Mathf.Max(1f, Mathf.Min(MaxToastWidth, windowRect.width - HorizontalInset * 2f));
            var toastRect = new Rect(
                Mathf.Max(HorizontalInset, (windowRect.width - width) * 0.5f),
                Mathf.Max(0f, windowRect.height - ToastHeight - BottomInset),
                width,
                ToastHeight);

            Color previousColor = GUI.color;
            GUI.color = GetBackgroundColor(severity);
            GUI.Box(toastRect, GUIContent.none);
            GUI.color = previousColor;

            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = Color.white;
            GUI.Label(toastRect, message, labelStyle);
        }

        private static Color GetBackgroundColor(InventoryToastSeverity value)
        {
            switch (value)
            {
                case InventoryToastSeverity.Success:
                    return new Color(0.08f, 0.48f, 0.18f, 0.94f);
                case InventoryToastSeverity.Warning:
                    return new Color(0.62f, 0.42f, 0.05f, 0.94f);
                default:
                    return new Color(0.62f, 0.1f, 0.1f, 0.94f);
            }
        }
    }
}
