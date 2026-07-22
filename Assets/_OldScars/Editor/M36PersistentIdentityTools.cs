using OldScars.Core.Items;
using UnityEditor;
using UnityEngine;

namespace OldScars.Editor
{
    public static class M36PersistentIdentityTools
    {
        private const string MenuPath = "Old Scars/Diagnostics/M36.1/Run Checkpoint A Item Identity %#i";

        [MenuItem(MenuPath)]
        public static void RunCheckpointAIdentityDiagnostics()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("M36.1 Checkpoint A diagnostics must run outside Play Mode.");
                return;
            }

            M36ItemIdentityDiagnostics.RunAndLog();
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateRunCheckpointAIdentityDiagnostics()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;
        }
    }
}
