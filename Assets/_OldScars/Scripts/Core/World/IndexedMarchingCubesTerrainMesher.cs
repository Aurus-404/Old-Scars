using System;
using System.Collections.Generic;
using UnityEngine;

namespace OldScars.Core.World
{
    /// <summary>
    /// Development bake-off backend. It extracts one indexed vertex per shared
    /// density-grid edge and connects those intersections from the six cube
    /// faces. Ambiguous four-crossing faces use the sign at the shared bilinear
    /// face center; neighboring cells therefore make the same decision from
    /// the same four samples. Separate closed loops remain separate. This is a
    /// bounded Marching Cubes variant, not a production LOD commitment.
    /// </summary>
    internal static class IndexedMarchingCubesTerrainMesher
    {
        private readonly struct EdgeKey : IEquatable<EdgeKey>
        {
            public EdgeKey(int x, int y, int z, int axis)
            {
                X = x;
                Y = y;
                Z = z;
                Axis = axis;
            }

            public int X { get; }
            public int Y { get; }
            public int Z { get; }
            public int Axis { get; }

            public bool Equals(EdgeKey other) =>
                X == other.X && Y == other.Y && Z == other.Z && Axis == other.Axis;
            public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);
            public override int GetHashCode() =>
                unchecked((((X * 397) ^ Y) * 397 ^ Z) * 397 ^ Axis);
        }

        private static readonly int[,] CubeCornerOffsets =
        {
            { 0, 0, 0 }, { 1, 0, 0 }, { 1, 0, 1 }, { 0, 0, 1 },
            { 0, 1, 0 }, { 1, 1, 0 }, { 1, 1, 1 }, { 0, 1, 1 }
        };

        private static readonly int[,] CubeEdges =
        {
            { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 },
            { 4, 5 }, { 5, 6 }, { 6, 7 }, { 7, 4 },
            { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 }
        };

        // Each face lists its corners and boundary edges in the same circular
        // order. Orientation is irrelevant to connectivity; winding is fixed
        // against the sampled outward normal after a loop is recovered.
        private static readonly int[,] FaceCorners =
        {
            { 0, 1, 2, 3 }, { 4, 5, 6, 7 },
            { 0, 4, 5, 1 }, { 1, 5, 6, 2 },
            { 3, 2, 6, 7 }, { 0, 3, 7, 4 }
        };

        private static readonly int[,] FaceEdges =
        {
            { 0, 1, 2, 3 }, { 4, 5, 6, 7 },
            { 8, 4, 9, 0 }, { 9, 5, 10, 1 },
            { 2, 10, 6, 11 }, { 3, 11, 7, 8 }
        };

        public static DeformableTerrainChunkMeshData Build(
            DeformableTerrainVolume volume,
            DeformableTerrainChunkId chunkId)
        {
            if (volume == null) throw new ArgumentNullException(nameof(volume));
            volume.ChunkBounds(chunkId);

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new[] { new List<int>(), new List<int>(), new List<int>() };
            var vertexCache = new Dictionary<EdgeKey, int>();

            int startX = chunkId.X * volume.Configuration.CellsPerChunkX;
            int endX = startX + volume.Configuration.CellsPerChunkX;
            int startY = chunkId.Y * volume.Configuration.CellsPerChunkY;
            int endY = startY + volume.Configuration.CellsPerChunkY;
            int startZ = chunkId.Z * volume.Configuration.CellsPerChunkZ;
            int endZ = startZ + volume.Configuration.CellsPerChunkZ;

            var densities = new float[8];
            var materials = new DeformableTerrainMaterial[8];
            var crossed = new bool[12];
            var adjacency = new List<int>[12];

            for (int z = startZ; z < endZ; z++)
            for (int y = startY; y < endY; y++)
            for (int x = startX; x < endX; x++)
            {
                bool hasSolid = false;
                bool hasAir = false;
                for (int corner = 0; corner < 8; corner++)
                {
                    int sampleX = x + CubeCornerOffsets[corner, 0];
                    int sampleY = y + CubeCornerOffsets[corner, 1];
                    int sampleZ = z + CubeCornerOffsets[corner, 2];
                    densities[corner] = volume.DensityAtSample(sampleX, sampleY, sampleZ);
                    materials[corner] = volume.MaterialAtSample(sampleX, sampleY, sampleZ);
                    hasSolid |= densities[corner] >= 0f;
                    hasAir |= densities[corner] < 0f;
                }
                if (!hasSolid || !hasAir) continue;

                for (int edge = 0; edge < 12; edge++)
                {
                    int first = CubeEdges[edge, 0];
                    int second = CubeEdges[edge, 1];
                    crossed[edge] = (densities[first] >= 0f) != (densities[second] >= 0f);
                    adjacency[edge] = crossed[edge] ? new List<int>(2) : null;
                }

                ConnectFaceLoops(densities, crossed, adjacency);
                DeformableTerrainMaterial material = ResolveCellMaterial(densities, materials);
                EmitLoops(
                    volume, x, y, z, densities, crossed, adjacency, material,
                    vertexCache, vertices, normals, uvs, triangles[(int)material]);
            }

            return new DeformableTerrainChunkMeshData(
                chunkId, vertices, normals, uvs, triangles);
        }

