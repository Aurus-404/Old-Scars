using System;
using System.Collections.Generic;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using UnityEngine;

namespace OldScars.Core.Visuals
{
    [DisallowMultipleComponent]
    public sealed class EntityVisualRigRuntime : MonoBehaviour
    {
        [SerializeField] private string visualRigProfileId;
        [SerializeField] private VisualPartBinding[] partBindings = Array.Empty<VisualPartBinding>();
        [SerializeField] private VisualSocketBinding[] socketBindings = Array.Empty<VisualSocketBinding>();

        private readonly Dictionary<string, Transform> partsById = new Dictionary<string, Transform>();
        private readonly Dictionary<string, Transform> socketsById = new Dictionary<string, Transform>();
        private readonly Dictionary<string, VisualPartDefinition> partDefinitions = new Dictionary<string, VisualPartDefinition>();
        private readonly Dictionary<string, VisualSocketDefinition> socketDefinitions = new Dictionary<string, VisualSocketDefinition>();
        private readonly Dictionary<string, List<string>> socketIdsByCapability = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, List<string>> socketIdsByRole = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, List<string>> socketIdsByPart = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, string> socketRoleByEquipmentSlot = new Dictionary<string, string>();
        private readonly Dictionary<string, bool> partAvailability = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> socketAvailability = new Dictionary<string, bool>();
        private VisualRigProfileDefinition activeProfile;

        public event EventHandler<VisualRigAvailabilityChangedEventArgs> AvailabilityChanged;

        public string VisualRigProfileId => activeProfile != null ? activeProfile.id : visualRigProfileId;
        public string RigFamilyId => activeProfile != null ? activeProfile.family_id : null;
        public VisualRigProfileDefinition ActiveProfile => activeProfile;
        public IReadOnlyList<VisualPartBinding> PartBindings => Array.AsReadOnly(partBindings ?? Array.Empty<VisualPartBinding>());
        public IReadOnlyList<VisualSocketBinding> SocketBindings => Array.AsReadOnly(socketBindings ?? Array.Empty<VisualSocketBinding>());
        public bool IsReady { get; private set; }

        private void Awake()
        {
            EnsureReady();
        }

        private void OnEnable()
        {
            EnsureReady();
        }

        public bool TrySetProfile(string profileId, out string reason)
        {
            reason = null;
            if (string.IsNullOrWhiteSpace(profileId))
            {
                reason = "Visual rig profile id is required.";
                return false;
            }

            if (!TryGetDatabase(out GameDatabase database))
            {
                visualRigProfileId = profileId;
                IsReady = false;
                return true;
            }

            VisualRigProfileDefinition profile = database.GetVisualRigProfile(profileId);
            if (profile == null)
            {
                reason = $"Visual rig profile '{profileId}' was not loaded.";
                return false;
            }

            if (visualRigProfileId == profile.id && IsReady)
                return true;

            visualRigProfileId = profile.id;
            IsReady = false;

            return RebuildBindings(out reason);
        }

        public void ConfigureBindings(
            string profileId,
            VisualPartBinding[] configuredParts,
            VisualSocketBinding[] configuredSockets)
        {
            visualRigProfileId = profileId;
            partBindings = configuredParts != null ? (VisualPartBinding[])configuredParts.Clone() : Array.Empty<VisualPartBinding>();
            socketBindings = configuredSockets != null ? (VisualSocketBinding[])configuredSockets.Clone() : Array.Empty<VisualSocketBinding>();
            IsReady = false;
            EnsureReady();
        }

        public bool EnsureReady()
        {
            if (IsReady)
                return true;
            if (!TryGetDatabase(out _))
                return false;
            if (!RebuildBindings(out string reason))
            {
                if (!string.IsNullOrWhiteSpace(reason))
                    Debug.LogError($"[EntityVisualRigRuntime] {name}: {reason}", this);
                return false;
            }
            return true;
        }

