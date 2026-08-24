using System;
using System.Collections.Generic;
using System.Globalization;

namespace OldScars.Core.World
{
    /// <summary>
    /// Deterministic Macro World Plan V1 generator. It samples logical points
    /// directly from existing SHA-256 generation domains, then derives a
    /// spatial minimum-spanning topology. It owns no Unity or runtime state.
    /// </summary>
    public static class MacroWorldPlanGenerator
    {
        private const int CandidateCountPerSector = 24;

        public static bool TryGenerate(
            WorldGenerationContext context,
            WorldGenerationSettings settings,
            out MacroWorldPlan plan,
            out string error)
        {
            plan = null;
            error = null;
            if (context == null)
            {
                error = "Macro world generation requires a WorldGenerationContext";
                return false;
            }
            if (settings == null)
            {
                error = "Macro world generation requires WorldGenerationSettings";
                return false;
            }

            try
            {
                long minX = -(settings.ResolvedWorldWidth / 2);
                long minY = -(settings.ResolvedWorldHeight / 2);
                var bounds = new FiniteMacroWorldBounds(
                    minX,
                    minY,
                    minX + settings.ResolvedWorldWidth,
                    minY + settings.ResolvedWorldHeight);
                List<MacroSectorPlacement> placements = GeneratePlacements(context, settings, bounds);
                if (!TryBuildSpatialTopology(placements, out WorldTopology topology, out error))
                    return false;
                if (!MacroWorldPlan.TryCreate(settings, bounds, placements, topology, out plan, out error))
                    return false;
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is FormatException ||
                exception is InvalidOperationException || exception is OverflowException)
            {
                error = "Macro World Plan V1 generation failed: " + exception.Message;
                return false;
            }
        }

        private static List<MacroSectorPlacement> GeneratePlacements(
            WorldGenerationContext context,
            WorldGenerationSettings settings,
            FiniteMacroWorldBounds bounds)
        {
            var placements = new List<MacroSectorPlacement>(settings.ResolvedSectorCount);
            long minimumSpacingSquared = settings.ResolvedMinimumSectorSpacing *
                                         settings.ResolvedMinimumSectorSpacing;
            for (int sectorIndex = 0; sectorIndex < settings.ResolvedSectorCount; sectorIndex++)
            {
                string scope = settings.DeterministicKey + "_sector_" +
                               sectorIndex.ToString("D6", CultureInfo.InvariantCulture);
                SectorId sectorId = SectorId.FromDeterministicDomain(
                    WorldDeterminism.DeriveDomainKey(context, scope, "identity"));

                bool found = false;
                MacroPoint2D selected = default;
                long selectedScore = long.MinValue;
                for (int candidateIndex = 0; candidateIndex < CandidateCountPerSector; candidateIndex++)
                {
                    DeterministicDomainKey domain = WorldDeterminism.DeriveDomainKey(
                        context,
                        scope,
                        "placement_" + candidateIndex.ToString("D2", CultureInfo.InvariantCulture));
                    MacroPoint2D candidate = SamplePoint(domain, bounds, settings.ResolvedMinimumSectorSpacing / 2);
                    long score;
                    if (placements.Count == 0)
                    {
                        long doubledCenterX = bounds.MinX + bounds.MaxXExclusive;
                        long doubledCenterY = bounds.MinY + bounds.MaxYExclusive;
                        long dx = candidate.X * 2 - doubledCenterX;
                        long dy = candidate.Y * 2 - doubledCenterY;
                        score = -(dx * dx + dy * dy);
                    }
                    else
                    {
                        score = long.MaxValue;
                        for (int existingIndex = 0; existingIndex < placements.Count; existingIndex++)
                        {
                            long distance = MacroWorldPlan.DistanceSquared(
                                candidate, placements[existingIndex].Position);
                            if (distance < score)
                                score = distance;
                        }
                        if (score < minimumSpacingSquared)
                            continue;
                    }

                    if (!found || score > selectedScore ||
                        score == selectedScore && ComparePoints(candidate, selected) < 0)
                    {
                        selected = candidate;
                        selectedScore = score;
                        found = true;
                    }
                }

                if (!found)
                {
                    throw new InvalidOperationException(
                        $"Could not place sector {sectorIndex} while preserving resolved minimum spacing " +
                        settings.ResolvedMinimumSectorSpacing + ".");
                }
                placements.Add(new MacroSectorPlacement(sectorId, selected));
            }
            return placements;
        }

