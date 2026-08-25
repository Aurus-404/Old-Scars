using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using OldScars.Core.World;
using UnityEngine;

namespace OldScars.Core.Persistence
{
    public enum WorldGameplayLoadDisposition
    {
        Restored,
        AbsentLegacy,
        Failed
    }

    public sealed class WorldGameplayPreparedSave
    {
        internal WorldGameplayPreparedSave(
            WorldId worldId,
            SectorId sectorId,
            string slotId,
            CurrentSliceSaveData snapshot,
            JToken payload)
        {
            WorldId = worldId;
            SectorId = sectorId;
            SlotId = slotId;
            Snapshot = snapshot;
            Payload = payload;
        }

        public WorldId WorldId { get; }
        public SectorId SectorId { get; }
        public string SlotId { get; }
        public CurrentSliceSaveData Snapshot { get; }
        internal JToken Payload { get; }
    }

    public sealed class WorldGameplayPreparationResult
    {
        internal WorldGameplayPreparationResult(WorldGameplayPreparedSave prepared, string phase, string failure)
        {
            Prepared = prepared;
            Phase = phase;
            Failure = failure;
        }

        public bool Success => Prepared != null && string.IsNullOrEmpty(Failure);
        public WorldGameplayPreparedSave Prepared { get; }
        public string Phase { get; }
        public string Failure { get; }
    }

    public sealed class WorldGameplayCommitResult
    {
        internal WorldGameplayCommitResult(bool success, string phase, string failure, CurrentSliceSaveData snapshot)
        {
            Success = success;
            Phase = phase;
            Failure = failure;
            Snapshot = snapshot;
        }

        public bool Success { get; }
        public string Phase { get; }
        public string Failure { get; }
        public CurrentSliceSaveData Snapshot { get; }
    }

    public sealed class WorldGameplayLoadResult
    {
        internal WorldGameplayLoadResult(
            WorldGameplayLoadDisposition disposition,
            string phase,
            string failure,
            CurrentSliceSaveData snapshot,
            CurrentSliceLoadResult currentSliceResult)
        {
            Disposition = disposition;
            Phase = phase;
            Failure = failure;
            Snapshot = snapshot;
            CurrentSliceResult = currentSliceResult;
        }

        public bool Success => Disposition != WorldGameplayLoadDisposition.Failed;
        public WorldGameplayLoadDisposition Disposition { get; }
        public string Phase { get; }
        public string Failure { get; }
        public CurrentSliceSaveData Snapshot { get; }
        public CurrentSliceLoadResult CurrentSliceResult { get; }
    }

    /// <summary>
    /// World-bound physical wrapper around the unchanged Current Slice logical
    /// payload. M37 remains the envelope/store authority and CurrentSlice
    /// services remain the gameplay capture/validation/apply authority.
    /// </summary>
    public static class WorldGameplayPersistenceService
    {
        public const string SnapshotType = "world_gameplay_v1";
        public const int SchemaVersion = 1;
        private const string SlotPrefix = "gameplay_";

#if UNITY_EDITOR
        public static bool DiagnosticInjectPrepareFailure { get; set; }
        public static bool DiagnosticInjectCommitFailure { get; set; }
#endif

        public static string GetSlotId(WorldId worldId)
        {
            if (!worldId.IsValid)
                throw new ArgumentException("A valid WorldId is required.", nameof(worldId));
            return SlotPrefix + worldId.Canonical;
        }

        public static WorldGameplayPreparationResult PrepareSave(WorldSession session)
        {
            if (session == null || !session.WorldId.IsValid)
                return PrepareFailure("BindingPreflight", "A valid active WorldSession is required.");
            if (!ReferenceEquals(WorldSessionService.ActiveSession, session))
                return PrepareFailure("BindingPreflight", "Gameplay state can only be captured for the active WorldSession.");
            if (!session.Topology.Sectors.Contains(session.ActiveSectorId))
                return PrepareFailure("BindingPreflight", "Active SectorId is absent from the committed WorldTopology.");

#if UNITY_EDITOR
            if (DiagnosticInjectPrepareFailure)
            {
                DiagnosticInjectPrepareFailure = false;
                return PrepareFailure("Capture", "Injected gameplay capture failure.");
            }
#endif

            CurrentSliceResult capture = CurrentSliceSnapshotService.Capture();
            if (!capture.Success)
                return PrepareFailure("Capture", capture.Failure);

            string slotId = GetSlotId(session.WorldId);
            var payload = new JObject
            {
                ["snapshotType"] = SnapshotType,
                ["schemaVersion"] = SchemaVersion,
                ["worldId"] = session.WorldId.Canonical,
                ["activeSectorId"] = session.ActiveSectorId.Canonical,
                ["currentSlice"] = CurrentSliceSnapshotService.ToPayload(capture.Snapshot)
            };
            return new WorldGameplayPreparationResult(
                new WorldGameplayPreparedSave(
                    session.WorldId,
                    session.ActiveSectorId,
                    slotId,
                    capture.Snapshot,
                    payload),
                "Prepared",
                null);
        }

