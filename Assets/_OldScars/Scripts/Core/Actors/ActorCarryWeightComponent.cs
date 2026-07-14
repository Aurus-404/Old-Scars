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

                if (!TryGetItemWeight(
                        entry.DefinitionId,
                        entry.Quantity,
                        out _,
                        out double stackWeightKg,
                        out error))
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

        public bool TryGetItemWeight(
            string definitionId,
            int quantity,
            out double unitWeightKg,
            out double stackWeightKg,
            out string error)
        {
            unitWeightKg = 0d;
            stackWeightKg = 0d;
            error = null;

            if (string.IsNullOrWhiteSpace(definitionId))
            {
                error = "Cannot calculate carry weight without an item definition id.";
                return RejectWeightResolution(error);
            }

            if (quantity < 0)
            {
                error = $"Cannot calculate carry weight for '{definitionId}' with quantity {quantity}.";
                return RejectWeightResolution(error);
            }

            if (GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
            {
                error = $"Cannot resolve physical.weight_kg for item '{definitionId}' because game data is not ready.";
                return RejectWeightResolution(error);
            }

            GameDatabase database = GameDataManager.Instance.Database;
            ItemDefinition definition = database != null ? database.GetItem(definitionId) : null;
            if (definition == null)
            {
                error = $"Cannot resolve carry weight because item definition '{definitionId}' was not found.";
                return RejectWeightResolution(error);
            }

            if (definition.physical == null || !definition.physical.weight_kg.HasValue)
            {
                error = $"Item '{definitionId}' has no explicit physical.weight_kg.";
                return RejectWeightResolution(error);
            }

            double resolvedUnitWeight = definition.physical.weight_kg.Value;
            if (!IsFinite(resolvedUnitWeight) || resolvedUnitWeight < 0d)
            {
                error = $"Item '{definitionId}' has invalid physical.weight_kg '{resolvedUnitWeight}'.";
                return RejectWeightResolution(error);
            }

            double resolvedStackWeight = resolvedUnitWeight * quantity;
            if (!IsFinite(resolvedStackWeight) || resolvedStackWeight < 0d)
            {
                error = $"Item '{definitionId}' produced invalid stack weight for quantity {quantity}.";
                return RejectWeightResolution(error);
            }

            unitWeightKg = resolvedUnitWeight;
            stackWeightKg = resolvedStackWeight;
            return true;
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
