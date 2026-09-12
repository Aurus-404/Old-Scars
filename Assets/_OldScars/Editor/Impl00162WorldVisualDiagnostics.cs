using System;
using System.Linq;
using OldScars.Core;
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
    public static class Impl00162WorldVisualDiagnostics
    {
        private const string BatchPendingKey = "OldScars.IMPL00162.BatchPending";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        private static readonly VisualSpec[] Specs =
        {
            new VisualSpec(
                "core:bandage_01",
                "core:bandage_world",
                "PFB_VIS_Bandage_World",
                "MAT_Bandage",
                0.18f),
            new VisualSpec(
                "core:water_bottle_01",
                "core:canteen_world",
                "PFB_VIS_Canteen_World",
                "MAT_Canteen",
                0.28f),
            new VisualSpec(
                "core:food_ration_01",
                "core:canned_food_world",
                "PFB_VIS_Canned_Food_PSX_World",
                "MAT_Canned_Food_PSX",
                0.14f,
                1)
        };

        static Impl00162WorldVisualDiagnostics()
        {
            EditorApplication.update += RunBatchWhenReady;
        }

        [MenuItem("Old Scars/Diagnostics/IMPL-0016.2 World Visual Placeholders")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying || GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
                throw new InvalidOperationException("IMPL-0016.2 diagnostics require Play Mode with Core data ready.");

            Require(GameDataManager.Instance.Report != null && GameDataManager.Instance.Report.ErrorCount == 0,
                "GameData loaded with validation errors.");

            GameDatabase database = GameDataManager.Instance.Database;
            var actor = new GameObject("IMPL-0016.2 World Visual Fixture");
            InventoryComponent inventory = actor.AddComponent<InventoryComponent>();
            ActorInteractionContext interaction = actor.AddComponent<ActorInteractionContext>();
            try
            {
                for (int index = 0; index < Specs.Length; index++)
                    ValidateSpec(database, inventory, interaction, Specs[index]);

                AssertAmmoBoxesWereNotMapped(database);
                Debug.Log("IMPL-0016.2 World Visual Placeholder Diagnostics: PASS");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
                WorldItemPickup[] leftovers = UnityEngine.Object.FindObjectsByType<WorldItemPickup>(
                    FindObjectsInactive.Include);
                for (int index = 0; index < leftovers.Length; index++)
                {
                    if (leftovers[index] != null && leftovers[index].name.StartsWith("Dropped World Item - ", StringComparison.Ordinal))
                        UnityEngine.Object.DestroyImmediate(leftovers[index].gameObject);
                }
            }
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("IMPL-0016.2 batch diagnostics require Unity batchmode.");
            if (EditorApplication.isCompiling)
                throw new InvalidOperationException("IMPL-0016.2 batch diagnostics cannot start while compiling.");

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

        private static void ValidateSpec(
            GameDatabase database,
            InventoryComponent inventory,
            ActorInteractionContext interaction,
            VisualSpec spec)
        {
            ItemDefinition item = database.GetItem(spec.ItemDefinitionId);
            Require(item != null, "Expected ItemDefinition was not loaded: " + spec.ItemDefinitionId);
            Require(item.equippable != true && (item.equip == null || item.equip.equippable != true),
                "World-only placeholder unexpectedly expanded Equipment: " + spec.ItemDefinitionId);

            ItemVisualProfileDefinition profile = database.GetItemVisualProfileByItemDefinitionId(spec.ItemDefinitionId);
            Require(profile != null && profile.world_asset_key == spec.AssetKey,
                "ItemVisualProfile did not resolve the expected world asset: " + spec.ItemDefinitionId);
            Require(profile.equipped_asset_key == profile.world_asset_key,
                "Non-equippable world placeholder should not introduce a distinct equipped asset.");

            VisualAssetDefinition asset = database.GetVisualAssetByKey(spec.AssetKey);
            Require(asset != null && asset.provider_id == "builtin",
                "VisualAsset is missing or does not use the builtin provider: " + spec.AssetKey);
            string providerError = null;
            GameObject prefab = null;
            Require(VisualAssetProviderRegistry.TryGet(asset.provider_id, out IVisualAssetProvider provider) &&
                    provider.TryResolvePrefab(asset, out prefab, out providerError) && prefab != null,
                "VisualAsset could not resolve: " + spec.AssetKey + " | " + providerError);
            Require(prefab.name == spec.PrefabName, "Unexpected prefab resolved for " + spec.AssetKey);
            Require(prefab.GetComponentsInChildren<Collider>(true).Length == 0 &&
                    prefab.GetComponentsInChildren<Rigidbody>(true).Length == 0 &&
                    prefab.GetComponentsInChildren<WorldItemPickup>(true).Length == 0 &&
                    prefab.GetComponentsInChildren<Light>(true).Length == 0 &&
                    prefab.GetComponentsInChildren<Camera>(true).Length == 0,
                "Presentation prefab contains imported scene, gameplay or physics authority: " + spec.PrefabName);
            Require(UsesMaterial(prefab, spec.MaterialName),
                "Presentation prefab does not use its derived material: " + spec.PrefabName);
            Require(Mathf.Abs(GetMaxRendererDimension(prefab) - spec.TargetDimension) <= 0.011f,
                "Presentation prefab physical dimension differs from its target: " + spec.PrefabName);
            Require(spec.ExpectedRendererCount <= 0 ||
                    prefab.GetComponentsInChildren<Renderer>(true).Length == spec.ExpectedRendererCount,
                "Presentation prefab contains an unexpected number of model renderers: " + spec.PrefabName);

            ItemInstance itemInstance = inventory.AddItemByDefinitionId(spec.ItemDefinitionId, 1);
            int sourceIndex = -1;
            Require(itemInstance != null && inventory.TryGetEntryByInstanceId(itemInstance.InstanceId, out sourceIndex, out _),
                "Could not create the runtime item fixture: " + spec.ItemDefinitionId);
            Require(DroppedWorldItemSpawner.TryDrop(
                    inventory, sourceIndex, 1, "core:drop", "Drop", out string dropMessage),
                "Existing drop authority rejected " + spec.ItemDefinitionId + ": " + dropMessage);

            WorldItemPickup pickup = UnityEngine.Object.FindObjectsByType<WorldItemPickup>(
                    FindObjectsInactive.Include)
                .Single(value => value.HasInitializedSource && value.ItemDefinitionId == spec.ItemDefinitionId);
            WorldItemDebugVisualBuilder.Build(pickup.transform, spec.ItemDefinitionId);
            WorldItemDebugVisualBuilder.Build(pickup.transform, spec.ItemDefinitionId);

            Transform visual = pickup.transform.Find("Visual");
            Require(pickup.GetComponents<BoxCollider>().Length == 1 && pickup.GetComponent<Rigidbody>() != null,
                "Dropped world item lost its existing collider/Rigidbody contract: " + spec.ItemDefinitionId);
            Require(pickup.GetComponent<WorldObjectTags>() != null && pickup.GetComponent<WorldObjectDebugInfo>() != null,
                "Dropped world item lost interaction/debug components: " + spec.ItemDefinitionId);
            Require(visual != null && visual.GetComponentsInChildren<Renderer>(true).Length > 0,
                "Dropped world item has no resolved world visual: " + spec.ItemDefinitionId);
            Require(visual.GetComponentsInChildren<Transform>(true).Count(value => value.name == "Visual Model") == 1,
                "Repeated visual rebuild produced a duplicate model: " + spec.ItemDefinitionId);
            Require(UsesMaterial(visual.gameObject, spec.MaterialName),
                "Dropped world item did not preserve the derived material: " + spec.ItemDefinitionId);

            DebugActionExecutionResult pickupResult = pickup.PickUp(
                interaction, pickup.GetComponent<WorldObjectTags>());
            Require(pickupResult.hasResult && pickup.Quantity == 0 &&
                    inventory.TryGetEntryByInstanceId(itemInstance.InstanceId, out _, out _),
                "Existing pickup/transfer authority did not complete: " + spec.ItemDefinitionId);
        }

        private static void AssertAmmoBoxesWereNotMapped(GameDatabase database)
        {
            ItemVisualProfileDefinition ammoProfile = database.GetItemVisualProfileByItemDefinitionId("core:ammo_303_british_01");
            Require(ammoProfile == null,
                "Ammo Boxes must not be mapped onto the individual core:ammo_303_british_01 identity.");
        }

        private static bool UsesMaterial(GameObject root, string expectedMaterialName)
        {
            return root.GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .Any(material => material != null && material.name == expectedMaterialName);
        }

        private static float GetMaxRendererDimension(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Require(renderers.Length > 0, "Visual prefab has no Renderer: " + root.name);
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private readonly struct VisualSpec
        {
            public VisualSpec(
                string itemDefinitionId,
                string assetKey,
                string prefabName,
                string materialName,
                float targetDimension,
                int expectedRendererCount = 0)
            {
                ItemDefinitionId = itemDefinitionId;
                AssetKey = assetKey;
                PrefabName = prefabName;
                MaterialName = materialName;
                TargetDimension = targetDimension;
                ExpectedRendererCount = expectedRendererCount;
            }

            public string ItemDefinitionId { get; }
            public string AssetKey { get; }
            public string PrefabName { get; }
            public string MaterialName { get; }
            public float TargetDimension { get; }
            public int ExpectedRendererCount { get; }
        }
    }
}
