namespace OldScars.Core.Items
{
    public enum CarryWeightState
    {
        Normal,
        Encumbered,
        HardBlocked
    }

    public readonly struct CarryWeightSnapshot
    {
        public CarryWeightSnapshot(
            double currentWeightKg,
            double softCapacityKg,
            double hardLimitKg,
            double encumbranceRatio,
            CarryWeightState state,
            bool isValid,
            string error)
        {
            CurrentWeightKg = currentWeightKg;
            SoftCapacityKg = softCapacityKg;
            HardLimitKg = hardLimitKg;
            EncumbranceRatio = encumbranceRatio;
            State = state;
            IsValid = isValid;
            Error = error;
        }

        public double CurrentWeightKg { get; }
        public double SoftCapacityKg { get; }
        public double HardLimitKg { get; }
        public double EncumbranceRatio { get; }
        public CarryWeightState State { get; }
        public bool IsValid { get; }
        public string Error { get; }

        public static CarryWeightSnapshot Invalid(string error)
        {
            return new CarryWeightSnapshot(0d, 0d, 0d, 0d, CarryWeightState.Normal, false, error);
        }
    }

    public readonly struct CarryWeightAcceptance
    {
        public CarryWeightAcceptance(
            bool accepted,
            double addedWeightKg,
            double currentWeightKg,
            double projectedWeightKg,
            double hardLimitKg,
            CarryWeightState state,
            string failureReason)
        {
            Accepted = accepted;
            AddedWeightKg = addedWeightKg;
            CurrentWeightKg = currentWeightKg;
            ProjectedWeightKg = projectedWeightKg;
            HardLimitKg = hardLimitKg;
            State = state;
            FailureReason = failureReason;
        }

        public bool Accepted { get; }
        public double AddedWeightKg { get; }
        public double CurrentWeightKg { get; }
        public double ProjectedWeightKg { get; }
        public double HardLimitKg { get; }
        public CarryWeightState State { get; }
        public string FailureReason { get; }

        public static CarryWeightAcceptance Unlimited()
        {
            return new CarryWeightAcceptance(true, 0d, 0d, 0d, double.PositiveInfinity, CarryWeightState.Normal, null);
        }
    }

    public interface ICarryWeightLimitedOwner
    {
        bool HasCarryWeightLimit { get; }
        CarryWeightSnapshot GetCarryWeightSnapshot();
        CarryWeightAcceptance EvaluateIncomingWeight(string definitionId, int quantity);
    }
}
