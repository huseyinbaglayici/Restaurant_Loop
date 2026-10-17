using System;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Save;
using UnityEngine;

namespace RestaurantLoop.Runtime.SceneFlow
{
    /// <summary>
    /// ProjectContext-scoped. Resolves its initial value in the CONSTRUCTOR, not in
    /// IInitializable.Initialize() — Initialize() runs during Unity's Start(), which is always
    /// after a GameplayScene installer's own InstallBindings() (Awake-time), so it would read a
    /// stale default.
    /// </summary>
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
