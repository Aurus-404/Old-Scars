using OldScars.Core.Visuals;
using UnityEngine;

namespace OldScars.Core.Items
{
    /// <summary>
    /// Maps world item definition IDs to curated visual-only prefabs.
    /// </summary>
    public static class WorldItemVisualResolver
    {
        public static bool TryBuild(Transform visualRoot, string itemDefinitionId)
        {
            string visualPrefabId = GetVisualPrefabId(itemDefinitionId);
            return WorldVisualPrefabRegistry.TryCreate(visualRoot, visualPrefabId, out _);
        }

        public static string GetVisualPrefabId(string itemDefinitionId)
        {
            switch (itemDefinitionId)
            {
                case "rusted_crowbar_01":
                    return "PFB_VIS_Rusted_Crowbar_PSX";

                case "lee_enfield_rifle_01":
                    return "PFB_VIS_Lee_Enfield_PSX";

                case "ammo_303_british_01":
                    return "PFB_VIS_Ammo_303_PSX";

                default:
                    return null;
            }
        }
    }
}
