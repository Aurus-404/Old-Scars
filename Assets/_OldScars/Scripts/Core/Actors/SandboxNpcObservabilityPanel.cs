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
        private bool showLastQueryVisual = true;
        private bool showShotVisual = true;
        private readonly List<string> trace = new List<string>(MaxTraceEntries);
        private int observedTransitionRevision = -1;
        private int observedAttackCount = -1;
        private ActorFunctionalState observedFunctionalState;
        private string observedThreat;
        private string observedRecognitionCandidate;

        public SandboxNpcMetadata Selected => selected;
        public bool IsVisible => visible;
        public int CurrentWorldVisualActorCount { get; private set; }
        public int LastWorldPerceptionEvidenceCount { get; private set; }
        public int CurrentWorldDrawnActorCount { get; private set; }

        public void BindRuntime(SandboxNpcController sandboxController, Camera gameplayCamera)
        {
            sandbox = sandboxController;
            this.gameplayCamera = gameplayCamera;
        }

        /// <summary>
        /// Development diagnostic seam. This only changes presentation visibility; it never changes AI state.
        /// </summary>
        public void SetVisibleForDiagnostics(bool value)
        {
            visible = value;
        }

        /// <summary>Historical query segment, not the blocker hit point (which is not retained).</summary>
        public static bool TryGetLastQuerySegment(ActorVisualPerceptionResult result,
            out Vector3 origin, out Vector3 endpoint, out string label)
        {
            origin = result.ObserverOrigin;
            endpoint = result.ObservedPosition;
            bool hasEvidence = HasLastPerceptionEvidence(result);
            label = hasEvidence ? "LAST QUERY " + result.Reason : "LAST: No evidence";
            return hasEvidence;
        }

        /// <summary>
        /// Returns the same live eye origin used by the current FOV and gaze presentation.
        /// Historical perception query origins are deliberately not used here.
        /// </summary>
        public bool TryGetCurrentVisualOrigin(SandboxNpcMetadata actor, out Vector3 origin)
        {
            origin = default;
            if (!CanDrawCurrentPerceptionVisuals(actor, out ActorVisualPerceptionService sight, out _))
                return false;
            origin = actor.transform.position + Vector3.up * sight.EyeHeight;
            return true;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f6Key.wasPressedThisFrame)
                visible = !visible;
            if (sandbox == null || selected == null)
                SelectFallback();
            if (selected != null)
                ObserveSelected();
            if (visible)
                RefreshWorldVisualState();
            else
            {
                CurrentWorldVisualActorCount = 0;
                LastWorldPerceptionEvidenceCount = 0;
            }
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
            showPerceptionVisual = GUILayout.Toggle(showPerceptionVisual, "Show CURRENT Gaze / FOV");
            showLastQueryVisual = GUILayout.Toggle(showLastQueryVisual, "Show LAST query segments (historical)");
            GUILayout.Label("LAST = saved query endpoints; NOT a historical blocker hit.");
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

            GUILayout.Label("AI / LAST PERCEPTION EVIDENCE", GUI.skin.box);
            GUILayout.Label($"State: {Text(ai?.State.ToString())} | Policy: {Text(ai?.Response.ToString())} | Threat: {Text(ai?.ThreatActorInstanceId)}");
            GUILayout.Label($"LastKnown: {(ai?.HasLastKnownPosition == true ? ai.LastKnownPosition.ToString("F2") : "<NONE>")} | Contact age: {ContactAge(ai)}");
            GUILayout.Label($"Search: {Text(ai?.LastSearchOutcome.ToString())} | Anchor: {(ai?.HasSearchAnchor == true ? ai.SearchAnchor.ToString("F2") : "<NONE>")} | Inspect remaining: {(ai?.IsSearchInspecting == true ? ai.SearchInspectionRemainingSeconds.ToString("0.00") + "s" : "<NONE>")}");
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
            ActorVisualPerceptionResult result = ResolveLastPerception(ai, acquisition);
            if (!TryGetLastQuerySegment(result, out _, out _, out string lastLabel))
            {
                GUILayout.Label(lastLabel);
                return;
            }
            GUILayout.Label($"LAST Perception: {result.Perceived} / {result.Reason} | distance {result.Distance:0.##} | FOV angle {result.HorizontalAngle:0.##}");
            GUILayout.Label($"LAST query origin: {result.ObserverOrigin:F2} | LAST target point: {result.ObservedPosition:F2}");
            GUILayout.Label("LAST target collider: " + Text(result.TargetCollider != null ? result.TargetCollider.name : null) + " | LAST blocker: " + Text(result.Blocker != null ? result.Blocker.name : null));
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
            CurrentWorldDrawnActorCount = 0;
            if (gameplayCamera == null) return;
            if (selected != null)
            {
                Vector3 screen = gameplayCamera.WorldToScreenPoint(selected.transform.position + Vector3.up * 2.2f);
                if (screen.z > 0f) GUI.Box(new Rect(screen.x - 62f, Screen.height - screen.y - 16f, 124f, 24f), "SELECTED NPC");
            }
            if (showPerceptionVisual || showLastQueryVisual)
            {
                IReadOnlyList<SandboxNpcMetadata> actors = sandbox?.Spawned;
                if (actors != null)
                    for (int index = 0; index < actors.Count; index++)
                        DrawActorWorldPerceptionVisuals(actors[index]);
            }

            if (selected == null) return;
            HumanEncounterAIController ai = selected.GetComponent<HumanEncounterAIController>();
            if (showShotVisual && ai != null && ai.LastShotTime > Time.timeAsDouble - ShotVisualLifetimeSeconds)
            {
                PhysicalShotResolution shot = ai.LastCombatResult.PhysicalShot;
                Color color = shot.Termination == PhysicalShotTermination.Miss ? Color.cyan :
                    ai.LastCombatResult.Combat.Armor.ArmorFound ? new Color(1f, .45f, 0f) : Color.red;
                DrawWorldLine(ai.LastShotOrigin, shot.EndPoint, color, "SHOT " + shot.Termination);
            }
        }

        private void DrawActorWorldPerceptionVisuals(SandboxNpcMetadata actor)
        {
            if (actor == null) return;
            HumanEncounterAIController ai = actor.GetComponent<HumanEncounterAIController>();
            ActorThreatAcquisitionController acquisition = actor.GetComponent<ActorThreatAcquisitionController>();
            ActorVisualPerceptionResult lastPerception = ResolveLastPerception(ai, acquisition);
            string actorLabel = DescribeWorldActor(actor);

            // LastPerception documents a previous production query only. It must never define the current FOV origin.
            if (showLastQueryVisual && TryGetLastQuerySegment(lastPerception, out Vector3 lastOrigin, out Vector3 lastEndpoint, out string lastLabel))
            {
                Color lastColor = lastPerception.Perceived ? Color.green : Color.yellow;
                lastColor.a = 0.35f;
                DrawWorldLine(lastOrigin, lastEndpoint,
                    lastColor, lastLabel + " " + actorLabel);
            }

            if (!showPerceptionVisual || !CanDrawCurrentPerceptionVisuals(actor, out ActorVisualPerceptionService sight, out ActorGazeController gaze) ||
                !TryGetCurrentVisualOrigin(actor, out Vector3 currentEye))
                return;

            float halfFov = sight.HorizontalFovDegrees * .5f;
            Vector3 perceptionForward = sight.CurrentPerceptionForward;
            Vector3 left = Quaternion.AngleAxis(-halfFov, Vector3.up) * perceptionForward;
            Vector3 right = Quaternion.AngleAxis(halfFov, Vector3.up) * perceptionForward;
            bool drawn = DrawWorldLine(currentEye, currentEye + left * sight.VisualRange, Color.gray, "CURRENT FOV " + actorLabel);
            drawn |= DrawWorldLine(currentEye, currentEye + right * sight.VisualRange, Color.gray, "CURRENT FOV " + actorLabel);
            if (gaze != null)
                drawn |= DrawWorldLine(currentEye, currentEye + gaze.CurrentGazeDirection * 5f, Color.magenta,
                    "CURRENT GAZE " + actorLabel + " " + gaze.Mode);
            if (drawn) CurrentWorldDrawnActorCount++;
        }

        private void RefreshWorldVisualState()
        {
            CurrentWorldVisualActorCount = 0;
            LastWorldPerceptionEvidenceCount = 0;
            IReadOnlyList<SandboxNpcMetadata> actors = sandbox?.Spawned;
            if (actors == null) return;
            for (int index = 0; index < actors.Count; index++)
            {
                SandboxNpcMetadata actor = actors[index];
                if (actor == null) continue;
                HumanEncounterAIController ai = actor.GetComponent<HumanEncounterAIController>();
                ActorThreatAcquisitionController acquisition = actor.GetComponent<ActorThreatAcquisitionController>();
                if (HasLastPerceptionEvidence(ResolveLastPerception(ai, acquisition)))
                    LastWorldPerceptionEvidenceCount++;
                if (CanDrawCurrentPerceptionVisuals(actor, out _, out _))
                    CurrentWorldVisualActorCount++;
            }
        }

        private static bool CanDrawCurrentPerceptionVisuals(
            SandboxNpcMetadata actor,
            out ActorVisualPerceptionService sight,
            out ActorGazeController gaze)
        {
            sight = null;
            gaze = null;
            if (actor == null) return false;
            ActorRuntimeIdentity identity = actor.GetComponent<ActorRuntimeIdentity>();
            ActorConditionComponent condition = actor.GetComponent<ActorConditionComponent>();
            ActorBehaviorController behavior = actor.GetComponent<ActorBehaviorController>();
            if (identity == null || !identity.IsRegistered || identity.LifecycleState != ActorLifecycleState.Alive ||
                (condition != null && !condition.CanPerformActiveActions) ||
                (behavior != null && behavior.Owner == ActorBehaviorOwner.Inactive))
                return false;
            sight = actor.GetComponent<ActorVisualPerceptionService>();
            if (sight == null || !sight.IsConfigured)
                return false;
            gaze = actor.GetComponent<ActorGazeController>();
            return gaze == null || gaze.Mode != ActorAttentionMode.Inactive;
        }

        private static ActorVisualPerceptionResult ResolveLastPerception(
            HumanEncounterAIController ai,
            ActorThreatAcquisitionController acquisition) =>
            ai != null && ai.Threat != null ? ai.LastPerception : acquisition != null ? acquisition.LastAcquisitionPerception : default;

        private static bool HasLastPerceptionEvidence(ActorVisualPerceptionResult result) =>
            !string.IsNullOrWhiteSpace(result.ObserverId) && !string.IsNullOrWhiteSpace(result.TargetId);

        private static string DescribeWorldActor(SandboxNpcMetadata actor)
        {
            ActorRuntimeIdentity identity = actor.GetComponent<ActorRuntimeIdentity>();
            ActorAffiliationComponent affiliation = actor.GetComponent<ActorAffiliationComponent>();
            ActorConditionComponent condition = actor.GetComponent<ActorConditionComponent>();
            string id = identity?.ActorInstanceId;
            string shortId = string.IsNullOrEmpty(id) ? "<NONE>" : id.Length <= 8 ? id : id.Substring(id.Length - 8);
            return Text(affiliation?.DebugDisplayName) + "#" + shortId;
        }

        private bool DrawWorldLine(Vector3 origin, Vector3 endpoint, Color color, string label)
        {
            if (origin == default || endpoint == default || gameplayCamera == null) return false;
            Vector3 a = gameplayCamera.WorldToScreenPoint(origin); Vector3 b = gameplayCamera.WorldToScreenPoint(endpoint);
            if (a.z <= 0f || b.z <= 0f) return false;
            Vector2 start = new Vector2(a.x, Screen.height - a.y), end = new Vector2(b.x, Screen.height - b.y);
            Color previous = GUI.color; GUI.color = color;
            DrawLine(start, end, 2f);
            Vector2 labelSize = GUI.skin.label.CalcSize(new GUIContent(label));
            GUI.Label(new Rect(end.x + 4f, end.y + 4f, labelSize.x, labelSize.y), label);
            GUI.color = previous;
            return true;
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