        private static MacroPoint2D SamplePoint(
            DeterministicDomainKey domain,
            FiniteMacroWorldBounds bounds,
            long edgeMargin)
        {
            ulong xBits = ParseUInt64(domain.Canonical, 0);
            ulong yBits = ParseUInt64(domain.Canonical, 16);
            long usableWidth = bounds.Width - edgeMargin * 2;
            long usableHeight = bounds.Height - edgeMargin * 2;
            if (usableWidth < 1 || usableHeight < 1)
                throw new InvalidOperationException("Resolved world bounds leave no room for macro placement.");
            long x = bounds.MinX + edgeMargin + (long)(xBits % (ulong)usableWidth);
            long y = bounds.MinY + edgeMargin + (long)(yBits % (ulong)usableHeight);
            return new MacroPoint2D(x, y);
        }

        private static bool TryBuildSpatialTopology(
            IList<MacroSectorPlacement> placementInputs,
            out WorldTopology topology,
            out string error)
        {
            topology = null;
            error = null;
            var placements = new List<MacroSectorPlacement>(placementInputs);
            placements.Sort((left, right) => left.SectorId.CompareTo(right.SectorId));
            var sectors = new List<SectorId>(placements.Count);
            for (int index = 0; index < placements.Count; index++)
                sectors.Add(placements[index].SectorId);

            var connections = new List<SectorConnection>(Math.Max(0, placements.Count - 1));
            var visited = new bool[placements.Count];
            visited[0] = true;
            int visitedCount = 1;
            while (visitedCount < placements.Count)
            {
                int selectedFrom = -1;
                int selectedTo = -1;
                long selectedDistance = long.MaxValue;
                string selectedKey = null;
                for (int from = 0; from < placements.Count; from++)
                {
                    if (!visited[from])
                        continue;
                    for (int to = 0; to < placements.Count; to++)
                    {
                        if (visited[to])
                            continue;
                        long distance = MacroWorldPlan.DistanceSquared(
                            placements[from].Position, placements[to].Position);
                        string key = BuildConnectionKey(
                            placements[from].SectorId, placements[to].SectorId);
                        if (distance < selectedDistance ||
                            distance == selectedDistance && string.CompareOrdinal(key, selectedKey) < 0)
                        {
                            selectedFrom = from;
                            selectedTo = to;
                            selectedDistance = distance;
                            selectedKey = key;
                        }
                    }
                }

                if (selectedFrom < 0 || selectedTo < 0)
                {
                    error = "Spatial topology derivation could not connect all macro sectors";
                    return false;
                }
                connections.Add(new SectorConnection(
                    selectedKey,
                    placements[selectedFrom].SectorId,
                    placements[selectedTo].SectorId));
                visited[selectedTo] = true;
                visitedCount++;
            }

            if (!WorldTopology.TryCreate(
                    sectors, connections, out topology, out WorldTopologyValidationResult validation))
            {
                error = "Generated spatial topology failed validation: " + validation.Description;
                return false;
            }
            return true;
        }

        private static string BuildConnectionKey(SectorId first, SectorId second)
        {
            string firstBody = first.Canonical.Substring("sector_".Length);
            string secondBody = second.Canonical.Substring("sector_".Length);
            if (string.CompareOrdinal(firstBody, secondBody) > 0)
            {
                string swap = firstBody;
                firstBody = secondBody;
                secondBody = swap;
            }
            return "regional_" + firstBody + "_" + secondBody;
        }

        private static ulong ParseUInt64(string lowercaseHex, int startIndex)
        {
            ulong value = 0;
            for (int index = startIndex; index < startIndex + 16; index++)
            {
                char character = lowercaseHex[index];
                uint digit = character <= '9'
                    ? (uint)(character - '0')
                    : (uint)(character - 'a' + 10);
                value = (value << 4) | digit;
            }
            return value;
        }

        private static int ComparePoints(MacroPoint2D left, MacroPoint2D right)
        {
            int x = left.X.CompareTo(right.X);
            return x != 0 ? x : left.Y.CompareTo(right.Y);
        }
    }
}
