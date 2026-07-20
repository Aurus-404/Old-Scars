using System.Collections.Generic;
using OldScars.Core.Actors;
using OldScars.Core.Combat;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using UnityEngine;

namespace OldScars.Core.Items
{
    /// <summary>
    /// OnGUI inventory panel for the playable debug loop.
    ///
    /// This is not final inventory UI. M33.1 adds a visual grid and manual
    /// placement testing while keeping the legacy list as a debug fallback.
    /// </summary>
    public sealed class InventoryDebugPanel : MonoBehaviour
    {
        private const float PanelWidth = 1180f;
        private const float PanelHeight = 660f;
        private const float PlayerColumnWidth = 292f;
        private const float EquipmentColumnWidth = 330f;
        private const float ColumnGap = 8f;
        private const float BodyVerticalReserve = 46f;
        private const float MinimumEquipmentViewportHeight = 300f;
        private const float MaximumEquipmentViewportHeight = 350f;
        private const float ScrollbarSize = 16f;

        [SerializeField, Range(20f, 64f)] private float gridVisualCellSize = 32f;

        [SerializeField] private InventoryComponent inventory;
        [SerializeField] private ActorNeedsComponent actorNeeds;
        [SerializeField] private ActorHealthComponent actorHealth;
        [SerializeField] private FirearmDebugController firearmController;
        [SerializeField] private ActorEquipmentComponent actorEquipment;

        private bool isVisible;
        private Vector2 scrollPosition;
        private Vector2 detailsScrollPosition;
        private bool showLegacyList;
        private readonly InventoryGridDebugView gridView = new InventoryGridDebugView();
        private readonly InventoryGridDragController dragController = new InventoryGridDragController();
        private readonly InventoryDebugToast toast = new InventoryDebugToast();
        private readonly EquipmentDebugListView equipmentListView = new EquipmentDebugListView();
        private readonly OwnedStorageInspectionDebugView inspectionView = new OwnedStorageInspectionDebugView();
        private InventoryUISessionController sessionController;
        private int observedGridSelectionVersion;
        private Vector2 gridScrollPosition;
        private PersonalStorageNavigator personalStorageNavigator;

        public bool IsVisible => isVisible;
        public InventoryComponent Inventory => inventory;

        private void Awake()
        {
            ResolveSessionController();
            ResolveActorNeeds();
            ResolveActorHealth();
            ResolveFirearmController();
            ResolveActorEquipment();
        }

        private void OnEnable()
        {
            ResolveSessionController();
            ResolveActorNeeds();
            ResolveActorHealth();
            ResolveFirearmController();
            ResolveActorEquipment();
        }

        public void Hide()
        {
            if (sessionController != null && sessionController.IsOpen)
                sessionController.CloseSession();
            else
                HideFromSession();
        }

        internal void BindSessionController(InventoryUISessionController controller)
        {
            sessionController = controller;
        }

        internal void ShowFromSession()
        {
            scrollPosition = Vector2.zero;
            detailsScrollPosition = Vector2.zero;
            toast.Clear();
            gridScrollPosition = Vector2.zero;
            gridView.SetVisualCellSize(gridVisualCellSize);
            inspectionView.Reset(gridVisualCellSize);
            personalStorageNavigator = sessionController != null
                ? sessionController.PersonalStorageNavigator
                : new PersonalStorageNavigator(inventory);
            isVisible = true;
        }

        internal void HideFromSession()
        {
            isVisible = false;
            gridView.Reset();
            dragController.Reset();
            inspectionView.Reset(gridVisualCellSize);
            toast.Clear();
        }

        internal bool CancelActiveDrag()
        {
            return dragController.CancelDrag();
        }

        internal void HandleRotationInput()
        {
            dragController.HandleRotationInput();
        }

        public bool ContainsScreenPosition(Vector2 screenPosition)
        {
            if (!isVisible)
                return false;

            Vector2 guiPoint = ToGuiPosition(screenPosition);
            return GetPanelRect().Contains(guiPoint);
        }

        private void OnGUI()
        {
            if (!isVisible)
                return;

            Rect panelRect = GetPanelRect();
            GUILayout.BeginArea(panelRect, GUI.skin.box);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !(sessionController?.BlocksInventoryContentInput ?? false);
            DrawHeader();

            if (inventory == null)
            {
                GUILayout.Label("No InventoryComponent assigned.");
                DrawCloseButton();
                ConsumeDragStatus();
                toast.Draw(new Rect(0f, 0f, panelRect.width, panelRect.height));
                GUI.enabled = previousEnabled;
                sessionController?.DrawContextOverlay(new Rect(0f, 0f, panelRect.width, panelRect.height));
                GUILayout.EndArea();
                sessionController?.ConsumeCurrentOnGUIEvent();
                return;
            }

            DrawThreeColumnBody(Mathf.Max(1f, panelRect.height - BodyVerticalReserve));

            ConsumeDragStatus();
            toast.Draw(new Rect(0f, 0f, panelRect.width, panelRect.height));
            GUI.enabled = previousEnabled;
            sessionController?.DrawContextOverlay(new Rect(0f, 0f, panelRect.width, panelRect.height));
            GUILayout.EndArea();
            sessionController?.ConsumeCurrentOnGUIEvent();
        }

        private void DrawThreeColumnBody(float bodyHeight)
        {
            dragController.BeginFrame(default);
            personalStorageNavigator?.Refresh();
            GUILayout.BeginHorizontal(GUILayout.Height(bodyHeight));

            DrawPlayerColumn(bodyHeight);

            GUILayout.Space(ColumnGap);
            DrawEquipmentColumn(bodyHeight);

            GUILayout.Space(ColumnGap);
            DrawDetailsColumn(bodyHeight);

            GUILayout.EndHorizontal();
            DrawOwnedStorageInspection();
            if (!(sessionController?.BlocksInventoryContentInput ?? false))
                dragController.ProcessOnGUI();
            SyncGridSelectionToSession();
        }

