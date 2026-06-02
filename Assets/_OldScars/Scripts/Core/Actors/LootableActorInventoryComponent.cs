using System.Collections.Generic;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Feedback;
using OldScars.Core.Interactions;
using OldScars.Core.Items;
using UnityEngine;

namespace OldScars.Core.Actors
{
    public sealed class LootableActorInventoryComponent : MonoBehaviour, IItemStorageDebugSource
    {
        [SerializeField] private InventoryComponent inventory;
        [SerializeField] private WorldObjectTags worldObjectTags;

        public bool HasStoredItems => inventory != null && !inventory.IsEmpty;
        public IReadOnlyList<ItemStorageEntry> StorageEntries => inventory != null ? inventory.GetStorageEntries() : EmptyEntries;

        private static readonly ItemStorageEntry[] EmptyEntries = new ItemStorageEntry[0];

        private void Awake()
        {
            ResolveReferences();
            SyncLootableTag();
        }

        private void LateUpdate()
        {
            SyncLootableTag();
        }

        public string GetStorageDebugTitle(WorldObjectTags target)
        {
            string targetName = target != null ? target.name : name;
            WorldObjectDebugInfo debugInfo = target != null ? target.GetComponent<WorldObjectDebugInfo>() : null;
            string displayName = debugInfo != null ? debugInfo.GetDisplayNameOrFallback(targetName, target) : targetName;
            return $"{displayName} Inventory (Debug)";
        }

        public bool CanOpenStorage(out string reason)
        {
            return CanAccessActorInventory(out reason);
        }

        public int TakeItem(int storageIndex, int quantity, InventoryComponent targetInventory, DebugActionExecutionContext executionContext, ActionDefinition action, out string message)
        {
            ResolveReferences();
            message = null;

            if (quantity < 1)
            {
                message = "Cantidad invalida.";
                return 0;
            }

            if (!CanAccessActorInventory(out string accessReason))
            {
                message = accessReason;
                return 0;
            }

            if (inventory == null)
            {
                message = "El actor muerto no tiene InventoryComponent.";
                return 0;
            }

            if (targetInventory == null)
            {
                message = "El actor no tiene inventario v0 configurado.";
                return 0;
            }

            ItemStorageEntry entry = inventory.GetEntry(storageIndex);
            if (entry == null || entry.Item == null)
            {
                message = "Slot de actor invalido.";
                return 0;
            }

            string definitionId = entry.DefinitionId;
            int requestedQuantity = Mathf.Min(quantity, entry.Quantity);
            int transferredQuantity = inventory.TransferItemTo(targetInventory, storageIndex, requestedQuantity);
            if (transferredQuantity <= 0)
            {
                message = "No se pudo transferir contenido.";
                return 0;
            }

            RecordLootReceived(definitionId, transferredQuantity, executionContext, action);
            SyncLootableTag();

            message = $"Tomaste {GetItemDisplayName(definitionId)} x{transferredQuantity}.";
            return transferredQuantity;
        }

        private bool CanAccessActorInventory(out string reason)
        {
            ResolveReferences();
            reason = null;

            if (worldObjectTags == null)
            {
                reason = "Error: actor sin tags de mundo.";
                return false;
            }

            if (!worldObjectTags.HasTag(ActorHealthComponent.DeadActorTag))
            {
                reason = "Este actor no esta muerto.";
                return false;
            }

            if (!worldObjectTags.HasTag(ActorHealthComponent.LootableActorTag))
            {
                reason = "Este cuerpo ya no se puede saquear.";
                return false;
            }

            if (inventory == null || inventory.IsEmpty)
            {
                SyncLootableTag();
                reason = "No queda contenido en este cuerpo.";
                return false;
            }

            return true;
        }

        private void SyncLootableTag()
        {
            ResolveReferences();
            if (worldObjectTags == null || inventory == null)
                return;

            if (!worldObjectTags.HasTag(ActorHealthComponent.DeadActorTag))
                return;

            if (inventory.IsEmpty)
                worldObjectTags.RemoveTag(ActorHealthComponent.LootableActorTag);
            else
                worldObjectTags.AddTag(ActorHealthComponent.LootableActorTag);
        }

        private void ResolveReferences()
        {
            if (inventory == null)
                inventory = GetComponent<InventoryComponent>();

            if (worldObjectTags == null)
                worldObjectTags = GetComponent<WorldObjectTags>();
        }

        private static void RecordLootReceived(string definitionId, int quantity, DebugActionExecutionContext executionContext, ActionDefinition action)
        {
            string displayName = GetItemDisplayName(definitionId);
            GameplayFeedbackLog.TryRecord(new GameplayFeedbackEntry(
                GameplayFeedbackEntryType.LootReceived,
                $"Encontraste {displayName} x{quantity}.",
                actorId: GetActorName(executionContext.ActorContext),
                actorDisplayName: GetActorName(executionContext.ActorContext),
                targetId: GetTargetName(executionContext.Target),
                targetDisplayName: GetTargetDisplayName(executionContext.Target),
                itemId: definitionId,
                itemDisplayName: displayName,
                actionId: action != null ? action.id : null,
                actionDisplayName: GetActionDisplayName(action),
                quantity: quantity));
        }

        private static string GetItemDisplayName(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
                return "(none)";

            if (GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
                return definitionId;

            GameDatabase database = GameDataManager.Instance.Database;
            ItemDefinition definition = database != null ? database.GetItem(definitionId) : null;
            if (definition == null || definition.display == null || string.IsNullOrWhiteSpace(definition.display.name))
                return definitionId;

            return definition.display.name;
        }

        private static string GetActorName(ActorInteractionContext actorContext)
        {
            return actorContext != null ? actorContext.name : null;
        }

        private static string GetTargetName(WorldObjectTags targetTags)
        {
            return targetTags != null ? targetTags.name : null;
        }

        private static string GetTargetDisplayName(WorldObjectTags targetTags)
        {
            if (targetTags == null)
                return null;

            WorldObjectDebugInfo debugInfo = targetTags.GetComponent<WorldObjectDebugInfo>();
            return debugInfo != null ? debugInfo.GetDisplayNameOrFallback(targetTags.name) : targetTags.name;
        }

        private static string GetActionDisplayName(ActionDefinition action)
        {
            if (action == null)
                return null;

            return action.display != null && !string.IsNullOrWhiteSpace(action.display.name)
                ? action.display.name
                : action.id;
        }
    }
}
