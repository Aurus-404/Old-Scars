using UnityEngine;

namespace OldScars.Core.Interactions
{
    public sealed class WorldObjectDebugInfo : MonoBehaviour
    {
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 6)] private string inspectText;

        public string DisplayName => displayName;
        public string InspectText => inspectText;

        public string GetDisplayNameOrFallback(string fallbackName)
        {
            return !string.IsNullOrWhiteSpace(displayName) ? displayName : fallbackName;
        }

        public string GetInspectTextOrFallback()
        {
            return !string.IsNullOrWhiteSpace(inspectText)
                ? inspectText
                : "No hay texto de inspeccion configurado para este objeto.";
        }
    }
}
