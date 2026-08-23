using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using OldScars.Core.Data;
using OldScars.Core.Data.Loading;

namespace OldScars.Core.World
{
    /// <summary>
    /// Immutable creation-time provenance evidence for one loaded content source.
    /// It records what was present; it does not decide generation or save compatibility.
    /// </summary>
    public sealed class WorldCreationContentSourceEvidence
    {
        internal WorldCreationContentSourceEvidence(
            string sourceId,
            string ownedNamespace,
            string version,
            bool isOfficialCore,
            string provenanceFingerprint)
        {
            SourceId = sourceId;
            OwnedNamespace = ownedNamespace;
            Version = version;
            IsOfficialCore = isOfficialCore;
            ProvenanceFingerprint = provenanceFingerprint;
        }

        public string SourceId { get; }
        public string OwnedNamespace { get; }
        public string Version { get; }
        public bool IsOfficialCore { get; }
        public string ProvenanceFingerprint { get; }
    }

    /// <summary>
    /// Creation-time snapshot of LoadedContentSet identity/provenance metadata.
    /// No current-content comparison is performed here because provenance is not
    /// a compatibility policy.
    /// </summary>
    public sealed class WorldCreationContentEvidence
    {
        private readonly ReadOnlyCollection<WorldCreationContentSourceEvidence> sources;

        private WorldCreationContentEvidence(
            string loadedContentSetFingerprint,
            IList<WorldCreationContentSourceEvidence> sources)
        {
            LoadedContentSetFingerprint = loadedContentSetFingerprint;
            this.sources = new ReadOnlyCollection<WorldCreationContentSourceEvidence>(
                new List<WorldCreationContentSourceEvidence>(sources));
        }

        public string LoadedContentSetFingerprint { get; }
        public IReadOnlyList<WorldCreationContentSourceEvidence> Sources => sources;

        public static WorldCreationContentEvidence Capture(LoadedContentSet loadedContentSet)
        {
            if (loadedContentSet == null)
                throw new ArgumentNullException(nameof(loadedContentSet));

            var captured = new List<WorldCreationContentSourceEvidence>(loadedContentSet.Sources.Count);
            for (int index = 0; index < loadedContentSet.Sources.Count; index++)
            {
                LoadedContentSource source = loadedContentSet.Sources[index];
                captured.Add(new WorldCreationContentSourceEvidence(
                    source.SourceId,
                    source.OwnedNamespace,
                    source.Version,
                    source.IsOfficialCore,
                    source.ProvenanceFingerprint));
            }

            if (!TryCreate(loadedContentSet.ProvenanceFingerprint, captured, out WorldCreationContentEvidence evidence,
                    out string error))
            {
                throw new ArgumentException("LoadedContentSet cannot be captured as world creation evidence: " + error,
                    nameof(loadedContentSet));
            }

            return evidence;
        }

        internal static bool TryCreate(
            string loadedContentSetFingerprint,
            IEnumerable<WorldCreationContentSourceEvidence> sourceInputs,
            out WorldCreationContentEvidence evidence,
            out string error)
        {
            evidence = null;
            error = null;
            if (!IsCanonicalSha256(loadedContentSetFingerprint))
            {
                error = "loaded content set provenance fingerprint must be 64 lowercase hexadecimal characters";
                return false;
            }
            if (sourceInputs == null)
            {
                error = "creation content source evidence collection is null";
                return false;
            }

            var sourceList = new List<WorldCreationContentSourceEvidence>(sourceInputs);
            if (sourceList.Count == 0)
            {
                error = "creation content evidence must contain at least the official Core source";
                return false;
            }

            var sourceIds = new HashSet<string>(StringComparer.Ordinal);
            var namespaces = new HashSet<string>(StringComparer.Ordinal);
            int officialCoreCount = 0;
            string previousExternalSourceId = null;
            for (int index = 0; index < sourceList.Count; index++)
            {
                WorldCreationContentSourceEvidence source = sourceList[index];
                if (source == null)
                {
                    error = $"creation content source evidence at index {index} is null";
                    return false;
                }
                if (!ContentId.TryValidateLocalId(source.SourceId, out string sourceIdError))
                {
                    error = $"creation content source_id at index {index} is invalid: {sourceIdError}";
                    return false;
                }
                if (!ContentId.TryValidateNamespace(source.OwnedNamespace, out string namespaceError))
                {
                    error = $"creation content namespace at index {index} is invalid: {namespaceError}";
                    return false;
                }
                if (!TryValidateVersionEvidence(source.Version, out string versionError))
                {
                    error = $"creation content version at index {index} is invalid: {versionError}";
                    return false;
                }
                if (!IsCanonicalSha256(source.ProvenanceFingerprint))
                {
                    error = $"creation content provenance fingerprint at index {index} must be 64 lowercase hexadecimal characters";
                    return false;
                }
                if (!sourceIds.Add(source.SourceId))
                {
                    error = $"duplicate creation content source_id '{source.SourceId}'";
                    return false;
                }
                if (!namespaces.Add(source.OwnedNamespace))
                {
                    error = $"duplicate creation content namespace '{source.OwnedNamespace}'";
                    return false;
                }

                if (source.IsOfficialCore)
                {
                    officialCoreCount++;
                    if (index != 0 || source.SourceId != GameDataLoader.OfficialCoreSourceId ||
                        source.OwnedNamespace != ContentId.CoreNamespace)
                    {
                        error = "official Core creation evidence must be first and use the reserved source_id/namespace pair";
                        return false;
                    }
                }
                else
                {
                    if (source.SourceId == GameDataLoader.OfficialCoreSourceId ||
                        source.OwnedNamespace == ContentId.CoreNamespace)
                    {
                        error = "external creation evidence cannot claim the reserved Core source_id or namespace";
                        return false;
                    }
                    if (previousExternalSourceId != null &&
                        string.CompareOrdinal(previousExternalSourceId, source.SourceId) >= 0)
                    {
                        error = "external creation content evidence must retain canonical source_id order";
                        return false;
                    }
                    previousExternalSourceId = source.SourceId;
                }
            }

            if (officialCoreCount != 1)
            {
                error = "creation content evidence must contain exactly one official Core source";
                return false;
            }

            evidence = new WorldCreationContentEvidence(loadedContentSetFingerprint, sourceList);
            return true;
        }

        private static bool TryValidateVersionEvidence(string version, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(version))
            {
                error = "value is null, empty or whitespace";
                return false;
            }
            if (!string.Equals(version, version.Trim(), StringComparison.Ordinal))
            {
                error = "leading or trailing whitespace is not allowed";
                return false;
            }
            for (int index = 0; index < version.Length; index++)
            {
                if (char.IsControl(version[index]))
                {
                    error = $"control character at position {index} is not allowed";
                    return false;
                }
            }
            return true;
        }

        private static bool IsCanonicalSha256(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 64)
                return false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!(character >= '0' && character <= '9') && !(character >= 'a' && character <= 'f'))
                    return false;
            }
            return true;
        }
    }
}
