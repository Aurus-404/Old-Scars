using System;
using System.Collections.Generic;
using System.Linq;
using OldScars.Core.Actors;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Items;
using UnityEngine;

namespace OldScars.Core.Combat
{
    public enum WeaponCombatCode
    {
        Success,
        Miss,
        InvalidActor,
        Incapacitated,
        NotEquipped,
        InvalidWeapon,
        Unloaded,
        Cycling,
        NoCompatibleAmmo,
        Full,
        OutOfRange,
        StorageChanged,
        ResolutionRejected
    }

    public readonly struct WeaponCombatResult
    {
        public WeaponCombatResult(
            WeaponCombatCode code,
            string message,
            int quantity = 0,
            CombatResolutionResult combat = default,
            PhysicalShotResolution physicalShot = default)
        {
            Code = code;
            Message = message;
            Quantity = quantity;
            Combat = combat;
            PhysicalShot = physicalShot;
        }

        public WeaponCombatCode Code { get; }
        public string Message { get; }
        public int Quantity { get; }
        public CombatResolutionResult Combat { get; }
        public PhysicalShotResolution PhysicalShot { get; }
        public bool Success => Code == WeaponCombatCode.Success || Code == WeaponCombatCode.Miss;
    }

    public static class WeaponCombatService
    {
        public static bool TryGetEquippedWeapon(
            ActorItemOwnershipComponent ownership,
            out ItemInstance item,
            out ItemDefinition definition,
            out FirearmProfileDefinition firearm,
            out WeaponProfileDefinition melee)
        {
            item = null;
            definition = null;
            firearm = null;
            melee = null;
            ActorEquipmentComponent equipment = ownership != null ? ownership.Equipment : null;
            if (equipment == null)
                return false;

            item = equipment.GetEquippedInstance(ActorEquipmentComponent.HandRightSlotId) ??
                   equipment.GetEquippedInstance(ActorEquipmentComponent.HandLeftSlotId);
            definition = Definition(item);
            if (item == null || definition == null)
                return false;

            GameDatabase database = Database();
            firearm = !string.IsNullOrWhiteSpace(definition.firearm_profile_id)
                ? database?.GetFirearmProfile(definition.firearm_profile_id)
                : null;
            melee = definition.combat != null && !string.IsNullOrWhiteSpace(definition.combat.weapon_profile)
                ? database?.GetWeaponProfile(definition.combat.weapon_profile)
                : null;
            return firearm != null || melee != null;
        }

        public static int GetCompatibleAmmoQuantity(ActorItemOwnershipComponent ownership, ItemInstance firearmItem)
        {
            if (!TryGetFirearmProfile(firearmItem, out FirearmProfileDefinition profile, out _))
                return 0;
            int total = 0;
            foreach (ItemStorageEntry entry in ownership?.GetAllOwnedEntries() ?? Array.Empty<ItemStorageEntry>())
            {
                if (TryResolveOwnedAmmo(ownership, entry, profile, firearmItem.LoadedAmmoProfileId,
                        out _, out _, out _, out _))
                    total += entry.Quantity;
            }
            return total;
        }

        public static WeaponCombatResult ReloadEquipped(ActorItemOwnershipComponent ownership, string expectedFirearmInstanceId)
        {
            if (!CanPerformActiveActions(ownership))
                return Fail(WeaponCombatCode.Incapacitated, "Functionally incapacitated actors cannot reload.");
            if (!TryGetEquippedWeapon(ownership, out ItemInstance firearmItem, out _, out FirearmProfileDefinition profile, out _) ||
                firearmItem.InstanceId != expectedFirearmInstanceId || profile == null)
            {
                return Fail(WeaponCombatCode.NotEquipped, "The same firearm is no longer equipped.");
            }
            if (firearmItem.LoadedRounds >= profile.magazine_capacity)
                return Fail(WeaponCombatCode.Full, "Firearm is already full.");

            IReadOnlyList<ItemStorageEntry> entries = ownership.GetAllOwnedEntries();
            for (int index = 0; index < entries.Count; index++)
            {
                ItemStorageEntry entry = entries[index];
                if (!TryResolveOwnedAmmo(ownership, entry, profile, firearmItem.LoadedAmmoProfileId,
                        out AmmoProfileDefinition ammo, out IGridStorageOwner owner,
                        out IGridStorageTransferEndpoint endpoint, out ItemStorageEntry liveEntry))
                    continue;

                int amount = Math.Min(profile.magazine_capacity - firearmItem.LoadedRounds, liveEntry.Quantity);
                if (amount <= 0)
                    continue;
                string previousAmmo = firearmItem.LoadedAmmoProfileId;
                int previousRounds = firearmItem.LoadedRounds;
                if (!firearmItem.TrySetFirearmState(ammo.id, previousRounds + amount, out string stateFailure))
                    return Fail(WeaponCombatCode.InvalidWeapon, stateFailure);

                InventoryMutationResult removal = endpoint.TransferBackend.Remove(liveEntry.Item.InstanceId, amount);
                if (!removal.Success)
                {
                    firearmItem.TrySetFirearmState(previousAmmo, previousRounds, out _);
                    return Fail(WeaponCombatCode.StorageChanged, "Compatible ammo changed before reload commit: " + removal.Message);
                }
                if (!owner.TryGetEntryByInstanceId(liveEntry.Item.InstanceId, out _, out _))
                    ItemOwnedStorageRegistry.Instance.UnbindItem(liveEntry.Item.InstanceId);
                endpoint.OnTransferCommittedOut(new GridStorageTransferReceipt(liveEntry.DefinitionId, removal), default);
                return new WeaponCombatResult(WeaponCombatCode.Success, $"Reloaded {amount} round(s).", amount);
            }

            return Fail(WeaponCombatCode.NoCompatibleAmmo, "No compatible owned ammo is available.");
        }

