using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using OldScars.Core.World;

namespace OldScars.Core.Persistence
{
    public sealed class WorldSaveCatalogEntry
    {
        internal WorldSaveCatalogEntry(WorldSession session)
        {
            SlotId = session.WorldId.Canonical;
            WorldId = session.WorldId;
            DisplayName = session.DisplayName;
            WorldSeed = session.GenerationContext.WorldSeed;
            GeneratorVersion = session.GenerationContext.GeneratorVersion;
            HasMacroWorldPlan = session.HasMacroWorldPlan;
            SizePreset = session.HasMacroWorldPlan
                ? session.MacroWorldPlan.GenerationSettings.WorldSizePreset
                : (WorldSizePreset?)null;
            MacroWorldPlanHash = session.HasMacroWorldPlan
                ? session.MacroWorldPlan.CanonicalHash
                : null;
            HasMacroGeography = session.HasMacroGeography;
            MacroGeographyHash = session.HasMacroGeography
                ? session.MacroGeography.CanonicalHash
                : null;
            HasMacroWater = session.HasMacroWater;
            LandCoverage = session.HasMacroWater
                ? session.MacroWater.GenerationSettings.LandCoverage
                : (LandCoveragePreset?)null;
            MacroWaterHash = session.HasMacroWater
                ? session.MacroWater.CanonicalHash
                : null;
            TopologyHash = session.Topology.CanonicalHash;
            ActiveSectorId = session.ActiveSectorId;
            CreationContentProvenanceFingerprint =
                session.CreationContentEvidence.LoadedContentSetFingerprint;
        }

        public string SlotId { get; }
        public WorldId WorldId { get; }
        public string DisplayName { get; }
        public WorldSeed WorldSeed { get; }
        public GeneratorVersion GeneratorVersion { get; }
        public bool HasMacroWorldPlan { get; }
        public WorldSizePreset? SizePreset { get; }
        public string MacroWorldPlanHash { get; }
        public bool HasMacroGeography { get; }
        public string MacroGeographyHash { get; }
        public bool HasMacroWater { get; }
        public LandCoveragePreset? LandCoverage { get; }
        public string MacroWaterHash { get; }
        public string TopologyHash { get; }
        public SectorId ActiveSectorId { get; }
        public string CreationContentProvenanceFingerprint { get; }
    }

    public sealed class WorldSaveCatalogIssue
    {
        internal WorldSaveCatalogIssue(string slotId, string failure)
        {
            SlotId = slotId;
            Failure = failure;
        }

        public string SlotId { get; }
        public string Failure { get; }
    }

    public sealed class WorldSaveCatalogResult
    {
        internal WorldSaveCatalogResult(
            IList<WorldSaveCatalogEntry> entries,
            IList<WorldSaveCatalogIssue> issues,
            string discoveryFailure)
        {
            Entries = new ReadOnlyCollection<WorldSaveCatalogEntry>(
                new List<WorldSaveCatalogEntry>(entries));
            Issues = new ReadOnlyCollection<WorldSaveCatalogIssue>(
                new List<WorldSaveCatalogIssue>(issues));
            DiscoveryFailure = discoveryFailure;
        }

        public IReadOnlyList<WorldSaveCatalogEntry> Entries { get; }
        public IReadOnlyList<WorldSaveCatalogIssue> Issues { get; }
        public string DiscoveryFailure { get; }
        public bool Success => string.IsNullOrEmpty(DiscoveryFailure);
    }

    /// <summary>
    /// Read-only semantic view over primary world_session_v1 slots. The existing
    /// PersistenceFileStore remains the sole authority for save paths and reads.
    /// </summary>
    public static class WorldSaveCatalog
    {
        public static WorldSaveCatalogResult Discover(PersistenceFileStore store = null)
        {
            PersistenceFileStore activeStore = store ?? new PersistenceFileStore();
            var entries = new List<WorldSaveCatalogEntry>();
            var issues = new List<WorldSaveCatalogIssue>();
            if (!activeStore.TryEnumeratePrimarySlotIds(out IReadOnlyList<string> slotIds, out string failure))
                return new WorldSaveCatalogResult(entries, issues, failure);

            for (int index = 0; index < slotIds.Count; index++)
            {
                string slotId = slotIds[index];
                if (!WorldId.TryParse(slotId, out _, out _))
                    continue;

                WorldSessionPersistenceResult read = WorldSessionPersistenceService.Read(slotId, activeStore);
                if (!read.Success)
                {
                    issues.Add(new WorldSaveCatalogIssue(
                        slotId,
                        $"{read.Phase}: {read.FailureCode}: {read.Failure}"));
                    continue;
                }
                entries.Add(new WorldSaveCatalogEntry(read.Session));
            }

            entries.Sort((left, right) =>
            {
                int displayName = string.CompareOrdinal(left.DisplayName, right.DisplayName);
                return displayName != 0
                    ? displayName
                    : string.CompareOrdinal(left.SlotId, right.SlotId);
            });
            return new WorldSaveCatalogResult(entries, issues, null);
        }
    }
}
