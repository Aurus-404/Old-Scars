using UnityEngine;

namespace OldScars.Core.ApplicationShell
{
    /// <summary>
    /// SampleScene-only adapter. The laboratory consumes the same shared
    /// gameplay wiring and fixture as the product runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SampleSceneGameplayRuntimeBootstrap : MonoBehaviour
    {
        [SerializeField] private PlayerGameplayComposition player;
        [SerializeField] private DevelopmentGameplayIntegrationFixture developmentFixture;

        public GameplayRuntimeComposition RuntimeComposition { get; private set; }
        public string Failure { get; private set; }

        private void Start()
        {
            if (!GameplayRuntimeComposition.TryCreateAndBind(
                    transform, player, out GameplayRuntimeComposition composition, out string failure))
            {
                Failure = failure;
                Debug.LogError("[SampleScene][GAMEPLAY_RUNTIME_FAIL]\nFailure: " + failure);
                return;
            }

            RuntimeComposition = composition;
            developmentFixture?.BindRuntime(player);
            bool fixtureReady = developmentFixture != null &&
                                developmentFixture.TryValidate(out _);
            Debug.Log("[SampleScene][GAMEPLAY_RUNTIME_READY]\n" +
                      composition.DescribeReadiness(fixtureReady));
        }
    }
}
