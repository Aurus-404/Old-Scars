using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OldScars.Core.Data.Loading;
using OldScars.Core.Data.Validation;
using OldScars.Core.Persistence;
using OldScars.Core.World;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace OldScars.EditorTools
{
    public static class MacroClimateDiagnostics
    {
        private const long GoldenSeed = 8675309123456789L;
        private const string SyntheticPipelineVersion = "world_pipeline_v99_probe";
        private const string SyntheticClimateContract = "macro_climate_v2_probe";
        private const string GoldenPlanHash =
            "3f300ba2129962493d2ab8f2ad6ec0863e96aa0ceeb400f9899f91889a34e91a";
        private const string GoldenGeographyHash =
            "c2d412fcdcb1b0e1b41f4fdbda2df01258758e6db9c6b93aac59b446be7dbd3e";
        private const string GoldenWaterHash =
            "ec29f501e4f36ae3b2313d3da6089f2fe6e92b052f18079c649e21ce8faabfc0";
        private const string GoldenHumanHash =
            "a786f018ce3bdea44aeb066c80e38cb1f5dc8e114c65bd7eb352489628245ba6";
        private const string GoldenClimateHash =
            "a4b7869a7d8deab093eb9b9c5f7a2da118156f22c61ac466fbd0a9e64958eec1";
        private const int RoutineSeedsPerCombination = 3;
        private const int StressSeedsPerCombination = 8;
        private const string PreviewDirectoryEnvironment = "OLD_SCARS_MACRO_CLIMATE_PREVIEW_DIR";

        public static void Run()
        {
            var failures = new List<string>();
            var softFindings = new List<string>();
            var measurements = new List<string>();
            string persistenceRoot = Path.Combine(
                Path.GetTempPath(), "OldScars_MacroClimate_" + Guid.NewGuid().ToString("N"));
            string previewPath = null;
            try
            {
                ValidateDeterminismIsolationAndGoldens(failures);
                ValidateRoutineCorpus(failures, softFindings);
                LoadedContentSet content = LoadValidatedCore(failures);
                ValidatePersistence(persistenceRoot, content, failures);
                MeasurePresets(content, measurements, failures);
                previewPath = ExportGoldenPreview();
            }
            catch (Exception exception)
            {
                failures.Add("Diagnostic threw " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                WorldSessionService.Close();
                if (Directory.Exists(persistenceRoot))
                    Directory.Delete(persistenceRoot, true);
            }

            Complete(
                "Macro Climate Baseline V1 Diagnostics",
                failures,
                "- contract: " + MacroClimateGenerator.DeterministicGenerationContract,
                "- upstream goldens: " + GoldenPlanHash + " / " + GoldenGeographyHash +
                " / " + GoldenWaterHash + " / " + GoldenHumanHash,
                "- Climate golden: " + GoldenClimateHash,
                "- +Macro Y is North; north colder / south warmer with regional anomaly and elevation cooling",
                "- moisture combines regional tendency, gradual ocean influence, and bounded orographic response",
                "- routine corpus: " + RoutineSeedsPerCombination + " seeds x 4 sizes x 3 coverages",
                "- soft findings: " + softFindings.Count.ToString(CultureInfo.InvariantCulture),
                "- timings / schema-6 payloads: " + string.Join("; ", measurements),
                "- preview: " + (previewPath ?? "<NOT GENERATED>"));
        }

        public static void RunStress()
        {
            var failures = new List<string>();
            var softFindings = new List<string>();
            int generated = 0;
            var stopwatch = Stopwatch.StartNew();
            foreach (WorldSizePreset size in Enum.GetValues(typeof(WorldSizePreset)))
            foreach (LandCoveragePreset coverage in Enum.GetValues(typeof(LandCoveragePreset)))
            for (int index = 0; index < StressSeedsPerCombination; index++)
            {
                long seed = unchecked((long)0x47ca31d69b54fe21UL +
                                      (long)size * 1000003L +
                                      (long)coverage * 65537L + index * 104729L);
                string label = "stress " + size + "/" + coverage + "/" + index;
                if (!TryGenerate(
                        seed, size, coverage, WorldSessionBootstrap.CurrentGeneratorVersion,
                        MacroClimateGenerator.DeterministicGenerationContract,
                        out GeneratedWorld world, out string error))
                {
                    failures.Add(label + " failed: " + error);
                    continue;
                }
                generated++;
                ValidateClimateQuality(world, label, failures, softFindings);
            }
            stopwatch.Stop();
            Complete(
                "Macro Climate Stress Corpus",
                failures,
                "- attempted: " + (StressSeedsPerCombination * 12),
                "- generated: " + generated,
                "- hard failures/rejections: " + failures.Count,
                "- soft quality findings: " + softFindings.Count,
                "- elapsed ms: " + stopwatch.ElapsedMilliseconds);
        }

        public static void ExportRepresentativePreviews()
        {
            string directory = ResolvePreviewDirectory();
            Directory.CreateDirectory(directory);
            var failures = new List<string>();
            foreach (WorldSizePreset size in Enum.GetValues(typeof(WorldSizePreset)))
            {
                string outputPath = Path.Combine(
                    directory, "OldScars_MacroClimate_" + size + "_High_" +
                               GoldenSeed.ToString(CultureInfo.InvariantCulture) + ".png");
                if (!TryGenerate(
                        GoldenSeed, size, LandCoveragePreset.High,
                        WorldSessionBootstrap.CurrentGeneratorVersion,
                        MacroClimateGenerator.DeterministicGenerationContract,
                        out GeneratedWorld world, out string error))
                {
                    failures.Add(size + " preview generation failed: " + error);
                    continue;
                }
                MacroGeographyPreviewExporter.Export(
                    world.Plan, world.Geography, world.Water, world.Climate,
                    world.Quality, world.Human, world.Starter,
                    outputPath, 320, 320, true);
            }
            Complete(
                "Macro Climate Representative Preview Export",
                failures,
                "- directory: " + directory,
                "- presets: Small/Medium/Large/Huge; coverage High; seed " + GoldenSeed);
        }

        private static void ValidateDeterminismIsolationAndGoldens(List<string> failures)
        {
            Check(TryGenerate(
                    GoldenSeed, WorldSizePreset.Large, LandCoveragePreset.High,
                    WorldSessionBootstrap.CurrentGeneratorVersion,
                    MacroClimateGenerator.DeterministicGenerationContract,
                    out GeneratedWorld first, out string firstError),
                "Golden Climate generation failed: " + Safe(firstError), failures);
            Check(TryGenerate(
                    GoldenSeed, WorldSizePreset.Large, LandCoveragePreset.High,
                    WorldSessionBootstrap.CurrentGeneratorVersion,
                    MacroClimateGenerator.DeterministicGenerationContract,
                    out GeneratedWorld repeated, out string repeatedError),
                "Repeated Climate generation failed: " + Safe(repeatedError), failures);
            if (first == null || repeated == null)
                return;

            Check(first.Climate.CanonicalHash == repeated.Climate.CanonicalHash,
                "Same seed/settings did not reproduce exact Climate evidence.", failures);
            WorldId firstWorld = WorldId.CreateNew();
            WorldId secondWorld = WorldId.CreateNew();
            Check(firstWorld != secondWorld && first.Climate.CanonicalHash == repeated.Climate.CanonicalHash,
                "Different WorldIds changed Climate evidence.", failures);

            Check(TryGenerate(
                    GoldenSeed + 1, WorldSizePreset.Large, LandCoveragePreset.High,
                    WorldSessionBootstrap.CurrentGeneratorVersion,
                    MacroClimateGenerator.DeterministicGenerationContract,
                    out GeneratedWorld differentSeed, out string differentSeedError) &&
                  differentSeed.Climate.CanonicalHash != first.Climate.CanonicalHash,
                "Different WorldSeed did not change Climate truth: " + Safe(differentSeedError), failures);

            Check(TryGenerate(
                    GoldenSeed, WorldSizePreset.Large, LandCoveragePreset.High,
                    SyntheticPipelineVersion,
                    MacroClimateGenerator.DeterministicGenerationContract,
                    out GeneratedWorld futurePipeline, out string futureError),
                "Synthetic pipeline probe failed: " + Safe(futureError), failures);
            if (futurePipeline != null)
            {
                Check(AllHashesEqual(first, futurePipeline),
                    "Changing only overall pipeline metadata changed pass-local truth.", failures);
            }

            Check(TryGenerate(
                    GoldenSeed, WorldSizePreset.Large, LandCoveragePreset.High,
                    WorldSessionBootstrap.CurrentGeneratorVersion,
                    SyntheticClimateContract,
                    out GeneratedWorld changedClimate, out string changedClimateError),
                "Synthetic Climate contract probe failed: " + Safe(changedClimateError), failures);
            if (changedClimate != null)
            {
                Check(changedClimate.Plan.CanonicalHash == first.Plan.CanonicalHash &&
                      changedClimate.Geography.CanonicalHash == first.Geography.CanonicalHash &&
                      changedClimate.Water.CanonicalHash == first.Water.CanonicalHash &&
                      changedClimate.Human.CanonicalHash == first.Human.CanonicalHash &&
                      changedClimate.Starter == first.Starter &&
                      changedClimate.Climate.CanonicalHash != first.Climate.CanonicalHash,
                    "Changing only Climate's pass contract perturbed upstream/Human/starter or failed to change Climate.",
                    failures);
            }

            Check(first.Plan.CanonicalHash == GoldenPlanHash,
                "MacroWorldPlan golden drifted: " + first.Plan.CanonicalHash, failures);
            Check(first.Geography.CanonicalHash == GoldenGeographyHash,
                "MacroGeography golden drifted: " + first.Geography.CanonicalHash, failures);
            Check(first.Water.CanonicalHash == GoldenWaterHash,
                "MacroWater golden drifted: " + first.Water.CanonicalHash, failures);
            Check(first.Human.CanonicalHash == GoldenHumanHash,
                "MacroHumanGeography golden drifted: " + first.Human.CanonicalHash, failures);
            Check(first.Climate.CanonicalHash == GoldenClimateHash,
                "MacroClimate golden is " + first.Climate.CanonicalHash +
                " (expected " + GoldenClimateHash + ").", failures);
            ValidateComponentCausality(first, failures);

            Check(TryGenerate(
                    GoldenSeed, WorldSizePreset.Large, LandCoveragePreset.Low,
                    WorldSessionBootstrap.CurrentGeneratorVersion,
                    MacroClimateGenerator.DeterministicGenerationContract,
                    out GeneratedWorld lowCoverage, out string lowCoverageError),
                "Low LandCoverage probe failed: " + Safe(lowCoverageError), failures);
            if (lowCoverage != null)
            {
                bool thermalEqual = true;
                bool moistureDiffers = false;
                for (int y = 0; y < first.Climate.SampleRows; y++)
                for (int x = 0; x < first.Climate.SampleColumns; x++)
                {
                    MacroClimateSample high = first.Climate.SampleAt(x, y);
                    MacroClimateSample low = lowCoverage.Climate.SampleAt(x, y);
                    thermalEqual &= high.ThermalIndex == low.ThermalIndex;
                    moistureDiffers |= high.MoistureIndex != low.MoistureIndex;
                }
                Check(first.Plan.CanonicalHash == lowCoverage.Plan.CanonicalHash &&
                      first.Geography.CanonicalHash == lowCoverage.Geography.CanonicalHash &&
                      first.Water.CanonicalHash != lowCoverage.Water.CanonicalHash &&
                      thermalEqual && moistureDiffers &&
                      first.Climate.CanonicalHash != lowCoverage.Climate.CanonicalHash,
                    "LandCoverage dependency must change Water/Climate moisture without changing Plan/Geography/Thermal.",
                    failures);
            }
        }

        private static void ValidateComponentCausality(
            GeneratedWorld world,
            List<string> failures)
        {
            MacroClimateGenerationSettings source = world.Climate.GenerationSettings;
            MacroClimatePlan noCoolingClimate = null;
            MacroClimatePlan noOceanClimate = null;
            MacroClimatePlan noOrographicClimate = null;
            string noCoolingError = null;
            string noOceanError = null;
            string noOroError = null;
            bool noCoolingSettingsBuilt = TryCreateSettingsVariant(
                source, 0, source.OceanInfluenceMaximumQ16,
                source.WindwardMaximumBoostQ16, source.LeewardMaximumReductionQ16,
                out MacroClimateGenerationSettings noCooling, out string noCoolingSettingsError);
            bool noCoolingBuilt = noCoolingSettingsBuilt &&
                MacroClimateGenerator.TryGenerateForResolvedSettingsDiagnostics(
                    world.Context, world.Geography, world.Water, noCooling,
                    out noCoolingClimate, out noCoolingError);
            Check(noCoolingBuilt,
                "Elevation-cooling causal probe failed: " +
                Safe(noCoolingSettingsError) + " / " + Safe(noCoolingError), failures);
            bool noOceanSettingsBuilt = TryCreateSettingsVariant(
                source, source.ElevationCoolingQ16, 0,
                source.WindwardMaximumBoostQ16, source.LeewardMaximumReductionQ16,
                out MacroClimateGenerationSettings noOcean, out string noOceanSettingsError);
            bool noOceanBuilt = noOceanSettingsBuilt &&
                MacroClimateGenerator.TryGenerateForResolvedSettingsDiagnostics(
                    world.Context, world.Geography, world.Water, noOcean,
                    out noOceanClimate, out noOceanError);
            Check(noOceanBuilt,
                "Ocean-influence causal probe failed: " +
                Safe(noOceanSettingsError) + " / " + Safe(noOceanError), failures);
            bool noOroSettingsBuilt = TryCreateSettingsVariant(
                source, source.ElevationCoolingQ16, source.OceanInfluenceMaximumQ16,
                0, 0,
                out MacroClimateGenerationSettings noOrographic, out string noOroSettingsError);
            bool noOroBuilt = noOroSettingsBuilt &&
                MacroClimateGenerator.TryGenerateForResolvedSettingsDiagnostics(
                    world.Context, world.Geography, world.Water, noOrographic,
                    out noOrographicClimate, out noOroError);
            Check(noOroBuilt,
                "Orographic causal probe failed: " +
                Safe(noOroSettingsError) + " / " + Safe(noOroError), failures);

            if (noCoolingClimate == null || noOceanClimate == null || noOrographicClimate == null)
                return;

            int coolingCells = 0;
            int maximumCooling = 0;
            int oceanInfluencedCells = 0;
            int deepInlandOceanInfluence = 0;
            int windwardCells = 0;
            int leewardCells = 0;
            int maximumWindward = 0;
            int maximumLeeward = 0;
            int[] oceanDistance = BuildOceanDistanceField(world.Water);
            for (int y = 0; y < world.Climate.SampleRows; y++)
            for (int x = 0; x < world.Climate.SampleColumns; x++)
            {
                int index = y * world.Climate.SampleColumns + x;
                MacroClimateSample committed = world.Climate.SampleAt(x, y);
                int cooling = noCoolingClimate.SampleAt(x, y).ThermalIndex -
                              committed.ThermalIndex;
                if (cooling > 0) coolingCells++;
                maximumCooling = Math.Max(maximumCooling, cooling);

                int ocean = committed.MoistureIndex - noOceanClimate.SampleAt(x, y).MoistureIndex;
                if (ocean > 0) oceanInfluencedCells++;
                if (oceanDistance[index] >= source.OceanInfluenceDistanceCells && ocean != 0)
                    deepInlandOceanInfluence++;

                int orographic = committed.MoistureIndex -
                                  noOrographicClimate.SampleAt(x, y).MoistureIndex;
                if (orographic > 0)
                {
                    windwardCells++;
                    maximumWindward = Math.Max(maximumWindward, orographic);
                }
                else if (orographic < 0)
                {
                    leewardCells++;
                    maximumLeeward = Math.Max(maximumLeeward, -orographic);
                }
            }

            Check(coolingCells > world.Climate.SampleCount * 9 / 10 && maximumCooling > 5000,
                "Elevation cooling did not causally lower most samples or meaningful high terrain; cells=" +
                coolingCells + ", max=" + maximumCooling, failures);
            Check(oceanInfluencedCells > world.Water.OceanSampleCount &&
                  deepInlandOceanInfluence == 0,
                "Ocean influence must be gradual/local and absent beyond its committed distance; influenced=" +
                oceanInfluencedCells + ", deepInland=" + deepInlandOceanInfluence, failures);
            Check(windwardCells > 0 && leewardCells > 0 &&
                  maximumWindward <= source.WindwardMaximumBoostQ16 &&
                  maximumLeeward <= source.LeewardMaximumReductionQ16,
                "Bounded orographic generation must causally produce both enhancement and rain shadow; " +
                "cells=" + windwardCells + "/" + leewardCells +
                ", max=" + maximumWindward + "/" + maximumLeeward, failures);
        }

        private static bool TryCreateSettingsVariant(
            MacroClimateGenerationSettings source,
            int elevationCoolingQ16,
            int oceanInfluenceMaximumQ16,
            int windwardMaximumBoostQ16,
            int leewardMaximumReductionQ16,
            out MacroClimateGenerationSettings settings,
            out string error)
        {
            return MacroClimateGenerationSettings.TryCreateResolved(
                source.GenerationContract,
                source.SampleColumns,
                source.SampleRows,
                source.ThermalRegionalFrequencyQ16,
                source.MoistureRegionalFrequencyQ16,
                source.SouthThermalBaselineQ16,
                source.NorthThermalBaselineQ16,
                source.ThermalRegionalAmplitudeQ16,
                elevationCoolingQ16,
                source.MoistureBaselineQ16,
                source.MoistureRegionalAmplitudeQ16,
                oceanInfluenceMaximumQ16,
                source.OceanInfluenceDistanceCells,
                source.OrographicLookbackCells,
                source.OrographicRiseThresholdQ16,
                windwardMaximumBoostQ16,
                leewardMaximumReductionQ16,
                source.OrographicResponseDivisor,
                source.PrevailingMoistureDirection,
                out settings,
                out error);
        }

        private static void ValidateRoutineCorpus(
            List<string> failures,
            List<string> softFindings)
        {
            int smallTransitions = -1;
            int hugeTransitions = -1;
            foreach (WorldSizePreset size in Enum.GetValues(typeof(WorldSizePreset)))
            foreach (LandCoveragePreset coverage in Enum.GetValues(typeof(LandCoveragePreset)))
            for (int index = 0; index < RoutineSeedsPerCombination; index++)
            {
                long seed = unchecked((long)0x61a23e9b05cd7841UL +
                                      (long)size * 1000003L +
                                      (long)coverage * 65537L + index * 7919L);
                string label = "routine " + size + "/" + coverage + "/" + index;
                if (!TryGenerate(
                        seed, size, coverage,
                        WorldSessionBootstrap.CurrentGeneratorVersion,
                        MacroClimateGenerator.DeterministicGenerationContract,
                        out GeneratedWorld world, out string error))
                {
                    failures.Add(label + " failed: " + error);
                    continue;
                }
                ClimateStatistics statistics =
                    ValidateClimateQuality(world, label, failures, softFindings);
                if (coverage == LandCoveragePreset.High && index == 0)
                {
                    if (size == WorldSizePreset.Small) smallTransitions = statistics.RegionalTransitions;
                    if (size == WorldSizePreset.Huge) hugeTransitions = statistics.RegionalTransitions;
                }
            }

            Check(smallTransitions >= 4,
                "Small must contain multiple substantial Climate variations; transitions=" + smallTransitions,
                failures);
            Check(hugeTransitions > smallTransitions,
                "Huge must add regional Climate variation rather than only stretch Small; small=" +
                smallTransitions + ", huge=" + hugeTransitions, failures);
        }

        private static ClimateStatistics ValidateClimateQuality(
            GeneratedWorld world,
            string label,
            List<string> failures,
            List<string> softFindings)
        {
            MacroClimatePlan climate = world.Climate;
            var thermal = new ushort[climate.SampleCount];
            var moisture = new ushort[climate.SampleCount];
            var elevations = new ushort[climate.SampleCount];
            int[] oceanDistance = BuildOceanDistanceField(world.Water);
            long thermalTotal = 0;
            long moistureTotal = 0;
            long northTotal = 0;
            long southTotal = 0;
            int northCount = 0;
            int southCount = 0;
            long coastalMoisture = 0;
            int coastalCount = 0;
            long inlandMoisture = 0;
            int inlandCount = 0;
            long neighborDeltaTotal = 0;
            int neighborEdges = 0;
            int maximumThermalDelta = 0;
            int maximumMoistureDelta = 0;
            int saturated = 0;

            for (int y = 0; y < climate.SampleRows; y++)
            for (int x = 0; x < climate.SampleColumns; x++)
            {
                int sample = y * climate.SampleColumns + x;
                MacroClimateSample value = climate.SampleAt(x, y);
                thermal[sample] = value.ThermalIndex;
                moisture[sample] = value.MoistureIndex;
                elevations[sample] = world.Geography.ElevationSampleAt(x, y);
                thermalTotal += value.ThermalIndex;
                moistureTotal += value.MoistureIndex;
                if (y >= climate.SampleRows * 3 / 4)
                {
                    northTotal += value.ThermalIndex;
                    northCount++;
                }
                if (y < climate.SampleRows / 4)
                {
                    southTotal += value.ThermalIndex;
                    southCount++;
                }
                if (oceanDistance[sample] <= 1)
                {
                    coastalMoisture += value.MoistureIndex;
                    coastalCount++;
                }
                if (oceanDistance[sample] >= climate.GenerationSettings.OceanInfluenceDistanceCells)
                {
                    inlandMoisture += value.MoistureIndex;
                    inlandCount++;
                }
                if (value.ThermalIndex == 0 || value.ThermalIndex == ushort.MaxValue ||
                    value.MoistureIndex == 0 || value.MoistureIndex == ushort.MaxValue)
                    saturated++;

                if (x > 0)
                    AccumulateNeighbor(value, climate.SampleAt(x - 1, y));
                if (y > 0)
                    AccumulateNeighbor(value, climate.SampleAt(x, y - 1));
            }

            ushort[] sortedThermal = (ushort[])thermal.Clone();
            ushort[] sortedMoisture = (ushort[])moisture.Clone();
            ushort[] sortedElevation = (ushort[])elevations.Clone();
            Array.Sort(sortedThermal);
            Array.Sort(sortedMoisture);
            Array.Sort(sortedElevation);
            ushort lowElevationThreshold = Percentile(sortedElevation, 25);
            ushort highElevationThreshold = Percentile(sortedElevation, 75);
            long lowElevationThermal = 0;
            int lowElevationCount = 0;
            long highElevationThermal = 0;
            int highElevationCount = 0;
            for (int index = 0; index < thermal.Length; index++)
            {
                if (elevations[index] <= lowElevationThreshold)
                {
                    lowElevationThermal += thermal[index];
                    lowElevationCount++;
                }
                if (elevations[index] >= highElevationThreshold)
                {
                    highElevationThermal += thermal[index];
                    highElevationCount++;
                }
            }

            ResolveDirection(
                climate.PrevailingMoistureDirection, out int directionX, out int directionY);
            long ridgeMoistureAdvantage = 0;
            int ridgeEvidenceCount = 0;
            for (int y = 0; y < climate.SampleRows; y++)
            for (int x = 0; x < climate.SampleColumns; x++)
            {
                int leewardX = x + directionX;
                int leewardY = y + directionY;
                if (leewardX < 0 || leewardX >= climate.SampleColumns ||
                    leewardY < 0 || leewardY >= climate.SampleRows) continue;
                int ridgeElevation = world.Geography.ElevationSampleAt(x, y);
                int leewardElevation = world.Geography.ElevationSampleAt(leewardX, leewardY);
                if (ridgeElevation - leewardElevation <
                    climate.GenerationSettings.OrographicRiseThresholdQ16) continue;
                ridgeMoistureAdvantage +=
                    climate.SampleAt(x, y).MoistureIndex -
                    climate.SampleAt(leewardX, leewardY).MoistureIndex;
                ridgeEvidenceCount++;
            }

            var statistics = new ClimateStatistics(
                sortedThermal[0], sortedThermal[sortedThermal.Length - 1],
                thermalTotal / thermal.Length,
                Percentile(sortedThermal, 10), Percentile(sortedThermal, 50),
                Percentile(sortedThermal, 90),
                northTotal / Math.Max(1, northCount),
                southTotal / Math.Max(1, southCount),
                lowElevationThermal / Math.Max(1, lowElevationCount),
                highElevationThermal / Math.Max(1, highElevationCount),
                sortedMoisture[0], sortedMoisture[sortedMoisture.Length - 1],
                moistureTotal / moisture.Length,
                Percentile(sortedMoisture, 10), Percentile(sortedMoisture, 50),
                Percentile(sortedMoisture, 90),
                coastalMoisture / Math.Max(1, coastalCount),
                inlandMoisture / Math.Max(1, inlandCount),
                maximumThermalDelta,
                maximumMoistureDelta,
                neighborDeltaTotal / Math.Max(1, neighborEdges * 2L),
                ridgeEvidenceCount,
                ridgeMoistureAdvantage / Math.Max(1, ridgeEvidenceCount),
                CountRegionalTransitions(climate),
                saturated);

            Check(statistics.ThermalMaximum - statistics.ThermalMinimum >= 9000,
                label + " has a broken/narrow thermal field: " + statistics.Describe(), failures);
            Check(statistics.MoistureMaximum - statistics.MoistureMinimum >= 9000,
                label + " has a broken/narrow moisture field: " + statistics.Describe(), failures);
            Check(statistics.SouthMean > statistics.NorthMean + 5000,
                label + " violates the north-cold/south-warm orientation: " + statistics.Describe(), failures);
            Check(statistics.MaximumThermalNeighborDelta < 22000 &&
                  statistics.MaximumMoistureNeighborDelta < 24000,
                label + " has discontinuous neighbor deltas: " + statistics.Describe(), failures);
            Check(statistics.SaturatedSampleCount * 100 < climate.SampleCount * 5,
                label + " has pathological saturation: " + statistics.Describe(), failures);

            if (statistics.HighElevationMean >= statistics.LowElevationMean)
                softFindings.Add(label + " global elevation/latitude mix obscures cooling: " + statistics.Describe());
            if (inlandCount > 0 && statistics.CoastalMean <= statistics.DeepInlandMean)
                softFindings.Add(label + " regional anomalies obscure aggregate ocean influence: " + statistics.Describe());
            if (statistics.OrographicEvidenceCount == 0 || statistics.RidgeMoistureAdvantage <= 0)
                softFindings.Add(label + " lacks an aggregate golden-style ridge/leeward signal: " + statistics.Describe());

            Debug.Log("[MacroClimate][QUALITY] " + label + " " + statistics.Describe());
            return statistics;

            void AccumulateNeighbor(MacroClimateSample first, MacroClimateSample second)
            {
                int thermalDelta = Math.Abs(first.ThermalIndex - second.ThermalIndex);
                int moistureDelta = Math.Abs(first.MoistureIndex - second.MoistureIndex);
                maximumThermalDelta = Math.Max(maximumThermalDelta, thermalDelta);
                maximumMoistureDelta = Math.Max(maximumMoistureDelta, moistureDelta);
                neighborDeltaTotal += thermalDelta + moistureDelta;
                neighborEdges++;
            }
        }

        private static void ValidatePersistence(
            string root,
            LoadedContentSet content,
            List<string> failures)
        {
            if (content == null) return;
            var store = new PersistenceFileStore(root);
            WorldSessionService.Close();
            WorldSessionOperationResult create = WorldSessionService.Create(
                "Climate Schema 6", new WorldSeed(20260827),
                WorldGenerationSettings.ResolvePreset(WorldSizePreset.Medium),
                LandCoveragePreset.Medium, content, store);
            Check(create.Success, "Schema 6 creation failed: " + Safe(create.Failure), failures);
            if (!create.Success) return;

            WorldSession expected = create.Session;
            JToken payload = WorldSessionPersistenceService.ToPayload(expected);
            Check((int)payload["schemaVersion"] == WorldSessionPersistenceService.CurrentSchemaVersion &&
                  payload["macroClimate"]?["thermalSamplesBase64"]?.Type == JTokenType.String &&
                  payload["macroClimate"]?["moistureSamplesBase64"]?.Type == JTokenType.String &&
                  payload["macroClimate"]?["generationSettings"]?["prevailingMoistureDirection"]?.Type ==
                  JTokenType.String,
                "Schema 6 must persist exact Climate fields/settings.", failures);

            WorldSessionService.Close();
            WorldSessionPersistenceResult read =
                WorldSessionPersistenceService.Read(expected.WorldId.Canonical, store);
            Check(read.Success && read.Session.HasMacroClimate &&
                  SessionsHaveSameCommittedTruth(expected, read.Session),
                "Schema 6 did not reconstruct exact committed Climate/world truth: " +
                Safe(read.Failure), failures);

            JObject corruptHash = (JObject)payload.DeepClone();
            corruptHash["macroClimate"]["canonicalHash"] = new string('0', 64);
            WorldSessionService.Close();
            Check(!WorldSessionPersistenceService.FromPayload(corruptHash).Success &&
                  !WorldSessionService.HasActiveSession,
                "Malformed Climate hash must fail before session publication.", failures);

            JObject corruptSamples = (JObject)payload.DeepClone();
            corruptSamples["macroClimate"]["thermalSamplesBase64"] = null;
            Check(!WorldSessionPersistenceService.FromPayload(corruptSamples).Success,
                "Null Climate samples must fail strict semantic preflight.", failures);

            for (int schema = WorldSessionPersistenceService.LegacySchemaVersion;
                 schema <= WorldSessionPersistenceService.MacroHumanGeographySchemaVersion;
                 schema++)
            {
                JObject legacyPayload = BuildLegacyPayload((JObject)payload, schema);
                WorldSessionPersistenceResult legacy =
                    WorldSessionPersistenceService.FromPayload(legacyPayload);
                bool expectedHuman = schema >= WorldSessionPersistenceService.MacroHumanGeographySchemaVersion;
                Check(legacy.Success && !legacy.Session.HasMacroClimate &&
                      legacy.Session.HasMacroHumanGeography == expectedHuman &&
                      (int)WorldSessionPersistenceService.ToPayload(legacy.Session)["schemaVersion"] == schema,
                    "Schema " + schema + " must load/re-save exact legacy truth without Climate fabrication: " +
                    Safe(legacy.Failure), failures);
            }
        }

        private static JObject BuildLegacyPayload(JObject current, int schema)
        {
            var legacy = new JObject
            {
                ["snapshotType"] = current["snapshotType"].DeepClone(),
                ["schemaVersion"] = schema,
                ["worldId"] = current["worldId"].DeepClone(),
                ["displayName"] = current["displayName"].DeepClone(),
                ["generationContext"] = current["generationContext"].DeepClone(),
                ["activeSectorId"] = current["activeSectorId"].DeepClone(),
                ["creationContentProvenance"] = current["creationContentProvenance"].DeepClone()
            };
            if (schema == WorldSessionPersistenceService.LegacySchemaVersion)
            {
                legacy["topology"] = current["macroWorldPlan"]["topology"].DeepClone();
                return legacy;
            }
            legacy["macroWorldPlan"] = current["macroWorldPlan"].DeepClone();
            if (schema >= WorldSessionPersistenceService.MacroGeographySchemaVersion)
                legacy["macroGeography"] = current["macroGeography"].DeepClone();
            if (schema >= WorldSessionPersistenceService.MacroWaterSchemaVersion)
                legacy["macroWater"] = current["macroWater"].DeepClone();
            if (schema >= WorldSessionPersistenceService.MacroHumanGeographySchemaVersion)
                legacy["macroHumanGeography"] = current["macroHumanGeography"].DeepClone();
            return legacy;
        }

        private static void MeasurePresets(
            LoadedContentSet content,
            IList<string> measurements,
            List<string> failures)
        {
            if (content == null) return;
            foreach (WorldSizePreset size in Enum.GetValues(typeof(WorldSizePreset)))
            {
                var stopwatch = Stopwatch.StartNew();
                bool built = WorldSessionBootstrap.TryBuildNew(
                    "Climate Size Evidence", new WorldSeed(99887766),
                    WorldGenerationSettings.ResolvePreset(size), LandCoveragePreset.High,
                    content, out WorldSession session, out string error);
                stopwatch.Stop();
                Check(built, "Climate performance fixture failed for " + size + ": " + Safe(error), failures);
                if (!built) continue;
                int serializedBytes = Encoding.UTF8.GetByteCount(
                    WorldSessionPersistenceService.ToPayload(session).ToString(Formatting.None));
                int climateRawBytes = session.MacroClimate.SampleCount * 4;
                measurements.Add(
                    size + "=" + stopwatch.ElapsedMilliseconds + "ms/" +
                    serializedBytes.ToString(CultureInfo.InvariantCulture) + "B payload/" +
                    climateRawBytes.ToString(CultureInfo.InvariantCulture) + "B climate raw");
            }
        }

        private static string ExportGoldenPreview()
        {
            if (!TryGenerate(
                    GoldenSeed, WorldSizePreset.Large, LandCoveragePreset.High,
                    WorldSessionBootstrap.CurrentGeneratorVersion,
                    MacroClimateGenerator.DeterministicGenerationContract,
                    out GeneratedWorld world, out string error))
                throw new InvalidOperationException("Golden preview generation failed: " + error);
            string directory = ResolvePreviewDirectory();
            Directory.CreateDirectory(directory);
            string outputPath = Path.Combine(
                directory, "OldScars_MacroClimate_Golden_Large_High.png");
            MacroGeographyPreviewExporter.Export(
                world.Plan, world.Geography, world.Water, world.Climate,
                world.Quality, world.Human, world.Starter,
                outputPath, 320, 320, true);
            return outputPath;
        }

        private static string ResolvePreviewDirectory()
        {
            string directory = Environment.GetEnvironmentVariable(PreviewDirectoryEnvironment);
            return string.IsNullOrWhiteSpace(directory)
                ? Path.Combine(Path.GetTempPath(), "OldScars_MacroClimatePreviews")
                : directory;
        }

        private static bool TryGenerate(
            long seed,
            WorldSizePreset size,
            LandCoveragePreset coverage,
            string pipelineVersion,
            string climatePassContract,
            out GeneratedWorld world,
            out string error)
        {
            world = null;
            error = null;
            var context = new WorldGenerationContext(
                new WorldSeed(seed), GeneratorVersion.Parse(pipelineVersion));
            if (!MacroWorldPlanGenerator.TryGenerate(
                    context, WorldGenerationSettings.ResolvePreset(size),
                    out MacroWorldPlan plan, out error)) return false;
            if (!MacroGeographyGenerator.TryGenerate(
                    context, plan, out MacroGeographyPlan geography, out error)) return false;
            if (!MacroWaterGenerator.TryGenerate(
                    plan, geography, coverage, out MacroWaterPlan water, out error)) return false;
            bool climateBuilt = string.Equals(
                climatePassContract,
                MacroClimateGenerator.DeterministicGenerationContract,
                StringComparison.Ordinal)
                ? MacroClimateGenerator.TryGenerate(
                    context, plan, geography, water, out MacroClimatePlan climate, out error)
                : MacroClimateGenerator.TryGenerateForPassContractDiagnostics(
                    context, plan, geography, water, climatePassContract,
                    out climate, out error);
            if (!climateBuilt) return false;
            if (!WorldGameplayQualityAnalyzer.TryAnalyze(
                    plan, geography, water,
                    out WorldGameplayQualityAnalysis quality, out error)) return false;
            if (!WorldStarterSectorSelector.TrySelect(
                    quality, out SectorId starter, out error)) return false;
            if (!MacroHumanGeographyGenerator.TryGenerate(
                    context, plan, geography, water, quality, starter,
                    out MacroHumanGeographyPlan human, out error)) return false;
            world = new GeneratedWorld(
                context, plan, geography, water, climate, quality, starter, human);
            return true;
        }

        private static bool SessionsHaveSameCommittedTruth(WorldSession expected, WorldSession actual)
        {
            return expected != null && actual != null &&
                   expected.WorldId == actual.WorldId &&
                   expected.GenerationContext.WorldSeed == actual.GenerationContext.WorldSeed &&
                   expected.GenerationContext.GeneratorVersion == actual.GenerationContext.GeneratorVersion &&
                   expected.MacroWorldPlan.CanonicalHash == actual.MacroWorldPlan.CanonicalHash &&
                   expected.MacroGeography.CanonicalHash == actual.MacroGeography.CanonicalHash &&
                   expected.MacroWater.CanonicalHash == actual.MacroWater.CanonicalHash &&
                   expected.MacroClimate.CanonicalHash == actual.MacroClimate.CanonicalHash &&
                   expected.MacroClimate.PrevailingMoistureDirection ==
                   actual.MacroClimate.PrevailingMoistureDirection &&
                   expected.MacroHumanGeography.CanonicalHash == actual.MacroHumanGeography.CanonicalHash &&
                   expected.Topology.CanonicalHash == actual.Topology.CanonicalHash &&
                   expected.ActiveSectorId == actual.ActiveSectorId;
        }

        private static bool AllHashesEqual(GeneratedWorld first, GeneratedWorld second)
        {
            return first.Plan.CanonicalHash == second.Plan.CanonicalHash &&
                   first.Geography.CanonicalHash == second.Geography.CanonicalHash &&
                   first.Water.CanonicalHash == second.Water.CanonicalHash &&
                   first.Climate.CanonicalHash == second.Climate.CanonicalHash &&
                   first.Human.CanonicalHash == second.Human.CanonicalHash &&
                   first.Starter == second.Starter;
        }

        private static int[] BuildOceanDistanceField(MacroWaterPlan water)
        {
            var distance = new int[water.SampleCount];
            var queue = new Queue<int>(water.SampleCount);
            for (int index = 0; index < distance.Length; index++)
            {
                int x = index % water.SampleColumns;
                int y = index / water.SampleColumns;
                if (water.SampleAt(x, y).IsOcean)
                {
                    distance[index] = 0;
                    queue.Enqueue(index);
                }
                else distance[index] = int.MaxValue;
            }
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int x = current % water.SampleColumns;
                int y = current / water.SampleColumns;
                int nextDistance = distance[current] + 1;
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0) continue;
                    int nextX = x + offsetX;
                    int nextY = y + offsetY;
                    if (nextX < 0 || nextX >= water.SampleColumns ||
                        nextY < 0 || nextY >= water.SampleRows) continue;
                    int next = nextY * water.SampleColumns + nextX;
                    if (distance[next] <= nextDistance) continue;
                    distance[next] = nextDistance;
                    queue.Enqueue(next);
                }
            }
            return distance;
        }

        private static int CountRegionalTransitions(MacroClimatePlan climate)
        {
            long mean = 0;
            for (int y = 0; y < climate.SampleRows; y++)
            for (int x = 0; x < climate.SampleColumns; x++)
                mean += climate.SampleAt(x, y).MoistureIndex;
            mean /= climate.SampleCount;
            int transitions = 0;
            int middleX = climate.SampleColumns / 2;
            int middleY = climate.SampleRows / 2;
            bool previous = climate.SampleAt(0, middleY).MoistureIndex >= mean;
            for (int x = 1; x < climate.SampleColumns; x++)
            {
                bool current = climate.SampleAt(x, middleY).MoistureIndex >= mean;
                if (current != previous) transitions++;
                previous = current;
            }
            previous = climate.SampleAt(middleX, 0).MoistureIndex >= mean;
            for (int y = 1; y < climate.SampleRows; y++)
            {
                bool current = climate.SampleAt(middleX, y).MoistureIndex >= mean;
                if (current != previous) transitions++;
                previous = current;
            }
            return transitions;
        }

        private static ushort Percentile(ushort[] sorted, int percent)
        {
            int index = (sorted.Length - 1) * percent / 100;
            return sorted[index];
        }

        private static void ResolveDirection(
            MacroMoistureDirection direction,
            out int x,
            out int y)
        {
            switch (direction)
            {
                case MacroMoistureDirection.North: x = 0; y = 1; return;
                case MacroMoistureDirection.NorthEast: x = 1; y = 1; return;
                case MacroMoistureDirection.East: x = 1; y = 0; return;
                case MacroMoistureDirection.SouthEast: x = 1; y = -1; return;
                case MacroMoistureDirection.South: x = 0; y = -1; return;
                case MacroMoistureDirection.SouthWest: x = -1; y = -1; return;
                case MacroMoistureDirection.West: x = -1; y = 0; return;
                case MacroMoistureDirection.NorthWest: x = -1; y = 1; return;
                default: throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        private static LoadedContentSet LoadValidatedCore(List<string> failures)
        {
            string modsRoot = Path.Combine(Application.streamingAssetsPath, "Mods");
            var report = new DataLoadReport();
            var loader = new GameDataLoader(modsRoot, report);
            loader.LoadAll();
            var validator = new DataValidator(loader.Database, loader.Tags, report);
            validator.Validate();
            if (report.HasErrors || !loader.TryBuildLoadedContentSet(out LoadedContentSet content))
            {
                failures.Add("Real Core failed loader/DataValidator/provenance validation.");
                return null;
            }
            return content;
        }

        private static void Complete(string title, IList<string> failures, params string[] evidence)
        {
            if (failures.Count > 0)
            {
                string failure = title + ": FAIL\n- " + string.Join("\n- ", failures);
                Debug.LogError(failure);
                throw new InvalidOperationException(failure);
            }
            Debug.Log(title + ": PASS\n" + string.Join("\n", evidence));
        }

        private static void Check(bool condition, string failure, ICollection<string> failures)
        {
            if (!condition) failures.Add(failure);
        }

        private static string Safe(string value) => string.IsNullOrEmpty(value) ? "<NONE>" : value;

        private sealed class GeneratedWorld
        {
            public GeneratedWorld(
                WorldGenerationContext context,
                MacroWorldPlan plan,
                MacroGeographyPlan geography,
                MacroWaterPlan water,
                MacroClimatePlan climate,
                WorldGameplayQualityAnalysis quality,
                SectorId starter,
                MacroHumanGeographyPlan human)
            {
                Context = context;
                Plan = plan;
                Geography = geography;
                Water = water;
                Climate = climate;
                Quality = quality;
                Starter = starter;
                Human = human;
            }

            public WorldGenerationContext Context { get; }
            public MacroWorldPlan Plan { get; }
            public MacroGeographyPlan Geography { get; }
            public MacroWaterPlan Water { get; }
            public MacroClimatePlan Climate { get; }
            public WorldGameplayQualityAnalysis Quality { get; }
            public SectorId Starter { get; }
            public MacroHumanGeographyPlan Human { get; }
        }

        private sealed class ClimateStatistics
        {
            public ClimateStatistics(
                ushort thermalMinimum,
                ushort thermalMaximum,
                long thermalMean,
                ushort thermalP10,
                ushort thermalP50,
                ushort thermalP90,
                long northMean,
                long southMean,
                long lowElevationMean,
                long highElevationMean,
                ushort moistureMinimum,
                ushort moistureMaximum,
                long moistureMean,
                ushort moistureP10,
                ushort moistureP50,
                ushort moistureP90,
                long coastalMean,
                long deepInlandMean,
                int maximumThermalNeighborDelta,
                int maximumMoistureNeighborDelta,
                long meanNeighborDelta,
                int orographicEvidenceCount,
                long ridgeMoistureAdvantage,
                int regionalTransitions,
                int saturatedSampleCount)
            {
                ThermalMinimum = thermalMinimum;
                ThermalMaximum = thermalMaximum;
                ThermalMean = thermalMean;
                ThermalP10 = thermalP10;
                ThermalP50 = thermalP50;
                ThermalP90 = thermalP90;
                NorthMean = northMean;
                SouthMean = southMean;
                LowElevationMean = lowElevationMean;
                HighElevationMean = highElevationMean;
                MoistureMinimum = moistureMinimum;
                MoistureMaximum = moistureMaximum;
                MoistureMean = moistureMean;
                MoistureP10 = moistureP10;
                MoistureP50 = moistureP50;
                MoistureP90 = moistureP90;
                CoastalMean = coastalMean;
                DeepInlandMean = deepInlandMean;
                MaximumThermalNeighborDelta = maximumThermalNeighborDelta;
                MaximumMoistureNeighborDelta = maximumMoistureNeighborDelta;
                MeanNeighborDelta = meanNeighborDelta;
                OrographicEvidenceCount = orographicEvidenceCount;
                RidgeMoistureAdvantage = ridgeMoistureAdvantage;
                RegionalTransitions = regionalTransitions;
                SaturatedSampleCount = saturatedSampleCount;
            }

            public ushort ThermalMinimum { get; }
            public ushort ThermalMaximum { get; }
            public long ThermalMean { get; }
            public ushort ThermalP10 { get; }
            public ushort ThermalP50 { get; }
            public ushort ThermalP90 { get; }
            public long NorthMean { get; }
            public long SouthMean { get; }
            public long LowElevationMean { get; }
            public long HighElevationMean { get; }
            public ushort MoistureMinimum { get; }
            public ushort MoistureMaximum { get; }
            public long MoistureMean { get; }
            public ushort MoistureP10 { get; }
            public ushort MoistureP50 { get; }
            public ushort MoistureP90 { get; }
            public long CoastalMean { get; }
            public long DeepInlandMean { get; }
            public int MaximumThermalNeighborDelta { get; }
            public int MaximumMoistureNeighborDelta { get; }
            public long MeanNeighborDelta { get; }
            public int OrographicEvidenceCount { get; }
            public long RidgeMoistureAdvantage { get; }
            public int RegionalTransitions { get; }
            public int SaturatedSampleCount { get; }

            public string Describe()
            {
                return "thermal=" + ThermalMinimum + "/" + ThermalP10 + "/" + ThermalP50 +
                       "/" + ThermalP90 + "/" + ThermalMaximum + " mean=" + ThermalMean +
                       " N/S=" + NorthMean + "/" + SouthMean +
                       " low/high=" + LowElevationMean + "/" + HighElevationMean +
                       " moisture=" + MoistureMinimum + "/" + MoistureP10 + "/" + MoistureP50 +
                       "/" + MoistureP90 + "/" + MoistureMaximum + " mean=" + MoistureMean +
                       " coast/inland=" + CoastalMean + "/" + DeepInlandMean +
                       " maxDelta=" + MaximumThermalNeighborDelta + "/" +
                       MaximumMoistureNeighborDelta + " meanDelta=" + MeanNeighborDelta +
                       " ridge=" + OrographicEvidenceCount + "/" + RidgeMoistureAdvantage +
                       " transitions=" + RegionalTransitions + " saturated=" + SaturatedSampleCount;
            }
        }
    }
}
