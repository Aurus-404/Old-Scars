using System;
using OldScars.Core.Actors;
using OldScars.Core.Identity;
using OldScars.Core.Interactions;
using OldScars.Core.Items;
using OldScars.Core.Visuals;
using UnityEngine;
using UnityEngine.AI;

namespace OldScars.Core.ApplicationShell
{
    /// <summary>
    /// Authored player/camera composition shared by the SampleScene laboratory
    /// and the product WorldRuntime. This marker owns composition wiring only;
    /// individual gameplay components remain their established authorities.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerGameplayComposition : MonoBehaviour
    {
        public const string ResourcePath = "PFB_PlayerGameplayComposition";

        [SerializeField] private ActorInteractionContext playerContext;
        [SerializeField] private ActorProfileComponent playerProfile;
        [SerializeField] private ActorRuntimeIdentity playerIdentity;
        [SerializeField] private PlayerMovementController movementController;
        [SerializeField] private PlayerMovementInputController movementInput;
        [SerializeField] private CameraRigController cameraRig;
        [SerializeField] private Camera gameplayCamera;

        private bool cameraBound;

        public ActorInteractionContext PlayerContext => playerContext;
        public Transform PlayerTransform => playerContext != null ? playerContext.transform : null;
        public ActorProfileComponent PlayerProfile => playerProfile;
        public ActorRuntimeIdentity PlayerIdentity => playerIdentity;
        public PlayerMovementController MovementController => movementController;
        public ActorStaminaComponent Stamina => playerContext != null ? playerContext.GetComponent<ActorStaminaComponent>() : null;
        public PlayerMovementInputController MovementInput => movementInput;
        public CameraRigController CameraRig => cameraRig;
        public Camera GameplayCamera => gameplayCamera;
        public PersistentSceneObjectId PersistentIdentity =>
            playerContext != null ? playerContext.GetComponent<PersistentSceneObjectId>() : null;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            if (!cameraBound)
                BindCameraToPlayer();
        }

        public static bool TryInstantiateAtSurface(
            Vector3 terrainSurfacePosition,
            Transform parent,
            out PlayerGameplayComposition composition,
            out string failure)
        {
            composition = null;
            failure = null;
            PlayerGameplayComposition prefab = Resources.Load<PlayerGameplayComposition>(ResourcePath);
            if (prefab == null)
            {
                failure = $"Shared player composition Resources/{ResourcePath} was not found.";
                return false;
            }

            PlayerGameplayComposition instance = null;
            try
            {
                instance = Instantiate(prefab, parent);
                instance.name = "Player Gameplay Composition";
                instance.ResolveReferences();
                if (!instance.TryValidateStructure(out failure))
                    throw new InvalidOperationException(failure);

                instance.PlacePlayerAtSurface(terrainSurfacePosition, Quaternion.identity);
                if (!instance.playerProfile.TryApplyRuntimeBootstrap(
                        instance.playerProfile.ActorProfileId, out failure))
                {
                    throw new InvalidOperationException(
                        "Player actor-profile bootstrap failed: " + failure);
                }

                instance.ResolveReferences();
                if (!instance.TryValidateRuntime(out failure))
                    throw new InvalidOperationException(failure);
                Physics.SyncTransforms();
                composition = instance;
                return true;
            }
            catch (Exception exception)
            {
                failure = string.IsNullOrWhiteSpace(exception.Message)
                    ? exception.GetType().Name
                    : exception.Message;
                if (instance != null)
                {
                    if (Application.isPlaying) Destroy(instance.gameObject);
                    else DestroyImmediate(instance.gameObject);
                }
                return false;
            }
        }

