using System;
using System.Collections.Generic;
using System.Globalization;

namespace OldScars.Core.World
{
    /// <summary>
    /// Deterministic creation-time Macro Climate V1 pass. It produces only a
    /// long-term thermal/moisture baseline; no runtime weather work exists here.
    /// </summary>
    public static class MacroClimateGenerator
    {
        public const string DeterministicGenerationContract = "macro_climate_v1";

        public static bool TryGenerate(
            WorldGenerationContext context,
            MacroWorldPlan macroWorldPlan,
            MacroGeographyPlan geography,
            MacroWaterPlan water,
            out MacroClimatePlan climate,
            out string error)
        {
            return TryGenerateWithPassContract(
                context,
                macroWorldPlan,
                geography,
                water,
                DeterministicGenerationContract,
                out climate,
                out error);
        }

#if UNITY_EDITOR
        public static bool TryGenerateForPassContractDiagnostics(
            WorldGenerationContext context,
            MacroWorldPlan macroWorldPlan,
            MacroGeographyPlan geography,
            MacroWaterPlan water,
            string passGenerationContract,
            out MacroClimatePlan climate,
            out string error)
        {
            return TryGenerateWithPassContract(
                context,
                macroWorldPlan,
                geography,
                water,
                passGenerationContract,
                out climate,
                out error);
        }

        /// <summary>
        /// Editor-only causal probe that runs the production inner generator
        /// with an explicitly validated resolved setting set. New Game never
        /// calls this seam.
        /// </summary>
        public static bool TryGenerateForResolvedSettingsDiagnostics(
            WorldGenerationContext context,
            MacroGeographyPlan geography,
            MacroWaterPlan water,
            MacroClimateGenerationSettings settings,
            out MacroClimatePlan climate,
            out string error)
        {
            climate = null;
            error = null;
            if (context == null || geography == null || water == null || settings == null)
            {
                error = "Resolved Climate diagnostic generation requires context, Geography, Water, and settings";
                return false;
            }
            try
            {
                GenerateSamples(
                    context.WorldSeed,
                    DeterministicGenerationContract,
                    geography,
                    water,
                    settings,
                    out ushort[] thermal,
                    out ushort[] moisture);
                return MacroClimatePlan.TryCreate(
                    settings, geography, water, thermal, moisture,
                    out climate, out error);
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is InvalidOperationException ||
                exception is FormatException || exception is OverflowException)
            {
                error = "Resolved Climate diagnostic generation failed: " + exception.Message;
                return false;
            }
        }
#endif

        private static bool TryGenerateWithPassContract(
            WorldGenerationContext context,
            MacroWorldPlan macroWorldPlan,
            MacroGeographyPlan geography,
            MacroWaterPlan water,
            string passGenerationContract,
            out MacroClimatePlan climate,
            out string error)
        {
            climate = null;
            error = null;
            if (context == null || macroWorldPlan == null || geography == null || water == null)
            {
                error = "Macro Climate generation requires context plus committed Plan/Geography/Water";
                return false;
            }
            if (macroWorldPlan.WorldBounds != geography.WorldBounds ||
                macroWorldPlan.WorldBounds != water.WorldBounds ||
                geography.SampleColumns != water.SampleColumns ||
                geography.SampleRows != water.SampleRows)
            {
                error = "Macro Climate inputs disagree on finite WorldBounds or macro grid";
                return false;
            }

            try
            {
                string directionScope = "macro_" +
                    WorldGenerationSettings.ToCanonical(
                        macroWorldPlan.GenerationSettings.WorldSizePreset) + "_" +
                    geography.SampleColumns.ToString(CultureInfo.InvariantCulture) + "x" +
                    geography.SampleRows.ToString(CultureInfo.InvariantCulture);
                ulong directionSeed = DeriveNumericSeed(
                    context.WorldSeed,
                    passGenerationContract,
                    directionScope,
                    "prevailing_moisture_direction");
                MacroMoistureDirection direction =
                    (MacroMoistureDirection)(directionSeed % 8UL);
                MacroClimateGenerationSettings settings =
                    MacroClimateGenerationSettings.Resolve(
                        macroWorldPlan.GenerationSettings.WorldSizePreset,
                        geography,
                        direction);

                GenerateSamples(
                    context.WorldSeed,
                    passGenerationContract,
                    geography,
                    water,
                    settings,
                    out ushort[] thermal,
                    out ushort[] moisture);
                return MacroClimatePlan.TryCreate(
                    settings, geography, water, thermal, moisture,
                    out climate, out error);
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is InvalidOperationException ||
                exception is FormatException || exception is OverflowException)
            {
                error = "Macro Climate V1 generation failed: " + exception.Message;
                return false;
            }
        }

