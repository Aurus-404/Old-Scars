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
            DrawHeader();

            if (targetInventory == null || storageSource == null)
            {
                GUILayout.Label("Player inventory or external storage source is missing.");
                ConsumeDragStatus();
                toast.Draw(new Rect(0f, 0f, panelRect.width, panelRect.height));
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
            dragController.ProcessOnGUI();
            SyncGridSelectionToSession();
            ConsumeDragStatus();
            toast.Draw(new Rect(0f, 0f, panelRect.width, panelRect.height));
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
                equipmentHeight);
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
                DrawUnavailableTransferButtons();
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
                return;
            }

            DrawSelectedGridDetails(activeOwner, playerSide, entry, CenterDetailsHeight);
            if (playerSide)
            {
                if (GUILayout.Button("Deposit 1", GUILayout.Height(28f)))
                    TransferQuantity(targetInventory, storageSource, entry.Item.InstanceId, 1);
                if (GUILayout.Button("Deposit Stack", GUILayout.Height(28f)))
                    TransferStack(targetInventory, storageSource, entry.Item.InstanceId);
                DrawEquipButtons(entry);
            }
            else
            {
                if (GUILayout.Button("Take 1", GUILayout.Height(28f)))
                    TransferQuantity(storageSource, targetInventory, entry.Item.InstanceId, 1);
                if (GUILayout.Button("Take Stack", GUILayout.Height(28f)))
                    TransferStack(storageSource, targetInventory, entry.Item.InstanceId);
            }

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
                GUI.enabled = false;
                GUILayout.Button("Desequipar al inventario", GUILayout.Height(28f));
                GUI.enabled = true;
                return;
            }

            GUILayout.Label(GetEntryLabel(entry));
            GUILayout.Label($"Instance: {entry.Item.InstanceId}");
            DrawSelectedItemWeight(entry);
            GUILayout.EndScrollView();
            if (GUILayout.Button("Desequipar al inventario", GUILayout.Height(28f)))
            {
                EquipmentMutationResult result = actorEquipment.Unequip(actorEquipment.PreviewUnequip(entry.Item.InstanceId));
                toast.Show(result.Message, result.Success ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
                if (result.Success)
                {
                    selection.ClearEquipment();
                    selection.SelectPersonal(result.InstanceId);
                    playerGridView.SelectInstance(result.InstanceId);
                    GUIUtility.ExitGUI();
                }
            }
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

        private static void DrawUnavailableTransferButtons()
        {
            GUI.enabled = false;
            GUILayout.Button("Take 1 / Deposit 1", GUILayout.Height(28f));
            GUILayout.Button("Take Stack / Deposit Stack", GUILayout.Height(28f));
            GUI.enabled = true;
        }

        private void DrawEquipButtons(ItemStorageEntry entry)
        {
            if (actorEquipment == null || entry?.Item == null)
                return;

            IReadOnlyList<EquipmentSlotSet> alternatives = actorEquipment.GetAvailableSlotSets(entry.Item.InstanceId);
            for (int index = 0; index < alternatives.Count; index++)
            {
                EquipmentSlotSet alternative = alternatives[index];
                if (!GUILayout.Button($"Equipar — {GetSlotSetLabel(alternative.SlotIds)}", GUILayout.Height(28f)))
                    continue;

                EquipmentMutationResult result = actorEquipment.Equip(
                    actorEquipment.PreviewEquip(entry.Item.InstanceId, alternative.SlotIds));
                toast.Show(result.Message, result.Success ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
                if (result.Success)
                {
                    string primarySlot = result.SlotIds.Length > 0 ? result.SlotIds[0] : null;
                    sessionController?.Selection.SelectEquipment(primarySlot, result.InstanceId, true);
                    ReconcileSelections();
                    GUIUtility.ExitGUI();
                }
            }
        }

        private string GetSlotSetLabel(IReadOnlyList<string> slotIds)
        {
            if (slotIds == null || slotIds.Count == 0)
                return "(none)";
            var labels = new string[slotIds.Count];
            for (int index = 0; index < slotIds.Count; index++)
            {
                EquipmentSlotDefinition definition = actorEquipment?.GetSlotDefinition(slotIds[index]);
                labels[index] = definition != null && !string.IsNullOrWhiteSpace(definition.display_name)
                    ? definition.display_name
                    : slotIds[index];
            }
            return string.Join(" + ", labels);
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

        private void TransferQuantity(IGridStorageOwner source, IGridStorageOwner target, string instanceId, int quantity)
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
        }

        private void TransferStack(IGridStorageOwner source, IGridStorageOwner target, string instanceId)
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