        private void DrawOwnedStorageInspection()
        {
            sessionController?.ValidateOwnedStorageInspection();
            ItemOwnedStorageRuntime inspected = sessionController?.InspectedOwnedStorage;
            if (inspected == null)
                return;

            Rect panelRect = GetPanelRect();
            var rect = new Rect(panelRect.width - 360f, 42f, 344f, Mathf.Min(570f, panelRect.height - 58f));
            inspectionView.Draw(
                rect,
                inspected,
                dragController,
                out bool closeRequested,
                out string rightClickedInstanceId,
                out Vector2 rightClickPosition);
            if (closeRequested)
            {
                sessionController.CloseOwnedStorageInspection();
                return;
            }

            if (string.IsNullOrWhiteSpace(rightClickedInstanceId) || sessionController.QuantityDialogOpen ||
                !inspected.TryGetEntryByInstanceId(rightClickedInstanceId, out _, out ItemStorageEntry entry) ||
                entry?.Item == null)
            {
                return;
            }

            if (dragController.CancelDrag())
            {
                sessionController.CloseContextMenu();
                Event.current?.Use();
                return;
            }

            inspectionView.GridView.SelectInstance(rightClickedInstanceId);
            IReadOnlyList<InventoryContextAction> actions = InventoryContextActionResolver.ResolvePersonalCompartment(
                inspected,
                inventory,
                actorEquipment,
                personalStorageNavigator,
                rightClickedInstanceId,
                false);
            sessionController.OpenContextMenu(
                new InventoryContextMenuRequest(
                    InventoryContextSourceKind.InspectedOwnedStorage,
                    inspected,
                    actorEquipment,
                    rightClickedInstanceId,
                    null,
                    entry.Quantity,
                    actions,
                    ExecuteContextAction),
                rightClickPosition);
            Event.current?.Use();
        }

