using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace OldScars.Core.Persistence
{
    public sealed class PersistenceWriteResult
    {
        internal PersistenceWriteResult(PersistenceFailureCode code, string failure, bool backupCreated, string strategy)
        {
            FailureCode = code;
            Failure = failure;
            BackupCreated = backupCreated;
            WriteStrategy = strategy;
        }

        public bool Success => FailureCode == PersistenceFailureCode.Success;
        public PersistenceFailureCode FailureCode { get; }
        public string Failure { get; }
        public bool BackupCreated { get; }
        public string WriteStrategy { get; }
    }

    public sealed class PersistenceLoadResult
    {
        internal PersistenceLoadResult(PersistenceFailureCode code, string failure, JToken payload,
            int formatVersion, string source, bool recoveryAttempted, bool recoverySucceeded,
            PersistenceFailureCode primaryFailureCode)
        {
            FailureCode = code;
            Failure = failure;
            Payload = payload;
            FormatVersion = formatVersion;
            Source = source;
            RecoveryAttempted = recoveryAttempted;
            RecoverySucceeded = recoverySucceeded;
            PrimaryFailureCode = primaryFailureCode;
        }

        public bool Success => FailureCode == PersistenceFailureCode.Success;
        public PersistenceFailureCode FailureCode { get; }
        public string Failure { get; }
        public JToken Payload { get; }
        public int FormatVersion { get; }
        public string Source { get; }
        public bool RecoveryAttempted { get; }
        public bool RecoverySucceeded { get; }
        public PersistenceFailureCode PrimaryFailureCode { get; }
    }

    public sealed class PersistenceFileStore
    {
        private static readonly Regex ValidSlotId = new Regex(
            "^[a-z0-9]+(?:_[a-z0-9]+)*$",
            RegexOptions.CultureInvariant);

        private readonly PersistenceSerializer serializer;

        public PersistenceFileStore()
            : this(Application.persistentDataPath)
        {
        }

        public PersistenceFileStore(string rootPath, PersistenceSerializer serializer = null)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
                throw new ArgumentException("Persistence root path is required.", nameof(rootPath));

            SavesDirectory = Path.Combine(Path.GetFullPath(rootPath), "Saves");
            this.serializer = serializer ?? new PersistenceSerializer();
        }

        public string SavesDirectory { get; }

        public PersistenceWriteResult Write(string slotId, JToken payload)
        {
            if (!TryGetPaths(slotId, out string primary, out string backup, out string temp))
                return WriteFailure(slotId, null, null, null, PersistenceFailureCode.InvalidSlotId, "Slot id is invalid.");

            PersistenceFailureCode serializationCode = serializer.Serialize(payload, out string json, out string serializationFailure);
            if (serializationCode != PersistenceFailureCode.Success)
                return WriteFailure(slotId, primary, backup, temp, serializationCode, serializationFailure);

            try
            {
                Directory.CreateDirectory(SavesDirectory);
                if (File.Exists(temp))
                    File.Delete(temp);
                WriteTempFile(temp, json);

                bool backupCreated = false;
                string strategy;
                if (!File.Exists(primary))
                {
                    File.Move(temp, primary);
                    strategy = "SameDirectoryMove";
                }
                else
                {
                    try
                    {
                        File.Replace(temp, primary, backup);
                        backupCreated = true;
                        strategy = "File.Replace";
                    }
                    catch (Exception ex) when (ex is PlatformNotSupportedException || ex is NotSupportedException)
                    {
                        PromoteWithFallback(primary, backup, temp);
                        backupCreated = File.Exists(backup);
                        strategy = "SameDirectoryMoveFallback";
                        Debug.LogWarning($"[Persistence][WRITE_FALLBACK]\nSlot: {slotId}\nReason: {ex.Message}\nActionTaken: preserved primary as backup before promoting temp");
                    }
                }

                if (File.Exists(temp))
                    File.Delete(temp);
                Debug.Log($"[Persistence][WRITE_COMMIT]\nSlot: {slotId}\nFormatVersion: {PersistenceSerializer.CurrentFormatVersion}\nBackupCreated: {backupCreated}\nStrategy: {strategy}");
                return new PersistenceWriteResult(PersistenceFailureCode.Success, null, backupCreated, strategy);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                TryDeleteTemp(temp);
                return WriteFailure(slotId, primary, backup, temp, PersistenceFailureCode.IoFailure, ex.Message);
            }
        }

        public PersistenceLoadResult Read(string slotId)
        {
            if (!TryGetPaths(slotId, out string primary, out string backup, out string temp))
                return LoadFailure(slotId, null, null, null, PersistenceFailureCode.InvalidSlotId, "Slot id is invalid.");

            bool primaryExists = File.Exists(primary);
            bool backupExists = File.Exists(backup);
            if (!primaryExists && !backupExists)
            {
                return new PersistenceLoadResult(PersistenceFailureCode.SaveNotFound,
                    "Neither primary nor backup save exists.", null, 0, null, false, false,
                    PersistenceFailureCode.SaveNotFound);
            }

            PersistenceDocumentResult primaryResult = primaryExists
                ? ReadDocument(primary)
                : new PersistenceDocumentResult(PersistenceFailureCode.SaveNotFound, "Primary save does not exist.", 0, null);
            if (primaryResult.Success)
                return Loaded(primaryResult, "Primary", false, false, PersistenceFailureCode.Success);

            if (primaryResult.FailureCode == PersistenceFailureCode.FutureVersionUnsupported ||
                primaryResult.FailureCode == PersistenceFailureCode.MigrationUnavailable)
            {
                return LoadFailure(slotId, primary, backup, temp, primaryResult.FailureCode,
                    primaryResult.Failure, primaryResult.FormatVersion, false, false, primaryResult.FailureCode);
            }

            if (!backupExists)
            {
                return LoadFailure(slotId, primary, backup, temp, primaryResult.FailureCode,
                    primaryResult.Failure, primaryResult.FormatVersion, false, false, primaryResult.FailureCode);
            }

            PersistenceDocumentResult backupResult = ReadDocument(backup);
            if (backupResult.Success)
            {
                Debug.LogWarning($"[Persistence][RECOVERY]\nSlot: {slotId}\nPrimaryFailure: {primaryResult.FailureCode}\nBackupValid: true\nRecoverySucceeded: true\nActionTaken: loaded backup without deleting or rewriting primary");
                return Loaded(backupResult, "Backup", true, true, primaryResult.FailureCode);
            }

            string failure = $"Primary failed with {primaryResult.FailureCode}: {primaryResult.Failure} Backup failed with {backupResult.FailureCode}: {backupResult.Failure}";
            return LoadFailure(slotId, primary, backup, temp, PersistenceFailureCode.RecoveryFailed,
                failure, backupResult.FormatVersion, true, false, primaryResult.FailureCode);
        }

        public bool TryGetPaths(string slotId, out string primary, out string backup, out string temp)
        {
            primary = null;
            backup = null;
            temp = null;
            if (string.IsNullOrWhiteSpace(slotId) || slotId.Length > 64 || !ValidSlotId.IsMatch(slotId))
                return false;

            primary = Path.Combine(SavesDirectory, slotId + ".json");
            backup = primary + ".bak";
            temp = primary + ".tmp";
            return true;
        }

        private PersistenceDocumentResult ReadDocument(string path)
        {
            try
            {
                return serializer.Deserialize(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return new PersistenceDocumentResult(PersistenceFailureCode.IoFailure, ex.Message, 0, null);
            }
        }

        private static void WriteTempFile(string path, string json)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(true);
            }
        }

        private static void PromoteWithFallback(string primary, string backup, string temp)
        {
            if (File.Exists(backup))
                File.Delete(backup);
            File.Move(primary, backup);
            try
            {
                File.Move(temp, primary);
            }
            catch
            {
                if (!File.Exists(primary) && File.Exists(backup))
                    File.Move(backup, primary);
                throw;
            }
        }

        private static PersistenceLoadResult Loaded(PersistenceDocumentResult document, string source,
            bool recoveryAttempted, bool recoverySucceeded, PersistenceFailureCode primaryFailure)
        {
            return new PersistenceLoadResult(PersistenceFailureCode.Success, null, document.Payload,
                document.FormatVersion, source, recoveryAttempted, recoverySucceeded, primaryFailure);
        }

        private PersistenceWriteResult WriteFailure(string slot, string primary, string backup, string temp,
            PersistenceFailureCode code, string failure)
        {
            Debug.LogError(BuildFailure("WRITE", slot, primary, backup, temp, code, failure, false, false));
            return new PersistenceWriteResult(code, failure, false, null);
        }

        private PersistenceLoadResult LoadFailure(string slot, string primary, string backup, string temp,
            PersistenceFailureCode code, string failure, int formatVersion = 0,
            bool recoveryAttempted = false, bool recoverySucceeded = false,
            PersistenceFailureCode primaryFailure = PersistenceFailureCode.Success)
        {
            Debug.LogError(BuildFailure("READ", slot, primary, backup, temp, code, failure, recoveryAttempted, recoverySucceeded, formatVersion));
            return new PersistenceLoadResult(code, failure, null, formatVersion, null, recoveryAttempted, recoverySucceeded, primaryFailure);
        }

        private static string BuildFailure(string operation, string slot, string primary, string backup,
            string temp, PersistenceFailureCode code, string failure, bool recoveryAttempted,
            bool recoverySucceeded, int formatVersion = 0)
        {
            return $"[Persistence][{operation}_FAILURE]\nOperation: {operation}\nSlot: {Value(slot)}\nPrimaryPath: {Value(primary)}\nBackupPath: {Value(backup)}\nTempPath: {Value(temp)}\nFormatVersion: {(formatVersion > 0 ? formatVersion.ToString() : "<UNKNOWN>")}\nCurrentFormatVersion: {PersistenceSerializer.CurrentFormatVersion}\nPrimaryExists: {Exists(primary)}\nBackupExists: {Exists(backup)}\nRecoveryAttempted: {recoveryAttempted}\nRecoverySucceeded: {recoverySucceeded}\nFailureCode: {code}\nFailure: {Value(failure)}\nActionTaken: no payload delivered; primary and backup preserved";
        }

        private static string Value(string value) => string.IsNullOrWhiteSpace(value) ? "<NONE>" : value;
        private static string Exists(string path) => string.IsNullOrWhiteSpace(path) ? "<UNKNOWN>" : File.Exists(path).ToString();

        private static void TryDeleteTemp(string temp)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(temp) && File.Exists(temp))
                    File.Delete(temp);
            }
            catch (Exception)
            {
                // The original IO failure remains authoritative; the stale temp is diagnostic context.
            }
        }
    }
}
