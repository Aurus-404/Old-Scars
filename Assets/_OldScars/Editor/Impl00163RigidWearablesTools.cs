using System;
using System.Collections.Generic;
using System.Linq;
using OldScars.Core.Interactions;
using OldScars.Core.Items;
using OldScars.Core.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldScars.EditorTools
{
    public static class Impl00163RigidWearablesTools
    {
        private const string VisualDirectory = "Assets/_OldScars/Resources/OldScarsVisuals";
        private const string MaterialDirectory = "Assets/_OldScars/Art/External/Sketchfab/RigidWearables/Materials";

        private static readonly WearableSpec[] Specs =
        {
            new WearableSpec(
                "Canvas_Belt", "core:canvas_belt_visual", "canvas_belt_01", "Cinturón de lona",
                "Equipo de cintura básico.",
                "Assets/_OldScars/Art/External/Sketchfab/Belt/Source/Belt.fbx", 0.62f, 0.2f,
                "PFB_VIS_Canvas_Belt_World", "PFB_VIS_Canvas_Belt_Equipped"),
            new WearableSpec(
                "Steel_Helmet", "core:steel_helmet_visual", "steel_helmet_01", "Casco de acero",
                "Protección básica de cabeza para pruebas de cobertura regional.",
                "Assets/_OldScars/Art/External/Sketchfab/M36_Stahlhelm/Source/M36_Stahlhelm.fbx", 0.34f, 1.35f,
                "PFB_VIS_Steel_Helmet_World", "PFB_VIS_Steel_Helmet_Equipped"),
            new WearableSpec(
                "Work_Goggles", "core:work_goggles_visual", "work_goggles_01", "Goggles de trabajo",
                "Goggles sencillos para tareas de polvo y escombros.",
                "Assets/_OldScars/Art/External/Sketchfab/Goggles/Source/source/SM_Goggles.fbx", 0.22f, 0.18f,
                "PFB_VIS_Work_Goggles_World", "PFB_VIS_Work_Goggles_Equipped")
        };

        [MenuItem("Old Scars/Visuals/IMPL-0016.3/Generate Rigid Wearables")]
        public static void GenerateAll()
        {
            EnsureFolder(VisualDirectory);
            EnsureFolder(MaterialDirectory);
            ConfigureImporters();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Material belt = CreateOrUpdateMaterial(
                "MAT_Belt_URP", new Color(0.42f, 0.30f, 0.16f),
                "Assets/_OldScars/Art/External/Sketchfab/Belt/Source/0/Material_baseColor.jpg",
                "Assets/_OldScars/Art/External/Sketchfab/Belt/Source/0/Material_normal.jpg",
                "Assets/_OldScars/Art/External/Sketchfab/Belt/Source/0/Material_metallicRoughness_metal.jpg",
                0.15f, 0.25f);
            Material helmet = CreateOrUpdateMaterial("MAT_M36_Helmet_URP", new Color(0.16f, 0.18f, 0.14f), null, null, null, 0.15f, 0.38f);
            Material helmetTrim = CreateOrUpdateMaterial("MAT_M36_Trim_URP", new Color(0.09f, 0.065f, 0.04f), null, null, null, 0f, 0.28f);
            Material goggles = CreateOrUpdateMaterial(
                "MAT_Goggles_URP", Color.white,
                "Assets/_OldScars/Art/External/Sketchfab/Goggles/Source/textures/Goggles_Diff.png",
                null, null, 0.05f, 0.42f);
            goggles.SetFloat("_WorkflowMode", 0f);
            goggles.SetTexture("_SpecGlossMap", LoadTexture(
                "Assets/_OldScars/Art/External/Sketchfab/Goggles/Source/textures/Goggles_Spec.png"));
            goggles.EnableKeyword("_SPECULAR_SETUP");
            goggles.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(goggles);

            for (int index = 0; index < Specs.Length; index++)
            {
                WearableSpec spec = Specs[index];
                Material[] materials = spec.Token == "Canvas_Belt"
                    ? new[] { belt }
                    : spec.Token == "Steel_Helmet"
                        ? new[] { helmet, helmetTrim }
                        : new[] { goggles };
                GenerateVisual(spec, false, materials);
                GenerateVisual(spec, true, materials);
                GenerateWorldItem(spec);
            }

            M41HumanDebugActorTools.GenerateHumanDebugActorRepresentation();
            UpgradeAuthoredHumanRigConsumers();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateGeneratedAssets();
            Debug.Log("[IMPL-0016.3] Generated Belt, M36 Stahlhelm and Goggles world/equipped visuals, world items and humanoid head/eyes sockets.");
        }

        public static void RunBatch()
        {
            try
            {
                GenerateAll();
                Debug.Log("[IMPL-0016.3] GENERATION: PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("[IMPL-0016.3] GENERATION: FAIL\n" + exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ConfigureImporters()
        {
            for (int index = 0; index < Specs.Length; index++)
            {
                AssetDatabase.ImportAsset(Specs[index].SourcePath, ImportAssetOptions.ForceSynchronousImport);
                if (AssetImporter.GetAtPath(Specs[index].SourcePath) is ModelImporter importer)
                {
                    importer.globalScale = 1f;
                    importer.importAnimation = false;
                    importer.importBlendShapes = false;
                    importer.importCameras = false;
                    importer.importLights = false;
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
            }

            ConfigureTexture("Assets/_OldScars/Art/External/Sketchfab/Belt/Source/0/Material_baseColor.jpg", true, TextureImporterType.Default);
            ConfigureTexture("Assets/_OldScars/Art/External/Sketchfab/Belt/Source/0/Material_normal.jpg", false, TextureImporterType.NormalMap);
            ConfigureTexture("Assets/_OldScars/Art/External/Sketchfab/Belt/Source/0/Material_metallicRoughness_metal.jpg", false, TextureImporterType.Default);
            ConfigureTexture("Assets/_OldScars/Art/External/Sketchfab/Belt/Source/0/Material_metallicRoughness_rough.jpg", false, TextureImporterType.Default);
            ConfigureTexture("Assets/_OldScars/Art/External/Sketchfab/Goggles/Source/textures/Goggles_Diff.png", true, TextureImporterType.Default);
            ConfigureTexture("Assets/_OldScars/Art/External/Sketchfab/Goggles/Source/textures/Goggles_Spec.png", false, TextureImporterType.Default);
        }

        private static void ConfigureTexture(string path, bool sRgb, TextureImporterType type)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                return;
            importer.textureType = type;
            importer.sRGBTexture = sRgb;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Point;
            importer.anisoLevel = 1;
            importer.SaveAndReimport();
        }

        private static Material CreateOrUpdateMaterial(
            string name, Color color, string albedoPath, string normalPath, string metallicPath, float metallic, float smoothness)
        {
            string path = MaterialDirectory + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("URP Lit shader was not found.");
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.SetTexture("_BaseMap", LoadTexture(albedoPath));
            Texture2D normal = LoadTexture(normalPath);
            material.SetTexture("_BumpMap", normal);
            if (normal != null)
                material.EnableKeyword("_NORMALMAP");
            Texture2D metallicMap = LoadTexture(metallicPath);
            material.SetTexture("_MetallicGlossMap", metallicMap);
            if (metallicMap != null)
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D LoadTexture(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void GenerateVisual(WearableSpec spec, bool equipped, Material[] materials)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(spec.SourcePath);
            if (source == null)
                throw new InvalidOperationException("Source model was not imported: " + spec.SourcePath);

            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                string prefabName = equipped ? spec.EquippedPrefabName : spec.WorldPrefabName;
                var root = new GameObject(prefabName);
                SceneManager.MoveGameObjectToScene(root, scene);
                Transform parent = root.transform;
                if (equipped)
                {
                    var attachment = new GameObject("AttachmentRoot");
                    attachment.transform.SetParent(root.transform, false);
                    parent = attachment.transform;
                    root.AddComponent<EquippedVisualPrefabBindings>().Configure(spec.VisualProfileId, attachment.transform);
                }

                GameObject model = PrefabUtility.InstantiatePrefab(source, scene) as GameObject;
                if (model == null)
                    throw new InvalidOperationException("Could not instantiate source model: " + spec.SourcePath);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.name = "Model";
                model.transform.SetParent(parent, false);
                StripToVisual(model);
                ApplyMaterials(model, materials);
                Normalize(root.transform, model.transform, spec.TargetMaxDimension, !equipped);

                string output = VisualDirectory + "/" + prefabName + ".prefab";
                if (PrefabUtility.SaveAsPrefabAsset(root, output) == null)
                    throw new InvalidOperationException("Could not save visual prefab: " + output);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void ApplyMaterials(GameObject root, Material[] materials)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                Material selected = materials[0];
                if (materials.Length > 1)
                {
                    string token = (renderer.name + " " + (renderer.sharedMaterial != null ? renderer.sharedMaterial.name : string.Empty)).ToLowerInvariant();
                    selected = token.Contains("strap") || token.Contains("material") ? materials[1] : materials[0];
                }
                int count = Math.Max(1, renderer.sharedMaterials.Length);
                var assigned = new Material[count];
                for (int materialIndex = 0; materialIndex < count; materialIndex++)
                    assigned[materialIndex] = selected;
                renderer.sharedMaterials = assigned;
            }
        }

        private static void Normalize(Transform root, Transform model, float targetMaxDimension, bool ground)
        {
            if (!TryGetBounds(root, out Bounds bounds))
                throw new InvalidOperationException("Generated visual has no renderer bounds.");
            float maxDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxDimension <= 0.0001f)
                throw new InvalidOperationException("Generated visual has invalid bounds.");
            model.localScale *= targetMaxDimension / maxDimension;
            if (!TryGetBounds(root, out bounds))
                throw new InvalidOperationException("Normalized visual has no renderer bounds.");
            Vector3 offset = -bounds.center;
            if (ground)
                offset.y = -bounds.min.y + 0.015f;
            model.position += offset;
        }

        private static void GenerateWorldItem(WearableSpec spec)
        {
            GameObject visual = AssetDatabase.LoadAssetAtPath<GameObject>(VisualDirectory + "/" + spec.WorldPrefabName + ".prefab");
            if (visual == null)
                throw new InvalidOperationException("World visual was not generated for " + spec.ItemDefinitionId);
            string path = "Assets/_OldScars/Resources/PFB_WorldItem_" + spec.ItemDefinitionId + ".prefab";
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("PFB_WorldItem_" + spec.ItemDefinitionId) { layer = 6 };
                SceneManager.MoveGameObjectToScene(root, scene);
                BoxCollider collider = root.AddComponent<BoxCollider>();
                Rigidbody body = root.AddComponent<Rigidbody>();
                body.mass = spec.Mass;
                WorldItemPickup pickup = root.AddComponent<WorldItemPickup>();
                var pickupSerialized = new SerializedObject(pickup);
                pickupSerialized.FindProperty("itemDefinitionId").stringValue = spec.ItemDefinitionId;
                pickupSerialized.ApplyModifiedPropertiesWithoutUndo();
                root.AddComponent<WorldObjectTags>().ApplyInitialTags(new[] { "world_item", "pickupable", "inspectable" });
                WorldObjectDebugInfo info = root.AddComponent<WorldObjectDebugInfo>();
                info.SetRuntimeDisplayName(spec.DisplayName);
                info.SetRuntimeInspectText(spec.InspectText);

                GameObject visualInstance = PrefabUtility.InstantiatePrefab(visual, scene) as GameObject;
                if (visualInstance == null)
                    throw new InvalidOperationException("Could not instantiate world visual for " + spec.ItemDefinitionId);
                visualInstance.name = "Visual";
                visualInstance.transform.SetParent(root.transform, false);
                if (!TryGetBounds(root.transform, out Bounds bounds))
                    throw new InvalidOperationException("World item visual has no bounds: " + spec.ItemDefinitionId);
                collider.center = root.transform.InverseTransformPoint(bounds.center);
                collider.size = bounds.size + Vector3.one * 0.02f;

                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                    throw new InvalidOperationException("Could not save world item prefab: " + path);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void ValidateGeneratedAssets()
        {
            for (int index = 0; index < Specs.Length; index++)
            {
                WearableSpec spec = Specs[index];
                GameObject world = AssetDatabase.LoadAssetAtPath<GameObject>(VisualDirectory + "/" + spec.WorldPrefabName + ".prefab");
                GameObject equipped = AssetDatabase.LoadAssetAtPath<GameObject>(VisualDirectory + "/" + spec.EquippedPrefabName + ".prefab");
                GameObject pickup = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_OldScars/Resources/PFB_WorldItem_" + spec.ItemDefinitionId + ".prefab");
                if (world == null || world.GetComponentInChildren<Renderer>(true) == null)
                    throw new InvalidOperationException(spec.Token + " world visual is invalid.");
                if (!EquippedVisualPrefabContract.TryValidate(equipped, out string error))
                    throw new InvalidOperationException(spec.Token + " equipped visual is invalid: " + error);
                if (equipped.GetComponentsInChildren<Renderer>(true).Length == 0)
                    throw new InvalidOperationException(spec.Token + " equipped visual has no renderer.");
                if (pickup == null || pickup.GetComponent<WorldItemPickup>() == null || pickup.GetComponent<BoxCollider>() == null ||
                    pickup.GetComponent<Rigidbody>() == null || pickup.GetComponent<WorldObjectTags>() == null || pickup.GetComponent<WorldObjectDebugInfo>() == null)
                    throw new InvalidOperationException(spec.Token + " world item gameplay contract is invalid.");
            }
        }

        private static void UpgradeAuthoredHumanRigConsumers()
        {
            const string playerPrefabPath = "Assets/_OldScars/Resources/PFB_PlayerGameplayComposition.prefab";
            GameObject playerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
            if (RequiresRigUpgrade(playerAsset))
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(playerPrefabPath);
                try
                {
                    UpgradeRigBindings(contents);
                    PrefabUtility.SaveAsPrefabAsset(contents, playerPrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            const string scenePath = "Assets/Scenes/SampleScene.unity";
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            bool sceneChanged = false;
            foreach (GameObject root in scene.GetRootGameObjects())
                sceneChanged |= UpgradeRigBindings(root);
            if (sceneChanged)
                EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static bool RequiresRigUpgrade(GameObject root)
        {
            if (root == null)
                return false;
            EntityVisualRigRuntime[] rigs = root.GetComponentsInChildren<EntityVisualRigRuntime>(true);
            for (int index = 0; index < rigs.Length; index++)
            {
                string profile = new SerializedObject(rigs[index]).FindProperty("visualRigProfileId").stringValue;
                if ((profile == "human_standard_visual_rig" || profile == "core:human_standard_visual_rig") &&
                    (!rigs[index].SocketBindings.Any(binding => binding.SocketId == "human_head_socket") ||
                     !rigs[index].SocketBindings.Any(binding => binding.SocketId == "human_eyes_socket")))
                    return true;
            }
            return false;
        }

        private static bool UpgradeRigBindings(GameObject root)
        {
            bool changed = false;
            EntityVisualRigRuntime[] rigs = root.GetComponentsInChildren<EntityVisualRigRuntime>(true);
            for (int index = 0; index < rigs.Length; index++)
            {
                EntityVisualRigRuntime rig = rigs[index];
                string configuredProfile = new SerializedObject(rig).FindProperty("visualRigProfileId").stringValue;
                if (configuredProfile != "human_standard_visual_rig" && configuredProfile != "core:human_standard_visual_rig")
                    continue;

                var sockets = new List<VisualSocketBinding>(rig.SocketBindings);
                if (sockets.Exists(binding => binding.SocketId == "human_head_socket") &&
                    sockets.Exists(binding => binding.SocketId == "human_eyes_socket"))
                    continue;

                Transform headBone = FindDescendant(rig.transform, "head");
                Transform torso = null;
                foreach (VisualPartBinding part in rig.PartBindings)
                {
                    if (part != null && part.PartId == "torso")
                    {
                        torso = part.Target;
                        break;
                    }
                }
                Transform anchor = headBone != null ? headBone : torso;
                if (anchor == null)
                    throw new InvalidOperationException("Human rig '" + rig.name + "' has no head or torso anchor.");

                Transform headSocket = GetOrCreateAuthoredSocket(anchor, "OS_SOCKET_Head",
                    headBone != null ? Vector3.zero : new Vector3(0f, 0.55f, 0f));
                Transform eyesSocket = GetOrCreateAuthoredSocket(anchor, "OS_SOCKET_Eyes",
                    headBone != null ? Vector3.zero : new Vector3(0f, 0.50f, 0.12f));
                sockets.RemoveAll(binding => binding.SocketId == "human_head_socket" || binding.SocketId == "human_eyes_socket");
                sockets.Add(new VisualSocketBinding("human_head_socket", headSocket));
                sockets.Add(new VisualSocketBinding("human_eyes_socket", eyesSocket));
                var parts = new List<VisualPartBinding>(rig.PartBindings);
                rig.ConfigureBindings(configuredProfile, parts.ToArray(), sockets.ToArray());
                EditorUtility.SetDirty(rig);
                changed = true;
            }
            return changed;
        }

        private static Transform GetOrCreateAuthoredSocket(Transform parent, string name, Vector3 localPosition)
        {
            Transform socket = parent.Find(name);
            if (socket == null)
            {
                socket = new GameObject(name).transform;
                socket.SetParent(parent, false);
            }
            socket.localPosition = localPosition;
            socket.localRotation = Quaternion.identity;
            socket.localScale = Vector3.one;
            return socket;
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

        private static bool TryGetBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool found = false;
            for (int index = 0; index < renderers.Length; index++)
            {
                if (!found) { bounds = renderers[index].bounds; found = true; }
                else bounds.Encapsulate(renderers[index].bounds);
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
                UnityEngine.Object.DestroyImmediate(components[index], true);
        }

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }

        private sealed class WearableSpec
        {
            public WearableSpec(string token, string visualProfileId, string itemDefinitionId, string displayName,
                string inspectText, string sourcePath, float targetMaxDimension, float mass,
                string worldPrefabName, string equippedPrefabName)
            {
                Token = token; VisualProfileId = visualProfileId; ItemDefinitionId = itemDefinitionId;
                DisplayName = displayName; InspectText = inspectText; SourcePath = sourcePath;
                TargetMaxDimension = targetMaxDimension; Mass = mass;
                WorldPrefabName = worldPrefabName; EquippedPrefabName = equippedPrefabName;
            }

            public string Token { get; }
            public string VisualProfileId { get; }
            public string ItemDefinitionId { get; }
            public string DisplayName { get; }
            public string InspectText { get; }
            public string SourcePath { get; }
            public float TargetMaxDimension { get; }
            public float Mass { get; }
            public string WorldPrefabName { get; }
            public string EquippedPrefabName { get; }
        }
    }
}