        private static void GenerateSamples(
            WorldSeed worldSeed,
            string passGenerationContract,
            MacroGeographyPlan geography,
            MacroWaterPlan water,
            MacroClimateGenerationSettings settings,
            out ushort[] thermal,
            out ushort[] moisture)
        {
            int sampleCount = checked(settings.SampleColumns * settings.SampleRows);
            thermal = new ushort[sampleCount];
            moisture = new ushort[sampleCount];

            string scope = "macro_climate_" +
                settings.SampleColumns.ToString(CultureInfo.InvariantCulture) + "x" +
                settings.SampleRows.ToString(CultureInfo.InvariantCulture);
            ulong thermalRegionalSeed = DeriveNumericSeed(
                worldSeed, passGenerationContract, scope, "thermal_regional");
            ulong moistureRegionalSeed = DeriveNumericSeed(
                worldSeed, passGenerationContract, scope, "moisture_regional");
            int[] oceanDistance = BuildOceanDistanceField(water);
            ResolveDirection(
                settings.PrevailingMoistureDirection,
                out int transportX,
                out int transportY);

            for (int y = 0; y < settings.SampleRows; y++)
            {
                int normalizedYQ16 = NormalizeIndexQ16(y, settings.SampleRows);
                for (int x = 0; x < settings.SampleColumns; x++)
                {
                    int index = y * settings.SampleColumns + x;
                    int normalizedXQ16 = NormalizeIndexQ16(x, settings.SampleColumns);
                    int thermalX = MultiplyQ16(
                        normalizedXQ16, settings.ThermalRegionalFrequencyQ16);
                    int thermalY = MultiplyQ16(
                        normalizedYQ16, settings.ThermalRegionalFrequencyQ16);
                    int moistureX = MultiplyQ16(
                        normalizedXQ16, settings.MoistureRegionalFrequencyQ16);
                    int moistureY = MultiplyQ16(
                        normalizedYQ16, settings.MoistureRegionalFrequencyQ16);

                    int latitudinal = LerpQ16(
                        settings.SouthThermalBaselineQ16,
                        settings.NorthThermalBaselineQ16,
                        normalizedYQ16);
                    int thermalNoise = StableValueNoise2D.SampleFbmQ16(
                        thermalRegionalSeed, thermalX, thermalY, 2);
                    int thermalAnomaly = ScaleCenteredQ16(
                        thermalNoise, settings.ThermalRegionalAmplitudeQ16);
                    int elevationCooling = ScaleQ16(
                        geography.ElevationSampleAt(x, y), settings.ElevationCoolingQ16);
                    thermal[index] = ClampToUShort(
                        latitudinal + thermalAnomaly - elevationCooling);

                    int moistureNoise = StableValueNoise2D.SampleFbmQ16(
                        moistureRegionalSeed, moistureX, moistureY, 2);
                    int regionalMoisture = settings.MoistureBaselineQ16 +
                        ScaleCenteredQ16(
                            moistureNoise, settings.MoistureRegionalAmplitudeQ16);
                    int oceanInfluence = ResolveOceanInfluence(
                        oceanDistance[index], settings);
                    ResolveOrographicInfluence(
                        geography,
                        x,
                        y,
                        transportX,
                        transportY,
                        settings,
                        out int windwardBoost,
                        out int leewardReduction);
                    moisture[index] = ClampToUShort(
                        regionalMoisture + oceanInfluence + windwardBoost - leewardReduction);
                }
            }
        }

