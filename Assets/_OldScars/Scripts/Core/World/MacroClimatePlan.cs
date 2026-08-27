using System;
using System.Globalization;

namespace OldScars.Core.World
{
    /// <summary>
    /// Dominant long-term moisture transport used only by Macro Climate V1.
    /// Positive macro Y is North; this is not runtime wind.
    /// </summary>
    public enum MacroMoistureDirection : byte
    {
        North = 0,
        NorthEast = 1,
        East = 2,
        SouthEast = 3,
        South = 4,
        SouthWest = 5,
        West = 6,
        NorthWest = 7
    }

    public readonly struct MacroClimateSample
    {
        public MacroClimateSample(ushort thermalIndex, ushort moistureIndex)
        {
            ThermalIndex = thermalIndex;
            MoistureIndex = moistureIndex;
        }

        /// <summary>Relative long-term thermal magnitude, not degrees Celsius.</summary>
        public ushort ThermalIndex { get; }

        /// <summary>
        /// Relative long-term precipitation/moisture tendency. It is not
        /// current humidity, rainfall, soil moisture, or water depth.
        /// </summary>
        public ushort MoistureIndex { get; }
    }

    /// <summary>
    /// Resolved generation-relevant Climate V1 tuning. Every value that can
    /// alter committed climate truth is persisted; execution settings are absent.
    /// </summary>
    public sealed class MacroClimateGenerationSettings
    {
        public const string CurrentContract = "macro_climate_v1";

        private MacroClimateGenerationSettings(
            int sampleColumns,
            int sampleRows,
            int thermalRegionalFrequencyQ16,
            int moistureRegionalFrequencyQ16,
            int southThermalBaselineQ16,
            int northThermalBaselineQ16,
            int thermalRegionalAmplitudeQ16,
            int elevationCoolingQ16,
            int moistureBaselineQ16,
            int moistureRegionalAmplitudeQ16,
            int oceanInfluenceMaximumQ16,
            int oceanInfluenceDistanceCells,
            int orographicLookbackCells,
            int orographicRiseThresholdQ16,
            int windwardMaximumBoostQ16,
            int leewardMaximumReductionQ16,
            int orographicResponseDivisor,
            MacroMoistureDirection prevailingMoistureDirection)
        {
            SampleColumns = sampleColumns;
            SampleRows = sampleRows;
            ThermalRegionalFrequencyQ16 = thermalRegionalFrequencyQ16;
            MoistureRegionalFrequencyQ16 = moistureRegionalFrequencyQ16;
            SouthThermalBaselineQ16 = southThermalBaselineQ16;
            NorthThermalBaselineQ16 = northThermalBaselineQ16;
            ThermalRegionalAmplitudeQ16 = thermalRegionalAmplitudeQ16;
            ElevationCoolingQ16 = elevationCoolingQ16;
            MoistureBaselineQ16 = moistureBaselineQ16;
            MoistureRegionalAmplitudeQ16 = moistureRegionalAmplitudeQ16;
            OceanInfluenceMaximumQ16 = oceanInfluenceMaximumQ16;
            OceanInfluenceDistanceCells = oceanInfluenceDistanceCells;
            OrographicLookbackCells = orographicLookbackCells;
            OrographicRiseThresholdQ16 = orographicRiseThresholdQ16;
            WindwardMaximumBoostQ16 = windwardMaximumBoostQ16;
            LeewardMaximumReductionQ16 = leewardMaximumReductionQ16;
            OrographicResponseDivisor = orographicResponseDivisor;
            PrevailingMoistureDirection = prevailingMoistureDirection;
        }

