using System;
using OldScars.Core.Actors;
using OldScars.Core.Combat;
using OldScars.Core.Feedback;
using OldScars.Core.Interactions;
using OldScars.Core.Items;
using UnityEngine;

namespace OldScars.Core.ApplicationShell
{
    /// <summary>
    /// Shared scene-local wiring for the existing gameplay authorities and
    /// development integration surfaces. It owns no gameplay state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayRuntimeComposition : MonoBehaviour
    {
        private PlayerGameplayComposition player;
        private GameplayFeedbackLog feedbackLog;
        private DebugFeedbackLogPanel feedbackPanel;
        private ContextualActionDebugResultPanel actionResultPanel;
        private DebugActionProgressController actionProgress;
        private ContextualActionDebugProgressPanel actionProgressPanel;
        private ContextualActionDebugPanel actionPanel;
        private DebugActionAvailabilityPanel actionAvailabilityPanel;
        private InventoryUISessionController inventorySession;
        private InventoryDebugPanel inventoryPanel;
        private ItemStorageDebugPanel storagePanel;
        private ActorNeedsDebugPanel needsPanel;
        private ActorHealthDebugWindow healthWindow;
        private DebugWorldUiInputBlocker inputBlocker;
        private WorldInteractionDebugTester worldInteraction;
        private FirearmDebugController firearmController;
        private SandboxNpcController sandboxNpcController;
        private SandboxNpcObservabilityPanel sandboxNpcObservabilityPanel;

        public PlayerGameplayComposition Player => player;
        public InventoryUISessionController InventorySession => inventorySession;
        public InventoryDebugPanel InventoryPanel => inventoryPanel;
        public ItemStorageDebugPanel StoragePanel => storagePanel;
        public ActorNeedsDebugPanel NeedsPanel => needsPanel;
        public ActorHealthDebugWindow HealthWindow => healthWindow;
        public DebugWorldUiInputBlocker InputBlocker => inputBlocker;
        public WorldInteractionDebugTester WorldInteraction => worldInteraction;
        public FirearmDebugController FirearmController => firearmController;
        public SandboxNpcController SandboxNpcController => sandboxNpcController;
        public SandboxNpcObservabilityPanel SandboxNpcObservability => sandboxNpcObservabilityPanel;

        public static bool TryCreateAndBind(
            Transform parent,
            PlayerGameplayComposition playerComposition,
            out GameplayRuntimeComposition composition,
            out string failure)
        {
            composition = null;
            failure = null;
            if (playerComposition == null)
            {
                failure = "Shared gameplay runtime requires the actual player composition.";
                return false;
            }

            GameplayRuntimeComposition[] existing =
                FindObjectsByType<GameplayRuntimeComposition>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (existing.Length > 0)
            {
                failure = "A shared GameplayRuntimeComposition already exists in the loaded runtime.";
                return false;
            }

            var root = new GameObject("Shared Gameplay Runtime");
            root.transform.SetParent(parent, false);
            GameplayRuntimeComposition created = root.AddComponent<GameplayRuntimeComposition>();
            try
            {
                created.BuildSurfaces();
                created.BindPlayer(playerComposition);
                if (!created.TryValidate(out failure))
                    throw new InvalidOperationException(failure);
                composition = created;
                return true;
            }
            catch (Exception exception)
            {
                failure = string.IsNullOrWhiteSpace(exception.Message)
                    ? exception.GetType().Name
                    : exception.Message;
                if (Application.isPlaying) Destroy(root);
                else DestroyImmediate(root);
                return false;
            }
        }

        public void BindPlayer(PlayerGameplayComposition playerComposition)
        {
            if (playerComposition == null)
                throw new ArgumentNullException(nameof(playerComposition));
            BuildSurfaces();
            player = playerComposition;

            ActorInteractionContext actor = player.PlayerContext;
            InventoryComponent inventory = actor.GetComponent<InventoryComponent>();
            ActorNeedsComponent needs = actor.GetComponent<ActorNeedsComponent>();
            ActorHealthComponent health = actor.GetComponent<ActorHealthComponent>();
            ActorEquipmentComponent equipment = actor.GetComponent<ActorEquipmentComponent>();
            firearmController = actor.GetComponent<FirearmDebugController>() ??
                                actor.gameObject.AddComponent<FirearmDebugController>();

            inventoryPanel.BindPlayer(inventory, needs, health, firearmController, equipment);
            healthWindow.BindRuntime(health, inventorySession);
            inventorySession.BindRuntime(
                inventoryPanel, storagePanel, inventory, player.MovementController, healthWindow);
            needsPanel.BindRuntime(
                needs,
                WorldClock.Current,
                inventorySession,
                player.MovementController,
                player.PlayerIdentity,
                player.CameraRig,
                player.GameplayCamera,
                inputBlocker,
                sandboxNpcController);
            sandboxNpcController.BindRuntime(player.PlayerTransform);
            sandboxNpcObservabilityPanel.BindRuntime(sandboxNpcController, player.GameplayCamera);
            inputBlocker.BindRuntime(
                actionPanel, actionResultPanel, inventoryPanel, storagePanel,
                needsPanel, healthWindow, inventorySession);
            worldInteraction.BindRuntime(
                actor, actionPanel, actionProgress, actionAvailabilityPanel, inputBlocker);
            player.MovementInput.BindRuntime(
                player.GameplayCamera, player.MovementController, inputBlocker,
                actionProgress, inventorySession);
            player.CameraRig.BindRuntime(player.PlayerTransform, inventorySession, inputBlocker);
            firearmController.BindRuntime(
                inventory, player.GameplayCamera, inputBlocker, player.MovementInput, actionProgress);
        }

        public bool TryValidate(out string failure)
        {
            failure = null;
            if (player == null || !player.TryValidateRuntime(out failure))
                return false;
            if (WorldClock.Current == null)
                return Fail("WorldClock authority is missing.", out failure);
            if (inventorySession == null || inventoryPanel == null || storagePanel == null)
                return Fail("Inventory runtime surfaces are incomplete.", out failure);
            if (needsPanel == null || healthWindow == null)
                return Fail("Needs/health runtime surfaces are incomplete.", out failure);
            if (worldInteraction == null || actionPanel == null || actionProgress == null || inputBlocker == null)
                return Fail("Interaction runtime surfaces are incomplete.", out failure);
            if (firearmController == null)
                return Fail("Existing firearm/combat input surface is missing.", out failure);
            if (sandboxNpcController == null)
                return Fail("Sandbox NPC spawn adapter is missing from the shared runtime.", out failure);
            if (sandboxNpcObservabilityPanel == null)
                return Fail("Sandbox NPC observability surface is missing from the shared runtime.", out failure);
            if (player.Stamina == null)
                return Fail("Player stamina authority is missing.", out failure);

            if (FindObjectsByType<PlayerGameplayComposition>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1)
                return Fail("Integrated runtime must contain exactly one player composition.", out failure);
            if (FindObjectsByType<InventoryUISessionController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1)
                return Fail("Integrated runtime must contain exactly one inventory UI session authority.", out failure);
            if (FindObjectsByType<ActorNeedsDebugPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1 ||
                FindObjectsByType<ActorHealthDebugWindow>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1)
                return Fail("Integrated runtime must contain one needs and one health integration surface.", out failure);
            if (FindObjectsByType<ActorStaminaComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1)
                return Fail("Integrated runtime must contain exactly one player stamina authority.", out failure);
            if (FindObjectsByType<WorldInteractionDebugTester>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1)
                return Fail("Integrated runtime must contain exactly one world interaction surface.", out failure);
            if (FindObjectsByType<WorldClock>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1)
                return Fail("Integrated runtime must contain exactly one WorldClock authority.", out failure);

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int mainCameraCount = 0;
            for (int index = 0; index < cameras.Length; index++)
                if (cameras[index].CompareTag("MainCamera")) mainCameraCount++;
            if (mainCameraCount != 1 || player.GameplayCamera == null || !player.GameplayCamera.CompareTag("MainCamera"))
                return Fail("Integrated runtime must contain exactly one gameplay MainCamera.", out failure);
            return true;
        }

        public string DescribeReadiness(bool fixtureReady)
        {
            return "Player: 1\nCamera: 1\nInventorySession: 1\nNeedsPanel: 1\n" +
                   "HealthWindow: 1\nWorldClock: 1\nWorldInteraction: 1\n" +
                   "DevelopmentFixture: " + (fixtureReady ? "READY" : "ABSENT");
        }

        private void BuildSurfaces()
        {
            feedbackLog = GetOrAdd(feedbackLog);
            feedbackPanel = GetOrAdd(feedbackPanel);
            actionResultPanel = GetOrAdd(actionResultPanel);
            actionProgress = GetOrAdd(actionProgress);
            actionProgressPanel = GetOrAdd(actionProgressPanel);
            actionPanel = GetOrAdd(actionPanel);
            actionAvailabilityPanel = GetOrAdd(actionAvailabilityPanel);
            inventorySession = GetOrAdd(inventorySession);
            inventoryPanel = GetOrAdd(inventoryPanel);
            storagePanel = GetOrAdd(storagePanel);
            needsPanel = GetOrAdd(needsPanel);
            healthWindow = GetOrAdd(healthWindow);
            inputBlocker = GetOrAdd(inputBlocker);
            worldInteraction = GetOrAdd(worldInteraction);
            sandboxNpcController = GetOrAdd(sandboxNpcController);
            sandboxNpcObservabilityPanel = GetOrAdd(sandboxNpcObservabilityPanel);
        }

        private T GetOrAdd<T>(T current) where T : Component
        {
            return current != null ? current : GetComponent<T>() ?? gameObject.AddComponent<T>();
        }

        private static bool Fail(string message, out string failure)
        {
            failure = message;
            return false;
        }
    }
}
