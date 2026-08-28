using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace OldScars.Core.World
{
    public enum DeformableTerrainMaterial : byte
    {
        Surface = 0,
        Soil = 1,
        Rock = 2
    }

    public readonly struct DeformableTerrainChunkId :
        IEquatable<DeformableTerrainChunkId>, IComparable<DeformableTerrainChunkId>
    {
        public DeformableTerrainChunkId(int x, int z)
        {
            X = x;
            Z = z;
        }

        public int X { get; }
        public int Z { get; }

        public int CompareTo(DeformableTerrainChunkId other)
        {
            int xComparison = X.CompareTo(other.X);
            return xComparison != 0 ? xComparison : Z.CompareTo(other.Z);
        }

        public bool Equals(DeformableTerrainChunkId other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is DeformableTerrainChunkId other && Equals(other);
        public override int GetHashCode() => unchecked(X * 397 ^ Z);
        public override string ToString() => "chunk_" + X + "_" + Z;
        public static bool operator ==(DeformableTerrainChunkId left, DeformableTerrainChunkId right) => left.Equals(right);
        public static bool operator !=(DeformableTerrainChunkId left, DeformableTerrainChunkId right) => !left.Equals(right);
    }

    public enum DeformableTerrainMutationKind
    {
        SubtractSphere = 0,
        SubtractCapsule = 1
    }

    /// <summary>
    /// Canonical, replayable spike mutation. This is deliberately not a world
    /// persistence schema and does not claim production terrain persistence.
    /// </summary>
    public sealed class DeformableTerrainMutation
    {
        public DeformableTerrainMutation(
            DeformableTerrainMutationKind kind,
            Vector3 start,
            Vector3 end,
            float radius)
        {
            if (!Enum.IsDefined(typeof(DeformableTerrainMutationKind), kind) ||
                !Finite(start) || !Finite(end) || float.IsNaN(radius) ||
                float.IsInfinity(radius) || radius <= 0f)
                throw new ArgumentException(
                    "A defined mutation kind, finite endpoints, and positive radius are required.");
            Kind = kind;
            Start = start;
            End = end;
            Radius = radius;
        }

        public DeformableTerrainMutationKind Kind { get; }
        public Vector3 Start { get; }
        public Vector3 End { get; }
        public float Radius { get; }

        public Bounds Bounds
        {
            get
            {
                Vector3 minimum = Vector3.Min(Start, End) - Vector3.one * Radius;
                Vector3 maximum = Vector3.Max(Start, End) + Vector3.one * Radius;
                var bounds = new Bounds();
                bounds.SetMinMax(minimum, maximum);
                return bounds;
            }
        }

        private static bool Finite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }

    public sealed class DeformableTerrainSpikeState
    {
        public const string PayloadType = "deformable_terrain_spike_v1";
        public const int SchemaVersion = 1;
        public const string PersistenceStatus = "SPIKE_NON_PRODUCTION";

        private readonly ReadOnlyCollection<DeformableTerrainMutation> mutations;

        public DeformableTerrainSpikeState(
            WorldId worldId,
            SectorId sectorId,
            string geographyHash,
            DeformableTerrainSpikeConfiguration configuration,
            Vector3 origin,
            float verticalCellSize,
            IEnumerable<DeformableTerrainMutation> mutations)
        {
            if (!worldId.IsValid || !sectorId.IsValid || string.IsNullOrWhiteSpace(geographyHash) ||
                configuration == null || verticalCellSize <= 0f || float.IsNaN(verticalCellSize) ||
                float.IsInfinity(verticalCellSize) || mutations == null)
                throw new ArgumentException("A complete terrain spike persistence identity is required.");
            WorldId = worldId;
            SectorId = sectorId;
            GeographyHash = geographyHash;
            Configuration = configuration;
            Origin = origin;
            VerticalCellSize = verticalCellSize;
            this.mutations = new ReadOnlyCollection<DeformableTerrainMutation>(
                new List<DeformableTerrainMutation>(mutations));
        }

        public WorldId WorldId { get; }
        public SectorId SectorId { get; }
        public string GeographyHash { get; }
        public DeformableTerrainSpikeConfiguration Configuration { get; }
        public Vector3 Origin { get; }
        public float VerticalCellSize { get; }
        public IReadOnlyList<DeformableTerrainMutation> Mutations => mutations;
    }

    /// <summary>
    /// One local, bounded scalar field. A single shared sample lattice feeds
    /// multiple materialization chunks, so adjacent chunk boundaries read the
    /// exact same density values. MacroGeography remains the surface authority.
    /// </summary>
    public sealed class DeformableTerrainVolume
    {
        private readonly float[] density;
        private readonly float[] baselineDensity;
        private readonly byte[] materials;

        private DeformableTerrainVolume(
            TerrainMaterializationPlan sourcePlan,
            DeformableTerrainSpikeConfiguration configuration,
            Vector3 origin,
            float verticalCellSize,
            float[] density,
            byte[] materials)
        {
            SourcePlan = sourcePlan;
            Configuration = configuration;
            Origin = origin;
            VerticalCellSize = verticalCellSize;
            this.density = density;
            baselineDensity = (float[])density.Clone();
            this.materials = materials;
        }

        public TerrainMaterializationPlan SourcePlan { get; }
        public DeformableTerrainSpikeConfiguration Configuration { get; }
        public Vector3 Origin { get; }
        public float VerticalCellSize { get; }
        public int SampleCountX => Configuration.TotalCellsX + 1;
        public int SampleCountY => Configuration.VerticalCells + 1;
        public int SampleCountZ => Configuration.TotalCellsZ + 1;
        public int DensitySampleCount => density.Length;
        public long ApproximateFieldBytes =>
            density.LongLength * sizeof(float) +
            baselineDensity.LongLength * sizeof(float) +
            materials.LongLength;

        public static bool TryCreate(
            TerrainMaterializationPlan plan,
            DeformableTerrainSpikeConfiguration configuration,
            out DeformableTerrainVolume volume,
            out string error)
        {
            volume = null;
            error = null;
            if (plan == null)
            {
                error = "a committed terrain materialization projection is required";
                return false;
            }
            if (configuration == null || !configuration.TryValidate(out error))
                return false;
            if (configuration.PhysicalWidth > plan.Configuration.PhysicalWidth ||
                configuration.PhysicalLength > plan.Configuration.PhysicalLength)
            {
                error = "deformable terrain spike volume must fit inside the existing logical-to-Unity projection";
                return false;
            }

            float halfWidth = configuration.PhysicalWidth * 0.5f;
            float halfLength = configuration.PhysicalLength * 0.5f;
            float minimumSurface = float.MaxValue;
            float maximumSurface = float.MinValue;
            for (int z = 0; z <= configuration.TotalCellsZ; z++)
            for (int x = 0; x <= configuration.TotalCellsX; x++)
            {
                float localX = -halfWidth + x * configuration.HorizontalCellSize;
                float localZ = -halfLength + z * configuration.HorizontalCellSize;
                float surface = plan.HeightNormalizedAtLocal(localX, localZ) *
                                plan.Configuration.VerticalRelief;
                minimumSurface = Math.Min(minimumSurface, surface);
                maximumSurface = Math.Max(maximumSurface, surface);
            }

            float originY = minimumSurface - configuration.UndergroundDepth;
            float verticalExtent = maximumSurface - minimumSurface +
                                   configuration.UndergroundDepth + configuration.AirHeadroom;
            float verticalCellSize = verticalExtent / configuration.VerticalCells;
            if (verticalCellSize <= 0f || float.IsNaN(verticalCellSize) || float.IsInfinity(verticalCellSize))
            {
                error = "resolved vertical density spacing is invalid";
                return false;
            }

            var origin = new Vector3(-halfWidth, originY, -halfLength);
            int sampleCount = configuration.TotalDensitySamples;
            var density = new float[sampleCount];
            var materials = new byte[sampleCount];
            int sampleCountX = configuration.TotalCellsX + 1;
            int sampleCountY = configuration.VerticalCells + 1;
            for (int z = 0; z <= configuration.TotalCellsZ; z++)
            for (int y = 0; y <= configuration.VerticalCells; y++)
            for (int x = 0; x <= configuration.TotalCellsX; x++)
            {
                float localX = origin.x + x * configuration.HorizontalCellSize;
                float localY = origin.y + y * verticalCellSize;
                float localZ = origin.z + z * configuration.HorizontalCellSize;
                float surface = plan.HeightNormalizedAtLocal(localX, localZ) *
                                plan.Configuration.VerticalRelief;
                float depth = surface - localY;
                int index = x + sampleCountX * (y + sampleCountY * z);
                density[index] = depth;
                materials[index] = (byte)(depth <= configuration.SurfaceLayerDepth
                    ? DeformableTerrainMaterial.Surface
                    : depth <= configuration.SoilLayerDepth
                        ? DeformableTerrainMaterial.Soil
                        : DeformableTerrainMaterial.Rock);
            }

            volume = new DeformableTerrainVolume(
                plan, configuration, origin, verticalCellSize, density, materials);
            return true;
        }

        public bool Contains(Vector3 localPosition)
        {
            return localPosition.x >= Origin.x &&
                   localPosition.x <= Origin.x + Configuration.PhysicalWidth &&
                   localPosition.y >= Origin.y &&
                   localPosition.y <= Origin.y + VerticalCellSize * Configuration.VerticalCells &&
                   localPosition.z >= Origin.z &&
                   localPosition.z <= Origin.z + Configuration.PhysicalLength;
        }

        public Vector3 SamplePosition(int x, int y, int z)
        {
            RequireSample(x, y, z);
            return new Vector3(
                Origin.x + x * Configuration.HorizontalCellSize,
                Origin.y + y * VerticalCellSize,
                Origin.z + z * Configuration.HorizontalCellSize);
        }

        public float DensityAtSample(int x, int y, int z)
        {
            RequireSample(x, y, z);
            return density[Index(x, y, z)];
        }

        public DeformableTerrainMaterial MaterialAtSample(int x, int y, int z)
        {
            RequireSample(x, y, z);
            return (DeformableTerrainMaterial)materials[Index(x, y, z)];
        }

        public float DensityAtLocal(Vector3 localPosition)
        {
            float gridX = (localPosition.x - Origin.x) / Configuration.HorizontalCellSize;
            float gridY = (localPosition.y - Origin.y) / VerticalCellSize;
            float gridZ = (localPosition.z - Origin.z) / Configuration.HorizontalCellSize;
            int x0 = ClampFloor(gridX, Configuration.TotalCellsX);
            int y0 = ClampFloor(gridY, Configuration.VerticalCells);
            int z0 = ClampFloor(gridZ, Configuration.TotalCellsZ);
            int x1 = Math.Min(Configuration.TotalCellsX, x0 + 1);
            int y1 = Math.Min(Configuration.VerticalCells, y0 + 1);
            int z1 = Math.Min(Configuration.TotalCellsZ, z0 + 1);
            float tx = Mathf.Clamp01(gridX - x0);
            float ty = Mathf.Clamp01(gridY - y0);
            float tz = Mathf.Clamp01(gridZ - z0);
            float x00 = Mathf.Lerp(density[Index(x0, y0, z0)], density[Index(x1, y0, z0)], tx);
            float x10 = Mathf.Lerp(density[Index(x0, y1, z0)], density[Index(x1, y1, z0)], tx);
            float x01 = Mathf.Lerp(density[Index(x0, y0, z1)], density[Index(x1, y0, z1)], tx);
            float x11 = Mathf.Lerp(density[Index(x0, y1, z1)], density[Index(x1, y1, z1)], tx);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, ty), Mathf.Lerp(x01, x11, ty), tz);
        }

        public Vector3 OutwardNormalAtLocal(Vector3 localPosition)
        {
            float xStep = Configuration.HorizontalCellSize * 0.5f;
            float yStep = VerticalCellSize * 0.5f;
            float zStep = Configuration.HorizontalCellSize * 0.5f;
            float dx = DensityAtLocal(localPosition + Vector3.right * xStep) -
                       DensityAtLocal(localPosition - Vector3.right * xStep);
            float dy = DensityAtLocal(localPosition + Vector3.up * yStep) -
                       DensityAtLocal(localPosition - Vector3.up * yStep);
            float dz = DensityAtLocal(localPosition + Vector3.forward * zStep) -
                       DensityAtLocal(localPosition - Vector3.forward * zStep);
            Vector3 outward = new Vector3(-dx / xStep, -dy / yStep, -dz / zStep);
            return outward.sqrMagnitude > 0.000001f ? outward.normalized : Vector3.up;
        }

        public Bounds ChunkBounds(DeformableTerrainChunkId chunkId)
        {
            RequireChunk(chunkId);
            Vector3 minimum = new Vector3(
                Origin.x + chunkId.X * Configuration.CellsPerChunkX * Configuration.HorizontalCellSize,
                Origin.y,
                Origin.z + chunkId.Z * Configuration.CellsPerChunkZ * Configuration.HorizontalCellSize);
            Vector3 size = new Vector3(
                Configuration.CellsPerChunkX * Configuration.HorizontalCellSize,
                Configuration.VerticalCells * VerticalCellSize,
                Configuration.CellsPerChunkZ * Configuration.HorizontalCellSize);
            return new Bounds(minimum + size * 0.5f, size);
        }

        public IEnumerable<DeformableTerrainChunkId> EnumerateChunks()
        {
            for (int x = 0; x < Configuration.ChunkCountX; x++)
            for (int z = 0; z < Configuration.ChunkCountZ; z++)
                yield return new DeformableTerrainChunkId(x, z);
        }

        public string ComputeDensityEvidence()
        {
            using (SHA256 sha = SHA256.Create())
            {
                AppendUtf8(sha, DeformableTerrainSpikeConfiguration.Contract);
                AppendInt32(sha, Configuration.TotalCellsX);
                AppendInt32(sha, Configuration.VerticalCells);
                AppendInt32(sha, Configuration.TotalCellsZ);
                AppendSingle(sha, Origin.x);
                AppendSingle(sha, Origin.y);
                AppendSingle(sha, Origin.z);
                AppendSingle(sha, Configuration.HorizontalCellSize);
                AppendSingle(sha, VerticalCellSize);
                for (int index = 0; index < density.Length; index++)
                {
                    AppendSingle(sha, density[index]);
                    AppendByte(sha, materials[index]);
                }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return ToHex(sha.Hash);
            }
        }

        internal void ResetToBaseline()
        {
            Array.Copy(baselineDensity, density, density.Length);
        }

        internal void Apply(DeformableTerrainMutation mutation)
        {
            Bounds bounds = mutation.Bounds;
            int minX = ClampFloor((bounds.min.x - Origin.x) / Configuration.HorizontalCellSize,
                Configuration.TotalCellsX);
            int maxX = ClampCeiling((bounds.max.x - Origin.x) / Configuration.HorizontalCellSize,
                Configuration.TotalCellsX);
            int minY = ClampFloor((bounds.min.y - Origin.y) / VerticalCellSize,
                Configuration.VerticalCells);
            int maxY = ClampCeiling((bounds.max.y - Origin.y) / VerticalCellSize,
                Configuration.VerticalCells);
            int minZ = ClampFloor((bounds.min.z - Origin.z) / Configuration.HorizontalCellSize,
                Configuration.TotalCellsZ);
            int maxZ = ClampCeiling((bounds.max.z - Origin.z) / Configuration.HorizontalCellSize,
                Configuration.TotalCellsZ);
            for (int z = minZ; z <= maxZ; z++)
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                Vector3 position = SamplePosition(x, y, z);
                float signedDistance = mutation.Kind == DeformableTerrainMutationKind.SubtractSphere
                    ? Vector3.Distance(position, mutation.Start) - mutation.Radius
                    : DistanceToSegment(position, mutation.Start, mutation.End) - mutation.Radius;
                int index = Index(x, y, z);
                density[index] = Math.Min(density[index], signedDistance);
            }
        }

        private int Index(int x, int y, int z)
        {
            return x + SampleCountX * (y + SampleCountY * z);
        }

        private void RequireSample(int x, int y, int z)
        {
            if (x < 0 || x >= SampleCountX || y < 0 || y >= SampleCountY ||
                z < 0 || z >= SampleCountZ)
                throw new ArgumentOutOfRangeException("Density sample lies outside the local spike volume.");
        }

        private void RequireChunk(DeformableTerrainChunkId chunkId)
        {
            if (chunkId.X < 0 || chunkId.X >= Configuration.ChunkCountX ||
                chunkId.Z < 0 || chunkId.Z >= Configuration.ChunkCountZ)
                throw new ArgumentOutOfRangeException(nameof(chunkId));
        }

        private static int ClampFloor(float value, int maximum)
        {
            return Math.Max(0, Math.Min(maximum, Mathf.FloorToInt(value)));
        }

        private static int ClampCeiling(float value, int maximum)
        {
            return Math.Max(0, Math.Min(maximum, Mathf.CeilToInt(value)));
        }

        private static float DistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
        {
            Vector3 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.000001f)
                return Vector3.Distance(point, start);
            float t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / lengthSquared);
            return Vector3.Distance(point, start + segment * t);
        }

        private static void AppendUtf8(HashAlgorithm hash, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            AppendInt32(hash, bytes.Length);
            hash.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }

        private static void AppendSingle(HashAlgorithm hash, float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            hash.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }

        private static void AppendInt32(HashAlgorithm hash, int value)
        {
            byte[] bytes =
            {
                (byte)value,
                (byte)(value >> 8),
                (byte)(value >> 16),
                (byte)(value >> 24)
            };
            hash.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }

        private static void AppendByte(HashAlgorithm hash, byte value)
        {
            byte[] bytes = { value };
            hash.TransformBlock(bytes, 0, 1, null, 0);
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (int index = 0; index < bytes.Length; index++)
                builder.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }

    /// <summary>
    /// The sole mutation boundary for the spike. Callers submit canonical
    /// operations; only this service edits density samples and resolves dirty
    /// materialization chunks.
    /// </summary>
    public sealed class DeformableTerrainMutationService
    {
        private readonly DeformableTerrainVolume volume;
        private readonly List<DeformableTerrainMutation> mutations =
            new List<DeformableTerrainMutation>();

        public DeformableTerrainMutationService(DeformableTerrainVolume volume)
        {
            this.volume = volume ?? throw new ArgumentNullException(nameof(volume));
        }

        public IReadOnlyList<DeformableTerrainMutation> Mutations =>
            new ReadOnlyCollection<DeformableTerrainMutation>(mutations);

        public IReadOnlyList<DeformableTerrainChunkId> SubtractSphere(Vector3 center, float radius)
        {
            return Apply(new DeformableTerrainMutation(
                DeformableTerrainMutationKind.SubtractSphere, center, center, radius), true);
        }

        public IReadOnlyList<DeformableTerrainChunkId> SubtractCapsule(
            Vector3 start, Vector3 end, float radius)
        {
            return Apply(new DeformableTerrainMutation(
                DeformableTerrainMutationKind.SubtractCapsule, start, end, radius), true);
        }

        public IReadOnlyList<DeformableTerrainChunkId> Replay(
            IEnumerable<DeformableTerrainMutation> orderedMutations)
        {
            if (orderedMutations == null)
                throw new ArgumentNullException(nameof(orderedMutations));
            var replay = new List<DeformableTerrainMutation>(orderedMutations);
            for (int index = 0; index < replay.Count; index++)
                ResolveAffectedChunks(replay[index]);

            volume.ResetToBaseline();
            mutations.Clear();
            var affected = new HashSet<DeformableTerrainChunkId>();
            foreach (DeformableTerrainMutation mutation in replay)
            {
                foreach (DeformableTerrainChunkId chunkId in Apply(mutation, true))
                    affected.Add(chunkId);
            }
            var ordered = new List<DeformableTerrainChunkId>(affected);
            ordered.Sort();
            return new ReadOnlyCollection<DeformableTerrainChunkId>(ordered);
        }

        public IReadOnlyList<DeformableTerrainChunkId> Reset()
        {
            volume.ResetToBaseline();
            mutations.Clear();
            var chunks = new List<DeformableTerrainChunkId>(volume.EnumerateChunks());
            chunks.Sort();
            return new ReadOnlyCollection<DeformableTerrainChunkId>(chunks);
        }

        public DeformableTerrainSpikeState CaptureState(WorldSession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            return new DeformableTerrainSpikeState(
                session.WorldId, session.ActiveSectorId, volume.SourcePlan.GeographyHash,
                volume.Configuration, volume.Origin, volume.VerticalCellSize, mutations);
        }

        private IReadOnlyList<DeformableTerrainChunkId> Apply(
            DeformableTerrainMutation mutation,
            bool record)
        {
            IReadOnlyList<DeformableTerrainChunkId> affected = ResolveAffectedChunks(mutation);
            volume.Apply(mutation);
            if (record)
                mutations.Add(mutation);
            return affected;
        }

        private IReadOnlyList<DeformableTerrainChunkId> ResolveAffectedChunks(
            DeformableTerrainMutation mutation)
        {
            if (mutation == null)
                throw new ArgumentNullException(nameof(mutation));
            Bounds mutationBounds = mutation.Bounds;
            var affected = new List<DeformableTerrainChunkId>();
            foreach (DeformableTerrainChunkId chunkId in volume.EnumerateChunks())
            {
                Bounds chunkBounds = volume.ChunkBounds(chunkId);
                // Bounds.Expand grows the total size, so four cells here add a
                // two-cell halo per side. That covers interpolation/gradient
                // samples used by normals even when the carved surface itself
                // remains just outside the neighboring chunk.
                chunkBounds.Expand(new Vector3(
                    volume.Configuration.HorizontalCellSize * 4f,
                    volume.VerticalCellSize * 4f,
                    volume.Configuration.HorizontalCellSize * 4f));
                if (chunkBounds.Intersects(mutationBounds))
                    affected.Add(chunkId);
            }
            affected.Sort();
            if (affected.Count == 0)
                throw new InvalidOperationException("Terrain mutation does not intersect the local spike volume.");
            return new ReadOnlyCollection<DeformableTerrainChunkId>(affected);
        }
    }
}
