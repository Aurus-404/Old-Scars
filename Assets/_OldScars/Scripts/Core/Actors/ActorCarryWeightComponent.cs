using System;
using System.Collections.Generic;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Items;
using UnityEngine;

namespace OldScars.Core.Actors
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InventoryComponent))]
    public sealed class ActorCarryWeightComponent : MonoBehaviour
    {
        private const double LimitEpsilon = 0.000001d;

        [SerializeField] private InventoryComponent inventoryComponent;
        [SerializeField] private ActorItemOwnershipComponent ownershipComponent;
        [SerializeField] private float baseCarryCapacityKg = 30f;
        [SerializeField] private double hardLimitMultiplier = 1.30d;

        private readonly HashSet<string> loggedErrors = new HashSet<string>();

        public float BaseCarryCapacityKg => baseCarryCapacityKg;
        public double HardLimitMultiplier => hardLimitMultiplier;
        public double CurrentWeightKg => GetSnapshot().CurrentWeightKg;
        public double SoftCapacityKg => GetSnapshot().SoftCapacityKg;
        public double HardLimitKg => GetSnapshot().HardLimitKg;
        public double EncumbranceRatio => GetSnapshot().EncumbranceRatio;
        public CarryWeightState State => GetSnapshot().State;

        private void Awake()
        {
            ResolveInventoryComponent();
            ResolveOwnershipComponent();
        }

        private void OnValidate()
        {
            if (inventoryComponent == null)
                inventoryComponent = GetComponent<InventoryComponent>();
            if (ownershipComponent == null)
                ownershipComponent = GetComponent<ActorItemOwnershipComponent>();
        }

        public CarryWeightSnapshot GetSnapshot()
        {
            if (!TryValidateConfiguration(out double softCapacityKg, out double hardLimitKg, out string error))
                return InvalidSnapshot(error);

            if (!ResolveInventoryComponent())
                return InvalidSnapshot("ActorCarryWeightComponent requires an InventoryComponent on the same GameObject.");

            ResolveOwnershipComponent();
            if (ownershipComponent != null && !ownershipComponent.ValidateUniqueOwnership(out string ownershipError))
                return InvalidSnapshot(ownershipError);

            double currentWeightKg = 0d;
            IReadOnlyList<ItemStorageEntry> entries = ownershipComponent != null
                ? ownershipComponent.GetAllDirectEntries()
                : inventoryComponent.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                ItemStorageEntry entry = entries[index];
                if (entry == null || entry.Item == null || entry.Quantity < 0)
                    return InvalidSnapshot($"Inventory entry {index} is invalid while calculating carry weight.");

                if (!ItemWeightResolver.TryGetEntryWeight(entry, entry.Quantity, out double stackWeightKg, out error))
                {
                    return InvalidSnapshot(error);
                }

                currentWeightKg += stackWeightKg;
            }

            if (!IsFinite(currentWeightKg) || currentWeightKg < 0d)
                return InvalidSnapshot($"Calculated carry weight is invalid ({currentWeightKg}).");

            CarryWeightState state = ClassifyState(currentWeightKg, softCapacityKg, hardLimitKg);
            double ratio = currentWeightKg / softCapacityKg;
            return new CarryWeightSnapshot(
                currentWeightKg,
                softCapacityKg,
                hardLimitKg,
                ratio,
                state,
                true,
                null);
        }

        public CarryWeightAcceptance EvaluateIncomingWeight(string definitionId, int quantity)
        {
            CarryWeightSnapshot snapshot = GetSnapshot();
            if (!snapshot.IsValid)
            {
                return new CarryWeightAcceptance(
                    false,
                    0d,
                    snapshot.CurrentWeightKg,
                    snapshot.CurrentWeightKg,
                    snapshot.HardLimitKg,
                    snapshot.State,
                    snapshot.Error ?? "Carry weight is unavailable.");
            }

            if (!TryGetItemWeight(definitionId, quantity, out _, out double addedWeightKg, out string error))
            {
                LogErrorOnce(error);
                return new CarryWeightAcceptance(
                    false,
                    0d,
                    snapshot.CurrentWeightKg,
                    snapshot.CurrentWeightKg,
                    snapshot.HardLimitKg,
                    snapshot.State,
                    error);
            }

            double projectedWeightKg = snapshot.CurrentWeightKg + addedWeightKg;
            CarryWeightState projectedState = ClassifyState(
                projectedWeightKg,
                snapshot.SoftCapacityKg,
                snapshot.HardLimitKg);
            bool conservesWeight = addedWeightKg <= LimitEpsilon;
            bool accepted = conservesWeight || projectedWeightKg <= snapshot.HardLimitKg + LimitEpsilon;
            string failureReason = accepted
                ? null
                : $"Too heavy: {projectedWeightKg:0.00} / {snapshot.HardLimitKg:0.00} kg";

            return new CarryWeightAcceptance(
                accepted,
                addedWeightKg,
                snapshot.CurrentWeightKg,
                projectedWeightKg,
                snapshot.HardLimitKg,
                projectedState,
                failureReason);
        }

        public CarryWeightQuantityLimit EvaluateIncomingQuantityLimit(string definitionId, int requestedQuantity)
        {
            if (requestedQuantity < 0)
                return CarryWeightQuantityLimit.Invalid(requestedQuantity, "Requested quantity cannot be negative.");

            CarryWeightSnapshot snapshot = GetSnapshot();
            if (!snapshot.IsValid)
            {
                return CarryWeightQuantityLimit.Invalid(
                    requestedQuantity,
                    snapshot.Error ?? "Carry weight is unavailable.");
            }

            if (!TryGetItemWeight(definitionId, 1, out double unitWeightKg, out _, out string error))
            {
                LogErrorOnce(error);
                return CarryWeightQuantityLimit.Invalid(requestedQuantity, error);
            }

            if (requestedQuantity == 0 || unitWeightKg == 0d)
            {
                return new CarryWeightQuantityLimit(
                    true,
                    requestedQuantity,
                    requestedQuantity,
                    unitWeightKg,
                    snapshot.CurrentWeightKg,
                    snapshot.HardLimitKg,
                    null);
            }

            int maximumQuantity = 0;
            if (snapshot.CurrentWeightKg < snapshot.HardLimitKg)
            {
                double remainingWeightKg = Math.Max(0d, snapshot.HardLimitKg - snapshot.CurrentWeightKg);
                double rawMaximum = Math.Floor((remainingWeightKg + LimitEpsilon) / unitWeightKg);
                if (rawMaximum >= requestedQuantity)
                {
                    maximumQuantity = requestedQuantity;
                }
                else if (rawMaximum > 0d)
                {
                    maximumQuantity = (int)Math.Min(rawMaximum, int.MaxValue);
                }

                maximumQuantity = Math.Min(maximumQuantity, requestedQuantity);
                while (maximumQuantity > 0 &&
                       snapshot.CurrentWeightKg + (unitWeightKg * maximumQuantity) > snapshot.HardLimitKg + LimitEpsilon)
                {
                    maximumQuantity--;
                }
                while (maximumQuantity < requestedQuantity &&
                       snapshot.CurrentWeightKg + (unitWeightKg * (maximumQuantity + 1)) <= snapshot.HardLimitKg + LimitEpsilon)
                {
                    maximumQuantity++;
                }
            }

            return new CarryWeightQuantityLimit(
                true,
                requestedQuantity,
                maximumQuantity,
                unitWeightKg,
                snapshot.CurrentWeightKg,
                snapshot.HardLimitKg,
                null);
        }

        public CarryWeightAcceptance EvaluateIncomingEntry(ItemStorageEntry entry, int quantity)
        {
            CarryWeightSnapshot snapshot = GetSnapshot();
            if (!snapshot.IsValid)
            {
                return new CarryWeightAcceptance(
                    false, 0d, snapshot.CurrentWeightKg, snapshot.CurrentWeightKg,
                    snapshot.HardLimitKg, snapshot.State, snapshot.Error ?? "Carry weight is unavailable.");
            }

            if (!ItemWeightResolver.TryGetEntryWeight(entry, quantity, out double addedWeightKg, out string error))
            {
                LogErrorOnce(error);
                return new CarryWeightAcceptance(
                    false, 0d, snapshot.CurrentWeightKg, snapshot.CurrentWeightKg,
                    snapshot.HardLimitKg, snapshot.State, error);
            }

            double projectedWeightKg = snapshot.CurrentWeightKg + addedWeightKg;
            CarryWeightState projectedState = ClassifyState(projectedWeightKg, snapshot.SoftCapacityKg, snapshot.HardLimitKg);
            bool accepted = addedWeightKg <= LimitEpsilon || projectedWeightKg <= snapshot.HardLimitKg + LimitEpsilon;
            return new CarryWeightAcceptance(
                accepted,
                addedWeightKg,
                snapshot.CurrentWeightKg,
                projectedWeightKg,
                snapshot.HardLimitKg,
                projectedState,
                accepted ? null : $"Too heavy: {projectedWeightKg:0.00} / {snapshot.HardLimitKg:0.00} kg");
        }

        public CarryWeightQuantityLimit EvaluateIncomingEntryQuantityLimit(ItemStorageEntry entry, int requestedQuantity)
        {
            if (entry == null || entry.Item == null)
                return CarryWeightQuantityLimit.Invalid(requestedQuantity, "Incoming item entry is invalid.");

            if (!entry.Item.HasOwnedStorage)
                return EvaluateIncomingQuantityLimit(entry.DefinitionId, requestedQuantity);

            if (requestedQuantity < 0 || requestedQuantity > entry.Quantity)
                return CarryWeightQuantityLimit.Invalid(requestedQuantity, "Requested quantity is invalid for the incoming entry.");

            CarryWeightSnapshot snapshot = GetSnapshot();
            if (!snapshot.IsValid)
                return CarryWeightQuantityLimit.Invalid(requestedQuantity, snapshot.Error ?? "Carry weight is unavailable.");

            if (requestedQuantity == 0)
                return new CarryWeightQuantityLimit(true, 0, 0, 0d, snapshot.CurrentWeightKg, snapshot.HardLimitKg, null);

            if (!ItemWeightResolver.TryGetEntryWeight(entry, 1, out double unitWeightKg, out string error))
                return CarryWeightQuantityLimit.Invalid(requestedQuantity, error);

            bool accepted = snapshot.CurrentWeightKg + unitWeightKg <= snapshot.HardLimitKg + LimitEpsilon;
            return new CarryWeightQuantityLimit(
                true,
                requestedQuantity,
                accepted ? 1 : 0,
                unitWeightKg,
                snapshot.CurrentWeightKg,
                snapshot.HardLimitKg,
                null);
        }

        public bool TryGetItemWeight(
            string definitionId,
            int quantity,
            out double unitWeightKg,
            out double stackWeightKg,
            out string error)
        {
            bool resolved = ItemWeightResolver.TryGetDefinitionWeight(
                definitionId, quantity, out unitWeightKg, out stackWeightKg, out error);
            if (resolved)
                return true;

            RejectWeightResolution(error);
            return false;
        }

        private bool ResolveInventoryComponent()
        {
            if (inventoryComponent == null)
                inventoryComponent = GetComponent<InventoryComponent>();

            return inventoryComponent != null;
        }

        private void ResolveOwnershipComponent()
        {
            if (ownershipComponent == null)
                ownershipComponent = GetComponent<ActorItemOwnershipComponent>();
        }

        private bool TryValidateConfiguration(out double softCapacityKg, out double hardLimitKg, out string error)
        {
            softCapacityKg = baseCarryCapacityKg;
            hardLimitKg = softCapacityKg * hardLimitMultiplier;
            error = null;

            if (!IsFinite(softCapacityKg) || softCapacityKg <= 0d)
            {
                error = $"Actor carry soft capacity must be finite and > 0 (got {baseCarryCapacityKg}).";
                return false;
            }

            if (!IsFinite(hardLimitMultiplier) || hardLimitMultiplier < 1d)
            {
                error = $"Actor carry hard limit multiplier must be finite and >= 1 (got {hardLimitMultiplier}).";
                return false;
            }

            if (!IsFinite(hardLimitKg) || hardLimitKg < softCapacityKg)
            {
                error = $"Actor carry hard limit is invalid ({hardLimitKg}).";
                return false;
            }

            return true;
        }

        private CarryWeightSnapshot InvalidSnapshot(string error)
        {
            LogErrorOnce(error);
            return CarryWeightSnapshot.Invalid(error);
        }

        private bool RejectWeightResolution(string error)
        {
            LogErrorOnce(error);
            return false;
        }

        private void LogErrorOnce(string error)
        {
            string safeError = string.IsNullOrWhiteSpace(error) ? "Unknown carry weight error." : error;
            if (loggedErrors.Add(safeError))
                Debug.LogError($"[ActorCarryWeightComponent] {safeError}", this);
        }

        private static CarryWeightState ClassifyState(double currentWeightKg, double softCapacityKg, double hardLimitKg)
        {
            if (currentWeightKg <= softCapacityKg + LimitEpsilon)
                return CarryWeightState.Normal;

            return currentWeightKg <= hardLimitKg + LimitEpsilon
                ? CarryWeightState.Encumbered
                : CarryWeightState.HardBlocked;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
