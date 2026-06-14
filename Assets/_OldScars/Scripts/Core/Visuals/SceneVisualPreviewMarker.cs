using UnityEngine;

namespace OldScars.Core.Visuals
{
    /// <summary>
    /// Marks a persistent editor-created visual preview and records placeholder renderer state.
    /// </summary>
    public sealed class SceneVisualPreviewMarker : MonoBehaviour
    {
        [SerializeField] private Renderer[] placeholderRenderers;
        [SerializeField] private bool[] placeholderEnabledStates;

        public void CapturePlaceholderStates(Renderer[] renderers)
        {
            placeholderRenderers = renderers;
            placeholderEnabledStates = new bool[renderers != null ? renderers.Length : 0];

            for (int index = 0; index < placeholderEnabledStates.Length; index++)
                placeholderEnabledStates[index] = placeholderRenderers[index] != null && placeholderRenderers[index].enabled;
        }

        public void RestorePlaceholderStates()
        {
            if (placeholderRenderers == null || placeholderEnabledStates == null)
                return;

            int count = Mathf.Min(placeholderRenderers.Length, placeholderEnabledStates.Length);
            for (int index = 0; index < count; index++)
            {
                if (placeholderRenderers[index] != null)
                    placeholderRenderers[index].enabled = placeholderEnabledStates[index];
            }
        }
    }
}
