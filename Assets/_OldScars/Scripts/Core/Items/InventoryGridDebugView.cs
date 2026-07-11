using System;
using System.Collections.Generic;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OldScars.Core.Items
{
    public sealed class InventoryGridDebugView
    {
        public const float CellSize = 42f;
        public const float CellGap = 2f;
        private const float DragThreshold = 4f;
        private const float ItemInset = 2f;

        private static readonly Color CellColor = new Color(0.11f, 0.12f, 0.14f, 1f);
        private static readonly Color CellBorderColor = new Color(0.33f, 0.36f, 0.4f, 1f);
        private static readonly Color SelectedColor = new Color(0.25f, 0.72f, 1f, 1f);
        private static readonly Color EquippedColor = new Color(1f, 0.78f, 0.18f, 1f);
        private static readonly Color ValidColor = new Color(0.18f, 0.85f, 0.3f, 0.42f);
        private static readonly Color InvalidColor = new Color(0.95f, 0.18f, 0.18f, 0.48f);
        private static readonly Color MissingPlacementColor = new Color(0.65f, 0.05f, 0.05f, 0.94f);

        private readonly HashSet<string> loggedMissingPlacementIds = new HashSet<string>();

        private string selectedInstanceId;
        private string pressedInstanceId;
        private Vector2 pressMousePosition;
        private Vector2 lastDragMousePosition;
        private int grabbedCellX;
        private int grabbedCellY;
        private bool requestedRotated;
        private bool isDragging;
        private bool hasCandidateCoordinates;
        private int candidateX;
        private int candidateY;
        private GridPlacementValidationResult candidatePreview;
        private string statusMessage;
        private Rect lastGridRect;
        private bool hasLastGridRect;

        public string SelectedInstanceId => selectedInstanceId;
        public bool IsDragging => isDragging;
        public string StatusMessage => statusMessage;

        public float GetRequiredWidth(int gridWidth)
        {
            return GetGridPixels(gridWidth);
        }

        public float GetRequiredHeight(int gridHeight)
        {
            return GetGridPixels(gridHeight);
        }

        public void Draw(InventoryComponent inventory, Rect gridRect)
        {
            lastGridRect = gridRect;
            hasLastGridRect = true;
            DrawGridCells(inventory, gridRect);

            if (inventory == null || !inventory.UsesGridLayout)
            {
                DrawCenteredMessage(gridRect, "GRID LAYOUT INACTIVE", MissingPlacementColor);
                ResetPointerState();
                return;
            }

            ReconcileSelection(inventory);
            HandlePointerEvent(inventory, gridRect);
            DrawPlacedItems(inventory, gridRect);

            if (isDragging)
                DrawDragPreview(inventory, gridRect);
        }

        public void HandleKeyboardInput(InventoryComponent inventory)
        {
            if (!isDragging || inventory == null || Keyboard.current == null ||
                !Keyboard.current.rKey.wasPressedThisFrame)
            {
                return;
            }

            if (!inventory.TryGetEntryByInstanceId(pressedInstanceId, out _, out ItemStorageEntry entry) ||
                entry == null || entry.Item == null ||
                !inventory.TryGetGridFootprint(entry.DefinitionId, out GridFootprint footprint, out _))
            {
                statusMessage = "Cannot rotate: item or footprint is unavailable.";
                return;
            }

            if (footprint.Width == footprint.Height)
            {
                if (hasLastGridRect)
                    UpdateCandidate(inventory, lastGridRect, lastDragMousePosition);
                else
                    hasCandidateCoordinates = false;
                statusMessage = "Rotation preview: square footprint unchanged.";
                return;
            }

            requestedRotated = !requestedRotated;
            grabbedCellX = Mathf.Clamp(grabbedCellX, 0, footprint.GetWidth(requestedRotated) - 1);
            grabbedCellY = Mathf.Clamp(grabbedCellY, 0, footprint.GetHeight(requestedRotated) - 1);
            if (hasLastGridRect)
                UpdateCandidate(inventory, lastGridRect, lastDragMousePosition);
            else
                hasCandidateCoordinates = false;
            statusMessage = requestedRotated ? "Rotation preview: rotated." : "Rotation preview: original.";
        }

        public bool CancelDrag()
        {
            if (!isDragging && string.IsNullOrWhiteSpace(pressedInstanceId))
                return false;

            ResetPointerState();
            statusMessage = "Grid move cancelled.";
            return true;
        }

        public bool TryGetSelectedEntry(
            InventoryComponent inventory,
            out int index,
            out ItemStorageEntry entry)
        {
            index = -1;
            entry = null;
            if (inventory == null || string.IsNullOrWhiteSpace(selectedInstanceId))
                return false;

            return inventory.TryGetEntryByInstanceId(selectedInstanceId, out index, out entry);
        }

        public void ReconcileSelection(InventoryComponent inventory)
        {
            if (string.IsNullOrWhiteSpace(selectedInstanceId))
                return;

            if (inventory != null && inventory.TryGetEntryByInstanceId(selectedInstanceId, out _, out _))
                return;

            selectedInstanceId = null;
            ResetPointerState();
        }

        public void Reset()
        {
            selectedInstanceId = null;
            statusMessage = null;
            hasLastGridRect = false;
            ResetPointerState();
        }

        private void HandlePointerEvent(InventoryComponent inventory, Rect gridRect)
        {
            Event guiEvent = Event.current;
            if (guiEvent == null)
                return;

            if (guiEvent.type == EventType.MouseDown && guiEvent.button == 0)
            {
                if (!gridRect.Contains(guiEvent.mousePosition))
                    return;

                string hitInstanceId = FindInstanceAtPosition(inventory, gridRect, guiEvent.mousePosition, out GridPlacement hitPlacement);
                if (string.IsNullOrWhiteSpace(hitInstanceId) || hitPlacement == null)
                    return;

                selectedInstanceId = hitInstanceId;
                pressedInstanceId = hitInstanceId;
                pressMousePosition = guiEvent.mousePosition;
                lastDragMousePosition = guiEvent.mousePosition;
                requestedRotated = hitPlacement.IsRotated;
                grabbedCellX = Mathf.Clamp(
                    Mathf.FloorToInt((guiEvent.mousePosition.x - GetPlacementRect(gridRect, hitPlacement).x) / GetCellPitch()),
                    0,
                    hitPlacement.EffectiveWidth - 1);
                grabbedCellY = Mathf.Clamp(
                    Mathf.FloorToInt((guiEvent.mousePosition.y - GetPlacementRect(gridRect, hitPlacement).y) / GetCellPitch()),
                    0,
                    hitPlacement.EffectiveHeight - 1);
                statusMessage = null;
                guiEvent.Use();
                return;
            }

            if (guiEvent.type == EventType.MouseDrag && guiEvent.button == 0 && !string.IsNullOrWhiteSpace(pressedInstanceId))
            {
                lastDragMousePosition = guiEvent.mousePosition;
                if (!isDragging && (guiEvent.mousePosition - pressMousePosition).sqrMagnitude >= DragThreshold * DragThreshold)
                    isDragging = true;

                if (isDragging)
                    UpdateCandidate(inventory, gridRect, guiEvent.mousePosition);

                guiEvent.Use();
                return;
            }

            if (guiEvent.type != EventType.MouseUp || guiEvent.button != 0 || string.IsNullOrWhiteSpace(pressedInstanceId))
                return;

            lastDragMousePosition = guiEvent.mousePosition;
            if (isDragging)
            {
                UpdateCandidate(inventory, gridRect, guiEvent.mousePosition);
                CommitCandidate(inventory);
            }

            ResetPointerState();
            guiEvent.Use();
        }

        private void CommitCandidate(InventoryComponent inventory)
        {
            if (!hasCandidateCoordinates || !candidatePreview.IsValid)
            {
                statusMessage = candidatePreview.Message ?? "Grid move cancelled: invalid destination.";
                return;
            }

            InventoryMutationResult result = inventory.MoveGridPlacement(
                pressedInstanceId,
                candidateX,
                candidateY,
                requestedRotated);
            statusMessage = result.Success
                ? $"Moved item to ({candidateX},{candidateY}){(requestedRotated ? " rotated" : string.Empty)}."
                : result.Message ?? "Grid move failed.";
        }

        private void UpdateCandidate(InventoryComponent inventory, Rect gridRect, Vector2 mousePosition)
        {
            hasCandidateCoordinates = false;
            candidatePreview = default;
            if (!gridRect.Contains(mousePosition))
                return;

            int hoveredX = Mathf.FloorToInt((mousePosition.x - gridRect.x) / GetCellPitch());
            int hoveredY = Mathf.FloorToInt((mousePosition.y - gridRect.y) / GetCellPitch());
            candidateX = hoveredX - grabbedCellX;
            candidateY = hoveredY - grabbedCellY;
            hasCandidateCoordinates = true;
            candidatePreview = inventory.PreviewGridPlacementMove(
                pressedInstanceId,
                candidateX,
                candidateY,
                requestedRotated);
        }

        private void DrawGridCells(InventoryComponent inventory, Rect gridRect)
        {
            int width = inventory != null && inventory.GridWidth > 0 ? inventory.GridWidth : 6;
            int height = inventory != null && inventory.GridHeight > 0 ? inventory.GridHeight : 8;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Rect cellRect = GetCellRect(gridRect, x, y);
                    DrawSolidRect(cellRect, CellBorderColor);
                    DrawSolidRect(Inset(cellRect, 1f), CellColor);
                }
            }
        }

        private void DrawPlacedItems(InventoryComponent inventory, Rect gridRect)
        {
            IReadOnlyList<ItemStorageEntry> entries = inventory.GetStorageEntries();
            int missingMessageIndex = 0;
            for (int index = 0; index < entries.Count; index++)
            {
                ItemStorageEntry entry = entries[index];
                ItemInstance item = entry != null ? entry.Item : null;
                if (item == null)
                    continue;

                if (!inventory.TryGetGridPlacement(item.InstanceId, out GridPlacement placement))
                {
                    DrawMissingPlacement(gridRect, item.InstanceId, missingMessageIndex++);
                    LogMissingPlacementOnce(item);
                    continue;
                }

                Rect itemRect = GetPlacementRect(gridRect, placement);
                bool isSelected = item.InstanceId == selectedInstanceId;
                bool isEquipped = inventory.IsRightHandStorageEntry(entry);
                bool isDraggedOrigin = isDragging && item.InstanceId == pressedInstanceId;
                DrawItem(entry, itemRect, isSelected, isEquipped, placement.IsRotated, isDraggedOrigin ? 0.38f : 1f);
            }
        }

        private void DrawDragPreview(InventoryComponent inventory, Rect gridRect)
        {
            if (!inventory.TryGetEntryByInstanceId(pressedInstanceId, out _, out ItemStorageEntry entry) ||
                entry == null || entry.Item == null ||
                !inventory.TryGetGridFootprint(entry.DefinitionId, out GridFootprint footprint, out _))
            {
                return;
            }

            int width = footprint.GetWidth(requestedRotated);
            int height = footprint.GetHeight(requestedRotated);
            bool isValid = hasCandidateCoordinates && candidatePreview.IsValid;
            Color previewColor = isValid ? ValidColor : InvalidColor;

            Rect ghostRect;
            if (hasCandidateCoordinates)
            {
                ghostRect = GetPlacementRect(gridRect, candidateX, candidateY, width, height);
                DrawCandidateCells(gridRect, candidateX, candidateY, width, height, previewColor);
            }
            else
            {
                ghostRect = new Rect(
                    lastDragMousePosition.x - grabbedCellX * GetCellPitch() - CellSize * 0.5f,
                    lastDragMousePosition.y - grabbedCellY * GetCellPitch() - CellSize * 0.5f,
                    GetGridPixels(width),
                    GetGridPixels(height));
            }

            DrawSolidRect(ghostRect, previewColor);
            DrawBorder(ghostRect, isValid ? new Color(0.25f, 1f, 0.4f, 1f) : new Color(1f, 0.2f, 0.2f, 1f), 2f);
            GUI.Label(ghostRect, GetShortLabel(GetDisplayName(entry.DefinitionId), ghostRect.width), GetCenteredLabelStyle());
        }

        private void DrawCandidateCells(Rect gridRect, int x, int y, int width, int height, Color color)
        {
            for (int cellY = 0; cellY < height; cellY++)
            {
                for (int cellX = 0; cellX < width; cellX++)
                    DrawSolidRect(GetCellRect(gridRect, x + cellX, y + cellY), color);
            }
        }

        private void DrawItem(
            ItemStorageEntry entry,
            Rect rect,
            bool isSelected,
            bool isEquipped,
            bool isRotated,
            float alpha)
        {
            ItemDefinition definition = GetItemDefinition(entry.DefinitionId);
            string displayName = definition != null && definition.display != null
                ? definition.display.name
                : entry.DefinitionId;
            string iconId = definition != null && definition.inventory.HasValue
                ? definition.inventory.Value.icon_id
                : null;

            Color fallbackColor = GetStableFallbackColor(iconId, displayName);
            fallbackColor.a *= alpha;
            DrawSolidRect(rect, fallbackColor);

            if (InventoryIconResolver.TryResolve(iconId, out Sprite sprite))
                DrawSprite(sprite, Inset(rect, ItemInset + 1f), alpha);

            DrawBorder(rect, isSelected ? SelectedColor : new Color(0f, 0f, 0f, 0.75f * alpha), isSelected ? 3f : 1f);
            if (isEquipped)
                DrawBorder(Inset(rect, isSelected ? 3f : 1f), EquippedColor, 2f);

            string shortLabel = GetShortLabel(displayName, rect.width);
            GUI.Label(new Rect(rect.x + 3f, rect.y + 2f, Mathf.Max(0f, rect.width - 6f), 20f), shortLabel, GetItemLabelStyle());
            GUI.Label(new Rect(rect.x + 3f, rect.yMax - 20f, Mathf.Max(0f, rect.width - 6f), 18f), $"x{entry.Quantity}", GetQuantityLabelStyle());

            if (isEquipped)
                GUI.Label(new Rect(rect.xMax - 20f, rect.y + 2f, 18f, 18f), "E", GetBadgeLabelStyle());
            if (isRotated)
                GUI.Label(new Rect(rect.x + 2f, rect.yMax - 20f, 18f, 18f), "R", GetBadgeLabelStyle());

            if (alpha < 0.99f)
                DrawSolidRect(rect, new Color(0f, 0f, 0f, 0.45f));
        }

        private string FindInstanceAtPosition(
            InventoryComponent inventory,
            Rect gridRect,
            Vector2 mousePosition,
            out GridPlacement placement)
        {
            placement = null;
            IReadOnlyList<ItemStorageEntry> entries = inventory.GetStorageEntries();
            for (int index = entries.Count - 1; index >= 0; index--)
            {
                ItemInstance item = entries[index] != null ? entries[index].Item : null;
                if (item == null || !inventory.TryGetGridPlacement(item.InstanceId, out GridPlacement candidate))
                    continue;

                if (!GetPlacementRect(gridRect, candidate).Contains(mousePosition))
                    continue;

                placement = candidate;
                return item.InstanceId;
            }

            return null;
        }

        private void DrawMissingPlacement(Rect gridRect, string instanceId, int messageIndex)
        {
            float y = gridRect.y + 4f + messageIndex * 24f;
            Rect messageRect = new Rect(gridRect.x + 4f, y, gridRect.width - 8f, 21f);
            DrawSolidRect(messageRect, MissingPlacementColor);
            GUI.Label(messageRect, $"MISSING PLACEMENT: {instanceId}", GetMissingPlacementStyle());
        }

        private void LogMissingPlacementOnce(ItemInstance item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.InstanceId) || !loggedMissingPlacementIds.Add(item.InstanceId))
                return;

            Debug.LogError(
                $"[InventoryGridDebugView] MISSING PLACEMENT for item '{item.DefinitionId}' [{item.InstanceId}]. " +
                "The UI will not invent a grid position; use Legacy List for debug access.");
        }

        private void ResetPointerState()
        {
            pressedInstanceId = null;
            isDragging = false;
            hasCandidateCoordinates = false;
            candidatePreview = default;
        }

        private static Rect GetCellRect(Rect gridRect, int x, int y)
        {
            float pitch = GetCellPitch();
            return new Rect(gridRect.x + x * pitch, gridRect.y + y * pitch, CellSize, CellSize);
        }

        private static Rect GetPlacementRect(Rect gridRect, GridPlacement placement)
        {
            return GetPlacementRect(
                gridRect,
                placement.X,
                placement.Y,
                placement.EffectiveWidth,
                placement.EffectiveHeight);
        }

        private static Rect GetPlacementRect(Rect gridRect, int x, int y, int width, int height)
        {
            float pitch = GetCellPitch();
            return new Rect(
                gridRect.x + x * pitch,
                gridRect.y + y * pitch,
                GetGridPixels(width),
                GetGridPixels(height));
        }

        private static float GetGridPixels(int cellCount)
        {
            return cellCount > 0 ? cellCount * CellSize + (cellCount - 1) * CellGap : 0f;
        }

        private static float GetCellPitch()
        {
            return CellSize + CellGap;
        }

        private static void DrawSprite(Sprite sprite, Rect targetRect, float alpha)
        {
            if (sprite == null || sprite.texture == null || targetRect.width <= 0f || targetRect.height <= 0f)
                return;

            Rect textureRect = sprite.textureRect;
            float spriteAspect = textureRect.height > 0f ? textureRect.width / textureRect.height : 1f;
            float targetAspect = targetRect.width / targetRect.height;
            Rect drawRect = targetRect;
            if (targetAspect > spriteAspect)
            {
                drawRect.width = targetRect.height * spriteAspect;
                drawRect.x = targetRect.x + (targetRect.width - drawRect.width) * 0.5f;
            }
            else
            {
                drawRect.height = targetRect.width / Mathf.Max(0.0001f, spriteAspect);
                drawRect.y = targetRect.y + (targetRect.height - drawRect.height) * 0.5f;
            }

            Rect uv = new Rect(
                textureRect.x / sprite.texture.width,
                textureRect.y / sprite.texture.height,
                textureRect.width / sprite.texture.width,
                textureRect.height / sprite.texture.height);
            Color previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTextureWithTexCoords(drawRect, sprite.texture, uv, true);
            GUI.color = previousColor;
        }

        private static Color GetStableFallbackColor(string iconId, string displayName)
        {
            string source = !string.IsNullOrWhiteSpace(iconId) ? iconId.Trim() : displayName ?? "item";
            uint hash = 2166136261u;
            for (int index = 0; index < source.Length; index++)
            {
                hash ^= source[index];
                hash *= 16777619u;
            }

            float hue = (hash % 360u) / 360f;
            return Color.HSVToRGB(hue, 0.5f, 0.65f);
        }

        private static string GetShortLabel(string displayName, float availableWidth)
        {
            string safeName = string.IsNullOrWhiteSpace(displayName) ? "ITEM" : displayName.Trim();
            if (availableWidth >= 120f)
                return safeName;

            string[] words = safeName.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 1)
            {
                string initials = string.Empty;
                for (int index = 0; index < words.Length && initials.Length < 3; index++)
                    initials += char.ToUpperInvariant(words[index][0]);
                return initials;
            }

            return safeName.Substring(0, Mathf.Min(3, safeName.Length)).ToUpperInvariant();
        }

        private static ItemDefinition GetItemDefinition(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId) || GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
                return null;

            GameDatabase database = GameDataManager.Instance.Database;
            return database != null ? database.GetItem(definitionId) : null;
        }

        private static string GetDisplayName(string definitionId)
        {
            ItemDefinition definition = GetItemDefinition(definitionId);
            return definition != null && definition.display != null && !string.IsNullOrWhiteSpace(definition.display.name)
                ? definition.display.name
                : definitionId ?? "ITEM";
        }

        private static void DrawCenteredMessage(Rect rect, string message, Color color)
        {
            Rect messageRect = new Rect(rect.x + 8f, rect.center.y - 14f, rect.width - 16f, 28f);
            DrawSolidRect(messageRect, color);
            GUI.Label(messageRect, message, GetMissingPlacementStyle());
        }

        private static void DrawSolidRect(Rect rect, Color color)
        {
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            Color previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private static void DrawBorder(Rect rect, Color color, float thickness)
        {
            DrawSolidRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawSolidRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawSolidRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawSolidRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private static Rect Inset(Rect rect, float amount)
        {
            return new Rect(
                rect.x + amount,
                rect.y + amount,
                Mathf.Max(0f, rect.width - amount * 2f),
                Mathf.Max(0f, rect.height - amount * 2f));
        }

        private static GUIStyle GetItemLabelStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontStyle = FontStyle.Bold,
                fontSize = 11,
                clipping = TextClipping.Clip
            };
            style.normal.textColor = Color.white;
            return style;
        }

        private static GUIStyle GetQuantityLabelStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.LowerRight,
                fontStyle = FontStyle.Bold,
                fontSize = 11
            };
            style.normal.textColor = Color.white;
            return style;
        }

        private static GUIStyle GetBadgeLabelStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 11
            };
            style.normal.textColor = EquippedColor;
            return style;
        }

        private static GUIStyle GetCenteredLabelStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                clipping = TextClipping.Clip
            };
            style.normal.textColor = Color.white;
            return style;
        }

        private static GUIStyle GetMissingPlacementStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 11,
                clipping = TextClipping.Clip
            };
            style.normal.textColor = Color.white;
            return style;
        }
    }
}
