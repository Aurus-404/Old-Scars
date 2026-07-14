using System;
using UnityEngine;

namespace OldScars.Core.Items
{
    internal sealed class InventoryContextMenuState
    {
        private const float MenuWidth = 280f;
        private const float MenuHeaderHeight = 24f;
        private const float MenuRowHeight = 28f;
        private const float MenuPadding = 6f;
        private const float MaximumMenuHeight = 340f;
        private const float QuantityDialogWidth = 300f;
        private const float QuantityDialogHeight = 176f;

        private InventoryContextMenuRequest request;
        private InventoryContextAction quantityDialogAction;
        private Vector2 menuScrollPosition;
        private string quantityText = "1";
        private bool confirmQuantityRequested;

        internal bool ContextMenuOpen { get; private set; }
        internal Vector2 ContextMenuPosition { get; private set; }
        internal InventoryContextSourceKind ContextMenuSourceKind => request?.SourceKind ?? InventoryContextSourceKind.Personal;
        internal object ContextMenuOwner => request != null && request.SourceKind == InventoryContextSourceKind.Equipment
            ? (object)request.Equipment
            : request?.Owner;
        internal string ContextMenuInstanceId => request?.InstanceId;
        internal string ContextMenuEquipmentSlotId => request?.EquipmentSlotId;
        internal System.Collections.Generic.IReadOnlyList<InventoryContextAction> ContextMenuActions =>
            request?.Actions ?? Array.Empty<InventoryContextAction>();
        internal bool QuantityDialogOpen { get; private set; }
        internal InventoryContextAction QuantityDialogAction => quantityDialogAction;
        internal int QuantityDialogValue { get; private set; } = 1;
        internal int QuantityDialogMaximum { get; private set; } = 1;
        internal bool BlocksContentInput => ContextMenuOpen || QuantityDialogOpen;

        internal void Open(InventoryContextMenuRequest value, Vector2 position)
        {
            if (value == null || value.Actions == null || value.Actions.Count == 0)
            {
                CloseAll();
                return;
            }

            request = value;
            ContextMenuPosition = position;
            ContextMenuOpen = true;
            QuantityDialogOpen = false;
            quantityDialogAction = null;
            menuScrollPosition = Vector2.zero;
        }

        internal bool CloseContextMenu()
        {
            if (!ContextMenuOpen)
                return false;

            ContextMenuOpen = false;
            request = null;
            menuScrollPosition = Vector2.zero;
            return true;
        }

        internal bool CancelQuantityDialog()
        {
            if (!QuantityDialogOpen)
                return false;

            QuantityDialogOpen = false;
            quantityDialogAction = null;
            request = null;
            quantityText = "1";
            QuantityDialogValue = 1;
            QuantityDialogMaximum = 1;
            confirmQuantityRequested = false;
            return true;
        }

        internal void CloseAll()
        {
            ContextMenuOpen = false;
            QuantityDialogOpen = false;
            request = null;
            quantityDialogAction = null;
            menuScrollPosition = Vector2.zero;
            quantityText = "1";
            QuantityDialogValue = 1;
            QuantityDialogMaximum = 1;
            confirmQuantityRequested = false;
        }

        internal bool ConfirmQuantityFromKeyboard()
        {
            if (!QuantityDialogOpen)
                return false;

            confirmQuantityRequested = true;
            return true;
        }

        internal void Draw(Rect windowRect)
        {
            if ((ContextMenuOpen || QuantityDialogOpen) && !IsTargetAvailable())
            {
                CloseAll();
                return;
            }

            if (QuantityDialogOpen)
                DrawQuantityDialog(windowRect);
            else if (ContextMenuOpen)
                DrawContextMenu(windowRect);
        }