        public string GenerationContract => CurrentContract;
        public int SampleColumns { get; }
        public int SampleRows { get; }
        public int ThermalRegionalFrequencyQ16 { get; }
        public int MoistureRegionalFrequencyQ16 { get; }
        public int SouthThermalBaselineQ16 { get; }
        public int NorthThermalBaselineQ16 { get; }
        public int ThermalRegionalAmplitudeQ16 { get; }
        public int ElevationCoolingQ16 { get; }
        public int MoistureBaselineQ16 { get; }
        public int MoistureRegionalAmplitudeQ16 { get; }
        public int OceanInfluenceMaximumQ16 { get; }
        public int OceanInfluenceDistanceCells { get; }
        public int OrographicLookbackCells { get; }
        public int OrographicRiseThresholdQ16 { get; }
        public int WindwardMaximumBoostQ16 { get; }
        public int LeewardMaximumReductionQ16 { get; }
        public int OrographicResponseDivisor { get; }
        public MacroMoistureDirection PrevailingMoistureDirection { get; }

        internal string DeterministicKey =>
            "climate_" + SampleColumns.ToString(CultureInfo.InvariantCulture) + "_" +
            SampleRows.ToString(CultureInfo.InvariantCulture) + "_" +
            ThermalRegionalFrequencyQ16.ToString(CultureInfo.InvariantCulture) + "_" +
            MoistureRegionalFrequencyQ16.ToString(CultureInfo.InvariantCulture) + "_" +
            SouthThermalBaselineQ16.ToString(CultureInfo.InvariantCulture) + "_" +
            NorthThermalBaselineQ16.ToString(CultureInfo.InvariantCulture) + "_" +
            ThermalRegionalAmplitudeQ16.ToString(CultureInfo.InvariantCulture) + "_" +
            ElevationCoolingQ16.ToString(CultureInfo.InvariantCulture) + "_" +
            MoistureBaselineQ16.ToString(CultureInfo.InvariantCulture) + "_" +
            MoistureRegionalAmplitudeQ16.ToString(CultureInfo.InvariantCulture) + "_" +
            OceanInfluenceMaximumQ16.ToString(CultureInfo.InvariantCulture) + "_" +
            OceanInfluenceDistanceCells.ToString(CultureInfo.InvariantCulture) + "_" +
            OrographicLookbackCells.ToString(CultureInfo.InvariantCulture) + "_" +
            OrographicRiseThresholdQ16.ToString(CultureInfo.InvariantCulture) + "_" +
            WindwardMaximumBoostQ16.ToString(CultureInfo.InvariantCulture) + "_" +
            LeewardMaximumReductionQ16.ToString(CultureInfo.InvariantCulture) + "_" +
            OrographicResponseDivisor.ToString(CultureInfo.InvariantCulture) + "_" +
            ToCanonical(PrevailingMoistureDirection);

        public static MacroClimateGenerationSettings Resolve(
            WorldSizePreset preset,
            MacroGeographyPlan geography,
            MacroMoistureDirection prevailingMoistureDirection)
        {
            if (geography == null)
                throw new ArgumentNullException(nameof(geography));

            int thermalFrequency;
            int moistureFrequency;
            int oceanDistance;
            int lookback;
            switch (preset)
            {
                case WorldSizePreset.Small:
                    thermalFrequency = 3 << 16;
                    moistureFrequency = 4 << 16;
                    oceanDistance = 8;
                    lookback = 5;
                    break;
                case WorldSizePreset.Medium:
                    thermalFrequency = 4 << 16;
                    moistureFrequency = 5 << 16;
                    oceanDistance = 10;
                    lookback = 6;
                    break;
                case WorldSizePreset.Large:
                    thermalFrequency = 5 << 16;
                    moistureFrequency = 7 << 16;
                    oceanDistance = 12;
                    lookback = 7;
                    break;
                case WorldSizePreset.Huge:
                    thermalFrequency = 7 << 16;
                    moistureFrequency = 10 << 16;
                    oceanDistance = 16;
                    lookback = 9;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(preset), preset,
                        "Unknown WorldSizePreset.");
            }

            return CreateValidated(
                geography.SampleColumns,
                geography.SampleRows,
                thermalFrequency,
                moistureFrequency,
                50500,
                18500,
                8500,
                15000,
                30000,
                14500,
                10500,
                oceanDistance,
                lookback,
                2500,
                6000,
                7000,
                3,
                prevailingMoistureDirection);
        }

