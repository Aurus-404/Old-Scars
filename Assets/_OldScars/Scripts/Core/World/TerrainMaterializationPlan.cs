using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace OldScars.Core.World
{
    /// <summary>
    /// One bounded logical window projected around a representative macro
    /// position. Max values are inclusive because materialization samples the
    /// last valid coordinate before the world's max-exclusive bound.
    /// </summary>
    public readonly struct TerrainMaterializationWindow : IEquatable<TerrainMaterializationWindow>
    {
        public TerrainMaterializationWindow(long minX, long minY, long maxXInclusive, long maxYInclusive)
        {
            if (maxXInclusive <= minX || maxYInclusive <= minY)
                throw new ArgumentException("A materialization window requires at least two logical coordinates per axis.");
            MinX = minX;
            MinY = minY;
            MaxXInclusive = maxXInclusive;
            MaxYInclusive = maxYInclusive;
        }

        public long MinX { get; }
        public long MinY { get; }
        public long MaxXInclusive { get; }
        public long MaxYInclusive { get; }
        public long LogicalWidth => MaxXInclusive - MinX + 1L;
        public long LogicalLength => MaxYInclusive - MinY + 1L;

        public bool Equals(TerrainMaterializationWindow other)
        {
            return MinX == other.MinX && MinY == other.MinY &&
                   MaxXInclusive == other.MaxXInclusive && MaxYInclusive == other.MaxYInclusive;
        }

        public override bool Equals(object obj) => obj is TerrainMaterializationWindow other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + MinX.GetHashCode();
                hash = hash * 31 + MinY.GetHashCode();
                hash = hash * 31 + MaxXInclusive.GetHashCode();
                hash = hash * 31 + MaxYInclusive.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(TerrainMaterializationWindow left, TerrainMaterializationWindow right) =>
            left.Equals(right);
        public static bool operator !=(TerrainMaterializationWindow left, TerrainMaterializationWindow right) =>
            !left.Equals(right);

        public override string ToString() =>
            "[" + MinX + "," + MinY + "]..[" + MaxXInclusive + "," + MaxYInclusive + "]";
    }

    public sealed class TerrainProjectedRoad
    {
        private readonly ReadOnlyCollection<Vector2> points;

        internal TerrainProjectedRoad(MacroRoadId roadId, MacroRoadClass roadClass, IList<Vector2> projectedPoints)
        {
            RoadId = roadId;
            RoadClass = roadClass;
            points = new ReadOnlyCollection<Vector2>(new List<Vector2>(projectedPoints));
        }

        public MacroRoadId RoadId { get; }
        public MacroRoadClass RoadClass { get; }
        public IReadOnlyList<Vector2> Points => points;
    }

    /// <summary>
    /// Immutable ephemeral projection built only from committed WorldSession
    /// truth plus physical spike configuration. It is not durable world truth
    /// and deliberately has no canonical hash.
    /// </summary>
    public sealed class TerrainMaterializationPlan
    {
        private readonly float[,] heights;
        private readonly byte[,] landforms;
        private readonly bool[,] oceanCells;
        private readonly ReadOnlyCollection<TerrainProjectedRoad> roads;

        internal TerrainMaterializationPlan(
            WorldId worldId,
            SectorId sectorId,
            MacroPoint2D logicalCenter,
            TerrainMaterializationWindow window,
            TerrainMaterializationConfiguration configuration,
            float[,] normalizedHeights,
            byte[,] sampledLandforms,
            bool[,] sampledOceanCells,
            IList<TerrainProjectedRoad> projectedRoads,
            ushort minimumElevation,
            ushort maximumElevation,
            ushort seaLevel,
            int intersectingRoadCount,
            string macroPlanHash,
            string geographyHash,
            string waterHash,
            string humanGeographyHash)
        {
            WorldId = worldId;
            SectorId = sectorId;
            LogicalCenter = logicalCenter;
            Window = window;
            Configuration = configuration.Copy();
            heights = (float[,])normalizedHeights.Clone();
            landforms = (byte[,])sampledLandforms.Clone();
            oceanCells = (bool[,])sampledOceanCells.Clone();
            roads = new ReadOnlyCollection<TerrainProjectedRoad>(
                new List<TerrainProjectedRoad>(projectedRoads));
            MinimumElevation = minimumElevation;
            MaximumElevation = maximumElevation;
            SeaLevel = seaLevel;
            IntersectingRoadCount = intersectingRoadCount;
            MacroPlanHash = macroPlanHash;
            GeographyHash = geographyHash;
            WaterHash = waterHash;
            HumanGeographyHash = humanGeographyHash;
        }

        public WorldId WorldId { get; }
        public SectorId SectorId { get; }
        public MacroPoint2D LogicalCenter { get; }
        public TerrainMaterializationWindow Window { get; }
        public TerrainMaterializationConfiguration Configuration { get; }
        public int HeightmapResolution => heights.GetLength(0);
        public int WaterMaskResolution => oceanCells.GetLength(0);
        public ushort MinimumElevation { get; }
        public ushort MaximumElevation { get; }
        public ushort SeaLevel { get; }
        public float NormalizedSeaLevel => SeaLevel / 65535f;
        public float PhysicalWaterLevel => NormalizedSeaLevel * Configuration.VerticalRelief;
        public IReadOnlyList<TerrainProjectedRoad> Roads => roads;
        public int IntersectingRoadCount { get; }
        public string MacroPlanHash { get; }
        public string GeographyHash { get; }
        public string WaterHash { get; }
        public string HumanGeographyHash { get; }

        public float HeightAt(int x, int z)
        {
            RequireHeightIndex(x, z);
            return heights[z, x];
        }

        public MacroLandform LandformAt(int x, int z)
        {
            RequireHeightIndex(x, z);
            return (MacroLandform)landforms[z, x];
        }

        public bool IsOceanCell(int x, int z)
        {
            if (x < 0 || x >= WaterMaskResolution || z < 0 || z >= WaterMaskResolution)
                throw new ArgumentOutOfRangeException("Water-mask cell lies outside the materialization plan.");
            return oceanCells[z, x];
        }

        public float HeightNormalizedAtLocal(float localX, float localZ)
        {
            double normalizedX = Clamp01((localX + Configuration.PhysicalWidth * 0.5f) /
                                         Configuration.PhysicalWidth);
            double normalizedZ = Clamp01((localZ + Configuration.PhysicalLength * 0.5f) /
                                         Configuration.PhysicalLength);
            double gridX = normalizedX * (HeightmapResolution - 1);
            double gridZ = normalizedZ * (HeightmapResolution - 1);
            int x0 = (int)Math.Floor(gridX);
            int z0 = (int)Math.Floor(gridZ);
            int x1 = Math.Min(HeightmapResolution - 1, x0 + 1);
            int z1 = Math.Min(HeightmapResolution - 1, z0 + 1);
            double tx = gridX - x0;
            double tz = gridZ - z0;
            double lower = heights[z0, x0] + (heights[z0, x1] - heights[z0, x0]) * tx;
            double upper = heights[z1, x0] + (heights[z1, x1] - heights[z1, x0]) * tx;
            return (float)(lower + (upper - lower) * tz);
        }

        public MacroLandform LandformAtNormalized(float normalizedX, float normalizedZ)
        {
            int x = NearestIndex(Clamp01(normalizedX), HeightmapResolution);
            int z = NearestIndex(Clamp01(normalizedZ), HeightmapResolution);
            return (MacroLandform)landforms[z, x];
        }

        public bool IsOceanAtNormalized(float normalizedX, float normalizedZ)
        {
            int x = Math.Min(WaterMaskResolution - 1,
                Math.Max(0, (int)Math.Floor(Clamp01(normalizedX) * WaterMaskResolution)));
            int z = Math.Min(WaterMaskResolution - 1,
                Math.Max(0, (int)Math.Floor(Clamp01(normalizedZ) * WaterMaskResolution)));
            return oceanCells[z, x];
        }

        public float[,] CopyHeights() => (float[,])heights.Clone();

        public bool HasEquivalentProjection(TerrainMaterializationPlan other)
        {
            if (other == null || Window != other.Window ||
                HeightmapResolution != other.HeightmapResolution ||
                WaterMaskResolution != other.WaterMaskResolution ||
                SeaLevel != other.SeaLevel || roads.Count != other.roads.Count)
                return false;
            for (int z = 0; z < HeightmapResolution; z++)
            for (int x = 0; x < HeightmapResolution; x++)
            {
                if (heights[z, x] != other.heights[z, x] ||
                    landforms[z, x] != other.landforms[z, x])
                    return false;
            }
            for (int z = 0; z < WaterMaskResolution; z++)
            for (int x = 0; x < WaterMaskResolution; x++)
                if (oceanCells[z, x] != other.oceanCells[z, x]) return false;
            for (int roadIndex = 0; roadIndex < roads.Count; roadIndex++)
            {
                TerrainProjectedRoad first = roads[roadIndex];
                TerrainProjectedRoad second = other.roads[roadIndex];
                if (first.RoadId != second.RoadId || first.RoadClass != second.RoadClass ||
                    first.Points.Count != second.Points.Count)
                    return false;
                for (int pointIndex = 0; pointIndex < first.Points.Count; pointIndex++)
                    if (first.Points[pointIndex] != second.Points[pointIndex]) return false;
            }
            return true;
        }

        private void RequireHeightIndex(int x, int z)
        {
            if (x < 0 || x >= HeightmapResolution || z < 0 || z >= HeightmapResolution)
                throw new ArgumentOutOfRangeException("Height sample lies outside the materialization plan.");
        }

        private static int NearestIndex(double normalized, int count)
        {
            return (int)Math.Max(0, Math.Min(count - 1,
                Math.Floor(normalized * (count - 1) + 0.5d)));
        }

        private static double Clamp01(double value) => Math.Max(0d, Math.Min(1d, value));
    }

    public static class TerrainMaterializationPlanner
    {
        public static bool TryBuildActiveRegion(
            WorldSession session,
            TerrainMaterializationConfiguration configuration,
            out TerrainMaterializationPlan plan,
            out string error)
        {
            plan = null;
            error = null;
            if (!TryValidateInputs(session, configuration, out error))
                return false;
            if (!session.MacroWorldPlan.TryGetSectorPlacement(
                    session.ActiveSectorId, out MacroSectorPlacement placement))
            {
                error = "active SectorId has no committed MacroWorldPlan placement";
                return false;
            }
            return TryBuildAt(session, configuration, placement.Position, out plan, out error);
        }

        /// <summary>
        /// Diagnostic/tooling seam for comparing representative committed
        /// regions. Product runtime should use TryBuildActiveRegion.
        /// </summary>
        public static bool TryBuildAt(
            WorldSession session,
            TerrainMaterializationConfiguration configuration,
            MacroPoint2D logicalCenter,
            out TerrainMaterializationPlan plan,
            out string error)
        {
            plan = null;
            error = null;
            if (!TryValidateInputs(session, configuration, out error))
                return false;
            if (!session.MacroWorldPlan.WorldBounds.Contains(logicalCenter))
            {
                error = "materialization center is outside committed finite WorldBounds";
                return false;
            }

            TerrainMaterializationWindow window = ResolveWindow(
                session.MacroWorldPlan.WorldBounds, logicalCenter,
                configuration.LogicalWidth, configuration.LogicalLength);
            int resolution = configuration.HeightmapResolution;
            var heights = new float[resolution, resolution];
            var landforms = new byte[resolution, resolution];
            ushort minimum = ushort.MaxValue;
            ushort maximum = ushort.MinValue;
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                MacroPoint2D point = SamplePoint(window, x, z, resolution, resolution);
                ushort elevation = session.MacroGeography.ElevationAt(point);
                heights[z, x] = elevation / 65535f;
                landforms[z, x] = (byte)session.MacroGeography.LandformAt(point);
                if (elevation < minimum) minimum = elevation;
                if (elevation > maximum) maximum = elevation;
            }

            int waterResolution = configuration.WaterMaskResolution;
            var ocean = new bool[waterResolution, waterResolution];
            for (int z = 0; z < waterResolution; z++)
            for (int x = 0; x < waterResolution; x++)
            {
                MacroPoint2D point = SampleCellCenter(window, x, z, waterResolution, waterResolution);
                ocean[z, x] = session.MacroWater.SampleAt(point).IsOcean;
            }

            List<TerrainProjectedRoad> roads = ProjectRoads(
                session.MacroHumanGeography.Roads, window, configuration,
                out int intersectingRoadCount);
            plan = new TerrainMaterializationPlan(
                session.WorldId,
                session.ActiveSectorId,
                logicalCenter,
                window,
                configuration,
                heights,
                landforms,
                ocean,
                roads,
                minimum,
                maximum,
                session.MacroWater.SeaLevel,
                intersectingRoadCount,
                session.MacroWorldPlan.CanonicalHash,
                session.MacroGeography.CanonicalHash,
                session.MacroWater.CanonicalHash,
                session.MacroHumanGeography.CanonicalHash);
            return true;
        }

        private static bool TryValidateInputs(
            WorldSession session,
            TerrainMaterializationConfiguration configuration,
            out string error)
        {
            error = null;
            if (session == null)
            {
                error = "a validated WorldSession is required";
                return false;
            }
            if (!session.HasMacroWorldPlan || !session.HasMacroGeography || !session.HasMacroWater ||
                !session.HasMacroHumanGeography)
            {
                error = "terrain materialization requires committed schema-5 Plan, Geography, Water, and Human Geography truth; legacy truth is not fabricated";
                return false;
            }
            if (configuration == null || !configuration.TryValidate(out error))
                return false;
            return true;
        }

        private static TerrainMaterializationWindow ResolveWindow(
            FiniteMacroWorldBounds bounds,
            MacroPoint2D center,
            long requestedWidth,
            long requestedLength)
        {
            long width = Math.Min(bounds.Width, requestedWidth);
            long length = Math.Min(bounds.Height, requestedLength);
            long minX = ClampWindowMinimum(center.X - width / 2L, bounds.MinX, bounds.MaxXExclusive - width);
            long minY = ClampWindowMinimum(center.Y - length / 2L, bounds.MinY, bounds.MaxYExclusive - length);
            return new TerrainMaterializationWindow(
                minX, minY, minX + width - 1L, minY + length - 1L);
        }

        private static long ClampWindowMinimum(long value, long minimum, long maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static MacroPoint2D SamplePoint(
            TerrainMaterializationWindow window,
            int x,
            int z,
            int columns,
            int rows)
        {
            return new MacroPoint2D(
                SampleAxis(window.MinX, window.LogicalWidth, x, columns),
                SampleAxis(window.MinY, window.LogicalLength, z, rows));
        }

        private static MacroPoint2D SampleCellCenter(
            TerrainMaterializationWindow window,
            int x,
            int z,
            int columns,
            int rows)
        {
            return new MacroPoint2D(
                SampleAxisFraction(window.MinX, window.LogicalWidth, 2L * x + 1L, 2L * columns),
                SampleAxisFraction(window.MinY, window.LogicalLength, 2L * z + 1L, 2L * rows));
        }

        private static long SampleAxis(long minimum, long extent, int index, int count)
        {
            if (count <= 1) return minimum;
            long numerator = checked((long)index * (extent - 1L));
            long denominator = count - 1L;
            return minimum + (numerator + denominator / 2L) / denominator;
        }

        private static long SampleAxisFraction(long minimum, long extent, long numerator, long denominator)
        {
            long scaled = checked(numerator * (extent - 1L));
            return minimum + (scaled + denominator / 2L) / denominator;
        }

        private static List<TerrainProjectedRoad> ProjectRoads(
            IReadOnlyList<MacroRoad> sourceRoads,
            TerrainMaterializationWindow window,
            TerrainMaterializationConfiguration configuration,
            out int intersectingRoadCount)
        {
            var projected = new List<TerrainProjectedRoad>();
            intersectingRoadCount = 0;
            for (int roadIndex = 0; roadIndex < sourceRoads.Count; roadIndex++)
            {
                MacroRoad road = sourceRoads[roadIndex];
                bool roadIntersects = false;
                List<Vector2> current = null;
                for (int pointIndex = 1; pointIndex < road.Polyline.Count; pointIndex++)
                {
                    MacroPoint2D first = road.Polyline[pointIndex - 1];
                    MacroPoint2D second = road.Polyline[pointIndex];
                    if (!TryClip(first, second, window, out double x0, out double y0, out double x1, out double y1))
                    {
                        FlushRoadFragment(road, current, projected);
                        current = null;
                        continue;
                    }

                    roadIntersects = true;
                    Vector2 start = ProjectPoint(x0, y0, window, configuration);
                    Vector2 end = ProjectPoint(x1, y1, window, configuration);
                    if (current == null || current.Count == 0 ||
                        (current[current.Count - 1] - start).sqrMagnitude > 0.0001f)
                    {
                        FlushRoadFragment(road, current, projected);
                        current = new List<Vector2> { start };
                    }
                    if ((current[current.Count - 1] - end).sqrMagnitude > 0.0001f)
                        current.Add(end);
                }
                FlushRoadFragment(road, current, projected);
                if (roadIntersects) intersectingRoadCount++;
            }
            return projected;
        }

        private static void FlushRoadFragment(
            MacroRoad road,
            IList<Vector2> current,
            ICollection<TerrainProjectedRoad> projected)
        {
            if (current != null && current.Count >= 2)
                projected.Add(new TerrainProjectedRoad(road.RoadId, road.RoadClass, current));
        }

        private static Vector2 ProjectPoint(
            double x,
            double y,
            TerrainMaterializationWindow window,
            TerrainMaterializationConfiguration configuration)
        {
            double normalizedX = (x - window.MinX) / (window.LogicalWidth - 1d);
            double normalizedY = (y - window.MinY) / (window.LogicalLength - 1d);
            return new Vector2(
                (float)((normalizedX - 0.5d) * configuration.PhysicalWidth),
                (float)((normalizedY - 0.5d) * configuration.PhysicalLength));
        }

        private static bool TryClip(
            MacroPoint2D first,
            MacroPoint2D second,
            TerrainMaterializationWindow window,
            out double x0,
            out double y0,
            out double x1,
            out double y1)
        {
            x0 = first.X;
            y0 = first.Y;
            x1 = second.X;
            y1 = second.Y;
            double dx = x1 - x0;
            double dy = y1 - y0;
            double entry = 0d;
            double exit = 1d;
            if (!ClipTest(-dx, x0 - window.MinX, ref entry, ref exit) ||
                !ClipTest(dx, window.MaxXInclusive - x0, ref entry, ref exit) ||
                !ClipTest(-dy, y0 - window.MinY, ref entry, ref exit) ||
                !ClipTest(dy, window.MaxYInclusive - y0, ref entry, ref exit))
                return false;
            double originalX = x0;
            double originalY = y0;
            if (exit < 1d)
            {
                x1 = originalX + exit * dx;
                y1 = originalY + exit * dy;
            }
            if (entry > 0d)
            {
                x0 = originalX + entry * dx;
                y0 = originalY + entry * dy;
            }
            return true;
        }

        private static bool ClipTest(double p, double q, ref double entry, ref double exit)
        {
            if (Math.Abs(p) < double.Epsilon)
                return q >= 0d;
            double ratio = q / p;
            if (p < 0d)
            {
                if (ratio > exit) return false;
                if (ratio > entry) entry = ratio;
            }
            else
            {
                if (ratio < entry) return false;
                if (ratio < exit) exit = ratio;
            }
            return true;
        }
    }
}
