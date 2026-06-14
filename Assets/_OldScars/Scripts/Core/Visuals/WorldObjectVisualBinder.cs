using UnityEngine;

namespace OldScars.Core.Visuals
{
    /// <summary>
    /// Replaces a placed object's primitive renderer with a curated child visual.
    /// Gameplay components and colliders remain on the parent object.
    /// </summary>
    public sealed class WorldObjectVisualBinder : MonoBehaviour
    {
        public const string VisualRootName = "Visual";

        [SerializeField] private string visualPrefabId;

        public string VisualPrefabId => visualPrefabId;

        private void Start()
        {
            BuildVisual();
        }

        private void BuildVisual()
        {
            if (string.IsNullOrWhiteSpace(visualPrefabId))
                return;

            Transform existingVisual = transform.Find(VisualRootName);
            if (HasRenderer(existingVisual))
            {
                DisablePlaceholderRenderers(existingVisual);
                Debug.Log($"[WorldObjectVisualBinder] Reused visualPrefabId={visualPrefabId} on {name}.");
                return;
            }

            if (existingVisual != null)
            {
                existingVisual.gameObject.SetActive(false);
                Destroy(existingVisual.gameObject);
            }

            var visualRootObject = new GameObject(VisualRootName);
            visualRootObject.layer = gameObject.layer;
            Transform visualRoot = visualRootObject.transform;
            visualRoot.SetParent(transform, false);

            if (!WorldVisualPrefabRegistry.TryCreate(visualRoot, visualPrefabId, out _))
            {
                visualRootObject.SetActive(false);
                Destroy(visualRootObject);
                return;
            }

            DisablePlaceholderRenderers(visualRoot);

            Debug.Log($"[WorldObjectVisualBinder] Applied visualPrefabId={visualPrefabId} to {name}.");
        }

        private void DisablePlaceholderRenderers(Transform visualRoot)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer != null && !renderer.transform.IsChildOf(visualRoot))
                    renderer.enabled = false;
            }
        }

        private static bool HasRenderer(Transform visual)
        {
            return visual != null && visual.GetComponentInChildren<Renderer>(true) != null;
        }
    }
}
