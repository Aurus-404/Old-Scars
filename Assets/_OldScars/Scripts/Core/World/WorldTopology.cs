using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace OldScars.Core.World
{
    /// <summary>
    /// One explicit undirected logical connection between two sectors. Distinct
    /// keys allow multiple connections for the same endpoint pair.
    /// </summary>
    public sealed class SectorConnection
    {
        public SectorConnection(string connectionKey, SectorId firstEndpoint, SectorId secondEndpoint)
        {
            WorldStableKey.Require(connectionKey, nameof(connectionKey));
            if (!firstEndpoint.IsValid)
                throw new ArgumentException("A valid first SectorId is required.", nameof(firstEndpoint));
            if (!secondEndpoint.IsValid)
                throw new ArgumentException("A valid second SectorId is required.", nameof(secondEndpoint));

            ConnectionKey = connectionKey;
            if (firstEndpoint.CompareTo(secondEndpoint) <= 0)
            {
                FirstEndpoint = firstEndpoint;
                SecondEndpoint = secondEndpoint;
            }
            else
            {
                FirstEndpoint = secondEndpoint;
                SecondEndpoint = firstEndpoint;
            }
        }

        public string ConnectionKey { get; }
        public SectorId FirstEndpoint { get; }
        public SectorId SecondEndpoint { get; }
    }

    public sealed class WorldTopologyValidationResult
    {
        private readonly ReadOnlyCollection<string> errors;

        internal WorldTopologyValidationResult(IList<string> errors)
        {
            this.errors = new ReadOnlyCollection<string>(new List<string>(errors));
        }

        public bool IsValid => errors.Count == 0;
        public IReadOnlyList<string> Errors => errors;
        public string Description => IsValid ? "VALID" : string.Join(" | ", errors);
    }

    /// <summary>
    /// Immutable connected logical sector graph. It contains no geometry,
    /// Unity objects, runtime lifecycle, materialization or persistence state.
    /// </summary>
    public sealed class WorldTopology
    {
        private const string HashContract = "old_scars_world_topology_v1";
        private readonly ReadOnlyCollection<SectorId> sectors;
        private readonly ReadOnlyCollection<SectorConnection> connections;

        private WorldTopology(IList<SectorId> sectors, IList<SectorConnection> connections)
        {
            this.sectors = new ReadOnlyCollection<SectorId>(new List<SectorId>(sectors));
            this.connections = new ReadOnlyCollection<SectorConnection>(new List<SectorConnection>(connections));
            CanonicalDescription = BuildCanonicalDescription(this.sectors, this.connections);
            CanonicalHash = BuildCanonicalHash(this.sectors, this.connections);
        }

        public IReadOnlyList<SectorId> Sectors => sectors;
        public IReadOnlyList<SectorConnection> Connections => connections;
        public string CanonicalDescription { get; }
        public string CanonicalHash { get; }

        public static bool TryCreate(
            IEnumerable<SectorId> sectorInputs,
            IEnumerable<SectorConnection> connectionInputs,
            out WorldTopology topology,
            out WorldTopologyValidationResult validation)
        {
            topology = null;
            var errors = new List<string>();
            List<SectorId> sortedSectors = MaterializeSectors(sectorInputs, errors);
            List<SectorConnection> sortedConnections = MaterializeConnections(connectionInputs, errors);

            var sectorIds = new HashSet<SectorId>();
            for (int index = 0; index < sortedSectors.Count; index++)
            {
                SectorId sector = sortedSectors[index];
                if (!sector.IsValid)
                {
                    errors.Add("Topology contains an invalid SectorId entry.");
                    continue;
                }
                if (!sectorIds.Add(sector))
                    errors.Add($"Duplicate SectorId '{sector.Canonical}'.");
            }

            var connectionKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < sortedConnections.Count; index++)
            {
                SectorConnection connection = sortedConnections[index];
                if (connection == null)
                {
                    errors.Add("Topology contains a null connection entry.");
                    continue;
                }

                if (!connectionKeys.Add(connection.ConnectionKey))
                    errors.Add($"Duplicate connection key '{connection.ConnectionKey}'.");
                if (connection.FirstEndpoint == connection.SecondEndpoint)
                {
                    errors.Add($"Connection '{connection.ConnectionKey}' references the same SectorId " +
                               $"'{connection.FirstEndpoint.Canonical}' at both endpoints.");
                }
                if (!sectorIds.Contains(connection.FirstEndpoint))
                {
                    errors.Add($"Connection '{connection.ConnectionKey}' references missing first endpoint " +
                               $"'{connection.FirstEndpoint.Canonical}'.");
                }
                if (!sectorIds.Contains(connection.SecondEndpoint))
                {
                    errors.Add($"Connection '{connection.ConnectionKey}' references missing second endpoint " +
                               $"'{connection.SecondEndpoint.Canonical}'.");
                }
            }

            if (sortedSectors.Count == 0)
                errors.Add("Committed world topology must contain at least one sector.");

            if (errors.Count == 0)
                ValidateConnectivity(sortedSectors, sortedConnections, errors);

            validation = new WorldTopologyValidationResult(errors);
            if (!validation.IsValid)
                return false;

            topology = new WorldTopology(sortedSectors, sortedConnections);
            return true;
        }

        private static List<SectorId> MaterializeSectors(
            IEnumerable<SectorId> inputs,
            List<string> errors)
        {
            if (inputs == null)
            {
                errors.Add("Sector collection is null.");
                return new List<SectorId>();
            }
            var result = new List<SectorId>(inputs);
            result.Sort((left, right) => left.CompareTo(right));
            return result;
        }

        private static List<SectorConnection> MaterializeConnections(
            IEnumerable<SectorConnection> inputs,
            List<string> errors)
        {
            if (inputs == null)
            {
                errors.Add("Connection collection is null.");
                return new List<SectorConnection>();
            }
            var result = new List<SectorConnection>(inputs);
            result.Sort(CompareConnections);
            return result;
        }

        private static int CompareConnections(SectorConnection left, SectorConnection right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;
            int key = string.CompareOrdinal(left.ConnectionKey, right.ConnectionKey);
            if (key != 0)
                return key;
            int first = left.FirstEndpoint.CompareTo(right.FirstEndpoint);
            return first != 0 ? first : left.SecondEndpoint.CompareTo(right.SecondEndpoint);
        }

        private static void ValidateConnectivity(
            IList<SectorId> sortedSectors,
            IList<SectorConnection> sortedConnections,
            List<string> errors)
        {
            var adjacency = new Dictionary<SectorId, List<SectorId>>();
            for (int index = 0; index < sortedSectors.Count; index++)
                adjacency[sortedSectors[index]] = new List<SectorId>();
            for (int index = 0; index < sortedConnections.Count; index++)
            {
                SectorConnection connection = sortedConnections[index];
                adjacency[connection.FirstEndpoint].Add(connection.SecondEndpoint);
                adjacency[connection.SecondEndpoint].Add(connection.FirstEndpoint);
            }

            var visited = new HashSet<SectorId>();
            var pending = new Queue<SectorId>();
            pending.Enqueue(sortedSectors[0]);
            visited.Add(sortedSectors[0]);
            while (pending.Count > 0)
            {
                SectorId current = pending.Dequeue();
                List<SectorId> neighbors = adjacency[current];
                neighbors.Sort((left, right) => left.CompareTo(right));
                for (int index = 0; index < neighbors.Count; index++)
                {
                    if (visited.Add(neighbors[index]))
                        pending.Enqueue(neighbors[index]);
                }
            }

            if (visited.Count == sortedSectors.Count)
                return;
            var unreachable = new List<string>();
            for (int index = 0; index < sortedSectors.Count; index++)
            {
                if (!visited.Contains(sortedSectors[index]))
                    unreachable.Add(sortedSectors[index].Canonical);
            }
            errors.Add("Committed world topology is disconnected. Unreachable SectorIds: " +
                       string.Join(", ", unreachable) + ".");
        }

        private static string BuildCanonicalDescription(
            IList<SectorId> sortedSectors,
            IList<SectorConnection> sortedConnections)
        {
            var builder = new StringBuilder();
            builder.Append(HashContract).Append('\n');
            builder.Append("sectors|").Append(sortedSectors.Count).Append('\n');
            for (int index = 0; index < sortedSectors.Count; index++)
                builder.Append("sector|").Append(sortedSectors[index].Canonical).Append('\n');
            builder.Append("connections|").Append(sortedConnections.Count);
            for (int index = 0; index < sortedConnections.Count; index++)
            {
                SectorConnection connection = sortedConnections[index];
                builder.Append('\n').Append("connection|").Append(connection.ConnectionKey)
                    .Append('|').Append(connection.FirstEndpoint.Canonical)
                    .Append('|').Append(connection.SecondEndpoint.Canonical);
            }
            return builder.ToString();
        }

        private static string BuildCanonicalHash(
            IList<SectorId> sortedSectors,
            IList<SectorConnection> sortedConnections)
        {
            return WorldCanonicalEncoding.ComputeSha256(stream =>
            {
                WorldCanonicalEncoding.WriteString(stream, HashContract);
                WorldCanonicalEncoding.WriteInt64(stream, sortedSectors.Count);
                for (int index = 0; index < sortedSectors.Count; index++)
                    WorldCanonicalEncoding.WriteString(stream, sortedSectors[index].Canonical);
                WorldCanonicalEncoding.WriteInt64(stream, sortedConnections.Count);
                for (int index = 0; index < sortedConnections.Count; index++)
                {
                    SectorConnection connection = sortedConnections[index];
                    WorldCanonicalEncoding.WriteString(stream, connection.ConnectionKey);
                    WorldCanonicalEncoding.WriteString(stream, connection.FirstEndpoint.Canonical);
                    WorldCanonicalEncoding.WriteString(stream, connection.SecondEndpoint.Canonical);
                }
            });
        }
    }
}
