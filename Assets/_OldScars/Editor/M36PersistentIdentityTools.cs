using System;
using System.Collections.Generic;
using System.Diagnostics;
using OldScars.Core.Actors;
using OldScars.Core.Identity;
using OldScars.Core.Interactions;
using OldScars.Core.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace OldScars.Editor
{
    public static class M36PersistentIdentityTools
    {
        private const string MenuPath = "Old Scars/Diagnostics/M36.1/Run Checkpoint A Item Identity %#i";
        private const string ApplyMenuPath = "Old Scars/Diagnostics/M36.1/Apply Approved SampleScene Identity";
        private const string ValidateMenuPath = "Old Scars/Diagnostics/M36.1/Validate Foundation Identity";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        private static readonly ApprovedIdentity[] ApprovedRoots =
        {
            new ApprovedIdentity("Debug Player", "scene_sample_scene_actor_player_primary"),
            new ApprovedIdentity("Debug NPC Capsule", "scene_sample_scene_actor_debug_npc_capsule"),
            new ApprovedIdentity("Debug House NPC", "scene_sample_scene_actor_debug_house_npc"),
            new ApprovedIdentity("Debug Locked Door", "scene_sample_scene_door_debug_locked"),
            new ApprovedIdentity("Debug Locked House Door Entrance", "scene_sample_scene_door_house_entrance"),
            new ApprovedIdentity("Debug Locked House Door Bedroom", "scene_sample_scene_door_house_bedroom"),
            new ApprovedIdentity("Debug Sealed Container", "scene_sample_scene_container_debug_sealed"),
            new ApprovedIdentity("Survival Supply Debug Crate", "scene_sample_scene_container_survival_supply_crate"),
            new ApprovedIdentity("Misc Debug Crate", "scene_sample_scene_container_misc_supply_crate"),
            new ApprovedIdentity("Fridge", "scene_sample_scene_container_house_fridge"),
            new ApprovedIdentity("Oven", "scene_sample_scene_container_house_oven"),
            new ApprovedIdentity("Countertop", "scene_sample_scene_container_house_countertop"),
            new ApprovedIdentity("Cupboard", "scene_sample_scene_container_house_cupboard"),
            new ApprovedIdentity("Upper countertop", "scene_sample_scene_container_house_upper_cupboard")
        };

        private static readonly ApprovedIdentity[] ApprovedWorldItems =
        {
            new ApprovedIdentity("Debug World Crowbar", "item_4c1952809f1a4968ac86384b5a331201", "rusted_crowbar_01"),
            new ApprovedIdentity("Debug World Lee-Enfield Rifle", "item_c0f66d58249e4892aa4632028975816e", "lee_enfield_rifle_01")
        };

        public static void RunCheckpointAIdentityDiagnostics()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("M36.1 Checkpoint A diagnostics must run outside Play Mode.");
                return;
            }

            M36ItemIdentityDiagnostics.RunAndLog();
        }

        private static bool ValidateRunCheckpointAIdentityDiagnostics()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;
        }

        public static void ApplyApprovedSampleSceneIdentity()
        {
            EnsureEditMode();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Dictionary<string, GameObject> byName = InspectExpectedScene(scene);
            ValidateExistingAuthoredValues(byName);

            bool changed = false;
            bool mutationStarted = false;
            try
            {
                mutationStarted = true;
                for (int index = 0; index < ApprovedRoots.Length; index++)
                {
                    ApprovedIdentity approved = ApprovedRoots[index];
                    GameObject target = byName[approved.Name];
                    PersistentSceneObjectId identity = target.GetComponent<PersistentSceneObjectId>();
                    if (identity == null)
                    {
                        identity = target.AddComponent<PersistentSceneObjectId>();
                        changed = true;
                    }

                    changed |= SetSerializedString(identity, "persistentId", approved.Id);
                }

                for (int index = 0; index < ApprovedWorldItems.Length; index++)
                {
                    ApprovedIdentity approved = ApprovedWorldItems[index];
                    changed |= SetSerializedString(
                        byName[approved.Name].GetComponent<WorldItemPickup>(),
                        "authoredItemInstanceId",
                        approved.Id);
                }

                if (changed)
                    EditorSceneManager.MarkSceneDirty(scene);

                ValidateLoadedScene(scene);
                if (changed && !EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException($"Could not save '{ScenePath}'.");
            }
            catch
            {
                if (mutationStarted)
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                throw;
            }

            Debug.Log($"M36.1 approved SampleScene identity applied. changed: {changed.ToString().ToLowerInvariant()}");
        }

        public static void ValidateFoundationIdentity()
        {
            EnsureEditMode();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ValidateLoadedScene(scene);
        }

        private static void ValidateLoadedScene(Scene scene)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            Dictionary<string, GameObject> byName = InspectExpectedScene(scene);
            var errors = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int duplicateCount = 0;
            int invalidCount = 0;

            PersistentSceneObjectId[] identities = FindSceneComponents<PersistentSceneObjectId>(scene);
            if (identities.Length != ApprovedRoots.Length)
                errors.Add($"Expected 14 authored roots, found {identities.Length}.");

            for (int index = 0; index < identities.Length; index++)
            {
                PersistentSceneObjectId identity = identities[index];
                if (!PersistentSceneObjectId.IsValidFormat(identity.PersistentId))
                {
                    invalidCount++;
                    errors.Add($"Invalid or empty persistent ID on '{identity.name}'.");
                }
                else if (!ids.Add(identity.PersistentId))
                {
                    duplicateCount++;
                    errors.Add($"Duplicate persistent ID '{identity.PersistentId}'.");
                }

                if (identity.GetComponents<PersistentSceneObjectId>().Length != 1)
                    errors.Add($"'{identity.name}' has more than one PersistentSceneObjectId.");
                if (identity.GetComponent<WorldItemPickup>() != null)
                    errors.Add($"World item '{identity.name}' must use only its item identity.");
            }

            for (int index = 0; index < ApprovedRoots.Length; index++)
            {
                ApprovedIdentity approved = ApprovedRoots[index];
                PersistentSceneObjectId identity = byName[approved.Name].GetComponent<PersistentSceneObjectId>();
                if (identity == null)
                    errors.Add($"Stateful root '{approved.Name}' has no PersistentSceneObjectId.");
                else if (!string.Equals(identity.PersistentId, approved.Id, StringComparison.Ordinal))
                    errors.Add($"Stateful root '{approved.Name}' does not contain its approved ID.");
            }

            var worldIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < ApprovedWorldItems.Length; index++)
            {
                ApprovedIdentity approved = ApprovedWorldItems[index];
                WorldItemPickup pickup = byName[approved.Name].GetComponent<WorldItemPickup>();
                string itemId = pickup.AuthoredItemInstanceId;
                if (!ItemInstanceIdRegistry.IsValidFormat(itemId))
                {
                    invalidCount++;
                    errors.Add($"World item '{approved.Name}' has an invalid or empty authored item ID.");
                }
                else if (!worldIds.Add(itemId))
                {
                    duplicateCount++;
                    errors.Add($"Duplicate authored world item ID '{itemId}'.");
                }
                if (!string.Equals(itemId, approved.Id, StringComparison.Ordinal))
                    errors.Add($"World item '{approved.Name}' does not contain its approved item ID.");
                if (!string.Equals(pickup.ItemDefinitionId, approved.DefinitionId, StringComparison.Ordinal))
                    errors.Add($"World item '{approved.Name}' does not reference definition '{approved.DefinitionId}'.");
                if (pickup.GetComponent<PersistentSceneObjectId>() != null)
                    errors.Add($"World item '{approved.Name}' must not have PersistentSceneObjectId.");
            }

            GameObject strangeMachine = byName["Debug Strange Machine"];
            if (strangeMachine.GetComponentsInChildren<PersistentSceneObjectId>(true).Length != 0)
                errors.Add("Debug Strange Machine must remain excluded from persistent identity.");

            stopwatch.Stop();
            if (errors.Count > 0)
            {
                string message = "M36.1 Foundation Identity Validation: FAIL\n- " + string.Join("\n- ", errors);
                Debug.LogError(message);
                throw new InvalidOperationException(message);
            }

            Debug.Log(
                "M36.1 Foundation Identity Validation: PASS\n" +
                "actors: 3\n" +
                "doors: 3\n" +
                "containers: 8\n" +
                "authored roots: 14\n" +
                "authored world item IDs: 2\n" +
                $"IDs duplicados: {duplicateCount}\n" +
                $"IDs inválidos: {invalidCount}\n" +
                $"elapsed: {stopwatch.Elapsed.TotalMilliseconds:F1} ms");
        }

        private static Dictionary<string, GameObject> InspectExpectedScene(Scene scene)
        {
            Dictionary<string, GameObject> byName = IndexUniqueNames(scene);
            for (int index = 0; index < ApprovedRoots.Length; index++)
                RequireNamed(byName, ApprovedRoots[index].Name);
            for (int index = 0; index < ApprovedWorldItems.Length; index++)
                RequireNamed(byName, ApprovedWorldItems[index].Name);
            RequireNamed(byName, "Debug Strange Machine");

            var actors = new HashSet<GameObject>();
            AddOwners(actors, FindSceneComponents<ActorInteractionContext>(scene));
            AddOwners(actors, FindSceneComponents<ActorProfileComponent>(scene));
            RequireApprovedSet("actors", actors, byName, 0, 3);

            var doors = new HashSet<GameObject>();
            AddOwners(doors, FindSceneComponents<DoorSwingController>(scene));
            doors.Add(byName["Debug Locked Door"]);
            RequireApprovedSet("doors", doors, byName, 3, 3);

            var containers = new HashSet<GameObject>();
            AddOwners(containers, FindSceneComponents<ContainerLootComponent>(scene));
            RequireApprovedSet("containers", containers, byName, 6, 8);

            var pickupRoots = new HashSet<GameObject>();
            AddOwners(pickupRoots, FindSceneComponents<WorldItemPickup>(scene));
            RequireApprovedSet("authored world items", pickupRoots, byName, 0, 2, ApprovedWorldItems);

            return byName;
        }

        private static void ValidateExistingAuthoredValues(Dictionary<string, GameObject> byName)
        {
            var approvedObjects = new HashSet<GameObject>();
            for (int index = 0; index < ApprovedRoots.Length; index++)
                approvedObjects.Add(byName[ApprovedRoots[index].Name]);

            Scene scene = byName[ApprovedRoots[0].Name].scene;
            PersistentSceneObjectId[] existingComponents = FindSceneComponents<PersistentSceneObjectId>(scene);
            for (int index = 0; index < existingComponents.Length; index++)
                if (!approvedObjects.Contains(existingComponents[index].gameObject))
                    throw new InvalidOperationException($"Unexpected PersistentSceneObjectId on '{existingComponents[index].name}'.");

            for (int index = 0; index < ApprovedRoots.Length; index++)
            {
                ApprovedIdentity approved = ApprovedRoots[index];
                PersistentSceneObjectId[] identities = byName[approved.Name].GetComponents<PersistentSceneObjectId>();
                if (identities.Length > 1)
                    throw new InvalidOperationException($"'{approved.Name}' has more than one PersistentSceneObjectId.");
                if (identities.Length == 1 && !string.IsNullOrEmpty(identities[0].PersistentId) && identities[0].PersistentId != approved.Id)
                    throw new InvalidOperationException($"'{approved.Name}' already contains a different persistent ID.");
            }

            for (int index = 0; index < ApprovedWorldItems.Length; index++)
            {
                ApprovedIdentity approved = ApprovedWorldItems[index];
                string existing = byName[approved.Name].GetComponent<WorldItemPickup>().AuthoredItemInstanceId;
                if (!string.IsNullOrEmpty(existing) && existing != approved.Id)
                    throw new InvalidOperationException($"'{approved.Name}' already contains a different authored item ID.");
            }
        }

        private static Dictionary<string, GameObject> IndexUniqueNames(Scene scene)
        {
            var result = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (result.ContainsKey(transform.name))
                    result[transform.name] = null;
                else
                    result.Add(transform.name, transform.gameObject);
            }
            return result;
        }

        private static void RequireNamed(Dictionary<string, GameObject> byName, string name)
        {
            if (!byName.TryGetValue(name, out GameObject target) || target == null)
                throw new InvalidOperationException($"Expected exactly one SampleScene object named '{name}'.");
        }

        private static T[] FindSceneComponents<T>(Scene scene) where T : Component
        {
            T[] all = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            var result = new List<T>();
            for (int index = 0; index < all.Length; index++)
                if (all[index].gameObject.scene == scene)
                    result.Add(all[index]);
            return result.ToArray();
        }

        private static void AddOwners<T>(HashSet<GameObject> owners, T[] components) where T : Component
        {
            for (int index = 0; index < components.Length; index++)
                owners.Add(components[index].gameObject);
        }

        private static void RequireApprovedSet(
            string label,
            HashSet<GameObject> actual,
            Dictionary<string, GameObject> byName,
            int offset,
            int count,
            ApprovedIdentity[] approved = null)
        {
            approved = approved ?? ApprovedRoots;
            var expected = new HashSet<GameObject>();
            for (int index = 0; index < count; index++)
                expected.Add(byName[approved[offset + index].Name]);
            if (actual.Count != count || !actual.SetEquals(expected))
                throw new InvalidOperationException($"Expected exactly {count} approved {label}, found {actual.Count} or a different set.");
        }

        private static bool SetSerializedString(UnityEngine.Object target, string propertyName, string value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException($"Serialized property '{propertyName}' was not found on '{target.name}'.");
            if (property.stringValue == value)
                return false;
            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
            return true;
        }

        private static void EnsureEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("M36.1 Foundation identity tools require Edit Mode after compilation.");
        }

        private readonly struct ApprovedIdentity
        {
            internal ApprovedIdentity(string name, string id, string definitionId = null)
            {
                Name = name;
                Id = id;
                DefinitionId = definitionId;
            }

            internal string Name { get; }
            internal string Id { get; }
            internal string DefinitionId { get; }
        }

    }
}
