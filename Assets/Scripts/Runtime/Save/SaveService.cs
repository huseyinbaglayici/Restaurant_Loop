using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace RestaurantLoop.Runtime.Save
{
    // Single owner of every persisted key. Key strings must never change or existing saves are lost.
    public class SaveService
    {
        private const string CurrentLevelIdKey = "CurrentLevelId";
        private const string CoinsKey = "Player_Coins";
        private const string LivesKey = "Player_Lives";
        private const string LastRegenTimeKey = "Player_LastRegenTime";
        private const string MusicVolumeKey = "Audio_MusicVolume";
        private const string SfxVolumeKey = "Audio_SfxVolume";

        public int LoadCurrentLevelId(int fallback) => LoadInt(CurrentLevelIdKey, fallback);
        public void SaveCurrentLevelId(int levelId) => ES3.Save(CurrentLevelIdKey, levelId);

        public int LoadCoins(int fallback) => LoadInt(CoinsKey, fallback);
        public void SaveCoins(int coins) => ES3.Save(CoinsKey, coins);

        public int LoadLives(int fallback) => LoadInt(LivesKey, fallback);
        public void SaveLives(int lives) => ES3.Save(LivesKey, lives);

        public bool TryLoadLastRegenTicks(out long ticks)
        {
            bool exists = ES3.KeyExists(LastRegenTimeKey);
            ticks = exists ? ES3.Load<long>(LastRegenTimeKey) : 0L;
            return exists;
        }

        public void SaveLastRegenTicks(long ticks) => ES3.Save(LastRegenTimeKey, ticks);

        public int LoadPowerUpCount(PowerUpButtonId id) => LoadInt($"PowerUp_Count_{id}", 0);
        public void SavePowerUpCount(PowerUpButtonId id, int count) => ES3.Save($"PowerUp_Count_{id}", count);

        public bool IsPowerUpUnlockGranted(PowerUpButtonId id) => ES3.KeyExists($"PowerUp_UnlockedGranted_{id}");
        public void MarkPowerUpUnlockGranted(PowerUpButtonId id) => ES3.Save($"PowerUp_UnlockedGranted_{id}", true);

        public void ResetPowerUp(PowerUpButtonId id)
        {
            ES3.DeleteKey($"PowerUp_Count_{id}");
            ES3.DeleteKey($"PowerUp_UnlockedGranted_{id}");
        }

        public bool IsLevelGrantClaimed(int level, PowerUpButtonId id) => ES3.KeyExists($"PowerUp_Grant_L{level}_{id}");
        public void MarkLevelGrantClaimed(int level, PowerUpButtonId id) => ES3.Save($"PowerUp_Grant_L{level}_{id}", true);

        // Volumes predate the ES3 save and stay in PlayerPrefs so existing settings carry over.
        public float MusicVolume
        {
            get => PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
            set { PlayerPrefs.SetFloat(MusicVolumeKey, value); PlayerPrefs.Save(); }
        }

        public float SfxVolume
        {
            get => PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
            set { PlayerPrefs.SetFloat(SfxVolumeKey, value); PlayerPrefs.Save(); }
        }

        private static int LoadInt(string key, int fallback) => ES3.KeyExists(key) ? ES3.Load<int>(key) : fallback;
    }
}
