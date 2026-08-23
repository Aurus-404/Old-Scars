using System;
using System.Security.Cryptography;
using OldScars.Core.Data.Loading;

namespace OldScars.Core.World
{
    /// <summary>
    /// Replaceable minimum bootstrap used before macro world planning exists.
    /// It creates exactly one deterministic starter sector and no geography.
    /// </summary>
    public static class WorldSessionBootstrap
    {
        public const string CurrentGeneratorVersion = "bootstrap_v1";

        public static bool TryBuildNew(
            string displayName,
            WorldSeed worldSeed,
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
            if (!WorldSession.TryNormalizeDisplayName(displayName, out string normalizedName, out error))
                return false;

            try
            {
                var context = new WorldGenerationContext(
                    worldSeed,
                    GeneratorVersion.Parse(CurrentGeneratorVersion));
                SectorId starterSector = SectorId.FromDeterministicDomain(
                    WorldDeterminism.DeriveDomainKey(context, "topology", "starter_sector"));
                if (!WorldTopology.TryCreate(
                        new[] { starterSector },
                        Array.Empty<SectorConnection>(),
                        out WorldTopology topology,
                        out WorldTopologyValidationResult topologyValidation))
                {
                    error = "Minimum bootstrap topology failed validation: " + topologyValidation.Description;
                    return false;
                }

                WorldCreationContentEvidence evidence = WorldCreationContentEvidence.Capture(loadedContentSet);
                return WorldSession.TryCreate(
                    WorldId.CreateNew(),
                    normalizedName,
                    context,
                    topology,
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
