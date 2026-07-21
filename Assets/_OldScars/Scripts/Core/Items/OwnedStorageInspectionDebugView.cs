using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using UnityEngine;

namespace OldScars.Core.Items
{
    /// <summary>
    /// Single reusable floating OnGUI window for an item-owned storage binding.
    /// It owns only visual state; content and placements remain in the runtime.
    /// </summary>
    internal sealed class OwnedStorageInspectionDebugView
    {
        private const float WindowWidth = 420f;
        private const float WindowHeight = 600f;
        private const float HeaderHeight = 30f;
        private const float ScrollbarSize = 16f;
        private const float WindowMargin = 16f;

        private readonly InventoryGridDebugView gridView = new InventoryGridDebugView();
        private Vector2 scroll;
        private Rect windowRect;
        private bool hasWindowPosition;
        private bool isWindowDragging;
        private Vector2 windowDragOffset;

        internal InventoryGridDebugView GridView => gridView;

        internal void Reset(float cellSize)
        {
            gridView.SetVisualCellSize(cellSize);
            ResetContent();
            hasWindowPosition = false;
            windowRect = default;
        }

        internal void ResetContent()
        {
            scroll = Vector2.zero;
            gridView.Reset();
            isWindowDragging = false;
            windowDragOffset = Vector2.zero;
        }

        internal void Draw(
            Rect screenRect,
            FloatingStorageWindowResolution resolution,
            InventoryGridDragController dragController,
            out bool closeRequested,
            out string rightClickedInstanceId,
            out Vector2 rightClickPosition)
        {
            closeRequested = false;
            rightClickedInstanceId = null;
            rightClickPosition = Vector2.zero;
            ItemOwnedStorageRuntime storage = resolution.Storage;
            if (storage == null)
                return;

            EnsureWindowPosition(screenRect);
            HandleWindowDrag(screenRect);

            GUI.Box(windowRect, GUIContent.none);
            GUI.BeginGroup(windowRect);
            float width = windowRect.width;
            Rect headerRect = new Rect(0f, 0f, width, HeaderHeight);
            GUI.Box(headerRect, GUIContent.none);
            GUI.Label(new Rect(10f, 5f, width - 52f, 22f), GetItemDisplayName(resolution.ContainerEntry));
            if (GUI.Button(new Rect(width - 32f, 4f, 24f, 22f), "X"))
                closeRequested = true;

            int occupiedCells = GetOccupiedCellCount(storage);
            int totalCells = Mathf.Max(0, storage.GridWidth * storage.GridHeight);
            double contentWeight = storage.GetContentWeightKg(out _);
            GUI.Label(new Rect(10f, 34f, width - 20f, 20f),
                $"{SafeText(resolution.SourceLabel)} · {occupiedCells} / {totalCells} celdas");
            GUI.Label(new Rect(10f, 53f, width - 20f, 20f),
                $"Peso interno: {contentWeight:0.00} kg · Grid: {storage.GridWidth}x{storage.GridHeight}");
            DrawSelectionSummary(new Rect(10f, 74f, width - 20f, 22f), storage);

            Rect areaRect = new Rect(10f, 100f, width - 20f, Mathf.Max(80f, windowRect.height - 110f));
            Rect clipRect = new Rect(
                areaRect.x,
                areaRect.y,
                Mathf.Max(1f, areaRect.width - ScrollbarSize),
                Mathf.Max(1f, areaRect.height - ScrollbarSize));
            float contentWidth = gridView.GetRequiredWidth(storage.GridWidth);
            float contentHeight = gridView.GetRequiredHeight(storage.GridHeight);
            scroll.x = Mathf.Clamp(scroll.x, 0f, Mathf.Max(0f, contentWidth - clipRect.width));
            scroll.y = Mathf.Clamp(scroll.y, 0f, Mathf.Max(0f, contentHeight - clipRect.height));
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
            gridView.Draw(storage, localGridRect, dragController);
            if (storage.GridStorageEntries.Count == 0)
                GUI.Label(new Rect(8f, 8f, clipRect.width - 16f, 24f), "Storage vacío.");
            if (gridView.TryGetRightClick(storage, localGridRect, out rightClickedInstanceId))
            {
                rightClickPosition = Event.current != null
                    ? Event.current.mousePosition + clipRect.position + windowRect.position
                    : windowRect.position;
            }
            GUI.EndGroup();

            Rect globalGridRect = new Rect(
                windowRect.x + clipRect.x - scroll.x,
                windowRect.y + clipRect.y - scroll.y,
                contentWidth,
                contentHeight);
            Rect globalClipRect = new Rect(
                windowRect.x + clipRect.x,
                windowRect.y + clipRect.y,
                clipRect.width,
                clipRect.height);
            dragController.RegisterEndpoint(storage, gridView, globalGridRect, globalClipRect);
            GUI.EndGroup();
        }

