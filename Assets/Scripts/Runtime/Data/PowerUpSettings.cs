using System;
using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    [Serializable]
    public class PowerUpDefinition
    {
        public PowerUpButtonId Id;
        public string Name;
        [Tooltip("If true, unlocks the first time player fails a level instead of level unlock")]
        public bool UnlockOnFirstFail = false;
        [Tooltip("Level at which this power-up is unlocked (if not UnlockOnFirstFail)")]
        public int UnlockLevel = 1;
        [Tooltip("Amount granted when unlocked for the first time")]
        public int StartingAmount = 3;

        [Header("Sprites")]
        [Tooltip("Sprite used when the power-up is unlocked and usable")]
        public Sprite UnlockedSprite;
        [Tooltip("Sprite used when the power-up is locked")]
        public Sprite LockedSprite;

        [Header("Offset Override")]
        [Tooltip("Optional custom offset for this powerup's text (if 0,0 uses global BadgeTextOffset)")]
        public Vector2 CustomTextOffset;

        [Tooltip("Optional custom tutorial panel position for this specific powerup (if 0,0 uses global PowerUpTutorialPanelPos)")]
        public Vector2 CustomTutorialPanelPos;
    }

    [Serializable]
    public class LevelPowerUpGrant
    {
        [Tooltip("Which level gives this grant")]
        public int Level = 1;
        public PowerUpButtonId PowerUp = PowerUpButtonId.Shuffle;
        [Tooltip("How many to give at this level")]
        public int Amount = 1;
    }

    [CreateAssetMenu(fileName = "PowerUpSettings", menuName = "RestaurantLoop/Power Up Settings")]
    public class PowerUpSettings : ScriptableObject
    {
        [Header("Badge / Count Text Layout & Style")]
        [Tooltip("Offset of the count/plus text relative to button center (e.g. X: 40, Y: -40 for bottom-right)")]
        [SerializeField] private Vector2 _badgeTextOffset = new Vector2(40f, -40f);
        [SerializeField] private float _badgeFontSize = 36f;
        [SerializeField] private Color _badgeTextColor = Color.white;
        [SerializeField] private TMPro.TMP_FontAsset _badgeFontAsset;

        [Header("Tutorial Bubble Layout (Power-Up Tutorial Only)")]
        [Tooltip("Anchored position for the tutorial explanation bubble when a power-up tutorial is active")]
        [SerializeField] private Vector2 _powerUpTutorialPanelPos = new Vector2(0f, -480f);

        [Header("Power-Up Definitions")]
        [SerializeField] private List<PowerUpDefinition> _definitions = new()
        {
            new PowerUpDefinition { Id = PowerUpButtonId.Shuffle, Name = "Shuffle", UnlockOnFirstFail = false, UnlockLevel = 2, StartingAmount = 3 },
            new PowerUpDefinition { Id = PowerUpButtonId.PickFromBottom, Name = "Pick From Bottom", UnlockOnFirstFail = false, UnlockLevel = 3, StartingAmount = 3 },
            new PowerUpDefinition { Id = PowerUpButtonId.ExtraTray, Name = "Extra Tray", UnlockOnFirstFail = true, UnlockLevel = 1, StartingAmount = 3 }
        };

        [Header("Additional Level Grants")]
        [SerializeField] private List<LevelPowerUpGrant> _levelGrants = new();

        public Vector2 BadgeTextOffset => _badgeTextOffset;
        public float BadgeFontSize => _badgeFontSize;
        public Color BadgeTextColor => _badgeTextColor;
        public TMPro.TMP_FontAsset BadgeFontAsset => _badgeFontAsset;
        public Vector2 PowerUpTutorialPanelPos => _powerUpTutorialPanelPos;

        public IReadOnlyList<PowerUpDefinition> Definitions => _definitions;
        public IReadOnlyList<LevelPowerUpGrant> LevelGrants => _levelGrants;

        public PowerUpDefinition GetDefinition(PowerUpButtonId id)
        {
            if (_definitions == null) return null;
            return _definitions.Find(d => d.Id == id);
        }

        public int GetUnlockLevel(PowerUpButtonId id)
        {
            var def = GetDefinition(id);
            return def != null ? def.UnlockLevel : 1;
        }

        public int GetStartingAmount(PowerUpButtonId id)
        {
            var def = GetDefinition(id);
            return def != null ? def.StartingAmount : 0;
        }

        public bool IsUnlocked(PowerUpButtonId id, int currentLevelId)
        {
            return currentLevelId >= GetUnlockLevel(id);
        }

        public List<LevelPowerUpGrant> GetGrantsForLevel(int levelId)
        {
            var result = new List<LevelPowerUpGrant>();
            if (_levelGrants == null) return result;
            for (int i = 0; i < _levelGrants.Count; i++)
            {
                if (_levelGrants[i].Level == levelId)
                {
                    result.Add(_levelGrants[i]);
                }
            }
            return result;
        }
    }
}
