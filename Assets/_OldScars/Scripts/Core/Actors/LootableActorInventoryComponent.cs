using System.Collections.Generic;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Feedback;
using OldScars.Core.Interactions;
using OldScars.Core.Items;
using UnityEngine;

namespace OldScars.Core.Actors
{
    public readonly struct LootableActorOwnedStorage
    {
        public LootableActorOwnedStorage(
            ItemStorageEntry containerEntry,
            ItemOwnedStorageRuntime storage,
            IReadOnlyList<string> occupiedSlots)
        {
            ContainerEntry = containerEntry;
            Storage = storage;
            OccupiedSlots = occupiedSlots;
        }

        public ItemStorageEntry ContainerEntry { get; }
        public ItemOwnedStorageRuntime Storage { get; }
        public IReadOnlyList<string> OccupiedSlots { get; }
    }

    public sealed class LootableActorInventoryComponent : MonoBehaviour, IItemStorageDebugSource, IGridStorageTransferEndpoint
    {
        [SerializeField] private InventoryComponent inventory;
        [SerializeField] private ActorEquipmentComponent equipment;
        [SerializeField] private ActorItemOwnershipComponent ownership;
        [SerializeField] private WorldObjectTags worldObjectTags;

        private readonly List<LootableActorOwnedStorage> equippedOwnedStorages =
            new List<LootableActorOwnedStorage>();

        public InventoryComponent Inventory
        {
            get
            {
                ResolveReferences();
                return inventory;
            }
        }

        public ActorEquipmentComponent Equipment
        {
            get
            {
                ResolveReferences();
                return equipment;
            }
        }

        public ActorItemOwnershipComponent Ownership
        {
            get
            {
                ResolveReferences();
                return ownership;
            }
        }

        public bool HasInventoryItems => Inventory != null && !Inventory.IsEmpty;
        public bool HasEquipmentItems => Equipment != null && !Equipment.IsEmpty;
        public bool HasStoredItems => HasLootableContent;
        public bool HasLootableContent
        {
            get
            {
                if (HasInventoryItems || HasEquipmentItems)
                    return true;

                IReadOnlyList<LootableActorOwnedStorage> storages = GetEquippedOwnedStorages();
                for (int index = 0; index < storages.Count; index++)
                {
                    if (storages[index].Storage != null && storages[index].Storage.GridStorageEntries.Count > 0)
                        return true;
                }
                return false;
            }
        }
        public IReadOnlyList<ItemStorageEntry> StorageEntries => inventory != null ? inventory.GetStorageEntries() : EmptyEntries;
        public string GridStorageDisplayName => name;
        public IReadOnlyList<ItemStorageEntry> GridStorageEntries => StorageEntries;
        public bool UsesGridLayout => inventory != null && inventory.UsesGridLayout;
        public int GridWidth => inventory != null ? inventory.GridWidth : 0;
        public int GridHeight => inventory != null ? inventory.GridHeight : 0;
        public int ConfiguredGridWidth => inventory != null ? inventory.ConfiguredGridWidth : 0;
        public int ConfiguredGridHeight => inventory != null ? inventory.ConfiguredGridHeight : 0;
        public GridStorageInitializationState GridInitializationState => inventory != null
            ? inventory.GridInitializationState
            : GridStorageInitializationState.Disabled;
        public string GridInitializationError => inventory != null ? inventory.GridInitializationError : "InventoryComponent is missing.";

        GridInventoryBackend IGridStorageTransferEndpoint.TransferBackend
        {
            get
            {
                ResolveReferences();
                return inventory != null
                    ? ((IGridStorageTransferEndpoint)inventory).TransferBackend
                    : null;
            }
        }

        private static readonly ItemStorageEntry[] EmptyEntries = new ItemStorageEntry[0];

        private void Awake()
        {
            ResolveReferences();
            SyncLootableTag();
        }

        private void LateUpdate()
        {
            SyncLootableTag();
        }

        public string GetStorageDebugTitle(WorldObjectTags target)
        {
            return $"Cadaver - {GetActorDisplayName(target)}";
        }

