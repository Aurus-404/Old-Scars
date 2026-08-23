using System;
using System.Collections.Generic;
using System.IO;
using OldScars.Core.Data.Loading;
using OldScars.Core.Data.Validation;
using UnityEditor;
using UnityEngine;

namespace OldScars.Editor
{
    public static class ContentSourceProvenanceDiagnostics
    {
        public static void Run()
        {
            var failures = new List<string>();
            ValidateRealCore(failures);

            string root = Path.Combine(
                Path.GetTempPath(),
                "OldScars_ContentSourceProvenance_" + Guid.NewGuid().ToString("N"));
            try
            {
                ValidateIdentityOrderingAndFolderRename(root, failures);
                ValidateIdentityConflicts(root, failures);
                ValidateNamespaceOwnershipAndReferences(root, failures);
                ValidateProvenanceInputs(root, failures);
                ValidateManifestFailures(root, failures);
            }
            catch (Exception exception)
            {
                failures.Add($"Diagnostic fixture threw {exception.GetType().Name}: {exception.Message}");
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }

            if (failures.Count > 0)
            {
                string message =
                    "Minimum Content Source Identity & Provenance Foundation: FAIL\n- " +
                    string.Join("\n- ", failures);
                Debug.LogError(message);
                throw new InvalidOperationException(message);
            }

            Debug.Log(
                "Minimum Content Source Identity & Provenance Foundation: PASS\n" +
                "- real Core manifest, definitions and DataValidator\n" +
                "- manifest identity, namespace ownership and deterministic source order\n" +
                "- folder/path/enumeration-independent SHA-256 provenance\n" +
                "- recognized byte changes tracked; unrelated files ignored\n" +
                "- duplicate/reserved/out-of-namespace failures actionable\n" +
                "- explicit cross-namespace references preserved\n" +
                "- temporary fixtures removed");
        }

        private static void ValidateRealCore(List<string> failures)
        {
            string modsRoot = Path.Combine(Application.streamingAssetsPath, "Mods");
            LoadResult result = Load(modsRoot, true);
            Check(result.Report.ErrorCount == 0,
                "Real Core must load through its manifest and pass DataValidator without errors.", failures);
            Check(result.Set != null && result.Set.Sources.Count == 1,
                "Real Core load must publish exactly one source in the validated content set.", failures);

            LoadedContentSource core = FindSource(result.Set, GameDataLoader.OfficialCoreSourceId);
            Check(core != null && core.IsOfficialCore && core.OwnedNamespace == "core" &&
                  core.Version == "1.0.0" && core.RecognizedInputs.Count > 0,
                "Real Core metadata must come from manifest.json and include recognized inputs.", failures);
        }

        private static void ValidateIdentityOrderingAndFolderRename(string root, List<string> failures)
        {
            string mods = NewModsRoot(root, "identity_rename");
            WriteManifest(Path.Combine(mods, "OfficialData"), "old_scars_core", "core", "1.0.0");
            string zFolder = Path.Combine(mods, "A_PhysicalFolder");
            WriteManifest(zFolder, "z_source", "z_content", "2.1.0");
            WriteStorage(zFolder, "z_content:test_storage", "storage.json");
            string aFolder = Path.Combine(mods, "Z_PhysicalFolder");
            WriteManifest(aFolder, "a_source", "a_content", "3");
            WriteStorage(aFolder, "a_content:test_storage", "storage.json");

            LoadResult before = Load(mods, false);
            Check(before.Report.ErrorCount == 0 && before.Set != null,
                "Valid manifest-backed sources must load successfully.", failures);
            Check(before.Set != null && before.Set.Sources.Count == 3 &&
                  before.Set.Sources[0].SourceId == "old_scars_core" &&
                  before.Set.Sources[1].SourceId == "a_source" &&
                  before.Set.Sources[2].SourceId == "z_source",
                "Source order must be official Core first, then canonical source_id order, not folder order.", failures);

            LoadedContentSource beforeSource = FindSource(before.Set, "z_source");
            string renamedFolder = Path.Combine(mods, "Renamed_Without_Identity_Effect");
            Directory.Move(zFolder, renamedFolder);
            LoadResult after = Load(mods, false);
            LoadedContentSource afterSource = FindSource(after.Set, "z_source");
            Check(after.Report.ErrorCount == 0 && after.Set != null &&
                  beforeSource != null && afterSource != null &&
                  beforeSource.SourceId == afterSource.SourceId &&
                  beforeSource.OwnedNamespace == afterSource.OwnedNamespace &&
                  beforeSource.Version == afterSource.Version &&
                  beforeSource.ProvenanceFingerprint == afterSource.ProvenanceFingerprint &&
                  before.Set.ProvenanceFingerprint == after.Set.ProvenanceFingerprint &&
                  before.Set.CanonicalDescription == after.Set.CanonicalDescription,
                "Renaming a source folder must not change identity, order, description or provenance.", failures);
        }

