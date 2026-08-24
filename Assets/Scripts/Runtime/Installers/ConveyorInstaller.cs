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
        [SerializeField] private Transform _trayDispenserRoot;

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
            Container.BindInterfacesAndSelfTo<QueueLinkVisualizer>().AsSingle();
            Container.BindInterfacesTo<CameraController>().AsSingle();
            Container.BindInterfacesAndSelfTo<ConveyorManager>().AsSingle();
            Container.BindInterfacesAndSelfTo<CustomerConsumeService>().AsSingle();
            Container.BindInterfacesTo<ConveyorEntryInput>().AsSingle();

            Container.BindInstance(_trayDispenserRoot).AsSingle();
            Container.BindInterfacesAndSelfTo<TrayDispenserAnchor>().AsSingle();

            // visual layer ticks after the logical one, flights last so they consume this frame's commands
            Container.BindInterfacesAndSelfTo<ConveyorVisualService>().AsSingle();
            Container.BindInterfacesAndSelfTo<TrayVisualService>().AsSingle();
            Container.BindInterfacesAndSelfTo<FoodFlightService>().AsSingle();
            Container.Bind<PowerUpService>().AsSingle();

            Container.BindInstance(_levelText).AsSingle();
            Container.BindInterfacesTo<LevelOverlayPresenter>().AsSingle();
        }
    }
}