        public bool RebuildBindings(out string reason)
        {
            reason = null;
            ClearIndexes();
            if (!TryGetDatabase(out GameDatabase database))
            {
                reason = "GameDatabase is not ready.";
                return false;
            }

            activeProfile = database.GetVisualRigProfile(visualRigProfileId);
            if (activeProfile == null)
            {
                reason = $"Visual rig profile '{visualRigProfileId}' was not loaded.";
                return false;
            }
            visualRigProfileId = activeProfile.id;

            if (!IndexConfiguredBindings(partBindings, partsById, binding => binding.PartId, binding => binding.Target, "part", out reason) ||
                !IndexConfiguredBindings(socketBindings, socketsById, binding => binding.SocketId, binding => binding.Target, "socket", out reason))
                return false;

            VisualPartDefinition[] profileParts = activeProfile.parts ?? Array.Empty<VisualPartDefinition>();
            for (int index = 0; index < profileParts.Length; index++)
            {
                VisualPartDefinition part = profileParts[index];
                if (part == null || string.IsNullOrWhiteSpace(part.id))
                    continue;
                partDefinitions[part.id] = part;
                bool available = part.enabled.GetValueOrDefault(true);
                partAvailability[part.id] = available;
                if (available && (!partsById.TryGetValue(part.id, out Transform target) || target == null))
                {
                    reason = $"Missing Transform binding for enabled part '{part.id}'.";
                    return false;
                }
            }

            VisualSocketDefinition[] profileSockets = activeProfile.sockets ?? Array.Empty<VisualSocketDefinition>();
            for (int index = 0; index < profileSockets.Length; index++)
            {
                VisualSocketDefinition socket = profileSockets[index];
                if (socket == null || string.IsNullOrWhiteSpace(socket.id))
                    continue;
                socketDefinitions[socket.id] = socket;
                if (!socketsById.TryGetValue(socket.id, out Transform target) || target == null)
                {
                    reason = $"Missing Transform binding for socket '{socket.id}'.";
                    return false;
                }

                if (!socketIdsByPart.TryGetValue(socket.part_id ?? string.Empty, out List<string> partSockets))
                {
                    partSockets = new List<string>();
                    socketIdsByPart[socket.part_id ?? string.Empty] = partSockets;
                }
                partSockets.Add(socket.id);

                if (!socketIdsByRole.TryGetValue(socket.role ?? string.Empty, out List<string> roleSockets))
                {
                    roleSockets = new List<string>();
                    socketIdsByRole[socket.role ?? string.Empty] = roleSockets;
                }
                roleSockets.Add(socket.id);

                string[] capabilities = socket.capabilities ?? Array.Empty<string>();
                for (int capabilityIndex = 0; capabilityIndex < capabilities.Length; capabilityIndex++)
                {
                    string capability = capabilities[capabilityIndex];
                    if (!socketIdsByCapability.TryGetValue(capability ?? string.Empty, out List<string> capableSockets))
                    {
                        capableSockets = new List<string>();
                        socketIdsByCapability[capability ?? string.Empty] = capableSockets;
                    }
                    capableSockets.Add(socket.id);
                }

                socketAvailability[socket.id] = socket.enabled.GetValueOrDefault(true) &&
                                                IsPartAvailable(socket.part_id);
            }

            VisualEquipmentSocketMappingDefinition[] mappings = activeProfile.equipment_slot_mappings ?? Array.Empty<VisualEquipmentSocketMappingDefinition>();
            for (int index = 0; index < mappings.Length; index++)
            {
                VisualEquipmentSocketMappingDefinition mapping = mappings[index];
                if (mapping != null && !string.IsNullOrWhiteSpace(mapping.equipment_slot_id))
                    socketRoleByEquipmentSlot[mapping.equipment_slot_id] = mapping.socket_role;
            }

            IsReady = true;
            return true;
        }

        public bool SetPartAvailable(string partId, bool available)
        {
            if (string.IsNullOrWhiteSpace(partId) || !partDefinitions.ContainsKey(partId))
                return false;
            if (partAvailability.TryGetValue(partId, out bool current) && current == available)
                return true;

            partAvailability[partId] = available;
            var affected = new List<string>();
            if (socketIdsByPart.TryGetValue(partId, out List<string> socketIds))
            {
                for (int index = 0; index < socketIds.Count; index++)
                {
                    string socketId = socketIds[index];
                    VisualSocketDefinition socket = socketDefinitions[socketId];
                    socketAvailability[socketId] = available && socket.enabled.GetValueOrDefault(true);
                    affected.Add(socketId);
                }
            }

            AvailabilityChanged?.Invoke(this, new VisualRigAvailabilityChangedEventArgs(partId, affected));
            return true;
        }

        public bool SetSocketAvailable(string socketId, bool available)
        {
            if (string.IsNullOrWhiteSpace(socketId) || !socketDefinitions.TryGetValue(socketId, out VisualSocketDefinition socket))
                return false;
            bool effective = available && IsPartAvailable(socket.part_id);
            if (socketAvailability.TryGetValue(socketId, out bool current) && current == effective)
                return true;
            socketAvailability[socketId] = effective;
            AvailabilityChanged?.Invoke(this, new VisualRigAvailabilityChangedEventArgs(socket.part_id, new[] { socketId }));
            return true;
        }

