using System.Collections;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Identity;
using OldScars.Core.Interactions;
using OldScars.Core.Items;
using OldScars.Core.Visuals;
using UnityEngine;
using UnityEngine.AI;

namespace OldScars.Core.Actors
{
    public sealed class ActorProfileComponent : MonoBehaviour
    {
        [SerializeField] private string actorProfileId;

        private bool profileApplied;
        private bool loggedWaitingForData;

        public string ActorProfileId => actorProfileId;
        public bool ProfileApplied => profileApplied;

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

        public bool TryApplyRuntimeBootstrap(string requestedProfileId, out string error)
        {
            error = null;
            if (!TryResolveProfile(requestedProfileId, out GameDatabase database, out ActorProfileDefinition profile, out error))
                return false;
            actorProfileId = profile.id;
            ApplyProfile(database);
            if (!profileApplied)
            {
                error = $"Actor profile '{profile.id}' did not complete bootstrap.";
                return false;
            }
            return true;
        }

        public bool TryPreparePersistenceRestore(string requestedProfileId, out string error)
        {
            error = null;
            if (!TryResolveProfile(requestedProfileId, out GameDatabase database, out ActorProfileDefinition profile, out error))
                return false;
            if (profileApplied && actorProfileId != profile.id)
            {
                error = $"Actor profile is already '{actorProfileId}' and cannot restore as '{profile.id}'.";
                return false;
            }

            actorProfileId = profile.id;
            profileApplied = true;
            EnsureAuthoredIdentity(profile.id);
            WarnIfDebugSeederExists();
            ApplyDisplayName(profile);
            ApplyInitialTags(profile);
            ApplyVitalIntegrity(profile);
            ApplyVisualRigProfile(profile);
            ApplyRuntimeCapabilities(profile);
            GetComponent<InventoryComponent>()?.PreparePersistenceRestore();
            return true;
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

            EnsureAuthoredIdentity(profile.id);

            WarnIfDebugSeederExists();
            ApplyDisplayName(profile);
            ApplyInitialTags(profile);
            ApplyHealth(profile);
            ApplyEquipmentLayout(profile);
            ApplyInitialInventory(profile);
            ApplyInitialEquipment(profile);
            ApplyVisualRigProfile(profile);
            ApplyRuntimeCapabilities(profile);

            Debug.Log($"[ActorProfileComponent] '{name}' applied actor profile '{actorProfileId}'.");
        }

        private bool TryResolveProfile(
            string requestedProfileId,
            out GameDatabase database,
            out ActorProfileDefinition profile,
            out string error)
        {
            database = GameDataManager.Instance?.Database;
            profile = null;
            error = null;
            if (database == null || GameDataManager.Instance?.IsReady != true)
            {
                error = "GameDatabase is not ready.";
                return false;
            }
            profile = database.GetActorProfile(requestedProfileId);
            if (profile == null)
            {
                error = $"Actor profile '{requestedProfileId ?? "<EMPTY>"}' was not found.";
                return false;
            }
            return true;
        }

        private void EnsureAuthoredIdentity(string canonicalProfileId)
        {
            PersistentSceneObjectId sceneIdentity = GetComponent<PersistentSceneObjectId>();
            if (sceneIdentity == null || !sceneIdentity.enabled)
                return;
            if (!ActorRuntimeIdentity.TryEnsureAuthored(gameObject, canonicalProfileId, out _, out string error))
            {
                Debug.LogError(
                    "[Actors][AUTHORED_IDENTITY_FAILURE]" +
                    $"\n  Actor: {name}" +
                    $"\n  PersistentSceneObjectId: {sceneIdentity.PersistentId ?? "<EMPTY>"}" +
                    $"\n  ActorProfileId: {canonicalProfileId ?? "<EMPTY>"}" +
                    $"\n  Failure: {error ?? "<UNKNOWN>"}");
            }
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
            ConfigureVitalIntegrity(health, profile.health);
        }

