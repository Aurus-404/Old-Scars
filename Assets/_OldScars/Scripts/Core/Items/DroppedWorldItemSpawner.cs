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

            BuildPlaceholderVisual(worldItem.transform, definitionId);
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

        private static void BuildPlaceholderVisual(Transform root, string definitionId)
        {
            if (definitionId == "rusted_crowbar_01")
            {
                CreatePrimitive(root, PrimitiveType.Cylinder, new Vector3(0f, 0.25f, 0f), new Vector3(0.08f, 0.65f, 0.08f), new Vector3(0f, 0f, 90f), new Color(0.45f, 0.48f, 0.5f));
                return;
            }

            if (definitionId == "bandage_01")
            {
                CreatePrimitive(root, PrimitiveType.Cube, new Vector3(0f, 0.2f, 0f), new Vector3(0.5f, 0.18f, 0.35f), Vector3.zero, new Color(0.9f, 0.9f, 0.82f));
                return;
            }

            if (definitionId == "water_bottle_01")
            {
                CreatePrimitive(root, PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 0f), new Vector3(0.22f, 0.3f, 0.22f), Vector3.zero, new Color(0.15f, 0.45f, 0.9f));
                return;
            }

            if (definitionId == "food_ration_01")
            {
                CreatePrimitive(root, PrimitiveType.Cube, new Vector3(0f, 0.2f, 0f), new Vector3(0.55f, 0.25f, 0.4f), Vector3.zero, new Color(0.75f, 0.35f, 0.08f));
                return;
            }

            if (definitionId == "scrap_metal_01")
            {
                CreatePrimitive(root, PrimitiveType.Cube, new Vector3(0f, 0.2f, 0f), new Vector3(0.45f, 0.25f, 0.45f), new Vector3(8f, 22f, 12f), new Color(0.22f, 0.24f, 0.26f));
                return;
            }

            CreatePrimitive(root, PrimitiveType.Cube, new Vector3(0f, 0.25f, 0f), new Vector3(0.45f, 0.45f, 0.45f), Vector3.zero, new Color(0.65f, 0.25f, 0.7f));
        }

        private static void CreatePrimitive(
            Transform root,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEulerAngles,
            Color color)
        {
            GameObject visual = GameObject.CreatePrimitive(primitiveType);
            visual.name = "Debug Visual";
            visual.layer = root.gameObject.layer;
            visual.transform.SetParent(root, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localScale = localScale;
            visual.transform.localEulerAngles = localEulerAngles;

            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
                Object.Destroy(visualCollider);

            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = color;
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
