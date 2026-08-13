using System;
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
                bool hit = TryFirstHit(currentAim.Origin, currentAim.Direction, firearm.range, out RaycastHit raycastHit);
                Vector3 end = hit ? raycastHit.point : currentAim.Origin + currentAim.Direction * firearm.range;
                WeaponCombatResult result = WeaponCombatService.FireEquipped(
                    ownership, item.InstanceId, hit ? raycastHit.collider : null, end);
                if (result.Code != WeaponCombatCode.Unloaded && result.Code != WeaponCombatCode.NotEquipped &&
                    result.Code != WeaponCombatCode.InvalidWeapon)
                {
                    nextAttackTime = Time.time + firearm.cycle_time;
                    ShowTracer(currentAim.Origin, end);
                }
                Record(result.Success ? GameplayFeedbackEntryType.Info : GameplayFeedbackEntryType.Warning, result.Message, definition, result.Quantity);
                return;
            }

            if (melee == null)
                return;
            string expectedId = item.InstanceId;
            Vector3 origin = currentAim.Origin;
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
            float offset = firearm != null ? firearm.muzzle_offset : 0.45f;
            if (!TryCalculateAim(offset, range, out AimSolution solution))
                return false;
            currentAim = solution;
            Vector3 flat = solution.Direction;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            SetLine(aimLine, solution.Origin, solution.Origin + solution.Direction * range, true);
            return true;
        }

        private bool TryCalculateAim(float forwardOffset, float range, out AimSolution solution)
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

            float muzzleY = ResolveMuzzleHeight();
            Vector3 actorOrigin = new Vector3(transform.position.x, muzzleY, transform.position.z);
            Vector3 flatForward = target - actorOrigin;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.001f)
                return false;
            Vector3 origin = actorOrigin + flatForward.normalized * forwardOffset;
            Vector3 direction = target - origin;
            if (direction.sqrMagnitude < 0.001f)
                return false;
            solution = new AimSolution(origin, direction.normalized);
            return true;
        }

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
            public AimSolution(Vector3 origin, Vector3 direction) { Origin = origin; Direction = direction; }
            public Vector3 Origin { get; }
            public Vector3 Direction { get; }
        }
    }
}
