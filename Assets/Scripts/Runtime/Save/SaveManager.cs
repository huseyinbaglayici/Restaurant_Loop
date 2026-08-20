using RestaurantLoop.Runtime.Data;
using RestaurantLoop.SceneFlow;
using RestaurantLoop.Signals;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Save
{
    public class SaveManager : ISaveManager, IInitializable
    {
        private const string LevelIdKey = "CurrentLevelId";
        private const int DefaultLevelId = 1;

        private readonly SignalBus _signalBus;
        private readonly CurrentLevelProvider _currentLevelProvider;
        private readonly LevelDatabase _levelDatabase;

        public SaveManager(SignalBus signalBus, CurrentLevelProvider currentLevelProvider, LevelDatabase levelDatabase)
        {
            _signalBus = signalBus;
            _currentLevelProvider = currentLevelProvider;
            _levelDatabase = levelDatabase;
        }

        public void Initialize()
        {
            _signalBus.Subscribe<SaveRequestedSignal>(_ => Save());
            _signalBus.Subscribe<LoadRequestedSignal>(_ => Load());

            // Boot-time default: resolve whatever level is current (or level 1 if no save exists
            // yet) into CurrentLevelProvider before anything asks for it.
            Load();
        }

        public void Save()
        {
        }

        public void Load()
        {
            int levelId = ES3.KeyExists(LevelIdKey) ? ES3.Load<int>(LevelIdKey) : DefaultLevelId;

            if (_levelDatabase.TryGetLevel(levelId, out var level))
                _currentLevelProvider.Current = level;
            else
                Debug.LogError($"[SaveManager] LevelDatabase has no level with id {levelId}.");
        }

        public void SetCurrentLevel(int levelId)
        {
            ES3.Save(LevelIdKey, levelId);
        }
    }
}
