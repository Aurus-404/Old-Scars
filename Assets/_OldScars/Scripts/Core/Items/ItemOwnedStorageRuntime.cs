using System;
using System.Collections.Generic;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;

namespace OldScars.Core.Items
{
    /// <summary>
    /// One spatial storage owned by one runtime ItemInstance.
    /// </summary>
    public sealed class ItemOwnedStorageRuntime : IGridStorageOwner, IGridStorageTransferEndpoint,
        IGridStorageIncomingGuard
    {
        public const string NestedStorageRejectionMessage = "No podés guardar un contenedor dentro de otro contenedor.";

        private readonly ItemInstance containerItem;
        private readonly ItemStorageProfileDefinition profile;
        private readonly ItemStorage storage = new ItemStorage();
        private readonly GridStorageRuntime gridRuntime;

        internal ItemOwnedStorageRuntime(ItemInstance containerItem, ItemStorageProfileDefinition profile)
            : this(containerItem, profile, null, true)
        {
        }

        internal ItemOwnedStorageRuntime(
            ItemInstance containerItem,
            ItemStorageProfileDefinition profile,
            Func<string, ItemDefinition> definitionResolver,
            bool initializeLayoutImmediately)
        {
            this.containerItem = containerItem;
            this.profile = profile;
            gridRuntime = new GridStorageRuntime(
                storage,
                definitionResolver ?? ResolveDefinition,
                true,
                profile.width,
                profile.height,
                initializeLayoutImmediately);
        }

        public string ContainerInstanceId => containerItem.InstanceId;
        public string ProfileId => profile.id;
        public double? MaxContentWeightKg => profile.max_content_weight_kg;
        public float CarriedWeightMultiplier => profile.carried_weight_multiplier ?? 1f;
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

        GridInventoryBackend IGridStorageTransferEndpoint.TransferBackend => gridRuntime.Backend;

        internal GridInventoryBackend Backend => gridRuntime.Backend;
        internal bool IsEmpty => storage.IsEmpty;

        internal bool CompleteInitialContentLoad(out string error)
        {
            return gridRuntime.CompleteInitialContentLoad(out error);
        }

        internal bool CompleteInitialContentLoadExact(
            IReadOnlyList<ItemStorageEntry> entries,
            IReadOnlyList<GridPlacement> placements,
            out string error)
        {
            return gridRuntime.CompleteInitialContentLoadExact(entries, placements, out error);
        }

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

            if (MaxContentWeightKg.HasValue)
            {
                double currentKg = GetContentWeightKg(out string weightError);
                if (weightError != null)
                {
                    reason = weightError;
                    return false;
                }
                if (!ItemWeightResolver.TryGetEntryWeight(entry, quantity, out double incomingKg, out weightError))
                {
                    reason = weightError;
                    return false;
                }

                double maximumKg = MaxContentWeightKg.Value;
                if (currentKg + incomingKg > maximumKg + 0.000001d)
                {
                    reason = $"Capacidad del contenedor: {currentKg:0.###} / {maximumKg:0.###} kg; ingreso: {incomingKg:0.###} kg. Retirá contenido antes de ingresar esa cantidad.";
                    return false;
                }
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

        private static ItemDefinition ResolveDefinition(string definitionId)
        {
            if (GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
                return null;

            GameDatabase database = GameDataManager.Instance.Database;
            return database != null ? database.GetItem(definitionId) : null;
        }
    }
}
