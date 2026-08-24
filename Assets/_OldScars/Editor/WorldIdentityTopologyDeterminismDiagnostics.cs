using System;
using System.Collections.Generic;
using OldScars.Core.World;
using UnityEngine;

namespace OldScars.Editor
{
    public static class WorldIdentityTopologyDeterminismDiagnostics
    {
        private const string ExpectedDomainKey =
            "b437abdb6f2ee8ea4edb571e5e80c12b186debdee4f51f020e798b9381d0dec6";
        private const string ExpectedTopologyHash =
            "3be0326555e55dfce4ea12dddf6f66c61452f4aafffb3c1539dec21c81ef1589";

        public static void Run()
        {
            var failures = new List<string>();
            try
            {
                ValidateWorldIdentity(failures);
                ValidateSeedAndDomainDerivation(failures);
                ValidateTopology(failures);
            }
            catch (Exception exception)
            {
                failures.Add($"Diagnostic threw {exception.GetType().Name}: {exception.Message}");
            }

            if (failures.Count > 0)
            {
                string message = "World Identity / Topology / Determinism Foundation: FAIL\n- " +
                                 string.Join("\n- ", failures);
                Debug.LogError(message);
                throw new InvalidOperationException(message);
            }

            Debug.Log(
                "World Identity / Topology / Determinism Foundation: PASS\n" +
                "- WorldId and WorldSeed domains round-trip independently\n" +
                "- SHA-256 derivation is seed/pass-contract stable, pipeline-version independent and WorldId-free\n" +
                "- SectorId and explicit multi-edge topology validated\n" +
                "- duplicate/missing/self/disconnected failures are actionable\n" +
                "- canonical topology is insertion and endpoint-order independent\n" +
                $"- golden domain key: {ExpectedDomainKey}\n" +
                $"- golden topology hash: {ExpectedTopologyHash}");
        }

        private static void ValidateWorldIdentity(List<string> failures)
        {
            const string canonical = "world_00112233445566778899aabbccddeeff";
            Check(WorldId.TryParse(canonical, out WorldId parsed, out _) &&
                  parsed.Canonical == canonical && WorldId.Parse(parsed.ToString()) == parsed,
                "A valid WorldId must round-trip canonically.", failures);

            string[] invalid =
            {
                null,
                string.Empty,
                "world_001122",
                "world_00112233445566778899AABBCCDDEEFF",
                "sector_00112233445566778899aabbccddeeff",
                " world_00112233445566778899aabbccddeeff"
            };
            for (int index = 0; index < invalid.Length; index++)
            {
                Check(!WorldId.TryParse(invalid[index], out _, out string error) &&
                      !string.IsNullOrWhiteSpace(error),
                    $"Invalid WorldId case {index} must fail actionably.", failures);
            }

            WorldId first = WorldId.CreateNew();
            WorldId second = WorldId.CreateNew();
            Check(first.IsValid && second.IsValid && first != second,
                "Two newly created worlds must receive distinct valid WorldIds.", failures);

            WorldSeed min = new WorldSeed(long.MinValue);
            WorldSeed max = new WorldSeed(long.MaxValue);
            Check(WorldSeed.TryParse(min.Canonical, out WorldSeed parsedMin, out _) && parsedMin == min &&
                  WorldSeed.TryParse(max.Canonical, out WorldSeed parsedMax, out _) && parsedMax == max &&
                  !WorldSeed.TryParse("0001", out _, out string seedError) &&
                  !string.IsNullOrWhiteSpace(seedError),
                "WorldSeed must preserve exact signed 64-bit canonical values.", failures);
        }

