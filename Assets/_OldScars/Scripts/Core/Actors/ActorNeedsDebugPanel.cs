using System.Collections.Generic;
using OldScars.Core.Feedback;
using OldScars.Core.Items;
using UnityEngine;

namespace OldScars.Core.Actors
{
    public sealed class ActorNeedsDebugPanel : MonoBehaviour
    {
        private const float PanelWidth = 220f;
        private const float PanelHeight = 230f;

        [SerializeField] private ActorNeedsComponent actorNeeds;
        [SerializeField] private ActorHealthComponent actorHealth;
        [SerializeField] private WorldClock worldClock;
        [SerializeField] private InventoryUISessionController inventorySessionController;
        [SerializeField] private float debugDamageAmount = 25f;
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
            panel.actorHealth = actorNeeds.GetComponent<ActorHealthComponent>();
            panel.visible = true;
        }

        private void Awake()
        {
            ResolveActorNeeds();
            ResolveActorHealth();
            ResolveWorldClock();
            ResolveInventorySessionController();
        }

        private void OnEnable()
        {
            ResolveActorNeeds();
            ResolveActorHealth();
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

            if (actorHealth == null)
            {
                ResolveActorHealth();
            }

            if (worldClock == null)
                ResolveWorldClock();

            GUILayout.BeginArea(GetPanelRect(), GUI.skin.box);
            GUILayout.Label("Needs (Debug)");

            GUILayout.Label(worldClock != null ? worldClock.DisplayTime : "World Clock: <NONE>");

            if (actorNeeds == null && actorHealth == null)
            {
                GUILayout.Label("No ActorNeeds/Health component.");
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

            DrawHealth();

            if (actorHealth != null && GUILayout.Button("Debug Damage Player", GUILayout.Height(24f)))
            {
                ApplyDebugDamageToPlayer();
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

        private void DrawHealth()
        {
            if (actorHealth == null)
                return;

            float maxHealth = actorHealth.MaxHealth;
            float percent = maxHealth > 0f ? Mathf.Clamp01(actorHealth.CurrentHealth / maxHealth) * 100f : 0f;
            GUILayout.Label($"Health: {actorHealth.CurrentHealth:0.#}/{maxHealth:0.#} ({percent:0}%)");
        }

        private void ApplyDebugDamageToPlayer()
        {
            if (actorHealth == null)
                return;

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

        private void ResolveActorHealth()
        {
            if (actorHealth != null)
            {
                return;
            }

            if (actorNeeds != null)
                actorHealth = actorNeeds.GetComponent<ActorHealthComponent>();

            if (actorHealth == null)
                actorHealth = FindAnyObjectByType<ActorHealthComponent>();
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
}
