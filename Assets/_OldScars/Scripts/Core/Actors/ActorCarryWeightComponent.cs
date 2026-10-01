using System;
using System.Collections.Generic;
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
        [SerializeField, Range(0.01f, 1f)] private float minimumMovementFactor = 0.15f; // Provisional tuning at 100% capacity.

        private readonly HashSet<string> loggedErrors = new HashSet<string>();

        public float BaseCarryCapacityKg => baseCarryCapacityKg;
        public double CurrentWeightKg => GetSnapshot().CurrentWeightKg;
        public double CarryCapacityKg => baseCarryCapacityKg;
        public double LoadRatio => GetSnapshot().LoadRatio;
        public float LocomotionFactor => GetSnapshot().LocomotionFactor;
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
            if (!TryValidateConfiguration(out double capacityKg, out string error))
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

            double ratio = currentWeightKg / capacityKg;
            CarryWeightState state = ratio <= 0.75d + LimitEpsilon
                ? CarryWeightState.Normal
                : ratio <= 1d + LimitEpsilon ? CarryWeightState.Encumbered : CarryWeightState.Overloaded;
            float factor = state == CarryWeightState.Overloaded ? 0f
                : state == CarryWeightState.Normal ? 1f
                : Mathf.Lerp(1f, minimumMovementFactor, Mathf.Clamp01((float)((ratio - 0.75d) / 0.25d)));
            return new CarryWeightSnapshot(currentWeightKg, capacityKg, ratio, state, factor, true, null);
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

        private bool TryValidateConfiguration(out double capacityKg, out string error)
        {
            capacityKg = baseCarryCapacityKg;
            error = null;
            if (!IsFinite(capacityKg) || capacityKg <= 0d)
                error = $"Actor carry capacity must be finite and positive (got {baseCarryCapacityKg}).";
            else if (!IsFinite(minimumMovementFactor) || minimumMovementFactor <= 0f || minimumMovementFactor > 1f)
                error = "Minimum carry movement factor must be finite and in (0, 1].";
            return error == null;
        }

        private CarryWeightSnapshot InvalidSnapshot(string error)
        {
            LogErrorOnce(error);
            return CarryWeightSnapshot.Invalid(error);
        }

        private void LogErrorOnce(string error)
        {
            string safeError = string.IsNullOrWhiteSpace(error) ? "Unknown carry weight error." : error;
            if (loggedErrors.Add(safeError))
                Debug.LogError($"[ActorCarryWeightComponent] {safeError}", this);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
