using OldScars.Core.ApplicationShell;
using UnityEditor;
using UnityEngine;

namespace OldScars.EditorTools
{
    public static class WorldRuntimeTerrainDevelopmentMenu
    {
        private const string Root = "Old Scars/Diagnostics/Terrain/Active Region Backend/";

        [MenuItem(Root + "Unity Terrain (Default)")]
        private static void SelectUnityTerrain() =>
            Select(WorldRuntimeTerrainDevelopmentSelection.UnityTerrain);

        [MenuItem(Root + "Volumetric - Marching Tetrahedra")]
        private static void SelectMarchingTetrahedra() =>
            Select(WorldRuntimeTerrainDevelopmentSelection.VolumetricMarchingTetrahedra);

        [MenuItem(Root + "Volumetric - Indexed Marching Cubes")]
        private static void SelectMarchingCubes() =>
            Select(WorldRuntimeTerrainDevelopmentSelection.VolumetricIndexedMarchingCubes);

        [MenuItem(Root + "Unity Terrain (Default)", true)]
        private static bool ValidateUnityTerrain() => Validate(
            WorldRuntimeTerrainDevelopmentSelection.UnityTerrain,
            Root + "Unity Terrain (Default)");

        [MenuItem(Root + "Volumetric - Marching Tetrahedra", true)]
        private static bool ValidateMarchingTetrahedra() => Validate(
            WorldRuntimeTerrainDevelopmentSelection.VolumetricMarchingTetrahedra,
            Root + "Volumetric - Marching Tetrahedra");

        [MenuItem(Root + "Volumetric - Indexed Marching Cubes", true)]
        private static bool ValidateMarchingCubes() => Validate(
            WorldRuntimeTerrainDevelopmentSelection.VolumetricIndexedMarchingCubes,
            Root + "Volumetric - Indexed Marching Cubes");

        private static void Select(WorldRuntimeTerrainDevelopmentSelection selection)
        {
            WorldRuntimeTerrainDevelopmentSettings.SetSelection(selection);
            Debug.Log("[WorldRuntime][DEVELOPMENT_TERRAIN_SELECTION]\nSelection: " + selection +
                      "\nAppliesTo: next WorldRuntime scene load\nPersistedWorldTruth: false");
        }

        private static bool Validate(
            WorldRuntimeTerrainDevelopmentSelection selection,
            string menuPath)
        {
            Menu.SetChecked(menuPath,
                WorldRuntimeTerrainDevelopmentSettings.CurrentSelection == selection);
            return true;
        }
    }
}
