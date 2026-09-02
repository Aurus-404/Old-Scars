using System;
using System.Collections.Generic;
using OldScars.Core.Combat;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OldScars.Core.Actors
{
    /// <summary>
    /// Development-only readout for sandbox actors. It observes existing runtime authorities;
    /// it does not select gameplay targets, evaluate perception, or resolve shots.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SandboxNpcObservabilityPanel : MonoBehaviour
    {
        private const float ShotVisualLifetimeSeconds = 0.8f;
        private const int MaxTraceEntries = 12;
        private SandboxNpcController sandbox;
        private Camera gameplayCamera;
        private SandboxNpcMetadata selected;
        private bool visible;
        private bool showPerceptionVisual = true;
        private bool showShotVisual = true;
        private readonly List<string> trace = new List<string>(MaxTraceEntries);
        private int observedTransitionRevision = -1;
        private int observedAttackCount = -1;
        private ActorFunctionalState observedFunctionalState;
        private string observedThreat;
        private string observedRecognitionCandidate;

        public SandboxNpcMetadata Selected => selected;

        public void BindRuntime(SandboxNpcController sandboxController, Camera gameplayCamera)
        {
            sandbox = sandboxController;
            this.gameplayCamera = gameplayCamera;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f6Key.wasPressedThisFrame)
                visible = !visible;
            if (sandbox == null || selected == null)
                SelectFallback();
            if (selected != null)
                ObserveSelected();
        }

        private void OnGUI()
        {
            if (!visible)
                return;
            DrawWorldSelectionAndVisuals();
            GUILayout.BeginArea(new Rect(Screen.width - 470f, 18f, 450f, Screen.height - 36f), GUI.skin.box);
            GUILayout.Label("NPC OBSERVABILITY  [F6]", GUI.skin.label);
            DrawSelectionControls();
            if (selected == null)
                GUILayout.Label("No sandbox NPC exists. Spawn one from F3.");
            else
                DrawStatus();
            GUILayout.EndArea();
        }

        private void DrawSelectionControls()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Previous")) SelectRelative(-1);
            if (GUILayout.Button("Next")) SelectRelative(1);
            GUILayout.EndHorizontal();
            showPerceptionVisual = GUILayout.Toggle(showPerceptionVisual, "Show real perception / FOV visual");
            showShotVisual = GUILayout.Toggle(showShotVisual, "Show latest real shot visual");
            GUILayout.Label("Selection is retained while its sandbox actor exists.");
        }

        private void DrawStatus()
        {
            ActorRuntimeIdentity identity = selected.GetComponent<ActorRuntimeIdentity>();
            ActorProfileComponent profile = selected.GetComponent<ActorProfileComponent>();
            ActorAffiliationComponent affiliation = selected.GetComponent<ActorAffiliationComponent>();
            ActorHealthComponent health = selected.GetComponent<ActorHealthComponent>();
            ActorConditionComponent condition = selected.GetComponent<ActorConditionComponent>();
            ActorMedicalStateComponent medical = selected.GetComponent<ActorMedicalStateComponent>();
            HumanEncounterAIController ai = selected.GetComponent<HumanEncounterAIController>();
            ActorThreatAcquisitionController acquisition = selected.GetComponent<ActorThreatAcquisitionController>();
            ActorNavigationController navigation = selected.GetComponent<ActorNavigationController>();
            ActorBehaviorController behavior = selected.GetComponent<ActorBehaviorController>();
            ActorGazeController gaze = selected.GetComponent<ActorGazeController>();

            GUILayout.Label("IDENTITY", GUI.skin.box);
            GUILayout.Label("Instance: " + Text(identity?.ActorInstanceId));
            GUILayout.Label("Profile: " + Text(profile?.ActorProfileId) + " | Affiliation: " + Text(affiliation?.DebugDisplayName));
            GUILayout.Label("Lifecycle: " + Text(identity?.LifecycleState.ToString()));
            GUILayout.Label("HEALTH / CONDITION", GUI.skin.box);
            GUILayout.Label($"Vital: {Value(health?.VitalIntegrity)} / {Value(health?.MaxVitalIntegrity)} | Blood: {Value(condition?.BloodFraction)}");
            GUILayout.Label($"Trauma: {Value(condition?.TransientTrauma)} | Pain: {Value(medical?.TotalPain)} | Bleed/h: {Value(medical?.EffectiveBleedingRatePerGameHour)}");
            GUILayout.Label($"Functional: {Text(condition?.ObservableState)} | Active actions: {YesNo(condition == null || condition.CanPerformActiveActions)} | Wounds: {medical?.WoundCount.ToString() ?? "<NONE>"}");
            DrawWounds(medical);

            GUILayout.Label("AI / PERCEPTION", GUI.skin.box);
            GUILayout.Label($"State: {Text(ai?.State.ToString())} | Policy: {Text(ai?.Response.ToString())} | Threat: {Text(ai?.ThreatActorInstanceId)}");
            GUILayout.Label($"LastKnown: {(ai?.HasLastKnownPosition == true ? ai.LastKnownPosition.ToString("F2") : "<NONE>")} | Contact age: {ContactAge(ai)}");
            GUILayout.Label($"Recognition: {Value(acquisition?.HighestRecognitionProgress)} / {Text(acquisition?.HighestRecognitionTargetActorInstanceId)} | Candidate: {Text(acquisition?.HighestRecognitionTargetActorInstanceId)}");
            GUILayout.Label($"Attention: {Text(gaze?.Mode.ToString())} | Gaze yaw: {Value(gaze?.CurrentBodyRelativeYaw)} | Angular error: {Value(gaze?.AngularError)}");
            DrawPerception(ai, acquisition);

            GUILayout.Label("NAVIGATION / ROAMING", GUI.skin.box);
            GUILayout.Label($"Nav: {Text(navigation?.State.ToString())} | Destination: {(navigation?.HasDestination == true ? navigation.Destination.ToString("F2") : "<NONE>")}");
            GUILayout.Label($"Owner: {Text(behavior?.Owner.ToString())} | Ambient orders: {behavior?.AmbientAcceptedOrderCount.ToString() ?? "<NONE>"} | Ambient travel: {Value(behavior?.AmbientDistanceTravelled)}");
            GUILayout.Label($"Home: {(behavior != null ? behavior.HomeAnchor.ToString("F2") : "<NONE>")} | Radius: {Value(behavior?.MaximumRoamRadius)} | Owner revision: {behavior?.OwnerRevision.ToString() ?? "<NONE>"}");

            GUILayout.Label("COMBAT", GUI.skin.box);
            DrawCombat(ai);
            GUILayout.Label("SEMANTIC TRACE", GUI.skin.box);
            for (int index = trace.Count - 1; index >= 0; index--) GUILayout.Label(trace[index]);
        }

        private void DrawWounds(ActorMedicalStateComponent medical)
        {
            if (medical == null || medical.WoundCount == 0) { GUILayout.Label("Wounds: none"); return; }
            foreach (BodyRegion region in ActorMedicalStateComponent.HumanRegions)
            {
                ActorMedicalWoundState[] wounds = medical.GetWounds(region);
                for (int index = 0; index < wounds.Length; index++)
                {
                    ActorMedicalWoundState wound = wounds[index];
                    GUILayout.Label($"- {region}: {wound.woundType} {wound.severity:0.##}, {wound.treatmentState}");
                }
            }
        }

        private static void DrawPerception(HumanEncounterAIController ai, ActorThreatAcquisitionController acquisition)
        {
            ActorVisualPerceptionResult result = ai != null && ai.Threat != null ? ai.LastPerception : acquisition != null ? acquisition.LastAcquisitionPerception : default;
            GUILayout.Label($"Perception: {result.Perceived} / {result.Reason} | distance {result.Distance:0.##} | FOV angle {result.HorizontalAngle:0.##}");
            GUILayout.Label($"Query origin: {result.ObserverOrigin:F2} | target point: {result.ObservedPosition:F2}");
            GUILayout.Label("Target collider: " + Text(result.TargetCollider != null ? result.TargetCollider.name : null) + " | blocker: " + Text(result.Blocker != null ? result.Blocker.name : null));
        }

        private void DrawCombat(HumanEncounterAIController ai)
        {
            ActorItemOwnershipComponent ownership = selected.GetComponent<ActorItemOwnershipComponent>();
            if (!WeaponCombatService.TryGetEquippedWeapon(ownership, out ItemInstance weapon, out _, out FirearmProfileDefinition firearm, out WeaponProfileDefinition melee))
            {
                GUILayout.Label("Weapon: <NONE>");
                return;
            }
            int reserve = firearm != null ? WeaponCombatService.GetCompatibleAmmoQuantity(ownership, weapon) : 0;
            string range = firearm != null ? firearm.range.ToString("0.##") : melee.melee_range.ToString("0.##");
            GUILayout.Label($"Weapon: {weapon.DefinitionId} | Ammo: {weapon.LoadedRounds} loaded / {reserve} reserve | Range: {range}");
            GUILayout.Label($"Distance: {ai?.CurrentTargetDistance:0.##} | Focus: {ai?.CurrentFocus:0.##} | Spread: {ai?.CurrentSpreadDegrees:0.##} | Reload: {YesNo(ai?.IsReloadPending == true)} | Attacks: {ai?.AttackCount}");
            if (ai != null && ai.LastShotTime > Time.timeAsDouble - ShotVisualLifetimeSeconds)
            {
                PhysicalShotResolution shot = ai.LastCombatResult.PhysicalShot;
                GUILayout.Label($"Latest real shot: {shot.Termination} | intent: {Text(ai.LastShotIntentTargetActorInstanceId)} | impact: {Text(shot.HitCollider != null ? shot.HitCollider.name : null)} | armor: {ai.LastCombatResult.Combat.Armor.ArmorFound}");
            }
        }

        private void DrawWorldSelectionAndVisuals()
        {
            if (selected == null || gameplayCamera == null) return;
            Vector3 screen = gameplayCamera.WorldToScreenPoint(selected.transform.position + Vector3.up * 2.2f);
            if (screen.z > 0f) GUI.Box(new Rect(screen.x - 62f, Screen.height - screen.y - 16f, 124f, 24f), "SELECTED NPC");
            HumanEncounterAIController ai = selected.GetComponent<HumanEncounterAIController>();
            ActorThreatAcquisitionController acquisition = selected.GetComponent<ActorThreatAcquisitionController>();
            ActorVisualPerceptionResult perception = ai != null && ai.Threat != null ? ai.LastPerception : acquisition != null ? acquisition.LastAcquisitionPerception : default;
            if (showPerceptionVisual)
            {
                DrawWorldLine(perception.ObserverOrigin, perception.Blocker != null ? perception.Blocker.bounds.center : perception.ObservedPosition,
                    perception.Perceived ? Color.green : Color.yellow, "LOS " + perception.Reason);
                ActorVisualPerceptionService sight = selected.GetComponent<ActorVisualPerceptionService>();
                if (sight != null && perception.ObserverOrigin != default)
                {
                    float halfFov = sight.HorizontalFovDegrees * .5f;
                    Vector3 perceptionForward = sight.CurrentPerceptionForward;
                    Vector3 left = Quaternion.AngleAxis(-halfFov, Vector3.up) * perceptionForward;
                    Vector3 right = Quaternion.AngleAxis(halfFov, Vector3.up) * perceptionForward;
                    DrawWorldLine(perception.ObserverOrigin, perception.ObserverOrigin + left * sight.VisualRange, Color.gray, "FOV");
                    DrawWorldLine(perception.ObserverOrigin, perception.ObserverOrigin + right * sight.VisualRange, Color.gray, "FOV");
                }
                ActorGazeController gaze = selected.GetComponent<ActorGazeController>();
                if (gaze != null && sight != null)
                {
                    Vector3 gazeOrigin = selected.transform.position + Vector3.up * sight.EyeHeight;
                    DrawWorldLine(gazeOrigin, gazeOrigin + gaze.CurrentGazeDirection * 5f, Color.magenta,
                        "GAZE " + gaze.Mode);
                }
            }
            if (showShotVisual && ai != null && ai.LastShotTime > Time.timeAsDouble - ShotVisualLifetimeSeconds)
            {
                PhysicalShotResolution shot = ai.LastCombatResult.PhysicalShot;
                Color color = shot.Termination == PhysicalShotTermination.Miss ? Color.cyan :
                    ai.LastCombatResult.Combat.Armor.ArmorFound ? new Color(1f, .45f, 0f) : Color.red;
                DrawWorldLine(ai.LastShotOrigin, shot.EndPoint, color, "SHOT " + shot.Termination);
            }
        }

        private void DrawWorldLine(Vector3 origin, Vector3 endpoint, Color color, string label)
        {
            if (origin == default || endpoint == default || gameplayCamera == null) return;
            Vector3 a = gameplayCamera.WorldToScreenPoint(origin); Vector3 b = gameplayCamera.WorldToScreenPoint(endpoint);
            if (a.z <= 0f || b.z <= 0f) return;
            Vector2 start = new Vector2(a.x, Screen.height - a.y), end = new Vector2(b.x, Screen.height - b.y);
            Color previous = GUI.color; GUI.color = color;
            DrawLine(start, end, 2f); GUI.Label(new Rect(end.x + 4f, end.y + 4f, 190f, 20f), label); GUI.color = previous;
        }

        private static void DrawLine(Vector2 start, Vector2 end, float width)
        {
            Vector2 delta = end - start; float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            Matrix4x4 matrix = GUI.matrix; GUIUtility.RotateAroundPivot(angle, start);
            GUI.DrawTexture(new Rect(start.x, start.y - width * .5f, delta.magnitude, width), Texture2D.whiteTexture);
            GUI.matrix = matrix;
        }

        private void ObserveSelected()
        {
            HumanEncounterAIController ai = selected.GetComponent<HumanEncounterAIController>();
            ActorConditionComponent condition = selected.GetComponent<ActorConditionComponent>();
            ActorThreatAcquisitionController acquisition = selected.GetComponent<ActorThreatAcquisitionController>();
            if (ai != null && ai.TransitionRevision != observedTransitionRevision) { observedTransitionRevision = ai.TransitionRevision; AddTrace("AI -> " + ai.State); }
            if (ai != null && ai.AttackCount != observedAttackCount)
            {
                observedAttackCount = ai.AttackCount;
                CombatResolutionResult combat = ai.LastCombatResult.Combat;
                AddTrace("Fire -> " + ai.LastCombatResult.PhysicalShot.Termination + " / " + ai.LastCombatResult.Code);
                if (combat.Armor.ArmorFound)
                    AddTrace("Armor " + combat.Armor.Outcome);
                if (combat.WoundApplied)
                    AddTrace("Wound " + combat.FinalWoundType + " vital " + combat.VitalIntegrityBefore.ToString("0.##") + "->" + combat.VitalIntegrityAfter.ToString("0.##"));
            }
            if (condition != null && condition.FunctionalState != observedFunctionalState) { observedFunctionalState = condition.FunctionalState; AddTrace("Condition -> " + condition.ObservableState); }
            string threat = ai?.ThreatActorInstanceId;
            if (threat != observedThreat) { observedThreat = threat; AddTrace("Threat -> " + Text(threat)); }
            string candidate = acquisition?.HighestRecognitionTargetActorInstanceId;
            if (candidate != observedRecognitionCandidate) { observedRecognitionCandidate = candidate; AddTrace("Recognition candidate -> " + Text(candidate)); }
        }

        private void SelectFallback() { if (sandbox?.LastSpawn != null) Select(sandbox.LastSpawn); }
        private void SelectRelative(int delta)
        {
            IReadOnlyList<SandboxNpcMetadata> actors = sandbox?.Spawned;
            if (actors == null || actors.Count == 0) return;
            int current = selected == null ? -1 : IndexOf(actors, selected);
            for (int step = 1; step <= actors.Count; step++)
            {
                SandboxNpcMetadata candidate = actors[(current + delta * step + actors.Count * 2) % actors.Count];
                if (candidate != null) { Select(candidate); return; }
            }
        }
        private static int IndexOf(IReadOnlyList<SandboxNpcMetadata> actors, SandboxNpcMetadata value) { for (int i = 0; i < actors.Count; i++) if (actors[i] == value) return i; return -1; }
        private void Select(SandboxNpcMetadata value)
        {
            if (selected == value) return; selected = value; trace.Clear(); observedTransitionRevision = -1; observedAttackCount = -1; observedThreat = null; observedRecognitionCandidate = null; ObserveSelected(); AddTrace("Selected " + Text(value?.GetComponent<ActorRuntimeIdentity>()?.ActorInstanceId));
        }
        private void AddTrace(string value) { if (string.IsNullOrWhiteSpace(value)) return; trace.Add(Time.time.ToString("0.0") + " " + value); if (trace.Count > MaxTraceEntries) trace.RemoveAt(0); }
        private static string Text(string value) => string.IsNullOrWhiteSpace(value) ? "<NONE>" : value;
        private static string Value(float? value) => value.HasValue ? value.Value.ToString("0.###") : "<NONE>";
        private static string ContactAge(HumanEncounterAIController ai) => ai != null && !double.IsNaN(ai.LastSeenTime)
            ? Math.Max(0d, Time.timeAsDouble - ai.LastSeenTime).ToString("0.00") + "s" : "<NONE>";
        private static string YesNo(bool value) => value ? "Yes" : "No";
    }
}
