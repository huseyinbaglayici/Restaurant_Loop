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
        [Tooltip("Camera uzaklaştıkça kameranın Y ekseninde (yukarı) ötelenme katsayısı (Örn: 2.0). 0 ise sabit kalır.")]
        public float CameraZoomYShiftMultiplier = 0f;
        [Tooltip("Camera uzaklaştıkça kameranın Z ekseninde (geriye/arkaya) ötelenme katsayısı (Örn: 2.0). 0 ise sabit kalır.")]
        public float CameraZoomZShiftMultiplier = 0f;
        [Tooltip("Camera uzaklaştıkça bu temanın Environment objesinin Z ekseninde (arkaya) gitme katsayısı (Örn: 2.0). 0 ise sabit kalır.")]
        public float CameraZOffsetMultiplier = 0f;
        [Tooltip("Custom Euler rotation for this specific theme's environment.")]
        public Vector3 EnvironmentRotation = Vector3.zero;
        [Tooltip("Custom scale multiplier for this specific theme's environment. Defaults to (1,1,1).")]
        public Vector3 EnvironmentScale = Vector3.one;
        [Header("Theme Prefabs / Visuals")]
        [Tooltip("Bu tema için Düz Conveyor prefab'i (Boşsa varsayılan ayar kullanılır).")]
        public GameObject ConveyorStraightPrefab;
        [Tooltip("Bu tema için Köşe Conveyor prefab'i (Boşsa varsayılan ayar kullanılır).")]
        public GameObject ConveyorCornerPrefab;
        [Tooltip("Bu tema için Rack (çıkış rafı) prefab'i (Boşsa varsayılan ayar kullanılır).")]
        public GameObject RackPrefab;
        [Tooltip("Bu tema için Extra Rack (ekstra raf powerup) prefab'i (Boşsa varsayılan ayar kullanılır).")]
        public GameObject ExtraRackPrefab;
        [Tooltip("Bu tema için Tepsi Dağıtıcı (Tray Dispenser) prefab'i veya görseli (Opsiyonel).")]
        public GameObject TrayDispenserPrefab;

        [Header("Theme Materials (Opsiyonel alternatif)")]
        [Tooltip("Bu tema için Rack (çıkış rafı) materyali.")]
        public Material RackMaterial;
        [Tooltip("Bu tema için Tepsi Dağıtıcı (Tray Dispenser) materyali.")]
        public Material TrayDispenserMaterial;
        [Tooltip("Bu tema için Conveyor (Bant) materyali.")]
        public Material ConveyorMaterial;
        [Tooltip("Bu tema için Conveyor Köşe (Corner) materyali (opsiyonel, boşsa ConveyorMaterial kullanılır).")]
        public Material ConveyorCornerMaterial;

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
        [Tooltip("Normal bölümler için buton görseli / sprite.")]
        public Sprite PlayButtonNormalSprite;
        [Tooltip("Zor (Hard) bölümler için buton görseli / sprite.")]
        public Sprite PlayButtonHardSprite;
        [Tooltip("Çok Zor (Super Hard) bölümler için buton görseli / sprite.")]
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