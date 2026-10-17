using RestaurantLoop.Runtime.Audio;
using RestaurantLoop.Runtime.Economy;
using RestaurantLoop.Runtime.Lives;
using RestaurantLoop.Runtime.Save;
using RestaurantLoop.Runtime.SceneFlow;
using Zenject;

namespace RestaurantLoop.Runtime.Installers
{
    // Project-wide services that live for the whole session.
    public class ServicesInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.Bind<CurrentLevelProvider>().AsSingle();
            Container.BindInterfacesAndSelfTo<SceneLoader>().AsSingle();
            Container.Bind<SaveService>().AsSingle();
            Container.BindInterfacesAndSelfTo<EconomyService>().AsSingle();
            Container.BindInterfacesAndSelfTo<LivesService>().AsSingle();
            Container.BindInterfacesAndSelfTo<PowerUpInventoryService>().AsSingle();
            Container.BindInterfacesAndSelfTo<AudioService>().AsSingle();
            Container.BindInterfacesAndSelfTo<MusicDirector>().AsSingle();
        }
    }
}
