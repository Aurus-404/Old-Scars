using System.Collections.Generic;
using OldScars.Core.Feedback;
using OldScars.Core.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OldScars.Core.Actors
{
    public sealed class ActorNeedsDebugPanel : MonoBehaviour
    {
        private const float PanelWidth = 220f;
        private const float PanelHeight = 170f;

        [SerializeField] private ActorNeedsComponent actorNeeds;
        [SerializeField] private WorldClock worldClock;
        [SerializeField] private InventoryUISessionController inventorySessionController;
        [SerializeField] private bool visible = true;

        public bool IsVisible => visible && (inventorySessionController == null || !inventorySessionController.IsOpen);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimePanel()
        {
            if (FindAnyObjectByType<ActorNeedsDebugPanel>() != null)
            {
                return;
            }

            ActorNeedsComponent actorNeeds = FindAnyObjectByType<ActorNeedsComponent>();
            if (actorNeeds == null)
            {
                return;
            }

            var panelObject = new GameObject("ActorNeedsDebugPanel_Runtime");
            ActorNeedsDebugPanel panel = panelObject.AddComponent<ActorNeedsDebugPanel>();
            panel.actorNeeds = actorNeeds;
            panel.visible = true;
        }

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
            ResolveInventorySessionController();
        }

        private void OnGUI()
        {
            if (!IsVisible)
            {
                return;
            }

            if (actorNeeds == null)
            {
                ResolveActorNeeds();
            }

            if (worldClock == null)
                ResolveWorldClock();

            GUILayout.BeginArea(GetPanelRect(), GUI.skin.box);
            GUILayout.Label("Needs (Debug)");

            GUILayout.Label(worldClock != null ? worldClock.DisplayTime : "World Clock: <NONE>");

            if (actorNeeds == null)
            {
                GUILayout.Label("No ActorNeeds component.");
                GUILayout.EndArea();
                return;
            }

            if (actorNeeds != null)
            {
                IReadOnlyList<ActorNeedState> states = actorNeeds.RuntimeStates;
                if (states == null || states.Count == 0)
                {
                    GUILayout.Label("No runtime needs.");
                }
                else
                {
                    for (int index = 0; index < states.Count; index++)
                    {
                        ActorNeedState state = states[index];
                        if (state == null || string.IsNullOrWhiteSpace(state.needId))
                        {
                            continue;
                        }

                        DrawNeed(state.needId, state.currentValue);
                    }
                }
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Rest 1h", GUILayout.Height(24f)))
                ApplyDebugRest(WorldClock.SecondsPerHour);
            if (GUILayout.Button("Sleep 8h", GUILayout.Height(24f)))
                ApplyDebugRest(WorldClock.SecondsPerHour * 8d);
            GUILayout.EndHorizontal();

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

        private void DrawNeed(string needId, float currentValue)
        {
            string displayName = actorNeeds.GetNeedDisplayName(needId);
            float maxValue = actorNeeds.GetNeedMaxValue(needId);
            float percent = maxValue > 0f ? Mathf.Clamp01(currentValue / maxValue) * 100f : 0f;
            GUILayout.Label($"{displayName}: {currentValue:0.#}/{maxValue:0.#} ({percent:0}%)");
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
        private const float WindowWidth = 300f;
        private const float WindowHeight = 180f;

        [SerializeField] private ActorHealthComponent actorHealth;
        [SerializeField] private InventoryUISessionController inventorySessionController;
        [SerializeField] private float debugDamageAmount = 25f;
        [SerializeField] private bool isOpen;

        private Rect windowRect = new Rect(252f, 16f, WindowWidth, WindowHeight);

        public bool IsOpen => isOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeWindow()
        {
            if (FindAnyObjectByType<ActorHealthDebugWindow>() != null)
                return;

            ActorNeedsComponent playerNeeds = FindAnyObjectByType<ActorNeedsComponent>();
            ActorHealthComponent health = playerNeeds != null
                ? playerNeeds.GetComponent<ActorHealthComponent>()
                : FindAnyObjectByType<ActorHealthComponent>();
            if (health == null)
                return;

            var windowObject = new GameObject("ActorHealthDebugWindow_Runtime");
            ActorHealthDebugWindow window = windowObject.AddComponent<ActorHealthDebugWindow>();
            window.actorHealth = health;
        }

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
            isOpen = !isOpen;
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
            if (actorHealth.MaxHealth > 0f && actorHealth.CurrentHealth / actorHealth.MaxHealth <= actorHealth.LowHealthThreshold)
                return "Critical";
            if (actorHealth.CurrentHealth < actorHealth.MaxHealth)
                return "Injured";
            return "Healthy";
        }

        private void DrawWindowContents(int windowId)
        {
            if (GUI.Button(new Rect(WindowWidth - 28f, 2f, 24f, 20f), "X"))
                Close();

            if (actorHealth == null)
            {
                GUILayout.Label("ActorHealthComponent: <NONE>");
            }
            else
            {
                GUILayout.Label("Estado general: " + GetQualitativeStatus());
                GUILayout.Label($"Health: {actorHealth.CurrentHealth:0.#}/{actorHealth.MaxHealth:0.#}");
                if (GUILayout.Button("Debug Damage Player", GUILayout.Height(24f)))
                    ApplyDebugDamageToPlayer();
            }

            GUI.DragWindow(new Rect(0f, 0f, WindowWidth - 32f, 24f));
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
            if (inventorySessionController == null)
                inventorySessionController = FindAnyObjectByType<InventoryUISessionController>();
        }

        private static Vector2 ToGuiPosition(Vector2 mousePosition)
        {
            return new Vector2(mousePosition.x, Screen.height - mousePosition.y);
        }
    }
}
