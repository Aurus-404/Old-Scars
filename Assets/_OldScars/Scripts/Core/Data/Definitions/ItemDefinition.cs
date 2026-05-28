namespace OldScars.Core.Data.Definitions
{
    /// <summary>
    /// Immutable definition of an item type loaded from JSON.
    ///
    /// This describes what a kind of item is. It does not describe a concrete
    /// runtime item instance. Future save data should store definition_id plus
    /// mutable instance data such as condition, owner, location and quantity.
    /// </summary>
    [System.Serializable]
    public sealed class ItemDefinition
    {
        public string type; // must be "item"
        public string id;
        public ItemDisplay display;
        public string[] categories; // UI grouping only; not system logic.
        public string[] tags;       // System-facing identifiers.
        public ItemPhysical physical;
        public ItemEconomy economy;
        public ItemEquip equip;     // null if not equippable.
        public ItemCombat combat;   // null if not usable as a weapon.
    }

    [System.Serializable]
    public sealed class ItemDisplay
    {
        public string name;
        public string description;
    }

    [System.Serializable]
    public sealed class ItemPhysical
    {
        public float weight_kg;
        public float volume_l;
        public int condition_max;
    }

    [System.Serializable]
    public sealed class ItemEconomy
    {
        public int base_buy_value;
        public int base_sell_value;
    }

    [System.Serializable]
    public sealed class ItemEquip
    {
        /// <summary>
        /// Equipment slots this item can occupy.
        ///
        /// Important Milestone 1 decision:
        /// We do not use required_sockets yet. A future EquipmentSystem should
        /// check whether the actor/entity has the selected slot/socket.
        ///
        /// Example: allowed_slots ["right_hand", "left_hand"] means the item
        /// can be equipped in either hand, not both at the same time.
        /// </summary>
        public string[] allowed_slots;
    }

    [System.Serializable]
    public sealed class ItemCombat
    {
        public string weapon_profile; // references WeaponProfileDefinition.id
        public ItemDamage damage;
        public string[] actions;      // references ActionDefinition.id[]
    }

    [System.Serializable]
    public sealed class ItemDamage
    {
        public int min;
        public int max;
    }
}
