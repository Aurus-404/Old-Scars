using System;
using System.Collections.Generic;
using OldScars.Core;
using OldScars.Core.Actors;
using OldScars.Core.Data;
using OldScars.Core.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldScars.EditorTools
{
    [InitializeOnLoad]
    public static class InventoryInteractionUxDiagnostics
    {
        private const string Menu = "Old Scars/Diagnostics/Inventory/Run Interaction UX Correction";
        private const string BatchPendingKey = "OldScars.InventoryInteractionUxDiagnostics.BatchPending";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        static InventoryInteractionUxDiagnostics()
        {
            EditorApplication.update += RunBatchWhenReady;
        }

        [MenuItem(Menu)]
        public static void Run()
        {
            if (!EditorApplication.isPlaying || GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
                throw new InvalidOperationException("Inventory Interaction UX diagnostics require Play Mode with GameData ready.");

            var errors = new List<string>();
            GameObject sourceObject = null;
            GameObject playerObject = null;
            try
            {
                sourceObject = new GameObject("InventoryInteractionUxDiagnostics_Source");
                playerObject = new GameObject("InventoryInteractionUxDiagnostics_Player");
                InventoryComponent source = sourceObject.AddComponent<InventoryComponent>();
                InventoryComponent player = playerObject.AddComponent<InventoryComponent>();
                ActorNeedsComponent needs = playerObject.AddComponent<ActorNeedsComponent>();
                ActorNeedState thirst = FindNeed(needs, "thirst");
                if (thirst != null)
                    thirst.currentValue = 0f;

                ItemInstance water = source.AddItemByDefinitionId("core:water_bottle_01", 3);
                ItemInstance scrap = source.AddItemByDefinitionId("core:scrap_metal_01", 1);
                Require(water != null && scrap != null, "diagnostic item setup", errors);

                IReadOnlyList<InventoryContextAction> waterActions =
                    InventoryContextActionResolver.ResolveExternal(source, water?.InstanceId);
                IReadOnlyList<InventoryContextAction> scrapActions =
                    InventoryContextActionResolver.ResolveExternal(source, scrap?.InstanceId);
                Require(HasAction(waterActions, InventoryContextActionKind.Use), "external consumable resolves Use", errors);
                Require(!HasAction(scrapActions, InventoryContextActionKind.Use), "external non-consumable omits Use", errors);
                Require(HasAction(waterActions, InventoryContextActionKind.ShowDetails), "external details action is exposed", errors);

                float thirstBefore = needs.GetNeedValue("thirst");
                InventoryItemUseResult useFirst = InventoryItemUseService.TryUseExternalItem(
                    source, water.InstanceId, player, needs, null, default);
                Require(useFirst.Success, "external consumption succeeds", errors);
                Require(GetQuantity(source, water.InstanceId) == 2, "external consumption removes exactly one", errors);
                Require(player.IsEmpty, "external consumption does not transfer to player inventory", errors);
                Require(needs.GetNeedValue("thirst") > thirstBefore, "external consumption restores player need", errors);

                Require(InventoryItemUseService.TryUseExternalItem(source, water.InstanceId, player, needs, null, default).Success,
                    "external second consumption succeeds", errors);
                Require(InventoryItemUseService.TryUseExternalItem(source, water.InstanceId, player, needs, null, default).Success,
                    "external final consumption succeeds", errors);
                Require(!source.TryGetEntryByInstanceId(water.InstanceId, out _, out _), "external final unit removes its entry", errors);

                ItemInstance quickTransfer = source.AddItemByDefinitionId("core:water_bottle_01", 3);
                InventoryMutationResult firstUnit = GridStorageTransferService.TransferQuantityAuto(
                    source, player, quickTransfer.InstanceId, 1, true, GridStorageTransferQuantityPolicy.Exact, default);
                InventoryMutationResult secondUnit = GridStorageTransferService.TransferQuantityAuto(
                    source, player, quickTransfer.InstanceId, 1, true, GridStorageTransferQuantityPolicy.Exact, default);
                Require(firstUnit.Success && secondUnit.Success, "two exact one-unit transfers succeed", errors);
                Require(GetQuantity(source, quickTransfer.InstanceId) == 1, "one-unit transfers preserve source remainder", errors);
                Require(GetTotalQuantity(player) == 2, "one-unit transfers merge at destination", errors);

                ItemInstance shiftTransfer = source.AddItemByDefinitionId("core:water_bottle_01", 3);
                InventoryMutationResult stackTransfer = GridStorageTransferService.TransferStackAuto(
                    source, player, shiftTransfer.InstanceId,
                    GridStorageTransferService.GetAutomaticQuantityPolicy(source, player), default);
                Require(stackTransfer.Success && !source.TryGetEntryByInstanceId(shiftTransfer.InstanceId, out _, out _),
                    "stack quick transfer remains available", errors);
            }
            finally
            {
                if (sourceObject != null) UnityEngine.Object.Destroy(sourceObject);
                if (playerObject != null) UnityEngine.Object.Destroy(playerObject);
            }

            if (errors.Count > 0)
            {
                string failure = "Inventory Interaction UX Correction Diagnostics: FAIL\n- " + string.Join("\n- ", errors);
                Debug.LogError(failure);
                throw new InvalidOperationException(failure);
            }

            Debug.Log("Inventory Interaction UX Correction Diagnostics: PASS");
        }

        [MenuItem(Menu, true)]
        private static bool ValidateRun() => !EditorApplication.isCompiling;

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Inventory Interaction UX batch diagnostics require Unity batchmode.");

            SessionState.SetBool(BatchPendingKey, true);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void RunBatchWhenReady()
        {
            if (!SessionState.GetBool(BatchPendingKey, false) || !EditorApplication.isPlaying || GameDataManager.Instance == null ||
                !GameDataManager.Instance.IsReady)
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

        private static bool HasAction(IReadOnlyList<InventoryContextAction> actions, InventoryContextActionKind kind)
        {
            if (actions == null)
                return false;
            for (int index = 0; index < actions.Count; index++)
                if (actions[index].Kind == kind)
                    return true;
            return false;
        }

        private static int GetQuantity(InventoryComponent inventory, string instanceId)
        {
            return inventory != null && inventory.TryGetEntryByInstanceId(instanceId, out _, out ItemStorageEntry entry)
                ? entry.Quantity
                : 0;
        }

        private static int GetTotalQuantity(InventoryComponent inventory)
        {
            int total = 0;
            if (inventory?.Entries == null)
                return total;
            foreach (ItemStorageEntry entry in inventory.Entries)
                total += entry?.Quantity ?? 0;
            return total;
        }

        private static ActorNeedState FindNeed(ActorNeedsComponent needs, string needId)
        {
            foreach (ActorNeedState state in needs.RuntimeStates)
                if (state != null && state.needId == needId)
                    return state;
            return null;
        }

        private static void Require(bool condition, string label, List<string> errors)
        {
            if (!condition)
                errors.Add(label + " failed.");
        }
    }
}
