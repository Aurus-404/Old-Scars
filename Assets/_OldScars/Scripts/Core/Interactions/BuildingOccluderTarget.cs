using UnityEngine;

namespace OldScars.Core.Interactions
{
    public sealed class BuildingOccluderTarget : MonoBehaviour
    {
        [SerializeField] private string buildingId = "m32_debug_test_house";
        [SerializeField] private BuildingOccluderTargetType targetType;
        [SerializeField] private bool hideByCameraRaycast = true;
        [SerializeField] private bool hideAlwaysWhenInside;
        [SerializeField] private int floorIndex;
        [SerializeField] private Renderer[] renderersToHide;
        [SerializeField] private Collider[] collidersToDisableWhileHidden;

        private bool[] initialRendererEnabledStates;
        private bool[] initialColliderEnabledStates;
        private bool initialStatesCached;
        private bool hidden;
        private Bounds cachedBounds;
        private bool hasCachedBounds;

        public string BuildingId => buildingId;
        public BuildingOccluderTargetType TargetType => targetType;
        public bool HideByCameraRaycast => hideByCameraRaycast;
        public bool HideAlwaysWhenInside => hideAlwaysWhenInside;
        public int FloorIndex => floorIndex;
        public bool IsHidden => hidden;

        private void Awake()
        {
            CacheInitialStates();
        }

        private void OnDisable()
        {
            if (Application.isPlaying && hidden)
                RestoreInitialState();
        }

        public void Hide()
        {
            CacheInitialStates();
            CacheCurrentBounds();

            SetRenderersEnabled(false);
            SetCollidersEnabled(false);
            hidden = true;
        }

        public void RestoreInitialState()
        {
            CacheInitialStates();

            RestoreRendererStates();
            RestoreColliderStates();
            hidden = false;
        }

        public bool TryGetCachedBounds(out Bounds bounds)
        {
            if (!hasCachedBounds)
                CacheCurrentBounds();

            bounds = cachedBounds;
            return hasCachedBounds;
        }

        private void CacheInitialStates()
        {
            if (initialStatesCached)
                return;

            int rendererCount = renderersToHide != null ? renderersToHide.Length : 0;
            initialRendererEnabledStates = new bool[rendererCount];
            for (int index = 0; index < rendererCount; index++)
            {
                Renderer targetRenderer = renderersToHide[index];
                initialRendererEnabledStates[index] = targetRenderer != null && targetRenderer.enabled;
            }

            int colliderCount = collidersToDisableWhileHidden != null ? collidersToDisableWhileHidden.Length : 0;
            initialColliderEnabledStates = new bool[colliderCount];
            for (int index = 0; index < colliderCount; index++)
            {
                Collider targetCollider = collidersToDisableWhileHidden[index];
                initialColliderEnabledStates[index] = targetCollider != null && targetCollider.enabled;
            }

            initialStatesCached = true;
        }

        private void CacheCurrentBounds()
        {
            hasCachedBounds = false;

            AddRendererBounds();
            AddColliderBounds();
        }

        private void AddRendererBounds()
        {
            if (renderersToHide == null)
                return;

            for (int index = 0; index < renderersToHide.Length; index++)
            {
                Renderer targetRenderer = renderersToHide[index];
                if (targetRenderer == null)
                    continue;

                AddBounds(targetRenderer.bounds);
            }
        }

        private void AddColliderBounds()
        {
            if (collidersToDisableWhileHidden == null)
                return;

            for (int index = 0; index < collidersToDisableWhileHidden.Length; index++)
            {
                Collider targetCollider = collidersToDisableWhileHidden[index];
                if (targetCollider == null)
                    continue;

                AddBounds(targetCollider.bounds);
            }
        }

        private void AddBounds(Bounds bounds)
        {
            if (!hasCachedBounds)
            {
                cachedBounds = bounds;
                hasCachedBounds = true;
                return;
            }

            cachedBounds.Encapsulate(bounds);
        }

        private void SetRenderersEnabled(bool enabled)
        {
            if (renderersToHide == null)
                return;

            for (int index = 0; index < renderersToHide.Length; index++)
            {
                Renderer targetRenderer = renderersToHide[index];
                if (targetRenderer != null)
                    targetRenderer.enabled = enabled;
            }
        }

        private void SetCollidersEnabled(bool enabled)
        {
            if (collidersToDisableWhileHidden == null)
                return;

            for (int index = 0; index < collidersToDisableWhileHidden.Length; index++)
            {
                Collider targetCollider = collidersToDisableWhileHidden[index];
                if (targetCollider != null)
                    targetCollider.enabled = enabled;
            }
        }

        private void RestoreRendererStates()
        {
            if (renderersToHide == null || initialRendererEnabledStates == null)
                return;

            int count = Mathf.Min(renderersToHide.Length, initialRendererEnabledStates.Length);
            for (int index = 0; index < count; index++)
            {
                Renderer targetRenderer = renderersToHide[index];
                if (targetRenderer != null)
                    targetRenderer.enabled = initialRendererEnabledStates[index];
            }
        }

        private void RestoreColliderStates()
        {
            if (collidersToDisableWhileHidden == null || initialColliderEnabledStates == null)
                return;

            int count = Mathf.Min(collidersToDisableWhileHidden.Length, initialColliderEnabledStates.Length);
            for (int index = 0; index < count; index++)
            {
                Collider targetCollider = collidersToDisableWhileHidden[index];
                if (targetCollider != null)
                    targetCollider.enabled = initialColliderEnabledStates[index];
            }
        }

        private void OnValidate()
        {
            if (floorIndex < 0)
                floorIndex = 0;
        }
    }

    public enum BuildingOccluderTargetType
    {
        Wall,
        Roof,
        UpperFloor
    }
}
