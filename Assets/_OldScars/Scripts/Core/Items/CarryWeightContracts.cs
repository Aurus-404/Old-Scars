namespace OldScars.Core.Items
{
    public enum CarryWeightState { Normal, Encumbered, Overloaded }

    public readonly struct CarryWeightSnapshot
    {
        public CarryWeightSnapshot(double currentWeightKg, double effectiveLoadKg, double carryCapacityKg, double loadRatio,
            CarryWeightState state, float locomotionFactor, bool isValid, string error)
        {
            CurrentWeightKg = currentWeightKg;
            EffectiveLoadKg = effectiveLoadKg;
            CarryCapacityKg = carryCapacityKg;
            LoadRatio = loadRatio;
            State = state;
            LocomotionFactor = locomotionFactor;
            IsValid = isValid;
            Error = error;
        }
        // Complete physical mass, including each owned-storage subtree exactly once.
        public double CurrentWeightKg { get; }
        // Derived locomotion load; only equipped carriers may discount their contents.
        public double EffectiveLoadKg { get; }
        public double CarryCapacityKg { get; }
        public double LoadRatio { get; }
        public CarryWeightState State { get; }
        public float LocomotionFactor { get; }
        public bool IsValid { get; }
        public string Error { get; }
        public static CarryWeightSnapshot Invalid(string error) =>
            new CarryWeightSnapshot(0d, 0d, 0d, 0d, CarryWeightState.Normal, 1f, false, error);
    }
}
