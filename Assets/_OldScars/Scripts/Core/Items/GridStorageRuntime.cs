using System;
using System.Collections.Generic;
using OldScars.Core.Data.Definitions;

namespace OldScars.Core.Items
{
    public sealed class GridStorageRuntime
    {
        private readonly GridInventoryBackend backend;
        private bool useGridLayout;
        private int configuredGridWidth;
        private int configuredGridHeight;

        public bool UsesGridLayout => backend.UsesGridLayout;
        public int GridWidth => backend.GridWidth;
        public int GridHeight => backend.GridHeight;
        public int ConfiguredGridWidth => configuredGridWidth;
        public int ConfiguredGridHeight => configuredGridHeight;
        public GridStorageInitializationState InitializationState { get; private set; }
        public string InitializationError { get; private set; }

        internal GridInventoryBackend Backend => backend;

        public GridStorageRuntime(
            ItemStorage storage,
            Func<string, ItemDefinition> definitionResolver,
            bool useGridLayout,
            int gridWidth,
            int gridHeight,
            bool initializeImmediately)
        {
            backend = new GridInventoryBackend(storage, definitionResolver);
            Configure(useGridLayout, gridWidth, gridHeight);

            if (initializeImmediately)
                TryInitializeLayout(out _);
        }

        public void Configure(bool enabled, int width, int height)
        {
            useGridLayout = enabled;
            configuredGridWidth = width;
            configuredGridHeight = height;
            InitializationError = null;
            InitializationState = enabled
                ? GridStorageInitializationState.Pending
                : GridStorageInitializationState.Disabled;

            if (!enabled)
                backend.DisableLayout();
        }

        public void BeginInitialContentLoad()
        {
            if (!useGridLayout)
            {
                InitializationState = GridStorageInitializationState.Disabled;
                return;
            }

            backend.DisableLayout();
            InitializationError = null;
            InitializationState = GridStorageInitializationState.Pending;
        }

        public bool CompleteInitialContentLoad(out string error)
        {
            return TryInitializeLayout(out error);
        }

        internal bool CompleteInitialContentLoadExact(
            IReadOnlyList<ItemStorageEntry> entries,
            IReadOnlyList<GridPlacement> placements,
            out string error)
        {
            InitializationError = null;
            InitializationState = useGridLayout
                ? GridStorageInitializationState.Pending
                : GridStorageInitializationState.Disabled;
            if (backend.TryReplaceWithExactEntries(
                    entries,
                    useGridLayout,
                    useGridLayout ? configuredGridWidth : 0,
                    useGridLayout ? configuredGridHeight : 0,
                    placements,
                    out error))
            {
                InitializationState = useGridLayout
                    ? GridStorageInitializationState.Active
                    : GridStorageInitializationState.Disabled;
                return true;
            }

            backend.DisableLayout();
            InitializationError = error;
            InitializationState = useGridLayout
                ? GridStorageInitializationState.LinearFallback
                : GridStorageInitializationState.Disabled;
            return false;
        }

        public bool TryInitializeLayout(out string error)
        {
            error = null;
            InitializationError = null;

            if (!useGridLayout)
            {
                backend.DisableLayout();
                InitializationState = GridStorageInitializationState.Disabled;
                return true;
            }

            InitializationState = GridStorageInitializationState.Pending;
            if (backend.TryEnableLayout(configuredGridWidth, configuredGridHeight, out error))
            {
                InitializationState = GridStorageInitializationState.Active;
                return true;
            }

            backend.DisableLayout();
            InitializationError = error;
            InitializationState = GridStorageInitializationState.LinearFallback;
            return false;
        }

        public bool TryGetPlacement(string instanceId, out GridPlacement placement)
        {
            return backend.TryGetPlacement(instanceId, out placement);
        }

        public bool TryResolveFootprint(string definitionId, out GridFootprint footprint, out bool usedFallback)
        {
            return backend.TryResolveFootprint(definitionId, out footprint, out usedFallback, out _);
        }

        public GridPlacementValidationResult PreviewMovePlacement(string instanceId, int x, int y, bool isRotated)
        {
            return backend.PreviewMovePlacement(instanceId, x, y, isRotated);
        }

        public InventoryMutationResult MovePlacement(string instanceId, int x, int y, bool isRotated)
        {
            return backend.MovePlacement(instanceId, x, y, isRotated);
        }
    }
}