        public string GetActorDisplayName(WorldObjectTags target = null)
        {
            WorldObjectTags resolvedTarget = target != null ? target : worldObjectTags;
            string targetName = resolvedTarget != null ? resolvedTarget.name : name;
            WorldObjectDebugInfo debugInfo = resolvedTarget != null
                ? resolvedTarget.GetComponent<WorldObjectDebugInfo>()
                : GetComponent<WorldObjectDebugInfo>();
            return debugInfo != null
                ? debugInfo.GetDisplayNameOrFallback(targetName, resolvedTarget)
                : targetName;
        }

        public IReadOnlyList<LootableActorOwnedStorage> GetEquippedOwnedStorages()
        {
            ResolveReferences();
            equippedOwnedStorages.Clear();
            if (equipment == null)
                return equippedOwnedStorages;

            IReadOnlyList<ItemStorageEntry> entries = equipment.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                ItemStorageEntry entry = entries[index];
                ItemInstance item = entry?.Item;
                if (item == null || !item.HasOwnedStorage ||
                    !ItemOwnedStorageRegistry.Instance.TryResolveOwnedStorage(
                        item.InstanceId,
                        out ItemOwnedStorageRuntime storage))
                {
                    continue;
                }

                equippedOwnedStorages.Add(new LootableActorOwnedStorage(
                    entry,
                    storage,
                    equipment.GetSlotsOccupiedBy(item.InstanceId)));
            }
            return equippedOwnedStorages;
        }

        public bool TryGetEquippedOwnedStorage(
            string containerInstanceId,
            out LootableActorOwnedStorage option)
        {
            IReadOnlyList<LootableActorOwnedStorage> options = GetEquippedOwnedStorages();
            for (int index = 0; index < options.Count; index++)
            {
                LootableActorOwnedStorage candidate = options[index];
                if (candidate.Storage != null &&
                    candidate.Storage.ContainerInstanceId == containerInstanceId)
                {
                    option = candidate;
                    return true;
                }
            }

            option = default;
            return false;
        }

        public void RefreshLootableState()
        {
            SyncLootableTag();
        }

        public bool CanOpenStorage(out string reason)
        {
            return CanAccessActorInventory(out reason);
        }

        public int TakeItem(int storageIndex, int quantity, InventoryComponent targetInventory, DebugActionExecutionContext executionContext, ActionDefinition action, out string message)
        {
            ResolveReferences();
            message = null;

            if (quantity < 1)
            {
                message = "Cantidad invalida.";
                return 0;
            }

            if (!CanAccessActorInventory(out string accessReason))
            {
                message = accessReason;
                return 0;
            }

            if (inventory == null)
            {
                message = "El actor muerto no tiene InventoryComponent.";
                return 0;
            }

            if (targetInventory == null)
            {
                message = "El actor no tiene inventario v0 configurado.";
                return 0;
            }

            ItemStorageEntry entry = inventory.GetEntry(storageIndex);
            if (entry == null || entry.Item == null)
            {
                message = "Slot de actor invalido.";
                return 0;
            }

            string definitionId = entry.DefinitionId;
            int requestedQuantity = Mathf.Min(quantity, entry.Quantity);
            InventoryMutationResult transferResult = GridStorageTransferService.TransferQuantityAuto(
                this,
                targetInventory,
                entry.Item.InstanceId,
                requestedQuantity,
                true,
                new GridStorageTransferContext(executionContext, action));
            if (!transferResult.Success)
            {
                message = transferResult.Message ?? "No se pudo transferir contenido.";
                return 0;
            }

            int transferredQuantity = transferResult.AffectedQuantity;

            message = $"Tomaste {GetItemDisplayName(definitionId)} x{transferredQuantity}.";
            return transferredQuantity;
        }

