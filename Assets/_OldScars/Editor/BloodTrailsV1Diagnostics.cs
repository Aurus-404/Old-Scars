using System;
using System.IO;
using System.Linq;
using OldScars.Core;
using OldScars.Core.Actors;
using OldScars.Core.ApplicationShell;
using OldScars.Core.Data;
using OldScars.Core.Items;
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
    [InitializeOnLoad]
    public static class BloodTrailsV1Diagnostics
    {
        private const string Key = "OldScars.BloodV1.";
        private const string SettingsPath = "Assets/Resources/BloodTrails/BloodTrailVisualSettings.asset";
        private const string R0MaterialPath = "Assets/_OldScars/Art/BloodTrailsR0/BloodMarkR0.mat";
        private const string Output = "Logs/BloodTrailsV1";
        private const float ProductiveMarkSizeMeters = .25f;
        private const float ProductiveProjectionDepth = .30f;
        private const float ProductiveDrawDistance = 50f;
        private static readonly string OutputAbsolute = Path.GetFullPath(Output);
        private static int stage;
        private static double stageStarted;
        private static WorldRuntimeSceneController runtime;
        private static WorldBloodMarkPool pool;
        private static GameObject floor, triggerFloor, slopeFloor;
        private static ActorRuntimeIdentity npc;
        private static GameObject treatmentActor;
        private static int playerBefore, npcBefore, treatmentBefore;
        private static int preBandageMarks;
        private static string treatmentWoundId;
        private static float bandageSpacingBefore, bandageSpacingAfter;
        private static float smallestMarkSize = float.PositiveInfinity;
        private static float largestMarkSize;
        private static int x1Marks, x100Marks;
        private static Camera camera;
        private static RenderTexture target;

        static BloodTrailsV1Diagnostics()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
        }

        public static void RunBatch()
        {
            Require(Application.isBatchMode && !EditorApplication.isCompiling, "Requires compiled Unity batchmode.");
            Directory.CreateDirectory(OutputAbsolute);
            File.WriteAllText(Path.Combine(OutputAbsolute, "result.txt"), "RUNNING — require fresh PASS.");
            EnsureVisualSettings();
            SessionState.SetBool(Key + "pending", true);
            SessionState.SetInt(Key + "stage", 0);
            SessionState.SetString(Key + "error", string.Empty);
            SessionState.SetString(Key + "store", Path.Combine(Path.GetTempPath(), "OldScars_BloodV1_" + Guid.NewGuid().ToString("N")));
            WorldRuntimeTerrainDevelopmentSettings.SetDiagnosticSelectionOverride(WorldRuntimeTerrainDevelopmentSelection.UnityTerrain);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("GameDataManager").AddComponent<GameDataManager>();
            EditorApplication.EnterPlaymode();
        }

        private static void EnsureVisualSettings()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(R0MaterialPath);
            Require(material != null, "R0 blood material is missing.");
            string directory = Path.GetDirectoryName(SettingsPath)?.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(directory))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
                AssetDatabase.CreateFolder("Assets/Resources", "BloodTrails");
            }
            BloodTrailVisualSettings settings = AssetDatabase.LoadAssetAtPath<BloodTrailVisualSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<BloodTrailVisualSettings>();
                settings.name = "BloodTrailVisualSettings";
                settings.SetBloodMarkMaterial(material);
                settings.SetPresentation(ProductiveMarkSizeMeters, ProductiveProjectionDepth, ProductiveDrawDistance);
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            else
            {
                settings.SetBloodMarkMaterial(material);
                settings.SetPresentation(ProductiveMarkSizeMeters, ProductiveProjectionDepth, ProductiveDrawDistance);
                EditorUtility.SetDirty(settings);
            }
            AssetDatabase.SaveAssets();
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            if (SessionState.GetBool(Key + "pending", false) &&
                (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                SessionState.SetString(Key + "error", message);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key + "pending", false)) return;
            try
            {
                Require(string.IsNullOrEmpty(SessionState.GetString(Key + "error", string.Empty)),
                    SessionState.GetString(Key + "error", string.Empty));
                if (!EditorApplication.isPlaying) return;
                Require(EditorApplication.timeSinceStartup - stageStarted < 60d || stage == 0, "Diagnostic stage timed out: " + stage);
                if (stage == 0)
                {
                    if (Time.frameCount < 5 || GameDataManager.Instance?.IsReady != true) return;
                    WorldSessionService.Close();
                    WorldSessionOperationResult created = WorldSessionService.Create("Blood Trails V1", new WorldSeed(941413002L),
                        WorldGenerationSettings.ResolvePreset(WorldSizePreset.Small), LandCoveragePreset.High,
                        GameDataManager.Instance.LoadedContentSet, new PersistenceFileStore(SessionState.GetString(Key + "store", string.Empty)));
                    Require(created.Success, "World session: " + created.Failure);
                    SetStage(1);
                    SceneManager.LoadScene(WorldApplicationScenes.WorldRuntimeSceneName);
                    return;
                }
                runtime = Object.FindAnyObjectByType<WorldRuntimeSceneController>();
                if (runtime == null || !runtime.GameplayStateReady || runtime.MaterializationController?.IsReady != true) return;
                if (stage == 1) Setup();
                else if (stage == 2) WaitForBandage();
                else if (stage == 3) Complete();
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void Setup()
        {
            WorldClock.Current.AdvanceDuringGameplay = false;
            runtime.GameplayRuntimeComposition.Player.SetGameplayInputEnabled(false);
            pool = WorldBloodMarkPool.Ensure();
            pool.ConfigureDiagnosticLimits(12, 45f);
            Terrain terrain = runtime.MaterializationController.Result.Terrain;
            Require(terrain != null && terrain.GetComponent<TerrainCollider>() != null, "WorldRuntime Terrain is unavailable.");
            Vector3 origin = runtime.MaterializationController.Result.SpawnPosition + new Vector3(16f, 0f, 16f);
            origin.y = terrain.SampleHeight(origin) + terrain.transform.position.y + 4f;
            Vector3 terrainPoint = runtime.MaterializationController.Result.SpawnPosition + new Vector3(32f, 0f, 32f);
            terrainPoint.y = terrain.SampleHeight(terrainPoint) + terrain.transform.position.y + .5f;
            ActorBloodTrailEmitter terrainEmitter = CreateFixture("Blood Terrain", terrainPoint, .25f, false);
            int terrainBefore = pool.AcquiredCount;
            Move(terrainEmitter, terrainPoint + Vector3.left * 3f, terrainPoint + Vector3.right * 3f, 12);
            Require(pool.AcquiredCount > terrainBefore, "Bleeding actor did not place a mark on real WorldRuntime Terrain.");
            Require(terrainEmitter.SurfaceQuerySaturationCount == 0, "Terrain surface query saturated its nonalloc buffer.");
            CreateSurfaces(origin);
            TestCompositionBoundary();

            ActorRuntimeIdentity player = runtime.PlayerComposition.PlayerIdentity;
            Require(player != null && player.GetComponents<ActorBloodTrailEmitter>().Length == 1, "Real Player does not have exactly one BloodTrailEmitter.");
            ActorBloodTrailEmitter playerEmitter = player.GetComponent<ActorBloodTrailEmitter>();
            int healthyBefore = pool.AcquiredCount;
            Move(playerEmitter, origin + Vector3.left * 5f, origin + Vector3.right * 5f, 10);
            Require(pool.AcquiredCount == healthyBefore, "Healthy moving Player emitted marks.");

            int playerRevision = player.GetComponent<ActorMedicalStateComponent>().Revision;
            ApplyWound(player.gameObject, 1, .12f);
            playerBefore = pool.AcquiredCount;
            Move(playerEmitter, origin + Vector3.left * 5f, origin + Vector3.right * 5f, 16);
            Require(pool.AcquiredCount > playerBefore && player.GetComponent<ActorMedicalStateComponent>().Revision == playerRevision + 1,
                "Bleeding Player did not use the medical-only emitter pipeline.");

            Require(ActorSpawnService.TrySpawn("core:debug_navigation_npc_01", origin + Vector3.forward * 6f, Quaternion.identity,
                out npc, out string npcFailure), "NPC spawn: " + npcFailure);
            npc.name = "Blood Trails V1 NPC";
            NavMeshAgentIfPresent(npc.gameObject, false);
            ActorBloodTrailEmitter npcEmitter = npc.GetComponent<ActorBloodTrailEmitter>();
            Require(npc.GetComponents<ActorBloodTrailEmitter>().Length == 1, "NPC does not have exactly one BloodTrailEmitter.");
            int npcRevision = npc.GetComponent<ActorMedicalStateComponent>().Revision;
            ApplyWound(npc.gameObject, 2, .12f);
            npcBefore = pool.AcquiredCount;
            Move(npcEmitter, origin + Vector3.left * 5f + Vector3.forward * 6f, origin + Vector3.right * 5f + Vector3.forward * 6f, 16);
            Require(pool.AcquiredCount > npcBefore && npc.GetComponent<ActorMedicalStateComponent>().Revision == npcRevision + 1,
                "Bleeding NPC did not share the Player pipeline or emitter mutated Medical.");

            TestSpacingAndStatic(origin);
            TestSurfaceFiltersAndAlignment(origin);
            BeginRealBandage(origin);
            SetStage(2);
        }

        private static void TestSpacingAndStatic(Vector3 origin)
        {
            ActorBloodTrailEmitter mild = CreateFixture("Blood Mild", origin + Vector3.forward * 12f, .03f, true);
            ActorBloodTrailEmitter severe = CreateFixture("Blood Severe", origin + Vector3.forward * 15f, .25f, true);
            int mildBefore = mild.EmittedCount, severeBefore = severe.EmittedCount;
            Move(mild, mild.transform.position + Vector3.left * 5f, mild.transform.position + Vector3.right * 5f, 20);
            Move(severe, severe.transform.position + Vector3.left * 5f, severe.transform.position + Vector3.right * 5f, 20);
            Require(severe.EmittedCount - severeBefore > mild.EmittedCount - mildBefore &&
                    severe.CurrentSpacingMeters < mild.CurrentSpacingMeters,
                "Severe bleeding is not denser than mild bleeding over equal distance.");
            int staticBefore = severe.EmittedCount;
            severe.ObservePositionForDiagnostics(severe.transform.position);
            severe.ObservePositionForDiagnostics(severe.transform.position);
            Require(severe.EmittedCount == staticBefore, "Static bleeding actor emitted marks.");

            ActorBloodTrailEmitter x1 = CreateFixture("Blood Clock X1", origin + Vector3.forward * 18f, .12f, true);
            ActorBloodTrailEmitter x100 = CreateFixture("Blood Clock X100", origin + Vector3.forward * 21f, .12f, true);
            Move(x1, x1.transform.position + Vector3.left * 5f, x1.transform.position + Vector3.right * 5f, 20);
            x1Marks = x1.EmittedCount;
            Require(WorldClock.Current.TrySetDebugTimeMultiplier(100f, out string clockFailure), clockFailure);
            Move(x100, x100.transform.position + Vector3.left * 5f, x100.transform.position + Vector3.right * 5f, 20);
            x100Marks = x100.EmittedCount;
            WorldClock.Current.ResetDebugTimeMultiplier();
            Require(x1Marks == x100Marks, "WorldClock changed spatial mark density: x1=" + x1Marks + " x100=" + x100Marks);

            ActorBloodTrailEmitter noBleed = CreateFixture("Blood None", origin + Vector3.forward * 24f, 0f, true);
            int noneBefore = noBleed.EmittedCount;
            Move(noBleed, noBleed.transform.position + Vector3.left * 4f, noBleed.transform.position + Vector3.right * 4f, 12);
            Require(noBleed.EmittedCount == noneBefore, "Below-threshold bleeding emitted marks.");
            noBleed.GetComponent<ActorHealthComponent>().ApplyVitalDamage(999f);
            Move(noBleed, noBleed.transform.position, noBleed.transform.position + Vector3.right * 4f, 8);
            Require(noBleed.EmittedCount == noneBefore, "Dead actor emitted a new mark.");
        }

        private static void TestSurfaceFiltersAndAlignment(Vector3 origin)
        {
            ActorBloodTrailEmitter filter = CreateFixture("Blood Filter", origin + Vector3.forward * 27f, .25f, true);
            int before = pool.AcquiredCount;
            Move(filter, filter.transform.position + Vector3.left * 3f, filter.transform.position + Vector3.right * 3f, 12);
            Require(pool.AcquiredCount > before, "Self-collider/trigger filter rejected valid opaque floor.");
            DecalProjector projector = pool.ActiveProjectors.Last();
            float opaqueAlignment = Vector3.Dot(projector.transform.forward, -Vector3.up);
            Require(opaqueAlignment > .98f, "Opaque floor projector is not aligned to hit normal: " + opaqueAlignment);
            ValidateProductiveMarkSize(projector);
            float opaqueSize = projector.size.x;

            ActorBloodTrailEmitter slope = CreateFixture("Blood Slope", slopeFloor.transform.position + Vector3.up * .5f, .25f, true);
            Vector3 slopeNormal = slopeFloor.transform.up;
            Move(slope, slope.transform.position + slopeFloor.transform.right * -3f, slope.transform.position + slopeFloor.transform.right * 3f, 12);
            projector = pool.ActiveProjectors.Last();
            Require(Vector3.Dot(projector.transform.forward, -slopeNormal) > .98f, "Slope projector is not aligned to RaycastHit.normal.");
            ValidateProductiveMarkSize(projector);
            Require(!Mathf.Approximately(opaqueSize, projector.size.x), "Deterministic mark variation did not vary scale.");
            Require(filter.SurfaceQuerySaturationCount == 0 && slope.SurfaceQuerySaturationCount == 0,
                "Surface query saturated its nonalloc buffer.");
        }

        private static void TestCompositionBoundary()
        {
            GameObject noMedical = new GameObject("Blood No Medical");
            Require(noMedical.GetComponent<ActorBloodTrailEmitter>() == null, "Actor without Medical received a BloodTrailEmitter.");
            noMedical.AddComponent<ActorMedicalStateComponent>();
            Require(noMedical.GetComponent<ActorBloodTrailEmitter>() == null,
                "ActorMedicalStateComponent directly materialized BloodTrailEmitter.");
            noMedical.AddComponent<ActorHealthComponent>();
            Require(noMedical.GetComponents<ActorBloodTrailEmitter>().Length == 1,
                "ActorHealth composition did not materialize exactly one BloodTrailEmitter.");
            Object.Destroy(noMedical);
        }

        private static void ValidateProductiveMarkSize(DecalProjector projector)
        {
            BloodTrailVisualSettings settings = Resources.Load<BloodTrailVisualSettings>("BloodTrails/BloodTrailVisualSettings");
            Require(settings != null, "Blood trail visual settings are unavailable at runtime.");
            float minimum = settings.BaseMarkSizeMeters * .85f - .0001f;
            float maximum = settings.BaseMarkSizeMeters * 1.15f + .0001f;
            Require(projector.size.x >= minimum && projector.size.x <= maximum &&
                    Mathf.Approximately(projector.size.x, projector.size.y) && projector.size.x < .5f &&
                    Mathf.Approximately(projector.size.z, settings.ProjectionDepth),
                "Productive blood mark size/depth is invalid: " + projector.size);
            smallestMarkSize = Mathf.Min(smallestMarkSize, projector.size.x);
            largestMarkSize = Mathf.Max(largestMarkSize, projector.size.x);
        }

        private static void BeginRealBandage(Vector3 origin)
        {
            treatmentActor = CreateFixture("Blood Bandage", origin + Vector3.forward * 31f, .12f, true).gameObject;
            ActorBloodTrailEmitter emitter = treatmentActor.GetComponent<ActorBloodTrailEmitter>();
            treatmentWoundId = treatmentActor.GetComponent<ActorMedicalStateComponent>().CaptureState().wounds.Single().woundId;
            bandageSpacingBefore = emitter.CurrentSpacingMeters;
            treatmentBefore = emitter.EmittedCount;
            Move(emitter, treatmentActor.transform.position + Vector3.left * 4f, treatmentActor.transform.position + Vector3.right * 4f, 12);
            Require(emitter.EmittedCount > treatmentBefore, "Pre-bandage bleeding did not emit.");
            preBandageMarks = emitter.EmittedCount - treatmentBefore;
            ItemInstance bandage = treatmentActor.GetComponent<InventoryComponent>().AddItemByDefinitionId("core:bandage_01", 1);
            Require(bandage != null, "Could not add real bandage to treatment actor.");
            ActorWoundTreatmentController treatment = treatmentActor.GetComponent<ActorWoundTreatmentController>();
            Require(treatment.TryStart(treatmentWoundId, ActorWoundTreatmentPurpose.Manual, out string failure), "Real timed bandage did not start: " + failure);
            stageStarted = EditorApplication.timeSinceStartup;
        }

        private static void WaitForBandage()
        {
            ActorWoundTreatmentController treatment = treatmentActor.GetComponent<ActorWoundTreatmentController>();
            if (treatment.IsTreating) return;
            Require(treatment.LastOutcome == ActorWoundTreatmentOutcome.Completed, "Real timed bandage did not complete.");
            ActorBloodTrailEmitter emitter = treatmentActor.GetComponent<ActorBloodTrailEmitter>();
            emitter.ObservePositionForDiagnostics(treatmentActor.transform.position);
            bandageSpacingAfter = emitter.CurrentSpacingMeters;
            int afterBefore = emitter.EmittedCount;
            Move(emitter, treatmentActor.transform.position + Vector3.left * 4f, treatmentActor.transform.position + Vector3.right * 4f, 12);
            Require(bandageSpacingAfter > bandageSpacingBefore && emitter.EmittedCount - afterBefore < preBandageMarks,
                "Bandage did not reduce future trail density through Medical state.");
            TestBudgetExpiryAndVisual();
            SetStage(3);
        }

        private static void TestBudgetExpiryAndVisual()
        {
            ActorBloodTrailEmitter visual = CreateFixture("Blood Visual Trail", floor.transform.position + Vector3.up * .5f, .25f, true);
            int createdBefore = pool.CreatedCount;
            Move(visual, floor.transform.position + Vector3.left * 6f + Vector3.up * .5f, floor.transform.position + Vector3.right * 6f + Vector3.up * .5f, 32);
            Require(pool.ActiveMarkCount <= pool.ActiveBudget && pool.RecycledCount > 0 && pool.CreatedCount <= pool.ActiveBudget,
                "Global budget/recycling did not bound pooled marks.");
            int createdAfterWarmup = pool.CreatedCount;
            Move(visual, floor.transform.position + Vector3.left * 6f + Vector3.forward * 2f + Vector3.up * .5f,
                floor.transform.position + Vector3.right * 6f + Vector3.forward * 2f + Vector3.up * .5f, 32);
            Require(pool.CreatedCount == createdAfterWarmup && createdAfterWarmup >= createdBefore,
                "Emission instantiated new marks after pool warm-up.");
            pool.ExpireAllForDiagnostics();
            pool.ConfigureDiagnosticLimits(pool.ActiveBudget, 45f);
            ActorBloodTrailEmitter visualEvidence = CreateFixture("Blood Visual Evidence", floor.transform.position + Vector3.forward * 3f + Vector3.up * .5f, .03f, true);
            Move(visualEvidence, floor.transform.position + Vector3.left * 6f + Vector3.forward * 3f + Vector3.up * .5f,
                floor.transform.position + Vector3.right * 6f + Vector3.forward * 3f + Vector3.up * .5f, 24);
            Require(pool.ActiveMarkCount >= 4, "Visual trail did not contain multiple separated marks.");
            CaptureVisualTrail();
            pool.ConfigureDiagnosticLimits(pool.ActiveBudget, .05f);
            stageStarted = EditorApplication.timeSinceStartup;
        }

        private static void Complete()
        {
            if (EditorApplication.timeSinceStartup - stageStarted < .15d) return;
            Require(pool.ExpiredCount > 0 && pool.ActiveMarkCount == 0, "Real-time expiry did not release pooled marks.");
            Debug.Log("Blood Trails V1 Diagnostics: PASS" +
                      "\n  PlayerMarks: " + (pool.AcquiredCount - playerBefore) +
                      "\n  NpcMarks: " + (pool.AcquiredCount - npcBefore) +
                      "\n  BandageSpacing: " + bandageSpacingBefore.ToString("0.###") + " -> " + bandageSpacingAfter.ToString("0.###") +
                      "\n  MarkSize: " + smallestMarkSize.ToString("0.###") + ".." + largestMarkSize.ToString("0.###") +
                      "\n  ClockDensity: x1=" + x1Marks + " x100=" + x100Marks +
                      "\n  Pool: Created=" + pool.CreatedCount + " Acquired=" + pool.AcquiredCount + " Recycled=" + pool.RecycledCount +
                      " Expired=" + pool.ExpiredCount + " Peak=" + pool.PeakActiveMarkCount + " Budget=" + pool.ActiveBudget);
            Finish(true, "Medical distance trail, pooling, real timed bandage and visual trail passed.");
        }

        private static void CreateSurfaces(Vector3 origin)
        {
            floor = CreateFloor("Blood opaque floor", origin, Quaternion.identity, false);
            triggerFloor = CreateFloor("Blood trigger above floor", origin + Vector3.up * .1f, Quaternion.identity, true);
            slopeFloor = CreateFloor("Blood slope floor", origin + Vector3.forward * 34f, Quaternion.Euler(0f, 0f, 30f), false);
        }

        private static GameObject CreateFloor(string name, Vector3 center, Quaternion rotation, bool trigger)
        {
            GameObject result = GameObject.CreatePrimitive(PrimitiveType.Cube);
            result.name = name;
            result.transform.SetPositionAndRotation(center - rotation * Vector3.up * .15f, rotation);
            bool isSlope = name.Contains("slope");
            result.transform.localScale = isSlope ? new Vector3(12f, .3f, 12f) : new Vector3(32f, .3f, 80f);
            result.GetComponent<Collider>().isTrigger = trigger;
            result.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = Color.gray };
            return result;
        }

        private static ActorBloodTrailEmitter CreateFixture(string name, Vector3 position, float bleeding, bool selfCollider)
        {
            GameObject root = new GameObject(name);
            root.transform.position = position;
            root.AddComponent<InventoryComponent>();
            root.AddComponent<ActorItemOwnershipComponent>();
            root.AddComponent<ActorHealthComponent>();
            if (selfCollider) root.AddComponent<SphereCollider>().radius = .4f;
            ActorBloodTrailEmitter emitter = root.GetComponent<ActorBloodTrailEmitter>();
            if (bleeding > 0f) ApplyWound(root, unchecked((int)(uint)name.GetHashCode()), bleeding);
            emitter.ObservePositionForDiagnostics(position);
            return emitter;
        }

        private static void ApplyWound(GameObject actor, int value, float bleeding)
        {
            ActorMedicalStateComponent medical = actor.GetComponent<ActorMedicalStateComponent>();
            Require(medical.TryApplyWound(WoundId(value), BodyRegion.LeftLeg, WoundType.Laceration, .1f, bleeding, .01f, out string failure),
                "Wound application: " + failure);
        }

        private static void Move(ActorBloodTrailEmitter emitter, Vector3 from, Vector3 to, int steps)
        {
            emitter.transform.position = from;
            emitter.ObservePositionForDiagnostics(from);
            for (int index = 1; index <= steps; index++)
            {
                Vector3 position = Vector3.Lerp(from, to, index / (float)steps);
                emitter.transform.position = position;
                Physics.SyncTransforms();
                emitter.ObservePositionForDiagnostics(position);
            }
        }

        private static void CaptureVisualTrail()
        {
            target = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera = new GameObject("Blood Trails V1 Evidence Camera").AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            // Frame the existing trail closely enough to resolve the productive 0.25 m marks.
            camera.orthographicSize = 5f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 30f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.gray;
            camera.transform.position = floor.transform.position + Vector3.up * 12f;
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            for (int index = 0; index < 8; index++)
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            var image = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                image.Apply();
                Color32[] pixels = image.GetPixels32();
                int bloodPixels = pixels.Count(pixel => pixel.r > pixel.g * 1.4f && pixel.r > pixel.b * 1.4f && pixel.r > 80);
                Require(bloodPixels > 100, "Visual trail was not visible in URP render request: " + bloodPixels);
                File.WriteAllBytes(Path.Combine(OutputAbsolute, "trail.png"), image.EncodeToPNG());
                Debug.Log("[BloodV1] VISUAL PASS trailPixels=" + bloodPixels + " active=" + pool.ActiveMarkCount);
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(image);
                target.Release();
            }
        }

        private static void NavMeshAgentIfPresent(GameObject actor, bool enabled)
        {
            UnityEngine.AI.NavMeshAgent agent = actor.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) agent.enabled = enabled;
        }

        private static string WoundId(int value) => "wound_" + ((uint)value).ToString("x32");
        private static void SetStage(int value) { stage = value; stageStarted = EditorApplication.timeSinceStartup; }
        private static void Require(bool condition, string failure) { if (!condition) throw new InvalidOperationException(failure); }
        private static void Fail(Exception error) { Debug.LogException(error); Finish(false, error.Message); }
        private static void Finish(bool success, string detail)
        {
            SessionState.SetBool(Key + "pending", false);
            WorldClock.Current?.ResetDebugTimeMultiplier();
            File.WriteAllText(Path.Combine(OutputAbsolute, "result.txt"), (success ? "PASS" : "FAIL") + "\n" + detail);
            Debug.Log("Blood Trails V1 Diagnostics: " + (success ? "PASS" : "FAIL") + " — " + detail);
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
            else EditorApplication.ExitPlaymode();
        }
    }
}
