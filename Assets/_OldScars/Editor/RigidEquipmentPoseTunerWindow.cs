using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Data.Loading;
using OldScars.Core.Data.Validation;
using OldScars.Core.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldScars.EditorTools
{
    public sealed class RigidEquipmentPoseTunerWindow : EditorWindow
    {
        internal const string AttachmentPosesAssetPath =
            "Assets/StreamingAssets/Mods/Core/attachment_poses/attachment_poses.json";
        private const string HumanoidPrefabPath =
            "Assets/_OldScars/Resources/OldScarsActorRepresentations/humanoid_standard.prefab";

        private RigidEquipmentPoseCatalog catalog;
        private RigidEquipmentPosePreview preview;
        private int itemIndex;
        private int contextIndex;
        private Vector2 scroll;
        private string status;

        [MenuItem("Old Scars/Visuals/Rigid Equipment Pose Tuner")]
        public static void Open()
        {
            RigidEquipmentPoseTunerWindow window = GetWindow<RigidEquipmentPoseTunerWindow>();
            window.titleContent = new GUIContent("Rigid Pose Tuner");
            window.minSize = new Vector2(430f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ClosePreview;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            ReloadCatalog();
        }

        private void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= ClosePreview;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            ClosePreview();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode || change == PlayModeStateChange.EnteredPlayMode)
                ClosePreview();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Rigid Equipment Pose Tuner", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Rigid equipped visuals only. Skinned/deformable wearables are excluded. " +
                "The preview uses a temporary additive scene and is never saved into gameplay scenes.",
                MessageType.Info);

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                ClosePreview();
                status = "Rigid Equipment Pose Tuner requires Edit Mode.";
                EditorGUILayout.HelpBox(status, MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }

            if (catalog == null || catalog.Entries.Count == 0)
            {
                EditorGUILayout.HelpBox(status ?? "No tuneable rigid attachment was found.", MessageType.Warning);
                if (GUILayout.Button("Reload Content"))
                    ReloadCatalog();
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.LabelField("Rig", catalog.Rig.DisplayName);
            string[] itemLabels = catalog.Entries.Select(entry => entry.DisplayName).ToArray();
            int nextItemIndex = EditorGUILayout.Popup("Item", itemIndex, itemLabels);
            if (nextItemIndex != itemIndex)
            {
                itemIndex = nextItemIndex;
                contextIndex = 0;
                ClosePreview();
            }

            RigidEquipmentPoseEntry entry = SelectedEntry;
            string[] contextLabels = entry.Contexts.Select(context => context.DisplayName).ToArray();
            int nextContextIndex = EditorGUILayout.Popup("Attachment Context", contextIndex, contextLabels);
            if (nextContextIndex != contextIndex)
            {
                contextIndex = nextContextIndex;
                ClosePreview();
            }

            RigidEquipmentPoseContext context = SelectedContext;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Item ID", entry.Item.id);
            EditorGUILayout.LabelField("ItemVisualProfile ID", entry.Profile.id);
            EditorGUILayout.LabelField("Equipped VisualAsset", entry.Asset.asset_key);
            EditorGUILayout.LabelField("Socket", context.Socket.id + " (" + context.Socket.role + ")");

            AttachmentPoseDefinition resolved = ResolveCurrentDefinition(entry, context);
            EditorGUILayout.LabelField("Resolved Pose", resolved != null ? resolved.id : "<identity / new pose>");

            if (!HasEditablePreview)
            {
                EditorGUILayout.HelpBox(
                    "Preview not loaded. Load the selected item to begin editing.",
                    MessageType.Info);
                if (GUILayout.Button("Load Preview"))
                    LoadPreview(entry, context);
                if (!string.IsNullOrWhiteSpace(status))
                    EditorGUILayout.HelpBox(status, MessageType.None);
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawTransformFields();

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Load Preview"))
                LoadPreview(entry, context);
            if (GUILayout.Button("Select AttachmentRoot"))
                SelectAttachmentRoot();
            if (GUILayout.Button("Reload Saved"))
                ReloadSaved();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Preview"))
                LoadPreview(entry, context);
            if (GUILayout.Button("Save Pose"))
                SavePose();
            if (GUILayout.Button("Close Preview"))
                ClosePreview();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(status))
                EditorGUILayout.HelpBox(status, MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        private void DrawTransformFields()
        {
            if (!TryGetEditableAttachmentRoot(out Transform attachmentRoot))
                return;
            Vector3 position = EditorGUILayout.Vector3Field("Position", attachmentRoot.localPosition);
            Vector3 rotation = EditorGUILayout.Vector3Field("Rotation", attachmentRoot.localEulerAngles);
            Vector3 scale = EditorGUILayout.Vector3Field("Scale", attachmentRoot.localScale);
            float uniform = EditorGUILayout.FloatField("Uniform Scale", scale.x);
            if (!Mathf.Approximately(uniform, scale.x))
                scale = Vector3.one * Mathf.Max(0.0001f, uniform);

            if (position == attachmentRoot.localPosition && rotation == attachmentRoot.localEulerAngles &&
                scale == attachmentRoot.localScale)
                return;

            Undo.RecordObject(attachmentRoot, "Tune rigid equipment attachment pose");
            attachmentRoot.localPosition = position;
            attachmentRoot.localEulerAngles = rotation;
            attachmentRoot.localScale = new Vector3(
                Mathf.Max(0.0001f, scale.x),
                Mathf.Max(0.0001f, scale.y),
                Mathf.Max(0.0001f, scale.z));
            SceneView.RepaintAll();
        }

        private void ReloadCatalog()
        {
            string selectedItemId = SelectedEntry != null ? SelectedEntry.Item.id : null;
            string selectedSocketId = SelectedContext != null ? SelectedContext.Socket.id : null;
            if (!RigidEquipmentPoseCatalog.TryLoad(HumanoidPrefabPath, out catalog, out string error))
            {
                status = error;
                catalog = null;
                return;
            }

            itemIndex = FindItemIndex(selectedItemId);
            contextIndex = FindContextIndex(selectedSocketId);
            status = "Loaded " + catalog.Entries.Count + " tuneable rigid attachment profile(s).";
        }

        private void LoadPreview(RigidEquipmentPoseEntry entry, RigidEquipmentPoseContext context)
        {
            ClosePreview();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                status = "Rigid Equipment Pose Tuner requires Edit Mode.";
                return;
            }
            try
            {
                preview = RigidEquipmentPosePreview.Create(catalog, entry, context, HumanoidPrefabPath);
                status = "Preview loaded. Use W/E/R in Scene View, then Save Pose.";
            }
            catch (Exception exception)
            {
                ClosePreview();
                status = "Preview failed: " + exception.Message;
                Debug.LogException(exception);
            }
        }

        private void ReloadSaved()
        {
            string itemId = SelectedEntry != null ? SelectedEntry.Item.id : null;
            string socketId = SelectedContext != null ? SelectedContext.Socket.id : null;
            ReloadCatalog();
            itemIndex = FindItemIndex(itemId);
            contextIndex = FindContextIndex(socketId);
            if (SelectedEntry != null && SelectedContext != null)
                LoadPreview(SelectedEntry, SelectedContext);
        }

        private void SavePose()
        {
            if (preview == null || !preview.IsValid)
            {
                status = "Load a preview before saving.";
                return;
            }

            RigidEquipmentPoseEntry entry = SelectedEntry;
            RigidEquipmentPoseContext context = SelectedContext;
            AttachmentPoseDefinition resolved = ResolveCurrentDefinition(entry, context);
            AttachmentPoseDefinition pose = RigidEquipmentPoseAuthoring.CreateDefinition(
                entry.Profile,
                catalog.Rig.Profile,
                context.Socket,
                resolved,
                preview.AttachmentRoot);
            string absolutePath = AssetPathToAbsolute(AttachmentPosesAssetPath);
            AttachmentPoseJsonStore.Save(absolutePath, pose);
            AssetDatabase.ImportAsset(AttachmentPosesAssetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log(
                "[RigidEquipmentPoseTuner] Saved pose '" + pose.id + "' for visual profile '" +
                entry.Profile.id + "' on socket '" + context.Socket.id + "'.",
                preview.AttachmentRoot);
            ReloadCatalog();
            itemIndex = FindItemIndex(entry.Item.id);
            contextIndex = FindContextIndex(context.Socket.id);
            status = "Saved pose " + pose.id + ".";
        }

        private AttachmentPoseDefinition ResolveCurrentDefinition(
            RigidEquipmentPoseEntry entry,
            RigidEquipmentPoseContext context)
        {
            if (catalog == null || entry == null || context == null)
                return null;
            AttachmentPoseResolver.TryResolveDefinition(
                catalog.Database,
                entry.Profile,
                catalog.Rig.Profile.id,
                catalog.Rig.Profile.family_id,
                context.Socket.id,
                context.Socket.role,
                out AttachmentPoseDefinition definition);
            return definition;
        }

        private void SelectAttachmentRoot()
        {
            if (preview == null || !preview.IsValid)
                return;
            Selection.activeTransform = preview.AttachmentRoot;
            if (!Application.isBatchMode && SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();
        }

        internal bool DiagnosticLoad(string itemId, string socketRole)
        {
            ReloadCatalog();
            itemIndex = catalog.Entries.FindIndex(entry => entry.Item.id == itemId);
            if (itemIndex < 0)
                return false;
            contextIndex = catalog.Entries[itemIndex].Contexts.FindIndex(context => context.Socket.role == socketRole);
            if (contextIndex < 0)
                return false;
            LoadPreview(SelectedEntry, SelectedContext);
            return preview != null && preview.IsValid;
        }

        internal Transform DiagnosticAttachmentRoot => preview != null ? preview.AttachmentRoot : null;
        internal RigidEquipmentPoseCatalog DiagnosticCatalog => catalog;
        internal bool DiagnosticHasEditablePreview => HasEditablePreview;

        internal void DiagnosticClosePreview()
        {
            ClosePreview();
        }

        private void ClosePreview()
        {
            if (preview != null)
            {
                preview.Dispose();
                preview = null;
            }
        }

        private bool HasEditablePreview => TryGetEditableAttachmentRoot(out _);

        private bool TryGetEditableAttachmentRoot(out Transform attachmentRoot)
        {
            attachmentRoot = preview != null && preview.IsValid ? preview.AttachmentRoot : null;
            return attachmentRoot != null;
        }

        private int FindItemIndex(string itemId)
        {
            if (catalog == null || catalog.Entries.Count == 0 || string.IsNullOrWhiteSpace(itemId))
                return 0;
            int found = catalog.Entries.FindIndex(entry => entry.Item.id == itemId);
            return found >= 0 ? found : 0;
        }

        private int FindContextIndex(string socketId)
        {
            RigidEquipmentPoseEntry entry = SelectedEntry;
            if (entry == null || string.IsNullOrWhiteSpace(socketId))
                return 0;
            int found = entry.Contexts.FindIndex(context => context.Socket.id == socketId);
            return found >= 0 ? found : 0;
        }

        private RigidEquipmentPoseEntry SelectedEntry =>
            catalog != null && itemIndex >= 0 && itemIndex < catalog.Entries.Count
                ? catalog.Entries[itemIndex]
                : null;

        private RigidEquipmentPoseContext SelectedContext =>
            SelectedEntry != null && contextIndex >= 0 && contextIndex < SelectedEntry.Contexts.Count
                ? SelectedEntry.Contexts[contextIndex]
                : null;

        internal static string AssetPathToAbsolute(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }

    internal sealed class RigidEquipmentPoseCatalog
    {
        private RigidEquipmentPoseCatalog(GameDatabase database, RigidEquipmentRigEntry rig, List<RigidEquipmentPoseEntry> entries)
        {
            Database = database;
            Rig = rig;
            Entries = entries;
        }

        public GameDatabase Database { get; }
        public RigidEquipmentRigEntry Rig { get; }
        public List<RigidEquipmentPoseEntry> Entries { get; }

        public static bool TryLoad(string humanoidPrefabPath, out RigidEquipmentPoseCatalog catalog, out string error)
        {
            catalog = null;
            error = null;
            var report = new DataLoadReport();
            string modsRoot = Path.Combine(Application.streamingAssetsPath, "Mods");
            var loader = new GameDataLoader(modsRoot, report);
            loader.LoadAll();
            var validator = new DataValidator(loader.Database, loader.Tags, report);
            validator.Validate();
            if (report.HasErrors)
            {
                error = "GameData could not be loaded: " + string.Join(" | ", report.Errors);
                return false;
            }

            GameObject rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(humanoidPrefabPath);
            EntityVisualRigRuntime rigRuntime = rigPrefab != null ? rigPrefab.GetComponent<EntityVisualRigRuntime>() : null;
            if (rigRuntime == null)
            {
                error = "humanoid_standard is missing EntityVisualRigRuntime.";
                return false;
            }

            SerializedProperty rigProfileProperty = new SerializedObject(rigRuntime).FindProperty("visualRigProfileId");
            string rigProfileId = rigProfileProperty != null ? rigProfileProperty.stringValue : rigRuntime.VisualRigProfileId;
            VisualRigProfileDefinition rigProfile = loader.Database.GetVisualRigProfile(rigProfileId);
            if (rigProfile == null)
            {
                error = "VisualRigProfile was not loaded for humanoid_standard: " + rigProfileId;
                return false;
            }

            var boundSocketIds = new HashSet<string>(
                rigRuntime.SocketBindings.Where(binding => binding != null && binding.Target != null)
                    .Select(binding => binding.SocketId),
                StringComparer.Ordinal);
            var rig = new RigidEquipmentRigEntry(rigProfile, rigPrefab, boundSocketIds);
            var entries = new List<RigidEquipmentPoseEntry>();
            foreach (ItemVisualProfileDefinition profile in loader.Database.GetAllItemVisualProfiles().OrderBy(value => value.id))
            {
                if (profile == null || !profile.enabled.GetValueOrDefault(true) ||
                    string.IsNullOrWhiteSpace(profile.equipped_asset_key))
                    continue;

                ItemDefinition item = loader.Database.GetItem(profile.item_definition_id);
                VisualAssetDefinition asset = loader.Database.GetVisualAssetByKey(profile.equipped_asset_key);
                if (item == null || item.equip == null || item.equip.equippable != true || asset == null)
                    continue;
                if (!VisualAssetProviderRegistry.TryGet(asset.provider_id, out IVisualAssetProvider provider) ||
                    !provider.TryResolvePrefab(asset, out GameObject prefab, out _) || prefab == null ||
                    !EquippedVisualPrefabContract.TryValidate(prefab, out _) ||
                    prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                    continue;

                List<RigidEquipmentPoseContext> contexts = BuildContexts(item, profile, rigProfile, boundSocketIds);
                if (contexts.Count > 0)
                    entries.Add(new RigidEquipmentPoseEntry(item, profile, asset, prefab, contexts));
            }

            catalog = new RigidEquipmentPoseCatalog(loader.Database, rig, entries);
            return true;
        }

        private static List<RigidEquipmentPoseContext> BuildContexts(
            ItemDefinition item,
            ItemVisualProfileDefinition profile,
            VisualRigProfileDefinition rig,
            HashSet<string> boundSocketIds)
        {
            var contexts = new List<RigidEquipmentPoseContext>();
            foreach (string[] slotSet in EnumerateSlotSets(item.equip))
            {
                VisualSocketDefinition resolved = ResolveRuntimeSocket(profile, slotSet, rig, boundSocketIds);
                if (resolved != null && contexts.All(existing => existing.Socket.id != resolved.id))
                    contexts.Add(new RigidEquipmentPoseContext(resolved));
            }
            return contexts;
        }

        private static IEnumerable<string[]> EnumerateSlotSets(ItemEquip equip)
        {
            if (equip.slot_sets != null && equip.slot_sets.Length > 0)
            {
                foreach (string[] slotSet in equip.slot_sets)
                    yield return slotSet ?? Array.Empty<string>();
                yield break;
            }
            if (equip.occupied_slots != null && equip.occupied_slots.Length > 0)
            {
                yield return equip.occupied_slots;
                yield break;
            }
            if (equip.allowed_slots != null)
            {
                foreach (string slot in equip.allowed_slots)
                    yield return new[] { slot };
            }
        }

        private static VisualSocketDefinition ResolveRuntimeSocket(
            ItemVisualProfileDefinition profile,
            IReadOnlyList<string> occupiedSlots,
            VisualRigProfileDefinition rig,
            HashSet<string> boundSocketIds)
        {
            if (profile.socket_policy == ItemVisualSocketPolicy.PreferredRoleThenCapability)
            {
                VisualSocketDefinition preferred = FindSocketByRole(
                    rig, profile.primary_socket_role, profile.required_socket_capabilities, boundSocketIds);
                if (preferred != null)
                    return preferred;
            }

            for (int index = 0; index < occupiedSlots.Count; index++)
            {
                string slot = occupiedSlots[index];
                VisualEquipmentSocketMappingDefinition mapping = (rig.equipment_slot_mappings ?? Array.Empty<VisualEquipmentSocketMappingDefinition>())
                    .FirstOrDefault(value => value != null && value.equipment_slot_id == slot);
                string role = mapping != null
                    ? mapping.socket_role
                    : (ContentId.TryParse(slot, out ContentId parsed, out _) ? parsed.LocalId : slot);
                VisualSocketDefinition resolved = FindSocketByRole(
                    rig, role, profile.required_socket_capabilities, boundSocketIds);
                if (resolved != null)
                    return resolved;
            }

            return profile.socket_policy == ItemVisualSocketPolicy.PreferredRoleThenCapability
                ? (rig.sockets ?? Array.Empty<VisualSocketDefinition>()).FirstOrDefault(
                    socket => IsCompatible(socket, profile.required_socket_capabilities, boundSocketIds))
                : null;
        }

        private static VisualSocketDefinition FindSocketByRole(
            VisualRigProfileDefinition rig,
            string role,
            IReadOnlyList<string> requiredCapabilities,
            HashSet<string> boundSocketIds)
        {
            return (rig.sockets ?? Array.Empty<VisualSocketDefinition>()).FirstOrDefault(
                socket => socket != null && socket.role == role && IsCompatible(socket, requiredCapabilities, boundSocketIds));
        }

        private static bool IsCompatible(
            VisualSocketDefinition socket,
            IReadOnlyList<string> requiredCapabilities,
            HashSet<string> boundSocketIds)
        {
            if (socket == null || !socket.enabled.GetValueOrDefault(true) || !boundSocketIds.Contains(socket.id))
                return false;
            string[] available = socket.capabilities ?? Array.Empty<string>();
            if (requiredCapabilities == null)
                return true;
            for (int index = 0; index < requiredCapabilities.Count; index++)
            {
                if (!available.Contains(requiredCapabilities[index]))
                    return false;
            }
            return true;
        }
    }

    internal sealed class RigidEquipmentRigEntry
    {
        public RigidEquipmentRigEntry(
            VisualRigProfileDefinition profile,
            GameObject prefab,
            HashSet<string> boundSocketIds)
        {
            Profile = profile;
            Prefab = prefab;
            BoundSocketIds = boundSocketIds;
        }

        public VisualRigProfileDefinition Profile { get; }
        public GameObject Prefab { get; }
        public HashSet<string> BoundSocketIds { get; }
        public string DisplayName => string.IsNullOrWhiteSpace(Profile.display_name) ? Profile.id : Profile.display_name;
    }

    internal sealed class RigidEquipmentPoseEntry
    {
        public RigidEquipmentPoseEntry(
            ItemDefinition item,
            ItemVisualProfileDefinition profile,
            VisualAssetDefinition asset,
            GameObject prefab,
            List<RigidEquipmentPoseContext> contexts)
        {
            Item = item;
            Profile = profile;
            Asset = asset;
            Prefab = prefab;
            Contexts = contexts;
        }

        public ItemDefinition Item { get; }
        public ItemVisualProfileDefinition Profile { get; }
        public VisualAssetDefinition Asset { get; }
        public GameObject Prefab { get; }
        public List<RigidEquipmentPoseContext> Contexts { get; }
        public string DisplayName =>
            (Item.display != null && !string.IsNullOrWhiteSpace(Item.display.name) ? Item.display.name : Item.id) +
            " (" + Item.id + ")";
    }

    internal sealed class RigidEquipmentPoseContext
    {
        public RigidEquipmentPoseContext(VisualSocketDefinition socket)
        {
            Socket = socket;
        }

        public VisualSocketDefinition Socket { get; }
        public string DisplayName => Socket.role + " — " + Socket.id;
    }

    internal sealed class RigidEquipmentPosePreview : IDisposable
    {
        private Scene previewScene;
        private Scene previousActiveScene;

        private RigidEquipmentPosePreview(Scene scene, Scene previous, Transform attachmentRoot)
        {
            previewScene = scene;
            previousActiveScene = previous;
            AttachmentRoot = attachmentRoot;
        }

        public Transform AttachmentRoot { get; private set; }
        public bool IsValid => previewScene.IsValid() && previewScene.isLoaded && AttachmentRoot != null;

        public static RigidEquipmentPosePreview Create(
            RigidEquipmentPoseCatalog catalog,
            RigidEquipmentPoseEntry entry,
            RigidEquipmentPoseContext context,
            string humanoidPrefabPath)
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                GameObject rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(humanoidPrefabPath);
                GameObject humanoid = PrefabUtility.InstantiatePrefab(rigPrefab, scene) as GameObject;
                if (humanoid == null)
                    throw new InvalidOperationException("Could not instantiate humanoid_standard preview.");
                humanoid.name = "Humanoid Preview — Rigid Equipment Pose Tuner (DO NOT SAVE)";

                EntityVisualRigRuntime rigRuntime = humanoid.GetComponent<EntityVisualRigRuntime>();
                VisualSocketBinding socketBinding = rigRuntime != null
                    ? rigRuntime.SocketBindings.FirstOrDefault(binding => binding != null && binding.SocketId == context.Socket.id)
                    : null;
                if (socketBinding == null || socketBinding.Target == null)
                    throw new InvalidOperationException("Preview rig has no binding for socket " + context.Socket.id + ".");

                GameObject equipped = PrefabUtility.InstantiatePrefab(entry.Prefab, scene) as GameObject;
                if (equipped == null)
                    throw new InvalidOperationException("Could not instantiate equipped visual " + entry.Asset.asset_key + ".");
                equipped.name = "Equipped Visual — " + entry.Item.id;
                equipped.transform.SetParent(socketBinding.Target, false);
                EquippedVisualPrefabBindings bindings = equipped.GetComponent<EquippedVisualPrefabBindings>();
                if (bindings == null || bindings.AttachmentRoot == null)
                    throw new InvalidOperationException("Equipped visual lost its AttachmentRoot binding.");

                AttachmentPoseValue pose = AttachmentPoseResolver.Resolve(
                    catalog.Database,
                    entry.Profile,
                    catalog.Rig.Profile.id,
                    catalog.Rig.Profile.family_id,
                    context.Socket.id,
                    context.Socket.role);
                ApplyPose(bindings.AttachmentRoot, pose);
                Selection.activeTransform = bindings.AttachmentRoot;
                if (!Application.isBatchMode && SceneView.lastActiveSceneView != null)
                    SceneView.lastActiveSceneView.FrameSelected();
                return new RigidEquipmentPosePreview(scene, previous, bindings.AttachmentRoot);
            }
            catch
            {
                if (previous.IsValid() && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
                throw;
            }
        }

        public void Dispose()
        {
            if (AttachmentRoot != null && Selection.activeTransform != null &&
                Selection.activeTransform.IsChildOf(AttachmentRoot.root))
                Selection.activeObject = null;
            AttachmentRoot = null;
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(previousActiveScene);
            if (previewScene.IsValid() && previewScene.isLoaded)
                EditorSceneManager.CloseScene(previewScene, true);
            previewScene = default;
            previousActiveScene = default;
        }

        private static void ApplyPose(Transform target, AttachmentPoseValue pose)
        {
            target.localPosition = pose.LocalPosition;
            target.localEulerAngles = pose.LocalEulerAngles;
            target.localScale = pose.LocalScale;
        }
    }

    internal static class RigidEquipmentPoseAuthoring
    {
        public static AttachmentPoseDefinition CreateDefinition(
            ItemVisualProfileDefinition profile,
            VisualRigProfileDefinition rig,
            VisualSocketDefinition socket,
            AttachmentPoseDefinition resolved,
            Transform attachmentRoot)
        {
            if (profile == null || rig == null || socket == null || attachmentRoot == null)
                throw new ArgumentNullException("Pose authoring requires profile, rig, socket and AttachmentRoot.");

            string poseId = resolved != null
                ? resolved.id
                : (!string.IsNullOrWhiteSpace(profile.persistent_pose_id)
                    ? profile.persistent_pose_id
                    : M35VisualRigTools.BuildPoseId(profile.id, rig.id, socket.id));
            return new AttachmentPoseDefinition
            {
                type = "attachment_pose",
                id = poseId,
                visual_profile_id = profile.id,
                rig_profile_id = resolved != null ? resolved.rig_profile_id : rig.id,
                rig_family_id = resolved != null ? resolved.rig_family_id : null,
                socket_id = resolved != null ? resolved.socket_id : socket.id,
                socket_role = resolved != null ? resolved.socket_role : socket.role,
                local_position = M35VisualRigTools.ToDefinition(attachmentRoot.localPosition),
                local_rotation = M35VisualRigTools.ToDefinition(
                    resolved != null && resolved.local_rotation != null && Quaternion.Angle(
                        attachmentRoot.localRotation,
                        Quaternion.Euler(resolved.local_rotation.x, resolved.local_rotation.y, resolved.local_rotation.z)) < 0.0001f
                        ? new Vector3(resolved.local_rotation.x, resolved.local_rotation.y, resolved.local_rotation.z)
                        : attachmentRoot.localEulerAngles),
                local_scale = M35VisualRigTools.ToDefinition(attachmentRoot.localScale)
            };
        }
    }

    internal static class AttachmentPoseJsonStore
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public static void Save(string filePath, AttachmentPoseDefinition pose)
        {
            if (string.IsNullOrWhiteSpace(filePath) || pose == null || string.IsNullOrWhiteSpace(pose.id))
                throw new ArgumentException("Attachment pose file path and definition are required.");
            string text = File.ReadAllText(filePath);
            JObject document = JObject.Parse(text);
            JArray poses = document["attachment_poses"] as JArray;
            if (poses == null)
                throw new InvalidOperationException("attachment_poses.json has no attachment_poses array.");
            int duplicates = poses.OfType<JObject>().Count(value => (string)value["id"] == pose.id);
            if (duplicates > 1)
                throw new InvalidOperationException("Duplicate AttachmentPose ID already exists: " + pose.id);

            List<JsonObjectRange> ranges = FindPoseRanges(text, out int arrayOpen, out int arrayClose);
            List<JsonObjectRange> matches = ranges.Where(range => range.Id == pose.id).ToList();
            if (matches.Count > 1)
                throw new InvalidOperationException("Duplicate AttachmentPose text blocks already exist: " + pose.id);

            string newline = text.Contains("\r\n") ? "\r\n" : "\n";
            string formatted = FormatPose(pose, newline);
            string updated;
            if (matches.Count == 1)
            {
                JsonObjectRange range = matches[0];
                updated = text.Substring(0, range.Start) + formatted + text.Substring(range.End);
            }
            else if (ranges.Count > 0)
            {
                JsonObjectRange last = ranges[ranges.Count - 1];
                string between = text.Substring(last.End, arrayClose - last.End);
                updated = text.Substring(0, last.End) + "," + newline + "    " + formatted +
                          between + text.Substring(arrayClose);
            }
            else
            {
                updated = text.Substring(0, arrayOpen + 1) + newline + "    " + formatted + newline + "  " +
                          text.Substring(arrayClose);
            }

            AtomicWrite(filePath, updated);
        }

        public static AttachmentPoseDefinition[] ReadAll(string filePath)
        {
            JObject document = JObject.Parse(File.ReadAllText(filePath));
            JArray poses = document["attachment_poses"] as JArray;
            return poses != null
                ? poses.ToObject<AttachmentPoseDefinition[]>() ?? Array.Empty<AttachmentPoseDefinition>()
                : Array.Empty<AttachmentPoseDefinition>();
        }

        private static List<JsonObjectRange> FindPoseRanges(string text, out int arrayOpen, out int arrayClose)
        {
            int propertyIndex = text.IndexOf("\"attachment_poses\"", StringComparison.Ordinal);
            arrayOpen = propertyIndex >= 0 ? text.IndexOf('[', propertyIndex) : -1;
            if (arrayOpen < 0)
                throw new InvalidOperationException("Could not locate attachment_poses array text.");

            var ranges = new List<JsonObjectRange>();
            bool inString = false;
            bool escaped = false;
            int objectDepth = 0;
            int objectStart = -1;
            int arrayDepth = 1;
            arrayClose = -1;
            for (int index = arrayOpen + 1; index < text.Length; index++)
            {
                char character = text[index];
                if (inString)
                {
                    if (escaped)
                        escaped = false;
                    else if (character == '\\')
                        escaped = true;
                    else if (character == '"')
                        inString = false;
                    continue;
                }
                if (character == '"')
                {
                    inString = true;
                    continue;
                }
                if (character == '[')
                    arrayDepth++;
                else if (character == ']')
                {
                    arrayDepth--;
                    if (arrayDepth == 0)
                    {
                        arrayClose = index;
                        break;
                    }
                }
                else if (character == '{')
                {
                    if (objectDepth++ == 0)
                        objectStart = index;
                }
                else if (character == '}' && objectDepth > 0 && --objectDepth == 0)
                {
                    int end = index + 1;
                    JObject parsed = JObject.Parse(text.Substring(objectStart, end - objectStart));
                    ranges.Add(new JsonObjectRange(objectStart, end, (string)parsed["id"]));
                }
            }
            if (arrayClose < 0)
                throw new InvalidOperationException("Could not locate the end of attachment_poses array text.");
            return ranges;
        }

        private static string FormatPose(AttachmentPoseDefinition pose, string newline)
        {
            var lines = new List<string>
            {
                "{",
                "      \"type\": \"attachment_pose\"",
                "      \"id\": " + JsonConvert.ToString(pose.id),
                "      \"visual_profile_id\": " + JsonConvert.ToString(pose.visual_profile_id)
            };
            AddOptional(lines, "rig_profile_id", pose.rig_profile_id);
            AddOptional(lines, "rig_family_id", pose.rig_family_id);
            AddOptional(lines, "socket_id", pose.socket_id);
            AddOptional(lines, "socket_role", pose.socket_role);
            lines.Add("      \"local_position\": " + FormatVector(pose.local_position));
            lines.Add("      \"local_rotation\": " + FormatVector(pose.local_rotation));
            lines.Add("      \"local_scale\": " + FormatVector(pose.local_scale));
            for (int index = 1; index < lines.Count - 1; index++)
                lines[index] += ",";
            lines.Add("    }");
            return string.Join(newline, lines);
        }

        private static void AddOptional(List<string> lines, string name, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                lines.Add("      \"" + name + "\": " + JsonConvert.ToString(value));
        }

        private static string FormatVector(Float3Definition value)
        {
            if (value == null)
                throw new InvalidOperationException("AttachmentPose Position/Rotation/Scale cannot be null.");
            return "{ \"x\": " + FormatFloat(value.x) + ", \"y\": " + FormatFloat(value.y) +
                   ", \"z\": " + FormatFloat(value.z) + " }";
        }

        private static string FormatFloat(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new InvalidOperationException("AttachmentPose values must be finite.");
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static void AtomicWrite(string filePath, string text)
        {
            string temporaryPath = filePath + ".tmp." + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporaryPath, text, Utf8NoBom);
                File.Replace(temporaryPath, filePath, null);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private readonly struct JsonObjectRange
        {
            public JsonObjectRange(int start, int end, string id)
            {
                Start = start;
                End = end;
                Id = id;
            }

            public int Start { get; }
            public int End { get; }
            public string Id { get; }
        }
    }
}
