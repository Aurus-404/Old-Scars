using UnityEngine;

namespace OldScars.Core.Actors
{
    /// <summary>
    /// Explicitly marks a collider as locomotion/general collision rather than combat anatomy.
    /// Legacy actors without ActorCombatHitRegion children remain valid combat targets.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class ActorLocomotionCollider : MonoBehaviour
    {
        public Transform ActorRoot
        {
            get
            {
                ActorHealthComponent health = GetComponentInParent<ActorHealthComponent>();
                return health != null ? health.transform : null;
            }
        }

        public bool HasExplicitCombatHitboxes =>
            ActorRoot != null && ActorRoot.GetComponentInChildren<ActorCombatHitRegion>(false) != null;
    }
}
