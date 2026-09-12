using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldScars.EditorTools
{
    public static class GeneratePsxVisualPrefabs
    {
        private const string MenuPath = "Old Scars/Visuals/Generate PSX Visual Prefabs";
        private const string Impl00162MenuPath = "Old Scars/Visuals/Generate IMPL-0016.2 World Visual Prefabs";
        private const string CrowbarRoot = "Assets/_OldScars/Art/External/Sketchfab/Crowbar_V1";
        private const string RifleRoot = "Assets/_OldScars/Art/External/Sketchfab/Retro_PS2_Bolt_Action_Rifle";
        private const string BandageRoot = "Assets/_OldScars/Art/External/Sketchfab/Bandage";
        private const string CanteenRoot = "Assets/_OldScars/Art/External/Sketchfab/Canteen";
        private const string CannedFoodRoot = "Assets/_OldScars/Art/External/Sketchfab/Canned_Food_PSX";
        private const string CrowbarMaterialPath = CrowbarRoot + "/Materials/MAT_Crowbar_V1.mat";
        private const string RifleMaterialPath = RifleRoot + "/Materials/MAT_Retro_Bolt_Action_Rifle.mat";
        private const string CrowbarMaskPath = CrowbarRoot + "/Derived/T_Crowbar_V1_MetallicSmoothness.png";
        private const string RifleMaskPath = RifleRoot + "/Derived/T_Retro_Bolt_Action_Rifle_MetallicSmoothness.png";

        private static readonly WorldPlaceholderSpec[] Impl00162Specs =
        {
            new WorldPlaceholderSpec(
                "PFB_VIS_Bandage_World",
                BandageRoot + "/Source/source/Bandage.fbx",
                BandageRoot + "/Prefabs/Resources/OldScarsVisuals/PFB_VIS_Bandage_World.prefab",
                BandageRoot + "/Materials/MAT_Bandage.mat",
                0.18f,
                0.01f,
                null),
            new WorldPlaceholderSpec(
                "PFB_VIS_Canteen_World",
                CanteenRoot + "/Source/source/Canteen.fbx",
                CanteenRoot + "/Prefabs/Resources/OldScarsVisuals/PFB_VIS_Canteen_World.prefab",
                CanteenRoot + "/Materials/MAT_Canteen.mat",
                0.28f,
                0.01f,
                null),
            new WorldPlaceholderSpec(
                "PFB_VIS_Canned_Food_PSX_World",
                CannedFoodRoot + "/Source/source/Canned Food.fbx",
                CannedFoodRoot + "/Prefabs/Resources/OldScarsVisuals/PFB_VIS_Canned_Food_PSX_World.prefab",
                CannedFoodRoot + "/Materials/MAT_Canned_Food_PSX.mat",
                0.14f,
                0.01f,
                "Can 1")
        };

        private static readonly VisualPrefabSpec[] Specs =
        {
            new VisualPrefabSpec(
                "PFB_VIS_Rusted_Crowbar_V1_World",
                CrowbarRoot + "/Source/source/Crowbar.fbx",
                "Crowbar",
                CrowbarRoot + "/Prefabs/Resources/OldScarsVisuals/PFB_VIS_Rusted_Crowbar_V1_World.prefab",
                CrowbarMaterialPath,
                0.85f,
                0.03f,
                new Vector3(0f, 0f, 90f)),
            new VisualPrefabSpec(
                "PFB_VIS_Lee_Enfield_RetroBolt_PS2_World",
                RifleRoot + "/Source/source/Junk Bolt action rifle.obj",
                "default",
                RifleRoot + "/Prefabs/Resources/OldScarsVisuals/PFB_VIS_Lee_Enfield_RetroBolt_PS2_World.prefab",
                RifleMaterialPath,
                1.20f,
                0.03f,
                Vector3.zero),
            new VisualPrefabSpec(
                "PFB_VIS_Ammo_303_PSX",
                "Assets/_OldScars/Art/External/Sketchfab/Survival_PSX/Source/Survival/Models/Survival.fbx",
                "Ammunition",
                "Assets/_OldScars/Art/External/Sketchfab/Survival_PSX/Prefabs/Resources/OldScarsVisuals/PFB_VIS_Ammo_303_PSX.prefab",
                null,
                0.30f,
                0.05f,
                Vector3.zero),
            new VisualPrefabSpec(
                "PFB_VIS_Crate_PSX",
                "Assets/_OldScars/Art/External/Sketchfab/PSX_Industrial_Pack/Source/industrial.fbx",
                "Crate",
                "Assets/_OldScars/Art/External/Sketchfab/PSX_Industrial_Pack/Prefabs/Resources/OldScarsVisuals/PFB_VIS_Crate_PSX.prefab",
                null,
                1.20f,
                0f,
                Vector3.zero),
            new VisualPrefabSpec(
                "PFB_VIS_Crate_Wood_PSX",
                "Assets/_OldScars/Art/External/Sketchfab/PSX_Industrial_Pack/Source/industrial.fbx",
                "Crate",
                "Assets/_OldScars/Art/External/Sketchfab/PSX_Industrial_Pack/Prefabs/Resources/OldScarsVisuals/PFB_VIS_Crate_Wood_PSX.prefab",
                null,
                1.20f,
                0f,
                Vector3.zero),
            new VisualPrefabSpec(
                "PFB_VIS_Crate_Metal_PSX",
                "Assets/_OldScars/Art/External/Sketchfab/PSX_Industrial_Pack/Source/industrial.fbx",
                "Crate",
                "Assets/_OldScars/Art/External/Sketchfab/PSX_Industrial_Pack/Prefabs/Resources/OldScarsVisuals/PFB_VIS_Crate_Metal_PSX.prefab",
                null,
                1.20f,
                0f,
                Vector3.zero)
        };

        [MenuItem(Impl00162MenuPath)]
        public static void GenerateImpl00162WorldVisuals()
        {
            PrepareImpl00162Assets();
            for (int index = 0; index < Impl00162Specs.Length; index++)
            {
                GenerateWorldPlaceholder(Impl00162Specs[index]);
                Debug.Log($"[GeneratePsxVisualPrefabs] Generated {Impl00162Specs[index].PrefabName}.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("IMPL-0016.2 World Visual Prefab Generation: PASS");
        }

        [MenuItem(MenuPath)]
        private static void GenerateAll()
        {
            PrepareExternalWeaponAssets();
            var failures = new List<string>();

            for (int index = 0; index < Specs.Length; index++)
            {
                VisualPrefabSpec spec = Specs[index];
                try
                {
                    Generate(spec);
                    Debug.Log($"[GeneratePsxVisualPrefabs] Generated {spec.PrefabName}.");
                }
                catch (Exception exception)
                {
                    failures.Add($"{spec.PrefabName}: {exception.Message}");
                    Debug.LogError($"[GeneratePsxVisualPrefabs] Failed {spec.PrefabName}: {exception}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (failures.Count > 0)
            {
                Debug.LogWarning(
                    $"[GeneratePsxVisualPrefabs] Generated {Specs.Length - failures.Count}/{Specs.Length} prefabs. Failures: {string.Join(" | ", failures)}");
                return;
            }

            RefreshWorldItemPrefab(
                "Assets/_OldScars/Resources/PFB_WorldItem_rusted_crowbar_01.prefab",
                CrowbarRoot + "/Prefabs/Resources/OldScarsVisuals/PFB_VIS_Rusted_Crowbar_V1_World.prefab");
            RefreshWorldItemPrefab(
                "Assets/_OldScars/Resources/PFB_WorldItem_lee_enfield_rifle_01.prefab",
                RifleRoot + "/Prefabs/Resources/OldScarsVisuals/PFB_VIS_Lee_Enfield_RetroBolt_PS2_World.prefab");
            M35VisualRigTools.GenerateWeaponVisualPrefabs();
            AssetDatabase.SaveAssets();

            Debug.Log($"[GeneratePsxVisualPrefabs] Generated all {Specs.Length} real visual prefabs.");
        }

        private static void Generate(VisualPrefabSpec spec)
        {
            GameObject sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(spec.SourceAssetPath);
            if (sourceAsset == null)
                throw new InvalidOperationException($"Source model not found or not a GameObject: {spec.SourceAssetPath}");

            Scene previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject sourceInstance = PrefabUtility.InstantiatePrefab(sourceAsset, previewScene) as GameObject;
                if (sourceInstance == null)
                    throw new InvalidOperationException($"Could not instantiate source model: {spec.SourceAssetPath}");

                GameObject prefabRoot = new GameObject(spec.PrefabName);
                SceneManager.MoveGameObjectToScene(prefabRoot, previewScene);

                Transform sourceChild = FindDescendant(sourceInstance.transform, spec.SourceChildName);
                if (sourceChild == null)
                    throw new InvalidOperationException($"Source child not found: {spec.SourceChildName}");

                if (sourceChild.GetComponentInChildren<Renderer>(true) == null)
                    throw new InvalidOperationException($"Source child has no Renderer: {spec.SourceChildName}");

                GameObject model = UnityEngine.Object.Instantiate(sourceChild.gameObject);
                model.name = "Model";
                model.transform.SetParent(prefabRoot.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = spec.LocalEulerAngles == Vector3.zero
                    ? sourceChild.rotation
                    : Quaternion.Euler(spec.LocalEulerAngles) * sourceChild.rotation;
                model.transform.localScale = sourceChild.lossyScale;
                model.SetActive(true);

                UnpackIfNeeded(model);
                StripImportedRuntimeComponents(model);
                if (!string.IsNullOrWhiteSpace(spec.MaterialPath))
                    ApplyMaterial(model, spec.MaterialPath);
                NormalizeVisual(prefabRoot.transform, model.transform, spec.TargetMaxDimension, spec.GroundClearance);

                if (prefabRoot.GetComponentInChildren<Renderer>(true) == null)
                    throw new InvalidOperationException($"Generated prefab has no Renderer: {spec.PrefabName}");

                EnsureAssetDirectory(Path.GetDirectoryName(spec.OutputPath));
                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, spec.OutputPath);
                if (savedPrefab == null)
                    throw new InvalidOperationException($"PrefabUtility could not save: {spec.OutputPath}");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static void PrepareExternalWeaponAssets()
        {
            ConfigureModelImporter(CrowbarRoot + "/Source/source/Crowbar.fbx");
            ConfigureModelImporter(RifleRoot + "/Source/source/Junk Bolt action rifle.obj");

            string crowbarBase = CrowbarRoot + "/Source/textures/pixil-frame-0_(15).png";
            string crowbarRoughness = CrowbarRoot + "/Source/textures/pixil-layer-Layer_2_(1).png";
            string rifleBase = RifleRoot + "/Source/textures/Junk_boltie_C.png";
            string rifleNormal = RifleRoot + "/Source/textures/Junk_boltie_N.png";
            string rifleRm = RifleRoot + "/Source/textures/Junk_boltie_RM.png";

            ConfigureTextureImporter(crowbarBase, TextureImporterType.Default, true);
            ConfigureTextureImporter(crowbarRoughness, TextureImporterType.Default, false);
            ConfigureTextureImporter(rifleBase, TextureImporterType.Default, true);
            ConfigureTextureImporter(rifleNormal, TextureImporterType.NormalMap, false);
            ConfigureTextureImporter(rifleRm, TextureImporterType.Default, false);

            CreateMetallicSmoothnessTexture(crowbarRoughness, CrowbarMaskPath, false);
            CreateMetallicSmoothnessTexture(rifleRm, RifleMaskPath, true);
            CreateOrUpdateMaterial(CrowbarMaterialPath, crowbarBase, null, CrowbarMaskPath);
            CreateOrUpdateMaterial(RifleMaterialPath, rifleBase, rifleNormal, RifleMaskPath);
        }

        private static void GenerateWorldPlaceholder(WorldPlaceholderSpec spec)
        {
            GameObject sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(spec.SourceAssetPath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(spec.MaterialPath);
            if (sourceAsset == null)
                throw new InvalidOperationException("Source model was not found: " + spec.SourceAssetPath);
            if (material == null)
                throw new InvalidOperationException("Derived material was not found: " + spec.MaterialPath);

            Scene previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject sourceInstance = PrefabUtility.InstantiatePrefab(sourceAsset, previewScene) as GameObject;
                if (sourceInstance == null)
                    throw new InvalidOperationException("Could not instantiate source model: " + spec.SourceAssetPath);

                GameObject prefabRoot = new GameObject(spec.PrefabName);
                SceneManager.MoveGameObjectToScene(prefabRoot, previewScene);
                GameObject model = sourceInstance;
                if (!string.IsNullOrWhiteSpace(spec.SourceChildName))
                {
                    UnpackIfNeeded(sourceInstance);
                    Transform selected = FindDescendant(sourceInstance.transform, spec.SourceChildName);
                    if (selected == null || selected.GetComponentInChildren<Renderer>(true) == null)
                        throw new InvalidOperationException("Source child was not found or has no Renderer: " + spec.SourceChildName);

                    model = selected.gameObject;
                    model.transform.SetParent(prefabRoot.transform, false);
                    UnityEngine.Object.DestroyImmediate(sourceInstance);
                }

                model.name = "Model";
                model.transform.SetParent(prefabRoot.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;
                model.SetActive(true);

                UnpackIfNeeded(model);
                StripImportedRuntimeComponents(model);
                StripImportedSceneObjects(model);
                ApplyMaterial(model, material);
                NormalizeVisual(prefabRoot.transform, model.transform, spec.TargetMaxDimension, spec.GroundClearance);

                EnsureAssetDirectory(Path.GetDirectoryName(spec.OutputPath));
                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, spec.OutputPath);
                if (savedPrefab == null)
                    throw new InvalidOperationException("Could not save visual prefab: " + spec.OutputPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static void PrepareImpl00162Assets()
        {
            ConfigureModelImporter(BandageRoot + "/Source/source/Bandage.fbx");
            ConfigureModelImporter(CanteenRoot + "/Source/source/Canteen.fbx");
            ConfigureModelImporter(CannedFoodRoot + "/Source/source/Canned Food.fbx");

            PreparePbrMaterial(
                BandageRoot,
                "Bandage_DefaultMaterial_BaseColor.png",
                "Bandage_DefaultMaterial_Normal.png",
                "Bandage_DefaultMaterial_Metallic.png",
                "Bandage_DefaultMaterial_Roughness.png",
                "Bandage_DefaultMaterial_AO.png",
                "MAT_Bandage.mat",
                "T_Bandage_MetallicSmoothness.png");
            PreparePbrMaterial(
                CanteenRoot,
                "Canteen_BaseColor.png",
                "Canteen_Normal.png",
                "Canteen_Metallic.png",
                "Canteen_Roughness.png",
                "Canteen_AO.png",
                "MAT_Canteen.mat",
                "T_Canteen_MetallicSmoothness.png");

            string cannedBase = CannedFoodRoot + "/Source/textures/Canned Food.png";
            ConfigureTextureImporter(cannedBase, TextureImporterType.Default, true);
            CreateOrUpdateMaterial(
                CannedFoodRoot + "/Materials/MAT_Canned_Food_PSX.mat",
                cannedBase,
                null,
                null,
                null);
        }

        private static void PreparePbrMaterial(
            string root,
            string baseName,
            string normalName,
            string metallicName,
            string roughnessName,
            string occlusionName,
            string materialName,
            string maskName)
        {
            string textureRoot = root + "/Source/textures/";
            string basePath = textureRoot + baseName;
            string normalPath = textureRoot + normalName;
            string metallicPath = textureRoot + metallicName;
            string roughnessPath = textureRoot + roughnessName;
            string occlusionPath = textureRoot + occlusionName;
            string maskPath = root + "/Derived/" + maskName;

            ConfigureTextureImporter(basePath, TextureImporterType.Default, true);
            ConfigureTextureImporter(normalPath, TextureImporterType.NormalMap, false);
            ConfigureTextureImporter(metallicPath, TextureImporterType.Default, false);
            ConfigureTextureImporter(roughnessPath, TextureImporterType.Default, false);
            ConfigureTextureImporter(occlusionPath, TextureImporterType.Default, false);
            CreateMetallicSmoothnessTexture(metallicPath, roughnessPath, maskPath);
            CreateOrUpdateMaterial(
                root + "/Materials/" + materialName,
                basePath,
                normalPath,
                maskPath,
                occlusionPath);
        }

        private static void ConfigureModelImporter(string assetPath)
        {
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException("External model importer was not found: " + assetPath);

            bool changed = importer.globalScale != 1f || importer.importAnimation || importer.importBlendShapes ||
                           importer.isReadable || importer.meshCompression != ModelImporterMeshCompression.Off ||
                           importer.materialImportMode != ModelImporterMaterialImportMode.None;
            importer.globalScale = 1f;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            if (changed)
                importer.SaveAndReimport();
        }

        private static void ConfigureTextureImporter(string assetPath, TextureImporterType type, bool srgb)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("External texture importer was not found: " + assetPath);

            bool changed = importer.textureType != type || importer.sRGBTexture != srgb || !importer.mipmapEnabled ||
                           importer.filterMode != FilterMode.Point || importer.anisoLevel != 1 || importer.isReadable;
            importer.textureType = type;
            importer.sRGBTexture = srgb;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Point;
            importer.anisoLevel = 1;
            importer.isReadable = false;
            if (changed)
                importer.SaveAndReimport();
        }

        private static void CreateMetallicSmoothnessTexture(string sourcePath, string outputPath, bool metallicFromBlue)
        {
            TextureImporter sourceImporter = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
            if (sourceImporter == null)
                throw new InvalidOperationException("Mask source importer was not found: " + sourcePath);

            sourceImporter.isReadable = true;
            sourceImporter.SaveAndReimport();
            try
            {
                Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
                if (source == null)
                    throw new InvalidOperationException("Mask source texture was not found: " + sourcePath);

                Color32[] sourcePixels = source.GetPixels32();
                var output = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true, true);
                var outputPixels = new Color32[sourcePixels.Length];
                for (int index = 0; index < sourcePixels.Length; index++)
                {
                    byte metallic = metallicFromBlue ? sourcePixels[index].b : (byte)192;
                    byte smoothness = (byte)(255 - sourcePixels[index].r);
                    outputPixels[index] = new Color32(metallic, 0, 0, smoothness);
                }
                output.SetPixels32(outputPixels);
                output.Apply(true, false);

                EnsureAssetDirectory(Path.GetDirectoryName(outputPath));
                string absolutePath = Path.Combine(
                    Directory.GetParent(Application.dataPath).FullName,
                    outputPath.Replace('/', Path.DirectorySeparatorChar));
                File.WriteAllBytes(absolutePath, output.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(output);
            }
            finally
            {
                sourceImporter.isReadable = false;
                sourceImporter.SaveAndReimport();
            }

            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureTextureImporter(outputPath, TextureImporterType.Default, false);
        }

        private static void CreateMetallicSmoothnessTexture(
            string metallicPath,
            string roughnessPath,
            string outputPath)
        {
            TextureImporter metallicImporter = AssetImporter.GetAtPath(metallicPath) as TextureImporter;
            TextureImporter roughnessImporter = AssetImporter.GetAtPath(roughnessPath) as TextureImporter;
            if (metallicImporter == null || roughnessImporter == null)
                throw new InvalidOperationException("Metallic/roughness source importer was not found.");

            metallicImporter.isReadable = true;
            roughnessImporter.isReadable = true;
            metallicImporter.SaveAndReimport();
            roughnessImporter.SaveAndReimport();
            try
            {
                Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(metallicPath);
                Texture2D roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(roughnessPath);
                if (metallic == null || roughness == null || metallic.width != roughness.width || metallic.height != roughness.height)
                    throw new InvalidOperationException("Metallic and roughness textures must exist with matching dimensions.");

                Color32[] metallicPixels = metallic.GetPixels32();
                Color32[] roughnessPixels = roughness.GetPixels32();
                var outputPixels = new Color32[metallicPixels.Length];
                for (int index = 0; index < outputPixels.Length; index++)
                {
                    outputPixels[index] = new Color32(
                        metallicPixels[index].r,
                        0,
                        0,
                        (byte)(255 - roughnessPixels[index].r));
                }

                var output = new Texture2D(metallic.width, metallic.height, TextureFormat.RGBA32, true, true);
                output.SetPixels32(outputPixels);
                output.Apply(true, false);
                EnsureAssetDirectory(Path.GetDirectoryName(outputPath));
                string absolutePath = Path.Combine(
                    Directory.GetParent(Application.dataPath).FullName,
                    outputPath.Replace('/', Path.DirectorySeparatorChar));
                File.WriteAllBytes(absolutePath, output.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(output);
            }
            finally
            {
                metallicImporter.isReadable = false;
                roughnessImporter.isReadable = false;
                metallicImporter.SaveAndReimport();
                roughnessImporter.SaveAndReimport();
            }

            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureTextureImporter(outputPath, TextureImporterType.Default, false);
        }

        private static void CreateOrUpdateMaterial(
            string materialPath,
            string baseTexturePath,
            string normalTexturePath,
            string metallicSmoothnessPath)
        {
            EnsureAssetDirectory(Path.GetDirectoryName(materialPath));
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    throw new InvalidOperationException("URP Lit shader was not found.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }

            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseTexturePath));
            Texture2D normal = string.IsNullOrWhiteSpace(normalTexturePath)
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture2D>(normalTexturePath);
            material.SetTexture("_BumpMap", normal);
            if (normal != null)
                material.EnableKeyword("_NORMALMAP");
            else
                material.DisableKeyword("_NORMALMAP");
            material.SetTexture(
                "_MetallicGlossMap",
                AssetDatabase.LoadAssetAtPath<Texture2D>(metallicSmoothnessPath));
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);
        }

        private static void CreateOrUpdateMaterial(
            string materialPath,
            string baseTexturePath,
            string normalTexturePath,
            string metallicSmoothnessPath,
            string occlusionPath)
        {
            EnsureAssetDirectory(Path.GetDirectoryName(materialPath));
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    throw new InvalidOperationException("URP Lit shader was not found.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }

            Texture2D normal = LoadOptionalTexture(normalTexturePath);
            Texture2D metallicSmoothness = LoadOptionalTexture(metallicSmoothnessPath);
            Texture2D occlusion = LoadOptionalTexture(occlusionPath);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseTexturePath));
            material.SetTexture("_BumpMap", normal);
            material.SetTexture("_MetallicGlossMap", metallicSmoothness);
            material.SetTexture("_OcclusionMap", occlusion);
            SetKeyword(material, "_NORMALMAP", normal != null);
            SetKeyword(material, "_METALLICSPECGLOSSMAP", metallicSmoothness != null);
            SetKeyword(material, "_OCCLUSIONMAP", occlusion != null);
            material.SetFloat("_Metallic", metallicSmoothness != null ? 1f : 0f);
            material.SetFloat("_Smoothness", metallicSmoothness != null ? 1f : 0.25f);
            material.SetFloat("_OcclusionStrength", 1f);
            EditorUtility.SetDirty(material);
        }

        private static Texture2D LoadOptionalTexture(string assetPath)
        {
            return string.IsNullOrWhiteSpace(assetPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }

        private static void ApplyMaterial(GameObject root, string materialPath)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
                throw new InvalidOperationException("Derived material was not found: " + materialPath);

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Material[] materials = renderers[index].sharedMaterials;
                if (materials.Length == 0)
                    materials = new Material[1];
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                    materials[materialIndex] = material;
                renderers[index].sharedMaterials = materials;
            }
        }

        private static void ApplyMaterial(GameObject root, Material material)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException("Imported model has no Renderer: " + root.name);

            for (int index = 0; index < renderers.Length; index++)
            {
                Material[] materials = renderers[index].sharedMaterials;
                if (materials.Length == 0)
                    materials = new Material[1];
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                    materials[materialIndex] = material;
                renderers[index].sharedMaterials = materials;
            }
        }

        private static void RefreshWorldItemPrefab(string worldItemPath, string visualPrefabPath)
        {
            GameObject visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(visualPrefabPath);
            if (visualPrefab == null)
                throw new InvalidOperationException("World visual prefab was not found: " + visualPrefabPath);

            GameObject root = PrefabUtility.LoadPrefabContents(worldItemPath);
            try
            {
                Transform previousVisual = root.transform.Find("Visual");
                if (previousVisual != null)
                    UnityEngine.Object.DestroyImmediate(previousVisual.gameObject);

                GameObject visual = PrefabUtility.InstantiatePrefab(visualPrefab, root.scene) as GameObject;
                if (visual == null)
                    throw new InvalidOperationException("Could not instantiate world visual: " + visualPrefabPath);
                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
                SetLayerRecursively(visual, root.layer);
                FitRootBoxCollider(root, visual.transform);
                PrefabUtility.SaveAsPrefabAsset(root, worldItemPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void FitRootBoxCollider(GameObject root, Transform visual)
        {
            BoxCollider collider = root.GetComponent<BoxCollider>();
            if (collider == null || root.GetComponents<BoxCollider>().Length != 1)
                throw new InvalidOperationException("World item requires exactly one root BoxCollider: " + root.name);
            if (!TryGetBoundsInRootSpace(root.transform, visual, out Bounds bounds))
                throw new InvalidOperationException("World item visual has no bounds: " + root.name);

            collider.center = bounds.center;
            collider.size = new Vector3(
                Mathf.Max(0.12f, bounds.size.x + 0.06f),
                Mathf.Max(0.12f, bounds.size.y + 0.06f),
                Mathf.Max(0.12f, bounds.size.z + 0.06f));
        }

        private static bool TryGetBoundsInRootSpace(Transform root, Transform visual, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Bounds rendererBounds = renderers[rendererIndex].localBounds;
                Vector3 center = rendererBounds.center;
                Vector3 extents = rendererBounds.extents;
                for (int cornerIndex = 0; cornerIndex < 8; cornerIndex++)
                {
                    Vector3 corner = center + new Vector3(
                        (cornerIndex & 1) == 0 ? -extents.x : extents.x,
                        (cornerIndex & 2) == 0 ? -extents.y : extents.y,
                        (cornerIndex & 4) == 0 ? -extents.z : extents.z);
                    Vector3 point = root.InverseTransformPoint(renderers[rendererIndex].transform.TransformPoint(corner));
                    if (!found)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        found = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }
            return found;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            for (int index = 0; index < root.transform.childCount; index++)
                SetLayerRecursively(root.transform.GetChild(index).gameObject, layer);
        }

        private static void EnsureAssetDirectory(string assetDirectory)
        {
            if (string.IsNullOrWhiteSpace(assetDirectory))
                return;
            string absolutePath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                assetDirectory.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(absolutePath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void NormalizeVisual(
            Transform prefabRoot,
            Transform model,
            float targetMaxDimension,
            float groundClearance)
        {
            if (targetMaxDimension <= 0.0001f)
                throw new InvalidOperationException($"Target dimension must be positive: {targetMaxDimension}");

            if (!TryGetCombinedBounds(prefabRoot, out Bounds bounds))
                throw new InvalidOperationException($"Generated model has no Renderer bounds: {model.name}");

            float maxDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxDimension <= 0.0001f)
                throw new InvalidOperationException($"Generated model has invalid Renderer bounds: {model.name}");

            model.localScale *= targetMaxDimension / maxDimension;

            if (!TryGetCombinedBounds(prefabRoot, out bounds))
                throw new InvalidOperationException($"Normalized model has no Renderer bounds: {model.name}");

            Vector3 worldOffset = new Vector3(
                -bounds.center.x,
                Mathf.Max(0f, groundClearance) - bounds.min.y,
                -bounds.center.z);
            model.position += worldOffset;
        }

        private static bool TryGetCombinedBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;

            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private static Transform FindDescendant(Transform root, string childName)
        {
            if (root.name == childName)
                return root;

            for (int index = 0; index < root.childCount; index++)
            {
                Transform found = FindDescendant(root.GetChild(index), childName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static void UnpackIfNeeded(GameObject model)
        {
            GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(model);
            if (prefabRoot != null)
            {
                PrefabUtility.UnpackPrefabInstance(
                    prefabRoot,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
            }
        }

        private static void StripImportedRuntimeComponents(GameObject root)
        {
            DestroyComponents<Collider>(root);
            DestroyComponents<Rigidbody>(root);
            DestroyComponents<Joint>(root);
            DestroyComponents<CharacterController>(root);
        }

        private static void StripImportedSceneObjects(GameObject root)
        {
            DestroyComponentGameObjects<Light>(root);
            DestroyComponentGameObjects<Camera>(root);
        }

        private static void DestroyComponentGameObjects<T>(GameObject root)
            where T : Component
        {
            T[] components = root.GetComponentsInChildren<T>(true);
            for (int index = 0; index < components.Length; index++)
            {
                if (components[index] != null)
                    UnityEngine.Object.DestroyImmediate(components[index].gameObject);
            }
        }

        private static void DestroyComponents<T>(GameObject root)
            where T : Component
        {
            T[] components = root.GetComponentsInChildren<T>(true);
            for (int index = 0; index < components.Length; index++)
                UnityEngine.Object.DestroyImmediate(components[index]);
        }

        private sealed class VisualPrefabSpec
        {
            public VisualPrefabSpec(
                string prefabName,
                string sourceAssetPath,
                string sourceChildName,
                string outputPath,
                string materialPath,
                float targetMaxDimension,
                float groundClearance,
                Vector3 localEulerAngles)
            {
                PrefabName = prefabName;
                SourceAssetPath = sourceAssetPath;
                SourceChildName = sourceChildName;
                OutputPath = outputPath;
                MaterialPath = materialPath;
                TargetMaxDimension = targetMaxDimension;
                GroundClearance = groundClearance;
                LocalEulerAngles = localEulerAngles;
            }

            public string PrefabName { get; }
            public string SourceAssetPath { get; }
            public string SourceChildName { get; }
            public string OutputPath { get; }
            public string MaterialPath { get; }
            public float TargetMaxDimension { get; }
            public float GroundClearance { get; }
            public Vector3 LocalEulerAngles { get; }
        }

        private sealed class WorldPlaceholderSpec
        {
            public WorldPlaceholderSpec(
                string prefabName,
                string sourceAssetPath,
                string outputPath,
                string materialPath,
                float targetMaxDimension,
                float groundClearance,
                string sourceChildName)
            {
                PrefabName = prefabName;
                SourceAssetPath = sourceAssetPath;
                OutputPath = outputPath;
                MaterialPath = materialPath;
                TargetMaxDimension = targetMaxDimension;
                GroundClearance = groundClearance;
                SourceChildName = sourceChildName;
            }

            public string PrefabName { get; }
            public string SourceAssetPath { get; }
            public string OutputPath { get; }
            public string MaterialPath { get; }
            public float TargetMaxDimension { get; }
            public float GroundClearance { get; }
            public string SourceChildName { get; }
        }
    }
}
