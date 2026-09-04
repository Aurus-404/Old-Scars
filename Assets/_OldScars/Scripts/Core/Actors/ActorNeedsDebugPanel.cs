using System;
using System.Collections.Generic;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Feedback;
using OldScars.Core.Interactions;
using OldScars.Core.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OldScars.Core.Actors
{
    public sealed class ActorNeedsDebugPanel : MonoBehaviour
    {
        private const float PanelWidth = 340f;
        private const float PanelHeight = 500f;
        private const int MaximumItemDebugQuantity = 1000;
        private static readonly float[] TimeMultipliers = { 1f, 2f, 3f, 5f, 10f, 20f, 50f, 100f };

        [SerializeField] private ActorNeedsComponent actorNeeds;
        [SerializeField] private WorldClock worldClock;
        [SerializeField] private InventoryUISessionController inventorySessionController;
        [SerializeField] private bool visible;

        private PlayerMovementController movementController;
        private ActorRuntimeIdentity playerIdentity;
        private ActorDebugAiAcquisitionExclusion playerAiAcquisitionExclusion;
        private ActorStaminaComponent stamina;
        private CameraRigController cameraRig;
        private Camera gameplayCamera;
        private DebugWorldUiInputBlocker inputBlocker;
        private SandboxNpcController sandboxNpcController;
        private Vector2 scrollPosition;
        private bool teleportArmed;
        private string teleportFeedback;
        private string itemDebugFilter = string.Empty;
        private string selectedItemDefinitionId;
        private string itemDebugFeedback;
        private Vector2 itemDebugScrollPosition;
        private int itemDebugQuantity = 1;
        private string itemDebugQuantityText = "1";
        private string sandboxSeedText = SandboxNpcController.DefaultBaseSeed.ToString();
        private string sandboxFeedback;

        private const string ItemDebugFilterControlName = "OldScars.ItemDebugFilter";

        public bool IsVisible => IsDevelopmentBuild && visible &&
                                 (inventorySessionController == null || !inventorySessionController.IsOpen);
        public bool IsTeleportArmed => teleportArmed;
        public bool IsItemDebugFilterFocused =>
            IsVisible && GUI.GetNameOfFocusedControl() == ItemDebugFilterControlName;

        private void Awake()
        {
            ResolveActorNeeds();
            ResolveWorldClock();
            ResolveInventorySessionController();
        }

        private void OnEnable()
        {
            ResolveActorNeeds();
            ResolveWorldClock();
            ResolveInventorySessionController();
        }

        private void Start()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (!IsDevelopmentBuild)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame)
            {
                visible = !visible;
                if (!visible)
                    DisarmTeleport();
            }

            if (teleportArmed && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                DisarmTeleport();
                teleportFeedback = "Teleport cancelled.";
            }

            HandleTeleportInput();
        }

        private void OnGUI()
        {
            if (!IsVisible)
            {
                return;
            }

            ResolveReferences();
            GUILayout.BeginArea(GetPanelRect(), GUI.skin.box);
            GUILayout.Label("RUNTIME DEBUG TOOLS");
            scrollPosition = GUILayout.BeginScrollView(
                scrollPosition,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUILayout.Height(PanelHeight - 34f));
            DrawPlayerControls();
            DrawNeedsControls();
            DrawWorldTimeControls();
            DrawCameraControls();
            DrawWorldControls();
            DrawNpcSandboxControls();
            DrawItemDebugControls();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        public bool ContainsScreenPosition(Vector2 screenPosition)
        {
            if (!IsVisible)
            {
                return false;
            }

            Vector2 guiPoint = ToGuiPosition(screenPosition);
            return GetPanelRect().Contains(guiPoint);
        }

        public void ToggleVisibility()
        {
            if (!IsDevelopmentBuild)
                return;

            visible = !visible;
            if (!visible)
                DisarmTeleport();
        }

        public void BindRuntime(
            ActorNeedsComponent needs,
            WorldClock clock,
            InventoryUISessionController inventorySession,
            PlayerMovementController movement,
            ActorRuntimeIdentity playerRuntimeIdentity,
            CameraRigController camera,
            Camera playerCamera,
            DebugWorldUiInputBlocker blocker,
            SandboxNpcController sandbox)
        {
            actorNeeds = needs;
            worldClock = clock;
            inventorySessionController = inventorySession;
            movementController = movement;
            playerIdentity = playerRuntimeIdentity;
            playerAiAcquisitionExclusion = playerIdentity != null
                ? playerIdentity.GetComponent<ActorDebugAiAcquisitionExclusion>()
                : null;
            stamina = movement != null ? movement.Stamina : null;
            cameraRig = camera;
            gameplayCamera = playerCamera;
            inputBlocker = blocker;
            sandboxNpcController = sandbox;
        }

        private void DrawNpcSandboxControls()
        {
            GUILayout.Space(6f);
            GUILayout.Label("NPC SANDBOX");
            if (sandboxNpcController == null)
            {
                GUILayout.Label("Sandbox spawn adapter: <NONE>");
                return;
            }
            GUILayout.Label("Base seed");
            sandboxSeedText = GUILayout.TextField(sandboxSeedText ?? string.Empty);
            if (GUILayout.Button("Spawn Random NPC", GUILayout.Height(24f)))
            {
                if (!sandboxNpcController.TrySetBaseSeed(sandboxSeedText, out string seedError))
                    sandboxFeedback = seedError;
                else if (!sandboxNpcController.TrySpawnRandomNpc(out _, out string spawnError))
                    sandboxFeedback = spawnError;
                else
                    sandboxFeedback = sandboxNpcController.LastFeedback;
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Spawn Blue NPC", GUILayout.Height(24f)))
            {
                if (!sandboxNpcController.TrySetBaseSeed(sandboxSeedText, out string seedError))
                    sandboxFeedback = seedError;
                else if (!sandboxNpcController.TrySpawnBlueNpc(out _, out string spawnError))
                    sandboxFeedback = spawnError;
                else
                    sandboxFeedback = sandboxNpcController.LastFeedback;
            }
            if (GUILayout.Button("Spawn Red NPC", GUILayout.Height(24f)))
            {
                if (!sandboxNpcController.TrySetBaseSeed(sandboxSeedText, out string seedError))
                    sandboxFeedback = seedError;
                else if (!sandboxNpcController.TrySpawnRedNpc(out _, out string spawnError))
                    sandboxFeedback = spawnError;
                else
                    sandboxFeedback = sandboxNpcController.LastFeedback;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Sequence: " + sandboxNpcController.SpawnSequence);
            if (!string.IsNullOrWhiteSpace(sandboxFeedback))
                GUILayout.Label(sandboxFeedback);
            GUILayout.Label(sandboxNpcController.DescribeLastSpawn());
            DrawSandboxNpcHealthDiagnostics();
        }

        private void DrawSandboxNpcHealthDiagnostics()
        {
            SandboxNpcMetadata lastSpawn = sandboxNpcController?.LastSpawn;
            if (lastSpawn == null)
                return;

            ActorRuntimeIdentity identity = lastSpawn.GetComponent<ActorRuntimeIdentity>();
            ActorHealthComponent health = lastSpawn.GetComponent<ActorHealthComponent>();
            ActorMedicalStateComponent medical = lastSpawn.GetComponent<ActorMedicalStateComponent>();
            string healthValue = health != null
                ? $"{health.CurrentHealth:0.#} / {health.MaxHealth:0.#}"
                : "<NONE>";
            string vitalFraction = medical != null ? medical.VitalFraction.ToString("0.###") : "<NONE>";
            string wounds = medical != null ? medical.WoundCount.ToString() : "<NONE>";
            string bleeding = medical != null
                ? medical.EffectiveBleedingRatePerGameHour.ToString("0.###")
                : "<NONE>";
            string lifecycle = identity != null
                ? identity.LifecycleState.ToString()
                : health != null && health.IsDead ? "Dead" : "Alive";
            GUILayout.Label("HEALTH DIAGNOSTIC\n" +
                            "Health: " + healthValue + "\n" +
                            "Vital fraction: " + vitalFraction + "\n" +
                            "Wounds: " + wounds + "\n" +
                            "Effective bleeding / game hour: " + bleeding + "\n" +
                            "Lifecycle: " + lifecycle);
        }

        private void DrawPlayerControls()
        {
            GUILayout.Space(4f);
            GUILayout.Label("PLAYER");
            if (movementController == null)
            {
                GUILayout.Label("Movement authority: <NONE>");
                return;
            }

            GUILayout.Label($"Movement multiplier: {movementController.DebugMovementMultiplier:0.##}x");
            float movementMultiplier = GUILayout.HorizontalSlider(movementController.DebugMovementMultiplier, 0.25f, 8f);
            if (!Mathf.Approximately(movementMultiplier, movementController.DebugMovementMultiplier))
                movementController.SetDebugMovementMultiplier(movementMultiplier);
            if (GUILayout.Button("Reset movement multiplier", GUILayout.Height(22f)))
                movementController.ResetDebugMovementMultiplier();

            DrawAiAcquisitionExclusionControl();

            if (stamina == null)
            {
                GUILayout.Label("Stamina authority: <NONE>");
                return;
            }

            GUILayout.Label($"Stamina: {stamina.CurrentStamina:0.#}/{stamina.MaximumStamina:0.#}" +
                            (stamina.IsExhausted ? " (exhausted)" : string.Empty));
            float staminaValue = GUILayout.HorizontalSlider(stamina.CurrentStamina, 0f, stamina.MaximumStamina);
            if (!Mathf.Approximately(staminaValue, stamina.CurrentStamina))
                stamina.TrySetCurrentStamina(staminaValue);
            if (GUILayout.Button("Full stamina", GUILayout.Height(22f)))
                stamina.TrySetCurrentStamina(stamina.MaximumStamina);
        }

        private void DrawNeedsControls()
        {
            GUILayout.Space(4f);
            GUILayout.Label("NEEDS");
            if (actorNeeds == null)
            {
                GUILayout.Label("ActorNeeds authority: <NONE>");
                return;
            }

            DrawNeedControl("hunger");
            DrawNeedControl("thirst");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Rest 1h", GUILayout.Height(22f)))
                ApplyDebugRest(WorldClock.SecondsPerHour);
            if (GUILayout.Button("Sleep 8h", GUILayout.Height(22f)))
                ApplyDebugRest(WorldClock.SecondsPerHour * 8d);
            GUILayout.EndHorizontal();
        }

        private void DrawNeedControl(string needId)
        {
            if (!actorNeeds.HasNeed(needId))
                return;

            float currentValue = actorNeeds.GetNeedValue(needId);
            float maximum = actorNeeds.GetNeedMaxValue(needId);
            string name = actorNeeds.GetNeedDisplayName(needId);
            GUILayout.Label($"{name}: {currentValue:0.#}/{maximum:0.#}");
            float nextValue = GUILayout.HorizontalSlider(currentValue, 0f, maximum);
            if (!Mathf.Approximately(currentValue, nextValue))
                actorNeeds.TrySetNeedValue(needId, nextValue);
        }

        private void DrawWorldTimeControls()
        {
            GUILayout.Space(4f);
            GUILayout.Label("WORLD TIME");
            if (worldClock == null)
            {
                GUILayout.Label("WorldClock authority: <NONE>");
                return;
            }

            GUILayout.Label($"{worldClock.DisplayTime}  ·  {worldClock.DebugTimeMultiplier:0}x");
            for (int index = 0; index < TimeMultipliers.Length; index += 4)
            {
                GUILayout.BeginHorizontal();
                for (int button = index; button < Mathf.Min(index + 4, TimeMultipliers.Length); button++)
                {
                    float multiplier = TimeMultipliers[button];
                    if (GUILayout.Button($"{multiplier:0}x", GUILayout.Height(22f)))
                        worldClock.TrySetDebugTimeMultiplier(multiplier, out _);
                }
                GUILayout.EndHorizontal();
            }
        }

        private void DrawCameraControls()
        {
            GUILayout.Space(4f);
            GUILayout.Label("CAMERA");
            if (cameraRig == null)
            {
                GUILayout.Label("CameraRig authority: <NONE>");
                return;
            }

            GUILayout.Label($"Pitch: {cameraRig.PitchDegrees:0.#}° · Zoom: {cameraRig.ActualZoomDistance:0.#}/{cameraRig.DesiredZoomDistance:0.#}");
            if (GUILayout.Button("Reset Camera", GUILayout.Height(22f)))
                cameraRig.ResetCamera();
        }

        private void DrawWorldControls()
        {
            GUILayout.Space(4f);
            GUILayout.Label("WORLD");
            if (GUILayout.Button(teleportArmed ? "Cancel Teleport" : "Teleport", GUILayout.Height(24f)))
            {
                if (teleportArmed)
                {
                    DisarmTeleport();
                    teleportFeedback = "Teleport cancelled.";
                }
                else
                {
                    teleportArmed = true;
                    teleportFeedback = "TELEPORT: ARMED — click a valid ground position.";
                }
            }

            if (teleportArmed)
                GUILayout.Label("TELEPORT: ARMED\nClick a valid world position");
            if (!string.IsNullOrWhiteSpace(teleportFeedback))
                GUILayout.Label(teleportFeedback);
        }

        private void DrawItemDebugControls()
        {
            GUILayout.Space(6f);
            GUILayout.Label("ITEM DEBUG");

            GameDataManager dataManager = GameDataManager.Instance;
            GameDatabase database = dataManager != null && dataManager.IsReady
                ? dataManager.Database
                : null;
            if (database == null)
            {
                GUILayout.Label("GameDataManager: <NOT READY>");
                return;
            }

            GUI.SetNextControlName(ItemDebugFilterControlName);
            itemDebugFilter = GUILayout.TextField(itemDebugFilter ?? string.Empty);
            string filter = itemDebugFilter.Trim();
            var matches = new List<ItemDefinition>();
            foreach (ItemDefinition definition in database.GetAllItems())
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.id))
                    continue;

                string displayName = definition.display != null ? definition.display.name : null;
                if (filter.Length > 0 &&
                    (definition.id.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) &&
                    (string.IsNullOrWhiteSpace(displayName) ||
                     displayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0))
                    continue;

                matches.Add(definition);
            }
            matches.Sort((left, right) => string.CompareOrdinal(left.id, right.id));

            itemDebugScrollPosition = GUILayout.BeginScrollView(
                itemDebugScrollPosition,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUILayout.Height(150f));
            for (int index = 0; index < matches.Count; index++)
            {
                ItemDefinition definition = matches[index];
                if (GUILayout.Button(FormatItemButtonLabel(definition), GUILayout.ExpandWidth(true), GUILayout.Height(22f)))
                    selectedItemDefinitionId = definition.id;
            }
            GUILayout.EndScrollView();

            if (matches.Count == 0)
                GUILayout.Label("No matching ItemDefinitions.");

            ItemDefinition selected = !string.IsNullOrWhiteSpace(selectedItemDefinitionId)
                ? database.GetItem(selectedItemDefinitionId)
                : null;
            GUILayout.Label("Selected: " + (selected == null ? "<NONE>" : FormatItem(selected)));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Quantity", GUILayout.Width(58f));
            itemDebugQuantityText = GUILayout.TextField(itemDebugQuantityText ?? "1", GUILayout.Width(64f));
            bool validQuantity = int.TryParse(itemDebugQuantityText, out int requestedQuantity);
            if (validQuantity)
            {
                itemDebugQuantity = Mathf.Clamp(requestedQuantity, 1, MaximumItemDebugQuantity);
                itemDebugQuantityText = itemDebugQuantity.ToString();
            }
            else
            {
                GUILayout.Label("Enter 1-" + MaximumItemDebugQuantity);
            }
            bool previousGuiEnabled = GUI.enabled;
            GUI.enabled = previousGuiEnabled && validQuantity;
            if (GUILayout.Button("Give " + itemDebugQuantity, GUILayout.Height(24f)))
                GrantSelectedItem(itemDebugQuantity);
            GUI.enabled = previousGuiEnabled;
            GUILayout.EndHorizontal();
            if (!string.IsNullOrWhiteSpace(itemDebugFeedback))
                GUILayout.Label(itemDebugFeedback);
        }

        private void GrantSelectedItem(int quantity)
        {
            if (string.IsNullOrWhiteSpace(selectedItemDefinitionId))
            {
                itemDebugFeedback = "Give failed: select an ItemDefinition first.";
                return;
            }

            InventoryComponent inventory = actorNeeds != null
                ? actorNeeds.GetComponent<InventoryComponent>()
                : null;
            if (inventory == null)
            {
                itemDebugFeedback = "Give failed: player InventoryComponent is missing.";
                return;
            }

            ItemInstance item = inventory.AddItemByDefinitionId(selectedItemDefinitionId, quantity);
            if (item == null)
            {
                itemDebugFeedback = "Give failed: no legal inventory space or item could not be created.";
                return;
            }

            itemDebugFeedback = "Granted " + GetItemDisplayName(item.DefinitionId) + " x" + quantity +
                                " [" + item.InstanceId + "]";
        }

        private static string FormatItem(ItemDefinition definition)
        {
            return GetItemDisplayName(definition) + "\nContentId: " + definition.id;
        }

        private static string FormatItemButtonLabel(ItemDefinition definition)
        {
            string label = GetItemDisplayName(definition);
            const int maximumLength = 34;
            if (label.Length <= maximumLength)
                return label;
            return label.Substring(0, maximumLength - 3) + "...";
        }

        private static string GetItemDisplayName(ItemDefinition definition)
        {
            string displayName = definition?.display != null ? definition.display.name : null;
            return string.IsNullOrWhiteSpace(displayName) ? definition?.id ?? "<NONE>" : displayName.Trim();
        }

        private static string GetItemDisplayName(string definitionId)
        {
            return GetItemDisplayName(GetItemDefinition(definitionId));
        }

        private static ItemDefinition GetItemDefinition(string definitionId)
        {
            GameDatabase database = GameDataManager.Instance?.Database;
            return database != null ? database.GetItem(definitionId) : null;
        }

        private void ApplyDebugRest(double durationGameSeconds)
        {
            ActorRestResult result = ActorRestService.TryRest(actorNeeds, durationGameSeconds);
            string actorName = actorNeeds != null ? actorNeeds.name : "<NONE>";
            string actorId = actorNeeds != null
                ? actorNeeds.GetComponent<ActorRuntimeIdentity>()?.ActorInstanceId ?? actorName
                : "<NONE>";
            GameplayFeedbackLog.TryRecord(new GameplayFeedbackEntry(
                GameplayFeedbackEntryType.Info,
                result.Message,
                actorId: actorId,
                actorDisplayName: actorName,
                debugOnly: true));

            if (!result.Success)
                Debug.LogWarning("[Rest][DEBUG_REST_REJECTED]" +
                    $"\nActorId: {actorId}\nDurationGameSeconds: {durationGameSeconds:R}" +
                    $"\nFailureCode: {result.FailureCode}\nFailure: {result.Message}\nActionTaken: world time was not advanced");
        }

        private void ResolveActorNeeds()
        {
            if (actorNeeds != null)
            {
                return;
            }

            actorNeeds = FindAnyObjectByType<ActorNeedsComponent>();
        }

        private void ResolveWorldClock()
        {
            if (worldClock == null)
                worldClock = WorldClock.Current;
        }

        private void ResolveInventorySessionController()
        {
            if (inventorySessionController == null)
                inventorySessionController = FindAnyObjectByType<InventoryUISessionController>();
        }

        private void ResolveReferences()
        {
            ResolveActorNeeds();
            ResolveWorldClock();
            ResolveInventorySessionController();
            if (movementController == null && actorNeeds != null)
                movementController = actorNeeds.GetComponent<PlayerMovementController>();
            if (playerIdentity == null && actorNeeds != null)
                playerIdentity = actorNeeds.GetComponent<ActorRuntimeIdentity>();
            if (playerAiAcquisitionExclusion == null && playerIdentity != null)
                playerAiAcquisitionExclusion = playerIdentity.GetComponent<ActorDebugAiAcquisitionExclusion>();
            if (stamina == null && movementController != null)
                stamina = movementController.Stamina;
            if (gameplayCamera == null)
                gameplayCamera = Camera.main;
            if (cameraRig == null && gameplayCamera != null)
                cameraRig = gameplayCamera.GetComponentInParent<CameraRigController>();
            if (inputBlocker == null)
                inputBlocker = FindAnyObjectByType<DebugWorldUiInputBlocker>();
        }

        private void DrawAiAcquisitionExclusionControl()
        {
            if (playerIdentity == null)
            {
                GUILayout.Label("Invisible to AI: <PLAYER IDENTITY NONE>");
                return;
            }

            if (playerAiAcquisitionExclusion == null)
                playerAiAcquisitionExclusion = playerIdentity.gameObject.AddComponent<ActorDebugAiAcquisitionExclusion>();

            bool current = playerAiAcquisitionExclusion.IsExcludedFromAutomaticThreatAcquisition;
            bool requested = GUILayout.Toggle(current, "Invisible to AI");
            if (requested != current)
                playerAiAcquisitionExclusion.SetExcludedFromAutomaticThreatAcquisition(requested);
            GUILayout.Label(requested
                ? "AI acquisition: EXCLUDED (debug)"
                : "AI acquisition: ELIGIBLE");
        }

        private void HandleTeleportInput()
        {
            if (!teleportArmed || !IsVisible || Mouse.current == null ||
                !Mouse.current.leftButton.wasPressedThisFrame || gameplayCamera == null || movementController == null)
            {
                return;
            }

            Vector2 screenPosition = Mouse.current.position.ReadValue();
            if (GetPanelRect().Contains(ToGuiPosition(screenPosition)) ||
                inputBlocker != null && inputBlocker.IsPointerOverBlockingPanel(screenPosition))
            {
                return;
            }

            Ray ray = gameplayCamera.ScreenPointToRay(screenPosition);
            if (!TryResolveTeleportGround(ray, out RaycastHit groundHit))
            {
                teleportFeedback = "Teleport rejected: select valid ground in the materialized world.";
                return;
            }

            CharacterController controller = movementController.GetComponent<CharacterController>();
            if (controller == null)
            {
                teleportFeedback = "Teleport rejected: player CharacterController is missing.";
                return;
            }

            float clearance = Mathf.Max(0f, controller.height * 0.5f - controller.center.y + controller.skinWidth);
            Vector3 destination = groundHit.point + Vector3.up * clearance;
            if (!movementController.TryTeleportTo(destination, groundHit.collider, out string failure))
            {
                teleportFeedback = "Teleport rejected: " + failure;
                return;
            }

            DisarmTeleport();
            teleportFeedback = "Teleport complete.";
        }

        private bool TryResolveTeleportGround(Ray ray, out RaycastHit resolved)
        {
            resolved = default;
            RaycastHit[] hits = Physics.RaycastAll(ray, 500f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            float nearestDistance = float.PositiveInfinity;
            Transform player = movementController != null ? movementController.transform : null;
            for (int index = 0; index < hits.Length; index++)
            {
                RaycastHit hit = hits[index];
                Collider collider = hit.collider;
                if (collider == null || hit.normal.y < 0.65f || hit.distance >= nearestDistance ||
                    player != null && collider.transform.IsChildOf(player) ||
                    collider.GetComponentInParent<CharacterController>() != null ||
                    collider.GetComponentInParent<ActorInteractionContext>() != null ||
                    collider.GetComponentInParent<ActorRuntimeIdentity>() != null ||
                    !IsMaterializedGround(collider))
                {
                    continue;
                }

                resolved = hit;
                nearestDistance = hit.distance;
            }

            return resolved.collider != null;
        }

        private static bool IsMaterializedGround(Collider collider)
        {
            if (collider == null || collider.isTrigger)
                return false;

            if (collider is TerrainCollider)
                return true;

            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0 && collider.gameObject.layer == groundLayer)
                return true;

            // Authored building/platform surfaces opt into the existing
            // visibility marker. The hit-normal and CharacterController
            // clearance checks above still reject walls, actors and ceilings.
            BuildingOccluderTarget buildingSurface =
                collider.GetComponentInParent<BuildingOccluderTarget>();
            return buildingSurface != null && !buildingSurface.IsHidden;
        }

        private void DisarmTeleport()
        {
            teleportArmed = false;
        }

        private static bool IsDevelopmentBuild => Application.isEditor || Debug.isDebugBuild;

        private static Rect GetPanelRect()
        {
            return new Rect(16f, 16f, PanelWidth, PanelHeight);
        }

        private static Vector2 ToGuiPosition(Vector2 mousePosition)
        {
            return new Vector2(mousePosition.x, Screen.height - mousePosition.y);
        }
    }

    public sealed class ActorHealthDebugWindow : MonoBehaviour
    {
        private const int WindowId = 39041;
        private const float WindowWidth = 700f;
        private const float WindowHeight = 530f;

        [SerializeField] private ActorHealthComponent actorHealth;
        [SerializeField] private InventoryUISessionController inventorySessionController;
        [SerializeField] private float debugDamageAmount = 25f;
        [SerializeField] private bool isOpen;

        private Rect windowRect = new Rect(252f, 16f, WindowWidth, WindowHeight);
        private ActorMedicalStateComponent medicalState;
        private ActorItemOwnershipComponent itemOwnership;
        private ActorWoundTreatmentController woundTreatment;
        private BodyRegion selectedRegion = BodyRegion.Torso;
        private string selectedWoundId;
        private string feedback;
        private Vector2 woundScroll;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (isOpen && keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (!keyboard.hKey.wasPressedThisFrame)
                return;

            if (isOpen)
                Close();
            else
                Open();
        }

        private void OnGUI()
        {
            if (!isOpen)
                return;

            ResolveReferences();
            windowRect = GUI.Window(WindowId, windowRect, DrawWindowContents, "SALUD");
        }

        public void Toggle()
        {
            if (isOpen)
                Close();
            else
                Open();
        }

        public void Open()
        {
            ResolveReferences();
            if (inventorySessionController != null && inventorySessionController.IsOpen)
                inventorySessionController.CloseSession();
            isOpen = true;
        }

        public void Close()
        {
            isOpen = false;
        }

        public void SetActorHealth(ActorHealthComponent health)
        {
            actorHealth = health;
            medicalState = actorHealth != null ? actorHealth.GetComponent<ActorMedicalStateComponent>() : null;
            itemOwnership = actorHealth != null ? actorHealth.GetComponent<ActorItemOwnershipComponent>() : null;
            woundTreatment = actorHealth != null ? actorHealth.GetComponent<ActorWoundTreatmentController>() : null;
            selectedWoundId = null;
        }

        public void BindRuntime(
            ActorHealthComponent health,
            InventoryUISessionController inventorySession)
        {
            inventorySessionController = inventorySession;
            SetActorHealth(health);
        }

        public bool ContainsScreenPosition(Vector2 screenPosition)
        {
            return isOpen && windowRect.Contains(ToGuiPosition(screenPosition));
        }

        public string GetQualitativeStatus()
        {
            if (actorHealth == null)
                return "<NONE>";
            if (actorHealth.IsDead)
                return "Dead";
            if (medicalState != null && medicalState.EffectiveBleedingRatePerGameHour > 0.35f)
                return "Critical";
            if (actorHealth.MaxHealth > 0f && actorHealth.CurrentHealth / actorHealth.MaxHealth <= actorHealth.LowHealthThreshold)
                return "Critical";
            if (actorHealth.CurrentHealth < actorHealth.MaxHealth || (medicalState != null && medicalState.WoundCount > 0))
                return "Injured";
            return "Healthy";
        }

        public string GetRegionAssessment(BodyRegion region)
        {
            if (medicalState == null)
                return "Sin datos";

            ActorMedicalWoundState[] wounds = medicalState.GetWounds(region);
            if (wounds.Length == 0)
                return "Se ve bien.";

            float effectiveBleeding = 0f;
            float maximumSeverity = 0f;
            float pain = 0f;
            bool allBleedingControlled = true;
            for (int index = 0; index < wounds.Length; index++)
            {
                effectiveBleeding += ActorMedicalStateComponent.EffectiveBleedingRate(wounds[index]);
                maximumSeverity = Mathf.Max(maximumSeverity, wounds[index].severity);
                pain += wounds[index].painContribution;
                if (wounds[index].bleedingRatePerGameHour > 0f &&
                    wounds[index].treatmentState != WoundTreatmentState.Bandaged.ToString())
                {
                    allBleedingControlled = false;
                }
            }

            string severity = ActorMedicalStateComponent.SeverityLabel(maximumSeverity);
            string bleeding = effectiveBleeding <= 0f ? "No está sangrando." :
                allBleedingControlled ? "La herida parece estable bajo el vendaje." :
                effectiveBleeding < 0.15f ? "Está sangrando un poco." : "Está sangrando bastante.";
            string painText = pain < 0.3f ? "Duele un poco." : pain < 0.7f ? "Me duele." : "Me duele bastante.";
            return $"Tengo {wounds.Length} herida(s), gravedad {severity}. {bleeding} {painText}";
        }

        private void DrawWindowContents(int windowId)
        {
            if (GUI.Button(new Rect(WindowWidth - 28f, 2f, 24f, 20f), "X"))
                Close();

            if (actorHealth == null || medicalState == null)
            {
                GUI.Label(new Rect(20f, 42f, WindowWidth - 40f, 24f), "Estado médico del actor: <NONE>");
            }
            else
            {
                GUI.Label(new Rect(20f, 32f, 215f, 24f), "REGIONES DEL CUERPO");
                DrawBodyRegion(BodyRegion.Head, new Rect(84f, 58f, 72f, 32f));
                DrawBodyRegion(BodyRegion.Torso, new Rect(70f, 96f, 100f, 56f));
                DrawBodyRegion(BodyRegion.LeftArm, new Rect(18f, 101f, 46f, 90f));
                DrawBodyRegion(BodyRegion.RightArm, new Rect(176f, 101f, 46f, 90f));
                DrawBodyRegion(BodyRegion.LeftLeg, new Rect(70f, 159f, 46f, 108f));
                DrawBodyRegion(BodyRegion.RightLeg, new Rect(124f, 159f, 46f, 108f));
                GUI.Label(new Rect(20f, 285f, 210f, 48f), $"Estado general: {GetQualitativeStatus()}\nDolor: {PainLabel(medicalState.TotalPain)}");

                DrawSelectedRegionPanel();
                DrawDebugArea();
            }

            GUI.DragWindow(new Rect(0f, 0f, WindowWidth - 32f, 24f));
        }

        private void DrawBodyRegion(BodyRegion region, Rect rect)
        {
            Color previous = GUI.color;
            bool hovered = rect.Contains(Event.current.mousePosition);
            if (region == selectedRegion)
                GUI.color = new Color(1f, 0.72f, 0.35f);
            else if (hovered)
                GUI.color = new Color(1f, 0.9f, 0.65f);

            if (GUI.Button(rect, RegionLabel(region)))
                SelectRegion(region);
            GUI.color = previous;

            if (hovered)
                GUI.Label(new Rect(20f, 340f, 210f, 42f), GetRegionAssessment(region));
        }

        private void DrawSelectedRegionPanel()
        {
            ActorMedicalWoundState[] wounds = medicalState.GetWounds(selectedRegion);
            if (!ContainsWound(wounds, selectedWoundId))
                selectedWoundId = wounds.Length > 0 ? wounds[0].woundId : null;

            GUILayout.BeginArea(new Rect(245f, 35f, 435f, 382f), GUI.skin.box);
            GUILayout.Label(RegionLabel(selectedRegion).ToUpperInvariant());
            GUILayout.Label(GetRegionAssessment(selectedRegion));
            GUILayout.Space(6f);
            GUILayout.Label("HERIDAS DURABLES");

            woundScroll = GUILayout.BeginScrollView(woundScroll, GUILayout.Height(205f));
            if (wounds.Length == 0)
            {
                GUILayout.Label("No hay heridas registradas en esta región.");
            }
            else
            {
                for (int index = 0; index < wounds.Length; index++)
                {
                    ActorMedicalWoundState wound = wounds[index];
                    string marker = wound.woundId == selectedWoundId ? "> " : string.Empty;
                    string treatment = wound.treatmentState == WoundTreatmentState.Bandaged.ToString()
                        ? "vendada"
                        : "sin tratar";
                    if (GUILayout.Button(
                            $"{marker}{WoundLabel(wound)} · {ActorMedicalStateComponent.SeverityLabel(wound.severity)} · {treatment}",
                            GUILayout.Height(28f)))
                    {
                        selectedWoundId = wound.woundId;
                        feedback = null;
                    }
                }
            }
            GUILayout.EndScrollView();

            ActorMedicalWoundState selected = medicalState.GetWound(selectedWoundId);
            if (selected != null)
            {
                GUILayout.Label($"Sangrado: {BleedingLabel(ActorMedicalStateComponent.EffectiveBleedingRate(selected))}");
                GUILayout.Label($"Dolor: {PainLabel(selected.painContribution)}");
            }

            int treatmentQuantity = InventoryItemUseService.GetAvailableWoundTreatmentQuantity(itemOwnership);
            if (woundTreatment?.IsTreating == true)
            {
                GUILayout.Label($"Vendando {woundTreatment.WoundRegion}... {woundTreatment.Progress * 100f:0}%");
                GUILayout.Label($"Restante: {woundTreatment.RemainingSeconds:0.0}s");
            }
            else if (feedback == "Vendando..." && woundTreatment != null &&
                     woundTreatment.LastOutcome != ActorWoundTreatmentOutcome.InProgress)
                feedback = woundTreatment.LastMessage;
            GUI.enabled = selected != null && woundTreatment?.IsTreating != true;
            if (GUILayout.Button($"Aplicar vendaje (disponibles: {treatmentQuantity})", GUILayout.Height(30f)))
            {
                string failure = null;
                bool started = woundTreatment != null && woundTreatment.TryStart(
                    selectedWoundId,
                    ActorWoundTreatmentPurpose.Manual,
                    out failure);
                feedback = started ? "Vendando..." : failure ?? "No se pudo iniciar el vendaje.";
            }
            GUI.enabled = true;
            if (!string.IsNullOrWhiteSpace(feedback))
                GUILayout.Label(feedback);
            GUILayout.EndArea();
        }

        private void DrawDebugArea()
        {
            GUI.Box(new Rect(20f, 430f, 660f, 76f), "DEBUG — controles de prueba");
            if (GUI.Button(new Rect(35f, 458f, 245f, 30f), $"Crear laceración moderada: {RegionLabel(selectedRegion)}"))
            {
                bool applied = medicalState.ApplyWound(
                    selectedRegion,
                    WoundType.Laceration,
                    0.5f,
                    out string woundId,
                    out string failure);
                selectedWoundId = applied ? woundId : selectedWoundId;
                feedback = applied ? "Herida debug creada." : failure;
            }
            if (GUI.Button(new Rect(292f, 458f, 180f, 30f), "Daño sistémico debug"))
                ApplyDebugDamageToPlayer();
            GUI.Label(
                new Rect(484f, 454f, 180f, 42f),
                $"Reserva vital debug:\n{actorHealth.CurrentHealth:0.#}/{actorHealth.MaxHealth:0.#}");
        }

        private void ApplyDebugDamageToPlayer()
        {
            float beforeHealth = actorHealth.CurrentHealth;
            float amount = Mathf.Max(0f, debugDamageAmount);
            bool applied = actorHealth.ApplyDamage(amount);
            float afterHealth = actorHealth.CurrentHealth;
            string actorName = actorHealth.name;
            string message = applied
                ? $"{actorName} debug damage: {amount:0.#}. Health {beforeHealth:0.#}->{afterHealth:0.#}/{actorHealth.MaxHealth:0.#}."
                : $"{actorName} debug damage not applied. Health {afterHealth:0.#}/{actorHealth.MaxHealth:0.#}.";

            GameplayFeedbackLog.TryRecord(new GameplayFeedbackEntry(
                GameplayFeedbackEntryType.Info,
                message,
                actorId: actorName,
                actorDisplayName: actorName,
                debugOnly: true));
        }

        private void ResolveReferences()
        {
            if (actorHealth == null)
            {
                ActorNeedsComponent playerNeeds = FindAnyObjectByType<ActorNeedsComponent>();
                actorHealth = playerNeeds != null
                    ? playerNeeds.GetComponent<ActorHealthComponent>()
                    : FindAnyObjectByType<ActorHealthComponent>();
            }
            if (medicalState == null && actorHealth != null)
                medicalState = actorHealth.GetComponent<ActorMedicalStateComponent>();
            if (itemOwnership == null && actorHealth != null)
                itemOwnership = actorHealth.GetComponent<ActorItemOwnershipComponent>();
            if (inventorySessionController == null)
                inventorySessionController = FindAnyObjectByType<InventoryUISessionController>();
        }

        private void SelectRegion(BodyRegion region)
        {
            selectedRegion = region;
            ActorMedicalWoundState[] wounds = medicalState != null
                ? medicalState.GetWounds(region)
                : new ActorMedicalWoundState[0];
            selectedWoundId = wounds.Length > 0 ? wounds[0].woundId : null;
            feedback = null;
        }

        private static bool ContainsWound(ActorMedicalWoundState[] wounds, string woundId)
        {
            if (string.IsNullOrWhiteSpace(woundId))
                return false;
            for (int index = 0; index < wounds.Length; index++)
            {
                if (wounds[index].woundId == woundId)
                    return true;
            }
            return false;
        }

        private static string RegionLabel(BodyRegion region)
        {
            switch (region)
            {
                case BodyRegion.Head: return "Cabeza";
                case BodyRegion.Torso: return "Torso";
                case BodyRegion.LeftArm: return "Brazo izq.";
                case BodyRegion.RightArm: return "Brazo der.";
                case BodyRegion.LeftLeg: return "Pierna izq.";
                case BodyRegion.RightLeg: return "Pierna der.";
                default: return region.ToString();
            }
        }

        private static string WoundLabel(ActorMedicalWoundState wound)
        {
            if (wound == null)
                return "<NONE>";
            if (wound.woundType == WoundType.Laceration.ToString())
                return "Laceración";
            if (wound.woundType == WoundType.Puncture.ToString())
                return "Punción";
            if (wound.woundType == WoundType.Blunt.ToString())
                return "Contusión";
            return wound.woundType;
        }

        private static string BleedingLabel(float rate)
        {
            if (rate <= 0f) return "Sin sangrado";
            if (rate < 0.15f) return "Leve";
            if (rate < 0.35f) return "Moderado";
            return "Grave";
        }

        private static string PainLabel(float pain)
        {
            if (pain <= 0f) return "Sin dolor";
            if (pain < 0.3f) return "Leve";
            if (pain < 0.7f) return "Moderado";
            return "Intenso";
        }

        private static Vector2 ToGuiPosition(Vector2 mousePosition)
        {
            return new Vector2(mousePosition.x, Screen.height - mousePosition.y);
        }
    }
}