        private void ApplyVitalIntegrity(ActorProfileDefinition profile)
        {
            if (profile.health == null)
                return;

            ActorHealthComponent health = GetComponent<ActorHealthComponent>();
            if (health == null)
                return;

            ConfigureVitalIntegrity(health, profile.health);
        }

        private void ConfigureVitalIntegrity(ActorHealthComponent health, ActorProfileHealth profileHealth)
        {
            if (!health.TryConfigureVitalIntegrity(profileHealth.vital_integrity, out string vitalFailure))
            {
                Debug.LogError(
                    $"[ActorProfileComponent] '{name}' cannot apply vital_integrity from actor profile " +
                    $"'{actorProfileId}': {vitalFailure}");
            }
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

            EntityVisualRigRuntime visualRig = GetComponentInChildren<EntityVisualRigRuntime>(true);
            if (visualRig == null)
                return;

            if (!visualRig.TrySetProfile(profile.visual_rig_profile_id, out string reason))
            {
                Debug.LogWarning(
                    $"[ActorProfileComponent] '{name}' could not apply visual rig profile " +
                    $"'{profile.visual_rig_profile_id}': {reason}");
            }
        }

        private void ApplyRuntimeCapabilities(ActorProfileDefinition profile)
        {
            ApplyConsciousness(profile);
            ApplyNavigation(profile);
            ApplyVisualPerception(profile);
            ApplyEncounterAI(profile);
        }

        private void ApplyConsciousness(ActorProfileDefinition profile)
        {
            ActorConditionComponent condition = GetComponent<ActorConditionComponent>();
            if (condition == null)
            {
                Debug.LogError(
                    "[Actors][CONSCIOUSNESS_PROFILE_REJECTED]" +
                    $"\n  Actor: {name}" +
                    $"\n  ActorProfileId: {profile.id ?? "<EMPTY>"}" +
                    "\n  Failure: ActorConditionComponent is missing" +
                    "\n  ActionTaken: functional condition remains unavailable");
                return;
            }
            if (!condition.TryConfigure(profile.consciousness, out string error))
            {
                Debug.LogError(
                    "[Actors][CONSCIOUSNESS_PROFILE_REJECTED]" +
                    $"\n  Actor: {name}" +
                    $"\n  ActorProfileId: {profile.id ?? "<EMPTY>"}" +
                    $"\n  Failure: {error ?? "<UNKNOWN>"}" +
                    "\n  ActionTaken: default runtime condition tuning remains active");
            }
        }

