using System;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Save;
using UnityEngine;

namespace RestaurantLoop.Runtime.SceneFlow
{
    // Resolves in the constructor, not Initialize(): Initialize runs at Start time, after GameplayScene's
    // installers have already read Current during Awake.
    public class CurrentLevelProvider
    {
        private const int DefaultLevelId = 1;

        private readonly LevelDatabase _levelDatabase;
        private readonly SaveService _saveService;
        private ConveyorLevelDataSO _current;

        public event Action<ConveyorLevelDataSO> OnCurrentLevelChanged;

        public ConveyorLevelDataSO Current
        {
            get => _current;
            set
            {
                if (_current != value)
                {
                    _current = value;
                    OnCurrentLevelChanged?.Invoke(_current);
                }
            }
        }

        public CurrentLevelProvider(LevelDatabase levelDatabase, SaveService saveService)
        {
            _levelDatabase = levelDatabase;
            _saveService = saveService;
            ResolveFromSave();
        }

        public void ResolveFromSave()
        {
            int levelId;
            if (ShowcaseModeConfig.IsEnabled && ShowcaseModeConfig.Playlist.Count > 0)
            {
                levelId = ShowcaseModeConfig.GetCurrentLevelId();
            }
            else
            {
                levelId = _saveService.LoadCurrentLevelId(DefaultLevelId);
            }

            if (_levelDatabase.TryGetLevel(levelId, out var level))
                Current = level;
            else
                Debug.LogError($"[CurrentLevelProvider] LevelDatabase has no level with id {levelId}.");
        }
    }
}
