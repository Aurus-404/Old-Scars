using Newtonsoft.Json;

namespace OldScars.Core.Data.Loading
{
    internal sealed class ContentSourceManifest
    {
        [JsonProperty("source_id")]
        internal string SourceId { get; set; }

        [JsonProperty("namespace")]
        internal string OwnedNamespace { get; set; }

        [JsonProperty("version")]
        internal string Version { get; set; }
    }
}
