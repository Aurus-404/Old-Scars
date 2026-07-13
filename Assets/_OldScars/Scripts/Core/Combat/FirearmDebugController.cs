using System;
using System.Collections.Generic;
using OldScars.Core.Actors;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Feedback;
using OldScars.Core.Interactions;
using OldScars.Core.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OldScars.Core.Combat
{
    /// <summary>
    /// Small runtime-only firearm prototype for an item equipped in right_hand.
    /// JSON owns firearm/ammo values; this component owns input and raycasts.
    /// </summary>
    public sealed class FirearmDebugController : MonoBehaviour
    {
        [SerializeField] private InventoryComponent inventory;
        [SerializeField] private Camera inputCamera;
        [SerializeField] private DebugWorldUiInputBlocker uiInputBlocker;
        [SerializeField] private PointClickMovementInputController movementInput;
        [SerializeField] private LayerMask hitLayerMask;
        [SerializeField] private float muzzleHeight;
        [SerializeField] private float tracerDuration = 0.12f;

        private bool isAimActive;
        private bool restoreMovementInput;
        private string aimedFirearmInstanceId;
        private float nextFireTime;
        private float tracerEndTime;
        private AimSolution currentAimSolution;
        private bool hasCurrentAimSolution;
        private LineRenderer aimLine;
        private LineRenderer tracerLine;

        public bool IsAimActive => isAimActive;
        public bool HasEquippedFirearm => TryGetEquippedFirearm(out _, out _);
        public string EquippedFirearmDisplayName
        {
            get
            {
                return TryGetEquippedFirearm(out ItemDefinition item, out FirearmProfileDefinition profile)
                    ? GetDisplayName(item, profile != null ? profile.display_name : null)
                    : null;
            }
        }

        public string StatusText
        {
            get
            {
                if (!HasEquippedFirearm)
                    return null;

                if (!isAimActive)
                    return "Aim disabled.";

                float remainingCycle = Mathf.Max(0f, nextFireTime - Time.time);
                return remainingCycle > 0f
                    ? $"Aim active. Bolt cycling: {remainingCycle:0.0}s."
                    : "Aim active. Rifle ready.";
            }
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnDisable()
        {
            DisableAim(false);
        }

        private void Update()
        {
            UpdateTracer();

            if (uiInputBlocker != null && uiInputBlocker.BlocksWorldInput)
                return;

            HandleAimToggleInput();

            if (!isAimActive)
                return;

            if (!IsAimedFirearmStillEquipped())
            {
                DisableAim(true);
                return;
            }

            if (!TryUpdateAim())
                return;

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
                return;

            Vector2 mousePosition = mouse.position.ReadValue();
            if (uiInputBlocker != null && uiInputBlocker.ConsumeLeftClickIfNeeded(mousePosition))
                return;

            TryFire();
        }

        private void HandleAimToggleInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.fKey.wasPressedThisFrame)
                return;

            if (isAimActive)
            {
                DisableAim(true);
                return;
            }

            if (HasEquippedFirearm)
                EnableAim();
        }

        public bool EnableAim()
        {
            ResolveReferences();
            if (isAimActive)
                return true;

            if (!TryGetEquippedFirearm(out _, out _))
            {
                RecordFeedback(GameplayFeedbackEntryType.Warning, "No firearm equipped in right_hand.");
                return false;
            }

            ItemInstance equippedItem = inventory.GetRightHandItemInstance();
            aimedFirearmInstanceId = equippedItem != null ? equippedItem.InstanceId : null;
            isAimActive = true;

            PointClickMovementController movementController = GetComponent<PointClickMovementController>();
            if (movementController != null)
                movementController.ClearTarget();

            if (movementInput != null)
            {
                restoreMovementInput = movementInput.enabled;
                movementInput.enabled = false;
            }

            EnsureDebugLines();
            RecordFeedback(GameplayFeedbackEntryType.Info, "Rifle aim enabled.");
            return true;
        }

        public void DisableAim()
        {
            DisableAim(true);
        }

        private void DisableAim(bool recordFeedback)
        {
            if (!isAimActive)
                return;

            isAimActive = false;
            aimedFirearmInstanceId = null;
            hasCurrentAimSolution = false;

            if (movementInput != null && restoreMovementInput)
                movementInput.enabled = true;

            restoreMovementInput = false;

            if (aimLine != null)
                aimLine.enabled = false;

            if (recordFeedback)
                RecordFeedback(GameplayFeedbackEntryType.Info, "Rifle aim disabled.");
        }

        private bool TryUpdateAim()
        {
            if (!TryGetEquippedFirearm(out _, out FirearmProfileDefinition firearmProfile))
                return false;

            if (!TryCalculateAimSolution(firearmProfile, out AimSolution solution))
            {
                hasCurrentAimSolution = false;
                if (aimLine != null)
                    aimLine.enabled = false;
                return false;
            }

            currentAimSolution = solution;
            hasCurrentAimSolution = true;
            transform.rotation = Quaternion.LookRotation(solution.AimDirection, Vector3.up);

            float lineDistance = Mathf.Min(
                firearmProfile.range,
                Vector3.Distance(solution.MuzzleWorldPosition, solution.MouseAimTargetWorldPosition));
            SetLine(
                aimLine,
                solution.MuzzleWorldPosition,
                solution.MuzzleWorldPosition + solution.AimDirection * lineDistance,
                true);
            return true;
        }

        private void TryFire()
        {
            if (!TryGetEquippedFirearm(out ItemDefinition firearmItem, out FirearmProfileDefinition firearmProfile))
            {
                DisableAim(true);
                return;
            }

            if (!hasCurrentAimSolution)
                return;

            if (Time.time < nextFireTime)
            {
                RecordFeedback(GameplayFeedbackEntryType.Warning, "Rifle still cycling, cannot fire.", firearmItem);
                return;
            }

            if (!TryFindCompatibleAmmo(firearmProfile, out int ammoIndex, out ItemDefinition ammoItem, out AmmoProfileDefinition ammoProfile))
            {
                RecordFeedback(GameplayFeedbackEntryType.Warning, "No compatible ammo.", firearmItem);
                return;
            }

            if (!inventory.TryRemoveItemAt(ammoIndex, 1))
            {
                RecordFeedback(GameplayFeedbackEntryType.Warning, "No compatible ammo.", firearmItem);
                return;
            }

            RecordFeedback(GameplayFeedbackEntryType.Info, "Rifle fired.", firearmItem);
            RecordFeedback(GameplayFeedbackEntryType.ItemUsed, $"Consumed {GetDisplayName(ammoItem, ammoProfile.display_name)} x1.", ammoItem, 1);

            nextFireTime = Time.time + firearmProfile.cycle_time;
            RecordFeedback(GameplayFeedbackEntryType.Info, "Rifle cycling bolt.", firearmItem);

            AimSolution shot = currentAimSolution;
            bool hitSomething = TryGetFirstValidHit(
                shot.MuzzleWorldPosition,
                shot.AimDirection,
                firearmProfile.range,
                out RaycastHit hit);
            Vector3 tracerEndPoint = hitSomething
                ? hit.point
                : shot.MuzzleWorldPosition + shot.AimDirection * firearmProfile.range;
            ShowTracer(shot.MuzzleWorldPosition, tracerEndPoint);

            if (!hitSomething)
            {
                RecordFeedback(GameplayFeedbackEntryType.Info, "Rifle missed.", firearmItem);
                return;
            }

            string targetName = GetHitDisplayName(hit.collider);
            RecordFeedback(GameplayFeedbackEntryType.Info, $"Rifle hit target: {targetName}.", firearmItem);

            ActorHealthComponent health = hit.collider != null ? hit.collider.GetComponentInParent<ActorHealthComponent>() : null;
            if (health == null || !health.ApplyDamage(ammoProfile.damage))
                return;

            RecordFeedback(
                GameplayFeedbackEntryType.Info,
                $"Rifle damaged actor: {targetName} damage {ammoProfile.damage:0.#}.",
                firearmItem);
        }

        private bool TryFindCompatibleAmmo(
            FirearmProfileDefinition firearmProfile,
            out int ammoIndex,
            out ItemDefinition ammoItem,
            out AmmoProfileDefinition ammoProfile)
        {
            ammoIndex = -1;
            ammoItem = null;
            ammoProfile = null;

            if (inventory == null || firearmProfile == null || firearmProfile.accepted_ammo_profile_ids == null)
                return false;

            IReadOnlyList<ItemStorageEntry> entries = inventory.GetStorageEntries();
            for (int index = 0; index < entries.Count; index++)
            {
                ItemStorageEntry entry = entries[index];
                ItemDefinition candidateItem = GetItemDefinition(entry != null ? entry.DefinitionId : null);
                if (candidateItem == null || string.IsNullOrWhiteSpace(candidateItem.ammo_profile_id))
                    continue;

                if (!Contains(firearmProfile.accepted_ammo_profile_ids, candidateItem.ammo_profile_id))
                    continue;

                AmmoProfileDefinition candidateProfile = GetDatabase()?.GetAmmoProfile(candidateItem.ammo_profile_id);
                if (candidateProfile == null)
                    continue;

                ammoIndex = index;
                ammoItem = candidateItem;
                ammoProfile = candidateProfile;
                return true;
            }

            return false;
        }

        private bool TryGetEquippedFirearm(out ItemDefinition item, out FirearmProfileDefinition profile)
        {
            item = null;
            profile = null;

            ResolveReferences();
            if (inventory == null)
                return false;

            item = GetItemDefinition(inventory.GetRightHandItemDefinitionId());
            if (item == null || string.IsNullOrWhiteSpace(item.firearm_profile_id))
                return false;

            profile = GetDatabase()?.GetFirearmProfile(item.firearm_profile_id);
            return profile != null;
        }

        private bool IsAimedFirearmStillEquipped()
        {
            if (!TryGetEquippedFirearm(out _, out _))
                return false;

            ItemInstance equippedItem = inventory.GetRightHandItemInstance();
            return equippedItem != null && equippedItem.InstanceId == aimedFirearmInstanceId;
        }

        private bool TryCalculateAimSolution(FirearmProfileDefinition firearmProfile, out AimSolution solution)
        {
            solution = default;
            Mouse mouse = Mouse.current;
            if (mouse == null || inputCamera == null)
                return false;

            Vector2 mousePosition = mouse.position.ReadValue();
            Ray mouseRay = inputCamera.ScreenPointToRay(new Vector3(mousePosition.x, mousePosition.y, 0f));
            float aimPlaneHeight = transform.position.y + muzzleHeight;
            var aimPlane = new Plane(Vector3.up, new Vector3(0f, aimPlaneHeight, 0f));
            if (!aimPlane.Raycast(mouseRay, out float enter))
                return false;

            Vector3 mouseAimTargetWorldPosition = mouseRay.GetPoint(enter);
            Vector3 actorAimOrigin = new Vector3(transform.position.x, aimPlaneHeight, transform.position.z);
            Vector3 actorToTarget = mouseAimTargetWorldPosition - actorAimOrigin;
            actorToTarget.y = 0f;
            if (actorToTarget.sqrMagnitude < 0.001f)
                return false;

            Vector3 visualAimDirection = actorToTarget.normalized;
            float muzzleOffset = firearmProfile != null ? firearmProfile.muzzle_offset : 0.8f;
            Vector3 muzzleWorldPosition = actorAimOrigin + visualAimDirection * muzzleOffset;
            Vector3 aimVector = mouseAimTargetWorldPosition - muzzleWorldPosition;
            aimVector.y = 0f;
            if (aimVector.sqrMagnitude < 0.001f || Vector3.Dot(aimVector, visualAimDirection) <= 0f)
                return false;

            solution = new AimSolution(
                muzzleWorldPosition,
                mouseAimTargetWorldPosition,
                aimVector.normalized);
            return true;
        }

        private bool TryGetFirstValidHit(Vector3 origin, Vector3 direction, float range, out RaycastHit result)
        {
            int mask = hitLayerMask.value != 0 ? hitLayerMask.value : Physics.DefaultRaycastLayers;
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, range, mask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            for (int index = 0; index < hits.Length; index++)
            {
                Collider hitCollider = hits[index].collider;
                if (hitCollider == null || hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform))
                    continue;

                result = hits[index];
                return true;
            }

            result = default;
            return false;
        }

        private void ResolveReferences()
        {
            if (inventory == null)
                inventory = GetComponent<InventoryComponent>();

            if (inputCamera == null)
                inputCamera = Camera.main;

            if (uiInputBlocker == null)
                uiInputBlocker = FindAnyObjectByType<DebugWorldUiInputBlocker>();

            if (movementInput == null)
                movementInput = FindAnyObjectByType<PointClickMovementInputController>();
        }

        private void EnsureDebugLines()
        {
            if (aimLine == null)
                aimLine = CreateDebugLine("Firearm Aim Line", new Color(0.1f, 0.85f, 1f, 0.9f), 0.025f);

            if (tracerLine == null)
                tracerLine = CreateDebugLine("Firearm Tracer", new Color(1f, 0.8f, 0.1f, 1f), 0.06f);
        }

        private LineRenderer CreateDebugLine(string objectName, Color color, float width)
        {
            var lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(transform, false);

            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.numCapVertices = 2;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");

            if (shader != null)
                line.material = new Material(shader);

            line.enabled = false;
            return line;
        }

        private void ShowTracer(Vector3 start, Vector3 end)
        {
            EnsureDebugLines();
            SetLine(tracerLine, start, end, true);
            tracerEndTime = Time.time + Mathf.Max(0.02f, tracerDuration);
        }

        private void UpdateTracer()
        {
            if (tracerLine != null && tracerLine.enabled && Time.time >= tracerEndTime)
                tracerLine.enabled = false;
        }

        private static void SetLine(LineRenderer line, Vector3 start, Vector3 end, bool enabled)
        {
            if (line == null)
                return;

            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.enabled = enabled;
        }

        private void RecordFeedback(GameplayFeedbackEntryType type, string message, ItemDefinition item = null, int quantity = 0)
        {
            string itemDisplayName = item != null ? GetDisplayName(item, null) : null;
            GameplayFeedbackLog.TryRecord(new GameplayFeedbackEntry(
                type,
                message,
                actorId: name,
                actorDisplayName: name,
                itemId: item != null ? item.id : null,
                itemDisplayName: itemDisplayName,
                quantity: quantity,
                debugOnly: true));

            Debug.Log($"[FirearmDebugController] {message}");
        }

        private static string GetHitDisplayName(Collider hitCollider)
        {
            if (hitCollider == null)
                return "(none)";

            WorldObjectDebugInfo debugInfo = hitCollider.GetComponentInParent<WorldObjectDebugInfo>();
            return debugInfo != null
                ? debugInfo.GetDisplayNameOrFallback(hitCollider.name)
                : hitCollider.name;
        }

        private static GameDatabase GetDatabase()
        {
            return GameDataManager.Instance != null && GameDataManager.Instance.IsReady
                ? GameDataManager.Instance.Database
                : null;
        }

        private static ItemDefinition GetItemDefinition(string definitionId)
        {
            return string.IsNullOrWhiteSpace(definitionId) ? null : GetDatabase()?.GetItem(definitionId);
        }

        private static string GetDisplayName(ItemDefinition item, string fallback)
        {
            if (item != null && item.display != null && !string.IsNullOrWhiteSpace(item.display.name))
                return item.display.name;

            if (!string.IsNullOrWhiteSpace(fallback))
                return fallback;

            return item != null ? item.id : "(none)";
        }

        private static bool Contains(string[] values, string expected)
        {
            if (values == null || string.IsNullOrWhiteSpace(expected))
                return false;

            for (int index = 0; index < values.Length; index++)
            {
                if (values[index] == expected)
                    return true;
            }

            return false;
        }

        private readonly struct AimSolution
        {
            public AimSolution(
                Vector3 muzzleWorldPosition,
                Vector3 mouseAimTargetWorldPosition,
                Vector3 aimDirection)
            {
                MuzzleWorldPosition = muzzleWorldPosition;
                MouseAimTargetWorldPosition = mouseAimTargetWorldPosition;
                AimDirection = aimDirection;
            }

            public Vector3 MuzzleWorldPosition { get; }
            public Vector3 MouseAimTargetWorldPosition { get; }
            public Vector3 AimDirection { get; }
        }
    }
}