        private void DrawPlayerColumn(float bodyHeight)
        {
            GUILayout.BeginVertical(
                GUI.skin.box,
                GUILayout.Width(PlayerColumnWidth),
                GUILayout.Height(bodyHeight));
            GUILayout.Label("Player Grid (drag; R rotates)");
            DrawPersonalStorageSelector();

            IGridStorageOwner owner = GetActivePersonalOwner();

            GUILayout.BeginHorizontal();
            GUILayout.Label(
                owner != null && owner.UsesGridLayout
                    ? $"Grid: {owner.GridWidth}x{owner.GridHeight} | cell {gridVisualCellSize:0}px"
                    : "Grid inactive",
                GUILayout.ExpandWidth(true));
            if (GUILayout.Button(showLegacyList ? "Grid" : "Legacy", GUILayout.Width(76f), GUILayout.Height(24f)))
                showLegacyList = !showLegacyList;
            GUILayout.EndHorizontal();

            if (owner == null || showLegacyList || !owner.UsesGridLayout)
            {
                DrawLegacyStorage(owner, bodyHeight - 98f);
            }
            else
            {
                DrawScrollableGrid(owner, bodyHeight - 98f);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }

        private void DrawEquipmentColumn(float bodyHeight)
        {
            ResolveActorEquipment();
            GUILayout.BeginVertical(
                GUI.skin.box,
                GUILayout.Width(EquipmentColumnWidth),
                GUILayout.Height(bodyHeight));

            float equipmentHeight = Mathf.Clamp(
                bodyHeight * 0.56f,
                MinimumEquipmentViewportHeight,
                MaximumEquipmentViewportHeight);
            equipmentListView.Draw(
                actorEquipment,
                sessionController != null ? sessionController.Selection : new InventoryUISessionSelection(),
                EquipmentColumnWidth - 12f,
                equipmentHeight,
                HandleEquipmentRowClick,
                RegisterEquipmentDropTarget,
                EquipmentDebugListPresentation.OccupiedItemsOnly);

            GUILayout.Space(ColumnGap);
            DrawPersonalSessionFooter(Mathf.Max(1f, bodyHeight - equipmentHeight - ColumnGap - 12f));
            GUILayout.EndVertical();
        }

        private void DrawDetailsColumn(float bodyHeight)
        {
            GUILayout.BeginVertical(
                GUI.skin.box,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(bodyHeight));
            detailsScrollPosition.x = 0f;
            detailsScrollPosition = GUILayout.BeginScrollView(
                detailsScrollPosition,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            detailsScrollPosition.x = 0f;
            DrawSelectedItemDetails();
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawPersonalSessionFooter(float footerHeight)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Height(footerHeight));
            GUILayout.Label("Inventory Session");
            DrawCarryWeightSection();
            GUILayout.Label("Shift+click: transfer stack");
            GUILayout.Label("Drag: move/merge | R: rotate");
            DrawFirearmSection();
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }

        private void SyncGridSelectionToSession()
        {
            if (dragController.SelectionVersion == observedGridSelectionVersion)
                return;

            observedGridSelectionVersion = dragController.SelectionVersion;
            if (!string.IsNullOrWhiteSpace(gridView.SelectedInstanceId))
                sessionController?.Selection.SelectPersonal(gridView.SelectedInstanceId);
        }

        private void HandleGridRightClick(
            IGridStorageOwner owner,
            InventoryGridDebugView view,
            Rect gridRect)
        {
            if (sessionController == null || sessionController.QuantityDialogOpen ||
                !view.TryGetRightClick(owner, gridRect, out string instanceId))
            {
                return;
            }

            Event guiEvent = Event.current;
            if (dragController.CancelDrag())
            {
                sessionController.CloseContextMenu();
                guiEvent?.Use();
                return;
            }

            if (string.IsNullOrWhiteSpace(instanceId) ||
                !owner.TryGetEntryByInstanceId(instanceId, out _, out ItemStorageEntry entry) ||
                entry?.Item == null)
            {
                sessionController.CloseContextMenu();
                guiEvent?.Use();
                return;
            }

            view.SelectInstance(instanceId);
            dragController.SetActiveOwner(owner);
            sessionController.Selection.SelectPersonalFromContext(instanceId);
            ResolveActorEquipment();
            IReadOnlyList<InventoryContextAction> actions = InventoryContextActionResolver.ResolvePersonalCompartment(
                owner,
                inventory,
                actorEquipment,
                personalStorageNavigator,
                instanceId,
                false);
            sessionController.OpenContextMenu(
                new InventoryContextMenuRequest(
                    InventoryContextSourceKind.Personal,
                    owner,
                    actorEquipment,
                    instanceId,
                    null,
                    entry.Quantity,
                    actions,
                    ExecuteContextAction),
                guiEvent != null ? ToPanelLocalPosition(guiEvent.mousePosition) : Vector2.zero);
            guiEvent?.Use();
        }

        private void HandleEquipmentRowClick(EquipmentDebugRowClick click)
        {
            if (click.MouseButton != 1 || sessionController == null || sessionController.QuantityDialogOpen)
                return;

            Event guiEvent = Event.current;
            if (dragController.CancelDrag())
            {
                sessionController.CloseContextMenu();
                return;
            }

            if (string.IsNullOrWhiteSpace(click.InstanceId))
            {
                sessionController.CloseContextMenu();
                return;
            }

            ResolveActorEquipment();
            if (actorEquipment == null ||
                !actorEquipment.TryGetEntryByInstanceId(click.InstanceId, out ItemStorageEntry entry) ||
                entry?.Item == null)
            {
                sessionController.CloseContextMenu();
                return;
            }

            sessionController.Selection.SelectEquipmentFromContext(click.SlotId, click.InstanceId);

            IReadOnlyList<InventoryContextAction> actions = InventoryContextActionResolver.ResolveEquipment(
                actorEquipment,
                click.SlotId,
                click.InstanceId,
                personalStorageNavigator,
                null,
                default);
            IReadOnlyList<string> occupiedSlots = actorEquipment.GetSlotsOccupiedBy(click.InstanceId);
            sessionController.OpenContextMenu(
                new InventoryContextMenuRequest(
                    InventoryContextSourceKind.Equipment,
                    null,
                    actorEquipment,
                    click.InstanceId,
                    click.SlotId,
                    entry.Quantity,
                    actions,
                    ExecuteContextAction,
                    occupiedSlots),
                guiEvent != null ? ToPanelLocalPosition(guiEvent.mousePosition) : click.RowRect.position);
        }

        private void RegisterEquipmentDropTarget(string slotId, Rect rowRect)
        {
            rowRect.position -= GetPanelRect().position;
            dragController.RegisterEquipmentDropTarget(
                slotId,
                rowRect,
                HandleEquipmentDrop);
        }

        private InventoryEquipmentDropResult HandleEquipmentDrop(InventoryEquipmentDropRequest request)
        {
            ResolveActorEquipment();
            if (actorEquipment == null || request.SourceOwner == null)
            {
                return new InventoryEquipmentDropResult(false, "El origen o equipment ya no está disponible.");
            }

            bool actorOwned = ItemOwnedStorageRegistry.Instance.ShareRootOwner(request.SourceOwner, inventory);
            if (actorOwned && TryGetCompatibleSlotSet(request.SourceOwner, request.SourceInstanceId, request.SlotId, out string[] slotSet))
            {
                EquipmentPreview preview = actorEquipment.PreviewEquip(request.SourceOwner, request.SourceInstanceId, slotSet);
                EquipmentMutationResult result;
                if (preview.FailureCode == EquipmentFailureCode.SlotOccupied)
                {
                    EquipmentReplacementPlan plan = actorEquipment.PreviewEquipReplacing(
                        request.SourceOwner,
                        request.SourceInstanceId,
                        slotSet);
                    result = actorEquipment.EquipReplacing(request.SourceOwner, plan);
                }
                else
                {
                    result = actorEquipment.Equip(request.SourceOwner, preview);
                }

                if (!result.Success)
                {
                    return new InventoryEquipmentDropResult(
                        false,
                        EquipmentFailureMessageFormatter.FormatFailure(result.FailureCode, actorEquipment, slotSet));
                }

                personalStorageNavigator?.Refresh();
                string primarySlot = result.SlotIds.Length > 0 ? result.SlotIds[0] : request.SlotId;
                sessionController?.Selection.SelectEquipmentFromContext(primarySlot, result.InstanceId, true);
                gridView.ReconcileSelection(GetActivePersonalOwner());
                return new InventoryEquipmentDropResult(
                    true,
                    EquipmentFailureMessageFormatter.FormatSuccess(actorEquipment, result.InstanceId, false, preview.FailureCode == EquipmentFailureCode.SlotOccupied));
            }

            ItemStorageEntry occupant = actorEquipment.GetEquippedStorageEntry(request.SlotId);
            if (occupant?.Item?.HasOwnedStorage != true)
                return new InventoryEquipmentDropResult(false, actorOwned
                    ? "El objeto no es compatible con ese slot."
                    : "No podés equipar directamente un objeto que no pertenece al actor.");

            InventoryMutationResult transfer = GridStorageTransferService.TransferStackAuto(
                request.SourceOwner,
                occupant.Item.OwnedStorage,
                request.SourceInstanceId,
                GridStorageTransferQuantityPolicy.Exact,
                default);
            if (!transfer.Success)
                return new InventoryEquipmentDropResult(false, transfer.Message ?? "No se pudo guardar el objeto.");

            IGridStorageOwner visibleOwner = GetActivePersonalOwner();
            gridView.ReconcileSelection(visibleOwner);
            if (ReferenceEquals(request.SourceOwner, visibleOwner) &&
                !request.SourceOwner.TryGetEntryByInstanceId(request.SourceInstanceId, out _, out _))
            {
                sessionController?.Selection.ClearPersonalIfMissing(request.SourceInstanceId);
            }
            return new InventoryEquipmentDropResult(true, "Objeto guardado en el compartimento equipado.");
        }

        private void HandleEquipmentHover(string slotId)
        {
            IGridStorageOwner source = dragController.ActiveDragSourceOwner;
            string instanceId = dragController.ActiveDragSourceInstanceId;
            if (source == null || string.IsNullOrWhiteSpace(instanceId) ||
                TryGetCompatibleSlotSet(source, instanceId, slotId, out _))
            {
                return;
            }

            ItemStorageEntry occupant = actorEquipment?.GetEquippedStorageEntry(slotId);
            if (occupant?.Item?.HasOwnedStorage == true && personalStorageNavigator.TrySelectContainer(occupant.Item.InstanceId))
            {
                gridView.Reset();
                gridScrollPosition = Vector2.zero;
                sessionController?.Selection.SelectPersonal(null);
            }
        }

        private bool TryGetCompatibleSlotSet(
            IGridStorageOwner sourceOwner,
            string instanceId,
            string targetSlotId,
            out string[] slotSet)
        {
            slotSet = null;
            IReadOnlyList<EquipmentSlotSet> alternatives = actorEquipment != null
                ? actorEquipment.GetCompatibleSlotSets(sourceOwner, instanceId)
                : null;
            if (alternatives == null)
                return false;

            for (int index = 0; index < alternatives.Count; index++)
            {
                string[] candidate = alternatives[index].SlotIds;
                for (int slotIndex = 0; slotIndex < candidate.Length; slotIndex++)
                {
                    if (candidate[slotIndex] == targetSlotId)
                    {
                        slotSet = candidate;
                        return true;
                    }
                }
            }
            return false;
        }

        private IGridStorageOwner GetActivePersonalOwner()
        {
            return personalStorageNavigator?.SelectedOwner ?? inventory;
        }

        private void DrawPersonalStorageSelector()
        {
            IReadOnlyList<PersonalStorageOption> options = personalStorageNavigator?.GetOptions();
            if (options == null || options.Count == 0)
                return;

            string selectedId = personalStorageNavigator.SelectedContainerInstanceId;
            int selectedIndex = 0;
            for (int index = 1; index < options.Count; index++)
            {
                if (options[index].ContainerInstanceId == selectedId)
                    selectedIndex = index;
            }

            GUILayout.BeginHorizontal();
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && options.Count > 1;
            if (GUILayout.Button("<", GUILayout.Width(24f), GUILayout.Height(22f)))
                SelectPersonalStorageOption(options[(selectedIndex - 1 + options.Count) % options.Count]);
            GUI.enabled = previousEnabled;
            GUILayout.Label(options[selectedIndex].Label, GUILayout.ExpandWidth(true));
            GUI.enabled = previousEnabled && options.Count > 1;
            if (GUILayout.Button(">", GUILayout.Width(24f), GUILayout.Height(22f)))
                SelectPersonalStorageOption(options[(selectedIndex + 1) % options.Count]);
            GUI.enabled = previousEnabled;
            GUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(selectedId) &&
                personalStorageNavigator.TryGetContainerEntry(selectedId, out ItemStorageEntry containerEntry) &&
                containerEntry?.Item?.OwnedStorage != null)
            {
                double contentWeight = containerEntry.Item.OwnedStorage.GetContentWeightKg(out _);
                GUILayout.Label($"Contenido: {contentWeight:0.00} kg");
            }
        }

