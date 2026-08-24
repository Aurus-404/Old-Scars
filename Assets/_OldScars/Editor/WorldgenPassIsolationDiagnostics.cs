using System;
using System.Collections.Generic;
using OldScars.Core.World;
using UnityEngine;

namespace OldScars.EditorTools
{
    public static class WorldgenPassIsolationDiagnostics
    {
        private const long GoldenSeed = 8675309123456789L;
        private const string HistoricalPipelineVersion = "macro_water_quality_v1";
        private const string SyntheticFuturePipelineVersion = "macro_climate_v1";
        private const string SyntheticGeographyContract = "macro_geography_v2_probe";
        private const string GoldenPlanHash =
            "3f300ba2129962493d2ab8f2ad6ec0863e96aa0ceeb400f9899f91889a34e91a";
        private const string GoldenGeographyHash =
            "c2d412fcdcb1b0e1b41f4fdbda2df01258758e6db9c6b93aac59b446be7dbd3e";
        private const string GoldenWaterHash =
            "ec29f501e4f36ae3b2313d3da6089f2fe6e92b052f18079c649e21ce8faabfc0";

        public static void Run()
        {
            var failures = new List<string>();
            try
            {
                ValidateOverallPipelineIsolation(failures);
                ValidatePassDependencyAndSettingsIsolation(failures);
                ValidateWorldIdAndFuzz(failures);
            }
            catch (Exception exception)
            {
                failures.Add("Diagnostic threw " + exception.GetType().Name + ": " + exception.Message);
            }

            if (failures.Count > 0)
            {
                string failure = "Worldgen Pass Isolation Correction: FAIL\n- " +
                                 string.Join("\n- ", failures);
                Debug.LogError(failure);
                throw new InvalidOperationException(failure);
            }

            Debug.Log(
                "Worldgen Pass Isolation Correction: PASS\n" +
                "- overall pipeline version is absent from pass-domain derivation\n" +
                "- Plan contract: " + MacroWorldPlanGenerator.DeterministicGenerationContract + "\n" +
                "- Geography contract: " + MacroGeographyGenerator.DeterministicGenerationContract + "\n" +
                "- Water contract: " + MacroWaterGenerationSettings.CurrentContract + "\n" +
                "- restored Plan golden: " + GoldenPlanHash + "\n" +
                "- restored Geography golden: " + GoldenGeographyHash + "\n" +
                "- corrected Water golden: " + GoldenWaterHash + "\n" +
                "- isolation fuzz: 2 seeds x 4 size presets");
        }

