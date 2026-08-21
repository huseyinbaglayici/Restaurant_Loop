using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.SceneFlow;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Installers
{
    /// <summary>
    /// Scene-scoped — attach to GameplayScene's SceneContext (Mono Installers list), not
    /// ProjectContext. Conveyor services only need to exist while GameplayScene is loaded.
    ///
    /// Level comes from CurrentLevelProvider, populated by SaveManager at ProjectContext boot
    /// (ES3 save -> LevelDatabase lookup, defaults to level 1 if no save exists) — no manual
    /// drag-in needed, works whether GameplayScene is entered via MainMenu or opened directly.
    /// </summary>
    public class ConveyorInstaller : MonoInstaller
    {
        [Inject] private CurrentLevelProvider _currentLevelProvider;

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
            Container.Bind<RuntimeTableState>().AsSingle();
            Container.BindInterfacesTo<ConveyorPieceSpawner>().AsSingle();
            Container.BindInterfacesAndSelfTo<ExitStackAnchor>().AsSingle();
            Container.BindInterfacesAndSelfTo<QueueAnchor>().AsSingle();
            Container.BindInterfacesAndSelfTo<QueueSpawner>().AsSingle();
            Container.BindInterfacesAndSelfTo<ConveyorManager>().AsSingle();
            Container.BindInterfacesTo<ConveyorEntryInput>().AsSingle();
        }
    }
}