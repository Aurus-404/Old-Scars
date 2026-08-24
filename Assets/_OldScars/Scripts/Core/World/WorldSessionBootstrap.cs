using System;
using System.Security.Cryptography;
using OldScars.Core.Data.Loading;

namespace OldScars.Core.World
{
    /// <summary>
    /// New Game composition boundary. Macro World Plan V1 is the current plan
    /// producer and remains replaceable by later versioned macro passes.
    /// </summary>
    public static class WorldSessionBootstrap
    {
        public const string CurrentGeneratorVersion = "macro_geography_v1";
        public const string LegacyGeneratorVersion = "bootstrap_v1";
        public const string LegacyMacroPlanGeneratorVersion = "macro_plan_v1";

        public static bool TryBuildNew(
            string displayName,
            WorldSeed worldSeed,
            WorldGenerationSettings generationSettings,
            LoadedContentSet loadedContentSet,
            out WorldSession session,
            out string error)
        {
            session = null;
            error = null;
            if (loadedContentSet == null)
            {
                error = "New Game requires a validated LoadedContentSet";
                return false;
            }
            if (generationSettings == null)
            {
                error = "New Game requires validated WorldGenerationSettings";
                return false;
            }
            if (!WorldSession.TryNormalizeDisplayName(displayName, out string normalizedName, out error))
                return false;

            try
            {
                var context = new WorldGenerationContext(
                    worldSeed,
                    GeneratorVersion.Parse(CurrentGeneratorVersion));
                if (!MacroWorldPlanGenerator.TryGenerate(
                        context, generationSettings, out MacroWorldPlan macroWorldPlan, out string planError))
                {
                    error = planError;
                    return false;
                }
                if (!MacroGeographyGenerator.TryGenerate(
                        context, macroWorldPlan, out MacroGeographyPlan macroGeography,
                        out string geographyError))
                {
                    error = geographyError;
                    return false;
                }
                SectorId starterSector = macroWorldPlan.FindCentralSectorId();

                WorldCreationContentEvidence evidence = WorldCreationContentEvidence.Capture(loadedContentSet);
                return WorldSession.TryCreate(
                    WorldId.CreateNew(),
                    normalizedName,
                    context,
                    macroWorldPlan,
                    macroGeography,
                    starterSector,
                    evidence,
                    out session,
                    out error);
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is FormatException || exception is CryptographicException)
            {
                error = $"New Game bootstrap failed: {exception.Message}";
                return false;
            }
        }

        public static WorldSeed CreateRandomSeed()
        {
            var bytes = new byte[sizeof(long)];
            using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
                generator.GetBytes(bytes);

            ulong unsigned = 0;
            for (int index = 0; index < bytes.Length; index++)
                unsigned = (unsigned << 8) | bytes[index];
            return new WorldSeed(unchecked((long)unsigned));
        }
    }
}
