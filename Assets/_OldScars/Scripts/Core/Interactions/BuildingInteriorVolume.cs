using UnityEngine;

namespace OldScars.Core.Interactions
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BuildingInteriorVolume : MonoBehaviour
    {
        [SerializeField] private string buildingId = "m32_debug_test_house";
        [SerializeField] private BuildingVisibilityManager manager;
        [SerializeField] private BoxCollider interiorCollider;

        public string BuildingId => buildingId;

        private void Awake()
        {
            CacheReferences();
        }

        private void Reset()
        {
            interiorCollider = GetComponent<BoxCollider>();
            if (interiorCollider != null)
                interiorCollider.isTrigger = true;
        }

        private void OnValidate()
        {
            if (interiorCollider == null)
                interiorCollider = GetComponent<BoxCollider>();

            if (interiorCollider != null)
                interiorCollider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            ActorInteractionContext actor = GetPlayerActor(other);
            if (actor == null)
                return;

            CacheReferences();
            if (manager != null)
                manager.NotifyPlayerEntered(this, actor);
        }

        private void OnTriggerExit(Collider other)
        {
            ActorInteractionContext actor = GetPlayerActor(other);
            if (actor == null)
                return;

            CacheReferences();
            if (manager != null)
                manager.NotifyPlayerExited(this, actor);
        }

        public bool ContainsPlayer(ActorInteractionContext actor)
        {
            if (!IsPlayerActor(actor))
                return false;

            return ContainsWorldPoint(actor.transform.position);
        }

        public bool ContainsWorldPoint(Vector3 worldPosition)
        {
            EnsureInteriorCollider();
            if (interiorCollider == null)
                return false;

            Vector3 localPoint = interiorCollider.transform.InverseTransformPoint(worldPosition);
            Vector3 localCenter = interiorCollider.center;
            Vector3 halfSize = interiorCollider.size * 0.5f;

            return Mathf.Abs(localPoint.x - localCenter.x) <= halfSize.x
                && Mathf.Abs(localPoint.y - localCenter.y) <= halfSize.y
                && Mathf.Abs(localPoint.z - localCenter.z) <= halfSize.z;
        }

        private void CacheReferences()
        {
            EnsureInteriorCollider();

            if (manager == null)
                manager = FindAnyObjectByType<BuildingVisibilityManager>();
        }

        private void EnsureInteriorCollider()
        {
            if (interiorCollider == null)
                interiorCollider = GetComponent<BoxCollider>();

            if (interiorCollider != null && !interiorCollider.isTrigger)
                interiorCollider.isTrigger = true;
        }

        private static ActorInteractionContext GetPlayerActor(Collider other)
        {
            if (other == null)
                return null;

            ActorInteractionContext actor = other.GetComponentInParent<ActorInteractionContext>();
            return IsPlayerActor(actor) ? actor : null;
        }

        private static bool IsPlayerActor(ActorInteractionContext actor)
        {
            if (actor == null || actor.ActorTags == null)
                return false;

            string[] actorTags = actor.ActorTags;
            for (int index = 0; index < actorTags.Length; index++)
            {
                if (actorTags[index] == "player")
                    return true;
            }

            return false;
        }
    }
}