        private void DrawContextMenu(Rect windowRect)
        {
            if (request == null || request.Actions == null || request.Actions.Count == 0)
            {
                CloseAll();
                return;
            }

            float contentHeight = request.Actions.Count * MenuRowHeight;
            float height = Mathf.Min(MaximumMenuHeight, MenuHeaderHeight + contentHeight + MenuPadding * 2f);
            Rect menuRect = ClampToWindow(
                new Rect(ContextMenuPosition.x, ContextMenuPosition.y, MenuWidth, height),
                windowRect);

            Event guiEvent = Event.current;
            if (guiEvent != null && guiEvent.type == EventType.MouseDown &&
                (guiEvent.button == 0 || guiEvent.button == 1) &&
                !menuRect.Contains(guiEvent.mousePosition))
            {
                CloseAll();
                guiEvent.Use();
                return;
            }

            GUI.Box(menuRect, GUIContent.none);
            GUI.Label(
                new Rect(menuRect.x + MenuPadding, menuRect.y + 2f, menuRect.width - MenuPadding * 2f, MenuHeaderHeight),
                "Acciones del item");

            Rect viewport = new Rect(
                menuRect.x + MenuPadding,
                menuRect.y + MenuHeaderHeight,
                menuRect.width - MenuPadding * 2f,
                menuRect.height - MenuHeaderHeight - MenuPadding);
            Rect content = new Rect(0f, 0f, Mathf.Max(1f, viewport.width - 18f), contentHeight);
            menuScrollPosition.x = 0f;
            menuScrollPosition = GUI.BeginScrollView(
                viewport,
                menuScrollPosition,
                content,
                false,
                contentHeight > viewport.height);
            menuScrollPosition.x = 0f;

            InventoryContextAction chosen = null;
            for (int index = 0; index < request.Actions.Count; index++)
            {
                InventoryContextAction action = request.Actions[index];
                Rect rowRect = new Rect(0f, index * MenuRowHeight, content.width, MenuRowHeight - 2f);
                bool previousEnabled = GUI.enabled;
                GUI.enabled = previousEnabled && action.Enabled;
                string label = action.Enabled || string.IsNullOrWhiteSpace(action.DisabledReason)
                    ? action.Label
                    : $"{action.Label} ({ShortReason(action.DisabledReason)})";
                if (GUI.Button(rowRect, label))
                    chosen = action;
                GUI.enabled = previousEnabled;
            }
            GUI.EndScrollView();

            if (chosen == null)
                return;

            if (chosen.RequiresQuantityDialog)
                OpenQuantityDialog(chosen);
            else
                Execute(chosen, GetDefaultQuantity(chosen));
        }

        private void DrawQuantityDialog(Rect windowRect)
        {
            if (request == null || quantityDialogAction == null)
            {
                CloseAll();
                return;
            }

            if (confirmQuantityRequested)
            {
                confirmQuantityRequested = false;
                ConfirmQuantity();
                return;
            }

            Rect dialogRect = ClampToWindow(
                new Rect(
                    windowRect.center.x - QuantityDialogWidth * 0.5f,
                    windowRect.center.y - QuantityDialogHeight * 0.5f,
                    QuantityDialogWidth,
                    QuantityDialogHeight),
                windowRect);

            Event guiEvent = Event.current;
            if (guiEvent != null && guiEvent.type == EventType.MouseDown &&
                (guiEvent.button == 0 || guiEvent.button == 1) &&
                !dialogRect.Contains(guiEvent.mousePosition))
            {
                guiEvent.Use();
            }

            GUI.Box(dialogRect, GUIContent.none);
            GUI.Label(new Rect(dialogRect.x + 16f, dialogRect.y + 12f, dialogRect.width - 32f, 22f), "Cantidad:");

            string edited = GUI.TextField(
                new Rect(dialogRect.x + 16f, dialogRect.y + 38f, dialogRect.width - 32f, 28f),
                quantityText);
            quantityText = FilterDigits(edited);
            if (int.TryParse(quantityText, out int parsed))
                SetQuantity(parsed);

            if (GUI.Button(new Rect(dialogRect.x + 16f, dialogRect.y + 76f, 54f, 28f), "-"))
                SetQuantity(QuantityDialogValue - 1);
            GUI.Label(
                new Rect(dialogRect.x + 76f, dialogRect.y + 76f, dialogRect.width - 152f, 28f),
                $"{QuantityDialogValue} / {QuantityDialogMaximum}",
                CenteredLabel());
            if (GUI.Button(new Rect(dialogRect.xMax - 70f, dialogRect.y + 76f, 54f, 28f), "+"))
                SetQuantity(QuantityDialogValue + 1);

            if (GUI.Button(new Rect(dialogRect.x + 16f, dialogRect.y + 120f, 126f, 32f), "Confirmar"))
            {
                ConfirmQuantity();
                return;
            }
            if (GUI.Button(new Rect(dialogRect.xMax - 142f, dialogRect.y + 120f, 126f, 32f), "Cancelar"))
                CancelQuantityDialog();
        }