        private static void ValidateIdentityConflicts(string root, List<string> failures)
        {
            string duplicateId = NewModsRoot(root, "duplicate_source_id");
            WriteManifest(Path.Combine(duplicateId, "Core"), "old_scars_core", "core", "1");
            WriteManifest(Path.Combine(duplicateId, "One"), "duplicate_source", "one", "1");
            WriteManifest(Path.Combine(duplicateId, "Two"), "duplicate_source", "two", "1");
            LoadResult duplicateIdResult = Load(duplicateId, false);
            Check(HasError(duplicateIdResult.Report, "Duplicate content source_id 'duplicate_source'") &&
                  duplicateIdResult.Loader.Database.ItemCount == 0,
                "Duplicate source_id must fail before any definitions register.", failures);

            string duplicateNamespace = NewModsRoot(root, "duplicate_namespace");
            WriteManifest(Path.Combine(duplicateNamespace, "Core"), "old_scars_core", "core", "1");
            WriteManifest(Path.Combine(duplicateNamespace, "One"), "source_one", "shared", "1");
            WriteManifest(Path.Combine(duplicateNamespace, "Two"), "source_two", "shared", "1");
            LoadResult duplicateNamespaceResult = Load(duplicateNamespace, false);
            Check(HasError(duplicateNamespaceResult.Report, "Duplicate owned namespace 'shared'") &&
                  duplicateNamespaceResult.Loader.Database.ItemCount == 0,
                "Duplicate namespace ownership must fail before definitions register.", failures);

            string coreClaim = NewModsRoot(root, "external_core_claim");
            WriteManifest(Path.Combine(coreClaim, "Core"), "old_scars_core", "core", "1");
            WriteManifest(Path.Combine(coreClaim, "External"), "external_source", "core", "1");
            LoadResult coreClaimResult = Load(coreClaim, false);
            Check(HasError(coreClaimResult.Report, "external sources may claim neither"),
                "An external source claiming namespace 'core' must fail actionably.", failures);

            string coreIdentityClaim = NewModsRoot(root, "external_core_identity_claim");
            WriteManifest(Path.Combine(coreIdentityClaim, "Core"), "old_scars_core", "core", "1");
            WriteManifest(Path.Combine(coreIdentityClaim, "External"), "old_scars_core", "external", "1");
            LoadResult coreIdentityResult = Load(coreIdentityClaim, false);
            Check(HasError(coreIdentityResult.Report, "external sources may claim neither"),
                "An external source claiming the reserved official Core source_id must fail actionably.", failures);
        }

        private static void ValidateNamespaceOwnershipAndReferences(string root, List<string> failures)
        {
            string outside = NewModsRoot(root, "outside_namespace");
            WriteManifest(Path.Combine(outside, "Core"), "old_scars_core", "core", "1");
            string owned = Path.Combine(outside, "Owned");
            WriteManifest(owned, "owned_source", "owned", "1");
            WriteStorage(owned, "other:test_storage", "storage.json");
            LoadResult outsideResult = Load(outside, false);
            Check(HasError(outsideResult.Report, "declared outside its owned namespace 'owned'"),
                "A definition outside its source-owned namespace must fail.", failures);

            string references = NewModsRoot(root, "cross_namespace_reference");
            string core = Path.Combine(references, "Core");
            WriteManifest(core, "old_scars_core", "core", "1");
            WriteTags(core);
            string external = Path.Combine(references, "External");
            WriteManifest(external, "external_source", "external", "1");
            WriteStorage(external, "external:test_storage", "storage.json");
            WriteItem(core, "test_item", "external:test_storage", "item.json");
            LoadResult referenceResult = Load(references, true);
            Check(referenceResult.Report.ErrorCount == 0 &&
                  referenceResult.Loader.Database.GetItem("core:test_item") != null &&
                  referenceResult.Loader.Database.GetItemStorageProfile("external:test_storage") != null &&
                  referenceResult.Loader.Database.GetItem("core:test_item").owned_storage_profile_id ==
                  "external:test_storage",
                "A valid explicit cross-namespace reference must remain allowed and resolvable.", failures);
        }

