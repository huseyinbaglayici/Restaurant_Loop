using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Save;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Installers
{
    public class SaveInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            var levelDatabase = Resources.Load<LevelDatabase>("LevelDatabase");
            if (levelDatabase == null)
            {
                Debug.LogError("[SaveInstaller] LevelDatabase.asset not found under Resources — SaveManager not bound.");
                return;
            }

            Container.BindInstance(levelDatabase).AsSingle();
            Container.BindInterfacesTo<SaveManager>().AsSingle();
        }
    }
}
