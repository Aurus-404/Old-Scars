using UnityEngine;

namespace OldScars.Core.Actors
{
    /// <summary>
    /// Development-only companion control for Runtime Debug Tools. It never owns health state;
    /// it only asks the existing Health / Medical / Condition authorities to restore the living
    /// Player to a healthy baseline for QA.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerMedicalResetDebugControl : MonoBehaviour
    {
        private const float PanelX = 16f;
        private const float PanelY = 522f;
        private const float PanelWidth = 340f;
        private const float PanelHeight = 72f;

        private ActorNeedsDebugPanel debugPanel;
        private ActorRuntimeIdentity playerIdentity;
        private string feedback;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeControl()
        {
            if (!IsDevelopmentBuild || Object.FindAnyObjectByType<PlayerMedicalResetDebugControl>() != null)
                return;

            var host = new GameObject("PlayerMedicalResetDebugControl")
            {
                hideFlags = HideFlags.DontSave
            };
            Object.DontDestroyOnLoad(host);
            host.AddComponent<PlayerMedicalResetDebugControl>();
        }

        private void OnGUI()
        {
            if (!IsDevelopmentBuild)
                return;

            ResolveReferences();
            if (debugPanel == null || !debugPanel.IsVisible)
                return;

            GUILayout.BeginArea(new Rect(PanelX, PanelY, PanelWidth, PanelHeight), GUI.skin.box);
            GUILayout.Label("PLAYER MEDICAL DEBUG");

            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && TryResolveAuthorities(
                out _, out _, out _, out string availabilityFailure);
            if (GUILayout.Button("Heal All / Reset Medical State", GUILayout.Height(24f)))
            {
                if (TryResetPlayerMedicalState(out string failure))
                    feedback = "Medical state restored to healthy baseline.";
                else
                    feedback = "Medical reset failed: " + failure;
            }
            GUI.enabled = previousEnabled;

            if (!string.IsNullOrWhiteSpace(feedback))
                GUILayout.Label(feedback);
            else if (!string.IsNullOrWhiteSpace(availabilityFailure))
                GUILayout.Label(availabilityFailure);

            GUILayout.EndArea();
        }

        public bool TryResetPlayerMedicalState(out string failure)
        {
            if (!TryResolveAuthorities(
                    out ActorHealthComponent health,
                    out ActorMedicalStateComponent medical,
                    out ActorConditionComponent condition,
                    out failure))
                return false;

            if (health.IsDead || playerIdentity.LifecycleState == ActorLifecycleState.Dead)
            {
                failure = "Dead Player is not resurrected by Heal All; restart/respawn remains a separate lifecycle action.";
                return false;
            }
            if (!condition.IsConfigured)
            {
                failure = "Player condition authority is not configured.";
                return false;
            }

            playerIdentity.GetComponent<ActorWoundTreatmentController>()?
                .Cancel("Debug Heal All / Reset Medical State");

            if (!medical.TryApplyPersistenceState(ActorMedicalStateComponent.HealthyBaseline(), out failure))
                return false;
            if (!condition.TryApplyPersistenceState(ActorConditionComponent.HealthyBaseline(), out failure))
                return false;

            if (health.CurrentHealth < health.MaxHealth)
                health.Heal(health.MaxHealth);

            bool restored = Mathf.Approximately(health.CurrentHealth, health.MaxHealth) &&
                            medical.WoundCount == 0 &&
                            Mathf.Approximately(medical.EffectiveBleedingRatePerGameHour, 0f) &&
                            Mathf.Approximately(medical.TotalPain, 0f) &&
                            Mathf.Approximately(condition.BloodFraction, 1f) &&
                            Mathf.Approximately(condition.TransientTrauma, 0f) &&
                            condition.FunctionalState == ActorFunctionalState.Conscious;
            if (!restored)
            {
                failure = "Existing medical authorities did not converge to the healthy baseline.";
                return false;
            }

            Debug.Log(
                "[DEBUG][PLAYER_MEDICAL_RESET]" +
                $"\n  Actor: {playerIdentity.ActorInstanceId}" +
                $"\n  Vital: {health.CurrentHealth:0.###}/{health.MaxHealth:0.###}" +
                $"\n  Blood: {condition.BloodFraction:0.###}" +
                $"\n  Wounds: {medical.WoundCount}" +
                $"\n  State: {condition.FunctionalState}");
            failure = null;
            return true;
        }

        private bool TryResolveAuthorities(
            out ActorHealthComponent health,
            out ActorMedicalStateComponent medical,
            out ActorConditionComponent condition,
            out string failure)
        {
            ResolveReferences();
            health = playerIdentity != null ? playerIdentity.GetComponent<ActorHealthComponent>() : null;
            medical = playerIdentity != null ? playerIdentity.GetComponent<ActorMedicalStateComponent>() : null;
            condition = playerIdentity != null ? playerIdentity.GetComponent<ActorConditionComponent>() : null;

            if (playerIdentity == null)
            {
                failure = "Player identity: <NONE>";
                return false;
            }
            if (health == null || medical == null || condition == null)
            {
                failure = "Player Health / Medical / Condition authority is unavailable.";
                return false;
            }

            failure = null;
            return true;
        }

        private void ResolveReferences()
        {
            if (debugPanel == null)
                debugPanel = Object.FindAnyObjectByType<ActorNeedsDebugPanel>();

            if (playerIdentity == null)
            {
                PlayerMovementController movement = Object.FindAnyObjectByType<PlayerMovementController>();
                if (movement != null)
                    playerIdentity = movement.GetComponent<ActorRuntimeIdentity>();
            }
        }

        private static bool IsDevelopmentBuild => Application.isEditor || Debug.isDebugBuild;
    }
}
