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
using UnityEngine.SceneManagement;

namespace OldScars.Editor
{
    [InitializeOnLoad]
    public static class M41NpcSandboxDiagnostics
    {
        private const string PendingKey = "OldScars.M41_3.Pending";
        private const string PhaseKey = "OldScars.M41_3.Phase";
        private const string RootKey = "OldScars.M41_3.Root";
        private const string ProfileId = SandboxNpcController.SandboxActorProfileId;
        private const string LoadoutId = "core:debug_sandbox_npc_loadout_01";
        private const string PlayerFirearmId = "core:semi_automatic_rifle_01";
        private const string AmmoId = "core:ammo_303_british_01";
        private static readonly List<string> spawnedActorIds = new List<string>();
        private static readonly Dictionary<string, Vector3> initialPositions = new Dictionary<string, Vector3>();
        private static float observationStarted;
        private static int observationStartFrame;
        private static string deadActorId;
        private static string preDeathBelongings;
        private static int deathFrame;

        static M41NpcSandboxDiagnostics() => EditorApplication.update += Update;

        public static void RunBatchWorldRuntime()
        {
            if (!Application.isBatchMode || EditorApplication.isCompiling)
                throw new InvalidOperationException("M41.3 WorldRuntime diagnostics require compiled Unity batchmode.");
            string root = Path.Combine(Path.GetTempPath(), "OldScars_M41_3_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            SessionState.SetString(RootKey, root);
            SessionState.SetInt(PhaseKey, 0);
            SessionState.SetBool(PendingKey, true);
            EditorSceneManager.OpenScene(WorldApplicationScenes.MainMenuScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void Update()
        {
            if (!SessionState.GetBool(PendingKey, false) || !EditorApplication.isPlaying) return;
            try
            {
                int phase = SessionState.GetInt(PhaseKey, 0);
                if (phase == 0)
                {
                    if (GameDataManager.Instance == null || !GameDataManager.Instance.IsReady) return;
                    ValidateDefinitionContracts(GameDataManager.Instance.Database);
                    WorldSessionService.Close();
                    var store = new PersistenceFileStore(SessionState.GetString(RootKey, string.Empty));
                    WorldSessionOperationResult created = WorldSessionService.Create(
                        "M41.3 NPC Sandbox", new WorldSeed(41303),
                        WorldGenerationSettings.ResolvePreset(WorldSizePreset.Small),
                        LandCoveragePreset.High, GameDataManager.Instance.LoadedContentSet, store);
                    Require(created.Success, "Could not create diagnostic WorldSession: " + created.Failure);
                    SessionState.SetInt(PhaseKey, 1);
                    SceneManager.LoadScene(WorldApplicationScenes.WorldRuntimeSceneName, LoadSceneMode.Single);
                    return;
                }

                WorldRuntimeSceneController runtime = UnityEngine.Object.FindAnyObjectByType<WorldRuntimeSceneController>();
                if (runtime == null || !runtime.GameplayStateReady || runtime.GameplayRuntimeComposition == null) return;
                if (phase == 1)
                {
                    SetupRuntimeEvidence(runtime);
                    observationStarted = Time.time;
                    observationStartFrame = Time.frameCount;
                    SessionState.SetInt(PhaseKey, 2);
                    return;
                }
                if (phase == 2 && Time.time - observationStarted >= 2f &&
                    Time.frameCount - observationStartFrame >= 30)
                {
                    BeginCombatAndDeathEvidence(runtime);
                    deathFrame = Time.frameCount;
                    SessionState.SetInt(PhaseKey, 3);
                    return;
                }
                if (phase == 3 && Time.frameCount > deathFrame)
                {
                    CompleteCorpseAndPersistenceEvidence();
                    Debug.Log("M41.3 NPC Sandbox Spawn & Randomized Loadouts Diagnostics: PASS");
                    Finish(0);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Finish(1);
            }
        }

        private static void ValidateDefinitionContracts(GameDatabase database)
        {
            ActorProfileDefinition actor = database.GetActorProfile(ProfileId);
            ActorLoadoutProfileDefinition loadout = database.GetActorLoadoutProfile(LoadoutId);
            Require(actor != null && actor.loadout_profile_id == LoadoutId && actor.navigation != null && actor.encounter_ai == null,
                "Sandbox actor profile does not preserve the spawn/navigation-only M41.3 boundary.");
            Require(loadout?.groups != null && loadout.groups.Length >= 10,
                "Core sandbox loadout profile was not loaded through the Definition database.");
            Require(loadout.groups.Any(group => group.choices.Any(choice => choice.none)),
                "Actor loadout does not expose explicit NONE outcomes.");

            Require(ActorLoadoutService.ResolveWeightedChoiceIndex(new[] { 2, 3, 5 }, 0) == 0 &&
                    ActorLoadoutService.ResolveWeightedChoiceIndex(new[] { 2, 3, 5 }, 1) == 0 &&
                    ActorLoadoutService.ResolveWeightedChoiceIndex(new[] { 2, 3, 5 }, 2) == 1 &&
                    ActorLoadoutService.ResolveWeightedChoiceIndex(new[] { 2, 3, 5 }, 4) == 1 &&
                    ActorLoadoutService.ResolveWeightedChoiceIndex(new[] { 2, 3, 5 }, 5) == 2 &&
                    ActorLoadoutService.ResolveWeightedChoiceIndex(new[] { 2, 3, 5 }, 9) == 2,
                "Integer cumulative-weight boundary resolution is not exact.");

            ActorLoadoutGroupDefinition weapon = loadout.groups.Single(group => group.id == "weapon");
            foreach (ActorLoadoutChoiceDefinition choice in weapon.choices.Where(value => value.equipment != null))
            {
                foreach (ActorProfileInitialEquipmentEntry equipment in choice.equipment)
                {
                    ItemDefinition item = database.GetItem(equipment.item_id);
                    if (item == null || string.IsNullOrWhiteSpace(item.firearm_profile_id)) continue;
                    FirearmProfileDefinition firearm = database.GetFirearmProfile(item.firearm_profile_id);
                    Require(choice.inventory != null && choice.inventory.Any(entry =>
                            firearm.accepted_ammo_profile_ids.Contains(database.GetItem(entry.item_id)?.ammo_profile_id)),
                        "Weapon package is not data-valid against firearm accepted ammo profiles: " + item.id);
                }
            }
        }

        private static void SetupRuntimeEvidence(WorldRuntimeSceneController runtime)
        {
            spawnedActorIds.Clear();
            initialPositions.Clear();
            GameplayRuntimeComposition composition = runtime.GameplayRuntimeComposition;
            SandboxNpcController sandbox = composition.SandboxNpcController;
            Require(sandbox != null && composition.NeedsPanel != null && composition.InputBlocker != null,
                "Shared WorldRuntime composition lacks the M41.3 F3 sandbox adapter or recent input surfaces.");

            SandboxNpcMetadata first = null;
            string firstError = null;
            Require(sandbox.TrySetBaseSeed("1001", out _) && sandbox.TrySpawnRandomNpc(out first, out firstError),
                "First reproducibility spawn failed: " + firstError);
            string signature = first.LoadoutSignature;
            string firstId = first.GetComponent<ActorRuntimeIdentity>().ActorInstanceId;
            Require(ActorSpawnService.TryRemoveRuntimeRepresentationForRestore(firstId, out string removeFirst),
                "Could not remove first reproducibility actor: " + removeFirst);
            SandboxNpcMetadata second = null;
            string secondError = null;
            Require(sandbox.TrySetBaseSeed("2002", out _) && sandbox.TrySetBaseSeed("1001", out _) &&
                    sandbox.TrySpawnRandomNpc(out second, out secondError),
                "Second reproducibility spawn failed: " + secondError);
            Require(second.LoadoutSignature == signature && second.DerivedSpawnSeed == first.DerivedSpawnSeed,
                "Same controlled seed/input did not reproduce the exact loadout signature.");
            Require(ActorSpawnService.TryRemoveRuntimeRepresentationForRestore(
                    second.GetComponent<ActorRuntimeIdentity>().ActorInstanceId, out string removeSecond),
                "Could not remove second reproducibility actor: " + removeSecond);

            Require(sandbox.TrySetBaseSeed(SandboxNpcController.DefaultBaseSeed.ToString(), out _),
                "Could not select the controlled variation seed.");
            var signatures = new HashSet<string>(StringComparer.Ordinal);
            var actorIds = new HashSet<string>(StringComparer.Ordinal);
            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            bool observedNone = false;
            for (int index = 0; index < 12; index++)
            {
                Require(sandbox.TrySpawnRandomNpc(out SandboxNpcMetadata metadata, out string spawnError),
                    "Multi-NPC spawn " + index + " failed: " + spawnError);
                ActorRuntimeIdentity identity = metadata.GetComponent<ActorRuntimeIdentity>();
                Require(actorIds.Add(identity.ActorInstanceId), "Duplicate ActorInstanceId in simultaneous sandbox actors.");
                signatures.Add(metadata.LoadoutSignature);
                spawnedActorIds.Add(identity.ActorInstanceId);
                initialPositions[identity.ActorInstanceId] = identity.transform.position;
                ActorEquipmentComponent equipment = identity.GetComponent<ActorEquipmentComponent>();
                InventoryComponent inventory = identity.GetComponent<InventoryComponent>();
                ActorItemOwnershipComponent ownership = identity.GetComponent<ActorItemOwnershipComponent>();
                string ownershipError = null;
                Require(equipment != null && inventory != null && ownership != null && ownership.ValidateUniqueOwnership(out ownershipError),
                    "Spawned actor lacks real equipment/inventory/ownership: " + ownershipError);
                foreach (ItemStorageEntry entry in ownership.GetAllOwnedEntries())
                    Require(itemIds.Add(entry.Item.InstanceId), "Duplicate ItemInstanceId across sandbox NPCs: " + entry.Item.InstanceId);
                Require(identity.GetComponent<ActorNavigationController>() != null &&
                        identity.GetComponent<ActorBehaviorController>()?.IsAmbientConfigured == true,
                    "Spawned actor lacks navigation plus configured behavior ownership.");
                observedNone |= equipment.Entries.Count == 0 || inventory.Entries.Count == 0;
            }
            Require(signatures.Count >= 5, "Controlled repeated spawns did not produce useful loadout variation.");
            Require(observedNone, "Controlled spawn corpus did not exercise any intentional empty loadout outcome.");
            Debug.Log("[M41.3][MULTI_NPC] Actors=12 UniqueActors=" + actorIds.Count +
                      " UniqueItems=" + itemIds.Count + " UniqueSignatures=" + signatures.Count);
        }

        private static void BeginCombatAndDeathEvidence(WorldRuntimeSceneController runtime)
        {
            ActorRuntimeIdentity[] actors = spawnedActorIds.Select(id =>
                ActorRuntimeRegistry.TryGet(id, out ActorRuntimeIdentity actor) ? actor : null).Where(actor => actor != null).ToArray();
            Require(actors.Length == 12, "One or more simultaneous sandbox actors disappeared before validation.");
            Require(actors.Any(actor => actor.GetComponent<ActorBehaviorController>().AmbientDistanceTravelled > 0.5f &&
                                       Vector3.Distance(initialPositions[actor.ActorInstanceId], actor.transform.position) > 0.05f),
                "Sandbox roaming did not produce physically travelled distance through behavior ownership. " +
                "NavMeshVertices=" + UnityEngine.AI.NavMesh.CalculateTriangulation().vertices.Length + "; " +
                string.Join("; ", actors.Select(actor => actor.ActorInstanceId + "=" +
                    "owner:" + actor.GetComponent<ActorBehaviorController>().Owner +
                    ",lifecycle:" + actor.LifecycleState +
                    ",accepted:" + actor.GetComponent<ActorBehaviorController>().AmbientAcceptedOrderCount +
                    ",travel:" + actor.GetComponent<ActorBehaviorController>().AmbientDistanceTravelled.ToString("0.###") +
                    ",failed:" + actor.GetComponent<ActorBehaviorController>().AmbientFailedDecisionCount +
                    ",initial:" + initialPositions[actor.ActorInstanceId].ToString("F2") +
                    ",current:" + actor.transform.position.ToString("F2") +
                    ",onNavMesh:" + actor.GetComponent<ActorNavigationController>().Agent.isOnNavMesh +
                    ",detail:" + actor.GetComponent<ActorBehaviorController>().LastAmbientDecisionFailure)));

            ActorRuntimeIdentity target = actors.First(actor => !actor.GetComponent<ActorEquipmentComponent>().Entries.Any(entry =>
            {
                ItemDefinition definition = GameDataManager.Instance.Database.GetItem(entry.DefinitionId);
                return definition != null && !string.IsNullOrWhiteSpace(definition.armor_profile_id);
            }));
            target.GetComponent<ActorNavigationController>().Stop();
            target.transform.rotation = Quaternion.identity;
            Physics.SyncTransforms();
            Collider collider = target.GetComponent<Collider>();
            Require(collider != null, "Sandbox NPC lacks its generic physical/body-region receiver collider.");
            AssertSixRegions(target.transform, collider);
            preDeathBelongings = Belongings(target);

            PlayerGameplayComposition player = runtime.PlayerComposition;
            ActorItemOwnershipComponent playerOwnership = player.PlayerContext.GetComponent<ActorItemOwnershipComponent>();
            InventoryComponent playerInventory = player.PlayerContext.GetComponent<InventoryComponent>();
            ActorEquipmentComponent playerEquipment = player.PlayerContext.GetComponent<ActorEquipmentComponent>();
            ItemInstance firearm = playerInventory.AddItemByDefinitionId(PlayerFirearmId, 1);
            Require(firearm != null, "Could not create the player's real M41.2 firearm.");
            EquipmentPreview preview = playerEquipment.PreviewEquip(firearm.InstanceId);
            Require(preview.Success && !preview.RequiresChoice && playerEquipment.Equip(preview).Success,
                "Could not equip the player's real firearm through Equipment authority.");
            Require(playerInventory.AddItemByDefinitionId(AmmoId, 20) != null &&
                    WeaponCombatService.ReloadEquipped(playerOwnership, firearm.InstanceId).Success,
                "Could not load the player's real firearm from owned compatible ammo.");

            Vector3 head = RegionPoint(collider.bounds, 0f, 0.9f);
            Vector3 leftLeg = RegionPoint(collider.bounds, -0.65f, 0.25f);
            WeaponCombatResult headShot = WeaponCombatService.FireEquipped(playerOwnership, firearm.InstanceId, collider, head);
            Require(headShot.Success && headShot.Combat.Region == BodyRegion.Head,
                "Player firearm did not resolve localized Head damage: " + headShot.Message);
            WeaponCombatResult legShot = WeaponCombatService.FireEquipped(playerOwnership, firearm.InstanceId, collider, leftLeg);
            Require(legShot.Success && legShot.Combat.Region == BodyRegion.LeftLeg,
                "Player firearm did not resolve localized LeftLeg damage: " + legShot.Message);
            ActorHealthComponent health = target.GetComponent<ActorHealthComponent>();
            while (!health.IsDead && firearm.LoadedRounds > 0)
            {
                WeaponCombatResult finishingShot = WeaponCombatService.FireEquipped(
                    playerOwnership, firearm.InstanceId, collider, RegionPoint(collider.bounds, 0f, 0.65f));
                Require(finishingShot.Success, "Player firearm could not complete normal health/lifecycle death: " + finishingShot.Message);
            }
            if (!health.IsDead)
            {
                string clockFailure = WorldClock.Current == null ? "WorldClock authority is unavailable." : null;
                Require(WorldClock.Current != null &&
                        WorldClock.Current.TryAdvanceGameTime(WorldClock.SecondsPerHour, out clockFailure),
                    "Could not advance the existing WorldClock to resolve firearm wound bleeding: " + clockFailure);
            }
            Require(health.IsDead && target.LifecycleState == ActorLifecycleState.Dead,
                "Sandbox NPC did not die through the existing M39/M40 health/lifecycle path.");
            deadActorId = target.ActorInstanceId;
        }

        private static void CompleteCorpseAndPersistenceEvidence()
        {
            Require(ActorRuntimeRegistry.TryGet(deadActorId, out ActorRuntimeIdentity target),
                "Dead sandbox actor representation disappeared before corpse validation.");
            Require(target.GetComponent<ActorBehaviorController>()?.Owner == ActorBehaviorOwner.Inactive,
                "Dead sandbox actor retained active behavior ownership.");
            LootableActorInventoryComponent corpse = target.GetComponent<LootableActorInventoryComponent>();
            corpse.RefreshLootableState();
            Require(corpse.CanOpenStorage(out string corpseError), "Dead sandbox actor is not lootable: " + corpseError);
            string afterDeath = Belongings(target);
            Require(afterDeath == preDeathBelongings, "Death changed the actor's exact ItemInstance/equipment/inventory state.");
            InventoryUISessionController inventorySession = UnityEngine.Object.FindAnyObjectByType<InventoryUISessionController>();
            Require(inventorySession != null, "Shared WorldRuntime inventory session is unavailable for corpse reopen validation.");
            inventorySession.OpenExternal(corpse, null, default, null);
            Require(inventorySession.State == InventoryUISessionState.External &&
                    Belongings(target) == preDeathBelongings,
                "Opening the corpse changed or rerolled belongings.");
            inventorySession.CloseSession();
            Require(inventorySession.State == InventoryUISessionState.Closed,
                "Shared inventory session did not close after corpse inspection.");
            inventorySession.OpenExternal(corpse, null, default, null);
            Require(inventorySession.State == InventoryUISessionState.External &&
                    Belongings(target) == preDeathBelongings,
                "Reopening the corpse changed or rerolled belongings.");
            inventorySession.CloseSession();

            ActorRuntimeIdentity[] actors = spawnedActorIds.Select(id =>
                ActorRuntimeRegistry.TryGet(id, out ActorRuntimeIdentity actor) ? actor : null).Where(actor => actor != null).ToArray();
            string root = SessionState.GetString(RootKey, string.Empty);
            var store = new PersistenceFileStore(root);
            CurrentSliceResult capture = CurrentSliceSnapshotService.Capture();
            Require(capture.Success, "M41.3 Current Slice capture failed: " + capture.Failure);
            PersistenceWriteResult write = store.Write("m41_3_sandbox", CurrentSliceSnapshotService.ToPayload(capture.Snapshot));
            Require(write.Success, "M41.3 Current Slice write failed: " + write.Failure);
            string persistedActorId = actors.First(actor => actor.LifecycleState == ActorLifecycleState.Alive).ActorInstanceId;
            string persistedBelongings = Belongings(actors.First(actor => actor.ActorInstanceId == persistedActorId));
            Require(ActorSpawnService.TryRemoveRuntimeRepresentationForRestore(persistedActorId, out string removeError),
                "Could not teardown persisted sandbox actor: " + removeError);
            CurrentSliceLoadResult load = CurrentSliceLoadService.Load("m41_3_sandbox", store);
            Require(load.Success, "M41.3 Current Slice restore failed: " + load.Failure);
            Require(ActorRuntimeRegistry.TryGet(persistedActorId, out ActorRuntimeIdentity restored) &&
                    Belongings(restored) == persistedBelongings,
                "Persistence restore did not preserve exact actor/items/equipment/ownership without reroll.");
            CurrentSliceResult recapture = CurrentSliceSnapshotService.Capture();
            Require(recapture.Success && CurrentSliceSnapshotService.Compare(capture.Snapshot, recapture.Snapshot).Equivalent,
                "M41.3 Current Slice round-trip changed represented sandbox state.");
        }

        private static string Belongings(ActorRuntimeIdentity actor)
        {
            ActorItemOwnershipComponent ownership = actor.GetComponent<ActorItemOwnershipComponent>();
            ActorEquipmentComponent equipment = actor.GetComponent<ActorEquipmentComponent>();
            string failure = null;
            Require(ownership != null && equipment != null && ownership.ValidateUniqueOwnership(out failure),
                "Actor ownership invalid while comparing belongings: " + failure);
            return string.Join("|", ownership.GetAllOwnedEntries().Where(entry => entry?.Item != null)
                .Select(entry => entry.Item.InstanceId + ":" + entry.DefinitionId + ":" + entry.Quantity + ":" +
                                 string.Join("+", equipment.GetSlotsOccupiedBy(entry.Item.InstanceId)))
                .OrderBy(value => value, StringComparer.Ordinal));
        }

        private static void AssertSixRegions(Transform actor, Collider collider)
        {
            Require(CombatResolutionService.ResolveBodyRegion(actor, collider, RegionPoint(collider.bounds, 0f, 0.9f)) == BodyRegion.Head, "Head region mapping failed.");
            Require(CombatResolutionService.ResolveBodyRegion(actor, collider, RegionPoint(collider.bounds, 0f, 0.65f)) == BodyRegion.Torso, "Torso region mapping failed.");
            Require(CombatResolutionService.ResolveBodyRegion(actor, collider, RegionPoint(collider.bounds, -0.65f, 0.65f)) == BodyRegion.LeftArm, "LeftArm region mapping failed.");
            Require(CombatResolutionService.ResolveBodyRegion(actor, collider, RegionPoint(collider.bounds, 0.65f, 0.65f)) == BodyRegion.RightArm, "RightArm region mapping failed.");
            Require(CombatResolutionService.ResolveBodyRegion(actor, collider, RegionPoint(collider.bounds, -0.65f, 0.25f)) == BodyRegion.LeftLeg, "LeftLeg region mapping failed.");
            Require(CombatResolutionService.ResolveBodyRegion(actor, collider, RegionPoint(collider.bounds, 0.65f, 0.25f)) == BodyRegion.RightLeg, "RightLeg region mapping failed.");
        }

        private static Vector3 RegionPoint(Bounds bounds, float normalizedX, float normalizedY) =>
            new Vector3(bounds.center.x + bounds.extents.x * normalizedX,
                bounds.min.y + bounds.size.y * normalizedY, bounds.center.z);

        private static void Finish(int exitCode)
        {
            SessionState.SetBool(PendingKey, false);
            WorldSessionService.Close();
            string root = SessionState.GetString(RootKey, string.Empty);
            if (Directory.Exists(root)) Directory.Delete(root, true);
            EditorApplication.Exit(exitCode);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
