using System;

namespace OldScars.Core.Actors
{
    public enum ActorRestFailureCode
    {
        None,
        MissingActor,
        ActorInactive,
        MissingHealth,
        ActorDead,
        InvalidDuration,
        WorldClockUnavailable,
        ClockAdvanceRejected
    }

    public sealed class ActorRestResult
    {
        private ActorRestResult(
            ActorRestFailureCode failureCode,
            string message,
            double elapsedGameSecondsBefore,
            double elapsedGameSecondsAfter)
        {
            FailureCode = failureCode;
            Message = message;
            ElapsedGameSecondsBefore = elapsedGameSecondsBefore;
            ElapsedGameSecondsAfter = elapsedGameSecondsAfter;
        }

        public bool Success => FailureCode == ActorRestFailureCode.None;
        public ActorRestFailureCode FailureCode { get; }
        public string Message { get; }
        public double ElapsedGameSecondsBefore { get; }
        public double ElapsedGameSecondsAfter { get; }
        public double AdvancedGameSeconds => ElapsedGameSecondsAfter - ElapsedGameSecondsBefore;
        public bool AdditionalRecoveryApplied => false;

        internal static ActorRestResult Succeeded(double before, double after)
        {
            return new ActorRestResult(
                ActorRestFailureCode.None,
                $"Rest completed. World time advanced {(after - before) / WorldClock.SecondsPerHour:0.##} game hours; no health or wound recovery was applied.",
                before,
                after);
        }

        internal static ActorRestResult Failed(ActorRestFailureCode code, string message, double elapsed)
        {
            return new ActorRestResult(code, message, elapsed, elapsed);
        }
    }

    public static class ActorRestService
    {
        public static ActorRestResult TryRest(ActorNeedsComponent actorNeeds, double durationGameSeconds)
        {
            WorldClock clock = WorldClock.Current;
            double elapsed = clock != null ? clock.ElapsedGameSeconds : 0d;
            if (actorNeeds == null)
                return ActorRestResult.Failed(ActorRestFailureCode.MissingActor, "Rest requires an actor with needs.", elapsed);
            if (!actorNeeds.isActiveAndEnabled || !actorNeeds.gameObject.activeInHierarchy)
                return ActorRestResult.Failed(ActorRestFailureCode.ActorInactive, "Rest requires an active actor with enabled needs.", elapsed);

            ActorHealthComponent health = actorNeeds.GetComponent<ActorHealthComponent>();
            if (health == null)
                return ActorRestResult.Failed(ActorRestFailureCode.MissingHealth, "Rest requires ActorHealthComponent lifecycle authority.", elapsed);
            ActorRuntimeIdentity identity = actorNeeds.GetComponent<ActorRuntimeIdentity>();
            if (health.IsDead || identity != null && identity.LifecycleState == ActorLifecycleState.Dead)
                return ActorRestResult.Failed(ActorRestFailureCode.ActorDead, "Dead actors cannot rest and are not revived.", elapsed);
            if (double.IsNaN(durationGameSeconds) || double.IsInfinity(durationGameSeconds) || durationGameSeconds <= 0d)
                return ActorRestResult.Failed(ActorRestFailureCode.InvalidDuration, $"Rest duration must be finite and positive; received {durationGameSeconds:R}.", elapsed);
            if (clock == null)
                return ActorRestResult.Failed(ActorRestFailureCode.WorldClockUnavailable, "World Clock authority is unavailable.", elapsed);

            clock.Bind(actorNeeds);
            double before = clock.ElapsedGameSeconds;
            if (!clock.TryAdvanceGameTime(durationGameSeconds, out string failure))
                return ActorRestResult.Failed(ActorRestFailureCode.ClockAdvanceRejected, "Rest did not advance time: " + failure, before);

            return ActorRestResult.Succeeded(before, clock.ElapsedGameSeconds);
        }
    }
}
