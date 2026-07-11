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
        public int max_stack = 1;   // Simple runtime storage stacking limit.
        public bool? equippable;    // Transitional flat equip eligibility. Prefer equip.equippable.
        public ItemPhysical physical;
        public ItemEconomy economy;
        public ItemEquip equip;     // Optional equipment metadata for equippable items.
        public ItemCombat combat;   // null if not usable as a weapon.
        public ItemConsumable consumable; // null if not consumable.
        public ItemInventoryMetadata? inventory; // Optional spatial-inventory metadata. Missing data falls back to 1x1.
        public string firearm_profile_id; // references FirearmProfileDefinition.id
        public string ammo_profile_id;    // references AmmoProfileDefinition.id
    }

    [System.Serializable]
    public struct ItemInventoryMetadata
    {
        public ItemFootprintDefinition? footprint;
        public string icon_id;
    }

    [System.Serializable]
    public struct ItemFootprintDefinition
    {
        public int width;
        public int height;
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
        public bool? equippable;

        /// <summary>
        /// Equipment slots this item can occupy.
        ///
        /// Milestone 23 supports only right_hand. A future EquipmentSystem
        /// should expand this without changing item storage ownership.
        /// </summary>
        public string[] allowed_slots;

        /// <summary>
        /// Equipment slots occupied once the item is equipped.
        /// </summary>
        public string[] occupied_slots;
    }

    [System.Serializable]
    public sealed class ItemCombat
    {
        public string weapon_profile; // references WeaponProfileDefinition.id
        public ItemDamage damage;
        public string[] actions;      // references ActionDefinition.id[]
    }

    [System.Serializable]
    public sealed class ItemConsumable
    {
        public ItemNeedRestore[] restore_needs;
        public ItemHealthRestore restore_health;
    }

    [System.Serializable]
    public sealed class ItemNeedRestore
    {
        public string need_id;
        public float amount;
    }

    [System.Serializable]
    public sealed class ItemHealthRestore
    {
        public float amount;
    }

    [System.Serializable]
    public sealed class ItemDamage
    {
        public int min;
        public int max;
    }
}
