namespace OldScars.Core.Data.Definitions
{
    public static class ItemVisualSocketPolicy
    {
        public const string EquipmentSlot = "equipment_slot";
        public const string PreferredRoleThenCapability = "preferred_role_then_capability";
    }

    public static class ItemVisualFallback
    {
        public const string None = "none";
        public const string DebugBox = "debug_box";
    }

    [System.Serializable]
    public sealed class ItemVisualProfileDefinition
    {
        public string type; // must be "item_visual_profile"
        public string id;
        public string item_definition_id;
        public string world_asset_key;
        public string equipped_asset_key;
        public string socket_policy;
        public string primary_socket_role;
        public string[] required_socket_capabilities;
        public string persistent_pose_id;
        public string primary_grip_binding;
        public string secondary_grip_binding;
        public string fallback_visual;
        public bool? enabled;
    }
}
