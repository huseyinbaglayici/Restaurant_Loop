using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Tutorial;
using RestaurantLoop.Runtime.UI;
using RestaurantLoop.SceneFlow;
using Runtime.Conveyor;
using TMPro;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Installers
{
    // scene-scoped, GameplayScene's SceneContext
    public class ConveyorInstaller : MonoInstaller
    {
        [Inject] private CurrentLevelProvider _currentLevelProvider;
        [Inject] private CustomerPrefabSettings _customerPrefabSettings;
        [Inject(Id = "CustomerPoolSize")] private int _customerInitialSize;
        [SerializeField] private Camera _mainCamera;
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private TextMeshProUGUI _moneyText;
        [SerializeField] private Transform _trayDispenserRoot;
        [SerializeField] private RectTransform _customerOrderCanvasRoot;
        [SerializeField] private Transform _orderIconsGroup;
        [SerializeField] private Transform _orderTextsGroup;
        [SerializeField] private TutorialView _tutorialView;

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
            Container.BindInterfacesAndSelfTo<CameraController>().AsSingle();
            Container.BindInterfacesAndSelfTo<TableController>().AsSingle();
            Container.BindInterfacesAndSelfTo<EnvironmentController>().AsSingle();
            Container.BindInterfacesAndSelfTo<ConveyorManager>().AsSingle();
            Container.BindInterfacesAndSelfTo<CustomerConsumeService>().AsSingle();
            Container.BindInterfacesTo<ConveyorEntryInput>().AsSingle();
            Container.BindInterfacesAndSelfTo<ConveyorAutoClearService>().AsSingle();

            var dispenserRoot = ResolveTrayDispenserRoot();
            if (dispenserRoot != null)
            {
                Container.BindInstance(dispenserRoot).WithId("TrayDispenserRoot").AsCached();
            }
            Container.BindInterfacesAndSelfTo<TrayDispenserAnchor>().AsSingle();

            // visual layer ticks after the logical one, flights last so they consume this frame's commands
            Container.BindInterfacesAndSelfTo<ConveyorVisualService>().AsSingle();
            Container.BindInterfacesAndSelfTo<TrayVisualService>().AsSingle();
            Container.BindInterfacesAndSelfTo<FoodFlightService>().AsSingle();
            Container.Bind<PowerUpService>().AsSingle();

            Container.BindInstance(_levelText).AsSingle();
            Container.Bind<TextMeshProUGUI>().WithId("GameplayMoneyText").FromInstance(_moneyText).AsCached();
            Container.BindInterfacesTo<LevelOverlayPresenter>().AsSingle();

            Container.Bind<TutorialTargetRegistry>().AsSingle();
            Container.BindInterfacesAndSelfTo<TutorialInputGate>().AsSingle();
            Container.BindInstance(_tutorialView).AsSingle();
            Container.BindInterfacesAndSelfTo<TutorialController>().AsSingle();

            BindCustomerOrderWidgetPool();
        }

        // CustomerController.Pool and the per-variant visual pools live in PoolInstaller
        // (ProjectContext/DDOL) so customers survive GameplayScene unload/reload instead of being
        // destroyed and reprewarmed every level. Only the order widget pool stays scene-scoped here
        // — it needs GameplayScene's own Canvas refs, which SceneLoader recreates every reload.
        private void BindCustomerOrderWidgetPool()
        {
            if (_customerPrefabSettings == null || _customerPrefabSettings.CustomerOrderWidgetPrefab == null ||
                _customerOrderCanvasRoot == null || _orderIconsGroup == null || _orderTextsGroup == null)
            {
                Debug.LogError(
                    "[ConveyorInstaller] CustomerOrderWidgetPrefab, _customerOrderCanvasRoot, _orderIconsGroup " +
                    "or _orderTextsGroup isn't assigned — customer order bubbles won't work this level.");
                return;
            }

            // Every widget's Image and TMP_Text graphics get reparented under these two shared groups on
            // spawn (see CustomerOrderWidget.OnSpawned) so uGUI's batcher can merge them across widgets.
            Container.BindInstance(_orderIconsGroup).WithId("OrderIconsGroup").AsCached();
            Container.BindInstance(_orderTextsGroup).WithId("OrderTextsGroup").AsCached();

            Container.BindMemoryPool<CustomerOrderWidget, CustomerOrderWidget.Pool>()
                .WithInitialSize(_customerInitialSize)
                .FromComponentInNewPrefab(_customerPrefabSettings.CustomerOrderWidgetPrefab)
                .UnderTransform(_customerOrderCanvasRoot)
                .NonLazy();
        }

        // The Inspector reference is a plain scene object drag-reference.
        // If unassigned, falls back to finding 'tepsi_dagitici' in the scene.
        // If neither exists, returns null without crashing since ThemeSettings can instantiate it dynamically.
        private Transform ResolveTrayDispenserRoot()
        {
            if (_trayDispenserRoot != null) return _trayDispenserRoot;

            var found = GameObject.Find("tepsi_dagitici");
            if (found != null)
            {
                return found.transform;
            }

            return null;
        }
    }
}
