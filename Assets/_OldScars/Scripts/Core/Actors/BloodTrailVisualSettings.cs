using UnityEngine;

namespace OldScars.Core.Actors
{
    /// <summary>Runtime reference to the R0 decal material; it owns no gameplay state.</summary>
    [CreateAssetMenu(menuName = "Old Scars/Rendering/Blood Trail Visual Settings")]
    public sealed class BloodTrailVisualSettings : ScriptableObject
    {
        [SerializeField] private Material bloodMarkMaterial;
        [SerializeField] private float baseMarkSizeMeters = .25f;
        [SerializeField] private float projectionDepth = .30f;
        [SerializeField] private float drawDistance = 50f;

        public Material BloodMarkMaterial => bloodMarkMaterial;
        public float BaseMarkSizeMeters => baseMarkSizeMeters;
        public float ProjectionDepth => projectionDepth;
        public float DrawDistance => drawDistance;

#if UNITY_EDITOR
        public void SetBloodMarkMaterial(Material value) => bloodMarkMaterial = value;
        public void SetPresentation(float sizeMeters, float depth, float distance)
        {
            baseMarkSizeMeters = sizeMeters;
            projectionDepth = depth;
            drawDistance = distance;
        }
#endif
    }
}
