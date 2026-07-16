namespace OldScars.Core.Data.Definitions
{
    [System.Serializable]
    public sealed class VisualRigProfileDefinition
    {
        public string type; // must be "visual_rig_profile"
        public string id;
        public string display_name;
        public string family_id;
        public VisualPartDefinition[] parts;
        public VisualSocketDefinition[] sockets;
        public VisualEquipmentSocketMappingDefinition[] equipment_slot_mappings;
    }

    [System.Serializable]
    public sealed class VisualPartDefinition
    {
        public string id;
        public string parent_part_id;
        public string damage_region_id;
        public bool detachable;
        public bool? enabled;
    }

    [System.Serializable]
    public sealed class VisualSocketDefinition
    {
        public string id;
        public string part_id;
        public string role;
        public string[] capabilities;
        public bool? enabled;
    }

    [System.Serializable]
    public sealed class VisualEquipmentSocketMappingDefinition
    {
        public string equipment_slot_id;
        public string socket_role;
    }
}
