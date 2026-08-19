using Zenject;
using RestaurantLoop.SceneFlow;

namespace RestaurantLoop.Installers
{
    public class SceneLoaderInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesTo<SceneLoader>().AsSingle();
        }
    }
}
