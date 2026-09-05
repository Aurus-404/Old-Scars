using UnityEngine;

namespace OldScars.Core.Actors
{
    /// <summary>Runtime reference to the R0 decal material; it owns no gameplay state.</summary>
    [CreateAssetMenu(menuName = "Old Scars/Rendering/Blood Trail Visual Settings")]
    public sealed class BloodTrailVisualSettings : ScriptableObject
    {
        [SerializeField] private Material bloodMarkMaterial;

        public Material BloodMarkMaterial => bloodMarkMaterial;

#if UNITY_EDITOR
        public void SetBloodMarkMaterial(Material value) => bloodMarkMaterial = value;
#endif
    }
}
