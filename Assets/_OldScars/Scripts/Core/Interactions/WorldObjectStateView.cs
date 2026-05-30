using System;
using UnityEngine;

namespace OldScars.Core.Interactions
{
    public sealed class WorldObjectStateView : MonoBehaviour
    {
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

        [SerializeField] private WorldObjectTags worldObjectTags;
        [SerializeField] private VisualRule[] rules;

        private string lastRuntimeTagsSignature;
        private bool warnedMissingTags;
        private bool warnedRootReference;
        private MaterialPropertyBlock materialPropertyBlock;

        private void OnEnable()
        {
            ResolveTagsReference();
            ApplyIfRuntimeTagsChanged(true);
        }

        private void Start()
        {
            ResolveTagsReference();
            ApplyIfRuntimeTagsChanged(true);
        }

        private void Update()
        {
            ApplyIfRuntimeTagsChanged(false);
        }

        private void ResolveTagsReference()
        {
            if (worldObjectTags == null)
                worldObjectTags = GetComponent<WorldObjectTags>();
        }

        private void ApplyIfRuntimeTagsChanged(bool force)
        {
            if (worldObjectTags == null)
            {
                WarnMissingTagsOnce();
                return;
            }

            string[] runtimeTags = worldObjectTags.RuntimeTags;
            string currentSignature = BuildRuntimeTagsSignature(runtimeTags);

            if (!force && currentSignature == lastRuntimeTagsSignature)
                return;

            lastRuntimeTagsSignature = currentSignature;
            ApplyBestRule(runtimeTags);
        }

        private void ApplyBestRule(string[] runtimeTags)
        {
            VisualRule bestRule = null;

            if (rules != null)
            {
                for (int index = 0; index < rules.Length; index++)
                {
                    VisualRule rule = rules[index];
                    if (rule == null || !rule.IsValidFor(runtimeTags))
                        continue;

                    if (bestRule == null || rule.Priority > bestRule.Priority)
                        bestRule = rule;
                }
            }

            if (bestRule != null)
                bestRule.Apply(this);
        }

        private static string BuildRuntimeTagsSignature(string[] runtimeTags)
        {
            if (runtimeTags == null || runtimeTags.Length == 0)
                return string.Empty;

            string[] sortedTags = new string[runtimeTags.Length];
            runtimeTags.CopyTo(sortedTags, 0);
            Array.Sort(sortedTags, StringComparer.Ordinal);
            return string.Join("|", sortedTags);
        }

        private bool CanApplyToGameObject(GameObject target)
        {
            if (target == null)
                return false;

            if (target != gameObject)
                return true;

            if (!warnedRootReference)
            {
                Debug.LogWarning($"[WorldObjectStateView] '{name}' has a visual rule referencing its root GameObject. Root activation is ignored so gameplay components stay active.");
                warnedRootReference = true;
            }

            return false;
        }

        private void ApplyDebugColor(Renderer target, Color color)
        {
            if (target == null)
                return;

            if (materialPropertyBlock == null)
                materialPropertyBlock = new MaterialPropertyBlock();

            materialPropertyBlock.Clear();
            target.GetPropertyBlock(materialPropertyBlock);
            materialPropertyBlock.SetColor(BaseColorPropertyId, color);
            materialPropertyBlock.SetColor(ColorPropertyId, color);
            target.SetPropertyBlock(materialPropertyBlock);
        }

        private void WarnMissingTagsOnce()
        {
            if (warnedMissingTags)
                return;

            Debug.LogWarning($"[WorldObjectStateView] '{name}' has no WorldObjectTags reference and none was found on the same GameObject.");
            warnedMissingTags = true;
        }

        [Serializable]
        private sealed class VisualRule
        {
            [SerializeField] private string stateName;
            [SerializeField] private int priority;
            [SerializeField] private string[] requiredTags;
            [SerializeField] private string[] forbiddenTags;
            [SerializeField] private GameObject[] activateGameObjects;
            [SerializeField] private GameObject[] deactivateGameObjects;
            [SerializeField] private Transform rotateTransform;
            [SerializeField] private Vector3 localEulerAngles;
            [SerializeField] private Renderer[] colorRenderers;
            [SerializeField] private bool applyDebugColor;
            [SerializeField] private Color debugColor = Color.white;

            public int Priority => priority;

            public bool IsValidFor(string[] runtimeTags)
            {
                if (!ContainsAll(runtimeTags, requiredTags))
                    return false;

                return !ContainsAny(runtimeTags, forbiddenTags);
            }

            public void Apply(WorldObjectStateView owner)
            {
                SetActive(owner, activateGameObjects, true);
                SetActive(owner, deactivateGameObjects, false);

                if (rotateTransform != null)
                    rotateTransform.localEulerAngles = localEulerAngles;

                if (applyDebugColor)
                    ApplyColor(owner, colorRenderers, debugColor);
            }

            public override string ToString()
            {
                return string.IsNullOrWhiteSpace(stateName) ? base.ToString() : stateName;
            }

            private static void SetActive(WorldObjectStateView owner, GameObject[] gameObjects, bool isActive)
            {
                if (owner == null || gameObjects == null)
                    return;

                for (int index = 0; index < gameObjects.Length; index++)
                {
                    GameObject target = gameObjects[index];
                    if (!owner.CanApplyToGameObject(target))
                        continue;

                    if (target.activeSelf != isActive)
                        target.SetActive(isActive);
                }
            }

            private static void ApplyColor(WorldObjectStateView owner, Renderer[] renderers, Color color)
            {
                if (owner == null || renderers == null)
                    return;

                for (int index = 0; index < renderers.Length; index++)
                    owner.ApplyDebugColor(renderers[index], color);
            }

            private static bool ContainsAll(string[] availableTags, string[] required)
            {
                if (required == null || required.Length == 0)
                    return true;

                for (int index = 0; index < required.Length; index++)
                {
                    string requiredTag = required[index];
                    if (string.IsNullOrWhiteSpace(requiredTag))
                        continue;

                    if (!ContainsTag(availableTags, requiredTag))
                        return false;
                }

                return true;
            }

            private static bool ContainsAny(string[] availableTags, string[] forbidden)
            {
                if (forbidden == null || forbidden.Length == 0)
                    return false;

                for (int index = 0; index < forbidden.Length; index++)
                {
                    string forbiddenTag = forbidden[index];
                    if (string.IsNullOrWhiteSpace(forbiddenTag))
                        continue;

                    if (ContainsTag(availableTags, forbiddenTag))
                        return true;
                }

                return false;
            }

            private static bool ContainsTag(string[] tags, string tag)
            {
                if (tags == null || string.IsNullOrWhiteSpace(tag))
                    return false;

                for (int index = 0; index < tags.Length; index++)
                {
                    if (tags[index] == tag)
                        return true;
                }

                return false;
            }
        }
    }
}
