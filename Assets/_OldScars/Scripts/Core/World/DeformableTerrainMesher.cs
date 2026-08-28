using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace OldScars.Core.World
{
    public sealed class DeformableTerrainChunkMeshData
    {
        private readonly ReadOnlyCollection<Vector3> vertices;
        private readonly ReadOnlyCollection<Vector3> normals;
        private readonly ReadOnlyCollection<Vector2> uvs;
        private readonly ReadOnlyCollection<int>[] triangles;

        internal DeformableTerrainChunkMeshData(
            DeformableTerrainChunkId chunkId,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int>[] triangles)
        {
            ChunkId = chunkId;
            this.vertices = new ReadOnlyCollection<Vector3>(vertices);
            this.normals = new ReadOnlyCollection<Vector3>(normals);
            this.uvs = new ReadOnlyCollection<Vector2>(uvs);
            this.triangles = new ReadOnlyCollection<int>[triangles.Length];
            for (int index = 0; index < triangles.Length; index++)
                this.triangles[index] = new ReadOnlyCollection<int>(triangles[index]);
        }

        public DeformableTerrainChunkId ChunkId { get; }
        public IReadOnlyList<Vector3> Vertices => vertices;
        public IReadOnlyList<Vector3> Normals => normals;
        public IReadOnlyList<Vector2> UVs => uvs;
        public int SubMeshCount => triangles.Length;
        public int TriangleCount =>
            (triangles[0].Count + triangles[1].Count + triangles[2].Count) / 3;
        public IReadOnlyList<int> Triangles(int subMesh) => triangles[subMesh];
    }

    /// <summary>
    /// Small smooth-isosurface mesher for the technical spike. Each cube is
    /// decomposed along the same global 0-to-6 diagonal into six tetrahedra.
    /// This avoids a large marching-cubes lookup table while retaining
    /// interpolated, truly volumetric surfaces and deterministic boundaries.
    /// </summary>
    public static class DeformableTerrainMesher
    {
        private static readonly int[,] CubeCornerOffsets =
        {
            { 0, 0, 0 }, { 1, 0, 0 }, { 1, 0, 1 }, { 0, 0, 1 },
            { 0, 1, 0 }, { 1, 1, 0 }, { 1, 1, 1 }, { 0, 1, 1 }
        };

        private static readonly int[,] Tetrahedra =
        {
            { 0, 5, 1, 6 }, { 0, 1, 2, 6 }, { 0, 2, 3, 6 },
            { 0, 3, 7, 6 }, { 0, 7, 4, 6 }, { 0, 4, 5, 6 }
        };

        private static readonly int[,] TetraEdges =
        {
            { 0, 1 }, { 0, 2 }, { 0, 3 }, { 1, 2 }, { 1, 3 }, { 2, 3 }
        };

        public static DeformableTerrainChunkMeshData Build(
            DeformableTerrainVolume volume,
            DeformableTerrainChunkId chunkId)
        {
            if (volume == null)
                throw new ArgumentNullException(nameof(volume));
            volume.ChunkBounds(chunkId);

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new[] { new List<int>(), new List<int>(), new List<int>() };
            int startX = chunkId.X * volume.Configuration.CellsPerChunkX;
            int endX = startX + volume.Configuration.CellsPerChunkX;
            int startZ = chunkId.Z * volume.Configuration.CellsPerChunkZ;
            int endZ = startZ + volume.Configuration.CellsPerChunkZ;

            var cubePositions = new Vector3[8];
            var cubeDensities = new float[8];
            var cubeMaterials = new DeformableTerrainMaterial[8];
            var tetraPositions = new Vector3[4];
            var tetraDensities = new float[4];
            for (int z = startZ; z < endZ; z++)
            for (int y = 0; y < volume.Configuration.VerticalCells; y++)
            for (int x = startX; x < endX; x++)
            {
                bool hasSolid = false;
                bool hasAir = false;
                for (int corner = 0; corner < 8; corner++)
                {
                    int sampleX = x + CubeCornerOffsets[corner, 0];
                    int sampleY = y + CubeCornerOffsets[corner, 1];
                    int sampleZ = z + CubeCornerOffsets[corner, 2];
                    cubePositions[corner] = volume.SamplePosition(sampleX, sampleY, sampleZ);
                    cubeDensities[corner] = volume.DensityAtSample(sampleX, sampleY, sampleZ);
                    cubeMaterials[corner] = volume.MaterialAtSample(sampleX, sampleY, sampleZ);
                    hasSolid |= cubeDensities[corner] >= 0f;
                    hasAir |= cubeDensities[corner] < 0f;
                }
                if (!hasSolid || !hasAir)
                    continue;

                DeformableTerrainMaterial material = ResolveCellMaterial(cubeDensities, cubeMaterials);
                for (int tetrahedron = 0; tetrahedron < 6; tetrahedron++)
                {
                    for (int vertex = 0; vertex < 4; vertex++)
                    {
                        int cubeCorner = Tetrahedra[tetrahedron, vertex];
                        tetraPositions[vertex] = cubePositions[cubeCorner];
                        tetraDensities[vertex] = cubeDensities[cubeCorner];
                    }
                    PolygoniseTetrahedron(
                        volume, tetraPositions, tetraDensities, material,
                        vertices, normals, uvs, triangles[(int)material]);
                }
            }

            return new DeformableTerrainChunkMeshData(
                chunkId, vertices, normals, uvs, triangles);
        }

        private static void PolygoniseTetrahedron(
            DeformableTerrainVolume volume,
            Vector3[] positions,
            float[] densities,
            DeformableTerrainMaterial material,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles)
        {
            var intersections = new List<Vector3>(4);
            for (int edge = 0; edge < 6; edge++)
            {
                int first = TetraEdges[edge, 0];
                int second = TetraEdges[edge, 1];
                bool firstSolid = densities[first] >= 0f;
                bool secondSolid = densities[second] >= 0f;
                if (firstSolid == secondSolid)
                    continue;
                Vector3 firstPosition = positions[first];
                Vector3 secondPosition = positions[second];
                float firstDensity = densities[first];
                float secondDensity = densities[second];
                if (ComparePosition(firstPosition, secondPosition) > 0)
                {
                    Vector3 positionSwap = firstPosition;
                    firstPosition = secondPosition;
                    secondPosition = positionSwap;
                    float densitySwap = firstDensity;
                    firstDensity = secondDensity;
                    secondDensity = densitySwap;
                }
                float denominator = firstDensity - secondDensity;
                float t = Math.Abs(denominator) > 0.000001f
                    ? firstDensity / denominator
                    : 0.5f;
                Vector3 point = Vector3.LerpUnclamped(firstPosition, secondPosition, t);
                AddUnique(intersections, point);
            }
            if (intersections.Count < 3)
                return;

            Vector3 centroid = Vector3.zero;
            Vector3 desiredNormal = Vector3.zero;
            for (int index = 0; index < intersections.Count; index++)
            {
                centroid += intersections[index];
                desiredNormal += volume.OutwardNormalAtLocal(intersections[index]);
            }
            centroid /= intersections.Count;
            desiredNormal = desiredNormal.sqrMagnitude > 0.000001f
                ? desiredNormal.normalized
                : Vector3.up;
            SortAroundNormal(intersections, centroid, desiredNormal);
            if (Vector3.Dot(
                    Vector3.Cross(intersections[1] - intersections[0], intersections[2] - intersections[0]),
                    desiredNormal) < 0f)
                intersections.Reverse();

            for (int triangle = 1; triangle < intersections.Count - 1; triangle++)
            {
                AddVertex(volume, intersections[0], vertices, normals, uvs, triangles);
                AddVertex(volume, intersections[triangle], vertices, normals, uvs, triangles);
                AddVertex(volume, intersections[triangle + 1], vertices, normals, uvs, triangles);
            }
        }

        private static void AddVertex(
            DeformableTerrainVolume volume,
            Vector3 position,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles)
        {
            Vector3 normal = volume.OutwardNormalAtLocal(position);
            triangles.Add(vertices.Count);
            vertices.Add(position);
            normals.Add(normal);
            uvs.Add(ProjectUv(position, normal));
        }

        private static Vector2 ProjectUv(Vector3 position, Vector3 normal)
        {
            const float scale = 0.125f;
            Vector3 absolute = new Vector3(Math.Abs(normal.x), Math.Abs(normal.y), Math.Abs(normal.z));
            if (absolute.y >= absolute.x && absolute.y >= absolute.z)
                return new Vector2(position.x, position.z) * scale;
            if (absolute.x >= absolute.z)
                return new Vector2(position.z, position.y) * scale;
            return new Vector2(position.x, position.y) * scale;
        }

        private static void SortAroundNormal(
            List<Vector3> points,
            Vector3 centroid,
            Vector3 normal)
        {
            Vector3 tangent = Vector3.Cross(
                Math.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right, normal).normalized;
            Vector3 bitangent = Vector3.Cross(normal, tangent);
            points.Sort((left, right) =>
            {
                Vector3 leftDelta = left - centroid;
                Vector3 rightDelta = right - centroid;
                float leftAngle = Mathf.Atan2(
                    Vector3.Dot(leftDelta, bitangent), Vector3.Dot(leftDelta, tangent));
                float rightAngle = Mathf.Atan2(
                    Vector3.Dot(rightDelta, bitangent), Vector3.Dot(rightDelta, tangent));
                return leftAngle.CompareTo(rightAngle);
            });
        }

        private static void AddUnique(List<Vector3> points, Vector3 candidate)
        {
            for (int index = 0; index < points.Count; index++)
                if ((points[index] - candidate).sqrMagnitude < 0.0000001f)
                    return;
            points.Add(candidate);
        }

        private static int ComparePosition(Vector3 left, Vector3 right)
        {
            int x = left.x.CompareTo(right.x);
            if (x != 0) return x;
            int y = left.y.CompareTo(right.y);
            return y != 0 ? y : left.z.CompareTo(right.z);
        }

        private static DeformableTerrainMaterial ResolveCellMaterial(
            float[] densities,
            DeformableTerrainMaterial[] materials)
        {
            int bestIndex = 0;
            float closestSolidDensity = float.MaxValue;
            for (int index = 0; index < densities.Length; index++)
            {
                if (densities[index] < 0f || densities[index] >= closestSolidDensity)
                    continue;
                closestSolidDensity = densities[index];
                bestIndex = index;
            }
            return materials[bestIndex];
        }
    }
}
