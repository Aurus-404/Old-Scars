using System.Collections.Generic;
using OldScars.Core.Actors;
using OldScars.Core.Data.Definitions;
using UnityEngine;

namespace OldScars.Core.Items
{
    public sealed class EquipmentDebugListView
    {
        private const float RowHeight = 28f;
        private const float GroupHeight = 22f;
        private const float RowHorizontalPadding = 8f;
        private const float RowLabelGap = 8f;
        private const float SlotLabelRatio = 0.43f;
        private readonly List<EquipmentLayoutGroupDefinition> orderedGroups = new List<EquipmentLayoutGroupDefinition>();
        private readonly List<EquipmentLayoutSlotDefinition> orderedSlots = new List<EquipmentLayoutSlotDefinition>();
        private GUIStyle slotLabelStyle;
        private GUIStyle itemLabelStyle;

        public void Draw(
            ActorEquipmentComponent equipment,
            InventoryUISessionSelection selection,
            float width,
            float height)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(width), GUILayout.Height(height));
            GUILayout.Label("Equipment");

            if (equipment == null)
            {
                GUILayout.Label("ActorEquipmentComponent is not configured.");
                GUILayout.EndVertical();
                return;
            }

            EquipmentLayoutDefinition layout = equipment.GetActiveLayout();
            if (layout == null)
            {
                GUILayout.Label($"Waiting for equipment layout '{equipment.EquipmentLayoutId}'.");
                GUILayout.EndVertical();
                return;
            }

            BuildOrder(layout);
            if (selection.TryConsumeEquipmentAutoScroll(out string autoScrollSlotId))
                selection.EquipmentScrollPosition = new Vector2(0f, GetSlotOffset(autoScrollSlotId));

            Vector2 scrollPosition = selection.EquipmentScrollPosition;
            scrollPosition.x = 0f;
            scrollPosition = GUILayout.BeginScrollView(
                scrollPosition,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUILayout.Width(width - 10f),
                GUILayout.Height(height - 34f));
            scrollPosition.x = 0f;
            selection.EquipmentScrollPosition = scrollPosition;

            float rowWidth = Mathf.Max(120f, width - 30f);

            for (int groupIndex = 0; groupIndex < orderedGroups.Count; groupIndex++)
            {
                EquipmentLayoutGroupDefinition group = orderedGroups[groupIndex];
                GUILayout.Label(group.display_name, GUILayout.Width(rowWidth), GUILayout.Height(GroupHeight));
                for (int slotIndex = 0; slotIndex < orderedSlots.Count; slotIndex++)
                {
                    EquipmentLayoutSlotDefinition slot = orderedSlots[slotIndex];
                    if (slot.group_id != group.id)
                        continue;
                    DrawSlotRow(equipment, selection, slot, rowWidth);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawSlotRow(
            ActorEquipmentComponent equipment,
            InventoryUISessionSelection selection,
            EquipmentLayoutSlotDefinition slot,
            float width)
        {
            EquipmentSlotDefinition definition = equipment.GetSlotDefinition(slot.slot_id);
            string slotName = definition != null && !string.IsNullOrWhiteSpace(definition.display_name)
                ? definition.display_name
                : slot.slot_id;
            ItemStorageEntry entry = equipment.GetEquippedStorageEntry(slot.slot_id);
            string itemName = entry != null ? GetItemName(entry) : "Vacío";
            string multiSlot = IsTwoHanded(equipment, entry) ? " — 2H" : string.Empty;
            bool selected = selection.SelectedEquipmentSlotId == slot.slot_id;

            EnsureStyles();
            Rect rowRect = GUILayoutUtility.GetRect(
                width,
                RowHeight,
                GUILayout.Width(width),
                GUILayout.Height(RowHeight));

            Color previous = GUI.backgroundColor;
            if (selected)
                GUI.backgroundColor = new Color(0.42f, 0.72f, 0.92f);
            bool clicked = GUI.Button(rowRect, GUIContent.none);
            GUI.backgroundColor = previous;

            float contentWidth = Mathf.Max(0f, rowRect.width - RowHorizontalPadding * 2f);
            float slotWidth = Mathf.Max(0f, contentWidth * SlotLabelRatio - RowLabelGap * 0.5f);
            float itemWidth = Mathf.Max(0f, contentWidth - slotWidth - RowLabelGap);
            var slotRect = new Rect(
                rowRect.x + RowHorizontalPadding,
                rowRect.y,
                slotWidth,
                rowRect.height);
            var itemRect = new Rect(
                slotRect.xMax + RowLabelGap,
                rowRect.y,
                itemWidth,
                rowRect.height);
            GUI.Label(slotRect, slotName, slotLabelStyle);
            GUI.Label(itemRect, itemName + multiSlot, itemLabelStyle);

            if (clicked)
            {
                selection.SelectEquipment(
                    slot.slot_id,
                    entry?.Item?.InstanceId,
                    false);
            }
        }

        private void EnsureStyles()
        {
            if (slotLabelStyle == null)
            {
                slotLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip,
                    wordWrap = false
                };
            }

            if (itemLabelStyle == null)
            {
                itemLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleRight,
                    clipping = TextClipping.Clip,
                    wordWrap = false
                };
            }
        }

