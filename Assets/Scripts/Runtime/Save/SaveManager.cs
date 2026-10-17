using RestaurantLoop.Runtime.SceneFlow;
using RestaurantLoop.Runtime.Signals;
using Zenject;

namespace RestaurantLoop.Runtime.Save
{
    public class SaveManager : IInitializable
    {
        private readonly SignalBus _signalBus;
        private readonly CurrentLevelProvider _currentLevelProvider;

        public SaveManager(SignalBus signalBus, CurrentLevelProvider currentLevelProvider)
        {
            _signalBus = signalBus;
            _currentLevelProvider = currentLevelProvider;
        }

        public void Initialize()
        {
            _signalBus.Subscribe<SaveRequestedSignal>(_ => Save());
            _signalBus.Subscribe<LoadRequestedSignal>(_ => Load());
        }

        public void Save()
        {
        }

        // CurrentLevelProvider already resolves the boot-time default itself (constructor, not
        // here — see its doc comment). This is for an explicit re-load later.
        public void Load()
        {
            _currentLevelProvider.ResolveFromSave();
        }

        public void SetCurrentLevel(int levelId)
        {
            ES3.Save(CurrentLevelProvider.LevelIdKey, levelId);
        }
    }
}