        private static void ConnectFaceLoops(
            float[] densities,
            bool[] crossed,
            List<int>[] adjacency)
        {
            for (int face = 0; face < 6; face++)
            {
                var crossingEdges = new int[4];
                int crossingCount = 0;
                for (int side = 0; side < 4; side++)
                {
                    int edge = FaceEdges[face, side];
                    if (crossed[edge]) crossingEdges[crossingCount++] = edge;
                }
                if (crossingCount == 0) continue;
                if (crossingCount == 2)
                {
                    Connect(adjacency, crossingEdges[0], crossingEdges[1]);
                    continue;
                }
                if (crossingCount != 4)
                    throw new InvalidOperationException("Marching Cubes face has an invalid crossing count.");

                float center = 0f;
                for (int corner = 0; corner < 4; corner++)
                    center += densities[FaceCorners[face, corner]];
                bool centerSolid = center >= 0f;
                bool firstCornerSolid = densities[FaceCorners[face, 0]] >= 0f;
                if (centerSolid == firstCornerSolid)
                {
                    Connect(adjacency, FaceEdges[face, 0], FaceEdges[face, 1]);
                    Connect(adjacency, FaceEdges[face, 2], FaceEdges[face, 3]);
                }
                else
                {
                    Connect(adjacency, FaceEdges[face, 3], FaceEdges[face, 0]);
                    Connect(adjacency, FaceEdges[face, 1], FaceEdges[face, 2]);
                }
            }
        }

        private static void Connect(List<int>[] adjacency, int first, int second)
        {
            if (!adjacency[first].Contains(second)) adjacency[first].Add(second);
            if (!adjacency[second].Contains(first)) adjacency[second].Add(first);
        }

        private static void EmitLoops(
            DeformableTerrainVolume volume,
            int cellX,
            int cellY,
            int cellZ,
            float[] densities,
            bool[] crossed,
            List<int>[] adjacency,
            DeformableTerrainMaterial material,
            IDictionary<EdgeKey, int> vertexCache,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles)
        {
            var visited = new bool[12];
            for (int start = 0; start < 12; start++)
            {
                if (!crossed[start] || visited[start]) continue;
                if (adjacency[start] == null || adjacency[start].Count != 2)
                    throw new InvalidOperationException("Marching Cubes edge connectivity is not manifold.");

                var loopEdges = new List<int>(12);
                int previous = -1;
                int current = start;
                for (int step = 0; step <= 12; step++)
                {
                    if (visited[current] && current != start)
                        throw new InvalidOperationException("Marching Cubes loop intersects a previously emitted loop.");
                    if (current == start && step > 0) break;
                    visited[current] = true;
                    loopEdges.Add(current);
                    List<int> neighbors = adjacency[current];
                    if (neighbors == null || neighbors.Count != 2)
                        throw new InvalidOperationException("Marching Cubes loop contains an open edge.");
                    int next = neighbors[0] != previous ? neighbors[0] : neighbors[1];
                    previous = current;
                    current = next;
                }
                if (current != start)
                    throw new InvalidOperationException("Marching Cubes loop did not close inside one cell.");

                var loopVertices = new List<int>(loopEdges.Count);
                for (int index = 0; index < loopEdges.Count; index++)
                {
                    int vertex = ResolveVertex(
                        volume, cellX, cellY, cellZ, loopEdges[index], densities,
                        vertexCache, vertices, normals, uvs);
                    if (loopVertices.Count == 0 || loopVertices[loopVertices.Count - 1] != vertex)
                        loopVertices.Add(vertex);
                }
                if (loopVertices.Count > 1 && loopVertices[0] == loopVertices[loopVertices.Count - 1])
                    loopVertices.RemoveAt(loopVertices.Count - 1);
                if (loopVertices.Count < 3) continue;

                Vector3 polygonNormal = Vector3.zero;
                Vector3 desiredNormal = Vector3.zero;
                for (int index = 0; index < loopVertices.Count; index++)
                {
                    Vector3 currentPosition = vertices[loopVertices[index]];
                    Vector3 nextPosition = vertices[loopVertices[(index + 1) % loopVertices.Count]];
                    polygonNormal += Vector3.Cross(currentPosition, nextPosition);
                    desiredNormal += normals[loopVertices[index]];
                }
                if (Vector3.Dot(polygonNormal, desiredNormal) < 0f)
                    loopVertices.Reverse();

                for (int index = 1; index < loopVertices.Count - 1; index++)
                    AddTriangle(loopVertices[0], loopVertices[index], loopVertices[index + 1], vertices, triangles);
            }
        }