        private static int[] BuildOceanDistanceField(MacroWaterPlan water)
        {
            int count = water.SampleCount;
            var distance = new int[count];
            var queue = new Queue<int>(count);
            for (int index = 0; index < count; index++)
            {
                int x = index % water.SampleColumns;
                int y = index / water.SampleColumns;
                if (water.SampleAt(x, y).IsOcean)
                {
                    distance[index] = 0;
                    queue.Enqueue(index);
                }
                else
                {
                    distance[index] = int.MaxValue;
                }
            }
            if (queue.Count == 0)
                throw new InvalidOperationException("Macro Climate ocean influence requires committed ocean truth.");

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int x = current % water.SampleColumns;
                int y = current / water.SampleColumns;
                int nextDistance = distance[current] + 1;
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0) continue;
                    int nextX = x + offsetX;
                    int nextY = y + offsetY;
                    if (nextX < 0 || nextX >= water.SampleColumns ||
                        nextY < 0 || nextY >= water.SampleRows) continue;
                    int next = nextY * water.SampleColumns + nextX;
                    if (distance[next] <= nextDistance) continue;
                    distance[next] = nextDistance;
                    queue.Enqueue(next);
                }
            }
            return distance;
        }

        private static int ResolveOceanInfluence(
            int oceanDistanceCells,
            MacroClimateGenerationSettings settings)
        {
            if (oceanDistanceCells >= settings.OceanInfluenceDistanceCells)
                return 0;
            return settings.OceanInfluenceMaximumQ16 *
                   (settings.OceanInfluenceDistanceCells - oceanDistanceCells) /
                   settings.OceanInfluenceDistanceCells;
        }

        private static void ResolveOrographicInfluence(
            MacroGeographyPlan geography,
            int x,
            int y,
            int transportX,
            int transportY,
            MacroClimateGenerationSettings settings,
            out int windwardBoost,
            out int leewardReduction)
        {
            int currentElevation = geography.ElevationSampleAt(x, y);
            int weightedRise = 0;
            int weightedBarrier = 0;
            for (int step = 1; step <= settings.OrographicLookbackCells; step++)
            {
                int upwindX = x - transportX * step;
                int upwindY = y - transportY * step;
                if (upwindX < 0 || upwindX >= geography.SampleColumns ||
                    upwindY < 0 || upwindY >= geography.SampleRows)
                    break;
                int upwindElevation = geography.ElevationSampleAt(upwindX, upwindY);
                int attenuation = settings.OrographicLookbackCells - step + 1;
                int rise = Math.Max(0, currentElevation - upwindElevation) * attenuation /
                           settings.OrographicLookbackCells;
                int barrier = Math.Max(0, upwindElevation - currentElevation) * attenuation /
                              settings.OrographicLookbackCells;
                weightedRise = Math.Max(weightedRise, rise);
                weightedBarrier = Math.Max(weightedBarrier, barrier);
            }

            windwardBoost = weightedRise <= settings.OrographicRiseThresholdQ16
                ? 0
                : Math.Min(
                    settings.WindwardMaximumBoostQ16,
                    (weightedRise - settings.OrographicRiseThresholdQ16) /
                    settings.OrographicResponseDivisor);
            leewardReduction = weightedBarrier <= settings.OrographicRiseThresholdQ16
                ? 0
                : Math.Min(
                    settings.LeewardMaximumReductionQ16,
                    (weightedBarrier - settings.OrographicRiseThresholdQ16) /
                    settings.OrographicResponseDivisor);
        }

        private static void ResolveDirection(
            MacroMoistureDirection direction,
            out int x,
            out int y)
        {
            switch (direction)
            {
                case MacroMoistureDirection.North: x = 0; y = 1; return;
                case MacroMoistureDirection.NorthEast: x = 1; y = 1; return;
                case MacroMoistureDirection.East: x = 1; y = 0; return;
                case MacroMoistureDirection.SouthEast: x = 1; y = -1; return;
                case MacroMoistureDirection.South: x = 0; y = -1; return;
                case MacroMoistureDirection.SouthWest: x = -1; y = -1; return;
                case MacroMoistureDirection.West: x = -1; y = 0; return;
                case MacroMoistureDirection.NorthWest: x = -1; y = 1; return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction,
                        "Unknown MacroMoistureDirection.");
            }
        }

        private static ulong DeriveNumericSeed(
            WorldSeed worldSeed,
            string passGenerationContract,
            string scope,
            string pass)
        {
            DeterministicDomainKey domain = WorldDeterminism.DerivePassDomainKey(
                worldSeed, passGenerationContract, scope, pass);
            ulong value = 0;
            for (int index = 0; index < 16; index++)
            {
                char character = domain.Canonical[index];
                uint digit = character <= '9'
                    ? (uint)(character - '0')
                    : (uint)(character - 'a' + 10);
                value = (value << 4) | digit;
            }
            return value;
        }

        private static int NormalizeIndexQ16(int index, int count) =>
            (int)((long)index * 65535 / (count - 1));

        private static int MultiplyQ16(int first, int second) =>
            (int)(((long)first * second + 32768) >> 16);

        private static int ScaleQ16(int normalized, int scale) =>
            (int)(((long)normalized * scale + 32767) / 65535);

        private static int ScaleCenteredQ16(int normalized, int amplitude) =>
            (int)((long)(normalized - 32768) * amplitude / 32768);

        private static int LerpQ16(int first, int second, int amount) =>
            first + (int)((long)(second - first) * amount / 65535);

        private static ushort ClampToUShort(int value) =>
            (ushort)Math.Max(0, Math.Min(ushort.MaxValue, value));
    }
}
