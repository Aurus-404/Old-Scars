using UnityEngine;

namespace OldScars.Core.Items
{
    /// <summary>
    /// Builds the shared visual for a world item definition.
    /// Curated visual prefabs are preferred and debug primitives remain the fallback.
    /// </summary>
    public static class WorldItemDebugVisualBuilder
    {
        private const string VisualRootName = "Visual";
        private const string LegacyVisualRootName = "World Item Debug Visual";

        public static void Build(Transform worldItemRoot, string itemDefinitionId)
        {
            if (worldItemRoot == null || string.IsNullOrWhiteSpace(itemDefinitionId))
                return;

            Transform existingVisual = worldItemRoot.Find(VisualRootName);
            if (HasRenderer(existingVisual))
            {
                existingVisual.gameObject.SetActive(true);
                DisablePlaceholderRenderers(worldItemRoot, existingVisual);
                return;
            }

            RemoveExistingGeneratedVisual(worldItemRoot);

            var visualRootObject = new GameObject(VisualRootName);
            visualRootObject.layer = worldItemRoot.gameObject.layer;
            Transform visualRoot = visualRootObject.transform;
            visualRoot.SetParent(worldItemRoot, false);

            if (WorldItemVisualResolver.TryBuild(visualRoot, itemDefinitionId))
                return;

            switch (itemDefinitionId)
            {
                case "rusted_crowbar_01":
                    CreatePrimitive(visualRoot, PrimitiveType.Cylinder, new Vector3(0f, 0.25f, 0f), new Vector3(0.08f, 0.65f, 0.08f), new Vector3(0f, 0f, 90f), new Color(0.45f, 0.48f, 0.5f));
                    break;

                case "lee_enfield_rifle_01":
                    CreatePrimitive(visualRoot, PrimitiveType.Cube, new Vector3(-0.25f, 0.22f, 0f), new Vector3(1.4f, 0.18f, 0.3f), Vector3.zero, new Color(0.32f, 0.18f, 0.08f));
                    CreatePrimitive(visualRoot, PrimitiveType.Cube, new Vector3(-0.82f, 0.22f, 0f), new Vector3(0.55f, 0.28f, 0.38f), Vector3.zero, new Color(0.25f, 0.12f, 0.05f));
                    CreatePrimitive(visualRoot, PrimitiveType.Cylinder, new Vector3(0.82f, 0.22f, 0f), new Vector3(0.045f, 0.7f, 0.045f), new Vector3(0f, 0f, 90f), new Color(0.16f, 0.17f, 0.18f));
                    break;

                case "ammo_303_british_01":
                    CreatePrimitive(visualRoot, PrimitiveType.Cube, new Vector3(0f, 0.18f, 0f), new Vector3(0.38f, 0.2f, 0.28f), Vector3.zero, new Color(0.55f, 0.38f, 0.08f));
                    break;

                case "bandage_01":
                    CreatePrimitive(visualRoot, PrimitiveType.Cube, new Vector3(0f, 0.2f, 0f), new Vector3(0.5f, 0.18f, 0.35f), Vector3.zero, new Color(0.9f, 0.9f, 0.82f));
                    break;

                case "water_bottle_01":
                    CreatePrimitive(visualRoot, PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 0f), new Vector3(0.22f, 0.3f, 0.22f), Vector3.zero, new Color(0.15f, 0.45f, 0.9f));
                    break;

                case "food_ration_01":
                    CreatePrimitive(visualRoot, PrimitiveType.Cube, new Vector3(0f, 0.2f, 0f), new Vector3(0.55f, 0.25f, 0.4f), Vector3.zero, new Color(0.75f, 0.35f, 0.08f));
                    break;

                case "scrap_metal_01":
                    CreatePrimitive(visualRoot, PrimitiveType.Cube, new Vector3(0f, 0.2f, 0f), new Vector3(0.45f, 0.25f, 0.45f), new Vector3(8f, 22f, 12f), new Color(0.22f, 0.24f, 0.26f));
                    break;

                default:
                    CreatePrimitive(visualRoot, PrimitiveType.Cube, new Vector3(0f, 0.25f, 0f), new Vector3(0.45f, 0.45f, 0.45f), Vector3.zero, new Color(0.65f, 0.25f, 0.7f));
                    break;
            }
        }

        private static void RemoveExistingGeneratedVisual(Transform worldItemRoot)
        {
            Transform existingVisual = worldItemRoot.Find(VisualRootName);
            RemoveVisual(existingVisual);

            Transform legacyVisual = worldItemRoot.Find(LegacyVisualRootName);
            RemoveVisual(legacyVisual);
        }

        private static void RemoveVisual(Transform visual)
        {
            if (visual == null)
                return;

            visual.gameObject.SetActive(false);
            Object.Destroy(visual.gameObject);
        }

        private static bool HasRenderer(Transform visual)
        {
            return visual != null && visual.GetComponentInChildren<Renderer>(true) != null;
        }

        private static void DisablePlaceholderRenderers(Transform worldItemRoot, Transform visual)
        {
            Renderer[] renderers = worldItemRoot.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer != null && !renderer.transform.IsChildOf(visual))
                    renderer.enabled = false;
            }
        }

        private static void CreatePrimitive(
            Transform root,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEulerAngles,
            Color color)
        {
            GameObject visual = GameObject.CreatePrimitive(primitiveType);
            visual.name = "Debug Visual";
            visual.layer = root.gameObject.layer;
            visual.transform.SetParent(root, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localScale = localScale;
            visual.transform.localEulerAngles = localEulerAngles;

            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
                Object.Destroy(visualCollider);

            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = color;
        }
    }
}
