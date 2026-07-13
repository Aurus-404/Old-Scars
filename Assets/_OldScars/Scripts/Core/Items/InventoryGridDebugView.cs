using System;
using System.Collections.Generic;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using UnityEngine;

namespace OldScars.Core.Items
{
    public sealed class InventoryGridDebugView
    {
        public const float CellSize = 42f;
        public const float CellGap = 2f;
        public const float CellPitch = CellSize + CellGap;
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

        public string SelectedInstanceId => selectedInstanceId;

        public float GetRequiredWidth(int gridWidth)
        {
            return GetGridPixels(gridWidth);
        }

        public float GetRequiredHeight(int gridHeight)
        {
            return GetGridPixels(gridHeight);
        }

        public void Draw(
            IGridStorageOwner owner,
            Rect gridRect,
            InventoryGridDragController dragController = null)
        {
            DrawGridCells(owner, gridRect);
            if (owner == null || !owner.UsesGridLayout)
            {
                string message = owner != null && owner.GridInitializationState == GridStorageInitializationState.LinearFallback
                    ? "GRID FALLBACK: LEGACY LIST"
                    : "GRID LAYOUT INACTIVE";
                DrawCenteredMessage(gridRect, message, MissingPlacementColor);
                return;
            }

            ReconcileSelection(owner);
            DrawPlacedItems(owner, gridRect, dragController);
        }

        public bool TryGetSelectedEntry(
            IGridStorageOwner owner,
            out int index,
            out ItemStorageEntry entry)
        {
            index = -1;
            entry = null;
            return owner != null && !string.IsNullOrWhiteSpace(selectedInstanceId) &&
                   owner.TryGetEntryByInstanceId(selectedInstanceId, out index, out entry);
        }

        public void ReconcileSelection(IGridStorageOwner owner)
        {
            if (string.IsNullOrWhiteSpace(selectedInstanceId))
                return;

            if (owner != null && owner.TryGetEntryByInstanceId(selectedInstanceId, out _, out _))
                return;

            selectedInstanceId = null;
        }

        public void SelectInstance(string instanceId)
        {
            selectedInstanceId = string.IsNullOrWhiteSpace(instanceId) ? null : instanceId;
        }

        public void Reset()
        {
            selectedInstanceId = null;
        }

        internal string FindInstanceAtPosition(
            IGridStorageOwner owner,
            Rect gridRect,
            Vector2 mousePosition,
            out GridPlacement placement)
        {
            if (!TryGetCellAtPosition(owner, gridRect, mousePosition, out int cellX, out int cellY))
            {
                placement = null;
                return null;
            }

            return FindInstanceAtCell(owner, cellX, cellY, out placement);
        }

        internal string FindInstanceAtCell(
            IGridStorageOwner owner,
            int cellX,
            int cellY,
            out GridPlacement placement)
        {
            placement = null;
            if (owner == null || cellX < 0 || cellY < 0 || cellX >= owner.GridWidth || cellY >= owner.GridHeight)
                return null;

            IReadOnlyList<ItemStorageEntry> entries = owner.GridStorageEntries;
            for (int index = entries.Count - 1; index >= 0; index--)
            {
                ItemInstance item = entries[index] != null ? entries[index].Item : null;
                if (item == null || !owner.TryGetGridPlacement(item.InstanceId, out GridPlacement candidate))
                    continue;

                bool occupiesCell = cellX >= candidate.X && cellX < candidate.X + candidate.EffectiveWidth &&
                                     cellY >= candidate.Y && cellY < candidate.Y + candidate.EffectiveHeight;
                if (!occupiesCell)
                    continue;

                placement = candidate;
                return item.InstanceId;
            }

            return null;
        }

        internal bool TryGetCellAtPosition(
            IGridStorageOwner owner,
            Rect gridRect,
            Vector2 mousePosition,
            out int cellX,
            out int cellY)
        {
            cellX = -1;
            cellY = -1;
            if (owner == null || !gridRect.Contains(mousePosition))
                return false;

            cellX = Mathf.FloorToInt((mousePosition.x - gridRect.x) / CellPitch);
            cellY = Mathf.FloorToInt((mousePosition.y - gridRect.y) / CellPitch);
            return cellX >= 0 && cellY >= 0 && cellX < owner.GridWidth && cellY < owner.GridHeight;
        }

