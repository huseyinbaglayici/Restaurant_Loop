using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Effects;
using RestaurantLoop.Runtime.Tutorial;
using RestaurantLoop.Runtime.UI;
using RestaurantLoop.Runtime.SceneFlow;
using TMPro;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Installers
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
        [SerializeField] private Transform _levelHolder;
        [SerializeField] private Transform _topLevelHolder;
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
                    "[ConveyorInstaller] CurrentLevelProvider is empty — it should have resolved a level from the save at boot.");
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
            Container.BindInterfacesAndSelfTo<ThemeEnvironmentService>().AsSingle();
            Container.Bind<BeltPath>().AsSingle();
            Container.Bind<BeltEntryGate>().AsSingle();
            Container.BindInterfacesAndSelfTo<ConveyorManager>().AsSingle();
            Container.Bind<CustomerSightLines>().AsSingle();
            Container.BindInterfacesAndSelfTo<CustomerConsumeService>().AsSingle();
            Container.BindInterfacesTo<ConveyorEntryInput>().AsSingle();
            Container.Bind<ConveyorPointer>().AsSingle();
            Container.Bind<QueuePickHandler>().AsSingle();
            Container.BindInterfacesAndSelfTo<ExitStackPickHandler>().AsSingle();
            Container.BindInterfacesAndSelfTo<ConveyorAutoClearService>().AsSingle();

            var dispenserRoot = ResolveTrayDispenserRoot();
            if (dispenserRoot != null)
            {
                Container.BindInstance(dispenserRoot).WithId("TrayDispenserRoot").AsCached();
            }
            Container.BindInterfacesAndSelfTo<TrayDispenserAnchor>().AsSingle();

            // visual layer ticks after the logical one, flights last so they consume this frame's commands
            Container.Bind<CustomerServeVisuals>().AsSingle();
            Container.BindInterfacesAndSelfTo<ConveyorVisualService>().AsSingle();
            Container.BindInterfacesTo<ConveyorAudio>().AsSingle();
            Container.BindInterfacesAndSelfTo<TrayVisualService>().AsSingle();
            Container.BindInterfacesAndSelfTo<FoodFlightService>().AsSingle();
            Container.BindInterfacesAndSelfTo<FxService>().AsSingle();
            Container.BindInterfacesAndSelfTo<QueuePowerUpAnimator>().AsSingle();
            Container.Bind<PowerUpService>().AsSingle();
            Container.Bind<PowerUpPurchaseService>().AsSingle();
            Container.Bind<LevelProgression>().AsSingle();
            Container.Bind<LevelBoardCleanup>().AsSingle();
            Container.Bind<KeepPlayingOffer>().AsSingle();

            Container.BindInstance(_levelText).AsSingle();
            Container.Bind<TextMeshProUGUI>().WithId("GameplayMoneyText").FromInstance(_moneyText).AsCached();
            Container.BindInstance(_levelHolder).WithId("LevelHolder").AsCached();
            Container.BindInstance(_topLevelHolder).WithId("TopLevelHolder").AsCached();
            Container.BindInterfacesTo<LevelOverlayPresenter>().AsSingle();

            Container.Bind<TutorialTargetRegistry>().AsSingle();
            Container.BindInterfacesAndSelfTo<TutorialInputGate>().AsSingle();
            Container.BindInstance(_tutorialView).AsSingle();
            Container.BindInterfacesAndSelfTo<TutorialController>().AsSingle();

            BindCustomerOrderWidgetPool();
        }

        // Customer pools live in PoolInstaller so they survive scene reloads; the order widget pool stays
        // scene-scoped because it needs this scene's canvas refs.
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

            // Widget graphics are reparented under these shared groups on spawn so uGUI can batch them.
            Container.BindInstance(_orderIconsGroup).WithId("OrderIconsGroup").AsCached();
            Container.BindInstance(_orderTextsGroup).WithId("OrderTextsGroup").AsCached();

            Container.BindMemoryPool<CustomerOrderWidget, CustomerOrderWidget.Pool>()
                .WithInitialSize(_customerInitialSize)
                .FromComponentInNewPrefab(_customerPrefabSettings.CustomerOrderWidgetPrefab)
                .UnderTransform(_customerOrderCanvasRoot)
                .NonLazy();
        }

        // Falls back to the scene object by name; null is fine, the theme can spawn the dispenser itself.
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
