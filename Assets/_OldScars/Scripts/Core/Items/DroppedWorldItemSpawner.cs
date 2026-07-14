using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Feedback;
using OldScars.Core.Interactions;
using UnityEngine;

namespace OldScars.Core.Items
{
    /// <summary>
    /// Runtime-only helper for spawning simple debug world items near an actor.
    /// </summary>
    public static class DroppedWorldItemSpawner
    {
        private const string WorldItemTag = "world_item";
        private const string PickupableTag = "pickupable";
        private const string InspectableTag = "inspectable";
        private const float DropDistance = 1.25f;

        public static bool TryDrop(
            InventoryComponent inventory,
            int sourceIndex,
            int requestedQuantity,
            string dropActionId,
            string dropActionDisplayName,
            out string message)
        {
            message = null;

            ItemStorageEntry sourceEntry = inventory != null ? inventory.GetEntry(sourceIndex) : null;
            if (inventory == null || sourceEntry == null || sourceEntry.Item == null || requestedQuantity < 1)
            {
                message = "Drop failed: invalid inventory item.";
                return false;
            }

            int dropQuantity = Mathf.Min(requestedQuantity, sourceEntry.Quantity);
            string definitionId = sourceEntry.DefinitionId;
            string instanceId = sourceEntry.Item.InstanceId;
            string displayName = GetItemDisplayName(definitionId);
            bool wasEquipped = inventory.IsRightHandEquippedIndex(sourceIndex);

            GameObject worldItem = CreateWorldItemRoot(inventory.transform, displayName);
            WorldItemPickup pickup = worldItem.AddComponent<WorldItemPickup>();
            int transferredQuantity = pickup.ReceiveDroppedItem(inventory, sourceIndex, dropQuantity);
            if (transferredQuantity <= 0)
            {
                Object.Destroy(worldItem);
                message = $"Drop failed: {displayName} was not transferred.";
                return false;
            }

            bool rightHandCleared = wasEquipped && inventory.GetRightHandItemInstance() == null;

            RecordDrop(
                inventory,
                worldItem,
                definitionId,
                displayName,
                transferredQuantity,
                dropActionId,
                dropActionDisplayName,
                rightHandCleared);

            Debug.Log(
                $"[DroppedWorldItemSpawner] Dropped {definitionId} x{transferredQuantity} [{instanceId}] " +
                $"at {worldItem.transform.position}.");

            message = $"Dropped {displayName} x{transferredQuantity}.";
            return true;
        }

        public static bool TryDrop(
            IGridStorageOwner sourceOwner,
            string sourceInstanceId,
            InventoryComponent actorInventory,
            int requestedQuantity,
            string dropActionId,
            string dropActionDisplayName,
            out string message)
        {
            message = null;
            if (sourceOwner == null || actorInventory == null ||
                !ItemOwnedStorageRegistry.Instance.ShareRootOwner(sourceOwner, actorInventory) ||
                !sourceOwner.TryGetEntryByInstanceId(sourceInstanceId, out _, out ItemStorageEntry sourceEntry) ||
                sourceEntry?.Item == null || requestedQuantity < 1)
            {
                message = "Drop failed: invalid actor-owned item.";
                return false;
            }

            int dropQuantity = Mathf.Min(requestedQuantity, sourceEntry.Quantity);
            string definitionId = sourceEntry.DefinitionId;
            string displayName = GetItemDisplayName(definitionId);
            GameObject worldItem = CreateWorldItemRoot(actorInventory.transform, displayName);
            WorldItemPickup pickup = worldItem.AddComponent<WorldItemPickup>();
            int transferredQuantity = pickup.ReceiveDroppedItem(sourceOwner, sourceInstanceId, dropQuantity);
            if (transferredQuantity <= 0)
            {
                Object.Destroy(worldItem);
                message = $"Drop failed: {displayName} was not transferred.";
                return false;
            }

            RecordDrop(
                actorInventory,
                worldItem,
                definitionId,
                displayName,
                transferredQuantity,
                dropActionId,
                dropActionDisplayName,
                false);
            message = $"Dropped {displayName} x{transferredQuantity}.";
            return true;
        }