        private static void ValidateProvenanceInputs(string root, List<string> failures)
        {
            string bytesRoot = NewModsRoot(root, "recognized_bytes");
            WriteManifest(Path.Combine(bytesRoot, "Core"), "old_scars_core", "core", "1");
            string source = Path.Combine(bytesRoot, "Source");
            WriteManifest(source, "bytes_source", "bytes", "1");
            string recognizedFile = WriteStorage(source, "bytes:test_storage", "storage.json");
            LoadResult before = Load(bytesRoot, false);
            string beforeFingerprint = FindSource(before.Set, "bytes_source")?.ProvenanceFingerprint;
            File.AppendAllText(recognizedFile, Environment.NewLine);
            LoadResult after = Load(bytesRoot, false);
            string afterFingerprint = FindSource(after.Set, "bytes_source")?.ProvenanceFingerprint;
            Check(before.Report.ErrorCount == 0 && after.Report.ErrorCount == 0 &&
                  !string.IsNullOrEmpty(beforeFingerprint) && beforeFingerprint != afterFingerprint,
                "Changing recognized JSON bytes must change per-source provenance.", failures);

            string unrelatedRoot = NewModsRoot(root, "unrecognized_files");
            WriteManifest(Path.Combine(unrelatedRoot, "Core"), "old_scars_core", "core", "1");
            string unrelatedSource = Path.Combine(unrelatedRoot, "Source");
            WriteManifest(unrelatedSource, "unrelated_source", "unrelated", "1");
            WriteStorage(unrelatedSource, "unrelated:test_storage", "storage.json");
            LoadResult withoutUnrelated = Load(unrelatedRoot, false);
            Directory.CreateDirectory(Path.Combine(unrelatedSource, "notes"));
            File.WriteAllText(Path.Combine(unrelatedSource, "README.md"), "not authoritative");
            File.WriteAllText(Path.Combine(unrelatedSource, "notes", "ignored.json"), "{\"ignored\":true}");
            LoadResult withUnrelated = Load(unrelatedRoot, false);
            Check(withoutUnrelated.Set != null && withUnrelated.Set != null &&
                  withoutUnrelated.Set.ProvenanceFingerprint == withUnrelated.Set.ProvenanceFingerprint,
                "Adding unrecognized files must not change provenance.", failures);

            string orderOne = NewModsRoot(root, "enumeration_order_one");
            string orderTwo = NewModsRoot(root, "enumeration_order_two");
            CreateEnumerationFixture(orderOne, false);
            CreateEnumerationFixture(orderTwo, true);
            LoadResult firstOrder = Load(orderOne, false);
            LoadResult secondOrder = Load(orderTwo, false);
            Check(firstOrder.Set != null && secondOrder.Set != null &&
                  firstOrder.Set.ProvenanceFingerprint == secondOrder.Set.ProvenanceFingerprint &&
                  firstOrder.Set.CanonicalDescription == secondOrder.Set.CanonicalDescription,
                "Different file creation/enumeration order must produce identical provenance.", failures);
        }

        private static void ValidateManifestFailures(string root, List<string> failures)
        {
            string missing = NewModsRoot(root, "missing_manifest");
            Directory.CreateDirectory(Path.Combine(missing, "NoManifest"));
            LoadResult missingResult = Load(missing, false);
            Check(HasError(missingResult.Report, "missing required root manifest 'manifest.json'") &&
                  HasError(missingResult.Report, "never inferred from the folder name"),
                "A missing manifest must fail with actionable identity guidance.", failures);

            string malformed = NewModsRoot(root, "malformed_manifest");
            string malformedSource = Path.Combine(malformed, "Broken");
            Directory.CreateDirectory(malformedSource);
            File.WriteAllText(Path.Combine(malformedSource, GameDataLoader.ManifestFileName), "{not json");
            LoadResult malformedResult = Load(malformed, false);
            Check(HasError(malformedResult.Report, "Failed to parse content source manifest"),
                "A malformed manifest must fail actionably.", failures);

            ValidateRequiredManifestField(root, "missing_source_id",
                "{\"namespace\":\"core\",\"version\":\"1\"}", "'source_id' is invalid", failures);
            ValidateRequiredManifestField(root, "invalid_source_id",
                "{\"source_id\":\"Bad-Source\",\"namespace\":\"bad_source\",\"version\":\"1\"}",
                "'source_id' is invalid", failures);
            ValidateRequiredManifestField(root, "missing_namespace",
                "{\"source_id\":\"some_source\",\"version\":\"1\"}", "'namespace' is invalid", failures);
            ValidateRequiredManifestField(root, "invalid_namespace",
                "{\"source_id\":\"some_source\",\"namespace\":\"Bad-Namespace\",\"version\":\"1\"}",
                "'namespace' is invalid", failures);
            ValidateRequiredManifestField(root, "missing_version",
                "{\"source_id\":\"some_source\",\"namespace\":\"some_source\"}",
                "'version' is invalid", failures);
        }

