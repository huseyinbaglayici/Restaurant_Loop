using RestaurantLoop.Runtime.Economy;
using RestaurantLoop.Runtime.Lives;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Save;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Installers
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

            var powerUpSettings = Resources.Load<PowerUpSettings>("PowerUpSettings");
            if (powerUpSettings != null)
            {
                Container.BindInstance(powerUpSettings).AsSingle();
            }

            Container.BindInterfacesTo<SaveManager>().AsSingle();
            Container.BindInterfacesTo<EconomyService>().AsSingle();
            Container.BindInterfacesTo<LivesService>().AsSingle();
            Container.BindInterfacesAndSelfTo<PowerUpInventoryService>().AsSingle();
        }
    }
}
