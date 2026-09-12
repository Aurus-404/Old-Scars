using System;
using System.Linq;
using System.Reflection;
using OldScars.Core;
using OldScars.Core.Actors;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Interactions;
using OldScars.Core.Items;
using OldScars.Core.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OldScars.Editor
{
    [InitializeOnLoad]
    public static class Impl00163RigidWearablesDiagnostics
    {
        private const string PendingKey = "OldScars.IMPL00163.BatchPending";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        private static readonly WearableExpectation[] Expectations =
        {
            new WearableExpectation("core:canvas_belt_01", "core:canvas_belt_visual", "core:waist", "human_waist_socket", "core:canvas_belt_human_waist_pose", "Canvas_Belt"),
            new WearableExpectation("core:steel_helmet_01", "core:steel_helmet_visual", "core:head", "human_head_socket", "core:steel_helmet_human_head_pose", "Steel_Helmet"),
            new WearableExpectation("core:work_goggles_01", "core:work_goggles_visual", "core:eyes", "human_eyes_socket", "core:work_goggles_human_eyes_pose", "Work_Goggles")
        };

        static Impl00163RigidWearablesDiagnostics() => EditorApplication.update += RunWhenReady;

        public static void RunBatch()
        {
            if (!Application.isBatchMode || EditorApplication.isCompiling)
                throw new InvalidOperationException("IMPL-0016.3 diagnostics require stable Unity batchmode.");
            SessionState.SetBool(PendingKey, true);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void RunWhenReady()
        {
            if (!SessionState.GetBool(PendingKey, false) || !EditorApplication.isPlaying ||
                GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
                return;

            SessionState.EraseBool(PendingKey);
            try
            {
                Run();
                Debug.Log("IMPL-0016.3 Rigid Wearables Coverage Diagnostics: PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Run()
        {
            GameDatabase database = GameDataManager.Instance.Database;
            Require(database != null, "GameData did not publish a database.");
            ValidateLoadedData(database);
            ValidatePrefabContracts();
            ValidateRuntimeEquipment(database);

            bool tunerPresent = TypeCache.GetTypesDerivedFrom<EditorWindow>()
                .Any(type => type.Name == "RigidEquipmentPoseTunerWindow");
            if (tunerPresent)
                Debug.Log("[IMPL-0016.3] RigidEquipmentPoseTunerWindow is present; all three profiles satisfy its generic discovery contract.");
            else
                Debug.LogWarning("[IMPL-0016.3] RigidEquipmentPoseTunerWindow is not present in origin/dev. Generic profile/prefab/pose discoverability passed; direct tuner UI discovery remains an integration gate with IMPL-0016.1A.");
        }

        private static void ValidateLoadedData(GameDatabase database)
        {
            VisualRigProfileDefinition rig = database.GetVisualRigProfile("core:human_standard_visual_rig");
            Require(rig != null, "Human visual rig profile is missing.");
            foreach (WearableExpectation expected in Expectations)
            {
                ItemDefinition item = database.GetItem(expected.ItemId);
                ItemVisualProfileDefinition profile = database.GetItemVisualProfile(expected.ProfileId);
                AttachmentPoseDefinition pose = database.GetAttachmentPose(expected.PoseId);
                Require(item?.equip?.slot_sets != null && item.equip.slot_sets.Any(set => set != null && set.Contains(expected.SlotId)),
                    expected.ItemId + " does not reuse the expected existing Equipment slot.");
                Require(profile != null && profile.item_definition_id == expected.ItemId && profile.enabled.GetValueOrDefault(true),
                    expected.ItemId + " has no enabled item visual profile.");
                Require(database.GetVisualAssetByKey(profile.world_asset_key) != null &&
                        database.GetVisualAssetByKey(profile.equipped_asset_key) != null,
                    expected.ItemId + " does not resolve both visual assets.");
                Require(pose != null && pose.visual_profile_id == expected.ProfileId && pose.rig_profile_id == rig.id,
                    expected.ItemId + " has no persistent humanoid attachment pose.");
                Require(rig.sockets.Any(socket => socket != null && socket.id == expected.SocketId),
                    expected.ItemId + " socket is absent from the rig profile.");
                Require(rig.equipment_slot_mappings.Any(mapping => mapping != null && mapping.equipment_slot_id == expected.SlotId),
                    expected.ItemId + " slot has no rig socket mapping.");
            }
        }

        private static void ValidatePrefabContracts()
        {
            GameObject humanoid = Resources.Load<GameObject>("OldScarsActorRepresentations/humanoid_standard");
            EntityVisualRigRuntime rig = humanoid != null ? humanoid.GetComponent<EntityVisualRigRuntime>() : null;
            Require(rig != null, "humanoid_standard has no EntityVisualRigRuntime.");
            foreach (WearableExpectation expected in Expectations)
            {
                Require(rig.SocketBindings.Any(binding => binding.SocketId == expected.SocketId && binding.Target != null),
                    "humanoid_standard is missing Transform binding " + expected.SocketId + ".");
                GameObject equipped = Resources.Load<GameObject>("OldScarsVisuals/PFB_VIS_" + expected.PrefabToken + "_Equipped");
                Require(EquippedVisualPrefabContract.TryValidate(equipped, out string error),
                    expected.ItemId + " equipped prefab contract failed: " + error);
                Require(equipped.GetComponentsInChildren<Renderer>(true).Length > 0 &&
                        equipped.GetComponentInChildren<SkinnedMeshRenderer>(true) == null,
                    expected.ItemId + " is not a rigid rendered attachment.");
                GameObject world = Resources.Load<GameObject>("PFB_WorldItem_" + expected.ItemId.Substring("core:".Length));
                Require(world != null && world.GetComponent<WorldItemPickup>() != null && world.GetComponent<BoxCollider>() != null &&
                        world.GetComponent<Rigidbody>() != null && world.GetComponent<WorldObjectTags>() != null &&
                        world.GetComponent<WorldObjectDebugInfo>() != null && world.GetComponentInChildren<Renderer>(true) != null,
                    expected.ItemId + " world prefab lost its gameplay/physics/visual contract.");
                WorldItemPickup pickup = world.GetComponent<WorldItemPickup>();
                string authoredId = new SerializedObject(pickup).FindProperty("itemDefinitionId").stringValue;
                Require(authoredId == expected.ItemId.Substring("core:".Length) && world.transform.Find("Visual") != null,
                    expected.ItemId + " world prefab lost its authored identity or Visual child.");
                string[] tags = world.GetComponent<WorldObjectTags>().InitialTags;
                Require(tags.Contains("world_item") && tags.Contains("pickupable") && tags.Contains("inspectable"),
                    expected.ItemId + " world prefab lost required interaction tags.");
            }
        }

        private static void ValidateRuntimeEquipment(GameDatabase database)
        {
            GameObject actor = null;
            GameObject representation = null;
            try
            {
                actor = new GameObject("IMPL-0016.3 Equipment Fixture");
                InventoryComponent inventory = actor.AddComponent<InventoryComponent>();
                actor.AddComponent<ActorItemOwnershipComponent>();
                ActorEquipmentComponent equipment = actor.AddComponent<ActorEquipmentComponent>();
                GameObject prefab = Resources.Load<GameObject>("OldScarsActorRepresentations/humanoid_standard");
                representation = UnityEngine.Object.Instantiate(prefab, actor.transform, false);
                EntityVisualRigRuntime rig = representation.GetComponent<EntityVisualRigRuntime>();
                EntityEquipmentVisualSynchronizer synchronizer = representation.GetComponent<EntityEquipmentVisualSynchronizer>();
                Require(rig != null && rig.EnsureReady() && synchronizer != null,
                    "Runtime humanoid visual composition is not ready.");
                synchronizer.Configure(equipment, rig);

                foreach (WearableExpectation expected in Expectations)
                {
                    ItemVisualProfileDefinition profile = database.GetItemVisualProfile(expected.ProfileId);
                    Require(rig.TryResolveForEquipmentSlot(expected.SlotId, profile.required_socket_capabilities, out VisualSocketResolution socket) &&
                            socket.SocketId == expected.SocketId,
                        expected.ItemId + " did not resolve the deterministic expected socket.");
                    AttachmentPoseValue pose = AttachmentPoseResolver.Resolve(database, profile, rig.VisualRigProfileId,
                        rig.RigFamilyId, socket.SocketId, socket.Role);
                    AttachmentPoseDefinition authored = database.GetAttachmentPose(expected.PoseId);
                    Require(SameVector(pose.LocalPosition, authored.local_position) && SameVector(pose.LocalScale, authored.local_scale),
                        expected.ItemId + " did not resolve its authored AttachmentPose.");

                    ItemInstance item = inventory.AddItemByDefinitionId(expected.ItemId, 1);
                    Require(item != null, "Could not create " + expected.ItemId + ".");
                    EquipmentPreview preview = equipment.PreviewEquip(inventory, item.InstanceId, new[] { expected.SlotId });
                    Require(preview.Success && equipment.Equip(inventory, preview).Success,
                        "Could not equip " + expected.ItemId + " through ActorEquipmentComponent.");
                    Require(representation.GetComponentsInChildren<EquippedVisualInstanceMarker>(true)
                            .Count(marker => marker.InstanceId == item.InstanceId) == 1,
                        expected.ItemId + " did not create exactly one visual for its InstanceId.");
                }

                Require(synchronizer.ActiveVisualCount == 3, "Belt, helmet and goggles did not coexist as three independent visuals.");
                EquippedVisualInstanceMarker[] markers = representation.GetComponentsInChildren<EquippedVisualInstanceMarker>(true);
                Require(markers.Select(marker => marker.InstanceId).Distinct(StringComparer.Ordinal).Count() == 3,
                    "Runtime visuals contain a duplicate InstanceId.");
                Require(markers.Single(marker => marker.DefinitionId == "core:steel_helmet_01").SocketId == "human_head_socket" &&
                        markers.Single(marker => marker.DefinitionId == "core:work_goggles_01").SocketId == "human_eyes_socket",
                    "Helmet and goggles did not remain independent on head/eyes sockets.");

                ItemInstance backpack = inventory.AddItemByDefinitionId("core:small_backpack_01", 1);
                EquipmentPreview backpackPreview = equipment.PreviewEquip(
                    inventory, backpack.InstanceId, new[] { ActorEquipmentComponent.BackSlotId });
                Require(backpackPreview.Success && equipment.Equip(inventory, backpackPreview).Success &&
                        synchronizer.ActiveVisualCount == 4 &&
                        representation.GetComponentsInChildren<EquippedVisualInstanceMarker>(true)
                            .Count(marker => marker.InstanceId == backpack.InstanceId) == 1,
                    "Belt did not coexist with the existing backpack visual.");

                EquippedVisualInstanceMarker originalGoggles = representation
                    .GetComponentsInChildren<EquippedVisualInstanceMarker>(true)
                    .Single(marker => marker.DefinitionId == "core:work_goggles_01");
                ItemInstance replacementGoggles = inventory.AddItemByDefinitionId("core:work_goggles_01", 1);
                EquipmentReplacementPlan replacement = equipment.PreviewEquipReplacing(
                    inventory, replacementGoggles.InstanceId, new[] { "core:eyes" });
                Require(replacement.Success && equipment.EquipReplacing(inventory, replacement).Success &&
                        synchronizer.ActiveVisualCount == 4 &&
                        representation.GetComponentsInChildren<EquippedVisualInstanceMarker>(true)
                            .Count(marker => marker.InstanceId == replacementGoggles.InstanceId) == 1 &&
                        !equipment.IsEquipped(originalGoggles.InstanceId),
                    "Goggles replacement did not preserve exactly one visual without duplicates.");

                markers = representation.GetComponentsInChildren<EquippedVisualInstanceMarker>(true);
                RendererSnapshot[] equipmentMaterials = markers.SelectMany(marker => marker.GetComponentsInChildren<Renderer>(true))
                    .Distinct().Select(renderer => new RendererSnapshot(renderer)).ToArray();
                InvokeTint(actor, SandboxCombatAffiliation.Blue);
                Require(equipmentMaterials.All(snapshot => snapshot.IsUnchanged()), "Blue tint modified wearable materials.");
                InvokeTint(actor, SandboxCombatAffiliation.Red);
                Require(equipmentMaterials.All(snapshot => snapshot.IsUnchanged()), "Red tint modified wearable materials.");

                EquippedVisualInstanceMarker goggles = markers.Single(marker => marker.InstanceId == replacementGoggles.InstanceId);
                EquipmentPreview unequip = equipment.PreviewUnequip(goggles.InstanceId);
                Require(unequip.Success && equipment.Unequip(unequip).Success && synchronizer.ActiveVisualCount == 3,
                    "Goggles unequip did not remove exactly one reflected visual.");
                synchronizer.RebuildFromCurrentState();
                Require(synchronizer.ActiveVisualCount == 3, "Visual rebuild duplicated rigid wearables.");
            }
            finally
            {
                if (actor != null)
                    UnityEngine.Object.DestroyImmediate(actor);
            }
        }

        private static void InvokeTint(GameObject actor, SandboxCombatAffiliation affiliation)
        {
            MethodInfo method = typeof(SandboxNpcController).GetMethod("ApplyDebugColor", BindingFlags.Static | BindingFlags.NonPublic);
            Require(method != null, "SandboxNpcController.ApplyDebugColor is unavailable.");
            method.Invoke(null, new object[] { actor, affiliation });
        }

        private static bool SameVector(Vector3 actual, Float3Definition expected)
        {
            return expected != null && Vector3.Distance(actual, new Vector3(expected.x, expected.y, expected.z)) < 0.0001f;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private sealed class WearableExpectation
        {
            public WearableExpectation(string itemId, string profileId, string slotId, string socketId, string poseId, string prefabToken)
            {
                ItemId = itemId; ProfileId = profileId; SlotId = slotId; SocketId = socketId; PoseId = poseId; PrefabToken = prefabToken;
            }
            public string ItemId { get; }
            public string ProfileId { get; }
            public string SlotId { get; }
            public string SocketId { get; }
            public string PoseId { get; }
            public string PrefabToken { get; }
        }

        private sealed class RendererSnapshot
        {
            private readonly Renderer renderer;
            private readonly Material[] materials;
            private readonly Color[] colors;

            public RendererSnapshot(Renderer renderer)
            {
                this.renderer = renderer;
                materials = renderer.sharedMaterials;
                colors = materials.Select(material => material != null ? material.color : default).ToArray();
            }

            public bool IsUnchanged()
            {
                Material[] current = renderer.sharedMaterials;
                return current.Length == materials.Length && current.Select((material, index) =>
                    material == materials[index] && (material == null || material.color == colors[index])).All(value => value);
            }
        }
    }
}
