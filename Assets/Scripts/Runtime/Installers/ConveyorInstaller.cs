using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.UI;
using RestaurantLoop.SceneFlow;
using TMPro;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Installers
{
    // scene-scoped, GameplayScene's SceneContext
    public class ConveyorInstaller : MonoInstaller
    {
        [Inject] private CurrentLevelProvider _currentLevelProvider;
        [SerializeField] private Camera _mainCamera;
        [SerializeField] private TextMeshProUGUI _levelText;

        public override void InstallBindings()
        {
            var level = _currentLevelProvider.Current;
            if (level == null)
            {
                Debug.LogError(
                    "[ConveyorInstaller] CurrentLevelProvider is empty — SaveManager should have resolved a level at boot.");
                return;
            }

            Container.BindInstance(level).AsSingle();
            Container.BindInstance(_mainCamera != null ? _mainCamera : Camera.main).AsSingle();
            Container.Bind<RuntimeCustomerState>().AsSingle();
            Container.BindInterfacesTo<ConveyorPieceSpawner>().AsSingle();
            Container.BindInterfacesAndSelfTo<CustomerSpawner>().AsSingle();
            Container.BindInterfacesAndSelfTo<ExitStackAnchor>().AsSingle();
            Container.BindInterfacesAndSelfTo<QueueAnchor>().AsSingle();
            Container.BindInterfacesAndSelfTo<QueueSpawner>().AsSingle();
            Container.BindInterfacesTo<CameraController>().AsSingle();
            Container.BindInterfacesAndSelfTo<ConveyorManager>().AsSingle();
            Container.BindInterfacesAndSelfTo<CustomerConsumeService>().AsSingle();
            Container.BindInterfacesTo<ConveyorEntryInput>().AsSingle();
            Container.Bind<PowerUpService>().AsSingle();

            Container.BindInstance(_levelText).AsSingle();
            Container.BindInterfacesTo<LevelOverlayPresenter>().AsSingle();
        }
    }
}