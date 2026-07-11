using System;
using System.Collections.Generic;

namespace OldScars.Core.Items
{
    public sealed class GridInventoryLayout
    {
        private readonly Dictionary<string, GridPlacement> placements = new Dictionary<string, GridPlacement>();

        public int Width { get; }
        public int Height { get; }
        public int Version { get; private set; }
        public IReadOnlyCollection<GridPlacement> Placements => placements.Values;

        public GridInventoryLayout(int width, int height)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height));

            Width = width;
            Height = height;
        }

        public bool TryGetPlacement(string instanceId, out GridPlacement placement)
        {
            placement = null;
            return !string.IsNullOrWhiteSpace(instanceId) && placements.TryGetValue(instanceId, out placement);
        }

        internal bool TryFindFirstFit(GridFootprint footprint, IReadOnlyList<ReservedRect> reservations, out ReservedRect result)
        {
            result = default;
            if (footprint == null)
                return false;

            if (TryFindFirstFit(footprint, false, reservations, out result))
                return true;

            return footprint.Width != footprint.Height &&
                   TryFindFirstFit(footprint, true, reservations, out result);
        }

        internal bool TryAddPlacement(string instanceId, ReservedRect rect)
        {
            if (string.IsNullOrWhiteSpace(instanceId) || placements.ContainsKey(instanceId) || !CanPlace(rect, null, null))
                return false;

            placements.Add(instanceId, new GridPlacement(
                instanceId,
                rect.X,
                rect.Y,
                rect.IsRotated,
                rect.Width,
                rect.Height));
            Version++;
            return true;
        }

        internal bool TryCreateMoveCandidate(
            string instanceId,
            GridFootprint footprint,
            int x,
            int y,
            bool isRotated,
            out GridPlacement candidate)
        {
            candidate = null;
            if (string.IsNullOrWhiteSpace(instanceId) || footprint == null ||
                !placements.TryGetValue(instanceId, out GridPlacement previous))
            {
                return false;
            }

            bool effectiveRotated = footprint.Width == footprint.Height
                ? previous.IsRotated
                : isRotated;

            var rect = new ReservedRect(
                x,
                y,
                footprint.GetWidth(effectiveRotated),
                footprint.GetHeight(effectiveRotated),
                effectiveRotated);
            if (!CanPlace(rect, null, instanceId))
                return false;

            candidate = new GridPlacement(instanceId, rect.X, rect.Y, rect.IsRotated, rect.Width, rect.Height);
            return true;
        }

        internal bool TryMovePlacement(GridPlacement candidate)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.InstanceId) ||
                !placements.TryGetValue(candidate.InstanceId, out GridPlacement previous))
            {
                return false;
            }

            var rect = new ReservedRect(
                candidate.X,
                candidate.Y,
                candidate.EffectiveWidth,
                candidate.EffectiveHeight,
                candidate.IsRotated);
            if (!CanPlace(rect, null, candidate.InstanceId))
                return false;

            if (previous.X == candidate.X && previous.Y == candidate.Y &&
                previous.IsRotated == candidate.IsRotated &&
                previous.EffectiveWidth == candidate.EffectiveWidth &&
                previous.EffectiveHeight == candidate.EffectiveHeight)
            {
                return true;
            }

            placements[candidate.InstanceId] = candidate;
            Version++;
            return true;
        }

        internal bool RemovePlacement(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId) || !placements.Remove(instanceId))
                return false;

            Version++;
            return true;
        }

        internal StateSnapshot CaptureState()
        {
            var items = new GridPlacement[placements.Count];
            int index = 0;
            foreach (GridPlacement placement in placements.Values)
                items[index++] = placement;

            return new StateSnapshot(items);
        }

        internal void RestoreState(StateSnapshot snapshot)
        {
            placements.Clear();
            GridPlacement[] items = snapshot.Placements;
            if (items != null)
            {
                for (int index = 0; index < items.Length; index++)
                {
                    GridPlacement placement = items[index];
                    if (placement != null && !string.IsNullOrWhiteSpace(placement.InstanceId))
                        placements[placement.InstanceId] = placement;
                }
            }

            Version++;
        }

        internal bool ValidateNoOverlapOrBounds()
        {
            var seen = new List<GridPlacement>();
            foreach (GridPlacement placement in placements.Values)
            {
                var rect = new ReservedRect(
                    placement.X,
                    placement.Y,
                    placement.EffectiveWidth,
                    placement.EffectiveHeight,
                    placement.IsRotated);
                if (!IsWithinBounds(rect))
                    return false;

                for (int index = 0; index < seen.Count; index++)
                {
                    GridPlacement other = seen[index];
                    if (Overlaps(rect, new ReservedRect(other.X, other.Y, other.EffectiveWidth, other.EffectiveHeight, other.IsRotated)))
                        return false;
                }

                seen.Add(placement);
            }

            return true;
        }

        private bool TryFindFirstFit(GridFootprint footprint, bool rotated, IReadOnlyList<ReservedRect> reservations, out ReservedRect result)
        {
            int width = footprint.GetWidth(rotated);
            int height = footprint.GetHeight(rotated);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var candidate = new ReservedRect(x, y, width, height, rotated);
                    if (CanPlace(candidate, reservations, null))
                    {
                        result = candidate;
                        return true;
                    }
                }
            }

            result = default;
            return false;
        }

        private bool CanPlace(
            ReservedRect candidate,
            IReadOnlyList<ReservedRect> reservations,
            string ignoredInstanceId)
        {
            if (!IsWithinBounds(candidate))
                return false;

            foreach (GridPlacement placement in placements.Values)
            {
                if (!string.IsNullOrWhiteSpace(ignoredInstanceId) && placement.InstanceId == ignoredInstanceId)
                    continue;

                var occupied = new ReservedRect(
                    placement.X,
                    placement.Y,
                    placement.EffectiveWidth,
                    placement.EffectiveHeight,
                    placement.IsRotated);
                if (Overlaps(candidate, occupied))
                    return false;
            }

            if (reservations != null)
            {
                for (int index = 0; index < reservations.Count; index++)
                {
                    if (Overlaps(candidate, reservations[index]))
                        return false;
                }
            }

            return true;
        }

        private bool IsWithinBounds(ReservedRect rect)
        {
            return rect.X >= 0 && rect.Y >= 0 && rect.Width > 0 && rect.Height > 0 &&
                   rect.X + rect.Width <= Width && rect.Y + rect.Height <= Height;
        }

        private static bool Overlaps(ReservedRect left, ReservedRect right)
        {
            return left.X < right.X + right.Width && left.X + left.Width > right.X &&
                   left.Y < right.Y + right.Height && left.Y + left.Height > right.Y;
        }

        internal readonly struct ReservedRect
        {
            public ReservedRect(int x, int y, int width, int height, bool isRotated)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
                IsRotated = isRotated;
            }

            public readonly int X;
            public readonly int Y;
            public readonly int Width;
            public readonly int Height;
            public readonly bool IsRotated;
        }

        internal readonly struct StateSnapshot
        {
            public StateSnapshot(GridPlacement[] placements)
            {
                Placements = placements;
            }

            public readonly GridPlacement[] Placements;
        }
    }
}