        private void ApplyNavigation(ActorProfileDefinition profile)
        {
            if (profile.navigation == null)
                return;
            if (GetComponent<PlayerMovementController>() != null || GetComponent<PlayerMovementInputController>() != null)
            {
                Debug.LogError(
                    "[Actors][NAVIGATION_PROFILE_REJECTED]" +
                    $"\n  Actor: {name}" +
                    $"\n  ActorProfileId: {profile.id ?? "<EMPTY>"}" +
                    "\n  Failure: NPC navigation cannot share the player movement authority." +
                    "\n  ActionTaken: no NavMeshAgent or ActorNavigationController was added");
                return;
            }

            NavMeshAgent agent = GetComponent<NavMeshAgent>();
            if (agent == null)
                agent = gameObject.AddComponent<NavMeshAgent>();
            Collider bodyCollider = GetComponent<Collider>();
            if (bodyCollider != null)
            {
                // Runtime actors are positioned immediately before their profile is applied.
                // Ensure collider bounds reflect that pose before deriving NavMeshAgent geometry.
                Physics.SyncTransforms();
                if (bodyCollider is CapsuleCollider capsule && capsule.direction == 1)
                {
                    Vector3 scale = transform.lossyScale;
                    float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                    float height = Mathf.Max(capsule.height * Mathf.Abs(scale.y), radius * 2f);
                    float centerY = capsule.center.y * Mathf.Abs(scale.y);
                    agent.baseOffset = Mathf.Max(0f, height * 0.5f - centerY);
                    agent.height = Mathf.Max(0.1f, height);
                    agent.radius = Mathf.Max(0.05f, radius);
                }
                else
                {
                    Bounds bounds = bodyCollider.bounds;
                    agent.baseOffset = Mathf.Max(0f, transform.position.y - bounds.min.y);
                    agent.height = Mathf.Max(0.1f, bounds.size.y);
                    agent.radius = Mathf.Max(0.05f, Mathf.Min(bounds.extents.x, bounds.extents.z));
                }
            }
            ActorNavigationController navigation = GetComponent<ActorNavigationController>();
            if (navigation == null)
                navigation = gameObject.AddComponent<ActorNavigationController>();
            if (!navigation.TryConfigure(
                    profile.navigation.speed,
                    profile.navigation.acceleration,
                    profile.navigation.angular_speed,
                    profile.navigation.stopping_distance,
                    out string error))
            {
                Debug.LogError(
                    "[Actors][NAVIGATION_PROFILE_REJECTED]" +
                    $"\n  Actor: {name}" +
                    $"\n  ActorProfileId: {profile.id ?? "<EMPTY>"}" +
                    $"\n  Failure: {error ?? "<UNKNOWN>"}" +
                    "\n  ActionTaken: navigation capability remains unavailable");
            }

            if (GetComponent<Rigidbody>() == null)
                gameObject.AddComponent<Rigidbody>();
            if (GetComponent<ActorPhysicalCollapseController>() == null)
                gameObject.AddComponent<ActorPhysicalCollapseController>();
        }

        private void ApplyVisualPerception(ActorProfileDefinition profile)
        {
            if (profile.visual_perception == null)
                return;
            ActorVisualPerceptionService perception = GetComponent<ActorVisualPerceptionService>();
            if (perception == null)
                perception = gameObject.AddComponent<ActorVisualPerceptionService>();
            if (!perception.TryConfigure(
                    profile.visual_perception.visual_range,
                    profile.visual_perception.horizontal_fov_degrees,
                    profile.visual_perception.eye_height,
                    profile.visual_perception.recognition_near_seconds,
                    profile.visual_perception.recognition_far_seconds,
                    profile.visual_perception.recognition_decay_seconds,
                    out string error))
            {
                Debug.LogError(
                    "[Actors][PERCEPTION_PROFILE_REJECTED]" +
                    $"\n  Actor: {name}" +
                    $"\n  ActorProfileId: {profile.id ?? "<EMPTY>"}" +
                    $"\n  Failure: {error ?? "<UNKNOWN>"}" +
                    "\n  ActionTaken: visual perception capability remains unavailable");
            }
        }

        private void ApplyEncounterAI(ActorProfileDefinition profile)
        {
            if (profile.encounter_ai == null)
                return;
            if (GetComponent<PlayerMovementController>() != null || GetComponent<PlayerMovementInputController>() != null)
            {
                Debug.LogError(
                    "[Actors][ENCOUNTER_AI_PROFILE_REJECTED]" +
                    $"\n  Actor: {name}" +
                    $"\n  ActorProfileId: {profile.id ?? "<EMPTY>"}" +
                    "\n  Failure: Human Encounter AI cannot share the player movement authority." +
                    "\n  ActionTaken: no HumanEncounterAIController was added");
                return;
            }

            HumanEncounterAIController controller = GetComponent<HumanEncounterAIController>();
            if (controller == null)
                controller = gameObject.AddComponent<HumanEncounterAIController>();
            if (!controller.TryConfigure(profile.encounter_ai, out string error))
            {
                Debug.LogError(
                    "[Actors][ENCOUNTER_AI_PROFILE_REJECTED]" +
                    $"\n  Actor: {name}" +
                    $"\n  ActorProfileId: {profile.id ?? "<EMPTY>"}" +
                    $"\n  Failure: {error ?? "<UNKNOWN>"}" +
                    "\n  ActionTaken: encounter AI capability remains unavailable");
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
