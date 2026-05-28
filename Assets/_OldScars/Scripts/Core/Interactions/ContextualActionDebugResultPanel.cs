using UnityEngine;
using UnityEngine.InputSystem;

namespace OldScars.Core.Interactions
{
    public sealed class ContextualActionDebugResultPanel : MonoBehaviour
    {
        private const float PanelWidth = 420f;
        private const float PanelHeight = 220f;

        private bool isVisible;
        private string title;
        private string body;
        private Vector2 scrollPosition;

        public bool IsVisible => isVisible;

        public void Show(DebugActionExecutionResult result)
        {
            if (!result.hasResult)
                return;

            title = SafeText(result.title, "Resultado");
            body = SafeText(result.body, "Sin contenido.");
            scrollPosition = Vector2.zero;
            isVisible = true;
        }

        public void Hide()
        {
            isVisible = false;
            title = null;
            body = null;
        }

        public bool ContainsScreenPosition(Vector2 screenPosition)
        {
            if (!isVisible)
                return false;

            Vector2 guiPoint = ToGuiPosition(screenPosition);
            return GetPanelRect().Contains(guiPoint);
        }

        private void Update()
        {
            if (!isVisible || Keyboard.current == null)
                return;

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
                Hide();
        }

        private void OnGUI()
        {
            if (!isVisible)
                return;

            GUILayout.BeginArea(GetPanelRect(), GUI.skin.box);
            GUILayout.Label(title);
            GUILayout.Space(8f);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(130f));
            GUILayout.Label(body);
            GUILayout.EndScrollView();

            GUILayout.Space(8f);

            if (GUILayout.Button("Close", GUILayout.Height(24f)))
                Hide();

            GUILayout.EndArea();
        }

        private static Rect GetPanelRect()
        {
            float x = Mathf.Max(0f, (Screen.width - PanelWidth) * 0.5f);
            float y = Mathf.Max(0f, Screen.height - PanelHeight - 24f);
            return new Rect(x, y, PanelWidth, PanelHeight);
        }

        private static Vector2 ToGuiPosition(Vector2 mousePosition)
        {
            return new Vector2(mousePosition.x, Screen.height - mousePosition.y);
        }

        private static string SafeText(string value, string fallback)
        {
            return !string.IsNullOrWhiteSpace(value) ? value : fallback;
        }
    }
}