        public static WeaponCombatResult FireEquipped(
            ActorItemOwnershipComponent ownership,
            string expectedFirearmInstanceId,
            Collider hitCollider,
            Vector3 hitPoint) =>
            FireEquippedCore(ownership, expectedFirearmInstanceId, hitCollider, hitPoint, null, null);

        public static WeaponCombatResult FireEquipped(
            ActorItemOwnershipComponent ownership,
            string expectedFirearmInstanceId,
            PhysicalShotResolver physicalResolver) =>
            FireEquippedCore(ownership, expectedFirearmInstanceId, null, default, null, physicalResolver);

#if UNITY_EDITOR
        public static WeaponCombatResult DiagnosticFireEquipped(
            ActorItemOwnershipComponent ownership,
            string expectedFirearmInstanceId,
            Collider hitCollider,
            Vector3 hitPoint,
            float penetrationPower)
        {
            if (float.IsNaN(penetrationPower) || float.IsInfinity(penetrationPower) || penetrationPower < 0f)
                return Fail(WeaponCombatCode.InvalidWeapon, "Diagnostic penetration power must be finite and >= 0.");
            return FireEquippedCore(ownership, expectedFirearmInstanceId, hitCollider, hitPoint, penetrationPower, null);
        }

        public static WeaponCombatResult DiagnosticFireEquipped(
            ActorItemOwnershipComponent ownership,
            string expectedFirearmInstanceId,
            float penetrationPower,
            PhysicalShotResolver physicalResolver)
        {
            if (float.IsNaN(penetrationPower) || float.IsInfinity(penetrationPower) || penetrationPower < 0f)
                return Fail(WeaponCombatCode.InvalidWeapon, "Diagnostic penetration power must be finite and >= 0.");
            return FireEquippedCore(ownership, expectedFirearmInstanceId, null, default, penetrationPower, physicalResolver);
        }
#endif

