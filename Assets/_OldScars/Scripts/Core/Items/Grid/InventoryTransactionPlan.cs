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
            Transfer
        }

        internal OperationKind Operation;
        internal ItemStorage SourceStorage;
        internal ItemStorage TargetStorage;
        internal GridInventoryLayout SourceLayout;
        internal GridInventoryLayout TargetLayout;
        internal ItemDefinition Definition;
        internal GridFootprint Footprint;
        internal string SourceInstanceId;
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
        internal readonly List<GridInventoryLayout.ReservedRect> ReservedPlacements = new List<GridInventoryLayout.ReservedRect>();
    }
}
