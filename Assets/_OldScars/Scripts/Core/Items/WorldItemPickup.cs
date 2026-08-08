using System.Collections.Generic;
using OldScars.Core.Actors;
using OldScars.Core.Data;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Feedback;
using OldScars.Core.Interactions;
using UnityEngine;

namespace OldScars.Core.Items
{
    /// <summary>
    /// Minimal world item pickup bridge for configured and runtime-dropped items.
    ///
    /// This is not a final loot, ownership, persistence, or world item system.
    /// It owns one runtime ItemStorage entry so pickup can reuse the same
    /// instance-preserving transfer rules as inventories and containers.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class WorldItemPickup : MonoBehaviour, IGridStorageOwner, IGridStorageTransferEndpoint
    {
        private const string PickupableTag = "pickupable";
        private const string PickedUpTag = "picked_up";
        private static readonly Vector3 DefaultColliderCenter = new Vector3(0f, 0.25f, 0f);
        private static readonly Vector3 DefaultColliderSize = new Vector3(0.8f, 0.5f, 0.8f);

        [SerializeField] private string itemDefinitionId;
        [SerializeField] private string authoredItemInstanceId;

        private readonly ItemStorage storage = new ItemStorage();
        private GridInventoryBackend transferBackend;
        private bool destroyAfterPickup;
        private bool sourceInitialized;

        public string ItemDefinitionId
        {
            get
            {
                ItemStorageEntry entry = storage.GetEntry(0);
                return entry != null ? entry.DefinitionId : itemDefinitionId;
            }
        }

        public string AuthoredItemInstanceId => authoredItemInstanceId;

        public int Quantity
        {
            get
            {
                ItemStorageEntry entry = storage.GetEntry(0);
                return entry != null ? entry.Quantity : 0;
            }
        }

        public string GridStorageDisplayName => ItemDefinitionId;
        public IReadOnlyList<ItemStorageEntry> GridStorageEntries => storage.Entries;
        internal int ContentVersion => storage.Version;
        internal GridInventoryBackend TransactionBackend => GetTransferBackend();
        public bool UsesGridLayout => false;
        public int GridWidth => 0;
        public int GridHeight => 0;
        public int ConfiguredGridWidth => 0;
        public int ConfiguredGridHeight => 0;
        public GridStorageInitializationState GridInitializationState => GridStorageInitializationState.Disabled;
        public string GridInitializationError => null;

        GridInventoryBackend IGridStorageTransferEndpoint.TransferBackend => GetTransferBackend();

        private void Awake()
        {
            EnsureSimplePhysics();
        }

        private void Start()
        {
            WorldItemDebugVisualBuilder.Build(transform, ItemDefinitionId);
        }

        public int ReceiveDroppedItem(InventoryComponent sourceInventory, int sourceIndex, int quantity)
        {
            if (sourceInventory == null || !storage.IsEmpty || quantity < 1)
                return 0;

            ItemStorageEntry sourceEntry = sourceInventory.GetEntry(sourceIndex);
            string sourceInstanceId = sourceEntry?.Item?.InstanceId;
            if (string.IsNullOrWhiteSpace(sourceInstanceId))
                return 0;

            InventoryMutationResult result = GridStorageTransferService.TransferQuantityAuto(
                sourceInventory,
                this,
                sourceInstanceId,
                quantity,
                true,
                GridStorageTransferQuantityPolicy.Exact,
                default);
            if (!result.Success || result.AffectedQuantity < 1)
                return 0;

            ItemStorageEntry entry = storage.GetEntry(0);
            itemDefinitionId = entry != null ? entry.DefinitionId : itemDefinitionId;
            sourceInitialized = true;
            destroyAfterPickup = true;
            return result.AffectedQuantity;
        }

        public int ReceiveDroppedItem(IGridStorageOwner sourceOwner, string sourceInstanceId, int quantity)
        {
            if (sourceOwner == null || !storage.IsEmpty || quantity < 1)
            {
                return 0;
            }

            InventoryMutationResult result = GridStorageTransferService.TransferQuantityAuto(
                sourceOwner,
                this,
                sourceInstanceId,
                quantity,
                true,
                GridStorageTransferQuantityPolicy.Exact,
                default);
            if (!result.Success || result.AffectedQuantity < 1)
                return 0;

            ItemStorageEntry entry = storage.GetEntry(0);
            itemDefinitionId = entry != null ? entry.DefinitionId : itemDefinitionId;
            sourceInitialized = true;
            destroyAfterPickup = true;
            return result.AffectedQuantity;
        }

        public int ReceiveDroppedEquipment(ActorEquipmentComponent sourceEquipment, string sourceInstanceId)
        {
            if (sourceEquipment == null || !storage.IsEmpty || string.IsNullOrWhiteSpace(sourceInstanceId))
                return 0;

            EquipmentStorageTransferPlan plan = sourceEquipment.PreviewTransferEquippedToStorage(
                sourceInstanceId,
                this,
                default);
            EquipmentMutationResult result = sourceEquipment.TransferEquippedToStorage(
                this,
                plan,
                default);
            if (!result.Success)
                return 0;

            ItemStorageEntry entry = storage.GetEntry(0);
            itemDefinitionId = entry != null ? entry.DefinitionId : itemDefinitionId;
            sourceInitialized = true;
            destroyAfterPickup = true;
            return entry != null ? entry.Quantity : 0;
        }

        public bool TryGetEntryByInstanceId(string instanceId, out int index, out ItemStorageEntry entry)
        {
            index = storage.GetEntryIndexByInstanceId(instanceId);
            entry = index >= 0 ? storage.GetEntry(index) : null;
            return entry?.Item != null;
        }

        public bool TryGetGridPlacement(string instanceId, out GridPlacement placement)
        {
            placement = null;
            return false;
        }

        public bool TryGetGridFootprint(string definitionId, out GridFootprint footprint, out bool usedFallback)
        {
            return GridFootprint.TryResolve(GetItemDefinition(definitionId), out footprint, out usedFallback, out _);
        }

        public GridPlacementValidationResult PreviewGridPlacementMove(string instanceId, int x, int y, bool isRotated)
        {
            return GridPlacementValidationResult.Invalid(
                InventoryMutationResult.MutationFailure.InvalidArguments,
                "World item storage is linear.");
        }

        public InventoryMutationResult MoveGridPlacement(string instanceId, int x, int y, bool isRotated)
        {
            return InventoryMutationResult.Rejected(
                InventoryMutationResult.MutationFailure.InvalidArguments,
                "World item storage is linear.",
                0,
                instanceId);
        }

        public bool IsInstanceEquipped(string instanceId)
        {
            return false;
        }

        bool IGridStorageTransferEndpoint.CanTransferOut(GridStorageTransferContext context, out string reason)
        {
            reason = null;
            return true;
        }

        bool IGridStorageTransferEndpoint.CanTransferIn(GridStorageTransferContext context, out string reason)
        {
            reason = storage.IsEmpty ? null : "World item storage already contains an item.";
            return storage.IsEmpty;
        }

        void IGridStorageTransferEndpoint.OnTransferCommittedOut(GridStorageTransferReceipt receipt, GridStorageTransferContext context)
        {
        }

        void IGridStorageTransferEndpoint.OnTransferCommittedIn(GridStorageTransferReceipt receipt, GridStorageTransferContext context)
        {
        }

        public DebugActionExecutionResult PickUp(ActorInteractionContext actorContext, WorldObjectTags targetTags)
        {
            if (actorContext == null)
            {
                Debug.LogWarning("[WorldItemPickup] Cannot pick up item without an actor context.");
                return DebugActionExecutionResult.Info("Recoger", "No hay actor configurado para recoger este objeto.");
            }

            if (targetTags == null)
            {
                Debug.LogWarning("[WorldItemPickup] Cannot pick up item without target tags.");
                return DebugActionExecutionResult.Info("Recoger", "Este objeto no tiene tags de mundo configurados.");
            }

            if (targetTags.HasTag(PickedUpTag) || !targetTags.HasTag(PickupableTag))
                return DebugActionExecutionResult.Info("Recoger", "Este objeto ya fue recogido o no se puede recoger.");

            if (!EnsureConfiguredItemStorage(out string initializationError))
                return DebugActionExecutionResult.Info("Recoger", initializationError);

            InventoryComponent inventory = actorContext.GetInventoryComponent();
            if (inventory == null)
            {
                Debug.LogWarning("[WorldItemPickup] Actor has no InventoryComponent. Add InventoryComponent to the Debug Player for Milestone 14 pickup tests.");
                return DebugActionExecutionResult.Info("Recoger", "El actor no tiene inventario v0 configurado.");
            }

            ItemStorageEntry pickupEntry = storage.GetEntry(0);
            ItemInstance item = pickupEntry != null ? pickupEntry.Item : null;
            int pickupQuantity = pickupEntry != null ? pickupEntry.Quantity : 0;
            if (item == null || pickupQuantity < 1)
                return DebugActionExecutionResult.Info("Recoger", $"No se pudo recoger '{SafeText(itemDefinitionId)}'.");

            InventoryMutationResult transferResult = GridStorageTransferService.TransferQuantityAuto(
                this,
                inventory,
                item.InstanceId,
                pickupQuantity,
                true,
                GridStorageTransferQuantityPolicy.Exact,
                default);
            if (!transferResult.Success)
            {
                string failureMessage = transferResult.Failure == InventoryMutationResult.MutationFailure.CarryWeightLimitExceeded
                    ? transferResult.Message ?? "Too heavy."
                    : $"No se pudo recoger '{SafeText(itemDefinitionId)}'.";
                return DebugActionExecutionResult.Info("Recoger", failureMessage);
            }

            int transferredQuantity = transferResult.AffectedQuantity;
            return FinalizeCommittedPickup(
                actorContext,
                targetTags,
                item,
                transferredQuantity,
                "Recoger",
                "Recogiste");
        }

        internal bool TryPrepareTransactionSource(out ItemStorageEntry entry, out string error)
        {
            entry = null;
            error = null;
            if (!EnsureConfiguredItemStorage(out string initializationError))
            {
                error = initializationError;
                return false;
            }

            entry = storage.GetEntry(0);
            if (entry?.Item == null || entry.Quantity < 1)
            {
                error = "La fuente mundial no contiene una instancia transferible.";
                return false;
            }

            return true;
        }

        internal bool TryValidateTransactionSource(
            string expectedInstanceId,
            string expectedDefinitionId,
            int expectedQuantity,
            int expectedContentVersion,
            out ItemStorageEntry entry,
            out string error)
        {
            entry = null;
            error = null;
            if (this == null || !isActiveAndEnabled || gameObject == null || !gameObject.activeInHierarchy)
            {
                error = "El objeto mundial ya no existe o no estÃ¡ activo.";
                return false;
            }

            if (storage.Version != expectedContentVersion)
            {
                error = "La cantidad o el estado de la fuente mundial cambiÃ³.";
                return false;
            }

            if (!TryGetEntryByInstanceId(expectedInstanceId, out _, out entry) || entry?.Item == null)
            {
                error = "La instancia mundial seleccionada ya no estÃ¡ disponible.";
                return false;
            }

            if (entry.DefinitionId != expectedDefinitionId || entry.Quantity != expectedQuantity)
            {
                error = "La definiciÃ³n o cantidad del objeto mundial cambiÃ³.";
                return false;
            }

            return true;
        }

        internal bool IsTransactionSourceEmpty(string instanceId)
        {
            return storage.GetEntryByInstanceId(instanceId) == null;
        }

        internal DebugActionExecutionResult FinalizeCommittedPickup(
            ActorInteractionContext actorContext,
            WorldObjectTags targetTags,
            ItemInstance item,
            int transferredQuantity,
            string resultTitle,
            string resultVerb)
        {
            bool addedPickedUp = targetTags.AddTag(PickedUpTag);
            bool removedPickupable = targetTags.RemoveTag(PickupableTag);
            DisableVisiblePickupParts();
            DisablePhysics();

            string displayName = GetItemDisplayName(item != null ? item.DefinitionId : itemDefinitionId);
            RecordItemPickedUp(actorContext, targetTags, item, displayName, transferredQuantity);
            RecordTargetStateChanged(actorContext, targetTags, addedPickedUp, removedPickupable);

            Debug.Log($"[WorldItemPickup] Committed {SafeText(item?.DefinitionId)} x{transferredQuantity} [{SafeText(item?.InstanceId)}].");

            if (destroyAfterPickup)
                Destroy(gameObject);

            return DebugActionExecutionResult.Info(
                string.IsNullOrWhiteSpace(resultTitle) ? "Recoger" : resultTitle,
                $"{(string.IsNullOrWhiteSpace(resultVerb) ? "Recogiste" : resultVerb)} {displayName} x{transferredQuantity}.");
        }

        private bool EnsureConfiguredItemStorage(out string error)
        {
            error = null;
            if (!storage.IsEmpty)
            {
                sourceInitialized = true;
                return true;
            }

            if (sourceInitialized)
            {
                error = "La fuente mundial ya fue inicializada y no contiene una instancia transferible.";
                return false;
            }

            GameDataManager dataManager = GameDataManager.Instance;
            if (dataManager == null || !dataManager.IsReady || dataManager.Database == null)
            {
                error = "Los datos del juego todavía no están disponibles.";
                Debug.LogWarning("[WorldItemPickup] Game database is not ready.", this);
                return false;
            }

            ItemDefinition definition = dataManager.Database.GetItem(itemDefinitionId);
            if (definition == null)
            {
                error = $"No existe la definición '{SafeText(itemDefinitionId)}'.";
                Debug.LogWarning(
                    $"[WorldItemPickup] Item definition '{SafeText(itemDefinitionId)}' was not found in the ready game database.",
                    this);
                return false;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(authoredItemInstanceId))
                    throw new System.InvalidOperationException($"Authored world item '{name}' requires an authored item instance id.");

                storage.AddItem(ItemInstance.CreateAuthored(definition, authoredItemInstanceId));
                sourceInitialized = true;
                ItemOwnedStorageRegistry.Instance.BindEntries(storage.Entries, this);
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[WorldItemPickup] Failed to initialize authored world item '{name}': {exception.Message}", this);
                error = exception.Message;
                return false;
            }
        }

