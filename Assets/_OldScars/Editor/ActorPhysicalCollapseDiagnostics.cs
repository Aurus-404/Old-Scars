using System;
using System.IO;
using System.Linq;
using OldScars.Core;
using OldScars.Core.Actors;
using OldScars.Core.Combat;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Interactions;
using OldScars.Core.Items;
using OldScars.Core.Persistence;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace OldScars.Editor
{
    [InitializeOnLoad]
    public static class ActorPhysicalCollapseDiagnostics
    {
        private const string PendingKey = "OldScars.PhysicalCollapse.Pending";
        private const string StageKey = "OldScars.PhysicalCollapse.Stage";
        private const string FailureKey = "OldScars.PhysicalCollapse.Failure";
        private const string RootKey = "OldScars.PhysicalCollapse.Root";
        private const string Slot = "actor_physical_collapse_state";
        private const string ProfileId = "core:debug_encounter_fight_01";
        private const string HeadWoundA = "wound_f1111111111111111111111111111111";
        private const string HeadWoundB = "wound_f2222222222222222222222222222222";

        private static ActorRuntimeIdentity unconsciousActor;
        private static ActorRuntimeIdentity deadActor;
        private static string unconsciousActorId;
        private static string deadActorId;
        private static string[] unconsciousBelongings;
        private static string[] deadBelongings;
        private static Vector3 navigationStart;
        private static float stageStartedAt;
        private static float unconsciousCollapseAngle;
        private static float recoveredBottomError;
        private static float deadCollapseAngle;

        static ActorPhysicalCollapseDiagnostics()
        {
            EditorApplication.update -= Continue;
            EditorApplication.update += Continue;
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Actor Physical Collapse diagnostics require idle compiled Edit Mode.");

            ClearSession();
            string root = Path.Combine(Path.GetTempPath(), "OldScars_PhysicalCollapse_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            SessionState.SetString(RootKey, root);
            SessionState.SetBool(PendingKey, true);
            SessionState.SetInt(StageKey, 0);
            SessionState.EraseString(FailureKey);
            EditorSceneManager.OpenScene(M41SampleSceneNavigationTools.ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void Continue()
        {
            if (!SessionState.GetBool(PendingKey, false))
                return;
            try
            {
                if (EditorApplication.isPlaying)
                {
                    if (!Ready())
                        return;
                    WorldClock.Current.AdvanceDuringGameplay = false;
                    switch (SessionState.GetInt(StageKey, 0))
                    {
                        case 0:
                            SetupUnconsciousCollapse();
                            SetStage(1);
                            break;
                        case 1:
                            if (Time.time - stageStartedAt >= 1.5f)
                            {
                                ProveUnconsciousFallAndRestore();
                                SetStage(2);
                            }
                            break;
                        case 2:
                            if (Time.time - stageStartedAt >= 0.25f)
                            {
                                ProveRestoredCollapseAndRecover();
                                SetStage(3);
                            }
                            break;
                        case 3:
                            if (Time.time - stageStartedAt >= 0.25f)
                            {
                                ProveUprightAndStartNavigation();
                                SetStage(4);
                            }
                            break;
                        case 4:
                            if (ProveNavigationResumed())
                            {
                                SetupDeadCollapse();
                                SetStage(5);
                            }
                            else if (Time.time - stageStartedAt > 6f)
                            {
                                ActorNavigationController navigation = unconsciousActor.GetComponent<ActorNavigationController>();
                                NavMeshAgent agent = navigation.Agent;
                                throw new InvalidOperationException(
                                    "Recovered NPC did not resume real navigation within six seconds. " +
                                    $"State={navigation.State}, Failure={navigation.Failure}, AgentEnabled={agent.enabled}, " +
                                    $"OnNavMesh={agent.isOnNavMesh}, Stopped={agent.isStopped}, " +
                                    $"Velocity={agent.velocity.magnitude:0.###}, Remaining={agent.remainingDistance:0.###}, " +
                                    $"Moved={Vector3.Distance(navigationStart, unconsciousActor.transform.position):0.###}.");
                            }
                            break;
                        case 5:
                            if (Time.time - stageStartedAt >= 1.5f)
                            {
                                ProveDeadFallAndRestore();
                                SetStage(6);
                            }
                            break;
                        case 6:
                            if (Time.time - stageStartedAt >= 0.3f)
                            {
                                ProveRestoredDeadRemainsCollapsed();
                                SetStage(99);
                                EditorApplication.ExitPlaymode();
                            }
                            break;
                    }
                    return;
                }

                if (!EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetInt(StageKey, 0) == 99)
                    Finish();
                else if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    FailAndFinish("Actor Physical Collapse diagnostic was interrupted before completion.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetString(FailureKey, exception.Message);
                SessionState.SetInt(StageKey, 99);
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorApplication.ExitPlaymode();
                else
                    Finish();
            }
        }

        private static bool Ready()
        {
            return Time.frameCount >= 5 && WorldClock.Current != null &&
                   GameDataManager.Instance != null && GameDataManager.Instance.IsReady &&
                   NavMesh.CalculateTriangulation().vertices.Length > 0;
        }

        private static void SetupUnconsciousCollapse()
        {
            unconsciousActor = SpawnAtMarker(M41SampleSceneNavigationTools.StartName);
            DisableAcquisition(unconsciousActor);
            unconsciousActorId = unconsciousActor.ActorInstanceId;
            unconsciousBelongings = BelongingIds(unconsciousActor);
            Require(unconsciousBelongings.Length > 0, "Unconscious collapse fixture has no real belongings.");

            ActorPhysicalCollapseController collapse = unconsciousActor.GetComponent<ActorPhysicalCollapseController>();
            ActorNavigationController navigation = unconsciousActor.GetComponent<ActorNavigationController>();
            NavMeshAgent agent = navigation?.Agent;
            Rigidbody body = unconsciousActor.GetComponent<Rigidbody>();
            Collider collider = unconsciousActor.GetComponent<Collider>();
            Require(collapse != null && navigation != null && navigation.IsConfigured && agent != null && agent.enabled &&
                    agent.isOnNavMesh && body != null && body.isKinematic && !body.useGravity &&
                    collider != null && collider.enabled && Vector3.Angle(unconsciousActor.transform.up, Vector3.up) < 1f,
                "Conscious NPC did not start upright with kinematic physics and sole NavMesh control.");

            ActorMedicalStateComponent medical = unconsciousActor.GetComponent<ActorMedicalStateComponent>();
            Require(medical.TryApplyWound(
                    HeadWoundA, BodyRegion.Head, WoundType.Blunt, 0.4f, 0f, 0.3f, out string failure) &&
                    medical.TryApplyWound(
                        HeadWoundB, BodyRegion.Head, WoundType.Blunt, 0.5f, 0f, 0.05f, out failure),
                "Could not create recoverable unconscious state: " + failure);

            ActorConditionComponent condition = unconsciousActor.GetComponent<ActorConditionComponent>();
            Require(condition.IsUnconscious && unconsciousActor.LifecycleState == ActorLifecycleState.Alive &&
                    collapse.IsCollapsed && collapse.IsDynamic && !agent.enabled && collider.enabled,
                "Alive+Unconscious did not hand off NavMesh control to dynamic capsule physics.");

            Vector3 goal = MarkerPosition(M41SampleSceneNavigationTools.GoalName);
            Require(!navigation.TryNavigate(goal, out ActorNavigationResult navigationResult) &&
                    navigationResult.Failure == ActorNavigationFailure.Incapacitated,
                "Unconscious actor accepted navigation.");
            ActorItemOwnershipComponent ownership = unconsciousActor.GetComponent<ActorItemOwnershipComponent>();
            Require(WeaponCombatService.TryGetEquippedWeapon(
                    ownership, out ItemInstance weapon, out _, out _, out _),
                "Unconscious collapse fixture lost its equipped weapon.");
            Require(WeaponCombatService.FireEquipped(
                        ownership, weapon.InstanceId, (Collider)null, Vector3.zero).Code == WeaponCombatCode.Incapacitated &&
                    WeaponCombatService.StrikeEquipped(
                        ownership, weapon.InstanceId, (Collider)null, Vector3.zero).Code == WeaponCombatCode.Incapacitated,
                "Unconscious actor was allowed to fire or strike.");
            Require(Inspect(unconsciousActor).IndexOf("Estado: Noqueado", StringComparison.Ordinal) >= 0,
                "Examinar did not report 'Estado: Noqueado' from the living condition authority.");
            Require(unconsciousActor.ActorInstanceId == unconsciousActorId &&
                    unconsciousBelongings.SequenceEqual(BelongingIds(unconsciousActor), StringComparer.Ordinal),
                "Unconscious collapse changed identity or belongings.");
        }

        private static void ProveUnconsciousFallAndRestore()
        {
            ActorPhysicalCollapseController collapse = unconsciousActor.GetComponent<ActorPhysicalCollapseController>();
            unconsciousCollapseAngle = Vector3.Angle(unconsciousActor.transform.up, Vector3.up);
            Require(collapse.IsDynamic && unconsciousCollapseAngle >= 15f &&
                    unconsciousActor.LifecycleState == ActorLifecycleState.Alive,
                "Dynamic physics did not visibly move the unconscious capsule away from vertical.");

            CurrentSliceSaveResult save = CurrentSliceSnapshotService.Save(Slot, Store());
            Require(save.Success, "Current Slice could not save the physically collapsed unconscious actor: " + save.Failure);
            CurrentSliceLoadResult load = CurrentSliceLoadService.Load(Slot, Store());
            Require(load.Success && ActorRuntimeRegistry.TryGet(unconsciousActorId, out unconsciousActor),
                "Current Slice could not restore the physically collapsed unconscious actor: " + load.Failure);
        }

        private static void ProveRestoredCollapseAndRecover()
        {
            ActorPhysicalCollapseController collapse = unconsciousActor.GetComponent<ActorPhysicalCollapseController>();
            ActorConditionComponent condition = unconsciousActor.GetComponent<ActorConditionComponent>();
            Require(condition.IsUnconscious && unconsciousActor.LifecycleState == ActorLifecycleState.Alive &&
                    collapse != null && collapse.IsDynamic &&
                    !unconsciousActor.GetComponent<NavMeshAgent>().enabled &&
                    unconsciousBelongings.SequenceEqual(BelongingIds(unconsciousActor), StringComparer.Ordinal),
                "Restored Alive+Unconscious actor did not derive the same physical collapse representation.");

            Require(WorldClock.Current.TryAdvanceGameTime(WorldClock.SecondsPerHour * 2d, out string failure),
                "Transient unconscious recovery advance failed: " + failure);
            Require(condition.CanPerformActiveActions,
                "Transient unconscious cause did not recover through ActorConditionComponent.");
        }

        private static void ProveUprightAndStartNavigation()
        {
            ActorPhysicalCollapseController collapse = unconsciousActor.GetComponent<ActorPhysicalCollapseController>();
            ActorNavigationController navigation = unconsciousActor.GetComponent<ActorNavigationController>();
            NavMeshAgent agent = navigation.Agent;
            Rigidbody body = collapse.Body;
            Collider collider = unconsciousActor.GetComponent<Collider>();
            Require(!collapse.IsCollapsed && !collapse.IsDynamic && body.isKinematic && !body.useGravity &&
                    agent.enabled && agent.isOnNavMesh &&
                    Vector3.Angle(unconsciousActor.transform.up, Vector3.up) < 1f,
                "Recovered capsule did not become upright/kinematic before returning NavMesh control.");

            Require(NavMesh.SamplePosition(unconsciousActor.transform.position, out NavMeshHit ground, 1.5f, agent.areaMask),
                "Recovered capsule has no nearby NavMesh ground.");
            Physics.SyncTransforms();
            recoveredBottomError = Mathf.Abs(collider.bounds.min.y - ground.position.y);
            Require(recoveredBottomError <= 0.15f,
                "Recovered capsule is embedded in or floating above its NavMesh ground.");

            HumanEncounterAIController encounter = unconsciousActor.GetComponent<HumanEncounterAIController>();
            Require(encounter == null || encounter.State == HumanEncounterAIState.Idle,
                "Recovered encounter AI did not leave its incapacitated state.");
            if (encounter != null)
                encounter.enabled = false;
            navigationStart = unconsciousActor.transform.position;
            Require(navigation.TryNavigate(
                    MarkerPosition(M41SampleSceneNavigationTools.GoalName), out ActorNavigationResult result) && result.Accepted,
                "Recovered NPC could not accept a real navigation order: " + result.Detail);
        }

        private static bool ProveNavigationResumed()
        {
            ActorNavigationController navigation = unconsciousActor.GetComponent<ActorNavigationController>();
            bool moved = Vector3.Distance(navigationStart, unconsciousActor.transform.position) > 0.2f;
            if (!moved && navigation.State != ActorNavigationState.Reached)
                return false;
            navigation.Stop();
            return true;
        }

        private static void SetupDeadCollapse()
        {
            deadActor = SpawnAtMarker(M41SampleSceneNavigationTools.TargetName);
            DisableAcquisition(deadActor);
            deadActorId = deadActor.ActorInstanceId;
            deadBelongings = BelongingIds(deadActor);
            ActorHealthComponent health = deadActor.GetComponent<ActorHealthComponent>();
            Require(health.ApplyDamage(health.CurrentHealth),
                "Existing ActorHealth authority rejected terminal damage fixture.");

            ActorPhysicalCollapseController collapse = deadActor.GetComponent<ActorPhysicalCollapseController>();
            NavMeshAgent agent = deadActor.GetComponent<NavMeshAgent>();
            WorldObjectTags tags = deadActor.GetComponent<WorldObjectTags>();
            Require(health.IsDead && deadActor.LifecycleState == ActorLifecycleState.Dead && collapse.IsDynamic &&
                    !agent.enabled && tags.HasTag(ActorHealthComponent.DeadActorTag) &&
                    tags.HasTag(ActorHealthComponent.LootableActorTag),
                "Death did not enter physical collapse while preserving corpse semantics.");
            Require(Inspect(deadActor).IndexOf("Estado: Muerto", StringComparison.Ordinal) >= 0,
                "Examinar did not report 'Estado: Muerto' from lifecycle authority.");
        }

        private static void ProveDeadFallAndRestore()
        {
            deadCollapseAngle = Vector3.Angle(deadActor.transform.up, Vector3.up);
            Require(deadCollapseAngle >= 15f && deadActor.GetComponent<ActorPhysicalCollapseController>().IsDynamic,
                "Dead capsule did not visibly collapse under physics.");

            CurrentSliceSaveResult save = CurrentSliceSnapshotService.Save(Slot, Store());
            Require(save.Success, "Current Slice could not save dead physical collapse: " + save.Failure);
            CurrentSliceLoadResult load = CurrentSliceLoadService.Load(Slot, Store());
            Require(load.Success && ActorRuntimeRegistry.TryGet(deadActorId, out deadActor),
                "Current Slice could not restore dead physical collapse: " + load.Failure);
        }

        private static void ProveRestoredDeadRemainsCollapsed()
        {
            ActorPhysicalCollapseController collapse = deadActor.GetComponent<ActorPhysicalCollapseController>();
            ActorHealthComponent health = deadActor.GetComponent<ActorHealthComponent>();
            WorldObjectTags tags = deadActor.GetComponent<WorldObjectTags>();
            Require(health.IsDead && deadActor.LifecycleState == ActorLifecycleState.Dead &&
                    collapse.IsDynamic && tags.HasTag(ActorHealthComponent.LootableActorTag) &&
                    deadBelongings.SequenceEqual(BelongingIds(deadActor), StringComparer.Ordinal),
                "Restored dead actor lost permanent collapse, lifecycle, lootability or belongings.");
            Require(WorldClock.Current.TryAdvanceGameTime(WorldClock.SecondsPerHour * 2d, out string failure),
                "Dead permanence clock advance failed: " + failure);
            Require(collapse.IsCollapsed && collapse.IsDynamic &&
                    !deadActor.GetComponent<ActorNavigationController>().TryNavigate(
                        MarkerPosition(M41SampleSceneNavigationTools.StartName), out ActorNavigationResult result) &&
                    result.Failure == ActorNavigationFailure.Dead,
                "Dead actor recovered or regained navigation.");
            Require(Inspect(deadActor).IndexOf("Estado: Muerto", StringComparison.Ordinal) >= 0,
                "Restored dead actor inspection lost lifecycle-derived state.");
            Require(deadActor.GetComponents<ActorPhysicalCollapseController>().Length == 1 &&
                    deadActor.GetComponents<ActorConditionComponent>().Length == 1 &&
                    deadActor.GetComponents<ActorHealthComponent>().Length == 1 &&
                    deadActor.GetComponents<ActorNavigationController>().Length == 1,
                "Duplicate collapse, condition, health or navigation authority was introduced.");

            Debug.Log(
                "Actor Physical Collapse Diagnostics: PASS\n" +
                "- Alive+Unconscious: dynamic Rigidbody, NavMesh disabled, identity/belongings preserved, Examine=Noqueado\n" +
                "- Physical fall angle: unconscious=" + unconsciousCollapseAngle.ToString("0.###") +
                " degrees, dead=" + deadCollapseAngle.ToString("0.###") + " degrees\n" +
                "- Recovery: upright/kinematic, ground error=" + recoveredBottomError.ToString("0.###") +
                "m, existing navigation resumed\n" +
                "- Dead: permanent dynamic collapse, lootable corpse, Examine=Muerto\n" +
                "- Current Slice: unconscious and dead states re-derived physical representation after restore");
        }

        private static ActorRuntimeIdentity SpawnAtMarker(string markerName)
        {
            Vector3 position = MarkerPosition(markerName);
            Require(ActorSpawnService.TrySpawn(
                    ProfileId, position, Quaternion.identity, out ActorRuntimeIdentity actor, out string failure),
                "Runtime actor spawn failed at " + markerName + ": " + failure);
            ActorNavigationController navigation = actor.GetComponent<ActorNavigationController>();
            Require(navigation != null && navigation.Agent != null && navigation.Agent.isOnNavMesh &&
                    navigation.Agent.Warp(position),
                "Spawned actor could not align with the existing NavMeshAgent at " + markerName + ".");
            return actor;
        }

        private static Vector3 MarkerPosition(string markerName)
        {
            Transform marker = M41SampleSceneNavigationTools.FindMarker(markerName);
            Require(marker != null, "Navigation marker is missing: " + markerName);
            Require(NavMesh.SamplePosition(marker.position, out NavMeshHit hit, 2f, NavMesh.AllAreas),
                "Navigation marker did not resolve to NavMesh: " + markerName);
            return hit.position;
        }

        private static void DisableAcquisition(ActorRuntimeIdentity actor)
        {
            ActorThreatAcquisitionController acquisition = actor.GetComponent<ActorThreatAcquisitionController>();
            if (acquisition != null)
                acquisition.enabled = false;
        }

        private static string Inspect(ActorRuntimeIdentity actor)
        {
            ActionDefinition action = GameDataManager.Instance.Database.GetAction("core:examine_object");
            DebugActionExecutionResult result = DebugActionExecutor.Execute(
                action, actor.GetComponent<WorldObjectTags>(), null);
            Require(result.hasResult, "core:examine_object produced no target-info result.");
            return result.body ?? string.Empty;
        }

        private static string[] BelongingIds(ActorRuntimeIdentity actor)
        {
            ActorItemOwnershipComponent ownership = actor.GetComponent<ActorItemOwnershipComponent>();
            return (ownership?.GetAllOwnedEntries() ?? Array.Empty<ItemStorageEntry>())
                .Where(entry => entry?.Item != null)
                .Select(entry => entry.Item.InstanceId + "x" + entry.Quantity)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private static PersistenceFileStore Store()
        {
            string root = SessionState.GetString(RootKey, string.Empty);
            Require(!string.IsNullOrWhiteSpace(root), "Diagnostic persistence root is missing.");
            return new PersistenceFileStore(root);
        }

        private static void SetStage(int stage)
        {
            SessionState.SetInt(StageKey, stage);
            stageStartedAt = Time.time;
        }

        private static void Require(bool condition, string failure)
        {
            if (!condition)
                throw new InvalidOperationException(failure);
        }

        private static void Finish()
        {
            string failure = SessionState.GetString(FailureKey, string.Empty);
            string root = SessionState.GetString(RootKey, string.Empty);
            if (EditorSceneManager.GetActiveScene().isDirty)
                failure = Append(failure, "Diagnostics left SampleScene dirty; it was not saved.");
            try
            {
                if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
                    Directory.Delete(root, true);
            }
            catch (Exception exception)
            {
                failure = Append(failure, "Temporary cleanup failed: " + exception.Message);
            }

            bool success = string.IsNullOrWhiteSpace(failure);
            ClearSession();
            if (!success)
                Debug.LogError("Actor Physical Collapse Diagnostics: FAIL\n- " + failure);
            if (Application.isBatchMode)
                EditorApplication.Exit(success ? 0 : 1);
        }

        private static void FailAndFinish(string failure)
        {
            SessionState.SetString(FailureKey, failure);
            Finish();
        }

        private static string Append(string current, string value) =>
            string.IsNullOrWhiteSpace(current) ? value : current + "\n- " + value;

        private static void ClearSession()
        {
            SessionState.SetBool(PendingKey, false);
            SessionState.EraseString(StageKey);
            SessionState.EraseString(FailureKey);
            SessionState.EraseString(RootKey);
            unconsciousActor = null;
            deadActor = null;
            unconsciousActorId = null;
            deadActorId = null;
            unconsciousBelongings = null;
            deadBelongings = null;
            stageStartedAt = 0f;
        }
    }
}
