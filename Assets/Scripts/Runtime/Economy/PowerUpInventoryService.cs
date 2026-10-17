using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Save;
using RestaurantLoop.Runtime.SceneFlow;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Economy
{
    public class PowerUpInventoryService : IInitializable
    {
        private readonly PowerUpSettings _settings;
        private readonly CurrentLevelProvider _currentLevelProvider;
        private readonly SaveService _saveService;

        private readonly Dictionary<PowerUpButtonId, int> _counts = new();

        public event Action<PowerUpButtonId, int> OnCountChanged;
        public event Action OnInventoryChanged;

        public PowerUpInventoryService(
            PowerUpSettings settings,
            CurrentLevelProvider currentLevelProvider,
            SaveService saveService)
        {
            _settings = settings;
            _currentLevelProvider = currentLevelProvider;
            _saveService = saveService;
        }

        public void Initialize()
        {
            foreach (PowerUpButtonId id in Enum.GetValues(typeof(PowerUpButtonId)))
                _counts[id] = _saveService.LoadPowerUpCount(id);

            // Check if any rewards/unlocks should be granted for the current player level
            int currentLevelId = _currentLevelProvider?.Current != null ? _currentLevelProvider.Current.LevelId : 1;
            CheckLevelRewards(currentLevelId);
        }

        public static bool IsInfinitePowerUpsEnabled()
        {
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetBool("RestaurantLoop_InfinitePowerUps", false);
#else
            return false;
#endif
        }

        public int GetCount(PowerUpButtonId id)
        {
            if (IsInfinitePowerUpsEnabled()) return 99;
            return _counts.TryGetValue(id, out int count) ? count : 0;
        }

        public void AddCount(PowerUpButtonId id, int amount)
        {
            if (amount <= 0) return;

            int newCount = GetCount(id) + amount;
            _counts[id] = newCount;
            _saveService.SavePowerUpCount(id, newCount);

            OnCountChanged?.Invoke(id, newCount);
            OnInventoryChanged?.Invoke();
        }

        public bool TryUse(PowerUpButtonId id)
        {
            if (IsInfinitePowerUpsEnabled())
            {
                return true;
            }

            int current = GetCount(id);
            if (current <= 0) return false;

            int newCount = current - 1;
            _counts[id] = newCount;
            _saveService.SavePowerUpCount(id, newCount);

            OnCountChanged?.Invoke(id, newCount);
            OnInventoryChanged?.Invoke();
            return true;
        }

        public bool IsUnlocked(PowerUpButtonId id, int currentLevelId)
        {
            if (IsInfinitePowerUpsEnabled()) return true;
            if (_settings == null) return true;
            var def = _settings.GetDefinition(id);
            if (def == null) return true;

            if (def.UnlockOnFirstFail)
            {
                return _saveService.IsPowerUpUnlockGranted(id);
            }

            return currentLevelId >= def.UnlockLevel;
        }

        public void NotifyLevelFailed()
        {
            if (_settings == null || _settings.Definitions == null) return;

            bool anyGranted = false;
            foreach (var def in _settings.Definitions)
            {
                if (def != null && def.UnlockOnFirstFail)
                {
                    if (!_saveService.IsPowerUpUnlockGranted(def.Id))
                    {
                        if (def.StartingAmount > 0)
                        {
                            int newCount = GetCount(def.Id) + def.StartingAmount;
                            _counts[def.Id] = newCount;
                            _saveService.SavePowerUpCount(def.Id, newCount);
                            anyGranted = true;
                        }
                        _saveService.MarkPowerUpUnlockGranted(def.Id);
                    }
                }
            }

            if (anyGranted)
            {
                OnInventoryChanged?.Invoke();
            }
        }

        public void CheckLevelRewards(int currentLevelId)
        {
            if (_settings == null) return;

            bool anyGranted = false;

            // 1) Check initial level-based unlock rewards (skip first-fail power-ups)
            if (_settings.Definitions != null)
            {
                foreach (var def in _settings.Definitions)
                {
                    if (def == null || def.UnlockOnFirstFail) continue;
                    if (currentLevelId >= def.UnlockLevel)
                    {
                        if (!_saveService.IsPowerUpUnlockGranted(def.Id))
                        {
                            if (def.StartingAmount > 0)
                            {
                                int newCount = GetCount(def.Id) + def.StartingAmount;
                                _counts[def.Id] = newCount;
                                _saveService.SavePowerUpCount(def.Id, newCount);
                                anyGranted = true;
                            }
                            _saveService.MarkPowerUpUnlockGranted(def.Id);
                        }
                    }
                }
            }

            // 2) Check milestone level grants up to current level
            if (_settings.LevelGrants != null)
            {
                foreach (var grant in _settings.LevelGrants)
                {
                    if (grant == null) continue;
                    if (currentLevelId >= grant.Level)
                    {
                        if (!_saveService.IsLevelGrantClaimed(grant.Level, grant.PowerUp))
                        {
                            if (grant.Amount > 0)
                            {
                                int newCount = GetCount(grant.PowerUp) + grant.Amount;
                                _counts[grant.PowerUp] = newCount;
                                _saveService.SavePowerUpCount(grant.PowerUp, newCount);
                                anyGranted = true;
                            }
                            _saveService.MarkLevelGrantClaimed(grant.Level, grant.PowerUp);
                        }
                    }
                }
            }

            if (anyGranted)
            {
                OnInventoryChanged?.Invoke();
            }
        }
    }
}
