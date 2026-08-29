using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using OldScars.Core;
using OldScars.Core.Actors;
using OldScars.Core.Data;
using OldScars.Core.Items;
using OldScars.Core.Persistence;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OldScars.Editor
{
    [InitializeOnLoad]
    public static class M38NeedsWorldClockRecoveryDiagnostics
    {
        private const string Menu = "Old Scars/Diagnostics/Actors/Run M38.1 Needs World Clock & Recovery";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string PhaseKey = "OldScars.M38.1.NeedsClock.Phase";
        private const string RootKey = "OldScars.M38.1.NeedsClock.Root";
        private const string ErrorKey = "OldScars.M38.1.NeedsClock.Error";
        private const string EnterA = "enter_a";
        private const string ExitA = "exit_a";
        private const string EnterB = "enter_b";
        private const string Finish = "finish";
        private const string InitialSlot = "m38_1_initial";
        private const string TargetSlot = "m38_1_known_clock_needs";
        private const string LegacySlot = "m38_1_legacy_without_clock";
        private const string LegacyStaminaSlot = "m38_1_legacy_without_stamina";
        private const string InvalidSlot = "m38_1_invalid_clock";
        private const string InvalidStaminaSlot = "m38_1_invalid_stamina";
        private const double KnownElapsedGameSeconds =
            WorldClock.SecondsPerDay * 2d + WorldClock.SecondsPerHour * 18d + WorldClock.SecondsPerMinute * 42d;

        static M38NeedsWorldClockRecoveryDiagnostics()
        {
            EditorApplication.update += Continue;
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("M38.1 diagnostics require idle Edit Mode.");

            ClearSession();
            string root = Path.Combine(Path.GetTempPath(), "OldScars_M38_1_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            SessionState.SetString(RootKey, root);
            SessionState.SetString(ErrorKey, string.Empty);
            SessionState.SetString(PhaseKey, EnterA);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static bool ValidateRun()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;
        }

        private static void Continue()
        {
            string phase = SessionState.GetString(PhaseKey, string.Empty);
            if (string.IsNullOrWhiteSpace(phase))
                return;

            if ((phase == EnterA || phase == EnterB) && EditorApplication.isPlaying && WorldClock.Current != null)
                WorldClock.Current.AdvanceDuringGameplay = false;

            if (phase == EnterA && Ready())
            {
                ExecutePlayPhase(RunSessionA, ExitA);
                return;
            }
            if (phase == ExitA && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                SessionState.SetString(PhaseKey, EnterB);
                EditorApplication.EnterPlaymode();
                return;
            }
            if (phase == EnterB && Ready())
            {
                ExecutePlayPhase(RunSessionB, Finish);
                return;
            }
            if (phase == Finish && !EditorApplication.isPlayingOrWillChangePlaymode)
                FinalizeRun();
        }

        private static bool Ready()
        {
            return EditorApplication.isPlaying && Time.frameCount >= 5 && WorldClock.Current != null &&
                   GameDataManager.Instance != null && GameDataManager.Instance.IsReady;
        }

        private static void ExecutePlayPhase(Action action, string nextPhase)
        {
            try
            {
                action();
                SessionState.SetString(PhaseKey, nextPhase);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetString(ErrorKey, exception.Message);
                SessionState.SetString(PhaseKey, Finish);
            }
            EditorApplication.ExitPlaymode();
        }

        private static void RunSessionA()
        {
            WorldClock clock = Clock();
            ActorNeedsComponent needs = PlayerNeeds();
            ActorStaminaComponent stamina = PlayerStamina(needs);
            ActorHealthComponent health = needs.GetComponent<ActorHealthComponent>();
            Require(health != null && !health.IsDead, "Player must be Alive for M38.1 diagnostics.");

            CurrentSliceSaveData initial = Capture("initial bootstrap");
            Write(Store(), InitialSlot, initial);

            RunClockAndNeedProgression(clock, needs);
            RunConsumableRegression(needs, health);
            RunRestAndDeadActorRegression(clock, needs, health);
            RunStaminaRegression(needs, stamina);
            float[] expectedDebugMultipliers = { 1f, 2f, 3f, 5f, 10f, 20f, 50f, 100f };
            for (int index = 0; index < expectedDebugMultipliers.Length; index++)
            {
                float multiplier = expectedDebugMultipliers[index];
                Require(clock.TrySetDebugTimeMultiplier(multiplier, out _) &&
                        Near((float)clock.GameSecondsPerRealSecond,
                            (float)(WorldClock.DefaultGameSecondsPerRealSecond * multiplier)),
                    $"WorldClock did not apply the {multiplier:0}x debug multiplier relative to its authored baseline.");
            }
            RunDebugMultiplierFrameRegression(clock);

            RestoreClock(clock, KnownElapsedGameSeconds);
            SetNeed(needs, "hunger", 61f);
            SetNeed(needs, "thirst", 47f);
            SetStamina(stamina, 37.5f);
            CurrentSliceSaveData target = Capture("known Day 3 clock and needs");
            Require(target.worldClock != null && Near(target.worldClock.elapsedGameSeconds, KnownElapsedGameSeconds) &&
                    target.player.needs.Length == 2 && target.player.stamina != null &&
                    Near(target.player.stamina.currentStamina, 37.5f),
                "Known World Clock/needs/stamina were not captured into the Current Slice DTO.");
            Write(Store(), TargetSlot, target);

            JObject legacyPayload = (JObject)CurrentSliceSnapshotService.ToPayload(target).DeepClone();
            Require(legacyPayload.Remove("worldClock"), "Could not construct legacy schema-v1 payload without World Clock.");
            PersistenceWriteResult legacyWrite = Store().Write(LegacySlot, legacyPayload);
            Require(legacyWrite.Success, "Legacy-compatible payload write failed: " + legacyWrite.Failure);

            JObject legacyStaminaPayload = (JObject)CurrentSliceSnapshotService.ToPayload(target).DeepClone();
            Require(legacyStaminaPayload["player"] is JObject legacyPlayer && legacyPlayer.Remove("stamina"),
                "Could not construct legacy schema-v1 payload without stamina.");
            PersistenceWriteResult legacyStaminaWrite = Store().Write(LegacyStaminaSlot, legacyStaminaPayload);
            Require(legacyStaminaWrite.Success, "Legacy-stamina fixture write failed: " + legacyStaminaWrite.Failure);

            JObject invalidPayload = (JObject)CurrentSliceSnapshotService.ToPayload(target).DeepClone();
            invalidPayload["worldClock"]["elapsedGameSeconds"] = -1d;
            PersistenceWriteResult invalidWrite = Store().Write(InvalidSlot, invalidPayload);
            Require(invalidWrite.Success, "Invalid-clock fixture write failed: " + invalidWrite.Failure);

            JObject invalidStaminaPayload = (JObject)CurrentSliceSnapshotService.ToPayload(target).DeepClone();
            invalidStaminaPayload["player"]["stamina"]["currentStamina"] = -1f;
            PersistenceWriteResult invalidStaminaWrite = Store().Write(InvalidStaminaSlot, invalidStaminaPayload);
            Require(invalidStaminaWrite.Success, "Invalid-stamina fixture write failed: " + invalidStaminaWrite.Failure);
        }

        private static void RunSessionB()
        {
            WorldClock clock = Clock();
            ActorNeedsComponent needs = PlayerNeeds();
            ActorStaminaComponent stamina = PlayerStamina(needs);
            Require(clock.Day == 1 && clock.ElapsedGameSeconds < WorldClock.SecondsPerHour,
                $"Fresh-session World Clock did not bootstrap on Day 1; elapsed={clock.ElapsedGameSeconds:R}.");
            Require(Near(clock.DebugTimeMultiplier, 1f), "Debug WorldClock multiplier leaked into a fresh runtime session.");

            CurrentSliceSaveData target = Read(TargetSlot);
            CurrentSliceLoadResult targetLoad = CurrentSliceLoadService.Load(TargetSlot, Store());
            Require(targetLoad.Success, "Fresh-session World Clock/needs load failed: " + targetLoad.Failure);
            Require(Near(clock.ElapsedGameSeconds, KnownElapsedGameSeconds) && clock.Day == 3 && clock.Hour == 18 && clock.Minute == 42,
                $"Fresh-session load did not restore Day 3 18:42 exactly; got {clock.DisplayTime} ({clock.ElapsedGameSeconds:R}).");
            Require(Near(needs.GetNeedValue("hunger"), 61f) && Near(needs.GetNeedValue("thirst"), 47f),
                "Fresh-session load did not restore Hunger/Thirst exactly.");
            Require(Near(stamina.CurrentStamina, 37.5f), "Fresh-session load did not restore stamina exactly.");
            AssertEquivalent(target, Capture("post fresh-session target load"), "fresh-session World Clock/needs round-trip");
            AssertActorLifecycle(target, "fresh-session M38 actor lifecycle regression");

            CurrentSliceSaveData legacy = Read(LegacySlot);
            Require(legacy.worldClock != null && Near(legacy.worldClock.elapsedGameSeconds, WorldClock.DefaultElapsedGameSeconds),
                "Legacy schema-v1 World Clock absence did not normalize to Day 1 00:00.");
            CurrentSliceLoadResult legacyLoad = CurrentSliceLoadService.Load(LegacySlot, Store());
            Require(legacyLoad.Success && Near(clock.ElapsedGameSeconds, WorldClock.DefaultElapsedGameSeconds),
                "Legacy schema-v1 load did not apply the documented default World Clock: " + legacyLoad.Failure);
            Require(Near(needs.GetNeedValue("hunger"), 61f) && Near(needs.GetNeedValue("thirst"), 47f),
                "Legacy schema-v1 load did not preserve its represented needs.");
            Require(Near(stamina.CurrentStamina, 37.5f),
                "Legacy World Clock omission did not alter represented stamina.");

            CurrentSliceSaveData legacyStamina = Read(LegacyStaminaSlot);
            Require(legacyStamina.player.stamina != null && Near(legacyStamina.player.stamina.currentStamina, stamina.MaximumStamina),
                "Legacy schema-v1 stamina omission did not normalize to initial maximum stamina.");
            CurrentSliceLoadResult legacyStaminaLoad = CurrentSliceLoadService.Load(LegacyStaminaSlot, Store());
            Require(legacyStaminaLoad.Success && Near(stamina.CurrentStamina, stamina.MaximumStamina),
                "Legacy schema-v1 stamina load did not apply the documented full-reserve default: " + legacyStaminaLoad.Failure);

            CurrentSliceSaveData beforeInvalid = Capture("pre invalid-clock preflight");
            CurrentSliceLoadResult invalidLoad = CurrentSliceLoadService.Load(InvalidSlot, Store());
            Require(invalidLoad.FailureCode == CurrentSliceLoadFailureCode.SemanticPreflightFailed && !invalidLoad.MutationStarted,
                "Invalid World Clock was not rejected before mutation.");
            AssertEquivalent(beforeInvalid, Capture("post invalid-clock preflight"), "invalid World Clock no-mutation preflight");

            CurrentSliceSaveData beforeInvalidStamina = Capture("pre invalid-stamina preflight");
            CurrentSliceLoadResult invalidStaminaLoad = CurrentSliceLoadService.Load(InvalidStaminaSlot, Store());
            Require(invalidStaminaLoad.FailureCode == CurrentSliceLoadFailureCode.SemanticPreflightFailed &&
                    !invalidStaminaLoad.MutationStarted,
                "Invalid stamina was not rejected before mutation.");
            AssertEquivalent(beforeInvalidStamina, Capture("post invalid-stamina preflight"), "invalid stamina no-mutation preflight");

            RestoreClock(clock, WorldClock.SecondsPerDay * 5d + 321d);
            SetNeed(needs, "hunger", 72f);
            SetNeed(needs, "thirst", 55f);
            SetStamina(stamina, 41f);
            CurrentSliceSaveData beforeFault = Capture("pre post-runtime-state fault");
            CurrentSliceLoadService.DiagnosticInjectFailureAfterRuntimeStateRestore = true;
            CurrentSliceLoadResult fault = CurrentSliceLoadService.Load(TargetSlot, Store());
            Require(fault.FailureCode == CurrentSliceLoadFailureCode.ApplyFailed &&
                    fault.RollbackAttempted && fault.RollbackSucceeded,
                "Post-clock/needs fault did not report successful rollback: " + fault.Failure);
            CurrentSliceSaveData afterFault = Capture("post World Clock/needs rollback");
            AssertEquivalent(beforeFault, afterFault, "World Clock/needs rollback");
            Require(Near(clock.ElapsedGameSeconds, beforeFault.worldClock.elapsedGameSeconds) &&
                    Near(needs.GetNeedValue("hunger"), Need(beforeFault, "hunger")) &&
                    Near(needs.GetNeedValue("thirst"), Need(beforeFault, "thirst")) &&
                    Near(stamina.CurrentStamina, beforeFault.player.stamina.currentStamina),
                "Rollback did not restore World Clock, needs and stamina exactly.");

            CurrentSliceSaveData initial = Read(InitialSlot);
            CurrentSliceLoadResult cleanup = CurrentSliceLoadService.Load(InitialSlot, Store());
            Require(cleanup.Success, "Initial-state cleanup failed: " + cleanup.Failure);
            AssertEquivalent(initial, Capture("initial cleanup"), "M38.1 diagnostic cleanup");
            CurrentSliceLoadService.DiagnosticInjectFailureAfterRuntimeStateRestore = false;
        }

        private static void RunClockAndNeedProgression(WorldClock clock, ActorNeedsComponent needs)
        {
            RestoreClock(clock, WorldClock.DefaultElapsedGameSeconds);
            SetNeed(needs, "hunger", 90f);
            SetNeed(needs, "thirst", 90f);
            float hungerBefore = needs.GetNeedValue("hunger");
            float thirstBefore = needs.GetNeedValue("thirst");
            double duration = WorldClock.SecondsPerHour * 2d;
            Require(clock.TryAdvanceGameTime(duration, out string failure), "Known clock advance failed: " + failure);
            Require(Near(clock.ElapsedGameSeconds, duration) && clock.Day == 1 && clock.Hour == 2 && clock.Minute == 0,
                "World Clock Day/HH:MM derivation is incoherent after known advance.");
            Require(Near(needs.GetNeedValue("hunger"),
                    hungerBefore - (float)(needs.Profile.GetNeed("hunger").DecayPerGameHour * 2d)) &&
                    Near(needs.GetNeedValue("thirst"),
                    thirstBefore - (float)(needs.Profile.GetNeed("thirst").DecayPerGameHour * 2d)),
                "Hunger/Thirst did not advance exactly once from the World Clock game-time delta.");

            double unchanged = clock.ElapsedGameSeconds;
            Require(!clock.TryAdvanceGameTime(double.NaN, out _) &&
                    !clock.TryAdvanceGameTime(-1d, out _) &&
                    Near(clock.ElapsedGameSeconds, unchanged),
                "Invalid game-time durations mutated the World Clock.");
            Require(!clock.TryRestoreElapsedGameSeconds(WorldClock.MaxElapsedGameSeconds + 1d, out _) &&
                    Near(clock.ElapsedGameSeconds, unchanged),
                "Out-of-range persistence time mutated the World Clock.");
        }

        private static void RunDebugMultiplierFrameRegression(WorldClock clock)
        {
            bool previousAdvanceDuringGameplay = clock.AdvanceDuringGameplay;
            double baselineDelta = 0d;
            var observations = new List<string>();
            try
            {
                clock.AdvanceDuringGameplay = true;
                float[] multipliers = { 1f, 2f, 10f, 100f };
                for (int index = 0; index < multipliers.Length; index++)
                {
                    float multiplier = multipliers[index];
                    RestoreClock(clock, WorldClock.DefaultElapsedGameSeconds);
                    Require(clock.TrySetDebugTimeMultiplier(multiplier, out string multiplierFailure),
                        "Could not set frame-proof multiplier: " + multiplierFailure);
                    clock.SendMessage("Update", SendMessageOptions.RequireReceiver);
                    double delta = clock.ElapsedGameSeconds;
                    Require(delta > 0d, $"WorldClock Update did not advance at {multiplier:0}x.");
                    if (multiplier == 1f)
                        baselineDelta = delta;
                    else
                        Require(Near(delta, baselineDelta * multiplier, Math.Max(0.001d, baselineDelta * multiplier * 0.01d)),
                            $"WorldClock Update advanced {delta:R} at {multiplier:0}x; expected approximately {(baselineDelta * multiplier):R}.");
                    observations.Add($"{multiplier:0}x={delta:R}");
                }
            }
            finally
            {
                clock.ResetDebugTimeMultiplier();
                clock.AdvanceDuringGameplay = previousAdvanceDuringGameplay;
            }
            Debug.Log("[WorldClock][DEBUG_MULTIPLIER_FRAME_PROOF] " + string.Join(", ", observations));
        }

        private static void RunConsumableRegression(ActorNeedsComponent needs, ActorHealthComponent health)
        {
            GameObject sourceObject = new GameObject("M38.1 Consumable Source");
            try
            {
                InventoryComponent source = sourceObject.AddComponent<InventoryComponent>();
                ItemInstance water = source.AddItemByDefinitionId("core:water_bottle_01", 2);
                ItemInstance food = source.AddItemByDefinitionId("core:food_ration_01", 2);
                Require(water != null && food != null, "Food/Water diagnostic setup failed.");
                SetNeed(needs, "hunger", 20f);
                SetNeed(needs, "thirst", 25f);

                Require(source.TryGetEntryByInstanceId(water.InstanceId, out int waterIndex, out _) &&
                        source.TryGetEntryByInstanceId(food.InstanceId, out int foodIndex, out _),
                    "Food/Water entries were not indexed in the real InventoryComponent.");
                InventoryItemUseResult waterUse = InventoryItemUseService.TryUseItem(source, waterIndex, needs, health);
                Require(waterUse.Success && Quantity(source, water.InstanceId) == 1 && Near(needs.GetNeedValue("thirst"), 60f),
                    "Water did not restore thirst and consume exactly one unit: " + waterUse.Message);
                source.TryGetEntryByInstanceId(food.InstanceId, out foodIndex, out _);
                InventoryItemUseResult foodUse = InventoryItemUseService.TryUseItem(source, foodIndex, needs, health);
                Require(foodUse.Success && Quantity(source, food.InstanceId) == 1 && Near(needs.GetNeedValue("hunger"), 55f),
                    "Food did not restore hunger and consume exactly one unit: " + foodUse.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sourceObject);
            }
        }

        private static void RunRestAndDeadActorRegression(
            WorldClock clock,
            ActorNeedsComponent playerNeeds,
            ActorHealthComponent playerHealth)
        {
            RestoreClock(clock, WorldClock.SecondsPerDay);
            SetNeed(playerNeeds, "hunger", 80f);
            SetNeed(playerNeeds, "thirst", 80f);
            float healthBefore = playerHealth.CurrentHealth;
            ActorRestResult sleep = ActorRestService.TryRest(playerNeeds, WorldClock.SecondsPerHour * 8d);
            Require(sleep.Success && Near(sleep.AdvancedGameSeconds, WorldClock.SecondsPerHour * 8d) &&
                    Near(clock.ElapsedGameSeconds, WorldClock.SecondsPerDay + WorldClock.SecondsPerHour * 8d),
                "Sleep did not advance the World Clock by exactly eight game hours: " + sleep.Message);
            Require(Near(playerNeeds.GetNeedValue("hunger"), 65.6f) && Near(playerNeeds.GetNeedValue("thirst"), 56f),
                "Sleep did not apply the same eight-hour Hunger/Thirst progression.");
            Require(Near(playerHealth.CurrentHealth, healthBefore) && !sleep.AdditionalRecoveryApplied,
                "M38.1 rest applied health/wound recovery outside its contract.");

            GameObject deadObject = new GameObject("M38.1 Dead Actor");
            try
            {
                ActorHealthComponent deadHealth = deadObject.AddComponent<ActorHealthComponent>();
                ActorNeedsComponent deadNeeds = deadObject.AddComponent<ActorNeedsComponent>();
                SetNeed(deadNeeds, "hunger", 70f);
                SetNeed(deadNeeds, "thirst", 65f);
                deadNeeds.enabled = false;
                double beforeInactiveRest = clock.ElapsedGameSeconds;
                ActorRestResult inactiveRest = ActorRestService.TryRest(deadNeeds, WorldClock.SecondsPerHour);
                Require(!inactiveRest.Success && inactiveRest.FailureCode == ActorRestFailureCode.ActorInactive &&
                        Near(clock.ElapsedGameSeconds, beforeInactiveRest),
                    "Inactive actor rest was not rejected without advancing time.");
                deadNeeds.enabled = true;
                Require(clock.TryAdvanceGameTime(WorldClock.SecondsPerHour, out string reenableFailure),
                    "Re-enabled needs clock advance failed: " + reenableFailure);
                Require(deadNeeds.GetNeedValue("hunger") < 70f && deadNeeds.GetNeedValue("thirst") < 65f,
                    "Re-enabled ActorNeedsComponent did not reconnect to the World Clock.");
                SetNeed(deadNeeds, "hunger", 70f);
                SetNeed(deadNeeds, "thirst", 65f);
                deadHealth.Kill();
                double beforeAdvance = clock.ElapsedGameSeconds;
                Require(clock.TryAdvanceGameTime(WorldClock.SecondsPerHour, out string failure),
                    "Dead-actor clock advance failed: " + failure);
                Require(Near(deadNeeds.GetNeedValue("hunger"), 70f) && Near(deadNeeds.GetNeedValue("thirst"), 65f),
                    "Dead actor continued accumulating needs progression.");
                double beforeRejectedRest = clock.ElapsedGameSeconds;
                ActorRestResult deadRest = ActorRestService.TryRest(deadNeeds, WorldClock.SecondsPerHour);
                Require(!deadRest.Success && deadRest.FailureCode == ActorRestFailureCode.ActorDead &&
                        Near(clock.ElapsedGameSeconds, beforeRejectedRest) && deadHealth.IsDead,
                    "Dead actor rest was not rejected without time advance/revival.");
                Require(clock.ElapsedGameSeconds > beforeAdvance, "World Clock itself did not advance for living actors.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(deadObject);
            }
        }

        private static void RunStaminaRegression(ActorNeedsComponent needs, ActorStaminaComponent stamina)
        {
            SetNeed(needs, "hunger", 100f);
            SetNeed(needs, "thirst", 100f);
            SetStamina(stamina, stamina.MaximumStamina);
            float hungerBeforeSprint = needs.GetNeedValue("hunger");
            float thirstBeforeSprint = needs.GetNeedValue("thirst");
            Require(stamina.Advance(1f, true) && stamina.CurrentStamina < stamina.MaximumStamina &&
                    needs.GetNeedValue("hunger") < hungerBeforeSprint && needs.GetNeedValue("thirst") < thirstBeforeSprint,
                "Active sprint did not drain real stamina and extra Hunger/Thirst reserves.");

            SetNeed(needs, "hunger", 100f);
            SetNeed(needs, "thirst", 100f);
            SetStamina(stamina, stamina.MaximumStamina);
            float hungerBeforeHighStaminaSprint = needs.GetNeedValue("hunger");
            Require(stamina.Advance(0.25f, true), "High-stamina sprint setup did not advance.");
            float highStaminaHungerCost = hungerBeforeHighStaminaSprint - needs.GetNeedValue("hunger");

            SetNeed(needs, "hunger", 100f);
            SetNeed(needs, "thirst", 100f);
            SetStamina(stamina, Mathf.Min(stamina.MaximumStamina, stamina.SprintRecoveryThreshold + 1f));
            float hungerBeforeLowStaminaSprint = needs.GetNeedValue("hunger");
            Require(stamina.Advance(0.25f, true), "Low-stamina sprint setup did not advance.");
            float lowStaminaHungerCost = hungerBeforeLowStaminaSprint - needs.GetNeedValue("hunger");
            Require(lowStaminaHungerCost > highStaminaHungerCost,
                "Low stamina did not increase continued sprint Hunger cost.");

            float hungerBeforeRest = needs.GetNeedValue("hunger");
            float thirstBeforeRest = needs.GetNeedValue("thirst");
            Require(stamina.Advance(0.5f, false) &&
                    Near(needs.GetNeedValue("hunger"), hungerBeforeRest) && Near(needs.GetNeedValue("thirst"), thirstBeforeRest),
                "Resting stamina recovery incorrectly applied extra exertion Need cost.");

            SetNeed(needs, "hunger", 100f);
            SetNeed(needs, "thirst", 100f);
            SetStamina(stamina, 0f);
            Require(!stamina.CanSprint && stamina.IsExhausted, "Zero stamina did not enforce sprint lockout.");
            stamina.Advance(0.5f, false);
            Require(!stamina.CanSprint, "Sprint lockout cleared before the configured recovery threshold.");
            stamina.Advance(0.5f, false);
            Require(stamina.CanSprint, "Sprint lockout did not clear at the recovery threshold.");

            SetNeed(needs, "hunger", 0f);
            SetNeed(needs, "thirst", 0f);
            SetStamina(stamina, 0f);
            stamina.Advance(0.5f, false);
            float severeReserveRecovery = stamina.CurrentStamina;
            SetNeed(needs, "hunger", 100f);
            SetNeed(needs, "thirst", 100f);
            SetStamina(stamina, 0f);
            stamina.Advance(0.5f, false);
            Require(stamina.CurrentStamina > severeReserveRecovery,
                "High Hunger/Thirst reserves did not improve stamina recovery over severe low reserves.");

            float beforeInvalidConsume = needs.GetNeedValue("hunger");
            Require(!needs.TryConsumeNeed("unknown", 1f) && !needs.TryConsumeNeed("hunger", -1f) &&
                    Near(needs.GetNeedValue("hunger"), beforeInvalidConsume),
                "Needs consumption API accepted invalid requests or mutated a reserve.");
        }

        private static WorldClock Clock()
        {
            WorldClock clock = WorldClock.Current;
            Require(clock != null, "World Clock authority is unavailable.");
            clock.AdvanceDuringGameplay = false;
            return clock;
        }

        private static ActorNeedsComponent PlayerNeeds()
        {
            ActorNeedsComponent[] candidates = UnityEngine.Object.FindObjectsByType<ActorNeedsComponent>(FindObjectsInactive.Exclude);
            ActorNeedsComponent needs = candidates.SingleOrDefault(candidate =>
                candidate.GetComponent<OldScars.Core.Interactions.ActorInteractionContext>()?.ActorTags.Contains("player") == true);
            Require(needs != null, $"Expected exactly one player ActorNeedsComponent; found {candidates.Length} total needs components.");
            return needs;
        }

        private static ActorStaminaComponent PlayerStamina(ActorNeedsComponent needs)
        {
            ActorStaminaComponent stamina = needs != null ? needs.GetComponent<ActorStaminaComponent>() : null;
            Require(stamina != null, "Player ActorStaminaComponent is unavailable.");
            return stamina;
        }

        private static void RestoreClock(WorldClock clock, double elapsed)
        {
            Require(clock.TryRestoreElapsedGameSeconds(elapsed, out string failure), "World Clock setup failed: " + failure);
        }

        private static void SetNeed(ActorNeedsComponent needs, string needId, float value)
        {
            Require(needs != null && needs.HasNeed(needId), $"Actor does not expose need '{needId}'.");
            if (!Near(needs.GetNeedValue(needId), value))
                Require(needs.TrySetNeedValue(needId, value), $"Could not set need '{needId}' to {value:R}.");
        }

        private static void SetStamina(ActorStaminaComponent stamina, float value)
        {
            Require(stamina != null, "Player ActorStaminaComponent is unavailable.");
            if (!Near(stamina.CurrentStamina, value))
                Require(stamina.TrySetCurrentStamina(value), $"Could not set stamina to {value:R}.");
        }

        private static int Quantity(InventoryComponent inventory, string instanceId)
        {
            return inventory.TryGetEntryByInstanceId(instanceId, out _, out ItemStorageEntry entry) ? entry.Quantity : 0;
        }

        private static float Need(CurrentSliceSaveData snapshot, string needId)
        {
            NeedState state = (snapshot.player.needs ?? Array.Empty<NeedState>())
                .Single(value => value != null && value.needId == needId);
            return state.currentValue;
        }

        private static PersistenceFileStore Store()
        {
            string root = SessionState.GetString(RootKey, string.Empty);
            Require(!string.IsNullOrWhiteSpace(root), "Temporary persistence root is missing.");
            return new PersistenceFileStore(root);
        }

        private static CurrentSliceSaveData Capture(string label)
        {
            CurrentSliceResult result = CurrentSliceSnapshotService.Capture();
            Require(result.Success, label + " capture failed: " + result.Failure);
            return result.Snapshot;
        }

        private static void Write(PersistenceFileStore store, string slot, CurrentSliceSaveData snapshot)
        {
            PersistenceWriteResult result = store.Write(slot, CurrentSliceSnapshotService.ToPayload(snapshot));
            Require(result.Success, $"Slot '{slot}' write failed: {result.Failure}");
        }

        private static CurrentSliceSaveData Read(string slot)
        {
            CurrentSliceResult result = CurrentSliceSnapshotService.Read(slot, Store());
            Require(result.Success, $"Slot '{slot}' read/preflight failed: {result.Failure}");
            return result.Snapshot;
        }

        private static void AssertEquivalent(CurrentSliceSaveData expected, CurrentSliceSaveData actual, string label)
        {
            CurrentSliceComparisonResult comparison = CurrentSliceSnapshotService.Compare(expected, actual);
            Require(comparison.Equivalent, label + " differs: " + comparison.Difference);
        }

        private static void AssertActorLifecycle(CurrentSliceSaveData snapshot, string label)
        {
            foreach (ActorState state in snapshot.actors ?? Array.Empty<ActorState>())
            {
                Require(state != null && ActorRuntimeRegistry.TryGet(state.actorInstanceId, out ActorRuntimeIdentity identity) &&
                        identity.ActorProfileId == state.actorProfileId &&
                        identity.LifecycleState.ToString() == state.lifecycleState,
                    $"{label}: actor '{state?.actorInstanceId ?? "<NONE>"}' lost identity/profile/lifecycle continuity.");
            }
        }

        private static bool Near(double left, double right, double tolerance = 0.0001d)
        {
            return Math.Abs(left - right) <= tolerance;
        }

        private static void Require(bool condition, string failure)
        {
            if (!condition)
                throw new InvalidOperationException(failure);
        }

        private static void FinalizeRun()
        {
            string failure = SessionState.GetString(ErrorKey, string.Empty);
            string root = SessionState.GetString(RootKey, string.Empty);
            if (EditorSceneManager.GetActiveScene().isDirty)
                failure = Append(failure, "Diagnostics left SampleScene dirty; it was not saved.");
            try
            {
                if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
                    Directory.Delete(root, true);
                if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
                    failure = Append(failure, "Temporary persistence root still exists after cleanup.");
            }
            catch (Exception exception)
            {
                failure = Append(failure, "Temporary cleanup failed: " + exception.Message);
            }

            bool success = string.IsNullOrWhiteSpace(failure);
            ClearSession();
            if (success)
                Debug.Log("M38.1 Needs, World Clock & Recovery Diagnostics: PASS");
            else
                Debug.LogError("M38.1 Needs, World Clock & Recovery Diagnostics: FAIL\n- " + failure);
            if (Application.isBatchMode)
                EditorApplication.Exit(success ? 0 : 1);
        }

        private static string Append(string current, string value)
        {
            return string.IsNullOrWhiteSpace(current) ? value : current + "\n- " + value;
        }

        private static void ClearSession()
        {
            CurrentSliceLoadService.DiagnosticInjectFailureAfterRuntimeStateRestore = false;
            SessionState.EraseString(PhaseKey);
            SessionState.EraseString(RootKey);
            SessionState.EraseString(ErrorKey);
        }
    }
}