        private static void ValidateOverallPipelineIsolation(List<string> failures)
        {
            WorldGenerationSettings settings =
                WorldGenerationSettings.ResolvePreset(WorldSizePreset.Large);
            var historical = new WorldGenerationContext(
                new WorldSeed(GoldenSeed), GeneratorVersion.Parse(HistoricalPipelineVersion));
            var current = new WorldGenerationContext(
                historical.WorldSeed, GeneratorVersion.Parse(WorldSessionBootstrap.CurrentGeneratorVersion));
            var future = new WorldGenerationContext(
                historical.WorldSeed, GeneratorVersion.Parse(SyntheticFuturePipelineVersion));

            Generate(historical, settings, LandCoveragePreset.High,
                out MacroWorldPlan historicalPlan, out MacroGeographyPlan historicalGeography,
                out MacroWaterPlan historicalWater, failures, "historical pipeline metadata");
            Generate(current, settings, LandCoveragePreset.High,
                out MacroWorldPlan currentPlan, out MacroGeographyPlan currentGeography,
                out MacroWaterPlan currentWater, failures, "current pipeline metadata");
            Generate(future, settings, LandCoveragePreset.High,
                out MacroWorldPlan futurePlan, out MacroGeographyPlan futureGeography,
                out MacroWaterPlan futureWater, failures, "synthetic Climate pipeline metadata");
            if (historicalPlan == null || historicalGeography == null || historicalWater == null ||
                currentPlan == null || currentGeography == null || currentWater == null ||
                futurePlan == null || futureGeography == null || futureWater == null)
                return;

            Check(historicalPlan.CanonicalHash == currentPlan.CanonicalHash &&
                  historicalPlan.CanonicalHash == futurePlan.CanonicalHash,
                "Changing only overall pipeline version changed MacroWorldPlan.", failures);
            Check(historicalGeography.CanonicalHash == currentGeography.CanonicalHash &&
                  historicalGeography.CanonicalHash == futureGeography.CanonicalHash,
                "Changing only overall pipeline version changed MacroGeography.", failures);
            Check(historicalWater.CanonicalHash == currentWater.CanonicalHash &&
                  historicalWater.CanonicalHash == futureWater.CanonicalHash,
                "Changing only overall pipeline version changed MacroWater with identical real inputs.", failures);
            Check(currentPlan.CanonicalHash == GoldenPlanHash,
                "Original MacroWorldPlan golden was not restored. Got " + currentPlan.CanonicalHash + ".",
                failures);
            Check(currentGeography.CanonicalHash == GoldenGeographyHash,
                "Original MacroGeography golden was not restored. Got " +
                currentGeography.CanonicalHash + ".", failures);
            Check(currentWater.CanonicalHash == GoldenWaterHash,
                "Corrected-upstream Water golden drifted. Expected " + GoldenWaterHash +
                ", got " + currentWater.CanonicalHash + ".", failures);

            DeterministicDomainKey climateV1 = WorldDeterminism.DerivePassDomainKey(
                current.WorldSeed, "macro_climate_v1", "macro_large", "moisture");
            DeterministicDomainKey climateV2 = WorldDeterminism.DerivePassDomainKey(
                current.WorldSeed, "macro_climate_v2", "macro_large", "moisture");
            Check(climateV1 != climateV2 &&
                  currentPlan.CanonicalHash == futurePlan.CanonicalHash &&
                  currentGeography.CanonicalHash == futureGeography.CanonicalHash &&
                  currentWater.CanonicalHash == futureWater.CanonicalHash,
                "A synthetic future Climate contract must isolate its own domain without perturbing existing passes.",
                failures);
        }

        private static void ValidatePassDependencyAndSettingsIsolation(List<string> failures)
        {
            var context = new WorldGenerationContext(
                new WorldSeed(GoldenSeed),
                GeneratorVersion.Parse(WorldSessionBootstrap.CurrentGeneratorVersion));
            WorldGenerationSettings settings =
                WorldGenerationSettings.ResolvePreset(WorldSizePreset.Large);
            Generate(context, settings, LandCoveragePreset.High,
                out MacroWorldPlan plan, out MacroGeographyPlan geography,
                out MacroWaterPlan highWater, failures, "pass dependency baseline");
            if (plan == null || geography == null || highWater == null)
                return;

            bool changedGeographyBuilt = MacroGeographyGenerator.TryGenerateForPassContractDiagnostics(
                context, plan, SyntheticGeographyContract,
                out MacroGeographyPlan changedGeography, out string changedGeographyError);
            Check(changedGeographyBuilt && changedGeography != null,
                "Synthetic Geography contract failed to generate: " + Safe(changedGeographyError) + ".",
                failures);
            if (changedGeography != null)
            {
                Check(changedGeography.CanonicalHash != geography.CanonicalHash,
                    "Changing Geography's own pass contract did not change Geography evidence.", failures);
                Check(plan.CanonicalHash == GoldenPlanHash,
                    "Changing Geography's own pass contract perturbed the upstream plan.", failures);
                bool changedWaterBuilt = MacroWaterGenerator.TryGenerate(
                    plan, changedGeography, LandCoveragePreset.High,
                    out MacroWaterPlan changedWater, out string changedWaterError);
                Check(changedWaterBuilt && changedWater != null &&
                      changedWater.CanonicalHash != highWater.CanonicalHash,
                    "Water should respond to its changed Geography input. " + Safe(changedWaterError), failures);
            }

            bool lowBuilt = MacroWaterGenerator.TryGenerate(
                plan, geography, LandCoveragePreset.Low,
                out MacroWaterPlan lowWater, out string lowError);
            Check(lowBuilt && lowWater != null &&
                  lowWater.CanonicalHash != highWater.CanonicalHash &&
                  plan.CanonicalHash == GoldenPlanHash &&
                  geography.CanonicalHash == GoldenGeographyHash,
                "LandCoverage must change Water only. " + Safe(lowError), failures);
        }

