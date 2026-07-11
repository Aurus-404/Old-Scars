namespace OldScars.Core.Items
{
    public readonly struct GridPlacementValidationResult
    {
        public bool IsValid { get; }
        public InventoryMutationResult.MutationFailure Failure { get; }
        public string Message { get; }
        public GridPlacement Candidate { get; }
        public bool UsedFallbackFootprint { get; }

        private GridPlacementValidationResult(
            bool isValid,
            InventoryMutationResult.MutationFailure failure,
            string message,
            GridPlacement candidate,
            bool usedFallbackFootprint)
        {
            IsValid = isValid;
            Failure = failure;
            Message = message;
            Candidate = candidate;
            UsedFallbackFootprint = usedFallbackFootprint;
        }

        internal static GridPlacementValidationResult Valid(GridPlacement candidate, bool usedFallbackFootprint)
        {
            return new GridPlacementValidationResult(
                true,
                InventoryMutationResult.MutationFailure.None,
                null,
                candidate,
                usedFallbackFootprint);
        }

        internal static GridPlacementValidationResult Invalid(
            InventoryMutationResult.MutationFailure failure,
            string message)
        {
            return new GridPlacementValidationResult(false, failure, message, null, false);
        }
    }
}