        private static WeaponCombatResult FireEquippedCore(
            ActorItemOwnershipComponent ownership,
            string expectedFirearmInstanceId,
            Collider hitCollider,
            Vector3 hitPoint,
            float? diagnosticPenetrationPower,
            PhysicalShotResolver physicalResolver)
        {
            if (!CanPerformActiveActions(ownership))
                return Fail(WeaponCombatCode.Incapacitated, "Functionally incapacitated actors cannot fire.");
            if (!TryGetEquippedWeapon(ownership, out ItemInstance firearmItem, out _, out FirearmProfileDefinition profile, out _) ||
                firearmItem.InstanceId != expectedFirearmInstanceId || profile == null)
                return Fail(WeaponCombatCode.NotEquipped, "The same firearm is not equipped.");
            if (physicalResolver == null && hitCollider != null &&
                Vector3.Distance(ownership.transform.position, hitPoint) > profile.range + 0.05f)
            {
                return Fail(WeaponCombatCode.OutOfRange, "Firearm target is out of range.");
            }
            if (firearmItem.LoadedRounds <= 0 || string.IsNullOrWhiteSpace(firearmItem.LoadedAmmoProfileId))
                return Fail(WeaponCombatCode.Unloaded, "Firearm is unloaded. Press R to reload.");

            AmmoProfileDefinition ammo = Database()?.GetAmmoProfile(firearmItem.LoadedAmmoProfileId);
            if (ammo == null || !Contains(profile.accepted_ammo_profile_ids, ammo.id))
                return Fail(WeaponCombatCode.InvalidWeapon, "Loaded ammo state is not compatible with the firearm profile.");
            if (!TryWoundType(ammo.wound_type, out WoundType woundType))
                return Fail(WeaponCombatCode.InvalidWeapon, "Ammo wound type is invalid after data validation.");

            float originalPower = diagnosticPenetrationPower ?? ammo.penetration_power;
            float remainingPower = originalPower;
            PhysicalShotResolution physicalShot = default;
            if (physicalResolver != null)
            {
                try
                {
                    physicalShot = physicalResolver(originalPower);
                }
                catch (Exception exception)
                {
                    return Fail(WeaponCombatCode.ResolutionRejected,
                        $"Physical shot resolution threw {exception.GetType().Name}: {exception.Message}");
                }

                if (!ValidPhysicalShot(physicalShot, originalPower, out string physicalFailure))
                    return Fail(WeaponCombatCode.ResolutionRejected, physicalFailure);
                hitCollider = physicalShot.Termination == PhysicalShotTermination.Impact
                    ? physicalShot.HitCollider
                    : null;
                hitPoint = physicalShot.EndPoint;
                remainingPower = physicalShot.RemainingPower;
            }

            if (!firearmItem.TryConsumeLoadedRound(out string consumeFailure))
                return Fail(WeaponCombatCode.Unloaded, consumeFailure);

            if (physicalResolver != null)
            {
                if (physicalShot.Termination == PhysicalShotTermination.Miss)
                {
                    return new WeaponCombatResult(
                        WeaponCombatCode.Miss,
                        "Shot missed; one loaded round was consumed.",
                        1,
                        physicalShot: physicalShot);
                }
                if (physicalShot.Termination == PhysicalShotTermination.SurfaceStopped)
                {
                    PenetrationResolution surface = physicalShot.LastSurfaceResolution;
                    return new WeaponCombatResult(
                        WeaponCombatCode.Success,
                        $"Projectile stopped by penetrable surface '{physicalShot.TerminalSurfaceProfileId}' " +
                        $"(power {surface.IncomingPower:0.###} <= resistance {surface.AppliedResistance:0.###}).",
                        1,
                        physicalShot: physicalShot);
                }
                if (physicalShot.Termination == PhysicalShotTermination.SurfaceLimitStopped)
                {
                    return new WeaponCombatResult(
                        WeaponCombatCode.Success,
                        $"Projectile stopped at the bounded penetration surface limit " +
                        $"after {physicalShot.PenetratedSurfaceCount} surface(s).",
                        1,
                        physicalShot: physicalShot);
                }
            }

            if (hitCollider == null)
                return new WeaponCombatResult(WeaponCombatCode.Miss, "Shot missed; one loaded round was consumed.", 1,
                    physicalShot: physicalShot);

            CombatResolutionResult combat = CombatResolutionService.ResolveImpact(
                hitCollider,
                hitPoint,
                new CombatImpact(ownership.gameObject, firearmItem, CombatAttackKind.Firearm, woundType,
                    ammo.wound_severity, ammo.bleeding_rate_per_game_hour, ammo.pain_contribution,
                    originalPower,
                    remainingPower));
            WeaponCombatCode code = combat.Code == CombatResolutionCode.Miss || combat.Code == CombatResolutionCode.InvalidTarget
                ? WeaponCombatCode.Miss
                : combat.Resolved ? WeaponCombatCode.Success : WeaponCombatCode.ResolutionRejected;
            return new WeaponCombatResult(code, combat.Message, 1, combat, physicalShot);
        }

        public static WeaponCombatResult StrikeEquipped(
            ActorItemOwnershipComponent ownership,
            string expectedWeaponInstanceId,
            Collider hitCollider,
            Vector3 hitPoint)
        {
            if (!CanPerformActiveActions(ownership))
                return Fail(WeaponCombatCode.Incapacitated, "Functionally incapacitated actors cannot strike.");
            if (!TryGetEquippedWeapon(ownership, out ItemInstance weapon, out _, out _, out WeaponProfileDefinition profile) ||
                weapon.InstanceId != expectedWeaponInstanceId || profile == null)
                return Fail(WeaponCombatCode.NotEquipped, "The same melee weapon is not equipped.");
            if (hitCollider == null)
                return new WeaponCombatResult(WeaponCombatCode.Miss, "Melee attack missed.");
            if (Vector3.Distance(ownership.transform.position, hitPoint) > profile.melee_range + 0.05f)
                return Fail(WeaponCombatCode.OutOfRange, "Melee target is out of range.");
            if (!TryWoundType(profile.wound_type, out WoundType woundType))
                return Fail(WeaponCombatCode.InvalidWeapon, "Weapon wound type is invalid after data validation.");

            CombatResolutionResult combat = CombatResolutionService.ResolveImpact(
                hitCollider,
                hitPoint,
                new CombatImpact(ownership.gameObject, weapon, CombatAttackKind.Melee, woundType,
                    profile.wound_severity, profile.bleeding_rate_per_game_hour, profile.pain_contribution,
                    profile.wound_severity,
                    profile.wound_severity));
            WeaponCombatCode code = combat.Code == CombatResolutionCode.Miss || combat.Code == CombatResolutionCode.InvalidTarget
                ? WeaponCombatCode.Miss
                : combat.Resolved ? WeaponCombatCode.Success : WeaponCombatCode.ResolutionRejected;
            return new WeaponCombatResult(code, combat.Message, 0, combat);
        }

