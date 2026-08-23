using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OldScars.Core.World;

namespace OldScars.Core.Persistence
{
    public enum WorldSessionPersistenceFailureCode
    {
        Success,
        ReadFailed,
        WriteFailed,
        MalformedPayload,
        SemanticPreflightFailed
    }

    public sealed class WorldSessionPersistenceResult
    {
        internal WorldSessionPersistenceResult(
            WorldSessionPersistenceFailureCode failureCode,
            string phase,
            string failure,
            WorldSession session)
        {
            FailureCode = failureCode;
            Phase = phase;
            Failure = failure;
            Session = session;
        }

        public bool Success => FailureCode == WorldSessionPersistenceFailureCode.Success;
        public WorldSessionPersistenceFailureCode FailureCode { get; }
        public string Phase { get; }
        public string Failure { get; }
        public WorldSession Session { get; }
    }

    /// <summary>
    /// Sibling world_session_v1 payload adapter over M37's existing envelope and
    /// file store. Deserialization and semantic preflight never publish a session.
    /// </summary>
    public static class WorldSessionPersistenceService
    {
        public const string SnapshotType = "world_session_v1";
        public const int CurrentSchemaVersion = 1;

        private static readonly JsonSerializer PayloadSerializer = JsonSerializer.Create(
            new JsonSerializerSettings
            {
                Culture = CultureInfo.InvariantCulture,
                DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Decimal,
                MissingMemberHandling = MissingMemberHandling.Error,
                NullValueHandling = NullValueHandling.Include,
                ReferenceLoopHandling = ReferenceLoopHandling.Error,
                TypeNameHandling = TypeNameHandling.None
            });

        public static JToken ToPayload(WorldSession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            var sectors = new string[session.Topology.Sectors.Count];
            for (int index = 0; index < sectors.Length; index++)
                sectors[index] = session.Topology.Sectors[index].Canonical;

            var connections = new WorldConnectionSaveData[session.Topology.Connections.Count];
            for (int index = 0; index < connections.Length; index++)
            {
                SectorConnection connection = session.Topology.Connections[index];
                connections[index] = new WorldConnectionSaveData
                {
                    connectionKey = connection.ConnectionKey,
                    firstSectorId = connection.FirstEndpoint.Canonical,
                    secondSectorId = connection.SecondEndpoint.Canonical
                };
            }

            IReadOnlyList<WorldCreationContentSourceEvidence> sourceEvidence =
                session.CreationContentEvidence.Sources;
            var sources = new WorldContentSourceEvidenceSaveData[sourceEvidence.Count];
            for (int index = 0; index < sources.Length; index++)
            {
                WorldCreationContentSourceEvidence source = sourceEvidence[index];
                sources[index] = new WorldContentSourceEvidenceSaveData
                {
                    sourceId = source.SourceId,
                    ownedNamespace = source.OwnedNamespace,
                    version = source.Version,
                    isOfficialCore = source.IsOfficialCore,
                    provenanceFingerprint = source.ProvenanceFingerprint
                };
            }

            var saveData = new WorldSessionSaveData
            {
                snapshotType = SnapshotType,
                schemaVersion = CurrentSchemaVersion,
                worldId = session.WorldId.Canonical,
                displayName = session.DisplayName,
                generationContext = new WorldGenerationContextSaveData
                {
                    worldSeed = session.GenerationContext.WorldSeed.Canonical,
                    generatorVersion = session.GenerationContext.GeneratorVersion.Canonical
                },
                topology = new WorldTopologySaveData
                {
                    canonicalHash = session.Topology.CanonicalHash,
                    sectors = sectors,
                    connections = connections
                },
                activeSectorId = session.ActiveSectorId.Canonical,
                creationContentProvenance = new WorldContentEvidenceSaveData
                {
                    loadedContentSetFingerprint =
                        session.CreationContentEvidence.LoadedContentSetFingerprint,
                    sources = sources
                }
            };
            return JToken.FromObject(saveData, PayloadSerializer);
        }

        public static WorldSessionPersistenceResult FromPayload(JToken payload)
        {
            if (!(payload is JObject payloadObject))
                return Fail(WorldSessionPersistenceFailureCode.MalformedPayload, "Deserialize",
                    "World Session payload must be a JSON object.");

            if (!(payloadObject["snapshotType"] is JValue snapshotTypeToken) ||
                snapshotTypeToken.Type != JTokenType.String)
            {
                return Fail(WorldSessionPersistenceFailureCode.MalformedPayload, "Deserialize",
                    "snapshotType must be a present JSON string.");
            }
            if (!(payloadObject["schemaVersion"] is JValue schemaVersionToken) ||
                schemaVersionToken.Type != JTokenType.Integer)
            {
                return Fail(WorldSessionPersistenceFailureCode.MalformedPayload, "Deserialize",
                    "schemaVersion must be a present JSON integer.");
            }

            WorldSessionSaveData data;
            try
            {
                data = payload.ToObject<WorldSessionSaveData>(PayloadSerializer);
            }
            catch (Exception exception) when (
                exception is JsonException || exception is InvalidOperationException ||
                exception is FormatException || exception is OverflowException)
            {
                return Fail(WorldSessionPersistenceFailureCode.MalformedPayload, "Deserialize",
                    "World Session payload deserialization failed: " + exception.Message);
            }

            return Preflight(data);
        }

