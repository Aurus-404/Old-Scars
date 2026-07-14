using System.Collections.Generic;
using OldScars.Core.Actors;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Interactions;
using UnityEngine;

namespace OldScars.Core.Items
{
    public sealed class ItemStorageDebugPanel : MonoBehaviour
    {
        private const float MaxPanelWidth = 1120f;
        private const float MaxPanelHeight = 700f;
        private const float MinimumGridColumnWidth = 220f;
        private const float GridColumnPadding = 24f;
        private const float CenterColumnWidth = 330f;
        private const float ColumnGap = 8f;
        private const float PanelHorizontalPadding = 24f;
        private const float BodyVerticalReserve = 42f;
        private const float MinimumEquipmentViewportHeight = 300f;
        private const float MaximumEquipmentViewportHeight = 330f;
        private const float CenterDetailsHeight = 72f;

        private IItemStorageDebugSource storageSource;
        private InventoryComponent targetInventory;
        private DebugActionExecutionContext executionContext;
        private ActionDefinition action;
        private string title;
        private bool isVisible;
        private bool showPlayerLegacyList;
        private bool showExternalLegacyList;
        private Vector2 playerLegacyScroll;
        private Vector2 externalLegacyScroll;
        private InventoryUISessionController sessionController;
        private Rect sessionPanelRect;
        private float playerColumnWidth;
        private float externalColumnWidth;
        private ActorEquipmentComponent actorEquipment;
        private Vector2 centerDetailsScrollPosition;

        private readonly InventoryGridDebugView playerGridView = new InventoryGridDebugView();
        private readonly InventoryGridDebugView externalGridView = new InventoryGridDebugView();
        private readonly InventoryGridDragController dragController = new InventoryGridDragController();
        private readonly InventoryDebugToast toast = new InventoryDebugToast();
        private readonly EquipmentDebugListView equipmentListView = new EquipmentDebugListView();
        private int observedGridSelectionVersion;

        public bool IsVisible => isVisible;

        public static ItemStorageDebugPanel GetOrCreate()
        {
            ItemStorageDebugPanel panel = FindAnyObjectByType<ItemStorageDebugPanel>();
            if (panel != null)
                return panel;

            var panelObject = new GameObject("ItemStorageDebugPanel_Runtime");
            return panelObject.AddComponent<ItemStorageDebugPanel>();
        }

        public void Show(
            ContainerLootComponent sourceContainer,
            InventoryComponent inventory,
            DebugActionExecutionContext context,
            ActionDefinition sourceAction)
        {
            Show(sourceContainer as IItemStorageDebugSource, inventory, context, sourceAction);
        }