        private void OpenQuantityDialog(InventoryContextAction action)
        {
            ContextMenuOpen = false;
            QuantityDialogOpen = true;
            quantityDialogAction = action;
            QuantityDialogMaximum = Mathf.Max(1, request.MaximumQuantity);
            confirmQuantityRequested = false;
            SetQuantity(1);
        }

        private void ConfirmQuantity()
        {
            if (!QuantityDialogOpen || quantityDialogAction == null)
                return;

            SetQuantity(QuantityDialogValue);
            Execute(quantityDialogAction, QuantityDialogValue);
        }

        private void Execute(InventoryContextAction action, int quantity)
        {
            InventoryContextMenuRequest currentRequest = request;
            Action<InventoryContextActionInvocation> executor = currentRequest?.Executor;
            CloseAll();
            executor?.Invoke(new InventoryContextActionInvocation(currentRequest, action, Mathf.Max(0, quantity)));
        }

        private int GetDefaultQuantity(InventoryContextAction action)
        {
            switch (action.Kind)
            {
                case InventoryContextActionKind.DropOne:
                case InventoryContextActionKind.TakeOne:
                case InventoryContextActionKind.DepositOne:
                case InventoryContextActionKind.Use:
                    return 1;
                case InventoryContextActionKind.DropStack:
                case InventoryContextActionKind.TakeStack:
                case InventoryContextActionKind.DepositStack:
                    return request != null ? request.MaximumQuantity : 1;
                default:
                    return 0;
            }
        }

        private void SetQuantity(int value)
        {
            QuantityDialogValue = Mathf.Clamp(value, 1, Mathf.Max(1, QuantityDialogMaximum));
            quantityText = QuantityDialogValue.ToString();
        }

        private static Rect ClampToWindow(Rect rect, Rect windowRect)
        {
            rect.x = Mathf.Clamp(rect.x, windowRect.x, Mathf.Max(windowRect.x, windowRect.xMax - rect.width));
            rect.y = Mathf.Clamp(rect.y, windowRect.y, Mathf.Max(windowRect.y, windowRect.yMax - rect.height));
            return rect;
        }

        private static string FilterDigits(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var chars = new char[value.Length];
            int count = 0;
            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsDigit(value[index]))
                    chars[count++] = value[index];
            }
            return new string(chars, 0, count);
        }

        private static string ShortReason(string reason)
        {
            const int maximumLength = 42;
            string safe = reason.Trim();
            return safe.Length <= maximumLength ? safe : safe.Substring(0, maximumLength - 3) + "...";
        }

        private static GUIStyle CenteredLabel()
        {
            return new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip
            };
        }

        private bool IsTargetAvailable()
        {
            if (request == null || string.IsNullOrWhiteSpace(request.InstanceId))
                return false;

            if (request.SourceKind == InventoryContextSourceKind.Equipment)
            {
                return request.Equipment != null &&
                       request.Equipment.GetEquippedStorageEntry(request.EquipmentSlotId)?.Item?.InstanceId == request.InstanceId;
            }

            return request.Owner != null &&
                   request.Owner.TryGetEntryByInstanceId(request.InstanceId, out _, out ItemStorageEntry entry) &&
                   entry?.Item != null;
        }
    }
}
