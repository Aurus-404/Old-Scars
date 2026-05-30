using System.Collections.Generic;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Feedback;
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
                RecordActionCompleted(action, executionContext);
                return DebugActionExecutionResult.None();
            }

            DebugActionExecutionResult result = DebugActionExecutionResult.None();
            bool hadTagMutationEffect = false;
            var addedTags = new List<string>();
            var removedTags = new List<string>();

            for (int index = 0; index < action.effects.Length; index++)
            {
                ActionEffectDefinition effect = action.effects[index];
                if (IsTagMutationEffect(effect))
                    hadTagMutationEffect = true;

                DebugActionExecutionResult effectResult = ExecuteEffect(effect, executionContext, action, index, addedTags, removedTags);
                if (effectResult.hasResult)
                    result = effectResult;
            }

            if (hadTagMutationEffect)
                LogTargetTagsAfterMutation(target, targetName);

            RecordTargetStateChanged(action, executionContext, addedTags, removedTags);
            RecordActionCompleted(action, executionContext);

            return result;
        }

        private static DebugActionExecutionResult ExecuteEffect(
            ActionEffectDefinition effect,
            DebugActionExecutionContext executionContext,
            ActionDefinition action,
            int effectIndex,
            List<string> addedTags,
            List<string> removedTags)
        {
            string actionId = action != null ? action.id : null;
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
                {
                    Debug.Log($"[DebugActionExecutor] {effectContext}: removed runtime tag '{effect.tag}' from target.");
                    removedTags.Add(effect.tag);
                }
                else if (!hadTag)
                {
                    Debug.Log($"[DebugActionExecutor] {effectContext}: runtime tag '{effect.tag}' was not present on target; no tag was removed.");
                }
                else
                {
                    Debug.Log($"[DebugActionExecutor] {effectContext}: runtime tag '{effect.tag}' could not be removed.");
                }

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
                {
                    Debug.Log($"[DebugActionExecutor] {effectContext}: added runtime tag '{effect.tag}' to target.");
                    addedTags.Add(effect.tag);
                }
                else if (hadTag)
                {
                    Debug.Log($"[DebugActionExecutor] {effectContext}: runtime tag '{effect.tag}' already existed on target; no tag was added.");
                }
                else
                {
                    Debug.Log($"[DebugActionExecutor] {effectContext}: runtime tag '{effect.tag}' could not be added.");
                }

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
                return ExecuteSearchContainer(effectContext, executionContext, action);
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

        private static DebugActionExecutionResult ExecuteSearchContainer(string effectContext, DebugActionExecutionContext executionContext, ActionDefinition action)
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
            return containerLoot.Search(executionContext, action);
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

        private static void RecordActionCompleted(ActionDefinition action, DebugActionExecutionContext executionContext)
        {
            string actionDisplayName = GetActionDisplayName(action);
            string equippedItemId = GetEquippedItemId(executionContext.EquippedItemId);

            GameplayFeedbackLog.TryRecord(new GameplayFeedbackEntry(
                GameplayFeedbackEntryType.ActionCompleted,
                $"Accion completada: {SafeText(actionDisplayName)}.",
                actorId: GetActorName(executionContext.ActorContext),
                actorDisplayName: GetActorName(executionContext.ActorContext),
                targetId: GetTargetName(executionContext.Target),
                targetDisplayName: GetTargetDisplayName(executionContext.Target),
                itemId: equippedItemId,
                itemDisplayName: GetItemDisplayName(equippedItemId),
                actionId: action != null ? action.id : null,
                actionDisplayName: actionDisplayName));
        }

        private static void RecordTargetStateChanged(ActionDefinition action, DebugActionExecutionContext executionContext, List<string> addedTags, List<string> removedTags)
        {
            if ((addedTags == null || addedTags.Count == 0) && (removedTags == null || removedTags.Count == 0))
                return;

            string targetDisplayName = GetTargetDisplayName(executionContext.Target);
            string equippedItemId = GetEquippedItemId(executionContext.EquippedItemId);

            GameplayFeedbackLog.TryRecord(new GameplayFeedbackEntry(
                GameplayFeedbackEntryType.TargetStateChanged,
                $"Estado actualizado: {SafeText(targetDisplayName)}.",
                actorId: GetActorName(executionContext.ActorContext),
                actorDisplayName: GetActorName(executionContext.ActorContext),
                targetId: GetTargetName(executionContext.Target),
                targetDisplayName: targetDisplayName,
                itemId: equippedItemId,
                itemDisplayName: GetItemDisplayName(equippedItemId),
                actionId: action != null ? action.id : null,
                actionDisplayName: GetActionDisplayName(action),
                addedTags: addedTags != null ? addedTags.ToArray() : null,
                removedTags: removedTags != null ? removedTags.ToArray() : null,
                debugOnly: true));
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

            string title = debugInfo.GetDisplayNameOrFallback(targetName, target);
            string body = debugInfo.GetInspectTextOrFallback(target);
            body = AppendContainerDebugStorageSummary(body, target);
            Debug.Log($"[DebugActionExecutor] {effectContext}: show_target_info for '{title}'.");
            return DebugActionExecutionResult.Info(title, body);
        }

        private static string AppendContainerDebugStorageSummary(string body, WorldObjectTags target)
        {
            ContainerLootComponent containerLoot = target != null ? target.GetComponent<ContainerLootComponent>() : null;
            if (containerLoot == null)
                return body;

            return SafeText(body) + "\n\n[DEBUG STORAGE]\n" + containerLoot.GetDebugStorageSummary();
        }

        private static string GetActionDisplayName(ActionDefinition action)
        {
            if (action == null)
                return null;

            if (action.display != null && !string.IsNullOrWhiteSpace(action.display.name))
                return action.display.name;

            return action.id;
        }

        private static string GetActorName(ActorInteractionContext actorContext)
        {
            return actorContext != null ? actorContext.name : null;
        }

        private static string GetTargetName(WorldObjectTags target)
        {
            return target != null ? target.name : null;
        }

        private static string GetTargetDisplayName(WorldObjectTags target)
        {
            if (target == null)
                return null;

            WorldObjectDebugInfo debugInfo = target.GetComponent<WorldObjectDebugInfo>();
            return debugInfo != null ? debugInfo.GetDisplayNameOrFallback(target.name) : target.name;
        }

        private static string GetEquippedItemId(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            string trimmedItemId = itemId.Trim();
            return trimmedItemId.ToLowerInvariant() == "none" ? null : trimmedItemId;
        }

        private static string GetItemDisplayName(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            if (GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
                return SafeText(itemId);

            GameDatabase database = GameDataManager.Instance.Database;
            ItemDefinition definition = database != null ? database.GetItem(itemId) : null;
            if (definition == null || definition.display == null || string.IsNullOrWhiteSpace(definition.display.name))
                return SafeText(itemId);

            return definition.display.name;
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
