using System;
using System.Collections.Generic;
using System.Linq;
using OldScars.Core;
using OldScars.Core.Actors;
using OldScars.Core.Combat;
using OldScars.Core.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldScars.Editor
{
    [InitializeOnLoad]
    public static class M41AnatomicalHitboxDiagnostics
    {
        private const string PhaseKey = "OldScars.M41.AnatomicalHitboxes.Phase";
        private const string ErrorKey = "OldScars.M41.AnatomicalHitboxes.Error";
        private const string Enter = "enter";
        private const string Running = "running";
        private const string Finish = "finish";
        private const string HumanProfile = "core:debug_npc_capsule_01";
        private const string LegacyProfile = "core:debug_navigation_npc_01";
        private const string WhiteProfile = "core:debug_sandbox_npc_01";
        private const string CombatProfile = "core:debug_combat_sandbox_npc_01";
        private const string ActorPrefix = "M41.7 Diagnostic ";

        private static readonly BodyRegion[] Regions =
        {
            BodyRegion.Head,
            BodyRegion.Torso,
            BodyRegion.LeftArm,
            BodyRegion.RightArm,
            BodyRegion.LeftLeg,
            BodyRegion.RightLeg
        };

        private static readonly List<string> regionEvidence = new List<string>();
        private static GameObject shooter;
        private static ActorRuntimeIdentity collapseActor;
        private static double collapseDeadline;
        private static float visualHeight;
        private static bool capsuleBypass;
        private static BodyRegion legacyRegion;
        private static string perceptionEvidence;

        static M41AnatomicalHitboxDiagnostics()
        {
            EditorApplication.update -= Continue;
            EditorApplication.update += Continue;
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("M41 anatomical diagnostics require idle compiled Edit Mode.");
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
                RemoveDiagnosticActors();
                if (shooter != null)
                    UnityEngine.Object.Destroy(shooter);
                SessionState.SetString(PhaseKey, Finish);
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorApplication.ExitPlaymode();
            }
        }

        private static void BeginRun()
        {
            Require(GameDataManager.Instance.Report?.ErrorCount == 0, "Game data validation contains errors.");
            ValidateRepresentationPrefab();
            shooter = new GameObject(ActorPrefix + "Shooter");
            RunSixRegionShots();
            RunLegacyFallback();
            RunPerceptionRegression();
            SetupSandboxAndCollapseEvidence();
            collapseDeadline = Time.timeAsDouble + 4d;
        }

        private static void TickRun()
        {
            if (Time.timeAsDouble > collapseDeadline)
                throw new InvalidOperationException("Human hitboxes did not survive physical collapse before timeout.");
            ActorPhysicalCollapseController collapse =
                collapseActor != null ? collapseActor.GetComponent<ActorPhysicalCollapseController>() : null;
            if (collapse == null || !collapse.IsCollapsed || !collapse.IsDynamic)
                return;

            ActorCombatHitRegion[] hitboxes = collapseActor.GetComponentsInChildren<ActorCombatHitRegion>(false);
            Require(hitboxes.Length == Regions.Length && hitboxes.All(value => value.ActorRoot == collapseActor.transform),
                "Physical collapse detached or invalidated anatomical hitbox ownership.");
            Require(hitboxes.All(value => value.GetComponent<Collider>()?.enabled == true),
                "Physical collapse disabled an anatomical combat collider.");

            Debug.Log(
                "M41 Human Debug Actor & Anatomical Hitboxes Diagnostics: PASS\n" +
                $"- Existing PSX_Char_Male_Base static bind-pose renderer height={visualHeight:0.###}m; Animator count=0\n" +
                $"- Physical regions: {regionEvidence.Count}/6 exact\n" +
                string.Join("\n", regionEvidence) + "\n" +
                $"- Locomotion capsule bypass while enabled: {capsuleBypass}; legacy fallback region={legacyRegion}\n" +
                "- Perception: " + perceptionEvidence + "\n" +
                "- White profile and shared Blue/Red combat profile materialized the same human rig family; " +
                "root physical collapse preserved all six region owners");
            RemoveDiagnosticActors();
            UnityEngine.Object.Destroy(shooter);
            shooter = null;
            SessionState.SetString(PhaseKey, Finish);
            EditorApplication.ExitPlaymode();
        }

        private static void ValidateRepresentationPrefab()
        {
            GameObject prefab = Resources.Load<GameObject>("OldScarsActorRepresentations/humanoid_standard");
            Require(prefab != null, "humanoid_standard runtime representation prefab is missing.");
            Require(prefab.GetComponent<CapsuleCollider>() != null &&
                    prefab.GetComponent<ActorLocomotionCollider>() != null,
                "Human representation does not expose its locomotion capsule role.");
            Require(prefab.GetComponent<EntityVisualRigRuntime>() != null &&
                    prefab.GetComponent<EntityEquipmentVisualSynchronizer>() != null,
                "Human representation does not preserve the existing visual-rig/equipment seams.");
            Require(prefab.GetComponentsInChildren<Animator>(true).Length == 0,
                "Human debug representation must remain static and Animator-free.");
            ValidateAnatomy(prefab.transform, false);
        }

        private static void RunSixRegionShots()
        {
            const float spacing = 3f;
            Vector3 basePosition = new Vector3(-8f, 20f, 38f);
            for (int index = 0; index < Regions.Length; index++)
            {
                BodyRegion intended = Regions[index];
                ActorRuntimeIdentity target = Spawn(
                    HumanProfile, basePosition + Vector3.right * (index * spacing), Quaternion.identity,
                    ActorPrefix + intended);
                float height = ValidateAnatomy(target.transform, true);
                if (index == 0)
                    visualHeight = height;

                ActorCombatHitRegion explicitRegion = target.GetComponentsInChildren<ActorCombatHitRegion>(false)
                    .Single(value => value.Region == intended);
                Collider intendedCollider = explicitRegion.GetComponent<Collider>();
                Vector3 hitCenter = intendedCollider.bounds.center;
                Vector3 origin = hitCenter - Vector3.forward * 3f;
                Vector3 direction = Vector3.forward;
                Physics.SyncTransforms();

                if (intended == BodyRegion.Torso)
                {
                    CapsuleCollider capsule = target.GetComponent<CapsuleCollider>();
                    Require(capsule != null && capsule.enabled &&
                            capsule.Raycast(new Ray(origin, direction), out _, 6f),
                        "Torso bypass fixture did not physically intersect the enabled locomotion capsule.");
                }

                PhysicalShotResolution physical = PhysicalShotPathResolver.Resolve(
                    shooter.transform, origin, direction, 6f, 1f);
                Require(physical.Termination == PhysicalShotTermination.Impact &&
                        physical.HitCollider == intendedCollider,
                    $"Physical shot intended for {intended} hit " +
                    $"'{physical.HitCollider?.name ?? "<NONE>"}' instead of its anatomical collider.");
                Require(physical.HitCollider.GetComponent<ActorCombatHitRegion>() == explicitRegion,
                    $"Physical shot for {intended} did not terminate on an explicit region component.");

                CombatResolutionResult combat = CombatResolutionService.ResolveImpact(
                    physical.HitCollider,
                    physical.EndPoint,
                    new CombatImpact(
                        shooter, null, CombatAttackKind.Firearm, WoundType.Puncture,
                        0.05f, 0.01f, 0.01f, physical.OriginalPower, physical.RemainingPower));
                Require(combat.WoundApplied && combat.Region == intended,
                    $"Combat resolution intended {intended} but resolved {combat.Region?.ToString() ?? "<NONE>"}: " +
                    combat.Message);
                ActorMedicalWoundState wound = target.GetComponent<ActorMedicalStateComponent>()
                    .GetWound(combat.WoundId);
                Require(wound != null && wound.region == intended.ToString(),
                    $"Persisted medical wound did not retain explicit {intended} region.");
                if (intended == BodyRegion.Torso)
                    capsuleBypass = physical.HitCollider != target.GetComponent<CapsuleCollider>();

                regionEvidence.Add(
                    $"  {intended}: origin={origin:F3}; direction={direction:F3}; " +
                    $"collider={physical.HitCollider.name}; hit={physical.EndPoint:F3}; " +
                    $"explicit={explicitRegion.Region}; final={combat.Region}; wound={wound.region}");
            }
        }

        private static void RunLegacyFallback()
        {
            Transform marker = RequireSceneObject(M41SampleSceneNavigationTools.StartName).transform;
            ActorRuntimeIdentity legacy = Spawn(
                LegacyProfile, marker.position, marker.rotation,
                ActorPrefix + "LegacyFallback");
            Require(legacy.GetComponentInChildren<ActorCombatHitRegion>(false) == null,
                "Legacy fallback fixture unexpectedly received explicit anatomical hitboxes.");
            Collider capsule = legacy.GetComponent<Collider>();
            Require(capsule != null, "Legacy fallback actor has no collider.");
            Vector3 direction = Vector3.ProjectOnPlane(legacy.transform.forward, Vector3.up).normalized;
            Vector3 origin = capsule.bounds.center - direction * 3f;
            PhysicalShotResolution physical = PhysicalShotPathResolver.Resolve(
                shooter.transform, origin, direction, 6f, 1f);
            Require(physical.Termination == PhysicalShotTermination.Impact && physical.HitCollider == capsule,
                "Legacy capsule no longer terminates the shared physical shot path.");
            CombatResolutionResult combat = CombatResolutionService.ResolveImpact(
                physical.HitCollider,
                physical.EndPoint,
                new CombatImpact(
                    shooter, null, CombatAttackKind.Firearm, WoundType.Puncture,
                    0.05f, 0.01f, 0.01f, physical.OriginalPower, physical.RemainingPower));
            Require(combat.WoundApplied && combat.Region == BodyRegion.Torso,
                "Legacy collider bounds fallback did not preserve Torso resolution.");
            legacyRegion = combat.Region.Value;
            Require(ActorSpawnService.TryRemoveRuntimeRepresentationForRestore(
                    legacy.ActorInstanceId, out string removalError),
                "Legacy fallback fixture cleanup failed: " + removalError);
        }

        private static void RunPerceptionRegression()
        {
            Transform observerMarker = RequireSceneObject(M41SampleSceneNavigationTools.ObserverName).transform;
            Transform targetMarker = RequireSceneObject(M41SampleSceneNavigationTools.TargetName).transform;
            GameObject barrier = M41SampleSceneNavigationTools.FindBarrier();
            Require(barrier != null, "Navigation/perception barrier is missing.");
            barrier.SetActive(false);

            Vector3 forward = Vector3.ProjectOnPlane(targetMarker.position - observerMarker.position, Vector3.up).normalized;
            ActorRuntimeIdentity observer = Spawn(
                LegacyProfile, observerMarker.position, Quaternion.LookRotation(forward),
                ActorPrefix + "PerceptionObserver");
            ActorRuntimeIdentity target = Spawn(
                HumanProfile, targetMarker.position, Quaternion.identity,
                ActorPrefix + "PerceptionTarget");
            observer.GetComponent<ActorGazeController>()?.Configure(41307001L);
            Physics.SyncTransforms();

            ActorVisualPerceptionService perception = observer.GetComponent<ActorVisualPerceptionService>();
            ActorVisualPerceptionResult clear = perception.Evaluate(target);
            ActorLocomotionCollider locomotion = target.GetComponent<ActorLocomotionCollider>();
            Collider locomotionCollider = locomotion != null ? locomotion.GetComponent<Collider>() : null;
            Require(clear.Perceived && clear.TargetCollider == locomotionCollider &&
                    Vector3.Distance(clear.ObservedPosition, locomotionCollider.bounds.center) <= 0.001f,
                "Explicit combat hitboxes changed the productive perception target center/collider.");

            float distance = Vector3.Distance(observerMarker.position, targetMarker.position);
            target.transform.position = observer.transform.position - forward * distance;
            Physics.SyncTransforms();
            ActorVisualPerceptionResult outside = perception.Evaluate(target);
            Require(!outside.Perceived && outside.Reason == ActorVisualPerceptionReason.OutsideFov,
                "Anatomical hitboxes changed OutsideFov semantics.");

            target.transform.position = targetMarker.position;
            barrier.SetActive(true);
            Physics.SyncTransforms();
            ActorVisualPerceptionResult occluded = perception.Evaluate(target);
            Require(!occluded.Perceived && occluded.Reason == ActorVisualPerceptionReason.Occluded,
                "Anatomical hitboxes changed physical occlusion semantics.");
            barrier.SetActive(false);
            perceptionEvidence =
                $"clear={clear.Reason} via {clear.TargetCollider.name}; outside={outside.Reason}; " +
                $"barrier={occluded.Reason}; observed-center delta=" +
                $"{Vector3.Distance(clear.ObservedPosition, locomotionCollider.bounds.center):0.###}m";
        }

        private static void SetupSandboxAndCollapseEvidence()
        {
            Transform start = RequireSceneObject(M41SampleSceneNavigationTools.StartName).transform;
            Transform goal = RequireSceneObject(M41SampleSceneNavigationTools.GoalName).transform;
            Require(ActorSpawnService.TrySpawnWithLoadoutSeed(
                    WhiteProfile, start.position, start.rotation, 41307011L,
                    out ActorRuntimeIdentity white, out _, out string whiteError),
                "White sandbox representation spawn failed: " + whiteError);
            white.name = ActorPrefix + "WhiteRepresentation";
            Require(ActorSpawnService.TrySpawnWithLoadoutSeed(
                    CombatProfile, goal.position, goal.rotation, 41307012L,
                    out ActorRuntimeIdentity combat, out _, out string combatError),
                "Shared Blue/Red combat representation spawn failed: " + combatError);
            combat.name = ActorPrefix + "BlueRedRepresentation";
            ValidateAnatomy(white.transform, true);
            ValidateAnatomy(combat.transform, true);
            Require(white.GetComponent<EntityVisualRigRuntime>()?.VisualRigProfileId ==
                    "core:human_standard_visual_rig" &&
                    combat.GetComponent<EntityVisualRigRuntime>()?.VisualRigProfileId ==
                    "core:human_standard_visual_rig",
                "White/Blue/Red profiles did not reuse the human_standard visual rig.");

            collapseActor = white;
            collapseActor.GetComponent<ActorHealthComponent>().Kill();
        }

        private static float ValidateAnatomy(Transform root, bool requireActorOwnership)
        {
            ActorCombatHitRegion[] hitboxes = root.GetComponentsInChildren<ActorCombatHitRegion>(false);
            Require(hitboxes.Length == Regions.Length && hitboxes.Select(value => value.Region).Distinct().Count() == Regions.Length,
                $"'{root.name}' does not expose exactly one collider for all six BodyRegions.");
            foreach (BodyRegion region in Regions)
                Require(hitboxes.Count(value => value.Region == region) == 1,
                    $"'{root.name}' lacks one unique {region} combat hitbox.");
            Require(hitboxes.All(value => value.GetComponent<Collider>() != null &&
                                          !value.GetComponent<Collider>().isTrigger),
                $"'{root.name}' contains a missing/trigger anatomical collider.");
            if (requireActorOwnership)
                Require(hitboxes.All(value => value.ActorRoot == root),
                    $"'{root.name}' has an anatomical hitbox owned by another actor root.");

            for (int left = 0; left < hitboxes.Length; left++)
                for (int right = left + 1; right < hitboxes.Length; right++)
                    Require(!hitboxes[left].GetComponent<Collider>().bounds.Intersects(
                            hitboxes[right].GetComponent<Collider>().bounds),
                        $"Anatomical hitboxes {hitboxes[left].Region}/{hitboxes[right].Region} overlap.");

            SkinnedMeshRenderer renderer = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Require(renderer != null && renderer.enabled && renderer.bounds.size.y > 1.4f,
                $"'{root.name}' does not contain a visible human-sized SkinnedMeshRenderer.");
            Require(root.GetComponentsInChildren<Animator>(true).Length == 0,
                $"'{root.name}' unexpectedly contains an Animator.");
            return renderer.bounds.size.y;
        }

        private static ActorRuntimeIdentity Spawn(string profile, Vector3 position, Quaternion rotation, string name)
        {
            Require(ActorSpawnService.TrySpawn(
                    profile, position, rotation, out ActorRuntimeIdentity identity, out string error),
                name + " spawn failed: " + error);
            identity.name = name;
            return identity;
        }

        private static GameObject RequireSceneObject(string name)
        {
            GameObject value = GameObject.Find(name);
            Require(value != null, "Scene object is missing: " + name);
            return value;
        }

        private static void RemoveDiagnosticActors()
        {
            foreach (ActorRuntimeIdentity identity in ActorRuntimeRegistry.ActiveRepresentations
                         .Where(value => value != null && value.OriginKind == ActorOriginKind.Runtime &&
                                         value.name.StartsWith(ActorPrefix, StringComparison.Ordinal)).ToArray())
                ActorSpawnService.TryRemoveRuntimeRepresentationForRestore(identity.ActorInstanceId, out _);
        }

        private static void FinalizeRun()
        {
            string failure = SessionState.GetString(ErrorKey, string.Empty);
            bool success = string.IsNullOrEmpty(failure) && !EditorSceneManager.GetActiveScene().isDirty;
            if (!success)
                Debug.LogError("M41 Human Debug Actor & Anatomical Hitboxes Diagnostics: FAIL\n- " +
                               (string.IsNullOrEmpty(failure) ? "Diagnostic dirtied SampleScene." : failure));
            ClearRun();
            if (Application.isBatchMode)
                EditorApplication.Exit(success ? 0 : 1);
        }

        private static void ClearRun()
        {
            SessionState.EraseString(PhaseKey);
            SessionState.EraseString(ErrorKey);
            regionEvidence.Clear();
            shooter = null;
            collapseActor = null;
            visualHeight = 0f;
            capsuleBypass = false;
            legacyRegion = default;
            perceptionEvidence = null;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