        public static WorldGameplayCommitResult CommitPrepared(
            WorldSession session,
            WorldGameplayPreparedSave prepared,
            PersistenceFileStore store)
        {
            if (session == null || prepared == null || prepared.Snapshot == null)
                return CommitFailure("BindingPreflight", "Prepared gameplay state is missing.");
            if (prepared.WorldId != session.WorldId || prepared.SectorId != session.ActiveSectorId ||
                prepared.SlotId != GetSlotId(session.WorldId))
            {
                return CommitFailure(
                    "BindingPreflight",
                    "Prepared gameplay state no longer matches the active WorldId/SectorId binding.");
            }

#if UNITY_EDITOR
            if (DiagnosticInjectCommitFailure)
            {
                DiagnosticInjectCommitFailure = false;
                return CommitFailure("Write", "Injected gameplay write failure.");
            }
#endif

            PersistenceWriteResult write = (store ?? new PersistenceFileStore()).Write(
                prepared.SlotId,
                prepared.Payload);
            if (!write.Success)
                return CommitFailure("Write", write.FailureCode + ": " + write.Failure);

            LogGameplaySaveOk(session, prepared.Snapshot);
            return new WorldGameplayCommitResult(true, "Complete", null, prepared.Snapshot);
        }

        public static WorldGameplayLoadResult LoadAndApply(
            WorldSession session,
            PersistenceFileStore store = null)
        {
            if (session == null || !session.WorldId.IsValid)
                return LoadFailure("BindingPreflight", "A valid active WorldSession is required.");
            if (!ReferenceEquals(WorldSessionService.ActiveSession, session))
                return LoadFailure("BindingPreflight", "Gameplay state can only restore into the active WorldSession.");

            string slotId = GetSlotId(session.WorldId);
            PersistenceLoadResult read = (store ?? new PersistenceFileStore()).Read(slotId);
            if (!read.Success)
            {
                if (read.FailureCode == PersistenceFailureCode.SaveNotFound)
                {
                    Debug.Log(
                        "[WorldSave][GAMEPLAY_STATE_ABSENT_LEGACY]\n" +
                        "WorldId: " + session.WorldId.Canonical + "\n" +
                        "SectorId: " + session.ActiveSectorId.Canonical + "\n" +
                        "Slot: " + slotId + "\n" +
                        "ActionTaken: established shared player composition at safe legacy bootstrap; no old pose was claimed restored");
                    return new WorldGameplayLoadResult(
                        WorldGameplayLoadDisposition.AbsentLegacy,
                        "Read",
                        null,
                        null,
                        null);
                }
                return LoadFailure("Read", read.FailureCode + ": " + read.Failure);
            }

            if (!TryPreflightPayload(
                    read.Payload,
                    session,
                    out CurrentSliceSaveData snapshot,
                    out string preflightFailure))
            {
                return LoadFailure("SemanticPreflight", preflightFailure);
            }

            CurrentSliceLoadResult apply = CurrentSliceLoadService.LoadValidatedSnapshot(snapshot, slotId);
            if (!apply.Success)
            {
                return new WorldGameplayLoadResult(
                    WorldGameplayLoadDisposition.Failed,
                    apply.Phase,
                    apply.Failure,
                    null,
                    apply);
            }

            LogGameplayLoadOk(session, snapshot);
            return new WorldGameplayLoadResult(
                WorldGameplayLoadDisposition.Restored,
                "Complete",
                null,
                snapshot,
                apply);
        }

