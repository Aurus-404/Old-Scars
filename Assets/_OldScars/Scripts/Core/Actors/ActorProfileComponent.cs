using System.Collections;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Interactions;
using OldScars.Core.Items;
using OldScars.Core.Visuals;
using UnityEngine;

namespace OldScars.Core.Actors
{
    public sealed class ActorProfileComponent : MonoBehaviour
    {
        [SerializeField] private string actorProfileId;

        private bool profileApplied;
        private bool loggedWaitingForData;

        private IEnumerator Start()
        {
            if (string.IsNullOrWhiteSpace(actorProfileId))
            {
                Debug.LogError($"[ActorProfileComponent] '{name}' has no actorProfileId configured.");
                yield break;
            }

            while (!IsGameDataReady())
            {
                LogWaitingForDataOnce();
                yield return null;
            }

            ApplyProfile(GameDataManager.Instance.Database);
        }

        private static bool IsGameDataReady()
        {
            return GameDataManager.Instance != null &&
                   GameDataManager.Instance.IsReady &&
                   GameDataManager.Instance.Database != null;
        }

        private void ApplyProfile(GameDatabase database)
        {
            if (profileApplied)
                return;

            if (database == null)
            {
                Debug.LogError($"[ActorProfileComponent] '{name}' cannot apply actor profile '{actorProfileId}' because GameDatabase is null.");
                return;
            }

            ActorProfileDefinition profile = database.GetActorProfile(actorProfileId);
            if (profile == null)
            {
                Debug.LogError($"[ActorProfileComponent] '{name}' actor profile '{actorProfileId}' was not found.");
                return;
            }

            actorProfileId = profile.id;
            profileApplied = true;

            WarnIfDebugSeederExists();
            ApplyDisplayName(profile);
            ApplyInitialTags(profile);
            ApplyHealth(profile);
            ApplyEquipmentLayout(profile);
            ApplyInitialInventory(profile);
            ApplyInitialEquipment(profile);
            ApplyVisualRigProfile(profile);

            Debug.Log($"[ActorProfileComponent] '{name}' applied actor profile '{actorProfileId}'.");
        }

        private void ApplyDisplayName(ActorProfileDefinition profile)
        {
            if (string.IsNullOrWhiteSpace(profile.display_name))
                return;

            WorldObjectDebugInfo debugInfo = GetComponent<WorldObjectDebugInfo>();
            if (debugInfo == null)
            {
                Debug.LogWarning($"[ActorProfileComponent] '{name}' cannot apply display_name from actor profile '{actorProfileId}' because WorldObjectDebugInfo is missing.");
                return;
            }

            debugInfo.SetRuntimeDisplayName(profile.display_name);
        }

        private void ApplyInitialTags(ActorProfileDefinition profile)
        {
            if (profile.initial_tags == null || profile.initial_tags.Length == 0)
                return;

            WorldObjectTags worldObjectTags = GetComponent<WorldObjectTags>();
            if (worldObjectTags == null)
            {
                Debug.LogWarning($"[ActorProfileComponent] '{name}' cannot apply initial_tags from actor profile '{actorProfileId}' because WorldObjectTags is missing.");
                return;
            }

            worldObjectTags.ApplyInitialTags(profile.initial_tags);
        }

        private void ApplyHealth(ActorProfileDefinition profile)
        {
            if (profile.health == null)
                return;

            ActorHealthComponent health = GetComponent<ActorHealthComponent>();
            if (health == null)
            {
                Debug.LogWarning($"[ActorProfileComponent] '{name}' cannot apply health from actor profile '{actorProfileId}' because ActorHealthComponent is missing.");
                return;
            }

            health.ApplyInitialHealth(profile.health.max_health, profile.health.current_health);
        }

        private void ApplyInitialInventory(ActorProfileDefinition profile)
        {
            InventoryComponent inventory = GetComponent<InventoryComponent>();
            if (inventory == null)
            {
                if (profile.initial_inventory != null && profile.initial_inventory.Length > 0)
                {
                    Debug.LogWarning(
                        $"[ActorProfileComponent] '{name}' cannot apply initial_inventory from actor profile " +
                        $"'{actorProfileId}' because InventoryComponent is missing.");
                }
                return;
            }

            inventory.BeginInitialContentLoad();
            try
            {
                if (profile.initial_inventory == null)
                    return;

                for (int index = 0; index < profile.initial_inventory.Length; index++)
                {
                    ActorProfileInventoryEntry entry = profile.initial_inventory[index];
                    if (entry == null)
                        continue;

                    ItemInstance item = inventory.AddItemByDefinitionId(entry.item_id, entry.quantity);
                    if (item == null)
                    {
                        Debug.LogWarning(
                            $"[ActorProfileComponent] '{name}' failed to add '{entry.item_id}' x{entry.quantity} " +
                            $"from actor profile '{actorProfileId}'.");
                    }
                }
            }
            finally
            {
                inventory.CompleteInitialContentLoad();
            }
        }

        private void ApplyEquipmentLayout(ActorProfileDefinition profile)
        {
            if (string.IsNullOrWhiteSpace(profile.equipment_layout_id))
                return;

            ActorEquipmentComponent equipment = GetComponent<ActorEquipmentComponent>();
            if (equipment == null)
                return;

            if (!equipment.TrySetLayout(profile.equipment_layout_id, out string reason))
            {
                Debug.LogWarning(
                    $"[ActorProfileComponent] '{name}' could not apply equipment layout " +
                    $"'{profile.equipment_layout_id}': {reason}");
            }
        }

        private void ApplyInitialEquipment(ActorProfileDefinition profile)
        {
            if (profile.initial_equipment == null || profile.initial_equipment.Length == 0)
                return;

            ActorEquipmentComponent equipment = GetComponent<ActorEquipmentComponent>();
            if (equipment == null)
            {
                Debug.LogError(
                    $"[ActorProfileComponent] '{name}' cannot apply initial_equipment from actor profile " +
                    $"'{actorProfileId}' because ActorEquipmentComponent is missing.");
                return;
            }

            if (!EquipmentTransactionService.TryEquipInitialItems(equipment, profile.initial_equipment, out string error))
            {
                Debug.LogError(
                    $"[ActorProfileComponent] '{name}' failed to apply initial_equipment atomically from actor profile " +
                    $"'{actorProfileId}': {error}");
            }
        }

        private void ApplyVisualRigProfile(ActorProfileDefinition profile)
        {
            if (string.IsNullOrWhiteSpace(profile.visual_rig_profile_id))
                return;

            EntityVisualRigRuntime visualRig = GetComponent<EntityVisualRigRuntime>();
            if (visualRig == null)
                return;

            if (!visualRig.TrySetProfile(profile.visual_rig_profile_id, out string reason))
            {
                Debug.LogWarning(
                    $"[ActorProfileComponent] '{name}' could not apply visual rig profile " +
                    $"'{profile.visual_rig_profile_id}': {reason}");
            }
        }

        private void WarnIfDebugSeederExists()
        {
            if (GetComponent<DebugActorInventorySeeder>() == null)
                return;

            Debug.LogWarning($"[ActorProfileComponent] '{name}' also has DebugActorInventorySeeder. Keep only one inventory seeding path active when testing actor profile inventory.");
        }

        private void LogWaitingForDataOnce()
        {
            if (loggedWaitingForData)
                return;

            loggedWaitingForData = true;
            Debug.Log($"[ActorProfileComponent] '{name}' waiting for CoreDataSystem before applying actor profile '{actorProfileId}'.");
        }
    }
}
