using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace OldScars.Core.World
{
    public enum MacroBiomeFamily : byte
    {
        None = 0,
        PolarBarrens = 1,
        Tundra = 2,
        ColdDesert = 3,
        ColdSteppe = 4,
        BorealForest = 5,
        TemperateDesert = 6,
        TemperateGrassland = 7,
        TemperateWoodland = 8,
        TemperateForest = 9,
        TemperateRainforest = 10,
        HotDesert = 11,
        Savanna = 12,
        WarmForest = 13,
        TropicalRainforest = 14
    }

    public readonly struct MacroBiomeProfile
    {
        public MacroBiomeProfile(
            MacroBiomeFamily family,
            ushort thermalCenterQ16,
            ushort moistureCenterQ16)
        {
            Family = family;
            ThermalCenterQ16 = thermalCenterQ16;
            MoistureCenterQ16 = moistureCenterQ16;
        }

        public MacroBiomeFamily Family { get; }
        public ushort ThermalCenterQ16 { get; }
        public ushort MoistureCenterQ16 { get; }
    }

    public readonly struct MacroEnvironmentSample
    {
        public MacroEnvironmentSample(
            MacroBiomeFamily primaryBiome,
            MacroBiomeFamily secondaryBiome,
            ushort transitionQ16)
        {
            PrimaryBiome = primaryBiome;
            SecondaryBiome = secondaryBiome;
            TransitionQ16 = transitionQ16;
        }

        public MacroBiomeFamily PrimaryBiome { get; }
        public MacroBiomeFamily SecondaryBiome { get; }

        /// <summary>
        /// Zero is deep inside the primary ecological profile. 65535 means the
        /// primary and secondary profiles are approximately equally compatible.
        /// </summary>
        public ushort TransitionQ16 { get; }
    }

    /// <summary>
    /// Immutable engine-level ecological profiles used by Macro Environment V1.
    /// These are generation truth, not moddable content definitions.
    /// </summary>
    public sealed class MacroEnvironmentGenerationSettings
    {
        public const string CurrentContract = "macro_environment_v1";
        public const int RequiredTerrestrialFamilyCount = 14;

        private readonly ReadOnlyCollection<MacroBiomeProfile> profiles;

        private MacroEnvironmentGenerationSettings(
            string generationContract,
            int sampleColumns,
            int sampleRows,
            int thermalDistanceWeight,
            int moistureDistanceWeight,
            IList<MacroBiomeProfile> resolvedProfiles)
        {
            GenerationContract = generationContract;
            SampleColumns = sampleColumns;
            SampleRows = sampleRows;
            ThermalDistanceWeight = thermalDistanceWeight;
            MoistureDistanceWeight = moistureDistanceWeight;
            profiles = new ReadOnlyCollection<MacroBiomeProfile>(
                new List<MacroBiomeProfile>(resolvedProfiles));
        }

        public string GenerationContract { get; }
        public int SampleColumns { get; }
        public int SampleRows { get; }
        public int ThermalDistanceWeight { get; }
        public int MoistureDistanceWeight { get; }
        public IReadOnlyList<MacroBiomeProfile> Profiles => profiles;

        internal string DeterministicKey
        {
            get
            {
                string value = "environment_" + GenerationContract + "_" +
                    SampleColumns.ToString(CultureInfo.InvariantCulture) + "_" +
                    SampleRows.ToString(CultureInfo.InvariantCulture) + "_" +
                    ThermalDistanceWeight.ToString(CultureInfo.InvariantCulture) + "_" +
                    MoistureDistanceWeight.ToString(CultureInfo.InvariantCulture);
                for (int index = 0; index < profiles.Count; index++)
                {
                    MacroBiomeProfile profile = profiles[index];
                    value += "_" + ToCanonical(profile.Family) + "_" +
                        profile.ThermalCenterQ16.ToString(CultureInfo.InvariantCulture) + "_" +
                        profile.MoistureCenterQ16.ToString(CultureInfo.InvariantCulture);
                }
                return value;
            }
        }

        public static MacroEnvironmentGenerationSettings Resolve(MacroClimatePlan climate)
        {
            if (climate == null)
                throw new ArgumentNullException(nameof(climate));

            if (!TryCreateResolved(
                    CurrentContract,
                    climate.SampleColumns,
                    climate.SampleRows,
                    1,
                    1,
                    BuiltInProfiles(),
                    out MacroEnvironmentGenerationSettings settings,
                    out string error))
            {
                throw new InvalidOperationException(
                    "Built-in Macro Environment profiles are invalid: " + error + ".");
            }
            return settings;
        }

        public static bool TryCreateResolved(
            string generationContract,
            int sampleColumns,
            int sampleRows,
            int thermalDistanceWeight,
            int moistureDistanceWeight,
            IEnumerable<MacroBiomeProfile> profileInputs,
            out MacroEnvironmentGenerationSettings settings,
            out string error)
        {
            return TryCreateResolvedInternal(
                generationContract,
                requireCurrentContract: true,
                sampleColumns,
                sampleRows,
                thermalDistanceWeight,
                moistureDistanceWeight,
                profileInputs,
                out settings,
                out error);
        }

#if UNITY_EDITOR
        internal static bool TryCreateForDiagnostics(
            string generationContract,
            int sampleColumns,
            int sampleRows,
            int thermalDistanceWeight,
            int moistureDistanceWeight,
            IEnumerable<MacroBiomeProfile> profileInputs,
            out MacroEnvironmentGenerationSettings settings,
            out string error)
        {
            return TryCreateResolvedInternal(
                generationContract,
                requireCurrentContract: false,
                sampleColumns,
                sampleRows,
                thermalDistanceWeight,
                moistureDistanceWeight,
                profileInputs,
                out settings,
                out error);
        }
#endif

        public static string ToCanonical(MacroBiomeFamily family)
        {
            switch (family)
            {
                case MacroBiomeFamily.None: return "none";
                case MacroBiomeFamily.PolarBarrens: return "polar_barrens";
                case MacroBiomeFamily.Tundra: return "tundra";
                case MacroBiomeFamily.ColdDesert: return "cold_desert";
                case MacroBiomeFamily.ColdSteppe: return "cold_steppe";
                case MacroBiomeFamily.BorealForest: return "boreal_forest";
                case MacroBiomeFamily.TemperateDesert: return "temperate_desert";
                case MacroBiomeFamily.TemperateGrassland: return "temperate_grassland";
                case MacroBiomeFamily.TemperateWoodland: return "temperate_woodland";
                case MacroBiomeFamily.TemperateForest: return "temperate_forest";
                case MacroBiomeFamily.TemperateRainforest: return "temperate_rainforest";
                case MacroBiomeFamily.HotDesert: return "hot_desert";
                case MacroBiomeFamily.Savanna: return "savanna";
                case MacroBiomeFamily.WarmForest: return "warm_forest";
                case MacroBiomeFamily.TropicalRainforest: return "tropical_rainforest";
                default:
                    throw new ArgumentOutOfRangeException(nameof(family), family,
                        "Unknown MacroBiomeFamily.");
            }
        }

        public static bool TryParseFamily(
            string raw,
            out MacroBiomeFamily family,
            out string error)
        {
            error = null;
            switch (raw)
            {
                case "none": family = MacroBiomeFamily.None; return true;
                case "polar_barrens": family = MacroBiomeFamily.PolarBarrens; return true;
                case "tundra": family = MacroBiomeFamily.Tundra; return true;
                case "cold_desert": family = MacroBiomeFamily.ColdDesert; return true;
                case "cold_steppe": family = MacroBiomeFamily.ColdSteppe; return true;
                case "boreal_forest": family = MacroBiomeFamily.BorealForest; return true;
                case "temperate_desert": family = MacroBiomeFamily.TemperateDesert; return true;
                case "temperate_grassland": family = MacroBiomeFamily.TemperateGrassland; return true;
                case "temperate_woodland": family = MacroBiomeFamily.TemperateWoodland; return true;
                case "temperate_forest": family = MacroBiomeFamily.TemperateForest; return true;
                case "temperate_rainforest": family = MacroBiomeFamily.TemperateRainforest; return true;
                case "hot_desert": family = MacroBiomeFamily.HotDesert; return true;
                case "savanna": family = MacroBiomeFamily.Savanna; return true;
                case "warm_forest": family = MacroBiomeFamily.WarmForest; return true;
                case "tropical_rainforest": family = MacroBiomeFamily.TropicalRainforest; return true;
                default:
                    family = default;
                    error = "unknown MacroBiomeFamily '" + Safe(raw) + "'";
                    return false;
            }
        }

        private static bool TryCreateResolvedInternal(
            string generationContract,
            bool requireCurrentContract,
            int sampleColumns,
            int sampleRows,
            int thermalDistanceWeight,
            int moistureDistanceWeight,
            IEnumerable<MacroBiomeProfile> profileInputs,
            out MacroEnvironmentGenerationSettings settings,
            out string error)
        {
            settings = null;
            error = null;
            if (string.IsNullOrEmpty(generationContract) ||
                requireCurrentContract && !string.Equals(
                    generationContract, CurrentContract, StringComparison.Ordinal))
            {
                error = "unsupported environment generation contract '" +
                        Safe(generationContract) + "'";
                return false;
            }
            if (sampleColumns < 2 || sampleRows < 2 ||
                sampleColumns > 1025 || sampleRows > 1025)
            {
                error = "environment sample columns/rows must be between 2 and 1025";
                return false;
            }
            if (thermalDistanceWeight < 1 || thermalDistanceWeight > 1024 ||
                moistureDistanceWeight < 1 || moistureDistanceWeight > 1024)
            {
                error = "environment distance weights must be between 1 and 1024";
                return false;
            }
            if (profileInputs == null)
            {
                error = "environment profiles are required";
                return false;
            }

            var resolved = new List<MacroBiomeProfile>(profileInputs);
            resolved.Sort((left, right) => ((byte)left.Family).CompareTo((byte)right.Family));
            if (resolved.Count != RequiredTerrestrialFamilyCount)
            {
                error = "environment requires exactly 14 terrestrial biome profiles";
                return false;
            }

            var seenFamilies = new bool[RequiredTerrestrialFamilyCount + 1];
            var seenCenters = new HashSet<ulong>();
            for (int index = 0; index < resolved.Count; index++)
            {
                MacroBiomeProfile profile = resolved[index];
                int familyValue = (byte)profile.Family;
                if (!Enum.IsDefined(typeof(MacroBiomeFamily), profile.Family) ||
                    profile.Family == MacroBiomeFamily.None ||
                    familyValue < 1 || familyValue > RequiredTerrestrialFamilyCount)
                {
                    error = "environment profile contains an invalid terrestrial family";
                    return false;
                }
                if (seenFamilies[familyValue])
                {
                    error = "environment profile family '" +
                            ToCanonical(profile.Family) + "' is duplicated";
                    return false;
                }
                seenFamilies[familyValue] = true;
                ulong center = ((ulong)profile.ThermalCenterQ16 << 16) |
                               profile.MoistureCenterQ16;
                if (!seenCenters.Add(center))
                {
                    error = "environment profiles must have distinct ecological centers";
                    return false;
                }
            }

            for (int family = 1; family <= RequiredTerrestrialFamilyCount; family++)
            {
                if (seenFamilies[family]) continue;
                error = "environment profile set is missing family value " +
                        family.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            settings = new MacroEnvironmentGenerationSettings(
                generationContract,
                sampleColumns,
                sampleRows,
                thermalDistanceWeight,
                moistureDistanceWeight,
                resolved);
            return true;
        }

        private static MacroBiomeProfile[] BuiltInProfiles()
        {
            return new[]
            {
                new MacroBiomeProfile(MacroBiomeFamily.PolarBarrens, 3277, 29491),
                new MacroBiomeProfile(MacroBiomeFamily.Tundra, 9175, 38010),
                new MacroBiomeProfile(MacroBiomeFamily.ColdDesert, 13107, 6554),
                new MacroBiomeProfile(MacroBiomeFamily.ColdSteppe, 17695, 19661),
                new MacroBiomeProfile(MacroBiomeFamily.BorealForest, 18350, 45875),
                new MacroBiomeProfile(MacroBiomeFamily.TemperateDesert, 31457, 5243),
                new MacroBiomeProfile(MacroBiomeFamily.TemperateGrassland, 32768, 19661),
                new MacroBiomeProfile(MacroBiomeFamily.TemperateWoodland, 34078, 32768),
                new MacroBiomeProfile(MacroBiomeFamily.TemperateForest, 34078, 45875),
                new MacroBiomeProfile(MacroBiomeFamily.TemperateRainforest, 32768, 58982),
                new MacroBiomeProfile(MacroBiomeFamily.HotDesert, 52428, 5243),
                new MacroBiomeProfile(MacroBiomeFamily.Savanna, 51117, 22282),
                new MacroBiomeProfile(MacroBiomeFamily.WarmForest, 49806, 41942),
                new MacroBiomeProfile(MacroBiomeFamily.TropicalRainforest, 55049, 58982)
            };
        }

        private static string Safe(string value) =>
            string.IsNullOrEmpty(value) ? "<EMPTY>" : value;
    }

    /// <summary>
    /// Immutable committed world-wide ecological classification. Landform,
    /// Water, Climate, and Environment remain separate dimensions.
    /// </summary>
    public sealed class MacroEnvironmentPlan
    {
        private const string CanonicalContract = "old_scars_macro_environment_plan_v1";
        private readonly byte[] primarySamples;
        private readonly byte[] secondarySamples;
        private readonly ushort[] transitionSamples;

        private MacroEnvironmentPlan(
            MacroEnvironmentGenerationSettings generationSettings,
            FiniteMacroWorldBounds worldBounds,
            byte[] primary,
            byte[] secondary,
            ushort[] transition)
        {
            GenerationSettings = generationSettings;
            WorldBounds = worldBounds;
            primarySamples = (byte[])primary.Clone();
            secondarySamples = (byte[])secondary.Clone();
            transitionSamples = (ushort[])transition.Clone();
            CanonicalHash = BuildCanonicalHash();
        }

        public MacroEnvironmentGenerationSettings GenerationSettings { get; }
        public FiniteMacroWorldBounds WorldBounds { get; }
        public int SampleColumns => GenerationSettings.SampleColumns;
        public int SampleRows => GenerationSettings.SampleRows;
        public int SampleCount => primarySamples.Length;
        public string CanonicalHash { get; }

        public static bool TryCreate(
            MacroEnvironmentGenerationSettings generationSettings,
            MacroClimatePlan climate,
            MacroWaterPlan water,
            byte[] primary,
            byte[] secondary,
            ushort[] transition,
            out MacroEnvironmentPlan plan,
            out string error)
        {
            plan = null;
            error = null;
            if (generationSettings == null || climate == null || water == null)
            {
                error = "MacroEnvironmentPlan requires settings and committed Climate/Water";
                return false;
            }
            if (climate.WorldBounds != water.WorldBounds ||
                generationSettings.SampleColumns != climate.SampleColumns ||
                generationSettings.SampleRows != climate.SampleRows ||
                generationSettings.SampleColumns != water.SampleColumns ||
                generationSettings.SampleRows != water.SampleRows)
            {
                error = "Environment settings, Climate, and Water must share one grid and WorldBounds";
                return false;
            }

            int expected;
            try
            {
                expected = checked(generationSettings.SampleColumns * generationSettings.SampleRows);
            }
            catch (OverflowException)
            {
                error = "resolved environment sample count overflowed";
                return false;
            }
            if (primary == null || secondary == null || transition == null ||
                primary.Length != expected || secondary.Length != expected ||
                transition.Length != expected)
            {
                error = "environment arrays must exactly match the resolved grid";
                return false;
            }

            for (int index = 0; index < expected; index++)
            {
                MacroBiomeFamily primaryFamily = (MacroBiomeFamily)primary[index];
                MacroBiomeFamily secondaryFamily = (MacroBiomeFamily)secondary[index];
                bool primaryValid = Enum.IsDefined(typeof(MacroBiomeFamily), primaryFamily);
                bool secondaryValid = Enum.IsDefined(typeof(MacroBiomeFamily), secondaryFamily);
                MacroWaterSample waterSample = water.SampleAt(
                    index % generationSettings.SampleColumns,
                    index / generationSettings.SampleColumns);
                if (waterSample.IsOcean)
                {
                    if (!primaryValid || !secondaryValid ||
                        primaryFamily != MacroBiomeFamily.None ||
                        secondaryFamily != MacroBiomeFamily.None ||
                        transition[index] != 0)
                    {
                        error = "ocean environment sample " + index +
                                " must be None/None/0";
                        return false;
                    }
                    continue;
                }

                if (!primaryValid || !secondaryValid ||
                    primaryFamily == MacroBiomeFamily.None ||
                    secondaryFamily == MacroBiomeFamily.None)
                {
                    error = "land environment sample " + index +
                            " requires valid terrestrial primary and secondary families";
                    return false;
                }
                if (primaryFamily == secondaryFamily)
                {
                    error = "land environment sample " + index +
                            " has identical primary and secondary families";
                    return false;
                }
            }

            plan = new MacroEnvironmentPlan(
                generationSettings, climate.WorldBounds, primary, secondary, transition);
            return true;
        }

        public MacroEnvironmentSample SampleAt(int column, int row)
        {
            int index = RequireSampleIndex(column, row);
            return new MacroEnvironmentSample(
                (MacroBiomeFamily)primarySamples[index],
                (MacroBiomeFamily)secondarySamples[index],
                transitionSamples[index]);
        }

        public MacroEnvironmentSample SampleAt(MacroPoint2D position)
        {
            if (!TrySampleAt(position, out MacroEnvironmentSample sample))
                throw new ArgumentOutOfRangeException(nameof(position),
                    "Macro environment position is outside finite WorldBounds.");
            return sample;
        }

        public bool TrySampleAt(MacroPoint2D position, out MacroEnvironmentSample sample)
        {
            sample = default;
            if (!WorldBounds.Contains(position))
                return false;
            int column = ResolveNearestAxis(
                position.X, WorldBounds.MinX, WorldBounds.Width, SampleColumns);
            int row = ResolveNearestAxis(
                position.Y, WorldBounds.MinY, WorldBounds.Height, SampleRows);
            sample = SampleAt(column, row);
            return true;
        }

        internal byte[] CopyPrimarySamples() => (byte[])primarySamples.Clone();
        internal byte[] CopySecondarySamples() => (byte[])secondarySamples.Clone();
        internal ushort[] CopyTransitionSamples() => (ushort[])transitionSamples.Clone();

        private int RequireSampleIndex(int column, int row)
        {
            if (column < 0 || column >= SampleColumns || row < 0 || row >= SampleRows)
                throw new ArgumentOutOfRangeException(nameof(column),
                    "Macro environment sample coordinate is outside the committed grid.");
            return row * SampleColumns + column;
        }

        private string BuildCanonicalHash()
        {
            return WorldCanonicalEncoding.ComputeSha256(stream =>
            {
                WorldCanonicalEncoding.WriteString(stream, CanonicalContract);
                WorldCanonicalEncoding.WriteString(stream, GenerationSettings.GenerationContract);
                WorldCanonicalEncoding.WriteInt64(stream, SampleColumns);
                WorldCanonicalEncoding.WriteInt64(stream, SampleRows);
                WorldCanonicalEncoding.WriteInt64(
                    stream, GenerationSettings.ThermalDistanceWeight);
                WorldCanonicalEncoding.WriteInt64(
                    stream, GenerationSettings.MoistureDistanceWeight);
                WorldCanonicalEncoding.WriteInt64(
                    stream, GenerationSettings.Profiles.Count);
                for (int index = 0; index < GenerationSettings.Profiles.Count; index++)
                {
                    MacroBiomeProfile profile = GenerationSettings.Profiles[index];
                    stream.WriteByte((byte)profile.Family);
                    WriteUShort(stream, profile.ThermalCenterQ16);
                    WriteUShort(stream, profile.MoistureCenterQ16);
                }
                WorldCanonicalEncoding.WriteInt64(stream, WorldBounds.MinX);
                WorldCanonicalEncoding.WriteInt64(stream, WorldBounds.MinY);
                WorldCanonicalEncoding.WriteInt64(stream, WorldBounds.MaxXExclusive);
                WorldCanonicalEncoding.WriteInt64(stream, WorldBounds.MaxYExclusive);
                WorldCanonicalEncoding.WriteInt64(stream, SampleCount);
                for (int index = 0; index < SampleCount; index++)
                {
                    stream.WriteByte(primarySamples[index]);
                    stream.WriteByte(secondarySamples[index]);
                    WriteUShort(stream, transitionSamples[index]);
                }
            });
        }

        private static int ResolveNearestAxis(
            long coordinate,
            long minimum,
            long extent,
            int sampleCount)
        {
            long denominator = extent - 1;
            long scaled = checked((coordinate - minimum) * (sampleCount - 1L));
            long rounded = (scaled + denominator / 2) / denominator;
            return (int)Math.Max(0, Math.Min(sampleCount - 1L, rounded));
        }

        private static void WriteUShort(System.IO.Stream stream, ushort value)
        {
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }
    }
}