        private void EnsureSimplePhysics()
        {
            BoxCollider boxCollider = GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                boxCollider = gameObject.AddComponent<BoxCollider>();
                boxCollider.center = DefaultColliderCenter;
                boxCollider.size = DefaultColliderSize;
            }

            boxCollider.isTrigger = false;
            boxCollider.enabled = true;

            Rigidbody rigidbody = GetComponent<Rigidbody>();
            if (rigidbody == null)
                rigidbody = gameObject.AddComponent<Rigidbody>();

            rigidbody.useGravity = true;
            rigidbody.isKinematic = false;
            rigidbody.detectCollisions = true;
        }

        private void DisablePhysics()
        {
            Rigidbody rigidbody = GetComponent<Rigidbody>();
            if (rigidbody == null)
                return;

            rigidbody.Sleep();
            rigidbody.useGravity = false;
            rigidbody.detectCollisions = false;
            rigidbody.isKinematic = true;
        }

        private void DisableVisiblePickupParts()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>();
            for (int index = 0; index < colliders.Length; index++)
                colliders[index].enabled = false;

            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            for (int index = 0; index < renderers.Length; index++)
                renderers[index].enabled = false;
        }

        private static string GetItemDisplayName(string definitionId)
        {
            ItemDefinition definition = GetItemDefinition(definitionId);
            if (definition == null || definition.display == null || string.IsNullOrWhiteSpace(definition.display.name))
                return SafeText(definitionId);

            return definition.display.name;
        }

        private static void RecordItemPickedUp(
            ActorInteractionContext actorContext,
            WorldObjectTags targetTags,
            ItemInstance item,
            string itemDisplayName,
            int quantity)
        {
            GameplayFeedbackLog.TryRecord(new GameplayFeedbackEntry(
                GameplayFeedbackEntryType.ItemPickedUp,
                $"Picked up {SafeText(itemDisplayName)} x{quantity}.",
                actorId: GetActorName(actorContext),
                actorDisplayName: GetActorName(actorContext),
                targetId: GetTargetName(targetTags),
                targetDisplayName: GetTargetDisplayName(targetTags),
                itemId: item != null ? item.DefinitionId : null,
                itemDisplayName: itemDisplayName,
                quantity: quantity));
        }

        private static void RecordTargetStateChanged(ActorInteractionContext actorContext, WorldObjectTags targetTags, bool addedPickedUp, bool removedPickupable)
        {
            if (!addedPickedUp && !removedPickupable)
                return;

            GameplayFeedbackLog.TryRecord(new GameplayFeedbackEntry(
                GameplayFeedbackEntryType.TargetStateChanged,
                $"Estado actualizado: {SafeText(GetTargetDisplayName(targetTags))}.",
                actorId: GetActorName(actorContext),
                actorDisplayName: GetActorName(actorContext),
                targetId: GetTargetName(targetTags),
                targetDisplayName: GetTargetDisplayName(targetTags),
                addedTags: addedPickedUp ? new[] { PickedUpTag } : null,
                removedTags: removedPickupable ? new[] { PickupableTag } : null,
                debugOnly: true));
        }

        private static string GetActorName(ActorInteractionContext actorContext)
        {
            return actorContext != null ? actorContext.name : null;
        }

        private static string GetTargetName(WorldObjectTags targetTags)
        {
            return targetTags != null ? targetTags.name : null;
        }

        private static string GetTargetDisplayName(WorldObjectTags targetTags)
        {
            if (targetTags == null)
                return null;

            WorldObjectDebugInfo debugInfo = targetTags.GetComponent<WorldObjectDebugInfo>();
            return debugInfo != null ? debugInfo.GetDisplayNameOrFallback(targetTags.name) : targetTags.name;
        }

        private static ItemDefinition GetItemDefinition(string definitionId)
        {
            if (GameDataManager.Instance == null || !GameDataManager.Instance.IsReady)
                return null;

            GameDatabase database = GameDataManager.Instance.Database;
            return database != null ? database.GetItem(definitionId) : null;
        }

        private GridInventoryBackend GetTransferBackend()
        {
            if (transferBackend == null)
                transferBackend = new GridInventoryBackend(storage, GetItemDefinition);
            return transferBackend;
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
        }
    }
}