        private static void ValidateRequiredManifestField(
            string root,
            string caseName,
            string manifestJson,
            string expectedError,
            List<string> failures)
        {
            string mods = NewModsRoot(root, caseName);
            string source = Path.Combine(mods, "Source");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, GameDataLoader.ManifestFileName), manifestJson);
            LoadResult result = Load(mods, false);
            Check(HasError(result.Report, expectedError),
                $"Manifest case '{caseName}' must report {expectedError}.", failures);
        }

        private static void CreateEnumerationFixture(string mods, bool reverseCreation)
        {
            WriteManifest(Path.Combine(mods, "Core"), "old_scars_core", "core", "1");
            string source = Path.Combine(mods, "Source");
            WriteManifest(source, "order_source", "order", "1");
            if (reverseCreation)
            {
                WriteStorage(source, "order:b", "b.json");
                WriteStorage(source, "order:a", "a.json");
            }
            else
            {
                WriteStorage(source, "order:a", "a.json");
                WriteStorage(source, "order:b", "b.json");
            }
        }

        private static LoadResult Load(string modsRoot, bool validate)
        {
            var report = new DataLoadReport();
            var loader = new GameDataLoader(modsRoot, report);
            loader.LoadAll();
            if (validate && !report.HasErrors)
                new DataValidator(loader.Database, loader.Tags, report).Validate();

            LoadedContentSet set = null;
            if (!report.HasErrors)
                loader.TryBuildLoadedContentSet(out set);
            return new LoadResult(loader, report, set);
        }

        private static string NewModsRoot(string root, string caseName)
        {
            string result = Path.Combine(root, caseName, "Mods");
            Directory.CreateDirectory(result);
            return result;
        }

        private static void WriteManifest(string sourceRoot, string sourceId, string ownedNamespace, string version)
        {
            Directory.CreateDirectory(sourceRoot);
            File.WriteAllText(
                Path.Combine(sourceRoot, GameDataLoader.ManifestFileName),
                $"{{\"source_id\":\"{sourceId}\",\"namespace\":\"{ownedNamespace}\",\"version\":\"{version}\"}}");
        }

        private static string WriteStorage(string sourceRoot, string id, string fileName)
        {
            string directory = Path.Combine(sourceRoot, "item_storage_profiles");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, fileName);
            File.WriteAllText(path,
                "{\"item_storage_profiles\":[{\"type\":\"item_storage_profile\",\"id\":\"" + id +
                "\",\"display_name\":\"Test\",\"width\":1,\"height\":1}]}");
            return path;
        }

        private static void WriteItem(string sourceRoot, string id, string storageId, string fileName)
        {
            string directory = Path.Combine(sourceRoot, "items");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, fileName),
                "{\"items\":[{\"type\":\"item\",\"id\":\"" + id +
                "\",\"display\":{\"name\":\"Test\"},\"tags\":[\"item\"],\"max_stack\":1," +
                "\"physical\":{\"weight_kg\":1,\"volume_l\":1,\"condition_max\":100}," +
                "\"owned_storage_profile_id\":\"" + storageId + "\"}]}");
        }

        private static void WriteTags(string sourceRoot)
        {
            string directory = Path.Combine(sourceRoot, "tags");
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, "tags.json"),
                "{\"tags\":[{\"id\":\"item\",\"description\":\"Diagnostic item\"}]}");
        }

        private static LoadedContentSource FindSource(LoadedContentSet set, string sourceId)
        {
            if (set == null)
                return null;
            foreach (LoadedContentSource source in set.Sources)
            {
                if (string.Equals(source.SourceId, sourceId, StringComparison.Ordinal))
                    return source;
            }
            return null;
        }

        private static bool HasError(DataLoadReport report, string fragment)
        {
            foreach (string error in report.Errors)
            {
                if (error.IndexOf(fragment, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        private static void Check(bool condition, string failure, List<string> failures)
        {
            if (!condition)
                failures.Add(failure);
        }

        private sealed class LoadResult
        {
            internal LoadResult(GameDataLoader loader, DataLoadReport report, LoadedContentSet set)
            {
                Loader = loader;
                Report = report;
                Set = set;
            }

            internal GameDataLoader Loader { get; }
            internal DataLoadReport Report { get; }
            internal LoadedContentSet Set { get; }
        }
    }
}
