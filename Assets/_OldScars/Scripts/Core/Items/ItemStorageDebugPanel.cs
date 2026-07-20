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
        private enum LootableActorView
        {
            Empty,
            Equipment,
            Inventory,
            Containers,
            ContainerStorage
        }

        private const float MaxPanelWidth = 1120f;
        private const float MaxPanelHeight = 700f;
        private const float MinimumGridColumnWidth = 220f;
        private const float MaximumGridColumnWidth = 340f;
        private const float GridColumnPadding = 24f;
        private const float CenterColumnWidth = 330f;
        private const float ColumnGap = 8f;
        private const float PanelHorizontalPadding = 24f;
        private const float BodyVerticalReserve = 42f;
        private const float MinimumEquipmentViewportHeight = 300f;
        private const float MaximumEquipmentViewportHeight = 330f;
        private const float CenterDetailsHeight = 72f;
        private const float ScrollbarSize = 16f;

        [SerializeField, Range(20f, 64f)] private float gridVisualCellSize = 32f;

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
        private LootableActorInventoryComponent lootableActorSource;
        private LootableActorView lootableActorView;
        private ItemOwnedStorageRuntime lootableOwnedStorage;
        private string lootableOwnedStorageInstanceId;
        private Vector2 lootableContainersScroll;
        private Vector2 centerDetailsScrollPosition;
        private Vector2 playerGridScroll;
        private Vector2 externalGridScroll;
        private PersonalStorageNavigator personalStorageNavigator;

        private readonly InventoryGridDebugView playerGridView = new InventoryGridDebugView();
        private readonly InventoryGridDebugView externalGridView = new InventoryGridDebugView();
        private readonly InventoryGridDragController dragController = new InventoryGridDragController();
        private readonly InventoryDebugToast toast = new InventoryDebugToast();
        private readonly EquipmentDebugListView equipmentListView = new EquipmentDebugListView();
        private readonly InventoryUISessionSelection lootableEquipmentSelection =
            new InventoryUISessionSelection();
        private readonly OwnedStorageInspectionDebugView inspectionView = new OwnedStorageInspectionDebugView();
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
            lootableActorSource = source as LootableActorInventoryComponent;
            targetInventory = inventory;
            actorEquipment = targetInventory != null ? targetInventory.GetComponent<ActorEquipmentComponent>() : null;
            personalStorageNavigator = sessionController != null
                ? sessionController.PersonalStorageNavigator
                : new PersonalStorageNavigator(targetInventory);
            executionContext = context;
            action = sourceAction;
            title = source != null ? source.GetStorageDebugTitle(context.Target) : "Storage Debug Panel";
            showPlayerLegacyList = false;
            showExternalLegacyList = false;
            playerLegacyScroll = Vector2.zero;
            externalLegacyScroll = Vector2.zero;
            centerDetailsScrollPosition = Vector2.zero;
            playerGridScroll = Vector2.zero;
            externalGridScroll = Vector2.zero;
            lootableContainersScroll = Vector2.zero;
            lootableOwnedStorage = null;
            lootableOwnedStorageInstanceId = null;
            lootableEquipmentSelection.ResetTransient();
            lootableActorView = GetInitialLootableActorView();
            playerGridView.SetVisualCellSize(gridVisualCellSize);
            externalGridView.SetVisualCellSize(gridVisualCellSize);
            inspectionView.Reset(gridVisualCellSize);
            playerGridView.Reset();
            externalGridView.Reset();
            dragController.Reset();
            inspectionView.Reset(gridVisualCellSize);
            toast.Clear();
            playerColumnWidth = CalculateStorageColumnWidth(targetInventory, playerGridView);
            externalColumnWidth = lootableActorSource != null
                ? MaximumGridColumnWidth
                : CalculateStorageColumnWidth(storageSource, externalGridView);
            sessionPanelRect = CalculatePanelRect(playerColumnWidth, externalColumnWidth);
            isVisible = true;
        }

        internal void HideFromSession()
        {
            isVisible = false;
            storageSource = null;
            lootableActorSource = null;
            lootableActorView = LootableActorView.Empty;
            lootableOwnedStorage = null;
            lootableOwnedStorageInstanceId = null;
            lootableContainersScroll = Vector2.zero;
            lootableEquipmentSelection.ResetTransient();
            targetInventory = null;
            actorEquipment = null;
            personalStorageNavigator = null;
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

            ReconcileLootableActorView();
            dragController.BeginFrame(new GridStorageTransferContext(executionContext, action));
            personalStorageNavigator?.Refresh();
            IGridStorageOwner personalOwner = GetActivePersonalOwner();
            float bodyHeight = Mathf.Max(1f, panelRect.height - BodyVerticalReserve);
            GUILayout.BeginHorizontal(GUILayout.Height(bodyHeight));
            GUILayout.FlexibleSpace();
            DrawStorageColumn(personalOwner, playerGridView, true, playerColumnWidth, bodyHeight);
            GUILayout.Space(ColumnGap);
            DrawCenterColumn(bodyHeight);
            GUILayout.Space(ColumnGap);
            if (lootableActorSource != null)
                DrawLootableActorColumn(externalColumnWidth, bodyHeight);
            else
                DrawStorageColumn(storageSource, externalGridView, false, externalColumnWidth, bodyHeight);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            DrawOwnedStorageInspection(panelRect);
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

        private void DrawOwnedStorageInspection(Rect panelRect)
        {
            sessionController?.ValidateOwnedStorageInspection();
            ItemOwnedStorageRuntime inspected = sessionController?.InspectedOwnedStorage;
            if (inspected == null)
                return;

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
                targetInventory,
                actorEquipment,
                personalStorageNavigator,
                rightClickedInstanceId,
                true);
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

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(!string.IsNullOrWhiteSpace(title) ? title : "Storage Debug Panel");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(90f), GUILayout.Height(24f)))
                RequestClose();
            GUILayout.EndHorizontal();
        }

        private LootableActorView GetInitialLootableActorView()
        {
            if (lootableActorSource == null || !lootableActorSource.HasLootableContent)
                return LootableActorView.Empty;
            if (lootableActorSource.HasEquipmentItems)
                return LootableActorView.Equipment;
            return LootableActorView.Inventory;
        }

        private void DrawLootableActorColumn(float columnWidth, float bodyHeight)
        {
            GUILayout.BeginVertical(
                GUI.skin.box,
                GUILayout.Width(columnWidth),
                GUILayout.Height(bodyHeight));
            GUILayout.Label(lootableActorSource.GetActorDisplayName(executionContext.Target));
            DrawLootableActorTabs();

            float contentHeight = Mathf.Max(120f, bodyHeight - 68f);
            switch (lootableActorView)
            {
                case LootableActorView.Equipment:
                    DrawLootableEquipmentView(columnWidth, contentHeight);
                    break;
                case LootableActorView.Inventory:
                    DrawStorageColumnBody(
                        storageSource,
                        externalGridView,
                        false,
                        columnWidth,
                        contentHeight,
                        "Inventario de la entidad");
                    break;
                case LootableActorView.Containers:
                    DrawLootableContainersView(contentHeight);
                    break;
                case LootableActorView.ContainerStorage:
                    DrawLootableContainerStorage(columnWidth, contentHeight);
                    break;
                default:
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("No quedan pertenencias reales en esta entidad.");
                    GUILayout.Label("Inventario, Equipment y contenedores equipados estan vacios.");
                    GUILayout.FlexibleSpace();
                    break;
            }
            GUILayout.EndVertical();
        }

        private void DrawLootableActorTabs()
        {
            GUILayout.BeginHorizontal();
            DrawLootableActorTab("Equipamiento", LootableActorView.Equipment);
            DrawLootableActorTab("Inventario", LootableActorView.Inventory);
            DrawLootableActorTab("Contenedores", LootableActorView.Containers);
            GUILayout.EndHorizontal();
        }

        private void DrawLootableActorTab(string label, LootableActorView view)
        {
            Color previous = GUI.backgroundColor;
            if (lootableActorView == view ||
                (view == LootableActorView.Containers && lootableActorView == LootableActorView.ContainerStorage))
            {
                GUI.backgroundColor = new Color(0.32f, 0.68f, 0.92f);
            }
            if (GUILayout.Button(label, GUILayout.Height(26f)))
                SelectLootableActorView(view);
            GUI.backgroundColor = previous;
        }

        private void SelectLootableActorView(LootableActorView view)
        {
            dragController.CancelDrag();
            sessionController?.CloseContextMenu();
            lootableActorView = view;
            lootableOwnedStorage = null;
            lootableOwnedStorageInstanceId = null;
            externalGridView.Reset();
            externalGridScroll = Vector2.zero;
            sessionController?.Selection.SelectExternal(null);
        }

        private void DrawLootableEquipmentView(float columnWidth, float contentHeight)
        {
            ActorEquipmentComponent equipment = lootableActorSource.Equipment;
            float listHeight = Mathf.Max(180f, contentHeight - 92f);
            equipmentListView.Draw(
                equipment,
                lootableEquipmentSelection,
                columnWidth - 12f,
                listHeight,
                HandleLootableEquipmentRowClick,
                null,
                EquipmentDebugListPresentation.OccupiedItemsOnly);

            string selectedId = lootableEquipmentSelection.SelectedEquippedInstanceId;
            if (equipment == null || string.IsNullOrWhiteSpace(selectedId) ||
                !equipment.TryGetEntryByInstanceId(selectedId, out ItemStorageEntry entry) ||
                entry?.Item == null)
            {
                GUILayout.Label("Selecciona un item equipado; clic derecho para tomarlo.");
                return;
            }

            GUILayout.Label(GetEntryLabel(entry));
            GUILayout.Label($"Instance: {entry.Item.InstanceId} | Condition: {entry.Item.Condition:0.##}");
            GUILayout.Label($"Slots: {GetOccupiedSlotLabel(equipment, selectedId)}");
        }

        private void HandleLootableEquipmentRowClick(EquipmentDebugRowClick click)
        {
            if (click.MouseButton != 1 || sessionController == null || sessionController.QuantityDialogOpen)
                return;

            ActorEquipmentComponent equipment = lootableActorSource?.Equipment;
            if (equipment == null || string.IsNullOrWhiteSpace(click.InstanceId) ||
                !equipment.TryGetEntryByInstanceId(click.InstanceId, out ItemStorageEntry entry) ||
                entry?.Item == null)
            {
                sessionController.CloseContextMenu();
                return;
            }

            dragController.CancelDrag();
            lootableEquipmentSelection.SelectEquipment(click.SlotId, click.InstanceId, false);
            IReadOnlyList<InventoryContextAction> actions =
                InventoryContextActionResolver.ResolveLootableEquipment(
                    equipment,
                    click.SlotId,
                    click.InstanceId);
            sessionController.OpenContextMenu(
                new InventoryContextMenuRequest(
                    InventoryContextSourceKind.Equipment,
                    null,
                    equipment,
                    click.InstanceId,
                    click.SlotId,
                    entry.Quantity,
                    actions,
                    ExecuteContextAction,
                    equipment.GetSlotsOccupiedBy(click.InstanceId)),
                Event.current != null
                    ? ToPanelLocalPosition(Event.current.mousePosition)
                    : click.RowRect.position);
        }

        private void DrawLootableContainersView(float contentHeight)
        {
            IReadOnlyList<LootableActorOwnedStorage> options =
                lootableActorSource.GetEquippedOwnedStorages();
            if (options.Count == 0)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("La entidad no tiene contenedores equipados.");
                GUILayout.FlexibleSpace();
                return;
            }

            lootableContainersScroll = GUILayout.BeginScrollView(
                lootableContainersScroll,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUILayout.Height(contentHeight));
            for (int index = 0; index < options.Count; index++)
            {
                LootableActorOwnedStorage option = options[index];
                ItemStorageEntry entry = option.ContainerEntry;
                string itemName = entry != null ? GetItemDisplayName(entry.DefinitionId) : "Contenedor";
                string slot = option.OccupiedSlots != null && option.OccupiedSlots.Count > 0
                    ? GetSlotDisplayName(lootableActorSource.Equipment, option.OccupiedSlots[0])
                    : "sin slot";
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"{itemName} - {slot}");
                GUILayout.Label(
                    $"Contenido: {option.Storage.GridStorageEntries.Count} entries | " +
                    $"Grilla: {option.Storage.GridWidth}x{option.Storage.GridHeight}");
                if (GUILayout.Button("Abrir", GUILayout.Height(24f)))
                    OpenLootableOwnedStorage(option.Storage.ContainerInstanceId);
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
        }

        private void DrawLootableContainerStorage(float columnWidth, float contentHeight)
        {
            if (lootableOwnedStorage == null)
            {
                GUILayout.Label("El contenedor equipado ya no esta disponible.");
                return;
            }

            if (GUILayout.Button("Volver a contenedores", GUILayout.Height(26f)))
            {
                SelectLootableActorView(LootableActorView.Containers);
                return;
            }

            DrawStorageColumnBody(
                lootableOwnedStorage,
                externalGridView,
                false,
                columnWidth,
                Mathf.Max(100f, contentHeight - 30f),
                lootableOwnedStorage.GridStorageDisplayName);
        }

        private void ReconcileLootableActorView()
        {
            if (lootableActorSource == null)
                return;

            lootableActorSource.RefreshLootableState();
            if (!lootableActorSource.HasLootableContent)
            {
                lootableActorView = LootableActorView.Empty;
                lootableOwnedStorage = null;
                lootableOwnedStorageInstanceId = null;
                return;
            }

            if (lootableActorView == LootableActorView.Empty)
                lootableActorView = GetInitialLootableActorView();
            if (lootableActorView == LootableActorView.Equipment && !lootableActorSource.HasEquipmentItems)
                lootableActorView = LootableActorView.Inventory;

            if (lootableActorView != LootableActorView.ContainerStorage)
                return;

            if (!lootableActorSource.TryGetEquippedOwnedStorage(
                    lootableOwnedStorageInstanceId,
                    out LootableActorOwnedStorage current) ||
                !ReferenceEquals(current.Storage, lootableOwnedStorage))
            {
                lootableActorView = LootableActorView.Containers;
                lootableOwnedStorage = null;
                lootableOwnedStorageInstanceId = null;
                externalGridView.Reset();
                externalGridScroll = Vector2.zero;
                sessionController?.CloseContextMenu();
                sessionController?.Selection.SelectExternal(null);
            }
        }

        private IGridStorageOwner GetActiveExternalOwner()
        {
            return lootableActorView == LootableActorView.ContainerStorage && lootableOwnedStorage != null
                ? lootableOwnedStorage
                : storageSource;
        }

        private bool OpenLootableOwnedStorage(string containerInstanceId)
        {
            if (lootableActorSource == null ||
                !lootableActorSource.TryGetEquippedOwnedStorage(containerInstanceId, out LootableActorOwnedStorage option) ||
                option.Storage == null)
            {
                return false;
            }

            dragController.CancelDrag();
            sessionController?.CloseContextMenu();
            lootableOwnedStorage = option.Storage;
            lootableOwnedStorageInstanceId = option.Storage.ContainerInstanceId;
            lootableActorView = LootableActorView.ContainerStorage;
            externalGridView.Reset();
            externalGridScroll = Vector2.zero;
            sessionController?.Selection.SelectExternal(null);
            return true;
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
            DrawStorageColumnBody(
                owner,
                view,
                isPlayer,
                columnWidth,
                bodyHeight,
                isPlayer ? "Player Grid" : "External Storage Grid");
            GUILayout.EndVertical();
        }

        private void DrawStorageColumnBody(
            IGridStorageOwner owner,
            InventoryGridDebugView view,
            bool isPlayer,
            float columnWidth,
            float bodyHeight,
            string heading)
        {
            bool previousEnabled = GUI.enabled;
            if (!isPlayer && (sessionController?.HasOwnedStorageInspection ?? false))
                GUI.enabled = false;
            GUILayout.Label(heading);
            if (isPlayer)
                DrawPersonalStorageSelector();

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
                DrawScrollableGrid(owner, view, isPlayer, columnWidth, bodyHeight - (isPlayer ? 100f : 72f));
            }
            else
            {
                DrawLegacyList(owner, view, isPlayer, bodyHeight - 70f);
            }

            GUILayout.FlexibleSpace();
            GUI.enabled = previousEnabled;
        }

        private IGridStorageOwner GetActivePersonalOwner()
        {
            return personalStorageNavigator?.SelectedOwner ?? targetInventory;
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

            IGridStorageOwner selectedOwner = GetActivePersonalOwner();
            GUILayout.Label($"{selectedOwner.GridWidth}x{selectedOwner.GridHeight} | cell {gridVisualCellSize:0}px");
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

            playerGridView.Reset();
            playerGridScroll = Vector2.zero;
            sessionController?.Selection.SelectPersonal(null);
        }

        private void DrawScrollableGrid(
            IGridStorageOwner owner,
            InventoryGridDebugView view,
            bool isPlayer,
            float columnWidth,
            float availableHeight)
        {
            float viewportWidth = Mathf.Max(120f, columnWidth - 16f);
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

            float contentWidth = view.GetRequiredWidth(owner.GridWidth);
            float contentHeight = view.GetRequiredHeight(owner.GridHeight);
            Vector2 scroll = isPlayer ? playerGridScroll : externalGridScroll;
            float maxX = Mathf.Max(0f, contentWidth - clipRect.width);
            float maxY = Mathf.Max(0f, contentHeight - clipRect.height);
            scroll.x = Mathf.Clamp(scroll.x, 0f, maxX);
            scroll.y = Mathf.Clamp(scroll.y, 0f, maxY);

            scroll.x = GUI.HorizontalScrollbar(
                new Rect(areaRect.x, clipRect.yMax, clipRect.width, ScrollbarSize),
                scroll.x,
                clipRect.width,
                0f,
                Mathf.Max(clipRect.width, contentWidth));
            scroll.y = GUI.VerticalScrollbar(
                new Rect(clipRect.xMax, areaRect.y, ScrollbarSize, clipRect.height),
                scroll.y,
                clipRect.height,
                0f,
                Mathf.Max(clipRect.height, contentHeight));

            GUI.Box(clipRect, GUIContent.none);
            GUI.BeginGroup(clipRect);
            Rect localGridRect = new Rect(-scroll.x, -scroll.y, contentWidth, contentHeight);
            view.Draw(owner, localGridRect, dragController);
            bool blocksExternal = !isPlayer && (sessionController?.HasOwnedStorageInspection ?? false);
            if (!blocksExternal)
                HandleGridRightClick(owner, view, localGridRect, isPlayer);
            GUI.EndGroup();

            Rect globalGridRect = new Rect(
                clipRect.x - scroll.x,
                clipRect.y - scroll.y,
                contentWidth,
                contentHeight);
            if (!blocksExternal)
                dragController.RegisterEndpoint(owner, view, globalGridRect, clipRect);

            if (isPlayer)
                playerGridScroll = scroll;
            else
                externalGridScroll = scroll;
        }

        private void SyncGridSelectionToSession()
        {
            if (dragController.SelectionVersion == observedGridSelectionVersion)
                return;

            observedGridSelectionVersion = dragController.SelectionVersion;
            InventoryUISessionSelection selection = sessionController?.Selection;
            if (selection == null)
                return;

            IGridStorageOwner personalOwner = GetActivePersonalOwner();
            if (ReferenceEquals(dragController.ActiveOwner, personalOwner) &&
                !string.IsNullOrWhiteSpace(playerGridView.SelectedInstanceId))
            {
                selection.SelectPersonal(playerGridView.SelectedInstanceId);
            }
            else if (ReferenceEquals(dragController.ActiveOwner, GetActiveExternalOwner()) &&
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
                actions = InventoryContextActionResolver.ResolvePersonalCompartment(
                    owner,
                    targetInventory,
                    actorEquipment,
                    personalStorageNavigator,
                    instanceId,
                    true);
            }
            else
            {
                sessionController.Selection.SelectExternalFromContext(instanceId);
                sourceKind = InventoryContextSourceKind.External;
                actions = InventoryContextActionResolver.ResolveExternal(owner, instanceId);
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
                click.InstanceId,
                personalStorageNavigator,
                GetActiveExternalOwner(),
                new GridStorageTransferContext(executionContext, action));
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
            if (actorEquipment == null || request.SourceOwner == null)
            {
                return new InventoryEquipmentDropResult(false, "El origen o equipment ya no está disponible.");
            }

            bool actorOwned = ItemOwnedStorageRegistry.Instance.ShareRootOwner(request.SourceOwner, targetInventory);
            if (actorOwned && TryGetCompatibleSlotSet(request.SourceOwner, request.SourceInstanceId, request.SlotId, out string[] slotSet))
            {
                EquipmentPreview preview = actorEquipment.PreviewEquip(request.SourceOwner, request.SourceInstanceId, slotSet);
                bool replacing = preview.FailureCode == EquipmentFailureCode.SlotOccupied;
                EquipmentMutationResult result = replacing
                    ? actorEquipment.EquipReplacing(
                        request.SourceOwner,
                        actorEquipment.PreviewEquipReplacing(request.SourceOwner, request.SourceInstanceId, slotSet))
                    : actorEquipment.Equip(request.SourceOwner, preview);
                if (!result.Success)
                {
                    return new InventoryEquipmentDropResult(
                        false,
                        EquipmentFailureMessageFormatter.FormatFailure(result.FailureCode, actorEquipment, slotSet));
                }

                personalStorageNavigator?.Refresh();
                string primarySlot = result.SlotIds.Length > 0 ? result.SlotIds[0] : request.SlotId;
                sessionController?.Selection.SelectEquipmentFromContext(primarySlot, result.InstanceId, true);
                ReconcileSelections();
                return new InventoryEquipmentDropResult(
                    true,
                    EquipmentFailureMessageFormatter.FormatSuccess(actorEquipment, result.InstanceId, false, replacing));
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
                new GridStorageTransferContext(executionContext, action));
            if (!transfer.Success)
                return new InventoryEquipmentDropResult(false, transfer.Message ?? "No se pudo guardar el objeto.");

            IGridStorageOwner visibleOwner = GetActivePersonalOwner();
            ReconcileSelections();
            if (ReferenceEquals(request.SourceOwner, visibleOwner) &&
                !request.SourceOwner.TryGetEntryByInstanceId(request.SourceInstanceId, out _, out _))
            {
                sessionController?.Selection.ClearPersonalIfMissing(request.SourceInstanceId);
            }
            else if (ReferenceEquals(request.SourceOwner, GetActiveExternalOwner()) &&
                     !request.SourceOwner.TryGetEntryByInstanceId(request.SourceInstanceId, out _, out _))
            {
                sessionController?.Selection.ClearExternalIfMissing(request.SourceInstanceId);
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
                playerGridView.Reset();
                playerGridScroll = Vector2.zero;
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
                    if (invocation.Request.SourceKind == InventoryContextSourceKind.Equipment)
                    {
                        if (lootableActorSource != null &&
                            ReferenceEquals(invocation.Request.Equipment, lootableActorSource.Equipment))
                        {
                            lootableEquipmentSelection.SelectEquipment(
                                invocation.Request.EquipmentSlotId,
                                entry.Item.InstanceId,
                                false);
                        }
                        else
                        {
                            sessionController?.Selection.SelectEquipmentFromContext(
                                invocation.Request.EquipmentSlotId,
                                entry.Item.InstanceId,
                                true);
                        }
                    }
                    return;
                case InventoryContextActionKind.ReviewOwnedStorage:
                    if (invocation.Request.SourceKind == InventoryContextSourceKind.Equipment &&
                        lootableActorSource != null &&
                        ReferenceEquals(invocation.Request.Equipment, lootableActorSource.Equipment))
                    {
                        if (!OpenLootableOwnedStorage(entry.Item.InstanceId))
                            toast.Show("El contenedor equipado ya no pertenece al cadáver.", InventoryToastSeverity.Error);
                        return;
                    }
                    if (sessionController == null || !sessionController.OpenOwnedStorageInspection(entry.Item.InstanceId))
                        toast.Show("La mochila ya no pertenece al actor.", InventoryToastSeverity.Error);
                    return;
                case InventoryContextActionKind.Use:
                    UsePersonalItem(owner, entry.Item.InstanceId);
                    return;
                case InventoryContextActionKind.Equip:
                    if (invocation.Request.SourceKind == InventoryContextSourceKind.Equipment)
                        RelocateEquipmentItem(entry.Item.InstanceId, currentAction.EquipmentSlotIds);
                    else
                        EquipPersonalItem(owner, entry.Item.InstanceId, currentAction.EquipmentSlotIds);
                    return;
                case InventoryContextActionKind.EquipReplacing:
                    if (invocation.Request.SourceKind == InventoryContextSourceKind.Equipment)
                        RelocateEquipmentItem(entry.Item.InstanceId, currentAction.EquipmentSlotIds);
                    else
                        EquipReplacingPersonalItem(owner, entry.Item.InstanceId, currentAction.EquipmentSlotIds);
                    return;
                case InventoryContextActionKind.Unequip:
                    UnequipEquipmentItem(entry.Item.InstanceId);
                    return;
                case InventoryContextActionKind.DropOne:
                    DropPersonalItem(owner, entry.Item.InstanceId, 1, "drop_1", "Drop 1");
                    return;
                case InventoryContextActionKind.DropAmount:
                    DropPersonalItem(owner, entry.Item.InstanceId, requestedQuantity, "drop_amount", "Drop Amount");
                    return;
                case InventoryContextActionKind.DropStack:
                    if (invocation.Request.SourceKind == InventoryContextSourceKind.Equipment)
                        DropEquipmentItem(entry.Item.InstanceId);
                    else
                        DropPersonalItem(owner, entry.Item.InstanceId, entry.Quantity, "drop_stack", "Drop Stack");
                    return;
                case InventoryContextActionKind.TakeOne:
                    ApplyContextTransfer(TransferQuantity(owner, GetActivePersonalOwner(), entry.Item.InstanceId, 1), true);
                    return;
                case InventoryContextActionKind.TakeAmount:
                    ApplyContextTransfer(TransferQuantity(owner, GetActivePersonalOwner(), entry.Item.InstanceId, requestedQuantity), true);
                    return;
                case InventoryContextActionKind.TakeStack:
                    if (invocation.Request.SourceKind == InventoryContextSourceKind.Equipment &&
                        lootableActorSource != null &&
                        ReferenceEquals(invocation.Request.Equipment, lootableActorSource.Equipment))
                    {
                        TransferLootableEquipmentItem(invocation.Request.Equipment, entry.Item.InstanceId);
                        return;
                    }
                    ApplyContextTransfer(TransferStack(owner, GetActivePersonalOwner(), entry.Item.InstanceId), true);
                    return;
                case InventoryContextActionKind.DepositOne:
                    ApplyContextTransfer(TransferQuantity(owner, GetActiveExternalOwner(), entry.Item.InstanceId, 1), false);
                    return;
                case InventoryContextActionKind.DepositAmount:
                    ApplyContextTransfer(TransferQuantity(owner, GetActiveExternalOwner(), entry.Item.InstanceId, requestedQuantity), false);
                    return;
                case InventoryContextActionKind.DepositStack:
                    if (invocation.Request.SourceKind == InventoryContextSourceKind.Equipment)
                    {
                        TransferEquipmentItem(
                            entry.Item.InstanceId,
                            GetActiveExternalOwner(),
                            new GridStorageTransferContext(executionContext, action),
                            false);
                    }
                    else
                    {
                        ApplyContextTransfer(TransferStack(owner, GetActiveExternalOwner(), entry.Item.InstanceId), false);
                    }
                    return;
                case InventoryContextActionKind.MoveToPersonalOne:
                    ApplyPersonalCompartmentTransfer(TransferQuantity(owner, targetInventory, entry.Item.InstanceId, 1));
                    return;
                case InventoryContextActionKind.MoveToPersonalAmount:
                    ApplyPersonalCompartmentTransfer(TransferQuantity(owner, targetInventory, entry.Item.InstanceId, requestedQuantity));
                    return;
                case InventoryContextActionKind.MoveToPersonalStack:
                    ApplyPersonalCompartmentTransfer(TransferStack(owner, targetInventory, entry.Item.InstanceId));
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
                            ? requestedQuantity
                            : entry.Quantity;
                    if (invocation.Request.SourceKind == InventoryContextSourceKind.Equipment)
                    {
                        TransferEquipmentItem(entry.Item.InstanceId, ownedTarget, default, true);
                    }
                    else
                    {
                        ApplyPersonalCompartmentTransfer(
                            currentAction.Kind == InventoryContextActionKind.MoveToOwnedStorageStack
                                ? TransferStack(owner, ownedTarget, entry.Item.InstanceId)
                                : TransferQuantity(owner, ownedTarget, entry.Item.InstanceId, moveQuantity));
                    }
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
                owner = GetActivePersonalOwner();
                if (!ReferenceEquals(request.Owner, owner) || storageSource == null ||
                    owner == null || !owner.TryGetEntryByInstanceId(request.InstanceId, out index, out entry) || entry?.Item == null)
                {
                    return false;
                }
                actions = InventoryContextActionResolver.ResolvePersonalCompartment(
                    owner,
                    targetInventory,
                    actorEquipment,
                    personalStorageNavigator,
                    request.InstanceId,
                    true);
            }
            else if (request.SourceKind == InventoryContextSourceKind.External)
            {
                owner = GetActiveExternalOwner();
                if (!ReferenceEquals(request.Owner, owner) || owner == null ||
                    !owner.TryGetEntryByInstanceId(request.InstanceId, out index, out entry) || entry?.Item == null)
                {
                    return false;
                }
                actions = InventoryContextActionResolver.ResolveExternal(owner, request.InstanceId);
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
                    targetInventory,
                    actorEquipment,
                    personalStorageNavigator,
                    request.InstanceId,
                    true);
            }
            else if (request.SourceKind == InventoryContextSourceKind.Equipment)
            {
                ActorEquipmentComponent requestedEquipment = request.Equipment;
                if (requestedEquipment == null ||
                    requestedEquipment.GetEquippedStorageEntry(request.EquipmentSlotId)?.Item?.InstanceId != request.InstanceId ||
                    !SameSlotSet(request.SourceEquipmentSlotIds, requestedEquipment.GetSlotsOccupiedBy(request.InstanceId)) ||
                    !requestedEquipment.TryGetEntryByInstanceId(request.InstanceId, out entry) || entry?.Item == null)
                {
                    return false;
                }

                if (ReferenceEquals(requestedEquipment, actorEquipment))
                {
                    actions = InventoryContextActionResolver.ResolveEquipment(
                        actorEquipment,
                        request.EquipmentSlotId,
                        request.InstanceId,
                        personalStorageNavigator,
                        GetActiveExternalOwner(),
                        new GridStorageTransferContext(executionContext, action));
                }
                else if (lootableActorSource != null &&
                         ReferenceEquals(requestedEquipment, lootableActorSource.Equipment))
                {
                    actions = InventoryContextActionResolver.ResolveLootableEquipment(
                        requestedEquipment,
                        request.EquipmentSlotId,
                        request.InstanceId);
                }
                else
                {
                    return false;
                }
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
                HandleEquipmentRowClick,
                RegisterEquipmentDropTarget,
                EquipmentDebugListPresentation.OccupiedItemsOnly);
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
                owner = GetActivePersonalOwner();
                playerSide = true;
                instanceId = selection.SelectedPersonalItemInstanceId;
            }
            else if (selection.ActiveSide == InventoryUIActiveSide.External)
            {
                owner = GetActiveExternalOwner();
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

        private void UsePersonalItem(IGridStorageOwner sourceOwner, string instanceId)
        {
            ActorNeedsComponent needs = targetInventory.GetComponentInParent<ActorNeedsComponent>();
            ActorHealthComponent health = targetInventory.GetComponentInParent<ActorHealthComponent>();
            InventoryItemUseResult result = InventoryItemUseService.TryUseItem(
                sourceOwner,
                instanceId,
                targetInventory,
                needs,
                health);
            toast.Show(result.Message, result.Success ? InventoryToastSeverity.Success : InventoryToastSeverity.Warning);
            ReconcileSelections();
            if (!sourceOwner.TryGetEntryByInstanceId(instanceId, out _, out _))
                sessionController?.Selection.ClearPersonalIfMissing(instanceId);
        }

        private void EquipPersonalItem(IGridStorageOwner sourceOwner, string instanceId, IReadOnlyList<string> slotIds)
        {
            EquipmentMutationResult result = actorEquipment != null
                ? actorEquipment.Equip(sourceOwner, actorEquipment.PreviewEquip(sourceOwner, instanceId, slotIds))
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

        private void EquipReplacingPersonalItem(IGridStorageOwner sourceOwner, string instanceId, IReadOnlyList<string> slotIds)
        {
            EquipmentReplacementPlan plan = actorEquipment != null
                ? actorEquipment.PreviewEquipReplacing(sourceOwner, instanceId, slotIds)
                : null;
            EquipmentMutationResult result = actorEquipment != null
                ? actorEquipment.EquipReplacing(sourceOwner, plan)
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
            ReconcileSelections();
            GUIUtility.ExitGUI();
        }

        private void TransferEquipmentItem(
            string instanceId,
            IGridStorageOwner destination,
            GridStorageTransferContext context,
            bool personalDestination)
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
            if (personalDestination && ReferenceEquals(GetActivePersonalOwner(), destination))
            {
                playerGridView.SelectInstance(result.InstanceId);
                sessionController?.Selection.SelectPersonalFromContext(result.InstanceId);
            }
            else if (!personalDestination)
            {
                externalGridView.SelectInstance(result.InstanceId);
                sessionController?.Selection.SelectExternalFromContext(result.InstanceId);
            }
            sessionController?.CloseContextMenu();
            ReconcileSelections();
            GUIUtility.ExitGUI();
        }

        private void TransferLootableEquipmentItem(
            ActorEquipmentComponent sourceEquipment,
            string instanceId)
        {
            GridStorageTransferContext context = new GridStorageTransferContext(executionContext, action);
            EquipmentStorageTransferPlan plan = sourceEquipment?.PreviewTransferEquippedToStorage(
                instanceId,
                targetInventory,
                context);
            EquipmentMutationResult result = sourceEquipment != null
                ? sourceEquipment.TransferEquippedToStorage(targetInventory, plan, context)
                : EquipmentMutationResult.Rejected(
                    "Lootable actor equipment is unavailable.",
                    instanceId,
                    EquipmentFailureCode.MissingDependencies);
            string transferredName = result.InstanceId;
            if (result.Success &&
                targetInventory.TryGetEntryByInstanceId(
                    result.InstanceId,
                    out _,
                    out ItemStorageEntry transferredEntry))
            {
                transferredName = GetItemDisplayName(transferredEntry.DefinitionId);
            }
            toast.Show(
                result.Success
                    ? $"Tomaste {transferredName}."
                    : result.Message ?? "No se pudo retirar el item equipado.",
                result.Success ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
            if (!result.Success)
                return;

            personalStorageNavigator?.SelectPersonalInventory();
            lootableEquipmentSelection.ClearEquipment();
            playerGridView.SelectInstance(result.InstanceId);
            sessionController?.Selection.SelectPersonalFromContext(result.InstanceId);
            sessionController?.CloseContextMenu();
            lootableActorSource?.RefreshLootableState();
            ReconcileLootableActorView();
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

        private static string[] GetDisplacedIds(EquipmentRelocationPlan plan)
        {
            int count = plan?.DisplacedItems?.Length ?? 0;
            var result = new string[count];
            for (int index = 0; index < count; index++)
                result[index] = plan.DisplacedItems[index]?.InstanceId;
            return result;
        }

        private void DropPersonalItem(
            IGridStorageOwner sourceOwner,
            string instanceId,
            int quantity,
            string actionId,
            string actionDisplayName)
        {
            bool success = DroppedWorldItemSpawner.TryDrop(
                sourceOwner,
                instanceId,
                targetInventory,
                quantity,
                actionId,
                actionDisplayName,
                out string message);
            toast.Show(message, success ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
            ReconcileSelections();
            if (!sourceOwner.TryGetEntryByInstanceId(instanceId, out _, out _))
                sessionController?.Selection.ClearPersonalIfMissing(instanceId);
            GUIUtility.ExitGUI();
        }

        private void DropEquipmentItem(string instanceId)
        {
            bool success = DroppedWorldItemSpawner.TryDrop(
                actorEquipment,
                instanceId,
                targetInventory,
                "drop_stack",
                "Drop Stack",
                out string message);
            toast.Show(message, success ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
            if (!success)
                return;

            sessionController?.Selection.ClearEquipment();
            sessionController?.CloseContextMenu();
            ReconcileSelections();
            GUIUtility.ExitGUI();
        }

        private void ApplyContextTransfer(InventoryMutationResult result, bool tookToPersonal)
        {
            if (result == null || !result.Success)
                return;

            IGridStorageOwner externalOwner = GetActiveExternalOwner();
            string sourceInstanceId = result.SourceInstanceId;
            if (result.WasLimitedByWeight && result.SourceRemainingQuantity > 0 && tookToPersonal &&
                externalOwner != null && externalOwner.TryGetEntryByInstanceId(sourceInstanceId, out _, out _))
            {
                externalGridView.SelectInstance(sourceInstanceId);
                sessionController?.Selection.SelectExternalFromContext(sourceInstanceId);
                return;
            }

            string destinationInstanceId = result.DestinationInstanceId;
            if (tookToPersonal && !string.IsNullOrWhiteSpace(destinationInstanceId) &&
                GetActivePersonalOwner().TryGetEntryByInstanceId(destinationInstanceId, out _, out _))
            {
                playerGridView.SelectInstance(destinationInstanceId);
                sessionController?.Selection.SelectPersonalFromContext(destinationInstanceId);
                return;
            }

            if (!tookToPersonal && !string.IsNullOrWhiteSpace(destinationInstanceId) &&
                externalOwner != null && externalOwner.TryGetEntryByInstanceId(destinationInstanceId, out _, out _))
            {
                externalGridView.SelectInstance(destinationInstanceId);
                sessionController?.Selection.SelectExternalFromContext(destinationInstanceId);
                return;
            }

            if (tookToPersonal && externalOwner != null &&
                externalOwner.TryGetEntryByInstanceId(sourceInstanceId, out _, out _))
            {
                externalGridView.SelectInstance(sourceInstanceId);
                sessionController?.Selection.SelectExternalFromContext(sourceInstanceId);
            }
            else if (!tookToPersonal && GetActivePersonalOwner().TryGetEntryByInstanceId(sourceInstanceId, out _, out _))
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

        private void ApplyPersonalCompartmentTransfer(InventoryMutationResult result)
        {
            personalStorageNavigator?.Refresh();
            ReconcileSelections();
            if (result == null || !result.Success)
                return;

            IGridStorageOwner activeOwner = GetActivePersonalOwner();
            if (!string.IsNullOrWhiteSpace(result.DestinationInstanceId) &&
                activeOwner.TryGetEntryByInstanceId(result.DestinationInstanceId, out _, out _))
            {
                playerGridView.SelectInstance(result.DestinationInstanceId);
                sessionController?.Selection.SelectPersonalFromContext(result.DestinationInstanceId);
            }
            else
            {
                sessionController?.Selection.ClearPersonalIfMissing(result.SourceInstanceId);
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
            GridStorageTransferQuantityPolicy quantityPolicy =
                GridStorageTransferService.GetAutomaticQuantityPolicy(source, target);
            InventoryMutationResult result = GridStorageTransferService.TransferStackAuto(
                source,
                target,
                instanceId,
                quantityPolicy,
                new GridStorageTransferContext(executionContext, action));
            toast.Show(
                result.Success
                    ? result.WasLimitedByWeight
                        ? $"Tomaste {result.ActualTransferredQuantity} de {result.RequestedQuantity}. Límite de peso alcanzado."
                        : $"Transferred stack x{result.AffectedQuantity}."
                    : result.Message ?? "No se pudo transferir el stack.",
                result.Success ? InventoryToastSeverity.Success : InventoryToastSeverity.Error);
            ReconcileSelections();
            return result;
        }

        private void ReconcileSelections()
        {
            personalStorageNavigator?.Refresh();
            lootableActorSource?.RefreshLootableState();
            ReconcileLootableActorView();
            playerGridView.ReconcileSelection(GetActivePersonalOwner());
            externalGridView.ReconcileSelection(GetActiveExternalOwner());
        }

        private static string FormatUnitWeight(double unitWeightKg)
        {
            return unitWeightKg < 0.1d ? unitWeightKg.ToString("0.000") : unitWeightKg.ToString("0.00");
        }

        private static string GetOccupiedSlotLabel(
            ActorEquipmentComponent equipment,
            string instanceId)
        {
            IReadOnlyList<string> slots = equipment?.GetSlotsOccupiedBy(instanceId);
            if (slots == null || slots.Count == 0)
                return "(sin slots)";

            var labels = new string[slots.Count];
            for (int index = 0; index < slots.Count; index++)
                labels[index] = GetSlotDisplayName(equipment, slots[index]);
            return string.Join(" + ", labels);
        }

        private static string GetSlotDisplayName(
            ActorEquipmentComponent equipment,
            string slotId)
        {
            EquipmentSlotDefinition definition = equipment?.GetSlotDefinition(slotId);
            return definition != null && !string.IsNullOrWhiteSpace(definition.display_name)
                ? definition.display_name
                : SafeText(slotId);
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
            float fallbackExternalWidth = lootableActorSource != null
                ? MaximumGridColumnWidth
                : CalculateStorageColumnWidth(storageSource, externalGridView);
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
            return Mathf.Clamp(
                view.GetRequiredWidth(gridWidth) + GridColumnPadding,
                MinimumGridColumnWidth,
                MaximumGridColumnWidth);
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
