using System.Collections.Generic;
using UnityEngine;

namespace OldScars.Core.Actors
{
    public sealed class ActorNeedsDebugPanel : MonoBehaviour
    {
        private const float PanelWidth = 220f;
        private const float PanelHeight = 86f;

        [SerializeField] private ActorNeedsComponent actorNeeds;
        [SerializeField] private bool visible = true;

        public bool IsVisible => visible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimePanel()
        {
            if (FindAnyObjectByType<ActorNeedsDebugPanel>() != null)
            {
                return;
            }

            ActorNeedsComponent actorNeeds = FindAnyObjectByType<ActorNeedsComponent>();
            if (actorNeeds == null)
            {
                return;
            }

            var panelObject = new GameObject("ActorNeedsDebugPanel_Runtime");
            ActorNeedsDebugPanel panel = panelObject.AddComponent<ActorNeedsDebugPanel>();
            panel.actorNeeds = actorNeeds;
            panel.visible = true;
        }

        private void Awake()
        {
            ResolveActorNeeds();
        }

        private void OnEnable()
        {
            ResolveActorNeeds();
        }

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            if (actorNeeds == null)
            {
                ResolveActorNeeds();
            }

            GUILayout.BeginArea(GetPanelRect(), GUI.skin.box);
            GUILayout.Label("Needs (Debug)");

            if (actorNeeds == null)
            {
                GUILayout.Label("No ActorNeedsComponent.");
                GUILayout.EndArea();
                return;
            }

            IReadOnlyList<ActorNeedState> states = actorNeeds.RuntimeStates;
            if (states == null || states.Count == 0)
            {
                GUILayout.Label("No runtime needs.");
                GUILayout.EndArea();
                return;
            }

            for (int index = 0; index < states.Count; index++)
            {
                ActorNeedState state = states[index];
                if (state == null || string.IsNullOrWhiteSpace(state.needId))
                {
                    continue;
                }

                DrawNeed(state.needId, state.currentValue);
            }

            GUILayout.EndArea();
        }

        public bool ContainsScreenPosition(Vector2 screenPosition)
        {
            if (!visible)
            {
                return false;
            }

            Vector2 guiPoint = ToGuiPosition(screenPosition);
            return GetPanelRect().Contains(guiPoint);
        }

        private void DrawNeed(string needId, float currentValue)
        {
            string displayName = actorNeeds.GetNeedDisplayName(needId);
            float maxValue = actorNeeds.GetNeedMaxValue(needId);
            float percent = maxValue > 0f ? Mathf.Clamp01(currentValue / maxValue) * 100f : 0f;
            GUILayout.Label($"{displayName}: {currentValue:0.#}/{maxValue:0.#} ({percent:0}%)");
        }

        private void ResolveActorNeeds()
        {
            if (actorNeeds != null)
            {
                return;
            }

            actorNeeds = FindAnyObjectByType<ActorNeedsComponent>();
        }

        private static Rect GetPanelRect()
        {
            return new Rect(16f, 16f, PanelWidth, PanelHeight);
        }

        private static Vector2 ToGuiPosition(Vector2 mousePosition)
        {
            return new Vector2(mousePosition.x, Screen.height - mousePosition.y);
        }
    }
}
