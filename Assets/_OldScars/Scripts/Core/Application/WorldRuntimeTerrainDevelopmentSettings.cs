using System;
using OldScars.Core.World;
using UnityEngine;

namespace OldScars.Core.ApplicationShell
{
    public enum WorldRuntimeTerrainDevelopmentSelection
    {
        UnityTerrain = 0,
        VolumetricMarchingTetrahedra = 1,
        VolumetricIndexedMarchingCubes = 2
    }

    /// <summary>
    /// Development-only local selection for the active-region representation.
    /// It is neither world truth nor persisted save data. Release players always
    /// resolve the established Unity Terrain reference path.
    /// </summary>
    public static class WorldRuntimeTerrainDevelopmentSettings
    {
        private const string SelectionKey = "OldScars.WorldRuntime.DevelopmentTerrainSelection";
#if UNITY_EDITOR
        private static WorldRuntimeTerrainDevelopmentSelection? diagnosticSelectionOverride;
#endif

        public static WorldRuntimeTerrainDevelopmentSelection CurrentSelection
        {
            get
            {
#if UNITY_EDITOR
                if (diagnosticSelectionOverride.HasValue)
                    return diagnosticSelectionOverride.Value;
                // Batch diagnostics that are not explicitly testing the experimental
                // backend must remain independent of a developer's persisted menu choice.
                if (Application.isBatchMode)
                    return WorldRuntimeTerrainDevelopmentSelection.UnityTerrain;
#endif
                if (!Application.isEditor && !Debug.isDebugBuild)
                    return WorldRuntimeTerrainDevelopmentSelection.UnityTerrain;
                int stored = PlayerPrefs.GetInt(
                    SelectionKey, (int)WorldRuntimeTerrainDevelopmentSelection.UnityTerrain);
                return Enum.IsDefined(typeof(WorldRuntimeTerrainDevelopmentSelection), stored)
                    ? (WorldRuntimeTerrainDevelopmentSelection)stored
                    : WorldRuntimeTerrainDevelopmentSelection.UnityTerrain;
            }
        }

        public static bool UsesVolumetricTerrain =>
            CurrentSelection != WorldRuntimeTerrainDevelopmentSelection.UnityTerrain;

        public static DeformableTerrainMesherBackend SelectedMesher =>
            CurrentSelection == WorldRuntimeTerrainDevelopmentSelection.VolumetricIndexedMarchingCubes
                ? DeformableTerrainMesherBackend.IndexedMarchingCubes
                : DeformableTerrainMesherBackend.MarchingTetrahedra;

        public static void SetSelection(WorldRuntimeTerrainDevelopmentSelection selection)
        {
            if (!Enum.IsDefined(typeof(WorldRuntimeTerrainDevelopmentSelection), selection))
                throw new ArgumentOutOfRangeException(nameof(selection));
            PlayerPrefs.SetInt(SelectionKey, (int)selection);
            PlayerPrefs.Save();
        }

#if UNITY_EDITOR
        public static void SetDiagnosticSelectionOverride(
            WorldRuntimeTerrainDevelopmentSelection selection)
        {
            if (!Enum.IsDefined(typeof(WorldRuntimeTerrainDevelopmentSelection), selection))
                throw new ArgumentOutOfRangeException(nameof(selection));
            diagnosticSelectionOverride = selection;
        }

        public static void ClearDiagnosticSelectionOverride()
        {
            diagnosticSelectionOverride = null;
        }
#endif
    }
}