        private static void ValidateSeedAndDomainDerivation(List<string> failures)
        {
            GeneratorVersion version = GeneratorVersion.Parse("worldgen_1.0.0");
            var context = new WorldGenerationContext(new WorldSeed(424242424242L), version);
            WorldId firstWorld = WorldId.Parse("world_11111111111111111111111111111111");
            WorldId secondWorld = WorldId.Parse("world_22222222222222222222222222222222");

            const string foundationContract = "worldgen_1_0_0";
            DeterministicDomainKey first = WorldDeterminism.DerivePassDomainKey(
                context.WorldSeed, foundationContract, "world", "topology");
            DeterministicDomainKey repeated = WorldDeterminism.DerivePassDomainKey(
                context.WorldSeed, foundationContract, "world", "topology");
            Check(first == repeated && first.IsValid,
                "Same seed/pass-contract/scope/pass must derive the same valid domain key.", failures);

            var changedSeedContext = new WorldGenerationContext(new WorldSeed(424242424243L), version);
            Check(first != WorldDeterminism.DerivePassDomainKey(
                      changedSeedContext.WorldSeed, foundationContract, "world", "topology"),
                "Changing WorldSeed must change the domain key.", failures);
            var changedVersionContext = new WorldGenerationContext(
                context.WorldSeed,
                GeneratorVersion.Parse("worldgen_1.0.1"));
            Check(first == WorldDeterminism.DerivePassDomainKey(
                      changedVersionContext.WorldSeed, foundationContract, "world", "topology"),
                "Changing only overall GeneratorVersion must not change a pass domain key.", failures);
            Check(first != WorldDeterminism.DerivePassDomainKey(
                      context.WorldSeed, "worldgen_1_0_1", "world", "topology"),
                "Changing the owning pass generation contract must change its domain key.", failures);
            Check(first != WorldDeterminism.DerivePassDomainKey(
                      context.WorldSeed, foundationContract, "world", "roads"),
                "Different pass keys must isolate deterministic domains.", failures);
            Check(first != WorldDeterminism.DerivePassDomainKey(
                      context.WorldSeed, foundationContract, "sector_alpha", "topology"),
                "Different scope keys must isolate deterministic domains.", failures);

            // WorldIds are intentionally not accepted by context or derivation.
            DeterministicDomainKey firstWorldEvidence = DeriveForWorldIdentityProbe(firstWorld, context);
            DeterministicDomainKey secondWorldEvidence = DeriveForWorldIdentityProbe(secondWorld, context);
            Check(firstWorld != secondWorld && firstWorldEvidence == secondWorldEvidence,
                "Different WorldIds with the same generation context must not change deterministic evidence.", failures);
            Check(first.Canonical == ExpectedDomainKey,
                $"Fresh-run golden domain key drifted. Expected {ExpectedDomainKey}, got {first.Canonical}.", failures);
        }

