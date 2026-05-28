using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Interactions;
using UnityEngine;

namespace OldScars.Core.Items
{
    /// <summary>
    /// Minimal world item pickup bridge for Milestone 14.
    ///
    /// This is not a final pickup/drop, loot, container, ownership, or save
    /// system. It only lets a configured world object create one runtime
    /// ItemInstance in the actor inventory when the debug action completes.
    /// </summary>
    public sealed class WorldItemPickup : MonoBehaviour
    {
        private const string PickupableTag = "pickupable";
        private const string PickedUpTag = "picked_up";

        [SerializeField] private string itemDefinitionId;

        public string ItemDefinitionId => itemDefinitionId;

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

            ItemDefinition definition = GetItemDefinition(itemDefinitionId);
            if (definition == null)
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

            ItemInstance item = inventory.AddItemByDefinitionId(definition.id);
            if (item == null)
                return DebugActionExecutionResult.Info("Recoger", $"No se pudo recoger '{SafeText(itemDefinitionId)}'.");

            targetTags.AddTag(PickedUpTag);
            targetTags.RemoveTag(PickupableTag);
            DisableVisiblePickupParts();

            string displayName = GetItemDisplayName(item.DefinitionId);
            Debug.Log($"[WorldItemPickup] Picked up {item.DefinitionId} [{item.InstanceId}].");
            return DebugActionExecutionResult.Info("Recoger", $"Recogiste {displayName}.");
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
