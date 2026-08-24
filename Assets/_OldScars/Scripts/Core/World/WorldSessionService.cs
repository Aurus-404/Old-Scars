using OldScars.Core.Data.Loading;
using OldScars.Core.Persistence;
using UnityEngine;

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
            if (ActiveSession != null)
                return Fail(WorldSessionOperationFailureCode.ActiveSessionAlreadyExists, "Create",
                    "Close the active WorldSession before creating another world.");

            if (!WorldSessionBootstrap.TryBuildNew(
                    displayName, worldSeed, generationSettings, loadedContentSet,
                    out WorldSession candidate, out string buildFailure))
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
            return Success("Load", ActiveSession);
        }

        public static WorldSessionOperationResult Save(PersistenceFileStore store = null)
        {
            if (ActiveSession == null)
                return Fail(WorldSessionOperationFailureCode.NoActiveSession, "Save",
                    "No WorldSession is active.");

            WorldSessionPersistenceResult save = WorldSessionPersistenceService.Save(ActiveSession, store);
            return save.Success
                ? Success("Save", ActiveSession)
                : Fail(WorldSessionOperationFailureCode.WriteFailed, save.Phase,
                    $"{save.FailureCode}: {save.Failure}");
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
}
