using System;
using System.Globalization;

namespace OldScars.Core.World
{
    public enum WorldSizePreset
    {
        Small,
        Medium,
        Large,
        Huge
    }

    /// <summary>
    /// Immutable generation-relevant settings for Macro World Plan V1. The
    /// resolved values are durable inputs so later tuning of preset defaults
    /// cannot silently reinterpret an existing world.
    /// </summary>
    public sealed class WorldGenerationSettings
    {
        private WorldGenerationSettings(
            WorldSizePreset worldSizePreset,
            int resolvedSectorCount,
            long resolvedWorldWidth,
            long resolvedWorldHeight,
            long resolvedMinimumSectorSpacing)
        {
            WorldSizePreset = worldSizePreset;
            ResolvedSectorCount = resolvedSectorCount;
            ResolvedWorldWidth = resolvedWorldWidth;
            ResolvedWorldHeight = resolvedWorldHeight;
            ResolvedMinimumSectorSpacing = resolvedMinimumSectorSpacing;
        }

        public WorldSizePreset WorldSizePreset { get; }
        public int ResolvedSectorCount { get; }
        public long ResolvedWorldWidth { get; }
        public long ResolvedWorldHeight { get; }
        public long ResolvedMinimumSectorSpacing { get; }

        internal string DeterministicKey =>
            "size_" + ToCanonical(WorldSizePreset) + "_" +
            ResolvedSectorCount.ToString(CultureInfo.InvariantCulture) + "_" +
            ResolvedWorldWidth.ToString(CultureInfo.InvariantCulture) + "_" +
            ResolvedWorldHeight.ToString(CultureInfo.InvariantCulture) + "_" +
            ResolvedMinimumSectorSpacing.ToString(CultureInfo.InvariantCulture);

        public static WorldGenerationSettings ResolvePreset(WorldSizePreset preset)
        {
            switch (preset)
            {
                case WorldSizePreset.Small:
                    return CreateValidated(preset, 32, 8000, 8000, 500);
                case WorldSizePreset.Medium:
                    return CreateValidated(preset, 72, 14000, 14000, 600);
                case WorldSizePreset.Large:
                    return CreateValidated(preset, 128, 22000, 22000, 700);
                case WorldSizePreset.Huge:
                    return CreateValidated(preset, 224, 34000, 34000, 800);
                default:
                    throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown WorldSizePreset.");
            }
        }

        public static bool TryCreateResolved(
            WorldSizePreset preset,
            int resolvedSectorCount,
            long resolvedWorldWidth,
            long resolvedWorldHeight,
            long resolvedMinimumSectorSpacing,
            out WorldGenerationSettings settings,
            out string error)
        {
            settings = null;
            error = null;
            if (!Enum.IsDefined(typeof(WorldSizePreset), preset))
            {
                error = $"Unknown WorldSizePreset value '{(int)preset}'";
                return false;
            }
            if (resolvedSectorCount < 1)
            {
                error = "Resolved sector count must be positive";
                return false;
            }
            if (resolvedWorldWidth < 2 || resolvedWorldHeight < 2)
            {
                error = "Resolved finite world width and height must both be at least 2 logical units";
                return false;
            }
            if (resolvedMinimumSectorSpacing < 1)
            {
                error = "Resolved minimum sector spacing must be positive";
                return false;
            }
            if (resolvedMinimumSectorSpacing >= resolvedWorldWidth / 2 ||
                resolvedMinimumSectorSpacing >= resolvedWorldHeight / 2)
            {
                error = "Resolved minimum sector spacing must be smaller than half of each world extent";
                return false;
            }

            settings = new WorldGenerationSettings(
                preset,
                resolvedSectorCount,
                resolvedWorldWidth,
                resolvedWorldHeight,
                resolvedMinimumSectorSpacing);
            return true;
        }

        public static string ToCanonical(WorldSizePreset preset)
        {
            switch (preset)
            {
                case WorldSizePreset.Small: return "small";
                case WorldSizePreset.Medium: return "medium";
                case WorldSizePreset.Large: return "large";
                case WorldSizePreset.Huge: return "huge";
                default: throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown WorldSizePreset.");
            }
        }

        public static bool TryParsePreset(string raw, out WorldSizePreset preset, out string error)
        {
            error = null;
            switch (raw)
            {
                case "small": preset = WorldSizePreset.Small; return true;
                case "medium": preset = WorldSizePreset.Medium; return true;
                case "large": preset = WorldSizePreset.Large; return true;
                case "huge": preset = WorldSizePreset.Huge; return true;
                default:
                    preset = default;
                    error = "expected one of: small, medium, large, huge";
                    return false;
            }
        }

        private static WorldGenerationSettings CreateValidated(
            WorldSizePreset preset,
            int sectorCount,
            long width,
            long height,
            long minimumSpacing)
        {
            if (!TryCreateResolved(
                    preset, sectorCount, width, height, minimumSpacing,
                    out WorldGenerationSettings settings, out string error))
            {
                throw new InvalidOperationException("Built-in world-size tuning is invalid: " + error + ".");
            }
            return settings;
        }
    }
}