        internal static bool TryPreflightPayload(
            JToken payload,
            WorldSession expectedSession,
            out CurrentSliceSaveData snapshot,
            out string failure)
        {
            snapshot = null;
            failure = null;
            if (payload is not JObject root)
                return Fail("World gameplay payload must be a JSON object.", out failure);
            if (root["snapshotType"]?.Type != JTokenType.String ||
                root["snapshotType"]?.Value<string>() != SnapshotType)
                return Fail("World gameplay snapshotType must be '" + SnapshotType + "'.", out failure);
            if (root["schemaVersion"]?.Type != JTokenType.Integer ||
                root["schemaVersion"]?.Value<int>() != SchemaVersion)
                return Fail("World gameplay schemaVersion must be 1.", out failure);
            if (!WorldId.TryParse(root["worldId"]?.Value<string>(), out WorldId worldId, out string worldError))
                return Fail("World gameplay WorldId is invalid: " + worldError, out failure);
            if (!SectorId.TryParse(root["activeSectorId"]?.Value<string>(), out SectorId sectorId, out string sectorError))
                return Fail("World gameplay SectorId is invalid: " + sectorError, out failure);
            if (expectedSession == null || worldId != expectedSession.WorldId)
                return Fail("World gameplay binding belongs to another WorldId.", out failure);
            if (sectorId != expectedSession.ActiveSectorId || !expectedSession.Topology.Sectors.Contains(sectorId))
                return Fail("World gameplay binding does not match the active committed SectorId.", out failure);
            JToken currentSlice = root["currentSlice"];
            if (currentSlice == null || currentSlice.Type == JTokenType.Null)
                return Fail("World gameplay payload is missing currentSlice_v1 truth.", out failure);

            CurrentSliceResult parsed = CurrentSliceSnapshotService.FromPayload(currentSlice);
            if (!parsed.Success)
                return Fail("Current Slice semantic preflight failed: " + parsed.Failure, out failure);
            snapshot = parsed.Snapshot;
            return true;
        }

        private static void LogGameplaySaveOk(WorldSession session, CurrentSliceSaveData snapshot)
        {
            Debug.Log(
                "[WorldSave][GAMEPLAY_SAVE_OK]\n" +
                "WorldId: " + session.WorldId.Canonical + "\n" +
                "SectorId: " + session.ActiveSectorId.Canonical + "\n" +
                "ActorInstanceId: " + Value(snapshot.player?.actorInstanceId) + "\n" +
                "PersistentSceneObjectId: " + Value(snapshot.player?.persistentId) + "\n" +
                "LocalPosition: " + Position(snapshot.player?.pose) + "\n" +
                "Health: " + Health(snapshot.player) + "\n" +
                "Items: " + (snapshot.items?.Length ?? 0).ToString(CultureInfo.InvariantCulture) +
                " / Actors: " + (snapshot.actors?.Length ?? 0).ToString(CultureInfo.InvariantCulture));
        }

        private static void LogGameplayLoadOk(WorldSession session, CurrentSliceSaveData snapshot)
        {
            Debug.Log(
                "[WorldSave][GAMEPLAY_LOAD_OK]\n" +
                "WorldId: " + session.WorldId.Canonical + "\n" +
                "SectorId: " + session.ActiveSectorId.Canonical + "\n" +
                "ActorInstanceId: " + Value(snapshot.player?.actorInstanceId) + "\n" +
                "PersistentSceneObjectId: " + Value(snapshot.player?.persistentId) + "\n" +
                "LocalPosition: " + Position(snapshot.player?.pose) + "\n" +
                "Health: " + Health(snapshot.player));
        }

        private static WorldGameplayPreparationResult PrepareFailure(string phase, string failure)
        {
            Debug.LogError("[WorldSave][GAMEPLAY_SAVE_FAIL]\nPhase: " + phase + "\nFailure: " + failure);
            return new WorldGameplayPreparationResult(null, phase, failure);
        }

        private static WorldGameplayCommitResult CommitFailure(string phase, string failure)
        {
            Debug.LogError("[WorldSave][GAMEPLAY_SAVE_FAIL]\nPhase: " + phase + "\nFailure: " + failure);
            return new WorldGameplayCommitResult(false, phase, failure, null);
        }

