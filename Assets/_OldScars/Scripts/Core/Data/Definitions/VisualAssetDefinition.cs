namespace OldScars.Core.Data.Definitions
{
    [System.Serializable]
    public sealed class VisualAssetDefinition
    {
        public string type; // must be "visual_asset"
        public string id;
        public string asset_key;
        public string provider_id;
        public string provider_asset_id;
    }
}