        private static GameObject CreateWorldItemRoot(Transform actorTransform, string displayName)
        {
            var worldItem = new GameObject($"Dropped World Item - {displayName}");
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer >= 0)
                worldItem.layer = interactableLayer;

            worldItem.transform.position = GetDropPosition(actorTransform);
            worldItem.transform.rotation = Quaternion.identity;

            var collider = worldItem.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.25f, 0f);
            collider.size = new Vector3(0.8f, 0.5f, 0.8f);

            WorldObjectTags tags = worldItem.AddComponent<WorldObjectTags>();
            tags.ApplyInitialTags(new[] { WorldItemTag, PickupableTag, InspectableTag });

            WorldObjectDebugInfo debugInfo = worldItem.AddComponent<WorldObjectDebugInfo>();
            debugInfo.SetRuntimeDisplayName(displayName);
            debugInfo.SetRuntimeInspectText($"Dropped world item: {displayName}.");

            return worldItem;
        }

        private static Vector3 GetDropPosition(Transform actorTransform)
        {
            if (actorTransform == null)
                return Vector3.up * 0.15f;

            Vector3 forward = actorTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;

            Vector3 candidate = actorTransform.position + forward.normalized * DropDistance;
            int groundLayer = LayerMask.NameToLayer("Ground");
            int groundMask = groundLayer >= 0 ? 1 << groundLayer : Physics.DefaultRaycastLayers;

            if (Physics.Raycast(candidate + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f, groundMask, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.05f;

            return candidate + Vector3.up * 0.15f;
        }

        private static void RecordDrop(
            InventoryComponent inventory,
            GameObject worldItem,
            string definitionId,
            string displayName,
            int quantity,
            string actionId,
            string actionDisplayName,
            bool rightHandCleared)
        {
            string actorName = inventory != null ? inventory.name : null;
            string targetName = worldItem != null ? worldItem.name : null;

            GameplayFeedbackLog.TryRecord(new GameplayFeedbackEntry(
                GameplayFeedbackEntryType.ItemDropped,
                $"Dropped {displayName}{(actionId == "drop_stack" ? " stack" : string.Empty)} x{quantity}.",
                actorId: actorName,
                actorDisplayName: actorName,
                targetId: targetName,
                targetDisplayName: displayName,
                itemId: definitionId,
                itemDisplayName: displayName,
                actionId: actionId,
                actionDisplayName: actionDisplayName,
                quantity: quantity));

            GameplayFeedbackLog.TryRecord(new GameplayFeedbackEntry(
                GameplayFeedbackEntryType.WorldItemSpawned,
                $"World item spawned: {displayName} x{quantity}.",
                actorId: actorName,
                actorDisplayName: actorName,
                targetId: targetName,
                targetDisplayName: displayName,
                itemId: definitionId,
                itemDisplayName: displayName,
                quantity: quantity,
                debugOnly: true));

            if (!rightHandCleared)
                return;

            GameplayFeedbackLog.TryRecord(new GameplayFeedbackEntry(
                GameplayFeedbackEntryType.ItemUnequipped,
                "Dropped equipped item; right_hand cleared.",
                actorId: actorName,
                actorDisplayName: actorName,
                itemId: definitionId,
                itemDisplayName: displayName,
                quantity: 1));
        }

        private static string GetItemDisplayName(string definitionId)
        {
            if (GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
                return SafeText(definitionId);

            GameDatabase database = GameDataManager.Instance.Database;
            ItemDefinition definition = database != null ? database.GetItem(definitionId) : null;
            if (definition == null || definition.display == null || string.IsNullOrWhiteSpace(definition.display.name))
                return SafeText(definitionId);

            return definition.display.name;
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
        }
    }
}