        public int DepositItem(int inventoryIndex, int quantity, InventoryComponent sourceInventory, DebugActionExecutionContext executionContext, ActionDefinition action, out string message)
        {
            ResolveReferences();
            message = null;

            if (quantity < 1)
            {
                message = "Cantidad invalida.";
                return 0;
            }

            if (!CanDepositIntoActorInventory(out string accessReason))
            {
                message = accessReason;
                return 0;
            }

            if (inventory == null)
            {
                message = "El actor muerto no tiene InventoryComponent.";
                return 0;
            }

            if (sourceInventory == null)
            {
                message = "El actor no tiene inventario v0 configurado.";
                return 0;
            }

            if (ReferenceEquals(sourceInventory, inventory))
            {
                message = "No se puede depositar en el mismo inventario.";
                return 0;
            }

            ItemStorageEntry entry = sourceInventory.GetEntry(inventoryIndex);
            if (entry == null || entry.Item == null)
            {
                message = "Slot de inventario invalido.";
                return 0;
            }

            string definitionId = entry.DefinitionId;
            int requestedQuantity = Mathf.Min(quantity, entry.Quantity);
            InventoryMutationResult transferResult = GridStorageTransferService.TransferQuantityAuto(
                sourceInventory,
                this,
                entry.Item.InstanceId,
                requestedQuantity,
                true,
                new GridStorageTransferContext(executionContext, action));
            if (!transferResult.Success)
            {
                message = transferResult.Message ?? "No se pudo depositar contenido.";
                return 0;
            }

            int transferredQuantity = transferResult.AffectedQuantity;
            message = $"Depositaste {GetItemDisplayName(definitionId)} x{transferredQuantity}.";
            return transferredQuantity;
        }

        public bool TryGetEntryByInstanceId(string instanceId, out int index, out ItemStorageEntry entry)
        {
            ResolveReferences();
            if (inventory != null)
                return inventory.TryGetEntryByInstanceId(instanceId, out index, out entry);

            index = -1;
            entry = null;
            return false;
        }

        public bool TryGetGridPlacement(string instanceId, out GridPlacement placement)
        {
            ResolveReferences();
            placement = null;
            return inventory != null && inventory.TryGetGridPlacement(instanceId, out placement);
        }

        public bool TryGetGridFootprint(string definitionId, out GridFootprint footprint, out bool usedFallback)
        {
            ResolveReferences();
            footprint = null;
            usedFallback = false;
            return inventory != null && inventory.TryGetGridFootprint(definitionId, out footprint, out usedFallback);
        }

        public GridPlacementValidationResult PreviewGridPlacementMove(
            string instanceId,
            int x,
            int y,
            bool isRotated)
        {
            ResolveReferences();
            return inventory != null
                ? inventory.PreviewGridPlacementMove(instanceId, x, y, isRotated)
                : GridPlacementValidationResult.Invalid(
                    InventoryMutationResult.MutationFailure.GridLayoutUnavailable,
                    "Actor InventoryComponent is missing.");
        }

        public InventoryMutationResult MoveGridPlacement(string instanceId, int x, int y, bool isRotated)
        {
            ResolveReferences();
            return inventory != null
                ? inventory.MoveGridPlacement(instanceId, x, y, isRotated)
                : InventoryMutationResult.Rejected(
                    InventoryMutationResult.MutationFailure.GridLayoutUnavailable,
                    "Actor InventoryComponent is missing.",
                    0,
                    instanceId);
        }

        public bool IsInstanceEquipped(string instanceId)
        {
            ResolveReferences();
            return inventory != null && inventory.IsInstanceEquipped(instanceId);
        }

        private bool CanAccessActorInventory(out string reason)
        {
            ResolveReferences();
            reason = null;

            if (worldObjectTags == null)
            {
                reason = "Error: actor sin tags de mundo.";
                return false;
            }

            if (!worldObjectTags.HasTag(ActorHealthComponent.DeadActorTag))
            {
                reason = "Este actor no esta muerto.";
                return false;
            }

            if (!worldObjectTags.HasTag(ActorHealthComponent.LootableActorTag))
            {
                reason = "Este cuerpo ya no se puede saquear.";
                return false;
            }

            if (!HasLootableContent)
            {
                reason = "No queda contenido en este cuerpo.";
                return false;
            }

            return true;
        }

        private bool CanDepositIntoActorInventory(out string reason)
        {
            ResolveReferences();
            reason = null;

            if (worldObjectTags == null)
            {
                reason = "Error: actor sin tags de mundo.";
                return false;
            }

            if (!worldObjectTags.HasTag(ActorHealthComponent.DeadActorTag))
            {
                reason = "Este actor no esta muerto.";
                return false;
            }

            return true;
        }

