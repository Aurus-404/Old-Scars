using OldScars.Core.Data.Definitions;
using OldScars.Core.Items;
using UnityEngine;

namespace OldScars.Core.Interactions
{
    public static class DebugActionExecutor
    {
        private const string EffectTypeAddTag = "add_tag";
        private const string EffectTypeRemoveTag = "remove_tag";
        private const string EffectTypeShowTargetInfo = "show_target_info";
        private const string EffectTypePickUpItem = "pick_up_item";
        private const string EffectTypeSearchContainer = "search_container";
        private const string EffectTargetTarget = "target";

        public static DebugActionExecutionResult Execute(ActionDefinition action, WorldObjectTags target, string itemId)
        {
            return Execute(action, new DebugActionExecutionContext(null, target, itemId));
        }

        public static DebugActionExecutionResult Execute(ActionDefinition action, DebugActionExecutionContext executionContext)
        {
            if (action == null)
            {
                Debug.LogError("[DebugActionExecutor] Cannot execute a null action.");
                return DebugActionExecutionResult.None();
            }

            WorldObjectTags target = executionContext.Target;
            if (target == null)
            {
                Debug.LogError($"[DebugActionExecutor] Cannot execute '{SafeText(action.id)}' without a target.");
                return DebugActionExecutionResult.None();
            }

            string itemId = executionContext.EquippedItemId;
            string targetName = target.name;
            Debug.Log($"Executed debug action {SafeText(action.id)} on {SafeText(targetName)} using {SafeText(itemId)}");

            if (action.effects == null || action.effects.Length == 0)
            {
                Debug.Log($"[DebugActionExecutor] Action '{SafeText(action.id)}' has no debug effects.");
                return DebugActionExecutionResult.None();
            }

            DebugActionExecutionResult result = DebugActionExecutionResult.None();
            bool hadTagMutationEffect = false;

            for (int index = 0; index < action.effects.Length; index++)
            {
                ActionEffectDefinition effect = action.effects[index];
                if (IsTagMutationEffect(effect))
                    hadTagMutationEffect = true;

                DebugActionExecutionResult effectResult = ExecuteEffect(effect, executionContext, action.id, index);
                if (effectResult.hasResult)
                    result = effectResult;
            }

            if (hadTagMutationEffect)
                LogTargetTagsAfterMutation(target, targetName);

            return result;
        }

        private static DebugActionExecutionResult ExecuteEffect(ActionEffectDefinition effect, DebugActionExecutionContext executionContext, string actionId, int effectIndex)
        {
            string effectContext = $"Action '{SafeText(actionId)}' effect[{effectIndex}]";
            WorldObjectTags target = executionContext.Target;

            if (effect == null)
            {
                Debug.LogWarning($"[DebugActionExecutor] {effectContext}: effect is null.");
                return DebugActionExecutionResult.None();
            }

            if (string.IsNullOrWhiteSpace(effect.type))
            {
                Debug.LogWarning($"[DebugActionExecutor] {effectContext}: missing effect type.");
                return DebugActionExecutionResult.None();
            }

            if (effect.target != EffectTargetTarget)
            {
                Debug.LogWarning($"[DebugActionExecutor] {effectContext}: unsupported target '{SafeText(effect.target)}'.");
                return DebugActionExecutionResult.None();
            }

            if (effect.type == EffectTypeRemoveTag)
            {
                if (string.IsNullOrWhiteSpace(effect.tag))
                {
                    Debug.LogWarning($"[DebugActionExecutor] {effectContext}: missing tag.");
                    return DebugActionExecutionResult.None();
                }

                bool hadTag = target.HasTag(effect.tag);
                bool removed = target.RemoveTag(effect.tag);

                if (removed)
                    Debug.Log($"[DebugActionExecutor] {effectContext}: removed runtime tag '{effect.tag}' from target.");
                else if (!hadTag)
                    Debug.Log($"[DebugActionExecutor] {effectContext}: runtime tag '{effect.tag}' was not present on target; no tag was removed.");
                else
                    Debug.Log($"[DebugActionExecutor] {effectContext}: runtime tag '{effect.tag}' could not be removed.");

                return DebugActionExecutionResult.None();
            }

            if (effect.type == EffectTypeAddTag)
            {
                if (string.IsNullOrWhiteSpace(effect.tag))
                {
                    Debug.LogWarning($"[DebugActionExecutor] {effectContext}: missing tag.");
                    return DebugActionExecutionResult.None();
                }

                bool hadTag = target.HasTag(effect.tag);
                bool added = target.AddTag(effect.tag);

                if (added)
                    Debug.Log($"[DebugActionExecutor] {effectContext}: added runtime tag '{effect.tag}' to target.");
                else if (hadTag)
                    Debug.Log($"[DebugActionExecutor] {effectContext}: runtime tag '{effect.tag}' already existed on target; no tag was added.");
                else
                    Debug.Log($"[DebugActionExecutor] {effectContext}: runtime tag '{effect.tag}' could not be added.");

                return DebugActionExecutionResult.None();
            }

            if (effect.type == EffectTypeShowTargetInfo)
            {
                return BuildTargetInfoResult(target, effectContext);
            }

            if (effect.type == EffectTypePickUpItem)
            {
                return ExecutePickUpItem(effectContext, executionContext);
            }

            if (effect.type == EffectTypeSearchContainer)
            {
                return ExecuteSearchContainer(effectContext, executionContext);
            }

            Debug.LogWarning($"[DebugActionExecutor] {effectContext}: unsupported effect type '{effect.type}'.");
            return DebugActionExecutionResult.None();
        }

