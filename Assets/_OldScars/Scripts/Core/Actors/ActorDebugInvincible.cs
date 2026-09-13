using UnityEngine;

namespace OldScars.Core.Actors
{
    /// <summary>
    /// Ephemeral development-tooling rule that prevents this actor from reaching terminal Dead.
    /// Health and Condition remain the authorities for all numeric and physiological state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActorDebugInvincible : MonoBehaviour
    {
        public bool IsInvincible { get; private set; }

        public void SetInvincible(bool invincible)
        {
            IsInvincible = invincible;
        }
    }
}