        internal void DrawDragPreview(
            IGridStorageOwner sourceOwner,
            Rect destinationGridRect,
            string sourceInstanceId,
            Vector2 mousePosition,
            int grabbedCellX,
            int grabbedCellY,
            bool requestedRotated,
            bool hasCandidateCoordinates,
            int candidateX,
            int candidateY,
            GridPlacementValidationResult candidatePreview)
        {
            if (sourceOwner == null ||
                !sourceOwner.TryGetEntryByInstanceId(sourceInstanceId, out _, out ItemStorageEntry entry) ||
                entry == null || entry.Item == null ||
                !sourceOwner.TryGetGridFootprint(entry.DefinitionId, out GridFootprint footprint, out _))
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
                ghostRect = GetPlacementRect(destinationGridRect, candidateX, candidateY, width, height);
                DrawCandidateCells(destinationGridRect, candidateX, candidateY, width, height, previewColor);
            }
            else
            {
                ghostRect = new Rect(
                    mousePosition.x - grabbedCellX * CellPitch - CellSize * 0.5f,
                    mousePosition.y - grabbedCellY * CellPitch - CellSize * 0.5f,
                    GetGridPixels(width),
                    GetGridPixels(height));
            }

            DrawSolidRect(ghostRect, previewColor);
            DrawBorder(
                ghostRect,
                isValid ? new Color(0.25f, 1f, 0.4f, 1f) : new Color(1f, 0.2f, 0.2f, 1f),
                2f);
            GUI.Label(
                ghostRect,
                GetShortLabel(GetDisplayName(entry.DefinitionId), ghostRect.width),
                GetCenteredLabelStyle());
        }

        internal void DrawMergePreview(
            IGridStorageOwner destinationOwner,
            Rect destinationGridRect,
            string destinationInstanceId,
            GridStorageMergePreview preview)
        {
            if (destinationOwner == null || string.IsNullOrWhiteSpace(destinationInstanceId) ||
                !destinationOwner.TryGetGridPlacement(destinationInstanceId, out GridPlacement placement))
            {
                return;
            }

            Rect targetRect = GetPlacementRect(destinationGridRect, placement);
            Color previewColor = preview.IsValid ? ValidColor : InvalidColor;
            DrawSolidRect(targetRect, previewColor);
            DrawBorder(
                targetRect,
                preview.IsValid ? new Color(0.25f, 1f, 0.4f, 1f) : new Color(1f, 0.2f, 0.2f, 1f),
                3f);

            string label;
            if (preview.IsValid)
            {
                label = preview.TransferQuantity < preview.SourceQuantity
                    ? $"Merge +{preview.TransferQuantity} / {preview.SourceQuantity}"
                    : $"Merge +{preview.TransferQuantity}";
            }
            else
            {
                label = !string.IsNullOrWhiteSpace(preview.Message)
                    ? preview.Message
                    : "Destination occupied";
            }

            GUI.Label(targetRect, label, GetCenteredLabelStyle());
        }

        internal static Rect GetPlacementRect(Rect gridRect, GridPlacement placement)
        {
            return GetPlacementRect(
                gridRect,
                placement.X,
                placement.Y,
                placement.EffectiveWidth,
                placement.EffectiveHeight);
        }

