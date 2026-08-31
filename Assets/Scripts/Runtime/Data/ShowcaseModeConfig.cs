using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    public static class ShowcaseModeConfig
    {
        private const string PrefKeyEnabled = "ShowcaseMode_Enabled";
        private const string PrefKeySceneName = "ShowcaseMode_SceneName";
        private const string PrefKeyLevels = "ShowcaseMode_Levels";
        private const string PrefKeyIndex = "ShowcaseMode_CurrentIndex";

        public const string DefaultShowcaseSceneName = "EylulGameplayScene";
        public const string DefaultGameplaySceneName = "GameplayScene";
        public const string DefaultShowcaseMainMenuName = "EylulMainMenuScene";
        public const string DefaultMainMenuName = "MainMenuScene";
        public const string DefaultShowcaseBootstrapName = "EylulBoostrapScene";
        public const string DefaultBootstrapName = "BoostrapScene";

        private const string PrefKeyMainMenuName = "ShowcaseMode_MainMenuName";

        public static bool IsEnabled
        {
            get => PlayerPrefs.GetInt(PrefKeyEnabled, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(PrefKeyEnabled, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static string TargetSceneName
        {
            get => PlayerPrefs.GetString(PrefKeySceneName, DefaultShowcaseSceneName);
            set
            {
                PlayerPrefs.SetString(PrefKeySceneName, string.IsNullOrEmpty(value) ? DefaultShowcaseSceneName : value);
                PlayerPrefs.Save();
            }
        }

        public static string TargetMainMenuSceneName
        {
            get => PlayerPrefs.GetString(PrefKeyMainMenuName, DefaultShowcaseMainMenuName);
            set
            {
                PlayerPrefs.SetString(PrefKeyMainMenuName, string.IsNullOrEmpty(value) ? DefaultShowcaseMainMenuName : value);
                PlayerPrefs.Save();
            }
        }

        public static List<int> Playlist
        {
            get
            {
                string raw = PlayerPrefs.GetString(PrefKeyLevels, "");
                if (string.IsNullOrWhiteSpace(raw)) return new List<int>();

                return raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                          .Select(s => int.TryParse(s.Trim(), out int id) ? id : -1)
                          .Where(id => id > 0)
                          .ToList();
            }
            set
            {
                if (value == null || value.Count == 0)
                {
                    PlayerPrefs.SetString(PrefKeyLevels, "");
                }
                else
                {
                    PlayerPrefs.SetString(PrefKeyLevels, string.Join(",", value));
                }
                PlayerPrefs.Save();
            }
        }

        public static int CurrentPlaylistIndex
        {
            get => PlayerPrefs.GetInt(PrefKeyIndex, 0);
            set
            {
                PlayerPrefs.SetInt(PrefKeyIndex, Mathf.Max(0, value));
                PlayerPrefs.Save();
            }
        }

        public static int GetCurrentLevelId()
        {
            var list = Playlist;
            if (list.Count == 0) return 1;
            int idx = Mathf.Clamp(CurrentPlaylistIndex, 0, list.Count - 1);
            return list[idx];
        }

        public static bool TryAdvancePlaylist(out int nextLevelId)
        {
            var list = Playlist;
            if (list.Count == 0)
            {
                nextLevelId = 1;
                return false;
            }

            int nextIdx = CurrentPlaylistIndex + 1;
            if (nextIdx < list.Count)
            {
                CurrentPlaylistIndex = nextIdx;
                nextLevelId = list[nextIdx];
                return true;
            }

            // End of playlist: loop back or keep at last
            CurrentPlaylistIndex = 0;
            nextLevelId = list[0];
            return true;
        }

        public static void ResetPlaylistProgress()
        {
            CurrentPlaylistIndex = 0;
        }
    }
}