        public static WorldSessionPersistenceResult Save(
            WorldSession session,
            PersistenceFileStore store = null)
        {
            if (session == null)
                return Fail(WorldSessionPersistenceFailureCode.SemanticPreflightFailed, "Preflight",
                    "WorldSession is null.");

            JToken payload = ToPayload(session);
            WorldSessionPersistenceResult preflight = FromPayload(payload);
            if (!preflight.Success)
                return preflight;

            PersistenceWriteResult write = (store ?? new PersistenceFileStore()).Write(
                session.WorldId.Canonical,
                payload);
            return write.Success
                ? Success("Write", session)
                : Fail(WorldSessionPersistenceFailureCode.WriteFailed, "Write",
                    $"{write.FailureCode}: {write.Failure}");
        }

        public static WorldSessionPersistenceResult Read(
            string slotId,
            PersistenceFileStore store = null)
        {
            if (!WorldId.TryParse(slotId, out WorldId slotWorldId, out string slotError))
            {
                return Fail(WorldSessionPersistenceFailureCode.ReadFailed, "Read",
                    "World save slot must be a canonical WorldId: " + slotError + ".");
            }

            PersistenceLoadResult read = (store ?? new PersistenceFileStore()).Read(slotId);
            if (!read.Success)
            {
                return Fail(WorldSessionPersistenceFailureCode.ReadFailed, "Read",
                    $"{read.FailureCode}: {read.Failure}");
            }

            WorldSessionPersistenceResult preflight = FromPayload(read.Payload);
            if (!preflight.Success)
                return preflight;
            if (preflight.Session.WorldId != slotWorldId)
            {
                return Fail(WorldSessionPersistenceFailureCode.SemanticPreflightFailed, "SemanticPreflight",
                    $"Payload WorldId '{preflight.Session.WorldId.Canonical}' does not match slot '{slotId}'.");
            }
            return Success("Read", preflight.Session);
        }