        public void Show(
            IItemStorageDebugSource source,
            InventoryComponent inventory,
            DebugActionExecutionContext context,
            ActionDefinition sourceAction)
        {
            InventoryUISessionController.GetOrCreate().OpenExternal(source, inventory, context, sourceAction);
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

        internal void ShowFromSession(
            IItemStorageDebugSource source,
            InventoryComponent inventory,
            DebugActionExecutionContext context,
            ActionDefinition sourceAction)
        {
            storageSource = source;
            targetInventory = inventory;
            actorEquipment = targetInventory != null ? targetInventory.GetComponent<ActorEquipmentComponent>() : null;
            executionContext = context;
            action = sourceAction;
            title = source != null ? source.GetStorageDebugTitle(context.Target) : "Storage Debug Panel";
            showPlayerLegacyList = false;
            showExternalLegacyList = false;
            playerLegacyScroll = Vector2.zero;
            externalLegacyScroll = Vector2.zero;
            centerDetailsScrollPosition = Vector2.zero;
            playerGridView.Reset();
            externalGridView.Reset();
            dragController.Reset();
            toast.Clear();
            playerColumnWidth = CalculateStorageColumnWidth(targetInventory, playerGridView);
            externalColumnWidth = CalculateStorageColumnWidth(storageSource, externalGridView);
            sessionPanelRect = CalculatePanelRect(playerColumnWidth, externalColumnWidth);
            isVisible = true;
        }

        internal void HideFromSession()
        {
            isVisible = false;
            storageSource = null;
            targetInventory = null;
            actorEquipment = null;
            executionContext = new DebugActionExecutionContext(null, null, null);
            action = null;
            title = null;
            sessionPanelRect = default;
            playerColumnWidth = 0f;
            externalColumnWidth = 0f;
            playerGridView.Reset();
            externalGridView.Reset();
            dragController.Reset();
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

            return GetPanelRect().Contains(new Vector2(screenPosition.x, Screen.height - screenPosition.y));
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

            if (targetInventory == null || storageSource == null)
            {
                GUILayout.Label("Player inventory or external storage source is missing.");
                ConsumeDragStatus();
                toast.Draw(new Rect(0f, 0f, panelRect.width, panelRect.height));
                GUI.enabled = previousEnabled;
                sessionController?.DrawContextOverlay(new Rect(0f, 0f, panelRect.width, panelRect.height));
                GUILayout.EndArea();
                sessionController?.ConsumeCurrentOnGUIEvent();
                return;
            }

            dragController.BeginFrame(new GridStorageTransferContext(executionContext, action));
            float bodyHeight = Mathf.Max(1f, panelRect.height - BodyVerticalReserve);
            GUILayout.BeginHorizontal(GUILayout.Height(bodyHeight));
            GUILayout.FlexibleSpace();
            DrawStorageColumn(targetInventory, playerGridView, true, playerColumnWidth, bodyHeight);
            GUILayout.Space(ColumnGap);
            DrawCenterColumn(bodyHeight);
            GUILayout.Space(ColumnGap);
            DrawStorageColumn(storageSource, externalGridView, false, externalColumnWidth, bodyHeight);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (!(sessionController?.BlocksInventoryContentInput ?? false))
                dragController.ProcessOnGUI();
            SyncGridSelectionToSession();
            ConsumeDragStatus();
            toast.Draw(new Rect(0f, 0f, panelRect.width, panelRect.height));
            GUI.enabled = previousEnabled;
            sessionController?.DrawContextOverlay(new Rect(0f, 0f, panelRect.width, panelRect.height));
            GUILayout.EndArea();
            sessionController?.ConsumeCurrentOnGUIEvent();
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(!string.IsNullOrWhiteSpace(title) ? title : "Storage Debug Panel");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(90f), GUILayout.Height(24f)))
                RequestClose();
            GUILayout.EndHorizontal();
        }

