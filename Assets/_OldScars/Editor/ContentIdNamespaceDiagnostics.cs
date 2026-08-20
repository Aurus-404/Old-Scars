using System;
using System.Collections.Generic;
using System.IO;
using OldScars.Core.Data;
using OldScars.Core.Data.Loading;
using UnityEditor;
using UnityEngine;

namespace OldScars.Editor
{
    public static class ContentIdNamespaceDiagnostics
    {
        private const string MenuPath = "Old Scars/Diagnostics/Content IDs/Run Namespace Foundation";

        public static void Run()
        {
            var failures = new List<string>();
            ValidateParserAndResolution(failures);
            ValidateIsolatedLoaderFixture(failures);

            if (failures.Count > 0)
            {
                string message = "Global Content ID Namespace Foundation: FAIL\n- " + string.Join("\n- ", failures);
                Debug.LogError(message);
                throw new InvalidOperationException(message);
            }

            Debug.Log(
                "Global Content ID Namespace Foundation: PASS\n" +
                "- canonical parsing and invalid diagnostics\n" +
                "- Core-only legacy qualification\n" +
                "- canonical GameDatabase identity\n" +
                "- core:test_item and test_namespace:test_item coexist\n" +
                "- explicit cross-namespace reference preserved\n" +
                "- temporary fixture removed");
        }

        private static bool ValidateRun()
        {
            return !EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static void ValidateParserAndResolution(List<string> failures)
        {
            Check(ContentId.TryParse("core:test_item", out ContentId core, out _) &&
                  core.Namespace == "core" && core.LocalId == "test_item" && core.Canonical == "core:test_item",
                "core:test_item must parse canonically.", failures);
            Check(ContentId.TryParse("test_namespace:test_item", out ContentId external, out _) &&
                  external.Namespace == "test_namespace" && external.LocalId == "test_item",
                "test_namespace:test_item must parse canonically.", failures);

            string[] invalid =
            {
                "test_item", "Core:test_item", "core:TestItem", "core:test-item",
                "core:", ":test_item", "core:test:item", " core:test_item"
            };
            for (int index = 0; index < invalid.Length; index++)
            {
                Check(!ContentId.TryParse(invalid[index], out _, out string error) && !string.IsNullOrWhiteSpace(error),
                    $"Invalid ID '{invalid[index]}' must be rejected with a reason.", failures);
            }

            Check(ContentId.TryResolveLegacyCore(
                      "test_item", out ContentId legacyCore, out bool usedLegacy, out _) &&
                  usedLegacy && legacyCore == core,
                "Core legacy test_item must resolve to the same canonical identity as core:test_item.", failures);
            Check(!ContentId.TryResolve(
                      "test_item", ContentId.CoreNamespace, false, out _, out _, out string externalError) &&
                  !string.IsNullOrWhiteSpace(externalError),
                "A non-Core source must reject an unqualified Global Content ID.", failures);
        }

        private static void ValidateIsolatedLoaderFixture(List<string> failures)
        {
            string root = Path.Combine(Path.GetTempPath(), "OldScars_ContentId_" + Guid.NewGuid().ToString("N"));
            string mods = Path.Combine(root, "Mods");
            try
            {
                string coreItems = Path.Combine(mods, "Core", "items");
                string externalItems = Path.Combine(mods, "TestNamespaceMod", "items");
                string externalStorage = Path.Combine(mods, "TestNamespaceMod", "item_storage_profiles");
                Directory.CreateDirectory(coreItems);
                Directory.CreateDirectory(externalItems);
                Directory.CreateDirectory(externalStorage);

                File.WriteAllText(Path.Combine(coreItems, "items.json"),
                    "{\"items\":[{\"type\":\"item\",\"id\":\"test_item\"," +
                    "\"owned_storage_profile_id\":\"test_namespace:test_storage\"}]}");
                File.WriteAllText(Path.Combine(externalItems, "items.json"),
                    "{\"items\":[{\"type\":\"item\",\"id\":\"test_namespace:test_item\"}]}");
                File.WriteAllText(Path.Combine(externalStorage, "storage.json"),
                    "{\"item_storage_profiles\":[{\"type\":\"item_storage_profile\"," +
                    "\"id\":\"test_namespace:test_storage\"}]}");

                var report = new DataLoadReport();
                var loader = new GameDataLoader(mods, report);
                loader.LoadAll();

                Check(report.ErrorCount == 0,
                    "The isolated Core/external loader fixture must register without errors.", failures);
                Check(report.WarningCount > 0,
                    "The Core legacy definition must emit a migration warning.", failures);
                Check(loader.Database.ItemCount == 2,
                    "The fixture must register exactly two ItemDefinitions.", failures);

                var coreItem = loader.Database.GetItem("core:test_item");
                var legacyAlias = loader.Database.GetItem("test_item");
                var externalItem = loader.Database.GetItem("test_namespace:test_item");
                Check(coreItem != null && object.ReferenceEquals(coreItem, legacyAlias),
                    "Legacy test_item and core:test_item must resolve to one database object.", failures);
                Check(externalItem != null && !object.ReferenceEquals(coreItem, externalItem),
                    "core:test_item and test_namespace:test_item must coexist without collision.", failures);
                Check(coreItem != null && coreItem.owned_storage_profile_id == "test_namespace:test_storage" &&
                      loader.Database.GetItemStorageProfile(coreItem.owned_storage_profile_id) != null,
                    "An explicit cross-namespace definition reference must remain canonical and resolve.", failures);
            }
            catch (Exception exception)
            {
                failures.Add($"Isolated loader fixture threw {exception.GetType().Name}: {exception.Message}");
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        private static void Check(bool condition, string failure, List<string> failures)
        {
            if (!condition)
                failures.Add(failure);
        }
    }
}
