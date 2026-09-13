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
        private bool showCompactActorOverlay = true;
        private bool showPerceptionVisual = true;
        private bool showLastQueryVisual = true;
        private bool showCurrentAimVisual = true;
        private bool showShotVisual = true;
        private Vector2 inspectorScroll;
        private readonly Dictionary<HumanEncounterAIController, LatestShotEvidence> latestShotEvidenceByActor =
            new Dictionary<HumanEncounterAIController, LatestShotEvidence>();
        private readonly List<string> trace = new List<string>(MaxTraceEntries);
        private int observedTransitionRevision = -1;
        private int observedAttackCount = -1;
        private double observedLastShotTime = double.NegativeInfinity;
        private ActorFunctionalState observedFunctionalState;
        private bool hasObservedFunctionalState;
        private ActorLifecycleState? observedLifecycleState;
        private string observedThreat;
        private string observedRecognitionCandidate;

        public SandboxNpcMetadata Selected => selected;
        public bool IsVisible => visible;
        public int CurrentWorldVisualActorCount { get; private set; }
        public int LastWorldPerceptionEvidenceCount { get; private set; }
        public int CurrentWorldDrawnActorCount { get; private set; }
        public int CurrentWorldOverlayActorCount { get; private set; }
        public int SemanticTraceCount => trace.Count;
        public bool SemanticTraceContains(string token)
        {
            if (string.IsNullOrEmpty(token))
                return false;
            for (int index = 0; index < trace.Count; index++)
                if (trace[index].IndexOf(token, StringComparison.Ordinal) >= 0)
                    return true;
            return false;
        }

        public readonly struct CurrentTargetingEvidence
        {
            public CurrentTargetingEvidence(
                ActorRuntimeIdentity threat,
                HumanEncounterAIState state,
                HumanEncounterResponse response,
                float distance,
                float focus,
                float effectiveSpread,
                int attackCount,
                bool hasAimPoint,
                Vector3 aimPoint,
                bool usesPrimaryAimPoint)
            {
                Threat = threat;
                State = state;
                Response = response;
                Distance = distance;
                Focus = focus;
                EffectiveSpread = effectiveSpread;
                AttackCount = attackCount;
                HasAimPoint = hasAimPoint;
                AimPoint = aimPoint;
                UsesPrimaryAimPoint = usesPrimaryAimPoint;
            }

            public ActorRuntimeIdentity Threat { get; }
            public string ThreatActorInstanceId => Threat != null ? Threat.ActorInstanceId : null;
            public HumanEncounterAIState State { get; }
            public HumanEncounterResponse Response { get; }
            public float Distance { get; }
            public float Focus { get; }
            public float EffectiveSpread { get; }
            public int AttackCount { get; }
            public bool HasAimPoint { get; }
            public Vector3 AimPoint { get; }
            public bool UsesPrimaryAimPoint { get; }
        }

        public readonly struct LatestShotEvidence
        {
            public LatestShotEvidence(
                int sequence,
                double shotTime,
                string intendedTargetActorInstanceId,
                Vector3 origin,
                Vector3 direction,
                WeaponCombatResult result,
                string classification)
            {
                Sequence = sequence;
                ShotTime = shotTime;
                IntendedTargetActorInstanceId = intendedTargetActorInstanceId;
                Origin = origin;
                Direction = direction;
                Result = result;
                Classification = classification;
            }

            public int Sequence { get; }
            public double ShotTime { get; }
            public string IntendedTargetActorInstanceId { get; }
            public Vector3 Origin { get; }
            public Vector3 Direction { get; }
            public WeaponCombatResult Result { get; }
            public PhysicalShotResolution PhysicalShot => Result.PhysicalShot;
            public CombatResolutionResult Combat => Result.Combat;
            public string Classification { get; }
            public BodyRegion? Region => Combat.Region;
        }

        public static bool TryGetCurrentTargetingEvidence(
            SandboxNpcMetadata actor,
            out CurrentTargetingEvidence evidence)
        {
            evidence = default;
            HumanEncounterAIController ai = actor != null ? actor.GetComponent<HumanEncounterAIController>() : null;
            if (!TryGetCurrentThreat(ai, out ActorRuntimeIdentity threat))
                return false;

            bool hasAimPoint = ai.State == HumanEncounterAIState.Fighting &&
                               HasFiniteGameTime(ai.LastShotTime) &&
                               string.Equals(ai.LastShotIntentTargetActorInstanceId, threat.ActorInstanceId,
                                   StringComparison.Ordinal) &&
                               ai.CurrentShotDirection.sqrMagnitude > 0.0001f;
            bool usesPrimaryAimPoint = threat.GetComponentInChildren<ActorPrimaryAimPoint>(false) != null;
            evidence = new CurrentTargetingEvidence(
                threat,
                ai.State,
                ai.Response,
                ai.CurrentTargetDistance,
                ai.CurrentFocus,
                ai.CurrentSpreadDegrees,
                ai.AttackCount,
                hasAimPoint,
                hasAimPoint ? ai.CurrentAimPoint : default,
                usesPrimaryAimPoint);
            return true;
        }

        public static bool TryCaptureLatestShotEvidence(
            HumanEncounterAIController ai,
            out LatestShotEvidence evidence)
        {
            evidence = default;
            if (ai == null || !HasFiniteGameTime(ai.LastShotTime))
                return false;

            WeaponCombatResult result = ai.LastCombatResult;
            if (!result.PhysicalShot.IsResolved)
                return false;
            evidence = new LatestShotEvidence(
                ai.AttackCount,
                ai.LastShotTime,
                ai.LastShotIntentTargetActorInstanceId,
                ai.LastShotOrigin,
                ai.LastShotDirection,
                result,
                ClassifyShot(result.PhysicalShot, result.Combat));
            return true;
        }

        public bool TryGetCapturedLatestShotEvidence(
            SandboxNpcMetadata actor,
            out LatestShotEvidence evidence)
        {
            evidence = default;
            HumanEncounterAIController ai = actor != null ? actor.GetComponent<HumanEncounterAIController>() : null;
            return ai != null && latestShotEvidenceByActor.TryGetValue(ai, out evidence);
        }

        public static string ClassifyShot(PhysicalShotResolution physicalShot, CombatResolutionResult combat)
        {
            if (physicalShot.Termination == PhysicalShotTermination.Miss ||
                combat.Code == CombatResolutionCode.Miss)
                return "MISS";
            if (combat.Resolved)
                return "ACTOR HIT";
            if (physicalShot.Termination == PhysicalShotTermination.SurfaceStopped ||
                physicalShot.Termination == PhysicalShotTermination.SurfaceLimitStopped ||
                combat.Code == CombatResolutionCode.InvalidTarget)
                return "WORLD / OBSTACLE";
            return physicalShot.IsResolved ? "IMPACT / NO ACTOR RESULT" : "UNRESOLVED";
        }

        public static string DescribeBodyRegion(CombatResolutionResult combat) =>
            combat.Region.HasValue ? combat.Region.Value.ToString() : "NONE";

        public static bool TryBuildCompactActorOverlay(SandboxNpcMetadata actor, out string text)
        {
            text = null;
            if (!IsCompactOverlayEligible(actor))
                return false;

            ActorRuntimeIdentity identity = actor.GetComponent<ActorRuntimeIdentity>();
            ActorAffiliationComponent affiliation = actor.GetComponent<ActorAffiliationComponent>();
            ActorConditionComponent condition = actor.GetComponent<ActorConditionComponent>();
            ActorBehaviorController behavior = actor.GetComponent<ActorBehaviorController>();
            HumanEncounterAIController ai = actor.GetComponent<HumanEncounterAIController>();
            string affiliationText = affiliation != null
                ? Text(string.IsNullOrWhiteSpace(affiliation.DebugDisplayName)
                    ? affiliation.AffiliationId
                    : affiliation.DebugDisplayName)
                : "<NONE>";
            string state = ai != null ? ai.State.ToString() : "AI <NONE>";
            string target = TryGetCurrentThreat(ai, out ActorRuntimeIdentity threat)
                ? ShortActorId(threat.ActorInstanceId)
                : "NONE";
            if (behavior != null && behavior.Owner == ActorBehaviorOwner.Inactive)
            {
                state = "INACTIVE";
                target = "NONE";
            }

            text = $"[{affiliationText}] {state} #{ShortActorId(identity.ActorInstanceId)}\n" +
                   $"T:{target} | {Text(condition != null ? condition.ObservableState : null)}";
            return true;
        }

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
            CaptureLatestShotEvidence();
            if (selected != null)
                ObserveSelected();
            if (visible)
                RefreshWorldVisualState();
            else
            {
                CurrentWorldVisualActorCount = 0;
                LastWorldPerceptionEvidenceCount = 0;
                CurrentWorldOverlayActorCount = 0;
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
            {
                inspectorScroll = GUILayout.BeginScrollView(inspectorScroll,
                    GUILayout.Width(450f), GUILayout.Height(Mathf.Max(180f, Screen.height - 340f)));
                DrawStatus();
                GUILayout.EndScrollView();
            }
            GUILayout.EndArea();
        }

        private void DrawSelectionControls()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Previous")) SelectRelative(-1);
            if (GUILayout.Button("Next")) SelectRelative(1);
            GUILayout.EndHorizontal();
            showCompactActorOverlay = GUILayout.Toggle(showCompactActorOverlay, "Show compact CURRENT actor overlay");
            showPerceptionVisual = GUILayout.Toggle(showPerceptionVisual, "Show CURRENT Gaze / FOV");
            showLastQueryVisual = GUILayout.Toggle(showLastQueryVisual, "Show LAST query segments (historical)");
            GUILayout.Label("LAST = saved query endpoints; NOT a historical blocker hit.");
            showCurrentAimVisual = GUILayout.Toggle(showCurrentAimVisual, "Show CURRENT target / aim");
            showShotVisual = GUILayout.Toggle(showShotVisual, "Show latest real shot visual");
            GUILayout.Label("Selection is retained while its sandbox actor exists.");
        }

        private void OnDestroy()
        {
            latestShotEvidenceByActor.Clear();
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
            if (condition?.IsUnconscious == true)
                GUILayout.Label($"Unconscious dwell remaining: {condition.UnconsciousDwellRemainingSeconds:0.00}s");
            DrawWounds(medical);

            GUILayout.Label("AI / LAST PERCEPTION EVIDENCE", GUI.skin.box);
            GUILayout.Label($"State: {Text(ai?.State.ToString())} | Policy: {Text(ai?.Response.ToString())} | Threat: {Text(ai?.ThreatActorInstanceId)}");
            GUILayout.Label($"LastKnown: {(ai?.HasLastKnownPosition == true ? ai.LastKnownPosition.ToString("F2") : "<NONE>")} | Contact age: {ContactAge(ai)}");
            GUILayout.Label($"Search: {Text(ai?.LastSearchOutcome.ToString())} | Anchor: {(ai?.HasSearchAnchor == true ? ai.SearchAnchor.ToString("F2") : "<NONE>")} | Inspect remaining: {(ai?.IsSearchInspecting == true ? ai.SearchInspectionRemainingSeconds.ToString("0.00") + "s" : "<NONE>")}");
            GUILayout.Label($"Recognition: {Value(acquisition?.HighestRecognitionProgress)} / {Text(acquisition?.HighestRecognitionTargetActorInstanceId)} | Candidate: {Text(acquisition?.HighestRecognitionTargetActorInstanceId)}");
            GUILayout.Label($"Attention: {Text(gaze?.Mode.ToString())} | Gaze yaw: {Value(gaze?.CurrentBodyRelativeYaw)} | Angular error: {Value(gaze?.AngularError)}");
            DrawPerception(ai, acquisition);

            GUILayout.Label("TARGETING / AIM", GUI.skin.box);
            DrawTargeting(ai);

            GUILayout.Label("NAVIGATION / ROAMING", GUI.skin.box);
            GUILayout.Label($"Nav: {Text(navigation?.State.ToString())} | Destination: {(navigation?.HasDestination == true ? navigation.Destination.ToString("F2") : "<NONE>")}");
            GUILayout.Label($"Owner: {Text(behavior?.Owner.ToString())} | Ambient orders: {behavior?.AmbientAcceptedOrderCount.ToString() ?? "<NONE>"} | Ambient travel: {Value(behavior?.AmbientDistanceTravelled)}");
            GUILayout.Label($"Home: {(behavior != null ? behavior.HomeAnchor.ToString("F2") : "<NONE>")} | Radius: {Value(behavior?.MaximumRoamRadius)} | Owner revision: {behavior?.OwnerRevision.ToString() ?? "<NONE>"}");

            GUILayout.Label("COMBAT", GUI.skin.box);
            DrawCombat(ai);
            GUILayout.Label("SEMANTIC TRACE", GUI.skin.box);
            for (int index = trace.Count - 1; index >= 0; index--) GUILayout.Label(trace[index]);
        }

        private void DrawTargeting(HumanEncounterAIController ai)
        {
            GUILayout.Label($"AI state: {Text(ai?.State.ToString())} | Response: {Text(ai?.Response.ToString())}");
            if (!TryGetCurrentTargetingEvidence(selected, out CurrentTargetingEvidence evidence))
            {
                GUILayout.Label("Threat / current target: NONE");
                GUILayout.Label("Current target distance: NONE | CURRENT AIM: NONE");
                GUILayout.Label("Focus: NONE | Effective spread: NONE | Attacks: " + (ai?.AttackCount.ToString() ?? "<NONE>"));
                return;
            }

            GUILayout.Label("Threat / current target ActorInstanceId: " + evidence.ThreatActorInstanceId);
            GUILayout.Label("Current target distance: " + evidence.Distance.ToString("0.##"));
            GUILayout.Label(evidence.HasAimPoint
                ? "CURRENT AIM: " + evidence.AimPoint.ToString("F2")
                : "CURRENT AIM: NONE");
            GUILayout.Label("Target aim authority: " + (evidence.UsesPrimaryAimPoint ? "PRIMARY" : "AI-resolved target point"));
            GUILayout.Label($"Focus: {evidence.Focus:0.##} | Effective spread: {evidence.EffectiveSpread:0.##} | Attacks: {evidence.AttackCount}");
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
            }
            else
            {
                int reserve = firearm != null ? WeaponCombatService.GetCompatibleAmmoQuantity(ownership, weapon) : 0;
                string range = firearm != null ? firearm.range.ToString("0.##") : melee.melee_range.ToString("0.##");
                GUILayout.Label($"Weapon: {weapon.DefinitionId} | Ammo: {weapon.LoadedRounds} loaded / {reserve} reserve | Range: {range}");
            }
            GUILayout.Label("Reload: " + YesNo(ai?.IsReloadPending == true));
            DrawLatestShot();
        }

        private void DrawLatestShot()
        {
            GUILayout.Label("LAST REAL SHOT", GUI.skin.box);
            if (!TryGetCapturedLatestShotEvidence(selected, out LatestShotEvidence evidence))
            {
                GUILayout.Label("No real shot captured.");
                return;
            }

            PhysicalShotResolution shot = evidence.PhysicalShot;
            CombatResolutionResult combat = evidence.Combat;
            GUILayout.Label($"Sequence / AttackCount: {evidence.Sequence} | {evidence.Classification}");
            GUILayout.Label("Intended target ActorInstanceId: " + Text(evidence.IntendedTargetActorInstanceId));
            GUILayout.Label("LastShotOrigin: " + evidence.Origin.ToString("F2") + " | LastShotDirection: " + evidence.Direction.ToString("F3"));
            GUILayout.Label("Physical termination: " + shot.Termination + " | EndPoint: " + shot.EndPoint.ToString("F2"));
            GUILayout.Label("HitCollider: " + Text(shot.HitCollider != null ? shot.HitCollider.name : null) +
                            " | Combat.Region: " + DescribeBodyRegion(combat));
            GUILayout.Label("ArmorFound: " + YesNo(combat.Armor.ArmorFound) + " | Combat result: " + combat.Code +
                            " | Weapon result: " + evidence.Result.Code);
        }

        private void DrawWorldSelectionAndVisuals()
        {
            CurrentWorldDrawnActorCount = 0;
            if (gameplayCamera == null) return;
            if (showCompactActorOverlay)
                GUI.Box(new Rect(18f, 18f, 150f, 22f), "CURRENT ACTORS");
            if (selected != null)
            {
                Vector3 screen = gameplayCamera.WorldToScreenPoint(selected.transform.position + Vector3.up * 2.2f);
                if (screen.z > 0f) GUI.Box(new Rect(screen.x - 62f, Screen.height - screen.y - 16f, 124f, 24f), "SELECTED NPC");
            }
            IReadOnlyList<SandboxNpcMetadata> actors = sandbox?.Spawned;
            if (actors != null && (showCompactActorOverlay || showPerceptionVisual || showLastQueryVisual))
            {
                for (int index = 0; index < actors.Count; index++)
                {
                    SandboxNpcMetadata actor = actors[index];
                    if (showCompactActorOverlay)
                        DrawCompactActorOverlay(actor);
                    if (showPerceptionVisual || showLastQueryVisual)
                        DrawActorWorldPerceptionVisuals(actor);
                }
            }

            if (selected == null) return;
            if (showCurrentAimVisual && TryGetCurrentTargetingEvidence(selected, out CurrentTargetingEvidence targeting) &&
                targeting.HasAimPoint)
            {
                Vector3 origin = selected.transform.position + Vector3.up * 1.5f;
                DrawWorldLine(origin, targeting.AimPoint, Color.green, "CURRENT AIM");
                DrawWorldMarker(targeting.AimPoint, Color.green);
            }
            if (showShotVisual && TryGetCapturedLatestShotEvidence(selected, out LatestShotEvidence evidence) &&
                Time.timeAsDouble - evidence.ShotTime <= ShotVisualLifetimeSeconds)
            {
                Color color = evidence.Classification == "MISS" ? Color.cyan :
                    evidence.Classification == "ACTOR HIT" ? Color.red : new Color(1f, .55f, 0f);
                DrawWorldLine(evidence.Origin, evidence.PhysicalShot.EndPoint, color,
                    "LAST SHOT - " + evidence.Classification);
            }
        }

        private void DrawCompactActorOverlay(SandboxNpcMetadata actor)
        {
            if (!TryBuildCompactActorOverlay(actor, out string label) || gameplayCamera == null)
                return;
            Vector3 screen = gameplayCamera.WorldToScreenPoint(actor.transform.position + Vector3.up * 2.85f);
            if (screen.z <= 0f)
                return;
            GUI.Box(new Rect(screen.x - 94f, Screen.height - screen.y - 34f, 188f, 36f), label);
        }

        private void DrawWorldMarker(Vector3 point, Color color)
        {
            if (gameplayCamera == null)
                return;
            Vector3 screen = gameplayCamera.WorldToScreenPoint(point);
            if (screen.z <= 0f)
                return;
            Color previous = GUI.color;
            GUI.color = color;
            GUI.Box(new Rect(screen.x - 7f, Screen.height - screen.y - 7f, 14f, 14f), GUIContent.none);
            GUI.color = previous;
        }

        private void CaptureLatestShotEvidence()
        {
            IReadOnlyList<SandboxNpcMetadata> actors = sandbox?.Spawned;
            if (actors == null)
                return;
            for (int index = 0; index < actors.Count; index++)
            {
                SandboxNpcMetadata actor = actors[index];
                HumanEncounterAIController ai = actor != null ? actor.GetComponent<HumanEncounterAIController>() : null;
                if (ai == null || !TryCaptureLatestShotEvidence(ai, out LatestShotEvidence evidence))
                    continue;
                if (!latestShotEvidenceByActor.TryGetValue(ai, out LatestShotEvidence previous) ||
                    previous.ShotTime != evidence.ShotTime)
                    latestShotEvidenceByActor[ai] = evidence;
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
            CurrentWorldOverlayActorCount = 0;
            IReadOnlyList<SandboxNpcMetadata> actors = sandbox?.Spawned;
            if (actors == null) return;
            for (int index = 0; index < actors.Count; index++)
            {
                SandboxNpcMetadata actor = actors[index];
                if (actor == null) continue;
                if (IsCompactOverlayEligible(actor))
                    CurrentWorldOverlayActorCount++;
                HumanEncounterAIController ai = actor.GetComponent<HumanEncounterAIController>();
                ActorThreatAcquisitionController acquisition = actor.GetComponent<ActorThreatAcquisitionController>();
                if (HasLastPerceptionEvidence(ResolveLastPerception(ai, acquisition)))
                    LastWorldPerceptionEvidenceCount++;
                if (CanDrawCurrentPerceptionVisuals(actor, out _, out _))
                    CurrentWorldVisualActorCount++;
            }
        }

        private static bool IsCompactOverlayEligible(SandboxNpcMetadata actor)
        {
            if (actor == null || !actor.gameObject.activeInHierarchy)
                return false;
            ActorRuntimeIdentity identity = actor.GetComponent<ActorRuntimeIdentity>();
            ActorHealthComponent health = actor.GetComponent<ActorHealthComponent>();
            return identity != null && identity.IsRegistered && identity.LifecycleState == ActorLifecycleState.Alive &&
                   (health == null || !health.IsDead);
        }

        private static bool TryGetCurrentThreat(
            HumanEncounterAIController ai,
            out ActorRuntimeIdentity threat)
        {
            threat = null;
            if (ai == null || !ai.isActiveAndEnabled || !ai.gameObject.activeInHierarchy ||
                ai.State == HumanEncounterAIState.Inactive)
                return false;

            ActorRuntimeIdentity self = ai.GetComponent<ActorRuntimeIdentity>();
            ActorHealthComponent selfHealth = ai.GetComponent<ActorHealthComponent>();
            ActorConditionComponent selfCondition = ai.GetComponent<ActorConditionComponent>();
            ActorBehaviorController behavior = ai.GetComponent<ActorBehaviorController>();
            if (self == null || !self.IsRegistered || self.LifecycleState != ActorLifecycleState.Alive ||
                (selfHealth != null && selfHealth.IsDead) ||
                (selfCondition != null && !selfCondition.CanPerformActiveActions) ||
                (behavior != null && behavior.Owner == ActorBehaviorOwner.Inactive))
                return false;

            threat = ai.Threat;
            if (threat == null || !threat.gameObject.activeInHierarchy || !threat.IsRegistered ||
                threat.LifecycleState != ActorLifecycleState.Alive)
            {
                threat = null;
                return false;
            }
            ActorHealthComponent targetHealth = threat.GetComponent<ActorHealthComponent>();
            if (targetHealth != null && targetHealth.IsDead)
            {
                threat = null;
                return false;
            }
            return true;
        }

        private static bool HasFiniteGameTime(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static string ShortActorId(string actorInstanceId)
        {
            if (string.IsNullOrWhiteSpace(actorInstanceId))
                return "NONE";
            return actorInstanceId.Length <= 12 ? actorInstanceId : actorInstanceId.Substring(actorInstanceId.Length - 8);
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
            ActorRuntimeIdentity identity = selected.GetComponent<ActorRuntimeIdentity>();
            if (identity != null && identity.LifecycleState != observedLifecycleState)
            {
                observedLifecycleState = identity.LifecycleState;
                AddTrace("Lifecycle -> " + identity.LifecycleState);
            }
            if (ai != null && ai.TransitionRevision != observedTransitionRevision)
            {
                observedTransitionRevision = ai.TransitionRevision;
                AddTrace("Encounter -> " + ai.State);
            }
            if (ai != null && ai.AttackCount != observedAttackCount)
            {
                observedAttackCount = ai.AttackCount;
                if (!HasFiniteGameTime(ai.LastShotTime) || ai.LastShotTime == observedLastShotTime)
                    AddTrace("Attack -> " + ai.AttackCount + " / " + ai.LastCombatResult.Combat.Code);
            }
            if (TryGetCapturedLatestShotEvidence(selected, out LatestShotEvidence shot) &&
                shot.ShotTime != observedLastShotTime)
            {
                observedLastShotTime = shot.ShotTime;
                string region = shot.Region.HasValue ? shot.Region.Value.ToString() : "NONE";
                AddTrace("SHOT " + ShortActorId(shot.IntendedTargetActorInstanceId) + " -> " +
                         ShortActorId(identity?.ActorInstanceId) + " | " + shot.Classification + " | " + region);
                CombatResolutionResult combat = shot.Combat;
                if (combat.Armor.ArmorFound)
                    AddTrace("Armor " + combat.Armor.Outcome);
                if (combat.WoundApplied)
                    AddTrace("Wound " + combat.FinalWoundType + " vital " + combat.VitalIntegrityBefore.ToString("0.##") + "->" + combat.VitalIntegrityAfter.ToString("0.##"));
            }
            if (condition != null && (!hasObservedFunctionalState || condition.FunctionalState != observedFunctionalState))
            {
                ActorFunctionalState previous = observedFunctionalState;
                observedFunctionalState = condition.FunctionalState;
                bool isKo = condition.FunctionalState == ActorFunctionalState.Incapacitated ||
                            condition.FunctionalState == ActorFunctionalState.Unconscious;
                bool wasKo = hasObservedFunctionalState &&
                             (previous == ActorFunctionalState.Incapacitated || previous == ActorFunctionalState.Unconscious);
                bool recovered = wasKo && (condition.FunctionalState == ActorFunctionalState.Conscious ||
                                           condition.FunctionalState == ActorFunctionalState.Dazed);
                AddTrace((isKo ? "KO -> " : recovered ? "Recovery -> " : "Functional -> ") + condition.ObservableState);
                hasObservedFunctionalState = true;
            }
            string threat = ai?.ThreatActorInstanceId;
            if (threat != observedThreat)
            {
                string previousThreat = observedThreat;
                observedThreat = threat;
                if (string.IsNullOrWhiteSpace(previousThreat))
                    AddTrace("Threat acquired -> " + Text(threat));
                else if (string.IsNullOrWhiteSpace(threat))
                    AddTrace("Threat released <- " + previousThreat);
                else
                    AddTrace("Threat changed " + previousThreat + " -> " + threat);
            }
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
            if (selected == value) return;
            selected = value;
            trace.Clear();
            observedTransitionRevision = -1;
            observedAttackCount = -1;
            observedLastShotTime = double.NegativeInfinity;
            hasObservedFunctionalState = false;
            observedLifecycleState = null;
            observedThreat = null;
            observedRecognitionCandidate = null;
            ObserveSelected();
            AddTrace("Selected " + Text(value?.GetComponent<ActorRuntimeIdentity>()?.ActorInstanceId));
        }
        private void AddTrace(string value) { if (string.IsNullOrWhiteSpace(value)) return; trace.Add(Time.time.ToString("0.0") + " " + value); if (trace.Count > MaxTraceEntries) trace.RemoveAt(0); }
        private static string Text(string value) => string.IsNullOrWhiteSpace(value) ? "<NONE>" : value;
        private static string Value(float? value) => value.HasValue ? value.Value.ToString("0.###") : "<NONE>";
        private static string ContactAge(HumanEncounterAIController ai) => ai != null && !double.IsNaN(ai.LastSeenTime)
            ? Math.Max(0d, Time.timeAsDouble - ai.LastSeenTime).ToString("0.00") + "s" : "<NONE>";
        private static string YesNo(bool value) => value ? "Yes" : "No";
    }
}
