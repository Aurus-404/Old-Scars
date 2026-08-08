using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OldScars.Core.Persistence
{
    public enum PersistenceFailureCode
    {
        Success,
        SaveNotFound,
        InvalidSlotId,
        IoFailure,
        MalformedJson,
        InvalidEnvelope,
        FutureVersionUnsupported,
        MigrationUnavailable,
        RecoveryFailed,
        SerializationFailure
    }

    public interface ISaveMigration
    {
        int SourceFormatVersion { get; }
        int TargetFormatVersion { get; }
        JToken Migrate(JToken payload);
    }

    public sealed class PersistenceDocumentResult
    {
        internal PersistenceDocumentResult(PersistenceFailureCode failureCode, string failure, int formatVersion, JToken payload)
        {
            FailureCode = failureCode;
            Failure = failure;
            FormatVersion = formatVersion;
            Payload = payload;
        }

        public bool Success => FailureCode == PersistenceFailureCode.Success;
        public PersistenceFailureCode FailureCode { get; }
        public string Failure { get; }
        public int FormatVersion { get; }
        public JToken Payload { get; }
    }

    public sealed class PersistenceSerializer
    {
        public const int CurrentFormatVersion = 1;

        private static readonly JsonSerializerSettings SaveSettings = new JsonSerializerSettings
        {
            Culture = CultureInfo.InvariantCulture,
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Decimal,
            Formatting = Formatting.Indented,
            MissingMemberHandling = MissingMemberHandling.Error,
            NullValueHandling = NullValueHandling.Include,
            ReferenceLoopHandling = ReferenceLoopHandling.Error,
            StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
            TypeNameHandling = TypeNameHandling.None
        };

        private static readonly JsonLoadSettings LoadSettings = new JsonLoadSettings
        {
            CommentHandling = CommentHandling.Ignore,
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
            LineInfoHandling = LineInfoHandling.Load
        };

        private readonly List<ISaveMigration> migrations;

        public PersistenceSerializer(IEnumerable<ISaveMigration> migrations = null)
        {
            this.migrations = migrations != null
                ? new List<ISaveMigration>(migrations)
                : new List<ISaveMigration>();
        }

        public PersistenceFailureCode Serialize(JToken payload, out string json, out string failure)
        {
            json = null;
            failure = null;
            if (payload == null || payload.Type == JTokenType.Null)
            {
                failure = "Save payload must be present and non-null.";
                return PersistenceFailureCode.InvalidEnvelope;
            }

            try
            {
                var envelope = new JObject
                {
                    ["formatVersion"] = CurrentFormatVersion,
                    ["writtenUtc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    ["payload"] = payload.DeepClone()
                };
                json = JsonConvert.SerializeObject(envelope, SaveSettings);
                PersistenceDocumentResult validation = Deserialize(json);
                if (!validation.Success || validation.FormatVersion != CurrentFormatVersion)
                {
                    failure = validation.Failure ?? "Generated save document failed validation.";
                    json = null;
                    return validation.FailureCode == PersistenceFailureCode.Success
                        ? PersistenceFailureCode.InvalidEnvelope
                        : validation.FailureCode;
                }

                return PersistenceFailureCode.Success;
            }
            catch (JsonException ex)
            {
                failure = ex.Message;
                return PersistenceFailureCode.SerializationFailure;
            }
            catch (InvalidOperationException ex)
            {
                failure = ex.Message;
                return PersistenceFailureCode.SerializationFailure;
            }
        }

        public PersistenceDocumentResult Deserialize(string json)
        {
            JToken root;
            try
            {
                root = JToken.Parse(json, LoadSettings);
            }
            catch (JsonReaderException ex)
            {
                return Failed(PersistenceFailureCode.MalformedJson, ex.Message);
            }
            catch (ArgumentException ex)
            {
                return Failed(PersistenceFailureCode.MalformedJson, ex.Message);
            }

            if (!(root is JObject envelope))
                return Failed(PersistenceFailureCode.InvalidEnvelope, "Save root must be a JSON object.");
            if (!envelope.TryGetValue("formatVersion", StringComparison.Ordinal, out JToken versionToken) ||
                versionToken.Type != JTokenType.Integer)
                return Failed(PersistenceFailureCode.InvalidEnvelope, "formatVersion must be a present JSON integer.");
            if (!envelope.TryGetValue("payload", StringComparison.Ordinal, out JToken payload) ||
                payload == null || payload.Type == JTokenType.Null)
                return Failed(PersistenceFailureCode.InvalidEnvelope, "payload must be present and non-null.");

            int version;
            try
            {
                version = versionToken.Value<int>();
            }
            catch (Exception ex) when (ex is OverflowException || ex is FormatException)
            {
                return Failed(PersistenceFailureCode.InvalidEnvelope, "formatVersion is outside the supported integer range.");
            }

            if (version < 1)
                return TryMigrate(version, payload);
            if (version > CurrentFormatVersion)
            {
                return Failed(
                    PersistenceFailureCode.FutureVersionUnsupported,
                    $"Save format version {version} is newer than supported version {CurrentFormatVersion}.",
                    version);
            }

            return new PersistenceDocumentResult(PersistenceFailureCode.Success, null, version, payload.DeepClone());
        }

        private PersistenceDocumentResult TryMigrate(int version, JToken payload)
        {
            int activeVersion = version;
            JToken activePayload = payload.DeepClone();
            while (activeVersion < CurrentFormatVersion)
            {
                ISaveMigration migration = migrations.Find(candidate =>
                    candidate != null &&
                    candidate.SourceFormatVersion == activeVersion &&
                    candidate.TargetFormatVersion == activeVersion + 1);
                if (migration == null)
                {
                    return Failed(PersistenceFailureCode.MigrationUnavailable,
                        $"No migration is registered from save format version {activeVersion}.", activeVersion);
                }

                try
                {
                    activePayload = migration.Migrate(activePayload);
                }
                catch (Exception ex)
                {
                    return Failed(PersistenceFailureCode.InvalidEnvelope,
                        $"Migration from version {activeVersion} failed: {ex.Message}", activeVersion);
                }

                if (activePayload == null || activePayload.Type == JTokenType.Null)
                    return Failed(PersistenceFailureCode.InvalidEnvelope, "Migration produced a null payload.", activeVersion);
                activeVersion++;
            }

            return new PersistenceDocumentResult(PersistenceFailureCode.Success, null, activeVersion, activePayload);
        }

        private static PersistenceDocumentResult Failed(PersistenceFailureCode code, string failure, int formatVersion = 0)
        {
            return new PersistenceDocumentResult(code, failure, formatVersion, null);
        }
    }
}
