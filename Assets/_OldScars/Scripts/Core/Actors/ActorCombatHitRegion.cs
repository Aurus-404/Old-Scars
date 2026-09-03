using UnityEngine;

namespace OldScars.Core.Actors
{
    /// <summary>
    /// Identifies the anatomical BodyRegion represented by one combat collider.
    /// Medical consequences remain owned by CombatResolutionService.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class ActorCombatHitRegion : MonoBehaviour
    {
        [SerializeField] private BodyRegion region = BodyRegion.Torso;

        public BodyRegion Region => region;
        public Transform ActorRoot
        {
            get
            {
                ActorHealthComponent health = GetComponentInParent<ActorHealthComponent>();
                return health != null ? health.transform : null;
            }
        }

        public void Configure(BodyRegion bodyRegion)
        {
            region = bodyRegion;
        }

        public bool BelongsTo(Transform actorRoot) => actorRoot != null && ActorRoot == actorRoot;
    }

}
