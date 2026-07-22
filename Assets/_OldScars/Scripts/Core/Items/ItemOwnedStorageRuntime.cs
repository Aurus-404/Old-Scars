using System.Collections.Generic;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;

namespace OldScars.Core.Items
{
    /// <summary>
    /// One spatial storage owned by one runtime ItemInstance.
    /// </summary>
    public sealed class ItemOwnedStorageRuntime : IGridStorageOwner, IGridStorageTransferEndpoint,
        IGridStorageIncomingGuard, ICarryWeightLimitedOwner
    {
        public const string NestedStorageRejectionMessage = "No podés guardar un contenedor dentro de otro contenedor.";

        private readonly ItemInstance containerItem;
        private readonly ItemStorageProfileDefinition profile;
        private readonly ItemStorage storage = new ItemStorage();
        private readonly GridStorageRuntime gridRuntime;

        internal ItemOwnedStorageRuntime(ItemInstance containerItem, ItemStorageProfileDefinition profile)
        {
            this.containerItem = containerItem;
            this.profile = profile;
            gridRuntime = new GridStorageRuntime(
                storage,
                ResolveDefinition,
                true,
                profile.width,
                profile.height,
                true);
        }

        public string ContainerInstanceId => containerItem.InstanceId;
        public string ProfileId => profile.id;
        public string GridStorageDisplayName => profile.display_name;
        public IReadOnlyList<ItemStorageEntry> GridStorageEntries => storage.Entries;
        public bool UsesGridLayout => gridRuntime.UsesGridLayout;
        public int GridWidth => gridRuntime.GridWidth;
        public int GridHeight => gridRuntime.GridHeight;
        public int ConfiguredGridWidth => profile.width;
        public int ConfiguredGridHeight => profile.height;
        public GridStorageInitializationState GridInitializationState => gridRuntime.InitializationState;
        public string GridInitializationError => gridRuntime.InitializationError;
        public int ContentVersion => gridRuntime.Backend.StorageVersion;
        public int LayoutVersion => gridRuntime.Backend.LayoutVersion;
        public bool HasCarryWeightLimit => ResolveCarryWeightOwner() != null;

        GridInventoryBackend IGridStorageTransferEndpoint.TransferBackend => gridRuntime.Backend;

        public bool TryGetEntryByInstanceId(string instanceId, out int index, out ItemStorageEntry entry)
        {
            index = storage.GetEntryIndexByInstanceId(instanceId);
            entry = index >= 0 ? storage.GetEntry(index) : null;
            return entry != null && entry.Item != null;
        }

        public bool TryGetGridPlacement(string instanceId, out GridPlacement placement)
        {
            return gridRuntime.TryGetPlacement(instanceId, out placement);
        }

        public bool TryGetGridFootprint(string definitionId, out GridFootprint footprint, out bool usedFallback)
        {
            return gridRuntime.TryResolveFootprint(definitionId, out footprint, out usedFallback);
        }

        public GridPlacementValidationResult PreviewGridPlacementMove(string instanceId, int x, int y, bool isRotated)
        {
            return gridRuntime.PreviewMovePlacement(instanceId, x, y, isRotated);
        }

        public InventoryMutationResult MoveGridPlacement(string instanceId, int x, int y, bool isRotated)
        {
            return gridRuntime.MovePlacement(instanceId, x, y, isRotated);
        }

        public bool IsInstanceEquipped(string instanceId)
        {
            return false;
        }

        public CarryWeightSnapshot GetCarryWeightSnapshot()
        {
            ICarryWeightLimitedOwner owner = ResolveCarryWeightOwner();
            return owner != null
                ? owner.GetCarryWeightSnapshot()
                : CarryWeightSnapshot.Invalid("The item-owned storage has no carry-limited actor root owner.");
        }

        public CarryWeightAcceptance EvaluateIncomingWeight(string definitionId, int quantity)
        {
            ICarryWeightLimitedOwner owner = ResolveCarryWeightOwner();
            return owner != null ? owner.EvaluateIncomingWeight(definitionId, quantity) : CarryWeightAcceptance.Unlimited();
        }

        public CarryWeightQuantityLimit EvaluateIncomingQuantityLimit(string definitionId, int requestedQuantity)
        {
            ICarryWeightLimitedOwner owner = ResolveCarryWeightOwner();
            return owner != null
                ? owner.EvaluateIncomingQuantityLimit(definitionId, requestedQuantity)
                : new CarryWeightQuantityLimit(true, requestedQuantity, requestedQuantity, 0d, 0d, double.PositiveInfinity, null);
        }

        public CarryWeightAcceptance EvaluateIncomingEntry(ItemStorageEntry entry, int quantity)
        {
            ICarryWeightLimitedOwner owner = ResolveCarryWeightOwner();
            return owner != null ? owner.EvaluateIncomingEntry(entry, quantity) : CarryWeightAcceptance.Unlimited();
        }

        public CarryWeightQuantityLimit EvaluateIncomingEntryQuantityLimit(ItemStorageEntry entry, int requestedQuantity)
        {
            ICarryWeightLimitedOwner owner = ResolveCarryWeightOwner();
            return owner != null
                ? owner.EvaluateIncomingEntryQuantityLimit(entry, requestedQuantity)
                : new CarryWeightQuantityLimit(true, requestedQuantity, requestedQuantity, 0d, 0d, double.PositiveInfinity, null);
        }

        bool IGridStorageIncomingGuard.CanAcceptIncoming(ItemStorageEntry entry, int quantity, out string reason)
        {
            reason = null;
            if (entry == null || entry.Item == null || quantity < 1)
            {
                reason = "Invalid item-owned storage transfer.";
                return false;
            }

            if (entry.Item.HasOwnedStorage)
            {
                reason = NestedStorageRejectionMessage;
                return false;
            }

            return true;
        }

        bool IGridStorageTransferEndpoint.CanTransferOut(GridStorageTransferContext context, out string reason)
        {
            reason = null;
            return true;
        }

        bool IGridStorageTransferEndpoint.CanTransferIn(GridStorageTransferContext context, out string reason)
        {
            reason = null;
            return true;
        }

        void IGridStorageTransferEndpoint.OnTransferCommittedOut(GridStorageTransferReceipt receipt, GridStorageTransferContext context)
        {
        }

        void IGridStorageTransferEndpoint.OnTransferCommittedIn(GridStorageTransferReceipt receipt, GridStorageTransferContext context)
        {
        }

        internal double GetContentWeightKg(out string error)
        {
            error = null;
            double total = 0d;
            for (int index = 0; index < storage.Entries.Count; index++)
            {
                ItemStorageEntry entry = storage.Entries[index];
                if (!ItemWeightResolver.TryGetEntryWeight(entry, entry != null ? entry.Quantity : 0, out double weight, out error))
                    return 0d;
                total += weight;
            }

            return total;
        }

        private ICarryWeightLimitedOwner ResolveCarryWeightOwner()
        {
            return ItemOwnedStorageRegistry.Instance.TryResolveRootOwner(ContainerInstanceId, out object rootOwner, out _)
                ? rootOwner as ICarryWeightLimitedOwner
                : null;
        }

        private static ItemDefinition ResolveDefinition(string definitionId)
        {
            if (GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
                return null;

            GameDatabase database = GameDataManager.Instance.Database;
            return database != null ? database.GetItem(definitionId) : null;
        }
    }
}
