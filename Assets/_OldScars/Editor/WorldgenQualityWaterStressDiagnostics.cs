using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OldScars.Core.World;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace OldScars.EditorTools
{
    /// <summary>
    /// Deliberately larger one-time stress corpus. Routine regression stays in
    /// WorldgenQualityWaterDiagnostics; this reports rejection distribution and
    /// conservative extrema without becoming a production tuning authority.
    /// </summary>
    public static class WorldgenQualityWaterStressDiagnostics
    {
        private const int SeedsPerSize = 32;

        public static void Run()
        {
            var failures = new Dictionary<string, int>(StringComparer.Ordinal);
            var summaries = new List<CoverageSummary>();
            foreach (WorldSizePreset size in Enum.GetValues(typeof(WorldSizePreset)))
            foreach (LandCoveragePreset coverage in Enum.GetValues(typeof(LandCoveragePreset)))
                summaries.Add(new CoverageSummary(size, coverage));

            var timer = Stopwatch.StartNew();
            foreach (WorldSizePreset size in Enum.GetValues(typeof(WorldSizePreset)))
            {
                for (int seedIndex = 0; seedIndex < SeedsPerSize; seedIndex++)
                {
                    long seed = unchecked(
                        (long)0xd384f17a20c96b5eUL + (long)size * 1000003L + seedIndex * 104729L);
                    var context = new WorldGenerationContext(
                        new WorldSeed(seed),
                        GeneratorVersion.Parse(WorldSessionBootstrap.CurrentGeneratorVersion));
                    if (!MacroWorldPlanGenerator.TryGenerate(
                            context, WorldGenerationSettings.ResolvePreset(size),
                            out MacroWorldPlan plan, out string planError))
                    {
                        Count(failures, "plan: " + planError);
                        continue;
                    }
                    if (!MacroGeographyGenerator.TryGenerate(
                            context, plan, out MacroGeographyPlan geography, out string geographyError))
                    {
                        Count(failures, "geography: " + geographyError);
                        continue;
                    }
                    string planHash = plan.CanonicalHash;
                    string geographyHash = geography.CanonicalHash;
                    foreach (LandCoveragePreset coverage in Enum.GetValues(typeof(LandCoveragePreset)))
                    {
                        CoverageSummary summary = Find(summaries, size, coverage);
                        var passTimer = Stopwatch.StartNew();
                        if (!MacroWaterGenerator.TryGenerate(
                                plan, geography, coverage,
                                out MacroWaterPlan water, out string waterError))
                        {
                            Count(failures, "water " + size + "/" + coverage + ": " + waterError);
                            summary.Rejected++;
                            continue;
                        }
                        if (!WorldGameplayQualityAnalyzer.TryAnalyze(
                                plan, geography, water,
                                out WorldGameplayQualityAnalysis quality, out string qualityError))
                        {
                            Count(failures, "quality " + size + "/" + coverage + ": " + qualityError);
                            summary.Rejected++;
                            continue;
                        }
                        passTimer.Stop();
                        summary.Record(water, quality, passTimer.ElapsedMilliseconds);
                        if (plan.CanonicalHash != planHash || geography.CanonicalHash != geographyHash)
                            Count(failures, "Land Coverage mutated upstream evidence");
                        if (!quality.MeetsHardRequirements)
                        {
                            summary.Rejected++;
                            for (int index = 0; index < quality.HardFailures.Count; index++)
                                Count(failures, size + "/" + coverage + ": " + quality.HardFailures[index]);
                            continue;
                        }
                        if (!WorldStarterSectorSelector.TrySelect(quality, out _, out string starterError))
                        {
                            summary.Rejected++;
                            Count(failures, size + "/" + coverage + ": " + starterError);
                        }
                    }
                }
            }
            timer.Stop();

            var output = new StringBuilder();
            output.AppendLine("WORLDGEN GAMEPLAY QUALITY + MACRO WATER STRESS CORPUS");
            output.AppendLine("SeedsPerSize=" + SeedsPerSize.ToString(CultureInfo.InvariantCulture) +
                              " TotalWorldInputs=" + (SeedsPerSize * 4).ToString(CultureInfo.InvariantCulture) +
                              " TotalWaterVariants=" + (SeedsPerSize * 4 * 3).ToString(CultureInfo.InvariantCulture) +
                              " ElapsedMs=" + timer.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < summaries.Count; index++)
                output.AppendLine(summaries[index].Describe());
            output.AppendLine("FailureDistribution:");
            if (failures.Count == 0)
            {
                output.AppendLine("- none (0 hard rejections / 0 generation failures)");
                Debug.Log(output.ToString());
                return;
            }
            foreach (KeyValuePair<string, int> failure in failures)
                output.AppendLine("- " + failure.Value + " x " + failure.Key);
            Debug.LogError(output.ToString());
            throw new InvalidOperationException("Stress corpus found hard rejection/generation failures.");
        }

        private static CoverageSummary Find(
            IList<CoverageSummary> summaries,
            WorldSizePreset size,
            LandCoveragePreset coverage)
        {
            for (int index = 0; index < summaries.Count; index++)
                if (summaries[index].Size == size && summaries[index].Coverage == coverage)
                    return summaries[index];
            throw new InvalidOperationException("Stress summary bucket is missing.");
        }

        private static void Count(IDictionary<string, int> failures, string failure)
        {
            failures.TryGetValue(failure, out int count);
            failures[failure] = count + 1;
        }

        private sealed class CoverageSummary
        {
            internal CoverageSummary(WorldSizePreset size, LandCoveragePreset coverage)
            {
                Size = size;
                Coverage = coverage;
            }

            internal WorldSizePreset Size { get; }
            internal LandCoveragePreset Coverage { get; }
            internal int Generated { get; private set; }
            internal int Rejected { get; set; }
            private int minLand = int.MaxValue;
            private int maxLand;
            private int minCoast = int.MaxValue;
            private int minLandCorridor = int.MaxValue;
            private int minStarters = int.MaxValue;
            private int maxBasins;
            private long elapsed;

            internal void Record(
                MacroWaterPlan water,
                WorldGameplayQualityAnalysis quality,
                long elapsedMilliseconds)
            {
                Generated++;
                minLand = Math.Min(minLand, water.LandRatioQ16);
                maxLand = Math.Max(maxLand, water.LandRatioQ16);
                minCoast = Math.Min(minCoast,
                    (int)((long)water.CoastlineSampleCount * 65535 / water.SampleCount));
                minLandCorridor = Math.Min(minLandCorridor, quality.LargestLandTravelRegionQ16);
                minStarters = Math.Min(minStarters, quality.SuitableStarterCandidateCount);
                maxBasins = Math.Max(maxBasins, water.BasinCandidates.Count);
                elapsed += elapsedMilliseconds;
            }

            internal string Describe()
            {
                return Size + "/" + Coverage +
                       " generated=" + Generated +
                       " rejected=" + Rejected +
                       " landQ16=" + Safe(minLand) + ".." + maxLand +
                       " minCoastQ16=" + Safe(minCoast) +
                       " minLandCorridorQ16=" + Safe(minLandCorridor) +
                       " minStarterCandidates=" + Safe(minStarters) +
                       " maxBasins=" + maxBasins +
                       " avgWaterQualityMs=" + (Generated == 0 ? 0 : elapsed / Generated);
            }

            private static int Safe(int value) => value == int.MaxValue ? 0 : value;
        }
    }
}