        private void SelectPersonalStorageOption(PersonalStorageOption option)
        {
            dragController.CancelDrag();
            sessionController?.CloseContextMenu();
            if (option.IsPersonalInventory)
                personalStorageNavigator.SelectPersonalInventory();
            else
                personalStorageNavigator.TrySelectContainer(option.ContainerInstanceId);

            gridView.Reset();
            gridScrollPosition = Vector2.zero;
            sessionController?.Selection.SelectPersonal(null);
        }

        private void DrawScrollableGrid(IGridStorageOwner owner, float availableHeight)
        {
            float viewportWidth = PlayerColumnWidth - 16f;
            float viewportHeight = Mathf.Max(100f, availableHeight);
            Rect areaRect = GUILayoutUtility.GetRect(
                viewportWidth,
                viewportHeight,
                GUILayout.Width(viewportWidth),
                GUILayout.Height(viewportHeight));
            Rect clipRect = new Rect(
                areaRect.x,
                areaRect.y,
                Mathf.Max(1f, areaRect.width - ScrollbarSize),
                Mathf.Max(1f, areaRect.height - ScrollbarSize));
            float contentWidth = gridView.GetRequiredWidth(owner.GridWidth);
            float contentHeight = gridView.GetRequiredHeight(owner.GridHeight);
            float maxX = Mathf.Max(0f, contentWidth - clipRect.width);
            float maxY = Mathf.Max(0f, contentHeight - clipRect.height);
            gridScrollPosition.x = Mathf.Clamp(gridScrollPosition.x, 0f, maxX);
            gridScrollPosition.y = Mathf.Clamp(gridScrollPosition.y, 0f, maxY);
            gridScrollPosition.x = GUI.HorizontalScrollbar(
                new Rect(areaRect.x, clipRect.yMax, clipRect.width, ScrollbarSize),
                gridScrollPosition.x,
                clipRect.width,
                0f,
                Mathf.Max(clipRect.width, contentWidth));
            gridScrollPosition.y = GUI.VerticalScrollbar(
                new Rect(clipRect.xMax, areaRect.y, ScrollbarSize, clipRect.height),
                gridScrollPosition.y,
                clipRect.height,
                0f,
                Mathf.Max(clipRect.height, contentHeight));

            GUI.Box(clipRect, GUIContent.none);
            GUI.BeginGroup(clipRect);
            Rect localGridRect = new Rect(
                -gridScrollPosition.x,
                -gridScrollPosition.y,
                contentWidth,
                contentHeight);
            gridView.Draw(owner, localGridRect, dragController);
            HandleGridRightClick(owner, gridView, localGridRect);
            GUI.EndGroup();

            Rect globalGridRect = new Rect(
                clipRect.x - gridScrollPosition.x,
                clipRect.y - gridScrollPosition.y,
                contentWidth,
                contentHeight);
            dragController.RegisterEndpoint(owner, gridView, globalGridRect, clipRect);
        }

        private void ExecuteContextAction(InventoryContextActionInvocation invocation)
        {
            if (!TryResolveCurrentContextAction(
                    invocation,
                    out InventoryContextAction currentAction,
                    out IGridStorageOwner owner,
                    out int index,
                    out ItemStorageEntry entry))
            {
                toast.Show("Context action rejected: item or owner changed.", InventoryToastSeverity.Error);
                return;
            }

            int quantity = invocation.Quantity;
            if (currentAction.RequiresQuantityDialog && (quantity < 1 || quantity > entry.Quantity))
            {
                toast.Show("Context action rejected: quantity changed.", InventoryToastSeverity.Error);
                return;
            }

            switch (currentAction.Kind)
            {
                case InventoryContextActionKind.ShowDetails:
                    if (invocation.Request.SourceKind == InventoryContextSourceKind.Equipment)
                    {
                        sessionController?.Selection.SelectEquipmentFromContext(
                            invocation.Request.EquipmentSlotId,
                            entry.Item.InstanceId,
                            true);
                    }
                    return;
                case InventoryContextActionKind.ReviewOwnedStorage:
                    if (sessionController == null || !sessionController.OpenOwnedStorageInspection(entry.Item.InstanceId))
                        toast.Show("La mochila ya no pertenece al actor.", InventoryToastSeverity.Error);
                    return;
                case InventoryContextActionKind.Use:
                    UseItem(owner, entry.Item.InstanceId);
                    return;
                case InventoryContextActionKind.Equip:
                    if (invocation.Request.SourceKind == InventoryContextSourceKind.Equipment)
                        RelocateEquipmentItem(entry.Item.InstanceId, currentAction.EquipmentSlotIds);
                    else
                        EquipSelected(owner, entry.Item.InstanceId, currentAction.EquipmentSlotIds);
                    return;
                case InventoryContextActionKind.EquipReplacing:
                    if (invocation.Request.SourceKind == InventoryContextSourceKind.Equipment)
                        RelocateEquipmentItem(entry.Item.InstanceId, currentAction.EquipmentSlotIds);
                    else
                        EquipReplacingSelected(owner, entry.Item.InstanceId, currentAction.EquipmentSlotIds);
                    return;
                case InventoryContextActionKind.Unequip:
                    UnequipSelected(entry.Item.InstanceId);
                    return;
                case InventoryContextActionKind.DropOne:
                    DropItem(owner, entry.Item.InstanceId, 1, "drop_1", "Drop 1");
                    return;
                case InventoryContextActionKind.DropAmount:
                    DropItem(owner, entry.Item.InstanceId, quantity, "drop_amount", "Drop Amount");
                    return;
                case InventoryContextActionKind.DropStack:
                    if (invocation.Request.SourceKind == InventoryContextSourceKind.Equipment)
                        DropEquipmentItem(entry.Item.InstanceId);
                    else
                        DropItem(owner, entry.Item.InstanceId, entry.Quantity, "drop_stack", "Drop Stack");
                    return;
                case InventoryContextActionKind.MoveToPersonalOne:
                    TransferPersonalCompartment(owner, inventory, entry.Item.InstanceId, 1, false);
                    return;
                case InventoryContextActionKind.MoveToPersonalAmount:
                    TransferPersonalCompartment(owner, inventory, entry.Item.InstanceId, quantity, false);
                    return;
                case InventoryContextActionKind.MoveToPersonalStack:
                    TransferPersonalCompartment(owner, inventory, entry.Item.InstanceId, entry.Quantity, true);
                    return;
                case InventoryContextActionKind.MoveToOwnedStorageOne:
                case InventoryContextActionKind.MoveToOwnedStorageAmount:
                case InventoryContextActionKind.MoveToOwnedStorageStack:
                    if (!personalStorageNavigator.TryGetOwnedStorage(
                            currentAction.TargetContainerInstanceId,
                            out ItemOwnedStorageRuntime ownedTarget))
                    {
                        toast.Show("El compartimento personal ya no está accesible.", InventoryToastSeverity.Error);
                        return;
                    }
                    int moveQuantity = currentAction.Kind == InventoryContextActionKind.MoveToOwnedStorageOne
                        ? 1
                        : currentAction.Kind == InventoryContextActionKind.MoveToOwnedStorageAmount
                            ? quantity
                            : entry.Quantity;
                    if (invocation.Request.SourceKind == InventoryContextSourceKind.Equipment)
                    {
                        TransferEquipmentItem(entry.Item.InstanceId, ownedTarget, default);
                    }
                    else
                    {
                        TransferPersonalCompartment(
                            owner,
                            ownedTarget,
                            entry.Item.InstanceId,
                            moveQuantity,
                            currentAction.Kind == InventoryContextActionKind.MoveToOwnedStorageStack);
                    }
                    return;
                default:
                    toast.Show("Context action is not valid in the personal session.", InventoryToastSeverity.Error);
                    return;
            }
        }

