using UnityEngine;

namespace OldScars.Core.Actors
{
    /// <summary>
    /// Authored target-side point used as the primary destination for firearm aim.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActorPrimaryAimPoint : MonoBehaviour
    {
        public Transform Point => transform;
    }
}
