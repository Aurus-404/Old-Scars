using System;
using UnityEngine;

namespace OldScars.Core.Actors
{
    public sealed class WorldClock : MonoBehaviour
    {
        public const double DefaultElapsedGameSeconds = 0d;
        public const double DefaultGameSecondsPerRealSecond = 60d;
        public const double SecondsPerMinute = 60d;
        public const double SecondsPerHour = 3600d;
        public const double SecondsPerDay = 86400d;
        public const double MaxElapsedGameSeconds = SecondsPerDay * 3660000d;

        [SerializeField, Min(0.001f)]
        private float gameSecondsPerRealSecond = (float)DefaultGameSecondsPerRealSecond;
        [SerializeField]
        private bool advanceDuringGameplay = true;

        private double elapsedGameSeconds = DefaultElapsedGameSeconds;
        private bool limitFailureLogged;

        public static WorldClock Current { get; private set; }
        public double ElapsedGameSeconds => elapsedGameSeconds;
        public double GameSecondsPerRealSecond => gameSecondsPerRealSecond;
        public bool AdvanceDuringGameplay
        {
            get => advanceDuringGameplay;
            set => advanceDuringGameplay = value;
        }
        public int Day => (int)Math.Floor(elapsedGameSeconds / SecondsPerDay) + 1;
        public int Hour => (int)Math.Floor(elapsedGameSeconds / SecondsPerHour) % 24;
        public int Minute => (int)Math.Floor(elapsedGameSeconds / SecondsPerMinute) % 60;
        public string DisplayTime => $"Day {Day}  {Hour:00}:{Minute:00}";

        public event Action<double> GameTimeAdvanced;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeAuthority()
        {
            Current = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeAuthority()
        {
            if (Current != null)
            {
                Current.BindExistingNeeds();
                return;
            }

            WorldClock existing = FindAnyObjectByType<WorldClock>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.BindExistingNeeds();
                return;
            }

            var root = new GameObject("WorldClock_Runtime");
            root.AddComponent<WorldClock>();
        }

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogError("[WorldClock][DUPLICATE_AUTHORITY] A second WorldClock was rejected. ActionTaken: duplicate GameObject destroyed.");
                Destroy(gameObject);
                return;
            }

            Current = this;
            gameSecondsPerRealSecond = Mathf.Max(0.001f, gameSecondsPerRealSecond);
            DontDestroyOnLoad(gameObject);
            BindExistingNeeds();
        }

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        private void Update()
        {
            if (!advanceDuringGameplay || Time.deltaTime <= 0f)
                return;

            double deltaGameSeconds = Time.deltaTime * gameSecondsPerRealSecond;
            if (TryAdvanceGameTime(deltaGameSeconds, out string failure))
            {
                limitFailureLogged = false;
                return;
            }

            if (!limitFailureLogged)
            {
                limitFailureLogged = true;
                Debug.LogError("[WorldClock][PROGRESSION_REJECTED]" +
                    $"\nElapsedGameSeconds: {elapsedGameSeconds:R}\nDeltaGameSeconds: {deltaGameSeconds:R}" +
                    $"\nFailure: {failure}\nActionTaken: world time was not advanced");
            }
        }

        public bool TryAdvanceGameTime(double deltaGameSeconds, out string failure)
        {
            failure = null;
            if (!IsFinite(deltaGameSeconds) || deltaGameSeconds <= 0d)
            {
                failure = $"Duration must be finite and positive; received {deltaGameSeconds:R}.";
                return false;
            }

            double next = elapsedGameSeconds + deltaGameSeconds;
            if (!IsValidElapsedGameSeconds(next))
            {
                failure = $"World time would exceed the supported range 0..{MaxElapsedGameSeconds:R} seconds.";
                return false;
            }

            elapsedGameSeconds = next;
            GameTimeAdvanced?.Invoke(deltaGameSeconds);
            return true;
        }

        public bool TryRestoreElapsedGameSeconds(double value, out string failure)
        {
            failure = null;
            if (!IsValidElapsedGameSeconds(value))
            {
                failure = $"Elapsed game seconds must be finite and within 0..{MaxElapsedGameSeconds:R}; received {value:R}.";
                return false;
            }

            elapsedGameSeconds = value;
            limitFailureLogged = false;
            return true;
        }

        public static bool IsValidElapsedGameSeconds(double value)
        {
            return IsFinite(value) && value >= 0d && value <= MaxElapsedGameSeconds;
        }

        internal void Bind(ActorNeedsComponent needs)
        {
            if (needs != null)
                needs.ConnectWorldClock(this);
        }

        private void BindExistingNeeds()
        {
            ActorNeedsComponent[] needs = FindObjectsByType<ActorNeedsComponent>(FindObjectsInactive.Exclude);
            for (int index = 0; index < needs.Length; index++)
                Bind(needs[index]);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
