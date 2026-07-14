namespace OldScars.Core.Data.Definitions
{
    [System.Serializable]
    public sealed class EquipmentLayoutDefinition
    {
        public string type; // must be "equipment_layout"
        public string id;
        public string display_name;
        public EquipmentLayoutGroupDefinition[] groups;
        public EquipmentLayoutSlotDefinition[] slots;
    }

    [System.Serializable]
    public sealed class EquipmentLayoutGroupDefinition
    {
        public string id;
        public string display_name;
        public int display_order;
    }

    [System.Serializable]
    public sealed class EquipmentLayoutSlotDefinition
    {
        public string slot_id;
        public string group_id;
        public int display_order;
    }
}
