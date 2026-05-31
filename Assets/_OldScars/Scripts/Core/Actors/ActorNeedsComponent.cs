using System.Collections.Generic;
using UnityEngine;

namespace OldScars.Core.Actors
{
    public sealed class ActorNeedsComponent : MonoBehaviour
    {
        [SerializeField] private ActorNeedProfile profile = new ActorNeedProfile();
        [SerializeField] private ActorNeedState[] runtimeStates;

        public ActorNeedProfile Profile => profile;
        public IReadOnlyList<ActorNeedState> RuntimeStates => runtimeStates;

        private void Awake()
        {
            InitializeRuntimeState();
        }

        private void Update()
        {
            if (runtimeStates == null || runtimeStates.Length == 0 || profile?.needs == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            for (int i = 0; i < runtimeStates.Length; i++)
            {
                ActorNeedState state = runtimeStates[i];
                if (state == null)
                {
                    continue;
                }

                ActorNeedConfig config = profile.GetNeed(state.needId);
                if (config == null)
                {
                    continue;
                }

                float maxValue = Mathf.Max(0f, config.maxValue);
                float nextValue = state.currentValue - Mathf.Max(0f, config.decayPerSecond) * deltaTime;
                state.currentValue = Mathf.Clamp(nextValue, 0f, maxValue);
            }
        }

        public void RestoreNeed(string needId, float amount)
        {
            TryRestoreNeed(needId, amount);
        }

        public bool TryRestoreNeed(string needId, float amount)
        {
            ActorNeedState state = FindState(needId);
            ActorNeedConfig config = profile?.GetNeed(needId);
            if (state == null || config == null || amount <= 0f)
            {
                return false;
            }

            float maxValue = Mathf.Max(0f, config.maxValue);
            if (state.currentValue >= maxValue)
            {
                return false;
            }

            float previousValue = state.currentValue;
            state.currentValue = Mathf.Clamp(state.currentValue + amount, 0f, maxValue);
            return state.currentValue > previousValue;
        }

        public float GetNeedValue(string needId)
        {
            ActorNeedState state = FindState(needId);
            return state != null ? state.currentValue : 0f;
        }

        public float GetNeedMaxValue(string needId)
        {
            ActorNeedConfig config = profile?.GetNeed(needId);
            return config != null ? Mathf.Max(0f, config.maxValue) : 0f;
        }

        public string GetNeedDisplayName(string needId)
        {
            ActorNeedConfig config = profile?.GetNeed(needId);
            if (config == null || string.IsNullOrWhiteSpace(config.displayName))
            {
                return needId;
            }

            return config.displayName;
        }

        public bool HasNeed(string needId)
        {
            return FindState(needId) != null && profile?.GetNeed(needId) != null;
        }

        public bool CanRestoreNeed(string needId, float amount)
        {
            ActorNeedState state = FindState(needId);
            ActorNeedConfig config = profile?.GetNeed(needId);
            if (state == null || config == null || amount <= 0f)
            {
                return false;
            }

            return state.currentValue < Mathf.Max(0f, config.maxValue);
        }

        private void InitializeRuntimeState()
        {
            if (profile?.needs == null)
            {
                runtimeStates = new ActorNeedState[0];
                return;
            }

            bool needsRebuild = runtimeStates == null || runtimeStates.Length != profile.needs.Length;
            if (!needsRebuild)
            {
                for (int i = 0; i < profile.needs.Length; i++)
                {
                    ActorNeedConfig config = profile.needs[i];
                    ActorNeedState state = runtimeStates[i];
                    if (config == null || state == null || state.needId != config.needId)
                    {
                        needsRebuild = true;
                        break;
                    }
                }
            }

            if (needsRebuild)
            {
                runtimeStates = new ActorNeedState[profile.needs.Length];
                for (int i = 0; i < profile.needs.Length; i++)
                {
                    ActorNeedConfig config = profile.needs[i];
                    string needId = config != null ? config.needId : string.Empty;
                    float maxValue = config != null ? Mathf.Max(0f, config.maxValue) : 0f;
                    float initialValue = config != null ? config.initialValue : 0f;
                    runtimeStates[i] = new ActorNeedState(needId, Mathf.Clamp(initialValue, 0f, maxValue));
                }
            }
            else
            {
                for (int i = 0; i < runtimeStates.Length; i++)
                {
                    ActorNeedConfig config = profile.needs[i];
                    if (config == null || runtimeStates[i] == null)
                    {
                        continue;
                    }

                    runtimeStates[i].currentValue = Mathf.Clamp(runtimeStates[i].currentValue, 0f, Mathf.Max(0f, config.maxValue));
                }
            }
        }

        private ActorNeedState FindState(string needId)
        {
            if (string.IsNullOrWhiteSpace(needId) || runtimeStates == null)
            {
                return null;
            }

            for (int i = 0; i < runtimeStates.Length; i++)
            {
                ActorNeedState state = runtimeStates[i];
                if (state != null && state.needId == needId)
                {
                    return state;
                }
            }

            return null;
        }
    }
}
