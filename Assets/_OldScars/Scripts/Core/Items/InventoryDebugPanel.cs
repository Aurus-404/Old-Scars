using System.Collections.Generic;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OldScars.Core.Items
{
    /// <summary>
    /// OnGUI inventory panel for the Milestone 14 playable debug loop.
    ///
    /// This is not final inventory UI. It only displays the runtime item list
    /// and provides explicit equip/unequip buttons for testing.
    /// </summary>
    public sealed class InventoryDebugPanel : MonoBehaviour
    {
        private const float PanelWidth = 440f;
        private const float PanelHeight = 320f;

        [SerializeField] private InventoryComponent inventory;

        private bool isVisible;
        private Vector2 scrollPosition;

        public bool IsVisible => isVisible;

        public void Hide()
        {
            isVisible = false;
        }

        public bool ContainsScreenPosition(Vector2 screenPosition)
        {
            if (!isVisible)
                return false;

            Vector2 guiPoint = ToGuiPosition(screenPosition);
            return GetPanelRect().Contains(guiPoint);
        }

        private void Update()
        {
            if (Keyboard.current == null)
                return;

            if (Keyboard.current.iKey.wasPressedThisFrame)
            {
                isVisible = !isVisible;
                scrollPosition = Vector2.zero;
            }

            if (isVisible && Keyboard.current.escapeKey.wasPressedThisFrame)
                Hide();
        }

        private void OnGUI()
        {
            if (!isVisible)
                return;

            GUILayout.BeginArea(GetPanelRect(), GUI.skin.box);
            GUILayout.Label("Inventory (Debug v0)");

            if (inventory == null)
            {
                GUILayout.Label("No InventoryComponent assigned.");
                DrawCloseButton();
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label($"Equipped: {GetEquippedItemLabel()}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Unequip", GUILayout.Height(24f)))
                inventory.Unequip();

            if (GUILayout.Button("Close", GUILayout.Height(24f)))
                Hide();
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);

            IReadOnlyList<ItemStorageEntry> entries = inventory.GetStorageEntries();
            if (entries == null || entries.Count == 0)
            {
                GUILayout.Label("Inventory is empty.");
            }
            else
            {
                scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(210f));

                for (int index = 0; index < entries.Count; index++)
                    DrawItemRow(index, entries[index]);

                GUILayout.EndScrollView();
            }

            GUILayout.EndArea();
        }

        private void DrawItemRow(int index, ItemStorageEntry entry)
        {
            ItemInstance item = entry != null ? entry.Item : null;

            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label(GetItemLabel(index, entry), GUILayout.Width(300f));

            bool isEquipped = inventory.EquippedItemIndex == index;
            GUI.enabled = item != null && !isEquipped;
            if (GUILayout.Button(isEquipped ? "Equipped" : "Equip", GUILayout.Height(24f)))
                inventory.EquipIndex(index);
            GUI.enabled = true;

            GUILayout.EndHorizontal();
        }

        private string GetEquippedItemLabel()
        {
            ItemStorageEntry entry = inventory.GetEquippedStorageEntry();
            if (entry == null || entry.Item == null)
                return "(none)";

            return FormatItemDisplayName(entry);
        }

        private string GetItemLabel(int index, ItemStorageEntry entry)
        {
            ItemInstance item = entry != null ? entry.Item : null;
            if (item == null)
                return $"{index}: (none)";

            return $"{index}: {FormatItemDisplayName(entry)} [{item.InstanceId}] condition {item.Condition}";
        }

        private string FormatItemDisplayName(ItemStorageEntry entry)
        {
            if (entry == null || entry.Item == null)
                return "(none)";

            string displayName = GetItemDisplayName(entry.Item.DefinitionId);
            return entry.Quantity > 1 ? $"{displayName} x{entry.Quantity}" : displayName;
        }

        private static string GetItemDisplayName(string definitionId)
        {
            if (GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
                return SafeText(definitionId);

            GameDatabase database = GameDataManager.Instance.Database;
            ItemDefinition definition = database != null ? database.GetItem(definitionId) : null;
            if (definition == null || definition.display == null || string.IsNullOrWhiteSpace(definition.display.name))
                return SafeText(definitionId);

            return definition.display.name;
        }

        private static Rect GetPanelRect()
        {
            float x = Mathf.Max(0f, Screen.width - PanelWidth - 24f);
            float y = 24f;
            return new Rect(x, y, PanelWidth, PanelHeight);
        }

        private void DrawCloseButton()
        {
            GUILayout.Space(8f);
            if (GUILayout.Button("Close", GUILayout.Height(24f)))
                Hide();
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
