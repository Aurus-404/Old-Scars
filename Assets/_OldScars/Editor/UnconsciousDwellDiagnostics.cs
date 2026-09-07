using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using OldScars.Core;
using OldScars.Core.Actors;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Interactions;
using OldScars.Core.Persistence;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OldScars.Editor
{
    [InitializeOnLoad]
    public static class UnconsciousDwellDiagnostics
    {
        private const string Key = "OldScars.P2Dwell.";
        private const string ProfileId = "core:debug_encounter_fight_01";
        private const string Slot = "p2_dwell";
        private static IEnumerator cases;
        private static double started;
        private static double x1;
        private static double x100;
        private static int spawnIndex;
        private static double Now => Time.realtimeSinceStartupAsDouble;

        static UnconsciousDwellDiagnostics() => EditorApplication.update += Continue;

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("P2 requires idle compiled Edit Mode.");
            SessionState.SetString(Key + "root", Path.Combine(Path.GetTempPath(), "OldScars_P2_" + Guid.NewGuid().ToString("N")));
            SessionState.SetString(Key + "error", "");
            SessionState.SetString(Key + "phase", "enter");
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void Continue()
        {
            string phase = SessionState.GetString(Key + "phase", "");
            if (phase == "") return;
            try
            {
                if ((phase == "enter" || phase == "fresh") && EditorApplication.isPlaying &&
                    Time.frameCount >= 5 && GameDataManager.Instance?.IsReady == true && WorldClock.Current != null)
                {
                    WorldClock.Current.AdvanceDuringGameplay = false;
                    QuietActors();
                    started = Now;
                    cases = phase == "fresh" ? FreshLoad() : RunCases();
                    SessionState.SetString(Key + "phase", phase == "fresh" ? "runningFresh" : "running");
                }
                else if ((phase == "running" || phase == "runningFresh") && EditorApplication.isPlaying)
                {
                    Require(Now - started < 90d, "P2 timed out in " + phase);
                    if (cases.MoveNext()) return;
                    SessionState.SetString(Key + "phase", phase == "running" ? "offline" : "finish");
                    SessionState.SetString(Key + "offlineUntil", (EditorApplication.timeSinceStartup + 6d).ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                    EditorApplication.ExitPlaymode();
                }
                else if (phase == "offline" && !EditorApplication.isPlayingOrWillChangePlaymode &&
                    EditorApplication.timeSinceStartup >= double.Parse(SessionState.GetString(Key + "offlineUntil", "0"), System.Globalization.CultureInfo.InvariantCulture))
                {
                    SessionState.SetString(Key + "phase", "fresh");
                    EditorApplication.EnterPlaymode();
                }
                else if (phase == "finish" && !EditorApplication.isPlayingOrWillChangePlaymode)
                    Finish();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetString(Key + "error", exception.Message);
                SessionState.SetString(Key + "phase", "finish");
                if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
                else Finish();
            }
        }

        private static IEnumerator RunCases()
        {
            Require(GameDataManager.Instance.Report.ErrorCount == 0, "Core data validation failed.");
            ActorProfileConsciousness profile = GameDataManager.Instance.Database.GetActorProfile(ProfileId).consciousness;
            Require(profile.minimum_unconscious_real_seconds == 5f, "Expected provisional Core minimum 5 seconds.");
            var profileJson = JObject.FromObject(profile);
            profileJson.Remove("minimum_unconscious_real_seconds");
            var legacyProfile = profileJson.ToObject<ActorProfileConsciousness>();
            Require(ActorConditionComponent.TryValidateProfile(legacyProfile, out _) && legacyProfile.minimum_unconscious_real_seconds == 5f,
                "Legacy profile default is incompatible.");
            foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                legacyProfile.minimum_unconscious_real_seconds = invalid;
                Require(!ActorConditionComponent.TryValidateProfile(legacyProfile, out _), "Invalid profile dwell accepted.");
            }
            legacyProfile.minimum_unconscious_real_seconds = 0.3f;
            var configuredActor = Spawn();
            Require(configuredActor.TryConfigure(legacyProfile, out _), "Valid custom minimum rejected.");
            double customEntry = Now;
            KnockOut(configuredActor);
            AdvanceHours(2d);
            Require(configuredActor.IsUnconscious && Near(configuredActor.UnconsciousDwellRemainingSeconds, 0.3d),
                "Custom profile minimum was not used.");
            while (configuredActor.IsUnconscious)
            {
                Require(Now - customEntry < 2d, "Custom minimum failed to expire.");
                yield return null;
            }
            Require(Now - customEntry >= 0.3d, "Custom minimum expired early.");

            var player = UnityEngine.Object.FindObjectsByType<ActorInteractionContext>(FindObjectsInactive.Exclude)
                .Single(actor => actor.ActorTags.Contains("player")).GetComponent<ActorConditionComponent>();
            ActorConditionComponent npc = Spawn();
            Require(player.GetType() == npc.GetType(), "Player/NPC authority differs.");
            foreach (float multiplier in new[] { 1f, 100f })
            {
                ActorConditionComponent actor = multiplier == 1f ? player : npc;
                int events = 0;
                actor.FunctionalStateChanged += (_, __) => events++;
                double entry = Now;
                KnockOut(actor);
                Require(events == 1 && actor.IsUnconsciousDwellActive && Near(actor.UnconsciousDwellRemainingSeconds, 5d), "Entry did not start exactly once.");
                AdvanceHours(1d);
                Require(actor.ConsciousnessStability > 0.25f && actor.IsUnconscious && !actor.CanPerformActiveActions,
                    "Early physiological recovery bypassed the gate.");
                Require(WorldClock.Current.TrySetDebugTimeMultiplier(multiplier, out _), "Multiplier rejected.");
                WorldClock.Current.AdvanceDuringGameplay = true;
                double gameStart = WorldClock.Current.ElapsedGameSeconds;
                bool damaged = false;
                while (actor.IsUnconscious)
                {
                    double elapsed = Now - entry;
                    if (!damaged && elapsed > 1d)
                    {
                        double beforeDamageRemaining = actor.UnconsciousDwellRemainingSeconds;
                        Wound(actor, BodyRegion.LeftArm, WoundType.Laceration, 0.1f, 0.01f);
                        Require(actor.UnconsciousDwellRemainingSeconds == beforeDamageRemaining && events == 1, "Additional damage restarted dwell or emitted ping-pong.");
                        float blood = actor.BloodFraction;
                        float trauma = actor.TransientTrauma;
                        AdvanceHours(0.01d);
                        Require(actor.BloodFraction < blood && actor.TransientTrauma < trauma, "Physiology stopped during dwell.");
                        damaged = true;
                    }
                    Require(events == 1 && !actor.CanPerformActiveActions, "Gate exposed intermediate active state.");
                    Require(elapsed < 7d, "Dwell failed to release recoverable actor.");
                    yield return null;
                }
                double duration = Now - entry;
                Require(duration >= 5d && duration < 6d && events == 2 && damaged,
                    $"Real duration or transition count invalid: multiplier={multiplier} duration={duration:R} events={events} damaged={damaged}.");
                if (multiplier == 1f) x1 = duration; else x100 = duration;
                Debug.Log($"[P2][REAL_TIME] multiplier={multiplier} duration={duration:F3}s gameSeconds={WorldClock.Current.ElapsedGameSeconds - gameStart:F1} transitions={events}");
                WorldClock.Current.AdvanceDuringGameplay = false;
            }
            Require(Math.Abs(x1 - x100) < 0.5d, "x1/x100 real-time duration differs.");
            WorldClock.Current.ResetDebugTimeMultiplier();

            KnockOut(npc);
            Require(Near(npc.UnconsciousDwellRemainingSeconds, 5d), "New episode did not get a full minimum.");
            double poorEntry = Now;
            while (Now - poorEntry < 5.2d) yield return null;
            Require(npc.IsUnconscious && !npc.IsUnconsciousDwellActive, "Elapsed minimum forced wake-up or restarted itself.");
            // Keep the recovered value inside the unconscious recovery dead-band.
            var band = new ActorConditionStateData { bloodFraction = 1f, transientTrauma = 0.79f,
                unconsciousDwellRemainingSeconds = 0d, unconsciousRecoveryPending = true };
            Require(npc.TryApplyPersistenceState(band, out _) && npc.IsUnconscious, "Load lost exhausted-episode hysteresis.");
            AdvanceHours(2d);
            Require(!npc.IsUnconscious, "Eligible recovery remained blocked after expiry.");

            var incapacitated = Spawn();
            Wound(incapacitated, BodyRegion.Head, WoundType.Blunt, 0.55f, 0f);
            Require(incapacitated.FunctionalState == ActorFunctionalState.Incapacitated && !incapacitated.IsUnconsciousDwellActive,
                "Incapacitated alone started a dwell.");
            foreach (bool bloodDeath in new[] { false, true })
            {
                var fatal = Spawn();
                KnockOut(fatal);
                var health = fatal.GetComponent<ActorHealthComponent>();
                if (bloodDeath)
                {
                    Wound(fatal, BodyRegion.Torso, WoundType.Puncture, 0.1f, 1f);
                    AdvanceHours(0.95d);
                }
                else health.ApplyDamage(health.MaxVitalIntegrity * 2f);
                Require(health.IsDead && fatal.GetComponent<ActorRuntimeIdentity>().LifecycleState == ActorLifecycleState.Dead &&
                    !fatal.CanPerformActiveActions && !fatal.IsUnconsciousDwellActive, "Dwell blocked terminal death.");
                int revision = fatal.Revision;
                AdvanceHours(1d);
                Require(health.IsDead && revision == fatal.Revision, "Dead physiology mutated or resurrected.");
            }

            // Persist both routes after their physiology has already recovered inside the gate.
            KnockOut(player);
            KnockOut(npc);
            AdvanceHours(2d);
            double wait = Now;
            while (Now - wait < 1d) yield return null;
            var save = CurrentSliceSnapshotService.Save(Slot, Store());
            Require(save.Success, "P2 save failed: " + save.Failure);
            string npcId = npc.GetComponent<ActorRuntimeIdentity>().ActorInstanceId;
            double remaining = save.Snapshot.actors.Single(actor => actor.actorInstanceId == npcId).conditionState.unconsciousDwellRemainingSeconds.Value;
            Require(remaining > 3d && remaining < 4.2d && save.Snapshot.player.conditionState.unconsciousRecoveryPending,
                "Snapshot did not preserve a partial Player/NPC gate.");
            double savedAt = Now;
            while (Now - savedAt < 1d) yield return null;
            var load = CurrentSliceLoadService.Load(Slot, Store());
            Require(load.Success, "P2 load failed: " + load.Failure);
            QuietActors();
            Require(ActorRuntimeRegistry.TryGet(npcId, out var restored), "NPC identity lost during load.");
            npc = restored.GetComponent<ActorConditionComponent>();
            Require(npc.IsUnconscious && Near(npc.UnconsciousDwellRemainingSeconds, remaining, 0.02d) && player.IsUnconscious,
                "Load skipped gate, restarted full minimum or consumed snapshot age.");

            var before = CurrentSliceSnapshotService.Capture();
            CurrentSliceLoadService.DiagnosticInjectFailureAfterRuntimeStateRestore = true;
            try
            {
                var rollback = CurrentSliceLoadService.Load(Slot, Store());
                Require(!rollback.Success && rollback.RollbackSucceeded &&
                    CurrentSliceSnapshotService.Compare(before.Snapshot, CurrentSliceSnapshotService.Capture().Snapshot).Equivalent,
                    "Injected restore failure lost exact dwell rollback: " + rollback.Failure);
            }
            finally { CurrentSliceLoadService.DiagnosticInjectFailureAfterRuntimeStateRestore = false; }
            QuietActors();

            JObject payload = (JObject)CurrentSliceSnapshotService.ToPayload(save.Snapshot);
            foreach (JToken invalid in new JToken[] { JValue.CreateNull(), new JValue(-1d), new JValue(double.NaN), new JValue(double.PositiveInfinity) })
            {
                var bad = (JObject)payload.DeepClone();
                bad["player"]["conditionState"]["unconsciousDwellRemainingSeconds"] = invalid;
                var rejected = CurrentSliceLoadService.LoadPayload(bad, "P2 invalid dwell");
                Require(!rejected.Success && !rejected.MutationStarted, "Invalid present dwell bypassed preflight.");
            }
            var legacy = (JObject)payload.DeepClone();
            foreach (JObject state in legacy.SelectTokens("$..conditionState").OfType<JObject>())
            {
                state.Remove("unconsciousDwellRemainingSeconds");
                state.Remove("unconsciousRecoveryPending");
            }
            legacy["player"]["conditionState"]["bloodFraction"] = 1f;
            legacy["player"]["conditionState"]["transientTrauma"] = 1f;
            var legacyLoad = CurrentSliceLoadService.LoadPayload(legacy, "P2 legacy");
            Require(legacyLoad.Success && player.IsUnconscious && Near(player.UnconsciousDwellRemainingSeconds, 5d),
                "Old save failed safe full-minimum normalization: " + legacyLoad.Failure);
            QuietActors();
            Require(CurrentSliceLoadService.Load(Slot, Store()).Success, "Could not restore fresh-session snapshot.");
            SessionState.SetString(Key + "npc", npcId);
            SessionState.SetString(Key + "remaining", remaining.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            Debug.Log("[P2][CASES] PASS entry, physiology, no ping-pong, no forced wake, new episode, death x2, Player/NPC save/load, exact rollback, invalid preflight, legacy schema-v1.");
        }

        private static IEnumerator FreshLoad()
        {
            double loadedAt = Now;
            var load = CurrentSliceLoadService.Load(Slot, Store());
            Require(load.Success, "Fresh-session load failed: " + load.Failure);
            QuietActors();
            Require(ActorRuntimeRegistry.TryGet(SessionState.GetString(Key + "npc", ""), out var npc), "Fresh session lost actor identity.");
            var condition = npc.GetComponent<ActorConditionComponent>();
            double remaining = double.Parse(SessionState.GetString(Key + "remaining", "0"), System.Globalization.CultureInfo.InvariantCulture);
            Require(condition.IsUnconscious && Near(condition.UnconsciousDwellRemainingSeconds, remaining, 0.02d),
                "Offline interval consumed dwell or fresh session reset full minimum.");
            while (condition.IsUnconscious)
            {
                Require(Now - loadedAt < remaining + 1d, "Fresh restored dwell failed to finish.");
                yield return null;
            }
            Require(Now - loadedAt >= remaining - 0.02d,
                $"Fresh load woke before remaining real minimum: remaining={remaining:R} elapsed={Now - loadedAt:R}.");
            Debug.Log($"[P2][FRESH_SESSION] remaining={remaining:F3}s offline>=6s resumed={Now - loadedAt:F3}s PASS");
        }

        private static ActorConditionComponent Spawn()
        {
            Require(ActorSpawnService.TrySpawn(ProfileId, new Vector3(20f + spawnIndex++ * 3f, 1f, 20f), Quaternion.identity,
                out var actor, out string failure), "Spawn failed: " + failure);
            QuietActors();
            return actor.GetComponent<ActorConditionComponent>();
        }

        private static void QuietActors()
        {
            foreach (var actor in UnityEngine.Object.FindObjectsByType<ActorThreatAcquisitionController>(FindObjectsInactive.Exclude)) actor.enabled = false;
        }

        private static void KnockOut(ActorConditionComponent actor) => Wound(actor, BodyRegion.Head, WoundType.Blunt, 1f, 0f);
        private static void Wound(ActorConditionComponent actor, BodyRegion region, WoundType type, float severity, float bleeding)
        {
            Require(actor.GetComponent<ActorMedicalStateComponent>().TryApplyWound("wound_" + Guid.NewGuid().ToString("N"),
                region, type, severity, bleeding, 0f, out string failure), "Wound failed: " + failure);
        }
        private static void AdvanceHours(double hours) => Require(WorldClock.Current.TryAdvanceGameTime(hours * WorldClock.SecondsPerHour, out _), "Clock advance failed.");
        private static PersistenceFileStore Store() => new PersistenceFileStore(SessionState.GetString(Key + "root", ""));
        private static bool Near(double a, double b, double tolerance = 0.001d) => Math.Abs(a - b) <= tolerance;
        private static void Require(bool condition, string failure) { if (!condition) throw new InvalidOperationException(failure); }

        private static void Finish()
        {
            string error = SessionState.GetString(Key + "error", "");
            if (EditorSceneManager.GetActiveScene().isDirty) error += " Diagnostic left scene dirty (not saved).";
            SessionState.SetString(Key + "phase", "");
            if (error == "") Debug.Log("P2 Unconscious Dwell Diagnostics: PASS");
            else Debug.LogError("P2 Unconscious Dwell Diagnostics: FAIL " + error);
            if (Application.isBatchMode) EditorApplication.Exit(error == "" ? 0 : 1);
        }
    }
}
