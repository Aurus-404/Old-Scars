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
        public WeaponCombatResult(WeaponCombatCode code, string message, int quantity = 0, CombatResolutionResult combat = default)
        {
            Code = code;
            Message = message;
            Quantity = quantity;
            Combat = combat;
        }

        public WeaponCombatCode Code { get; }
        public string Message { get; }
        public int Quantity { get; }
        public CombatResolutionResult Combat { get; }
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
            Vector3 hitPoint)
        {
            if (!TryGetEquippedWeapon(ownership, out ItemInstance firearmItem, out _, out FirearmProfileDefinition profile, out _) ||
                firearmItem.InstanceId != expectedFirearmInstanceId || profile == null)
                return Fail(WeaponCombatCode.NotEquipped, "The same firearm is not equipped.");
            if (firearmItem.LoadedRounds <= 0 || string.IsNullOrWhiteSpace(firearmItem.LoadedAmmoProfileId))
                return Fail(WeaponCombatCode.Unloaded, "Firearm is unloaded. Press R to reload.");

            AmmoProfileDefinition ammo = Database()?.GetAmmoProfile(firearmItem.LoadedAmmoProfileId);
            if (ammo == null || !Contains(profile.accepted_ammo_profile_ids, ammo.id))
                return Fail(WeaponCombatCode.InvalidWeapon, "Loaded ammo state is not compatible with the firearm profile.");
            if (!firearmItem.TryConsumeLoadedRound(out string consumeFailure))
                return Fail(WeaponCombatCode.Unloaded, consumeFailure);
            if (hitCollider == null)
                return new WeaponCombatResult(WeaponCombatCode.Miss, "Shot missed; one loaded round was consumed.", 1);
            if (!TryWoundType(ammo.wound_type, out WoundType woundType))
                return Fail(WeaponCombatCode.InvalidWeapon, "Ammo wound type is invalid after data validation.");

            CombatResolutionResult combat = CombatResolutionService.ResolveImpact(
                hitCollider,
                hitPoint,
                new CombatImpact(ownership.gameObject, firearmItem, CombatAttackKind.Firearm, woundType,
                    ammo.wound_severity, ammo.bleeding_rate_per_game_hour, ammo.pain_contribution));
            WeaponCombatCode code = combat.Code == CombatResolutionCode.Miss || combat.Code == CombatResolutionCode.InvalidTarget
                ? WeaponCombatCode.Miss
                : combat.WoundApplied ? WeaponCombatCode.Success : WeaponCombatCode.ResolutionRejected;
            return new WeaponCombatResult(code, combat.Message, 1, combat);
        }

        public static WeaponCombatResult StrikeEquipped(
            ActorItemOwnershipComponent ownership,
            string expectedWeaponInstanceId,
            Collider hitCollider,
            Vector3 hitPoint)
        {
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
                    profile.wound_severity, profile.bleeding_rate_per_game_hour, profile.pain_contribution));
            WeaponCombatCode code = combat.Code == CombatResolutionCode.Miss || combat.Code == CombatResolutionCode.InvalidTarget
                ? WeaponCombatCode.Miss
                : combat.WoundApplied ? WeaponCombatCode.Success : WeaponCombatCode.ResolutionRejected;
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
        private static GameDatabase Database() => GameDataManager.Instance != null && GameDataManager.Instance.IsReady
            ? GameDataManager.Instance.Database : null;
        private static bool Contains(string[] values, string expected) => values != null && values.Contains(expected, StringComparer.Ordinal);
        private static bool TryWoundType(string value, out WoundType result) =>
            Enum.TryParse(value, false, out result) && Enum.IsDefined(typeof(WoundType), result) && result.ToString() == value;
        private static WeaponCombatResult Fail(WeaponCombatCode code, string message) => new WeaponCombatResult(code, message);
    }
}