        private void DrawStorageColumn(
            IGridStorageOwner owner,
            InventoryGridDebugView view,
            bool isPlayer,
            float columnWidth,
            float bodyHeight)
        {
            GUILayout.BeginVertical(
                GUI.skin.box,
                GUILayout.Width(columnWidth),
                GUILayout.Height(bodyHeight));
            GUILayout.Label(isPlayer ? "Player Grid" : "External Storage Grid");

            bool showLegacy = isPlayer ? showPlayerLegacyList : showExternalLegacyList;
            if (GUILayout.Button(showLegacy ? "Visual Grid" : "Legacy List", GUILayout.Height(24f)))
            {
                showLegacy = !showLegacy;
                if (isPlayer)
                    showPlayerLegacyList = showLegacy;
                else
                    showExternalLegacyList = showLegacy;
            }

            if (owner.GridInitializationState == GridStorageInitializationState.LinearFallback)
            {
                GUILayout.Label($"Grid fallback: {SafeText(owner.GridInitializationError)}");
                showLegacy = true;
            }

            if (!showLegacy && owner.UsesGridLayout)
            {
                int width = owner.GridWidth;
                int height = owner.GridHeight;
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                Rect gridRect = GUILayoutUtility.GetRect(
                    view.GetRequiredWidth(width),
                    view.GetRequiredHeight(height),
                    GUILayout.Width(view.GetRequiredWidth(width)),
                    GUILayout.Height(view.GetRequiredHeight(height)));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                view.Draw(owner, gridRect, dragController);
                dragController.RegisterEndpoint(owner, view, gridRect);
                HandleGridRightClick(owner, view, gridRect, isPlayer);
            }
            else
            {
                DrawLegacyList(owner, view, isPlayer, bodyHeight - 70f);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }

        private void SyncGridSelectionToSession()
        {
            if (dragController.SelectionVersion == observedGridSelectionVersion)
                return;

            observedGridSelectionVersion = dragController.SelectionVersion;
            InventoryUISessionSelection selection = sessionController?.Selection;
            if (selection == null)
                return;

            if (ReferenceEquals(dragController.ActiveOwner, targetInventory) &&
                !string.IsNullOrWhiteSpace(playerGridView.SelectedInstanceId))
            {
                selection.SelectPersonal(playerGridView.SelectedInstanceId);
            }
            else if (ReferenceEquals(dragController.ActiveOwner, storageSource) &&
                     !string.IsNullOrWhiteSpace(externalGridView.SelectedInstanceId))
            {
                selection.SelectExternal(externalGridView.SelectedInstanceId);
            }
        }

        private void HandleGridRightClick(
            IGridStorageOwner owner,
            InventoryGridDebugView view,
            Rect gridRect,
            bool isPlayer)
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
            IReadOnlyList<InventoryContextAction> actions;
            InventoryContextSourceKind sourceKind;
            if (isPlayer)
            {
                sessionController.Selection.SelectPersonalFromContext(instanceId);
                sourceKind = InventoryContextSourceKind.Personal;
                actions = InventoryContextActionResolver.ResolvePersonal(
                    targetInventory,
                    actorEquipment,
                    instanceId,
                    true);
            }
            else
            {
                sessionController.Selection.SelectExternalFromContext(instanceId);
                sourceKind = InventoryContextSourceKind.External;
                actions = InventoryContextActionResolver.ResolveExternal(storageSource, instanceId);
            }

            sessionController.OpenContextMenu(
                new InventoryContextMenuRequest(
                    sourceKind,
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

            if (string.IsNullOrWhiteSpace(click.InstanceId) || actorEquipment == null ||
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
                click.InstanceId);
            sessionController.OpenContextMenu(
                new InventoryContextMenuRequest(
                    InventoryContextSourceKind.Equipment,
                    null,
                    actorEquipment,
                    click.InstanceId,
                    click.SlotId,
                    entry.Quantity,
                    actions,
                    ExecuteContextAction),
                guiEvent != null ? ToPanelLocalPosition(guiEvent.mousePosition) : click.RowRect.position);
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
                toast.Show("Context action rejected: item, owner or storage changed.", InventoryToastSeverity.Error);
                return;
            }

            int requestedQuantity = invocation.Quantity;
            if (currentAction.RequiresQuantityDialog &&
                (requestedQuantity < 1 || requestedQuantity > entry.Quantity))
            {
                toast.Show("Context action rejected: quantity changed.", InventoryToastSeverity.Error);
                return;
            }

            switch (currentAction.Kind)
            {
                case InventoryContextActionKind.ShowDetails:
                    return;
                case InventoryContextActionKind.Use:
                    UsePersonalItem(index, entry.Item.InstanceId);
                    return;
                case InventoryContextActionKind.Equip:
                    EquipPersonalItem(entry.Item.InstanceId, currentAction.EquipmentSlotIds);
                    return;
                case InventoryContextActionKind.EquipReplacing:
                    EquipReplacingPersonalItem(entry.Item.InstanceId, currentAction.EquipmentSlotIds);
                    return;
                case InventoryContextActionKind.Unequip:
                    UnequipEquipmentItem(entry.Item.InstanceId);
                    return;
                case InventoryContextActionKind.DropOne:
                    DropPersonalItem(index, entry.Item.InstanceId, 1, "drop_1", "Drop 1");
                    return;
                case InventoryContextActionKind.DropAmount:
                    DropPersonalItem(index, entry.Item.InstanceId, requestedQuantity, "drop_amount", "Drop Amount");
                    return;
                case InventoryContextActionKind.DropStack:
                    DropPersonalItem(index, entry.Item.InstanceId, entry.Quantity, "drop_stack", "Drop Stack");
                    return;
                case InventoryContextActionKind.TakeOne:
                    ApplyContextTransfer(TransferQuantity(owner, targetInventory, entry.Item.InstanceId, 1), true);
                    return;
                case InventoryContextActionKind.TakeAmount:
                    ApplyContextTransfer(TransferQuantity(owner, targetInventory, entry.Item.InstanceId, requestedQuantity), true);
                    return;
                case InventoryContextActionKind.TakeStack:
                    ApplyContextTransfer(TransferStack(owner, targetInventory, entry.Item.InstanceId), true);
                    return;
                case InventoryContextActionKind.DepositOne:
                    ApplyContextTransfer(TransferQuantity(owner, storageSource, entry.Item.InstanceId, 1), false);
                    return;
                case InventoryContextActionKind.DepositAmount:
                    ApplyContextTransfer(TransferQuantity(owner, storageSource, entry.Item.InstanceId, requestedQuantity), false);
                    return;
                case InventoryContextActionKind.DepositStack:
                    ApplyContextTransfer(TransferStack(owner, storageSource, entry.Item.InstanceId), false);
                    return;
                default:
                    toast.Show("Context action is not supported by this session.", InventoryToastSeverity.Error);
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
            if (request == null || invocation.Action == null ||
                sessionController == null || sessionController.State != InventoryUISessionState.External)
            {
                return false;
            }

            IReadOnlyList<InventoryContextAction> actions;
            if (request.SourceKind == InventoryContextSourceKind.Personal)
            {
                owner = targetInventory;
                if (!ReferenceEquals(request.Owner, targetInventory) || storageSource == null ||
                    !targetInventory.TryGetEntryByInstanceId(request.InstanceId, out index, out entry) || entry?.Item == null)
                {
                    return false;
                }
                actions = InventoryContextActionResolver.ResolvePersonal(
                    targetInventory,
                    actorEquipment,
                    request.InstanceId,
                    true);
            }
            else if (request.SourceKind == InventoryContextSourceKind.External)
            {
                owner = storageSource;
                if (!ReferenceEquals(request.Owner, storageSource) ||
                    !storageSource.TryGetEntryByInstanceId(request.InstanceId, out index, out entry) || entry?.Item == null)
                {
                    return false;
                }
                actions = InventoryContextActionResolver.ResolveExternal(storageSource, request.InstanceId);
            }
            else if (request.SourceKind == InventoryContextSourceKind.Equipment)
            {
                if (!ReferenceEquals(request.Equipment, actorEquipment) || actorEquipment == null ||
                    actorEquipment.GetEquippedStorageEntry(request.EquipmentSlotId)?.Item?.InstanceId != request.InstanceId ||
                    !actorEquipment.TryGetEntryByInstanceId(request.InstanceId, out entry) || entry?.Item == null)
                {
                    return false;
                }
                actions = InventoryContextActionResolver.ResolveEquipment(
                    actorEquipment,
                    request.EquipmentSlotId,
                    request.InstanceId);
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
                if (candidate.Kind == requested.Kind && SameSlots(candidate.EquipmentSlotIds, requested.EquipmentSlotIds))
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

        private void DrawLegacyList(
            IGridStorageOwner owner,
            InventoryGridDebugView view,
            bool isPlayer,
            float height)
        {
            IReadOnlyList<ItemStorageEntry> entries = owner.GridStorageEntries;
            Vector2 scroll = isPlayer ? playerLegacyScroll : externalLegacyScroll;
            scroll.x = 0f;
            scroll = GUILayout.BeginScrollView(
                scroll,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUILayout.Height(Mathf.Max(80f, height)));
            scroll.x = 0f;
            if (entries == null || entries.Count == 0)
            {
                GUILayout.Label("Storage is empty.");
            }
            else
            {
                for (int index = 0; index < entries.Count; index++)
                {
                    ItemStorageEntry entry = entries[index];
                    if (GUILayout.Button(GetEntryLabel(entry), GUILayout.Height(26f)))
                    {
                        view.SelectInstance(entry?.Item?.InstanceId);
                        dragController.SetActiveOwner(owner);
                        if (isPlayer)
                            sessionController?.Selection.SelectPersonal(entry?.Item?.InstanceId);
                        else
                            sessionController?.Selection.SelectExternal(entry?.Item?.InstanceId);
                    }
                }
            }
            GUILayout.EndScrollView();

            if (isPlayer)
                playerLegacyScroll = scroll;
            else
                externalLegacyScroll = scroll;
        }

        private void DrawCenterColumn(float bodyHeight)
        {
            GUILayout.BeginVertical(
                GUI.skin.box,
                GUILayout.Width(CenterColumnWidth),
                GUILayout.Height(bodyHeight));

            float equipmentHeight = Mathf.Clamp(
                bodyHeight * 0.50f,
                MinimumEquipmentViewportHeight,
                MaximumEquipmentViewportHeight);
            equipmentListView.Draw(
                actorEquipment,
                sessionController != null ? sessionController.Selection : new InventoryUISessionSelection(),
                CenterColumnWidth - 12f,
                equipmentHeight,
                HandleEquipmentRowClick);
            GUILayout.Space(ColumnGap);
            DrawCenterFooter(Mathf.Max(1f, bodyHeight - equipmentHeight - ColumnGap - 12f));
            GUILayout.EndVertical();
        }

        private void DrawCenterFooter(float footerHeight)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Height(footerHeight));
            GUILayout.Label("Inventory Session");
            DrawCarryWeightSummary();
            GUILayout.Label("Shift + click: transfer stack");
            GUILayout.Label("Drag: move/merge | R: rotate");

            InventoryUISessionSelection selection = sessionController?.Selection;
            if (selection != null && selection.ActiveSide == InventoryUIActiveSide.Equipment)
            {
                DrawSelectedEquipmentActions(selection, CenterDetailsHeight);
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
                return;
            }

            if (!TryGetSessionSelectedEntry(
                    selection,
                    out IGridStorageOwner activeOwner,
                    out bool playerSide,
                    out ItemStorageEntry entry))
            {
                DrawEmptyDetails(CenterDetailsHeight);
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
                return;
            }

            DrawSelectedGridDetails(activeOwner, playerSide, entry, CenterDetailsHeight);
            GUILayout.Label("Right-click the selected item for transfer actions.");

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }

        private void DrawSelectedEquipmentActions(
            InventoryUISessionSelection selection,
            float detailsHeight)
        {
            centerDetailsScrollPosition.x = 0f;
            centerDetailsScrollPosition = GUILayout.BeginScrollView(
                centerDetailsScrollPosition,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUILayout.Height(detailsHeight));
            centerDetailsScrollPosition.x = 0f;
            GUILayout.Label("Selected side: Equipment");
            GUILayout.Label($"Slot: {SafeText(selection.SelectedEquipmentSlotId)}");
            if (actorEquipment == null || string.IsNullOrWhiteSpace(selection.SelectedEquippedInstanceId) ||
                !actorEquipment.TryGetEntryByInstanceId(selection.SelectedEquippedInstanceId, out ItemStorageEntry entry))
            {
                GUILayout.Label("Vacío");
                GUILayout.EndScrollView();
                return;
            }

            GUILayout.Label(GetEntryLabel(entry));
            GUILayout.Label($"Instance: {entry.Item.InstanceId}");
            DrawSelectedItemWeight(entry);
            GUILayout.EndScrollView();
            GUILayout.Label("Right-click the occupied slot for actions.");
        }

        private bool TryGetSessionSelectedEntry(
            InventoryUISessionSelection selection,
            out IGridStorageOwner owner,
            out bool playerSide,
            out ItemStorageEntry entry)
        {
            owner = null;
            playerSide = false;
            entry = null;
            if (selection == null)
                return false;

            string instanceId;
            if (selection.ActiveSide == InventoryUIActiveSide.Personal)
            {
                owner = targetInventory;
                playerSide = true;
                instanceId = selection.SelectedPersonalItemInstanceId;
            }
            else if (selection.ActiveSide == InventoryUIActiveSide.External)
            {
                owner = storageSource;
                instanceId = selection.SelectedExternalItemInstanceId;
            }
            else
            {
                return false;
            }

            return owner != null && !string.IsNullOrWhiteSpace(instanceId) &&
                   owner.TryGetEntryByInstanceId(instanceId, out _, out entry) &&
                   entry != null && entry.Item != null;
        }

        private void DrawSelectedGridDetails(
            IGridStorageOwner owner,
            bool playerSide,
            ItemStorageEntry entry,
            float height)
        {
            centerDetailsScrollPosition.x = 0f;
            centerDetailsScrollPosition = GUILayout.BeginScrollView(
                centerDetailsScrollPosition,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUILayout.Height(height));
            centerDetailsScrollPosition.x = 0f;
            GUILayout.Label(playerSide ? "Selected side: Player" : "Selected side: External");
            GUILayout.Label(GetEntryLabel(entry));
            GUILayout.Label($"Instance: {entry.Item.InstanceId}");
            DrawSelectedItemWeight(entry);
            if (owner.TryGetGridPlacement(entry.Item.InstanceId, out GridPlacement placement))
            {
                GUILayout.Label(
                    $"Placement: ({placement.X},{placement.Y}) " +
                    $"{placement.EffectiveWidth}x{placement.EffectiveHeight}");
            }
            GUILayout.EndScrollView();
        }

        private static void DrawEmptyDetails(float height)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Height(height));
            GUILayout.Label("Select an item or equipment slot.");
            GUILayout.EndVertical();
        }

