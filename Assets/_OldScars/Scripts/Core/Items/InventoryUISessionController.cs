using System.Collections.Generic;
using OldScars.Core.Actors;
using OldScars.Core.Data.Definitions;
using OldScars.Core.Interactions;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OldScars.Core.Items
{
    public enum InventoryUISessionState
    {
        Closed,
        Personal,
        External
    }

    internal enum FloatingStorageWindowSourceKind
    {
        PersonalOwned,
        LootableActorEquipment
    }

    internal readonly struct FloatingStorageWindowResolution
    {
        internal FloatingStorageWindowResolution(
            FloatingStorageWindowSourceKind sourceKind,
            ItemStorageEntry containerEntry,
            ItemOwnedStorageRuntime storage,
            string sourceLabel)
        {
            SourceKind = sourceKind;
            ContainerEntry = containerEntry;
            Storage = storage;
            SourceLabel = sourceLabel;
        }

        internal FloatingStorageWindowSourceKind SourceKind { get; }
        internal ItemStorageEntry ContainerEntry { get; }
        internal ItemOwnedStorageRuntime Storage { get; }
        internal string SourceLabel { get; }
    }

    internal sealed class FloatingStorageWindowBinding
    {
        private readonly FloatingStorageWindowSourceKind sourceKind;
        private readonly string containerInstanceId;
        private readonly InventoryComponent expectedPersonalInventory;
        private readonly LootableActorInventoryComponent expectedLootableActor;
        private readonly ItemOwnedStorageRuntime expectedStorage;

        internal FloatingStorageWindowBinding(
            FloatingStorageWindowSourceKind sourceKind,
            string containerInstanceId,
            ItemOwnedStorageRuntime expectedStorage,
            InventoryComponent expectedPersonalInventory,
            LootableActorInventoryComponent expectedLootableActor)
        {
            this.sourceKind = sourceKind;
            this.containerInstanceId = containerInstanceId;
            this.expectedStorage = expectedStorage;
            this.expectedPersonalInventory = expectedPersonalInventory;
            this.expectedLootableActor = expectedLootableActor;
        }

        internal FloatingStorageWindowSourceKind SourceKind => sourceKind;
        internal LootableActorInventoryComponent ExpectedLootableActor => expectedLootableActor;

        internal bool TryResolve(
            PersonalStorageNavigator navigator,
            out FloatingStorageWindowResolution resolution)
        {
            resolution = default;
            if (string.IsNullOrWhiteSpace(containerInstanceId) || expectedStorage == null ||
                !ItemOwnedStorageRegistry.Instance.TryResolveOwnedStorage(
                    containerInstanceId,
                    out ItemOwnedStorageRuntime currentStorage) ||
                !ReferenceEquals(currentStorage, expectedStorage))
            {
                return false;
            }

            if (sourceKind == FloatingStorageWindowSourceKind.PersonalOwned)
                return TryResolvePersonal(navigator, currentStorage, out resolution);

            return TryResolveLootableActor(currentStorage, out resolution);
        }

        private bool TryResolvePersonal(
            PersonalStorageNavigator navigator,
            ItemOwnedStorageRuntime currentStorage,
            out FloatingStorageWindowResolution resolution)
        {
            resolution = default;
            if (expectedPersonalInventory == null || navigator == null ||
                !ReferenceEquals(navigator.PersonalInventory, expectedPersonalInventory) ||
                !navigator.TryGetContainerEntry(containerInstanceId, out ItemStorageEntry entry) ||
                entry?.Item?.InstanceId != containerInstanceId ||
                !ReferenceEquals(entry.Item.OwnedStorage, currentStorage) ||
                !ItemOwnedStorageRegistry.Instance.TryResolveRootOwner(
                    containerInstanceId,
                    out object rootOwner,
                    out _) ||
                !ReferenceEquals(rootOwner, expectedPersonalInventory))
            {
                return false;
            }

            ActorEquipmentComponent equipment = expectedPersonalInventory.GetComponent<ActorEquipmentComponent>();
            string sourceLabel = navigator.IsEquippedOwnedStorage(containerInstanceId)
                ? GetEquipmentSourceLabel(equipment, containerInstanceId)
                : "Inventario personal";
            resolution = new FloatingStorageWindowResolution(
                sourceKind,
                entry,
                currentStorage,
                sourceLabel);
            return true;
        }

        private bool TryResolveLootableActor(
            ItemOwnedStorageRuntime currentStorage,
            out FloatingStorageWindowResolution resolution)
        {
            resolution = default;
            if (expectedLootableActor == null || !expectedLootableActor.CanOpenStorage(out _) ||
                !expectedLootableActor.TryGetEquippedOwnedStorage(
                    containerInstanceId,
                    out LootableActorOwnedStorage option) ||
                option.ContainerEntry?.Item?.InstanceId != containerInstanceId ||
                !ReferenceEquals(option.ContainerEntry.Item.OwnedStorage, currentStorage) ||
                !ReferenceEquals(option.Storage, currentStorage) ||
                !ItemOwnedStorageRegistry.Instance.TryResolveRootOwner(
                    containerInstanceId,
                    out object rootOwner,
                    out _) ||
                !ReferenceEquals(rootOwner, expectedLootableActor.Inventory))
            {
                return false;
            }

            resolution = new FloatingStorageWindowResolution(
                sourceKind,
                option.ContainerEntry,
                currentStorage,
                GetEquipmentSourceLabel(expectedLootableActor.Equipment, containerInstanceId));
            return true;
        }

        private static string GetEquipmentSourceLabel(
            ActorEquipmentComponent equipment,
            string instanceId)
        {
            IReadOnlyList<string> slots = equipment?.GetSlotsOccupiedBy(instanceId);
            if (slots == null || slots.Count == 0)
                return "Equipment";

            var labels = new string[slots.Count];
            for (int index = 0; index < slots.Count; index++)
            {
                EquipmentSlotDefinition definition = equipment.GetSlotDefinition(slots[index]);
                labels[index] = definition != null && !string.IsNullOrWhiteSpace(definition.display_name)
                    ? definition.display_name
                    : slots[index];
            }
            return string.Join(" + ", labels);
        }
    }

    public sealed class InventoryUISessionController : MonoBehaviour
    {
        [SerializeField] private InventoryDebugPanel inventoryPanel;
        [SerializeField] private ItemStorageDebugPanel storagePanel;
        [SerializeField] private InventoryComponent playerInventory;
        [SerializeField] private PlayerMovementController movementController;

        private readonly InventoryUISessionSelection selection = new InventoryUISessionSelection();
        private readonly InventoryContextMenuState contextMenuState = new InventoryContextMenuState();
        private PersonalStorageNavigator personalStorageNavigator;
        private FloatingStorageWindowBinding floatingStorageBinding;
        private string personalSelectionBeforeFloatingWindow;
        private bool hasCapturedPersonalSelection;

        public InventoryUISessionState State { get; private set; }
        public bool BlocksWorldInput => State != InventoryUISessionState.Closed;
        public bool IsOpen => BlocksWorldInput;
        public InventoryUISessionSelection Selection => selection;
        public bool BlocksInventoryContentInput => contextMenuState.BlocksContentInput;
        public bool ContextMenuOpen => contextMenuState.ContextMenuOpen;
        public bool QuantityDialogOpen => contextMenuState.QuantityDialogOpen;
        public PersonalStorageNavigator PersonalStorageNavigator
        {
            get
            {
                ResolveReferences();
                if (personalStorageNavigator == null ||
                    !ReferenceEquals(personalStorageNavigator.PersonalInventory, playerInventory))
                {
                    personalStorageNavigator = new PersonalStorageNavigator(playerInventory);
                }
                personalStorageNavigator.Refresh();
                return personalStorageNavigator;
            }
        }
        internal bool HasFloatingStorageWindow => floatingStorageBinding != null;

        public static InventoryUISessionController GetOrCreate()
        {
            InventoryUISessionController controller = FindAnyObjectByType<InventoryUISessionController>();
            if (controller != null)
                return controller;

            var controllerObject = new GameObject("InventoryUISessionController_Runtime");
            return controllerObject.AddComponent<InventoryUISessionController>();
        }

        private void Awake()
        {
            ResolveReferences();
            State = InventoryUISessionState.Closed;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.iKey.wasPressedThisFrame)
            {
                if (IsOpen)
                    CloseSession();
                else
                    OpenPersonal();
                return;
            }

            if (!IsOpen)
                return;

            ValidateFloatingStorageWindow();

            if ((keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) &&
                contextMenuState.ConfirmQuantityFromKeyboard())
            {
                return;
            }

            HandleActiveRotationInput();
            if (!keyboard.escapeKey.wasPressedThisFrame)
                return;

            if (contextMenuState.CancelQuantityDialog())
                return;
            if (contextMenuState.CloseContextMenu())
                return;
            if (!CancelActiveDrag())
            {
                if (!CloseFloatingStorageWindow())
                    CloseSession();
            }
        }

        public void OpenPersonal()
        {
            ResolveReferences();
            if (inventoryPanel == null)
            {
                Debug.LogError("[InventoryUISessionController] InventoryDebugPanel is missing.");
                return;
            }

            CloseFloatingStorageWindowInternal(true);
            BeginSession();
            storagePanel?.HideFromSession();
            inventoryPanel.ShowFromSession();
            State = InventoryUISessionState.Personal;
        }

        public void OpenExternal(
            IItemStorageDebugSource source,
            InventoryComponent inventory,
            DebugActionExecutionContext context,
            ActionDefinition action)
        {
            ResolveReferences();
            if (source == null || storagePanel == null)
            {
                Debug.LogError("[InventoryUISessionController] External storage source or panel is missing.");
                return;
            }

            if (inventory != null)
                playerInventory = inventory;

            CloseFloatingStorageWindowInternal(true);
            BeginSession();
            inventoryPanel?.HideFromSession();
            storagePanel.ShowFromSession(source, playerInventory, context, action);
            State = InventoryUISessionState.External;
        }

        public void CloseSession()
        {
            contextMenuState.CloseAll();
            CancelActiveDrag();
            CloseFloatingStorageWindowInternal(false);
            inventoryPanel?.HideFromSession();
            storagePanel?.HideFromSession();
            selection.ResetTransient();
            personalStorageNavigator?.SelectPersonalInventory();
            State = InventoryUISessionState.Closed;
        }

        public void OpenContextMenu(InventoryContextMenuRequest request, Vector2 guiPosition)
        {
            if (!IsOpen || request == null)
                return;

            contextMenuState.Open(request, guiPosition);
        }

        public void CloseContextMenu()
        {
            contextMenuState.CloseAll();
        }

        public bool OpenPersonalOwnedStorageWindow(string containerInstanceId)
        {
            if (!IsOpen)
                return false;

            PersonalStorageNavigator navigator = PersonalStorageNavigator;
            if (navigator == null || !TryResolvePersonalOwnedStorage(
                    navigator,
                    containerInstanceId,
                    out ItemOwnedStorageRuntime storage))
            {
                return false;
            }

            return OpenFloatingStorageWindow(new FloatingStorageWindowBinding(
                FloatingStorageWindowSourceKind.PersonalOwned,
                containerInstanceId,
                storage,
                playerInventory,
                null));
        }

        public bool OpenLootableEquipmentStorageWindow(
            LootableActorInventoryComponent lootableActor,
            string containerInstanceId)
        {
            if (!IsOpen || State != InventoryUISessionState.External || lootableActor == null ||
                !lootableActor.CanOpenStorage(out _) ||
                !lootableActor.TryGetEquippedOwnedStorage(
                    containerInstanceId,
                    out LootableActorOwnedStorage option) ||
                option.ContainerEntry?.Item?.InstanceId != containerInstanceId ||
                option.Storage == null)
            {
                return false;
            }

            return OpenFloatingStorageWindow(new FloatingStorageWindowBinding(
                FloatingStorageWindowSourceKind.LootableActorEquipment,
                containerInstanceId,
                option.Storage,
                playerInventory,
                lootableActor));
        }

        public bool CloseFloatingStorageWindow()
        {
            return CloseFloatingStorageWindowInternal(true);
        }

        internal bool TryResolveFloatingStorageWindow(out FloatingStorageWindowResolution resolution)
        {
            resolution = default;
            if (floatingStorageBinding == null)
                return false;

            PersonalStorageNavigator navigator = PersonalStorageNavigator;
            navigator?.SelectPersonalInventory();
            bool expectedSessionSource =
                floatingStorageBinding.SourceKind != FloatingStorageWindowSourceKind.LootableActorEquipment ||
                State == InventoryUISessionState.External && storagePanel != null &&
                storagePanel.IsBoundToLootableActor(floatingStorageBinding.ExpectedLootableActor);
            if (!expectedSessionSource ||
                !floatingStorageBinding.TryResolve(navigator, out resolution))
            {
                CloseFloatingStorageWindowInternal(true);
                return false;
            }
            return true;
        }

        public void ValidateFloatingStorageWindow()
        {
            if (floatingStorageBinding != null)
                TryResolveFloatingStorageWindow(out _);
        }

        private static bool TryResolvePersonalOwnedStorage(
            PersonalStorageNavigator navigator,
            string containerInstanceId,
            out ItemOwnedStorageRuntime storage)
        {
            storage = null;
            if (navigator == null)
                return false;

            return navigator.TryGetPersonalInventoryOwnedStorage(containerInstanceId, out storage, out _) ||
                   navigator.TryGetOwnedStorage(containerInstanceId, out storage);
        }

        private bool OpenFloatingStorageWindow(FloatingStorageWindowBinding binding)
        {
            if (binding == null)
                return false;

            PersonalStorageNavigator navigator = PersonalStorageNavigator;
            if (navigator == null || !binding.TryResolve(navigator, out _))
                return false;

            if (!hasCapturedPersonalSelection)
            {
                personalSelectionBeforeFloatingWindow = navigator.SelectedContainerInstanceId;
                hasCapturedPersonalSelection = true;
            }

            CancelActiveDrag();
            contextMenuState.CloseAll();
            floatingStorageBinding = binding;
            navigator.SelectPersonalInventory();
            selection.SelectPersonal(null);
            ResetFloatingStorageWindowViews(false);
            return true;
        }

        private bool CloseFloatingStorageWindowInternal(bool restorePersonalSelection)
        {
            if (floatingStorageBinding == null)
                return false;

            CancelActiveDrag();
            contextMenuState.CloseAll();
            floatingStorageBinding = null;
            selection.SelectPersonal(null);
            ResetFloatingStorageWindowViews(false);

            PersonalStorageNavigator navigator = personalStorageNavigator;
            if (restorePersonalSelection && navigator != null && hasCapturedPersonalSelection)
            {
                if (string.IsNullOrWhiteSpace(personalSelectionBeforeFloatingWindow) ||
                    !navigator.TrySelectContainer(personalSelectionBeforeFloatingWindow))
                {
                    navigator.SelectPersonalInventory();
                }
            }

            personalSelectionBeforeFloatingWindow = null;
            hasCapturedPersonalSelection = false;
            return true;
        }

        private void ResetFloatingStorageWindowViews(bool resetPosition)
        {
            inventoryPanel?.ResetFloatingStorageWindow(resetPosition);
            storagePanel?.ResetFloatingStorageWindow(resetPosition);
        }

        internal void DrawContextOverlay(Rect localWindowRect)
        {
            if (!IsOpen)
                return;

            contextMenuState.Draw(localWindowRect);
        }

        public bool CancelActiveDrag()
        {
            if (State == InventoryUISessionState.Personal)
                return inventoryPanel != null && inventoryPanel.CancelActiveDrag();
            if (State == InventoryUISessionState.External)
                return storagePanel != null && storagePanel.CancelActiveDrag();
            return false;
        }

        internal void BindPanel(InventoryDebugPanel panel)
        {
            if (panel == null)
                return;

            inventoryPanel = panel;
            if (playerInventory == null)
                playerInventory = panel.Inventory;
            panel.BindSessionController(this);
        }

        internal void ConsumeCurrentOnGUIEvent()
        {
            if (!BlocksWorldInput || Event.current == null)
                return;

            EventType type = Event.current.type;
            if (type == EventType.MouseDown || type == EventType.MouseUp || type == EventType.MouseDrag ||
                type == EventType.ScrollWheel || type == EventType.KeyDown || type == EventType.KeyUp)
            {
                Event.current.Use();
            }
        }

        private void HandleActiveRotationInput()
        {
            if (contextMenuState.BlocksContentInput)
                return;

            if (State == InventoryUISessionState.Personal)
                inventoryPanel?.HandleRotationInput();
            else if (State == InventoryUISessionState.External)
                storagePanel?.HandleRotationInput();
        }

        private void BeginSession()
        {
            if (!IsOpen)
                movementController?.ClearMovement();

            contextMenuState.CloseAll();
            inventoryPanel?.BindSessionController(this);
            storagePanel?.BindSessionController(this);
        }

        private void ResolveReferences()
        {
            if (inventoryPanel == null)
                inventoryPanel = FindAnyObjectByType<InventoryDebugPanel>();
            if (storagePanel == null)
                storagePanel = FindAnyObjectByType<ItemStorageDebugPanel>();
            if (playerInventory == null && inventoryPanel != null)
                playerInventory = inventoryPanel.Inventory;
            if (movementController == null && playerInventory != null)
                movementController = playerInventory.GetComponent<PlayerMovementController>();

            inventoryPanel?.BindSessionController(this);
            storagePanel?.BindSessionController(this);
        }
    }
}
