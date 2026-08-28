using System;

namespace OldScars.Core.World
{
    /// <summary>
    /// Deterministic creation-time ecological classification. It interprets
    /// committed Climate and Water without adding noise or runtime simulation.
    /// </summary>
    public static class MacroEnvironmentGenerator
    {
        public const string DeterministicGenerationContract = "macro_environment_v1";

        public static bool TryGenerate(
            WorldGenerationContext context,
            MacroClimatePlan climate,
            MacroWaterPlan water,
            out MacroEnvironmentPlan environment,
            out string error)
        {
            return TryGenerateWithContract(
                context,
                climate,
                water,
                DeterministicGenerationContract,
                out environment,
                out error);
        }

#if UNITY_EDITOR
        public static bool TryGenerateForPassContractDiagnostics(
            WorldGenerationContext context,
            MacroClimatePlan climate,
            MacroWaterPlan water,
            string passGenerationContract,
            out MacroEnvironmentPlan environment,
            out string error)
        {
            return TryGenerateWithContract(
                context,
                climate,
                water,
                passGenerationContract,
                out environment,
                out error);
        }
#endif

        private static bool TryGenerateWithContract(
            WorldGenerationContext context,
            MacroClimatePlan climate,
            MacroWaterPlan water,
            string passGenerationContract,
            out MacroEnvironmentPlan environment,
            out string error)
        {
            environment = null;
            error = null;
            if (context == null || !context.GeneratorVersion.IsValid ||
                climate == null || water == null)
            {
                error = "Macro Environment generation requires context plus committed Climate/Water";
                return false;
            }
            if (climate.WorldBounds != water.WorldBounds ||
                climate.SampleColumns != water.SampleColumns ||
                climate.SampleRows != water.SampleRows)
            {
                error = "Macro Environment inputs disagree on finite WorldBounds or macro grid";
                return false;
            }

            try
            {
                MacroEnvironmentGenerationSettings resolved =
                    MacroEnvironmentGenerationSettings.Resolve(climate);
                MacroEnvironmentGenerationSettings settings = resolved;
#if UNITY_EDITOR
                if (!string.Equals(
                        passGenerationContract,
                        DeterministicGenerationContract,
                        StringComparison.Ordinal))
                {
                    if (!MacroEnvironmentGenerationSettings.TryCreateForDiagnostics(
                            passGenerationContract,
                            resolved.SampleColumns,
                            resolved.SampleRows,
                            resolved.ThermalDistanceWeight,
                            resolved.MoistureDistanceWeight,
                            resolved.Profiles,
                            out settings,
                            out string diagnosticSettingsError))
                    {
                        error = diagnosticSettingsError;
                        return false;
                    }
                }
#else
                if (!string.Equals(
                        passGenerationContract,
                        DeterministicGenerationContract,
                        StringComparison.Ordinal))
                {
                    error = "unsupported Macro Environment generation contract";
                    return false;
                }
#endif

                GenerateSamples(
                    climate,
                    water,
                    settings,
                    out byte[] primary,
                    out byte[] secondary,
                    out ushort[] transition);
                return MacroEnvironmentPlan.TryCreate(
                    settings,
                    climate,
                    water,
                    primary,
                    secondary,
                    transition,
                    out environment,
                    out error);
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is InvalidOperationException ||
                exception is FormatException || exception is OverflowException)
            {
                error = "Macro Environment V1 generation failed: " + exception.Message;
                return false;
            }
        }

        private static void GenerateSamples(
            MacroClimatePlan climate,
            MacroWaterPlan water,
            MacroEnvironmentGenerationSettings settings,
            out byte[] primary,
            out byte[] secondary,
            out ushort[] transition)
        {
            int sampleCount = checked(settings.SampleColumns * settings.SampleRows);
            primary = new byte[sampleCount];
            secondary = new byte[sampleCount];
            transition = new ushort[sampleCount];

            for (int index = 0; index < sampleCount; index++)
            {
                int column = index % settings.SampleColumns;
                int row = index / settings.SampleColumns;
                if (water.SampleAt(column, row).IsOcean)
                {
                    primary[index] = (byte)MacroBiomeFamily.None;
                    secondary[index] = (byte)MacroBiomeFamily.None;
                    transition[index] = 0;
                    continue;
                }

                MacroClimateSample climateSample = climate.SampleAt(column, row);
                ResolveNearestProfiles(
                    climateSample,
                    settings,
                    out MacroBiomeFamily primaryFamily,
                    out MacroBiomeFamily secondaryFamily,
                    out ushort transitionQ16);
                primary[index] = (byte)primaryFamily;
                secondary[index] = (byte)secondaryFamily;
                transition[index] = transitionQ16;
            }
        }

        private static void ResolveNearestProfiles(
            MacroClimateSample sample,
            MacroEnvironmentGenerationSettings settings,
            out MacroBiomeFamily primary,
            out MacroBiomeFamily secondary,
            out ushort transition)
        {
            long bestDistance = long.MaxValue;
            long secondDistance = long.MaxValue;
            primary = MacroBiomeFamily.None;
            secondary = MacroBiomeFamily.None;

            for (int index = 0; index < settings.Profiles.Count; index++)
            {
                MacroBiomeProfile profile = settings.Profiles[index];
                long thermalDelta = (long)sample.ThermalIndex - profile.ThermalCenterQ16;
                long moistureDelta = (long)sample.MoistureIndex - profile.MoistureCenterQ16;
                long distance = checked(
                    thermalDelta * thermalDelta * settings.ThermalDistanceWeight +
                    moistureDelta * moistureDelta * settings.MoistureDistanceWeight);

                if (distance < bestDistance ||
                    distance == bestDistance && (byte)profile.Family < (byte)primary)
                {
                    secondDistance = bestDistance;
                    secondary = primary;
                    bestDistance = distance;
                    primary = profile.Family;
                }
                else if (distance < secondDistance ||
                         distance == secondDistance &&
                         (byte)profile.Family < (byte)secondary)
                {
                    secondDistance = distance;
                    secondary = profile.Family;
                }
            }

            if (primary == MacroBiomeFamily.None || secondary == MacroBiomeFamily.None ||
                primary == secondary || secondDistance <= 0 ||
                bestDistance < 0 || bestDistance > secondDistance)
            {
                throw new InvalidOperationException(
                    "ecological profile classification did not produce two valid ordered families");
            }

            long scaled = checked(bestDistance * ushort.MaxValue);
            long rounded = (scaled + secondDistance / 2) / secondDistance;
            transition = (ushort)Math.Max(0, Math.Min(ushort.MaxValue, rounded));
        }
    }
}
