using System.Collections.Generic;
using OldScars.Core.Actors;
using OldScars.Core.Combat;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OldScars.Core.Items
{
    /// <summary>
    /// OnGUI inventory panel for the playable debug loop.
    ///
    /// This is not final inventory UI. M33.1 adds a visual grid and manual
    /// placement testing while keeping the legacy list as a debug fallback.
    /// </summary>
    public sealed class InventoryDebugPanel : MonoBehaviour
    {
        private const float PanelWidth = 1020f;
        private const float PanelHeight = 600f;

        [SerializeField] private InventoryComponent inventory;
        [SerializeField] private ActorNeedsComponent actorNeeds;
        [SerializeField] private ActorHealthComponent actorHealth;
        [SerializeField] private FirearmDebugController firearmController;

        private bool isVisible;
        private Vector2 scrollPosition;
        private string lastMessage;
        private bool showLegacyList;
        private readonly InventoryGridDebugView gridView = new InventoryGridDebugView();

        public bool IsVisible => isVisible;

        private void Awake()
        {
            ResolveActorNeeds();
            ResolveActorHealth();
            ResolveFirearmController();
        }

        private void OnEnable()
        {
            ResolveActorNeeds();
            ResolveActorHealth();
            ResolveFirearmController();
        }

        public void Hide()
        {
            isVisible = false;
            gridView.Reset();
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
                if (isVisible)
                    Hide();
                else
                    isVisible = true;
                scrollPosition = Vector2.zero;
            }

            if (!isVisible)
                return;

            gridView.HandleKeyboardInput(inventory);
            if (Keyboard.current.escapeKey.wasPressedThisFrame && !gridView.CancelDrag())
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

            DrawEquippedSection();
            DrawFirearmSection();

            GUILayout.Space(8f);

            if (!string.IsNullOrWhiteSpace(lastMessage))
                GUILayout.Label(lastMessage);

            if (!string.IsNullOrWhiteSpace(gridView.StatusMessage))
                GUILayout.Label(gridView.StatusMessage);

            GUILayout.BeginHorizontal();
            GUILayout.Label(inventory.UsesGridLayout
                ? $"Grid backend: {inventory.GridWidth}x{inventory.GridHeight}"
                : "Grid backend inactive: use Legacy List.");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(showLegacyList ? "Visual Grid" : "Legacy List", GUILayout.Width(110f), GUILayout.Height(24f)))
                showLegacyList = !showLegacyList;
            GUILayout.EndHorizontal();

            if (showLegacyList || !inventory.UsesGridLayout)
                DrawLegacyStorage();
            else
                DrawVisualGridStorage();

            GUILayout.EndArea();
        }

        private void DrawVisualGridStorage()
        {
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(292f));
            GUILayout.Label("Player Grid (drag; R rotates)");
            Rect gridRect = GUILayoutUtility.GetRect(
                gridView.GetRequiredWidth(inventory.GridWidth),
                gridView.GetRequiredHeight(inventory.GridHeight),
                GUILayout.Width(gridView.GetRequiredWidth(inventory.GridWidth)),
                GUILayout.Height(gridView.GetRequiredHeight(inventory.GridHeight)));
            gridView.Draw(inventory, gridRect);
            GUILayout.EndVertical();

            GUILayout.Space(8f);
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.Height(390f));
            DrawSelectedItemDetails();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        private void DrawSelectedItemDetails()
        {
            GUILayout.Label("Selected Item");
            if (!gridView.TryGetSelectedEntry(inventory, out int index, out ItemStorageEntry entry))
            {
                GUILayout.Label("Click an item in the grid.");
                return;
            }

            ItemInstance item = entry.Item;
            GUILayout.Label(FormatItemDisplayName(entry));
            GUILayout.Label($"Instance: {item.InstanceId}");
            GUILayout.Label($"Condition: {item.Condition}");
            if (inventory.TryGetGridPlacement(item.InstanceId, out GridPlacement placement))
            {
                GUILayout.Label(
                    $"Placement: ({placement.X},{placement.Y}) " +
                    $"{placement.EffectiveWidth}x{placement.EffectiveHeight} " +
                    (placement.IsRotated ? "rotated" : "original"));
            }

            GUILayout.Space(8f);
            bool isEquipped = inventory.IsRightHandStorageEntry(entry);
            if (InventoryItemUseService.IsConsumable(entry) && GUILayout.Button("Use", GUILayout.Height(28f)))
                UseItem(index);

            if (isEquipped)
            {
                if (GUILayout.Button("Unequip", GUILayout.Height(28f)))
                    inventory.UnequipRightHand();
            }
            else if (inventory.CanEquipIndexToRightHand(index))
            {
                if (GUILayout.Button("Equip", GUILayout.Height(28f)))
                    inventory.TryEquipIndexToRightHand(index);
            }

            if (GUILayout.Button("Drop 1", GUILayout.Height(28f)))
                DropItem(index, 1, "drop_1", "Drop 1");

            if (entry.Quantity > 1 && GUILayout.Button("Drop Stack", GUILayout.Height(28f)))
                DropItem(index, entry.Quantity, "drop_stack", "Drop Stack");
        }

        private void DrawLegacyStorage()
        {
            GUILayout.Label("Storage (Legacy List):");
            IReadOnlyList<ItemStorageEntry> entries = inventory.GetStorageEntries();
            if (entries == null || entries.Count == 0)
            {
                GUILayout.Label("Storage is empty.");
                return;
            }

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(330f));
            for (int index = 0; index < entries.Count; index++)
                DrawItemRow(index, entries[index]);
            GUILayout.EndScrollView();
        }

        private void DrawEquippedSection()
        {
            GUILayout.Label("Equipped:");
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label($"Right Hand: {GetEquippedItemLabel()}", GUILayout.Width(340f));

            bool hasRightHandItem = inventory.GetRightHandStorageEntry() != null;
            GUI.enabled = hasRightHandItem;
            if (GUILayout.Button("Unequip", GUILayout.Height(24f), GUILayout.Width(90f)))
                inventory.UnequipRightHand();
            GUI.enabled = true;

            if (GUILayout.Button("Close", GUILayout.Height(24f), GUILayout.Width(90f)))
                Hide();

            GUILayout.EndHorizontal();
        }

        private void DrawFirearmSection()
        {
            ResolveFirearmController();
            if (firearmController == null || !firearmController.HasEquippedFirearm)
                return;

            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label($"Equipped Firearm: {firearmController.EquippedFirearmDisplayName}", GUILayout.Width(330f));
            GUILayout.Label(firearmController.StatusText, GUILayout.Width(240f));
            GUILayout.Label("F: Toggle Aim", GUILayout.Width(100f));

            GUILayout.EndHorizontal();
        }

        private void DrawItemRow(int index, ItemStorageEntry entry)
        {
            ItemInstance item = entry != null ? entry.Item : null;

            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label(GetItemLabel(index, entry), GUILayout.Width(590f));

            bool isEquipped = inventory.IsRightHandEquippedIndex(index);
            if (isEquipped || inventory.CanEquipIndexToRightHand(index))
            {
                GUI.enabled = item != null && !isEquipped;
                if (GUILayout.Button(isEquipped ? "Equipped" : "Equip", GUILayout.Height(24f), GUILayout.Width(80f)))
                    inventory.TryEquipIndexToRightHand(index);
                GUI.enabled = true;
            }

            if (InventoryItemUseService.IsConsumable(entry))
            {
                if (GUILayout.Button("Use", GUILayout.Height(24f), GUILayout.Width(80f)))
                    UseItem(index);
            }

            DrawDropButtons(index, entry);

            GUILayout.EndHorizontal();
        }

        private void DrawDropButtons(int index, ItemStorageEntry entry)
        {
            if (entry == null || entry.Item == null)
                return;

            if (entry.Quantity <= 1)
            {
                if (GUILayout.Button("Drop", GUILayout.Height(24f), GUILayout.Width(90f)))
                    DropItem(index, 1, "drop", "Drop");

                return;
            }

            if (GUILayout.Button("Drop 1", GUILayout.Height(24f), GUILayout.Width(90f)))
                DropItem(index, 1, "drop_1", "Drop 1");

            if (GUILayout.Button("Drop Stack", GUILayout.Height(24f), GUILayout.Width(100f)))
                DropItem(index, entry.Quantity, "drop_stack", "Drop Stack");
        }

        private void DropItem(int index, int quantity, string actionId, string actionDisplayName)
        {
            bool dropped = DroppedWorldItemSpawner.TryDrop(
                inventory,
                index,
                quantity,
                actionId,
                actionDisplayName,
                out string message);

            lastMessage = message;
            if (!dropped)
                Debug.LogWarning($"[InventoryDebugPanel] {message}");

            gridView.ReconcileSelection(inventory);
            GUIUtility.ExitGUI();
        }

        private void UseItem(int index)
        {
            ResolveActorNeeds();
            ResolveActorHealth();

            InventoryItemUseResult result = InventoryItemUseService.TryUseItem(inventory, index, actorNeeds, actorHealth);
            lastMessage = result.Message;
            if (!result.Success)
                Debug.Log($"[InventoryDebugPanel] Use failed: {result.Message}");

            gridView.ReconcileSelection(inventory);
        }

        private string GetEquippedItemLabel()
        {
            ItemStorageEntry entry = inventory.GetEquippedStorageEntry();
            if (entry == null || entry.Item == null)
                return "Empty";

            return FormatItemDisplayName(entry);
        }

        private string GetItemLabel(int index, ItemStorageEntry entry)
        {
            ItemInstance item = entry != null ? entry.Item : null;
            if (item == null)
                return $"{index}: (none)";

            string equippedMarker = inventory != null && inventory.IsRightHandEquippedIndex(index) ? " (Equipped)" : string.Empty;
            string gridDiagnostic = GetGridDiagnostic(item);
            return $"{index}: {FormatItemDisplayName(entry)}{equippedMarker} [{item.InstanceId}] condition {item.Condition} | {gridDiagnostic}";
        }

        private string GetGridDiagnostic(ItemInstance item)
        {
            if (inventory == null || item == null)
                return "grid unavailable";

            if (!inventory.TryGetGridFootprint(item.DefinitionId, out GridFootprint footprint, out bool usedFallback))
                return "footprint invalid";

            string fallbackLabel = usedFallback ? ", fallback 1x1" : string.Empty;
            if (!inventory.UsesGridLayout)
                return $"footprint {footprint.Width}x{footprint.Height}{fallbackLabel}, placement linear";

            if (!inventory.TryGetGridPlacement(item.InstanceId, out GridPlacement placement))
                return $"footprint {footprint.Width}x{footprint.Height}{fallbackLabel}, placement missing";

            string orientation = placement.IsRotated ? "rotated" : "original";
            return $"footprint {footprint.Width}x{footprint.Height}{fallbackLabel}, placement ({placement.X},{placement.Y}), {orientation}";
        }

        private string FormatItemDisplayName(ItemStorageEntry entry)
        {
            if (entry == null || entry.Item == null)
                return "(none)";

            string displayName = GetItemDisplayName(entry.Item.DefinitionId);
            return $"{displayName} x{entry.Quantity}";
        }

        private static string GetItemDisplayName(string definitionId)
        {
            ItemDefinition definition = GetItemDefinition(definitionId);
            if (definition == null || definition.display == null || string.IsNullOrWhiteSpace(definition.display.name))
                return SafeText(definitionId);

            return definition.display.name;
        }

        private static ItemDefinition GetItemDefinition(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
                return null;

            if (GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
                return null;

            GameDatabase database = GameDataManager.Instance.Database;
            return database != null ? database.GetItem(definitionId) : null;
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

        private void ResolveActorNeeds()
        {
            if (actorNeeds != null)
                return;

            if (inventory != null)
                actorNeeds = inventory.GetComponentInParent<ActorNeedsComponent>();

            if (actorNeeds == null)
                actorNeeds = FindAnyObjectByType<ActorNeedsComponent>();
        }

        private void ResolveActorHealth()
        {
            if (actorHealth != null)
                return;

            if (inventory != null)
                actorHealth = inventory.GetComponentInParent<ActorHealthComponent>();

            if (actorHealth == null)
                actorHealth = FindAnyObjectByType<ActorHealthComponent>();
        }

        private void ResolveFirearmController()
        {
            if (firearmController != null)
                return;

            if (inventory != null)
                firearmController = inventory.GetComponentInParent<FirearmDebugController>();

            if (firearmController == null)
                firearmController = FindAnyObjectByType<FirearmDebugController>();
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
        }
    }
}
