using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace RestaurantLoop.SceneFlow
{
    /// <summary>
    /// ProjectContext-scoped. Resolves its initial value in the CONSTRUCTOR, not in
    /// IInitializable.Initialize() — Initialize() runs during Unity's Start(), which is always
    /// after a GameplayScene installer's own InstallBindings() (Awake-time), so it would read a
    /// stale default. ResolveFromSave() is public so SaveManager.Load() can re-run it later.
    /// </summary>
    public class CurrentLevelProvider
    {
        public const string LevelIdKey = "CurrentLevelId";
        private const int DefaultLevelId = 1;

        private readonly LevelDatabase _levelDatabase;

        public ConveyorLevelDataSO Current { get; set; }

        public CurrentLevelProvider(LevelDatabase levelDatabase)
        {
            _levelDatabase = levelDatabase;
            ResolveFromSave();
        }

        public void ResolveFromSave()
        {
            int levelId = ES3.KeyExists(LevelIdKey) ? ES3.Load<int>(LevelIdKey) : DefaultLevelId;

            if (_levelDatabase.TryGetLevel(levelId, out var level))
                Current = level;
            else
                Debug.LogError($"[CurrentLevelProvider] LevelDatabase has no level with id {levelId}.");
        }
    }
}
