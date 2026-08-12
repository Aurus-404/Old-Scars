using System;

namespace OldScars.Core.Actors
{
    [Serializable]
    public sealed class ActorNeedProfile
    {
        public ActorNeedConfig[] needs =
        {
            new ActorNeedConfig("hunger", "Hunger", 100f, 100f, 0.03f),
            new ActorNeedConfig("thirst", "Thirst", 100f, 100f, 0.05f)
        };

        public ActorNeedConfig GetNeed(string needId)
        {
            if (string.IsNullOrWhiteSpace(needId) || needs == null)
            {
                return null;
            }

            for (int i = 0; i < needs.Length; i++)
            {
                ActorNeedConfig need = needs[i];
                if (need != null && need.needId == needId)
                {
                    return need;
                }
            }

            return null;
        }
    }

    [Serializable]
    public sealed class ActorNeedConfig
    {
        // The serialized field name is retained because SampleScene authors the pre-M38 real-time rates.
        // M38.1 converts that legacy baseline to an explicit per-game-hour rate without editing the scene.
        public const double LegacyRealSecondsPerGameHour =
            WorldClock.SecondsPerHour / WorldClock.DefaultGameSecondsPerRealSecond;

        public string needId;
        public string displayName;
        public float maxValue = 100f;
        public float initialValue = 100f;
        public float decayPerSecond;

        public double DecayPerGameHour => Math.Max(0d, decayPerSecond) * LegacyRealSecondsPerGameHour;

        public ActorNeedConfig()
        {
        }

        public ActorNeedConfig(string needId, string displayName, float maxValue, float initialValue, float decayPerSecond)
        {
            this.needId = needId;
            this.displayName = displayName;
            this.maxValue = maxValue;
            this.initialValue = initialValue;
            this.decayPerSecond = decayPerSecond;
        }
    }
}
