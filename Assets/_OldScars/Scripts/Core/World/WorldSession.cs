using System;

namespace OldScars.Core.World
{
    /// <summary>
    /// Immutable logical state required to identify and reopen one world. It is
    /// independent of scene objects and filesystem layout.
    /// </summary>
    public sealed class WorldSession
    {
        public const int MaximumDisplayNameLength = 80;

        private WorldSession(
            WorldId worldId,
            string displayName,
            WorldGenerationContext generationContext,
            WorldTopology topology,
            SectorId activeSectorId,
            WorldCreationContentEvidence creationContentEvidence)
        {
            WorldId = worldId;
            DisplayName = displayName;
            GenerationContext = generationContext;
            Topology = topology;
            ActiveSectorId = activeSectorId;
            CreationContentEvidence = creationContentEvidence;
        }

        public WorldId WorldId { get; }
        public string DisplayName { get; }
        public WorldGenerationContext GenerationContext { get; }
        public WorldTopology Topology { get; }
        public SectorId ActiveSectorId { get; }
        public WorldCreationContentEvidence CreationContentEvidence { get; }

        internal static bool TryCreate(
            WorldId worldId,
            string displayName,
            WorldGenerationContext generationContext,
            WorldTopology topology,
            SectorId activeSectorId,
            WorldCreationContentEvidence creationContentEvidence,
            out WorldSession session,
            out string error)
        {
            session = null;
            error = null;
            if (!worldId.IsValid)
            {
                error = "WorldSession requires a valid WorldId";
                return false;
            }
            if (!TryValidateDisplayName(displayName, out error))
                return false;
            if (generationContext == null || !generationContext.GeneratorVersion.IsValid)
            {
                error = "WorldSession requires a valid WorldGenerationContext";
                return false;
            }
            if (topology == null)
            {
                error = "WorldSession requires a validated WorldTopology";
                return false;
            }
            if (!activeSectorId.IsValid)
            {
                error = "WorldSession requires a valid active SectorId";
                return false;
            }
            bool activeSectorExists = false;
            for (int index = 0; index < topology.Sectors.Count; index++)
            {
                if (topology.Sectors[index] == activeSectorId)
                {
                    activeSectorExists = true;
                    break;
                }
            }
            if (!activeSectorExists)
            {
                error = $"Active SectorId '{activeSectorId.Canonical}' does not exist in the committed topology";
                return false;
            }
            if (creationContentEvidence == null)
            {
                error = "WorldSession requires creation content provenance evidence";
                return false;
            }

            session = new WorldSession(
                worldId,
                displayName,
                generationContext,
                topology,
                activeSectorId,
                creationContentEvidence);
            return true;
        }

        internal static bool TryNormalizeDisplayName(string raw, out string normalized, out string error)
        {
            normalized = raw?.Trim();
            return TryValidateDisplayName(normalized, out error);
        }

        private static bool TryValidateDisplayName(string value, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                error = "World display name is required";
                return false;
            }
            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                error = "World display name must not contain leading or trailing whitespace";
                return false;
            }
            if (value.Length > MaximumDisplayNameLength)
            {
                error = $"World display name exceeds {MaximumDisplayNameLength} characters";
                return false;
            }
            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsControl(value[index]))
                {
                    error = $"World display name contains a control character at position {index}";
                    return false;
                }
            }
            return true;
        }
    }
}
