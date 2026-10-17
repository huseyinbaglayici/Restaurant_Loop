using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    public enum ThemeType
    {
        Asian,
        Italian
    }

    [System.Serializable]
    public class ThemeEntry
    {
        public ThemeType Theme;
        [Tooltip("3D Environment / Background decor prefab placed behind the conveyor belt.")]
        public GameObject EnvironmentPrefab;
        [Tooltip("Custom position offset for this specific theme's environment (X, Y, Z).")]
        public Vector3 EnvironmentOffset = Vector3.zero;
        [Tooltip("How far the camera moves up on Y as it zooms out (e.g. 2.0). 0 = fixed.")]
        public float CameraZoomYShiftMultiplier = 0f;
        [Tooltip("How far the camera moves back on Z as it zooms out (e.g. 2.0). 0 = fixed.")]
        public float CameraZoomZShiftMultiplier = 0f;
        [Tooltip("How far this theme's environment moves back on Z as the camera zooms out (e.g. 2.0). 0 = fixed.")]
        public float CameraZOffsetMultiplier = 0f;
        [Tooltip("Custom Euler rotation for this specific theme's environment.")]
        public Vector3 EnvironmentRotation = Vector3.zero;
        [Tooltip("Custom scale multiplier for this specific theme's environment. Defaults to (1,1,1).")]
        public Vector3 EnvironmentScale = Vector3.one;
        [Header("Theme Prefabs / Visuals")]
        [Tooltip("Straight belt prefab for this theme (empty = default).")]
        public GameObject ConveyorStraightPrefab;
        [Tooltip("Corner belt prefab for this theme (empty = default).")]
        public GameObject ConveyorCornerPrefab;
        [Tooltip("Exit rack prefab for this theme (empty = default).")]
        public GameObject RackPrefab;
        [Tooltip("Extra Tray power-up rack prefab for this theme (empty = default).")]
        public GameObject ExtraRackPrefab;
        [Tooltip("Tray dispenser prefab for this theme (optional).")]
        public GameObject TrayDispenserPrefab;

        [Header("Loading & Main Menu UI")]
        [Tooltip("Loading screen background sprite for this theme.")]
        public Sprite LoadingScreenBackground;
        [Tooltip("Main menu background sprite for this theme.")]
        public Sprite MainMenuBackground;

        [Header("Audio")]
        [Tooltip("Gameplay background music for this theme (Normal).")]
        public AudioClip GameplayMusic;
        [Tooltip("Gameplay background music for Hard levels in this theme (optional, fallback to GameplayMusic if empty).")]
        public AudioClip HardGameplayMusic;
        [Tooltip("Gameplay background music for Super Hard levels in this theme (optional, fallback to Hard/GameplayMusic if empty).")]
        public AudioClip SuperHardGameplayMusic;
        [Tooltip("Menu background music for this theme (optional).")]
        public AudioClip MenuMusic;
        [Tooltip("Gameplay ambience track for this theme (optional).")]
        public AudioClip GameplayAmbience;
    }

    [CreateAssetMenu(fileName = "ThemeSettings", menuName = "RestaurantLoop/Theme Settings")]
    public class ThemeSettings : ScriptableObject
    {
        [Header("Main Menu Play Button Visuals (Per Difficulty)")]
        [Tooltip("Play button sprite for normal levels.")]
        public Sprite PlayButtonNormalSprite;
        [Tooltip("Play button sprite for Hard levels.")]
        public Sprite PlayButtonHardSprite;
        [Tooltip("Play button sprite for Super Hard levels.")]
        public Sprite PlayButtonSuperHardSprite;

        [Header("Themes")]
        [SerializeField] private List<ThemeEntry> _themes = new()
        {
            new ThemeEntry { Theme = ThemeType.Asian },
            new ThemeEntry { Theme = ThemeType.Italian }
        };

        public IReadOnlyList<ThemeEntry> Themes => _themes;

        public ThemeEntry GetTheme(ThemeType theme)
        {
            foreach (var entry in _themes)
            {
                if (entry != null && entry.Theme == theme)
                    return entry;
            }

            return _themes.Count > 0 ? _themes[0] : new ThemeEntry { Theme = ThemeType.Asian };
        }

        public Sprite GetPlayButtonSprite(LevelDifficulty difficulty)
        {
            return difficulty switch
            {
                LevelDifficulty.SuperHard => PlayButtonSuperHardSprite != null ? PlayButtonSuperHardSprite : (PlayButtonHardSprite != null ? PlayButtonHardSprite : PlayButtonNormalSprite),
                LevelDifficulty.Hard => PlayButtonHardSprite != null ? PlayButtonHardSprite : PlayButtonNormalSprite,
                _ => PlayButtonNormalSprite
            };
        }
    }
}