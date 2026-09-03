using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using UnityEngine;

namespace OldScars.Core.Actors
{
    /// <summary>
    /// Materializes an optional built-in debug representation selected by visual-rig family.
    /// ActorSpawnService keeps its capsule fallback when no representation asset exists.
    /// </summary>
    internal static class ActorRuntimeRepresentationFactory
    {
        private const string ResourcePrefix = "OldScarsActorRepresentations/";

        public static GameObject Create(ActorProfileDefinition actorProfile, Vector3 position, Quaternion rotation)
        {
            GameObject prefab = ResolvePrefab(actorProfile);
            GameObject root = prefab != null
                ? Object.Instantiate(prefab, position, rotation)
                : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.transform.SetPositionAndRotation(position, rotation);
            return root;
        }

        private static GameObject ResolvePrefab(ActorProfileDefinition actorProfile)
        {
            if (actorProfile == null || string.IsNullOrWhiteSpace(actorProfile.visual_rig_profile_id))
                return null;
            GameDatabase database = GameDataManager.Instance?.Database;
            VisualRigProfileDefinition profile = database?.GetVisualRigProfile(actorProfile.visual_rig_profile_id);
            if (profile == null || string.IsNullOrWhiteSpace(profile.family_id))
                return null;
            return Resources.Load<GameObject>(ResourcePrefix + profile.family_id);
        }
    }
}
