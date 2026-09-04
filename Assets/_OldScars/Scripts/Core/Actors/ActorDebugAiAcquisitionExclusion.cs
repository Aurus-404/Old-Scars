using UnityEngine;

namespace OldScars.Core.Actors
{
    /// <summary>
    /// Development-tooling marker that excludes this actor from automatic AI threat acquisition.
    /// It owns no affiliation, perception, combat, rendering, or persistence state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActorDebugAiAcquisitionExclusion : MonoBehaviour
    {
        public bool IsExcludedFromAutomaticThreatAcquisition { get; private set; }

        public void SetExcludedFromAutomaticThreatAcquisition(bool excluded)
        {
            IsExcludedFromAutomaticThreatAcquisition = excluded;
        }
    }
}
