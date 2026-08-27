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
    /// file store. Schema 6 persists Macro Climate V1. Schemas 1-5 remain
    /// explicit legacy shapes and never receive fabricated later-pass truth.
    /// </summary>
    public static class WorldSessionPersistenceService
    {
        public const string SnapshotType = "world_session_v1";
        public const int LegacySchemaVersion = 1;
        public const int MacroPlanSchemaVersion = 2;
        public const int MacroGeographySchemaVersion = 3;
        public const int MacroWaterSchemaVersion = 4;
        public const int MacroHumanGeographySchemaVersion = 5;
        public const int CurrentSchemaVersion = 6;

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
            if (session.IsLegacySchemaV3)
                return JToken.FromObject(BuildMacroGeographySaveData(session), PayloadSerializer);
            if (session.IsLegacySchemaV4)
                return JToken.FromObject(BuildMacroWaterSaveData(session), PayloadSerializer);
            if (session.IsLegacySchemaV5)
                return JToken.FromObject(BuildMacroHumanGeographySaveData(session), PayloadSerializer);
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
                if (schemaVersion == MacroGeographySchemaVersion)
                    return PreflightMacroGeography(payload.ToObject<WorldSessionV3SaveData>(PayloadSerializer));
                if (schemaVersion == MacroWaterSchemaVersion)
                    return PreflightMacroWater(payload.ToObject<WorldSessionV4SaveData>(PayloadSerializer));
                if (schemaVersion == MacroHumanGeographySchemaVersion)
                    return PreflightMacroHumanGeography(
                        payload.ToObject<WorldSessionV5SaveData>(PayloadSerializer));
                if (schemaVersion == CurrentSchemaVersion)
                    return PreflightCurrent(payload.ToObject<WorldSessionV6SaveData>(PayloadSerializer));
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

        private static WorldSessionPersistenceResult PreflightMacroGeography(WorldSessionV3SaveData data)
        {
            if (data == null)
                return SemanticFailure("Legacy schema-3 World Session payload deserialized to null.");
            if (data.snapshotType != SnapshotType || data.schemaVersion != MacroGeographySchemaVersion)
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
            if (!WorldSession.TryCreateLegacySchemaV3(
                    worldId, displayName, context, plan, geography, activeSectorId, contentEvidence,
                    out WorldSession session, out string sessionError))
            {
                return SemanticFailure(sessionError + ".");
            }
            return Success("SemanticPreflightLegacyV3", session);
        }

        private static WorldSessionPersistenceResult PreflightMacroWater(WorldSessionV4SaveData data)
        {
            if (data == null)
                return SemanticFailure("Legacy schema-4 World Session payload deserialized to null.");
            if (data.snapshotType != SnapshotType || data.schemaVersion != MacroWaterSchemaVersion)
                return SemanticFailure("World Session schema-4 header is inconsistent.");
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
                return SemanticFailure(geographyError);
            if (!TryReadMacroWater(
                    data.macroWater, geography, out MacroWaterPlan water, out string waterError))
                return SemanticFailure(waterError);
            if (!WorldGameplayQualityAnalyzer.TryAnalyze(
                    plan, geography, water,
                    out WorldGameplayQualityAnalysis quality, out string qualityError))
                return SemanticFailure("gameplay-quality analysis failed: " + qualityError + ".");
            if (!quality.MeetsHardRequirements)
                return SemanticFailure("persisted world fails hard gameplay-quality preflight: " +
                                       string.Join(" | ", quality.HardFailures) + ".");
            if (!WorldSession.TryCreateLegacySchemaV4(
                    worldId, displayName, context, plan, geography, water, quality,
                    activeSectorId, contentEvidence,
                    out WorldSession session, out string sessionError))
            {
                return SemanticFailure(sessionError + ".");
            }
            return Success("SemanticPreflightLegacyV4", session);
        }

        private static WorldSessionPersistenceResult PreflightMacroHumanGeography(
            WorldSessionV5SaveData data)
        {
            if (data == null)
                return SemanticFailure("Legacy schema-5 World Session payload deserialized to null.");
            if (data.snapshotType != SnapshotType ||
                data.schemaVersion != MacroHumanGeographySchemaVersion)
                return SemanticFailure("World Session schema-5 header is inconsistent.");
            if (!TryReadCommon(
                    data.worldId, data.displayName, data.generationContext, data.activeSectorId,
                    data.creationContentProvenance,
                    out WorldId worldId, out string displayName, out WorldGenerationContext context,
                    out SectorId activeSectorId, out WorldCreationContentEvidence contentEvidence,
                    out string commonError))
                return SemanticFailure(commonError);
            if (!TryReadMacroWorldPlan(data.macroWorldPlan, out MacroWorldPlan plan, out string planError))
                return SemanticFailure(planError);
            if (!TryReadMacroGeography(
                    data.macroGeography, plan.WorldBounds,
                    out MacroGeographyPlan geography, out string geographyError))
                return SemanticFailure(geographyError);
            if (!TryReadMacroWater(
                    data.macroWater, geography, out MacroWaterPlan water, out string waterError))
                return SemanticFailure(waterError);
            if (!WorldGameplayQualityAnalyzer.TryAnalyze(
                    plan, geography, water,
                    out WorldGameplayQualityAnalysis quality, out string qualityError))
                return SemanticFailure("gameplay-quality analysis failed: " + qualityError + ".");
            if (!quality.MeetsHardRequirements)
                return SemanticFailure("persisted world fails hard gameplay-quality preflight: " +
                                       string.Join(" | ", quality.HardFailures) + ".");
            if (!TryReadMacroHumanGeography(
                    data.macroHumanGeography, plan, geography, water, quality,
                    out MacroHumanGeographyPlan human, out string humanError))
                return SemanticFailure(humanError);
            if (!WorldSession.TryCreateLegacySchemaV5(
                    worldId, displayName, context, plan, geography, water, quality, human,
                    activeSectorId, contentEvidence,
                    out WorldSession session, out string sessionError))
                return SemanticFailure(sessionError + ".");
            return Success("SemanticPreflightLegacyV5", session);
        }

        private static WorldSessionPersistenceResult PreflightCurrent(WorldSessionV6SaveData data)
        {
            if (data == null)
                return SemanticFailure("World Session payload deserialized to null.");
            if (data.snapshotType != SnapshotType || data.schemaVersion != CurrentSchemaVersion)
                return SemanticFailure("World Session schema-6 header is inconsistent.");
            if (!TryReadCommon(
                    data.worldId, data.displayName, data.generationContext, data.activeSectorId,
                    data.creationContentProvenance,
                    out WorldId worldId, out string displayName, out WorldGenerationContext context,
                    out SectorId activeSectorId, out WorldCreationContentEvidence contentEvidence,
                    out string commonError))
                return SemanticFailure(commonError);
            if (!TryReadMacroWorldPlan(data.macroWorldPlan, out MacroWorldPlan plan, out string planError))
                return SemanticFailure(planError);
            if (!TryReadMacroGeography(
                    data.macroGeography, plan.WorldBounds,
                    out MacroGeographyPlan geography, out string geographyError))
                return SemanticFailure(geographyError);
            if (!TryReadMacroWater(
                    data.macroWater, geography, out MacroWaterPlan water, out string waterError))
                return SemanticFailure(waterError);
            if (!TryReadMacroClimate(
                    data.macroClimate, geography, water,
                    out MacroClimatePlan climate, out string climateError))
                return SemanticFailure(climateError);
            if (!WorldGameplayQualityAnalyzer.TryAnalyze(
                    plan, geography, water,
                    out WorldGameplayQualityAnalysis quality, out string qualityError))
                return SemanticFailure("gameplay-quality analysis failed: " + qualityError + ".");
            if (!quality.MeetsHardRequirements)
                return SemanticFailure("persisted world fails hard gameplay-quality preflight: " +
                                       string.Join(" | ", quality.HardFailures) + ".");
            if (!TryReadMacroHumanGeography(
                    data.macroHumanGeography, plan, geography, water, quality,
                    out MacroHumanGeographyPlan human, out string humanError))
                return SemanticFailure(humanError);
            if (!WorldSession.TryCreate(
                    worldId, displayName, context, plan, geography, water, climate, quality, human,
                    activeSectorId, contentEvidence,
                    out WorldSession session, out string sessionError))
                return SemanticFailure(sessionError + ".");
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

        private static bool TryReadMacroWater(
            MacroWaterSaveData data,
            MacroGeographyPlan geography,
            out MacroWaterPlan water,
            out string error)
        {
            water = null;
            error = null;
            if (data == null || data.generationSettings == null)
            {
                error = "macroWater and its generationSettings are required";
                return false;
            }
            MacroWaterGenerationSettingsSaveData settingsData = data.generationSettings;
            if (!MacroWaterGenerationSettings.TryParseCoverage(
                    settingsData.landCoverage, out LandCoveragePreset coverage,
                    out string coverageError))
            {
                error = "macroWater.generationSettings is invalid: " + coverageError + ".";
                return false;
            }
            if (!MacroWaterGenerationSettings.TryCreateResolved(
                    settingsData.generationContract,
                    coverage,
                    settingsData.sampleColumns,
                    settingsData.sampleRows,
                    settingsData.targetLandRatioQ16,
                    settingsData.minimumBasinCells,
                    out MacroWaterGenerationSettings settings,
                    out string settingsError))
            {
                error = "macroWater.generationSettings is invalid: " + settingsError + ".";
                return false;
            }

            if (!TryDecodeBase64(data.oceanMaskBase64, "macroWater.oceanMaskBase64",
                    out byte[] ocean, out error) ||
                !TryDecodeBase64(data.oceanBodyLabelsBase64, "macroWater.oceanBodyLabelsBase64",
                    out byte[] labelBytes, out error) ||
                !TryDecodeBase64(data.coastlineMaskBase64, "macroWater.coastlineMaskBase64",
                    out byte[] coastline, out error) ||
                !TryDecodeBase64(data.conditionedElevationBase64, "macroWater.conditionedElevationBase64",
                    out byte[] conditionedBytes, out error) ||
                !TryDecodeBase64(data.drainageDirectionsBase64, "macroWater.drainageDirectionsBase64",
                    out byte[] drainage, out error))
            {
                return false;
            }

            int sampleCount = checked(settings.SampleColumns * settings.SampleRows);
            if (ocean.Length != sampleCount || coastline.Length != sampleCount ||
                drainage.Length != sampleCount || labelBytes.Length != sampleCount * 2 ||
                conditionedBytes.Length != sampleCount * 2)
            {
                error = "macroWater raster byte lengths do not match the resolved grid";
                return false;
            }
            var labels = new ushort[sampleCount];
            var conditioned = new ushort[sampleCount];
            for (int index = 0; index < sampleCount; index++)
            {
                labels[index] = (ushort)((labelBytes[index * 2] << 8) | labelBytes[index * 2 + 1]);
                conditioned[index] = (ushort)((conditionedBytes[index * 2] << 8) |
                                               conditionedBytes[index * 2 + 1]);
            }
            var basins = new List<MacroBasinCandidate>();
            if (data.basinCandidates == null)
            {
                error = "macroWater.basinCandidates must be a present array";
                return false;
            }
            for (int index = 0; index < data.basinCandidates.Length; index++)
            {
                MacroBasinCandidateSaveData basin = data.basinCandidates[index];
                if (basin == null || basin.spillElevation < 0 || basin.spillElevation > ushort.MaxValue ||
                    basin.maximumFillDepth < 0 || basin.maximumFillDepth > ushort.MaxValue)
                {
                    error = "macroWater.basinCandidates[" + index + "] is malformed";
                    return false;
                }
                basins.Add(new MacroBasinCandidate(
                    basin.representativeSampleIndex,
                    basin.sampleCount,
                    (ushort)basin.spillElevation,
                    (ushort)basin.maximumFillDepth));
            }
            if (data.seaLevel < 0 || data.seaLevel > ushort.MaxValue)
            {
                error = "macroWater.seaLevel is outside the committed ushort range";
                return false;
            }
            if (!MacroWaterPlan.TryCreate(
                    settings, geography, (ushort)data.seaLevel, ocean, labels, coastline,
                    conditioned, drainage, basins, out water, out string validationError))
            {
                error = "macroWater failed validation: " + validationError;
                return false;
            }
            if (!string.Equals(data.canonicalHash, water.CanonicalHash, StringComparison.Ordinal))
            {
                error = "macroWater.canonicalHash mismatch; persisted '" +
                        Safe(data.canonicalHash) + "', reconstructed '" + water.CanonicalHash + "'.";
                water = null;
                return false;
            }
            return true;
        }

        private static bool TryReadMacroHumanGeography(
            MacroHumanGeographySaveData data,
            MacroWorldPlan worldPlan,
            MacroGeographyPlan geography,
            MacroWaterPlan water,
            WorldGameplayQualityAnalysis worldQuality,
            out MacroHumanGeographyPlan human,
            out string error)
        {
            human = null;
            error = null;
            if (data == null || data.generationSettings == null)
            {
                error = "macroHumanGeography and its generationSettings are required";
                return false;
            }
            MacroHumanGeographyGenerationSettingsSaveData settingsData = data.generationSettings;
            if (!MacroHumanGeographyGenerationSettings.TryCreateResolved(
                    settingsData.generationContract,
                    settingsData.sampleColumns,
                    settingsData.sampleRows,
                    settingsData.regionalHubTarget,
                    settingsData.localHubTarget,
                    settingsData.minimumRegionalSpacingCells,
                    settingsData.minimumLocalSpacingCells,
                    settingsData.extraPrimaryLinkTarget,
                    out MacroHumanGeographyGenerationSettings settings,
                    out string settingsError))
            {
                error = "macroHumanGeography.generationSettings is invalid: " + settingsError + ".";
                return false;
            }
            if (!SectorId.TryParse(
                    data.starterAccessSectorId, out SectorId starterAccessSectorId,
                    out string starterError))
            {
                error = "macroHumanGeography.starterAccessSectorId is invalid: " + starterError + ".";
                return false;
            }
            if (data.sites == null || data.roads == null)
            {
                error = "macroHumanGeography.sites and roads must be present arrays";
                return false;
            }

            var sites = new List<MacroHumanSite>(data.sites.Length);
            for (int index = 0; index < data.sites.Length; index++)
            {
                MacroHumanSiteSaveData site = data.sites[index];
                if (site == null)
                {
                    error = "macroHumanGeography.sites[" + index + "] is null.";
                    return false;
                }
                if (!MacroHumanSiteId.TryParse(
                        site.siteId, out MacroHumanSiteId siteId, out string siteIdError))
                {
                    error = "macroHumanGeography.sites[" + index + "].siteId is invalid: " +
                            siteIdError + ".";
                    return false;
                }
                if (!TryParseHubKind(site.kind, out MacroHumanHubKind kind))
                {
                    error = "macroHumanGeography.sites[" + index + "].kind is invalid.";
                    return false;
                }
                if (site.landComponentId < 1)
                {
                    error = "macroHumanGeography.sites[" + index + "].landComponentId must be positive.";
                    return false;
                }
                sites.Add(new MacroHumanSite(
                    siteId, kind, new MacroPoint2D(site.x, site.y), site.landComponentId));
            }

            var roads = new List<MacroRoad>(data.roads.Length);
            for (int index = 0; index < data.roads.Length; index++)
            {
                MacroRoadSaveData road = data.roads[index];
                if (road == null)
                {
                    error = "macroHumanGeography.roads[" + index + "] is null.";
                    return false;
                }
                if (!MacroRoadId.TryParse(
                        road.roadId, out MacroRoadId roadId, out string roadIdError))
                {
                    error = "macroHumanGeography.roads[" + index + "].roadId is invalid: " +
                            roadIdError + ".";
                    return false;
                }
                if (!TryParseRoadClass(road.roadClass, out MacroRoadClass roadClass))
                {
                    error = "macroHumanGeography.roads[" + index + "].roadClass is invalid.";
                    return false;
                }
                if (!MacroHumanSiteId.TryParse(
                        road.firstEndpoint, out MacroHumanSiteId first, out string firstError))
                {
                    error = "macroHumanGeography.roads[" + index + "].firstEndpoint is invalid: " +
                            firstError + ".";
                    return false;
                }
                if (!MacroHumanSiteId.TryParse(
                        road.secondEndpoint, out MacroHumanSiteId second, out string secondError))
                {
                    error = "macroHumanGeography.roads[" + index + "].secondEndpoint is invalid: " +
                            secondError + ".";
                    return false;
                }
                if (first == second)
                {
                    error = "macroHumanGeography.roads[" + index + "] endpoints must be distinct.";
                    return false;
                }
                if (road.polyline == null || road.polyline.Length < 2 ||
                    road.routedCellCount < 2 || road.totalTraversalCost < 1)
                {
                    error = "macroHumanGeography.roads[" + index + "] has invalid geometry/cost metadata.";
                    return false;
                }
                var points = new List<MacroPoint2D>(road.polyline.Length);
                for (int point = 0; point < road.polyline.Length; point++)
                {
                    MacroPoint2DSaveData value = road.polyline[point];
                    if (value == null)
                    {
                        error = "macroHumanGeography.roads[" + index + "].polyline[" + point + "] is null.";
                        return false;
                    }
                    points.Add(new MacroPoint2D(value.x, value.y));
                }
                roads.Add(new MacroRoad(
                    roadId, roadClass, first, second, points,
                    road.routedCellCount, road.totalTraversalCost));
            }

            if (!MacroHumanGeographyPlan.TryCreate(
                    settings, worldPlan, geography, water, worldQuality,
                    starterAccessSectorId, sites, roads, out human, out string validationError))
            {
                error = "macroHumanGeography failed validation: " + validationError;
                return false;
            }
            if (!string.Equals(data.canonicalHash, human.CanonicalHash, StringComparison.Ordinal))
            {
                error = "macroHumanGeography.canonicalHash mismatch; persisted '" +
                        Safe(data.canonicalHash) + "', reconstructed '" + human.CanonicalHash + "'.";
                human = null;
                return false;
            }
            return true;
        }

        private static bool TryReadMacroClimate(
            MacroClimateSaveData data,
            MacroGeographyPlan geography,
            MacroWaterPlan water,
            out MacroClimatePlan climate,
            out string error)
        {
            climate = null;
            error = null;
            if (data == null || data.generationSettings == null)
            {
                error = "macroClimate and its generationSettings are required";
                return false;
            }

            MacroClimateGenerationSettingsSaveData settingsData = data.generationSettings;
            if (!MacroClimateGenerationSettings.TryParseDirection(
                    settingsData.prevailingMoistureDirection,
                    out MacroMoistureDirection direction,
                    out string directionError))
            {
                error = "macroClimate.generationSettings.prevailingMoistureDirection is invalid: " +
                        directionError + ".";
                return false;
            }
            if (!MacroClimateGenerationSettings.TryCreateResolved(
                    settingsData.generationContract,
                    settingsData.sampleColumns,
                    settingsData.sampleRows,
                    settingsData.thermalRegionalFrequencyQ16,
                    settingsData.moistureRegionalFrequencyQ16,
                    settingsData.southThermalBaselineQ16,
                    settingsData.northThermalBaselineQ16,
                    settingsData.thermalRegionalAmplitudeQ16,
                    settingsData.elevationCoolingQ16,
                    settingsData.moistureBaselineQ16,
                    settingsData.moistureRegionalAmplitudeQ16,
                    settingsData.oceanInfluenceMaximumQ16,
                    settingsData.oceanInfluenceDistanceCells,
                    settingsData.orographicLookbackCells,
                    settingsData.orographicRiseThresholdQ16,
                    settingsData.windwardMaximumBoostQ16,
                    settingsData.leewardMaximumReductionQ16,
                    settingsData.orographicResponseDivisor,
                    direction,
                    out MacroClimateGenerationSettings settings,
                    out string settingsError))
            {
                error = "macroClimate.generationSettings is invalid: " + settingsError + ".";
                return false;
            }

            if (!TryDecodeBase64(
                    data.thermalSamplesBase64,
                    "macroClimate.thermalSamplesBase64",
                    out byte[] thermalBytes,
                    out error) ||
                !TryDecodeBase64(
                    data.moistureSamplesBase64,
                    "macroClimate.moistureSamplesBase64",
                    out byte[] moistureBytes,
                    out error))
            {
                return false;
            }

            int sampleCount = checked(settings.SampleColumns * settings.SampleRows);
            if (thermalBytes.Length != sampleCount * 2 ||
                moistureBytes.Length != sampleCount * 2)
            {
                error = "macroClimate sample byte lengths do not match the resolved grid";
                return false;
            }
            var thermal = new ushort[sampleCount];
            var moisture = new ushort[sampleCount];
            for (int index = 0; index < sampleCount; index++)
            {
                thermal[index] = (ushort)((thermalBytes[index * 2] << 8) |
                                           thermalBytes[index * 2 + 1]);
                moisture[index] = (ushort)((moistureBytes[index * 2] << 8) |
                                            moistureBytes[index * 2 + 1]);
            }

            if (!MacroClimatePlan.TryCreate(
                    settings, geography, water, thermal, moisture,
                    out climate, out string validationError))
            {
                error = "macroClimate failed validation: " + validationError;
                return false;
            }
            if (!string.Equals(data.canonicalHash, climate.CanonicalHash, StringComparison.Ordinal))
            {
                error = "macroClimate.canonicalHash mismatch; persisted '" +
                        Safe(data.canonicalHash) + "', reconstructed '" +
                        climate.CanonicalHash + "'.";
                climate = null;
                return false;
            }
            return true;
        }

        private static bool TryParseHubKind(string value, out MacroHumanHubKind kind)
        {
            if (string.Equals(value, "regional_hub", StringComparison.Ordinal))
            {
                kind = MacroHumanHubKind.RegionalHub;
                return true;
            }
            if (string.Equals(value, "local_hub", StringComparison.Ordinal))
            {
                kind = MacroHumanHubKind.LocalHub;
                return true;
            }
            kind = default;
            return false;
        }

        private static bool TryParseRoadClass(string value, out MacroRoadClass roadClass)
        {
            if (string.Equals(value, "primary", StringComparison.Ordinal))
            {
                roadClass = MacroRoadClass.Primary;
                return true;
            }
            if (string.Equals(value, "secondary", StringComparison.Ordinal))
            {
                roadClass = MacroRoadClass.Secondary;
                return true;
            }
            roadClass = default;
            return false;
        }

        private static bool TryDecodeBase64(
            string raw,
            string field,
            out byte[] bytes,
            out string error)
        {
            bytes = null;
            error = null;
            try
            {
                bytes = Convert.FromBase64String(raw ?? string.Empty);
                return true;
            }
            catch (FormatException exception)
            {
                error = field + " is not valid Base64: " + exception.Message;
                return false;
            }
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

        private static WorldSessionV3SaveData BuildMacroGeographySaveData(WorldSession session)
        {
            return new WorldSessionV3SaveData
            {
                snapshotType = SnapshotType,
                schemaVersion = MacroGeographySchemaVersion,
                worldId = session.WorldId.Canonical,
                displayName = session.DisplayName,
                generationContext = BuildGenerationContext(session),
                macroWorldPlan = BuildMacroWorldPlan(session.MacroWorldPlan),
                macroGeography = BuildMacroGeography(session.MacroGeography),
                activeSectorId = session.ActiveSectorId.Canonical,
                creationContentProvenance = BuildContentEvidence(session.CreationContentEvidence)
            };
        }

        private static WorldSessionV4SaveData BuildMacroWaterSaveData(WorldSession session)
        {
            return new WorldSessionV4SaveData
            {
                snapshotType = SnapshotType,
                schemaVersion = MacroWaterSchemaVersion,
                worldId = session.WorldId.Canonical,
                displayName = session.DisplayName,
                generationContext = BuildGenerationContext(session),
                macroWorldPlan = BuildMacroWorldPlan(session.MacroWorldPlan),
                macroGeography = BuildMacroGeography(session.MacroGeography),
                macroWater = BuildMacroWater(session.MacroWater),
                activeSectorId = session.ActiveSectorId.Canonical,
                creationContentProvenance = BuildContentEvidence(session.CreationContentEvidence)
            };
        }

        private static WorldSessionV5SaveData BuildMacroHumanGeographySaveData(WorldSession session)
        {
            return new WorldSessionV5SaveData
            {
                snapshotType = SnapshotType,
                schemaVersion = MacroHumanGeographySchemaVersion,
                worldId = session.WorldId.Canonical,
                displayName = session.DisplayName,
                generationContext = BuildGenerationContext(session),
                macroWorldPlan = BuildMacroWorldPlan(session.MacroWorldPlan),
                macroGeography = BuildMacroGeography(session.MacroGeography),
                macroWater = BuildMacroWater(session.MacroWater),
                macroHumanGeography = BuildMacroHumanGeography(session.MacroHumanGeography),
                activeSectorId = session.ActiveSectorId.Canonical,
                creationContentProvenance = BuildContentEvidence(session.CreationContentEvidence)
            };
        }

        private static WorldSessionV6SaveData BuildCurrentSaveData(WorldSession session)
        {
            return new WorldSessionV6SaveData
            {
                snapshotType = SnapshotType,
                schemaVersion = CurrentSchemaVersion,
                worldId = session.WorldId.Canonical,
                displayName = session.DisplayName,
                generationContext = BuildGenerationContext(session),
                macroWorldPlan = BuildMacroWorldPlan(session.MacroWorldPlan),
                macroGeography = BuildMacroGeography(session.MacroGeography),
                macroWater = BuildMacroWater(session.MacroWater),
                macroClimate = BuildMacroClimate(session.MacroClimate),
                macroHumanGeography = BuildMacroHumanGeography(session.MacroHumanGeography),
                activeSectorId = session.ActiveSectorId.Canonical,
                creationContentProvenance = BuildContentEvidence(session.CreationContentEvidence)
            };
        }

        private static MacroClimateSaveData BuildMacroClimate(MacroClimatePlan climate)
        {
            MacroClimateGenerationSettings settings = climate.GenerationSettings;
            ushort[] thermal = climate.CopyThermalSamples();
            ushort[] moisture = climate.CopyMoistureSamples();
            var thermalBytes = new byte[thermal.Length * 2];
            var moistureBytes = new byte[moisture.Length * 2];
            for (int index = 0; index < thermal.Length; index++)
            {
                thermalBytes[index * 2] = (byte)(thermal[index] >> 8);
                thermalBytes[index * 2 + 1] = (byte)thermal[index];
                moistureBytes[index * 2] = (byte)(moisture[index] >> 8);
                moistureBytes[index * 2 + 1] = (byte)moisture[index];
            }

            return new MacroClimateSaveData
            {
                generationSettings = new MacroClimateGenerationSettingsSaveData
                {
                    generationContract = settings.GenerationContract,
                    sampleColumns = settings.SampleColumns,
                    sampleRows = settings.SampleRows,
                    thermalRegionalFrequencyQ16 = settings.ThermalRegionalFrequencyQ16,
                    moistureRegionalFrequencyQ16 = settings.MoistureRegionalFrequencyQ16,
                    southThermalBaselineQ16 = settings.SouthThermalBaselineQ16,
                    northThermalBaselineQ16 = settings.NorthThermalBaselineQ16,
                    thermalRegionalAmplitudeQ16 = settings.ThermalRegionalAmplitudeQ16,
                    elevationCoolingQ16 = settings.ElevationCoolingQ16,
                    moistureBaselineQ16 = settings.MoistureBaselineQ16,
                    moistureRegionalAmplitudeQ16 = settings.MoistureRegionalAmplitudeQ16,
                    oceanInfluenceMaximumQ16 = settings.OceanInfluenceMaximumQ16,
                    oceanInfluenceDistanceCells = settings.OceanInfluenceDistanceCells,
                    orographicLookbackCells = settings.OrographicLookbackCells,
                    orographicRiseThresholdQ16 = settings.OrographicRiseThresholdQ16,
                    windwardMaximumBoostQ16 = settings.WindwardMaximumBoostQ16,
                    leewardMaximumReductionQ16 = settings.LeewardMaximumReductionQ16,
                    orographicResponseDivisor = settings.OrographicResponseDivisor,
                    prevailingMoistureDirection = MacroClimateGenerationSettings.ToCanonical(
                        settings.PrevailingMoistureDirection)
                },
                thermalSamplesBase64 = Convert.ToBase64String(thermalBytes),
                moistureSamplesBase64 = Convert.ToBase64String(moistureBytes),
                canonicalHash = climate.CanonicalHash
            };
        }

        private static MacroHumanGeographySaveData BuildMacroHumanGeography(MacroHumanGeographyPlan human)
        {
            MacroHumanGeographyGenerationSettings settings = human.GenerationSettings;
            var sites = new MacroHumanSiteSaveData[human.Sites.Count];
            for (int index = 0; index < sites.Length; index++)
            {
                MacroHumanSite site = human.Sites[index];
                sites[index] = new MacroHumanSiteSaveData
                {
                    siteId = site.SiteId.Canonical,
                    kind = site.Kind == MacroHumanHubKind.RegionalHub ? "regional_hub" : "local_hub",
                    x = site.Position.X,
                    y = site.Position.Y,
                    landComponentId = site.LandComponentId
                };
            }
            var roads = new MacroRoadSaveData[human.Roads.Count];
            for (int index = 0; index < roads.Length; index++)
            {
                MacroRoad road = human.Roads[index];
                var points = new MacroPoint2DSaveData[road.Polyline.Count];
                for (int point = 0; point < points.Length; point++)
                {
                    points[point] = new MacroPoint2DSaveData
                    {
                        x = road.Polyline[point].X,
                        y = road.Polyline[point].Y
                    };
                }
                roads[index] = new MacroRoadSaveData
                {
                    roadId = road.RoadId.Canonical,
                    roadClass = road.RoadClass == MacroRoadClass.Primary ? "primary" : "secondary",
                    firstEndpoint = road.FirstEndpoint.Canonical,
                    secondEndpoint = road.SecondEndpoint.Canonical,
                    polyline = points,
                    routedCellCount = road.RoutedCellCount,
                    totalTraversalCost = road.TotalTraversalCost
                };
            }
            return new MacroHumanGeographySaveData
            {
                generationSettings = new MacroHumanGeographyGenerationSettingsSaveData
                {
                    generationContract = settings.GenerationContract,
                    sampleColumns = settings.SampleColumns,
                    sampleRows = settings.SampleRows,
                    regionalHubTarget = settings.RegionalHubTarget,
                    localHubTarget = settings.LocalHubTarget,
                    minimumRegionalSpacingCells = settings.MinimumRegionalSpacingCells,
                    minimumLocalSpacingCells = settings.MinimumLocalSpacingCells,
                    extraPrimaryLinkTarget = settings.ExtraPrimaryLinkTarget
                },
                starterAccessSectorId = human.StarterAccessSectorId.Canonical,
                sites = sites,
                roads = roads,
                canonicalHash = human.CanonicalHash
            };
        }

        private static MacroGeographySaveData BuildMacroGeography(MacroGeographyPlan geography)
        {
            ushort[] elevationSamples = geography.CopyElevationSamples();
            var elevationBytes = new byte[elevationSamples.Length * 2];
            for (int index = 0; index < elevationSamples.Length; index++)
            {
                elevationBytes[index * 2] = (byte)(elevationSamples[index] >> 8);
                elevationBytes[index * 2 + 1] = (byte)elevationSamples[index];
            }
            MacroGeographyGenerationSettings geographySettings = geography.GenerationSettings;
            return new MacroGeographySaveData
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
            };
        }

        private static MacroWaterSaveData BuildMacroWater(MacroWaterPlan water)
        {
            MacroWaterGenerationSettings settings = water.GenerationSettings;
            ushort[] labels = water.CopyOceanBodyLabels();
            ushort[] conditioned = water.CopyConditionedElevations();
            var labelBytes = new byte[labels.Length * 2];
            var conditionedBytes = new byte[conditioned.Length * 2];
            for (int index = 0; index < labels.Length; index++)
            {
                labelBytes[index * 2] = (byte)(labels[index] >> 8);
                labelBytes[index * 2 + 1] = (byte)labels[index];
                conditionedBytes[index * 2] = (byte)(conditioned[index] >> 8);
                conditionedBytes[index * 2 + 1] = (byte)conditioned[index];
            }
            var basins = new MacroBasinCandidateSaveData[water.BasinCandidates.Count];
            for (int index = 0; index < basins.Length; index++)
            {
                MacroBasinCandidate basin = water.BasinCandidates[index];
                basins[index] = new MacroBasinCandidateSaveData
                {
                    representativeSampleIndex = basin.RepresentativeSampleIndex,
                    sampleCount = basin.SampleCount,
                    spillElevation = basin.SpillElevation,
                    maximumFillDepth = basin.MaximumFillDepth
                };
            }
            return new MacroWaterSaveData
            {
                generationSettings = new MacroWaterGenerationSettingsSaveData
                {
                    generationContract = settings.GenerationContract,
                    landCoverage = MacroWaterGenerationSettings.ToCanonical(settings.LandCoverage),
                    sampleColumns = settings.SampleColumns,
                    sampleRows = settings.SampleRows,
                    targetLandRatioQ16 = settings.TargetLandRatioQ16,
                    minimumBasinCells = settings.MinimumBasinCells
                },
                seaLevel = water.SeaLevel,
                oceanMaskBase64 = Convert.ToBase64String(water.CopyOceanMask()),
                oceanBodyLabelsBase64 = Convert.ToBase64String(labelBytes),
                coastlineMaskBase64 = Convert.ToBase64String(water.CopyCoastlineMask()),
                conditionedElevationBase64 = Convert.ToBase64String(conditionedBytes),
                drainageDirectionsBase64 = Convert.ToBase64String(water.CopyDrainageDirections()),
                basinCandidates = basins,
                canonicalHash = water.CanonicalHash
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
        private sealed class WorldSessionV4SaveData
        {
            public string snapshotType;
            public int schemaVersion;
            public string worldId;
            public string displayName;
            public WorldGenerationContextSaveData generationContext;
            public MacroWorldPlanSaveData macroWorldPlan;
            public MacroGeographySaveData macroGeography;
            public MacroWaterSaveData macroWater;
            public string activeSectorId;
            public WorldContentEvidenceSaveData creationContentProvenance;
        }

        [Serializable]
        private sealed class WorldSessionV5SaveData
        {
            public string snapshotType;
            public int schemaVersion;
            public string worldId;
            public string displayName;
            public WorldGenerationContextSaveData generationContext;
            public MacroWorldPlanSaveData macroWorldPlan;
            public MacroGeographySaveData macroGeography;
            public MacroWaterSaveData macroWater;
            public MacroHumanGeographySaveData macroHumanGeography;
            public string activeSectorId;
            public WorldContentEvidenceSaveData creationContentProvenance;
        }

        [Serializable]
        private sealed class WorldSessionV6SaveData
        {
            public string snapshotType;
            public int schemaVersion;
            public string worldId;
            public string displayName;
            public WorldGenerationContextSaveData generationContext;
            public MacroWorldPlanSaveData macroWorldPlan;
            public MacroGeographySaveData macroGeography;
            public MacroWaterSaveData macroWater;
            public MacroClimateSaveData macroClimate;
            public MacroHumanGeographySaveData macroHumanGeography;
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
        private sealed class MacroWaterSaveData
        {
            public MacroWaterGenerationSettingsSaveData generationSettings;
            public int seaLevel;
            public string oceanMaskBase64;
            public string oceanBodyLabelsBase64;
            public string coastlineMaskBase64;
            public string conditionedElevationBase64;
            public string drainageDirectionsBase64;
            public MacroBasinCandidateSaveData[] basinCandidates;
            public string canonicalHash;
        }

        [Serializable]
        private sealed class MacroWaterGenerationSettingsSaveData
        {
            public string generationContract;
            public string landCoverage;
            public int sampleColumns;
            public int sampleRows;
            public int targetLandRatioQ16;
            public int minimumBasinCells;
        }

        [Serializable]
        private sealed class MacroBasinCandidateSaveData
        {
            public int representativeSampleIndex;
            public int sampleCount;
            public int spillElevation;
            public int maximumFillDepth;
        }

        [Serializable]
        private sealed class MacroClimateSaveData
        {
            public MacroClimateGenerationSettingsSaveData generationSettings;
            public string thermalSamplesBase64;
            public string moistureSamplesBase64;
            public string canonicalHash;
        }

        [Serializable]
        private sealed class MacroClimateGenerationSettingsSaveData
        {
            public string generationContract;
            public int sampleColumns;
            public int sampleRows;
            public int thermalRegionalFrequencyQ16;
            public int moistureRegionalFrequencyQ16;
            public int southThermalBaselineQ16;
            public int northThermalBaselineQ16;
            public int thermalRegionalAmplitudeQ16;
            public int elevationCoolingQ16;
            public int moistureBaselineQ16;
            public int moistureRegionalAmplitudeQ16;
            public int oceanInfluenceMaximumQ16;
            public int oceanInfluenceDistanceCells;
            public int orographicLookbackCells;
            public int orographicRiseThresholdQ16;
            public int windwardMaximumBoostQ16;
            public int leewardMaximumReductionQ16;
            public int orographicResponseDivisor;
            public string prevailingMoistureDirection;
        }

        [Serializable]
        private sealed class MacroHumanGeographySaveData
        {
            public MacroHumanGeographyGenerationSettingsSaveData generationSettings;
            public string starterAccessSectorId;
            public MacroHumanSiteSaveData[] sites;
            public MacroRoadSaveData[] roads;
            public string canonicalHash;
        }

        [Serializable]
        private sealed class MacroHumanGeographyGenerationSettingsSaveData
        {
            public string generationContract;
            public int sampleColumns;
            public int sampleRows;
            public int regionalHubTarget;
            public int localHubTarget;
            public int minimumRegionalSpacingCells;
            public int minimumLocalSpacingCells;
            public int extraPrimaryLinkTarget;
        }

        [Serializable]
        private sealed class MacroHumanSiteSaveData
        {
            public string siteId;
            public string kind;
            public long x;
            public long y;
            public int landComponentId;
        }

        [Serializable]
        private sealed class MacroRoadSaveData
        {
            public string roadId;
            public string roadClass;
            public string firstEndpoint;
            public string secondEndpoint;
            public MacroPoint2DSaveData[] polyline;
            public int routedCellCount;
            public long totalTraversalCost;
        }

        [Serializable]
        private sealed class MacroPoint2DSaveData
        {
            public long x;
            public long y;
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
