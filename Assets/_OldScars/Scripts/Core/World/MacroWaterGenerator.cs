using System;
using System.Collections.Generic;

namespace OldScars.Core.World
{
    /// <summary>
    /// Deterministic generation-time Macro Water V1 pass. It chooses a global
    /// sea level against boundary-connected ocean coverage, labels bodies and
    /// coastlines, then builds one acyclic conditioned drainage tree. Nothing
    /// in this class runs per frame or per gameplay tick.
    /// </summary>
    public static class MacroWaterGenerator
    {
        public static bool TryGenerate(
            MacroWorldPlan macroWorldPlan,
            MacroGeographyPlan geography,
            LandCoveragePreset landCoverage,
            out MacroWaterPlan water,
            out string error)
        {
            water = null;
            error = null;
            if (macroWorldPlan == null || geography == null)
            {
                error = "Macro Water generation requires committed MacroWorldPlan and MacroGeography";
                return false;
            }
            if (macroWorldPlan.WorldBounds != geography.WorldBounds)
            {
                error = "Macro Water inputs disagree on finite WorldBounds";
                return false;
            }

            try
            {
                MacroWaterGenerationSettings settings =
                    MacroWaterGenerationSettings.Resolve(landCoverage, geography);
                ushort[] elevations = geography.CopyElevationSamples();
                ushort seaLevel = ResolveSeaLevel(
                    elevations, settings.SampleColumns, settings.SampleRows,
                    settings.TargetLandRatioQ16, out byte[] oceanMask);
                ushort[] oceanLabels = LabelOceanBodies(
                    oceanMask, settings.SampleColumns, settings.SampleRows);
                byte[] coastline = BuildCoastline(
                    oceanMask, settings.SampleColumns, settings.SampleRows);
                BuildConditionedDrainage(
                    elevations, oceanMask, settings.SampleColumns, settings.SampleRows,
                    out ushort[] conditioned, out byte[] drainage);
                List<MacroBasinCandidate> basins = BuildBasinCandidates(
                    elevations, conditioned, settings.SampleColumns, settings.SampleRows,
                    settings.MinimumBasinCells);

                return MacroWaterPlan.TryCreate(
                    settings, geography, seaLevel, oceanMask, oceanLabels,
                    coastline, conditioned, drainage, basins, out water, out error);
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is InvalidOperationException ||
                exception is OverflowException)
            {
                error = "Macro Water V1 generation failed: " + exception.Message;
                return false;
            }
        }

        private static ushort ResolveSeaLevel(
            ushort[] elevations,
            int columns,
            int rows,
            int targetLandRatioQ16,
            out byte[] selectedOcean)
        {
            var sorted = (ushort[])elevations.Clone();
            Array.Sort(sorted);
            int lower = 0;
            int upper = sorted.Length - 1;
            long bestDifference = long.MaxValue;
            ushort bestSeaLevel = sorted[0];
            byte[] bestOcean = null;

            while (lower <= upper)
            {
                int middle = lower + (upper - lower) / 2;
                ushort seaLevel = sorted[middle];
                byte[] ocean = BuildBoundaryConnectedOcean(
                    elevations, columns, rows, seaLevel, out int oceanCount);
                int landRatioQ16 = (int)((long)(elevations.Length - oceanCount) * 65535 /
                                               elevations.Length);
                long difference = Math.Abs((long)landRatioQ16 - targetLandRatioQ16);
                if (bestOcean == null || difference < bestDifference ||
                    difference == bestDifference && seaLevel < bestSeaLevel)
                {
                    bestDifference = difference;
                    bestSeaLevel = seaLevel;
                    bestOcean = ocean;
                }

                if (landRatioQ16 > targetLandRatioQ16)
                    lower = middle + 1;
                else if (landRatioQ16 < targetLandRatioQ16)
                    upper = middle - 1;
                else
                    break;
            }

            if (bestOcean == null)
                throw new InvalidOperationException("Sea-level search produced no candidate.");
            selectedOcean = bestOcean;
            return bestSeaLevel;
        }

