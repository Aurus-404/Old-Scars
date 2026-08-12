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
        private const float WindowWidth = 700f;
        private const float WindowHeight = 530f;

        [SerializeField] private ActorHealthComponent actorHealth;
        [SerializeField] private InventoryUISessionController inventorySessionController;
        [SerializeField] private float debugDamageAmount = 25f;
        [SerializeField] private bool isOpen;

        private Rect windowRect = new Rect(252f, 16f, WindowWidth, WindowHeight);
        private ActorMedicalStateComponent medicalState;
        private ActorItemOwnershipComponent itemOwnership;
        private BodyRegion selectedRegion = BodyRegion.Torso;
        private string selectedWoundId;
        private string feedback;
        private Vector2 woundScroll;

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
            selectedWoundId = null;
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
            GUI.enabled = selected != null;
            if (GUILayout.Button($"Aplicar vendaje (disponibles: {treatmentQuantity})", GUILayout.Height(30f)))
            {
                InventoryItemUseResult result = InventoryItemUseService.TryApplyWoundTreatment(
                    itemOwnership,
                    medicalState,
                    selectedWoundId);
                feedback = result.Message;
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