        private static int ResolveVertex(
            DeformableTerrainVolume volume,
            int cellX,
            int cellY,
            int cellZ,
            int cubeEdge,
            float[] densities,
            IDictionary<EdgeKey, int> vertexCache,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs)
        {
            int firstCorner = CubeEdges[cubeEdge, 0];
            int secondCorner = CubeEdges[cubeEdge, 1];
            int firstX = cellX + CubeCornerOffsets[firstCorner, 0];
            int firstY = cellY + CubeCornerOffsets[firstCorner, 1];
            int firstZ = cellZ + CubeCornerOffsets[firstCorner, 2];
            int secondX = cellX + CubeCornerOffsets[secondCorner, 0];
            int secondY = cellY + CubeCornerOffsets[secondCorner, 1];
            int secondZ = cellZ + CubeCornerOffsets[secondCorner, 2];
            float firstDensity = densities[firstCorner];
            float secondDensity = densities[secondCorner];

            EdgeKey key;
            if (firstDensity == 0f)
                key = new EdgeKey(firstX, firstY, firstZ, 3);
            else if (secondDensity == 0f)
                key = new EdgeKey(secondX, secondY, secondZ, 3);
            else if (firstX != secondX)
                key = new EdgeKey(Math.Min(firstX, secondX), firstY, firstZ, 0);
            else if (firstY != secondY)
                key = new EdgeKey(firstX, Math.Min(firstY, secondY), firstZ, 1);
            else
                key = new EdgeKey(firstX, firstY, Math.Min(firstZ, secondZ), 2);
            if (vertexCache.TryGetValue(key, out int existing)) return existing;

            if (CompareSample(firstX, firstY, firstZ, secondX, secondY, secondZ) > 0)
            {
                Swap(ref firstX, ref secondX);
                Swap(ref firstY, ref secondY);
                Swap(ref firstZ, ref secondZ);
                Swap(ref firstDensity, ref secondDensity);
            }
            Vector3 firstPosition = volume.SamplePosition(firstX, firstY, firstZ);
            Vector3 secondPosition = volume.SamplePosition(secondX, secondY, secondZ);
            float denominator = firstDensity - secondDensity;
            float t = Math.Abs(denominator) > 0.000001f ? firstDensity / denominator : 0.5f;
            Vector3 position = Vector3.LerpUnclamped(firstPosition, secondPosition, t);
            Vector3 normal = volume.OutwardNormalAtLocal(position);
            int created = vertices.Count;
            vertices.Add(position);
            normals.Add(normal);
            uvs.Add(ProjectUv(position, normal));
            vertexCache.Add(key, created);
            return created;
        }

        private static void AddTriangle(
            int first,
            int second,
            int third,
            IReadOnlyList<Vector3> vertices,
            ICollection<int> triangles)
        {
            if (first == second || second == third || first == third) return;
            if (Vector3.Cross(vertices[second] - vertices[first], vertices[third] - vertices[first])
                    .sqrMagnitude < 0.00000001f)
                return;
            triangles.Add(first);
            triangles.Add(second);
            triangles.Add(third);
        }

        private static DeformableTerrainMaterial ResolveCellMaterial(
            float[] densities,
            DeformableTerrainMaterial[] materials)
        {
            int bestIndex = 0;
            float closestSolidDensity = float.MaxValue;
            for (int index = 0; index < densities.Length; index++)
            {
                if (densities[index] < 0f || densities[index] >= closestSolidDensity) continue;
                closestSolidDensity = densities[index];
                bestIndex = index;
            }
            return materials[bestIndex];
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

        private static int CompareSample(
            int firstX, int firstY, int firstZ,
            int secondX, int secondY, int secondZ)
        {
            int x = firstX.CompareTo(secondX);
            if (x != 0) return x;
            int y = firstY.CompareTo(secondY);
            return y != 0 ? y : firstZ.CompareTo(secondZ);
        }

        private static void Swap(ref int first, ref int second)
        {
            int value = first;
            first = second;
            second = value;
        }

        private static void Swap(ref float first, ref float second)
        {
            float value = first;
            first = second;
            second = value;
        }
    }
}
