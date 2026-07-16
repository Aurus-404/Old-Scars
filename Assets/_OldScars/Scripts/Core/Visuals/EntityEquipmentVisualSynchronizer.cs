using System;
using System.Collections;
using System.Collections.Generic;
using OldScars.Core.Actors;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using UnityEngine;

namespace OldScars.Core.Visuals
{
    [DisallowMultipleComponent]
    public sealed class EntityEquipmentVisualSynchronizer : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour equipmentVisualSourceBehaviour;
        [SerializeField] private EntityVisualRigRuntime visualRig;
        [SerializeField] private bool logWarnings = true;

        private readonly Dictionary<string, EquippedVisualRuntime> visualsByInstanceId =
            new Dictionary<string, EquippedVisualRuntime>();
        private readonly HashSet<string> warnedMessages = new HashSet<string>();
        private IEquipmentVisualSource source;
        private Coroutine initializeRoutine;
        private EquipmentVisualStateSnapshot pendingSnapshot;
        private long lastCommittedRevision = -1L;
        private int lastEquipmentVersion = -1;
        private int lastStorageVersion = -1;

        public int ActiveVisualCount => visualsByInstanceId.Count;

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
            if (initializeRoutine == null)
                initializeRoutine = StartCoroutine(InitializeWhenReady());
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (initializeRoutine != null)
            {
                StopCoroutine(initializeRoutine);
                initializeRoutine = null;
            }
            pendingSnapshot = null;
            ClearAllVisuals();
        }

        public void Configure(MonoBehaviour sourceBehaviour, EntityVisualRigRuntime rig)
        {
            bool wasActive = isActiveAndEnabled;
            if (wasActive)
                Unsubscribe();
            equipmentVisualSourceBehaviour = sourceBehaviour;
            visualRig = rig;
            ResolveReferences();
            if (wasActive)
            {
                Subscribe();
                RebuildFromCurrentState();
            }
        }

        [ContextMenu("Rebuild Equipment Visuals")]
        public void RebuildFromCurrentState()
        {
            ResolveReferences();
            if (source == null)
            {
                WarnOnce("missing_source", $"[EntityEquipmentVisualSynchronizer] '{name}' has no IEquipmentVisualSource.");
                return;
            }

            EquipmentVisualStateSnapshot snapshot = source.CaptureVisualSnapshot();
            if (!TryApplySnapshot(snapshot, true))
                pendingSnapshot = snapshot;
        }

        private IEnumerator InitializeWhenReady()
        {
            while (GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
                yield return null;

            initializeRoutine = null;
            ResolveReferences();
            if (visualRig != null)
                visualRig.EnsureReady();
            RebuildFromCurrentState();
        }

        private void ResolveReferences()
        {
            if (visualRig == null)
                visualRig = GetComponent<EntityVisualRigRuntime>();

            source = equipmentVisualSourceBehaviour as IEquipmentVisualSource;
            if (source != null)
                return;

            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            for (int index = 0; index < behaviours.Length; index++)
            {
                if (behaviours[index] is IEquipmentVisualSource candidate)
                {
                    equipmentVisualSourceBehaviour = behaviours[index];
                    source = candidate;
                    break;
                }
            }
        }

        private void Subscribe()
        {
            if (source != null)
                source.VisualStateCommitted += HandleVisualStateCommitted;
            if (visualRig != null)
                visualRig.AvailabilityChanged += HandleRigAvailabilityChanged;
        }

        private void Unsubscribe()
        {
            if (source != null)
                source.VisualStateCommitted -= HandleVisualStateCommitted;
            if (visualRig != null)
                visualRig.AvailabilityChanged -= HandleRigAvailabilityChanged;
        }

        private void HandleVisualStateCommitted(object sender, EquipmentVisualStateCommittedEventArgs args)
        {
            if (args?.Snapshot == null)
                return;
            if (!TryApplySnapshot(args.Snapshot, false))
                pendingSnapshot = args.Snapshot;
        }

        private void HandleRigAvailabilityChanged(object sender, VisualRigAvailabilityChangedEventArgs args)
        {
            if (source == null)
                return;
            EquipmentVisualStateSnapshot snapshot = source.CaptureVisualSnapshot();
            if (!TryApplySnapshot(snapshot, true))
                pendingSnapshot = snapshot;
        }

        private bool TryApplySnapshot(EquipmentVisualStateSnapshot snapshot, bool force)
        {
            if (snapshot == null || GameDataManager.Instance == null || !GameDataManager.Instance.IsReady ||
                GameDataManager.Instance.Database == null || visualRig == null || !visualRig.EnsureReady())
                return false;

            if (!force && snapshot.CommittedRevision <= lastCommittedRevision)
                return true;

            if (force && snapshot.CommittedRevision == lastCommittedRevision &&
                snapshot.EquipmentVersion == lastEquipmentVersion &&
                snapshot.StorageVersion == lastStorageVersion && pendingSnapshot == null)
            {
                // A forced rebuild is still required for rig/provider invalidation.
            }

            Reconcile(snapshot, GameDataManager.Instance.Database);
            lastCommittedRevision = snapshot.CommittedRevision;
            lastEquipmentVersion = snapshot.EquipmentVersion;
            lastStorageVersion = snapshot.StorageVersion;
            pendingSnapshot = null;
            return true;
        }

        private void Reconcile(EquipmentVisualStateSnapshot snapshot, GameDatabase database)
        {
            var expectedByInstance = new Dictionary<string, ExpectedVisual>();
            IReadOnlyList<EquipmentVisualItemSnapshot> items = snapshot.EquippedItems;
            for (int index = 0; index < items.Count; index++)
            {
                EquipmentVisualItemSnapshot item = items[index];
                if (item == null || string.IsNullOrWhiteSpace(item.InstanceId))
                    continue;
                if (expectedByInstance.ContainsKey(item.InstanceId))
                {
                    WarnOnce(
                        "duplicate:" + item.InstanceId,
                        $"[EntityEquipmentVisualSynchronizer] Duplicate equipped InstanceId '{item.InstanceId}' was ignored; one visual is allowed per instance.");
                    continue;
                }

                ItemVisualProfileDefinition profile = database.GetItemVisualProfileByItemDefinitionId(item.DefinitionId);
                if (profile == null || !profile.enabled.GetValueOrDefault(true) || string.IsNullOrWhiteSpace(profile.equipped_asset_key))
                    continue;
                if (!TryResolveSocket(profile, item.OccupiedSlots, out VisualSocketResolution socket))
                {
                    WarnOnce(
                        "socket:" + item.InstanceId + ":" + visualRig.VisualRigProfileId,
                        $"[EntityEquipmentVisualSynchronizer] No compatible visual socket for '{item.DefinitionId}' on rig '{visualRig.VisualRigProfileId}'.");
                    continue;
                }

                AttachmentPoseValue pose = AttachmentPoseResolver.Resolve(
                    database,
                    profile,
                    visualRig.VisualRigProfileId,
                    visualRig.RigFamilyId,
                    socket.SocketId,
                    socket.Role);
                expectedByInstance[item.InstanceId] = new ExpectedVisual(item, profile, socket, pose);
            }

            var removals = new List<string>();
            foreach (KeyValuePair<string, EquippedVisualRuntime> pair in visualsByInstanceId)
            {
                if (!expectedByInstance.TryGetValue(pair.Key, out ExpectedVisual expected) || !pair.Value.Matches(expected))
                    removals.Add(pair.Key);
                else
                    ApplyPose(pair.Value.Bindings.AttachmentRoot, expected.Pose);
            }
            for (int index = 0; index < removals.Count; index++)
                RemoveVisual(removals[index]);

            foreach (KeyValuePair<string, ExpectedVisual> pair in expectedByInstance)
            {
                if (!visualsByInstanceId.ContainsKey(pair.Key))
                    TryCreateVisual(pair.Value, database);
            }
        }

        private bool TryResolveSocket(
            ItemVisualProfileDefinition profile,
            IReadOnlyList<string> occupiedSlots,
            out VisualSocketResolution socket)
        {
            IReadOnlyList<string> capabilities = profile.required_socket_capabilities ?? Array.Empty<string>();
            socket = default;

            if (profile.socket_policy == ItemVisualSocketPolicy.PreferredRoleThenCapability &&
                visualRig.TryResolveByRole(profile.primary_socket_role, capabilities, out socket))
                return true;

            for (int index = 0; index < occupiedSlots.Count; index++)
            {
                if (visualRig.TryResolveForEquipmentSlot(occupiedSlots[index], capabilities, out socket))
                    return true;
            }

            return profile.socket_policy == ItemVisualSocketPolicy.PreferredRoleThenCapability &&
                   visualRig.TryResolveByCapabilities(capabilities, out socket);
        }

        private void TryCreateVisual(ExpectedVisual expected, GameDatabase database)
        {
            GameObject instance = null;
            EquippedVisualPrefabBindings bindings = null;
            VisualAssetDefinition asset = database.GetVisualAssetByKey(expected.Profile.equipped_asset_key);
            string providerError = null;

            if (asset != null &&
                VisualAssetProviderRegistry.TryGet(asset.provider_id, out IVisualAssetProvider provider) &&
                provider.TryResolvePrefab(asset, out GameObject prefab, out providerError) &&
                EquippedVisualPrefabContract.TryValidate(prefab, out providerError))
            {
                instance = Instantiate(prefab, expected.Socket.Target, false);
                instance.name = "EquippedVisual_" + expected.Item.InstanceId;
                bindings = instance.GetComponent<EquippedVisualPrefabBindings>();
            }

            if (instance == null && expected.Profile.fallback_visual == ItemVisualFallback.DebugBox)
            {
                instance = CreateDebugFallback(expected.Socket.Target, expected.Profile.id, out bindings);
            }
            if (instance == null || bindings == null || bindings.AttachmentRoot == null)
            {
                WarnOnce(
                    "asset:" + expected.Profile.equipped_asset_key,
                    $"[EntityEquipmentVisualSynchronizer] Could not create '{expected.Profile.equipped_asset_key}': {providerError ?? "no provider or fallback available"}");
                if (instance != null)
                    Destroy(instance);
                return;
            }

            ApplyPose(bindings.AttachmentRoot, expected.Pose);
            SetLayerRecursively(instance, expected.Socket.Target.gameObject.layer);
            EquippedVisualInstanceMarker marker = instance.GetComponent<EquippedVisualInstanceMarker>();
            if (marker == null)
                marker = instance.AddComponent<EquippedVisualInstanceMarker>();
            marker.Configure(
                expected.Item.InstanceId,
                expected.Item.DefinitionId,
                expected.Profile.id,
                visualRig.VisualRigProfileId,
                expected.Socket.SocketId,
                expected.Socket.Role);

            visualsByInstanceId[expected.Item.InstanceId] = new EquippedVisualRuntime(
                expected.Item.InstanceId,
                expected.Profile.id,
                expected.Profile.equipped_asset_key,
                expected.Socket.SocketId,
                expected.Socket.Target,
                instance,
                bindings);
        }

        private static GameObject CreateDebugFallback(
            Transform parent,
            string visualProfileId,
            out EquippedVisualPrefabBindings bindings)
        {
            var root = new GameObject("EquippedVisual_DebugFallback");
            root.transform.SetParent(parent, false);
            var attachment = new GameObject("AttachmentRoot");
            attachment.transform.SetParent(root.transform, false);
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "DebugBox";
            box.transform.SetParent(attachment.transform, false);
            box.transform.localScale = new Vector3(0.18f, 0.18f, 0.18f);
            Collider collider = box.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
            bindings = root.AddComponent<EquippedVisualPrefabBindings>();
            bindings.Configure(visualProfileId, attachment.transform);
            return root;
        }

        private static void ApplyPose(Transform attachmentRoot, AttachmentPoseValue pose)
        {
            if (attachmentRoot == null)
                return;
            attachmentRoot.localPosition = pose.LocalPosition;
            attachmentRoot.localEulerAngles = pose.LocalEulerAngles;
            attachmentRoot.localScale = pose.LocalScale;
        }

        private void RemoveVisual(string instanceId)
        {
            if (!visualsByInstanceId.TryGetValue(instanceId, out EquippedVisualRuntime runtime))
                return;
            visualsByInstanceId.Remove(instanceId);
            if (runtime.Root != null)
                Destroy(runtime.Root);
        }

        private void ClearAllVisuals()
        {
            foreach (EquippedVisualRuntime runtime in visualsByInstanceId.Values)
            {
                if (runtime.Root != null)
                    Destroy(runtime.Root);
            }
            visualsByInstanceId.Clear();
            lastCommittedRevision = -1L;
            lastEquipmentVersion = -1;
            lastStorageVersion = -1;
        }

        private void WarnOnce(string key, string message)
        {
            if (logWarnings && warnedMessages.Add(key))
                Debug.LogWarning(message, this);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null)
                return;
            root.layer = layer;
            for (int index = 0; index < root.transform.childCount; index++)
                SetLayerRecursively(root.transform.GetChild(index).gameObject, layer);
        }

        private sealed class ExpectedVisual
        {
            public ExpectedVisual(
                EquipmentVisualItemSnapshot item,
                ItemVisualProfileDefinition profile,
                VisualSocketResolution socket,
                AttachmentPoseValue pose)
            {
                Item = item;
                Profile = profile;
                Socket = socket;
                Pose = pose;
            }

            public EquipmentVisualItemSnapshot Item { get; }
            public ItemVisualProfileDefinition Profile { get; }
            public VisualSocketResolution Socket { get; }
            public AttachmentPoseValue Pose { get; }
        }

        private sealed class EquippedVisualRuntime
        {
            public EquippedVisualRuntime(
                string instanceId,
                string visualProfileId,
                string assetKey,
                string socketId,
                Transform socketTarget,
                GameObject root,
                EquippedVisualPrefabBindings bindings)
            {
                InstanceId = instanceId;
                VisualProfileId = visualProfileId;
                AssetKey = assetKey;
                SocketId = socketId;
                SocketTarget = socketTarget;
                Root = root;
                Bindings = bindings;
            }

            public string InstanceId { get; }
            public string VisualProfileId { get; }
            public string AssetKey { get; }
            public string SocketId { get; }
            public Transform SocketTarget { get; }
            public GameObject Root { get; }
            public EquippedVisualPrefabBindings Bindings { get; }

            public bool Matches(ExpectedVisual expected)
            {
                return expected != null &&
                       InstanceId == expected.Item.InstanceId &&
                       VisualProfileId == expected.Profile.id &&
                       AssetKey == expected.Profile.equipped_asset_key &&
                       SocketId == expected.Socket.SocketId &&
                       SocketTarget == expected.Socket.Target &&
                       Root != null && Bindings != null && Bindings.AttachmentRoot != null;
            }
        }
    }
}