        private void SyncLootableTag()
        {
            ResolveReferences();
            if (worldObjectTags == null)
                return;

            if (!worldObjectTags.HasTag(ActorHealthComponent.DeadActorTag))
                return;

            if (!HasLootableContent)
                worldObjectTags.RemoveTag(ActorHealthComponent.LootableActorTag);
            else
                worldObjectTags.AddTag(ActorHealthComponent.LootableActorTag);
        }

        private void ResolveReferences()
        {
            if (inventory == null)
                inventory = GetComponent<InventoryComponent>();

            if (equipment == null)
                equipment = GetComponent<ActorEquipmentComponent>();

            if (ownership == null)
                ownership = GetComponent<ActorItemOwnershipComponent>();

            if (worldObjectTags == null)
                worldObjectTags = GetComponent<WorldObjectTags>();
        }

        bool IGridStorageTransferEndpoint.CanTransferOut(GridStorageTransferContext context, out string reason)
        {
            return CanAccessActorInventory(out reason);
        }

        bool IGridStorageTransferEndpoint.CanTransferIn(GridStorageTransferContext context, out string reason)
        {
            return CanDepositIntoActorInventory(out reason);
        }

        void IGridStorageTransferEndpoint.OnTransferCommittedOut(
            GridStorageTransferReceipt receipt,
            GridStorageTransferContext context)
        {
            if (receipt.SourceWasRemoved && inventory != null && inventory.IsInstanceEquipped(receipt.SourceInstanceId))
                inventory.UnequipRightHand();

            RecordLootReceived(
                receipt.DefinitionId,
                receipt.TransferredQuantity,
                context.ExecutionContext,
                context.Action);
            SyncLootableTag();
        }

        void IGridStorageTransferEndpoint.OnTransferCommittedIn(
            GridStorageTransferReceipt receipt,
            GridStorageTransferContext context)
        {
            SyncLootableTag();
        }

        private static void RecordLootReceived(string definitionId, int quantity, DebugActionExecutionContext executionContext, ActionDefinition action)
        {
            string displayName = GetItemDisplayName(definitionId);
            GameplayFeedbackLog.TryRecord(new GameplayFeedbackEntry(
                GameplayFeedbackEntryType.LootReceived,
                $"Encontraste {displayName} x{quantity}.",
                actorId: GetActorName(executionContext.ActorContext),
                actorDisplayName: GetActorName(executionContext.ActorContext),
                targetId: GetTargetName(executionContext.Target),
                targetDisplayName: GetTargetDisplayName(executionContext.Target),
                itemId: definitionId,
                itemDisplayName: displayName,
                actionId: action != null ? action.id : null,
                actionDisplayName: GetActionDisplayName(action),
                quantity: quantity));
        }

        private static string GetItemDisplayName(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
                return "(none)";

            if (GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
                return definitionId;

            GameDatabase database = GameDataManager.Instance.Database;
            ItemDefinition definition = database != null ? database.GetItem(definitionId) : null;
            if (definition == null || definition.display == null || string.IsNullOrWhiteSpace(definition.display.name))
                return definitionId;

            return definition.display.name;
        }

        private static string GetActorName(ActorInteractionContext actorContext)
        {
            return actorContext != null ? actorContext.name : null;
        }

        private static string GetTargetName(WorldObjectTags targetTags)
        {
            return targetTags != null ? targetTags.name : null;
        }

        private static string GetTargetDisplayName(WorldObjectTags targetTags)
        {
            if (targetTags == null)
                return null;

            WorldObjectDebugInfo debugInfo = targetTags.GetComponent<WorldObjectDebugInfo>();
            return debugInfo != null ? debugInfo.GetDisplayNameOrFallback(targetTags.name) : targetTags.name;
        }

        private static string GetActionDisplayName(ActionDefinition action)
        {
            if (action == null)
                return null;

            return action.display != null && !string.IsNullOrWhiteSpace(action.display.name)
                ? action.display.name
                : action.id;
        }
    }
}
