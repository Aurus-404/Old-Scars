using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
    /// M29-compatible MonoBehaviour/GUID graduated as M40's sole player combat input adapter.
    /// Equipment, ItemInstance state, services and M39 remain the gameplay authorities.
    /// </summary>
    public sealed class FirearmDebugController : MonoBehaviour
    {
        private const float PhysicalShotOriginEpsilon = 0.02f;
        private const float SurfaceContinuationEpsilon = 0.001f;
        private const int MaxPenetratedSurfaces = 4;

        [SerializeField] private InventoryComponent inventory;
        [SerializeField] private Camera inputCamera;
        [SerializeField] private DebugWorldUiInputBlocker uiInputBlocker;
        [SerializeField] private PlayerMovementInputController movementInput;
        [SerializeField] private LayerMask hitLayerMask;
        [SerializeField] private float muzzleHeight;
        [SerializeField] private float tracerDuration = 0.12f;

        private ActorItemOwnershipComponent ownership;
        private DebugActionProgressController progressController;
        private bool isCombatActive;
        private string aimedWeaponInstanceId;
        private float nextAttackTime;
        private float tracerEndTime;
        private AimSolution currentAim;
        private LineRenderer aimLine;
        private LineRenderer tracerLine;

        public bool IsAimActive => isCombatActive;
        public bool PreservesMovementInput => movementInput == null || movementInput.enabled;
        public bool IsAttackReady => Time.time >= nextAttackTime;
        public bool HasEquippedFirearm => TryGetEquipped(out _, out _, out FirearmProfileDefinition firearm, out _) && firearm != null;
        public bool HasEquippedWeapon => TryGetEquipped(out _, out _, out _, out _);
        public string EquippedFirearmDisplayName => HasEquippedFirearm ? EquippedWeaponDisplayName : null;
        public string EquippedWeaponDisplayName
        {
            get
            {
                return TryGetEquipped(out _, out ItemDefinition definition, out FirearmProfileDefinition firearm, out _)
                    ? DisplayName(definition, firearm?.display_name)
                    : null;
            }
        }

#if UNITY_EDITOR
        public void DiagnosticStartCycle(float seconds)
        {
            nextAttackTime = Time.time + Mathf.Max(0f, seconds);
        }
#endif

        public string StatusText
        {
            get
            {
                if (!TryGetEquipped(out ItemInstance item, out _, out FirearmProfileDefinition firearm, out _))
                    return null;
                string mode = isCombatActive ? "Combat active" : "Combat disabled";
                if (firearm == null)
                    return $"{mode}. Melee ready.";
                float remaining = Mathf.Max(0f, nextAttackTime - Time.time);
                string cycle = remaining > 0f ? $" Bolt: {remaining:0.0}s." : " Ready.";
                return $"{mode}. Loaded {item.LoadedRounds}/{firearm.magazine_capacity}.{cycle}";
            }
        }

        private void Awake() => ResolveReferences();
        private void OnDisable() => SetCombatMode(false, false);

        private void Update()
        {
            UpdateTracer();
            ResolveReferences();
            if (uiInputBlocker != null && uiInputBlocker.BlocksWorldInput)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
                SetCombatMode(!isCombatActive, true);
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
                TryStartReload();

            if (!isCombatActive)
                return;
            if (!IsAimedWeaponStillEquipped())
            {
                SetCombatMode(false, true);
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
            TryAttack();
        }

        public bool EnableAim() => SetCombatMode(true, true);
        public void DisableAim() => SetCombatMode(false, true);

        public bool SetCombatMode(bool enabled, bool feedback = true)
        {
            ResolveReferences();
            if (enabled)
            {
                if (!TryGetEquipped(out ItemInstance item, out _, out _, out _))
                {
                    if (feedback) Record(GameplayFeedbackEntryType.Warning, "No weapon equipped in hand slots.");
                    return false;
                }
                aimedWeaponInstanceId = item.InstanceId;
                isCombatActive = true;
                EnsureLines();
                if (feedback) Record(GameplayFeedbackEntryType.Info, "Combat mode enabled.");
                return true;
            }

            if (!isCombatActive)
                return true;
            isCombatActive = false;
            aimedWeaponInstanceId = null;
            if (aimLine != null) aimLine.enabled = false;
            if (feedback) Record(GameplayFeedbackEntryType.Info, "Combat mode disabled.");
            return true;
        }

        public bool TryStartReload()
        {
            ResolveReferences();
            if (!TryGetEquipped(out ItemInstance item, out ItemDefinition definition,
                    out FirearmProfileDefinition firearm, out _) || firearm == null)
            {
                Record(GameplayFeedbackEntryType.Warning, "No firearm equipped for reload.");
                return false;
            }
            if (item.LoadedRounds >= firearm.magazine_capacity)
            {
                Record(GameplayFeedbackEntryType.Warning, "Firearm is already full.", definition);
                return false;
            }
            if (WeaponCombatService.GetCompatibleAmmoQuantity(ownership, item) <= 0)
            {
                Record(GameplayFeedbackEntryType.Warning, "No compatible owned ammo.", definition);
                return false;
            }
            string expectedId = item.InstanceId;
            bool started = progressController != null && progressController.TryStartTimedOperation(
                firearm.reload_duration,
                "Reload firearm",
                DisplayName(definition, firearm.display_name),
                () => CompleteReload(expectedId, definition));
            if (!started)
                Record(GameplayFeedbackEntryType.Warning, "Reload could not start because another action is active.", definition);
            return started;
        }

        private DebugActionExecutionResult CompleteReload(string expectedId, ItemDefinition definition)
        {
            WeaponCombatResult result = WeaponCombatService.ReloadEquipped(ownership, expectedId);
            Record(result.Success ? GameplayFeedbackEntryType.Info : GameplayFeedbackEntryType.Warning, result.Message, definition, result.Quantity);
            return DebugActionExecutionResult.Info("Reload", result.Message);
        }

        private void TryAttack()
        {
            if (Time.time < nextAttackTime)
            {
                Record(GameplayFeedbackEntryType.Warning, "Weapon action is still cycling.");
                return;
            }
            if (!TryGetEquipped(out ItemInstance item, out ItemDefinition definition,
                    out FirearmProfileDefinition firearm, out WeaponProfileDefinition melee))
                return;

            if (firearm != null)
            {
                WeaponCombatResult result = WeaponCombatService.FireEquipped(
                    ownership,
                    item.InstanceId,
                    penetrationPower => ResolvePhysicalShot(
                        currentAim.PhysicalOrigin,
                        currentAim.Direction,
                        firearm.range,
                        penetrationPower));
                if (result.Quantity == 1)
                {
                    nextAttackTime = Time.time + firearm.cycle_time;
                    Vector3 end = result.PhysicalShot.IsResolved
                        ? result.PhysicalShot.EndPoint
                        : currentAim.PhysicalOrigin + currentAim.Direction * firearm.range;
                    ShowTracer(currentAim.PhysicalOrigin, end);
                }
                Record(result.Success ? GameplayFeedbackEntryType.Info : GameplayFeedbackEntryType.Warning, result.Message, definition, result.Quantity);
                return;
            }

            if (melee == null)
                return;
            string expectedId = item.InstanceId;
            Vector3 origin = currentAim.VisualOrigin;
            Vector3 direction = currentAim.Direction;
            bool started = progressController != null && progressController.TryStartTimedOperation(
                melee.attack_duration,
                "Melee attack",
                DisplayName(definition, null),
                () => CompleteMelee(expectedId, definition, melee, origin, direction));
            if (started)
                nextAttackTime = Time.time + melee.attack_duration + melee.attack_cooldown;
        }

        private DebugActionExecutionResult CompleteMelee(
            string expectedId,
            ItemDefinition definition,
            WeaponProfileDefinition profile,
            Vector3 origin,
            Vector3 direction)
        {
            bool hit = TryFirstHit(origin, direction, profile.melee_range, out RaycastHit raycastHit);
            Vector3 end = hit ? raycastHit.point : origin + direction * profile.melee_range;
            WeaponCombatResult result = WeaponCombatService.StrikeEquipped(
                ownership, expectedId, hit ? raycastHit.collider : null, end);
            ShowTracer(origin, end);
            Record(result.Success ? GameplayFeedbackEntryType.Info : GameplayFeedbackEntryType.Warning, result.Message, definition);
            return DebugActionExecutionResult.Info("Melee", result.Message);
        }

        private bool TryUpdateAim()
        {
            if (!TryGetEquipped(out _, out _, out FirearmProfileDefinition firearm, out WeaponProfileDefinition melee))
                return false;
            float range = firearm != null ? firearm.range : melee.melee_range;
            float visualOffset = firearm != null ? firearm.muzzle_offset : 0.45f;
            if (!TryCalculateAim(visualOffset, range, out AimSolution solution))
                return false;
            currentAim = solution;
            Vector3 flat = solution.Direction;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            Vector3 visualDirection = solution.Target - solution.VisualOrigin;
            float visualDistance = Mathf.Min(range, visualDirection.magnitude);
            SetLine(aimLine, solution.VisualOrigin,
                solution.VisualOrigin + visualDirection.normalized * visualDistance, true);
            return true;
        }

        private bool TryCalculateAim(float visualForwardOffset, float range, out AimSolution solution)
        {
            solution = default;
            Mouse mouse = Mouse.current;
            if (mouse == null || inputCamera == null)
                return false;
            Vector2 position = mouse.position.ReadValue();
            Ray cameraRay = inputCamera.ScreenPointToRay(position);
            Vector3 target;
            if (TryFirstHit(cameraRay.origin, cameraRay.direction, Mathf.Max(range, inputCamera.farClipPlane), out RaycastHit cameraHit))
                target = cameraHit.point;
            else
            {
                float height = ResolveMuzzleHeight();
                var plane = new Plane(Vector3.up, new Vector3(0f, height, 0f));
                if (!plane.Raycast(cameraRay, out float enter))
                    return false;
                target = cameraRay.GetPoint(enter);
            }

            return TryBuildAimSolution(target, visualForwardOffset, out solution);
        }

        private bool TryBuildAimSolution(Vector3 target, float visualForwardOffset, out AimSolution solution)
        {
            solution = default;
            Vector3 actorOrigin = new Vector3(transform.position.x, ResolveMuzzleHeight(), transform.position.z);
            Vector3 flatForward = target - actorOrigin;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.001f)
                return false;

            Vector3 flatDirection = flatForward.normalized;
            float targetDistance = Vector3.Distance(actorOrigin, target);
            float epsilon = Mathf.Min(PhysicalShotOriginEpsilon, targetDistance * 0.1f);
            Vector3 physicalOrigin = actorOrigin + flatDirection * epsilon;
            Vector3 visualOrigin = actorOrigin + flatDirection * Mathf.Max(0f, visualForwardOffset);
            Vector3 direction = target - physicalOrigin;
            if (direction.sqrMagnitude < 0.001f)
                return false;
            solution = new AimSolution(target, physicalOrigin, visualOrigin, direction.normalized);
            return true;
        }

#if UNITY_EDITOR
        public bool DiagnosticResolvePhysicalShot(
            Vector3 desiredTarget,
            float range,
            out Collider firstCollider,
            out Vector3 hitPoint,
            out Vector3 physicalOrigin)
        {
            firstCollider = null;
            hitPoint = desiredTarget;
            physicalOrigin = default;
            if (!TryBuildAimSolution(desiredTarget, 0f, out AimSolution solution))
                return false;

            physicalOrigin = solution.PhysicalOrigin;
            if (!TryFirstHit(solution.PhysicalOrigin, solution.Direction, range, out RaycastHit hit))
                return true;
            firstCollider = hit.collider;
            hitPoint = hit.point;
            return true;
        }

        public bool DiagnosticResolvePenetratingShot(
            Vector3 desiredTarget,
            float range,
            float penetrationPower,
            out PhysicalShotResolution resolution,
            out Vector3 physicalOrigin)
        {
            resolution = default;
            physicalOrigin = default;
            if (!TryBuildAimSolution(desiredTarget, 0f, out AimSolution solution))
                return false;
            physicalOrigin = solution.PhysicalOrigin;
            resolution = ResolvePhysicalShot(
                solution.PhysicalOrigin,
                solution.Direction,
                range,
                penetrationPower);
            return resolution.IsResolved;
        }
#endif

        private float ResolveMuzzleHeight()
        {
            if (muzzleHeight > 0f)
                return transform.position.y + muzzleHeight;
            Collider actorCollider = GetComponentInChildren<Collider>();
            return actorCollider != null ? actorCollider.bounds.center.y : transform.position.y + 1f;
        }

        private bool TryFirstHit(Vector3 origin, Vector3 direction, float range, out RaycastHit result)
        {
            int mask = hitLayerMask.value != 0 ? hitLayerMask.value : Physics.DefaultRaycastLayers;
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, range, mask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                Collider collider = hit.collider;
                if (collider == null || collider.transform == transform || collider.transform.IsChildOf(transform))
                    continue;
                result = hit;
                return true;
            }
            result = default;
            return false;
        }

        private PhysicalShotResolution ResolvePhysicalShot(
            Vector3 physicalOrigin,
            Vector3 direction,
            float range,
            float penetrationPower)
        {
            var ignoredColliders = new HashSet<Collider>();
            var penetratedSurfaceOwners = new HashSet<WorldObjectProfileComponent>();
            Vector3 currentOrigin = physicalOrigin;
            float remainingRange = Mathf.Max(0f, range);
            float remainingPower = penetrationPower;
            int penetratedSurfaces = 0;
            PenetrationResolution lastSurface = default;

            while (remainingRange > 0f)
            {
                if (!TryNextPhysicalHit(
                        currentOrigin,
                        direction,
                        remainingRange,
                        ignoredColliders,
                        out RaycastHit hit))
                {
                    return new PhysicalShotResolution(
                        PhysicalShotTermination.Miss,
                        null,
                        currentOrigin + direction * remainingRange,
                        penetrationPower,
                        remainingPower,
                        penetratedSurfaces,
                        lastSurface);
                }

                Collider collider = hit.collider;
                if (collider.GetComponentInParent<ActorHealthComponent>() != null)
                {
                    return new PhysicalShotResolution(
                        PhysicalShotTermination.Impact,
                        collider,
                        hit.point,
                        penetrationPower,
                        remainingPower,
                        penetratedSurfaces,
                        lastSurface);
                }

                WorldObjectProfileComponent surface = collider.GetComponentInParent<WorldObjectProfileComponent>();
                if (surface == null || !surface.TryGetPenetrationProfile(out PenetrationProfileDefinition profile))
                {
                    return new PhysicalShotResolution(
                        PhysicalShotTermination.Impact,
                        collider,
                        hit.point,
                        penetrationPower,
                        remainingPower,
                        penetratedSurfaces,
                        lastSurface);
                }

                if (penetratedSurfaceOwners.Contains(surface))
                {
                    // A compound collider belonging to an already-resolved
                    // surface is not another resistance layer. Actor receivers
                    // were checked first, preserving a future internal receiver.
                    ignoredColliders.Add(collider);
                    float duplicateAdvance = hit.distance + SurfaceContinuationEpsilon;
                    remainingRange = Mathf.Max(0f, remainingRange - duplicateAdvance);
                    currentOrigin = hit.point + direction * SurfaceContinuationEpsilon;
                    continue;
                }

                if (penetratedSurfaces >= MaxPenetratedSurfaces)
                {
                    return new PhysicalShotResolution(
                        PhysicalShotTermination.SurfaceLimitStopped,
                        null,
                        hit.point,
                        penetrationPower,
                        remainingPower,
                        penetratedSurfaces,
                        lastSurface,
                        profile.id);
                }

                lastSurface = PenetrationResolutionService.Resolve(
                    remainingPower,
                    new[]
                    {
                        new PenetrationLayer(
                            "world_surface_" + RuntimeHelpers.GetHashCode(surface),
                            profile.id,
                            0,
                            profile.resistance)
                    });
                if (lastSurface.Outcome == PenetrationOutcome.Stopped)
                {
                    return new PhysicalShotResolution(
                        PhysicalShotTermination.SurfaceStopped,
                        null,
                        hit.point,
                        penetrationPower,
                        0f,
                        penetratedSurfaces,
                        lastSurface,
                        profile.id);
                }

                remainingPower = lastSurface.ResidualPower;
                penetratedSurfaces++;
                ignoredColliders.Add(collider);
                penetratedSurfaceOwners.Add(surface);
                float advance = hit.distance + SurfaceContinuationEpsilon;
                remainingRange = Mathf.Max(0f, remainingRange - advance);
                currentOrigin = hit.point + direction * SurfaceContinuationEpsilon;
            }

            return new PhysicalShotResolution(
                PhysicalShotTermination.Miss,
                null,
                currentOrigin,
                penetrationPower,
                remainingPower,
                penetratedSurfaces,
                lastSurface);
        }

        private bool TryNextPhysicalHit(
            Vector3 origin,
            Vector3 direction,
            float range,
            ISet<Collider> ignoredColliders,
            out RaycastHit result)
        {
            int mask = hitLayerMask.value != 0 ? hitLayerMask.value : Physics.DefaultRaycastLayers;
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, range, mask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                Collider collider = hit.collider;
                if (collider == null || collider.transform == transform || collider.transform.IsChildOf(transform) ||
                    ignoredColliders.Contains(collider))
                    continue;
                result = hit;
                return true;
            }
            result = default;
            return false;
        }

        private bool TryGetEquipped(
            out ItemInstance item,
            out ItemDefinition definition,
            out FirearmProfileDefinition firearm,
            out WeaponProfileDefinition melee)
        {
            ResolveReferences();
            return WeaponCombatService.TryGetEquippedWeapon(ownership, out item, out definition, out firearm, out melee);
        }

        private bool IsAimedWeaponStillEquipped() =>
            TryGetEquipped(out ItemInstance item, out _, out _, out _) && item.InstanceId == aimedWeaponInstanceId;

        private void ResolveReferences()
        {
            if (inventory == null) inventory = GetComponent<InventoryComponent>();
            if (ownership == null) ownership = GetComponent<ActorItemOwnershipComponent>();
            if (inputCamera == null) inputCamera = Camera.main;
            if (uiInputBlocker == null) uiInputBlocker = FindAnyObjectByType<DebugWorldUiInputBlocker>();
            if (movementInput == null) movementInput = GetComponent<PlayerMovementInputController>();
            if (progressController == null) progressController = GetComponent<DebugActionProgressController>() ?? FindAnyObjectByType<DebugActionProgressController>();
        }

        private void EnsureLines()
        {
            if (aimLine == null) aimLine = CreateLine("Combat Aim Line", new Color(0.1f, 0.85f, 1f, 0.9f), 0.025f);
            if (tracerLine == null) tracerLine = CreateLine("Combat Trace", new Color(1f, 0.8f, 0.1f, 1f), 0.06f);
        }

        private LineRenderer CreateLine(string objectName, Color color, float width)
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
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null) line.material = new Material(shader);
            line.enabled = false;
            return line;
        }

        private void ShowTracer(Vector3 start, Vector3 end)
        {
            EnsureLines();
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
            if (line == null) return;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.enabled = enabled;
        }

        private void Record(GameplayFeedbackEntryType type, string message, ItemDefinition item = null, int quantity = 0)
        {
            GameplayFeedbackLog.TryRecord(new GameplayFeedbackEntry(
                type,
                message,
                actorId: name,
                actorDisplayName: name,
                itemId: item?.id,
                itemDisplayName: DisplayName(item, null),
                quantity: quantity,
                debugOnly: true));
            if (type == GameplayFeedbackEntryType.Warning) Debug.LogWarning("[Combat][PLAYER] " + message);
            else Debug.Log("[Combat][PLAYER] " + message);
        }

        private static string DisplayName(ItemDefinition item, string fallback)
        {
            if (item?.display != null && !string.IsNullOrWhiteSpace(item.display.name)) return item.display.name;
            return !string.IsNullOrWhiteSpace(fallback) ? fallback : item?.id ?? "<NONE>";
        }

        private readonly struct AimSolution
        {
            public AimSolution(Vector3 target, Vector3 physicalOrigin, Vector3 visualOrigin, Vector3 direction)
            {
                Target = target;
                PhysicalOrigin = physicalOrigin;
                VisualOrigin = visualOrigin;
                Direction = direction;
            }

            public Vector3 Target { get; }
            public Vector3 PhysicalOrigin { get; }
            public Vector3 VisualOrigin { get; }
            public Vector3 Direction { get; }
        }
    }
}
