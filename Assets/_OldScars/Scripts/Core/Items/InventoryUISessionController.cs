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

    public sealed class InventoryUISessionController : MonoBehaviour
    {
        [SerializeField] private InventoryDebugPanel inventoryPanel;
        [SerializeField] private ItemStorageDebugPanel storagePanel;
        [SerializeField] private InventoryComponent playerInventory;
        [SerializeField] private PointClickMovementController movementController;

        private readonly InventoryUISessionSelection selection = new InventoryUISessionSelection();

        public InventoryUISessionState State { get; private set; }
        public bool BlocksWorldInput => State != InventoryUISessionState.Closed;
        public bool IsOpen => BlocksWorldInput;
        public InventoryUISessionSelection Selection => selection;

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

            HandleActiveRotationInput();
            if (!keyboard.escapeKey.wasPressedThisFrame)
                return;

            if (!CancelActiveDrag())
                CloseSession();
        }

        public void OpenPersonal()
        {
            ResolveReferences();
            if (inventoryPanel == null)
            {
                Debug.LogError("[InventoryUISessionController] InventoryDebugPanel is missing.");
                return;
            }

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

            BeginSession();
            inventoryPanel?.HideFromSession();
            storagePanel.ShowFromSession(source, playerInventory, context, action);
            State = InventoryUISessionState.External;
        }

        public void CloseSession()
        {
            CancelActiveDrag();
            inventoryPanel?.HideFromSession();
            storagePanel?.HideFromSession();
            selection.ResetTransient();
            State = InventoryUISessionState.Closed;
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
            if (State == InventoryUISessionState.Personal)
                inventoryPanel?.HandleRotationInput();
            else if (State == InventoryUISessionState.External)
                storagePanel?.HandleRotationInput();
        }

        private void BeginSession()
        {
            if (!IsOpen)
                movementController?.ClearTarget();

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
                movementController = playerInventory.GetComponent<PointClickMovementController>();

            inventoryPanel?.BindSessionController(this);
            storagePanel?.BindSessionController(this);
        }
    }
}
