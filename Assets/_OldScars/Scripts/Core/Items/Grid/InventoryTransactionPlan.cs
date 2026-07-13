using System;
using System.Collections.Generic;
using OldScars.Core.Data.Definitions;

namespace OldScars.Core.Items
{
    internal sealed class InventoryTransactionPlan
    {
        internal enum OperationKind
        {
            Add,
            Remove,
            Transfer,
            DirectedMerge
        }

        internal OperationKind Operation;
        internal ItemStorage SourceStorage;
        internal ItemStorage TargetStorage;
        internal GridInventoryLayout SourceLayout;
        internal GridInventoryLayout TargetLayout;
        internal Func<string, ItemDefinition> SourceDefinitionResolver;
        internal Func<string, ItemDefinition> TargetDefinitionResolver;
        internal ItemDefinition Definition;
        internal GridFootprint Footprint;
        internal string SourceInstanceId;
        internal string DestinationInstanceId;
        internal int RequestedQuantity;
        internal int Quantity;
        internal int MergeQuantity;
        internal int NewEntryCount;
        internal bool SourceEntryWillBeRemoved;
        internal bool UsedFallbackFootprint;
        internal int ExpectedSourceStorageVersion;
        internal int ExpectedTargetStorageVersion;
        internal int ExpectedSourceLayoutVersion;
        internal int ExpectedTargetLayoutVersion;
        internal bool UsesExactTargetPlacement;
        internal readonly List<GridInventoryLayout.ReservedRect> ReservedPlacements = new List<GridInventoryLayout.ReservedRect>();
    }

    internal readonly struct GridExactPlacementRequest
    {
        internal GridExactPlacementRequest(int x, int y, bool isRotated)
        {
            X = x;
            Y = y;
            IsRotated = isRotated;
        }

        internal int X { get; }
        internal int Y { get; }
        internal bool IsRotated { get; }
    }
}
