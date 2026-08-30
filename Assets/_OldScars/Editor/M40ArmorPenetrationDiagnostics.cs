using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using OldScars.Core;
using OldScars.Core.Actors;
using OldScars.Core.Combat;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Data.Loading;
using OldScars.Core.Data.Validation;
using OldScars.Core.Interactions;
using OldScars.Core.Items;
using OldScars.Core.Persistence;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace OldScars.Editor
{
    [InitializeOnLoad]
    public static class M40ArmorPenetrationDiagnostics
    {
        private const string Menu = "Old Scars/Diagnostics/Combat/Run M40.1 Armor & Penetration";
        private const string ManualMenu = "Old Scars/Diagnostics/Combat/M40.1 Prepare or Cycle Manual Armor Target";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string Prefix = "OldScars.M40_1.Armor.";
        private const string PhaseKey = Prefix + "Phase";
        private const string RootKey = Prefix + "Root";
        private const string ErrorKey = Prefix + "Error";
        private const string TargetActorKey = Prefix + "TargetActor";
        private const string ArmorKey = Prefix + "Armor";
        private const string RifleKey = Prefix + "Rifle";
        private const string ArmorConditionKey = Prefix + "ArmorCondition";
        private const string EnterA = "enter_a";
        private const string ExitA = "exit_a";
        private const string EnterB = "enter_b";
        private const string Finish = "finish";
        private const string InitialSlot = "m40_1_initial";
        private const string TargetSlot = "m40_1_target";
        private const string RifleId = "core:lee_enfield_rifle_01";
        private const string CrowbarId = "core:rusted_crowbar_01";
        private const string AmmoItemId = "core:ammo_303_british_01";
        private const string AmmoProfileId = "core:ammo_303_british_01_profile";
        private const string ArmorItemId = "core:debug_torso_armor_01";
        private const string ArmorProfileId = "core:debug_torso_armor_01_profile";
        private const string ArmorPenetrationId = "core:debug_torso_armor_penetration_01";
        private const string ThinWorldProfileId = "core:debug_thin_penetrable_cover_01";
        private const string ResistantWorldProfileId = "core:debug_resistant_penetrable_cover_01";
        private const string TorsoOuterSlotId = "core:torso_outer", TorsoMiddleSlotId = "core:torso_middle";
        private const float Epsilon = 0.0001f;
        static M40ArmorPenetrationDiagnostics() => EditorApplication.update += Continue;
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("M40.1 diagnostics require idle Edit Mode.");
            ClearSession();
            string root = Path.Combine(Path.GetTempPath(), "OldScars_M40_1_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            SessionState.SetString(RootKey, root);
            SessionState.SetString(ErrorKey, string.Empty);
            SessionState.SetString(PhaseKey, EnterA);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        private static void PrepareOrToggleManualTarget() { try {
                Require(EditorApplication.isPlaying && GameDataManager.Instance?.IsReady == true, "Manual setup requires an active Play session with loaded game data.");
                ActorInteractionContext player = Player();
                ActorRuntimeIdentity target = ActorRuntimeRegistry.ActiveRepresentations.FirstOrDefault(identity => identity != null && identity.LifecycleState == ActorLifecycleState.Alive && identity.GetComponent<ActorItemOwnershipComponent>()?.GetAllOwnedEntries().Any(entry => entry.DefinitionId == ArmorItemId) == true);
                if (target == null) {
                    GameDatabase database = GameDataManager.Instance.Database;
                    ActorRuntimeIdentity source = ActorRuntimeRegistry.ActiveRepresentations.First(identity => identity != null && identity.OriginKind == ActorOriginKind.Authored && identity.LifecycleState == ActorLifecycleState.Alive && !string.IsNullOrWhiteSpace(database.GetActorProfile(identity.ActorProfileId)?.equipment_layout_id));
                    target = Spawn(source.ActorProfileId, player.transform.position + player.transform.forward * 3f);
                    target.name = "M40.1 Manual Armor Target"; target.transform.rotation = Quaternion.LookRotation(player.transform.position - target.transform.position, Vector3.up);
                    ActorItemOwnershipComponent createdOwnership = target.GetComponent<ActorItemOwnershipComponent>(); EquipArmor(createdOwnership, createdOwnership.PersonalInventory.AddItemByDefinitionId(ArmorItemId, 1), TorsoOuterSlotId); EquipArmor(createdOwnership, createdOwnership.PersonalInventory.AddItemByDefinitionId(ArmorItemId, 1), TorsoMiddleSlotId);
                } else {
                    ActorItemOwnershipComponent ownership = target.GetComponent<ActorItemOwnershipComponent>();
                    ItemInstance[] armors = ownership.GetAllOwnedEntries().Where(entry => entry.DefinitionId == ArmorItemId).Select(entry => entry.Item).OrderBy(item => item.InstanceId, StringComparer.Ordinal).ToArray(); Require(armors.Length == 2, "Manual target must own exactly two armor fixture instances.");
                    int equipped = armors.Count(item => ownership.Equipment.IsEquipped(item.InstanceId)); if (equipped == 2) UnequipArmor(ownership, armors[1]); else if (equipped == 1) UnequipArmor(ownership, armors.Single(item => ownership.Equipment.IsEquipped(item.InstanceId))); else { EquipArmor(ownership, armors[0], TorsoOuterSlotId); EquipArmor(ownership, armors[1], TorsoMiddleSlotId); }
                }
                ActorItemOwnershipComponent finalOwnership = target.GetComponent<ActorItemOwnershipComponent>(); ItemInstance[] finalArmors = finalOwnership.GetAllOwnedEntries().Where(entry => entry.DefinitionId == ArmorItemId).Select(entry => entry.Item).OrderBy(item => item.InstanceId, StringComparer.Ordinal).ToArray(); int finalEquipped = finalArmors.Count(item => finalOwnership.Equipment.IsEquipped(item.InstanceId));
                Debug.Log($"[M40.1 Manual Setup] Target='{target.name}' ActorInstanceId='{target.ActorInstanceId}' ArmorInstanceIds='{string.Join(",", finalArmors.Select(item => item.InstanceId))}' Mode='{(finalEquipped == 2 ? "StoppedTwoLayers" : finalEquipped == 1 ? "PenetratedOneLayer" : "UnarmoredInventoryOnly")}'.");
            } catch (Exception exception) { Debug.LogError("[M40.1 Manual Setup] " + exception.Message); } }
        private static void Continue()
        {
            string phase = SessionState.GetString(PhaseKey, string.Empty);
            if (string.IsNullOrWhiteSpace(phase)) return;
            if ((phase == EnterA || phase == EnterB) && EditorApplication.isPlaying && WorldClock.Current != null)
                WorldClock.Current.AdvanceDuringGameplay = false;
            if (phase == EnterA && Ready()) { Execute(RunSessionA, ExitA); return; }
            if (phase == ExitA && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                SessionState.SetString(PhaseKey, EnterB);
                EditorApplication.EnterPlaymode();
                return;
            }
            if (phase == EnterB && Ready()) { Execute(RunSessionB, Finish); return; }
            if (phase == Finish && !EditorApplication.isPlayingOrWillChangePlaymode) FinalizeRun();
        }
        private static bool Ready() => EditorApplication.isPlaying && Time.frameCount >= 5 && WorldClock.Current != null &&
                                       GameDataManager.Instance != null && GameDataManager.Instance.IsReady;
        private static void Execute(Action action, string next)
        {
            try { action(); SessionState.SetString(PhaseKey, next); }
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
            ActorInteractionContext player = Player();
            ActorItemOwnershipComponent playerOwnership = player.GetComponent<ActorItemOwnershipComponent>();
            InventoryComponent playerInventory = playerOwnership?.PersonalInventory;
            ActorEquipmentComponent playerEquipment = playerOwnership?.Equipment;
            FirearmDebugController input = player.GetComponent<FirearmDebugController>();
            Require(playerInventory != null && playerEquipment != null && input != null, "A. Player combat/equipment foundations are incomplete.");
            CurrentSliceSaveData initial = Capture("M40.1 initial");
            Write(InitialSlot, initial);
            GameDatabase database = GameDataManager.Instance.Database;
            ArmorProfileDefinition armorProfile = database.GetArmorProfile(ArmorProfileId);
            PenetrationProfileDefinition armorPenetration = database.GetPenetrationProfile(ArmorPenetrationId);
            AmmoProfileDefinition ammo = database.GetAmmoProfile(AmmoProfileId);
            ItemDefinition armorDefinition = database.GetItem(ArmorItemId);
            Require(GameDataManager.Instance.Report.ErrorCount == 0 && armorProfile != null && armorPenetration != null &&
                    ammo != null && armorDefinition?.armor_profile_id == ArmorProfileId &&
                    armorProfile.covered_regions.SequenceEqual(new[] { BodyRegion.Torso.ToString() }) &&
                    Near(armorPenetration.resistance, .325f) && Near(ammo.penetration_power, .65f) &&
                    database.GetWorldObjectProfile(ThinWorldProfileId)?.penetration_profile_id == "core:debug_thin_cover_01" &&
                    database.GetWorldObjectProfile(ResistantWorldProfileId)?.penetration_profile_id == "core:debug_resistant_cover_01" &&
                    ContentId.TryParse(ArmorProfileId, out _, out _) && ContentId.TryParse(ArmorPenetrationId, out _, out _),
                "A. Data-driven armor, penetration, world-surface or canonical ID contracts are invalid.");
            ItemInstance rifle = playerInventory.AddItemByDefinitionId(RifleId, 1);
            ItemInstance crowbar = playerInventory.AddItemByDefinitionId(CrowbarId, 1);
            Require(rifle != null && crowbar != null && playerInventory.AddItemByDefinitionId(AmmoItemId, 4) != null,
                "A/O. Could not create combat fixtures.");
            int looseAmmo = Quantity(playerOwnership, AmmoItemId);
            EquipWeapon(playerEquipment, playerInventory, rifle.InstanceId);
            ActorRuntimeIdentity source = ActorRuntimeRegistry.ActiveRepresentations.First(identity =>
                identity != null && identity.GetComponent<ActorInteractionContext>()?.ActorTags.Contains("player") != true &&
                identity.OriginKind == ActorOriginKind.Authored && identity.LifecycleState == ActorLifecycleState.Alive &&
                !string.IsNullOrWhiteSpace(database.GetActorProfile(identity.ActorProfileId)?.equipment_layout_id));
            ActorRuntimeIdentity target = Spawn(source.ActorProfileId, player.transform.position + player.transform.right * 1.1f);
            Collider targetCollider = target.GetComponent<Collider>();
            ActorMedicalStateComponent medical = target.GetComponent<ActorMedicalStateComponent>();
            ActorItemOwnershipComponent targetOwnership = target.GetComponent<ActorItemOwnershipComponent>();
            ItemInstance armor = targetOwnership.PersonalInventory.AddItemByDefinitionId(ArmorItemId, 1);
            Require(targetCollider != null && medical != null && armor != null && target.OriginKind == ActorOriginKind.Runtime,
                "B. Runtime target or armor fixture is incomplete.");
            Physics.SyncTransforms();
            Vector3 torso = Point(targetCollider.bounds, 0f, .65f);
            Reset(target);
            WeaponCombatResult inventoryOnly = FireDirect(playerOwnership, rifle, targetCollider, torso, null);
            AssertSingleWound(inventoryOnly, medical, BodyRegion.Torso, WoundType.Puncture, .65f, ArmorResolutionOutcome.Unarmored,
                ArmorCoverageStatus.NoArmorEquipped, "B/D inventory-only armor");
            EquipArmor(targetOwnership, armor);
            Reset(target);
            WeaponCombatResult stopped = FireDirect(playerOwnership, rifle, targetCollider, torso, .25f);
            AssertSingleWound(stopped, medical, BodyRegion.Torso, WoundType.Blunt, null, ArmorResolutionOutcome.Stopped,
                ArmorCoverageStatus.Covered, "E/F stopped blunt transfer");
            Require(stopped.Combat.Armor.ArmorItemInstanceId == armor.InstanceId && stopped.Combat.Armor.ArmorProfileId == ArmorProfileId &&
                    stopped.Combat.Armor.Penetration.DecisiveProfileId == ArmorPenetrationId,
                "E/F. Typed armor metadata did not identify the equipped ItemInstance/profile.");
            Reset(target);
            WeaponCombatResult noTrauma = FireDirect(playerOwnership, rifle, targetCollider, torso, .1f);
            Require(noTrauma.Success && noTrauma.Combat.Code == CombatResolutionCode.ResolvedNoWound &&
                    noTrauma.Combat.Armor.Outcome == ArmorResolutionOutcome.Stopped && medical.WoundCount == 0,
                "G. Below-threshold stopped impact created a medical wound: " + noTrauma.Message);
            Reset(target);
            WeaponCombatResult equality = FireDirect(playerOwnership, rifle, targetCollider, torso, .325f);
            Require(equality.Combat.Armor.Outcome == ArmorResolutionOutcome.Stopped &&
                    equality.Combat.FinalWoundType == WoundType.Blunt && medical.GetWounds(BodyRegion.Torso).Count() == 1,
                "H. Exact penetration == resistance was not deterministic Stopped.");
            Reset(target);
            WeaponCombatResult penetrated = FireDirect(playerOwnership, rifle, targetCollider, torso, 1f);
            AssertSingleWound(penetrated, medical, BodyRegion.Torso, WoundType.Puncture, .43875f,
                ArmorResolutionOutcome.Penetrated, ArmorCoverageStatus.Covered, "I/J residual penetration");
            Require(Near(penetrated.Combat.Armor.ResidualPower, .675f) && penetrated.Combat.FinalSeverity <= .65f,
                "I/J. Penetrated armor residual/severity was not bounded and explainable.");
            AssertUncovered(playerOwnership, rifle, target, armor, BodyRegion.Head, Point(targetCollider.bounds, 0f, .9f));
            AssertUncovered(playerOwnership, rifle, target, armor, BodyRegion.LeftArm, Point(targetCollider.bounds, -.9f, .65f));
            AssertUncovered(playerOwnership, rifle, target, armor, BodyRegion.RightArm, Point(targetCollider.bounds, .9f, .65f));
            AssertUncovered(playerOwnership, rifle, target, armor, BodyRegion.LeftLeg, Point(targetCollider.bounds, -.4f, .25f));
            AssertUncovered(playerOwnership, rifle, target, armor, BodyRegion.RightLeg, Point(targetCollider.bounds, .4f, .25f));
            ArmorLayerInput outer = new ArmorLayerInput("outer", "outer_profile", "outer_pen", 20, .3f, .2f, 0f, 1f);
            ArmorLayerInput inner = new ArmorLayerInput("inner", "inner_profile", "inner_pen", 10, .2f, .1f, 0f, 1f);
            ArmorResolution orderA = CombatResolutionService.ResolveArmorLayers(CombatAttackKind.Firearm, .4f,
                ArmorCoverageStatus.Covered, new[] { inner, outer });
            ArmorResolution orderB = CombatResolutionService.ResolveArmorLayers(CombatAttackKind.Firearm, .4f,
                ArmorCoverageStatus.Covered, new[] { outer, inner });
            Require(orderA.Outcome == ArmorResolutionOutcome.Stopped && orderB.Outcome == ArmorResolutionOutcome.Stopped &&
                    orderA.ArmorItemInstanceId == "inner" && orderB.ArmorItemInstanceId == "inner" &&
                    Near(orderA.Resistance, .5f) && Near(orderB.Resistance, .5f),
                "L. Multiple armor layers depended on input/list order.");
            target.transform.position = player.transform.position + player.transform.right * 1.1f;
            Physics.SyncTransforms();
            torso = Point(targetCollider.bounds, 0f, .65f);
            EquipWeapon(playerEquipment, playerInventory, crowbar.InstanceId);
            Reset(target);
            WeaponCombatResult protectedMelee = WeaponCombatService.StrikeEquipped(playerOwnership, crowbar.InstanceId, targetCollider, torso);
            AssertSingleWound(protectedMelee, medical, BodyRegion.Torso, WoundType.Blunt, .25f,
                ArmorResolutionOutcome.Penetrated, ArmorCoverageStatus.Covered, "M protected melee");
            UnequipArmor(targetOwnership, armor);
            Reset(target);
            WeaponCombatResult openMelee = WeaponCombatService.StrikeEquipped(playerOwnership, crowbar.InstanceId, targetCollider, torso);
            AssertSingleWound(openMelee, medical, BodyRegion.Torso, WoundType.Blunt, .45f,
                ArmorResolutionOutcome.Unarmored, ArmorCoverageStatus.NoArmorEquipped, "M unarmored melee regression");
            EquipWeapon(playerEquipment, playerInventory, rifle.InstanceId);
            target.transform.position = player.transform.position + player.transform.right * 2.5f;
            target.transform.rotation = Quaternion.LookRotation(player.transform.position - target.transform.position, Vector3.up);
            Physics.SyncTransforms();
            torso = Point(targetCollider.bounds, 0f, .65f);
            Reset(target);
            PhysicalShotResolution clear = Trace(input, torso, .65f, out Vector3 origin);
            Require(clear.Termination == PhysicalShotTermination.Impact && IsTarget(clear.HitCollider, target) &&
                    clear.PenetratedSurfaceCount == 0 && Near(clear.RemainingPower, .65f),
                "N/W. Clear no-surface M40 path changed.");
            WeaponCombatResult clearShot = FireTrace(playerOwnership, rifle, clear, null);
            AssertSingleWound(clearShot, medical, BodyRegion.Torso, WoundType.Puncture, .65f,
                ArmorResolutionOutcome.Unarmored, ArmorCoverageStatus.NoArmorEquipped, "world clear regression");
            Reset(target);
            PhysicalShotResolution thin = TraceThrough(input, torso, .65f, new[] { ThinWorldProfileId }, out _);
            WeaponCombatResult thinShot = FireTrace(playerOwnership, rifle, thin, null);
            Require(thin.Termination == PhysicalShotTermination.Impact && thin.PenetratedSurfaceCount == 1 && Near(thin.RemainingPower, .5f),
                "World 1. Thin cover did not continue with residual penetration.");
            AssertSingleWound(thinShot, medical, BodyRegion.Torso, WoundType.Puncture, .5f,
                ArmorResolutionOutcome.Unarmored, ArmorCoverageStatus.NoArmorEquipped, "world thin residual wound");
            EquipArmor(targetOwnership, armor); Reset(target); PhysicalShotResolution combined =
                TraceThrough(input, torso, 1f, new[] { ThinWorldProfileId }, out _);
            WeaponCombatResult combinedShot = FireTrace(playerOwnership, rifle, combined, 1f);
            AssertSingleWound(combinedShot, medical, BodyRegion.Torso, WoundType.Puncture, .34125f,
                ArmorResolutionOutcome.Penetrated, ArmorCoverageStatus.Covered, "world cover to wearable armor residual");
            Require(combined.PenetratedSurfaceCount == 1 && Near(combined.RemainingPower, .85f) && Near(combinedShot.Combat.Armor.ResidualPower, .525f),
                "World/wearable adapters did not spend one shared penetration budget.");
            UnequipArmor(targetOwnership, armor);
            Reset(target);
            PhysicalShotResolution resistant = TraceThrough(input, torso, .65f, new[] { ResistantWorldProfileId }, out _);
            WeaponCombatResult resistantShot = FireTrace(playerOwnership, rifle, resistant, null);
            Require(resistant.Termination == PhysicalShotTermination.SurfaceStopped && resistantShot.Quantity == 1 && medical.WoundCount == 0,
                "World 2. Resistant cover did not stop before the actor.");
            Reset(target);
            PhysicalShotResolution two = TraceThrough(input, torso, .65f, new[] { ThinWorldProfileId, ThinWorldProfileId }, out _);
            WeaponCombatResult twoShot = FireTrace(playerOwnership, rifle, two, null);
            Require(two.Termination == PhysicalShotTermination.Impact && two.PenetratedSurfaceCount == 2 && Near(two.RemainingPower, .35f),
                "World 3. Two sequential penetrable layers did not share one bounded budget.");
            AssertSingleWound(twoShot, medical, BodyRegion.Torso, WoundType.Puncture, .35f,
                ArmorResolutionOutcome.Unarmored, ArmorCoverageStatus.NoArmorEquipped, "world two-layer residual wound");
            Reset(target);
            PhysicalShotResolution exhausted = TraceThrough(input, torso, .3f, new[] { ThinWorldProfileId, ThinWorldProfileId }, out _);
            WeaponCombatResult exhaustedShot = FireTrace(playerOwnership, rifle, exhausted, .3f);
            Require(exhausted.Termination == PhysicalShotTermination.SurfaceStopped && exhausted.PenetratedSurfaceCount == 1 &&
                    exhaustedShot.Quantity == 1 && medical.WoundCount == 0,
                "World 4. Exhausted penetration budget reached the actor.");
            Reset(target);
            PhysicalShotResolution limited = TraceThrough(input, torso, 1.2f,
                new[] { ThinWorldProfileId, ThinWorldProfileId, ThinWorldProfileId, ThinWorldProfileId, ThinWorldProfileId }, out _);
            WeaponCombatResult limitedShot = FireTrace(playerOwnership, rifle, limited, 1.2f);
            Require(limited.Termination == PhysicalShotTermination.SurfaceLimitStopped && limited.PenetratedSurfaceCount == 4 &&
                    limitedShot.Quantity == 1 && medical.WoundCount == 0,
                "World budget. Bounded surface limit did not stop the attack.");
            Reset(target);
            PhysicalShotResolution opaque = TraceThrough(input, torso, .65f, new string[] { null }, out _);
            WeaponCombatResult opaqueShot = FireTrace(playerOwnership, rifle, opaque, null);
            Require(opaque.Termination == PhysicalShotTermination.Impact && opaque.HitCollider != null &&
                    !IsTarget(opaque.HitCollider, target) && opaqueShot.Code == WeaponCombatCode.Miss && medical.WoundCount == 0,
                "N. Ordinary near cover stopped being opaque world geometry.");
            UnityEngine.Object.DestroyImmediate(opaque.HitCollider.gameObject); Physics.SyncTransforms();
            Require(Quantity(playerOwnership, AmmoItemId) == looseAmmo, "O. Armor/fire resolution mutated loose inventory ammo.");
            input.DiagnosticStartCycle(10f); Require(!input.IsAttackReady, "V/W. Bolt cycle gate did not close.");
            input.DiagnosticStartCycle(0f); Require(input.IsAttackReady && input.PreservesMovementInput &&
                    UnityEngine.Object.FindAnyObjectByType<InventoryUISessionController>() != null && UnityEngine.Object.FindAnyObjectByType<ActorHealthDebugWindow>() != null &&
                    UnityEngine.Object.FindAnyObjectByType<CameraRigController>() != null,
                "V/W. Bolt cycle, WASD or H/I/RMB input foundations regressed.");
            Reset(target);
            EquipArmor(targetOwnership, armor);
            ActorRuntimeIdentity deathTarget = Spawn(source.ActorProfileId, player.transform.position + player.transform.forward * 1.1f);
            ActorItemOwnershipComponent deathOwnership = deathTarget.GetComponent<ActorItemOwnershipComponent>();
            ItemInstance corpseArmor = deathOwnership.PersonalInventory.AddItemByDefinitionId(ArmorItemId, 1);
            EquipArmor(deathOwnership, corpseArmor);
            ActorHealthComponent deathHealth = deathTarget.GetComponent<ActorHealthComponent>();
            ActorMedicalStateComponent deathMedical = deathTarget.GetComponent<ActorMedicalStateComponent>();
            Collider deathCollider = deathTarget.GetComponent<Collider>();
            Vector3 deathTorso = Point(deathCollider.bounds, 0f, .65f);
            WeaponCombatResult lethal = FireDirect(playerOwnership, rifle, deathCollider, deathTorso, 1f);
            string clockFailure = null;
            ActorConditionComponent deathCondition = deathTarget.GetComponent<ActorConditionComponent>();
            double lethalBleedingHours =
                (deathCondition.BloodFraction - deathCondition.FatalBloodFraction + 0.01f) /
                Math.Max(0.001f, deathMedical.EffectiveBleedingRatePerGameHour);
            Require(lethal.Combat.Armor.Outcome == ArmorResolutionOutcome.Penetrated && deathMedical.WoundCount == 1 &&
                    WorldClock.Current.TryAdvanceGameTime(WorldClock.SecondsPerHour * lethalBleedingHours, out clockFailure),
                "P. Lethal penetration setup/clock failed: " + clockFailure);
            Require(deathHealth.IsDead && deathTarget.LifecycleState == ActorLifecycleState.Dead &&
                    deathTarget.GetComponent<WorldObjectTags>()?.HasTag(ActorHealthComponent.LootableActorTag) == true &&
                    deathOwnership.Equipment.IsEquipped(corpseArmor.InstanceId),
                "P/Q. M39/M38 death or corpse equipment continuity failed.");
            EquipWeapon(playerEquipment, playerInventory, rifle.InstanceId);
            Require(rifle.TrySetFirearmState(AmmoProfileId, 5, out string firearmFailure), "R. Save firearm fixture failed: " + firearmFailure);
            CurrentSliceSaveData targetSave = Capture("M40.1 target");
            JObject payload = (JObject)CurrentSliceSnapshotService.ToPayload(targetSave);
            Require(targetSave.items.Any(item => item.instanceId == armor.InstanceId && item.condition == armor.Condition) &&
                    targetSave.equipment.Any(state => state.ownerPersistentId == target.ActorInstanceId &&
                        state.items.Any(entry => entry.instanceId == armor.InstanceId)) &&
                    !payload.Descendants().OfType<JProperty>().Any(property =>
                        property.Name.IndexOf("armorState", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        property.Name.IndexOf("penetrationState", StringComparison.OrdinalIgnoreCase) >= 0),
                "R/T. Snapshot omitted equipped armor or introduced forbidden durable armor/penetration state.");
            SessionState.SetString(TargetActorKey, target.ActorInstanceId);
            SessionState.SetString(ArmorKey, armor.InstanceId);
            SessionState.SetString(RifleKey, rifle.InstanceId);
            SessionState.SetInt(ArmorConditionKey, armor.Condition);
            Write(TargetSlot, targetSave);
        }
        private static void RunSessionB()
        {
            CurrentSliceSaveData expected = Read(TargetSlot);
            CurrentSliceLoadResult loaded = CurrentSliceLoadService.Load(TargetSlot, Store());
            Require(loaded.Success, "R. Fresh-session M40.1 load failed: " + loaded.Failure);
            AssertEquivalent(expected, Capture("M40.1 fresh load"), "R. exact armor/equipment round-trip");
            string actorId = SessionState.GetString(TargetActorKey, string.Empty);
            string armorId = SessionState.GetString(ArmorKey, string.Empty);
            string rifleId = SessionState.GetString(RifleKey, string.Empty);
            Require(ActorRuntimeRegistry.TryGet(actorId, out ActorRuntimeIdentity target), "R. Restored armor target was not found.");
            ActorItemOwnershipComponent targetOwnership = target.GetComponent<ActorItemOwnershipComponent>();
            Require(targetOwnership.Equipment.TryGetEntryByInstanceId(armorId, out ItemStorageEntry armorEntry) &&
                    armorEntry.Item.Condition == SessionState.GetInt(ArmorConditionKey, -1),
                "R. Same armor ItemInstance/Condition was not rehydrated equipped.");
            ActorInteractionContext player = Player();
            ActorItemOwnershipComponent playerOwnership = player.GetComponent<ActorItemOwnershipComponent>();
            ItemInstance rifle = playerOwnership.GetAllOwnedEntries().Single(entry => entry.Item.InstanceId == rifleId).Item;
            Collider collider = target.GetComponent<Collider>();
            Reset(target);
            WeaponCombatResult protectedAgain = FireDirect(playerOwnership, rifle, collider, Point(collider.bounds, 0f, .65f), null);
            Require(protectedAgain.Combat.Armor.Outcome == ArmorResolutionOutcome.Penetrated &&
                    protectedAgain.Combat.FinalWoundType == WoundType.Puncture && Near(protectedAgain.Combat.FinalSeverity, .325f),
                "S. Post-load equipped armor did not reproduce its residual Penetrated protection.");
            CurrentSliceSaveData beforeInvalid = Capture("M40.1 invalid-data baseline");
            AssertInvalidDataRejected();
            AssertEquivalent(beforeInvalid, Capture("M40.1 invalid-data aftermath"), "U. invalid data no mutation");
            Require(CurrentSliceSaveData.CurrentSchemaVersion == 1 && PersistenceSerializer.CurrentFormatVersion == 1,
                "T. M40.1 changed Current Slice schema/envelope versions.");
            CurrentSliceSaveData initial = Read(InitialSlot);
            Require(initial.items.All(item => item.definitionId != ArmorItemId), "T. Pre-M40.1 baseline unexpectedly contains armor.");
            CurrentSliceLoadResult cleanup = CurrentSliceLoadService.Load(InitialSlot, Store());
            Require(cleanup.Success, "T. Legacy/no-armor save failed to load: " + cleanup.Failure);
            AssertEquivalent(initial, Capture("M40.1 cleanup"), "T. legacy/no-armor exact load");
            Require(Capture("M40.1 no invention").items.All(item => item.definitionId != ArmorItemId),
                "T. Legacy load invented a new armor item.");
        }
        private static void AssertInvalidDataRejected()
        {
            var report = new DataLoadReport();
            var database = new GameDatabase(report);
            database.RegisterPenetrationProfile(new PenetrationProfileDefinition
                { type = "penetration_profile", id = "core:invalid_penetration", display_name = "Bad", resistance = float.NaN }, report);
            database.RegisterAmmoProfile(new AmmoProfileDefinition
            {
                type = "ammo_profile", id = "core:invalid_zero_penetration_projectile", display_name = "Bad projectile",
                caliber_tag = "ammo", wound_type = "Puncture", wound_severity = .5f,
                bleeding_rate_per_game_hour = .1f, pain_contribution = .1f, penetration_power = 0f,
                tags = new[] { "ammo" }
            }, report);
            database.RegisterArmorProfile(new ArmorProfileDefinition { id = "Bad Armor" }, report);
            database.RegisterArmorProfile(new ArmorProfileDefinition
            {
                type = "armor_profile", id = "core:invalid_armor_profile", display_name = "Bad", covered_regions = new[] { "0" },
                penetration_profile_id = "core:missing_penetration", impact_resistance = -1f,
                stopped_blunt_transfer = 1.1f, blunt_wound_threshold = float.NaN, layer_priority = -1
            }, report);
            database.RegisterItem(new ItemDefinition
                { type = "item", id = "core:invalid_armor_item", max_stack = 1, armor_profile_id = "core:missing_armor",
                    inventory = new ItemInventoryMetadata { footprint = new ItemFootprintDefinition { width = 1, height = 1 } } }, report);
            new DataValidator(database, new TagRegistry(), report).Validate();
            Require(report.ErrorCount >= 16 && report.Errors.Any(error => error.Contains("not canonical")) &&
                    report.Errors.Any(error => error.Contains("resistance")) && report.Errors.Any(error => error.Contains("penetration_power")) &&
                    report.Errors.Any(error => error.Contains("covered_regions")) &&
                    report.Errors.Any(error => error.Contains("penetration_profile_id")) &&
                    report.Errors.Any(error => error.Contains("armor_profile_id")) &&
                    report.Errors.Any(error => error.Contains("impact_resistance")) && report.Errors.Any(error => error.Contains("blunt_wound_threshold")) &&
                    report.Errors.Any(error => error.Contains("stopped_blunt_transfer")) &&
                    report.Errors.Any(error => error.Contains("layer_priority")),
                "U. Invalid armor/penetration data was not rejected comprehensively.");
        }
        private static void AssertUncovered(ActorItemOwnershipComponent shooter, ItemInstance rifle, ActorRuntimeIdentity target,
            ItemInstance armor, BodyRegion region, Vector3 point)
        {
            Reset(target);
            WeaponCombatResult result = FireDirect(shooter, rifle, target.GetComponent<Collider>(), point, null);
            AssertSingleWound(result, target.GetComponent<ActorMedicalStateComponent>(), region, WoundType.Puncture, .65f,
                ArmorResolutionOutcome.Unarmored, ArmorCoverageStatus.RegionNotCovered, "C/K " + region);
            Require(result.Combat.Armor.ArmorItemInstanceId == null && target.GetComponent<ActorEquipmentComponent>().IsEquipped(armor.InstanceId),
                "C/K. Uncovered region accidentally applied equipped torso armor.");
        }
        private static WeaponCombatResult FireDirect(ActorItemOwnershipComponent ownership, ItemInstance rifle,
            Collider collider, Vector3 point, float? power)
        {
            Load(rifle);
            int before = rifle.LoadedRounds;
            WeaponCombatResult result = power.HasValue
                ? WeaponCombatService.DiagnosticFireEquipped(ownership, rifle.InstanceId, collider, point, power.Value)
                : WeaponCombatService.FireEquipped(ownership, rifle.InstanceId, collider, point);
            Require(result.Quantity == 1 && rifle.LoadedRounds == before - 1,
                "O. Fire did not consume exactly one loaded round: " + result.Message);
            return result;
        }
        private static WeaponCombatResult FireTrace(ActorItemOwnershipComponent ownership, ItemInstance rifle,
            PhysicalShotResolution trace, float? power)
        {
            Load(rifle);
            int before = rifle.LoadedRounds;
            WeaponCombatResult result = power.HasValue
                ? WeaponCombatService.DiagnosticFireEquipped(ownership, rifle.InstanceId, power.Value, _ => trace)
                : WeaponCombatService.FireEquipped(ownership, rifle.InstanceId, _ => trace);
            Require(result.Quantity == 1 && rifle.LoadedRounds == before - 1,
                "O. Physical shot did not consume exactly one loaded round: " + result.Message);
            return result;
        }
        private static PhysicalShotResolution Trace(FirearmDebugController input, Vector3 target, float power, out Vector3 origin)
        {
            Require(input.DiagnosticResolvePenetratingShot(target, 40f, power, out PhysicalShotResolution result, out origin),
                "Physical penetration trace could not be resolved.");
            return result;
        }
        private static PhysicalShotResolution TraceThrough(FirearmDebugController input, Vector3 target, float power,
            IReadOnlyList<string> profiles, out Vector3 origin)
        {
            PhysicalShotResolution clear = Trace(input, target, power, out origin);
            Require(clear.Termination == PhysicalShotTermination.Impact, "World fixture requires a clear target ray before cover placement.");
            Vector3 direction = (target - origin).normalized;
            var covers = new List<GameObject>();
            try
            {
                for (int index = 0; index < profiles.Count; index++)
                    covers.Add(CreateCover(origin, direction, .6f + index * .3f, profiles[index]));
                Physics.SyncTransforms();
                return Trace(input, target, power, out _);
            }
            finally
            {
                for (int index = 0; index < covers.Count; index++)
                    if (profiles[index] != null) UnityEngine.Object.DestroyImmediate(covers[index]);
                Physics.SyncTransforms();
            }
        }
        private static GameObject CreateCover(Vector3 origin, Vector3 direction, float distance, string profileId)
        {
            GameObject cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cover.name = profileId ?? "M40.1 Opaque Near Cover";
            cover.transform.SetPositionAndRotation(origin + direction * distance, Quaternion.LookRotation(direction, Vector3.up));
            cover.transform.localScale = new Vector3(1.5f, 2f, .05f);
            if (!string.IsNullOrWhiteSpace(profileId)) cover.AddComponent<WorldObjectProfileComponent>().DiagnosticConfigure(profileId);
            return cover;
        }
        private static void AssertSingleWound(WeaponCombatResult result, ActorMedicalStateComponent medical, BodyRegion region,
            WoundType type, float? severity, ArmorResolutionOutcome outcome, ArmorCoverageStatus coverage, string label)
        {
            ActorMedicalWoundState[] wounds = medical.CaptureState().wounds;
            Require(result.Success && result.Combat.WoundApplied && wounds.Length == 1 &&
                    result.Combat.Region == region && result.Combat.FinalWoundType == type &&
                    wounds[0].region == region.ToString() && wounds[0].woundType == type.ToString() &&
                    result.Combat.Armor.Outcome == outcome && result.Combat.Armor.Coverage == coverage &&
                    (!severity.HasValue || Near(wounds[0].severity, severity.Value)),
                label + " failed: " + result.Message);
        }
        private static void Reset(ActorRuntimeIdentity actor)
        {
            ActorHealthComponent health = actor.GetComponent<ActorHealthComponent>();
            ActorMedicalStateComponent medical = actor.GetComponent<ActorMedicalStateComponent>();
            health.ApplyInitialHealth(health.MaxHealth, health.MaxHealth);
            Require(medical.TryApplyPersistenceState(ActorMedicalStateComponent.HealthyBaseline(), out string failure),
                "Could not reset diagnostic medical state: " + failure);
        }
        private static ActorRuntimeIdentity Spawn(string profileId, Vector3 position)
        {
            Require(ActorSpawnService.TrySpawn(profileId, position, Quaternion.identity, out ActorRuntimeIdentity actor, out string failure),
                "Runtime actor spawn failed: " + failure);
            return actor;
        }
        private static void EquipArmor(ActorItemOwnershipComponent ownership, ItemInstance armor, string slotId = TorsoOuterSlotId)
        {
            if (ownership.Equipment.IsEquipped(armor.InstanceId)) return;
            EquipmentPreview preview = ownership.Equipment.PreviewEquip(ownership.PersonalInventory, armor.InstanceId,
                new[] { slotId });
            Require(preview.Success && ownership.Equipment.Equip(ownership.PersonalInventory, preview).Success &&
                    ownership.Equipment.IsEquipped(armor.InstanceId), "Armor fixture could not be equipped.");
        }
        private static void UnequipArmor(ActorItemOwnershipComponent ownership, ItemInstance armor)
        {
            if (!ownership.Equipment.IsEquipped(armor.InstanceId)) return;
            EquipmentPreview preview = ownership.Equipment.PreviewUnequip(armor.InstanceId);
            Require(preview.Success && ownership.Equipment.Unequip(preview).Success &&
                    !ownership.Equipment.IsEquipped(armor.InstanceId), "Armor fixture could not be unequipped.");
        }
        private static void EquipWeapon(ActorEquipmentComponent equipment, InventoryComponent inventory, string instanceId)
        {
            if (equipment.IsEquipped(instanceId)) return;
            IReadOnlyList<EquipmentSlotSet> compatible = equipment.GetCompatibleSlotSets(inventory, instanceId);
            Require(compatible.Count > 0, "Weapon has no data-declared compatible slot set.");
            string[] slots = compatible[0].SlotIds;
            EquipmentMutationResult result;
            if (slots.Any(slot => equipment.GetEquippedInstance(slot) != null))
            {
                EquipmentReplacementPlan plan = equipment.PreviewEquipReplacing(inventory, instanceId, slots);
                Require(plan.Success, "Weapon replacement preview failed: " + plan.Message);
                result = equipment.EquipReplacing(inventory, plan);
            }
            else
            {
                EquipmentPreview plan = equipment.PreviewEquip(inventory, instanceId, slots);
                Require(plan.Success, "Weapon equip preview failed: " + plan.Message);
                result = equipment.Equip(inventory, plan);
            }
            Require(result.Success && equipment.IsEquipped(instanceId), "Weapon equip failed: " + result.Message);
        }
        private static void Load(ItemInstance rifle) =>
            Require(rifle.TrySetFirearmState(AmmoProfileId, 10, out string failure), "Could not load firearm fixture: " + failure);
        private static bool Near(float actual, float expected) => Mathf.Abs(actual - expected) <= Epsilon;
        private static Vector3 Point(Bounds bounds, float x, float y) =>
            new Vector3(bounds.center.x + bounds.extents.x * x, bounds.min.y + bounds.size.y * y, bounds.center.z);
        private static bool IsTarget(Collider collider, ActorRuntimeIdentity target) => collider != null &&
            (collider.transform == target.transform || collider.transform.IsChildOf(target.transform));
        private static int Quantity(ActorItemOwnershipComponent ownership, string definitionId) =>
            ownership.GetAllOwnedEntries().Where(entry => entry.DefinitionId == definitionId).Sum(entry => entry.Quantity);
        private static ActorInteractionContext Player()
        {
            ActorInteractionContext[] actors = UnityEngine.Object.FindObjectsByType<ActorInteractionContext>(FindObjectsInactive.Exclude);
            ActorInteractionContext player = actors.SingleOrDefault(candidate => candidate.ActorTags.Contains("player"));
            Require(player != null, $"Expected exactly one player; found {actors.Length} actors.");
            return player;
        }
        private static PersistenceFileStore Store() => new PersistenceFileStore(SessionState.GetString(RootKey, string.Empty));
        private static CurrentSliceSaveData Capture(string label)
        {
            CurrentSliceResult result = CurrentSliceSnapshotService.Capture();
            Require(result.Success, label + " capture failed: " + result.Failure);
            return result.Snapshot;
        }
        private static void Write(string slot, CurrentSliceSaveData data)
        {
            PersistenceWriteResult result = Store().Write(slot, CurrentSliceSnapshotService.ToPayload(data));
            Require(result.Success, $"Slot '{slot}' write failed: {result.Failure}");
        }
        private static CurrentSliceSaveData Read(string slot)
        {
            CurrentSliceResult result = CurrentSliceSnapshotService.Read(slot, Store());
            Require(result.Success, $"Slot '{slot}' read failed: {result.Failure}");
            return result.Snapshot;
        }
        private static void AssertEquivalent(CurrentSliceSaveData expected, CurrentSliceSaveData actual, string label)
        {
            CurrentSliceComparisonResult result = CurrentSliceSnapshotService.Compare(expected, actual);
            Require(result.Equivalent, label + " differs: " + result.Difference);
        }
        private static void Require(bool condition, string failure) { if (!condition) throw new InvalidOperationException(failure); }
        private static void FinalizeRun()
        {
            string failure = SessionState.GetString(ErrorKey, string.Empty);
            string root = SessionState.GetString(RootKey, string.Empty);
            if (EditorSceneManager.GetActiveScene().isDirty) failure = Append(failure, "Diagnostics left SampleScene dirty.");
            try { if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root)) Directory.Delete(root, true); }
            catch (Exception exception) { failure = Append(failure, "Temporary cleanup failed: " + exception.Message); }
            bool success = string.IsNullOrWhiteSpace(failure);
            ClearSession();
            if (success) Debug.Log("M40.1 Armor & Penetration Diagnostics: PASS");
            else Debug.LogError("M40.1 Armor & Penetration Diagnostics: FAIL\n- " + failure);
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
        }
        private static string Append(string current, string value) => string.IsNullOrWhiteSpace(current) ? value : current + "\n- " + value;
        private static void ClearSession()
        {
            SessionState.EraseString(PhaseKey); SessionState.EraseString(RootKey); SessionState.EraseString(ErrorKey);
            SessionState.EraseString(TargetActorKey); SessionState.EraseString(ArmorKey); SessionState.EraseString(RifleKey);
            SessionState.EraseInt(ArmorConditionKey);
        }
    }
}
