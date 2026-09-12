using System;
using System.Linq;
using System.Reflection;
using OldScars.Core;
using OldScars.Core.Actors;
using OldScars.Core.Data;
using OldScars.Core.Interactions;
using OldScars.Core.Items;
using OldScars.Core.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OldScars.Editor
{
    [InitializeOnLoad]
    public static class EquipmentVisualRuntimeWiringDiagnostics
    {
        private const string BatchPendingKey = "OldScars.IMPL0016.BatchPending";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string CrowbarProfileId = "core:debug_npc_capsule_01";
        private const string RifleProfileId = "core:debug_npc_capsule_rifle_test_01";
        private const string CrowbarItemId = "core:rusted_crowbar_01";
        private const string RifleItemId = "core:lee_enfield_rifle_01";
        private const string CrowbarMaterialName = "MAT_Crowbar_V1";
        private const string RifleMaterialName = "MAT_Retro_Bolt_Action_Rifle";
        private static readonly Color BlueTint = new Color(0.16f, 0.42f, 1f);
        private static readonly Color RedTint = new Color(0.9f, 0.12f, 0.08f);

        static EquipmentVisualRuntimeWiringDiagnostics() => EditorApplication.update += RunBatchWhenReady;

        public static void Run()
        {
            if (!EditorApplication.isPlaying || GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
                throw new InvalidOperationException("IMPL-0016 diagnostics require Play Mode with Core data ready.");

            ActorRuntimeIdentity crowbarActor = null;
            ActorRuntimeIdentity rifleActor = null;
            bool missingSourceWarningObserved = false;
            void CaptureMissingSourceWarning(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Warning &&
                    condition.Contains("[EntityEquipmentVisualSynchronizer]") &&
                    condition.Contains("has no IEquipmentVisualSource"))
                {
                    missingSourceWarningObserved = true;
                }
            }

            Application.logMessageReceived += CaptureMissingSourceWarning;
            try
            {
                Require(GameDataManager.Instance.Report != null && GameDataManager.Instance.Report.ErrorCount == 0,
                    "Game data validation contains errors.");
                AssertExternalPlaceholderAssets();
                AssertAuthoredPlayerUsesSameSeam();
                Require(ActorSpawnService.TrySpawn(
                        CrowbarProfileId, new Vector3(20f, 1f, 20f), Quaternion.identity,
                        out crowbarActor, out string crowbarFailure),
                    "Crowbar actor spawn failed: " + crowbarFailure);
                AssertVisualState(crowbarActor, "core:small_backpack_01", "core:rusted_crowbar_01");
                AssertAffiliationTintIsolation(crowbarActor.gameObject, SandboxCombatAffiliation.Blue, BlueTint);

                ActorEquipmentComponent crowbarEquipment = crowbarActor.GetComponent<ActorEquipmentComponent>();
                InventoryComponent crowbarInventory = crowbarActor.GetComponent<InventoryComponent>();
                EntityEquipmentVisualSynchronizer crowbarVisuals =
                    crowbarActor.GetComponentInChildren<EntityEquipmentVisualSynchronizer>(true);
                int equipmentVersionBeforeRebuild = crowbarEquipment.Version;
                int storageVersionBeforeRebuild = crowbarEquipment.StorageVersion;
                string[] equippedInstancesBeforeRebuild = crowbarEquipment.Entries
                    .Select(entry => entry.Item.InstanceId)
                    .OrderBy(instanceId => instanceId, StringComparer.Ordinal)
                    .ToArray();
                crowbarVisuals.RebuildFromCurrentState();
                Require(crowbarEquipment.Version == equipmentVersionBeforeRebuild &&
                        crowbarEquipment.StorageVersion == storageVersionBeforeRebuild &&
                        crowbarEquipment.Entries.Select(entry => entry.Item.InstanceId)
                            .OrderBy(instanceId => instanceId, StringComparer.Ordinal)
                            .SequenceEqual(equippedInstancesBeforeRebuild),
                    "Visual rebuild modified authoritative ActorEquipmentComponent state.");
                ItemInstance crowbar = crowbarEquipment.Entries.Single(entry =>
                    entry.DefinitionId == "core:rusted_crowbar_01").Item;
                EquipmentPreview unequip = crowbarEquipment.PreviewUnequip(crowbar.InstanceId);
                Require(unequip.Success && crowbarEquipment.Unequip(unequip).Success,
                    "Crowbar could not be unequipped through ActorEquipmentComponent.");
                Require(crowbarVisuals.ActiveVisualCount == 1,
                    "Crowbar unequip did not remove exactly its reflected visual.");

                ItemInstance rifleReplacement = crowbarInventory.AddItemByDefinitionId("core:lee_enfield_rifle_01", 1);
                Require(rifleReplacement != null, "Replacement rifle fixture could not be created.");
                EquipmentPreview riflePreview = crowbarEquipment.PreviewEquip(
                    crowbarInventory, rifleReplacement.InstanceId,
                    new[] { ActorEquipmentComponent.HandLeftSlotId, ActorEquipmentComponent.HandRightSlotId });
                Require(riflePreview.Success && crowbarEquipment.Equip(crowbarInventory, riflePreview).Success,
                    "Replacement rifle could not be equipped through ActorEquipmentComponent.");
                Require(crowbarVisuals.ActiveVisualCount == 2 && CountVisuals(crowbarActor, rifleReplacement.InstanceId) == 1,
                    "Two-handed rifle replacement did not produce exactly one visual without duplicates.");

                Require(ActorSpawnService.TrySpawn(
                        RifleProfileId, new Vector3(24f, 1f, 20f), Quaternion.identity,
                        out rifleActor, out string rifleFailure),
                    "Rifle actor spawn failed: " + rifleFailure);
                AssertVisualState(rifleActor, "core:small_backpack_01", "core:lee_enfield_rifle_01");
                AssertAffiliationTintIsolation(rifleActor.gameObject, SandboxCombatAffiliation.Red, RedTint);
                AssertRendererOrderIndependenceAndLegacyFallback();
                Require(!missingSourceWarningObserved,
                    "A correctly composed humanoid emitted the missing IEquipmentVisualSource warning.");

                Debug.Log("IMPL-0016 Equipment Visual Runtime Wiring Diagnostics: PASS");
            }
            finally
            {
                Application.logMessageReceived -= CaptureMissingSourceWarning;
                DestroyActor(crowbarActor);
                DestroyActor(rifleActor);
            }
        }

        private static void AssertAffiliationTintIsolation(
            GameObject actor,
            SandboxCombatAffiliation affiliation,
            Color expectedBodyColor)
        {
            Renderer[] bodyRenderers = actor.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.GetComponentInParent<EquippedVisualInstanceMarker>() == null)
                .ToArray();
            RendererMaterialSnapshot[] equipmentMaterials = actor
                .GetComponentsInChildren<EquippedVisualInstanceMarker>(true)
                .SelectMany(marker => marker.GetComponentsInChildren<Renderer>(true))
                .Distinct()
                .Select(renderer => new RendererMaterialSnapshot(renderer))
                .ToArray();
            Require(bodyRenderers.Length > 0, "Affiliation tint fixture has no body renderer.");
            Require(equipmentMaterials.Length > 0, "Affiliation tint fixture has no equipment renderer.");

            InvokeDebugTint(actor, affiliation);

            Require(bodyRenderers.All(renderer => SameColor(renderer.material.color, expectedBodyColor)),
                affiliation + " affiliation tint was not applied to every body renderer.");
            Require(equipmentMaterials.All(snapshot => snapshot.IsUnchanged()),
                affiliation + " affiliation tint modified an Equipment visual material or color.");
        }

        private static void AssertRendererOrderIndependenceAndLegacyFallback()
        {
            GameObject orderedFixture = null;
            GameObject legacyCapsule = null;
            try
            {
                orderedFixture = new GameObject("IMPL-0016 Renderer Order Fixture");
                GameObject equipment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                equipment.name = "Equipment First";
                equipment.transform.SetParent(orderedFixture.transform, false);
                equipment.transform.SetAsFirstSibling();
                equipment.AddComponent<EquippedVisualInstanceMarker>();
                Renderer equipmentRenderer = equipment.GetComponent<Renderer>();
                Color equipmentColor = new Color(0.72f, 0.31f, 0.58f);
                equipmentRenderer.material.color = equipmentColor;

                GameObject bodyA = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                bodyA.name = "Body A";
                bodyA.transform.SetParent(orderedFixture.transform, false);
                GameObject bodyB = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bodyB.name = "Body B";
                bodyB.transform.SetParent(orderedFixture.transform, false);
                Require(orderedFixture.GetComponentInChildren<Renderer>(true) == equipmentRenderer,
                    "Renderer order fixture did not place Equipment before body renderers.");

                InvokeDebugTint(orderedFixture, SandboxCombatAffiliation.Blue);
                Require(SameColor(bodyA.GetComponent<Renderer>().material.color, BlueTint) &&
                        SameColor(bodyB.GetComponent<Renderer>().material.color, BlueTint),
                    "Body tint depended on renderer child order or skipped a legitimate body renderer.");
                Require(SameColor(equipmentRenderer.material.color, equipmentColor),
                    "First-child Equipment renderer received the affiliation tint.");

                legacyCapsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                InvokeDebugTint(legacyCapsule, SandboxCombatAffiliation.Red);
                Require(SameColor(legacyCapsule.GetComponent<Renderer>().material.color, RedTint),
                    "Legacy capsule fallback no longer receives affiliation tint.");
            }
            finally
            {
                if (orderedFixture != null)
                    UnityEngine.Object.DestroyImmediate(orderedFixture);
                if (legacyCapsule != null)
                    UnityEngine.Object.DestroyImmediate(legacyCapsule);
            }
        }

        private static void InvokeDebugTint(GameObject actor, SandboxCombatAffiliation affiliation)
        {
            MethodInfo method = typeof(SandboxNpcController).GetMethod(
                "ApplyDebugColor",
                BindingFlags.Static | BindingFlags.NonPublic);
            Require(method != null, "SandboxNpcController.ApplyDebugColor diagnostic seam is missing.");
            method.Invoke(null, new object[] { actor, affiliation });
        }

        private static bool SameColor(Color left, Color right)
        {
            return Mathf.Abs(left.r - right.r) < 0.001f &&
                   Mathf.Abs(left.g - right.g) < 0.001f &&
                   Mathf.Abs(left.b - right.b) < 0.001f &&
                   Mathf.Abs(left.a - right.a) < 0.001f;
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("IMPL-0016 batch diagnostics require Unity batchmode.");
            if (EditorApplication.isCompiling)
                throw new InvalidOperationException("IMPL-0016 batch diagnostics cannot start while compiling.");

            SessionState.SetBool(BatchPendingKey, true);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void RunBatchWhenReady()
        {
            if (!SessionState.GetBool(BatchPendingKey, false) || !EditorApplication.isPlaying ||
                GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
            {
                return;
            }

            SessionState.EraseBool(BatchPendingKey);
            try
            {
                Run();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void AssertAuthoredPlayerUsesSameSeam()
        {
            GameObject playerPrefab = Resources.Load<GameObject>("PFB_PlayerGameplayComposition");
            Require(playerPrefab != null, "Shared player gameplay composition prefab is missing.");
            ActorEquipmentComponent equipment = playerPrefab.GetComponentInChildren<ActorEquipmentComponent>(true);
            EntityEquipmentVisualSynchronizer synchronizer =
                playerPrefab.GetComponentInChildren<EntityEquipmentVisualSynchronizer>(true);
            EntityVisualRigRuntime rig = playerPrefab.GetComponentInChildren<EntityVisualRigRuntime>(true);
            Require(equipment != null && synchronizer != null && rig != null,
                "Shared player equipment/visual composition is incomplete.");

            var serializedSynchronizer = new SerializedObject(synchronizer);
            Require(serializedSynchronizer.FindProperty("equipmentVisualSourceBehaviour").objectReferenceValue == equipment &&
                    serializedSynchronizer.FindProperty("visualRig").objectReferenceValue == rig,
                "Player does not consume the shared ActorEquipmentComponent -> EntityEquipmentVisualSynchronizer seam.");
        }

        private static void AssertExternalPlaceholderAssets()
        {
            GameDatabase database = GameDataManager.Instance.Database;
            AssertResolvedVisualAsset(
                database,
                CrowbarItemId,
                "PFB_VIS_Rusted_Crowbar_V1_World",
                "PFB_VIS_Rusted_Crowbar_V1_Held",
                CrowbarMaterialName,
                "Axe");
            AssertResolvedVisualAsset(
                database,
                RifleItemId,
                "PFB_VIS_Lee_Enfield_RetroBolt_PS2_World",
                "PFB_VIS_Lee_Enfield_RetroBolt_PS2_Held",
                RifleMaterialName,
                "hunting_rifle_01");
            AssertWorldItemPrefab("PFB_WorldItem_rusted_crowbar_01", CrowbarItemId, CrowbarMaterialName);
            AssertWorldItemPrefab("PFB_WorldItem_lee_enfield_rifle_01", RifleItemId, RifleMaterialName);
        }

        private static void AssertResolvedVisualAsset(
            GameDatabase database,
            string itemDefinitionId,
            string expectedWorldPrefabName,
            string expectedEquippedPrefabName,
            string expectedMaterialName,
            string forbiddenPlaceholderTransform)
        {
            var profile = database.GetItemVisualProfileByItemDefinitionId(itemDefinitionId);
            Require(profile != null, "Missing item visual profile for '" + itemDefinitionId + "'.");
            AssertResolvedPrefab(
                database, profile.world_asset_key, expectedWorldPrefabName, expectedMaterialName,
                forbiddenPlaceholderTransform, false);
            AssertResolvedPrefab(
                database, profile.equipped_asset_key, expectedEquippedPrefabName, expectedMaterialName,
                forbiddenPlaceholderTransform, true);
        }

        private static void AssertResolvedPrefab(
            GameDatabase database,
            string assetKey,
            string expectedPrefabName,
            string expectedMaterialName,
            string forbiddenPlaceholderTransform,
            bool equipped)
        {
            var visualAsset = database.GetVisualAssetByKey(assetKey);
            Require(visualAsset != null, "Missing visual asset for key '" + assetKey + "'.");
            string error = null;
            GameObject prefab = null;
            Require(VisualAssetProviderRegistry.TryGet(visualAsset.provider_id, out IVisualAssetProvider provider) &&
                    provider.TryResolvePrefab(visualAsset, out prefab, out error) && prefab != null,
                "Could not resolve visual asset '" + assetKey + "': " + error);
            Require(prefab.name == expectedPrefabName,
                "Visual asset '" + assetKey + "' resolved '" + prefab.name + "' instead of '" + expectedPrefabName + "'.");
            AssertUsesMaterial(prefab, expectedMaterialName);
            Require(prefab.GetComponentsInChildren<Transform>(true)
                    .All(transform => transform.name != forbiddenPlaceholderTransform),
                "Visual asset '" + assetKey + "' still contains placeholder transform '" +
                forbiddenPlaceholderTransform + "'.");
            if (equipped)
            {
                Require(EquippedVisualPrefabContract.TryValidate(prefab, out string validationError),
                    "Equipped visual prefab '" + expectedPrefabName + "' is invalid: " + validationError);
            }
        }

        private static void AssertWorldItemPrefab(
            string resourceName,
            string expectedItemDefinitionId,
            string expectedMaterialName)
        {
            GameObject prefab = Resources.Load<GameObject>(resourceName);
            Require(prefab != null, "World item prefab is missing: " + resourceName);
            WorldItemPickup pickup = prefab.GetComponent<WorldItemPickup>();
            Require(pickup != null && ContentId.TryResolveLegacyCore(
                    pickup.ItemDefinitionId, out ContentId resolved, out _, out _) &&
                    resolved.Canonical == expectedItemDefinitionId,
                "World item prefab '" + resourceName + "' lost its gameplay item identity.");
            Require(prefab.GetComponents<BoxCollider>().Length == 1 &&
                    prefab.GetComponent<Rigidbody>() != null &&
                    prefab.GetComponent<WorldObjectTags>() != null &&
                    prefab.GetComponent<WorldObjectDebugInfo>() != null,
                "World item prefab '" + resourceName + "' lost gameplay, physics or interaction components.");
            Require(prefab.transform.Find("Visual") != null,
                "World item prefab '" + resourceName + "' lost its authored Visual child.");
            AssertUsesMaterial(prefab, expectedMaterialName);
        }

        private static void AssertUsesMaterial(GameObject root, string expectedMaterialName)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Require(renderers.Length > 0 && renderers
                    .SelectMany(renderer => renderer.sharedMaterials)
                    .Any(material => material != null && material.name == expectedMaterialName),
                "Visual '" + root.name + "' does not use material '" + expectedMaterialName + "'.");
        }

        private static void AssertVisualState(ActorRuntimeIdentity actor, params string[] expectedDefinitionIds)
        {
            Require(actor != null, "Spawned actor identity is missing.");
            ActorEquipmentComponent equipment = actor.GetComponent<ActorEquipmentComponent>();
            EntityEquipmentVisualSynchronizer synchronizer =
                actor.GetComponentInChildren<EntityEquipmentVisualSynchronizer>(true);
            EntityVisualRigRuntime rig = actor.GetComponentInChildren<EntityVisualRigRuntime>(true);
            Require(equipment != null && synchronizer != null && rig != null,
                "Runtime humanoid equipment/visual composition is incomplete.");
            var serializedSynchronizer = new SerializedObject(synchronizer);
            Require(serializedSynchronizer.FindProperty("equipmentVisualSourceBehaviour").objectReferenceValue == equipment,
                "Runtime humanoid synchronizer is not explicitly bound to its ActorEquipmentComponent.");
            Require(synchronizer.ActiveVisualCount == expectedDefinitionIds.Length,
                "Runtime humanoid did not reflect exactly one visual per equipped item.");
            foreach (string definitionId in expectedDefinitionIds)
            {
                Require(HasVisual(actor, definitionId), "Missing equipped visual for '" + definitionId + "'.");
                if (definitionId == CrowbarItemId)
                    AssertUsesMaterial(FindVisual(actor, definitionId).gameObject, CrowbarMaterialName);
                else if (definitionId == RifleItemId)
                    AssertUsesMaterial(FindVisual(actor, definitionId).gameObject, RifleMaterialName);
            }

            EquippedVisualInstanceMarker backpack = actor
                .GetComponentsInChildren<EquippedVisualInstanceMarker>(true)
                .SingleOrDefault(marker => marker.DefinitionId == "core:small_backpack_01");
            Require(backpack != null && backpack.SocketId == "human_back_socket",
                "Small backpack did not resolve to the humanoid back socket.");
        }

        private static bool HasVisual(ActorRuntimeIdentity actor, string definitionId)
        {
            return FindVisual(actor, definitionId) != null;
        }

        private static EquippedVisualInstanceMarker FindVisual(ActorRuntimeIdentity actor, string definitionId)
        {
            return actor.GetComponentsInChildren<EquippedVisualInstanceMarker>(true)
                .SingleOrDefault(marker => marker.DefinitionId == definitionId);
        }

        private static int CountVisuals(ActorRuntimeIdentity actor, string instanceId)
        {
            return actor.GetComponentsInChildren<EquippedVisualInstanceMarker>(true)
                .Count(marker => marker.InstanceId == instanceId);
        }

        private static void DestroyActor(ActorRuntimeIdentity actor)
        {
            if (actor != null)
                UnityEngine.Object.DestroyImmediate(actor.gameObject);
        }

        private static void Require(bool condition, string failure)
        {
            if (!condition)
                throw new InvalidOperationException(failure);
        }

        private sealed class RendererMaterialSnapshot
        {
            private readonly Renderer renderer;
            private readonly Material[] materials;
            private readonly Color[] colors;

            public RendererMaterialSnapshot(Renderer renderer)
            {
                this.renderer = renderer;
                materials = renderer.sharedMaterials;
                colors = materials.Select(material => material != null ? material.color : default).ToArray();
            }

            public bool IsUnchanged()
            {
                Material[] current = renderer.sharedMaterials;
                if (current.Length != materials.Length)
                    return false;
                for (int index = 0; index < current.Length; index++)
                {
                    if (current[index] != materials[index])
                        return false;
                    if (current[index] != null && !SameColor(current[index].color, colors[index]))
                        return false;
                }
                return true;
            }
        }
    }
}
