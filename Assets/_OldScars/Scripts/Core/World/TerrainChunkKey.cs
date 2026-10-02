using System;
using System.IO;

namespace OldScars.Core.World
{
    /// <summary>
    /// Stage-1 address within a committed logical window and a compatible baseline layout.
    /// Sectors are region context, not technical chunks. No streaming or save schema is implied.
    /// </summary>
    public readonly struct TerrainChunkKey : IEquatable<TerrainChunkKey>
    {
        public const string AddressContract = "terrain_chunk_key_v1";
        // Bump when projection interpolation, density/material sampling or partition semantics change.
        public const string BaselineContract = "terrain_chunk_baseline_v1";

        internal TerrainChunkKey(WorldId worldId, SectorId sectorId,
            TerrainMaterializationWindow window, DeformableTerrainChunkId localChunk,
            string layoutEvidence)
        {
            WorldId = worldId;
            SectorId = sectorId;
            Window = window;
            LocalChunk = localChunk;
            LayoutEvidence = layoutEvidence;
            Canonical = "terrain_chunk_" + WorldCanonicalEncoding.ComputeSha256(stream =>
            {
                WorldCanonicalEncoding.WriteString(stream, AddressContract);
                WorldCanonicalEncoding.WriteString(stream, worldId.Canonical);
                WorldCanonicalEncoding.WriteString(stream, sectorId.Canonical);
                TerrainBaselineEvidence.WriteWindow(stream, window);
                WorldCanonicalEncoding.WriteInt64(stream, localChunk.X);
                WorldCanonicalEncoding.WriteInt64(stream, localChunk.Y);
                WorldCanonicalEncoding.WriteInt64(stream, localChunk.Z);
                WorldCanonicalEncoding.WriteString(stream, BaselineContract);
                WorldCanonicalEncoding.WriteString(stream, layoutEvidence);
            });
        }

        public WorldId WorldId { get; }
        public SectorId SectorId { get; }
        public TerrainMaterializationWindow Window { get; }
        public DeformableTerrainChunkId LocalChunk { get; }
        public string LayoutEvidence { get; }
        public string Canonical { get; }
        public bool IsValid => WorldId.IsValid && SectorId.IsValid && !string.IsNullOrEmpty(Canonical);
        public bool Equals(TerrainChunkKey other) =>
            string.Equals(Canonical, other.Canonical, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is TerrainChunkKey other && Equals(other);
        // Collection equality only; canonical identity/evidence always uses SHA-256 above.
        public override int GetHashCode() => WorldCanonicalEncoding.GetStableCollectionHashCode(Canonical);
        public override string ToString() => Canonical ?? string.Empty;
        public static bool operator ==(TerrainChunkKey left, TerrainChunkKey right) => left.Equals(right);
        public static bool operator !=(TerrainChunkKey left, TerrainChunkKey right) => !left.Equals(right);
    }

    internal static class TerrainBaselineEvidence
    {
        internal static string ComputeLayout(TerrainMaterializationPlan plan,
            DeformableTerrainSpikeConfiguration layout)
        {
            return WorldCanonicalEncoding.ComputeSha256(stream =>
            {
                WorldCanonicalEncoding.WriteString(stream, TerrainChunkKey.BaselineContract);
                WorldCanonicalEncoding.WriteString(stream, "terrain_chunk_layout_v1");
                // Geography is the current surface authority. Water/climate/roads/render/NavMesh
                // do not affect this density/material baseline and must not redefine compatibility.
                WorldCanonicalEncoding.WriteString(stream, plan.GeographyHash);
                WriteWindow(stream, plan.Window);
                WorldCanonicalEncoding.WriteInt64(stream, plan.HeightmapResolution);
                WriteSingle(stream, plan.Configuration.PhysicalWidth);
                WriteSingle(stream, plan.Configuration.PhysicalLength);
                WriteSingle(stream, plan.Configuration.VerticalRelief);
                WorldCanonicalEncoding.WriteInt64(stream, layout.ChunkCountX);
                WorldCanonicalEncoding.WriteInt64(stream, layout.ChunkCountY);
                WorldCanonicalEncoding.WriteInt64(stream, layout.ChunkCountZ);
                WorldCanonicalEncoding.WriteInt64(stream, layout.CellsPerChunkX);
                WorldCanonicalEncoding.WriteInt64(stream, layout.CellsPerChunkY);
                WorldCanonicalEncoding.WriteInt64(stream, layout.CellsPerChunkZ);
                WorldCanonicalEncoding.WriteInt64(stream, layout.VerticalCells);
                WriteSingle(stream, layout.HorizontalCellSize);
                WriteSingle(stream, layout.UndergroundDepth);
                WriteSingle(stream, layout.AirHeadroom);
                WriteSingle(stream, layout.SurfaceLayerDepth);
                WriteSingle(stream, layout.SoilLayerDepth);
            });
        }

        internal static void WriteWindow(Stream stream, TerrainMaterializationWindow window)
        {
            WorldCanonicalEncoding.WriteInt64(stream, window.MinX);
            WorldCanonicalEncoding.WriteInt64(stream, window.MinY);
            WorldCanonicalEncoding.WriteInt64(stream, window.MaxXInclusive);
            WorldCanonicalEncoding.WriteInt64(stream, window.MaxYInclusive);
        }

        internal static void WriteSingle(Stream stream, float value)
        {
            // IEEE-754 binary32, big-endian; no culture, text rounding or runtime object hash.
            int bits = BitConverter.SingleToInt32Bits(value);
            stream.WriteByte((byte)(bits >> 24));
            stream.WriteByte((byte)(bits >> 16));
            stream.WriteByte((byte)(bits >> 8));
            stream.WriteByte((byte)bits);
        }
    }
}