        private static WorldGameplayLoadResult LoadFailure(string phase, string failure)
        {
            Debug.LogError("[WorldSave][GAMEPLAY_LOAD_FAIL]\nPhase: " + phase + "\nFailure: " + failure);
            return new WorldGameplayLoadResult(
                WorldGameplayLoadDisposition.Failed,
                phase,
                failure,
                null,
                null);
        }

        private static bool Fail(string message, out string failure)
        {
            failure = message;
            return false;
        }

        private static string Position(PoseState pose)
        {
            Float3State value = pose?.position;
            return value == null
                ? "<NONE>"
                : "(" + value.x.ToString("R", CultureInfo.InvariantCulture) + ", " +
                  value.y.ToString("R", CultureInfo.InvariantCulture) + ", " +
                  value.z.ToString("R", CultureInfo.InvariantCulture) + ")";
        }

        private static string Value(string value) => string.IsNullOrWhiteSpace(value) ? "<NONE>" : value;

        private static string Health(PlayerState player) => player == null
            ? "<NONE>"
            : player.currentHealth.ToString("R", CultureInfo.InvariantCulture);
    }

    public sealed class WorldRuntimeSaveResult
    {
        internal WorldRuntimeSaveResult(
            bool success,
            string phase,
            string failure,
            bool worldSessionCommitted,
            bool gameplayCommitted,
            CurrentSliceSaveData snapshot)
        {
            Success = success;
            Phase = phase;
            Failure = failure;
            WorldSessionCommitted = worldSessionCommitted;
            GameplayCommitted = gameplayCommitted;
            Snapshot = snapshot;
        }

        public bool Success { get; }
        public string Phase { get; }
        public string Failure { get; }
        public bool WorldSessionCommitted { get; }
        public bool GameplayCommitted { get; }
        public CurrentSliceSaveData Snapshot { get; }
    }

    /// <summary>
    /// Small application-boundary coordinator for the two logical authorities
    /// that comprise an in-game checkpoint. M37 commits each sibling file; no
    /// multi-file transaction is invented here.
    /// </summary>
    public static class WorldRuntimeSaveService
    {
        public static WorldRuntimeSaveResult SaveActive(PersistenceFileStore store = null)
        {
            WorldSession session = WorldSessionService.ActiveSession;
            if (session == null)
                return Failure("Preflight", "No WorldSession is active.", false, false);

            WorldGameplayPreparationResult prepare = WorldGameplayPersistenceService.PrepareSave(session);
            if (!prepare.Success)
                return Failure("Gameplay" + prepare.Phase, prepare.Failure, false, false);

            PersistenceFileStore resolvedStore = store ??
                                                 WorldSessionService.ActivePersistenceStore ??
                                                 new PersistenceFileStore();
            WorldSessionPersistenceResult worldSave = WorldSessionPersistenceService.Save(session, resolvedStore);
            if (!worldSave.Success)
            {
                return Failure(
                    "WorldSession" + worldSave.Phase,
                    worldSave.FailureCode + ": " + worldSave.Failure,
                    false,
                    false);
            }

            WorldGameplayCommitResult gameplay = WorldGameplayPersistenceService.CommitPrepared(
                session,
                prepare.Prepared,
                resolvedStore);
            if (!gameplay.Success)
            {
                return Failure(
                    "Gameplay" + gameplay.Phase,
                    gameplay.Failure +
                    " WorldSession file committed first; overall Save remains failed and the partial checkpoint is explicit.",
                    true,
                    false);
            }

            WorldSessionObservability.LogSaveOk(session);
            return new WorldRuntimeSaveResult(
                true,
                "Complete",
                null,
                true,
                true,
                gameplay.Snapshot);
        }

        private static WorldRuntimeSaveResult Failure(
            string phase,
            string failure,
            bool worldCommitted,
            bool gameplayCommitted)
        {
            Debug.LogError(
                "[WorldSave][SAVE_FAIL]\n" +
                "Phase: " + phase + "\n" +
                "WorldSessionCommitted: " + worldCommitted + "\n" +
                "GameplayCommitted: " + gameplayCommitted + "\n" +
                "Failure: " + failure);
            return new WorldRuntimeSaveResult(
                false,
                phase,
                failure,
                worldCommitted,
                gameplayCommitted,
                null);
        }
    }
}