        private void DrawCarryWeightSummary()
        {
            CarryWeightSnapshot snapshot = targetInventory.GetCarryWeightSnapshot();
            if (snapshot.IsValid)
            {
                GUILayout.Label($"Carry: {snapshot.CurrentWeightKg:0.00} / {snapshot.SoftCapacityKg:0.00} kg");
                GUILayout.Label($"Hard limit: {snapshot.HardLimitKg:0.00} kg");
                GUILayout.Label($"Encumbrance: {snapshot.EncumbranceRatio * 100d:0}% — {snapshot.State}");
                return;
            }

            GUILayout.Label("Carry: unavailable");
            GUILayout.Label("Hard limit: --");
            GUILayout.Label("Encumbrance: -- — Invalid");
        }

        private void DrawSelectedItemWeight(ItemStorageEntry entry)
        {
            if (entry != null && entry.Item != null &&
                targetInventory.TryGetItemWeight(
                    entry.DefinitionId,
                    entry.Quantity,
                    out double unitWeightKg,
                    out double stackWeightKg,
                    out _))
            {
                GUILayout.Label($"Unit weight: {FormatUnitWeight(unitWeightKg)} kg");
                GUILayout.Label($"Stack weight: {stackWeightKg:0.00} kg");
                return;
            }

            GUILayout.Label("Unit weight: unavailable");
            GUILayout.Label("Stack weight: unavailable");
        }