        public static bool TryCreateResolved(
            string generationContract,
            int sampleColumns,
            int sampleRows,
            int thermalRegionalFrequencyQ16,
            int moistureRegionalFrequencyQ16,
            int southThermalBaselineQ16,
            int northThermalBaselineQ16,
            int thermalRegionalAmplitudeQ16,
            int elevationCoolingQ16,
            int moistureBaselineQ16,
            int moistureRegionalAmplitudeQ16,
            int oceanInfluenceMaximumQ16,
            int oceanInfluenceDistanceCells,
            int orographicLookbackCells,
            int orographicRiseThresholdQ16,
            int windwardMaximumBoostQ16,
            int leewardMaximumReductionQ16,
            int orographicResponseDivisor,
            MacroMoistureDirection prevailingMoistureDirection,
            out MacroClimateGenerationSettings settings,
            out string error)
        {
            settings = null;
            error = null;
            if (!string.Equals(generationContract, CurrentContract, StringComparison.Ordinal))
            {
                error = "unsupported climate generation contract '" + Safe(generationContract) + "'";
                return false;
            }
            if (sampleColumns < 2 || sampleRows < 2 || sampleColumns > 1025 || sampleRows > 1025)
            {
                error = "climate sample columns/rows must be between 2 and 1025";
                return false;
            }
            if (thermalRegionalFrequencyQ16 < 1 || moistureRegionalFrequencyQ16 < 1)
            {
                error = "climate regional frequencies must be positive";
                return false;
            }
            if (!IsQ16(southThermalBaselineQ16) || !IsQ16(northThermalBaselineQ16) ||
                !IsQ16(thermalRegionalAmplitudeQ16) || !IsQ16(elevationCoolingQ16) ||
                !IsQ16(moistureBaselineQ16) || !IsQ16(moistureRegionalAmplitudeQ16) ||
                !IsQ16(oceanInfluenceMaximumQ16) || !IsQ16(orographicRiseThresholdQ16) ||
                !IsQ16(windwardMaximumBoostQ16) || !IsQ16(leewardMaximumReductionQ16))
            {
                error = "normalized climate settings must remain in the ushort/Q16 range";
                return false;
            }
            if (southThermalBaselineQ16 <= northThermalBaselineQ16)
            {
                error = "south thermal baseline must be warmer than north for the canonical orientation";
                return false;
            }
            if (oceanInfluenceDistanceCells < 1 || oceanInfluenceDistanceCells > 256 ||
                orographicLookbackCells < 1 || orographicLookbackCells > 64)
            {
                error = "ocean influence distance or orographic lookback is outside the bounded contract";
                return false;
            }
            if (orographicResponseDivisor < 1 || orographicResponseDivisor > 64)
            {
                error = "orographic response divisor must be between 1 and 64";
                return false;
            }
            if (!Enum.IsDefined(typeof(MacroMoistureDirection), prevailingMoistureDirection))
            {
                error = "prevailing moisture direction is unknown";
                return false;
            }

            settings = new MacroClimateGenerationSettings(
                sampleColumns,
                sampleRows,
                thermalRegionalFrequencyQ16,
                moistureRegionalFrequencyQ16,
                southThermalBaselineQ16,
                northThermalBaselineQ16,
                thermalRegionalAmplitudeQ16,
                elevationCoolingQ16,
                moistureBaselineQ16,
                moistureRegionalAmplitudeQ16,
                oceanInfluenceMaximumQ16,
                oceanInfluenceDistanceCells,
                orographicLookbackCells,
                orographicRiseThresholdQ16,
                windwardMaximumBoostQ16,
                leewardMaximumReductionQ16,
                orographicResponseDivisor,
                prevailingMoistureDirection);
            return true;
        }

