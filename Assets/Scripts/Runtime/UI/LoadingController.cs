using System.Collections;
using UnityEngine;
using Zenject;
using RestaurantLoop.Signals;
using RestaurantLoop.SceneFlow;

namespace RestaurantLoop.UI
{
    public class LoadingController : MonoBehaviour
    {
        private const float MinLoadingDuration = 1f;

        [SerializeField] private GameObject loadingRoot;
        [SerializeField] private GameObject menuRoot;

        private ISceneLoader _sceneLoader;
        private SignalBus _signalBus;
        private float _loadingStartedAt;

        [Inject]
        public void Construct(ISceneLoader sceneLoader, SignalBus signalBus)
        {
            _sceneLoader = sceneLoader;
            _signalBus = signalBus;
        }

        private void Start()
        {
            _signalBus.Subscribe<LoadingStartedSignal>(ShowLoading);
            _signalBus.Subscribe<GameplaySceneReadySignal>(HideLoading);
            _signalBus.Subscribe<LevelEndedSignal>(ShowMenu);

            _sceneLoader.LoadGameplayScene();
        }

        private void OnDestroy()
        {
            _signalBus.Unsubscribe<LoadingStartedSignal>(ShowLoading);
            _signalBus.Unsubscribe<GameplaySceneReadySignal>(HideLoading);
            _signalBus.Unsubscribe<LevelEndedSignal>(ShowMenu);
        }

        private void ShowLoading()
        {
            _loadingStartedAt = Time.time;
            loadingRoot.SetActive(true);
            menuRoot.SetActive(false);
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

        private void ShowMenu(LevelEndedSignal signal)
        {
            menuRoot.SetActive(true);
        }
    }
}