        private static void ValidateTopology(List<string> failures)
        {
            var context = new WorldGenerationContext(
                new WorldSeed(424242424242L),
                GeneratorVersion.Parse("worldgen_1.0.0"));
            SectorId alpha = Sector(context, "sector_alpha");
            SectorId beta = Sector(context, "sector_beta");
            SectorId gamma = Sector(context, "sector_gamma");
            SectorId missing = Sector(context, "sector_missing");
            Check(SectorId.TryParse(alpha.Canonical, out SectorId parsedAlpha, out _) &&
                  parsedAlpha == alpha &&
                  !SectorId.TryParse("world_00112233445566778899aabbccddeeff", out _, out string sectorError) &&
                  !string.IsNullOrWhiteSpace(sectorError),
                "SectorId must round-trip in its own domain and reject WorldId text.", failures);

            var road = new SectorConnection("road_01", alpha, beta);
            var river = new SectorConnection("river_01", beta, alpha);
            var rail = new SectorConnection("railway_01", beta, gamma);

            Check(WorldTopology.TryCreate(
                      new[] { alpha, beta, gamma },
                      new[] { road, river, rail },
                      out WorldTopology first,
                      out WorldTopologyValidationResult firstValidation) && firstValidation.IsValid,
                "Valid connected multi-edge topology must succeed.", failures);

            Check(WorldTopology.TryCreate(
                      new[] { gamma, alpha, beta },
                      new[]
                      {
                          new SectorConnection("railway_01", gamma, beta),
                          new SectorConnection("river_01", alpha, beta),
                          new SectorConnection("road_01", beta, alpha)
                      },
                      out WorldTopology reordered,
                      out WorldTopologyValidationResult reorderedValidation) && reorderedValidation.IsValid &&
                  first != null && reordered != null &&
                  first.CanonicalDescription == reordered.CanonicalDescription &&
                  first.CanonicalHash == reordered.CanonicalHash,
                "Canonical topology evidence must ignore insertion and endpoint order.", failures);

            Check(first != null && first.Connections.Count == 3 &&
                  HasConnection(first, "road_01") && HasConnection(first, "river_01"),
                "Multiple distinct connections between the same sector pair must remain representable.", failures);

            CheckValidationFailure(
                new[] { alpha, alpha },
                Array.Empty<SectorConnection>(),
                "Duplicate SectorId",
                "Duplicate sectors", failures);
            CheckValidationFailure(
                new[] { alpha, beta },
                new[] { new SectorConnection("missing_target", alpha, missing) },
                "references missing",
                "Missing connection endpoint", failures);
            CheckValidationFailure(
                new[] { alpha, beta },
                new[]
                {
                    new SectorConnection("duplicate_connection", alpha, beta),
                    new SectorConnection("duplicate_connection", beta, alpha)
                },
                "Duplicate connection key",
                "Duplicate connection IDs", failures);
            CheckValidationFailure(
                new[] { alpha },
                new[] { new SectorConnection("self_connection", alpha, alpha) },
                "same SectorId",
                "Self connection", failures);
            CheckValidationFailure(
                new[] { alpha, beta, gamma },
                new[] { new SectorConnection("road_01", alpha, beta) },
                "disconnected",
                "Disconnected topology", failures);

            Check(first != null && first.CanonicalHash == ExpectedTopologyHash,
                $"Fresh-run golden topology hash drifted. Expected {ExpectedTopologyHash}, " +
                $"got {first?.CanonicalHash ?? "<NULL>"}.", failures);

            WorldId one = WorldId.Parse("world_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            WorldId two = WorldId.Parse("world_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
            WorldTopology oneTopology = BuildIdentityIndependentTopology(one, context);
            WorldTopology twoTopology = BuildIdentityIndependentTopology(two, context);
            Check(one != two && oneTopology != null && twoTopology != null &&
                  oneTopology.CanonicalHash == twoTopology.CanonicalHash,
                "WorldId must not alter deterministic SectorIds or topology evidence.", failures);
        }

        private static DeterministicDomainKey DeriveForWorldIdentityProbe(
            WorldId ignoredIdentity,
            WorldGenerationContext context)
        {
            if (!ignoredIdentity.IsValid)
                throw new ArgumentException("Probe requires a valid WorldId.", nameof(ignoredIdentity));
            return WorldDeterminism.DerivePassDomainKey(
                context.WorldSeed, "worldgen_1_0_0", "world", "topology");
        }

        private static WorldTopology BuildIdentityIndependentTopology(
            WorldId ignoredIdentity,
            WorldGenerationContext context)
        {
            if (!ignoredIdentity.IsValid)
                throw new ArgumentException("Probe requires a valid WorldId.", nameof(ignoredIdentity));
            SectorId alpha = Sector(context, "sector_alpha");
            SectorId beta = Sector(context, "sector_beta");
            WorldTopology.TryCreate(
                new[] { alpha, beta },
                new[] { new SectorConnection("road_01", alpha, beta) },
                out WorldTopology topology,
                out _);
            return topology;
        }

        private static SectorId Sector(WorldGenerationContext context, string passKey)
        {
            return SectorId.FromDeterministicDomain(
                WorldDeterminism.DerivePassDomainKey(
                    context.WorldSeed, "worldgen_1_0_0", "topology", passKey));
        }

        private static bool HasConnection(WorldTopology topology, string key)
        {
            for (int index = 0; index < topology.Connections.Count; index++)
            {
                if (topology.Connections[index].ConnectionKey == key)
                    return true;
            }
            return false;
        }

        private static void CheckValidationFailure(
            IEnumerable<SectorId> sectors,
            IEnumerable<SectorConnection> connections,
            string expectedFragment,
            string label,
            List<string> failures)
        {
            bool created = WorldTopology.TryCreate(
                sectors, connections, out WorldTopology topology,
                out WorldTopologyValidationResult validation);
            bool found = false;
            for (int index = 0; index < validation.Errors.Count; index++)
            {
                if (validation.Errors[index].IndexOf(expectedFragment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    found = true;
                    break;
                }
            }
            Check(!created && topology == null && !validation.IsValid && found,
                $"{label} must fail with deterministic actionable validation. Actual: {validation.Description}",
                failures);
        }

        private static void Check(bool condition, string failure, List<string> failures)
        {
            if (!condition)
                failures.Add(failure);
        }
    }
}
