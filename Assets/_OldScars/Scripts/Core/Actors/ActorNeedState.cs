using System;

namespace OldScars.Core.Actors
{
    [Serializable]
    public sealed class ActorNeedState
    {
        public string needId;
        public float currentValue;

        public ActorNeedState()
        {
        }

        public ActorNeedState(string needId, float currentValue)
        {
            this.needId = needId;
            this.currentValue = currentValue;
        }
    }
}