        private bool TryResolveCurrentContextAction(
            InventoryContextActionInvocation invocation,
            out InventoryContextAction currentAction,
            out IGridStorageOwner owner,
            out int index,
            out ItemStorageEntry entry)
        {
            currentAction = null;
            owner = null;
            index = -1;
            entry = null;
            InventoryContextMenuRequest request = invocation.Request;
            if (request == null || invocation.Action == null)
                return false;

            IReadOnlyList<InventoryContextAction> actions;
            if (request.SourceKind == InventoryContextSourceKind.Personal)
            {
                owner = GetActivePersonalOwner();
                if (!ReferenceEquals(request.Owner, owner) || owner == null ||
                    !owner.TryGetEntryByInstanceId(request.InstanceId, out index, out entry) ||
                    entry?.Item == null)
                {
                    return false;
                }
                actions = InventoryContextActionResolver.ResolvePersonalCompartment(
                    owner,
                    inventory,
                    actorEquipment,
                    personalStorageNavigator,
                    request.InstanceId,
                    false);
            }
            else if (request.SourceKind == InventoryContextSourceKind.InspectedOwnedStorage)
            {
                owner = sessionController?.InspectedOwnedStorage;
                if (!ReferenceEquals(request.Owner, owner) || owner == null ||
                    !owner.TryGetEntryByInstanceId(request.InstanceId, out index, out entry) || entry?.Item == null)
                {
                    return false;
                }
                actions = InventoryContextActionResolver.ResolvePersonalCompartment(
                    owner,
                    inventory,
                    actorEquipment,
                    personalStorageNavigator,
                    request.InstanceId,
                    false);
            }
            else if (request.SourceKind == InventoryContextSourceKind.Equipment)
            {
                if (!ReferenceEquals(request.Equipment, actorEquipment) || actorEquipment == null ||
                    actorEquipment.GetEquippedStorageEntry(request.EquipmentSlotId)?.Item?.InstanceId != request.InstanceId ||
                    !SameSlotSet(request.SourceEquipmentSlotIds, actorEquipment.GetSlotsOccupiedBy(request.InstanceId)) ||
                    !actorEquipment.TryGetEntryByInstanceId(request.InstanceId, out entry) || entry?.Item == null)
                {
                    return false;
                }
                actions = InventoryContextActionResolver.ResolveEquipment(
                    actorEquipment,
                    request.EquipmentSlotId,
                    request.InstanceId,
                    personalStorageNavigator,
                    null,
                    default);
            }
            else
            {
                return false;
            }

            currentAction = FindMatchingAction(actions, invocation.Action);
            return currentAction != null && currentAction.Enabled;
        }

        private static InventoryContextAction FindMatchingAction(
            IReadOnlyList<InventoryContextAction> actions,
            InventoryContextAction requested)
        {
            if (actions == null || requested == null)
                return null;

            for (int index = 0; index < actions.Count; index++)
            {
                InventoryContextAction candidate = actions[index];
                if (candidate.Kind == requested.Kind &&
                    candidate.TargetContainerInstanceId == requested.TargetContainerInstanceId &&
                    SameSlots(candidate.EquipmentSlotIds, requested.EquipmentSlotIds))
                    return candidate;
            }
            return null;
        }

        private static bool SameSlots(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            int leftCount = left?.Count ?? 0;
            int rightCount = right?.Count ?? 0;
            if (leftCount != rightCount)
                return false;
            for (int index = 0; index < leftCount; index++)
            {
                if (left[index] != right[index])
                    return false;
            }
            return true;
        }

