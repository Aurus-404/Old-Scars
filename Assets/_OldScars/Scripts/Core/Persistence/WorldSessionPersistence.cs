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
    /// file store. Schema 3 persists Macro Elevation / Landforms V1. Schemas 1
    /// and 2 remain explicit legacy shapes and never receive fabricated truth.
    /// </summary>
    public static class WorldSessionPersistenceService
    {
        public const string SnapshotType = "world_session_v1";
        public const int LegacySchemaVersion = 1;
        public const int MacroPlanSchemaVersion = 2;
        public const int CurrentSchemaVersion = 3;

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
            if (session.IsLegacySchemaV1)
                return JToken.FromObject(BuildLegacySaveData(session), PayloadSerializer);
            if (session.IsLegacySchemaV2)
                return JToken.FromObject(BuildMacroPlanSaveData(session), PayloadSerializer);
            return JToken.FromObject(BuildCurrentSaveData(session), PayloadSerializer);
        }

        public static WorldSessionPersistenceResult FromPayload(JToken payload)
        {
            if (!(payload is JObject payloadObject))
                return Malformed("World Session payload must be a JSON object.");
            if (!(payloadObject["snapshotType"] is JValue snapshotTypeToken) ||
                snapshotTypeToken.Type != JTokenType.String)
            {
                return Malformed("snapshotType must be a present JSON string.");
            }
            if (!(payloadObject["schemaVersion"] is JValue schemaVersionToken) ||
                schemaVersionToken.Type != JTokenType.Integer)
            {
                return Malformed("schemaVersion must be a present JSON integer.");
            }

            string snapshotType = snapshotTypeToken.Value<string>();
            int schemaVersion;
            try
            {
                schemaVersion = schemaVersionToken.Value<int>();
            }
            catch (Exception exception) when (exception is OverflowException || exception is FormatException)
            {
                return Malformed("schemaVersion is outside the supported integer range.");
            }
            if (snapshotType != SnapshotType)
                return SemanticFailure($"Unsupported World Session contract '{Safe(snapshotType)}' schema {schemaVersion}.");

            try
            {
                if (schemaVersion == LegacySchemaVersion)
                    return PreflightLegacy(payload.ToObject<WorldSessionV1SaveData>(PayloadSerializer));
                if (schemaVersion == MacroPlanSchemaVersion)
                    return PreflightMacroPlan(payload.ToObject<WorldSessionV2SaveData>(PayloadSerializer));
                if (schemaVersion == CurrentSchemaVersion)
                    return PreflightCurrent(payload.ToObject<WorldSessionV3SaveData>(PayloadSerializer));
                return SemanticFailure(
                    $"Unsupported World Session contract '{snapshotType}' schema {schemaVersion}.");
            }
            catch (Exception exception) when (
                exception is JsonException || exception is InvalidOperationException ||
                exception is FormatException || exception is OverflowException)
            {
                return Malformed("World Session payload deserialization failed: " + exception.Message);
            }
        }

        public static WorldSessionPersistenceResult Save(
            WorldSession session,
            PersistenceFileStore store = null)
        {
            if (session == null)
                return SemanticFailure("WorldSession is null.");

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
                return SemanticFailure(
                    $"Payload WorldId '{preflight.Session.WorldId.Canonical}' does not match slot '{slotId}'.");
            }
            return Success("Read", preflight.Session);
        }

        private static WorldSessionPersistenceResult PreflightLegacy(WorldSessionV1SaveData data)
        {
            if (data == null)
                return SemanticFailure("Legacy World Session payload deserialized to null.");
            if (data.snapshotType != SnapshotType || data.schemaVersion != LegacySchemaVersion)
                return SemanticFailure("Legacy World Session header is inconsistent.");
            if (!TryReadCommon(
                    data.worldId, data.displayName, data.generationContext, data.activeSectorId,
                    data.creationContentProvenance,
                    out WorldId worldId, out string displayName, out WorldGenerationContext context,
                    out SectorId activeSectorId, out WorldCreationContentEvidence contentEvidence,
                    out string commonError))
            {
                return SemanticFailure(commonError);
            }
            if (!TryReadTopology(data.topology, "topology", out WorldTopology topology, out string topologyError))
                return SemanticFailure(topologyError);
            if (!WorldSession.TryCreateLegacySchemaV1(
                    worldId, displayName, context, topology, activeSectorId, contentEvidence,
                    out WorldSession session, out string sessionError))
            {
                return SemanticFailure(sessionError + ".");
            }
            return Success("SemanticPreflightLegacyV1", session);
        }

        private static WorldSessionPersistenceResult PreflightMacroPlan(WorldSessionV2SaveData data)
        {
            if (data == null)
                return SemanticFailure("Legacy schema-2 World Session payload deserialized to null.");
            if (data.snapshotType != SnapshotType || data.schemaVersion != MacroPlanSchemaVersion)
                return SemanticFailure("World Session schema-2 header is inconsistent.");
            if (!TryReadCommon(
                    data.worldId, data.displayName, data.generationContext, data.activeSectorId,
                    data.creationContentProvenance,
                    out WorldId worldId, out string displayName, out WorldGenerationContext context,
                    out SectorId activeSectorId, out WorldCreationContentEvidence contentEvidence,
                    out string commonError))
            {
                return SemanticFailure(commonError);
            }
            if (!TryReadMacroWorldPlan(data.macroWorldPlan, out MacroWorldPlan plan, out string planError))
                return SemanticFailure(planError);
            if (!WorldSession.TryCreateLegacySchemaV2(
                    worldId, displayName, context, plan, activeSectorId, contentEvidence,
                    out WorldSession session, out string sessionError))
            {
                return SemanticFailure(sessionError + ".");
            }
            return Success("SemanticPreflightLegacyV2", session);
        }

        private static WorldSessionPersistenceResult PreflightCurrent(WorldSessionV3SaveData data)
        {
            if (data == null)
                return SemanticFailure("World Session payload deserialized to null.");
            if (data.snapshotType != SnapshotType || data.schemaVersion != CurrentSchemaVersion)
                return SemanticFailure("World Session schema-3 header is inconsistent.");
            if (!TryReadCommon(
                    data.worldId, data.displayName, data.generationContext, data.activeSectorId,
                    data.creationContentProvenance,
                    out WorldId worldId, out string displayName, out WorldGenerationContext context,
                    out SectorId activeSectorId, out WorldCreationContentEvidence contentEvidence,
                    out string commonError))
            {
                return SemanticFailure(commonError);
            }
            if (!TryReadMacroWorldPlan(data.macroWorldPlan, out MacroWorldPlan plan, out string planError))
                return SemanticFailure(planError);
            if (!TryReadMacroGeography(
                    data.macroGeography, plan.WorldBounds,
                    out MacroGeographyPlan geography, out string geographyError))
            {
                return SemanticFailure(geographyError);
            }
            if (!WorldSession.TryCreate(
                    worldId, displayName, context, plan, geography, activeSectorId, contentEvidence,
                    out WorldSession session, out string sessionError))
            {
                return SemanticFailure(sessionError + ".");
            }
            return Success("SemanticPreflight", session);
        }

        private static bool TryReadMacroWorldPlan(
            MacroWorldPlanSaveData data,
            out MacroWorldPlan plan,
            out string error)
        {
            plan = null;
            error = null;
            if (data == null)
            {
                error = "macroWorldPlan is required";
                return false;
            }
            if (data.generationSettings == null)
            {
                error = "macroWorldPlan.generationSettings is required";
                return false;
            }
            if (!WorldGenerationSettings.TryParsePreset(
                    data.generationSettings.worldSizePreset,
                    out WorldSizePreset preset,
                    out string presetError))
            {
                error = "macroWorldPlan.generationSettings.worldSizePreset is invalid: " + presetError + ".";
                return false;
            }
            if (!WorldGenerationSettings.TryCreateResolved(
                    preset,
                    data.generationSettings.resolvedSectorCount,
                    data.generationSettings.resolvedWorldWidth,
                    data.generationSettings.resolvedWorldHeight,
                    data.generationSettings.resolvedMinimumSectorSpacing,
                    out WorldGenerationSettings settings,
                    out string settingsError))
            {
                error = "macroWorldPlan.generationSettings is invalid: " + settingsError + ".";
                return false;
            }
            if (data.worldBounds == null)
            {
                error = "macroWorldPlan.worldBounds is required";
                return false;
            }

            FiniteMacroWorldBounds bounds;
            try
            {
                bounds = new FiniteMacroWorldBounds(
                    data.worldBounds.minX,
                    data.worldBounds.minY,
                    data.worldBounds.maxXExclusive,
                    data.worldBounds.maxYExclusive);
            }
            catch (ArgumentException exception)
            {
                error = "macroWorldPlan.worldBounds is invalid: " + exception.Message;
                return false;
            }
            if (data.sectorPlacements == null)
            {
                error = "macroWorldPlan.sectorPlacements is required";
                return false;
            }
            var placements = new List<MacroSectorPlacement>(data.sectorPlacements.Length);
            for (int index = 0; index < data.sectorPlacements.Length; index++)
            {
                MacroSectorPlacementSaveData placementData = data.sectorPlacements[index];
                if (placementData == null)
                {
                    error = $"macroWorldPlan.sectorPlacements[{index}] is null";
                    return false;
                }
                if (!SectorId.TryParse(placementData.sectorId, out SectorId sectorId, out string sectorError))
                {
                    error = $"macroWorldPlan.sectorPlacements[{index}].sectorId is invalid: {sectorError}.";
                    return false;
                }
                placements.Add(new MacroSectorPlacement(
                    sectorId, new MacroPoint2D(placementData.x, placementData.y)));
            }
            if (!TryReadTopology(
                    data.topology, "macroWorldPlan.topology", out WorldTopology topology, out error))
            {
                return false;
            }
            if (!MacroWorldPlan.TryCreate(
                    settings, bounds, placements, topology, out plan, out string validationError))
            {
                error = "macroWorldPlan failed validation: " + validationError;
                return false;
            }
            if (!string.Equals(data.canonicalHash, plan.CanonicalHash, StringComparison.Ordinal))
            {
                error = $"macroWorldPlan.canonicalHash mismatch; persisted '{Safe(data.canonicalHash)}', " +
                        $"reconstructed '{plan.CanonicalHash}'.";
                plan = null;
                return false;
            }
            return true;
        }

        private static bool TryReadMacroGeography(
            MacroGeographySaveData data,
            FiniteMacroWorldBounds worldBounds,
            out MacroGeographyPlan geography,
            out string error)
        {
            geography = null;
            error = null;
            if (data == null || data.generationSettings == null)
            {
                error = "macroGeography and its generationSettings are required";
                return false;
            }
            MacroGeographyGenerationSettingsSaveData settingsData = data.generationSettings;
            if (!MacroGeographyGenerationSettings.TryCreateResolved(
                    settingsData.generationContract,
                    settingsData.sampleColumns,
                    settingsData.sampleRows,
                    settingsData.regionalFrequencyQ16,
                    settingsData.baseElevationFrequencyQ16,
                    settingsData.detailFrequencyQ16,
                    settingsData.roughnessFrequencyQ16,
                    settingsData.resolvedAttempt,
                    out MacroGeographyGenerationSettings settings,
                    out string settingsError))
            {
                error = "macroGeography.generationSettings is invalid: " + settingsError + ".";
                return false;
            }

            byte[] elevationBytes;
            byte[] landformBytes;
            try
            {
                elevationBytes = Convert.FromBase64String(data.elevationSamplesBase64 ?? string.Empty);
                landformBytes = Convert.FromBase64String(data.landformSamplesBase64 ?? string.Empty);
            }
            catch (FormatException exception)
            {
                error = "macroGeography sample data is not valid Base64: " + exception.Message;
                return false;
            }
            int sampleCount = checked(settings.SampleColumns * settings.SampleRows);
            if (elevationBytes.Length != sampleCount * 2 || landformBytes.Length != sampleCount)
            {
                error = "macroGeography sample byte lengths do not match the resolved grid";
                return false;
            }
            var elevations = new ushort[sampleCount];
            for (int index = 0; index < elevations.Length; index++)
                elevations[index] = (ushort)((elevationBytes[index * 2] << 8) |
                                             elevationBytes[index * 2 + 1]);

            if (!MacroGeographyPlan.TryCreate(
                    settings,
                    worldBounds,
                    elevations,
                    landformBytes,
                    out geography,
                    out string validationError))
            {
                error = "macroGeography failed validation: " + validationError;
                return false;
            }
            if (!string.Equals(data.canonicalHash, geography.CanonicalHash, StringComparison.Ordinal))
            {
                error = "macroGeography.canonicalHash mismatch; persisted '" +
                        Safe(data.canonicalHash) + "', reconstructed '" +
                        geography.CanonicalHash + "'.";
                geography = null;
                return false;
            }
            return true;
        }

        private static bool TryReadCommon(
            string rawWorldId,
            string displayName,
            WorldGenerationContextSaveData generationContext,
            string rawActiveSectorId,
            WorldContentEvidenceSaveData creationContentProvenance,
            out WorldId worldId,
            out string validatedDisplayName,
            out WorldGenerationContext context,
            out SectorId activeSectorId,
            out WorldCreationContentEvidence contentEvidence,
            out string error)
        {
            worldId = default;
            validatedDisplayName = displayName;
            context = null;
            activeSectorId = default;
            contentEvidence = null;
            error = null;
            if (!WorldId.TryParse(rawWorldId, out worldId, out string worldIdError))
            {
                error = "worldId is invalid: " + worldIdError + ".";
                return false;
            }
            if (generationContext == null)
            {
                error = "generationContext is required";
                return false;
            }
            if (!WorldSeed.TryParse(generationContext.worldSeed, out WorldSeed seed, out string seedError))
            {
                error = "generationContext.worldSeed is invalid: " + seedError + ".";
                return false;
            }
            if (!GeneratorVersion.TryParse(
                    generationContext.generatorVersion,
                    out GeneratorVersion generatorVersion,
                    out string versionError))
            {
                error = "generationContext.generatorVersion is invalid: " + versionError + ".";
                return false;
            }
            context = new WorldGenerationContext(seed, generatorVersion);
            if (!SectorId.TryParse(rawActiveSectorId, out activeSectorId, out string activeSectorError))
            {
                error = "activeSectorId is invalid: " + activeSectorError + ".";
                return false;
            }
            if (!TryReadContentEvidence(creationContentProvenance, out contentEvidence, out error))
                return false;
            return true;
        }

        private static bool TryReadTopology(
            WorldTopologySaveData data,
            string path,
            out WorldTopology topology,
            out string error)
        {
            topology = null;
            error = null;
            if (data == null)
            {
                error = path + " is required";
                return false;
            }
            if (data.sectors == null || data.connections == null)
            {
                error = path + ".sectors and .connections are required";
                return false;
            }
            var sectors = new List<SectorId>(data.sectors.Length);
            for (int index = 0; index < data.sectors.Length; index++)
            {
                if (!SectorId.TryParse(data.sectors[index], out SectorId sectorId, out string sectorError))
                {
                    error = $"{path}.sectors[{index}] is invalid: {sectorError}.";
                    return false;
                }
                sectors.Add(sectorId);
            }
            var connections = new List<SectorConnection>(data.connections.Length);
            for (int index = 0; index < data.connections.Length; index++)
            {
                WorldConnectionSaveData connectionData = data.connections[index];
                if (connectionData == null)
                {
                    error = $"{path}.connections[{index}] is null";
                    return false;
                }
                if (!SectorId.TryParse(connectionData.firstSectorId, out SectorId first, out string firstError))
                {
                    error = $"{path}.connections[{index}].firstSectorId is invalid: {firstError}.";
                    return false;
                }
                if (!SectorId.TryParse(connectionData.secondSectorId, out SectorId second, out string secondError))
                {
                    error = $"{path}.connections[{index}].secondSectorId is invalid: {secondError}.";
                    return false;
                }
                try
                {
                    connections.Add(new SectorConnection(connectionData.connectionKey, first, second));
                }
                catch (ArgumentException exception)
                {
                    error = $"{path}.connections[{index}] is invalid: {exception.Message}";
                    return false;
                }
            }
            if (!WorldTopology.TryCreate(
                    sectors, connections, out topology, out WorldTopologyValidationResult validation))
            {
                error = path + " failed validation: " + validation.Description;
                return false;
            }
            if (!string.Equals(data.canonicalHash, topology.CanonicalHash, StringComparison.Ordinal))
            {
                error = $"{path}.canonicalHash mismatch; persisted '{Safe(data.canonicalHash)}', " +
                        $"reconstructed '{topology.CanonicalHash}'.";
                topology = null;
                return false;
            }
            return true;
        }

        private static bool TryReadContentEvidence(
            WorldContentEvidenceSaveData data,
            out WorldCreationContentEvidence contentEvidence,
            out string error)
        {
            contentEvidence = null;
            error = null;
            if (data == null || data.sources == null)
            {
                error = "creationContentProvenance and its sources are required";
                return false;
            }
            var sources = new List<WorldCreationContentSourceEvidence>(data.sources.Length);
            for (int index = 0; index < data.sources.Length; index++)
            {
                WorldContentSourceEvidenceSaveData source = data.sources[index];
                if (source == null)
                {
                    error = $"creationContentProvenance.sources[{index}] is null";
                    return false;
                }
                sources.Add(new WorldCreationContentSourceEvidence(
                    source.sourceId,
                    source.ownedNamespace,
                    source.version,
                    source.isOfficialCore,
                    source.provenanceFingerprint));
            }
            if (!WorldCreationContentEvidence.TryCreate(
                    data.loadedContentSetFingerprint, sources, out contentEvidence, out string evidenceError))
            {
                error = "creationContentProvenance is invalid: " + evidenceError + ".";
                return false;
            }
            return true;
        }

        private static WorldSessionV1SaveData BuildLegacySaveData(WorldSession session)
        {
            return new WorldSessionV1SaveData
            {
                snapshotType = SnapshotType,
                schemaVersion = LegacySchemaVersion,
                worldId = session.WorldId.Canonical,
                displayName = session.DisplayName,
                generationContext = BuildGenerationContext(session),
                topology = BuildTopology(session.Topology),
                activeSectorId = session.ActiveSectorId.Canonical,
                creationContentProvenance = BuildContentEvidence(session.CreationContentEvidence)
            };
        }

        private static WorldSessionV2SaveData BuildMacroPlanSaveData(WorldSession session)
        {
            return new WorldSessionV2SaveData
            {
                snapshotType = SnapshotType,
                schemaVersion = MacroPlanSchemaVersion,
                worldId = session.WorldId.Canonical,
                displayName = session.DisplayName,
                generationContext = BuildGenerationContext(session),
                macroWorldPlan = BuildMacroWorldPlan(session.MacroWorldPlan),
                activeSectorId = session.ActiveSectorId.Canonical,
                creationContentProvenance = BuildContentEvidence(session.CreationContentEvidence)
            };
        }

        private static WorldSessionV3SaveData BuildCurrentSaveData(WorldSession session)
        {
            MacroGeographyPlan geography = session.MacroGeography;
            ushort[] elevationSamples = geography.CopyElevationSamples();
            var elevationBytes = new byte[elevationSamples.Length * 2];
            for (int index = 0; index < elevationSamples.Length; index++)
            {
                elevationBytes[index * 2] = (byte)(elevationSamples[index] >> 8);
                elevationBytes[index * 2 + 1] = (byte)elevationSamples[index];
            }
            MacroGeographyGenerationSettings geographySettings = geography.GenerationSettings;
            return new WorldSessionV3SaveData
            {
                snapshotType = SnapshotType,
                schemaVersion = CurrentSchemaVersion,
                worldId = session.WorldId.Canonical,
                displayName = session.DisplayName,
                generationContext = BuildGenerationContext(session),
                macroWorldPlan = BuildMacroWorldPlan(session.MacroWorldPlan),
                macroGeography = new MacroGeographySaveData
                {
                    generationSettings = new MacroGeographyGenerationSettingsSaveData
                    {
                        generationContract = geographySettings.GenerationContract,
                        sampleColumns = geographySettings.SampleColumns,
                        sampleRows = geographySettings.SampleRows,
                        regionalFrequencyQ16 = geographySettings.RegionalFrequencyQ16,
                        baseElevationFrequencyQ16 = geographySettings.BaseElevationFrequencyQ16,
                        detailFrequencyQ16 = geographySettings.DetailFrequencyQ16,
                        roughnessFrequencyQ16 = geographySettings.RoughnessFrequencyQ16,
                        resolvedAttempt = geographySettings.ResolvedAttempt
                    },
                    elevationSamplesBase64 = Convert.ToBase64String(elevationBytes),
                    landformSamplesBase64 = Convert.ToBase64String(geography.CopyLandformSamples()),
                    canonicalHash = geography.CanonicalHash
                },
                activeSectorId = session.ActiveSectorId.Canonical,
                creationContentProvenance = BuildContentEvidence(session.CreationContentEvidence)
            };
        }

        private static MacroWorldPlanSaveData BuildMacroWorldPlan(MacroWorldPlan plan)
        {
            var placements = new MacroSectorPlacementSaveData[plan.SectorPlacements.Count];
            for (int index = 0; index < placements.Length; index++)
            {
                MacroSectorPlacement placement = plan.SectorPlacements[index];
                placements[index] = new MacroSectorPlacementSaveData
                {
                    sectorId = placement.SectorId.Canonical,
                    x = placement.Position.X,
                    y = placement.Position.Y
                };
            }
            return new MacroWorldPlanSaveData
            {
                generationSettings = new WorldGenerationSettingsSaveData
                {
                    worldSizePreset = WorldGenerationSettings.ToCanonical(
                        plan.GenerationSettings.WorldSizePreset),
                    resolvedSectorCount = plan.GenerationSettings.ResolvedSectorCount,
                    resolvedWorldWidth = plan.GenerationSettings.ResolvedWorldWidth,
                    resolvedWorldHeight = plan.GenerationSettings.ResolvedWorldHeight,
                    resolvedMinimumSectorSpacing = plan.GenerationSettings.ResolvedMinimumSectorSpacing
                },
                worldBounds = new FiniteMacroWorldBoundsSaveData
                {
                    minX = plan.WorldBounds.MinX,
                    minY = plan.WorldBounds.MinY,
                    maxXExclusive = plan.WorldBounds.MaxXExclusive,
                    maxYExclusive = plan.WorldBounds.MaxYExclusive
                },
                sectorPlacements = placements,
                topology = BuildTopology(plan.Topology),
                canonicalHash = plan.CanonicalHash
            };
        }

        private static WorldGenerationContextSaveData BuildGenerationContext(WorldSession session)
        {
            return new WorldGenerationContextSaveData
            {
                worldSeed = session.GenerationContext.WorldSeed.Canonical,
                generatorVersion = session.GenerationContext.GeneratorVersion.Canonical
            };
        }

        private static WorldTopologySaveData BuildTopology(WorldTopology topology)
        {
            var sectors = new string[topology.Sectors.Count];
            for (int index = 0; index < sectors.Length; index++)
                sectors[index] = topology.Sectors[index].Canonical;
            var connections = new WorldConnectionSaveData[topology.Connections.Count];
            for (int index = 0; index < connections.Length; index++)
            {
                SectorConnection connection = topology.Connections[index];
                connections[index] = new WorldConnectionSaveData
                {
                    connectionKey = connection.ConnectionKey,
                    firstSectorId = connection.FirstEndpoint.Canonical,
                    secondSectorId = connection.SecondEndpoint.Canonical
                };
            }
            return new WorldTopologySaveData
            {
                canonicalHash = topology.CanonicalHash,
                sectors = sectors,
                connections = connections
            };
        }

        private static WorldContentEvidenceSaveData BuildContentEvidence(
            WorldCreationContentEvidence evidence)
        {
            var sources = new WorldContentSourceEvidenceSaveData[evidence.Sources.Count];
            for (int index = 0; index < sources.Length; index++)
            {
                WorldCreationContentSourceEvidence source = evidence.Sources[index];
                sources[index] = new WorldContentSourceEvidenceSaveData
                {
                    sourceId = source.SourceId,
                    ownedNamespace = source.OwnedNamespace,
                    version = source.Version,
                    isOfficialCore = source.IsOfficialCore,
                    provenanceFingerprint = source.ProvenanceFingerprint
                };
            }
            return new WorldContentEvidenceSaveData
            {
                loadedContentSetFingerprint = evidence.LoadedContentSetFingerprint,
                sources = sources
            };
        }

        private static WorldSessionPersistenceResult Malformed(string failure)
        {
            return Fail(WorldSessionPersistenceFailureCode.MalformedPayload, "Deserialize", failure);
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
        private sealed class WorldSessionV1SaveData
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
        private sealed class WorldSessionV2SaveData
        {
            public string snapshotType;
            public int schemaVersion;
            public string worldId;
            public string displayName;
            public WorldGenerationContextSaveData generationContext;
            public MacroWorldPlanSaveData macroWorldPlan;
            public string activeSectorId;
            public WorldContentEvidenceSaveData creationContentProvenance;
        }

        [Serializable]
        private sealed class WorldSessionV3SaveData
        {
            public string snapshotType;
            public int schemaVersion;
            public string worldId;
            public string displayName;
            public WorldGenerationContextSaveData generationContext;
            public MacroWorldPlanSaveData macroWorldPlan;
            public MacroGeographySaveData macroGeography;
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
        private sealed class MacroWorldPlanSaveData
        {
            public WorldGenerationSettingsSaveData generationSettings;
            public FiniteMacroWorldBoundsSaveData worldBounds;
            public MacroSectorPlacementSaveData[] sectorPlacements;
            public WorldTopologySaveData topology;
            public string canonicalHash;
        }

        [Serializable]
        private sealed class MacroGeographySaveData
        {
            public MacroGeographyGenerationSettingsSaveData generationSettings;
            public string elevationSamplesBase64;
            public string landformSamplesBase64;
            public string canonicalHash;
        }

        [Serializable]
        private sealed class MacroGeographyGenerationSettingsSaveData
        {
            public string generationContract;
            public int sampleColumns;
            public int sampleRows;
            public int regionalFrequencyQ16;
            public int baseElevationFrequencyQ16;
            public int detailFrequencyQ16;
            public int roughnessFrequencyQ16;
            public int resolvedAttempt;
        }

        [Serializable]
        private sealed class WorldGenerationSettingsSaveData
        {
            public string worldSizePreset;
            public int resolvedSectorCount;
            public long resolvedWorldWidth;
            public long resolvedWorldHeight;
            public long resolvedMinimumSectorSpacing;
        }

        [Serializable]
        private sealed class FiniteMacroWorldBoundsSaveData
        {
            public long minX;
            public long minY;
            public long maxXExclusive;
            public long maxYExclusive;
        }

        [Serializable]
        private sealed class MacroSectorPlacementSaveData
        {
            public string sectorId;
            public long x;
            public long y;
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
