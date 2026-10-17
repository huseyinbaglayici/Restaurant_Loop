using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Installers
{
    // Binds every gameplay/config ScriptableObject once, so consumers just inject them.
    public class SettingsInstaller : MonoInstaller
    {
        [SerializeField] private LevelDatabase _levelDatabase;
        [SerializeField] private ThemeSettings _themeSettings;
        [SerializeField] private ConveyorTuning _conveyorTuning;
        [SerializeField] private ConveyorFeelSettings _conveyorFeelSettings;
        [SerializeField] private FoodPlacementSettings _foodPlacementSettings;
        [SerializeField] private FoodMeshSettings _foodMeshSettings;
        [SerializeField] private TableSettings _tableSettings;
        [SerializeField] private PowerUpSettings _powerUpSettings;
        [SerializeField] private AudioLibrary _audioLibrary;
        [SerializeField] private CustomerAnimationSet _customerAnimationSet;
        [SerializeField] private FxSettings _fxSettings;

        public override void InstallBindings()
        {
            Container.BindInstance(_levelDatabase).AsSingle();
            Container.BindInstance(_themeSettings).AsSingle();
            Container.BindInstance(_conveyorTuning).AsSingle();
            Container.BindInstance(_conveyorFeelSettings).AsSingle();
            Container.BindInstance(_foodPlacementSettings).AsSingle();
            Container.BindInstance(_foodMeshSettings).AsSingle();
            Container.BindInstance(_tableSettings).AsSingle();
            Container.BindInstance(_powerUpSettings).AsSingle();
            Container.BindInstance(_audioLibrary).AsSingle();
            Container.BindInstance(_customerAnimationSet).AsSingle();
            Container.BindInstance(_fxSettings).AsSingle();
        }
    }
}
