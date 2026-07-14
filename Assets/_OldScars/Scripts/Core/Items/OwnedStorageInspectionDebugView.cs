using UnityEngine;

namespace OldScars.Core.Items
{
    /// <summary>
    /// Small OnGUI overlay for an unequipped actor-owned container instance.
    /// It owns visual state only; content and placements remain in the runtime.
    /// </summary>
    internal sealed class OwnedStorageInspectionDebugView
    {
        private const float ScrollbarSize = 16f;
        private readonly InventoryGridDebugView gridView = new InventoryGridDebugView();
        private Vector2 scroll;

        internal InventoryGridDebugView GridView => gridView;

        internal void Reset(float cellSize)
        {
            scroll = Vector2.zero;
            gridView.SetVisualCellSize(cellSize);
            gridView.Reset();
        }

        internal void Draw(
            Rect rect,
            ItemOwnedStorageRuntime storage,
            InventoryGridDragController dragController,
            out bool closeRequested,
            out string rightClickedInstanceId,
            out Vector2 rightClickPosition)
        {
            closeRequested = false;
            rightClickedInstanceId = null;
            rightClickPosition = Vector2.zero;
            if (storage == null)
                return;

            GUI.Box(rect, GUIContent.none);
            GUI.BeginGroup(rect);
            float width = rect.width;
            GUI.Label(new Rect(10f, 6f, width - 100f, 22f), storage.GridStorageDisplayName);
            if (GUI.Button(new Rect(width - 82f, 5f, 72f, 24f), "Cerrar"))
                closeRequested = true;

            string suffix = storage.ContainerInstanceId.Length > 8
                ? storage.ContainerInstanceId.Substring(storage.ContainerInstanceId.Length - 8)
                : storage.ContainerInstanceId;
            GUI.Label(new Rect(10f, 30f, width - 20f, 20f), $"Instance: ...{suffix}");
            GUI.Label(new Rect(10f, 49f, width - 20f, 20f), $"Grid: {storage.GridWidth}x{storage.GridHeight}");
            double weight = storage.GetContentWeightKg(out _);
            GUI.Label(new Rect(10f, 68f, width - 20f, 20f), $"Contenido: {weight:0.00} kg");

            Rect areaRect = new Rect(10f, 92f, width - 20f, Mathf.Max(80f, rect.height - 102f));
            Rect clipRect = new Rect(areaRect.x, areaRect.y, areaRect.width - ScrollbarSize, areaRect.height - ScrollbarSize);
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
            if (gridView.TryGetRightClick(storage, localGridRect, out rightClickedInstanceId))
                rightClickPosition = Event.current != null ? Event.current.mousePosition + clipRect.position + rect.position : rect.position;
            GUI.EndGroup();

            Rect globalGridRect = new Rect(
                rect.x + clipRect.x - scroll.x,
                rect.y + clipRect.y - scroll.y,
                contentWidth,
                contentHeight);
            Rect globalClipRect = new Rect(
                rect.x + clipRect.x,
                rect.y + clipRect.y,
                clipRect.width,
                clipRect.height);
            dragController.RegisterEndpoint(storage, gridView, globalGridRect, globalClipRect);
            GUI.EndGroup();
        }
    }
}
