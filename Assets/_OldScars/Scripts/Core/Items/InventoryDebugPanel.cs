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
        private InventoryUISessionController sessionController;
        private int observedGridSelectionVersion;

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
            isVisible = true;
        }

        internal void HideFromSession()
        {
            isVisible = false;
            gridView.Reset();
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
            GUILayout.BeginHorizontal(GUILayout.Height(bodyHeight));

            DrawPlayerColumn(bodyHeight);

            GUILayout.Space(ColumnGap);
            DrawEquipmentColumn(bodyHeight);

            GUILayout.Space(ColumnGap);
            DrawDetailsColumn(bodyHeight);

            GUILayout.EndHorizontal();
            if (!(sessionController?.BlocksInventoryContentInput ?? false))
                dragController.ProcessOnGUI();
            SyncGridSelectionToSession();
        }

        private void DrawPlayerColumn(float bodyHeight)
        {
            GUILayout.BeginVertical(
                GUI.skin.box,
                GUILayout.Width(PlayerColumnWidth),
                GUILayout.Height(bodyHeight));
            GUILayout.Label("Player Grid (drag; R rotates)");

            GUILayout.BeginHorizontal();
            GUILayout.Label(
                inventory.UsesGridLayout
                    ? $"Grid: {inventory.GridWidth}x{inventory.GridHeight}"
                    : "Grid inactive",
                GUILayout.ExpandWidth(true));
            if (GUILayout.Button(showLegacyList ? "Grid" : "Legacy", GUILayout.Width(76f), GUILayout.Height(24f)))
                showLegacyList = !showLegacyList;
            GUILayout.EndHorizontal();

            if (showLegacyList || !inventory.UsesGridLayout)
            {
                DrawLegacyStorage(bodyHeight - 62f);
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                Rect gridRect = GUILayoutUtility.GetRect(
                    gridView.GetRequiredWidth(inventory.GridWidth),
                    gridView.GetRequiredHeight(inventory.GridHeight),
                    GUILayout.Width(gridView.GetRequiredWidth(inventory.GridWidth)),
                    GUILayout.Height(gridView.GetRequiredHeight(inventory.GridHeight)));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                gridView.Draw(inventory, gridRect, dragController);
                dragController.RegisterEndpoint(inventory, gridView, gridRect);
                HandleGridRightClick(inventory, gridView, gridRect);
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
                HandleEquipmentRowClick);

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
            IReadOnlyList<InventoryContextAction> actions = InventoryContextActionResolver.ResolvePersonal(
                inventory,
                actorEquipment,
                instanceId,
                false);
            sessionController.OpenContextMenu(
                new InventoryContextMenuRequest(
                    InventoryContextSourceKind.Personal,
                    inventory,
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
            if (!TryResolveCurrentContextAction(invocation, out InventoryContextAction currentAction, out int index, out ItemStorageEntry entry))
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
                    return;
                case InventoryContextActionKind.Use:
                    UseItem(index);
                    return;
                case InventoryContextActionKind.Equip:
                    EquipSelected(entry.Item.InstanceId, currentAction.EquipmentSlotIds);
                    return;
                case InventoryContextActionKind.Unequip:
                    UnequipSelected(entry.Item.InstanceId);
                    return;
                case InventoryContextActionKind.DropOne:
                    DropItem(index, 1, "drop_1", "Drop 1");
                    return;
                case InventoryContextActionKind.DropAmount:
                    DropItem(index, quantity, "drop_amount", "Drop Amount");
                    return;
                case InventoryContextActionKind.DropStack:
                    DropItem(index, entry.Quantity, "drop_stack", "Drop Stack");
                    return;
                default:
                    toast.Show("Context action is not valid in the personal session.", InventoryToastSeverity.Error);
                    return;
            }
        }

        private bool TryResolveCurrentContextAction(
            InventoryContextActionInvocation invocation,
            out InventoryContextAction currentAction,
            out int index,
            out ItemStorageEntry entry)
        {
            currentAction = null;
            index = -1;
            entry = null;
            InventoryContextMenuRequest request = invocation.Request;
            if (request == null || invocation.Action == null)
                return false;

            IReadOnlyList<InventoryContextAction> actions;
            if (request.SourceKind == InventoryContextSourceKind.Personal)
            {
                if (!ReferenceEquals(request.Owner, inventory) ||
                    !inventory.TryGetEntryByInstanceId(request.InstanceId, out index, out entry) ||
                    entry?.Item == null)
                {
                    return false;
                }
                actions = InventoryContextActionResolver.ResolvePersonal(
                    inventory,
                    actorEquipment,
                    request.InstanceId,
                    false);
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
            if (string.IsNullOrWhiteSpace(selectedInstanceId) ||
                !inventory.TryGetEntryByInstanceId(selectedInstanceId, out _, out ItemStorageEntry entry))
            {
                GUILayout.Label("Click an item in the grid.");
                return;
            }

            ItemInstance item = entry.Item;
            GUILayout.Label(FormatItemDisplayName(entry));
            GUILayout.Label($"Instance: {item.InstanceId}");
            DrawSelectedItemWeight(entry);
            if (inventory.TryGetGridPlacement(item.InstanceId, out GridPlacement placement))
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

        private void EquipSelected(string instanceId, IReadOnlyList<string> slotIds)
        {
            EquipmentPreview preview = actorEquipment.PreviewEquip(instanceId, slotIds);
            EquipmentMutationResult result = actorEquipment.Equip(preview);
            toast.Show(result.Message, result.Success ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
            if (!result.Success)
                return;

            string primarySlot = result.SlotIds.Length > 0 ? result.SlotIds[0] : null;
            sessionController?.Selection.SelectEquipmentFromContext(primarySlot, result.InstanceId, true);
            gridView.ReconcileSelection(inventory);
            GUIUtility.ExitGUI();
        }

        private void UnequipSelected(string instanceId)
        {
            EquipmentPreview preview = actorEquipment.PreviewUnequip(instanceId);
            EquipmentMutationResult result = actorEquipment.Unequip(preview);
            toast.Show(result.Message, result.Success ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
            if (!result.Success)
                return;

            sessionController?.Selection.ClearEquipment();
            sessionController?.Selection.SelectPersonalFromContext(result.InstanceId);
            gridView.SelectInstance(result.InstanceId);
            GUIUtility.ExitGUI();
        }

        private void DrawLegacyStorage(float height)
        {
            GUILayout.Label("Storage (Legacy List):");
            IReadOnlyList<ItemStorageEntry> entries = inventory.GetStorageEntries();
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
                DrawItemRow(index, entries[index]);
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
                inventory.TryGetItemWeight(
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

        private void DrawItemRow(int index, ItemStorageEntry entry)
        {
            ItemInstance item = entry != null ? entry.Item : null;
            if (GUILayout.Button(GetItemLabel(index, entry), GUILayout.Height(26f)))
            {
                gridView.SelectInstance(item?.InstanceId);
                sessionController?.Selection.SelectPersonal(item?.InstanceId);
            }
        }

        private void DropItem(int index, int quantity, string actionId, string actionDisplayName)
        {
            string sourceInstanceId = inventory.GetEntry(index)?.Item?.InstanceId;
            bool dropped = DroppedWorldItemSpawner.TryDrop(
                inventory,
                index,
                quantity,
                actionId,
                actionDisplayName,
                out string message);

            toast.Show(
                message,
                dropped ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
            if (!dropped)
                Debug.LogWarning($"[InventoryDebugPanel] {message}");

            gridView.ReconcileSelection(inventory);
            if (!string.IsNullOrWhiteSpace(sourceInstanceId) &&
                !inventory.TryGetEntryByInstanceId(sourceInstanceId, out _, out _))
            {
                sessionController?.Selection.ClearPersonalIfMissing(sourceInstanceId);
                sessionController?.CloseContextMenu();
            }
            GUIUtility.ExitGUI();
        }

        private void UseItem(int index)
        {
            string sourceInstanceId = inventory.GetEntry(index)?.Item?.InstanceId;
            ResolveActorNeeds();
            ResolveActorHealth();

            InventoryItemUseResult result = InventoryItemUseService.TryUseItem(inventory, index, actorNeeds, actorHealth);
            toast.Show(
                result.Message,
                result.Success ? InventoryToastSeverity.Success : InventoryToastSeverity.Warning);
            if (!result.Success)
                Debug.Log($"[InventoryDebugPanel] Use failed: {result.Message}");

            gridView.ReconcileSelection(inventory);
            if (!string.IsNullOrWhiteSpace(sourceInstanceId) &&
                !inventory.TryGetEntryByInstanceId(sourceInstanceId, out _, out _))
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
