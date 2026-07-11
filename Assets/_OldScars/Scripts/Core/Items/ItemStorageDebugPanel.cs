using System.Collections.Generic;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Interactions;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OldScars.Core.Items
{
    public sealed class ItemStorageDebugPanel : MonoBehaviour
    {
        private const float PanelWidth = 1120f;
        private const float PanelHeight = 650f;
        private const float ColumnWidth = 535f;
        private const float ColumnScrollHeight = 430f;

        private IItemStorageDebugSource storageSource;
        private InventoryComponent targetInventory;
        private DebugActionExecutionContext executionContext;
        private ActionDefinition action;
        private Vector2 storageScrollPosition;
        private Vector2 inventoryScrollPosition;
        private string title;
        private string lastMessage;
        private bool isVisible;
        private bool showLegacyList;
        private readonly InventoryGridDebugView gridView = new InventoryGridDebugView();

        public bool IsVisible => isVisible;

        public static ItemStorageDebugPanel GetOrCreate()
        {
            ItemStorageDebugPanel panel = FindAnyObjectByType<ItemStorageDebugPanel>();
            if (panel != null)
            {
                return panel;
            }

            var panelObject = new GameObject("ItemStorageDebugPanel_Runtime");
            return panelObject.AddComponent<ItemStorageDebugPanel>();
        }

        public void Show(ContainerLootComponent sourceContainer, InventoryComponent inventory, DebugActionExecutionContext context, ActionDefinition sourceAction)
        {
            Show(sourceContainer as IItemStorageDebugSource, inventory, context, sourceAction);
        }

        public void Show(IItemStorageDebugSource source, InventoryComponent inventory, DebugActionExecutionContext context, ActionDefinition sourceAction)
        {
            storageSource = source;
            targetInventory = inventory;
            executionContext = context;
            action = sourceAction;
            title = source != null ? source.GetStorageDebugTitle(context.Target) : BuildTitle(null, context.Target);
            lastMessage = null;
            storageScrollPosition = Vector2.zero;
            inventoryScrollPosition = Vector2.zero;
            showLegacyList = false;
            gridView.Reset();
            isVisible = true;
        }

        public void Hide()
        {
            isVisible = false;
            storageSource = null;
            targetInventory = null;
            executionContext = new DebugActionExecutionContext(null, null, null);
            action = null;
            title = null;
            lastMessage = null;
            showLegacyList = false;
            gridView.Reset();
        }

        public bool ContainsScreenPosition(Vector2 screenPosition)
        {
            if (!isVisible)
            {
                return false;
            }

            Vector2 guiPoint = ToGuiPosition(screenPosition);
            return GetPanelRect().Contains(guiPoint);
        }

        private void Update()
        {
            if (!isVisible || Keyboard.current == null)
            {
                return;
            }

            gridView.HandleKeyboardInput(targetInventory);
            if (Keyboard.current.escapeKey.wasPressedThisFrame && !gridView.CancelDrag())
                Hide();
        }

        private void OnGUI()
        {
            if (!isVisible)
            {
                return;
            }

            GUILayout.BeginArea(GetPanelRect(), GUI.skin.box);
            GUILayout.Label(!string.IsNullOrWhiteSpace(title) ? title : "Storage Debug Panel");

            if (!string.IsNullOrWhiteSpace(lastMessage))
            {
                GUILayout.Label(lastMessage);
            }

            if (storageSource == null)
            {
                GUILayout.Label("No storage source.");
                DrawCloseButton();
                GUILayout.EndArea();
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Height(24f), GUILayout.Width(100f)))
            {
                Hide();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);

            GUILayout.BeginHorizontal();
            if (DrawInventoryColumn())
            {
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }

            GUILayout.Space(8f);

            if (DrawStorageColumn())
            {
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private bool DrawInventoryColumn()
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(ColumnWidth));
            GUILayout.Label("Player Inventory");

            bool gridBatchBlocked = IsGridBatchTransferBlocked();
            GUI.enabled = !gridBatchBlocked;
            if (GUILayout.Button("Deposit All", GUILayout.Height(24f)))
            {
                DepositAll();
                GUILayout.EndVertical();
                GUI.enabled = true;
                return true;
            }
            GUI.enabled = true;

            if (gridBatchBlocked)
                GUILayout.Label("Deposit All is unavailable while the player grid participates. Transfer stacks individually.");

            GUILayout.Space(4f);
            if (targetInventory == null)
            {
                GUILayout.Label("No InventoryComponent assigned.");
            }
            else
            {
                if (GUILayout.Button(showLegacyList ? "Visual Grid" : "Legacy List", GUILayout.Height(24f)))
                    showLegacyList = !showLegacyList;

                if (!showLegacyList && targetInventory.UsesGridLayout)
                {
                    if (DrawInventoryGrid())
                    {
                        GUILayout.EndVertical();
                        return true;
                    }
                }
                else if (DrawLegacyInventoryList())
                {
                    GUILayout.EndVertical();
                    return true;
                }
            }

            GUILayout.EndVertical();
            return false;
        }

        private bool DrawInventoryGrid()
        {
            GUILayout.Label("Player Grid (drag; R rotates)");
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Rect gridRect = GUILayoutUtility.GetRect(
                gridView.GetRequiredWidth(targetInventory.GridWidth),
                gridView.GetRequiredHeight(targetInventory.GridHeight),
                GUILayout.Width(gridView.GetRequiredWidth(targetInventory.GridWidth)),
                GUILayout.Height(gridView.GetRequiredHeight(targetInventory.GridHeight)));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            gridView.Draw(targetInventory, gridRect);

            if (!string.IsNullOrWhiteSpace(gridView.StatusMessage))
                GUILayout.Label(gridView.StatusMessage);

            if (!gridView.TryGetSelectedEntry(targetInventory, out int index, out ItemStorageEntry entry))
            {
                GUILayout.Label("Select an item to deposit it.");
                return false;
            }

            GUILayout.Label(GetEntryLabel(index, entry));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Deposit 1", GUILayout.Height(24f)))
            {
                Deposit(index, 1);
                GUILayout.EndHorizontal();
                return true;
            }

            if (GUILayout.Button("Deposit Stack", GUILayout.Height(24f)))
            {
                Deposit(index, entry.Quantity);
                GUILayout.EndHorizontal();
                return true;
            }
            GUILayout.EndHorizontal();
            return false;
        }

        private bool DrawLegacyInventoryList()
        {
            IReadOnlyList<ItemStorageEntry> inventoryEntries = targetInventory.GetStorageEntries();
            if (inventoryEntries == null || inventoryEntries.Count == 0)
            {
                GUILayout.Label("Inventory is empty.");
                return false;
            }

            inventoryScrollPosition = GUILayout.BeginScrollView(inventoryScrollPosition, GUILayout.Height(ColumnScrollHeight));
            for (int index = 0; index < inventoryEntries.Count; index++)
            {
                if (!DrawInventoryEntry(index, inventoryEntries[index]))
                    continue;

                GUILayout.EndScrollView();
                return true;
            }
            GUILayout.EndScrollView();
            return false;
        }

        private bool DrawStorageColumn()
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(ColumnWidth));
            GUILayout.Label("Open Storage");

            bool gridBatchBlocked = IsGridBatchTransferBlocked();
            GUI.enabled = !gridBatchBlocked;
            if (GUILayout.Button("Take All", GUILayout.Height(24f)))
            {
                TakeAll();
                GUILayout.EndVertical();
                GUI.enabled = true;
                return true;
            }
            GUI.enabled = true;

            if (gridBatchBlocked)
                GUILayout.Label("Take All is unavailable while the player grid participates. Transfer stacks individually.");

            GUILayout.Space(4f);
            IReadOnlyList<ItemStorageEntry> entries = storageSource.StorageEntries;
            if (entries == null || entries.Count == 0)
            {
                GUILayout.Label("Storage is empty.");
            }
            else
            {
                storageScrollPosition = GUILayout.BeginScrollView(storageScrollPosition, GUILayout.Height(ColumnScrollHeight));
                for (int index = 0; index < entries.Count; index++)
                {
                    if (DrawStorageEntry(index, entries[index]))
                    {
                        GUILayout.EndScrollView();
                        GUILayout.EndVertical();
                        return true;
                    }
                }
                GUILayout.EndScrollView();
            }

            GUILayout.EndVertical();
            return false;
        }

        private bool DrawStorageEntry(int index, ItemStorageEntry entry)
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label(GetEntryLabel(index, entry), GUILayout.Width(310f));

            if (GUILayout.Button("Take 1", GUILayout.Height(24f), GUILayout.Width(75f)))
            {
                Take(index, 1);
                GUILayout.EndHorizontal();
                return true;
            }

            int stackQuantity = entry != null ? entry.Quantity : 0;
            if (GUILayout.Button("Take Stack", GUILayout.Height(24f), GUILayout.Width(95f)))
            {
                Take(index, stackQuantity);
                GUILayout.EndHorizontal();
                return true;
            }

            GUILayout.EndHorizontal();
            return false;
        }

        private bool DrawInventoryEntry(int index, ItemStorageEntry entry)
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label(GetEntryLabel(index, entry), GUILayout.Width(310f));

            if (GUILayout.Button("Deposit 1", GUILayout.Height(24f), GUILayout.Width(75f)))
            {
                Deposit(index, 1);
                GUILayout.EndHorizontal();
                return true;
            }

            int stackQuantity = entry != null ? entry.Quantity : 0;
            if (GUILayout.Button("Deposit Stack", GUILayout.Height(24f), GUILayout.Width(95f)))
            {
                Deposit(index, stackQuantity);
                GUILayout.EndHorizontal();
                return true;
            }

            GUILayout.EndHorizontal();
            return false;
        }

        private void Take(int index, int quantity)
        {
            if (storageSource == null)
            {
                lastMessage = "No storage source.";
                return;
            }

            int transferredQuantity = storageSource.TakeItem(index, quantity, targetInventory, executionContext, action, out string message);
            lastMessage = !string.IsNullOrWhiteSpace(message)
                ? message
                : transferredQuantity > 0 ? $"Transferred x{transferredQuantity}." : "Nothing transferred.";
            gridView.ReconcileSelection(targetInventory);
        }

        private void TakeAll()
        {
            if (IsGridBatchTransferBlocked())
            {
                lastMessage = "Take All is unavailable while the player grid participates. Transfer stacks individually.";
                return;
            }

            if (storageSource == null)
            {
                lastMessage = "No storage source.";
                return;
            }

            int totalTransferred = 0;
            while (storageSource.HasStoredItems)
            {
                IReadOnlyList<ItemStorageEntry> entries = storageSource.StorageEntries;
                if (entries == null || entries.Count == 0 || entries[0] == null)
                {
                    break;
                }

                int transferredQuantity = storageSource.TakeItem(0, entries[0].Quantity, targetInventory, executionContext, action, out string message);
                if (transferredQuantity <= 0)
                {
                    lastMessage = message;
                    return;
                }

                totalTransferred += transferredQuantity;
            }

            lastMessage = totalTransferred > 0 ? $"Transferred all: x{totalTransferred}." : "Nothing transferred.";
        }

        private void Deposit(int index, int quantity)
        {
            if (storageSource == null)
            {
                lastMessage = "No storage source.";
                return;
            }

            int transferredQuantity = storageSource.DepositItem(index, quantity, targetInventory, executionContext, action, out string message);
            lastMessage = !string.IsNullOrWhiteSpace(message)
                ? message
                : transferredQuantity > 0 ? $"Deposited x{transferredQuantity}." : "Nothing deposited.";
            gridView.ReconcileSelection(targetInventory);
        }

        private void DepositAll()
        {
            if (IsGridBatchTransferBlocked())
            {
                lastMessage = "Deposit All is unavailable while the player grid participates. Transfer stacks individually.";
                return;
            }

            if (storageSource == null)
            {
                lastMessage = "No storage source.";
                return;
            }

            if (targetInventory == null)
            {
                lastMessage = "No InventoryComponent assigned.";
                return;
            }

            int totalTransferred = 0;
            while (!targetInventory.IsEmpty)
            {
                IReadOnlyList<ItemStorageEntry> entries = targetInventory.GetStorageEntries();
                if (entries == null || entries.Count == 0 || entries[0] == null)
                    break;

                int transferredQuantity = storageSource.DepositItem(0, entries[0].Quantity, targetInventory, executionContext, action, out string message);
                if (transferredQuantity <= 0)
                {
                    lastMessage = message;
                    return;
                }

                totalTransferred += transferredQuantity;
            }

            lastMessage = totalTransferred > 0 ? $"Deposited all: x{totalTransferred}." : "Nothing deposited.";
        }

        private bool IsGridBatchTransferBlocked()
        {
            return targetInventory != null && targetInventory.UsesGridLayout;
        }

        private static string GetEntryLabel(int index, ItemStorageEntry entry)
        {
            if (entry == null || entry.Item == null)
            {
                return $"{index}: (none)";
            }

            string displayName = GetItemDisplayName(entry.DefinitionId);
            return $"{index}: {displayName} x{entry.Quantity} [{entry.Item.InstanceId}]";
        }

        private static string GetItemDisplayName(string definitionId)
        {
            if (GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
            {
                return SafeText(definitionId);
            }

            GameDatabase database = GameDataManager.Instance.Database;
            ItemDefinition definition = database != null ? database.GetItem(definitionId) : null;
            if (definition == null || definition.display == null || string.IsNullOrWhiteSpace(definition.display.name))
            {
                return SafeText(definitionId);
            }

            return definition.display.name;
        }

        private static string BuildTitle(ContainerLootComponent sourceContainer, WorldObjectTags target)
        {
            string targetName = target != null ? target.name : sourceContainer != null ? sourceContainer.name : "Storage";
            WorldObjectDebugInfo debugInfo = target != null ? target.GetComponent<WorldObjectDebugInfo>() : null;
            string displayName = debugInfo != null ? debugInfo.GetDisplayNameOrFallback(targetName, target) : targetName;
            return $"{displayName} Contents (Debug)";
        }

        private static Rect GetPanelRect()
        {
            float x = Mathf.Max(0f, (Screen.width - PanelWidth) * 0.5f);
            float y = 24f;
            return new Rect(x, y, PanelWidth, PanelHeight);
        }

        private void DrawCloseButton()
        {
            GUILayout.Space(8f);
            if (GUILayout.Button("Close", GUILayout.Height(24f)))
            {
                Hide();
            }
        }

        private static Vector2 ToGuiPosition(Vector2 mousePosition)
        {
            return new Vector2(mousePosition.x, Screen.height - mousePosition.y);
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
        }
    }
}
