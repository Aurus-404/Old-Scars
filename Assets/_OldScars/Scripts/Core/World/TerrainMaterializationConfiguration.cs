using System;
using UnityEngine;

namespace OldScars.Core.World
{
    /// <summary>
    /// Physical projection tuning for the terrain materialization spike. These
    /// values are scene/runtime representation choices, not generation inputs,
    /// world truth, persistence fields, or a macro-unit-to-metre contract.
    /// </summary>
    [Serializable]
    public sealed class TerrainMaterializationConfiguration
    {
        public const float MinimumPhysicalExtent = 64f;
        public const float MaximumPhysicalExtent = 4096f;
        public const long MinimumLogicalExtent = 64L;
        public const long MaximumLogicalExtent = 16000L;

        [Header("Physical projection (provisional)")]
        [SerializeField] private float physicalWidth = 768f;
        [SerializeField] private float physicalLength = 768f;
        [SerializeField] private float verticalRelief = 240f;

        [Header("Logical sample window")]
        [SerializeField] private long logicalWidth = 1800L;
        [SerializeField] private long logicalLength = 1800L;

        [Header("Technical partitions")]
        [SerializeField] private int heightmapResolution = 257;
        [SerializeField] private int waterMaskResolution = 64;
        [SerializeField] private int alphamapResolution = 128;
        [SerializeField] private int navMeshTileSize = 128;

        [Header("Fixture visualization")]
        [SerializeField] private float primaryRoadWidth = 4f;
        [SerializeField] private float maximumSpawnSlopeDegrees = 38f;

        public TerrainMaterializationConfiguration()
        {
        }

        public TerrainMaterializationConfiguration(
            float physicalWidth,
            float physicalLength,
            float verticalRelief,
            long logicalWidth,
            long logicalLength,
            int heightmapResolution,
            int waterMaskResolution,
            int alphamapResolution,
            int navMeshTileSize,
            float primaryRoadWidth,
            float maximumSpawnSlopeDegrees)
        {
            this.physicalWidth = physicalWidth;
            this.physicalLength = physicalLength;
            this.verticalRelief = verticalRelief;
            this.logicalWidth = logicalWidth;
            this.logicalLength = logicalLength;
            this.heightmapResolution = heightmapResolution;
            this.waterMaskResolution = waterMaskResolution;
            this.alphamapResolution = alphamapResolution;
            this.navMeshTileSize = navMeshTileSize;
            this.primaryRoadWidth = primaryRoadWidth;
            this.maximumSpawnSlopeDegrees = maximumSpawnSlopeDegrees;
        }

        public float PhysicalWidth => physicalWidth;
        public float PhysicalLength => physicalLength;
        public float VerticalRelief => verticalRelief;
        public long LogicalWidth => logicalWidth;
        public long LogicalLength => logicalLength;
        public int HeightmapResolution => heightmapResolution;
        public int WaterMaskResolution => waterMaskResolution;
        public int AlphamapResolution => alphamapResolution;
        public int NavMeshTileSize => navMeshTileSize;
        public float PrimaryRoadWidth => primaryRoadWidth;
        public float MaximumSpawnSlopeDegrees => maximumSpawnSlopeDegrees;

        public static TerrainMaterializationConfiguration CreateProvisionalBaseline()
        {
            return new TerrainMaterializationConfiguration();
        }

        public TerrainMaterializationConfiguration Copy()
        {
            return new TerrainMaterializationConfiguration(
                physicalWidth, physicalLength, verticalRelief,
                logicalWidth, logicalLength,
                heightmapResolution, waterMaskResolution, alphamapResolution,
                navMeshTileSize, primaryRoadWidth, maximumSpawnSlopeDegrees);
        }

        public bool TryValidate(out string error)
        {
            error = null;
            if (!FiniteInRange(physicalWidth, MinimumPhysicalExtent, MaximumPhysicalExtent) ||
                !FiniteInRange(physicalLength, MinimumPhysicalExtent, MaximumPhysicalExtent))
            {
                error = "physical width/length must be finite values between " +
                        MinimumPhysicalExtent + " and " + MaximumPhysicalExtent + " Unity units";
                return false;
            }
            if (!FiniteInRange(verticalRelief, 16f, 2048f))
            {
                error = "vertical relief must be finite and between 16 and 2048 Unity units";
                return false;
            }
            if (logicalWidth < MinimumLogicalExtent || logicalWidth > MaximumLogicalExtent ||
                logicalLength < MinimumLogicalExtent || logicalLength > MaximumLogicalExtent)
            {
                error = "logical sample extents must be between " + MinimumLogicalExtent +
                        " and " + MaximumLogicalExtent + " macro units";
                return false;
            }
            if (!ValidHeightmapResolution(heightmapResolution))
            {
                error = "heightmap resolution must be a power of two plus one between 33 and 1025";
                return false;
            }
            if (waterMaskResolution < 8 || waterMaskResolution > 256)
            {
                error = "water mask resolution must be between 8 and 256 cells per axis";
                return false;
            }
            if (alphamapResolution < 16 || alphamapResolution > 512 ||
                (alphamapResolution & (alphamapResolution - 1)) != 0)
            {
                error = "alphamap resolution must be a power of two between 16 and 512";
                return false;
            }
            if (navMeshTileSize < 32 || navMeshTileSize > 1024 ||
                (navMeshTileSize & (navMeshTileSize - 1)) != 0)
            {
                error = "NavMesh tile size must be a power of two between 32 and 1024 voxels";
                return false;
            }
            if (!FiniteInRange(primaryRoadWidth, 0.25f, 32f))
            {
                error = "primary road visualization width must be finite and between 0.25 and 32 Unity units";
                return false;
            }
            if (!FiniteInRange(maximumSpawnSlopeDegrees, 1f, 55f))
            {
                error = "maximum spawn slope must be finite and between 1 and 55 degrees";
                return false;
            }
            return true;
        }

        private static bool ValidHeightmapResolution(int value)
        {
            if (value < 33 || value > 1025)
                return false;
            int powerOfTwo = value - 1;
            return (powerOfTwo & (powerOfTwo - 1)) == 0;
        }

        private static bool FiniteInRange(float value, float minimum, float maximum)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) &&
                   value >= minimum && value <= maximum;
        }
    }
}
