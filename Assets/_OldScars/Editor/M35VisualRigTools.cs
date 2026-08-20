using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using OldScars.Core.Actors;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldScars.EditorTools
{
    public static class M35VisualRigTools
    {
        private const string MenuRoot = "Old Scars/Visuals/M35/";
        private const string SurvivalModelPath = "Assets/_OldScars/Art/External/Sketchfab/Survival_PSX/Source/Survival/Models/Survival.fbx";
        private const string CrowbarWorldPrefabPath = "Assets/_OldScars/Art/External/Sketchfab/Crowbar_PSX_LowPoly/Prefabs/Resources/OldScarsVisuals/PFB_VIS_Rusted_Crowbar_PSX.prefab";
        private const string RifleWorldPrefabPath = "Assets/_OldScars/Art/External/Sketchfab/Survival_PSX/Prefabs/Resources/OldScarsVisuals/PFB_VIS_Lee_Enfield_PSX.prefab";
        private const string SurvivalVisualsDirectory = "Assets/_OldScars/Art/External/Sketchfab/Survival_PSX/Prefabs/Resources/OldScarsVisuals";
        private const string BackpackWorldPath = SurvivalVisualsDirectory + "/PFB_VIS_Small_Backpack_World_PSX.prefab";
        private const string BackpackEquippedPath = SurvivalVisualsDirectory + "/PFB_VIS_Small_Backpack_Equipped_PSX.prefab";
        private const string RifleHeldPath = SurvivalVisualsDirectory + "/PFB_VIS_Lee_Enfield_Held_PSX.prefab";
        private const string CrowbarHeldPath = "Assets/_OldScars/Art/External/Sketchfab/Crowbar_PSX_LowPoly/Prefabs/Resources/OldScarsVisuals/PFB_VIS_Rusted_Crowbar_Held_PSX.prefab";
        private const string DebugCargoPrefabPath = "Assets/_OldScars/Debug/Visuals/PFB_DEBUG_CargoRig_M35.prefab";

        private static void ConfigureSelectedHumanRig()
        {
            ConfigureSelectedHumanRig(false);
        }

        private static void ConfigureSelectedHumanDebugRig()
        {
            ConfigureSelectedHumanRig(true);
        }

        private static void ConfigureSelectedHumanRig(bool useDebugSource)
        {
            GameObject actorRoot = Selection.activeGameObject;
            if (actorRoot == null)
            {
                Debug.LogError("[M35VisualRigTools] Select the replaceable actor/prefab instance root first.");
                return;
            }
            if (EditorUtility.IsPersistent(actorRoot))
            {
                Debug.LogError("[M35VisualRigTools] Imported assets are immutable. Select a scene or prefab instance, not PSX_Char_Male_Base.fbx.");
                return;
            }

            Transform spine = FindDescendant(actorRoot.transform, "spine_02");
            Transform handLeft = FindDescendant(actorRoot.transform, "hand_l");
            Transform handRight = FindDescendant(actorRoot.transform, "hand_r");
            if (spine == null || handLeft == null || handRight == null)
            {
                var missingBones = new List<string>();
                if (spine == null)
                    missingBones.Add("spine_02");
                if (handLeft == null)
                    missingBones.Add("hand_l");
                if (handRight == null)
                    missingBones.Add("hand_r");
                Debug.LogError(
                    $"[M35VisualRigTools] Selected human '{actorRoot.name}' is missing required bones: {string.Join(", ", missingBones)}.",
                    actorRoot);
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(useDebugSource
                ? "Configure M35 Human Debug Visual Rig"
                : "Configure M35 Human Visual Rig");
            EntityVisualRigRuntime rig = actorRoot.GetComponent<EntityVisualRigRuntime>();
            if (rig == null)
                rig = Undo.AddComponent<EntityVisualRigRuntime>(actorRoot);

            Transform back = GetOrCreateSocket(spine, "OS_SOCKET_Back");
            Transform waist = GetOrCreateSocket(spine, "OS_SOCKET_Waist");
            Transform sling = GetOrCreateSocket(spine, "OS_SOCKET_Sling");
            Transform left = GetOrCreateSocket(handLeft, "OS_SOCKET_HandLeft");
            Transform right = GetOrCreateSocket(handRight, "OS_SOCKET_HandRight");

            Undo.RecordObject(rig, "Configure visual rig bindings");
            rig.ConfigureBindings(
                "core:human_standard_visual_rig",
                new[]
                {
                    new VisualPartBinding("torso", spine),
                    new VisualPartBinding("arm_left", handLeft),
                    new VisualPartBinding("arm_right", handRight)
                },
                new[]
                {
                    new VisualSocketBinding("human_back_socket", back),
                    new VisualSocketBinding("human_waist_socket", waist),
                    new VisualSocketBinding("human_sling_socket", sling),
                    new VisualSocketBinding("human_hand_left_socket", left),
                    new VisualSocketBinding("human_hand_right_socket", right)
                });

            EntityEquipmentVisualSynchronizer synchronizer = actorRoot.GetComponent<EntityEquipmentVisualSynchronizer>();
            if (synchronizer == null)
                synchronizer = Undo.AddComponent<EntityEquipmentVisualSynchronizer>(actorRoot);
            ActorEquipmentComponent equipment = actorRoot.GetComponent<ActorEquipmentComponent>();
            DebugEquipmentVisualSnapshotSource debugSource = null;
            MonoBehaviour source = equipment;
            if (useDebugSource)
            {
                debugSource = actorRoot.GetComponent<DebugEquipmentVisualSnapshotSource>();
                if (debugSource == null)
                    debugSource = Undo.AddComponent<DebugEquipmentVisualSnapshotSource>(actorRoot);
                source = debugSource;
            }
            Undo.RecordObject(synchronizer, "Configure equipment visual synchronizer");
            synchronizer.Configure(source, rig);

            EditorUtility.SetDirty(rig);
            EditorUtility.SetDirty(synchronizer);
            if (debugSource != null)
                EditorUtility.SetDirty(debugSource);
            Undo.CollapseUndoOperations(undoGroup);
            if (!useDebugSource && equipment == null)
            {
                Debug.LogWarning(
                    "[M35VisualRigTools] Rig configured, but the selected root has no ActorEquipmentComponent. " +
                    "The same synchronizer is ready and will require an IEquipmentVisualSource before Play Mode.",
                    actorRoot);
            }
            Debug.Log(
                useDebugSource
                    ? "[M35VisualRigTools] Human debug visual rig configured with snapshot presets and Undo. The scene was not saved."
                    : "[M35VisualRigTools] Human visual rig configured with Undo. The scene was not saved.",
                actorRoot);
        }

        private static void GenerateM35VisualPrefabs()
        {
            EnsureAssetFolder(SurvivalVisualsDirectory);
            GenerateBackpackWorld();
            GenerateBackpackEquipped();
            GenerateEquippedWrapper(
                CrowbarWorldPrefabPath,
                CrowbarHeldPath,
                "PFB_VIS_Rusted_Crowbar_Held_PSX",
                "core:rusted_crowbar_visual",
                true,
                false);
            GenerateEquippedWrapper(
                RifleWorldPrefabPath,
                RifleHeldPath,
                "PFB_VIS_Lee_Enfield_Held_PSX",
                "core:lee_enfield_visual",
                true,
                true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[M35VisualRigTools] Generated backpack world/equipped and held crowbar/rifle visual prefabs.");
        }

        private static void GenerateDebugCargoRig()
        {
            EnsureAssetFolder("Assets/_OldScars/Debug/Visuals");
            Scene previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("PFB_DEBUG_CargoRig_M35");
                SceneManager.MoveGameObjectToScene(root, previewScene);
                var cargoBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cargoBody.name = "cargo_body";
                cargoBody.transform.SetParent(root.transform, false);
                cargoBody.transform.localScale = new Vector3(1.2f, 0.6f, 0.8f);
                DestroyImmediateIfPresent(cargoBody.GetComponent<Collider>());

                var cargoMountObject = new GameObject("cargo_mount");
                cargoMountObject.transform.SetParent(cargoBody.transform, false);
                cargoMountObject.transform.localPosition = new Vector3(0f, 0.5f, 0f);

                EntityVisualRigRuntime rig = root.AddComponent<EntityVisualRigRuntime>();
                rig.ConfigureBindings(
                    "core:debug_cargo_visual_rig",
                    new[] { new VisualPartBinding("cargo_body", cargoBody.transform) },
                    new[] { new VisualSocketBinding("cargo_mount", cargoMountObject.transform) });
                DebugEquipmentVisualSnapshotSource source = root.AddComponent<DebugEquipmentVisualSnapshotSource>();
                EntityEquipmentVisualSynchronizer synchronizer = root.AddComponent<EntityEquipmentVisualSynchronizer>();
                synchronizer.Configure(source, rig);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, DebugCargoPrefabPath);
                if (saved == null)
                    throw new InvalidOperationException("Could not save Debug Cargo Rig prefab.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[M35VisualRigTools] Generated Debug Cargo Rig with the universal synchronizer. No scene was modified.");
        }

        private static void ValidateSelectedRig()
        {
            EntityVisualRigRuntime rig = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<EntityVisualRigRuntime>()
                : null;
            if (rig == null)
            {
                Debug.LogError("[M35VisualRigTools] Select a root with EntityVisualRigRuntime.");
                return;
            }

            var ids = new HashSet<string>();
            foreach (VisualPartBinding binding in rig.PartBindings)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.PartId) || binding.Target == null || !ids.Add("part:" + binding.PartId))
                {
                    Debug.LogError("[M35VisualRigTools] Missing or duplicate visual part Transform binding.", rig);
                    return;
                }
            }
            foreach (VisualSocketBinding binding in rig.SocketBindings)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.SocketId) || binding.Target == null || !ids.Add("socket:" + binding.SocketId))
                {
                    Debug.LogError("[M35VisualRigTools] Missing or duplicate visual socket Transform binding.", rig);
                    return;
                }
            }
            if (Application.isPlaying && !rig.RebuildBindings(out string reason))
            {
                Debug.LogError("[M35VisualRigTools] Rig validation failed: " + reason, rig);
                return;
            }
            Debug.Log("[M35VisualRigTools] Selected rig bindings are structurally valid.", rig);
        }

        private static void ValidateGeneratedVisualPrefabs()
        {
            string[] equippedPaths = { BackpackEquippedPath, CrowbarHeldPath, RifleHeldPath };
            for (int index = 0; index < equippedPaths.Length; index++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(equippedPaths[index]);
                if (!EquippedVisualPrefabContract.TryValidate(prefab, out string error))
                {
                    Debug.LogError($"[M35VisualRigTools] {equippedPaths[index]}: {error}");
                    return;
                }
            }
            GameObject backpackWorld = AssetDatabase.LoadAssetAtPath<GameObject>(BackpackWorldPath);
            if (backpackWorld == null || backpackWorld.GetComponentInChildren<Renderer>(true) == null)
            {
                Debug.LogError("[M35VisualRigTools] Backpack world visual is missing or has no Renderer.");
                return;
            }
            Debug.Log("[M35VisualRigTools] Generated M35 visual prefabs satisfy their visual-only contracts.");
        }

        private static void CopySelectedAttachmentPoseJson()
        {
            Transform selected = Selection.activeTransform;
            EquippedVisualInstanceMarker marker = selected != null
                ? selected.GetComponentInParent<EquippedVisualInstanceMarker>()
                : null;
            EquippedVisualPrefabBindings bindings = marker != null
                ? marker.GetComponent<EquippedVisualPrefabBindings>()
                : null;
            if (marker == null || bindings == null || bindings.AttachmentRoot == null)
            {
                Debug.LogError("[M35VisualRigTools] Select a runtime equipped visual generated by EntityEquipmentVisualSynchronizer.");
                return;
            }

            Transform poseTransform = bindings.AttachmentRoot;
            var pose = new AttachmentPoseDefinition
            {
                type = "attachment_pose",
                id = BuildPoseId(marker.VisualProfileId, marker.RigProfileId, marker.SocketId),
                visual_profile_id = marker.VisualProfileId,
                rig_profile_id = marker.RigProfileId,
                socket_id = marker.SocketId,
                local_position = ToDefinition(poseTransform.localPosition),
                local_rotation = ToDefinition(poseTransform.localEulerAngles),
                local_scale = ToDefinition(poseTransform.localScale)
            };
            string json = JsonConvert.SerializeObject(pose, Formatting.Indented, new JsonSerializerSettings
            {
                Culture = CultureInfo.InvariantCulture,
                NullValueHandling = NullValueHandling.Ignore
            });
            EditorGUIUtility.systemCopyBuffer = json;
            Debug.Log("[M35VisualRigTools] AttachmentPoseDefinition copied to clipboard:\n" + json, poseTransform);
        }

        private static void GenerateBackpackWorld()
        {
            GenerateFromModelChild(
                SurvivalModelPath,
                "Backpack.001",
                BackpackWorldPath,
                "PFB_VIS_Small_Backpack_World_PSX",
                null,
                0.8f);
        }

        private static void GenerateBackpackEquipped()
        {
            GenerateFromModelChild(
                SurvivalModelPath,
                "Backpack",
                BackpackEquippedPath,
                "PFB_VIS_Small_Backpack_Equipped_PSX",
                "core:small_backpack_visual",
                0.8f);
        }

        private static void GenerateFromModelChild(
            string sourcePath,
            string sourceChildName,
            string outputPath,
            string prefabName,
            string visualProfileId,
            float targetMaxDimension)
        {
            GameObject sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (sourceAsset == null)
                throw new InvalidOperationException("Source model was not found: " + sourcePath);

            Scene previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject sourceInstance = PrefabUtility.InstantiatePrefab(sourceAsset, previewScene) as GameObject;
                Transform sourceChild = sourceInstance != null ? FindDescendant(sourceInstance.transform, sourceChildName) : null;
                if (sourceChild == null || sourceChild.GetComponentInChildren<Renderer>(true) == null)
                    throw new InvalidOperationException($"Source mesh '{sourceChildName}' was not found in {sourcePath}.");

                var root = new GameObject(prefabName);
                SceneManager.MoveGameObjectToScene(root, previewScene);
                Transform parent = root.transform;
                if (!string.IsNullOrWhiteSpace(visualProfileId))
                {
                    var attachment = new GameObject("AttachmentRoot");
                    attachment.transform.SetParent(root.transform, false);
                    parent = attachment.transform;
                    EquippedVisualPrefabBindings bindings = root.AddComponent<EquippedVisualPrefabBindings>();
                    bindings.Configure(visualProfileId, attachment.transform);
                }

                GameObject model = UnityEngine.Object.Instantiate(sourceChild.gameObject, parent, false);
                model.name = "Model";
                StripToVisual(model);
                NormalizeVisual(root.transform, model.transform, targetMaxDimension);
                SavePrefab(root, outputPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static void GenerateEquippedWrapper(
            string sourcePrefabPath,
            string outputPath,
            string prefabName,
            string visualProfileId,
            bool primaryGrip,
            bool secondaryGrip)
        {
            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPath);
            if (sourcePrefab == null)
                throw new InvalidOperationException("Source visual prefab was not found: " + sourcePrefabPath);

            Scene previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject(prefabName);
                SceneManager.MoveGameObjectToScene(root, previewScene);
                var attachment = new GameObject("AttachmentRoot");
                attachment.transform.SetParent(root.transform, false);
                GameObject model = PrefabUtility.InstantiatePrefab(sourcePrefab, previewScene) as GameObject;
                if (model == null)
                    throw new InvalidOperationException("Could not instantiate source visual: " + sourcePrefabPath);
                model.name = "Model";
                UnpackIfNeeded(model);
                model.transform.SetParent(attachment.transform, false);
                StripToVisual(model);

                Transform gripPrimary = primaryGrip ? CreateMarker(root.transform, "Grip_Primary") : null;
                Transform gripSecondary = secondaryGrip ? CreateMarker(root.transform, "Grip_Secondary") : null;
                EquippedVisualPrefabBindings bindings = root.AddComponent<EquippedVisualPrefabBindings>();
                bindings.Configure(visualProfileId, attachment.transform, gripPrimary, gripSecondary);
                SavePrefab(root, outputPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static void SavePrefab(GameObject root, string outputPath)
        {
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, outputPath);
            if (saved == null)
                throw new InvalidOperationException("Could not save prefab: " + outputPath);
        }

        private static Transform GetOrCreateSocket(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing;
            var socket = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(socket, "Create visual socket " + name);
            Undo.SetTransformParent(socket.transform, parent, "Parent visual socket " + name);
            socket.transform.localPosition = Vector3.zero;
            socket.transform.localRotation = Quaternion.identity;
            socket.transform.localScale = Vector3.one;
            return socket.transform;
        }

        private static Transform CreateMarker(Transform parent, string name)
        {
            var marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            return marker.transform;
        }

        private static void NormalizeVisual(Transform root, Transform model, float targetMaxDimension)
        {
            if (!TryGetCombinedBounds(root, out Bounds bounds))
                throw new InvalidOperationException("Generated visual has no Renderer bounds.");
            float maxDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxDimension <= 0.0001f)
                throw new InvalidOperationException("Generated visual has invalid bounds.");
            model.localScale *= targetMaxDimension / maxDimension;
            if (!TryGetCombinedBounds(root, out bounds))
                return;
            model.position += new Vector3(-bounds.center.x, -bounds.center.y, -bounds.center.z);
        }

        private static bool TryGetCombinedBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool found = false;
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
                    continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return found;
        }

        private static void StripToVisual(GameObject root)
        {
            DestroyComponents<Collider>(root);
            DestroyComponents<Rigidbody>(root);
            DestroyComponents<Joint>(root);
            DestroyComponents<CharacterController>(root);
            DestroyComponents<MonoBehaviour>(root);
            DestroyComponents<Animator>(root);
        }

        private static void DestroyComponents<T>(GameObject root) where T : Component
        {
            T[] components = root.GetComponentsInChildren<T>(true);
            for (int index = 0; index < components.Length; index++)
                UnityEngine.Object.DestroyImmediate(components[index]);
        }

        private static void DestroyImmediateIfPresent(UnityEngine.Object target)
        {
            if (target != null)
                UnityEngine.Object.DestroyImmediate(target);
        }

        private static void UnpackIfNeeded(GameObject instance)
        {
            GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(instance);
            if (prefabRoot != null)
            {
                PrefabUtility.UnpackPrefabInstance(
                    prefabRoot,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
            }
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null)
                return null;
            if (root.name == name)
                return root;
            for (int index = 0; index < root.childCount; index++)
            {
                Transform found = FindDescendant(root.GetChild(index), name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static void EnsureAssetFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }

        private static string BuildPoseId(string visualProfileId, string rigProfileId, string socketId)
        {
            if (!ContentId.TryParse(visualProfileId, out ContentId visual, out string visualError))
                throw new InvalidOperationException($"Invalid visual profile Global Content ID '{visualProfileId}': {visualError}.");
            if (!ContentId.TryParse(rigProfileId, out ContentId rig, out string rigError))
                throw new InvalidOperationException($"Invalid rig profile Global Content ID '{rigProfileId}': {rigError}.");
            string rigToken = visual.Namespace == rig.Namespace
                ? rig.LocalId
                : rig.Namespace + "_" + rig.LocalId;
            string raw = visual.Namespace + ":" + string.Join("_", visual.LocalId, rigToken, socketId, "pose");
            if (!ContentId.TryParse(raw, out ContentId pose, out string poseError))
                throw new InvalidOperationException($"Generated attachment pose ID '{raw}' is invalid: {poseError}.");
            return pose.Canonical;
        }

        private static Float3Definition ToDefinition(Vector3 value)
        {
            return new Float3Definition { x = value.x, y = value.y, z = value.z };
        }
    }
}
