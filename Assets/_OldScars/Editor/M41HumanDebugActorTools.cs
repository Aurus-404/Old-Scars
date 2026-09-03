using System;
using System.IO;
using OldScars.Core.Actors;
using OldScars.Core.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldScars.EditorTools
{
    public static class M41HumanDebugActorTools
    {
        public const string SourceModelPath =
            "Assets/_OldScars/Art/External/Sketchfab/HumanPlaceholder/source/PSX_Char_Male_Base.fbx";
        public const string OutputPrefabPath =
            "Assets/_OldScars/Resources/OldScarsActorRepresentations/humanoid_standard.prefab";

        [MenuItem("Old Scars/Diagnostics/AI/M41.5/Generate Human Debug Actor Representation")]
        public static void GenerateHumanDebugActorRepresentation()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModelPath);
            if (source == null)
                throw new InvalidOperationException("Human debug source model was not found: " + SourceModelPath);

            EnsureFolder("Assets/_OldScars/Resources/OldScarsActorRepresentations");
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject("humanoid_standard");
                SceneManager.MoveGameObjectToScene(root, preview);

                CapsuleCollider locomotion = root.AddComponent<CapsuleCollider>();
                locomotion.center = Vector3.zero;
                locomotion.radius = 0.5f;
                locomotion.height = 2f;
                locomotion.direction = 1;
                root.AddComponent<ActorLocomotionCollider>();

                GameObject model = PrefabUtility.InstantiatePrefab(source, preview) as GameObject;
                if (model == null)
                    throw new InvalidOperationException("Human debug source model could not be instantiated.");
                PrefabUtility.UnpackPrefabInstance(
                    model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.name = "PSX_Char_Male_Base_Static";
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = new Vector3(0f, -1.007f, 0f);
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;
                DestroyComponents<Animator>(model);
                DestroyComponents<Collider>(model);
                DestroyComponents<Rigidbody>(model);

                ConfigureRig(root, model);
                CreateHitbox(root.transform, BodyRegion.Head, new Vector3(0f, 0.72f, 0f),
                    new Vector3(0.34f, 0.34f, 0.34f), true);
                CreateHitbox(root.transform, BodyRegion.Torso, new Vector3(0f, 0.18f, 0f),
                    new Vector3(0.44f, 0.56f, 0.30f), false);
                CreateHitbox(root.transform, BodyRegion.LeftArm, new Vector3(-0.55f, 0.25f, 0f),
                    new Vector3(0.56f, 0.18f, 0.20f), false);
                CreateHitbox(root.transform, BodyRegion.RightArm, new Vector3(0.55f, 0.25f, 0f),
                    new Vector3(0.56f, 0.18f, 0.20f), false);
                CreateHitbox(root.transform, BodyRegion.LeftLeg, new Vector3(-0.14f, -0.52f, 0f),
                    new Vector3(0.20f, 0.82f, 0.24f), false);
                CreateHitbox(root.transform, BodyRegion.RightLeg, new Vector3(0.14f, -0.52f, 0f),
                    new Vector3(0.20f, 0.82f, 0.24f), false);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, OutputPrefabPath);
                if (saved == null)
                    throw new InvalidOperationException("Human debug representation prefab could not be saved.");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log(
                    "[M41HumanDebugActorTools] Generated static human representation from existing PSX_Char_Male_Base " +
                    "with one locomotion capsule and six explicit anatomical combat hitboxes.\n  Output: " +
                    OutputPrefabPath);
            }
            finally
            {
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        public static void RenderHumanDebugActorPreview()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OutputPrefabPath);
            if (prefab == null)
                throw new InvalidOperationException("Generate the human debug representation before rendering it.");

            Scene renderScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderTexture target = null;
            Texture2D image = null;
            try
            {
                GameObject actor = PrefabUtility.InstantiatePrefab(prefab, renderScene) as GameObject;
                if (actor == null)
                    throw new InvalidOperationException("Human debug representation could not be instantiated.");

                Renderer[] renderers = actor.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                    throw new InvalidOperationException("Human debug representation has no renderer.");
                Bounds visibleBounds = renderers[0].bounds;
                for (int index = 0; index < renderers.Length; index++)
                {
                    renderers[index].enabled = true;
                    if (renderers[index] is SkinnedMeshRenderer skinned)
                    {
                        skinned.updateWhenOffscreen = true;
                        var bakedMesh = new Mesh { name = skinned.sharedMesh.name + "_PreviewBake" };
                        skinned.BakeMesh(bakedMesh);
                        var bakedObject = new GameObject(skinned.name + "_PreviewBake");
                        bakedObject.transform.SetParent(skinned.transform, false);
                        bakedObject.AddComponent<MeshFilter>().sharedMesh = bakedMesh;
                        bakedObject.AddComponent<MeshRenderer>().sharedMaterials = skinned.sharedMaterials;
                        skinned.enabled = false;
                    }
                    visibleBounds.Encapsulate(renderers[index].bounds);
                }

                var keyLight = new GameObject("KeyLight").AddComponent<Light>();
                SceneManager.MoveGameObjectToScene(keyLight.gameObject, renderScene);
                keyLight.type = LightType.Directional;
                keyLight.intensity = 1.25f;
                keyLight.transform.rotation = Quaternion.Euler(35f, -35f, 0f);

                var fillLight = new GameObject("FillLight").AddComponent<Light>();
                SceneManager.MoveGameObjectToScene(fillLight.gameObject, renderScene);
                fillLight.type = LightType.Directional;
                fillLight.intensity = 0.65f;
                fillLight.transform.rotation = Quaternion.Euler(20f, 145f, 0f);

                var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
                SceneManager.MoveGameObjectToScene(camera.gameObject, renderScene);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.14f, 0.17f, 1f);
                float framingDistance = Mathf.Max(3.2f, visibleBounds.extents.magnitude * 3.5f);
                Vector3 viewingOffset = new Vector3(0.55f, 0.10f, -1f).normalized * framingDistance;
                camera.transform.position = visibleBounds.center + viewingOffset;
                camera.transform.LookAt(visibleBounds.center);
                camera.fieldOfView = 32f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = framingDistance * 3f;

                target = new RenderTexture(768, 768, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target;
                camera.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                image = new Texture2D(768, 768, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0f, 0f, 768f, 768f), 0, 0);
                image.Apply();
                RenderTexture.active = previous;

                string output = Path.GetFullPath(
                    Path.Combine(Application.dataPath, "../Logs/M41_phase7_human_preview.png"));
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.WriteAllBytes(output, image.EncodeToPNG());
                Debug.Log(
                    "[M41HumanDebugActorTools] Rendered human debug preview: " + output +
                    " Bounds=" + visibleBounds.center.ToString("F3") + "/" + visibleBounds.size.ToString("F3"));
            }
            finally
            {
                if (image != null)
                    UnityEngine.Object.DestroyImmediate(image);
                if (target != null)
                    UnityEngine.Object.DestroyImmediate(target);
                EditorSceneManager.CloseScene(renderScene, true);
            }
        }

        private static void ConfigureRig(GameObject root, GameObject model)
        {
            Transform spine = FindDescendant(model.transform, "spine_02");
            Transform handLeft = FindDescendant(model.transform, "hand_l");
            Transform handRight = FindDescendant(model.transform, "hand_r");
            if (spine == null || handLeft == null || handRight == null)
                throw new InvalidOperationException("Existing human model is missing spine_02, hand_l or hand_r.");

            Transform back = CreateSocket(spine, "OS_SOCKET_Back");
            Transform waist = CreateSocket(spine, "OS_SOCKET_Waist");
            Transform sling = CreateSocket(spine, "OS_SOCKET_Sling");
            Transform left = CreateSocket(handLeft, "OS_SOCKET_HandLeft");
            Transform right = CreateSocket(handRight, "OS_SOCKET_HandRight");

            EntityVisualRigRuntime rig = root.AddComponent<EntityVisualRigRuntime>();
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
            root.AddComponent<EntityEquipmentVisualSynchronizer>();
        }

        private static void CreateHitbox(
            Transform parent,
            BodyRegion region,
            Vector3 localCenter,
            Vector3 localSize,
            bool sphere)
        {
            var hitbox = new GameObject("CombatHitbox_" + region);
            hitbox.transform.SetParent(parent, false);
            hitbox.transform.localPosition = localCenter;
            if (sphere)
            {
                SphereCollider collider = hitbox.AddComponent<SphereCollider>();
                collider.radius = localSize.x * 0.5f;
            }
            else
            {
                BoxCollider collider = hitbox.AddComponent<BoxCollider>();
                collider.size = localSize;
            }
            hitbox.AddComponent<ActorCombatHitRegion>().Configure(region);
        }

        private static Transform CreateSocket(Transform parent, string name)
        {
            var socket = new GameObject(name);
            socket.transform.SetParent(parent, false);
            return socket.transform;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
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
    }
}
