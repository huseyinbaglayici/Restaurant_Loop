using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Economy;
using RestaurantLoop.Runtime.Save;
using RestaurantLoop.Runtime.SceneFlow;

namespace RestaurantLoop.Runtime.UI
{
    // Persistent player progress at the end of a level: coin reward, next saved level / showcase playlist, power-up unlocks.
    public class LevelProgression
    {
        private readonly EconomyService _economyService;
        private readonly SaveService _saveService;
        private readonly CurrentLevelProvider _currentLevelProvider;
        private readonly LevelDatabase _levelDatabase;
        private readonly PowerUpInventoryService _powerUpInventory;

        public LevelProgression(
            EconomyService economyService,
            SaveService saveService,
            CurrentLevelProvider currentLevelProvider,
            LevelDatabase levelDatabase,
            PowerUpInventoryService powerUpInventory)
        {
            _economyService = economyService;
            _saveService = saveService;
            _currentLevelProvider = currentLevelProvider;
            _levelDatabase = levelDatabase;
            _powerUpInventory = powerUpInventory;
        }

        public static int GetRewardCoins(LevelDifficulty difficulty)
        {
            return difficulty switch
            {
                LevelDifficulty.Hard => 75,
                LevelDifficulty.SuperHard => 100,
                LevelDifficulty.Bonus => 100,
                _ => 50
            };
        }

        public void AwardLevelCoins()
        {
            if (_currentLevelProvider.Current == null) return;

            _economyService.AddCoins(GetRewardCoins(_currentLevelProvider.Current.Difficulty));
        }

        // Only commits if the next id actually exists — otherwise this was the last level, so
        // progress stays put instead of saving a level id the database can't resolve on next boot.
        public void AdvanceSavedLevel()
        {
            if (_currentLevelProvider.Current == null) return;

            if (ShowcaseModeConfig.IsEnabled && ShowcaseModeConfig.Playlist.Count > 0)
            {
                if (ShowcaseModeConfig.TryAdvancePlaylist(out int nextShowcaseLevelId))
                {
                    _currentLevelProvider.ResolveFromSave();
                    _powerUpInventory.CheckLevelRewards(nextShowcaseLevelId);
                }
                return;
            }

            int nextLevelId = _currentLevelProvider.Current.LevelId + 1;
            if (!_levelDatabase.TryGetLevel(nextLevelId, out _)) return;

            _saveService.SaveCurrentLevelId(nextLevelId);
            _currentLevelProvider.ResolveFromSave();
            _powerUpInventory.CheckLevelRewards(nextLevelId);
        }

        // First fail unlocks the "unlock on first fail" power-ups.
        public void RecordLevelFailed() => _powerUpInventory.NotifyLevelFailed();
    }
}