        private void EnsureWindowPosition(Rect screenRect)
        {
            float width = Mathf.Min(WindowWidth, Mathf.Max(1f, screenRect.width - WindowMargin * 2f));
            float height = Mathf.Min(WindowHeight, Mathf.Max(HeaderHeight, screenRect.height - 58f));
            if (!hasWindowPosition)
            {
                windowRect = new Rect(
                    screenRect.xMax - width - WindowMargin,
                    screenRect.y + 42f,
                    width,
                    height);
                hasWindowPosition = true;
            }
            else
            {
                windowRect.width = width;
                windowRect.height = height;
            }
            ClampWindow(screenRect);
        }

        private void HandleWindowDrag(Rect screenRect)
        {
            Event guiEvent = Event.current;
            if (guiEvent == null)
                return;

            Rect globalHeader = new Rect(windowRect.x, windowRect.y, windowRect.width, HeaderHeight);
            Rect globalClose = new Rect(windowRect.xMax - 32f, windowRect.y + 4f, 24f, 22f);
            if (guiEvent.type == EventType.MouseDown && guiEvent.button == 0 && GUI.enabled &&
                globalHeader.Contains(guiEvent.mousePosition) && !globalClose.Contains(guiEvent.mousePosition))
            {
                isWindowDragging = true;
                windowDragOffset = guiEvent.mousePosition - windowRect.position;
                guiEvent.Use();
                return;
            }

            if (!isWindowDragging)
                return;

            if (guiEvent.type == EventType.MouseDrag && guiEvent.button == 0)
            {
                windowRect.position = guiEvent.mousePosition - windowDragOffset;
                ClampWindow(screenRect);
                guiEvent.Use();
                return;
            }

            if (guiEvent.type == EventType.MouseUp && guiEvent.button == 0)
            {
                isWindowDragging = false;
                guiEvent.Use();
            }
        }

        private void ClampWindow(Rect screenRect)
        {
            windowRect.x = Mathf.Clamp(
                windowRect.x,
                screenRect.xMin,
                Mathf.Max(screenRect.xMin, screenRect.xMax - windowRect.width));
            windowRect.y = Mathf.Clamp(
                windowRect.y,
                screenRect.yMin,
                Mathf.Max(screenRect.yMin, screenRect.yMax - HeaderHeight));
        }

        private void DrawSelectionSummary(Rect rect, ItemOwnedStorageRuntime storage)
        {
            string instanceId = gridView.SelectedInstanceId;
            if (string.IsNullOrWhiteSpace(instanceId) ||
                !storage.TryGetEntryByInstanceId(instanceId, out _, out ItemStorageEntry entry) ||
                entry?.Item == null)
            {
                GUI.Label(rect, "Seleccioná un item; clic derecho para acciones.");
                return;
            }

            string placementText = string.Empty;
            if (storage.TryGetGridPlacement(instanceId, out GridPlacement placement))
                placementText = $" · ({placement.X},{placement.Y}) {placement.EffectiveWidth}x{placement.EffectiveHeight}";
            string weightText = ItemWeightResolver.TryGetEntryWeight(
                entry,
                entry.Quantity,
                out double totalWeight,
                out _)
                ? $" · {totalWeight:0.00} kg"
                : string.Empty;
            GUI.Label(rect, $"{GetItemDisplayName(entry)} x{entry.Quantity}{weightText}{placementText}");
        }

        private static int GetOccupiedCellCount(ItemOwnedStorageRuntime storage)
        {
            int occupied = 0;
            for (int index = 0; index < storage.GridStorageEntries.Count; index++)
            {
                ItemStorageEntry entry = storage.GridStorageEntries[index];
                if (entry?.Item != null &&
                    storage.TryGetGridPlacement(entry.Item.InstanceId, out GridPlacement placement))
                {
                    occupied += placement.EffectiveWidth * placement.EffectiveHeight;
                }
            }
            return occupied;
        }

        private static string GetItemDisplayName(ItemStorageEntry entry)
        {
            string definitionId = entry?.DefinitionId;
            if (GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
                return SafeText(definitionId);

            GameDatabase database = GameDataManager.Instance.Database;
            ItemDefinition definition = database != null ? database.GetItem(definitionId) : null;
            return definition != null && definition.display != null && !string.IsNullOrWhiteSpace(definition.display.name)
                ? definition.display.name
                : SafeText(definitionId);
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(unknown)" : value;
        }
    }
}
