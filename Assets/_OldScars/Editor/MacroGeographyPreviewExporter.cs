using System;
using System.IO;
using OldScars.Core.World;
using UnityEngine;

namespace OldScars.EditorTools
{
    /// <summary>
    /// Diagnostic-only raster preview. The persisted logical plan remains the
    /// authority; this exporter only samples it through its public query API.
    /// </summary>
    public static class MacroGeographyPreviewExporter
    {
        private const string OutputEnvironment = "OLD_SCARS_MACRO_GEOGRAPHY_PREVIEW_PATH";
        private const long GoldenSeed = 8675309123456789L;

        public static void ExportGoldenPreview()
        {
            string outputPath = Environment.GetEnvironmentVariable(OutputEnvironment);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = Path.Combine(
                    Path.GetTempPath(), "OldScars_MacroGeography_Golden_Large.png");
            }

            var context = new WorldGenerationContext(
                new WorldSeed(GoldenSeed),
                GeneratorVersion.Parse(WorldSessionBootstrap.CurrentGeneratorVersion));
            if (!MacroWorldPlanGenerator.TryGenerate(
                    context,
                    WorldGenerationSettings.ResolvePreset(WorldSizePreset.Large),
                    out MacroWorldPlan plan,
                    out string planError))
            {
                throw new InvalidOperationException("Preview MacroWorldPlan generation failed: " + planError);
            }
            if (!MacroGeographyGenerator.TryGenerate(
                    context, plan, out MacroGeographyPlan geography, out string geographyError))
            {
                throw new InvalidOperationException("Preview macro geography generation failed: " + geographyError);
            }

            Export(plan, geography, outputPath, 512, 512, true);
            Debug.Log(
                "Macro Geography Preview Export: PASS\n" +
                "Path: " + outputPath + "\n" +
                "Seed: " + GoldenSeed + "\n" +
                "Size: Large\n" +
                "GeographyHash: " + geography.CanonicalHash);
        }

        public static void Export(
            MacroWorldPlan plan,
            MacroGeographyPlan geography,
            string outputPath,
            int panelWidth,
            int panelHeight,
            bool overlaySectors)
        {
            if (plan == null || geography == null)
                throw new ArgumentNullException(plan == null ? nameof(plan) : nameof(geography));
            if (plan.WorldBounds != geography.WorldBounds)
                throw new ArgumentException("Preview plan and geography bounds must match.");
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Preview output path is required.", nameof(outputPath));
            if (panelWidth < 64 || panelHeight < 64)
                throw new ArgumentOutOfRangeException(nameof(panelWidth), "Preview panels must be at least 64x64.");

            var texture = new Texture2D(panelWidth * 2, panelHeight, TextureFormat.RGB24, false, true);
            texture.name = "MacroGeographyPreview";
            try
            {
                for (int y = 0; y < panelHeight; y++)
                {
                    for (int x = 0; x < panelWidth; x++)
                    {
                        MacroPoint2D position = PixelToMacro(
                            plan.WorldBounds, x, y, panelWidth, panelHeight);
                        MacroGeographySample sample = default;
                        if (!geography.TrySampleAt(position, out sample))
                            throw new InvalidOperationException("Preview sampled outside finite world bounds.");

                        texture.SetPixel(x, y, ElevationColor(sample.Elevation));
                        texture.SetPixel(panelWidth + x, y, LandformColor(sample.Landform));
                    }
                }

                if (overlaySectors)
                    DrawSectorOverlay(texture, plan, panelWidth, panelHeight);

                texture.Apply(false, false);
                byte[] png = texture.EncodeToPNG();
                string directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllBytes(outputPath, png);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static MacroPoint2D PixelToMacro(
            FiniteMacroWorldBounds bounds,
            int x,
            int y,
            int width,
            int height)
        {
            long macroX = bounds.MinX + (bounds.Width - 1) * x / (width - 1L);
            long macroY = bounds.MinY + (bounds.Height - 1) * y / (height - 1L);
            return new MacroPoint2D(macroX, macroY);
        }

        private static Color ElevationColor(ushort elevation)
        {
            float value = elevation / 65535f;
            if (value < 0.35f)
                return Color.Lerp(new Color(0.08f, 0.16f, 0.10f), new Color(0.30f, 0.47f, 0.22f), value / 0.35f);
            if (value < 0.65f)
                return Color.Lerp(new Color(0.30f, 0.47f, 0.22f), new Color(0.48f, 0.33f, 0.20f), (value - 0.35f) / 0.30f);
            return Color.Lerp(new Color(0.48f, 0.33f, 0.20f), Color.white, (value - 0.65f) / 0.35f);
        }

        private static Color LandformColor(MacroLandform landform)
        {
            switch (landform)
            {
                case MacroLandform.Plains: return new Color(0.34f, 0.68f, 0.31f);
                case MacroLandform.RollingHills: return new Color(0.72f, 0.68f, 0.28f);
                case MacroLandform.Highlands: return new Color(0.63f, 0.39f, 0.22f);
                case MacroLandform.Mountains: return new Color(0.72f, 0.76f, 0.82f);
                default: return Color.magenta;
            }
        }

        private static void DrawSectorOverlay(
            Texture2D texture,
            MacroWorldPlan plan,
            int panelWidth,
            int panelHeight)
        {
            for (int index = 0; index < plan.SectorPlacements.Count; index++)
            {
                MacroPoint2D position = plan.SectorPlacements[index].Position;
                int x = (int)((position.X - plan.WorldBounds.MinX) * (panelWidth - 1L) /
                              (plan.WorldBounds.Width - 1));
                int y = (int)((position.Y - plan.WorldBounds.MinY) * (panelHeight - 1L) /
                              (plan.WorldBounds.Height - 1));
                DrawMarker(texture, x, y, Color.black, panelWidth, panelHeight);
                DrawMarker(texture, panelWidth + x, y, Color.white, panelWidth * 2, panelHeight);
            }
        }

        private static void DrawMarker(
            Texture2D texture,
            int centerX,
            int centerY,
            Color color,
            int maximumX,
            int maximumY)
        {
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    int x = centerX + offsetX;
                    int y = centerY + offsetY;
                    if (x >= 0 && x < maximumX && y >= 0 && y < maximumY)
                        texture.SetPixel(x, y, color);
                }
            }
        }
    }
}