        private void BuildOrder(EquipmentLayoutDefinition layout)
        {
            orderedGroups.Clear();
            orderedSlots.Clear();
            if (layout.groups != null)
                orderedGroups.AddRange(layout.groups);
            if (layout.slots != null)
                orderedSlots.AddRange(layout.slots);

            orderedGroups.Sort((left, right) => left.display_order.CompareTo(right.display_order));
            orderedSlots.Sort((left, right) =>
            {
                int groupComparison = GetGroupOrder(left.group_id).CompareTo(GetGroupOrder(right.group_id));
                return groupComparison != 0
                    ? groupComparison
                    : left.display_order.CompareTo(right.display_order);
            });
        }

        private float GetSlotOffset(string slotId)
        {
            float offset = 0f;
            for (int groupIndex = 0; groupIndex < orderedGroups.Count; groupIndex++)
            {
                offset += GroupHeight;
                string groupId = orderedGroups[groupIndex].id;
                for (int slotIndex = 0; slotIndex < orderedSlots.Count; slotIndex++)
                {
                    EquipmentLayoutSlotDefinition slot = orderedSlots[slotIndex];
                    if (slot.group_id != groupId)
                        continue;
                    if (slot.slot_id == slotId)
                        return Mathf.Max(0f, offset - RowHeight);
                    offset += RowHeight;
                }
            }
            return 0f;
        }

        private int GetGroupOrder(string groupId)
        {
            for (int index = 0; index < orderedGroups.Count; index++)
            {
                if (orderedGroups[index].id == groupId)
                    return orderedGroups[index].display_order;
            }
            return int.MaxValue;
        }

        private static bool IsTwoHanded(ActorEquipmentComponent equipment, ItemStorageEntry entry)
        {
            if (entry?.Item == null)
                return false;
            IReadOnlyList<string> slots = equipment.GetSlotsOccupiedBy(entry.Item.InstanceId);
            bool left = false;
            bool right = false;
            for (int index = 0; index < slots.Count; index++)
            {
                left |= slots[index] == ActorEquipmentComponent.HandLeftSlotId;
                right |= slots[index] == ActorEquipmentComponent.HandRightSlotId;
            }
            return left && right;
        }

        private static string GetItemName(ItemStorageEntry entry)
        {
            if (entry?.Item == null)
                return "Vacío";
            if (OldScars.Core.GameDataManager.Instance == null || !OldScars.Core.GameDataManager.Instance.IsReady)
                return entry.DefinitionId;
            ItemDefinition definition = OldScars.Core.GameDataManager.Instance.Database?.GetItem(entry.DefinitionId);
            return definition != null && definition.display != null && !string.IsNullOrWhiteSpace(definition.display.name)
                ? definition.display.name
                : entry.DefinitionId;
        }
    }
}
