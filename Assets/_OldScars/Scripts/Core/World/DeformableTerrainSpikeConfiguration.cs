using System;

namespace OldScars.Core.World
{
    /// <summary>
    /// Development-only physical representation tuning for the deformable
    /// terrain spike. It is neither macro world truth nor a production format.
    /// </summary>
    [Serializable]
    public sealed class DeformableTerrainSpikeConfiguration
    {
        public const string Contract = "deformable_terrain_spike_v1";

        private readonly int chunkCountX;
        private readonly int chunkCountZ;
        private readonly int cellsPerChunkX;
        private readonly int cellsPerChunkZ;
        private readonly int verticalCells;
        private readonly float horizontalCellSize;
        private readonly float undergroundDepth;
        private readonly float airHeadroom;
        private readonly float surfaceLayerDepth;
        private readonly float soilLayerDepth;

        public DeformableTerrainSpikeConfiguration(
            int chunkCountX,
            int chunkCountZ,
            int cellsPerChunkX,
            int cellsPerChunkZ,
            int verticalCells,
            float horizontalCellSize,
            float undergroundDepth,
            float airHeadroom,
            float surfaceLayerDepth,
            float soilLayerDepth)
        {
            this.chunkCountX = chunkCountX;
            this.chunkCountZ = chunkCountZ;
            this.cellsPerChunkX = cellsPerChunkX;
            this.cellsPerChunkZ = cellsPerChunkZ;
            this.verticalCells = verticalCells;
            this.horizontalCellSize = horizontalCellSize;
            this.undergroundDepth = undergroundDepth;
            this.airHeadroom = airHeadroom;
            this.surfaceLayerDepth = surfaceLayerDepth;
            this.soilLayerDepth = soilLayerDepth;

            if (!TryValidate(out string error))
                throw new ArgumentException(error, nameof(chunkCountX));
        }

        public int ChunkCountX => chunkCountX;
        public int ChunkCountZ => chunkCountZ;
        public int CellsPerChunkX => cellsPerChunkX;
        public int CellsPerChunkZ => cellsPerChunkZ;
        public int VerticalCells => verticalCells;
        public float HorizontalCellSize => horizontalCellSize;
        public float UndergroundDepth => undergroundDepth;
        public float AirHeadroom => airHeadroom;
        public float SurfaceLayerDepth => surfaceLayerDepth;
        public float SoilLayerDepth => soilLayerDepth;
        public int TotalCellsX => ChunkCountX * CellsPerChunkX;
        public int TotalCellsZ => ChunkCountZ * CellsPerChunkZ;
        public float PhysicalWidth => TotalCellsX * HorizontalCellSize;
        public float PhysicalLength => TotalCellsZ * HorizontalCellSize;
        public int TotalDensitySamples =>
            (TotalCellsX + 1) * (VerticalCells + 1) * (TotalCellsZ + 1);

        public static DeformableTerrainSpikeConfiguration CreateBaseline()
        {
            return new DeformableTerrainSpikeConfiguration(
                2, 2, 24, 24, 32, 2f, 32f, 20f, 1.5f, 10f);
        }

        public static DeformableTerrainSpikeConfiguration CreateCoarseComparison()
        {
            return new DeformableTerrainSpikeConfiguration(
                2, 2, 16, 16, 22, 3f, 33f, 21f, 1.5f, 10f);
        }

        public bool TryValidate(out string error)
        {
            error = null;
            if (ChunkCountX < 2 || ChunkCountX > 8 || ChunkCountZ < 2 || ChunkCountZ > 8)
            {
                error = "deformable terrain spike requires 2..8 chunks on each horizontal axis";
                return false;
            }
            if (CellsPerChunkX < 4 || CellsPerChunkX > 64 ||
                CellsPerChunkZ < 4 || CellsPerChunkZ > 64 ||
                VerticalCells < 8 || VerticalCells > 96)
            {
                error = "chunk cell counts are outside the bounded spike range";
                return false;
            }
            if (!FinitePositive(HorizontalCellSize) || !FinitePositive(UndergroundDepth) ||
                !FinitePositive(AirHeadroom) || !FinitePositive(SurfaceLayerDepth) ||
                !FinitePositive(SoilLayerDepth) || SoilLayerDepth <= SurfaceLayerDepth)
            {
                error = "cell size and depth tuning must be finite, positive, and ordered";
                return false;
            }
            return true;
        }

        public bool HasEquivalentLayout(DeformableTerrainSpikeConfiguration other)
        {
            return other != null &&
                   ChunkCountX == other.ChunkCountX && ChunkCountZ == other.ChunkCountZ &&
                   CellsPerChunkX == other.CellsPerChunkX &&
                   CellsPerChunkZ == other.CellsPerChunkZ &&
                   VerticalCells == other.VerticalCells &&
                   HorizontalCellSize == other.HorizontalCellSize &&
                   UndergroundDepth == other.UndergroundDepth &&
                   AirHeadroom == other.AirHeadroom &&
                   SurfaceLayerDepth == other.SurfaceLayerDepth &&
                   SoilLayerDepth == other.SoilLayerDepth;
        }

        private static bool FinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }
}