        public bool TryValidateStructure(out string failure)
        {
            ResolveReferences();
            failure = null;
            if (GetComponentsInChildren<ActorInteractionContext>(true).Length != 1 || playerContext == null)
                return Fail("Composition must contain exactly one ActorInteractionContext.", out failure);
            if (Array.IndexOf(playerContext.ActorTags, "player") < 0)
                return Fail("The authored ActorInteractionContext does not own the player role.", out failure);
            if (playerProfile == null || string.IsNullOrWhiteSpace(playerProfile.ActorProfileId))
                return Fail("Player ActorProfileComponent/profile identity is missing.", out failure);
            if (playerIdentity == null)
                return Fail("Player ActorRuntimeIdentity component is missing.", out failure);
            if (PersistentIdentity == null || !PersistentSceneObjectId.IsValidFormat(PersistentIdentity.PersistentId))
                return Fail("Player PersistentSceneObjectId is missing or invalid.", out failure);
            if (movementController == null || movementInput == null ||
                playerContext.GetComponent<CharacterController>() == null)
                return Fail("Player movement composition is incomplete.", out failure);
            if (playerContext.GetComponent<InventoryComponent>() == null ||
                playerContext.GetComponent<ActorEquipmentComponent>() == null ||
                playerContext.GetComponent<ActorItemOwnershipComponent>() == null ||
                playerContext.GetComponent<ActorCarryWeightComponent>() == null ||
                playerContext.GetComponent<ActorHealthComponent>() == null ||
                playerContext.GetComponent<ActorMedicalStateComponent>() == null ||
                playerContext.GetComponent<ActorNeedsComponent>() == null ||
                playerContext.GetComponent<ActorStaminaComponent>() == null)
                return Fail("Player inventory/equipment/ownership/health/medical/needs/stamina composition is incomplete.", out failure);
            if (playerContext.GetComponentInChildren<EntityVisualRigRuntime>(true) == null ||
                playerContext.GetComponentInChildren<EntityEquipmentVisualSynchronizer>(true) == null ||
                playerContext.GetComponent<ActorVisualAnimatorDriver>() == null)
                return Fail("Player visual-rig/animation composition is incomplete.", out failure);
            if (cameraRig == null || gameplayCamera == null ||
                !gameplayCamera.transform.IsChildOf(cameraRig.transform) ||
                !gameplayCamera.CompareTag("MainCamera"))
                return Fail("Gameplay CameraRig/Main Camera composition is incomplete.", out failure);
            if (GetComponentsInChildren<CameraRigController>(true).Length != 1 ||
                GetComponentsInChildren<Camera>(true).Length != 1)
                return Fail("Composition must contain exactly one CameraRigController and one Camera.", out failure);
            if (playerContext.GetComponent<NavMeshAgent>() != null ||
                playerContext.GetComponent<ActorNavigationController>() != null)
                return Fail("Player composition cannot acquire the NPC navigation authority.", out failure);
            return true;
        }

        public bool TryValidateRuntime(out string failure)
        {
            if (!TryValidateStructure(out failure))
                return false;
            if (!playerProfile.ProfileApplied)
                return Fail("Player ActorProfileComponent has not applied its authoritative profile.", out failure);
            if (!playerIdentity.IsRegistered ||
                playerIdentity.OriginKind != ActorOriginKind.Authored ||
                playerIdentity.ActorProfileId != playerProfile.ActorProfileId)
                return Fail("Player authored ActorInstanceId/profile registration is incomplete.", out failure);
            return true;
        }

        public void PlacePlayerAtSurface(Vector3 terrainSurfacePosition, Quaternion rotation)
        {
            ResolveReferences();
            if (playerContext == null)
                throw new InvalidOperationException("Player composition has no player transform.");
            CharacterController controller = playerContext.GetComponent<CharacterController>();
            float clearance = controller != null
                ? Mathf.Max(0f, controller.height * 0.5f - controller.center.y + controller.skinWidth)
                : 0f;
            bool controllerEnabled = controller != null && controller.enabled;
            if (controllerEnabled) controller.enabled = false;
            movementController?.ClearMovement();
            playerContext.transform.SetPositionAndRotation(
                terrainSurfacePosition + Vector3.up * clearance,
                rotation);
            if (controllerEnabled) controller.enabled = true;
        }

        public void BindCameraToPlayer()
        {
            ResolveReferences();
            if (cameraRig == null || playerContext == null)
                throw new InvalidOperationException("CameraRig cannot bind without the shared player composition.");
            cameraRig.SetFollowTarget(playerContext.transform);
            cameraBound = true;
        }

        public void SetGameplayInputEnabled(bool enabled)
        {
            ResolveReferences();
            if (movementInput != null) movementInput.enabled = enabled;
            if (movementController != null)
            {
                movementController.enabled = enabled;
                if (!enabled) movementController.ClearMovement();
            }
            if (cameraRig != null) cameraRig.enabled = enabled;
        }

        private void ResolveReferences()
        {
            if (playerContext == null)
                playerContext = GetComponentInChildren<ActorInteractionContext>(true);
            if (playerContext != null)
            {
                if (playerProfile == null) playerProfile = playerContext.GetComponent<ActorProfileComponent>();
                if (playerIdentity == null) playerIdentity = playerContext.GetComponent<ActorRuntimeIdentity>();
                if (movementController == null) movementController = playerContext.GetComponent<PlayerMovementController>();
                if (movementInput == null) movementInput = playerContext.GetComponent<PlayerMovementInputController>();
            }
            if (cameraRig == null)
                cameraRig = GetComponentInChildren<CameraRigController>(true);
            if (gameplayCamera == null)
                gameplayCamera = GetComponentInChildren<Camera>(true);
        }

        private static bool Fail(string message, out string failure)
        {
            failure = message;
            return false;
        }
    }
}
