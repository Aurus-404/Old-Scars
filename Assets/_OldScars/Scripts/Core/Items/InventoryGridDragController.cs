using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OldScars.Core.Items
{
    public sealed class InventoryGridDragController
    {
        private const float DragThreshold = 4f;
        private const float DoubleClickSeconds = 0.32f;
        private const float EquipmentHoverSeconds = 0.30f;

        private readonly List<EndpointView> endpoints = new List<EndpointView>(2);
        private readonly List<EquipmentDropTarget> equipmentTargets = new List<EquipmentDropTarget>();
        private readonly List<BlockedTransferRoute> blockedTransferRoutes = new List<BlockedTransferRoute>();
        private readonly Dictionary<IGridStorageOwner, IGridStorageOwner> quickTransferTargets =
            new Dictionary<IGridStorageOwner, IGridStorageOwner>();
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
        private string candidateRejectionMessage;
        private DropIntent dropIntent;
        private string pendingStatusMessage;
        private InventoryToastSeverity pendingStatusSeverity;
        private IGridStorageOwner lastClickOwner;
        private string lastClickInstanceId;
        private float lastClickTime = -10f;
        private EquipmentDropTarget hoveredEquipmentTarget;
        private float equipmentHoverStartedAt;
        private bool equipmentHoverActivated;

        public bool IsDragging => isDragging;
        public IGridStorageOwner ActiveDragSourceOwner => isDragging ? sourceEndpoint?.Owner : null;
        public string ActiveDragSourceInstanceId => isDragging ? sourceInstanceId : null;
        public IGridStorageOwner ActiveOwner { get; private set; }
        public int SelectionVersion { get; private set; }

        public void BeginFrame(GridStorageTransferContext context)
        {
            endpoints.Clear();
            equipmentTargets.Clear();
            blockedTransferRoutes.Clear();
            quickTransferTargets.Clear();
            transferContext = context;
        }

        public void SetQuickTransferTarget(IGridStorageOwner source, IGridStorageOwner target)
        {
            if (source == null || target == null || ReferenceEquals(source, target))
                return;
            quickTransferTargets[source] = target;
        }

        public void BlockTransferRoute(
            IGridStorageOwner first,
            IGridStorageOwner second,
            string message)
        {
            if (first == null || second == null || ReferenceEquals(first, second))
                return;
            blockedTransferRoutes.Add(new BlockedTransferRoute(first, second, message));
        }

        public void RegisterEquipmentDropTarget(
            string slotId,
            Rect rect,
            System.Func<InventoryEquipmentDropRequest, InventoryEquipmentDropResult> onDrop,
            System.Action<string> onHover = null)
        {
            if (string.IsNullOrWhiteSpace(slotId) || onDrop == null)
                return;
            equipmentTargets.Add(new EquipmentDropTarget(slotId, rect, onDrop, onHover));
        }

        public void RegisterEndpoint(IGridStorageOwner owner, InventoryGridDebugView view, Rect rect)
        {
            RegisterEndpoint(owner, view, rect, rect);
        }

        public void RegisterEndpoint(IGridStorageOwner owner, InventoryGridDebugView view, Rect rect, Rect clipRect)
        {
            if (owner == null || view == null)
                return;

            endpoints.Add(new EndpointView(owner, view, rect, clipRect));
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
            equipmentTargets.Clear();
            blockedTransferRoutes.Clear();
            quickTransferTargets.Clear();
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
                SelectionVersion++;
                bool isDoubleClick = endpoints.Count >= 2 && ReferenceEquals(lastClickOwner, endpoint.Owner) &&
                                     lastClickInstanceId == instanceId &&
                                     Time.unscaledTime - lastClickTime <= DoubleClickSeconds;
                if (IsShiftPressed() || isDoubleClick)
                {
                    TransferQuick(endpoint, instanceId);
                    lastClickOwner = null;
                    lastClickInstanceId = null;
                    lastClickTime = -10f;
                    guiEvent.Use();
                    return;
                }

                lastClickOwner = endpoint.Owner;
                lastClickInstanceId = instanceId;
                lastClickTime = Time.unscaledTime;

                sourceEndpoint = endpoint;
                sourceInstanceId = instanceId;
                sourcePlacement = placement;
                pressMousePosition = guiEvent.mousePosition;
                lastMousePosition = guiEvent.mousePosition;
                requestedRotated = placement.IsRotated;
                Rect placementRect = endpoint.View.GetPlacementRect(endpoint.Rect, placement);
                grabbedCellX = Mathf.Clamp(
                    Mathf.FloorToInt((guiEvent.mousePosition.x - placementRect.x) / endpoint.View.CellPitch),
                    0,
                    placement.EffectiveWidth - 1);
                grabbedCellY = Mathf.Clamp(
                    Mathf.FloorToInt((guiEvent.mousePosition.y - placementRect.y) / endpoint.View.CellPitch),
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
            EndpointView target = FindQuickTransferTarget(source);
            if (target == null)
            {
                SetStatus("Elegí explícitamente el inventario de destino.", InventoryToastSeverity.Warning);
                return;
            }

            if (TryGetBlockedTransferMessage(source.Owner, target.Owner, out string blockedMessage))
            {
                SetStatus(blockedMessage, InventoryToastSeverity.Error);
                return;
            }

            GridStorageTransferQuantityPolicy quantityPolicy =
                GridStorageTransferService.GetAutomaticQuantityPolicy(source.Owner, target.Owner);
            InventoryMutationResult result = GridStorageTransferService.TransferStackAuto(
                source.Owner,
                target.Owner,
                instanceId,
                quantityPolicy,
                transferContext);
            SetStatus(
                result.Success
                    ? result.WasLimitedByWeight
                        ? $"Transferidas {result.ActualTransferredQuantity} de {result.RequestedQuantity} unidades por límite de peso."
                        : $"Transferred stack x{result.AffectedQuantity}."
                    : result.Message ?? "No se pudo transferir el stack.",
                result.Success ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
            if (result.Success && result.WasLimitedByWeight && result.SourceRemainingQuantity > 0)
            {
                source.View.ReconcileSelection(source.Owner);
                target.View.ReconcileSelection(target.Owner);
                source.View.SelectInstance(result.SourceInstanceId);
                ActiveOwner = source.Owner;
            }
            else
            {
                ReconcileAfterTransfer(source, target, result);
            }
        }

        private void UpdateCandidate(Vector2 mousePosition)
        {
            EquipmentDropTarget equipmentTarget = FindEquipmentTarget(mousePosition);
            UpdateEquipmentHover(equipmentTarget);
            destinationEndpoint = FindEndpoint(mousePosition);
            hasCandidateCoordinates = false;
            candidatePreview = default;
            mergePreview = default;
            destinationInstanceId = null;
            candidateRejectionMessage = null;
            dropIntent = DropIntent.None;
            if (equipmentTarget != null)
            {
                destinationEndpoint = null;
                return;
            }
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

            if (TryGetBlockedTransferMessage(
                    sourceEndpoint.Owner,
                    destinationEndpoint.Owner,
                    out candidateRejectionMessage))
            {
                dropIntent = DropIntent.Blocked;
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
            EquipmentDropTarget equipmentTarget = FindEquipmentTarget(lastMousePosition);
            if (equipmentTarget != null)
            {
                InventoryEquipmentDropResult result = equipmentTarget.OnDrop(
                    new InventoryEquipmentDropRequest(
                        sourceEndpoint.Owner,
                        sourceInstanceId,
                        sourcePlacement,
                        equipmentTarget.SlotId));
                SetStatus(
                    result.Message ?? (result.Success ? "Equipment drop completed." : "Equipment drop rejected."),
                    result.Success ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
                return;
            }

            if (!hasCandidateCoordinates || destinationEndpoint == null || dropIntent == DropIntent.None)
            {
                SetStatus("Grid move cancelled: invalid destination.", InventoryToastSeverity.Error);
                return;
            }

            if (dropIntent == DropIntent.Blocked)
            {
                SetStatus(
                    candidateRejectionMessage ?? "Esa transferencia directa no está disponible.",
                    InventoryToastSeverity.Error);
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
            GUI.BeginGroup(drawingEndpoint.ClipRect);
            Rect localGridRect = new Rect(
                drawingEndpoint.Rect.x - drawingEndpoint.ClipRect.x,
                drawingEndpoint.Rect.y - drawingEndpoint.ClipRect.y,
                drawingEndpoint.Rect.width,
                drawingEndpoint.Rect.height);
            Vector2 localMousePosition = lastMousePosition - drawingEndpoint.ClipRect.position;
            if (dropIntent == DropIntent.DirectedMerge && destinationEndpoint != null)
            {
                drawingEndpoint.View.DrawMergePreview(
                    destinationEndpoint.Owner,
                    localGridRect,
                    destinationInstanceId,
                    mergePreview);
                GUI.EndGroup();
                return;
            }

            drawingEndpoint.View.DrawDragPreview(
                sourceEndpoint.Owner,
                localGridRect,
                sourceInstanceId,
                localMousePosition,
                grabbedCellX,
                grabbedCellY,
                requestedRotated,
                hasCandidateCoordinates,
                candidateX,
                candidateY,
                candidatePreview);
            GUI.EndGroup();
        }

        private EndpointView FindEndpoint(Vector2 mousePosition)
        {
            for (int index = endpoints.Count - 1; index >= 0; index--)
            {
                if (endpoints[index].ClipRect.Contains(mousePosition) && endpoints[index].Rect.Contains(mousePosition))
                    return endpoints[index];
            }

            return null;
        }

        private EndpointView FindQuickTransferTarget(EndpointView source)
        {
            if (source != null && quickTransferTargets.TryGetValue(source.Owner, out IGridStorageOwner targetOwner))
            {
                for (int index = 0; index < endpoints.Count; index++)
                {
                    if (ReferenceEquals(endpoints[index].Owner, targetOwner))
                        return endpoints[index];
                }
                return null;
            }

            if (endpoints.Count != 2)
                return null;

            for (int index = 0; index < endpoints.Count; index++)
            {
                if (!ReferenceEquals(endpoints[index], source))
                    return endpoints[index];
            }

            return null;
        }

        private bool TryGetBlockedTransferMessage(
            IGridStorageOwner source,
            IGridStorageOwner target,
            out string message)
        {
            for (int index = 0; index < blockedTransferRoutes.Count; index++)
            {
                BlockedTransferRoute route = blockedTransferRoutes[index];
                if (route.Matches(source, target))
                {
                    message = route.Message;
                    return true;
                }
            }

            message = null;
            return false;
        }

        private EquipmentDropTarget FindEquipmentTarget(Vector2 mousePosition)
        {
            for (int index = 0; index < equipmentTargets.Count; index++)
            {
                if (equipmentTargets[index].Rect.Contains(mousePosition))
                    return equipmentTargets[index];
            }
            return null;
        }

        private void UpdateEquipmentHover(EquipmentDropTarget target)
        {
            if (!ReferenceEquals(hoveredEquipmentTarget, target))
            {
                hoveredEquipmentTarget = target;
                equipmentHoverStartedAt = Time.unscaledTime;
                equipmentHoverActivated = false;
            }

            if (target == null || equipmentHoverActivated ||
                Time.unscaledTime - equipmentHoverStartedAt < EquipmentHoverSeconds)
            {
                return;
            }

            equipmentHoverActivated = true;
            target.OnHover?.Invoke(target.SlotId);
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
            candidateRejectionMessage = null;
            dropIntent = DropIntent.None;
            hoveredEquipmentTarget = null;
            equipmentHoverActivated = false;
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
            internal EndpointView(IGridStorageOwner owner, InventoryGridDebugView view, Rect rect, Rect clipRect)
            {
                Owner = owner;
                View = view;
                Rect = rect;
                ClipRect = clipRect;
            }

            internal IGridStorageOwner Owner { get; }
            internal InventoryGridDebugView View { get; }
            internal Rect Rect { get; }
            internal Rect ClipRect { get; }
        }

        private sealed class EquipmentDropTarget
        {
            internal EquipmentDropTarget(
                string slotId,
                Rect rect,
                System.Func<InventoryEquipmentDropRequest, InventoryEquipmentDropResult> onDrop,
                System.Action<string> onHover)
            {
                SlotId = slotId;
                Rect = rect;
                OnDrop = onDrop;
                OnHover = onHover;
            }

            internal string SlotId { get; }
            internal Rect Rect { get; }
            internal System.Func<InventoryEquipmentDropRequest, InventoryEquipmentDropResult> OnDrop { get; }
            internal System.Action<string> OnHover { get; }
        }

        private enum DropIntent
        {
            None,
            Placement,
            DirectedMerge,
            Blocked
        }

        private sealed class BlockedTransferRoute
        {
            internal BlockedTransferRoute(
                IGridStorageOwner first,
                IGridStorageOwner second,
                string message)
            {
                First = first;
                Second = second;
                Message = string.IsNullOrWhiteSpace(message)
                    ? "Esa transferencia directa no está disponible."
                    : message;
            }

            internal IGridStorageOwner First { get; }
            internal IGridStorageOwner Second { get; }
            internal string Message { get; }

            internal bool Matches(IGridStorageOwner source, IGridStorageOwner target)
            {
                return ReferenceEquals(First, source) && ReferenceEquals(Second, target) ||
                       ReferenceEquals(First, target) && ReferenceEquals(Second, source);
            }
        }
    }

    public readonly struct InventoryEquipmentDropRequest
    {
        public InventoryEquipmentDropRequest(
            IGridStorageOwner sourceOwner,
            string sourceInstanceId,
            GridPlacement sourcePlacement,
            string slotId)
        {
            SourceOwner = sourceOwner;
            SourceInstanceId = sourceInstanceId;
            SourcePlacement = sourcePlacement;
            SlotId = slotId;
        }

        public IGridStorageOwner SourceOwner { get; }
        public string SourceInstanceId { get; }
        public GridPlacement SourcePlacement { get; }
        public string SlotId { get; }
    }

    public readonly struct InventoryEquipmentDropResult
    {
        public InventoryEquipmentDropResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public bool Success { get; }
        public string Message { get; }
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