        private static WorldSessionPersistenceResult Preflight(WorldSessionSaveData data)
        {
            if (data == null)
                return SemanticFailure("World Session payload deserialized to null.");
            if (data.snapshotType != SnapshotType || data.schemaVersion != CurrentSchemaVersion)
            {
                return SemanticFailure(
                    $"Unsupported World Session contract '{Safe(data.snapshotType)}' schema {data.schemaVersion}.");
            }
            if (!WorldId.TryParse(data.worldId, out WorldId worldId, out string worldIdError))
                return SemanticFailure("worldId is invalid: " + worldIdError + ".");
            if (data.generationContext == null)
                return SemanticFailure("generationContext is required.");
            if (!WorldSeed.TryParse(data.generationContext.worldSeed, out WorldSeed seed, out string seedError))
                return SemanticFailure("generationContext.worldSeed is invalid: " + seedError + ".");
            if (!GeneratorVersion.TryParse(
                    data.generationContext.generatorVersion,
                    out GeneratorVersion generatorVersion,
                    out string generatorVersionError))
            {
                return SemanticFailure(
                    "generationContext.generatorVersion is invalid: " + generatorVersionError + ".");
            }

            if (data.topology == null)
                return SemanticFailure("topology is required.");
            if (data.topology.sectors == null)
                return SemanticFailure("topology.sectors is required.");
            if (data.topology.connections == null)
                return SemanticFailure("topology.connections is required.");

            var sectors = new List<SectorId>(data.topology.sectors.Length);
            for (int index = 0; index < data.topology.sectors.Length; index++)
            {
                if (!SectorId.TryParse(data.topology.sectors[index], out SectorId sectorId, out string sectorError))
                {
                    return SemanticFailure($"topology.sectors[{index}] is invalid: {sectorError}.");
                }
                sectors.Add(sectorId);
            }

            var connections = new List<SectorConnection>(data.topology.connections.Length);
            for (int index = 0; index < data.topology.connections.Length; index++)
            {
                WorldConnectionSaveData connectionData = data.topology.connections[index];
                if (connectionData == null)
                    return SemanticFailure($"topology.connections[{index}] is null.");
                if (!SectorId.TryParse(connectionData.firstSectorId, out SectorId first, out string firstError))
                {
                    return SemanticFailure(
                        $"topology.connections[{index}].firstSectorId is invalid: {firstError}.");
                }
                if (!SectorId.TryParse(connectionData.secondSectorId, out SectorId second, out string secondError))
                {
                    return SemanticFailure(
                        $"topology.connections[{index}].secondSectorId is invalid: {secondError}.");
                }
                try
                {
                    connections.Add(new SectorConnection(connectionData.connectionKey, first, second));
                }
                catch (ArgumentException exception)
                {
                    return SemanticFailure(
                        $"topology.connections[{index}] is invalid: {exception.Message}");
                }
            }

            if (!WorldTopology.TryCreate(
                    sectors,
                    connections,
                    out WorldTopology topology,
                    out WorldTopologyValidationResult topologyValidation))
            {
                return SemanticFailure("topology failed validation: " + topologyValidation.Description);
            }
            if (!string.Equals(data.topology.canonicalHash, topology.CanonicalHash, StringComparison.Ordinal))
            {
                return SemanticFailure(
                    $"topology.canonicalHash mismatch; persisted '{Safe(data.topology.canonicalHash)}', " +
                    $"reconstructed '{topology.CanonicalHash}'.");
            }
            if (!SectorId.TryParse(data.activeSectorId, out SectorId activeSectorId, out string activeSectorError))
                return SemanticFailure("activeSectorId is invalid: " + activeSectorError + ".");

            if (data.creationContentProvenance == null)
                return SemanticFailure("creationContentProvenance is required.");
            if (data.creationContentProvenance.sources == null)
                return SemanticFailure("creationContentProvenance.sources is required.");
            var sourceEvidence = new List<WorldCreationContentSourceEvidence>(
                data.creationContentProvenance.sources.Length);
            for (int index = 0; index < data.creationContentProvenance.sources.Length; index++)
            {
                WorldContentSourceEvidenceSaveData source = data.creationContentProvenance.sources[index];
                if (source == null)
                    return SemanticFailure($"creationContentProvenance.sources[{index}] is null.");
                sourceEvidence.Add(new WorldCreationContentSourceEvidence(
                    source.sourceId,
                    source.ownedNamespace,
                    source.version,
                    source.isOfficialCore,
                    source.provenanceFingerprint));
            }
            if (!WorldCreationContentEvidence.TryCreate(
                    data.creationContentProvenance.loadedContentSetFingerprint,
                    sourceEvidence,
                    out WorldCreationContentEvidence contentEvidence,
                    out string evidenceError))
            {
                return SemanticFailure("creationContentProvenance is invalid: " + evidenceError + ".");
            }

            var context = new WorldGenerationContext(seed, generatorVersion);
            if (!WorldSession.TryCreate(
                    worldId,
                    data.displayName,
                    context,
                    topology,
                    activeSectorId,
                    contentEvidence,
                    out WorldSession session,
                    out string sessionError))
            {
                return SemanticFailure(sessionError + ".");
            }

            return Success("SemanticPreflight", session);
        }

        private static WorldSessionPersistenceResult SemanticFailure(string failure)
        {
            return Fail(
                WorldSessionPersistenceFailureCode.SemanticPreflightFailed,
                "SemanticPreflight",
                failure);
        }

        private static WorldSessionPersistenceResult Success(string phase, WorldSession session)
        {
            return new WorldSessionPersistenceResult(
                WorldSessionPersistenceFailureCode.Success, phase, null, session);
        }

        private static WorldSessionPersistenceResult Fail(
            WorldSessionPersistenceFailureCode code,
            string phase,
            string failure)
        {
            return new WorldSessionPersistenceResult(code, phase, failure, null);
        }

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<EMPTY>" : value;
        }

        [Serializable]
        private sealed class WorldSessionSaveData
        {
            public string snapshotType;
            public int schemaVersion;
            public string worldId;
            public string displayName;
            public WorldGenerationContextSaveData generationContext;
            public WorldTopologySaveData topology;
            public string activeSectorId;
            public WorldContentEvidenceSaveData creationContentProvenance;
        }

        [Serializable]
        private sealed class WorldGenerationContextSaveData
        {
            public string worldSeed;
            public string generatorVersion;
        }

        [Serializable]
        private sealed class WorldTopologySaveData
        {
            public string canonicalHash;
            public string[] sectors;
            public WorldConnectionSaveData[] connections;
        }

        [Serializable]
        private sealed class WorldConnectionSaveData
        {
            public string connectionKey;
            public string firstSectorId;
            public string secondSectorId;
        }

        [Serializable]
        private sealed class WorldContentEvidenceSaveData
        {
            public string loadedContentSetFingerprint;
            public WorldContentSourceEvidenceSaveData[] sources;
        }

        [Serializable]
        private sealed class WorldContentSourceEvidenceSaveData
        {
            public string sourceId;
            public string ownedNamespace;
            public string version;
            public bool isOfficialCore;
            public string provenanceFingerprint;
        }
    }
}
