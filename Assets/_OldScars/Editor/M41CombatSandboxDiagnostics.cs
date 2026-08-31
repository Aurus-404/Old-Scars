using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OldScars.Core;
using OldScars.Core.Actors;
using OldScars.Core.ApplicationShell;
using OldScars.Core.Combat;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Items;
using OldScars.Core.Persistence;
using OldScars.Core.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace OldScars.Editor
{
    [InitializeOnLoad]
    public static class M41CombatSandboxDiagnostics
    {
        private const string PendingKey = "OldScars.M41CombatSandbox.Pending";
        private const string StageKey = "OldScars.M41CombatSandbox.Stage";
        private const string FailureKey = "OldScars.M41CombatSandbox.Failure";
        private const string RootKey = "OldScars.M41CombatSandbox.Root";
        private const long WorldSeed = 941400441L;
        private const long SandboxSeed = 41400414L;
        private const string AimTargetAffiliation = "diagnostic_aim_target";
        private const string MeleeTargetAffiliation = "diagnostic_melee_target";

        private static WorldRuntimeSceneController runtime;
        private static SandboxNpcController sandbox;
        private static ActorRuntimeIdentity player;
        private static ActorRuntimeIdentity automaticRed;
        private static ActorRuntimeIdentity meleeRed;
        private static ActorRuntimeIdentity armoredBlue;
        private static ActorRuntimeIdentity corpseBlue;
        private static GameObject losWall;
        private static float stageStartedAt;
        private static int automaticAttackBaseline;
        private static int firstAutomaticAttackCount;
        private static float firstAutomaticAttackTime;
        private static int automaticHitBaseline;
        private static int playerWoundBaseline;
        private static float playerHealthBaseline;
        private static float rangeHealthBaseline;
        private static float rangeInitialDistance;
        private static int rangeAttackBaseline;
        private static bool rangeClosingObserved;
        private static bool meleeClosingObserved;
        private static int meleeAttackBaseline;
        private static float meleeHealthBaseline;
        private static bool corpseFinisherStarted;
        private static int corpseWoundBaseline;
        private static string corpseBelongingsBefore;

        static M41CombatSandboxDiagnostics()
        {
            EditorApplication.update -= Continue;
            EditorApplication.update += Continue;
        }

        public static void RunBatchWorldRuntime()
        {
            if (!Application.isBatchMode || EditorApplication.isCompiling)
                throw new InvalidOperationException("M41.4 diagnostics require compiled Unity batchmode.");

            string root = Path.Combine(Path.GetTempPath(), "OldScars_M41_4_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            SessionState.SetString(RootKey, root);
            SessionState.SetBool(PendingKey, true);
            SessionState.SetInt(StageKey, 0);
            SessionState.EraseString(FailureKey);
            WorldRuntimeTerrainDevelopmentSettings.SetDiagnosticSelectionOverride(
                WorldRuntimeTerrainDevelopmentSelection.VolumetricIndexedMarchingCubes);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("GameDataManager").AddComponent<GameDataManager>();
            EditorApplication.EnterPlaymode();
        }

        private static void Continue()
        {
            if (!SessionState.GetBool(PendingKey, false))
                return;
            try
            {
                WorldRuntimeTerrainDevelopmentSettings.SetDiagnosticSelectionOverride(
                    WorldRuntimeTerrainDevelopmentSelection.VolumetricIndexedMarchingCubes);
                if (EditorApplication.isPlaying)
                {
                    RunPlayStage();
                    return;
                }
                if (!EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetInt(StageKey, 0) == 99)
                    Finish(0);
                else if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    Finish(1, "M41.4 WorldRuntime diagnostic was interrupted before completion.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetString(FailureKey, exception.Message);
                SessionState.SetInt(StageKey, 99);
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorApplication.ExitPlaymode();
                else
                    Finish(1, exception.Message);
            }
        }

        private static void RunPlayStage()
        {
            int stage = SessionState.GetInt(StageKey, 0);
            if (stage == 0)
            {
                if (Time.frameCount < 5 || GameDataManager.Instance?.IsReady != true)
                    return;
                CreateWorldSessionAndLoadRuntime();
                SetStage(1);
                return;
            }

            runtime = UnityEngine.Object.FindAnyObjectByType<WorldRuntimeSceneController>();
            if (runtime == null || !runtime.GameplayStateReady || runtime.GameplayRuntimeComposition == null)
            {
                if (Time.time - stageStartedAt > 30f)
                    throw new InvalidOperationException("real WorldRuntime did not reach M41.4 gameplay readiness.");
                return;
            }

            switch (stage)
            {
                case 1:
                    SetupCorpusAndBlockedLos();
                    SetStage(2);
                    break;
                case 2:
                    VerifyBlockedLosThenOpen();
                    break;
                case 3:
                    ObserveAutomaticAcquisitionReaction();
                    break;
                case 4:
                    ObserveAutomaticFirstShot();
                    break;
                case 5:
                    VerifyAutomaticAimAndCadence();
                    break;
                case 6:
                    VerifyRangeClosing();
                    break;
                case 7:
                    VerifyMeleeAndCorpse();
                    break;
                case 8:
                    VerifyNpcAgainstPlayer();
                    break;
            }
        }

        private static void CreateWorldSessionAndLoadRuntime()
        {
            WorldSessionService.Close();
            var store = new PersistenceFileStore(SessionState.GetString(RootKey, string.Empty));
            WorldSessionOperationResult created = WorldSessionService.Create(
                "M41.4 Combat Sandbox", new OldScars.Core.World.WorldSeed(WorldSeed),
                WorldGenerationSettings.ResolvePreset(WorldSizePreset.Small),
                LandCoveragePreset.High, GameDataManager.Instance.LoadedContentSet, store);
            Require(created.Success, "Could not create M41.4 procedural WorldSession: " + created.Failure);
            SceneManager.LoadScene(WorldApplicationScenes.WorldRuntimeSceneName, LoadSceneMode.Single);
        }

        private static void SetupCorpusAndBlockedLos()
        {
            Require(runtime.TerrainSelection == WorldRuntimeTerrainDevelopmentSelection.VolumetricIndexedMarchingCubes &&
                    runtime.VolumetricTerrainController != null && runtime.VolumetricTerrainController.IsReady &&
                    runtime.VolumetricTerrainController.MesherBackend == DeformableTerrainMesherBackend.IndexedMarchingCubes,
                "M41.4 did not run inside real indexed marching-cubes WorldRuntime.");
            sandbox = runtime.GameplayRuntimeComposition.SandboxNpcController;
            player = runtime.PlayerComposition.PlayerIdentity;
            Require(sandbox != null && player != null && player.IsRegistered,
                "M41.4 runtime sandbox/player authorities are unavailable.");
            Require(sandbox.TrySetBaseSeed(SandboxSeed.ToString(), out string seedError),
                "Could not configure M41.4 sandbox seed: " + seedError);

            SpawnRequiredCombatActors();
            ActorAffiliationComponent playerAffiliation = player.GetComponent<ActorAffiliationComponent>();
            ActorAffiliationComponent blue = armoredBlue.GetComponent<ActorAffiliationComponent>();
            ActorAffiliationComponent otherBlue = corpseBlue.GetComponent<ActorAffiliationComponent>();
            ActorAffiliationComponent red = automaticRed.GetComponent<ActorAffiliationComponent>();
            ActorAffiliationComponent otherRed = meleeRed.GetComponent<ActorAffiliationComponent>();
            Require(playerAffiliation?.AffiliationId == SandboxNpcController.PlayerAffiliationId &&
                    blue?.AffiliationId == SandboxNpcController.BlueAffiliationId &&
                    red?.AffiliationId == SandboxNpcController.RedAffiliationId,
                "Player/Blue/Red runtime affiliations were not configured.");
            Require(red.GetDispositionToward(blue) == ActorDisposition.Hostile &&
                    red.GetDispositionToward(playerAffiliation) == ActorDisposition.Hostile &&
                    blue.GetDispositionToward(red) == ActorDisposition.Hostile &&
                    blue.GetDispositionToward(playerAffiliation) == ActorDisposition.Neutral &&
                    blue.GetDispositionToward(otherBlue) == ActorDisposition.Neutral &&
                    red.GetDispositionToward(otherRed) == ActorDisposition.Neutral,
                "M41.4 baseline disposition matrix is incorrect.");

            Require(blue.TryConfigure(AimTargetAffiliation, "Blue", Array.Empty<string>(), out string blueError),
                "Could not isolate aim target affiliation: " + blueError);
            Require(red.TryConfigure(SandboxNpcController.RedAffiliationId, "Red", new[] { AimTargetAffiliation }, out string redError),
                "Could not isolate Red acquisition relation: " + redError);
            Require(TryPlacePairWithClearPerception(automaticRed, armoredBlue, 12f, out string placementError),
                "Could not place blocked-LOS combat pair: " + placementError);
            automaticRed.GetComponent<ActorHealthComponent>().ApplyInitialHealth(500f, 500f);
            armoredBlue.GetComponent<ActorHealthComponent>().ApplyInitialHealth(500f, 500f);
            ConfigureDiagnosticConsciousnessResilience(armoredBlue);
            ConfigureDiagnosticConsciousnessResilience(corpseBlue);
            CreateLosWall(automaticRed, armoredBlue);
            ActorVisualPerceptionResult blocked = automaticRed.GetComponent<ActorVisualPerceptionService>().Evaluate(armoredBlue);
            Require(!blocked.Perceived && blocked.Reason == ActorVisualPerceptionReason.Occluded,
                "Diagnostic wall did not invalidate existing ActorVisualPerceptionService LOS.");
            automaticAttackBaseline = automaticRed.GetComponent<HumanEncounterAIController>().AttackCount;
            automaticRed.GetComponent<ActorThreatAcquisitionController>().enabled = true;
            stageStartedAt = Time.time;
        }

        private static void SpawnRequiredCombatActors()
        {
            for (int attempt = 0; attempt < 48 && (automaticRed == null || meleeRed == null); attempt++)
            {
                Require(sandbox.TrySpawnRedNpc(out SandboxNpcMetadata metadata, out string error),
                    "Red combat spawn failed: " + error);
                ActorRuntimeIdentity candidate = metadata.GetComponent<ActorRuntimeIdentity>();
                candidate.GetComponent<ActorThreatAcquisitionController>().enabled = false;
                candidate.GetComponent<HumanEncounterAIController>().ClearThreat("Diagnostic selection");
                WeaponCombatService.TryGetEquippedWeapon(
                    candidate.GetComponent<ActorItemOwnershipComponent>(), out _, out _,
                    out FirearmProfileDefinition firearm, out WeaponProfileDefinition melee);
                if (automaticRed == null && firearm?.fire_mode == "automatic")
                    automaticRed = candidate;
                else if (meleeRed == null && melee != null)
                    meleeRed = candidate;
                else
                    Remove(candidate);
            }
            Require(automaticRed != null && meleeRed != null,
                "Deterministic combat corpus did not produce automatic-firearm and melee Red actors.");

            for (int attempt = 0; attempt < 48 && (armoredBlue == null || corpseBlue == null); attempt++)
            {
                Require(sandbox.TrySpawnBlueNpc(out SandboxNpcMetadata metadata, out string error),
                    "Blue combat spawn failed: " + error);
                ActorRuntimeIdentity candidate = metadata.GetComponent<ActorRuntimeIdentity>();
                candidate.GetComponent<ActorThreatAcquisitionController>().enabled = false;
                candidate.GetComponent<HumanEncounterAIController>().ClearThreat("Diagnostic selection");
                if (armoredBlue == null && HasTorsoArmor(candidate))
                    armoredBlue = candidate;
                else if (corpseBlue == null)
                    corpseBlue = candidate;
                else
                    Remove(candidate);
            }
            Require(armoredBlue != null && corpseBlue != null && armoredBlue != corpseBlue,
                "Deterministic combat corpus did not produce distinct armored and corpse Blue actors.");
            Require(ActorRuntimeRegistry.ActiveCount >= 5,
                "M41.4 corpus did not retain multiple simultaneous actors plus Player.");
        }

        private static void ConfigureDiagnosticConsciousnessResilience(ActorRuntimeIdentity actor)
        {
            ActorProfileConsciousness source = GameDataManager.Instance.Database
                .GetActorProfile(actor.ActorProfileId).consciousness;
            var diagnostic = new ActorProfileConsciousness
            {
                consciousness_resilience = 100f,
                pain_tolerance = source.pain_tolerance,
                blunt_trauma_resistance = source.blunt_trauma_resistance,
                dazed_threshold = source.dazed_threshold,
                incapacitated_threshold = source.incapacitated_threshold,
                unconscious_threshold = source.unconscious_threshold,
                blood_pressure_start_fraction = source.blood_pressure_start_fraction,
                fatal_blood_fraction = source.fatal_blood_fraction,
                trauma_recovery_per_game_hour = source.trauma_recovery_per_game_hour,
                blood_recovery_per_game_hour = source.blood_recovery_per_game_hour,
                recovery_hysteresis = source.recovery_hysteresis
            };
            Require(actor.GetComponent<ActorConditionComponent>().TryConfigure(diagnostic, out string failure),
                "Could not configure diagnostic combat-target resilience: " + failure);
        }

        private static void VerifyBlockedLosThenOpen()
        {
            if (Time.time - stageStartedAt < 1f)
                return;
            ActorThreatAcquisitionController acquisition = automaticRed.GetComponent<ActorThreatAcquisitionController>();
            HumanEncounterAIController ai = automaticRed.GetComponent<HumanEncounterAIController>();
            Require(ai.Threat == null && acquisition.AcquisitionScanCount >= 2 &&
                    acquisition.PerceptionEvaluationCount >= 2 &&
                    acquisition.LastAcquisitionPerception.Reason == ActorVisualPerceptionReason.Occluded,
                "Hostile candidate was acquired through invalid wall/LOS or was not evaluated through perception.");
            Require(acquisition.RegistryBufferExpansionCount == 0 && acquisition.CandidateBufferExpansionCount == 0,
                "Acquisition expanded its reusable registry/candidate buffers for the bounded multi-NPC corpus.");
            UnityEngine.Object.Destroy(losWall);
            losWall = null;
            Physics.SyncTransforms();
            SetStage(3);
        }

        private static void ObserveAutomaticAcquisitionReaction()
        {
            HumanEncounterAIController ai = automaticRed.GetComponent<HumanEncounterAIController>();
            if (ai.Threat != armoredBlue)
            {
                if (Time.time - stageStartedAt > 3f)
                    throw new InvalidOperationException("Red did not automatically acquire perceived Blue through TryAssignThreat.");
                return;
            }
            Require(ai.AttackCount == automaticAttackBaseline &&
                    (ai.State == HumanEncounterAIState.Idle || ai.State == HumanEncounterAIState.Alerted),
                "Automatic acquisition skipped the configured reaction/alert delay.");
            SetStage(4);
        }

        private static void ObserveAutomaticFirstShot()
        {
            HumanEncounterAIController ai = automaticRed.GetComponent<HumanEncounterAIController>();
            if (ai.AttackCount == automaticAttackBaseline)
            {
                Require(Time.time - stageStartedAt < 6f,
                    "Automatic Red never fired after valid reaction, reload and engagement.");
                return;
            }
            firstAutomaticAttackCount = ai.AttackCount;
            firstAutomaticAttackTime = Time.time;
            SetStage(5);
        }

        private static void VerifyAutomaticAimAndCadence()
        {
            HumanEncounterAIController ai = automaticRed.GetComponent<HumanEncounterAIController>();
            int burstDelta = ai.AttackCount - firstAutomaticAttackCount;
            bool evidenceReady = burstDelta >= 5 && ai.PhysicalActorHitCount > 0 && ai.PhysicalMissCount > 0 &&
                                 ai.ArmoredActorHitCount > 0 && ai.CurrentFocus > 0.5f;
            if (!evidenceReady)
            {
                if (Time.time - firstAutomaticAttackTime > 8f)
                    throw new InvalidOperationException(
                        "Imperfect automatic aim evidence incomplete: Attacks=" + ai.AttackCount +
                        ", BurstDelta=" + burstDelta +
                        ", ActorHits=" + ai.PhysicalActorHitCount +
                        ", Misses=" + ai.PhysicalMissCount +
                        ", Obstacles=" + ai.PhysicalObstacleImpactCount +
                        ", ArmoredHits=" + ai.ArmoredActorHitCount +
                        ", Focus=" + ai.CurrentFocus.ToString("0.###") +
                        ", Spread=" + ai.CurrentSpreadDegrees.ToString("0.###") +
                        ", TargetHealth=" + armoredBlue.GetComponent<ActorHealthComponent>().CurrentHealth.ToString("0.###") + ".");
                return;
            }
            Require(Time.time - firstAutomaticAttackTime <= 8f &&
                    ai.CurrentSpreadDegrees >= 0.65f &&
                    ai.CurrentSpreadDegrees < ai.CurrentDefocusedSpreadDegrees &&
                    ai.AimSampleSequence >= (ulong)ai.AttackCount,
                "Focus/spread did not preserve a non-zero physical error cone with deterministic samples.");
            Require(burstDelta >= 5,
                "Automatic firing remained artificially limited by the 0.25s tactical decision interval.");
            SetupRangeScenario();
            SetStage(6);
        }

        private static void SetupRangeScenario()
        {
            HumanEncounterAIController ai = automaticRed.GetComponent<HumanEncounterAIController>();
            ActorThreatAcquisitionController acquisition = automaticRed.GetComponent<ActorThreatAcquisitionController>();
            ai.ClearThreat("Begin range scenario");
            acquisition.enabled = false;
            Require(armoredBlue.GetComponent<ActorAffiliationComponent>().TryConfigure(
                    SandboxNpcController.BlueAffiliationId, "Blue", Array.Empty<string>(), out string oldTargetError),
                "Could not release prior aim target affiliation: " + oldTargetError);
            Require(corpseBlue.GetComponent<ActorAffiliationComponent>().TryConfigure(
                    AimTargetAffiliation, "Blue", Array.Empty<string>(), out string newTargetError),
                "Could not isolate fresh range target: " + newTargetError);
            FirearmProfileDefinition firearm = EquippedFirearm(automaticRed);
            Require(firearm != null, "Automatic Red lost its firearm before the range scenario.");
            Require(TryPlacePairWithClearPerception(
                        automaticRed, corpseBlue, firearm.range + 8f, out string placementError),
                "Could not establish clear perception beyond firearm.range: " + placementError);
            corpseBlue.GetComponent<ActorHealthComponent>().ApplyInitialHealth(100f, 100f);
            rangeHealthBaseline = corpseBlue.GetComponent<ActorHealthComponent>().CurrentHealth;
            rangeAttackBaseline = ai.AttackCount;
            rangeInitialDistance = Vector3.Distance(automaticRed.transform.position, corpseBlue.transform.position);
            rangeClosingObserved = false;
            acquisition.enabled = true;
        }

        private static void VerifyRangeClosing()
        {
            HumanEncounterAIController ai = automaticRed.GetComponent<HumanEncounterAIController>();
            ActorHealthComponent targetHealth = corpseBlue.GetComponent<ActorHealthComponent>();
            FirearmProfileDefinition firearm = EquippedFirearm(automaticRed);
            float currentDistance = Vector3.Distance(automaticRed.transform.position, corpseBlue.transform.position);
            if (currentDistance > firearm.range)
            {
                Require(ai.AttackCount == rangeAttackBaseline && Mathf.Approximately(targetHealth.CurrentHealth, rangeHealthBaseline),
                    "Firearm resolved an attack/damage while the target was beyond firearm.range.");
            }
            rangeClosingObserved |= ai.IsClosingDistance &&
                                    automaticRed.GetComponent<ActorNavigationController>().State == ActorNavigationState.Moving;
            if (!rangeClosingObserved || rangeInitialDistance - currentDistance < 2f || ai.CurrentFocus <= 0.5f)
            {
                if (Time.time - stageStartedAt > 8f)
                    throw new InvalidOperationException("Firearm actor did not close distance/focus from beyond physical range.");
                return;
            }
            Require(ai.CurrentSpreadDegrees > 0f && ai.CurrentSpreadDegrees < ai.CurrentDefocusedSpreadDegrees,
                "Range/focus context did not reduce spread while preserving non-zero error.");
            SetupMeleeScenario();
            SetStage(7);
        }

        private static void SetupMeleeScenario()
        {
            automaticRed.GetComponent<ActorThreatAcquisitionController>().enabled = false;
            automaticRed.GetComponent<HumanEncounterAIController>().ClearThreat("Range evidence complete");
            automaticRed.GetComponent<ActorNavigationController>().Stop();

            ActorAffiliationComponent targetAffiliation = corpseBlue.GetComponent<ActorAffiliationComponent>();
            ActorAffiliationComponent attackerAffiliation = meleeRed.GetComponent<ActorAffiliationComponent>();
            Require(targetAffiliation.TryConfigure(
                    MeleeTargetAffiliation, "Blue", Array.Empty<string>(), out string targetError),
                "Could not configure isolated melee target disposition: " + targetError);
            Require(attackerAffiliation.TryConfigure(
                    SandboxNpcController.RedAffiliationId, "Red", new[] { MeleeTargetAffiliation }, out string attackerError),
                "Could not configure isolated melee attacker disposition: " + attackerError);
            Require(TryPlacePairWithClearPerception(meleeRed, corpseBlue, 3.5f, out string placementError),
                "Could not place melee closing pair: " + placementError);
            corpseBlue.GetComponent<ActorHealthComponent>().ApplyInitialHealth(100f, 100f);
            corpseBelongingsBefore = Belongings(corpseBlue);
            meleeAttackBaseline = meleeRed.GetComponent<HumanEncounterAIController>().AttackCount;
            meleeHealthBaseline = corpseBlue.GetComponent<ActorHealthComponent>().CurrentHealth;
            meleeClosingObserved = false;
            corpseFinisherStarted = false;
            meleeRed.GetComponent<ActorThreatAcquisitionController>().enabled = true;
        }

        private static void VerifyMeleeAndCorpse()
        {
            HumanEncounterAIController ai = meleeRed.GetComponent<HumanEncounterAIController>();
            ActorHealthComponent health = corpseBlue.GetComponent<ActorHealthComponent>();
            ActorMedicalStateComponent medical = corpseBlue.GetComponent<ActorMedicalStateComponent>();
            WeaponProfileDefinition melee = EquippedMelee(meleeRed);
            if (!corpseFinisherStarted && ai.AttackCount == meleeAttackBaseline)
            {
                Require(Mathf.Approximately(health.CurrentHealth, meleeHealthBaseline),
                    "Melee damaged its target before an in-range strike.");
                meleeClosingObserved |= ai.IsClosingDistance &&
                                        meleeRed.GetComponent<ActorNavigationController>().State == ActorNavigationState.Moving;
            }
            else if (!corpseFinisherStarted)
            {
                Require(meleeClosingObserved && ai.LastCombatResult.Combat.Region.HasValue &&
                        ai.LastCombatResult.Combat.FinalWoundType == WoundType.Blunt &&
                        medical.WoundCount > 0 && ai.CurrentTargetDistance <= melee.melee_range + 0.05f,
                    "Melee did not close to melee_range before localized impact.");
                SetupCorpseFinisher();
                return;
            }

            if (corpseFinisherStarted && !health.IsDead && medical.WoundCount > corpseWoundBaseline &&
                automaticRed.GetComponent<HumanEncounterAIController>().LastCombatResult.Combat.FinalWoundType == WoundType.Puncture)
            {
                string clockFailure = "WorldClock authority is unavailable.";
                Require(WorldClock.Current != null &&
                        WorldClock.Current.TryAdvanceGameTime(WorldClock.SecondsPerHour, out clockFailure),
                    "Firearm wound could not advance real M39 bleeding to death: " + clockFailure);
            }

            if (!corpseFinisherStarted || !health.IsDead)
            {
                if (Time.time - stageStartedAt > 12f)
                    throw new InvalidOperationException(
                        "Melee/firearm NPC combat did not reach real M39/M40 death: MeleeAttacks=" + ai.AttackCount +
                        ", Baseline=" + meleeAttackBaseline +
                        ", Distance=" + ai.CurrentTargetDistance.ToString("0.###") +
                        ", Health=" + health.CurrentHealth.ToString("0.###") +
                        ", Wounds=" + medical.WoundCount +
                        ", FinisherStarted=" + corpseFinisherStarted +
                        ", Navigation=" + meleeRed.GetComponent<ActorNavigationController>().State +
                        ", Velocity=" + meleeRed.GetComponent<ActorNavigationController>().Agent.velocity.magnitude.ToString("0.###") + ".");
                return;
            }
            Require(corpseBlue.LifecycleState == ActorLifecycleState.Dead &&
                    corpseBlue.GetComponent<ActorMedicalStateComponent>().WoundCount > 0,
                "Melee death did not use localized health/wounds/lifecycle.");
            LootableActorInventoryComponent corpse = corpseBlue.GetComponent<LootableActorInventoryComponent>();
            corpse.RefreshLootableState();
            Require(corpse.CanOpenStorage(out string corpseError) && Belongings(corpseBlue) == corpseBelongingsBefore,
                "Death/corpse continuity changed exact belongings: " + corpseError);
            SetupPlayerScenario();
            SetStage(8);
        }

        private static void SetupCorpseFinisher()
        {
            meleeRed.GetComponent<ActorThreatAcquisitionController>().enabled = false;
            meleeRed.GetComponent<HumanEncounterAIController>().ClearThreat("Melee range evidence complete");
            meleeRed.GetComponent<ActorNavigationController>().Stop();

            ActorAffiliationComponent automaticAffiliation = automaticRed.GetComponent<ActorAffiliationComponent>();
            Require(automaticAffiliation.TryConfigure(
                    SandboxNpcController.RedAffiliationId, "Red", new[] { MeleeTargetAffiliation }, out string affiliationError),
                "Could not configure isolated firearm corpse finisher: " + affiliationError);
            Require(TryPlacePairWithClearPerception(automaticRed, corpseBlue, 10f, out string placementError),
                "Could not place physical firearm corpse finisher: " + placementError);

            ActorMedicalStateComponent medical = corpseBlue.GetComponent<ActorMedicalStateComponent>();
            ActorHealthComponent health = corpseBlue.GetComponent<ActorHealthComponent>();
            corpseWoundBaseline = medical.WoundCount;
            health.ApplyInitialHealth(health.MaxHealth, 1f);
            automaticRed.GetComponent<HumanEncounterAIController>().ClearThreat("Begin physical corpse finisher");
            automaticRed.GetComponent<ActorThreatAcquisitionController>().enabled = true;
            corpseFinisherStarted = true;
            stageStartedAt = Time.time;
        }

        private static void SetupPlayerScenario()
        {
            meleeRed.GetComponent<ActorThreatAcquisitionController>().enabled = false;
            ActorAffiliationComponent red = automaticRed.GetComponent<ActorAffiliationComponent>();
            Require(red.TryConfigure(
                    SandboxNpcController.RedAffiliationId, "Red",
                    new[] { SandboxNpcController.PlayerAffiliationId }, out string error),
                "Could not restore Red hostile-to-Player relation: " + error);
            Require(TryPlaceAttackerNearTarget(automaticRed, player, 10f, out string placementError),
                "Could not place NPC↔Player engagement: " + placementError);
            HumanEncounterAIController ai = automaticRed.GetComponent<HumanEncounterAIController>();
            ai.ClearThreat("Begin player scenario");
            automaticHitBaseline = ai.PhysicalActorHitCount;
            ActorHealthComponent playerHealth = player.GetComponent<ActorHealthComponent>();
            playerHealthBaseline = playerHealth.CurrentHealth;
            playerWoundBaseline = player.GetComponent<ActorMedicalStateComponent>().WoundCount;
            automaticRed.GetComponent<ActorThreatAcquisitionController>().enabled = true;
        }

        private static void VerifyNpcAgainstPlayer()
        {
            HumanEncounterAIController ai = automaticRed.GetComponent<HumanEncounterAIController>();
            ActorHealthComponent playerHealth = player.GetComponent<ActorHealthComponent>();
            ActorMedicalStateComponent playerMedical = player.GetComponent<ActorMedicalStateComponent>();
            if (ai.Threat != player || ai.PhysicalActorHitCount <= automaticHitBaseline ||
                playerMedical.WoundCount <= playerWoundBaseline && Mathf.Approximately(playerHealth.CurrentHealth, playerHealthBaseline))
            {
                if (Time.time - stageStartedAt > 10f)
                    throw new InvalidOperationException("Red did not automatically acquire and physically damage Player.");
                return;
            }
            automaticRed.GetComponent<ActorThreatAcquisitionController>().enabled = false;
            ai.ClearThreat("M41.4 diagnostic complete");
            ActorThreatAcquisitionController acquisition = automaticRed.GetComponent<ActorThreatAcquisitionController>();
            Require(acquisition.RegistryBufferExpansionCount == 0 && acquisition.CandidateBufferExpansionCount == 0 &&
                    acquisition.AcquisitionScanCount < Time.frameCount,
                "Acquisition behaved like a per-frame/allocation-growing registry scan.");
            Debug.Log(
                "M41.4 Affiliation, Range-Aware Combat & Imperfect Aim Diagnostics: PASS\n" +
                "- Runtime: real WorldRuntime + VolumetricIndexedMarchingCubes\n" +
                "- Relations: Red->Blue/Player hostile; Blue->Player and same-team neutral\n" +
                "- LOS: occluded hostile rejected before automatic TryAssignThreat\n" +
                "- NPCvsNPC: auto firearm + melee localized combat; armor and corpse continuity observed\n" +
                "- NPCvsPlayer: automatic perceived acquisition and localized physical damage observed\n" +
                "- Range: no attack/damage beyond firearm.range; firearm/melee closing observed\n" +
                "- Aim: Hits=" + ai.PhysicalActorHitCount +
                " Misses=" + ai.PhysicalMissCount +
                " ObstacleImpacts=" + ai.PhysicalObstacleImpactCount +
                " ArmoredHits=" + ai.ArmoredActorHitCount +
                " Focus=" + ai.CurrentFocus.ToString("0.###") +
                " Spread=" + ai.CurrentSpreadDegrees.ToString("0.###") + "deg\n" +
                "- Acquisition: Scans=" + acquisition.AcquisitionScanCount +
                " Visits=" + acquisition.RegistryCandidateVisitCount +
                " LOSChecks=" + acquisition.PerceptionEvaluationCount +
                " BufferExpansions=0");
            SessionState.SetInt(StageKey, 99);
            EditorApplication.ExitPlaymode();
        }

        private static bool TryPlacePairWithClearPerception(
            ActorRuntimeIdentity observer,
            ActorRuntimeIdentity target,
            float requestedDistance,
            out string error)
        {
            Vector3 center = player.transform.position;
            ActorVisualPerceptionReason lastReason = ActorVisualPerceptionReason.LineOfSightMiss;
            int sampledPairs = 0;
            for (int index = 0; index < 24; index++)
            {
                float angle = index * 15f;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 lateral = Vector3.Cross(Vector3.up, direction).normalized;
                Vector3 pairCenter = center + lateral * 18f;
                Vector3 observerRequest = pairCenter - direction * requestedDistance * 0.5f;
                Vector3 targetRequest = pairCenter + direction * requestedDistance * 0.5f;
                if (!NavMesh.SamplePosition(observerRequest, out NavMeshHit observerHit, 6f, NavMesh.AllAreas) ||
                    !NavMesh.SamplePosition(targetRequest, out NavMeshHit targetHit, 6f, NavMesh.AllAreas))
                    continue;
                sampledPairs++;
                Place(observer, observerHit.position, Quaternion.LookRotation(direction));
                Place(target, targetHit.position, Quaternion.LookRotation(-direction));
                Physics.SyncTransforms();
                float actualDistance = Vector3.Distance(observer.transform.position, target.transform.position);
                if (actualDistance < requestedDistance - 2f)
                    continue;
                ActorVisualPerceptionResult perception = observer.GetComponent<ActorVisualPerceptionService>().Evaluate(target);
                lastReason = perception.Reason;
                if (perception.Perceived)
                {
                    error = null;
                    return true;
                }
            }
            error = "No complete NavMesh pair with clear existing perception was found at " + requestedDistance +
                    "m (sampled=" + sampledPairs + ", lastReason=" + lastReason + ").";
            return false;
        }

        private static bool TryPlaceAttackerNearTarget(
            ActorRuntimeIdentity attacker,
            ActorRuntimeIdentity target,
            float requestedDistance,
            out string error)
        {
            for (int index = 0; index < 24; index++)
            {
                Vector3 direction = Quaternion.Euler(0f, index * 15f, 0f) * Vector3.forward;
                if (!NavMesh.SamplePosition(target.transform.position - direction * requestedDistance,
                        out NavMeshHit hit, 5f, NavMesh.AllAreas))
                    continue;
                Place(attacker, hit.position, Quaternion.LookRotation(direction));
                Physics.SyncTransforms();
                if (attacker.GetComponent<ActorVisualPerceptionService>().Evaluate(target).Perceived)
                {
                    error = null;
                    return true;
                }
            }
            error = "No clear nearby NavMesh attacker position was found.";
            return false;
        }

        private static void Place(ActorRuntimeIdentity actor, Vector3 position, Quaternion rotation)
        {
            actor.GetComponent<HumanEncounterAIController>().ClearThreat("Diagnostic placement");
            ActorNavigationController navigation = actor.GetComponent<ActorNavigationController>();
            navigation.Stop();
            Require(navigation.Agent != null && navigation.Agent.isOnNavMesh && navigation.Agent.Warp(position),
                "Diagnostic actor could not warp through its existing NavMeshAgent: " + actor.ActorInstanceId);
            actor.transform.rotation = rotation;
            navigation.Agent.nextPosition = position;
        }

        private static void CreateLosWall(ActorRuntimeIdentity observer, ActorRuntimeIdentity target)
        {
            Vector3 observerCenter = observer.GetComponent<Collider>().bounds.center;
            Vector3 targetCenter = target.GetComponent<Collider>().bounds.center;
            Vector3 direction = targetCenter - observerCenter;
            losWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            losWall.name = "M41.4 Diagnostic LOS Wall";
            losWall.transform.SetPositionAndRotation(
                Vector3.Lerp(observerCenter, targetCenter, 0.5f),
                Quaternion.LookRotation(direction.normalized));
            losWall.transform.localScale = new Vector3(4f, 3f, 0.6f);
            Physics.SyncTransforms();
        }

        private static bool HasTorsoArmor(ActorRuntimeIdentity actor)
        {
            GameDatabase database = GameDataManager.Instance.Database;
            ActorEquipmentComponent equipment = actor.GetComponent<ActorEquipmentComponent>();
            return equipment.Entries.Any(entry => entry?.Item != null &&
                !string.IsNullOrWhiteSpace(database.GetItem(entry.DefinitionId)?.armor_profile_id) &&
                equipment.GetSlotsOccupiedBy(entry.Item.InstanceId).Contains("core:torso_outer"));
        }

        private static FirearmProfileDefinition EquippedFirearm(ActorRuntimeIdentity actor)
        {
            WeaponCombatService.TryGetEquippedWeapon(
                actor.GetComponent<ActorItemOwnershipComponent>(), out _, out _,
                out FirearmProfileDefinition firearm, out _);
            return firearm;
        }

        private static WeaponProfileDefinition EquippedMelee(ActorRuntimeIdentity actor)
        {
            WeaponCombatService.TryGetEquippedWeapon(
                actor.GetComponent<ActorItemOwnershipComponent>(), out _, out _, out _,
                out WeaponProfileDefinition melee);
            return melee;
        }

        private static string Belongings(ActorRuntimeIdentity actor)
        {
            ActorItemOwnershipComponent ownership = actor.GetComponent<ActorItemOwnershipComponent>();
            ActorEquipmentComponent equipment = actor.GetComponent<ActorEquipmentComponent>();
            string failure = null;
            Require(ownership != null && equipment != null && ownership.ValidateUniqueOwnership(out failure),
                "Actor ownership invalid while comparing corpse belongings: " + failure);
            return string.Join("|", ownership.GetAllOwnedEntries().Where(entry => entry?.Item != null)
                .Select(entry => entry.Item.InstanceId + ":" + entry.DefinitionId + ":" + entry.Quantity + ":" +
                                 string.Join("+", equipment.GetSlotsOccupiedBy(entry.Item.InstanceId)))
                .OrderBy(value => value, StringComparer.Ordinal));
        }

        private static void Remove(ActorRuntimeIdentity actor)
        {
            if (actor != null)
                ActorSpawnService.TryRemoveRuntimeRepresentationForRestore(actor.ActorInstanceId, out _);
        }

        private static void SetStage(int stage)
        {
            SessionState.SetInt(StageKey, stage);
            stageStartedAt = Time.time;
        }

        private static void Finish(int exitCode, string immediateFailure = null)
        {
            string failure = string.IsNullOrWhiteSpace(immediateFailure)
                ? SessionState.GetString(FailureKey, string.Empty)
                : immediateFailure;
            SessionState.SetBool(PendingKey, false);
            SessionState.SetInt(StageKey, 0);
            SessionState.EraseString(FailureKey);
            WorldRuntimeTerrainDevelopmentSettings.ClearDiagnosticSelectionOverride();
            WorldSessionService.Close();
            string root = SessionState.GetString(RootKey, string.Empty);
            SessionState.EraseString(RootKey);
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
                Directory.Delete(root, true);
            runtime = null;
            sandbox = null;
            player = null;
            automaticRed = null;
            meleeRed = null;
            armoredBlue = null;
            corpseBlue = null;
            losWall = null;
            if (!string.IsNullOrWhiteSpace(failure))
                Debug.LogError("M41.4 Affiliation, Range-Aware Combat & Imperfect Aim Diagnostics: FAIL\n" + failure);
            EditorApplication.Exit(string.IsNullOrWhiteSpace(failure) && exitCode == 0 ? 0 : 1);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
