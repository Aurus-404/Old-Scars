using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Data.Loading;
using OldScars.Core.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldScars.EditorTools
{
    public static class RigidEquipmentPoseTunerDiagnostics
    {
        private const string HumanoidPrefabPath =
            "Assets/_OldScars/Resources/OldScarsActorRepresentations/humanoid_standard.prefab";

        [MenuItem("Old Scars/Diagnostics/IMPL-0016.1A Rigid Equipment Pose Tuner")]
        public static void Run()
        {
            string livePosePath = RigidEquipmentPoseTunerWindow.AssetPathToAbsolute(
                RigidEquipmentPoseTunerWindow.AttachmentPosesAssetPath);
            byte[] livePoseBytes = File.ReadAllBytes(livePosePath);
            int initialSceneCount = SceneManager.sceneCount;
            Scene initialActiveScene = SceneManager.GetActiveScene();
            Dictionary<Scene, bool> initialDirtyStates = CaptureDirtyStates();
            string temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "OldScars_IMPL0016_1A_" + Guid.NewGuid().ToString("N"));
            RigidEquipmentPoseTunerWindow window = null;
            try
            {
                Require(RigidEquipmentPoseCatalog.TryLoad(HumanoidPrefabPath, out RigidEquipmentPoseCatalog catalog, out string error),
                    "GameData/catalog load failed: " + error);
                Require(catalog.Entries.Count == 6,
                    "Expected exactly the six current rigid equipped profiles, found " + catalog.Entries.Count + ".");

                RigidEquipmentPoseEntry backpack = RequireEntry(catalog, "core:small_backpack_01");
                RigidEquipmentPoseEntry crowbar = RequireEntry(catalog, "core:rusted_crowbar_01");
                RigidEquipmentPoseEntry rifle = RequireEntry(catalog, "core:lee_enfield_rifle_01");
                RigidEquipmentPoseEntry belt = RequireEntry(catalog, "core:canvas_belt_01");
                RigidEquipmentPoseEntry helmet = RequireEntry(catalog, "core:steel_helmet_01");
                RigidEquipmentPoseEntry goggles = RequireEntry(catalog, "core:work_goggles_01");
                RequireRoles(backpack, "back");
                RequireRoles(crowbar, "hand_right", "hand_left");
                RequireRoles(rifle, "hand_right");
                RequireRoles(belt, "waist");
                RequireRoles(helmet, "head");
                RequireRoles(goggles, "eyes");

                window = ScriptableObject.CreateInstance<RigidEquipmentPoseTunerWindow>();
                Require(window.DiagnosticCatalog != null,
                    "Cold-open tuner did not initialize its catalog.");
                Require(!window.DiagnosticHasEditablePreview && window.DiagnosticAttachmentRoot == null,
                    "Cold-open tuner unexpectedly requires or retains an editable preview.");

                Require(window.DiagnosticLoad(backpack.Item.id, "back"),
                    "Cold-open Load Preview seam could not create the selected preview.");
                Require(window.DiagnosticHasEditablePreview && window.DiagnosticAttachmentRoot != null,
                    "Load Preview did not expose editable transform controls.");
                window.DiagnosticClosePreview();
                Require(!window.DiagnosticHasEditablePreview && window.DiagnosticAttachmentRoot == null,
                    "Closing the preview did not return the tuner to its safe cold state.");
                AssertSceneState(initialSceneCount, initialActiveScene, initialDirtyStates);

                ValidatePreview(window, catalog, backpack.Item.id, "back", initialSceneCount, initialActiveScene, initialDirtyStates);
                ValidatePreview(window, catalog, crowbar.Item.id, "hand_left", initialSceneCount, initialActiveScene, initialDirtyStates);
                ValidatePreview(window, catalog, rifle.Item.id, "hand_right", initialSceneCount, initialActiveScene, initialDirtyStates);
                ValidatePreview(window, catalog, belt.Item.id, "waist", initialSceneCount, initialActiveScene, initialDirtyStates);
                ValidatePreview(window, catalog, helmet.Item.id, "head", initialSceneCount, initialActiveScene, initialDirtyStates);
                ValidatePreview(window, catalog, goggles.Item.id, "eyes", initialSceneCount, initialActiveScene, initialDirtyStates);

                Require(window.DiagnosticLoad(crowbar.Item.id, "hand_right"), "Crowbar Right Hand preview did not load.");
                Transform attachmentRoot = window.DiagnosticAttachmentRoot;
                Require(attachmentRoot != null, "Crowbar preview has no AttachmentRoot.");
                var position = new Vector3(0.1234567f, -0.2345678f, 0.3456789f);
                var requestedRotation = new Vector3(17.25f, 128.5f, 271.75f);
                var scale = new Vector3(0.8123456f, 0.9234567f, 1.0345678f);
                attachmentRoot.localPosition = position;
                attachmentRoot.localEulerAngles = requestedRotation;
                attachmentRoot.localScale = scale;
                Vector3 rotation = attachmentRoot.localEulerAngles;
                Require(livePoseBytes.SequenceEqual(File.ReadAllBytes(livePosePath)),
                    "Editing the preview mutated attachment_poses.json before Save.");

                RigidEquipmentPoseContext rightHand = crowbar.Contexts.Single(value => value.Socket.role == "hand_right");
                Require(AttachmentPoseResolver.TryResolveDefinition(
                        catalog.Database,
                        crowbar.Profile,
                        catalog.Rig.Profile.id,
                        catalog.Rig.Profile.family_id,
                        rightHand.Socket.id,
                        rightHand.Socket.role,
                        out AttachmentPoseDefinition resolved),
                    "Crowbar Right Hand did not resolve its current AttachmentPoseDefinition.");
                Require(resolved.id == "core:rusted_crowbar_human_right_pose",
                    "Crowbar Right Hand resolved an unexpected pose: " + resolved.id);
                AttachmentPoseDefinition authored = RigidEquipmentPoseAuthoring.CreateDefinition(
                    crowbar.Profile, catalog.Rig.Profile, rightHand.Socket, resolved, attachmentRoot);

                Directory.CreateDirectory(temporaryDirectory);
                string fixturePath = Path.Combine(temporaryDirectory, "attachment_poses.json");
                File.Copy(livePosePath, fixturePath);
                JObject before = JObject.Parse(File.ReadAllText(fixturePath));
                AttachmentPoseJsonStore.Save(fixturePath, authored);
                JObject after = JObject.Parse(File.ReadAllText(fixturePath));
                AssertUnrelatedEntriesUnchanged(before, after, authored.id);

                AttachmentPoseDefinition[] saved = AttachmentPoseJsonStore.ReadAll(fixturePath);
                Require(saved.Select(value => value.id).Distinct(StringComparer.Ordinal).Count() == saved.Length,
                    "Save created duplicate AttachmentPose IDs.");
                AttachmentPoseDefinition savedPose = saved.Single(value => value.id == authored.id);
                AssertVector(savedPose.local_position, position, "saved Position");
                AssertVector(savedPose.local_rotation, rotation, "saved Rotation");
                AssertVector(savedPose.local_scale, scale, "saved Scale");

                GameDatabase resolverDatabase = BuildPoseDatabase(saved);
                AttachmentPoseValue runtimePose = AttachmentPoseResolver.Resolve(
                    resolverDatabase,
                    crowbar.Profile,
                    catalog.Rig.Profile.id,
                    catalog.Rig.Profile.family_id,
                    rightHand.Socket.id,
                    rightHand.Socket.role);
                AssertVector(runtimePose.LocalPosition, position, "runtime Position");
                AssertVector(runtimePose.LocalEulerAngles, rotation, "runtime Rotation");
                AssertVector(runtimePose.LocalScale, scale, "runtime Scale");

                attachmentRoot.localPosition = runtimePose.LocalPosition;
                attachmentRoot.localEulerAngles = runtimePose.LocalEulerAngles;
                attachmentRoot.localScale = runtimePose.LocalScale;
                AssertVector(attachmentRoot.localPosition, position, "reloaded Position");
                AssertRotation(attachmentRoot.localRotation, rotation, "reloaded Rotation");
                AssertVector(attachmentRoot.localScale, scale, "reloaded Scale");

                int poseCount = saved.Length;
                AttachmentPoseJsonStore.Save(fixturePath, authored);
                Require(AttachmentPoseJsonStore.ReadAll(fixturePath).Length == poseCount,
                    "Saving an existing pose created a duplicate definition.");

                window.DiagnosticClosePreview();
                AssertSceneState(initialSceneCount, initialActiveScene, initialDirtyStates);
                Require(livePoseBytes.SequenceEqual(File.ReadAllBytes(livePosePath)),
                    "Diagnostics modified the live attachment_poses.json file.");
                Debug.Log("IMPL-0016.1A Rigid Equipment Pose Tuner Diagnostics: PASS");
            }
            finally
            {
                if (window != null)
                    UnityEngine.Object.DestroyImmediate(window);
                if (Directory.Exists(temporaryDirectory))
                    Directory.Delete(temporaryDirectory, true);
                Require(livePoseBytes.SequenceEqual(File.ReadAllBytes(livePosePath)),
                    "Diagnostics did not preserve the live attachment_poses.json bytes.");
            }
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("IMPL-0016.1A batch diagnostics require batchmode.");
            try
            {
                if (string.IsNullOrWhiteSpace(SceneManager.GetActiveScene().path))
                {
                    EditorBuildSettingsScene fixture = EditorBuildSettings.scenes.FirstOrDefault(
                        value => value.enabled && !string.IsNullOrWhiteSpace(value.path));
                    if (fixture == null)
                        throw new InvalidOperationException("No enabled Build Settings scene is available for the preview isolation fixture.");
                    EditorSceneManager.OpenScene(fixture.path, OpenSceneMode.Single);
                }
                Run();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ValidatePreview(
            RigidEquipmentPoseTunerWindow window,
            RigidEquipmentPoseCatalog catalog,
            string itemId,
            string socketRole,
            int initialSceneCount,
            Scene initialActiveScene,
            Dictionary<Scene, bool> initialDirtyStates)
        {
            Require(window.DiagnosticLoad(itemId, socketRole), itemId + " could not load on " + socketRole + ".");
            Transform attachmentRoot = window.DiagnosticAttachmentRoot;
            Require(attachmentRoot != null && attachmentRoot.name == "AttachmentRoot",
                itemId + " preview did not select the equipped prefab AttachmentRoot.");
            RigidEquipmentPoseEntry entry = catalog.Entries.Single(value => value.Item.id == itemId);
            RigidEquipmentPoseContext context = entry.Contexts.Single(value => value.Socket.role == socketRole);
            AttachmentPoseValue expected = AttachmentPoseResolver.Resolve(
                catalog.Database,
                entry.Profile,
                catalog.Rig.Profile.id,
                catalog.Rig.Profile.family_id,
                context.Socket.id,
                context.Socket.role);
            AssertVector(attachmentRoot.localPosition, expected.LocalPosition, itemId + " preview Position");
            AssertRotation(attachmentRoot.localRotation, expected.LocalEulerAngles, itemId + " preview Rotation");
            AssertVector(attachmentRoot.localScale, expected.LocalScale, itemId + " preview Scale");
            window.DiagnosticClosePreview();
            AssertSceneState(initialSceneCount, initialActiveScene, initialDirtyStates);
        }

        private static RigidEquipmentPoseEntry RequireEntry(RigidEquipmentPoseCatalog catalog, string itemId)
        {
            RigidEquipmentPoseEntry entry = catalog.Entries.SingleOrDefault(value => value.Item.id == itemId);
            Require(entry != null, "Tuneable rigid item was not found: " + itemId);
            Require(EquippedVisualPrefabContract.TryValidate(entry.Prefab, out string error),
                "Equipped prefab contract failed for " + itemId + ": " + error);
            Require(entry.Prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) == null,
                "Skinned/deformable visual entered the rigid tuner: " + itemId);
            return entry;
        }

        private static void RequireRoles(RigidEquipmentPoseEntry entry, params string[] expectedRoles)
        {
            string[] actual = entry.Contexts.Select(value => value.Socket.role).ToArray();
            Require(actual.SequenceEqual(expectedRoles),
                entry.Item.id + " contexts differ. Expected [" + string.Join(", ", expectedRoles) +
                "], got [" + string.Join(", ", actual) + "].");
        }

        private static GameDatabase BuildPoseDatabase(IEnumerable<AttachmentPoseDefinition> poses)
        {
            var report = new DataLoadReport();
            var database = new GameDatabase(report);
            foreach (AttachmentPoseDefinition pose in poses)
                database.RegisterAttachmentPose(pose, report);
            Require(!report.HasErrors, "Fixture AttachmentPose database has registration errors.");
            return database;
        }

        private static void AssertUnrelatedEntriesUnchanged(JObject before, JObject after, string changedId)
        {
            Dictionary<string, JToken> beforeById = ((JArray)before["attachment_poses"])
                .OfType<JObject>()
                .ToDictionary(value => (string)value["id"], value => (JToken)value, StringComparer.Ordinal);
            Dictionary<string, JToken> afterById = ((JArray)after["attachment_poses"])
                .OfType<JObject>()
                .ToDictionary(value => (string)value["id"], value => (JToken)value, StringComparer.Ordinal);
            Require(beforeById.Count == afterById.Count, "Updating an existing pose changed the entry count.");
            foreach (KeyValuePair<string, JToken> pair in beforeById)
            {
                if (pair.Key == changedId)
                    continue;
                Require(afterById.TryGetValue(pair.Key, out JToken candidate) && JToken.DeepEquals(pair.Value, candidate),
                    "Unrelated AttachmentPose changed: " + pair.Key);
            }
        }

        private static Dictionary<Scene, bool> CaptureDirtyStates()
        {
            var result = new Dictionary<Scene, bool>();
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                result[scene] = scene.isDirty;
            }
            return result;
        }

        private static void AssertSceneState(
            int expectedCount,
            Scene expectedActive,
            Dictionary<Scene, bool> expectedDirtyStates)
        {
            Require(SceneManager.sceneCount == expectedCount, "Preview did not close its temporary additive scene.");
            Require(SceneManager.GetActiveScene() == expectedActive,
                "Preview did not restore the previously active scene.");
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                Require(expectedDirtyStates.TryGetValue(scene, out bool dirty) && scene.isDirty == dirty,
                    "Preview changed gameplay scene dirtiness: " + scene.path);
            }
        }

        private static void AssertVector(Float3Definition actual, Vector3 expected, string label)
        {
            Require(actual != null, label + " is null.");
            AssertVector(new Vector3(actual.x, actual.y, actual.z), expected, label);
        }

        private static void AssertVector(Vector3 actual, Vector3 expected, string label)
        {
            Require(Mathf.Abs(actual.x - expected.x) <= 0.00001f &&
                    Mathf.Abs(actual.y - expected.y) <= 0.00001f &&
                    Mathf.Abs(actual.z - expected.z) <= 0.00001f,
                label + " differs. Expected " + expected.ToString("F7") + ", got " + actual.ToString("F7") + ".");
        }

        private static void AssertRotation(Quaternion actual, Vector3 expectedEuler, string label)
        {
            float angle = Quaternion.Angle(actual, Quaternion.Euler(expectedEuler));
            Require(angle < 0.0001f, label + " differs by " + angle.ToString("R") + " degrees.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