        private static DebugActionExecutionResult ExecutePickUpItem(string effectContext, DebugActionExecutionContext executionContext)
        {
            WorldObjectTags target = executionContext.Target;
            WorldItemPickup pickup = target != null ? target.GetComponent<WorldItemPickup>() : null;

            if (pickup == null)
            {
                string message = "Target has no WorldItemPickup component.";
                Debug.LogWarning($"[DebugActionExecutor] {effectContext}: {message}");
                return DebugActionExecutionResult.Info("Recoger", message);
            }

            Debug.Log($"[DebugActionExecutor] {effectContext}: pick_up_item for '{SafeText(pickup.ItemDefinitionId)}'.");
            return pickup.PickUp(executionContext.ActorContext, target);
        }

        private static DebugActionExecutionResult ExecuteSearchContainer(string effectContext, DebugActionExecutionContext executionContext)
        {
            WorldObjectTags target = executionContext.Target;
            ContainerLootComponent containerLoot = target != null ? target.GetComponent<ContainerLootComponent>() : null;

            if (containerLoot == null)
            {
                string message = "Target has no ContainerLootComponent component.";
                Debug.LogWarning($"[DebugActionExecutor] {effectContext}: {message}");
                return DebugActionExecutionResult.Info("Buscar contenedor", message);
            }

            Debug.Log($"[DebugActionExecutor] {effectContext}: search_container for loot table '{SafeText(containerLoot.LootTableId)}'.");
            return containerLoot.Search(executionContext);
        }

        private static bool IsTagMutationEffect(ActionEffectDefinition effect)
        {
            if (effect == null)
                return false;

            return effect.type == EffectTypeAddTag || effect.type == EffectTypeRemoveTag;
        }

        private static void LogTargetTagsAfterMutation(WorldObjectTags target, string targetName)
        {
            Debug.Log(
                "[DebugActionExecutor] Target tag state after runtime tag effects:" +
                $"\n  Target: {SafeText(targetName)}" +
                $"\n  Initial tags: {FormatTags(target.InitialTags)}" +
                $"\n  Runtime tags: {FormatTags(target.RuntimeTags)}");
        }

        private static DebugActionExecutionResult BuildTargetInfoResult(WorldObjectTags target, string effectContext)
        {
            WorldObjectDebugInfo debugInfo = target.GetComponent<WorldObjectDebugInfo>();
            string targetName = SafeText(target.name);

            if (debugInfo == null)
            {
                string message = $"Target '{targetName}' has no WorldObjectDebugInfo component.";
                Debug.LogWarning($"[DebugActionExecutor] {effectContext}: {message}");
                return DebugActionExecutionResult.Info(targetName, message);
            }

            string title = debugInfo.GetDisplayNameOrFallback(targetName);
            string body = debugInfo.GetInspectTextOrFallback();
            Debug.Log($"[DebugActionExecutor] {effectContext}: show_target_info for '{title}'.");
            return DebugActionExecutionResult.Info(title, body);
        }

        private static string FormatTags(string[] tags)
        {
            return tags != null && tags.Length > 0 ? string.Join(", ", tags) : "(none)";
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
        }
    }
}
