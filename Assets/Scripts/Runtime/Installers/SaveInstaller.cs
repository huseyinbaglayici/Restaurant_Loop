using Zenject;
using RestaurantLoop.Save;

namespace RestaurantLoop.Installers
{
    public class SaveInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesTo<SaveManager>().AsSingle();
        }
    }
}
