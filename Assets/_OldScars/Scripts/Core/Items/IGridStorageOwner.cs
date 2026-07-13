using System.Collections.Generic;

namespace OldScars.Core.Items
{
    public enum GridStorageInitializationState
    {
        Disabled,
        Pending,
        Active,
        LinearFallback
    }

    public interface IGridStorageOwner
    {
        string GridStorageDisplayName { get; }
        IReadOnlyList<ItemStorageEntry> GridStorageEntries { get; }
        bool UsesGridLayout { get; }
        int GridWidth { get; }
        int GridHeight { get; }
        int ConfiguredGridWidth { get; }
        int ConfiguredGridHeight { get; }
        GridStorageInitializationState GridInitializationState { get; }
        string GridInitializationError { get; }

        bool TryGetEntryByInstanceId(string instanceId, out int index, out ItemStorageEntry entry);
        bool TryGetGridPlacement(string instanceId, out GridPlacement placement);
        bool TryGetGridFootprint(string definitionId, out GridFootprint footprint, out bool usedFallback);
        GridPlacementValidationResult PreviewGridPlacementMove(string instanceId, int x, int y, bool isRotated);
        InventoryMutationResult MoveGridPlacement(string instanceId, int x, int y, bool isRotated);
        bool IsInstanceEquipped(string instanceId);
    }
}
