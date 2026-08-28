namespace OldScars.Core.Combat
{
    /// <summary>
    /// Canonical data values for firearm trigger behavior. This is deliberately
    /// a small input-policy contract: WeaponCombatService remains authoritative
    /// for ammunition consumption and physical combat resolution.
    /// </summary>
    public static class FirearmActionModes
    {
        public const string ManualCycle = "manual_cycle";
        public const string SemiAutomatic = "semi_automatic";
        public const string Automatic = "automatic";

        public static bool IsDefined(string value) =>
            value == ManualCycle || value == SemiAutomatic || value == Automatic;

        public static bool UsesHeldTrigger(string value) => value == Automatic;

        public static bool ShouldAttemptFire(string value, bool wasPressedThisFrame, bool isHeld) =>
            UsesHeldTrigger(value) ? isHeld : wasPressedThisFrame;

        public static string DisplayName(string value)
        {
            switch (value)
            {
                case ManualCycle: return "Manual cycle";
                case SemiAutomatic: return "Semi-automatic";
                case Automatic: return "Automatic";
                default: return "Invalid fire mode";
            }
        }
    }
}