        private void DrawGridCells(IGridStorageOwner owner, Rect gridRect)
        {
            int width = owner != null && owner.GridWidth > 0 ? owner.GridWidth : Mathf.Max(1, owner?.ConfiguredGridWidth ?? 6);
            int height = owner != null && owner.GridHeight > 0 ? owner.GridHeight : Mathf.Max(1, owner?.ConfiguredGridHeight ?? 8);
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

        private void DrawPlacedItems(
            IGridStorageOwner owner,
            Rect gridRect,
            InventoryGridDragController dragController)
        {
            IReadOnlyList<ItemStorageEntry> entries = owner.GridStorageEntries;
            int missingMessageIndex = 0;
            for (int index = 0; index < entries.Count; index++)
            {
                ItemStorageEntry entry = entries[index];
                ItemInstance item = entry != null ? entry.Item : null;
                if (item == null)
                    continue;

                if (!owner.TryGetGridPlacement(item.InstanceId, out GridPlacement placement))
                {
                    DrawMissingPlacement(gridRect, item.InstanceId, missingMessageIndex++);
                    LogMissingPlacementOnce(owner, item);
                    continue;
                }

                bool draggedOrigin = dragController != null && dragController.IsDraggedSource(owner, item.InstanceId);
                DrawItem(
                    entry,
                    GetPlacementRect(gridRect, placement),
                    item.InstanceId == selectedInstanceId,
                    owner.IsInstanceEquipped(item.InstanceId),
                    placement.IsRotated,
                    draggedOrigin ? 0.38f : 1f);
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

            GUI.Label(new Rect(rect.x + 3f, rect.y + 2f, Mathf.Max(0f, rect.width - 6f), 20f),
                GetShortLabel(displayName, rect.width), GetItemLabelStyle());
            GUI.Label(new Rect(rect.x + 3f, rect.yMax - 20f, Mathf.Max(0f, rect.width - 6f), 18f),
                $"x{entry.Quantity}", GetQuantityLabelStyle());

            if (isEquipped)
                GUI.Label(new Rect(rect.xMax - 20f, rect.y + 2f, 18f, 18f), "E", GetBadgeLabelStyle());
            if (isRotated)
                GUI.Label(new Rect(rect.x + 2f, rect.yMax - 20f, 18f, 18f), "R", GetBadgeLabelStyle());
            if (alpha < 0.99f)
                DrawSolidRect(rect, new Color(0f, 0f, 0f, 0.45f));
        }

        private static void DrawCandidateCells(Rect gridRect, int x, int y, int width, int height, Color color)
        {
            for (int cellY = 0; cellY < height; cellY++)
            {
                for (int cellX = 0; cellX < width; cellX++)
                    DrawSolidRect(GetCellRect(gridRect, x + cellX, y + cellY), color);
            }
        }

        private static Rect GetCellRect(Rect gridRect, int x, int y)
        {
            return new Rect(gridRect.x + x * CellPitch, gridRect.y + y * CellPitch, CellSize, CellSize);
        }

        private static Rect GetPlacementRect(Rect gridRect, int x, int y, int width, int height)
        {
            return new Rect(
                gridRect.x + x * CellPitch,
                gridRect.y + y * CellPitch,
                GetGridPixels(width),
                GetGridPixels(height));
        }

        private void DrawMissingPlacement(Rect gridRect, string instanceId, int messageIndex)
        {
            Rect messageRect = new Rect(
                gridRect.x + 4f,
                gridRect.y + 4f + messageIndex * 24f,
                gridRect.width - 8f,
                21f);
            DrawSolidRect(messageRect, MissingPlacementColor);
            GUI.Label(messageRect, $"MISSING PLACEMENT: {instanceId}", GetMissingPlacementStyle());
        }

        private void LogMissingPlacementOnce(IGridStorageOwner owner, ItemInstance item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.InstanceId) || !loggedMissingPlacementIds.Add(item.InstanceId))
                return;

            Debug.LogError(
                $"[InventoryGridDebugView] MISSING PLACEMENT for '{owner?.GridStorageDisplayName}' item " +
                $"'{item.DefinitionId}' [{item.InstanceId}]. The UI will not invent a position; use Legacy List.");
        }

        private static float GetGridPixels(int cellCount)
        {
            return cellCount > 0 ? cellCount * CellSize + (cellCount - 1) * CellGap : 0f;
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

            return Color.HSVToRGB((hash % 360u) / 360f, 0.5f, 0.65f);
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
            return new Rect(rect.x + amount, rect.y + amount,
                Mathf.Max(0f, rect.width - amount * 2f), Mathf.Max(0f, rect.height - amount * 2f));
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
