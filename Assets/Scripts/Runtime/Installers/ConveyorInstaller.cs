using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
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
        [SerializeField] private RectTransform _customerOrderCanvasRoot;
        [SerializeField] private Transform _orderIconsGroup;
        [SerializeField] private Transform _orderTextsGroup;
        [SerializeField] private int _customerInitialSize = 10;

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

            Container.BindInstance(ResolveTrayDispenserRoot()).AsCached();
            Container.BindInterfacesAndSelfTo<TrayDispenserAnchor>().AsSingle();

            // visual layer ticks after the logical one, flights last so they consume this frame's commands
            Container.BindInterfacesAndSelfTo<ConveyorVisualService>().AsSingle();
            Container.BindInterfacesAndSelfTo<TrayVisualService>().AsSingle();
            Container.BindInterfacesAndSelfTo<FoodFlightService>().AsSingle();
            Container.Bind<PowerUpService>().AsSingle();

            Container.BindInstance(_levelText).AsSingle();
            Container.BindInterfacesTo<LevelOverlayPresenter>().AsSingle();

            BindCustomerPool();
        }

        private void BindCustomerPool()
        {
            var customerSettings = Resources.Load<CustomerPrefabSettings>("CustomerPrefabSettings");
            if (customerSettings == null || customerSettings.CustomerPrefab == null)
            {
                Debug.LogError(
                    "[ConveyorInstaller] CustomerPrefabSettings.asset missing or has no CustomerPrefab assigned " +
                    "under Assets/Resources — customer pool not bound.");
                return;
            }

            Container.BindInstance(customerSettings).AsSingle();

            var animationSet = Resources.Load<CustomerAnimationSet>("CustomerAnimationSet");
            if (animationSet == null)
            {
                Debug.LogError(
                    "[ConveyorInstaller] CustomerAnimationSet.asset not found under Assets/Resources — " +
                    "create it via RestaurantLoop/Customer Animation Set, customer pool not bound.");
                return;
            }

            Container.BindInstance(animationSet).AsSingle();

            Container.BindMemoryPool<CustomerController, CustomerController.Pool>()
                .WithInitialSize(_customerInitialSize)
                .FromComponentInNewPrefab(customerSettings.CustomerPrefab)
                .UnderTransformGroup("Customers")
                .NonLazy();

            if (customerSettings.CustomerOrderWidgetPrefab == null || _customerOrderCanvasRoot == null ||
                _orderIconsGroup == null || _orderTextsGroup == null)
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
                .FromComponentInNewPrefab(customerSettings.CustomerOrderWidgetPrefab)
                .UnderTransform(_customerOrderCanvasRoot)
                .NonLazy();
        }

        // The Inspector reference is a plain scene object drag-reference — a stray delete/undo in the
        // Editor can silently clear it (Unity's undo doesn't always restore cross-object references).
        // Falls back to finding the dispenser by name so a broken reference doesn't take the whole tray
        // feature down; still logs loudly so the Inspector field gets re-wired properly.
        private Transform ResolveTrayDispenserRoot()
        {
            if (_trayDispenserRoot != null) return _trayDispenserRoot;

            var found = GameObject.Find("tepsi_dagitici");
            if (found != null)
            {
                Debug.LogWarning(
                    "[ConveyorInstaller] _trayDispenserRoot wasn't assigned in the Inspector — found " +
                    "'tepsi_dagitici' by name instead. Re-assign the field on the ConveyorInstaller " +
                    "component so this stops depending on the fallback.");
                return found.transform;
            }

            Debug.LogError(
                "[ConveyorInstaller] _trayDispenserRoot isn't assigned and no 'tepsi_dagitici' GameObject " +
                "was found in the scene — trays won't work this level.");
            return null;
        }
    }
}