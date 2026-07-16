using System;
using System.Collections.Generic;
using OldScars.Core.Actors;
using UnityEngine;

namespace OldScars.Core.Items
{
    public enum InventoryContextActionKind
    {
        ShowDetails,
        ReviewOwnedStorage,
        Use,
        Equip,
        EquipReplacing,
        Unequip,
        DropOne,
        DropAmount,
        DropStack,
        TakeOne,
        TakeAmount,
        TakeStack,
        DepositOne,
        DepositAmount,
        DepositStack,
        MoveToPersonalOne,
        MoveToPersonalAmount,
        MoveToPersonalStack,
        MoveToOwnedStorageOne,
        MoveToOwnedStorageAmount,
        MoveToOwnedStorageStack
    }

    public enum InventoryContextSourceKind
    {
        Personal,
        External,
        Equipment,
        InspectedOwnedStorage,
        World
    }

    public sealed class InventoryContextAction
    {
        public InventoryContextAction(
            InventoryContextActionKind kind,
            string label,
            bool enabled = true,
            string disabledReason = null,
            IReadOnlyList<string> equipmentSlotIds = null,
            string detail = null,
            string targetContainerInstanceId = null)
        {
            Kind = kind;
            Label = string.IsNullOrWhiteSpace(label) ? kind.ToString() : label;
            Enabled = enabled;
            DisabledReason = disabledReason;
            EquipmentSlotIds = Copy(equipmentSlotIds);
            Detail = detail;
            TargetContainerInstanceId = targetContainerInstanceId;
        }

        public InventoryContextActionKind Kind { get; }
        public string Label { get; }
        public bool Enabled { get; }
        public string DisabledReason { get; }
        public string[] EquipmentSlotIds { get; }
        public string Detail { get; }
        public string TargetContainerInstanceId { get; }

        public bool RequiresQuantityDialog =>
            Kind == InventoryContextActionKind.DropAmount ||
            Kind == InventoryContextActionKind.TakeAmount ||
            Kind == InventoryContextActionKind.DepositAmount ||
            Kind == InventoryContextActionKind.MoveToPersonalAmount ||
            Kind == InventoryContextActionKind.MoveToOwnedStorageAmount;

        private static string[] Copy(IReadOnlyList<string> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<string>();

            var copy = new string[source.Count];
            for (int index = 0; index < source.Count; index++)
                copy[index] = source[index];
            return copy;
        }
    }

    public sealed class InventoryContextMenuRequest
    {
        public InventoryContextMenuRequest(
            InventoryContextSourceKind sourceKind,
            IGridStorageOwner owner,
            ActorEquipmentComponent equipment,
            string instanceId,
            string equipmentSlotId,
            int maximumQuantity,
            IReadOnlyList<InventoryContextAction> actions,
            Action<InventoryContextActionInvocation> executor,
            IReadOnlyList<string> sourceEquipmentSlotIds = null)
        {
            SourceKind = sourceKind;
            Owner = owner;
            Equipment = equipment;
            InstanceId = instanceId;
            EquipmentSlotId = equipmentSlotId;
            MaximumQuantity = Mathf.Max(1, maximumQuantity);
            Actions = actions ?? Array.Empty<InventoryContextAction>();
            Executor = executor;
            SourceEquipmentSlotIds = Array.AsReadOnly(Copy(sourceEquipmentSlotIds));
        }

        public InventoryContextSourceKind SourceKind { get; }
        public IGridStorageOwner Owner { get; }
        public ActorEquipmentComponent Equipment { get; }
        public string InstanceId { get; }
        public string EquipmentSlotId { get; }
        public IReadOnlyList<string> SourceEquipmentSlotIds { get; }
        public int MaximumQuantity { get; }
        public IReadOnlyList<InventoryContextAction> Actions { get; }
        internal Action<InventoryContextActionInvocation> Executor { get; }

        private static string[] Copy(IReadOnlyList<string> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<string>();

            var copy = new string[source.Count];
            for (int index = 0; index < source.Count; index++)
                copy[index] = source[index];
            return copy;
        }
    }

    public readonly struct InventoryContextActionInvocation
    {
        public InventoryContextActionInvocation(
            InventoryContextMenuRequest request,
            InventoryContextAction action,
            int quantity)
        {
            Request = request;
            Action = action;
            Quantity = quantity;
        }

        public InventoryContextMenuRequest Request { get; }
        public InventoryContextAction Action { get; }
        public int Quantity { get; }
    }

    public readonly struct EquipmentDebugRowClick
    {
        public EquipmentDebugRowClick(string slotId, string instanceId, Rect rowRect, int mouseButton)
        {
            SlotId = slotId;
            InstanceId = instanceId;
            RowRect = rowRect;
            MouseButton = mouseButton;
        }

        public string SlotId { get; }
        public string InstanceId { get; }
        public Rect RowRect { get; }
        public int MouseButton { get; }
    }
}
