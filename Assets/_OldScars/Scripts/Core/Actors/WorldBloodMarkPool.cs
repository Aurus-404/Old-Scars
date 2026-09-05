using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace OldScars.Core.Actors
{
    /// <summary>
    /// Scene-global visual owner for transient blood marks. It is intentionally
    /// independent from Medical, save data, actors, AI and world materialization.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldBloodMarkPool : MonoBehaviour
    {
        public const int DefaultActiveBudget = 128;
        public const float DefaultLifetimeRealSeconds = 45f;

        private const string SettingsResourcePath = "BloodTrails/BloodTrailVisualSettings";
        private readonly List<PooledMark> active = new List<PooledMark>();
        private readonly Stack<PooledMark> available = new Stack<PooledMark>();
        private BloodTrailVisualSettings settings;
        private bool missingSettingsLogged;
        private int activeBudget = DefaultActiveBudget;
        private float lifetimeRealSeconds = DefaultLifetimeRealSeconds;
        private ulong serial;

        public static WorldBloodMarkPool Current { get; private set; }
        public int ActiveMarkCount => active.Count;
        public int PeakActiveMarkCount { get; private set; }
        public int CreatedCount { get; private set; }
        public int AcquiredCount { get; private set; }
        public int RecycledCount { get; private set; }
        public int ExpiredCount { get; private set; }
        public int ActiveBudget => activeBudget;
        public float LifetimeRealSeconds => lifetimeRealSeconds;
        public IReadOnlyList<DecalProjector> ActiveProjectors
        {
            get
            {
                var projectors = new List<DecalProjector>(active.Count);
                for (int index = 0; index < active.Count; index++) projectors.Add(active[index].projector);
                return projectors;
            }
        }

        public static WorldBloodMarkPool Ensure()
        {
            if (Current != null) return Current;
            WorldBloodMarkPool existing = FindAnyObjectByType<WorldBloodMarkPool>();
            if (existing != null) return existing;
            return new GameObject("World Blood Mark Pool").AddComponent<WorldBloodMarkPool>();
        }

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Destroy(gameObject);
                return;
            }
            Current = this;
            settings = Resources.Load<BloodTrailVisualSettings>(SettingsResourcePath);
        }

        private void Update() => ReleaseExpired(Time.realtimeSinceStartupAsDouble);

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        public bool TryPlace(Vector3 point, Vector3 normal, float scale, float rotationDegrees)
        {
            if (!EnsureSettings()) return false;
            ReleaseExpired(Time.realtimeSinceStartupAsDouble);
            PooledMark mark;
            if (available.Count > 0)
                mark = available.Pop();
            else if (active.Count < activeBudget)
                mark = Create();
            else
            {
                mark = active[0];
                active.RemoveAt(0);
                RecycledCount++;
            }

            Quaternion orientation = Quaternion.AngleAxis(rotationDegrees, normal) *
                                     Quaternion.LookRotation(-normal, Vector3.forward);
            mark.projector.transform.SetPositionAndRotation(point + normal * .01f, orientation);
            float markSize = settings.BaseMarkSizeMeters * scale;
            mark.projector.size = new Vector3(markSize, markSize, settings.ProjectionDepth);
            mark.projector.pivot = Vector3.zero;
            mark.projector.drawDistance = settings.DrawDistance;
            mark.expiresAt = Time.realtimeSinceStartupAsDouble + lifetimeRealSeconds;
            mark.order = ++serial;
            mark.projector.gameObject.SetActive(true);
            active.Add(mark);
            AcquiredCount++;
            PeakActiveMarkCount = Mathf.Max(PeakActiveMarkCount, active.Count);
            return true;
        }

        private bool EnsureSettings()
        {
            if (settings == null) settings = Resources.Load<BloodTrailVisualSettings>(SettingsResourcePath);
            if (settings != null && settings.BloodMarkMaterial != null) return true;
            if (!missingSettingsLogged)
            {
                missingSettingsLogged = true;
                Debug.LogError("[BloodTrails] BloodTrailVisualSettings or the R0 material is missing; marks are disabled.");
            }
            return false;
        }

        private PooledMark Create()
        {
            var root = new GameObject("Pooled Blood Mark");
            root.transform.SetParent(transform, false);
            root.SetActive(false);
            var projector = root.AddComponent<DecalProjector>();
            projector.material = settings.BloodMarkMaterial;
            CreatedCount++;
            return new PooledMark { projector = projector };
        }

        private void ReleaseExpired(double now)
        {
            for (int index = active.Count - 1; index >= 0; index--)
            {
                PooledMark mark = active[index];
                if (mark.expiresAt > now) continue;
                active.RemoveAt(index);
                mark.projector.gameObject.SetActive(false);
                available.Push(mark);
                ExpiredCount++;
            }
        }

        public void ConfigureDiagnosticLimits(int budget, float lifetimeSeconds)
        {
            if (!Application.isEditor || budget < 1 || lifetimeSeconds <= 0f)
                throw new InvalidOperationException("Diagnostic pool limits are invalid.");
            activeBudget = budget;
            lifetimeRealSeconds = lifetimeSeconds;
            double expiresAt = Time.realtimeSinceStartupAsDouble + lifetimeSeconds;
            for (int index = 0; index < active.Count; index++) active[index].expiresAt = expiresAt;
            ReleaseExpired(Time.realtimeSinceStartupAsDouble);
        }

        public void ExpireAllForDiagnostics()
        {
            if (!Application.isEditor) throw new InvalidOperationException("Diagnostic expiry is Editor-only.");
            ReleaseExpired(double.PositiveInfinity);
        }

        private sealed class PooledMark
        {
            public DecalProjector projector;
            public double expiresAt;
            public ulong order;
        }
    }
}
