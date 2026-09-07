using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OldScars.Core;
using OldScars.Core.Actors;
using OldScars.Core.Combat;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace OldScars.Editor
{
    [InitializeOnLoad]
    public static class P3CombatContinuityDiagnostics
    {
        private const string Key = "OldScars.P3Continuity.";
        private const string Profile = "core:debug_encounter_fight_01";
        private const string Ammo = "core:ammo_303_british_01";
        private static readonly Stack<IEnumerator> work = new Stack<IEnumerator>();
        private static ActorRuntimeIdentity blue, red;
        private static GameObject barrier;
        private static double started;
        private static double Now => Time.realtimeSinceStartupAsDouble;
        private static HumanEncounterAIController AI(ActorRuntimeIdentity actor) => actor.GetComponent<HumanEncounterAIController>();
        private static ActorConditionComponent Condition(ActorRuntimeIdentity actor) => actor.GetComponent<ActorConditionComponent>();

        static P3CombatContinuityDiagnostics() => EditorApplication.update += Continue;

        public static void Run()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling, "P3 requires idle compiled Edit Mode.");
            SessionState.SetString(Key + "error", "");
            SessionState.SetString(Key + "phase", "enter");
            EditorSceneManager.OpenScene(M41SampleSceneNavigationTools.ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void Continue()
        {
            string phase = SessionState.GetString(Key + "phase", "");
            if (phase == "") return;
            try
            {
                if (phase == "enter" && EditorApplication.isPlaying && Time.frameCount >= 5 && GameDataManager.Instance?.IsReady == true && WorldClock.Current != null)
                {
                    started = Now;
                    work.Clear();
                    work.Push(Cases());
                    SessionState.SetString(Key + "phase", "running");
                }
                else if (phase == "running" && EditorApplication.isPlaying)
                {
                    Require(Now - started < 150d, "P3 overall timeout.");
                    while (work.Count > 0)
                    {
                        if (!work.Peek().MoveNext()) { work.Pop(); continue; }
                        if (work.Peek().Current is IEnumerator child) { work.Push(child); continue; }
                        return;
                    }
                    SessionState.SetString(Key + "phase", "finish");
                    EditorApplication.ExitPlaymode();
                }
                else if (phase == "finish" && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    string error = SessionState.GetString(Key + "error", "");
                    SessionState.SetString(Key + "phase", "");
                    if (error == "") Debug.Log("P3 Combat Continuity Diagnostics: PASS");
                    else Debug.LogError("P3 Combat Continuity Diagnostics: FAIL " + error);
                    if (Application.isBatchMode) EditorApplication.Exit(error == "" ? 0 : 1);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetString(Key + "error", exception.Message);
                SessionState.SetString(Key + "phase", "finish");
                if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
            }
        }

        private static IEnumerator Cases()
        {
            Require(GameDataManager.Instance.Report.ErrorCount == 0, "Core validation failed.");
            WorldClock.Current.AdvanceDuringGameplay = false;
            WorldClock.Current.ResetDebugTimeMultiplier();
            foreach (var acquisition in UnityEngine.Object.FindObjectsByType<ActorThreatAcquisitionController>(FindObjectsInactive.Exclude)) acquisition.enabled = false;
            barrier = M41SampleSceneNavigationTools.FindBarrier();
            Require(barrier != null, "Missing navigation fixture barrier.");
            barrier.SetActive(false);
            blue = Spawn("blue", -4f);
            red = Spawn("red", 4f);
            var json = JObject.FromObject(GameDataManager.Instance.Database.GetActorProfile(Profile).encounter_ai);
            json.Remove("recent_enemy_memory_seconds");
            var tuning = json.ToObject<ActorProfileEncounterAI>();
            Require(tuning.recent_enemy_memory_seconds == 60f, "Legacy profile default changed.");
            foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                tuning.recent_enemy_memory_seconds = invalid;
                Require(!AI(blue).TryConfigure(tuning, out _), "Invalid recent-enemy duration accepted.");
            }
            tuning.recent_enemy_memory_seconds = 60f;
            Require(AI(blue).TryConfigure(tuning, out _), "Valid memory tuning rejected.");
            FacePair();
            yield return Wait(() => Fighting(blue, red) && Fighting(red, blue), 12d, "normal automatic Blue/Red recognition and Fight");
            Require(AI(blue).CombatContextSequence == 1 && AI(red).CombatContextSequence == 1, "Initial context not unique.");
            Debug.Log("[P3][A] Normal automatic recognition/Fight PASS");

            KnockOut(red);
            yield return Wait(() => AI(blue).Threat == null && AI(red).Threat == null, 2d, "KO releases current Threat on both sides");
            int blueAttacks = AI(blue).AttackCount, redAttacks = AI(red).AttackCount;
            double redRemaining = AI(red).RecentEnemyRemainingSeconds;
            Wound(blue, BodyRegion.LeftArm, 0.1f, 0.02f);
            Require(blue.GetComponent<InventoryComponent>().AddItemByDefinitionId("core:bandage_01", 1) != null, "Bandage fixture failed.");
            yield return Wait(() => AI(blue).RoutineSelfTreatmentCount > 0, 12d, "routine treatment starts with remembered enemy");
            yield return Wait(() => blue.GetComponent<ActorWoundTreatmentController>().CompletedCount == 1, 12d, "routine treatment completes");
            Require(AI(blue).RecentEnemyActorInstanceId == red.ActorInstanceId && AI(red).RecentEnemyActorInstanceId == blue.ActorInstanceId,
                "KO or routine treatment erased context.");
            Require(Math.Abs(AI(red).RecentEnemyRemainingSeconds - redRemaining) < 0.1d, "Own KO consumed memory duration.");
            SetRounds(blue, 7);
            Require(blue.GetComponent<InventoryComponent>().AddItemByDefinitionId(Ammo, 3) != null, "Ammo fixture failed.");
            int ammo = AmmoCount(blue), reloads = AI(blue).ReloadCount;
            yield return Wait(() => AI(blue).IsReloadPending, 4d, "remembered-enemy AmbientTopOff");
            Require(AI(blue).PendingReloadPurpose == "AmbientTopOff" && AI(blue).Threat == null, "Memory blocked safe reload ownership.");
            yield return Wait(() => !AI(blue).IsReloadPending, 4d, "AmbientTopOff completion");
            Require(Firearm(blue).LoadedRounds == 10 && AmmoCount(blue) == ammo - 3 && AI(blue).ReloadCount == reloads + 1,
                "Remembered-enemy reload did not consume exactly once.");
            double armedKoStart = Now;
            while (Now - armedKoStart < 1d)
            {
                Require(Firearm(blue).LoadedRounds == 10 && AI(blue).AttackCount == blueAttacks && AI(blue).Threat == null,
                    "Armed actor deliberately fired at remembered KO.");
                yield return null;
            }
            SetRounds(blue, 0);
            Require(AI(blue).AttackCount == blueAttacks && AI(red).AttackCount == redAttacks && !Condition(red).CanPerformActiveActions,
                "Active action or deliberate attack occurred during KO.");
            Require(!AI(blue).HasLastKnownPosition && !AI(blue).HasSearchAnchor, "KO memory retained spatial tracking.");
            Debug.Log("[P3][B/C/I/J] KO memory, no attacks, paused own timer, routine treatment and AmbientTopOff PASS");

            int blueRevision = AI(blue).TransitionRevision, redRevision = AI(red).TransitionRevision;
            AdvanceRecovery();
            yield return Wait(() => Condition(red).CanPerformActiveActions, 7d, "visible recovery after P2 dwell");
            FacePair();
            yield return Wait(() => Fighting(blue, red) && Fighting(red, blue), 12d, "visible recovery reacquisition");
            Require(AI(blue).CombatContextSequence == 1 && AI(red).CombatContextSequence == 1 &&
                AI(blue).CombatContinuityResumeCount == 1 && AI(red).CombatContinuityResumeCount == 1, "Visible recovery created a new combat context.");
            Require(AI(blue).TransitionRevision == blueRevision + 1 && AI(red).TransitionRevision == redRevision + 2,
                "Visible recovery introduced an extra state cycle (expected Idle->Fighting, Inactive->Idle->Fighting).");
            Debug.Log("[P3][D] Visible recovery preserves both contexts PASS");

            KnockOut(red);
            yield return Wait(() => AI(blue).Threat == null && AI(red).Threat == null, 2d, "second KO releases active context");
            // A fixture wall hides both old and moved positions; no production perception override.
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "P3 temporary LOS occluder";
            wall.transform.position = Point(0f) + Vector3.up * 3f;
            wall.transform.localScale = new Vector3(30f, 8f, 1f);
            var obstacle = wall.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            Move(blue, Point(-4f));
            Move(red, Point(4f) + Vector3.right);
            Physics.SyncTransforms();
            AdvanceRecovery();
            yield return Wait(() => Condition(red).CanPerformActiveActions, 7d, "occluded recovery after P2 dwell");
            FacePair();
            Require(!blue.GetComponent<ActorVisualPerceptionService>().Evaluate(red).Perceived &&
                !red.GetComponent<ActorVisualPerceptionService>().Evaluate(blue).Perceived, "Recovery fixture must occlude both directions.");
            double hiddenStart = Now;
            while (Now - hiddenStart < 2d)
            {
                Require(AI(blue).Threat == null && AI(red).Threat == null && !AI(blue).HasSearchAnchor && !AI(red).HasSearchAnchor &&
                    !AI(blue).HasLastKnownPosition && !AI(red).HasLastKnownPosition,
                    $"Hidden recovery leaked spatial knowledge or reacquired. Blue={blue.transform.position} Red={red.transform.position} bluePerception={blue.GetComponent<ActorVisualPerceptionService>().Evaluate(red).Reason} redPerception={red.GetComponent<ActorVisualPerceptionService>().Evaluate(blue).Reason}");
                Require(AI(blue).RecentEnemyActorInstanceId == red.ActorInstanceId && AI(red).RecentEnemyActorInstanceId == blue.ActorInstanceId,
                    "Hidden recovery lost identity context.");
                yield return null;
            }
            Require(!blue.GetComponent<ActorVisualPerceptionService>().Evaluate(red).Perceived, "Hidden fixture did not occlude perception.");
            blueRevision = AI(blue).TransitionRevision;
            redRevision = AI(red).TransitionRevision;
            wall.SetActive(false);
            FacePair();
            bool partialRecognition = false;
            double reacquireStart = Now;
            while (!Fighting(blue, red) || !Fighting(red, blue))
            {
                if (blue.GetComponent<ActorThreatAcquisitionController>().TryGetRecognitionProgress(red, out float progress) && progress > 0f && progress < 1f)
                    partialRecognition = true;
                Require(Now - reacquireStart < 12d, "Unoccluded recognition timed out.");
                yield return null;
            }
            Require(partialRecognition && AI(blue).CombatContextSequence == 1 && AI(red).CombatContextSequence == 1 &&
                AI(blue).CombatContinuityResumeCount == 2 && AI(red).CombatContinuityResumeCount == 2, "Memory bypassed Recognition or duplicated hidden recovery context.");
            Require(AI(blue).TransitionRevision == blueRevision + 1 && AI(red).TransitionRevision == redRevision + 1,
                "Recognized hidden recovery introduced a new Alerted/state cycle instead of resuming Fighting.");
            UnityEngine.Object.Destroy(wall);
            Debug.Log("[P3][E] Hidden moved enemy reveals no position; normal Recognition resumes same context PASS");

            KnockOut(red);
            yield return Wait(() => AI(blue).Threat == null, 2d, "KO before new enemy");
            var replacement = Spawn("red", 4f);
            FaceActors(blue, replacement);
            yield return Wait(() => Fighting(blue, replacement), 12d, "new hostile replaces KO memory");
            Require(AI(blue).RecentEnemyActorInstanceId == replacement.ActorInstanceId && AI(blue).CombatContextSequence == 2,
                "Old memory blocked/reused new enemy context.");
            KnockOut(replacement);
            yield return Wait(() => AI(blue).Threat == null, 2d, "replacement KO");
            replacement.GetComponent<ActorHealthComponent>().ApplyDamage(100000f);
            yield return Wait(() => AI(blue).RecentEnemyActorInstanceId == null, 2d, "remembered enemy death cleanup");
            Require(AI(replacement).RecentEnemyActorInstanceId == null && AI(blue).Threat == null, "Dead actor retained/reactivated context.");
            Remove(replacement);
            Remove(red);
            Debug.Log("[P3][F/H] New enemy replacement and terminal death cleanup PASS");

            red = Spawn("red", 4f);
            FacePair();
            yield return Wait(() => Fighting(blue, red), 12d, "invalidation encounter");
            KnockOut(red);
            yield return Wait(() => AI(blue).Threat == null, 2d, "KO before removal");
            Remove(red);
            yield return Wait(() => AI(blue).RecentEnemyActorInstanceId == null && AI(blue).RecentEnemyRemainingSeconds == 0d, 2d, "runtime removal cleanup");
            Debug.Log("[P3][G] Runtime removal clears reference and duration PASS");

            tuning.recent_enemy_memory_seconds = 0.8f;
            Require(AI(blue).TryConfigure(tuning, out _), "Custom bounded memory rejected.");
            red = Spawn("red", 4f);
            FacePair();
            yield return Wait(() => Fighting(blue, red), 12d, "expiry encounter");
            KnockOut(blue);
            yield return Wait(() => AI(blue).Threat == null, 2d, "own KO before expiry");
            double paused = AI(blue).RecentEnemyRemainingSeconds, pauseStart = Now;
            while (Now - pauseStart < 1.2d) yield return null;
            Require(paused > 0 && Math.Abs(AI(blue).RecentEnemyRemainingSeconds - paused) < 0.1d, "Short custom duration expired in own KO.");
            KnockOut(red);
            // Let only Blue recover; Red remains incapacitated after the shared physiology advance.
            AdvanceRecovery();
            Wound(red, BodyRegion.Head, 1f, 0f);
            yield return Wait(() => Condition(blue).CanPerformActiveActions, 7d, "expiry owner recovery");
            yield return Wait(() => AI(blue).RecentEnemyActorInstanceId == null, 2d, "capable owner bounded expiry while enemy stays KO");
            Require(!Condition(red).CanPerformActiveActions && AI(blue).Threat == null, "Expiry fixture or inactive-enemy policy failed.");
            Debug.Log("[P3][EXPIRY] Configurable real-time window, own KO pause, remote KO does not pause owner PASS");
            Remove(blue); Remove(red);
        }

        private static ActorRuntimeIdentity Spawn(string faction, float z)
        {
            Require(ActorSpawnService.TrySpawn(Profile, Point(z), Quaternion.identity, out var actor, out string failure), "Spawn failed: " + failure);
            actor.name = "P3 " + faction;
            actor.GetComponent<ActorBehaviorController>().ConfigureAmbient(93101L);
            var affiliation = actor.GetComponent<ActorAffiliationComponent>() ?? actor.gameObject.AddComponent<ActorAffiliationComponent>();
            Require(affiliation.TryConfigure(faction, faction, new[] { faction == "blue" ? "red" : "blue" }, out failure), failure);
            var acquisition = actor.GetComponent<ActorThreatAcquisitionController>() ?? actor.gameObject.AddComponent<ActorThreatAcquisitionController>();
            Require(acquisition.TryConfigure(93102L, out failure), failure);
            acquisition.enabled = true;
            var inventory = actor.GetComponent<InventoryComponent>();
            foreach (var entry in inventory.Entries.Where(e => e.DefinitionId == Ammo).ToArray())
                Require(inventory.TryGetEntryByInstanceId(entry.Item.InstanceId, out int index, out var live) && inventory.TryRemoveItemAt(index, live.Quantity), "Remove fixture ammo failed.");
            SetRounds(actor, 0); // Real encounter, empty weapons: fixture prevents uncontrolled wounds, never bypasses AI gates.
            return actor;
        }
        private static Vector3 Point(float z) => GameObject.Find(M41SampleSceneNavigationTools.FixtureRootName).transform.TransformPoint(new Vector3(-4f, 0f, z));
        private static void Move(ActorRuntimeIdentity actor, Vector3 position)
        {
            var agent = actor.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh) Require(agent.Warp(position), "Fixture warp failed.");
            else actor.transform.position = position;
        }
        private static void FacePair() => FaceActors(blue, red);
        private static void FaceActors(ActorRuntimeIdentity left, ActorRuntimeIdentity right)
        {
            foreach (var pair in new[] { (left, right), (right, left) })
            {
                Vector3 direction = Vector3.ProjectOnPlane(pair.Item2.transform.position - pair.Item1.transform.position, Vector3.up);
                pair.Item1.transform.rotation = Quaternion.LookRotation(direction);
                pair.Item1.GetComponent<ActorGazeController>().Configure(93103L);
            }
            Physics.SyncTransforms();
        }
        private static bool Fighting(ActorRuntimeIdentity actor, ActorRuntimeIdentity target) => AI(actor).Threat == target && AI(actor).State == HumanEncounterAIState.Fighting;
        private static void KnockOut(ActorRuntimeIdentity actor) { Wound(actor, BodyRegion.Head, 1f, 0f); Require(Condition(actor).IsUnconscious, "KO fixture failed."); }
        private static void Wound(ActorRuntimeIdentity actor, BodyRegion region, float severity, float bleeding) =>
            Require(actor.GetComponent<ActorMedicalStateComponent>().TryApplyWound("wound_" + Guid.NewGuid().ToString("N"), region, WoundType.Blunt, severity, bleeding, 0f, out string failure), "Wound failed: " + failure);
        private static void AdvanceRecovery() => Require(WorldClock.Current.TryAdvanceGameTime(2d * WorldClock.SecondsPerHour, out _), "Recovery advance failed.");
        private static ItemInstance Firearm(ActorRuntimeIdentity actor)
        {
            Require(WeaponCombatService.TryGetEquippedWeapon(actor.GetComponent<ActorItemOwnershipComponent>(), out ItemInstance weapon, out _, out FirearmProfileDefinition firearm, out _) && firearm != null, "Missing fixture firearm.");
            return weapon;
        }
        private static void SetRounds(ActorRuntimeIdentity actor, int count) => Require(Firearm(actor).TrySetFirearmState(count == 0 ? null : "core:ammo_303_british_01_profile", count, out _), "Fixture rounds failed.");
        private static int AmmoCount(ActorRuntimeIdentity actor) => actor.GetComponent<ActorItemOwnershipComponent>().GetAllOwnedEntries().Where(e => e.DefinitionId == Ammo).Sum(e => e.Quantity);
        private static void Remove(ActorRuntimeIdentity actor) => Require(ActorSpawnService.TryRemoveRuntimeRepresentationForRestore(actor.ActorInstanceId, out _), "Runtime removal failed.");
        private static IEnumerator Wait(Func<bool> predicate, double timeout, string boundary)
        {
            double begin = Now;
            while (!predicate()) { Require(Now - begin < timeout, "P3 timed out: " + boundary); yield return null; }
        }
        private static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
    }
}
