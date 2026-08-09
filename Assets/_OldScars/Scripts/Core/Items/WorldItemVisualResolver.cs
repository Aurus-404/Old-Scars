using OldScars.Core.Data;
using OldScars.Core.Visuals;
using UnityEngine;

namespace OldScars.Core.Items
{
    /// <summary>
    /// Resolves data-driven world visuals first, then preserves the curated
    /// legacy mapping used by items that have not migrated yet.
    /// </summary>
    public static class WorldItemVisualResolver
    {
        public static bool TryBuild(Transform visualRoot, string itemDefinitionId)
        {
            if (TryBuildFromItemVisualProfile(visualRoot, itemDefinitionId))
                return true;

            string visualPrefabId = GetVisualPrefabId(itemDefinitionId);
            return WorldVisualPrefabRegistry.TryCreate(visualRoot, visualPrefabId, out _);
        }

        private static bool TryBuildFromItemVisualProfile(Transform visualRoot, string itemDefinitionId)
        {
            if (visualRoot == null || string.IsNullOrWhiteSpace(itemDefinitionId) ||
                GameDataManager.Instance == null || !GameDataManager.Instance.IsReady ||
                GameDataManager.Instance.Database == null)
                return false;

            var database = GameDataManager.Instance.Database;
            var profile = database.GetItemVisualProfileByItemDefinitionId(itemDefinitionId);
            if (profile == null || !profile.enabled.GetValueOrDefault(true) || string.IsNullOrWhiteSpace(profile.world_asset_key))
                return false;

            var asset = database.GetVisualAssetByKey(profile.world_asset_key);
            string error = null;
            if (asset == null ||
                !VisualAssetProviderRegistry.TryGet(asset.provider_id, out IVisualAssetProvider provider) ||
                !provider.TryResolvePrefab(asset, out GameObject prefab, out error) ||
                prefab == null)
            {
                if (!string.IsNullOrWhiteSpace(error))
                    Debug.LogWarning($"[WorldItemVisualResolver] {error}");
                return false;
            }

            GameObject instance = Object.Instantiate(prefab, visualRoot, false);
            if (instance == null)
                return false;
            instance.name = "Visual Model";
            SetLayerRecursively(instance, visualRoot.gameObject.layer);
            if (instance.GetComponentInChildren<Renderer>(true) != null)
                return true;

            instance.SetActive(false);
            Object.Destroy(instance);
            return false;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            for (int index = 0; index < root.transform.childCount; index++)
                SetLayerRecursively(root.transform.GetChild(index).gameObject, layer);
        }

        public static string GetVisualPrefabId(string itemDefinitionId)
        {
            string canonicalId = ContentId.TryResolveLegacyCore(
                itemDefinitionId, out ContentId resolved, out _, out _)
                ? resolved.Canonical
                : itemDefinitionId;
            switch (canonicalId)
            {
                case "core:rusted_crowbar_01":
                    return "PFB_VIS_Rusted_Crowbar_PSX";

                case "core:lee_enfield_rifle_01":
                    return "PFB_VIS_Lee_Enfield_PSX";

                case "core:ammo_303_british_01":
                    return "PFB_VIS_Ammo_303_PSX";

                default:
                    return null;
            }
        }
    }
}
