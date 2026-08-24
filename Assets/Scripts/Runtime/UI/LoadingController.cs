using System.Collections;
using UnityEngine;
using Zenject;
using RestaurantLoop.Signals;

namespace RestaurantLoop.UI
{
    // Lives in MainMenuScene (loaded once at boot, never unloaded) so the same instance covers every
    // scene transition — boot's implicit continue-into-Gameplay included, not just later Play/Claim
    // Menu<->Gameplay loads. Owns only the loading overlay; MainMenu's own content visibility is
    // MainMenuVisibilityController's job (same scene, separate concern).
    //
    // loadingRoot's authored default is ACTIVE. That's deliberate: on cold boot nothing fires
    // LoadingStartedSignal before this scene exists (there's no subscriber to hear it), so the
    // default-on state is what covers the instant this scene wakes up. _loadingStartedAt defaults to
    // 0, which happens to line up with "loading became visible at scene start" for that same reason,
    // so HideLoading()'s elapsed-time math is still correct without an explicit boot-time Show call.
    public class LoadingController : MonoBehaviour
    {
        private const float MinLoadingDuration = 1f;

        [SerializeField] private GameObject loadingRoot;

        private SignalBus _signalBus;
        private float _loadingStartedAt;

        [Inject]
        public void Construct(SignalBus signalBus)
        {
            _signalBus = signalBus;

            // Note: MainMenuReadySignal is NOT one of the hide triggers — on cold boot that signal
            // chains straight into loading GameplayScene (see SceneLoader), and this screen should
            // stay up through that so the menu is never actually seen.
            _signalBus.Subscribe<LoadingStartedSignal>(ShowLoading);
            _signalBus.Subscribe<GameplaySceneReadySignal>(HideLoading);
            _signalBus.Subscribe<MenuReadySignal>(HideLoading);
        }

        private void OnDestroy()
        {
            _signalBus.Unsubscribe<LoadingStartedSignal>(ShowLoading);
            _signalBus.Unsubscribe<GameplaySceneReadySignal>(HideLoading);
            _signalBus.Unsubscribe<MenuReadySignal>(HideLoading);
        }

        private void ShowLoading()
        {
            _loadingStartedAt = Time.time;
            loadingRoot.SetActive(true);
        }

        private void HideLoading()
        {
            var elapsed = Time.time - _loadingStartedAt;
            var remaining = MinLoadingDuration - elapsed;

            if (remaining > 0f)
            {
                StartCoroutine(HideLoadingAfterDelay(remaining));
            }
            else
            {
                loadingRoot.SetActive(false);
            }
        }

        private IEnumerator HideLoadingAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            loadingRoot.SetActive(false);
        }
    }
}
