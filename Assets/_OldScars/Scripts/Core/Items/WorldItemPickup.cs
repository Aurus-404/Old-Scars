using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Feedback;
using OldScars.Core.Interactions;
using UnityEngine;

namespace OldScars.Core.Items
{
    /// <summary>
    /// Minimal world item pickup bridge for configured and runtime-dropped items.
    ///
    /// This is not a final loot, ownership, persistence, or world item system.
    /// It owns one runtime ItemStorage entry so pickup can reuse the same
    /// instance-preserving transfer rules as inventories and containers.
    /// </summary>
    public sealed class WorldItemPickup : MonoBehaviour
    {
        private const string PickupableTag = "pickupable";
        private const string PickedUpTag = "picked_up";

        [SerializeField] private string itemDefinitionId;

        private readonly ItemStorage storage = new ItemStorage();
        private bool destroyAfterPickup;

        public string ItemDefinitionId
        {
            get
            {
                ItemStorageEntry entry = storage.GetEntry(0);
                return entry != null ? entry.DefinitionId : itemDefinitionId;
            }
        }

        public int Quantity
        {
            get
            {
                ItemStorageEntry entry = storage.GetEntry(0);
                return entry != null ? entry.Quantity : 0;
            }
        }

        public int ReceiveDroppedItem(InventoryComponent sourceInventory, int sourceIndex, int quantity)
        {
            if (sourceInventory == null || !storage.IsEmpty || quantity < 1)
                return 0;

            int transferredQuantity = sourceInventory.TransferItemTo(storage, sourceIndex, quantity);
            if (transferredQuantity <= 0)
                return 0;

            ItemStorageEntry entry = storage.GetEntry(0);
            itemDefinitionId = entry != null ? entry.DefinitionId : itemDefinitionId;
            destroyAfterPickup = true;
            return transferredQuantity;
        }

        public DebugActionExecutionResult PickUp(ActorInteractionContext actorContext, WorldObjectTags targetTags)
        {
            if (actorContext == null)
            {
                Debug.LogWarning("[WorldItemPickup] Cannot pick up item without an actor context.");
                return DebugActionExecutionResult.Info("Recoger", "No hay actor configurado para recoger este objeto.");
            }

            if (targetTags == null)
            {
                Debug.LogWarning("[WorldItemPickup] Cannot pick up item without target tags.");
                return DebugActionExecutionResult.Info("Recoger", "Este objeto no tiene tags de mundo configurados.");
            }

            if (targetTags.HasTag(PickedUpTag) || !targetTags.HasTag(PickupableTag))
                return DebugActionExecutionResult.Info("Recoger", "Este objeto ya fue recogido o no se puede recoger.");

            if (!EnsureConfiguredItemStorage())
            {
                Debug.LogWarning($"[WorldItemPickup] Item definition '{SafeText(itemDefinitionId)}' was not found or data is not ready.");
                return DebugActionExecutionResult.Info("Recoger", $"No se pudo validar '{SafeText(itemDefinitionId)}'.");
            }

            InventoryComponent inventory = actorContext.GetInventoryComponent();
            if (inventory == null)
            {
                Debug.LogWarning("[WorldItemPickup] Actor has no InventoryComponent. Add InventoryComponent to the Debug Player for Milestone 14 pickup tests.");
                return DebugActionExecutionResult.Info("Recoger", "El actor no tiene inventario v0 configurado.");
            }

            ItemStorageEntry pickupEntry = storage.GetEntry(0);
            ItemInstance item = pickupEntry != null ? pickupEntry.Item : null;
            int pickupQuantity = pickupEntry != null ? pickupEntry.Quantity : 0;
            int transferredQuantity = inventory.TransferItemFrom(storage, 0, pickupQuantity);
            if (item == null || transferredQuantity <= 0)
                return DebugActionExecutionResult.Info("Recoger", $"No se pudo recoger '{SafeText(itemDefinitionId)}'.");

            bool addedPickedUp = targetTags.AddTag(PickedUpTag);
            bool removedPickupable = targetTags.RemoveTag(PickupableTag);
            DisableVisiblePickupParts();

            string displayName = GetItemDisplayName(item.DefinitionId);
            RecordItemPickedUp(actorContext, targetTags, item, displayName, transferredQuantity);
            RecordTargetStateChanged(actorContext, targetTags, addedPickedUp, removedPickupable);

            Debug.Log($"[WorldItemPickup] Picked up {item.DefinitionId} x{transferredQuantity} [{item.InstanceId}].");

            if (destroyAfterPickup)
                Destroy(gameObject);

            return DebugActionExecutionResult.Info("Recoger", $"Recogiste {displayName} x{transferredQuantity}.");
        }

        private bool EnsureConfiguredItemStorage()
        {
            if (!storage.IsEmpty)
                return true;

            ItemDefinition definition = GetItemDefinition(itemDefinitionId);
            if (definition == null)
                return false;

            storage.AddItem(new ItemInstance(definition));
            return true;
        }

        private void DisableVisiblePickupParts()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>();
            for (int index = 0; index < colliders.Length; index++)
                colliders[index].enabled = false;

            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            for (int index = 0; index < renderers.Length; index++)
                renderers[index].enabled = false;
        }

        private static string GetItemDisplayName(string definitionId)
        {
            ItemDefinition definition = GetItemDefinition(definitionId);
            if (definition == null || definition.display == null || string.IsNullOrWhiteSpace(definition.display.name))
                return SafeText(definitionId);

            return definition.display.name;
        }

        private static void RecordItemPickedUp(
            ActorInteractionContext actorContext,
            WorldObjectTags targetTags,
            ItemInstance item,
            string itemDisplayName,
            int quantity)
        {
            GameplayFeedbackLog.TryRecord(new GameplayFeedbackEntry(
                GameplayFeedbackEntryType.ItemPickedUp,
                $"Picked up {SafeText(itemDisplayName)} x{quantity}.",
                actorId: GetActorName(actorContext),
                actorDisplayName: GetActorName(actorContext),
                targetId: GetTargetName(targetTags),
                targetDisplayName: GetTargetDisplayName(targetTags),
                itemId: item != null ? item.DefinitionId : null,
                itemDisplayName: itemDisplayName,
                quantity: quantity));
        }

        private static void RecordTargetStateChanged(ActorInteractionContext actorContext, WorldObjectTags targetTags, bool addedPickedUp, bool removedPickupable)
        {
            if (!addedPickedUp && !removedPickupable)
                return;

            GameplayFeedbackLog.TryRecord(new GameplayFeedbackEntry(
                GameplayFeedbackEntryType.TargetStateChanged,
                $"Estado actualizado: {SafeText(GetTargetDisplayName(targetTags))}.",
                actorId: GetActorName(actorContext),
                actorDisplayName: GetActorName(actorContext),
                targetId: GetTargetName(targetTags),
                targetDisplayName: GetTargetDisplayName(targetTags),
                addedTags: addedPickedUp ? new[] { PickedUpTag } : null,
                removedTags: removedPickupable ? new[] { PickupableTag } : null,
                debugOnly: true));
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

        private static ItemDefinition GetItemDefinition(string definitionId)
        {
            if (GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
                return null;

            GameDatabase database = GameDataManager.Instance.Database;
            return database != null ? database.GetItem(definitionId) : null;
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
        }
    }
}
