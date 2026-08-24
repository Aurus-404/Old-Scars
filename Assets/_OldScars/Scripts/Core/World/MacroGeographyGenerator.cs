using System;
using System.Collections.Generic;
using System.Globalization;

namespace OldScars.Core.World
{
    /// <summary>
    /// Deterministic multi-field Macro Elevation / Landforms V1 generator.
    /// SHA-256 derives a small fixed set of pass seeds; all inner-loop sampling
    /// uses the explicit integer mixer/noise below.
    /// </summary>
    public static class MacroGeographyGenerator
    {
        public const string DeterministicGenerationContract = "macro_geography_v1";

        public static bool TryGenerate(
            WorldGenerationContext context,
            MacroWorldPlan macroWorldPlan,
            out MacroGeographyPlan geography,
            out string error)
        {
            return TryGenerateWithPassContract(
                context,
                macroWorldPlan,
                DeterministicGenerationContract,
                out geography,
                out error);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only contract probe used to prove pass-version isolation. New
        /// Game always uses <see cref="DeterministicGenerationContract"/>.
        /// </summary>
        public static bool TryGenerateForPassContractDiagnostics(
            WorldGenerationContext context,
            MacroWorldPlan macroWorldPlan,
            string passGenerationContract,
            out MacroGeographyPlan geography,
            out string error)
        {
            return TryGenerateWithPassContract(
                context,
                macroWorldPlan,
                passGenerationContract,
                out geography,
                out error);
        }
#endif

        private static bool TryGenerateWithPassContract(
            WorldGenerationContext context,
            MacroWorldPlan macroWorldPlan,
            string passGenerationContract,
            out MacroGeographyPlan geography,
            out string error)
        {
            geography = null;
            error = null;
            if (context == null)
            {
                error = "Macro geography generation requires a WorldGenerationContext";
                return false;
            }
            if (macroWorldPlan == null)
            {
                error = "Macro geography generation requires a validated MacroWorldPlan";
                return false;
            }

            var attemptFailures = new List<string>();
            for (int attempt = 0; attempt < MacroGeographyGenerationSettings.MaximumAttempts; attempt++)
            {
                MacroGeographyGenerationSettings settings =
                    MacroGeographyGenerationSettings.ResolvePreset(
                        macroWorldPlan.GenerationSettings.WorldSizePreset, attempt);
                try
                {
                    GenerateSamples(
                        context.WorldSeed,
                        passGenerationContract,
                        macroWorldPlan.GenerationSettings,
                        settings,
                        out ushort[] elevations,
                        out byte[] landforms);
                    if (MacroGeographyPlan.TryCreate(
                            settings,
                            macroWorldPlan.WorldBounds,
                            elevations,
                            landforms,
                            out geography,
                            out string validationError))
                    {
                        return true;
                    }
                    attemptFailures.Add("attempt " + attempt.ToString(CultureInfo.InvariantCulture) +
                                        ": " + validationError);
                }
                catch (Exception exception) when (
                    exception is ArgumentException || exception is FormatException ||
                    exception is InvalidOperationException || exception is OverflowException)
                {
                    attemptFailures.Add("attempt " + attempt.ToString(CultureInfo.InvariantCulture) +
                                        " threw " + exception.GetType().Name + ": " + exception.Message);
                }
            }

            error = "Macro geography exhausted its bounded deterministic attempts: " +
                    string.Join(" | ", attemptFailures);
            return false;
        }

        private static void GenerateSamples(
            WorldSeed worldSeed,
            string passGenerationContract,
            WorldGenerationSettings worldSettings,
            MacroGeographyGenerationSettings settings,
            out ushort[] elevations,
            out byte[] landforms)
        {
            int sampleCount = checked(settings.SampleColumns * settings.SampleRows);
            var regionalScores = new int[sampleCount];
            var reliefPercentiles = new int[sampleCount];
            elevations = new ushort[sampleCount];
            landforms = new byte[sampleCount];

            string scope = "macro_" + WorldGenerationSettings.ToCanonical(worldSettings.WorldSizePreset) +
                           "_" + settings.DeterministicKey;
            ulong landformSeed = DeriveNumericSeed(
                worldSeed, passGenerationContract, scope, "landform_regions");
            ulong upheavalSeed = DeriveNumericSeed(
                worldSeed, passGenerationContract, scope, "regional_upheaval");
            ulong baseElevationSeed = DeriveNumericSeed(
                worldSeed, passGenerationContract, scope, "base_elevation");
            ulong detailSeed = DeriveNumericSeed(
                worldSeed, passGenerationContract, scope, "relief_detail");
            ulong ridgeSeed = DeriveNumericSeed(
                worldSeed, passGenerationContract, scope, "mountain_ridges");
            ulong roughnessSeed = DeriveNumericSeed(
                worldSeed, passGenerationContract, scope, "surface_roughness");

            for (int y = 0; y < settings.SampleRows; y++)
            {
                int normalizedYQ16 = NormalizeIndexQ16(y, settings.SampleRows);
                for (int x = 0; x < settings.SampleColumns; x++)
                {
                    int normalizedXQ16 = NormalizeIndexQ16(x, settings.SampleColumns);
                    int regionalX = MultiplyQ16(normalizedXQ16, settings.RegionalFrequencyQ16);
                    int regionalY = MultiplyQ16(normalizedYQ16, settings.RegionalFrequencyQ16);
                    int broadX = regionalX / 2;
                    int broadY = regionalY / 2;
                    int broadRegion = StableValueNoise2D.SampleFbmQ16(
                        landformSeed, regionalX, regionalY, 2);
                    int upheaval = StableValueNoise2D.SampleFbmQ16(
                        upheavalSeed, broadX, broadY, 2);
                    int ridgeMask = StableValueNoise2D.RidgedQ16(upheaval);
                    int index = y * settings.SampleColumns + x;
                    regionalScores[index] =
                        (broadRegion * 3 + upheaval + ridgeMask) / 5;
                }
            }

            AssignLandformsAndPercentiles(regionalScores, landforms, reliefPercentiles);

            for (int y = 0; y < settings.SampleRows; y++)
            {
                int normalizedYQ16 = NormalizeIndexQ16(y, settings.SampleRows);
                for (int x = 0; x < settings.SampleColumns; x++)
                {
                    int normalizedXQ16 = NormalizeIndexQ16(x, settings.SampleColumns);
                    int index = y * settings.SampleColumns + x;
                    int relief = reliefPercentiles[index];

                    int baseX = MultiplyQ16(normalizedXQ16, settings.BaseElevationFrequencyQ16);
                    int baseY = MultiplyQ16(normalizedYQ16, settings.BaseElevationFrequencyQ16);
                    int detailX = MultiplyQ16(normalizedXQ16, settings.DetailFrequencyQ16);
                    int detailY = MultiplyQ16(normalizedYQ16, settings.DetailFrequencyQ16);
                    int roughX = MultiplyQ16(normalizedXQ16, settings.RoughnessFrequencyQ16);
                    int roughY = MultiplyQ16(normalizedYQ16, settings.RoughnessFrequencyQ16);

                    int baseField = StableValueNoise2D.SampleFbmQ16(
                        baseElevationSeed, baseX, baseY, 2);
                    int detailField = StableValueNoise2D.SampleFbmQ16(
                        detailSeed, detailX, detailY, 3);
                    int ridgeField = StableValueNoise2D.RidgedQ16(
                        StableValueNoise2D.SampleFbmQ16(ridgeSeed, detailX / 2, detailY / 2, 2));
                    int roughnessField = StableValueNoise2D.SampleFbmQ16(
                        roughnessSeed, roughX, roughY, 2);

                    int baseElevation = 9000 + ScaleQ16(baseField, 18000) + ScaleQ16(relief, 11000);
                    int detailAmplitude = 650 + ScaleQ16(relief, 6800);
                    int detailContribution = (detailField - 32768) * detailAmplitude / 32768;
                    int roughnessAmplitude = 220 + ScaleQ16(relief, 3000);
                    int roughnessContribution =
                        (roughnessField - 32768) * roughnessAmplitude / 32768;
                    int mountainMask = SmoothStepQ16(45500, 61500, relief);
                    int squaredRidge = MultiplyQ16(ridgeField, ridgeField);
                    int ridgeContribution = ScaleQ16(
                        MultiplyQ16(squaredRidge, mountainMask), 24000);

                    int elevation = baseElevation + detailContribution +
                                    roughnessContribution + ridgeContribution;
                    elevations[index] = (ushort)Math.Max(0, Math.Min(ushort.MaxValue, elevation));
                }
            }
        }

        private static void AssignLandformsAndPercentiles(
            int[] regionalScores,
            byte[] landforms,
            int[] reliefPercentiles)
        {
            var ranked = new RankedSample[regionalScores.Length];
            for (int index = 0; index < ranked.Length; index++)
                ranked[index] = new RankedSample(regionalScores[index], index);
            Array.Sort(ranked, RankedSampleComparer.Instance);

            int plainsEnd = ranked.Length * 34 / 100;
            int rollingEnd = ranked.Length * 66 / 100;
            int highlandsEnd = ranked.Length * 88 / 100;
            int denominator = Math.Max(1, ranked.Length - 1);
            for (int rank = 0; rank < ranked.Length; rank++)
            {
                int index = ranked[rank].Index;
                reliefPercentiles[index] = (int)((long)rank * 65535 / denominator);
                landforms[index] = rank < plainsEnd
                    ? (byte)MacroLandform.Plains
                    : rank < rollingEnd
                        ? (byte)MacroLandform.RollingHills
                        : rank < highlandsEnd
                            ? (byte)MacroLandform.Highlands
                            : (byte)MacroLandform.Mountains;
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

        private static int NormalizeIndexQ16(int index, int sampleCount)
        {
            return (int)((long)index * 65536 / (sampleCount - 1));
        }

        private static int MultiplyQ16(int first, int second)
        {
            return (int)(((long)first * second + 32768) >> 16);
        }

        private static int ScaleQ16(int normalized, int scale)
        {
            return (int)(((long)normalized * scale + 32767) / 65535);
        }

        private static int SmoothStepQ16(int lower, int upper, int value)
        {
            if (value <= lower) return 0;
            if (value >= upper) return 65535;
            int t = (int)((long)(value - lower) * 65535 / (upper - lower));
            int tSquared = MultiplyQ16(t, t);
            return MultiplyQ16(tSquared, 196608 - t * 2);
        }

        private readonly struct RankedSample
        {
            internal RankedSample(int score, int index)
            {
                Score = score;
                Index = index;
            }

            internal int Score { get; }
            internal int Index { get; }
        }

        private sealed class RankedSampleComparer : IComparer<RankedSample>
        {
            internal static readonly RankedSampleComparer Instance = new RankedSampleComparer();

            public int Compare(RankedSample left, RankedSample right)
            {
                int score = left.Score.CompareTo(right.Score);
                return score != 0 ? score : left.Index.CompareTo(right.Index);
            }
        }
    }

    internal static class StableValueNoise2D
    {
        private const int OneQ16 = 65536;

        internal static int SampleFbmQ16(ulong seed, int xQ16, int yQ16, int octaves)
        {
            int total = 0;
            int totalWeight = 0;
            int weight = 1 << Math.Max(0, octaves - 1);
            int x = xQ16;
            int y = yQ16;
            for (int octave = 0; octave < octaves; octave++)
            {
                total += SampleQ16(seed + (ulong)octave * 0x9e3779b97f4a7c15UL, x, y) * weight;
                totalWeight += weight;
                weight = Math.Max(1, weight / 2);
                x = checked(x * 2);
                y = checked(y * 2);
            }
            return total / totalWeight;
        }

        internal static int RidgedQ16(int valueQ16)
        {
            int centered = Math.Abs(valueQ16 * 2 - 65535);
            return 65535 - Math.Min(65535, centered);
        }

        private static int SampleQ16(ulong seed, int xQ16, int yQ16)
        {
            int latticeX = FloorToLattice(xQ16);
            int latticeY = FloorToLattice(yQ16);
            int fractionX = xQ16 - latticeX * OneQ16;
            int fractionY = yQ16 - latticeY * OneQ16;
            int fadeX = FadeQ16(fractionX);
            int fadeY = FadeQ16(fractionY);

            int lower = LerpQ16(
                LatticeValue(seed, latticeX, latticeY),
                LatticeValue(seed, latticeX + 1, latticeY),
                fadeX);
            int upper = LerpQ16(
                LatticeValue(seed, latticeX, latticeY + 1),
                LatticeValue(seed, latticeX + 1, latticeY + 1),
                fadeX);
            return LerpQ16(lower, upper, fadeY);
        }

        private static int FloorToLattice(int fixedPoint)
        {
            if (fixedPoint >= 0)
                return fixedPoint / OneQ16;
            return -((-fixedPoint + OneQ16 - 1) / OneQ16);
        }

        private static int FadeQ16(int value)
        {
            int squared = MultiplyQ16(value, value);
            return MultiplyQ16(squared, 196608 - value * 2);
        }

        private static int LerpQ16(int first, int second, int amount)
        {
            long scaled = (long)(second - first) * amount;
            long rounded = scaled >= 0 ? scaled + 32768 : scaled - 32768;
            return first + (int)(rounded / OneQ16);
        }

        private static int MultiplyQ16(int first, int second)
        {
            return (int)(((long)first * second + 32768) >> 16);
        }

        private static int LatticeValue(ulong seed, int x, int y)
        {
            unchecked
            {
                ulong value = seed;
                value ^= (ulong)(long)x * 0x9e3779b185ebca87UL;
                value ^= (ulong)(long)y * 0xc2b2ae3d27d4eb4fUL;
                value += 0x165667b19e3779f9UL;
                value = (value ^ (value >> 30)) * 0xbf58476d1ce4e5b9UL;
                value = (value ^ (value >> 27)) * 0x94d049bb133111ebUL;
                value ^= value >> 31;
                return (int)(value >> 48);
            }
        }
    }
}
