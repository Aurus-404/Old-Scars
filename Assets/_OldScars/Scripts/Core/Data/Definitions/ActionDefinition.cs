using System.Collections.Generic;

namespace OldScars.Core.Data.Definitions
{
    /// <summary>
    /// Definition of an executable action: combat move or contextual interaction.
    ///
    /// In Old Scars, exploration and tactical situations should speak the same
    /// action language. This is the data definition; execution will belong to
    /// future C# command/action systems.
    /// </summary>
    [System.Serializable]
    public sealed class ActionDefinition
    {
        public string type; // must be "action"
        public string id;
        public string[] contexts;
        public ActionDisplay display;
        public ActionRequirements requirements;
        public ActionCost cost;
        public ActionModifiers modifiers;
        public ActionEffectDefinition[] effects;
        public bool interruptible;
    }

    [System.Serializable]
    public sealed class ActionEffectDefinition
    {
        public string type;
        public string target;
        public string tag;
        public float amount;
    }

    [System.Serializable]
    public sealed class ActionDisplay
    {
        public string name;
        public string description;
        public string failure_text;
    }

    [System.Serializable]
    public sealed class ActionRequirements
    {
        public string[] actor_tags;
        public string[] target_tags;
        // Legacy-compatible schema name. In contextual actions this currently
        // means required equipped item tags, not final equipment/weapon logic.
        public string[] weapon_tags;
        public Dictionary<string, float> actor_min_stats;
    }

    [System.Serializable]
    public sealed class ActionCost
    {
        public float stamina;
        public float time;
    }

    [System.Serializable]
    public sealed class ActionModifiers
    {
        public float damage_multiplier;
        public float accuracy_modifier;
    }
}
