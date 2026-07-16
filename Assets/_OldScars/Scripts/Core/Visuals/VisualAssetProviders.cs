using System;
using System.Collections.Generic;
using OldScars.Core.Data.Definitions;
using UnityEngine;

namespace OldScars.Core.Visuals
{
    public interface IVisualAssetProvider
    {
        string ProviderId { get; }

        bool TryResolvePrefab(VisualAssetDefinition asset, out GameObject prefab, out string error);
    }

    public static class VisualAssetProviderRegistry
    {
        private static readonly Dictionary<string, IVisualAssetProvider> Providers =
            new Dictionary<string, IVisualAssetProvider>();

        static VisualAssetProviderRegistry()
        {
            Register(BuiltInVisualAssetProvider.Instance);
        }

        public static bool Register(IVisualAssetProvider provider)
        {
            if (provider == null || string.IsNullOrWhiteSpace(provider.ProviderId))
                return false;
            Providers[provider.ProviderId] = provider;
            return true;
        }

        public static bool TryGet(string providerId, out IVisualAssetProvider provider)
        {
            provider = null;
            return !string.IsNullOrWhiteSpace(providerId) && Providers.TryGetValue(providerId, out provider);
        }
    }

    public sealed class BuiltInVisualAssetProvider : IVisualAssetProvider
    {
        public const string Id = "builtin";
        public static readonly BuiltInVisualAssetProvider Instance = new BuiltInVisualAssetProvider();

        private BuiltInVisualAssetProvider()
        {
        }

        public string ProviderId => Id;

        public bool TryResolvePrefab(VisualAssetDefinition asset, out GameObject prefab, out string error)
        {
            prefab = null;
            error = null;
            if (asset == null)
            {
                error = "Visual asset definition is missing.";
                return false;
            }
            if (asset.provider_id != Id)
            {
                error = $"Visual asset '{asset.asset_key}' requires provider '{asset.provider_id}', not '{Id}'.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(asset.provider_asset_id))
            {
                error = $"Visual asset '{asset.asset_key}' has no provider_asset_id.";
                return false;
            }

            try
            {
                prefab = Resources.Load<GameObject>(asset.provider_asset_id);
            }
            catch (Exception exception)
            {
                error = $"Could not load '{asset.asset_key}': {exception.GetType().Name}: {exception.Message}";
                return false;
            }

            if (prefab == null)
            {
                error = $"Built-in visual asset '{asset.asset_key}' was not found at Resources/{asset.provider_asset_id}.";
                return false;
            }
            return true;
        }
    }
}
