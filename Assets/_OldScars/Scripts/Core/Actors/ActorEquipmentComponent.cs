using System;
using System.Collections;
using System.Collections.Generic;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Feedback;
using OldScars.Core.Items;
using UnityEngine;

namespace OldScars.Core.Actors
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InventoryComponent))]
    [RequireComponent(typeof(ActorItemOwnershipComponent))]
    public sealed class ActorEquipmentComponent : MonoBehaviour, IEquipmentVisualSource
    {
        public const string BackSlotId = "back";
        public const string DefaultHumanLayoutId = "human_standard_01";
        public const string HandLeftSlotId = "hand_left";
        public const string HandRightSlotId = "hand_right";

        [SerializeField] private string equipmentLayoutId = DefaultHumanLayoutId;
        [SerializeField] private InventoryComponent inventoryComponent;
        [SerializeField] private ActorItemOwnershipComponent ownershipComponent;

        private readonly ItemStorage equipmentStorage = new ItemStorage();
        private readonly Dictionary<string, string> slotToInstanceId = new Dictionary<string, string>();
        private readonly Dictionary<string, string[]> instanceToSlots = new Dictionary<string, string[]>();
        private GridInventoryBackend equipmentBackend;
        private int equipmentVersion;
        private long committedVisualRevision;

        public event EventHandler<EquipmentVisualStateCommittedEventArgs> VisualStateCommitted;

        public string EquipmentLayoutId => equipmentLayoutId;
        public int Version => equipmentVersion;
        public int StorageVersion => equipmentStorage.Version;
        public IReadOnlyList<ItemStorageEntry> Entries => equipmentStorage.Entries;
        public bool IsEmpty => equipmentStorage.IsEmpty;

        public EquipmentVisualStateSnapshot CaptureVisualSnapshot()
        {
            return CaptureVisualSnapshot(committedVisualRevision);
        }

        internal InventoryComponent PersonalInventory
        {
            get
            {
                ResolveReferences();
                return inventoryComponent;
            }
        }

        internal ActorItemOwnershipComponent Ownership
        {
            get
            {
                ResolveReferences();
                return ownershipComponent;
            }
        }

        internal GridInventoryBackend Backend
        {
            get
            {
                if (equipmentBackend == null)
                    equipmentBackend = new GridInventoryBackend(equipmentStorage, ResolveItemDefinition);
                return equipmentBackend;
            }
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private IEnumerator Start()
        {
            while (GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
                yield return null;

            ResolveReferences();
            if (inventoryComponent != null && !inventoryComponent.TryMigrateLegacyRightHandToEquipment(this))
            {
                Debug.LogWarning(
                    "[ActorEquipmentComponent] Legacy right_hand migration was rejected; " +
                    "the legacy value remains available for diagnosis.",
                    this);
            }
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(equipmentLayoutId))
                equipmentLayoutId = DefaultHumanLayoutId;
            ResolveReferences();
        }

        public EquipmentLayoutDefinition GetActiveLayout()
        {
            GameDatabase database = GetDatabase();
            return database != null ? database.GetEquipmentLayout(equipmentLayoutId) : null;
        }

        public bool TrySetLayout(string layoutId, out string reason)
        {
            reason = null;
            if (string.IsNullOrWhiteSpace(layoutId))
            {
                reason = "Equipment layout id is required.";
                return false;
            }

            GameDatabase database = GetDatabase();
            if (database == null || database.GetEquipmentLayout(layoutId) == null)
            {
                reason = $"Equipment layout '{layoutId}' was not loaded.";
                return false;
            }

            if (!equipmentStorage.IsEmpty || slotToInstanceId.Count > 0)
            {
                reason = "Cannot change equipment layout while items are equipped.";
                return false;
            }

            if (equipmentLayoutId == layoutId)
                return true;

            equipmentLayoutId = layoutId;
            equipmentVersion++;
            return true;
        }

        public EquipmentSlotDefinition GetSlotDefinition(string slotId)
        {
            GameDatabase database = GetDatabase();
            return database != null ? database.GetEquipmentSlot(slotId) : null;
        }

        public bool HasSlot(string slotId)
        {
            EquipmentLayoutDefinition layout = GetActiveLayout();
            if (layout == null || layout.slots == null || string.IsNullOrWhiteSpace(slotId))
                return false;

            for (int index = 0; index < layout.slots.Length; index++)
            {
                EquipmentLayoutSlotDefinition slot = layout.slots[index];
                if (slot != null && slot.slot_id == slotId)
                    return true;
            }
            return false;
        }

        public ItemInstance GetEquippedInstance(string slotId)
        {
            ItemStorageEntry entry = GetEquippedStorageEntry(slotId);
            return entry != null ? entry.Item : null;
        }

        public ItemStorageEntry GetEquippedStorageEntry(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId) || !slotToInstanceId.TryGetValue(slotId, out string instanceId))
                return null;
            return equipmentStorage.GetEntryByInstanceId(instanceId);
        }

        public bool TryGetEntryByInstanceId(string instanceId, out ItemStorageEntry entry)
        {
            entry = equipmentStorage.GetEntryByInstanceId(instanceId);
            return entry != null && entry.Item != null;
        }

        public IReadOnlyList<string> GetSlotsOccupiedBy(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId) || !instanceToSlots.TryGetValue(instanceId, out string[] slots))
                return Array.Empty<string>();
            return (string[])slots.Clone();
        }

        public bool IsEquipped(string instanceId)
        {
            return !string.IsNullOrWhiteSpace(instanceId) && instanceToSlots.ContainsKey(instanceId);
        }

        public IReadOnlyList<EquipmentSlotSet> GetCompatibleSlotSets(string instanceId)
        {
            return EquipmentTransactionService.GetCompatibleSlotSets(this, instanceId, false);
        }

        public IReadOnlyList<EquipmentSlotSet> GetCompatibleSlotSets(IGridStorageOwner sourceOwner, string instanceId)
        {
            return ReferenceEquals(sourceOwner, PersonalInventory)
                ? GetCompatibleSlotSets(instanceId)
                : EquipmentOwnedStorageTransactionService.GetCompatibleSlotSets(this, sourceOwner, instanceId);
        }

        public IReadOnlyList<EquipmentSlotSet> GetCompatibleEquippedSlotSets(string instanceId)
        {
            return EquipmentTransactionService.GetCompatibleEquippedSlotSets(this, instanceId);
        }

        public IReadOnlyList<EquipmentSlotSet> GetAvailableSlotSets(string instanceId)
        {
            return EquipmentTransactionService.GetCompatibleSlotSets(this, instanceId, true);
        }

        public EquipmentPreview PreviewEquip(string instanceId, IReadOnlyList<string> requestedSlotSet = null)
        {
            return EquipmentTransactionService.PreviewEquip(this, instanceId, requestedSlotSet);
        }

        public EquipmentPreview PreviewEquip(
            IGridStorageOwner sourceOwner,
            string instanceId,
            IReadOnlyList<string> requestedSlotSet = null)
        {
            return ReferenceEquals(sourceOwner, PersonalInventory)
                ? PreviewEquip(instanceId, requestedSlotSet)
                : EquipmentOwnedStorageTransactionService.PreviewEquip(this, sourceOwner, instanceId, requestedSlotSet);
        }

        public EquipmentMutationResult Equip(EquipmentPreview preview)
        {
            return EquipmentTransactionService.Equip(this, preview);
        }

        internal EquipmentMutationResult EquipLegacyMigration(EquipmentPreview preview)
        {
            return EquipmentTransactionService.Equip(this, preview, null);
        }

        internal void CommitLegacyMigrationVisualState()
        {
            CommitVisualState(EquipmentVisualCommitKind.LegacyMigration);
        }

        public EquipmentMutationResult Equip(IGridStorageOwner sourceOwner, EquipmentPreview preview)
        {
            return ReferenceEquals(sourceOwner, PersonalInventory)
                ? Equip(preview)
                : EquipmentOwnedStorageTransactionService.Equip(this, sourceOwner, preview);
        }

        public EquipmentReplacementPlan PreviewEquipReplacing(
            string instanceId,
            IReadOnlyList<string> requestedSlotSet)
        {
            return EquipmentTransactionService.PreviewEquipReplacing(this, instanceId, requestedSlotSet);
        }

        public EquipmentReplacementPlan PreviewEquipReplacing(
            IGridStorageOwner sourceOwner,
            string instanceId,
            IReadOnlyList<string> requestedSlotSet)
        {
            return ReferenceEquals(sourceOwner, PersonalInventory)
                ? PreviewEquipReplacing(instanceId, requestedSlotSet)
                : EquipmentOwnedStorageTransactionService.PreviewEquipReplacing(this, sourceOwner, instanceId, requestedSlotSet);
        }

        public EquipmentMutationResult EquipReplacing(EquipmentReplacementPlan plan)
        {
            return EquipmentTransactionService.EquipReplacing(this, plan);
        }

        public EquipmentMutationResult EquipReplacing(IGridStorageOwner sourceOwner, EquipmentReplacementPlan plan)
        {
            return ReferenceEquals(sourceOwner, PersonalInventory)
                ? EquipReplacing(plan)
                : EquipmentOwnedStorageTransactionService.EquipReplacing(this, sourceOwner, plan);
        }

        public EquipmentPreview PreviewUnequip(string instanceId)
        {
            return EquipmentTransactionService.PreviewUnequip(this, instanceId);
        }

        public EquipmentMutationResult Unequip(EquipmentPreview preview)
        {
            return EquipmentTransactionService.Unequip(this, preview);
        }

        public EquipmentRelocationPlan PreviewRelocateEquipped(
            string instanceId,
            IReadOnlyList<string> requestedSlotSet)
        {
            return EquipmentTransactionService.PreviewRelocateEquipped(this, instanceId, requestedSlotSet);
        }

        public EquipmentMutationResult RelocateEquipped(EquipmentRelocationPlan plan)
        {
            return EquipmentTransactionService.RelocateEquipped(this, plan);
        }

        public EquipmentStorageTransferPlan PreviewTransferEquippedToStorage(
            string instanceId,
            IGridStorageOwner destination,
            GridStorageTransferContext context)
        {
            return EquipmentTransactionService.PreviewTransferEquippedToStorage(this, instanceId, destination, context);
        }

        public EquipmentMutationResult TransferEquippedToStorage(
            IGridStorageOwner destination,
            EquipmentStorageTransferPlan plan,
            GridStorageTransferContext context)
        {
            return EquipmentTransactionService.TransferEquippedToStorage(this, destination, plan, context);
        }

        internal bool IsSlotFree(string slotId, string exceptInstanceId = null)
        {
            if (!slotToInstanceId.TryGetValue(slotId, out string occupiedBy))
                return true;
            return !string.IsNullOrWhiteSpace(exceptInstanceId) && occupiedBy == exceptInstanceId;
        }

        internal bool TryGetSlotOccupant(string slotId, out string instanceId)
        {
            instanceId = null;
            return !string.IsNullOrWhiteSpace(slotId) &&
                   slotToInstanceId.TryGetValue(slotId, out instanceId) &&
                   !string.IsNullOrWhiteSpace(instanceId);
        }

        internal EquipmentStateSnapshot CaptureEquipmentState()
        {
            var slots = new Dictionary<string, string>(slotToInstanceId);
            var instances = new Dictionary<string, string[]>();
            foreach (KeyValuePair<string, string[]> pair in instanceToSlots)
                instances[pair.Key] = pair.Value != null ? (string[])pair.Value.Clone() : Array.Empty<string>();
            return new EquipmentStateSnapshot(slots, instances, equipmentVersion);
        }

        internal void RestoreEquipmentState(EquipmentStateSnapshot snapshot)
        {
            slotToInstanceId.Clear();
            foreach (KeyValuePair<string, string> pair in snapshot.SlotToInstanceId)
                slotToInstanceId[pair.Key] = pair.Value;

            instanceToSlots.Clear();
            foreach (KeyValuePair<string, string[]> pair in snapshot.InstanceToSlots)
                instanceToSlots[pair.Key] = pair.Value != null ? (string[])pair.Value.Clone() : Array.Empty<string>();
            equipmentVersion = snapshot.Version;
        }

        internal void AssignSlots(string instanceId, string[] slotIds)
        {
            if (string.IsNullOrWhiteSpace(instanceId) || slotIds == null || slotIds.Length == 0)
                throw new InvalidOperationException("Cannot assign empty equipment slots.");
            if (instanceToSlots.ContainsKey(instanceId))
                throw new InvalidOperationException($"Item instance '{instanceId}' is already equipped.");

            for (int index = 0; index < slotIds.Length; index++)
            {
                string slotId = slotIds[index];
                if (!HasSlot(slotId) || !IsSlotFree(slotId))
                    throw new InvalidOperationException($"Equipment slot '{slotId}' is unavailable.");
            }

            string[] storedSlots = (string[])slotIds.Clone();
            instanceToSlots[instanceId] = storedSlots;
            for (int index = 0; index < storedSlots.Length; index++)
                slotToInstanceId[storedSlots[index]] = instanceId;
            equipmentVersion++;
        }

        internal void ClearSlots(string instanceId)
        {
            if (!instanceToSlots.TryGetValue(instanceId, out string[] slots))
                throw new InvalidOperationException($"Item instance '{instanceId}' is not equipped.");

            for (int index = 0; index < slots.Length; index++)
                slotToInstanceId.Remove(slots[index]);
            instanceToSlots.Remove(instanceId);
            equipmentVersion++;
        }

        internal void RecordEquipped(ItemInstance item)
        {
            RecordFeedback(GameplayFeedbackEntryType.ItemEquipped, "Equipaste", item);
        }

        internal void RebindActorOwnedItems()
        {
            ResolveReferences();
            if (inventoryComponent == null)
                return;

            ItemOwnedStorageRegistry.Instance.BindEntries(inventoryComponent.Entries, inventoryComponent);
            ItemOwnedStorageRegistry.Instance.BindEntries(equipmentStorage.Entries, inventoryComponent);
        }

        internal void RecordUnequipped(ItemInstance item)
        {
            RecordFeedback(GameplayFeedbackEntryType.ItemUnequipped, "Desequipaste", item);
        }

        internal void CommitVisualState(EquipmentVisualCommitKind commitKind)
        {
            try
            {
                long nextRevision = committedVisualRevision + 1L;
                EquipmentVisualStateSnapshot snapshot = CaptureVisualSnapshot(nextRevision);
                committedVisualRevision = nextRevision;

                EventHandler<EquipmentVisualStateCommittedEventArgs> handlers = VisualStateCommitted;
                if (handlers == null)
                    return;

                var args = new EquipmentVisualStateCommittedEventArgs(commitKind, snapshot);
                Delegate[] subscribers = handlers.GetInvocationList();
                for (int index = 0; index < subscribers.Length; index++)
                {
                    try
                    {
                        ((EventHandler<EquipmentVisualStateCommittedEventArgs>)subscribers[index])(this, args);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception, this);
                    }
                }
            }
            catch (Exception exception)
            {
                // Presentation observers must never turn a confirmed gameplay commit into a rollback.
                Debug.LogException(exception, this);
            }
        }

        private EquipmentVisualStateSnapshot CaptureVisualSnapshot(long revision)
        {
            var items = new List<EquipmentVisualItemSnapshot>(equipmentStorage.Entries.Count);
            for (int index = 0; index < equipmentStorage.Entries.Count; index++)
            {
                ItemStorageEntry entry = equipmentStorage.Entries[index];
                if (entry == null || entry.Item == null || string.IsNullOrWhiteSpace(entry.Item.InstanceId))
                    continue;

                items.Add(new EquipmentVisualItemSnapshot(
                    entry.Item.InstanceId,
                    entry.Item.DefinitionId,
                    GetSlotsOccupiedBy(entry.Item.InstanceId)));
            }

            return new EquipmentVisualStateSnapshot(
                revision,
                equipmentVersion,
                equipmentStorage.Version,
                equipmentLayoutId,
                items);
        }

        private void RecordFeedback(GameplayFeedbackEntryType type, string verb, ItemInstance item)
        {
            if (item == null)
                return;
            ItemDefinition definition = ResolveItemDefinition(item.DefinitionId);
            string displayName = definition != null && definition.display != null && !string.IsNullOrWhiteSpace(definition.display.name)
                ? definition.display.name
                : item.DefinitionId;
            GameplayFeedbackLog.TryRecord(new GameplayFeedbackEntry(
                type,
                $"{verb} {displayName}.",
                actorId: name,
                actorDisplayName: name,
                itemId: item.DefinitionId,
                itemDisplayName: displayName,
                quantity: 1));
        }

        private void ResolveReferences()
        {
            if (inventoryComponent == null)
                inventoryComponent = GetComponent<InventoryComponent>();
            if (ownershipComponent == null)
                ownershipComponent = GetComponent<ActorItemOwnershipComponent>();
            ownershipComponent?.BindEquipment(this);
        }

        private static GameDatabase GetDatabase()
        {
            return GameDataManager.Instance != null && GameDataManager.Instance.IsReady
                ? GameDataManager.Instance.Database
                : null;
        }

        private static ItemDefinition ResolveItemDefinition(string definitionId)
        {
            GameDatabase database = GetDatabase();
            return database != null ? database.GetItem(definitionId) : null;
        }

        internal readonly struct EquipmentStateSnapshot
        {
            internal EquipmentStateSnapshot(
                Dictionary<string, string> slotToInstanceId,
                Dictionary<string, string[]> instanceToSlots,
                int version)
            {
                SlotToInstanceId = slotToInstanceId;
                InstanceToSlots = instanceToSlots;
                Version = version;
            }

            internal Dictionary<string, string> SlotToInstanceId { get; }
            internal Dictionary<string, string[]> InstanceToSlots { get; }
            internal int Version { get; }
        }
    }
}