        private static byte[] BuildBoundaryConnectedOcean(
            ushort[] elevations,
            int columns,
            int rows,
            ushort seaLevel,
            out int oceanCount)
        {
            var ocean = new byte[elevations.Length];
            var queued = new bool[elevations.Length];
            var queue = new Queue<int>();
            for (int x = 0; x < columns; x++)
            {
                EnqueueBoundary(x, 0);
                EnqueueBoundary(x, rows - 1);
            }
            for (int y = 1; y + 1 < rows; y++)
            {
                EnqueueBoundary(0, y);
                EnqueueBoundary(columns - 1, y);
            }

            oceanCount = 0;
            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                ocean[index] = 1;
                oceanCount++;
                int x = index % columns;
                int y = index / columns;
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0) continue;
                    int nextX = x + offsetX;
                    int nextY = y + offsetY;
                    if (nextX < 0 || nextX >= columns || nextY < 0 || nextY >= rows) continue;
                    int next = nextY * columns + nextX;
                    if (queued[next] || elevations[next] > seaLevel) continue;
                    queued[next] = true;
                    queue.Enqueue(next);
                }
            }
            return ocean;

            void EnqueueBoundary(int x, int y)
            {
                int index = y * columns + x;
                if (queued[index] || elevations[index] > seaLevel) return;
                queued[index] = true;
                queue.Enqueue(index);
            }
        }

        private static ushort[] LabelOceanBodies(byte[] ocean, int columns, int rows)
        {
            var labels = new ushort[ocean.Length];
            var queue = new Queue<int>();
            int nextBody = 0;
            for (int start = 0; start < ocean.Length; start++)
            {
                if (ocean[start] == 0 || labels[start] != 0) continue;
                nextBody++;
                if (nextBody > ushort.MaxValue)
                    throw new InvalidOperationException("Ocean body count exceeds durable ushort identity capacity.");
                ushort body = (ushort)nextBody;
                labels[start] = body;
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    int x = current % columns;
                    int y = current / columns;
                    for (int offsetY = -1; offsetY <= 1; offsetY++)
                    for (int offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        if (offsetX == 0 && offsetY == 0) continue;
                        int nextX = x + offsetX;
                        int nextY = y + offsetY;
                        if (nextX < 0 || nextX >= columns || nextY < 0 || nextY >= rows) continue;
                        int next = nextY * columns + nextX;
                        if (ocean[next] == 0 || labels[next] != 0) continue;
                        labels[next] = body;
                        queue.Enqueue(next);
                    }
                }
            }
            return labels;
        }

        private static byte[] BuildCoastline(byte[] ocean, int columns, int rows)
        {
            var coastline = new byte[ocean.Length];
            for (int index = 0; index < ocean.Length; index++)
            {
                if (ocean[index] != 0) continue;
                int x = index % columns;
                int y = index / columns;
                for (int offsetY = -1; offsetY <= 1 && coastline[index] == 0; offsetY++)
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0) continue;
                    int nextX = x + offsetX;
                    int nextY = y + offsetY;
                    if (nextX >= 0 && nextX < columns && nextY >= 0 && nextY < rows &&
                        ocean[nextY * columns + nextX] != 0)
                    {
                        coastline[index] = 1;
                        break;
                    }
                }
            }
            return coastline;
        }

        private static void BuildConditionedDrainage(
            ushort[] elevations,
            byte[] ocean,
            int columns,
            int rows,
            out ushort[] conditioned,
            out byte[] drainage)
        {
            conditioned = new ushort[elevations.Length];
            drainage = new byte[elevations.Length];
            for (int index = 0; index < drainage.Length; index++)
                drainage[index] = MacroWaterPlan.DrainageOutlet;

            var visited = new bool[elevations.Length];
            var heap = new StableSampleMinHeap(elevations.Length);
            for (int index = 0; index < ocean.Length; index++)
            {
                if (ocean[index] == 0) continue;
                visited[index] = true;
                conditioned[index] = elevations[index];
                heap.Push(elevations[index], index);
            }
            if (heap.Count == 0)
                throw new InvalidOperationException("Conditioned drainage requires a global ocean outlet.");

            while (heap.Count > 0)
            {
                heap.Pop(out ushort currentHeight, out int current);
                int x = current % columns;
                int y = current / columns;
                for (int direction = 0; direction < 8; direction++)
                {
                    int nextX = x + MacroWaterPlan.DirectionX[direction];
                    int nextY = y + MacroWaterPlan.DirectionY[direction];
                    if (nextX < 0 || nextX >= columns || nextY < 0 || nextY >= rows) continue;
                    int next = nextY * columns + nextX;
                    if (visited[next]) continue;
                    visited[next] = true;
                    ushort nextHeight = Math.Max(elevations[next], currentHeight);
                    conditioned[next] = nextHeight;
                    drainage[next] = OppositeDirection((byte)direction);
                    heap.Push(nextHeight, next);
                }
            }
            for (int index = 0; index < visited.Length; index++)
                if (!visited[index])
                    throw new InvalidOperationException("Conditioned drainage did not cover the finite world grid.");
        }

        internal static List<MacroBasinCandidate> BuildBasinCandidates(
            ushort[] elevations,
            ushort[] conditioned,
            int columns,
            int rows,
            int minimumCells)
        {
            var basins = new List<MacroBasinCandidate>();
            var visited = new bool[elevations.Length];
            var queue = new Queue<int>();
            for (int start = 0; start < elevations.Length; start++)
            {
                if (visited[start] || conditioned[start] <= elevations[start]) continue;
                visited[start] = true;
                queue.Enqueue(start);
                int count = 0;
                int representative = start;
                int maximumDepth = 0;
                int spillElevation = 0;
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    count++;
                    representative = Math.Min(representative, current);
                    maximumDepth = Math.Max(maximumDepth, conditioned[current] - elevations[current]);
                    spillElevation = Math.Max(spillElevation, conditioned[current]);
                    int x = current % columns;
                    int y = current / columns;
                    for (int offsetY = -1; offsetY <= 1; offsetY++)
                    for (int offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        if (offsetX == 0 && offsetY == 0) continue;
                        int nextX = x + offsetX;
                        int nextY = y + offsetY;
                        if (nextX < 0 || nextX >= columns || nextY < 0 || nextY >= rows) continue;
                        int next = nextY * columns + nextX;
                        if (visited[next] || conditioned[next] <= elevations[next]) continue;
                        visited[next] = true;
                        queue.Enqueue(next);
                    }
                }
                if (count >= minimumCells)
                {
                    basins.Add(new MacroBasinCandidate(
                        representative, count, (ushort)spillElevation, (ushort)maximumDepth));
                }
            }
            basins.Sort((left, right) =>
                left.RepresentativeSampleIndex.CompareTo(right.RepresentativeSampleIndex));
            return basins;
        }

        private static byte OppositeDirection(byte direction) => (byte)((direction + 4) % 8);

        private sealed class StableSampleMinHeap
        {
            private readonly List<Entry> entries;

            internal StableSampleMinHeap(int capacity)
            {
                entries = new List<Entry>(capacity);
            }

            internal int Count => entries.Count;

            internal void Push(ushort priority, int sampleIndex)
            {
                var entry = new Entry(priority, sampleIndex);
                entries.Add(entry);
                int index = entries.Count - 1;
                while (index > 0)
                {
                    int parent = (index - 1) / 2;
                    if (Compare(entries[parent], entry) <= 0) break;
                    entries[index] = entries[parent];
                    index = parent;
                }
                entries[index] = entry;
            }

            internal void Pop(out ushort priority, out int sampleIndex)
            {
                Entry root = entries[0];
                Entry last = entries[entries.Count - 1];
                entries.RemoveAt(entries.Count - 1);
                if (entries.Count > 0)
                {
                    int index = 0;
                    while (true)
                    {
                        int left = index * 2 + 1;
                        if (left >= entries.Count) break;
                        int right = left + 1;
                        int selected = right < entries.Count &&
                                       Compare(entries[right], entries[left]) < 0
                            ? right
                            : left;
                        if (Compare(last, entries[selected]) <= 0) break;
                        entries[index] = entries[selected];
                        index = selected;
                    }
                    entries[index] = last;
                }
                priority = root.Priority;
                sampleIndex = root.SampleIndex;
            }

            private static int Compare(Entry left, Entry right)
            {
                int priority = left.Priority.CompareTo(right.Priority);
                return priority != 0 ? priority : left.SampleIndex.CompareTo(right.SampleIndex);
            }

            private readonly struct Entry
            {
                internal Entry(ushort priority, int sampleIndex)
                {
                    Priority = priority;
                    SampleIndex = sampleIndex;
                }

                internal ushort Priority { get; }
                internal int SampleIndex { get; }
            }
        }
    }
}
