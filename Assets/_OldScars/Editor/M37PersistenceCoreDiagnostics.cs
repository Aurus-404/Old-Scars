using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OldScars.Core.Persistence;
using UnityEditor;
using UnityEngine;

namespace OldScars.Editor
{
    public static class M37PersistenceCoreDiagnostics
    {
        private const string MenuPath = "Old Scars/Diagnostics/M37.0/Run Persistence Core Diagnostics";

        public static void RunPersistenceCoreDiagnostics()
        {
            var failures = new List<string>();
            string tempBase = Path.GetFullPath(Path.GetTempPath());
            string root = Path.Combine(tempBase, "OldScars_M37_0_" + Guid.NewGuid().ToString("N"));
            try
            {
                Check(Path.GetFullPath(root).StartsWith(tempBase, StringComparison.OrdinalIgnoreCase),
                    "Diagnostic root must remain under the system temp directory.", failures);
                RunScenarios(root, failures);
            }
            catch (Exception ex)
            {
                failures.Add("Unexpected diagnostic exception: " + ex);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(root))
                        Directory.Delete(root, true);
                }
                catch (Exception ex)
                {
                    failures.Add("Diagnostic cleanup failed: " + ex.Message);
                }
            }

            Check(!Directory.Exists(root), "Diagnostic root remained after cleanup.", failures);
            string report = failures.Count == 0
                ? "M37.0 Persistence Core Diagnostics: PASS"
                : "M37.0 Persistence Core Diagnostics: FAIL\n- " + string.Join("\n- ", failures);
            if (failures.Count > 0)
            {
                Debug.LogError(report);
                throw new InvalidOperationException(report);
            }

            Debug.Log(report);
        }

        private static bool CanRun()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;
        }

        private static void RunScenarios(string root, List<string> failures)
        {
            var serializer = new PersistenceSerializer();
            var store = new PersistenceFileStore(root, serializer);
            var original = new JObject
            {
                ["label"] = "first",
                ["count"] = 7,
                ["enabled"] = true,
                ["nested"] = new JObject { ["values"] = new JArray(1, 2, 3) }
            };
            var updated = new JObject { ["label"] = "second", ["count"] = 9 };

            PersistenceFailureCode serializeCode = serializer.Serialize(original, out string json, out string serializeFailure);
            Check(serializeCode == PersistenceFailureCode.Success, "V1 serialization failed: " + serializeFailure, failures);
            PersistenceDocumentResult document = serializer.Deserialize(json);
            Check(document.Success && document.FormatVersion == 1 && JToken.DeepEquals(document.Payload, original),
                "V1 envelope serialize/deserialize did not preserve version and payload.", failures);

            PersistenceWriteResult firstWrite = store.Write("round_trip", original);
            PersistenceLoadResult firstRead = store.Read("round_trip");
            Check(firstWrite.Success && firstRead.Success && JToken.DeepEquals(firstRead.Payload, original),
                "First write/read failed or changed the payload.", failures);

            PersistenceWriteResult overwrite = store.Write("round_trip", updated);
            PersistenceLoadResult currentRead = store.Read("round_trip");
            Check(overwrite.Success && overwrite.BackupCreated && currentRead.Success && JToken.DeepEquals(currentRead.Payload, updated),
                "Overwrite did not commit the updated payload with a backup.", failures);

            store.TryGetPaths("round_trip", out string primary, out string backup, out _);
            PersistenceDocumentResult backupDocument = serializer.Deserialize(File.ReadAllText(backup));
            Check(File.Exists(backup) && backupDocument.Success && JToken.DeepEquals(backupDocument.Payload, original),
                "Backup did not preserve the previous payload.", failures);

            File.WriteAllText(primary, "{ corrupt primary");
            PersistenceLoadResult recovered = store.Read("round_trip");
            Check(recovered.Success && recovered.RecoveryAttempted && recovered.RecoverySucceeded &&
                  recovered.Source == "Backup" && JToken.DeepEquals(recovered.Payload, original),
                "Corrupt primary did not recover the valid backup.", failures);

            WriteRaw(store, "both_invalid", "{ bad primary", "{ bad backup");
            PersistenceLoadResult bothInvalid = store.Read("both_invalid");
            Check(bothInvalid.FailureCode == PersistenceFailureCode.RecoveryFailed && !bothInvalid.RecoverySucceeded,
                "Invalid primary and backup did not produce RecoveryFailed.", failures);

            WriteRaw(store, "future_version", Envelope(PersistenceSerializer.CurrentFormatVersion + 1, original));
            Check(store.Read("future_version").FailureCode == PersistenceFailureCode.FutureVersionUnsupported,
                "Future format version was not rejected explicitly.", failures);

            WriteRaw(store, "old_version", Envelope(0, original));
            Check(store.Read("old_version").FailureCode == PersistenceFailureCode.MigrationUnavailable,
                "Old format without migration did not produce MigrationUnavailable.", failures);

            PersistenceWriteResult invalidSlot = store.Write("../escape", original);
            Check(invalidSlot.FailureCode == PersistenceFailureCode.InvalidSlotId &&
                  !File.Exists(Path.Combine(root, "escape.json")),
                "Invalid slot/path was not rejected safely.", failures);

            store.TryGetPaths("temp_cleanup", out _, out _, out string staleTemp);
            Directory.CreateDirectory(store.SavesDirectory);
            File.WriteAllText(staleTemp, "stale temp");
            PersistenceWriteResult tempWrite = store.Write("temp_cleanup", original);
            Check(tempWrite.Success && !File.Exists(staleTemp), "Stale temp file was not cleaned.", failures);

            PersistenceWriteResult exactWrite = store.Write("exact_payload", original);
            PersistenceLoadResult exactRead = store.Read("exact_payload");
            Check(exactWrite.Success && exactRead.Success && JToken.DeepEquals(exactRead.Payload, original),
                "Infrastructure round-trip changed the diagnostic payload.", failures);
            Check(Directory.GetFiles(root, "*.tmp", SearchOption.AllDirectories).Length == 0,
                "Temporary files remained before diagnostic cleanup.", failures);
        }

        private static string Envelope(int version, JToken payload)
        {
            return new JObject { ["formatVersion"] = version, ["payload"] = payload }.ToString(Formatting.None);
        }

        private static void WriteRaw(PersistenceFileStore store, string slot, string primaryJson, string backupJson = null)
        {
            store.TryGetPaths(slot, out string primary, out string backup, out _);
            Directory.CreateDirectory(store.SavesDirectory);
            File.WriteAllText(primary, primaryJson);
            if (backupJson != null)
                File.WriteAllText(backup, backupJson);
        }

        private static void Check(bool condition, string failure, List<string> failures)
        {
            if (!condition)
                failures.Add(failure);
        }
    }
}