        private static bool SameSlotSet(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            int leftCount = left?.Count ?? 0;
            int rightCount = right?.Count ?? 0;
            if (leftCount == 0 || leftCount != rightCount)
                return false;
            for (int leftIndex = 0; leftIndex < leftCount; leftIndex++)
            {
                bool found = false;
                for (int rightIndex = 0; rightIndex < rightCount; rightIndex++)
                {
                    if (left[leftIndex] == right[rightIndex])
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    return false;
            }
            return true;
        }

        private void DrawSelectedItemDetails()
        {
            InventoryUISessionSelection selection = sessionController?.Selection;
            if (selection != null && selection.ActiveSide == InventoryUIActiveSide.Equipment)
            {
                DrawSelectedEquipmentDetails(selection);
                return;
            }

            GUILayout.Label("Selected Personal Item");
            string selectedInstanceId = selection != null && selection.ActiveSide == InventoryUIActiveSide.Personal
                ? selection.SelectedPersonalItemInstanceId
                : gridView.SelectedInstanceId;
            IGridStorageOwner activeOwner = GetActivePersonalOwner();
            if (string.IsNullOrWhiteSpace(selectedInstanceId) || activeOwner == null ||
                !activeOwner.TryGetEntryByInstanceId(selectedInstanceId, out _, out ItemStorageEntry entry))
            {
                GUILayout.Label("Click an item in the grid.");
                return;
            }

            ItemInstance item = entry.Item;
            GUILayout.Label(FormatItemDisplayName(entry));
            GUILayout.Label($"Instance: {item.InstanceId}");
            DrawSelectedItemWeight(entry);
            if (activeOwner.TryGetGridPlacement(item.InstanceId, out GridPlacement placement))
            {
                GUILayout.Label(
                    $"Placement: ({placement.X},{placement.Y}) " +
                    $"{placement.EffectiveWidth}x{placement.EffectiveHeight} " +
                    (placement.IsRotated ? "rotated" : "original"));
            }

            GUILayout.Space(8f);
            GUILayout.Label("Right-click the grid item for actions.");
        }

        private void DrawSelectedEquipmentDetails(InventoryUISessionSelection selection)
        {
            GUILayout.Label("Selected Equipment Slot");
            GUILayout.Label($"Slot: {SafeText(selection.SelectedEquipmentSlotId)}");
            if (actorEquipment == null || string.IsNullOrWhiteSpace(selection.SelectedEquippedInstanceId) ||
                !actorEquipment.TryGetEntryByInstanceId(selection.SelectedEquippedInstanceId, out ItemStorageEntry entry))
            {
                GUILayout.Label("Vacío");
                return;
            }

            GUILayout.Label(FormatItemDisplayName(entry));
            GUILayout.Label($"Instance: {entry.Item.InstanceId}");
            GUILayout.Label($"Slots: {GetSlotSetLabel(actorEquipment.GetSlotsOccupiedBy(entry.Item.InstanceId))}");
            DrawSelectedItemWeight(entry);
            GUILayout.Space(8f);
            GUILayout.Label("Right-click the occupied slot for actions.");
        }

        private void EquipSelected(IGridStorageOwner sourceOwner, string instanceId, IReadOnlyList<string> slotIds)
        {
            EquipmentPreview preview = actorEquipment.PreviewEquip(sourceOwner, instanceId, slotIds);
            EquipmentMutationResult result = actorEquipment.Equip(sourceOwner, preview);
            string message = result.Success
                ? EquipmentFailureMessageFormatter.FormatSuccess(actorEquipment, result.InstanceId, false, false)
                : EquipmentFailureMessageFormatter.FormatFailure(result.FailureCode, actorEquipment, slotIds);
            toast.Show(message, result.Success ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
            if (!result.Success)
                return;

            string primarySlot = result.SlotIds.Length > 0 ? result.SlotIds[0] : null;
            sessionController?.Selection.SelectEquipmentFromContext(primarySlot, result.InstanceId, true);
            gridView.ReconcileSelection(GetActivePersonalOwner());
            GUIUtility.ExitGUI();
        }

        private void EquipReplacingSelected(IGridStorageOwner sourceOwner, string instanceId, IReadOnlyList<string> slotIds)
        {
            EquipmentReplacementPlan plan = actorEquipment.PreviewEquipReplacing(sourceOwner, instanceId, slotIds);
            EquipmentMutationResult result = actorEquipment.EquipReplacing(sourceOwner, plan);
            string[] displacedIds = GetDisplacedIds(plan);
            string message = result.Success
                ? EquipmentFailureMessageFormatter.FormatSuccess(actorEquipment, result.InstanceId, false, true)
                : EquipmentFailureMessageFormatter.FormatFailure(result.FailureCode, actorEquipment, slotIds, displacedIds);
            toast.Show(message, result.Success ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
            if (!result.Success)
                return;

            string primarySlot = result.SlotIds.Length > 0 ? result.SlotIds[0] : null;
            sessionController?.Selection.SelectEquipmentFromContext(primarySlot, result.InstanceId, true);
            gridView.ReconcileSelection(GetActivePersonalOwner());
            GUIUtility.ExitGUI();
        }

        private void UnequipSelected(string instanceId)
        {
            EquipmentPreview preview = actorEquipment.PreviewUnequip(instanceId);
            EquipmentMutationResult result = actorEquipment.Unequip(preview);
            string message = result.Success
                ? EquipmentFailureMessageFormatter.FormatSuccess(actorEquipment, result.InstanceId, true, false)
                : EquipmentFailureMessageFormatter.FormatFailure(
                    result.FailureCode,
                    actorEquipment,
                    actorEquipment.GetSlotsOccupiedBy(instanceId),
                    new[] { instanceId });
            toast.Show(message, result.Success ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
            if (!result.Success)
                return;

            sessionController?.Selection.ClearEquipment();
            sessionController?.Selection.SelectPersonalFromContext(result.InstanceId);
            gridView.SelectInstance(result.InstanceId);
            GUIUtility.ExitGUI();
        }

        private void RelocateEquipmentItem(string instanceId, IReadOnlyList<string> slotIds)
        {
            EquipmentRelocationPlan plan = actorEquipment?.PreviewRelocateEquipped(instanceId, slotIds);
            EquipmentMutationResult result = actorEquipment != null
                ? actorEquipment.RelocateEquipped(plan)
                : EquipmentMutationResult.Rejected("Actor equipment is unavailable.", instanceId, EquipmentFailureCode.MissingDependencies);
            string[] displacedIds = GetDisplacedIds(plan);
            string message = result.Success
                ? EquipmentFailureMessageFormatter.FormatSuccess(actorEquipment, result.InstanceId, false, plan != null && plan.DisplacedItems.Length > 0)
                : EquipmentFailureMessageFormatter.FormatFailure(result.FailureCode, actorEquipment, slotIds, displacedIds);
            toast.Show(message, result.Success ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
            if (!result.Success)
                return;

            string primarySlot = result.SlotIds.Length > 0 ? result.SlotIds[0] : null;
            sessionController?.Selection.SelectEquipmentFromContext(primarySlot, result.InstanceId, true);
            sessionController?.CloseContextMenu();
            GUIUtility.ExitGUI();
        }

        private void TransferEquipmentItem(
            string instanceId,
            IGridStorageOwner destination,
            GridStorageTransferContext context)
        {
            EquipmentStorageTransferPlan plan = actorEquipment?.PreviewTransferEquippedToStorage(instanceId, destination, context);
            EquipmentMutationResult result = actorEquipment != null
                ? actorEquipment.TransferEquippedToStorage(destination, plan, context)
                : EquipmentMutationResult.Rejected("Actor equipment is unavailable.", instanceId, EquipmentFailureCode.MissingDependencies);
            toast.Show(
                result.Success ? $"Transferred {result.InstanceId}." : result.Message ?? "Equipment transfer failed.",
                result.Success ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
            if (!result.Success)
                return;

            sessionController?.Selection.ClearEquipment();
            if (ReferenceEquals(GetActivePersonalOwner(), destination))
            {
                gridView.SelectInstance(result.InstanceId);
                sessionController?.Selection.SelectPersonalFromContext(result.InstanceId);
            }
            sessionController?.CloseContextMenu();
            gridView.ReconcileSelection(GetActivePersonalOwner());
            GUIUtility.ExitGUI();
        }

        private static string[] GetDisplacedIds(EquipmentReplacementPlan plan)
        {
            int count = plan?.DisplacedItems?.Length ?? 0;
            var result = new string[count];
            for (int index = 0; index < count; index++)
                result[index] = plan.DisplacedItems[index]?.InstanceId;
            return result;
        }

        private static string[] GetDisplacedIds(EquipmentRelocationPlan plan)
        {
            int count = plan?.DisplacedItems?.Length ?? 0;
            var result = new string[count];
            for (int index = 0; index < count; index++)
                result[index] = plan.DisplacedItems[index]?.InstanceId;
            return result;
        }

        private void DrawLegacyStorage(IGridStorageOwner owner, float height)
        {
            GUILayout.Label("Storage (Legacy List):");
            IReadOnlyList<ItemStorageEntry> entries = owner != null ? owner.GridStorageEntries : null;
            if (entries == null || entries.Count == 0)
            {
                GUILayout.Label("Storage is empty.");
                return;
            }

            scrollPosition.x = 0f;
            scrollPosition = GUILayout.BeginScrollView(
                scrollPosition,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUILayout.Height(Mathf.Max(80f, height - 24f)));
            scrollPosition.x = 0f;
            for (int index = 0; index < entries.Count; index++)
            {
                ItemStorageEntry entry = entries[index];
                if (GUILayout.Button(FormatItemDisplayName(entry), GUILayout.Height(26f)))
                {
                    gridView.SelectInstance(entry?.Item?.InstanceId);
                    sessionController?.Selection.SelectPersonal(entry?.Item?.InstanceId);
                }
            }
            GUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Inventory (Debug v0)");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Height(24f), GUILayout.Width(90f)))
                RequestClose();
            GUILayout.EndHorizontal();
        }

        private void DrawFirearmSection()
        {
            ResolveFirearmController();
            if (firearmController == null || !firearmController.HasEquippedFirearm)
                return;

            GUILayout.Label($"Firearm: {firearmController.EquippedFirearmDisplayName}");
            GUILayout.Label(firearmController.StatusText);
            GUILayout.Label("F: Toggle Aim");
        }

        private void DrawCarryWeightSection()
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Height(68f));
            CarryWeightSnapshot snapshot = inventory.GetCarryWeightSnapshot();
            if (snapshot.IsValid)
            {
                GUILayout.Label($"Carry: {snapshot.CurrentWeightKg:0.00} / {snapshot.SoftCapacityKg:0.00} kg");
                GUILayout.Label($"Hard limit: {snapshot.HardLimitKg:0.00} kg");
                GUILayout.Label($"Encumbrance: {snapshot.EncumbranceRatio * 100d:0}% — {snapshot.State}");
            }
            else
            {
                GUILayout.Label("Carry: unavailable");
                GUILayout.Label("Hard limit: --");
                GUILayout.Label("Encumbrance: -- — Invalid");
            }
            GUILayout.EndVertical();
        }

        private void DrawSelectedItemWeight(ItemStorageEntry entry)
        {
            if (entry != null && entry.Item != null &&
                ItemWeightResolver.TryGetDefinitionWeight(
                    entry.DefinitionId, entry.Quantity, out double unitWeightKg, out double stackWeightKg, out _))
            {
                GUILayout.Label($"Unit weight: {FormatUnitWeight(unitWeightKg)} kg");
                if (ItemWeightResolver.TryGetEntryWeight(entry, entry.Quantity, out double totalWeightKg, out _) &&
                    totalWeightKg > stackWeightKg + 0.000001d)
                {
                    GUILayout.Label($"Contained weight: {totalWeightKg - stackWeightKg:0.00} kg");
                    GUILayout.Label($"Total weight: {totalWeightKg:0.00} kg");
                }
                else
                {
                    GUILayout.Label($"Stack weight: {stackWeightKg:0.00} kg");
                }
                return;
            }

            GUILayout.Label("Unit weight: unavailable");
            GUILayout.Label("Stack weight: unavailable");
        }

        private void DrawItemRow(int index, ItemStorageEntry entry)
        {
            ItemInstance item = entry != null ? entry.Item : null;
            if (GUILayout.Button(GetItemLabel(index, entry), GUILayout.Height(26f)))
            {
                gridView.SelectInstance(item?.InstanceId);
                sessionController?.Selection.SelectPersonal(item?.InstanceId);
            }
        }

        private void TransferPersonalCompartment(
            IGridStorageOwner source,
            IGridStorageOwner target,
            string instanceId,
            int quantity,
            bool fullStack)
        {
            InventoryMutationResult result = fullStack
                ? GridStorageTransferService.TransferStackAuto(
                    source,
                    target,
                    instanceId,
                    GridStorageTransferQuantityPolicy.Exact,
                    default)
                : GridStorageTransferService.TransferQuantityAuto(
                    source,
                    target,
                    instanceId,
                    quantity,
                    true,
                    GridStorageTransferQuantityPolicy.Exact,
                    default);

            toast.Show(
                result.Success
                    ? $"Transferred x{result.AffectedQuantity}."
                    : result.Message ?? "Transfer failed.",
                result.Success ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
            personalStorageNavigator?.Refresh();
            gridView.ReconcileSelection(GetActivePersonalOwner());
            if (result.Success && !string.IsNullOrWhiteSpace(result.DestinationInstanceId) &&
                GetActivePersonalOwner().TryGetEntryByInstanceId(result.DestinationInstanceId, out _, out _))
            {
                gridView.SelectInstance(result.DestinationInstanceId);
                sessionController?.Selection.SelectPersonalFromContext(result.DestinationInstanceId);
            }
            else if (result.Success)
            {
                sessionController?.Selection.ClearPersonalIfMissing(result.SourceInstanceId);
            }
        }

        private void DropItem(IGridStorageOwner sourceOwner, string sourceInstanceId, int quantity, string actionId, string actionDisplayName)
        {
            bool dropped = DroppedWorldItemSpawner.TryDrop(
                sourceOwner,
                sourceInstanceId,
                inventory,
                quantity,
                actionId,
                actionDisplayName,
                out string message);

            toast.Show(
                message,
                dropped ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
            if (!dropped)
                Debug.LogWarning($"[InventoryDebugPanel] {message}");

            gridView.ReconcileSelection(GetActivePersonalOwner());
            if (!string.IsNullOrWhiteSpace(sourceInstanceId) &&
                !sourceOwner.TryGetEntryByInstanceId(sourceInstanceId, out _, out _))
            {
                sessionController?.Selection.ClearPersonalIfMissing(sourceInstanceId);
                sessionController?.CloseContextMenu();
            }
            GUIUtility.ExitGUI();
        }

        private void DropEquipmentItem(string instanceId)
        {
            bool dropped = DroppedWorldItemSpawner.TryDrop(
                actorEquipment,
                instanceId,
                inventory,
                "drop_stack",
                "Drop Stack",
                out string message);
            toast.Show(message, dropped ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
            if (!dropped)
                return;

            sessionController?.Selection.ClearEquipment();
            sessionController?.CloseContextMenu();
            GUIUtility.ExitGUI();
        }

        private void UseItem(IGridStorageOwner sourceOwner, string sourceInstanceId)
        {
            ResolveActorNeeds();
            ResolveActorHealth();

            InventoryItemUseResult result = InventoryItemUseService.TryUseItem(
                sourceOwner,
                sourceInstanceId,
                inventory,
                actorNeeds,
                actorHealth);
            toast.Show(
                result.Message,
                result.Success ? InventoryToastSeverity.Success : InventoryToastSeverity.Warning);
            if (!result.Success)
                Debug.Log($"[InventoryDebugPanel] Use failed: {result.Message}");

            gridView.ReconcileSelection(GetActivePersonalOwner());
            if (!string.IsNullOrWhiteSpace(sourceInstanceId) &&
                !sourceOwner.TryGetEntryByInstanceId(sourceInstanceId, out _, out _))
            {
                sessionController?.Selection.ClearPersonalIfMissing(sourceInstanceId);
                sessionController?.CloseContextMenu();
            }
        }

        private string GetItemLabel(int index, ItemStorageEntry entry)
        {
            ItemInstance item = entry != null ? entry.Item : null;
            if (item == null)
                return $"{index}: (none)";

            string equippedMarker = inventory != null && inventory.IsRightHandEquippedIndex(index) ? " (Equipped)" : string.Empty;
            string gridDiagnostic = GetGridDiagnostic(item);
            return $"{index}: {FormatItemDisplayName(entry)}{equippedMarker} [{item.InstanceId}] condition {item.Condition} | {gridDiagnostic}";
        }

        private string GetGridDiagnostic(ItemInstance item)
        {
            if (inventory == null || item == null)
                return "grid unavailable";

            if (!inventory.TryGetGridFootprint(item.DefinitionId, out GridFootprint footprint, out bool usedFallback))
                return "footprint invalid";

            string fallbackLabel = usedFallback ? ", fallback 1x1" : string.Empty;
            if (!inventory.UsesGridLayout)
                return $"footprint {footprint.Width}x{footprint.Height}{fallbackLabel}, placement linear";

            if (!inventory.TryGetGridPlacement(item.InstanceId, out GridPlacement placement))
                return $"footprint {footprint.Width}x{footprint.Height}{fallbackLabel}, placement missing";

            string orientation = placement.IsRotated ? "rotated" : "original";
            return $"footprint {footprint.Width}x{footprint.Height}{fallbackLabel}, placement ({placement.X},{placement.Y}), {orientation}";
        }

        private string FormatItemDisplayName(ItemStorageEntry entry)
        {
            if (entry == null || entry.Item == null)
                return "(none)";

            string displayName = GetItemDisplayName(entry.Item.DefinitionId);
            return $"{displayName} x{entry.Quantity}";
        }

        private static string GetItemDisplayName(string definitionId)
        {
            ItemDefinition definition = GetItemDefinition(definitionId);
            if (definition == null || definition.display == null || string.IsNullOrWhiteSpace(definition.display.name))
                return SafeText(definitionId);

            return definition.display.name;
        }

        private static ItemDefinition GetItemDefinition(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
                return null;

            if (GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
                return null;

            GameDatabase database = GameDataManager.Instance.Database;
            return database != null ? database.GetItem(definitionId) : null;
        }

        private static string FormatUnitWeight(double unitWeightKg)
        {
            return unitWeightKg < 0.1d ? unitWeightKg.ToString("0.000") : unitWeightKg.ToString("0.00");
        }

        private string GetSlotSetLabel(IReadOnlyList<string> slotIds)
        {
            if (slotIds == null || slotIds.Count == 0)
                return "(none)";
            var labels = new string[slotIds.Count];
            for (int index = 0; index < slotIds.Count; index++)
            {
                EquipmentSlotDefinition definition = actorEquipment != null
                    ? actorEquipment.GetSlotDefinition(slotIds[index])
                    : null;
                labels[index] = definition != null && !string.IsNullOrWhiteSpace(definition.display_name)
                    ? definition.display_name
                    : slotIds[index];
            }
            return string.Join(" + ", labels);
        }

        private static Rect GetPanelRect()
        {
            float width = Mathf.Max(1f, Mathf.Min(PanelWidth, Screen.width - 24f));
            float height = Mathf.Max(1f, Mathf.Min(PanelHeight, Screen.height - 48f));
            return new Rect(
                Mathf.Max(0f, (Screen.width - width) * 0.5f),
                Mathf.Max(0f, (Screen.height - height) * 0.5f),
                width,
                height);
        }

        private void ConsumeDragStatus()
        {
            if (dragController.TryConsumeStatus(out string message, out InventoryToastSeverity severity))
                toast.Show(message, severity);
        }

        private void DrawCloseButton()
        {
            GUILayout.Space(8f);
            if (GUILayout.Button("Close", GUILayout.Height(24f)))
                RequestClose();
        }

        private void RequestClose()
        {
            if (sessionController != null)
                sessionController.CloseSession();
            else
                HideFromSession();
        }

        private static Vector2 ToGuiPosition(Vector2 mousePosition)
        {
            return new Vector2(mousePosition.x, Screen.height - mousePosition.y);
        }

        private static Vector2 ToPanelLocalPosition(Vector2 currentGuiPosition)
        {
            return GUIUtility.GUIToScreenPoint(currentGuiPosition) - GetPanelRect().position;
        }

        private void ResolveActorNeeds()
        {
            if (actorNeeds != null)
                return;

            if (inventory != null)
                actorNeeds = inventory.GetComponentInParent<ActorNeedsComponent>();

            if (actorNeeds == null)
                actorNeeds = FindAnyObjectByType<ActorNeedsComponent>();
        }

        private void ResolveActorHealth()
        {
            if (actorHealth != null)
                return;

            if (inventory != null)
                actorHealth = inventory.GetComponentInParent<ActorHealthComponent>();

            if (actorHealth == null)
                actorHealth = FindAnyObjectByType<ActorHealthComponent>();
        }

        private void ResolveFirearmController()
        {
            if (firearmController != null)
                return;

            if (inventory != null)
                firearmController = inventory.GetComponentInParent<FirearmDebugController>();

            if (firearmController == null)
                firearmController = FindAnyObjectByType<FirearmDebugController>();
        }

        private void ResolveActorEquipment()
        {
            if (actorEquipment == null && inventory != null)
                actorEquipment = inventory.GetComponent<ActorEquipmentComponent>();
        }

        private void ResolveSessionController()
        {
            if (sessionController == null)
                InventoryUISessionController.GetOrCreate().BindPanel(this);
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
        }
    }
}
