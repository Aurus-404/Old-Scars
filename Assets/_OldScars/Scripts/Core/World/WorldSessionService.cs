using System.Globalization;
using System.Text;
using OldScars.Core.Data.Loading;
using OldScars.Core.Persistence;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace OldScars.Core.World
{
    public enum WorldSessionOperationFailureCode
    {
        Success,
        ActiveSessionAlreadyExists,
        NoActiveSession,
        InvalidInput,
        ReadFailed,
        SemanticPreflightFailed,
        WriteFailed
    }

    public sealed class WorldSessionOperationResult
    {
        internal WorldSessionOperationResult(
            WorldSessionOperationFailureCode failureCode,
            string phase,
            string failure,
            WorldSession session)
        {
            FailureCode = failureCode;
            Phase = phase;
            Failure = failure;
            Session = session;
        }

        public bool Success => FailureCode == WorldSessionOperationFailureCode.Success;
        public WorldSessionOperationFailureCode FailureCode { get; }
        public string Phase { get; }
        public string Failure { get; }
        public WorldSession Session { get; }
    }

    /// <summary>
    /// Single logical authority for the currently opened world. It owns only
    /// lifecycle publication; persistence IO remains in PersistenceFileStore.
    /// </summary>
    public static class WorldSessionService
    {
        public static WorldSession ActiveSession { get; private set; }
        public static bool HasActiveSession => ActiveSession != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            ActiveSession = null;
        }

        public static WorldSessionOperationResult Create(
            string displayName,
            WorldSeed worldSeed,
            WorldGenerationSettings generationSettings,
            LoadedContentSet loadedContentSet,
            PersistenceFileStore store = null)
        {
            return Create(
                displayName, worldSeed, generationSettings, LandCoveragePreset.High,
                loadedContentSet, store);
        }

        public static WorldSessionOperationResult Create(
            string displayName,
            WorldSeed worldSeed,
            WorldGenerationSettings generationSettings,
            LandCoveragePreset landCoverage,
            LoadedContentSet loadedContentSet,
            PersistenceFileStore store = null)
        {
            if (ActiveSession != null)
                return Fail(WorldSessionOperationFailureCode.ActiveSessionAlreadyExists, "Create",
                    "Close the active WorldSession before creating another world.");

            Stopwatch generationStopwatch = Stopwatch.StartNew();
            bool built = WorldSessionBootstrap.TryBuildNew(
                    displayName, worldSeed, generationSettings, landCoverage, loadedContentSet,
                    out WorldSession candidate, out string buildFailure);
            generationStopwatch.Stop();
            if (!built)
            {
                return Fail(WorldSessionOperationFailureCode.InvalidInput, "Bootstrap", buildFailure);
            }

            WorldSessionPersistenceResult save = WorldSessionPersistenceService.Save(candidate, store);
            if (!save.Success)
            {
                return Fail(WorldSessionOperationFailureCode.WriteFailed, "InitialSave",
                    $"{save.FailureCode}: {save.Failure}");
            }

            ActiveSession = candidate;
            WorldSessionObservability.LogWorldCreated(
                candidate, generationStopwatch.ElapsedMilliseconds);
            return Success("Create", candidate);
        }

        public static WorldSessionOperationResult Load(
            string slotId,
            PersistenceFileStore store = null)
        {
            if (ActiveSession != null)
                return Fail(WorldSessionOperationFailureCode.ActiveSessionAlreadyExists, "Load",
                    "Close the active WorldSession before loading another world.");

            WorldSessionPersistenceResult load = WorldSessionPersistenceService.Read(slotId, store);
            if (!load.Success)
            {
                WorldSessionOperationFailureCode code = load.FailureCode == WorldSessionPersistenceFailureCode.SemanticPreflightFailed
                    ? WorldSessionOperationFailureCode.SemanticPreflightFailed
                    : WorldSessionOperationFailureCode.ReadFailed;
                return Fail(code, load.Phase, $"{load.FailureCode}: {load.Failure}");
            }

            ActiveSession = load.Session;
            WorldSessionObservability.LogLoadOk(ActiveSession);
            return Success("Load", ActiveSession);
        }

        public static WorldSessionOperationResult Save(PersistenceFileStore store = null)
        {
            if (ActiveSession == null)
                return Fail(WorldSessionOperationFailureCode.NoActiveSession, "Save",
                    "No WorldSession is active.");

            WorldSessionPersistenceResult save = WorldSessionPersistenceService.Save(ActiveSession, store);
            if (!save.Success)
            {
                return Fail(WorldSessionOperationFailureCode.WriteFailed, save.Phase,
                    $"{save.FailureCode}: {save.Failure}");
            }

            WorldSessionObservability.LogSaveOk(ActiveSession);
            return Success("Save", ActiveSession);
        }

        public static void Close()
        {
            ActiveSession = null;
        }

        private static WorldSessionOperationResult Success(string phase, WorldSession session)
        {
            return new WorldSessionOperationResult(
                WorldSessionOperationFailureCode.Success, phase, null, session);
        }

        private static WorldSessionOperationResult Fail(
            WorldSessionOperationFailureCode code,
            string phase,
            string failure)
        {
            return new WorldSessionOperationResult(code, phase, failure, null);
        }
    }

    /// <summary>
    /// Formats lifecycle-boundary evidence from already committed WorldSession
    /// truth. It never generates, validates, mutates, or continuously samples a
    /// world; callers decide the single lifecycle moment at which a log occurs.
    /// </summary>
    internal static class WorldSessionObservability
    {
        public static void LogWorldCreated(WorldSession session, long generationElapsedMilliseconds)
        {
            if (session == null)
                return;

            bool hasGeography = TryGetActiveGeography(session, out MacroGeographySample geography);
            bool hasWater = TryGetActiveWater(session, out MacroWaterSample water);
            var builder = new StringBuilder(768);
            builder.Append("[Worldgen][WORLD_CREATED]\n")
                .Append("WorldId: ").Append(session.WorldId.Canonical).Append('\n')
                .Append("Seed: ").Append(session.GenerationContext.WorldSeed.Canonical).Append('\n')
                .Append("PipelineVersion: ").Append(session.GenerationContext.GeneratorVersion.Canonical).Append('\n')
                .Append("WorldSize: ").Append(session.MacroWorldPlan.GenerationSettings.WorldSizePreset).Append('\n')
                .Append("LandCoverage: ").Append(session.MacroWater.GenerationSettings.LandCoverage).Append('\n')
                .Append("MacroWorldPlanContract: ").Append(MacroWorldPlanGenerator.DeterministicGenerationContract).Append('\n')
                .Append("MacroWorldPlanHash: ").Append(session.MacroWorldPlan.CanonicalHash).Append('\n')
                .Append("SectorCount: ").Append(session.MacroWorldPlan.SectorPlacements.Count.ToString(CultureInfo.InvariantCulture)).Append('\n')
                .Append("MacroGeographyContract: ").Append(MacroGeographyGenerator.DeterministicGenerationContract).Append('\n')
                .Append("MacroGeographyHash: ").Append(session.MacroGeography.CanonicalHash).Append('\n')
                .Append("MacroWaterContract: ").Append(session.MacroWater.GenerationSettings.GenerationContract).Append('\n')
                .Append("MacroWaterHash: ").Append(session.MacroWater.CanonicalHash).Append('\n')
                .Append("SeaLevel: ").Append(session.MacroWater.SeaLevel.ToString(CultureInfo.InvariantCulture)).Append("/65535\n")
                .Append("ActiveSector: ").Append(session.ActiveSectorId.Canonical).Append('\n')
                .Append("StarterLandform: ").Append(hasGeography ? geography.Landform.ToString() : "<UNAVAILABLE>").Append('\n')
                .Append("StarterElevation: ").Append(hasGeography
                    ? geography.Elevation.ToString(CultureInfo.InvariantCulture) + "/65535"
                    : "<UNAVAILABLE>").Append('\n')
                .Append("StarterSurface: ").Append(hasWater ? DescribeSurface(water) : "<UNAVAILABLE>").Append('\n')
                .Append("SuitableStarterCandidates: ")
                .Append(session.GameplayQuality.SuitableStarterCandidateCount.ToString(CultureInfo.InvariantCulture))
                .Append('/')
                .Append(session.GameplayQuality.StarterCandidates.Count.ToString(CultureInfo.InvariantCulture)).Append('\n')
                .Append("GenerationElapsedMs: ")
                .Append(generationElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
            Debug.Log(builder.ToString());
        }

        public static void LogLoadOk(WorldSession session)
        {
            if (session == null)
                return;

            Debug.Log(
                "[WorldSession][LOAD_OK]\n" +
                "WorldId: " + session.WorldId.Canonical + "\n" +
                "SchemaVersion: " + SchemaVersion(session).ToString(CultureInfo.InvariantCulture) + "\n" +
                "Seed: " + session.GenerationContext.WorldSeed.Canonical + "\n" +
                "PipelineVersion: " + session.GenerationContext.GeneratorVersion.Canonical + "\n" +
                "MacroWorldPlanHash: " + HashOrAbsent(session.MacroWorldPlan?.CanonicalHash) + "\n" +
                "MacroGeographyHash: " + HashOrAbsent(session.MacroGeography?.CanonicalHash) + "\n" +
                "MacroWaterHash: " + HashOrAbsent(session.MacroWater?.CanonicalHash) + "\n" +
                "ActiveSector: " + session.ActiveSectorId.Canonical + "\n" +
                "LegacyState: " + DescribeLegacyState(session));
        }

        public static void LogSaveOk(WorldSession session)
        {
            if (session == null)
                return;

            Debug.Log(
                "[WorldSession][SAVE_OK]\n" +
                "WorldId: " + session.WorldId.Canonical + "\n" +
                "SchemaVersion: " + SchemaVersion(session).ToString(CultureInfo.InvariantCulture) + "\n" +
                "ActiveSector: " + session.ActiveSectorId.Canonical);
        }

        public static void LogRuntimeReady(WorldSession session)
        {
            if (session == null)
                return;

            bool hasGeography = TryGetActiveGeography(session, out MacroGeographySample geography);
            bool hasWater = TryGetActiveWater(session, out MacroWaterSample water);
            Debug.Log(
                "[WorldRuntime][SESSION_READY]\n" +
                "WorldId: " + session.WorldId.Canonical + "\n" +
                "ActiveSector: " + session.ActiveSectorId.Canonical + "\n" +
                "WorldSize: " + (session.HasMacroWorldPlan
                    ? session.MacroWorldPlan.GenerationSettings.WorldSizePreset.ToString()
                    : "<ABSENT: legacy schema 1>") + "\n" +
                "LandCoverage: " + (session.HasMacroWater
                    ? session.MacroWater.GenerationSettings.LandCoverage.ToString()
                    : "<ABSENT: " + DescribeLegacyState(session) + ">") + "\n" +
                "Landform: " + (hasGeography ? geography.Landform.ToString() : "<ABSENT>") + "\n" +
                "Elevation: " + (hasGeography
                    ? geography.Elevation.ToString(CultureInfo.InvariantCulture) + "/65535"
                    : "<ABSENT>") + "\n" +
                "Surface: " + (hasWater ? DescribeSurface(water) : "<ABSENT>"));
        }

        private static bool TryGetActiveGeography(
            WorldSession session,
            out MacroGeographySample sample)
        {
            sample = default;
            return session != null && session.HasMacroWorldPlan && session.HasMacroGeography &&
                   session.MacroWorldPlan.TryGetSectorPlacement(
                       session.ActiveSectorId, out MacroSectorPlacement placement) &&
                   session.MacroGeography.TrySampleAt(placement.Position, out sample);
        }

        private static bool TryGetActiveWater(WorldSession session, out MacroWaterSample sample)
        {
            sample = default;
            if (session == null || !session.HasMacroWorldPlan || !session.HasMacroWater ||
                !session.MacroWorldPlan.TryGetSectorPlacement(
                    session.ActiveSectorId, out MacroSectorPlacement placement))
            {
                return false;
            }

            sample = session.MacroWater.SampleAt(placement.Position);
            return true;
        }

        private static int SchemaVersion(WorldSession session)
        {
            if (session.IsLegacySchemaV1)
                return WorldSessionPersistenceService.LegacySchemaVersion;
            if (session.IsLegacySchemaV2)
                return WorldSessionPersistenceService.MacroPlanSchemaVersion;
            if (session.IsLegacySchemaV3)
                return WorldSessionPersistenceService.MacroGeographySchemaVersion;
            return WorldSessionPersistenceService.CurrentSchemaVersion;
        }

        private static string DescribeLegacyState(WorldSession session)
        {
            if (session.IsLegacySchemaV1)
                return "schema 1; MacroWorldPlan/Geography/Water absent by contract";
            if (session.IsLegacySchemaV2)
                return "schema 2; MacroGeography/Water absent by contract";
            if (session.IsLegacySchemaV3)
                return "schema 3; MacroWater absent by contract";
            return "none (current schema)";
        }

        private static string HashOrAbsent(string hash) =>
            string.IsNullOrEmpty(hash) ? "<ABSENT>" : hash;

        private static string DescribeSurface(MacroWaterSample sample) =>
            sample.IsOcean ? "Ocean" : sample.IsCoastline ? "Coast" : "Land";
    }
}
