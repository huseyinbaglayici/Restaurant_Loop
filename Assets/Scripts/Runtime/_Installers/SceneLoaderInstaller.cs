using Zenject;
using RestaurantLoop.Runtime.SceneFlow;

namespace RestaurantLoop.Runtime.Installers
{
    public class SceneLoaderInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.Bind<CurrentLevelProvider>().AsSingle();
            Container.BindInterfacesTo<SceneLoader>().AsSingle();
        }
    }
}
