using OldScars.Core.Items;
using OldScars.Core.Actors;
using UnityEngine;

namespace OldScars.Core.Interactions
{
    public sealed class DebugWorldUiInputBlocker : MonoBehaviour
    {
        [SerializeField] private ContextualActionDebugPanel actionPanel;
        [SerializeField] private ContextualActionDebugResultPanel resultPanel;
        [SerializeField] private InventoryDebugPanel inventoryPanel;
        [SerializeField] private ItemStorageDebugPanel storagePanel;
        [SerializeField] private ActorNeedsDebugPanel actorNeedsPanel;
        [SerializeField] private InventoryUISessionController inventorySessionController;

        public bool BlocksWorldInput
        {
            get
            {
                EnsureReferences();
                return inventorySessionController != null && inventorySessionController.BlocksWorldInput;
            }
        }

        private void Awake()
        {
            if (actionPanel == null)
                actionPanel = FindAnyObjectByType<ContextualActionDebugPanel>();

            if (resultPanel == null)
                resultPanel = FindAnyObjectByType<ContextualActionDebugResultPanel>();

            if (inventoryPanel == null)
                inventoryPanel = FindAnyObjectByType<InventoryDebugPanel>();

            if (storagePanel == null)
                storagePanel = FindAnyObjectByType<ItemStorageDebugPanel>();

            if (actorNeedsPanel == null)
                actorNeedsPanel = FindAnyObjectByType<ActorNeedsDebugPanel>();

            if (inventorySessionController == null)
                inventorySessionController = FindAnyObjectByType<InventoryUISessionController>();
        }

        public bool ConsumeLeftClickIfNeeded(Vector2 screenPosition)
        {
            EnsureReferences();

            if (inventorySessionController != null && inventorySessionController.BlocksWorldInput)
                return true;

            if (actorNeedsPanel != null && actorNeedsPanel.ContainsScreenPosition(screenPosition))
                return true;

            bool actionPanelOpen = actionPanel != null && actionPanel.IsVisible;
            bool resultPanelOpen = resultPanel != null && resultPanel.IsVisible;
            bool inventoryPanelOpen = inventoryPanel != null && inventoryPanel.IsVisible;
            bool storagePanelOpen = storagePanel != null && storagePanel.IsVisible;

            if (!actionPanelOpen && !resultPanelOpen && !inventoryPanelOpen && !storagePanelOpen)
                return false;

            bool clickInsideOpenPanel =
                (actionPanelOpen && actionPanel.ContainsScreenPosition(screenPosition)) ||
                (resultPanelOpen && resultPanel.ContainsScreenPosition(screenPosition)) ||
                (inventoryPanelOpen && inventoryPanel.ContainsScreenPosition(screenPosition)) ||
                (storagePanelOpen && storagePanel.ContainsScreenPosition(screenPosition));

            if (!clickInsideOpenPanel)
            {
                if (actionPanelOpen)
                    actionPanel.Hide();

                if (resultPanelOpen)
                    resultPanel.Hide();

                if (inventoryPanelOpen)
                    inventoryPanel.Hide();

                if (storagePanelOpen)
                    storagePanel.Hide();
            }

            return true;
        }

        public bool IsPointerOverBlockingPanel(Vector2 screenPosition)
        {
            EnsureReferences();

            if (inventorySessionController != null && inventorySessionController.BlocksWorldInput)
                return true;

            return (actorNeedsPanel != null && actorNeedsPanel.ContainsScreenPosition(screenPosition)) ||
                   (actionPanel != null && actionPanel.IsVisible && actionPanel.ContainsScreenPosition(screenPosition)) ||
                   (resultPanel != null && resultPanel.IsVisible && resultPanel.ContainsScreenPosition(screenPosition)) ||
                   (inventoryPanel != null && inventoryPanel.IsVisible && inventoryPanel.ContainsScreenPosition(screenPosition)) ||
                   (storagePanel != null && storagePanel.IsVisible && storagePanel.ContainsScreenPosition(screenPosition));
        }

        private void EnsureReferences()
        {
            if (actionPanel == null)
                actionPanel = FindAnyObjectByType<ContextualActionDebugPanel>();

            if (resultPanel == null)
                resultPanel = FindAnyObjectByType<ContextualActionDebugResultPanel>();

            if (inventoryPanel == null)
                inventoryPanel = FindAnyObjectByType<InventoryDebugPanel>();

            if (storagePanel == null)
                storagePanel = FindAnyObjectByType<ItemStorageDebugPanel>();

            if (actorNeedsPanel == null)
                actorNeedsPanel = FindAnyObjectByType<ActorNeedsDebugPanel>();

            if (inventorySessionController == null)
                inventorySessionController = FindAnyObjectByType<InventoryUISessionController>();
        }
    }
}
