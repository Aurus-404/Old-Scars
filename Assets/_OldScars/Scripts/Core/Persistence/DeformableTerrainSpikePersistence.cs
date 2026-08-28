using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OldScars.Core.World;
using UnityEngine;

namespace OldScars.Core.Persistence
{
    /// <summary>
    /// Explicit non-production persistence adapter for the terrain spike. It
    /// uses M37's serializer/store envelope and proves canonical operation replay
    /// without changing world_session_v1 or claiming World Persistence V1.
    /// </summary>
    public static class DeformableTerrainSpikePersistence
    {
        public static JObject ToPayload(DeformableTerrainSpikeState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            var mutations = new JArray();
            for (int index = 0; index < state.Mutations.Count; index++)
            {
                DeformableTerrainMutation mutation = state.Mutations[index];
                mutations.Add(new JObject
                {
                    ["kind"] = mutation.Kind.ToString(),
                    ["start"] = Vector(mutation.Start),
                    ["end"] = Vector(mutation.End),
                    ["radius"] = mutation.Radius
                });
            }
            DeformableTerrainSpikeConfiguration configuration = state.Configuration;
            return new JObject
            {
                ["payloadType"] = DeformableTerrainSpikeState.PayloadType,
                ["schemaVersion"] = DeformableTerrainSpikeState.SchemaVersion,
                ["persistenceStatus"] = DeformableTerrainSpikeState.PersistenceStatus,
                ["worldId"] = state.WorldId.Canonical,
                ["sectorId"] = state.SectorId.Canonical,
                ["geographyHash"] = state.GeographyHash,
                ["configuration"] = new JObject
                {
                    ["contract"] = DeformableTerrainSpikeConfiguration.Contract,
                    ["chunkCountX"] = configuration.ChunkCountX,
                    ["chunkCountZ"] = configuration.ChunkCountZ,
                    ["cellsPerChunkX"] = configuration.CellsPerChunkX,
                    ["cellsPerChunkZ"] = configuration.CellsPerChunkZ,
                    ["verticalCells"] = configuration.VerticalCells,
                    ["horizontalCellSize"] = configuration.HorizontalCellSize,
                    ["undergroundDepth"] = configuration.UndergroundDepth,
                    ["airHeadroom"] = configuration.AirHeadroom,
                    ["surfaceLayerDepth"] = configuration.SurfaceLayerDepth,
                    ["soilLayerDepth"] = configuration.SoilLayerDepth
                },
                ["origin"] = Vector(state.Origin),
                ["verticalCellSize"] = state.VerticalCellSize,
                ["mutations"] = mutations
            };
        }

        public static bool TryFromPayload(
            JToken payload,
            WorldSession expectedSession,
            TerrainMaterializationPlan expectedPlan,
            out DeformableTerrainSpikeState state,
            out string error)
        {
            state = null;
            error = null;
            if (!(payload is JObject root))
                return Fail("terrain spike payload root must be an object", out error);
            if (!ExactProperties(root,
                    "payloadType", "schemaVersion", "persistenceStatus", "worldId", "sectorId",
                    "geographyHash", "configuration", "origin", "verticalCellSize", "mutations"))
                return Fail("terrain spike payload contains missing or unknown root fields", out error);
            if (!String(root, "payloadType", out string payloadType) ||
                payloadType != DeformableTerrainSpikeState.PayloadType ||
                !Integer(root, "schemaVersion", out int schemaVersion) ||
                schemaVersion != DeformableTerrainSpikeState.SchemaVersion ||
                !String(root, "persistenceStatus", out string status) ||
                status != DeformableTerrainSpikeState.PersistenceStatus)
                return Fail("terrain spike payload contract/schema/status is invalid", out error);
            if (!String(root, "worldId", out string worldRaw))
                return Fail("terrain spike WorldId must be a string", out error);
            if (!WorldId.TryParse(worldRaw, out WorldId worldId, out string worldError))
                return Fail("terrain spike WorldId is invalid: " + worldError, out error);
            if (!String(root, "sectorId", out string sectorRaw))
                return Fail("terrain spike SectorId must be a string", out error);
            if (!SectorId.TryParse(sectorRaw, out SectorId sectorId, out string sectorError))
                return Fail("terrain spike SectorId is invalid: " + sectorError, out error);
            if (expectedSession == null || worldId != expectedSession.WorldId ||
                sectorId != expectedSession.ActiveSectorId)
                return Fail("terrain spike payload belongs to a different world or active sector", out error);
            if (!String(root, "geographyHash", out string geographyHash) ||
                expectedPlan == null || geographyHash != expectedPlan.GeographyHash)
                return Fail("terrain spike geography evidence does not match the committed projection", out error);
            if (!(root["configuration"] is JObject configurationObject) ||
                !TryConfiguration(configurationObject, out DeformableTerrainSpikeConfiguration configuration, out error))
                return false;
            if (!TryVector(root["origin"], out Vector3 origin, out error) ||
                !Number(root, "verticalCellSize", out float verticalCellSize) || verticalCellSize <= 0f)
                return Fail(error ?? "terrain spike vertical spacing is invalid", out error);
            if (!(root["mutations"] is JArray mutationArray))
                return Fail("terrain spike mutations must be an array", out error);
            if (!DeformableTerrainVolume.TryCreate(
                    expectedPlan, configuration, out DeformableTerrainVolume expectedVolume,
                    out string volumeError) || expectedVolume.Origin != origin ||
                expectedVolume.VerticalCellSize != verticalCellSize)
                return Fail(
                    "terrain spike baseline origin/spacing does not match the committed projection: " +
                    volumeError, out error);
            var mutations = new List<DeformableTerrainMutation>(mutationArray.Count);
            for (int index = 0; index < mutationArray.Count; index++)
            {
                if (!(mutationArray[index] is JObject mutationObject) ||
                    !ExactProperties(mutationObject, "kind", "start", "end", "radius") ||
                    !String(mutationObject, "kind", out string kindRaw) ||
                    !Enum.TryParse(kindRaw, false, out DeformableTerrainMutationKind kind) ||
                    !Enum.IsDefined(typeof(DeformableTerrainMutationKind), kind) ||
                    !TryVector(mutationObject["start"], out Vector3 start, out error) ||
                    !TryVector(mutationObject["end"], out Vector3 end, out error) ||
                    !Number(mutationObject, "radius", out float radius) || radius <= 0f)
                    return Fail("terrain spike mutation " + index + " is malformed: " + error, out error);
                mutations.Add(new DeformableTerrainMutation(kind, start, end, radius));
            }
            state = new DeformableTerrainSpikeState(
                worldId, sectorId, geographyHash, configuration, origin,
                verticalCellSize, mutations);
            return true;
        }

