using System.Collections.Generic;
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
        private const float CenterColumnWidth = 250f;
        private const float ColumnGap = 8f;
        private const float PanelHorizontalPadding = 24f;

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

        private readonly InventoryGridDebugView playerGridView = new InventoryGridDebugView();
        private readonly InventoryGridDebugView externalGridView = new InventoryGridDebugView();
        private readonly InventoryGridDragController dragController = new InventoryGridDragController();
        private readonly InventoryDebugToast toast = new InventoryDebugToast();

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
            executionContext = context;
            action = sourceAction;
            title = source != null ? source.GetStorageDebugTitle(context.Target) : "Storage Debug Panel";
            showPlayerLegacyList = false;
            showExternalLegacyList = false;
            playerLegacyScroll = Vector2.zero;
            externalLegacyScroll = Vector2.zero;
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
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            DrawStorageColumn(targetInventory, playerGridView, true, playerColumnWidth);
            GUILayout.Space(ColumnGap);
            DrawCenterColumn();
            GUILayout.Space(ColumnGap);
            DrawStorageColumn(storageSource, externalGridView, false, externalColumnWidth);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            dragController.ProcessOnGUI();
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
            float columnWidth)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(columnWidth));
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
                DrawLegacyList(owner, view, isPlayer);
            }

            GUILayout.EndVertical();
        }

        private void DrawLegacyList(IGridStorageOwner owner, InventoryGridDebugView view, bool isPlayer)
        {
            IReadOnlyList<ItemStorageEntry> entries = owner.GridStorageEntries;
            Vector2 scroll = isPlayer ? playerLegacyScroll : externalLegacyScroll;
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(410f));
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
                    }
                }
            }
            GUILayout.EndScrollView();

            if (isPlayer)
                playerLegacyScroll = scroll;
            else
                externalLegacyScroll = scroll;
        }

        private void DrawCenterColumn()
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(CenterColumnWidth));
            GUILayout.Label("Inventory Session");
            GUILayout.Label($"Right Hand: {GetRightHandLabel()}");
            GUILayout.Label("Shift + click: transfer stack");
            GUILayout.Label("Drag: empty cell or compatible stack");
            GUILayout.Label("R: rotate during drag");
            GUILayout.Space(8f);

            IGridStorageOwner activeOwner = dragController.ActiveOwner;
            if (activeOwner == null && !string.IsNullOrWhiteSpace(playerGridView.SelectedInstanceId))
                activeOwner = targetInventory;
            else if (activeOwner == null && !string.IsNullOrWhiteSpace(externalGridView.SelectedInstanceId))
                activeOwner = storageSource;
            InventoryGridDebugView activeView = ResolveActiveView(activeOwner, out bool playerSide);
            if (activeOwner == null || activeView == null ||
                !activeView.TryGetSelectedEntry(activeOwner, out _, out ItemStorageEntry entry) ||
                entry == null || entry.Item == null)
            {
                GUILayout.Label("Select an item on either side.");
                DrawDisabledBatchButtons();
                GUILayout.FlexibleSpace();
                GUILayout.Label("Reserved: equipment/paper doll future");
                GUILayout.EndVertical();
                return;
            }

            GUILayout.Label(playerSide ? "Selected side: Player" : "Selected side: External");
            GUILayout.Label(GetEntryLabel(entry));
            GUILayout.Label($"Instance: {entry.Item.InstanceId}");
            if (activeOwner.TryGetGridPlacement(entry.Item.InstanceId, out GridPlacement placement))
            {
                GUILayout.Label(
                    $"Placement: ({placement.X},{placement.Y}) " +
                    $"{placement.EffectiveWidth}x{placement.EffectiveHeight}");
            }

            GUILayout.Space(8f);
            if (playerSide)
            {
                if (GUILayout.Button("Deposit 1", GUILayout.Height(28f)))
                    TransferQuantity(targetInventory, storageSource, entry.Item.InstanceId, 1);
                if (GUILayout.Button("Deposit Stack", GUILayout.Height(28f)))
                    TransferStack(targetInventory, storageSource, entry.Item.InstanceId);
            }
            else
            {
                if (GUILayout.Button("Take 1", GUILayout.Height(28f)))
                    TransferQuantity(storageSource, targetInventory, entry.Item.InstanceId, 1);
                if (GUILayout.Button("Take Stack", GUILayout.Height(28f)))
                    TransferStack(storageSource, targetInventory, entry.Item.InstanceId);
            }

            DrawDisabledBatchButtons();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Reserved: equipment/paper doll future");
            GUILayout.EndVertical();
        }

        private InventoryGridDebugView ResolveActiveView(IGridStorageOwner activeOwner, out bool playerSide)
        {
            if (ReferenceEquals(activeOwner, targetInventory))
            {
                playerSide = true;
                return playerGridView;
            }

            if (ReferenceEquals(activeOwner, storageSource))
            {
                playerSide = false;
                return externalGridView;
            }

            playerSide = true;
            return null;
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

        private static void DrawDisabledBatchButtons()
        {
            GUILayout.Space(8f);
            GUI.enabled = false;
            GUILayout.Button("Take All (disabled)", GUILayout.Height(24f));
            GUILayout.Button("Deposit All (disabled)", GUILayout.Height(24f));
            GUI.enabled = true;
        }

        private string GetRightHandLabel()
        {
            ItemStorageEntry entry = targetInventory != null ? targetInventory.GetRightHandStorageEntry() : null;
            return entry != null ? GetEntryLabel(entry) : "Empty";
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
