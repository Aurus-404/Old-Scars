using System;
using System.Collections.Generic;
using System.Linq;
using OldScars.Core;
using OldScars.Core.Actors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace OldScars.Editor
{
    [InitializeOnLoad]
    public static class M41SearchV1Diagnostics
    {
        private const string PhaseKey = "OldScars.M41.SearchV1.Phase";
        private const string ErrorKey = "OldScars.M41.SearchV1.Error";
        private const string Enter = "enter";
        private const string Running = "running";
        private const string Finish = "finish";
        private const string FightProfile = "core:debug_encounter_fight_01";
        private const string AvoidProfile = "core:debug_encounter_avoid_01";
        private const string TargetProfile = "core:debug_navigation_npc_01";
        private const string ActorPrefix = "M41.6 Diagnostic ";
        private const long AmbientSeed = 41306001L;

        private static readonly List<HumanEncounterAIState> stateSequence = new List<HumanEncounterAIState>();
        private static readonly List<ActorBehaviorOwner> ownerSequence = new List<ActorBehaviorOwner>();

        private static ActorRuntimeIdentity actor;
        private static ActorRuntimeIdentity target;
        private static HumanEncounterAIController encounter;
        private static ActorBehaviorController behavior;
        private static ActorNavigationController navigation;
        private static int stage;
        private static double deadline;
        private static double assertionTime;
        private static Vector3 searchStartPosition;
        private static Vector3 frozenObservedPosition;
        private static Vector3 frozenSearchAnchor;
        private static Vector3 frozenDestination;
        private static int frozenPlanAttempts;
        private static int attacksAtContactLoss;
        private static float maximumSearchTravel;
        private static float reacquireTravel;
        private static float releaseTravel;
        private static float releaseArrivalDistance;
        private static float ambientResumeTravel;
        private static float ambientTravelAtRelease;
        private static Vector3 releasePosition;
        private static float inspectionDuration;
        private static string reacquireStates;
        private static string reacquireOwners;
        private static string releaseStates;
        private static string releaseOwners;
        private static int reacquireSearchCount;
        private static int releaseSearchCount;
        private static int avoidSearchCount;
        private static int inactiveSearchCount;
        private static HumanEncounterSearchOutcome invalidTargetOutcome;

        static M41SearchV1Diagnostics()
        {
            EditorApplication.update -= Continue;
            EditorApplication.update += Continue;
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("M41 Search V1 diagnostics require idle compiled Edit Mode.");
            ClearRun();
            SessionState.SetString(PhaseKey, Enter);
            EditorSceneManager.OpenScene(M41SampleSceneNavigationTools.ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void Continue()
        {
            string phase = SessionState.GetString(PhaseKey, string.Empty);
            if (string.IsNullOrEmpty(phase))
                return;
            try
            {
                if (phase == Enter && EditorApplication.isPlaying && Time.frameCount >= 5 &&
                    GameDataManager.Instance?.IsReady == true)
                {
                    BeginRun();
                    SessionState.SetString(PhaseKey, Running);
                }
                else if (phase == Running && EditorApplication.isPlaying)
                    TickRun();
                else if (phase == Finish && !EditorApplication.isPlayingOrWillChangePlaymode)
                    FinalizeRun();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetString(ErrorKey, exception.Message);
                SessionState.SetString(PhaseKey, Finish);
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorApplication.ExitPlaymode();
            }
        }

        private static void BeginRun()
        {
            Require(GameDataManager.Instance.Report?.ErrorCount == 0, "Game data validation contains errors.");
            GameObject barrier = M41SampleSceneNavigationTools.FindBarrier();
            Require(barrier != null, "M41 navigation/perception barrier is missing.");
            barrier.SetActive(false);
            SetupFightScenario("Reacquire");
            SetStage(1, 4d);
        }

        private static void TickRun()
        {
            if (Time.timeAsDouble > deadline)
                throw new InvalidOperationException("M41 Search V1 diagnostic stage timed out: " + stage);
            RecordTimeline();
            if (encounter != null && encounter.State == HumanEncounterAIState.Searching)
            {
                maximumSearchTravel = Mathf.Max(maximumSearchTravel,
                    FlatDistance(actor.transform.position, searchStartPosition));
                Require(encounter.AttackCount == attacksAtContactLoss,
                    "AttackCount increased without fresh perception during Search.");
            }

            switch (stage)
            {
                case 1: WaitForReacquireFight(); break;
                case 2: WaitForReacquireLostContact(); break;
                case 3: WaitForReacquireSearch(); break;
                case 4: VerifyReacquireNoWallhack(); break;
                case 5: WaitForRealReacquire(); break;
                case 6: WaitForReleaseFight(); break;
                case 7: WaitForReleaseLostContact(); break;
                case 8: WaitForReleaseSearch(); break;
                case 9: WaitForReleaseArrival(); break;
                case 10: WaitForReleaseAndAmbient(); break;
                case 11: WaitForAvoidPolicy(); break;
                case 12: VerifyAvoidLostContact(); break;
                case 13: WaitForInactiveFight(); break;
                case 14: WaitForInactiveSearch(); break;
                case 15: VerifySearchInactive(); break;
                case 16: WaitForInvalidFight(); break;
                case 17: WaitForInvalidSearch(); break;
                case 18: VerifyInvalidTargetAbort(); break;
            }
        }

        private static void WaitForReacquireFight()
        {
            if (encounter.State != HumanEncounterAIState.Fighting)
                return;
            Require(encounter.LastPerception.Perceived, "Reacquire branch never began from perceived Fighting.");
            LoseContact();
            SetStage(2, 2d);
        }

        private static void WaitForReacquireLostContact()
        {
            if (encounter.State != HumanEncounterAIState.LostContact)
                return;
            Require(behavior.Owner == ActorBehaviorOwner.Encounter,
                "LostContact ceded Encounter ownership before the explicit Search transition.");
            SetStage(3, 2d);
        }

        private static void WaitForReacquireSearch()
        {
            if (encounter.State != HumanEncounterAIState.Searching)
                return;
            CaptureFrozenSearch();
            Require(behavior.Owner == ActorBehaviorOwner.Search &&
                    navigation.State == ActorNavigationState.Moving &&
                    encounter.LastSearchOutcome == HumanEncounterSearchOutcome.Navigating,
                "Reacquire branch did not enter real Search ownership with one Moving order.");
            MoveTargetHidden(7f);
            assertionTime = Time.timeAsDouble + 0.35d;
            SetStage(4, 2d);
        }

        private static void VerifyReacquireNoWallhack()
        {
            if (Time.timeAsDouble < assertionTime)
                return;
            RequireFrozenSearch("Reacquire no-wallhack");
            Require(maximumSearchTravel > 0.25f,
                "Reacquire Search did not produce physical movement before LOS restoration.");
            PlaceTarget(frozenSearchAnchor);
            SetStage(5, 4d);
        }

        private static void WaitForRealReacquire()
        {
            if (encounter.State != HumanEncounterAIState.Alerted)
            {
                Require(behavior.Owner != ActorBehaviorOwner.Ambient,
                    "Reacquire branch passed through Ambient while preserving its threat.");
                return;
            }
            reacquireTravel = FlatDistance(actor.transform.position, searchStartPosition);
            reacquireSearchCount = encounter.SearchCount;
            Require(encounter.Threat == target && behavior.Owner == ActorBehaviorOwner.Encounter &&
                    encounter.LastPerception.Perceived &&
                    encounter.LastSearchOutcome == HumanEncounterSearchOutcome.Reacquired &&
                    !encounter.HasSearchAnchor && encounter.AttackCount == attacksAtContactLoss,
                "Search reacquisition did not preserve the threat and restore Encounter without an attack.");
            reacquireStates = FormatStates();
            reacquireOwners = FormatOwners();
            RequireContainsReacquireTimeline();
            SetupFightScenario("Release");
            SetStage(6, 4d);
        }

        private static void WaitForReleaseFight()
        {
            if (encounter.State != HumanEncounterAIState.Fighting)
                return;
            Require(encounter.LastPerception.Perceived, "Release branch never began from perceived Fighting.");
            LoseContact();
            SetStage(7, 2d);
        }

        private static void WaitForReleaseLostContact()
        {
            if (encounter.State != HumanEncounterAIState.LostContact)
                return;
            SetStage(8, 2d);
        }

        private static void WaitForReleaseSearch()
        {
            if (encounter.State != HumanEncounterAIState.Searching)
                return;
            CaptureFrozenSearch();
            Require(behavior.Owner == ActorBehaviorOwner.Search && navigation.State == ActorNavigationState.Moving,
                "Release branch did not start Search navigation.");
            MoveTargetHidden(9f);
            assertionTime = Time.timeAsDouble + 0.3d;
            SetStage(9, 8d);
        }

        private static void WaitForReleaseArrival()
        {
            if (Time.timeAsDouble >= assertionTime)
                RequireFrozenSearch("Release no-wallhack");
            if (!encounter.IsSearchInspecting)
                return;
            releaseTravel = FlatDistance(actor.transform.position, searchStartPosition);
            releaseArrivalDistance = FlatDistance(actor.transform.position, frozenSearchAnchor);
            inspectionDuration = encounter.SearchInspectionDurationSeconds;
            Require(behavior.Owner == ActorBehaviorOwner.Search &&
                    navigation.State == ActorNavigationState.Idle &&
                    encounter.LastSearchOutcome == HumanEncounterSearchOutcome.Inspecting &&
                    releaseTravel > 1f && releaseArrivalDistance <= 0.4f,
                "Search did not arrive physically and begin its bounded inspection window.");
            SetStage(10, 8d);
        }

        private static void WaitForReleaseAndAmbient()
        {
            if (encounter.State != HumanEncounterAIState.Idle)
            {
                Require(encounter.AttackCount == attacksAtContactLoss,
                    "Release branch attacked during inspection without fresh perception.");
                return;
            }
            if (releasePosition == default)
            {
                releasePosition = actor.transform.position;
                ambientTravelAtRelease = behavior.AmbientDistanceTravelled;
                releaseSearchCount = encounter.SearchCount;
                releaseStates = FormatStates();
                releaseOwners = FormatOwners();
                Require(encounter.Threat == null && behavior.Owner == ActorBehaviorOwner.Ambient &&
                        encounter.LastSearchOutcome == HumanEncounterSearchOutcome.Released &&
                        !encounter.HasSearchAnchor,
                    "Inspection expiry did not release threat/Search memory to Idle/Ambient.");
                RequireContainsReleaseTimeline();
                return;
            }

            ambientResumeTravel = behavior.AmbientDistanceTravelled - ambientTravelAtRelease;
            if (ambientResumeTravel < 0.5f || FlatDistance(actor.transform.position, releasePosition) < 0.5f)
                return;
            SetupAvoidScenario();
            SetStage(11, 4d);
        }

        private static void WaitForAvoidPolicy()
        {
            if (encounter.State != HumanEncounterAIState.Avoiding)
                return;
            LoseContact();
            SetStage(12, 2d);
        }

        private static void VerifyAvoidLostContact()
        {
            if (encounter.State != HumanEncounterAIState.LostContact)
                return;
            assertionTime = Time.timeAsDouble + 0.4d;
            avoidSearchCount = encounter.SearchCount;
            Require(behavior.Owner == ActorBehaviorOwner.Encounter && avoidSearchCount == 0 &&
                    !encounter.HasSearchAnchor,
                "Avoid policy incorrectly transferred lost contact into Search pursuit.");
            encounter.ClearThreat("Avoid Search regression complete");
            SetupFightScenario("Inactive");
            SetStage(13, 4d);
        }

        private static void WaitForInactiveFight()
        {
            if (encounter.State != HumanEncounterAIState.Fighting)
                return;
            LoseContact();
            SetStage(14, 2d);
        }

        private static void WaitForInactiveSearch()
        {
            if (encounter.State != HumanEncounterAIState.Searching)
                return;
            inactiveSearchCount = encounter.SearchCount;
            attacksAtContactLoss = encounter.AttackCount;
            Require(actor.GetComponent<ActorConditionComponent>().TryApplyPersistenceState(
                    new ActorConditionStateData { bloodFraction = 1f, transientTrauma = 1f }, out string error),
                "Could not apply controlled incapacity to Search actor: " + error);
            SetStage(15, 2d);
        }

        private static void VerifySearchInactive()
        {
            if (encounter.State != HumanEncounterAIState.Inactive)
                return;
            Require(behavior.Owner == ActorBehaviorOwner.Inactive &&
                    navigation.State == ActorNavigationState.Idle && encounter.Threat == null &&
                    !encounter.HasSearchAnchor && encounter.AttackCount == attacksAtContactLoss &&
                    encounter.LastSearchOutcome == HumanEncounterSearchOutcome.Aborted,
                "Incapacitated Search actor retained owner, movement, threat, memory or attack activity.");
            SetupFightScenario("InvalidTarget");
            SetStage(16, 4d);
        }

        private static void WaitForInvalidFight()
        {
            if (encounter.State != HumanEncounterAIState.Fighting)
                return;
            LoseContact();
            SetStage(17, 2d);
        }

        private static void WaitForInvalidSearch()
        {
            if (encounter.State != HumanEncounterAIState.Searching)
                return;
            target.GetComponent<ActorHealthComponent>().Kill();
            SetStage(18, 2d);
        }

        private static void VerifyInvalidTargetAbort()
        {
            if (encounter.State != HumanEncounterAIState.Idle)
                return;
            invalidTargetOutcome = encounter.LastSearchOutcome;
            Require(encounter.Threat == null && behavior.Owner == ActorBehaviorOwner.Ambient &&
                    !encounter.HasSearchAnchor && invalidTargetOutcome == HumanEncounterSearchOutcome.Aborted,
                "Search did not abort cleanly after its target became invalid.");
            CompleteRun();
        }

        private static void SetupFightScenario(string label)
        {
            SetupScenario(FightProfile, label);
        }

        private static void SetupAvoidScenario()
        {
            SetupScenario(AvoidProfile, "Avoid");
        }

        private static void SetupScenario(string profile, string label)
        {
            RemoveDiagnosticActors();
            Transform root = RequireFixtureRoot();
            Vector3 actorPosition = root.TransformPoint(new Vector3(-4f, 0f, -4f));
            Vector3 targetPosition = root.TransformPoint(new Vector3(-4f, 0f, 4f));
            actor = Spawn(profile, actorPosition, Face(targetPosition - actorPosition), ActorPrefix + label + " Actor");
            target = Spawn(TargetProfile, targetPosition, Face(actorPosition - targetPosition), ActorPrefix + label + " Target");
            encounter = actor.GetComponent<HumanEncounterAIController>();
            behavior = actor.GetComponent<ActorBehaviorController>();
            navigation = actor.GetComponent<ActorNavigationController>();
            Require(encounter?.IsConfigured == true && behavior != null && navigation?.IsConfigured == true,
                "Search fixture lacks configured Encounter, Behavior or Navigation.");
            ActorThreatAcquisitionController acquisition = actor.GetComponent<ActorThreatAcquisitionController>();
            if (acquisition != null)
                acquisition.enabled = false;
            behavior.ConfigureAmbient(AmbientSeed + label.Length);
            actor.GetComponent<ActorGazeController>().ConfigureFromIdentity();
            ActorNavigationController targetNavigation = target.GetComponent<ActorNavigationController>();
            targetNavigation?.Stop();
            Require(encounter.TryAssignThreat(target, out string error), "Search threat assignment failed: " + error);
            stateSequence.Clear();
            ownerSequence.Clear();
            releasePosition = default;
            searchStartPosition = actor.transform.position;
            RecordTimeline();
            Physics.SyncTransforms();
        }

        private static void LoseContact()
        {
            Require(encounter.HasLastKnownPosition && encounter.LastPerception.Perceived,
                "Contact loss lacked a legitimate perceived LastKnownPosition.");
            attacksAtContactLoss = encounter.AttackCount;
            searchStartPosition = actor.transform.position;
            MoveTargetHidden(40f);
        }

        private static void CaptureFrozenSearch()
        {
            frozenObservedPosition = encounter.SearchObservedPosition;
            frozenSearchAnchor = encounter.SearchAnchor;
            frozenDestination = navigation.Destination;
            frozenPlanAttempts = encounter.NavigationPlanAttemptCount;
            Require(encounter.HasSearchAnchor &&
                    Vector3.Distance(frozenObservedPosition, encounter.LastKnownPosition) <= 0.01f &&
                    Vector3.Distance(frozenDestination, frozenSearchAnchor) <= 0.05f,
                "Search anchor/order did not derive from the frozen observed LastKnownPosition.");
        }

        private static void RequireFrozenSearch(string context)
        {
            Require(Vector3.Distance(encounter.SearchObservedPosition, frozenObservedPosition) <= 0.001f &&
                    Vector3.Distance(encounter.SearchAnchor, frozenSearchAnchor) <= 0.001f &&
                    encounter.NavigationPlanAttemptCount == frozenPlanAttempts &&
                    (navigation.State != ActorNavigationState.Moving ||
                     Vector3.Distance(navigation.Destination, frozenDestination) <= 0.001f),
                context + " changed frozen Search information/order after hidden target motion.");
        }

        private static void MoveTargetHidden(float offset)
        {
            Vector3 hidden = RequireFixtureRoot().TransformPoint(new Vector3(0f, 0f, 45f + offset));
            PlaceTarget(hidden);
        }

        private static void PlaceTarget(Vector3 position)
        {
            NavMeshAgent agent = target.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled)
                agent.enabled = false;
            target.transform.SetPositionAndRotation(position, Face(actor.transform.position - position));
            Physics.SyncTransforms();
        }

        private static void RecordTimeline()
        {
            if (encounter == null || behavior == null)
                return;
            if (stateSequence.Count == 0 || stateSequence[stateSequence.Count - 1] != encounter.State)
                stateSequence.Add(encounter.State);
            if (ownerSequence.Count == 0 || ownerSequence[ownerSequence.Count - 1] != behavior.Owner)
                ownerSequence.Add(behavior.Owner);
        }

        private static void RequireContainsReacquireTimeline()
        {
            Require(ContainsOrdered(stateSequence, HumanEncounterAIState.Fighting, HumanEncounterAIState.LostContact,
                        HumanEncounterAIState.Searching, HumanEncounterAIState.Alerted) &&
                    ContainsOrdered(ownerSequence, ActorBehaviorOwner.Encounter, ActorBehaviorOwner.Search,
                        ActorBehaviorOwner.Encounter) &&
                    !ownerSequence.Contains(ActorBehaviorOwner.Ambient),
                "Reacquire state/owner timeline was incomplete or passed through Ambient: " +
                FormatStates() + " / " + FormatOwners());
        }

        private static void RequireContainsReleaseTimeline()
        {
            Require(ContainsOrdered(stateSequence, HumanEncounterAIState.Fighting, HumanEncounterAIState.LostContact,
                        HumanEncounterAIState.Searching, HumanEncounterAIState.Idle) &&
                    ContainsOrdered(ownerSequence, ActorBehaviorOwner.Encounter, ActorBehaviorOwner.Search,
                        ActorBehaviorOwner.Ambient),
                "Release state/owner timeline was incomplete: " + FormatStates() + " / " + FormatOwners());
        }

        private static bool ContainsOrdered<T>(IReadOnlyList<T> values, params T[] expected)
        {
            int expectedIndex = 0;
            for (int index = 0; index < values.Count && expectedIndex < expected.Length; index++)
            {
                if (EqualityComparer<T>.Default.Equals(values[index], expected[expectedIndex]))
                    expectedIndex++;
            }
            return expectedIndex == expected.Length;
        }

        private static string FormatStates() => string.Join(" -> ", stateSequence);
        private static string FormatOwners() => string.Join(" -> ", ownerSequence);

        private static ActorRuntimeIdentity Spawn(string profile, Vector3 position, Quaternion rotation, string name)
        {
            Require(ActorSpawnService.TrySpawn(profile, position, rotation,
                    out ActorRuntimeIdentity identity, out string error), name + " spawn failed: " + error);
            identity.name = name;
            return identity;
        }

        private static Transform RequireFixtureRoot()
        {
            GameObject root = GameObject.Find(M41SampleSceneNavigationTools.FixtureRootName);
            Require(root != null, "M41 navigation fixture root is missing.");
            return root.transform;
        }

        private static Quaternion Face(Vector3 direction)
        {
            Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up);
            return flat.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(flat) : Quaternion.identity;
        }

        private static float FlatDistance(Vector3 left, Vector3 right) =>
            Vector3.ProjectOnPlane(left - right, Vector3.up).magnitude;

        private static void SetStage(int value, double timeoutSeconds)
        {
            stage = value;
            deadline = Time.timeAsDouble + timeoutSeconds;
        }

        private static void CompleteRun()
        {
            Debug.Log(
                "M41 LostContact / Search V1 Diagnostics: PASS\n" +
                $"- Reacquire states: {reacquireStates}; owners: {reacquireOwners}; travel={reacquireTravel:0.###}m; searches={reacquireSearchCount}\n" +
                $"- Release states: {releaseStates}; owners: {releaseOwners}; travel={releaseTravel:0.###}m; arrival error={releaseArrivalDistance:0.###}m; inspection={inspectionDuration:0.###}s; searches={releaseSearchCount}\n" +
                $"- Ambient resume travel={ambientResumeTravel:0.###}m; maximum observed Search travel={maximumSearchTravel:0.###}m\n" +
                $"- No-wallhack: observed={frozenObservedPosition:F3}; anchor={frozenSearchAnchor:F3}; one plan attempt remained frozen\n" +
                $"- Avoid searches={avoidSearchCount}; incapacity searches={inactiveSearchCount} -> Inactive; invalid target outcome={invalidTargetOutcome}\n" +
                "- AttackCount remained stable throughout LostContact/Search without fresh perception");
            RemoveDiagnosticActors();
            SessionState.SetString(PhaseKey, Finish);
            EditorApplication.ExitPlaymode();
        }

        private static void FinalizeRun()
        {
            string failure = SessionState.GetString(ErrorKey, string.Empty);
            bool success = string.IsNullOrEmpty(failure) && !EditorSceneManager.GetActiveScene().isDirty;
            if (!success)
                Debug.LogError("M41 LostContact / Search V1 Diagnostics: FAIL\n- " +
                               (string.IsNullOrEmpty(failure) ? "Diagnostic dirtied SampleScene." : failure));
            ClearRun();
            if (Application.isBatchMode)
                EditorApplication.Exit(success ? 0 : 1);
        }

        private static void RemoveDiagnosticActors()
        {
            foreach (ActorRuntimeIdentity identity in ActorRuntimeRegistry.ActiveRepresentations
                         .Where(value => value != null && value.OriginKind == ActorOriginKind.Runtime &&
                                         value.name.StartsWith(ActorPrefix, StringComparison.Ordinal)).ToArray())
                ActorSpawnService.TryRemoveRuntimeRepresentationForRestore(identity.ActorInstanceId, out _);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void ClearRun()
        {
            SessionState.EraseString(PhaseKey);
            SessionState.EraseString(ErrorKey);
            actor = null;
            target = null;
            encounter = null;
            behavior = null;
            navigation = null;
            stateSequence.Clear();
            ownerSequence.Clear();
            stage = 0;
            maximumSearchTravel = 0f;
        }
    }
}
