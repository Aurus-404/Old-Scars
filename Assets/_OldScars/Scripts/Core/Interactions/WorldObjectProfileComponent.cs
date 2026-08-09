using System.Collections;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using UnityEngine;

namespace OldScars.Core.Interactions
{
    public sealed class WorldObjectProfileComponent : MonoBehaviour
    {
        [SerializeField] private string worldObjectProfileId;

        private bool profileApplied;
        private bool loggedWaitingForData;

        private IEnumerator Start()
        {
            if (string.IsNullOrWhiteSpace(worldObjectProfileId))
            {
                Debug.LogError($"[WorldObjectProfileComponent] '{name}' has no worldObjectProfileId configured.");
                yield break;
            }

            while (!IsGameDataReady())
            {
                LogWaitingForDataOnce();
                yield return null;
            }

            ApplyProfile(GameDataManager.Instance.Database);
        }

        private static bool IsGameDataReady()
        {
            return GameDataManager.Instance != null &&
                   GameDataManager.Instance.IsReady &&
                   GameDataManager.Instance.Database != null;
        }

        private void ApplyProfile(GameDatabase database)
        {
            if (profileApplied)
                return;

            if (database == null)
            {
                Debug.LogError($"[WorldObjectProfileComponent] '{name}' cannot apply world object profile '{worldObjectProfileId}' because GameDatabase is null.");
                return;
            }

            WorldObjectProfileDefinition profile = database.GetWorldObjectProfile(worldObjectProfileId);
            if (profile == null)
            {
                Debug.LogError($"[WorldObjectProfileComponent] '{name}' world object profile '{worldObjectProfileId}' was not found.");
                return;
            }

            worldObjectProfileId = profile.id;
            profileApplied = true;

            ApplyDisplayName(profile);
            ApplyInitialTags(profile);

            Debug.Log($"[WorldObjectProfileComponent] '{name}' applied world object profile '{worldObjectProfileId}'.");
        }

        private void ApplyDisplayName(WorldObjectProfileDefinition profile)
        {
            if (string.IsNullOrWhiteSpace(profile.display_name))
                return;

            WorldObjectDebugInfo debugInfo = GetComponent<WorldObjectDebugInfo>();
            if (debugInfo == null)
            {
                Debug.LogWarning($"[WorldObjectProfileComponent] '{name}' cannot apply display_name from world object profile '{worldObjectProfileId}' because WorldObjectDebugInfo is missing.");
                return;
            }

            debugInfo.SetRuntimeDisplayName(profile.display_name);
        }

        private void ApplyInitialTags(WorldObjectProfileDefinition profile)
        {
            if (profile.initial_tags == null || profile.initial_tags.Length == 0)
                return;

            WorldObjectTags worldObjectTags = GetComponent<WorldObjectTags>();
            if (worldObjectTags == null)
            {
                Debug.LogWarning($"[WorldObjectProfileComponent] '{name}' cannot apply initial_tags from world object profile '{worldObjectProfileId}' because WorldObjectTags is missing.");
                return;
            }

            worldObjectTags.ApplyInitialTags(profile.initial_tags);
        }

        private void LogWaitingForDataOnce()
        {
            if (loggedWaitingForData)
                return;

            loggedWaitingForData = true;
            Debug.Log($"[WorldObjectProfileComponent] '{name}' waiting for CoreDataSystem before applying world object profile '{worldObjectProfileId}'.");
        }
    }
}
