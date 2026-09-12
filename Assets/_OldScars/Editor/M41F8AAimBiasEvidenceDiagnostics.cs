using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using OldScars.Core;
using OldScars.Core.Actors;
using OldScars.Core.Combat;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace OldScars.Editor
{
    [InitializeOnLoad]
    public static class M41F8CAimBiasEvidenceDiagnostics
    {
        private const string PhaseKey = "OldScars.M41.F8C.Phase";
        private const string ErrorKey = "OldScars.M41.F8C.Error";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string ActorProfileId = "core:debug_combat_sandbox_npc_01";
        private const string ShooterAffiliationId = "f8a_evidence_shooter";
        private const string TargetAffiliationId = "f8a_evidence_target";
        private const int RequiredPairs = 120;
        private const int ShotsPerRun = 10;
        private const int MaximumSeedAttempts = 128;
        private const float MinimumDistance = 8f;
        private const float MaximumDistance = 12f;
        private const long ShooterLoadoutSeedStart = 41408000L;
        private const long TargetLoadoutSeedStart = 41409000L;
        private const long AimSeedStart = 41410000L;

        private static readonly BodyRegion[] Regions =
        {
            BodyRegion.Head,
            BodyRegion.Torso,
            BodyRegion.LeftArm,
            BodyRegion.RightArm,
            BodyRegion.LeftLeg,
            BodyRegion.RightLeg
        };

        private static readonly List<PairRecord> Pairs = new List<PairRecord>(RequiredPairs);

        private static string phase;
        private static string csvPath;
        private static GameObject losBarrier;
        private static Vector3 shooterPosition;
        private static Vector3 targetPosition;
        private static Quaternion shooterRotation;
        private static Quaternion targetRotation;
        private static ActorRuntimeIdentity shooter;
        private static ActorRuntimeIdentity target;
        private static long shooterLoadoutSeed;
        private static long targetLoadoutSeed;
        private static string shooterLoadoutSignature;
        private static string targetLoadoutSignature;
        private static string firearmDefinitionId;
        private static string firearmProfileId;
        private static string ammoProfileId;
        private static int magazineCapacity;
        private static int initialLoadedRounds;
        private static int shooterSeedAttempts;
        private static int targetSeedAttempts;
        private static int probeWaitFrame;
        private static int pairIndex;
        private static bool legacyCondition;
        private static ShotRecord pendingLegacy;
        private static int lastObservedAttackCount;
        private static double conditionStartedAt;

        static M41F8CAimBiasEvidenceDiagnostics()
        {
            EditorApplication.update -= Continue;
            EditorApplication.update += Continue;
        }

        [MenuItem("Old Scars/Diagnostics/M41 F8C Paired Aim Bias Control")]
        public static void Run()
        {
            Start(false);
        }

        public static void RunBatch()
        {
            Start(true);
        }

        private static void Start(bool requireBatchMode)
        {
            if (requireBatchMode && !Application.isBatchMode)
                throw new InvalidOperationException("F8C paired diagnostic requires Unity batchmode.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("F8C requires idle compiled Edit Mode.");

            ResetRunState();
            SessionState.EraseString(ErrorKey);
            SessionState.SetString(PhaseKey, "enter");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void Continue()
        {
            phase = SessionState.GetString(PhaseKey, string.Empty);
            if (string.IsNullOrEmpty(phase))
                return;

            try
            {
                if (phase == "enter")
                {
                    if (!EditorApplication.isPlaying)
                    {
                        if (!EditorApplication.isPlayingOrWillChangePlaymode)
                            throw new InvalidOperationException("F8C Play Mode entry was interrupted.");
                        return;
                    }
                    if (Time.frameCount < 5 || GameDataManager.Instance?.IsReady != true)
                        return;
                    BeginRun();
                    SetPhase("find-shooter");
                    return;
                }

                if (phase == "find-shooter" && EditorApplication.isPlaying)
                {
                    ProbeShooterLoadout();
                    return;
                }
                if (phase == "find-target" && EditorApplication.isPlaying)
                {
                    ProbeTargetLoadout();
                    return;
                }
                if (phase == "start-legacy" && EditorApplication.isPlaying)
                {
                    if (Time.frameCount > probeWaitFrame)
                    {
                        legacyCondition = true;
                        StartCondition();
                    }
                    return;
                }
                if (phase == "start-primary" && EditorApplication.isPlaying)
                {
                    if (Time.frameCount > probeWaitFrame)
                    {
                        legacyCondition = false;
                        StartCondition();
                    }
                    return;
                }
                if (phase == "running" && EditorApplication.isPlaying)
                {
                    TickCondition();
                    return;
                }
                if (phase == "finish" && !EditorApplication.isPlayingOrWillChangePlaymode)
                    FinalizeRun();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetString(ErrorKey, exception.Message);
                TryRemoveActor(shooter);
                TryRemoveActor(target);
                if (losBarrier != null)
                    losBarrier.SetActive(true);
                SessionState.SetString(PhaseKey, "finish");
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorApplication.ExitPlaymode();
                else
                    FinalizeRun();
            }
        }

        private static void BeginRun()
        {
            Require(GameDataManager.Instance.Report?.ErrorCount == 0,
                "Game data validation contains errors.");

            Transform start = M41SampleSceneNavigationTools.FindMarker(M41SampleSceneNavigationTools.StartName);
            Transform goal = M41SampleSceneNavigationTools.FindMarker(M41SampleSceneNavigationTools.GoalName);
            Require(start != null && goal != null, "F8C SampleScene start/goal markers are unavailable.");
            bool hasStart = NavMesh.SamplePosition(start.position, out NavMeshHit startHit, 1f, NavMesh.AllAreas);
            bool hasGoal = NavMesh.SamplePosition(goal.position, out NavMeshHit goalHit, 1f, NavMesh.AllAreas);
            Require(hasStart && hasGoal,
                "F8C actor positions could not be resolved on the existing SampleScene NavMesh.");

            shooterPosition = startHit.position;
            targetPosition = goalHit.position;
            Vector3 toTarget = Vector3.ProjectOnPlane(targetPosition - shooterPosition, Vector3.up);
            Require(toTarget.sqrMagnitude > 0.01f, "F8C fixture markers are coincident.");
            float nominalDistance = toTarget.magnitude;
            Require(nominalDistance >= MinimumDistance && nominalDistance <= MaximumDistance,
                $"F8C marker distance {nominalDistance:0.###}m is outside the controlled 8-12m range.");
            shooterRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            targetRotation = Quaternion.LookRotation(-toTarget.normalized, Vector3.up);

            losBarrier = M41SampleSceneNavigationTools.FindBarrier();
            if (losBarrier != null)
                losBarrier.SetActive(false);
            Physics.SyncTransforms();

            csvPath = Path.Combine(Path.GetTempPath(),
                "OldScars_F8C_AimBias_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".csv");
            Debug.Log("M41 F8C: preparing deterministic shooter and unarmored target loadouts; " +
                      $"profile={ActorProfileId}; nominal distance={nominalDistance:0.###}m; player=absent in SampleScene.");
        }

        private static void ProbeShooterLoadout()
        {
            if (Time.frameCount <= probeWaitFrame)
                return;
            if (shooterSeedAttempts >= MaximumSeedAttempts)
                throw new InvalidOperationException(
                    $"No deterministic automatic firearm loadout with a full-run compatible ammo supply was found in {MaximumSeedAttempts} seeds.");

            long candidateSeed = ShooterLoadoutSeedStart + shooterSeedAttempts++;
            if (!TrySpawn(candidateSeed, shooterPosition, shooterRotation,
                    out ActorRuntimeIdentity candidate, out ActorLoadoutResult loadout, out string error))
                throw new InvalidOperationException("F8C shooter loadout probe failed to spawn: " + error);

            if (TryReadAutomaticLoadout(candidate, out ItemInstance firearmItem,
                    out FirearmProfileDefinition firearm, out string candidateAmmo) &&
                firearm.magazine_capacity >= ShotsPerRun &&
                CompatibleAmmoQuantity(candidate, candidateAmmo) >= ShotsPerRun)
            {
                shooterLoadoutSeed = candidateSeed;
                shooterLoadoutSignature = loadout.Signature;
                firearmDefinitionId = firearmItem.DefinitionId;
                firearmProfileId = firearm.id;
                ammoProfileId = candidateAmmo;
                magazineCapacity = firearm.magazine_capacity;
                initialLoadedRounds = firearmItem.LoadedRounds;
                RemoveActor(candidate);
                probeWaitFrame = Time.frameCount;
                SetPhase("find-target");
                return;
            }

            RemoveActor(candidate);
            probeWaitFrame = Time.frameCount;
        }

        private static void ProbeTargetLoadout()
        {
            if (Time.frameCount <= probeWaitFrame)
                return;
            if (targetSeedAttempts >= MaximumSeedAttempts)
                throw new InvalidOperationException(
                    $"No deterministic unarmored target loadout found in {MaximumSeedAttempts} seeds.");

            long candidateSeed = TargetLoadoutSeedStart + targetSeedAttempts++;
            if (!TrySpawn(candidateSeed, targetPosition, targetRotation,
                    out ActorRuntimeIdentity candidate, out ActorLoadoutResult loadout, out string error))
                throw new InvalidOperationException("F8C target loadout probe failed to spawn: " + error);

            if (!HasEquippedArmor(candidate))
            {
                targetLoadoutSeed = candidateSeed;
                targetLoadoutSignature = loadout.Signature;
                ValidateHumanHitRegions(candidate);
            }
            RemoveActor(candidate);
            probeWaitFrame = Time.frameCount;
            if (targetLoadoutSeed != 0L)
                SetPhase("start-legacy");
        }

        private static void StartCondition()
        {
            Require(pairIndex < RequiredPairs,
                $"F8C attempted to create pair {pairIndex + 1} beyond the exact {RequiredPairs}-pair contract.");
            if (!TrySpawn(shooterLoadoutSeed, shooterPosition, shooterRotation,
                    out shooter, out ActorLoadoutResult shooterLoadout, out string shooterError))
                throw new InvalidOperationException("F8C shooter spawn failed: " + shooterError);
            if (!TrySpawn(targetLoadoutSeed, targetPosition, targetRotation,
                    out target, out ActorLoadoutResult targetLoadout, out string targetError))
                throw new InvalidOperationException("F8C target spawn failed: " + targetError);

            Require(shooterLoadout.Signature == shooterLoadoutSignature,
                "F8C shooter loadout changed between paired conditions.");
            Require(targetLoadout.Signature == targetLoadoutSignature,
                "F8C target loadout changed between paired conditions.");
            Require(TryReadAutomaticLoadout(shooter, out ItemInstance firearmItem,
                        out FirearmProfileDefinition firearm, out string loadedAmmoProfileId) &&
                    firearmItem.DefinitionId == firearmDefinitionId && firearm.id == firearmProfileId &&
                    loadedAmmoProfileId == ammoProfileId && firearmItem.LoadedRounds == initialLoadedRounds &&
                    CompatibleAmmoQuantity(shooter, loadedAmmoProfileId) >= 1,
                "F8C paired shooter firearm, ammo, or starting rounds differ from the selected fixture.");
            ValidateHumanHitRegions(target);

            ActorPrimaryAimPoint[] authoredAimPoints = target.GetComponentsInChildren<ActorPrimaryAimPoint>(false);
            Require(authoredAimPoints.Length == 1,
                $"F8C fixture expected one authored aim point before condition setup, found {authoredAimPoints.Length}.");
            if (legacyCondition)
            {
                // The production resolver searches active children; disabling only this runtime fixture object
                // forces its existing locomotion-collider fallback without changing authored content.
                authoredAimPoints[0].gameObject.SetActive(false);
                Require(target.GetComponentInChildren<ActorPrimaryAimPoint>(false) == null,
                    "F8C LEGACY fixture still exposes a Primary Aim Point to the production resolver.");
            }
            else
            {
                Require(authoredAimPoints[0].gameObject.activeInHierarchy,
                    "F8C PRIMARY fixture aim point is not active.");
            }

            Place(shooter, shooterPosition, shooterRotation);
            Place(target, targetPosition, targetRotation);
            ConfigureAffiliation(shooter, ShooterAffiliationId, new[] { TargetAffiliationId });
            ConfigureAffiliation(target, TargetAffiliationId, Array.Empty<string>());
            Physics.SyncTransforms();

            HumanEncounterAIController encounter = shooter.GetComponent<HumanEncounterAIController>();
            Require(encounter != null && encounter.IsConfigured,
                "F8C shooter has no configured production HumanEncounterAIController.");
            encounter.ConfigureDeterministicAimSeed(AimSeedStart + pairIndex);
            Require(encounter.TryAssignThreat(target, out string assignmentError),
                "F8C production threat assignment failed: " + assignmentError);

            lastObservedAttackCount = encounter.AttackCount;
            conditionStartedAt = Time.timeAsDouble;
            SetPhase("running");
        }

        private static void TickCondition()
        {
            Require(shooter != null && target != null, "F8C current paired condition lost its actor fixture.");
            HumanEncounterAIController encounter = shooter.GetComponent<HumanEncounterAIController>();
            if (encounter.AttackCount > lastObservedAttackCount)
            {
                Require(encounter.AttackCount == lastObservedAttackCount + 1,
                    "F8C condition produced more than one shot before the diagnostic could observe it.");
                ShotRecord shot = CaptureShot(encounter, legacyCondition);
                Require(encounter.AttackCount == lastObservedAttackCount + 1,
                    "F8C condition fired more than exactly one first shot.");
                RemoveActor(shooter);
                RemoveActor(target);
                shooter = null;
                target = null;
                probeWaitFrame = Time.frameCount;

                if (legacyCondition)
                {
                    pendingLegacy = shot;
                    SetPhase("start-primary");
                }
                else
                {
                    Require(pendingLegacy != null, "F8C PRIMARY shot has no paired LEGACY sample.");
                    ValidatePair(pendingLegacy, shot);
                    Pairs.Add(new PairRecord { Seed = AimSeedStart + pairIndex, Legacy = pendingLegacy, Primary = shot });
                    pendingLegacy = null;
                    pairIndex++;
                    if (pairIndex == RequiredPairs)
                    {
                        ValidateCompleteSample();
                        WriteReportAndExit();
                    }
                    else
                    {
                        SetPhase("start-legacy");
                    }
                }
                return;
            }

            ActorConditionComponent targetCondition = target.GetComponent<ActorConditionComponent>();
            if (target.LifecycleState == ActorLifecycleState.Dead ||
                targetCondition == null || !targetCondition.CanPerformActiveActions)
                throw new InvalidOperationException("F8C target became unable to receive its required first shot.");
            if (Time.timeAsDouble - conditionStartedAt > 40d)
                throw new InvalidOperationException(
                    $"F8C first shot timed out (condition={(legacyCondition ? "LEGACY" : "PRIMARY")}, " +
                    $"seed={AimSeedStart + pairIndex}, state={encounter.State}, " +
                    $"targetSeen={encounter.LastPerception.Perceived}).");
        }

        private static ShotRecord CaptureShot(HumanEncounterAIController encounter, bool isLegacy)
        {
            WeaponCombatResult result = encounter.LastCombatResult;
            PhysicalShotResolution physical = result.PhysicalShot;
            Require(result.Quantity == 1 && physical.IsResolved,
                "F8C observed an AttackCount change without one resolved productive physical shot.");
            Require(encounter.AimSampleSequence == 1,
                $"F8C first shot aim_sample_sequence was {encounter.AimSampleSequence}, expected 1.");
            Require(encounter.LastShotIntentTargetActorInstanceId == target.ActorInstanceId,
                "F8C production shot intent no longer matches the controlled target.");

            ActorCombatHitRegion[] regions = target.GetComponentsInChildren<ActorCombatHitRegion>(false);
            ActorCombatHitRegion torsoRegion = regions.FirstOrDefault(value => value.Region == BodyRegion.Torso);
            Collider torsoCollider = torsoRegion != null ? torsoRegion.GetComponent<Collider>() : null;
            Require(torsoCollider != null, "F8C target lost its explicit torso reference collider.");

            ActorLocomotionCollider locomotion = target.GetComponentsInChildren<ActorLocomotionCollider>(false)
                .FirstOrDefault(value => value.GetComponent<Collider>() != null);
            Collider legacyCollider = locomotion != null ? locomotion.GetComponent<Collider>() : null;
            Require(legacyCollider != null, "F8C target lost its production locomotion fallback collider.");
            ActorPrimaryAimPoint[] activeAimPoints = target.GetComponentsInChildren<ActorPrimaryAimPoint>(false);
            Vector3 expectedAimPoint;
            string aimPointSource;
            if (isLegacy)
            {
                Require(activeAimPoints.Length == 0,
                    "F8C LEGACY condition did not hide Primary Aim Point from the production resolver.");
                expectedAimPoint = legacyCollider.bounds.center;
                aimPointSource = "legacy-locomotion-collider-fallback";
            }
            else
            {
                Require(activeAimPoints.Length == 1,
                    $"F8C PRIMARY condition expected one active authored aim point, found {activeAimPoints.Length}.");
                expectedAimPoint = activeAimPoints[0].Point.position;
                aimPointSource = "ActorPrimaryAimPoint:" + activeAimPoints[0].name;
            }

            Vector3 aimPoint = encounter.CurrentAimPoint;
            Require(Vector3.Distance(aimPoint, expectedAimPoint) <= 0.002f,
                $"F8C {(isLegacy ? "LEGACY fallback" : "PRIMARY authored point")} did not match CurrentAimPoint.");

            Collider hitCollider = physical.HitCollider;
            ActorRuntimeIdentity hitActor = hitCollider != null
                ? hitCollider.GetComponentInParent<ActorRuntimeIdentity>()
                : null;
            BodyRegion? actualRegion = hitActor == target && result.Combat.Region.HasValue
                ? result.Combat.Region
                : null;
            ActorNavigationController shooterNavigation = shooter.GetComponent<ActorNavigationController>();
            ActorNavigationController targetNavigation = target.GetComponent<ActorNavigationController>();

            return new ShotRecord
            {
                Run = pairIndex + 1,
                AimSeed = AimSeedStart + pairIndex,
                AimSampleSequence = encounter.AimSampleSequence,
                ShooterActorInstanceId = shooter.ActorInstanceId,
                TargetActorInstanceId = encounter.LastShotIntentTargetActorInstanceId,
                ShooterPosition = shooter.transform.position,
                TargetPosition = target.transform.position,
                TargetDistance = Vector3.Distance(encounter.LastShotOrigin, aimPoint),
                AimPoint = aimPoint,
                AimPointSource = aimPointSource,
                TorsoReferenceCollider = torsoCollider.name,
                TorsoCenterReference = torsoCollider.bounds.center,
                AimPointVerticalDelta = aimPoint.y - torsoCollider.bounds.center.y,
                ShotOrigin = encounter.LastShotOrigin,
                ShotDirection = encounter.LastShotDirection,
                Focus = encounter.CurrentFocus,
                SpreadDegrees = encounter.CurrentSpreadDegrees,
                DefocusedSpreadDegrees = encounter.CurrentDefocusedSpreadDegrees,
                ShooterSpeed = shooterNavigation?.Agent != null ? shooterNavigation.Agent.velocity.magnitude : 0f,
                TargetSpeed = targetNavigation?.Agent != null ? targetNavigation.Agent.velocity.magnitude : 0f,
                PhysicalTermination = physical.Termination,
                HitCollider = hitCollider != null ? hitCollider.name : string.Empty,
                HitActorInstanceId = hitActor != null ? hitActor.ActorInstanceId : string.Empty,
                HitEndPoint = physical.EndPoint,
                BodyRegion = actualRegion,
                CombatCode = result.Combat.Code,
                TerminalSurfaceProfileId = physical.TerminalSurfaceProfileId ?? string.Empty,
                PenetratedSurfaceCount = physical.PenetratedSurfaceCount,
                FirearmDefinitionId = firearmDefinitionId,
                FirearmProfileId = firearmProfileId,
                AmmoProfileId = ammoProfileId,
                RemainingRounds = EquippedFirearmItem(shooter)?.LoadedRounds ?? -1
            };
        }

        private static void ValidatePair(ShotRecord legacy, ShotRecord primary)
        {
            Require(legacy.AimSeed == primary.AimSeed && legacy.AimSampleSequence == 1 && primary.AimSampleSequence == 1,
                "F8C pair seed or first-sample sequence does not match.");
            Require(Mathf.Abs(legacy.Focus - primary.Focus) <= 0.000001f,
                $"F8C Focus differs for seed {legacy.AimSeed}: {legacy.Focus} vs {primary.Focus}.");
            Require(Mathf.Abs(legacy.SpreadDegrees - primary.SpreadDegrees) <= 0.002f,
                $"F8C spread differs beyond tolerance for seed {legacy.AimSeed}: {legacy.SpreadDegrees} vs {primary.SpreadDegrees}.");
            Require(Vector3.Distance(legacy.ShotOrigin, primary.ShotOrigin) <= 0.0001f,
                $"F8C shot origin differs for seed {legacy.AimSeed}.");
            Require(Vector3.Distance(legacy.ShooterPosition, primary.ShooterPosition) <= 0.0001f &&
                    Vector3.Distance(legacy.TargetPosition, primary.TargetPosition) <= 0.0001f,
                $"F8C shooter/target position differs for seed {legacy.AimSeed}.");
            Require(Mathf.Abs(legacy.ShooterSpeed - primary.ShooterSpeed) <= 0.001f &&
                    Mathf.Abs(legacy.TargetSpeed - primary.TargetSpeed) <= 0.001f,
                $"F8C movement differs for seed {legacy.AimSeed}: shooter {legacy.ShooterSpeed}/{primary.ShooterSpeed}, target {legacy.TargetSpeed}/{primary.TargetSpeed}.");
            Require(legacy.FirearmDefinitionId == primary.FirearmDefinitionId &&
                    legacy.FirearmProfileId == primary.FirearmProfileId && legacy.AmmoProfileId == primary.AmmoProfileId,
                $"F8C firearm/ammo differs for seed {legacy.AimSeed}.");
            Require(legacy.Run == primary.Run && legacy.Run == Pairs.Count + 1,
                $"F8C pair order is not one-to-one at seed {legacy.AimSeed}.");
        }

        private static void ValidateCompleteSample()
        {
            Require(Pairs.Count == RequiredPairs && pairIndex == RequiredPairs,
                $"F8C requires exactly {RequiredPairs} complete pairs; got {Pairs.Count}.");
            Require(Pairs.Select(value => value.Seed).Distinct().Count() == RequiredPairs &&
                    Pairs.Select(value => value.Seed).SequenceEqual(Enumerable.Range(0, RequiredPairs).Select(i => AimSeedStart + i)),
                "F8C seed set is incomplete, duplicated, or out of contract order.");
            Require(Pairs.All(value => value.Legacy.AimSeed == value.Seed && value.Primary.AimSeed == value.Seed &&
                                       value.Legacy.AimSampleSequence == 1 && value.Primary.AimSampleSequence == 1),
                "F8C has a missing/mispaired condition or non-first aim sample.");
        }

        private static void WriteReportAndExit()
        {
            string directory = Path.GetDirectoryName(csvPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(csvPath, BuildCsv(), new UTF8Encoding(false));
            Debug.Log(BuildSummary());
            SetPhase("finish");
            EditorApplication.ExitPlaymode();
        }

        private static string BuildCsv()
        {
            var builder = new StringBuilder();
            string[] fields = { "run", "aim_seed", "aim_sample_sequence", "shooter_actor_instance_id", "target_actor_instance_id", "shooter_position", "target_position", "target_distance_m", "aim_point_source", "aim_point", "torso_reference_collider", "torso_center_reference", "aim_point_vertical_delta_m", "shot_origin", "shot_direction", "focus", "effective_spread_degrees", "defocused_spread_degrees", "shooter_speed_mps", "target_speed_mps", "physical_termination", "hit_collider", "hit_actor_instance_id", "hit_end_point", "body_region", "combat_code", "terminal_surface_profile_id", "penetrated_surface_count", "firearm_definition_id", "firearm_profile_id", "ammo_profile_id", "remaining_loaded_rounds" };
            builder.Append("seed");
            foreach (string prefix in new[] { "legacy", "primary" })
                foreach (string field in fields)
                    builder.Append(',').Append(prefix).Append('_').Append(field);
            builder.AppendLine();
            foreach (PairRecord pair in Pairs)
            {
                builder.Append(pair.Seed);
                AppendShot(builder, pair.Legacy);
                AppendShot(builder, pair.Primary);
                builder.AppendLine();
            }
            return builder.ToString();
        }

        private static void AppendShot(StringBuilder builder, ShotRecord sample)
        {
            builder.Append(',').Append(sample.Run)
                .Append(',').Append(sample.AimSeed)
                .Append(',').Append(sample.AimSampleSequence)
                .Append(',').Append(Csv(sample.ShooterActorInstanceId))
                .Append(',').Append(Csv(sample.TargetActorInstanceId))
                .Append(',').Append(Csv(Vector(sample.ShooterPosition)))
                .Append(',').Append(Csv(Vector(sample.TargetPosition)))
                .Append(',').Append(Number(sample.TargetDistance))
                .Append(',').Append(Csv(sample.AimPointSource))
                .Append(',').Append(Csv(Vector(sample.AimPoint)))
                .Append(',').Append(Csv(sample.TorsoReferenceCollider))
                .Append(',').Append(Csv(Vector(sample.TorsoCenterReference)))
                .Append(',').Append(Number(sample.AimPointVerticalDelta))
                .Append(',').Append(Csv(Vector(sample.ShotOrigin)))
                .Append(',').Append(Csv(Vector(sample.ShotDirection)))
                .Append(',').Append(Number(sample.Focus))
                .Append(',').Append(Number(sample.SpreadDegrees))
                .Append(',').Append(Number(sample.DefocusedSpreadDegrees))
                .Append(',').Append(Number(sample.ShooterSpeed))
                .Append(',').Append(Number(sample.TargetSpeed))
                .Append(',').Append(sample.PhysicalTermination)
                .Append(',').Append(Csv(sample.HitCollider))
                .Append(',').Append(Csv(sample.HitActorInstanceId))
                .Append(',').Append(Csv(Vector(sample.HitEndPoint)))
                .Append(',').Append(sample.BodyRegion?.ToString() ?? Category(sample))
                .Append(',').Append(sample.CombatCode)
                .Append(',').Append(Csv(sample.TerminalSurfaceProfileId))
                .Append(',').Append(sample.PenetratedSurfaceCount)
                .Append(',').Append(Csv(sample.FirearmDefinitionId))
                .Append(',').Append(Csv(sample.FirearmProfileId))
                .Append(',').Append(Csv(sample.AmmoProfileId))
                .Append(',').Append(sample.RemainingRounds);
        }

        private static string BuildSummary()
        {
            ShotRecord[] legacy = Pairs.Select(value => value.Legacy).ToArray();
            ShotRecord[] primary = Pairs.Select(value => value.Primary).ToArray();
            float[] spreadDeltas = Pairs.Select(value => value.Primary.SpreadDegrees - value.Legacy.SpreadDegrees).ToArray();
            float[] absoluteSpreadDeltas = spreadDeltas.Select(Mathf.Abs).ToArray();
            int legacyLegs = legacy.Count(value => IsRegion(value, BodyRegion.LeftLeg) || IsRegion(value, BodyRegion.RightLeg));
            int primaryLegs = primary.Count(value => IsRegion(value, BodyRegion.LeftLeg) || IsRegion(value, BodyRegion.RightLeg));
            bool noCompensatoryLegTransitions = !Pairs.Any(value =>
                !IsLeg(value.Legacy) && IsLeg(value.Primary));
            string classification = primaryLegs < legacyLegs && noCompensatoryLegTransitions
                ? "A — El uso de locomotion center era causa material del sesgo bajo."
                : "No A — los 120 pares no cumplen simultáneamente reducción clara de piernas y ausencia de transiciones compensatorias hacia piernas.";

            var builder = new StringBuilder();
            builder.AppendLine("M41 F8C PAIRED CONTROL: PASS")
                .AppendLine($"Pairs: {Pairs.Count}; LEGACY shots: {legacy.Length}; PRIMARY shots: {primary.Length}; aim seeds: {Pairs.Select(value => value.Seed).Distinct().Count()}")
                .AppendLine("                LEGACY   PRIMARY")
                .AppendLine(CountLine("Shots", legacy.Length, primary.Length))
                .AppendLine(CountLine("Actor hits", legacy.Count(IsTargetActorHit), primary.Count(IsTargetActorHit)))
                .AppendLine(CountLine("Head", CountRegion(legacy, BodyRegion.Head), CountRegion(primary, BodyRegion.Head)))
                .AppendLine(CountLine("Torso", CountRegion(legacy, BodyRegion.Torso), CountRegion(primary, BodyRegion.Torso)))
                .AppendLine(CountLine("LeftArm", CountRegion(legacy, BodyRegion.LeftArm), CountRegion(primary, BodyRegion.LeftArm)))
                .AppendLine(CountLine("RightArm", CountRegion(legacy, BodyRegion.RightArm), CountRegion(primary, BodyRegion.RightArm)))
                .AppendLine(CountLine("LeftLeg", CountRegion(legacy, BodyRegion.LeftLeg), CountRegion(primary, BodyRegion.LeftLeg)))
                .AppendLine(CountLine("RightLeg", CountRegion(legacy, BodyRegion.RightLeg), CountRegion(primary, BodyRegion.RightLeg)))
                .AppendLine(CountLine("Combined Legs", legacyLegs, primaryLegs))
                .AppendLine(CountLine("Miss", legacy.Count(IsMiss), primary.Count(IsMiss)))
                .AppendLine(CountLine("Ground/world", legacy.Count(IsObstacleStop), primary.Count(IsObstacleStop)))
                .AppendLine("Paired transitions:");
            foreach (string transition in new[] { "Leg -> Torso", "Leg -> Miss", "Torso -> Torso", "Torso -> Leg", "Miss -> Torso", "Miss -> Miss" })
                builder.AppendLine($"{transition}: {Pairs.Count(value => Transition(value) == transition)}");
            builder.AppendLine("All nonzero transitions:");
            foreach (var group in Pairs.GroupBy(Transition).Where(value => value.Count() > 0).OrderBy(value => value.Key))
                builder.AppendLine($"{group.Key}: {group.Count()}");
            builder.AppendLine($"Aim-point vertical delta vs torso center (m): LEGACY mean={legacy.Average(value => value.AimPointVerticalDelta):0.###}; PRIMARY mean={primary.Average(value => value.AimPointVerticalDelta):0.###}; mean paired delta={Pairs.Average(value => value.Primary.AimPointVerticalDelta - value.Legacy.AimPointVerticalDelta):0.###}")
                .AppendLine($"Paired spread delta PRIMARY-LEGACY (degrees): signed mean={spreadDeltas.Average():0.######}; mean absolute={absoluteSpreadDeltas.Average():0.######}; max absolute={absoluteSpreadDeltas.Max():0.######}")
                .AppendLine($"Equivalence: seed/sequence=PASS (120 unique seeds, sequence 1); focus=PASS; spread=PASS (max tolerance 0.002 degrees); origin=PASS (max tolerance 0.0001m); movement=PASS (max speed delta 0.001m/s); firearm/ammo=PASS ({firearmDefinitionId}/{firearmProfileId}, {ammoProfileId}); profiles=PASS ({ActorProfileId})")
                .AppendLine($"Fixture: shooter loadout seed={shooterLoadoutSeed}; target loadout seed={targetLoadoutSeed}; origin contract=HumanEncounterAIController.PhysicalOrigin; target unarmored; same SampleScene positions and runtime hitboxes; only LEGACY fixture aim-point GameObject was deactivated to reach production locomotion-collider fallback.")
                .AppendLine("ISSUE-0008 classification: " + classification)
                .AppendLine("No gameplay tuning or runtime changes were made for F8C. M41CombatSandboxDiagnostics was not rerun because HumanEncounterAIController was unchanged.")
                .AppendLine("Paired CSV: " + csvPath);
            return builder.ToString();
        }

        private static string CountLine(string label, int legacy, int primary) => $"{label,-16} {legacy,3}      {primary,3}";

        private static int CountRegion(IEnumerable<ShotRecord> shots, BodyRegion region) =>
            shots.Count(value => IsRegion(value, region));

        private static bool IsRegion(ShotRecord sample, BodyRegion region) =>
            sample.BodyRegion == region && IsTargetActorHit(sample);

        private static bool IsLeg(ShotRecord sample) =>
            IsRegion(sample, BodyRegion.LeftLeg) || IsRegion(sample, BodyRegion.RightLeg);

        private static string Category(ShotRecord sample) => IsTargetActorHit(sample)
            ? sample.BodyRegion.Value.ToString()
            : IsObstacleStop(sample) ? "Ground/world" : IsOtherActorImpact(sample) ? "OtherActor" : "Miss";

        private static string Transition(PairRecord pair)
        {
            bool legacyTorso = IsRegion(pair.Legacy, BodyRegion.Torso);
            bool primaryTorso = IsRegion(pair.Primary, BodyRegion.Torso);
            bool legacyMiss = IsMiss(pair.Legacy);
            bool primaryMiss = IsMiss(pair.Primary);
            if (IsLeg(pair.Legacy) && primaryTorso) return "Leg -> Torso";
            if (IsLeg(pair.Legacy) && primaryMiss) return "Leg -> Miss";
            if (legacyTorso && primaryTorso) return "Torso -> Torso";
            if (legacyTorso && IsLeg(pair.Primary)) return "Torso -> Leg";
            if (legacyMiss && primaryTorso) return "Miss -> Torso";
            if (legacyMiss && primaryMiss) return "Miss -> Miss";
            return Category(pair.Legacy) + " -> " + Category(pair.Primary);
        }

        private static string Percent(int count, int denominator) => denominator == 0
            ? "n/a"
            : (100d * count / denominator).ToString("0.0", CultureInfo.InvariantCulture) + "%";

        private static bool IsTargetActorHit(ShotRecord sample) =>
            sample.BodyRegion.HasValue && sample.HitActorInstanceId == sample.TargetActorInstanceId;

        private static bool IsObstacleStop(ShotRecord sample) =>
            sample.PhysicalTermination == PhysicalShotTermination.SurfaceStopped ||
            sample.PhysicalTermination == PhysicalShotTermination.SurfaceLimitStopped ||
            sample.PhysicalTermination == PhysicalShotTermination.Impact &&
            !string.IsNullOrEmpty(sample.HitCollider) &&
            string.IsNullOrEmpty(sample.HitActorInstanceId);

        private static bool IsOtherActorImpact(ShotRecord sample) =>
            sample.PhysicalTermination == PhysicalShotTermination.Impact &&
            !string.IsNullOrEmpty(sample.HitActorInstanceId) &&
            sample.HitActorInstanceId != sample.TargetActorInstanceId;

        private static bool IsMiss(ShotRecord sample) =>
            !IsTargetActorHit(sample) && !IsObstacleStop(sample) && !IsOtherActorImpact(sample);

        private static void FinalizeRun()
        {
            string failure = SessionState.GetString(ErrorKey, string.Empty);
            if (!string.IsNullOrEmpty(failure))
                Debug.LogError("M41 F8C AIM BIAS EVIDENCE: FAIL\n- " + failure);
            SessionState.EraseString(PhaseKey);
            SessionState.EraseString(ErrorKey);
            if (Application.isBatchMode)
                EditorApplication.Exit(string.IsNullOrEmpty(failure) ? 0 : 1);
        }

        private static bool TrySpawn(
            long loadoutSeed,
            Vector3 position,
            Quaternion rotation,
            out ActorRuntimeIdentity identity,
            out ActorLoadoutResult loadout,
            out string error) =>
            ActorSpawnService.TrySpawnWithLoadoutSeed(
                ActorProfileId, position, rotation, loadoutSeed, out identity, out loadout, out error);

        private static bool TryReadAutomaticLoadout(
            ActorRuntimeIdentity actor,
            out ItemInstance firearmItem,
            out FirearmProfileDefinition firearm,
            out string loadedAmmoProfileId)
        {
            firearmItem = null;
            firearm = null;
            loadedAmmoProfileId = null;
            if (actor == null || !WeaponCombatService.TryGetEquippedWeapon(
                    actor.GetComponent<ActorItemOwnershipComponent>(), out firearmItem, out _,
                    out firearm, out _) || firearm == null || firearm.fire_mode != "automatic")
                return false;
            loadedAmmoProfileId = !string.IsNullOrWhiteSpace(firearmItem.LoadedAmmoProfileId)
                ? firearmItem.LoadedAmmoProfileId
                : FindCompatibleAmmoProfile(actor, firearm);
            return !string.IsNullOrWhiteSpace(loadedAmmoProfileId);
        }

        private static string FindCompatibleAmmoProfile(
            ActorRuntimeIdentity actor,
            FirearmProfileDefinition firearm)
        {
            ActorItemOwnershipComponent ownership = actor.GetComponent<ActorItemOwnershipComponent>();
            GameDatabase database = GameDataManager.Instance.Database;
            foreach (var entry in ownership.GetAllOwnedEntries())
            {
                ItemDefinition definition = database.GetItem(entry.DefinitionId);
                if (definition == null || string.IsNullOrWhiteSpace(definition.ammo_profile_id) ||
                    firearm.accepted_ammo_profile_ids == null ||
                    !firearm.accepted_ammo_profile_ids.Contains(definition.ammo_profile_id))
                    continue;
                return definition.ammo_profile_id;
            }
            return null;
        }

        private static int CompatibleAmmoQuantity(ActorRuntimeIdentity actor, string ammoProfileId)
        {
            if (actor == null || string.IsNullOrWhiteSpace(ammoProfileId))
                return 0;
            GameDatabase database = GameDataManager.Instance.Database;
            int total = 0;
            foreach (var entry in actor.GetComponent<ActorItemOwnershipComponent>().GetAllOwnedEntries())
            {
                ItemDefinition definition = database.GetItem(entry.DefinitionId);
                if (definition != null && definition.ammo_profile_id == ammoProfileId)
                    total += entry.Quantity;
            }
            return total;
        }

        private static ItemInstance EquippedFirearmItem(ActorRuntimeIdentity actor)
        {
            return actor != null && WeaponCombatService.TryGetEquippedWeapon(
                actor.GetComponent<ActorItemOwnershipComponent>(), out ItemInstance item, out _,
                out FirearmProfileDefinition _, out _)
                ? item
                : null;
        }

        private static bool HasEquippedArmor(ActorRuntimeIdentity actor)
        {
            GameDatabase database = GameDataManager.Instance.Database;
            ActorEquipmentComponent equipment = actor.GetComponent<ActorEquipmentComponent>();
            return equipment != null && equipment.Entries.Any(entry => entry?.Item != null &&
                !string.IsNullOrWhiteSpace(database.GetItem(entry.DefinitionId)?.armor_profile_id));
        }

        private static void ValidateHumanHitRegions(ActorRuntimeIdentity actor)
        {
            ActorCombatHitRegion[] regions = actor.GetComponentsInChildren<ActorCombatHitRegion>(false);
            Require(regions.Length == Regions.Length && regions.Select(value => value.Region).Distinct().Count() == Regions.Length,
                "F8C combat sandbox actor does not expose six distinct explicit human BodyRegions.");
            foreach (BodyRegion region in Regions)
                Require(regions.Count(value => value.Region == region) == 1,
                    $"F8C actor lacks exactly one {region} collider.");
            Require(regions.All(value => value.GetComponent<Collider>() != null && !value.GetComponent<Collider>().isTrigger),
                "F8C actor contains a missing or trigger combat region collider.");
        }

        private static void Place(ActorRuntimeIdentity actor, Vector3 position, Quaternion rotation)
        {
            ActorNavigationController navigation = actor.GetComponent<ActorNavigationController>();
            Require(navigation != null && navigation.Agent != null,
                "F8C actor has no existing ActorNavigationController/NavMeshAgent.");
            navigation.Stop();
            Require(navigation.Agent.isOnNavMesh && navigation.Agent.Warp(position),
                "F8C actor could not be placed through its existing NavMeshAgent.");
            actor.transform.rotation = rotation;
            navigation.Agent.nextPosition = position;
        }

        private static void ConfigureAffiliation(ActorRuntimeIdentity actor, string id, string[] hostiles)
        {
            ActorAffiliationComponent affiliation = actor.GetComponent<ActorAffiliationComponent>() ??
                                                     actor.gameObject.AddComponent<ActorAffiliationComponent>();
            Require(affiliation.TryConfigure(id, id, hostiles, out string error),
                $"F8C affiliation '{id}' was rejected: {error}");
        }

        private static void RemoveActor(ActorRuntimeIdentity actor)
        {
            if (actor == null)
                return;
            Require(ActorSpawnService.TryRemoveRuntimeRepresentationForRestore(
                    actor.ActorInstanceId, out string error),
                "F8C actor cleanup failed: " + error);
        }

        private static void TryRemoveActor(ActorRuntimeIdentity actor)
        {
            if (actor != null)
                ActorSpawnService.TryRemoveRuntimeRepresentationForRestore(actor.ActorInstanceId, out _);
        }

        private static string Csv(string value) =>
            "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

        private static string Number(float value) => value.ToString("0.######", CultureInfo.InvariantCulture);

        private static string Vector(Vector3 value) =>
            Number(value.x) + " " + Number(value.y) + " " + Number(value.z);

        private static void SetPhase(string value)
        {
            phase = value;
            SessionState.SetString(PhaseKey, value);
        }

        private static void ResetRunState()
        {
            Pairs.Clear();
            pendingLegacy = null;
            shooter = null;
            target = null;
            losBarrier = null;
            shooterSeedAttempts = 0;
            targetSeedAttempts = 0;
            probeWaitFrame = -1;
            pairIndex = 0;
            legacyCondition = true;
            lastObservedAttackCount = 0;
            shooterLoadoutSeed = 0L;
            targetLoadoutSeed = 0L;
            shooterLoadoutSignature = null;
            targetLoadoutSignature = null;
            firearmDefinitionId = null;
            firearmProfileId = null;
            ammoProfileId = null;
            magazineCapacity = 0;
            initialLoadedRounds = 0;
            csvPath = null;
            conditionStartedAt = 0d;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private sealed class ShotRecord
        {
            public int Run;
            public long AimSeed;
            public ulong AimSampleSequence;
            public string ShooterActorInstanceId;
            public string TargetActorInstanceId;
            public Vector3 ShooterPosition;
            public Vector3 TargetPosition;
            public float TargetDistance;
            public Vector3 AimPoint;
            public string AimPointSource;
            public string TorsoReferenceCollider;
            public Vector3 TorsoCenterReference;
            public float AimPointVerticalDelta;
            public Vector3 ShotOrigin;
            public Vector3 ShotDirection;
            public float Focus;
            public float SpreadDegrees;
            public float DefocusedSpreadDegrees;
            public float ShooterSpeed;
            public float TargetSpeed;
            public PhysicalShotTermination PhysicalTermination;
            public string HitCollider;
            public string HitActorInstanceId;
            public Vector3 HitEndPoint;
            public BodyRegion? BodyRegion;
            public CombatResolutionCode CombatCode;
            public string TerminalSurfaceProfileId;
            public int PenetratedSurfaceCount;
            public string FirearmDefinitionId;
            public string FirearmProfileId;
            public string AmmoProfileId;
            public int RemainingRounds;
        }

        private sealed class PairRecord
        {
            public long Seed;
            public ShotRecord Legacy;
            public ShotRecord Primary;
        }
    }
}
