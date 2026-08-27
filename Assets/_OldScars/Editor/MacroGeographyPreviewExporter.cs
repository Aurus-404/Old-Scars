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
            if (!MacroWaterGenerator.TryGenerate(
                    plan, geography, LandCoveragePreset.High,
                    out MacroWaterPlan water, out string waterError))
                throw new InvalidOperationException("Preview Macro Water generation failed: " + waterError);
            if (!MacroClimateGenerator.TryGenerate(
                    context, plan, geography, water,
                    out MacroClimatePlan climate, out string climateError))
                throw new InvalidOperationException("Preview Macro Climate generation failed: " + climateError);
            if (!WorldGameplayQualityAnalyzer.TryAnalyze(
                    plan, geography, water,
                    out WorldGameplayQualityAnalysis quality, out string qualityError))
                throw new InvalidOperationException("Preview gameplay-quality analysis failed: " + qualityError);
            if (!WorldStarterSectorSelector.TrySelect(
                    quality, out SectorId starter, out string starterError))
                throw new InvalidOperationException("Preview starter selection failed: " + starterError);
            if (!MacroHumanGeographyGenerator.TryGenerate(
                    context, plan, geography, water, quality, starter,
                    out MacroHumanGeographyPlan human, out string humanError))
                throw new InvalidOperationException("Preview Macro Human Geography generation failed: " + humanError);

            Export(plan, geography, water, climate, quality, human, starter, outputPath, 320, 320, true);
            Debug.Log(
                "Macro Geography Preview Export: PASS\n" +
                "Path: " + outputPath + "\n" +
                "Seed: " + GoldenSeed + "\n" +
                "Size: Large\n" +
                "Coverage: High\n" +
                "GeographyHash: " + geography.CanonicalHash + "\n" +
                "WaterHash: " + water.CanonicalHash + "\n" +
                "ClimateHash: " + climate.CanonicalHash + "\n" +
                "PrevailingMoistureDirection: " + climate.PrevailingMoistureDirection + "\n" +
                "HumanGeographyHash: " + human.CanonicalHash + "\n" +
                "Starter: " + starter.Canonical);
        }

        public static void Export(
            MacroWorldPlan plan,
            MacroGeographyPlan geography,
            MacroWaterPlan water,
            WorldGameplayQualityAnalysis quality,
            MacroHumanGeographyPlan human,
            SectorId starterSector,
            string outputPath,
            int panelWidth,
            int panelHeight,
            bool overlaySectors)
        {
            ExportInternal(
                plan, geography, water, null, quality, human, starterSector,
                outputPath, panelWidth, panelHeight, overlaySectors);
        }

        public static void Export(
            MacroWorldPlan plan,
            MacroGeographyPlan geography,
            MacroWaterPlan water,
            MacroClimatePlan climate,
            WorldGameplayQualityAnalysis quality,
            MacroHumanGeographyPlan human,
            SectorId starterSector,
            string outputPath,
            int panelWidth,
            int panelHeight,
            bool overlaySectors)
        {
            ExportInternal(
                plan, geography, water, climate, quality, human, starterSector,
                outputPath, panelWidth, panelHeight, overlaySectors);
        }

        private static void ExportInternal(
            MacroWorldPlan plan,
            MacroGeographyPlan geography,
            MacroWaterPlan water,
            MacroClimatePlan climate,
            WorldGameplayQualityAnalysis quality,
            MacroHumanGeographyPlan human,
            SectorId starterSector,
            string outputPath,
            int panelWidth,
            int panelHeight,
            bool overlaySectors)
        {
            if (plan == null || geography == null || water == null || quality == null || human == null)
                throw new ArgumentNullException("Worldgen Inspector inputs are required.");
            if (plan.WorldBounds != geography.WorldBounds ||
                geography.WorldBounds != water.WorldBounds ||
                climate != null && geography.WorldBounds != climate.WorldBounds)
                throw new ArgumentException("Inspector plan/geography/water bounds must match.");
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Preview output path is required.", nameof(outputPath));
            if (panelWidth < 64 || panelHeight < 64)
                throw new ArgumentOutOfRangeException(nameof(panelWidth),
                    "Preview panels must be at least 64x64.");

            int panelColumns = climate == null ? 3 : 4;
            var texture = new Texture2D(panelWidth * panelColumns, panelHeight * 2,
                TextureFormat.RGB24, false, true);
            texture.name = "OldScarsWorldgenInspectorPreview";
            try
            {
                for (int y = 0; y < panelHeight; y++)
                for (int x = 0; x < panelWidth; x++)
                {
                    MacroPoint2D position = PixelToMacro(
                        plan.WorldBounds, x, y, panelWidth, panelHeight);
                    if (!geography.TrySampleAt(position, out MacroGeographySample geographySample))
                        throw new InvalidOperationException("Inspector sampled outside finite bounds.");
                    MacroWaterSample waterSample = water.SampleAt(position);
                    int sampleX = NearestSample(
                        position.X, plan.WorldBounds.MinX, plan.WorldBounds.Width,
                        quality.SampleColumns);
                    int sampleY = NearestSample(
                        position.Y, plan.WorldBounds.MinY, plan.WorldBounds.Height,
                        quality.SampleRows);

                    texture.SetPixel(x, y, ElevationColor(geographySample.Elevation));
                    texture.SetPixel(panelWidth + x, y, LandformColor(geographySample.Landform));
                    if (climate == null)
                    {
                        texture.SetPixel(panelWidth * 2 + x, y,
                            QualityColor(quality, sampleX, sampleY));
                        texture.SetPixel(x, panelHeight + y, WaterColor(waterSample));
                        texture.SetPixel(panelWidth + x, panelHeight + y,
                            DrainageColor(geographySample, waterSample));
                        texture.SetPixel(panelWidth * 2 + x, panelHeight + y,
                            WaterColor(waterSample));
                    }
                    else
                    {
                        MacroClimateSample climateSample = climate.SampleAt(position);
                        texture.SetPixel(panelWidth * 2 + x, y,
                            ThermalColor(climateSample.ThermalIndex));
                        texture.SetPixel(panelWidth * 3 + x, y,
                            MoistureColor(climateSample.MoistureIndex));
                        texture.SetPixel(x, panelHeight + y,
                            QualityColor(quality, sampleX, sampleY));
                        texture.SetPixel(panelWidth + x, panelHeight + y,
                            WaterColor(waterSample));
                        texture.SetPixel(panelWidth * 2 + x, panelHeight + y,
                            DrainageColor(geographySample, waterSample));
                        texture.SetPixel(panelWidth * 3 + x, panelHeight + y,
                            WaterColor(waterSample));
                    }
                }

                DrawBasinOverlay(texture, water, panelWidth, panelHeight, panelColumns);
                DrawHumanInfrastructurePanel(
                    texture, plan, human, panelWidth, panelHeight, panelColumns);
                if (overlaySectors)
                    DrawInspectorSectorOverlays(
                        texture, plan, starterSector, panelWidth, panelHeight, panelColumns);

                texture.Apply(false, false);
                byte[] png = texture.EncodeToPNG();
                string directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllBytes(outputPath, png);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
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

        private static Color QualityColor(
            WorldGameplayQualityAnalysis quality,
            int x,
            int y)
        {
            if (quality.HasSitePotentialAt(x, y)) return new Color(0.18f, 0.82f, 0.34f);
            if (quality.HasTraversalPotentialAt(x, y)) return new Color(0.78f, 0.70f, 0.18f);
            float gradient = Mathf.Clamp01(quality.GradientSampleAt(x, y) / 9000f);
            return Color.Lerp(new Color(0.30f, 0.22f, 0.18f), new Color(0.90f, 0.12f, 0.10f), gradient);
        }

        private static Color WaterColor(MacroWaterSample sample)
        {
            if (sample.IsOcean)
            {
                float variation = (sample.OceanBodyId % 5) * 0.035f;
                return new Color(0.05f + variation, 0.20f + variation, 0.52f + variation);
            }
            if (sample.IsCoastline) return new Color(0.86f, 0.74f, 0.42f);
            return new Color(0.23f, 0.40f, 0.20f);
        }

        private static Color ThermalColor(ushort thermal)
        {
            float value = thermal / 65535f;
            if (value < 0.5f)
                return Color.Lerp(new Color(0.08f, 0.20f, 0.58f), Color.white, value * 2f);
            return Color.Lerp(Color.white, new Color(0.82f, 0.12f, 0.05f), (value - 0.5f) * 2f);
        }

        private static Color MoistureColor(ushort moisture)
        {
            float value = moisture / 65535f;
            if (value < 0.5f)
                return Color.Lerp(new Color(0.48f, 0.28f, 0.10f), new Color(0.44f, 0.66f, 0.32f), value * 2f);
            return Color.Lerp(new Color(0.44f, 0.66f, 0.32f), new Color(0.05f, 0.30f, 0.72f), (value - 0.5f) * 2f);
        }

        private static Color DrainageColor(
            MacroGeographySample geography,
            MacroWaterSample water)
        {
            if (water.IsOcean) return new Color(0.05f, 0.15f, 0.38f);
            int fillDepth = Math.Max(0, water.ConditionedElevation - geography.Elevation);
            if (fillDepth > 0)
                return Color.Lerp(new Color(0.28f, 0.22f, 0.46f), Color.magenta,
                    Mathf.Clamp01(fillDepth / 8000f));
            return Color.HSVToRGB((water.DrainageDirection % 8) / 8f, 0.55f, 0.68f);
        }

        private static void DrawBasinOverlay(
            Texture2D texture,
            MacroWaterPlan water,
            int panelWidth,
            int panelHeight,
            int panelColumns)
        {
            for (int index = 0; index < water.BasinCandidates.Count; index++)
            {
                MacroBasinCandidate basin = water.BasinCandidates[index];
                int sampleX = basin.RepresentativeSampleIndex % water.SampleColumns;
                int sampleY = basin.RepresentativeSampleIndex / water.SampleColumns;
                int x = sampleX * (panelWidth - 1) / (water.SampleColumns - 1);
                int y = sampleY * (panelHeight - 1) / (water.SampleRows - 1);
                int basinPanel = panelColumns == 4 ? 2 : 1;
                DrawMarker(texture, panelWidth * basinPanel + x, panelHeight + y,
                    Color.white, panelWidth * panelColumns, panelHeight * 2);
            }
        }

        private static void DrawHumanInfrastructurePanel(
            Texture2D texture,
            MacroWorldPlan plan,
            MacroHumanGeographyPlan human,
            int panelWidth,
            int panelHeight,
            int panelColumns)
        {
            int offsetX = panelWidth * (panelColumns - 1);
            int offsetY = panelHeight;
            for (int index = 0; index < human.Roads.Count; index++)
            {
                MacroRoad road = human.Roads[index];
                Color color = road.RoadClass == MacroRoadClass.Primary
                    ? new Color(0.95f, 0.55f, 0.08f)
                    : new Color(0.90f, 0.82f, 0.48f);
                for (int point = 1; point < road.Polyline.Count; point++)
                {
                    ToPixel(plan.WorldBounds, road.Polyline[point - 1], panelWidth, panelHeight,
                        out int firstX, out int firstY);
                    ToPixel(plan.WorldBounds, road.Polyline[point], panelWidth, panelHeight,
                        out int secondX, out int secondY);
                    DrawLine(texture, offsetX + firstX, offsetY + firstY,
                        offsetX + secondX, offsetY + secondY,
                        color, panelWidth * panelColumns, panelHeight * 2);
                    if (road.RoadClass == MacroRoadClass.Primary)
                        DrawLine(texture, offsetX + firstX, offsetY + firstY + 1,
                            offsetX + secondX, offsetY + secondY + 1,
                            color, panelWidth * panelColumns, panelHeight * 2);
                }
            }
            for (int index = 0; index < human.Sites.Count; index++)
            {
                MacroHumanSite site = human.Sites[index];
                ToPixel(plan.WorldBounds, site.Position, panelWidth, panelHeight,
                    out int x, out int y);
                DrawMarker(texture, offsetX + x, offsetY + y,
                    site.Kind == MacroHumanHubKind.RegionalHub ? Color.red : Color.white,
                    panelWidth * panelColumns, panelHeight * 2);
                if (site.Kind == MacroHumanHubKind.RegionalHub)
                {
                    DrawMarker(texture, offsetX + x + 1, offsetY + y,
                        Color.red, panelWidth * panelColumns, panelHeight * 2);
                }
            }
        }

        private static void DrawInspectorSectorOverlays(
            Texture2D texture,
            MacroWorldPlan plan,
            SectorId starter,
            int panelWidth,
            int panelHeight,
            int panelColumns)
        {
            for (int index = 0; index < plan.SectorPlacements.Count; index++)
            {
                MacroSectorPlacement placement = plan.SectorPlacements[index];
                ToPixel(plan.WorldBounds, placement.Position, panelWidth, panelHeight,
                    out int x, out int y);
                Color color = placement.SectorId == starter ? Color.yellow : Color.white;
                int topPanel = panelColumns == 4 ? 1 : 2;
                DrawMarker(texture, panelWidth * topPanel + x, y, color,
                    panelWidth * panelColumns, panelHeight * 2);
                DrawMarker(texture, x, panelHeight + y, color,
                    panelWidth * panelColumns, panelHeight * 2);
            }
        }

        private static void DrawLine(
            Texture2D texture,
            int x0,
            int y0,
            int x1,
            int y1,
            Color color,
            int maximumX,
            int maximumY)
        {
            int dx = Math.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            while (true)
            {
                if (x0 >= 0 && x0 < maximumX && y0 >= 0 && y0 < maximumY)
                    texture.SetPixel(x0, y0, color);
                if (x0 == x1 && y0 == y1) break;
                int twice = error * 2;
                if (twice >= dy) { error += dy; x0 += sx; }
                if (twice <= dx) { error += dx; y0 += sy; }
            }
        }

        private static void ToPixel(
            FiniteMacroWorldBounds bounds,
            MacroPoint2D point,
            int width,
            int height,
            out int x,
            out int y)
        {
            x = (int)((point.X - bounds.MinX) * (width - 1L) / (bounds.Width - 1));
            y = (int)((point.Y - bounds.MinY) * (height - 1L) / (bounds.Height - 1));
        }

        private static int NearestSample(long coordinate, long minimum, long extent, int count)
        {
            long numerator = (coordinate - minimum) * (count - 1L);
            long denominator = extent - 1;
            return (int)Math.Max(0, Math.Min(count - 1L,
                (numerator * 2 + denominator) / (denominator * 2)));
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
