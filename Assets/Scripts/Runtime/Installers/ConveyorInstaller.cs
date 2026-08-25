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

            Container.BindInstance(ResolveTrayDispenserRoot()).AsSingle();
            Container.BindInterfacesAndSelfTo<TrayDispenserAnchor>().AsSingle();

            // visual layer ticks after the logical one, flights last so they consume this frame's commands
            Container.BindInterfacesAndSelfTo<ConveyorVisualService>().AsSingle();
            Container.BindInterfacesAndSelfTo<TrayVisualService>().AsSingle();
            Container.BindInterfacesAndSelfTo<FoodFlightService>().AsSingle();
            Container.Bind<PowerUpService>().AsSingle();

            Container.BindInstance(_levelText).AsSingle();
            Container.BindInterfacesTo<LevelOverlayPresenter>().AsSingle();
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