using OldScars.Core.Data.Definitions;

namespace OldScars.Core.Items
{
    public sealed class GridFootprint
    {
        public static GridFootprint OneByOne { get; } = new GridFootprint(1, 1);

        public int Width { get; }
        public int Height { get; }

        public GridFootprint(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int GetWidth(bool rotated)
        {
            return rotated ? Height : Width;
        }

        public int GetHeight(bool rotated)
        {
            return rotated ? Width : Height;
        }

        public static bool TryResolve(ItemDefinition definition, out GridFootprint footprint, out bool usedFallback, out string error)
        {
            footprint = null;
            usedFallback = false;
            error = null;

            if (definition == null)
            {
                error = "Item definition was not found.";
                return false;
            }

            if (!definition.inventory.HasValue || !definition.inventory.Value.footprint.HasValue)
            {
                footprint = OneByOne;
                usedFallback = true;
                return true;
            }

            ItemInventoryMetadata inventory = definition.inventory.Value;
            ItemFootprintDefinition data = inventory.footprint.Value;
            if (data.width <= 0 || data.height <= 0)
            {
                error = $"Invalid footprint {data.width}x{data.height} for item '{definition.id}'.";
                return false;
            }

            footprint = new GridFootprint(data.width, data.height);
            return true;
        }
    }
}