        private static bool TryGetFirearmProfile(ItemInstance item, out FirearmProfileDefinition profile, out ItemDefinition definition)
        {
            definition = Definition(item);
            profile = definition != null && !string.IsNullOrWhiteSpace(definition.firearm_profile_id)
                ? Database()?.GetFirearmProfile(definition.firearm_profile_id)
                : null;
            return item != null && item.HasFirearmState && profile != null;
        }

        private static bool TryResolveOwnedAmmo(
            ActorItemOwnershipComponent ownership,
            ItemStorageEntry entry,
            FirearmProfileDefinition firearm,
            string requiredAmmoProfileId,
            out AmmoProfileDefinition ammo,
            out IGridStorageOwner owner,
            out IGridStorageTransferEndpoint endpoint,
            out ItemStorageEntry liveEntry)
        {
            ammo = null;
            owner = null;
            endpoint = null;
            liveEntry = null;
            ItemDefinition item = Definition(entry?.Item);
            if (ownership == null || entry?.Item == null || entry.Quantity < 1 || item == null ||
                string.IsNullOrWhiteSpace(item.ammo_profile_id) ||
                !Contains(firearm.accepted_ammo_profile_ids, item.ammo_profile_id) ||
                !string.IsNullOrWhiteSpace(requiredAmmoProfileId) && item.ammo_profile_id != requiredAmmoProfileId ||
                !ItemOwnedStorageRegistry.Instance.TryGetDirectOwner(entry.Item.InstanceId, out object directOwner) ||
                !(directOwner is IGridStorageOwner directStorage) || !(directOwner is IGridStorageTransferEndpoint directEndpoint) ||
                !directStorage.TryGetEntryByInstanceId(entry.Item.InstanceId, out _, out liveEntry) ||
                !ReferenceEquals(liveEntry, entry) ||
                !ItemOwnedStorageRegistry.Instance.ShareRootOwner(directStorage, ownership.PersonalInventory))
                return false;

            ammo = Database()?.GetAmmoProfile(item.ammo_profile_id);
            if (ammo == null)
                return false;
            owner = directStorage;
            endpoint = directEndpoint;
            return true;
        }

        private static ItemDefinition Definition(ItemInstance item) => item == null ? null : Database()?.GetItem(item.DefinitionId);
        private static bool CanPerformActiveActions(ActorItemOwnershipComponent ownership)
        {
            ActorConditionComponent condition = ownership != null
                ? ownership.GetComponent<ActorConditionComponent>()
                : null;
            return condition == null || condition.CanPerformActiveActions;
        }
        private static GameDatabase Database() => GameDataManager.Instance != null && GameDataManager.Instance.IsReady
            ? GameDataManager.Instance.Database : null;
        private static bool Contains(string[] values, string expected) => values != null && values.Contains(expected, StringComparer.Ordinal);
        private static bool ValidPhysicalShot(
            PhysicalShotResolution shot,
            float expectedOriginalPower,
            out string failure)
        {
            if (!shot.IsResolved || !Enum.IsDefined(typeof(PhysicalShotTermination), shot.Termination) ||
                !FiniteVector(shot.EndPoint) ||
                float.IsNaN(shot.OriginalPower) || float.IsInfinity(shot.OriginalPower) ||
                float.IsNaN(shot.RemainingPower) || float.IsInfinity(shot.RemainingPower) ||
                shot.OriginalPower < 0f || shot.RemainingPower < 0f ||
                shot.RemainingPower > shot.OriginalPower ||
                Mathf.Abs(shot.OriginalPower - expectedOriginalPower) > 0.0001f ||
                shot.PenetratedSurfaceCount < 0 ||
                shot.Termination == PhysicalShotTermination.Impact && shot.HitCollider == null ||
                shot.Termination != PhysicalShotTermination.Impact && shot.HitCollider != null)
            {
                failure = "Physical shot resolver returned an invalid terminal collider, endpoint or penetration budget.";
                return false;
            }

            failure = null;
            return true;
        }

        private static bool FiniteVector(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        private static bool TryWoundType(string value, out WoundType result) =>
            Enum.TryParse(value, false, out result) && Enum.IsDefined(typeof(WoundType), result) && result.ToString() == value;
        private static WeaponCombatResult Fail(WeaponCombatCode code, string message) => new WeaponCombatResult(code, message);
    }
}