        private void UsePersonalItem(int index, string instanceId)
        {
            ActorNeedsComponent needs = targetInventory.GetComponentInParent<ActorNeedsComponent>();
            ActorHealthComponent health = targetInventory.GetComponentInParent<ActorHealthComponent>();
            InventoryItemUseResult result = InventoryItemUseService.TryUseItem(targetInventory, index, needs, health);
            toast.Show(result.Message, result.Success ? InventoryToastSeverity.Success : InventoryToastSeverity.Warning);
            ReconcileSelections();
            if (!targetInventory.TryGetEntryByInstanceId(instanceId, out _, out _))
                sessionController?.Selection.ClearPersonalIfMissing(instanceId);
        }

        private void EquipPersonalItem(string instanceId, IReadOnlyList<string> slotIds)
        {
            EquipmentMutationResult result = actorEquipment != null
                ? actorEquipment.Equip(actorEquipment.PreviewEquip(instanceId, slotIds))
                : EquipmentMutationResult.Rejected(
                    "Actor equipment is unavailable.",
                    instanceId,
                    EquipmentFailureCode.MissingDependencies);
            string message = result.Success
                ? EquipmentFailureMessageFormatter.FormatSuccess(actorEquipment, result.InstanceId, false, false)
                : EquipmentFailureMessageFormatter.FormatFailure(result.FailureCode, actorEquipment, slotIds);
            toast.Show(message, result.Success ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
            if (!result.Success)
                return;

            string primarySlot = result.SlotIds.Length > 0 ? result.SlotIds[0] : null;
            sessionController?.Selection.SelectEquipmentFromContext(primarySlot, result.InstanceId, true);
            ReconcileSelections();
            GUIUtility.ExitGUI();
        }

        private void EquipReplacingPersonalItem(string instanceId, IReadOnlyList<string> slotIds)
        {
            EquipmentReplacementPlan plan = actorEquipment != null
                ? actorEquipment.PreviewEquipReplacing(instanceId, slotIds)
                : null;
            EquipmentMutationResult result = actorEquipment != null
                ? actorEquipment.EquipReplacing(plan)
                : EquipmentMutationResult.Rejected(
                    "Actor equipment is unavailable.",
                    instanceId,
                    EquipmentFailureCode.MissingDependencies);
            string[] displacedIds = GetDisplacedIds(plan);
            string message = result.Success
                ? EquipmentFailureMessageFormatter.FormatSuccess(actorEquipment, result.InstanceId, false, true)
                : EquipmentFailureMessageFormatter.FormatFailure(result.FailureCode, actorEquipment, slotIds, displacedIds);
            toast.Show(message, result.Success ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
            if (!result.Success)
                return;

            string primarySlot = result.SlotIds.Length > 0 ? result.SlotIds[0] : null;
            sessionController?.Selection.SelectEquipmentFromContext(primarySlot, result.InstanceId, true);
            ReconcileSelections();
            GUIUtility.ExitGUI();
        }

        private void UnequipEquipmentItem(string instanceId)
        {
            EquipmentMutationResult result = actorEquipment != null
                ? actorEquipment.Unequip(actorEquipment.PreviewUnequip(instanceId))
                : EquipmentMutationResult.Rejected(
                    "Actor equipment is unavailable.",
                    instanceId,
                    EquipmentFailureCode.MissingDependencies);
            string message = result.Success
                ? EquipmentFailureMessageFormatter.FormatSuccess(actorEquipment, result.InstanceId, true, false)
                : EquipmentFailureMessageFormatter.FormatFailure(
                    result.FailureCode,
                    actorEquipment,
                    actorEquipment != null ? actorEquipment.GetSlotsOccupiedBy(instanceId) : null,
                    new[] { instanceId });
            toast.Show(message, result.Success ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
            if (!result.Success)
                return;

            sessionController?.Selection.ClearEquipment();
            sessionController?.Selection.SelectPersonalFromContext(result.InstanceId);
            playerGridView.SelectInstance(result.InstanceId);
            ReconcileSelections();
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

        private void DropPersonalItem(
            int index,
            string instanceId,
            int quantity,
            string actionId,
            string actionDisplayName)
        {
            bool success = DroppedWorldItemSpawner.TryDrop(
                targetInventory,
                index,
                quantity,
                actionId,
                actionDisplayName,
                out string message);
            toast.Show(message, success ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
            ReconcileSelections();
            if (!targetInventory.TryGetEntryByInstanceId(instanceId, out _, out _))
                sessionController?.Selection.ClearPersonalIfMissing(instanceId);
            GUIUtility.ExitGUI();
        }

        private void ApplyContextTransfer(InventoryMutationResult result, bool tookToPersonal)
        {
            if (result == null || !result.Success)
                return;

            string destinationInstanceId = result.DestinationInstanceId;
            if (tookToPersonal && !string.IsNullOrWhiteSpace(destinationInstanceId) &&
                targetInventory.TryGetEntryByInstanceId(destinationInstanceId, out _, out _))
            {
                playerGridView.SelectInstance(destinationInstanceId);
                sessionController?.Selection.SelectPersonalFromContext(destinationInstanceId);
                return;
            }

            if (!tookToPersonal && !string.IsNullOrWhiteSpace(destinationInstanceId) &&
                storageSource.TryGetEntryByInstanceId(destinationInstanceId, out _, out _))
            {
                externalGridView.SelectInstance(destinationInstanceId);
                sessionController?.Selection.SelectExternalFromContext(destinationInstanceId);
                return;
            }

            string sourceInstanceId = result.SourceInstanceId;
            if (tookToPersonal && storageSource.TryGetEntryByInstanceId(sourceInstanceId, out _, out _))
            {
                externalGridView.SelectInstance(sourceInstanceId);
                sessionController?.Selection.SelectExternalFromContext(sourceInstanceId);
            }
            else if (!tookToPersonal && targetInventory.TryGetEntryByInstanceId(sourceInstanceId, out _, out _))
            {
                playerGridView.SelectInstance(sourceInstanceId);
                sessionController?.Selection.SelectPersonalFromContext(sourceInstanceId);
            }
            else if (tookToPersonal)
            {
                sessionController?.Selection.ClearExternalIfMissing(sourceInstanceId);
            }
            else
            {
                sessionController?.Selection.ClearPersonalIfMissing(sourceInstanceId);
            }
        }

        private InventoryMutationResult TransferQuantity(IGridStorageOwner source, IGridStorageOwner target, string instanceId, int quantity)
        {
            InventoryMutationResult result = GridStorageTransferService.TransferQuantityAuto(
                source,
                target,
                instanceId,
                quantity,
                true,
                new GridStorageTransferContext(executionContext, action));
            toast.Show(
                result.Success
                    ? $"Transferred x{result.AffectedQuantity}."
                    : result.Message ?? "Transfer failed.",
                result.Success ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
            ReconcileSelections();
            return result;
        }

        private InventoryMutationResult TransferStack(IGridStorageOwner source, IGridStorageOwner target, string instanceId)
        {
            InventoryMutationResult result = GridStorageTransferService.TransferStackAuto(
                source,
                target,
                instanceId,
                new GridStorageTransferContext(executionContext, action));
            toast.Show(
                result.Success
                    ? $"Transferred stack x{result.AffectedQuantity}."
                    : result.Message ?? "Stack transfer failed.",
                result.Success ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
            ReconcileSelections();
            return result;
        }

        private void ReconcileSelections()
        {
            playerGridView.ReconcileSelection(targetInventory);
            externalGridView.ReconcileSelection(storageSource);
        }

        private static string FormatUnitWeight(double unitWeightKg)
        {
            return unitWeightKg < 0.1d ? unitWeightKg.ToString("0.000") : unitWeightKg.ToString("0.00");
        }

        private static string GetEntryLabel(ItemStorageEntry entry)
        {
            if (entry == null || entry.Item == null)
                return "(none)";

            return $"{GetItemDisplayName(entry.DefinitionId)} x{entry.Quantity}";
        }

        private static string GetItemDisplayName(string definitionId)
        {
            if (GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
                return SafeText(definitionId);

            GameDatabase database = GameDataManager.Instance.Database;
            ItemDefinition definition = database != null ? database.GetItem(definitionId) : null;
            return definition != null && definition.display != null && !string.IsNullOrWhiteSpace(definition.display.name)
                ? definition.display.name
                : SafeText(definitionId);
        }

        private Rect GetPanelRect()
        {
            if (sessionPanelRect.width > 0f && sessionPanelRect.height > 0f)
                return sessionPanelRect;

            float fallbackPlayerWidth = CalculateStorageColumnWidth(targetInventory, playerGridView);
            float fallbackExternalWidth = CalculateStorageColumnWidth(storageSource, externalGridView);
            return CalculatePanelRect(fallbackPlayerWidth, fallbackExternalWidth);
        }

        private Vector2 ToPanelLocalPosition(Vector2 currentGuiPosition)
        {
            return GUIUtility.GUIToScreenPoint(currentGuiPosition) - GetPanelRect().position;
        }

        private static Rect CalculatePanelRect(float leftColumnWidth, float rightColumnWidth)
        {
            float contentWidth = leftColumnWidth + ColumnGap + CenterColumnWidth + ColumnGap + rightColumnWidth;
            float width = Mathf.Max(1f, Mathf.Min(MaxPanelWidth, Mathf.Min(contentWidth + PanelHorizontalPadding, Screen.width - 24f)));
            float height = Mathf.Max(1f, Mathf.Min(MaxPanelHeight, Screen.height - 48f));
            return new Rect(
                Mathf.Max(0f, (Screen.width - width) * 0.5f),
                Mathf.Max(0f, (Screen.height - height) * 0.5f),
                width,
                height);
        }

        private static float CalculateStorageColumnWidth(IGridStorageOwner owner, InventoryGridDebugView view)
        {
            int gridWidth = owner != null && owner.GridWidth > 0
                ? owner.GridWidth
                : Mathf.Max(1, owner != null ? owner.ConfiguredGridWidth : 6);
            return Mathf.Max(MinimumGridColumnWidth, view.GetRequiredWidth(gridWidth) + GridColumnPadding);
        }

        private void ConsumeDragStatus()
        {
            if (dragController.TryConsumeStatus(out string message, out InventoryToastSeverity severity))
                toast.Show(message, severity);
        }

        private void RequestClose()
        {
            if (sessionController != null)
                sessionController.CloseSession();
            else
                HideFromSession();
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
        }
    }
}