        public static string ToCanonical(MacroMoistureDirection direction)
        {
            switch (direction)
            {
                case MacroMoistureDirection.North: return "n";
                case MacroMoistureDirection.NorthEast: return "ne";
                case MacroMoistureDirection.East: return "e";
                case MacroMoistureDirection.SouthEast: return "se";
                case MacroMoistureDirection.South: return "s";
                case MacroMoistureDirection.SouthWest: return "sw";
                case MacroMoistureDirection.West: return "w";
                case MacroMoistureDirection.NorthWest: return "nw";
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction,
                        "Unknown MacroMoistureDirection.");
            }
        }

        public static bool TryParseDirection(
            string raw,
            out MacroMoistureDirection direction,
            out string error)
        {
            error = null;
            switch (raw)
            {
                case "n": direction = MacroMoistureDirection.North; return true;
                case "ne": direction = MacroMoistureDirection.NorthEast; return true;
                case "e": direction = MacroMoistureDirection.East; return true;
                case "se": direction = MacroMoistureDirection.SouthEast; return true;
                case "s": direction = MacroMoistureDirection.South; return true;
                case "sw": direction = MacroMoistureDirection.SouthWest; return true;
                case "w": direction = MacroMoistureDirection.West; return true;
                case "nw": direction = MacroMoistureDirection.NorthWest; return true;
                default:
                    direction = default;
                    error = "expected one of: n, ne, e, se, s, sw, w, nw";
                    return false;
            }
        }

        private static MacroClimateGenerationSettings CreateValidated(
            int sampleColumns,
            int sampleRows,
            int thermalRegionalFrequencyQ16,
            int moistureRegionalFrequencyQ16,
            int southThermalBaselineQ16,
            int northThermalBaselineQ16,
            int thermalRegionalAmplitudeQ16,
            int elevationCoolingQ16,
            int moistureBaselineQ16,
            int moistureRegionalAmplitudeQ16,
            int oceanInfluenceMaximumQ16,
            int oceanInfluenceDistanceCells,
            int orographicLookbackCells,
            int orographicRiseThresholdQ16,
            int windwardMaximumBoostQ16,
            int leewardMaximumReductionQ16,
            int orographicResponseDivisor,
            MacroMoistureDirection prevailingMoistureDirection)
        {
            if (!TryCreateResolved(
                    CurrentContract,
                    sampleColumns,
                    sampleRows,
                    thermalRegionalFrequencyQ16,
                    moistureRegionalFrequencyQ16,
                    southThermalBaselineQ16,
                    northThermalBaselineQ16,
                    thermalRegionalAmplitudeQ16,
                    elevationCoolingQ16,
                    moistureBaselineQ16,
                    moistureRegionalAmplitudeQ16,
                    oceanInfluenceMaximumQ16,
                    oceanInfluenceDistanceCells,
                    orographicLookbackCells,
                    orographicRiseThresholdQ16,
                    windwardMaximumBoostQ16,
                    leewardMaximumReductionQ16,
                    orographicResponseDivisor,
                    prevailingMoistureDirection,
                    out MacroClimateGenerationSettings settings,
                    out string error))
            {
                throw new InvalidOperationException("Built-in macro climate tuning is invalid: " + error + ".");
            }
            return settings;
        }

        private static bool IsQ16(int value) => value >= 0 && value <= ushort.MaxValue;
        private static string Safe(string value) => string.IsNullOrEmpty(value) ? "<EMPTY>" : value;
    }

    /// <summary>
    /// Immutable committed world-wide long-term climate baseline. It is pure
    /// logical data and does not represent biome identity or runtime weather.
    /// </summary>
    public sealed class MacroClimatePlan
    {
        private const string CanonicalContract = "old_scars_macro_climate_plan_v1";
        private readonly ushort[] thermalSamples;
        private readonly ushort[] moistureSamples;

        private MacroClimatePlan(
            MacroClimateGenerationSettings generationSettings,
            FiniteMacroWorldBounds worldBounds,
            ushort[] thermal,
            ushort[] moisture)
        {
            GenerationSettings = generationSettings;
            WorldBounds = worldBounds;
            thermalSamples = (ushort[])thermal.Clone();
            moistureSamples = (ushort[])moisture.Clone();
            CanonicalHash = BuildCanonicalHash();
        }

        public MacroClimateGenerationSettings GenerationSettings { get; }
        public FiniteMacroWorldBounds WorldBounds { get; }
        public int SampleColumns => GenerationSettings.SampleColumns;
        public int SampleRows => GenerationSettings.SampleRows;
        public int SampleCount => thermalSamples.Length;
        public MacroMoistureDirection PrevailingMoistureDirection =>
            GenerationSettings.PrevailingMoistureDirection;
        public string CanonicalHash { get; }

        public static bool TryCreate(
            MacroClimateGenerationSettings generationSettings,
            MacroGeographyPlan geography,
            MacroWaterPlan water,
            ushort[] thermal,
            ushort[] moisture,
            out MacroClimatePlan plan,
            out string error)
        {
            plan = null;
            error = null;
            if (generationSettings == null || geography == null || water == null)
            {
                error = "MacroClimatePlan requires resolved settings and committed Geography/Water";
                return false;
            }
            if (geography.WorldBounds != water.WorldBounds ||
                generationSettings.SampleColumns != geography.SampleColumns ||
                generationSettings.SampleRows != geography.SampleRows ||
                generationSettings.SampleColumns != water.SampleColumns ||
                generationSettings.SampleRows != water.SampleRows)
            {
                error = "Climate settings, Geography, and Water must share one grid and WorldBounds";
                return false;
            }

            int expected;
            try
            {
                expected = checked(generationSettings.SampleColumns * generationSettings.SampleRows);
            }
            catch (OverflowException)
            {
                error = "resolved climate sample count overflowed";
                return false;
            }
            if (thermal == null || moisture == null ||
                thermal.Length != expected || moisture.Length != expected)
            {
                error = "thermal and moisture arrays must exactly match the resolved Climate grid";
                return false;
            }

            ushort thermalMin = ushort.MaxValue;
            ushort thermalMax = ushort.MinValue;
            ushort moistureMin = ushort.MaxValue;
            ushort moistureMax = ushort.MinValue;
            for (int index = 0; index < expected; index++)
            {
                thermalMin = Math.Min(thermalMin, thermal[index]);
                thermalMax = Math.Max(thermalMax, thermal[index]);
                moistureMin = Math.Min(moistureMin, moisture[index]);
                moistureMax = Math.Max(moistureMax, moisture[index]);
            }
            if (thermalMin == thermalMax || moistureMin == moistureMax)
            {
                error = "Climate fields must not be constant";
                return false;
            }

            plan = new MacroClimatePlan(
                generationSettings, geography.WorldBounds, thermal, moisture);
            return true;
        }

        public MacroClimateSample SampleAt(int column, int row)
        {
            int index = RequireSampleIndex(column, row);
            return new MacroClimateSample(thermalSamples[index], moistureSamples[index]);
        }

        public MacroClimateSample SampleAt(MacroPoint2D position)
        {
            if (!TrySampleAt(position, out MacroClimateSample sample))
                throw new ArgumentOutOfRangeException(nameof(position),
                    "Macro climate position is outside finite WorldBounds.");
            return sample;
        }

        public bool TrySampleAt(MacroPoint2D position, out MacroClimateSample sample)
        {
            sample = default;
            if (!WorldBounds.Contains(position))
                return false;
            sample = new MacroClimateSample(ThermalAt(position), MoistureAt(position));
            return true;
        }

        public ushort ThermalAt(MacroPoint2D position) => InterpolatedAt(position, thermalSamples);
        public ushort MoistureAt(MacroPoint2D position) => InterpolatedAt(position, moistureSamples);

        internal ushort[] CopyThermalSamples() => (ushort[])thermalSamples.Clone();
        internal ushort[] CopyMoistureSamples() => (ushort[])moistureSamples.Clone();

        private ushort InterpolatedAt(MacroPoint2D position, ushort[] samples)
        {
            if (!WorldBounds.Contains(position))
                throw new ArgumentOutOfRangeException(nameof(position),
                    "Macro climate position is outside finite WorldBounds.");
            ResolveAxis(position.X, WorldBounds.MinX, WorldBounds.Width, SampleColumns,
                out int x0, out int x1, out long xNumerator, out long xDenominator);
            ResolveAxis(position.Y, WorldBounds.MinY, WorldBounds.Height, SampleRows,
                out int y0, out int y1, out long yNumerator, out long yDenominator);
            long lower = Interpolate(
                samples[y0 * SampleColumns + x0], samples[y0 * SampleColumns + x1],
                xNumerator, xDenominator);
            long upper = Interpolate(
                samples[y1 * SampleColumns + x0], samples[y1 * SampleColumns + x1],
                xNumerator, xDenominator);
            return (ushort)Math.Max(0, Math.Min(ushort.MaxValue,
                Interpolate(lower, upper, yNumerator, yDenominator)));
        }

        private int RequireSampleIndex(int column, int row)
        {
            if (column < 0 || column >= SampleColumns || row < 0 || row >= SampleRows)
                throw new ArgumentOutOfRangeException(nameof(column),
                    "Macro climate sample coordinate is outside the committed grid.");
            return row * SampleColumns + column;
        }

        private string BuildCanonicalHash()
        {
            return WorldCanonicalEncoding.ComputeSha256(stream =>
            {
                WorldCanonicalEncoding.WriteString(stream, CanonicalContract);
                WorldCanonicalEncoding.WriteString(stream, GenerationSettings.GenerationContract);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.SampleColumns);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.SampleRows);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.ThermalRegionalFrequencyQ16);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.MoistureRegionalFrequencyQ16);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.SouthThermalBaselineQ16);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.NorthThermalBaselineQ16);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.ThermalRegionalAmplitudeQ16);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.ElevationCoolingQ16);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.MoistureBaselineQ16);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.MoistureRegionalAmplitudeQ16);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.OceanInfluenceMaximumQ16);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.OceanInfluenceDistanceCells);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.OrographicLookbackCells);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.OrographicRiseThresholdQ16);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.WindwardMaximumBoostQ16);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.LeewardMaximumReductionQ16);
                WorldCanonicalEncoding.WriteInt64(stream, GenerationSettings.OrographicResponseDivisor);
                WorldCanonicalEncoding.WriteInt64(stream, (byte)GenerationSettings.PrevailingMoistureDirection);
                WorldCanonicalEncoding.WriteInt64(stream, WorldBounds.MinX);
                WorldCanonicalEncoding.WriteInt64(stream, WorldBounds.MinY);
                WorldCanonicalEncoding.WriteInt64(stream, WorldBounds.MaxXExclusive);
                WorldCanonicalEncoding.WriteInt64(stream, WorldBounds.MaxYExclusive);
                WorldCanonicalEncoding.WriteInt64(stream, SampleCount);
                for (int index = 0; index < SampleCount; index++)
                {
                    stream.WriteByte((byte)(thermalSamples[index] >> 8));
                    stream.WriteByte((byte)thermalSamples[index]);
                    stream.WriteByte((byte)(moistureSamples[index] >> 8));
                    stream.WriteByte((byte)moistureSamples[index]);
                }
            });
        }

        private static void ResolveAxis(
            long coordinate,
            long minimum,
            long extent,
            int sampleCount,
            out int lower,
            out int upper,
            out long numerator,
            out long denominator)
        {
            denominator = extent - 1;
            long scaled = checked((coordinate - minimum) * (sampleCount - 1L));
            lower = (int)(scaled / denominator);
            numerator = scaled % denominator;
            if (lower >= sampleCount - 1)
            {
                lower = sampleCount - 1;
                upper = lower;
                numerator = 0;
            }
            else
            {
                upper = lower + 1;
            }
        }

        private static long Interpolate(long first, long second, long numerator, long denominator)
        {
            if (numerator == 0 || first == second)
                return first;
            long scaled = checked((second - first) * numerator);
            long rounded = scaled >= 0
                ? (scaled + denominator / 2) / denominator
                : (scaled - denominator / 2) / denominator;
            return first + rounded;
        }
    }
}