        private static bool TryConfiguration(
            JObject value,
            out DeformableTerrainSpikeConfiguration configuration,
            out string error)
        {
            configuration = null;
            error = null;
            if (!ExactProperties(value,
                    "contract", "chunkCountX", "chunkCountZ", "cellsPerChunkX", "cellsPerChunkZ",
                    "verticalCells", "horizontalCellSize", "undergroundDepth", "airHeadroom",
                    "surfaceLayerDepth", "soilLayerDepth") ||
                !String(value, "contract", out string contract) ||
                contract != DeformableTerrainSpikeConfiguration.Contract ||
                !Integer(value, "chunkCountX", out int chunkCountX) ||
                !Integer(value, "chunkCountZ", out int chunkCountZ) ||
                !Integer(value, "cellsPerChunkX", out int cellsPerChunkX) ||
                !Integer(value, "cellsPerChunkZ", out int cellsPerChunkZ) ||
                !Integer(value, "verticalCells", out int verticalCells) ||
                !Number(value, "horizontalCellSize", out float horizontalCellSize) ||
                !Number(value, "undergroundDepth", out float undergroundDepth) ||
                !Number(value, "airHeadroom", out float airHeadroom) ||
                !Number(value, "surfaceLayerDepth", out float surfaceLayerDepth) ||
                !Number(value, "soilLayerDepth", out float soilLayerDepth))
                return Fail("terrain spike configuration is malformed", out error);
            try
            {
                configuration = new DeformableTerrainSpikeConfiguration(
                    chunkCountX, chunkCountZ, cellsPerChunkX, cellsPerChunkZ,
                    verticalCells, horizontalCellSize, undergroundDepth, airHeadroom,
                    surfaceLayerDepth, soilLayerDepth);
                return true;
            }
            catch (ArgumentException exception)
            {
                return Fail(exception.Message, out error);
            }
        }

        private static JObject Vector(Vector3 value)
        {
            return new JObject { ["x"] = value.x, ["y"] = value.y, ["z"] = value.z };
        }

        private static bool TryVector(JToken token, out Vector3 value, out string error)
        {
            value = default;
            error = null;
            if (!(token is JObject vector) || !ExactProperties(vector, "x", "y", "z") ||
                !Number(vector, "x", out float x) || !Number(vector, "y", out float y) ||
                !Number(vector, "z", out float z))
                return Fail("vector must contain only finite x/y/z numbers", out error);
            value = new Vector3(x, y, z);
            return true;
        }

        private static bool ExactProperties(JObject value, params string[] expected)
        {
            var names = new HashSet<string>(expected, StringComparer.Ordinal);
            int count = 0;
            foreach (JProperty property in value.Properties())
            {
                count++;
                if (!names.Contains(property.Name))
                    return false;
            }
            return count == names.Count;
        }

        private static bool String(JObject value, string name, out string result)
        {
            JToken token = value[name];
            result = token != null && token.Type == JTokenType.String ? token.Value<string>() : null;
            return result != null;
        }

        private static bool Integer(JObject value, string name, out int result)
        {
            result = 0;
            JToken token = value[name];
            if (token == null || token.Type != JTokenType.Integer)
                return false;
            try { result = token.Value<int>(); return true; }
            catch (Exception exception) when (exception is OverflowException || exception is FormatException)
            { return false; }
        }

        private static bool Number(JObject value, string name, out float result)
        {
            result = 0f;
            JToken token = value[name];
            if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer))
                return false;
            try
            {
                result = token.Value<float>();
                return !float.IsNaN(result) && !float.IsInfinity(result);
            }
            catch (Exception exception) when (
                exception is OverflowException || exception is FormatException || exception is InvalidCastException)
            { return false; }
        }

        private static bool Fail(string failure, out string error)
        {
            error = failure;
            return false;
        }
    }
}