        public bool TryResolveForEquipmentSlot(
            string equipmentSlotId,
            IReadOnlyList<string> requiredCapabilities,
            out VisualSocketResolution resolution)
        {
            resolution = default;
            if (string.IsNullOrWhiteSpace(equipmentSlotId))
                return false;

            if (TryGetDatabase(out GameDatabase database) &&
                database.TryResolveEquipmentSlotId(equipmentSlotId, out string canonicalSlotId, out _))
            {
                equipmentSlotId = canonicalSlotId;
            }

            string fallbackRole = ContentId.TryParse(equipmentSlotId, out ContentId slotContentId, out _)
                ? slotContentId.LocalId
                : equipmentSlotId;
            string role = socketRoleByEquipmentSlot.TryGetValue(equipmentSlotId, out string mappedRole)
                ? mappedRole
                : fallbackRole;
            return TryResolveByRole(role, requiredCapabilities, out resolution);
        }

        public bool TryResolveByRole(
            string role,
            IReadOnlyList<string> requiredCapabilities,
            out VisualSocketResolution resolution)
        {
            resolution = default;
            if (!IsReady || string.IsNullOrWhiteSpace(role) ||
                !socketIdsByRole.TryGetValue(role, out List<string> socketIds))
                return false;

            for (int index = 0; index < socketIds.Count; index++)
            {
                VisualSocketDefinition socket = socketDefinitions[socketIds[index]];
                if (TryCreateResolution(socket, requiredCapabilities, out resolution))
                    return true;
            }
            return false;
        }

        public bool TryResolveByCapabilities(
            IReadOnlyList<string> requiredCapabilities,
            out VisualSocketResolution resolution)
        {
            resolution = default;
            if (!IsReady || requiredCapabilities == null || requiredCapabilities.Count == 0 ||
                !socketIdsByCapability.TryGetValue(requiredCapabilities[0], out List<string> socketIds))
                return false;

            for (int index = 0; index < socketIds.Count; index++)
            {
                VisualSocketDefinition socket = socketDefinitions[socketIds[index]];
                if (TryCreateResolution(socket, requiredCapabilities, out resolution))
                    return true;
            }
            return false;
        }

        public bool IsPartAvailable(string partId)
        {
            return !string.IsNullOrWhiteSpace(partId) &&
                   partAvailability.TryGetValue(partId, out bool available) &&
                   available;
        }

        public bool IsSocketAvailable(string socketId)
        {
            return !string.IsNullOrWhiteSpace(socketId) &&
                   socketAvailability.TryGetValue(socketId, out bool available) &&
                   available;
        }

        private bool TryCreateResolution(
            VisualSocketDefinition socket,
            IReadOnlyList<string> requiredCapabilities,
            out VisualSocketResolution resolution)
        {
            resolution = default;
            if (socket == null || !IsSocketAvailable(socket.id) || !HasAllCapabilities(socket, requiredCapabilities) ||
                !socketsById.TryGetValue(socket.id, out Transform target) || target == null)
                return false;

            resolution = new VisualSocketResolution(socket.id, socket.role, socket.part_id, target);
            return true;
        }

        private static bool HasAllCapabilities(VisualSocketDefinition socket, IReadOnlyList<string> required)
        {
            if (required == null || required.Count == 0)
                return true;
            string[] available = socket.capabilities ?? Array.Empty<string>();
            for (int requiredIndex = 0; requiredIndex < required.Count; requiredIndex++)
            {
                bool found = false;
                for (int availableIndex = 0; availableIndex < available.Length; availableIndex++)
                {
                    if (available[availableIndex] == required[requiredIndex])
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    return false;
            }
            return true;
        }

        private static bool IndexConfiguredBindings<T>(
            T[] bindings,
            Dictionary<string, Transform> target,
            Func<T, string> getId,
            Func<T, Transform> getTransform,
            string kind,
            out string reason)
            where T : class
        {
            reason = null;
            if (bindings == null)
                return true;
            for (int index = 0; index < bindings.Length; index++)
            {
                T binding = bindings[index];
                if (binding == null)
                    continue;
                string id = getId(binding);
                Transform transform = getTransform(binding);
                if (string.IsNullOrWhiteSpace(id) || transform == null)
                {
                    reason = $"Configured {kind} binding at index {index} is incomplete.";
                    return false;
                }
                if (target.ContainsKey(id))
                {
                    reason = $"Duplicate configured {kind} binding '{id}'.";
                    return false;
                }
                target[id] = transform;
            }
            return true;
        }

        private void ClearIndexes()
        {
            IsReady = false;
            activeProfile = null;
            partsById.Clear();
            socketsById.Clear();
            partDefinitions.Clear();
            socketDefinitions.Clear();
            socketIdsByCapability.Clear();
            socketIdsByRole.Clear();
            socketIdsByPart.Clear();
            socketRoleByEquipmentSlot.Clear();
            partAvailability.Clear();
            socketAvailability.Clear();
        }

        private static bool TryGetDatabase(out GameDatabase database)
        {
            database = GameDataManager.Instance != null && GameDataManager.Instance.IsReady
                ? GameDataManager.Instance.Database
                : null;
            return database != null;
        }
    }
}
