using OldScars.Core.Items;
using UnityEngine;

namespace OldScars.Core.Interactions
{
    public sealed class DebugWorldUiInputBlocker : MonoBehaviour
    {
        [SerializeField] private ContextualActionDebugPanel actionPanel;
        [SerializeField] private ContextualActionDebugResultPanel resultPanel;
        [SerializeField] private InventoryDebugPanel inventoryPanel;

        private void Awake()
        {
            if (actionPanel == null)
                actionPanel = FindAnyObjectByType<ContextualActionDebugPanel>();

            if (resultPanel == null)
                resultPanel = FindAnyObjectByType<ContextualActionDebugResultPanel>();

            if (inventoryPanel == null)
                inventoryPanel = FindAnyObjectByType<InventoryDebugPanel>();
        }

        public bool ConsumeLeftClickIfNeeded(Vector2 screenPosition)
        {
            EnsureReferences();

            bool actionPanelOpen = actionPanel != null && actionPanel.IsVisible;
            bool resultPanelOpen = resultPanel != null && resultPanel.IsVisible;
            bool inventoryPanelOpen = inventoryPanel != null && inventoryPanel.IsVisible;

            if (!actionPanelOpen && !resultPanelOpen && !inventoryPanelOpen)
                return false;

            bool clickInsideOpenPanel =
                (actionPanelOpen && actionPanel.ContainsScreenPosition(screenPosition)) ||
                (resultPanelOpen && resultPanel.ContainsScreenPosition(screenPosition)) ||
                (inventoryPanelOpen && inventoryPanel.ContainsScreenPosition(screenPosition));

            if (!clickInsideOpenPanel)
            {
                if (actionPanelOpen)
                    actionPanel.Hide();

                if (resultPanelOpen)
                    resultPanel.Hide();

                if (inventoryPanelOpen)
                    inventoryPanel.Hide();
            }

            return true;
        }

        private void EnsureReferences()
        {
            if (actionPanel == null)
                actionPanel = FindAnyObjectByType<ContextualActionDebugPanel>();

            if (resultPanel == null)
                resultPanel = FindAnyObjectByType<ContextualActionDebugResultPanel>();

            if (inventoryPanel == null)
                inventoryPanel = FindAnyObjectByType<InventoryDebugPanel>();
        }
    }
}
