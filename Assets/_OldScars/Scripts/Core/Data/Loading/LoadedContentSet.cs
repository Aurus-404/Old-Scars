using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OldScars.Core.Data.Loading
{
    /// <summary>
    /// One authoritative input consumed by the current content loader.
    /// Paths are normalized relative to the content-source root and never
    /// contain a local machine path or the source-root directory name.
    /// </summary>
    public sealed class LoadedContentInput
    {
        internal LoadedContentInput(string relativePath, long byteLength, string sha256)
        {
            RelativePath = relativePath;
            ByteLength = byteLength;
            Sha256 = sha256;
        }

        public string RelativePath { get; }
        public long ByteLength { get; }
        public string Sha256 { get; }
    }

    /// <summary>
    /// Immutable provenance metadata for one successfully loaded content source.
    /// The fingerprint describes source identity/version plus recognized input
    /// paths and exact bytes. It is evidence, not a compatibility decision.
    /// </summary>
    public sealed class LoadedContentSource
    {
        private readonly ReadOnlyCollection<LoadedContentInput> recognizedInputs;

        internal LoadedContentSource(
            string sourceId,
            string ownedNamespace,
            string version,
            bool isOfficialCore,
            string provenanceFingerprint,
            IList<LoadedContentInput> recognizedInputs)
        {
            SourceId = sourceId;
            OwnedNamespace = ownedNamespace;
            Version = version;
            IsOfficialCore = isOfficialCore;
            ProvenanceFingerprint = provenanceFingerprint;
            this.recognizedInputs = new ReadOnlyCollection<LoadedContentInput>(
                new List<LoadedContentInput>(recognizedInputs));
        }

        public string SourceId { get; }
        public string OwnedNamespace { get; }
        public string Version { get; }
        public bool IsOfficialCore { get; }
        public string ProvenanceFingerprint { get; }
        public IReadOnlyList<LoadedContentInput> RecognizedInputs => recognizedInputs;
    }

    /// <summary>
    /// Immutable, canonically ordered description of the validated content set.
    /// GameDataManager publishes it only after loader and DataValidator success.
    /// </summary>
    public sealed class LoadedContentSet
    {
        private readonly ReadOnlyCollection<LoadedContentSource> sources;

        internal LoadedContentSet(
            IList<LoadedContentSource> sources,
            string provenanceFingerprint,
            string canonicalDescription)
        {
            this.sources = new ReadOnlyCollection<LoadedContentSource>(
                new List<LoadedContentSource>(sources));
            ProvenanceFingerprint = provenanceFingerprint;
            CanonicalDescription = canonicalDescription;
        }

        public IReadOnlyList<LoadedContentSource> Sources => sources;
        public string ProvenanceFingerprint { get; }
        public string CanonicalDescription { get; }
    }

    internal static class ContentProvenance
    {
        private const string SourceDomain = "old_scars_content_source_provenance_v1";
        private const string SetDomain = "old_scars_loaded_content_set_provenance_v1";

        internal static LoadedContentSource BuildSource(ContentLoadContext context)
        {
            List<ContentInputFile> files = context.GetRecognizedInputs();
            var inputs = new List<LoadedContentInput>(files.Count);

            using (var canonical = new MemoryStream())
            {
                WriteString(canonical, SourceDomain);
                WriteString(canonical, context.SourceId);
                WriteString(canonical, context.OwnedNamespace);
                WriteString(canonical, context.Version);
                WriteInt64(canonical, files.Count);

                foreach (ContentInputFile file in files)
                {
                    byte[] bytes = file.ExactBytes;
                    WriteString(canonical, file.RelativePath);
                    WriteInt64(canonical, bytes.LongLength);
                    canonical.Write(bytes, 0, bytes.Length);
                    inputs.Add(new LoadedContentInput(
                        file.RelativePath,
                        bytes.LongLength,
                        ComputeSha256(bytes)));
                }

                return new LoadedContentSource(
                    context.SourceId,
                    context.OwnedNamespace,
                    context.Version,
                    context.IsOfficialCore,
                    ComputeSha256(canonical.ToArray()),
                    inputs);
            }
        }

        internal static LoadedContentSet BuildSet(IList<LoadedContentSource> orderedSources)
        {
            var description = new StringBuilder();
            using (var canonical = new MemoryStream())
            {
                WriteString(canonical, SetDomain);
                WriteInt64(canonical, orderedSources.Count);

                for (int index = 0; index < orderedSources.Count; index++)
                {
                    LoadedContentSource source = orderedSources[index];
                    WriteString(canonical, source.SourceId);
                    WriteString(canonical, source.OwnedNamespace);
                    WriteString(canonical, source.Version);
                    WriteString(canonical, source.ProvenanceFingerprint);

                    if (index > 0)
                        description.Append('\n');
                    description.Append(source.SourceId)
                        .Append('|').Append(source.OwnedNamespace)
                        .Append('|').Append(source.Version)
                        .Append('|').Append(source.ProvenanceFingerprint);
                }

                return new LoadedContentSet(
                    orderedSources,
                    ComputeSha256(canonical.ToArray()),
                    description.ToString());
            }
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                var result = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                    result.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }

        private static void WriteString(Stream stream, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            WriteInt64(stream, bytes.LongLength);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteInt64(Stream stream, long value)
        {
            unchecked
            {
                for (int shift = 56; shift >= 0; shift -= 8)
                    stream.WriteByte((byte)(value >> shift));
            }
        }
    }
}