        private static void ValidateWorldIdAndFuzz(List<string> failures)
        {
            WorldId first = WorldId.CreateNew();
            WorldId second = WorldId.CreateNew();
            var context = new WorldGenerationContext(
                new WorldSeed(GoldenSeed),
                GeneratorVersion.Parse(WorldSessionBootstrap.CurrentGeneratorVersion));
            WorldGenerationSettings settings =
                WorldGenerationSettings.ResolvePreset(WorldSizePreset.Small);
            Generate(context, settings, LandCoveragePreset.Medium,
                out MacroWorldPlan firstPlan, out MacroGeographyPlan firstGeography,
                out MacroWaterPlan firstWater, failures, "WorldId probe A");
            Generate(context, settings, LandCoveragePreset.Medium,
                out MacroWorldPlan secondPlan, out MacroGeographyPlan secondGeography,
                out MacroWaterPlan secondWater, failures, "WorldId probe B");
            Check(first != second && firstPlan != null && secondPlan != null &&
                  firstGeography != null && secondGeography != null &&
                  firstWater != null && secondWater != null &&
                  firstPlan.CanonicalHash == secondPlan.CanonicalHash &&
                  firstGeography.CanonicalHash == secondGeography.CanonicalHash &&
                  firstWater.CanonicalHash == secondWater.CanonicalHash,
                "Different WorldIds with identical generation inputs changed worldgen evidence.", failures);

            WorldSizePreset[] presets =
            {
                WorldSizePreset.Small,
                WorldSizePreset.Medium,
                WorldSizePreset.Large,
                WorldSizePreset.Huge
            };
            long[] seeds = { -400000000000000003L, 700000000000000019L };
            for (int presetIndex = 0; presetIndex < presets.Length; presetIndex++)
            {
                for (int seedIndex = 0; seedIndex < seeds.Length; seedIndex++)
                {
                    WorldGenerationSettings fuzzSettings =
                        WorldGenerationSettings.ResolvePreset(presets[presetIndex]);
                    var oldContext = new WorldGenerationContext(
                        new WorldSeed(seeds[seedIndex]),
                        GeneratorVersion.Parse(HistoricalPipelineVersion));
                    var newContext = new WorldGenerationContext(
                        oldContext.WorldSeed,
                        GeneratorVersion.Parse(WorldSessionBootstrap.CurrentGeneratorVersion));
                    string label = "fuzz " + presets[presetIndex] + "/" + seeds[seedIndex];
                    Generate(oldContext, fuzzSettings, LandCoveragePreset.High,
                        out MacroWorldPlan oldPlan, out MacroGeographyPlan oldGeography,
                        out MacroWaterPlan oldWater, failures, label + " old");
                    Generate(newContext, fuzzSettings, LandCoveragePreset.High,
                        out MacroWorldPlan newPlan, out MacroGeographyPlan newGeography,
                        out MacroWaterPlan newWater, failures, label + " new");
                    Check(oldPlan != null && newPlan != null &&
                          oldGeography != null && newGeography != null &&
                          oldWater != null && newWater != null &&
                          oldPlan.CanonicalHash == newPlan.CanonicalHash &&
                          oldGeography.CanonicalHash == newGeography.CanonicalHash &&
                          oldWater.CanonicalHash == newWater.CanonicalHash,
                        label + " changed when only overall pipeline version changed.", failures);
                }
            }
        }

        private static void Generate(
            WorldGenerationContext context,
            WorldGenerationSettings settings,
            LandCoveragePreset coverage,
            out MacroWorldPlan plan,
            out MacroGeographyPlan geography,
            out MacroWaterPlan water,
            List<string> failures,
            string label)
        {
            plan = null;
            geography = null;
            water = null;
            if (!MacroWorldPlanGenerator.TryGenerate(context, settings, out plan, out string planError))
            {
                failures.Add(label + " plan failed: " + planError);
                return;
            }
            if (!MacroGeographyGenerator.TryGenerate(
                    context, plan, out geography, out string geographyError))
            {
                failures.Add(label + " geography failed: " + geographyError);
                return;
            }
            if (!MacroWaterGenerator.TryGenerate(
                    plan, geography, coverage, out water, out string waterError))
                failures.Add(label + " Water failed: " + waterError);
        }

        private static void Check(bool condition, string failure, List<string> failures)
        {
            if (!condition)
                failures.Add(failure);
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "<NONE>" : value;
        }
    }
}
