using System;
using System.IO;
using System.Linq;
using System.Reflection;
using OldScars.Core;
using OldScars.Core.ApplicationShell;
using OldScars.Core.Data;
using OldScars.Core.Persistence;
using OldScars.Core.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OldScars.Editor
{
    // R0 only: explicit batch fixture, never attached to gameplay or saved into WorldRuntime.
    [InitializeOnLoad]
    public static class BloodTrailsR0Diagnostics
    {
        private const string Key = "OldScars.BloodR0.";
        private const string AssetRoot = "Assets/_OldScars/Art/BloodTrailsR0";
        private const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        private static readonly string Output = Path.GetFullPath("Logs/BloodTrailsR0");
        private static Camera camera;
        private static DecalProjector projector;
        private static GameObject floor;
        private static Material floorMaterial;
        private static RenderTexture target;
        private static Vector3 point, normal;
        private static Color32[] baseline;
        private static int scenario, phase, warmup;
        private static double deadline;
        private static readonly string[] Cases = { "exterior", "opaque-floor", "inclined-floor" };

        static BloodTrailsR0Diagnostics()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
        }

        public static void RunBatch()
        {
            Require(Application.isBatchMode && !EditorApplication.isCompiling, "Requires compiled batchmode with GPU.");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "result.txt"), "RUNNING — require a fresh PASS in the Unity log.");
            ConfigureAssets();
            ValidateConfiguration();
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "GPU unavailable; visual verification pending.");
            SessionState.SetBool(Key + "pending", true);
            SessionState.SetInt(Key + "stage", 0);
            SessionState.SetString(Key + "error", "");
            SessionState.SetString(Key + "store", Path.Combine(Path.GetTempPath(), "OldScars_BloodR0_" + Guid.NewGuid().ToString("N")));
            WorldRuntimeTerrainDevelopmentSettings.SetDiagnosticSelectionOverride(WorldRuntimeTerrainDevelopmentSelection.UnityTerrain);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("GameDataManager").AddComponent<GameDataManager>();
            EditorApplication.EnterPlaymode();
        }

        private static void ConfigureAssets()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            Require(data != null, "PC renderer missing.");
            var features = data.rendererFeatures.OfType<DecalRendererFeature>().ToArray();
            Require(features.Length <= 1, "Multiple decal features.");
            if (features.Length == 0)
            {
                var feature = ScriptableObject.CreateInstance<DecalRendererFeature>();
                feature.name = "Blood Trails R0 Decal";
                feature.SetActive(true);
                var serialized = new SerializedObject(feature);
                serialized.FindProperty("m_Settings.technique").enumValueIndex = 0;
                serialized.FindProperty("m_Settings.maxDrawDistance").floatValue = 50f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.AddObjectToAsset(feature, data);
                data.rendererFeatures.Add(feature);
                data.SetDirty();
                EditorUtility.SetDirty(data);
            }
            // Mirror Unity's feature map using real serialized local IDs, never invented YAML IDs.
            var rendererSerialized = new SerializedObject(data);
            var map = rendererSerialized.FindProperty("m_RendererFeatureMap");
            map.arraySize = data.rendererFeatures.Count;
            for (int i = 0; i < map.arraySize; i++)
            {
                Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data.rendererFeatures[i], out string _, out long id), "Feature local ID unavailable.");
                map.GetArrayElementAtIndex(i).longValue = id;
            }
            rendererSerialized.ApplyModifiedPropertiesWithoutUndo();
            Directory.CreateDirectory(AssetRoot);
            AssetDatabase.Refresh();
            string texturePath = AssetRoot + "/BloodMarkR0.png";
            if (!File.Exists(texturePath))
            {
                var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
                for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float u = (x - 63.5f) / 63.5f, v = (y - 63.5f) / 63.5f;
                    float angle = Mathf.Atan2(v, u);
                    float edge = .65f + .10f * Mathf.Sin(5 * angle) + .07f * Mathf.Cos(9 * angle);
                    float alpha = Mathf.Clamp01((edge - Mathf.Sqrt(u * u + v * v)) * 40f) * .92f;
                    texture.SetPixel(x, y, new Color(.48f, .012f, .025f, alpha));
                }
                texture.Apply();
                File.WriteAllBytes(texturePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(texturePath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            string materialPath = AssetRoot + "/BloodMarkR0.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) == null)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.unity.render-pipelines.universal/Shaders/Decal.shadergraph");
                Require(shader != null, "URP default decal shader missing.");
                var material = new Material(shader) { name = "BloodMarkR0", enableInstancing = true };
                material.SetTexture("Base_Map", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                material.SetFloat("Normal_Blend", 0f);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            AssetDatabase.SaveAssets();
        }

        private static void ValidateConfiguration()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            Require(QualitySettings.renderPipeline == pipeline && GraphicsSettings.defaultRenderPipeline == pipeline, "Active PC pipeline mismatch.");
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            var serialized = new SerializedObject(pipeline);
            Require(serialized.FindProperty("m_DefaultRendererIndex").intValue == 0 &&
                serialized.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue == data, "Default PC renderer mismatch.");
            Require((int)data.renderingMode == 2, "Forward+ was changed.");
            Require(data.rendererFeatures.Any(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion" && f.isActive), "Active SSAO missing.");
            var decal = data.rendererFeatures.OfType<DecalRendererFeature>().Single();
            Require(decal.isActive && new SerializedObject(decal).FindProperty("m_Settings.technique").enumValueIndex == 0, "Active Automatic decal missing.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetRoot + "/BloodMarkR0.mat");
            Require(material != null && material.shader.isSupported && material.HasProperty("Base_Map") && material.GetTexture("Base_Map") != null, "Invalid decal material.");
            Debug.Log("[BloodR0] CONFIG PASS: PC Forward+; SSAO active; Decal Automatic, maxDistance=50, layers=false; shader=" + material.shader.name + "; GPU=" + SystemInfo.graphicsDeviceName);
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            if (SessionState.GetBool(Key + "pending", false) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                SessionState.SetString(Key + "error", message);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key + "pending", false)) return;
            try
            {
                Require(string.IsNullOrEmpty(SessionState.GetString(Key + "error", "")), SessionState.GetString(Key + "error", ""));
                if (!EditorApplication.isPlaying) return;
                if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 180;
                Require(EditorApplication.timeSinceStartup < deadline, "World/render timeout.");
                WorldRuntimeTerrainDevelopmentSettings.SetDiagnosticSelectionOverride(WorldRuntimeTerrainDevelopmentSelection.UnityTerrain);
                int stage = SessionState.GetInt(Key + "stage", 0);
                if (stage == 0)
                {
                    if (Time.frameCount < 5 || GameDataManager.Instance?.IsReady != true) return;
                    WorldSessionService.Close();
                    var result = WorldSessionService.Create("Blood R0", new WorldSeed(941413001L),
                        WorldGenerationSettings.ResolvePreset(WorldSizePreset.Small), LandCoveragePreset.High,
                        GameDataManager.Instance.LoadedContentSet, new PersistenceFileStore(SessionState.GetString(Key + "store", "")));
                    Require(result.Success, "World creation: " + result.Failure);
                    SessionState.SetInt(Key + "stage", 1);
                    SceneManager.LoadScene(WorldApplicationScenes.WorldRuntimeSceneName);
                    return;
                }
                var runtime = Object.FindAnyObjectByType<WorldRuntimeSceneController>();
                if (runtime == null || !runtime.GameplayStateReady || runtime.MaterializationController?.IsReady != true) return;
                if (stage == 1)
                {
                    ValidateConfiguration();
                    Time.timeScale = 0;
                    target = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
                    target.Create();
                    camera = new GameObject("Blood R0 Evidence Camera").AddComponent<Camera>();
                    camera.enabled = false;
                    camera.orthographic = true;
                    camera.orthographicSize = 2.5f;
                    camera.nearClipPlane = .1f;
                    camera.farClipPlane = 25;
                    camera.targetTexture = target;
                    camera.GetUniversalAdditionalCameraData().SetRenderer(0);
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = Color.gray;
                    projector = new GameObject("Blood R0 Projector").AddComponent<DecalProjector>();
                    projector.material = AssetDatabase.LoadAssetAtPath<Material>(AssetRoot + "/BloodMarkR0.mat");
                    projector.size = new Vector3(2, 2, .3f);
                    projector.pivot = Vector3.zero;
                    projector.drawDistance = 50;
                    SetupCase(runtime);
                    SessionState.SetInt(Key + "stage", 2);
                    return;
                }
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                Require(string.IsNullOrEmpty(SessionState.GetString(Key + "error", "")), SessionState.GetString(Key + "error", ""));
                if (++warmup < 8) return;
                warmup = 0;
                Require(projector.IsValid(), "Projector invalid after URP render; passes=" + projector.material.passCount + "; pipeline=" + GraphicsSettings.currentRenderPipeline);
                var pixels = ReadImage(Cases[scenario] + (phase == 0 ? "-off" : phase == 1 ? "-on" : "-out-of-depth"));
                if (phase == 0)
                {
                    baseline = pixels;
                    projector.enabled = true;
                    phase = 1;
                }
                else if (phase == 1)
                {
                    int changed = ChangedPixels(baseline, pixels);
                    Require(changed > 500 && changed < 35000, Cases[scenario] + " decal coverage unexpected: " + changed);
                    var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
                    var feature = data.rendererFeatures.OfType<DecalRendererFeature>().Single();
                    var ssao = data.rendererFeatures.Single(f => f.GetType().Name == "ScreenSpaceAmbientOcclusion");
                    Require(ssao.isActive && ssao.GetType().GetField("m_Material", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(ssao) is Material,
                        "SSAO did not prepare its rendering resources.");
                    object technique = typeof(DecalRendererFeature).GetField("m_Technique", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(feature);
                    Debug.Log("[BloodR0] " + Cases[scenario] + " RENDER PASS changedPixels=" + changed + "; technique=" + technique + "; point=" + point + "; normal=" + normal);
                    projector.transform.position = point + normal * 1f;
                    phase = 2;
                }
                else
                {
                    int changed = ChangedPixels(baseline, pixels);
                    Require(changed < 100, "Out-of-depth projection or unstable baseline: " + changed);
                    Debug.Log("[BloodR0] " + Cases[scenario] + " DEPTH PASS changedPixels=" + changed);
                    if (++scenario < Cases.Length) SetupCase(runtime);
                    else Finish(true, "All three surfaces rendered; depth negative controls passed.");
                }
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        private static void SetupCase(WorldRuntimeSceneController runtime)
        {
            var terrain = runtime.MaterializationController.Result.Terrain;
            Require(terrain != null && terrain.GetComponent<TerrainCollider>() != null, "Real WorldRuntime Terrain unavailable.");
            point = runtime.MaterializationController.Result.SpawnPosition + new Vector3(5, 0, 5);
            point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
            Vector3 local = point - terrain.transform.position;
            normal = terrain.terrainData.GetInterpolatedNormal(local.x / terrain.terrainData.size.x, local.z / terrain.terrainData.size.z);
            if (scenario > 0)
            {
                if (floor == null)
                {
                    floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    floor.name = "Blood R0 opaque floor fixture";
                    floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    floorMaterial.color = new Color(.6f, .6f, .6f);
                    floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
                }
                point.y += 4;
                normal = scenario == 1 ? Vector3.up : Quaternion.Euler(0, 0, 30) * Vector3.up;
                floor.transform.SetPositionAndRotation(point - normal * .15f, Quaternion.FromToRotation(Vector3.up, normal));
                floor.transform.localScale = new Vector3(7, .3f, 7);
            }
            projector.transform.SetPositionAndRotation(point, Quaternion.LookRotation(-normal, Vector3.forward));
            projector.enabled = false;
            camera.transform.position = point + normal * 7f - Vector3.forward * 2f;
            camera.transform.LookAt(point, Vector3.forward);
            phase = 0;
            warmup = 0;
        }

        private static Color32[] ReadImage(string name)
        {
            var previous = RenderTexture.active;
            var image = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(Output, name + ".png"), image.EncodeToPNG());
                return image.GetPixels32();
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
        }

        private static int ChangedPixels(Color32[] a, Color32[] b)
        {
            int count = 0;
            for (int i = 0; i < a.Length; i++)
                if (Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b) > 30) count++;
            return count;
        }

        private static void Finish(bool success, string detail)
        {
            SessionState.SetBool(Key + "pending", false);
            Time.timeScale = 1;
            if (target != null) target.Release();
            Debug.Log("Blood Trails R0 Diagnostics: " + (success ? "PASS" : "FAIL") + " — " + detail);
            File.WriteAllText(Path.Combine(Output, "result.txt"), (success ? "PASS" : "FAIL") + "\n" + detail);
            EditorApplication.Exit(success ? 0 : 1);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
