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
        private const float PanelWidth = 560f;
        private const float PanelHeight = 360f;

        private ContainerLootComponent container;
        private InventoryComponent targetInventory;
        private DebugActionExecutionContext executionContext;
        private ActionDefinition action;
        private Vector2 scrollPosition;
        private string title;
        private string lastMessage;
        private bool isVisible;

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
            container = sourceContainer;
            targetInventory = inventory;
            executionContext = context;
            action = sourceAction;
            title = BuildTitle(sourceContainer, context.Target);
            lastMessage = null;
            scrollPosition = Vector2.zero;
            isVisible = true;
        }

        public void Hide()
        {
            isVisible = false;
            container = null;
            targetInventory = null;
            executionContext = new DebugActionExecutionContext(null, null, null);
            action = null;
            title = null;
            lastMessage = null;
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

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Hide();
            }
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

            if (container == null)
            {
                GUILayout.Label("No storage source.");
                DrawCloseButton();
                GUILayout.EndArea();
                return;
            }

            IReadOnlyList<ItemStorageEntry> entries = container.StorageEntries;
            if (entries == null || entries.Count == 0)
            {
                GUILayout.Label("Storage is empty.");
                DrawCloseButton();
                GUILayout.EndArea();
                return;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Take All", GUILayout.Height(24f)))
            {
                TakeAll();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }

            if (GUILayout.Button("Close", GUILayout.Height(24f)))
            {
                Hide();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(250f));
            for (int index = 0; index < entries.Count; index++)
            {
                if (DrawEntry(index, entries[index]))
                {
                    GUILayout.EndScrollView();
                    GUILayout.EndArea();
                    return;
                }
            }
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private bool DrawEntry(int index, ItemStorageEntry entry)
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

        private void Take(int index, int quantity)
        {
            if (container == null)
            {
                lastMessage = "No storage source.";
                return;
            }

            int transferredQuantity = container.TakeItem(index, quantity, targetInventory, executionContext, action, out string message);
            lastMessage = !string.IsNullOrWhiteSpace(message)
                ? message
                : transferredQuantity > 0 ? $"Transferred x{transferredQuantity}." : "Nothing transferred.";
        }

        private void TakeAll()
        {
            if (container == null)
            {
                lastMessage = "No storage source.";
                return;
            }

            int totalTransferred = 0;
            while (container.HasStoredItems)
            {
                IReadOnlyList<ItemStorageEntry> entries = container.StorageEntries;
                if (entries == null || entries.Count == 0 || entries[0] == null)
                {
                    break;
                }

                int transferredQuantity = container.TakeItem(0, entries[0].Quantity, targetInventory, executionContext, action, out string message);
                if (transferredQuantity <= 0)
                {
                    lastMessage = message;
                    return;
                }

                totalTransferred += transferredQuantity;
            }

            lastMessage = totalTransferred > 0 ? $"Transferred all: x{totalTransferred}." : "Nothing transferred.";
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
            float y = 72f;
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
