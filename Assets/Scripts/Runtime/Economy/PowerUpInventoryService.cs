using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.SceneFlow;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Economy
{
    public class PowerUpInventoryService : IPowerUpInventoryService, IInitializable
    {
        private readonly PowerUpSettings _settings;
        private readonly CurrentLevelProvider _currentLevelProvider;

        private readonly Dictionary<PowerUpButtonId, int> _counts = new();

        public event Action<PowerUpButtonId, int> OnCountChanged;
        public event Action OnInventoryChanged;

        public PowerUpInventoryService(
            [Inject(Optional = true)] PowerUpSettings settings = null,
            [Inject(Optional = true)] CurrentLevelProvider currentLevelProvider = null)
        {
            _settings = settings;
            _currentLevelProvider = currentLevelProvider;
        }

        public void Initialize()
        {
            // Load existing counts from save
            foreach (PowerUpButtonId id in Enum.GetValues(typeof(PowerUpButtonId)))
            {
                string key = GetCountSaveKey(id);
                int count = ES3.KeyExists(key) ? ES3.Load<int>(key) : 0;
                _counts[id] = count;
            }

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
            ES3.Save(GetCountSaveKey(id), newCount);

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
            ES3.Save(GetCountSaveKey(id), newCount);

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
                return ES3.KeyExists($"PowerUp_UnlockedGranted_{id}");
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
                    string unlockKey = $"PowerUp_UnlockedGranted_{def.Id}";
                    if (!ES3.KeyExists(unlockKey))
                    {
                        if (def.StartingAmount > 0)
                        {
                            int newCount = GetCount(def.Id) + def.StartingAmount;
                            _counts[def.Id] = newCount;
                            ES3.Save(GetCountSaveKey(def.Id), newCount);
                            anyGranted = true;
                        }
                        ES3.Save(unlockKey, true);
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
                        string unlockKey = $"PowerUp_UnlockedGranted_{def.Id}";
                        if (!ES3.KeyExists(unlockKey))
                        {
                            if (def.StartingAmount > 0)
                            {
                                int newCount = GetCount(def.Id) + def.StartingAmount;
                                _counts[def.Id] = newCount;
                                ES3.Save(GetCountSaveKey(def.Id), newCount);
                                anyGranted = true;
                            }
                            ES3.Save(unlockKey, true);
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
                        string grantKey = $"PowerUp_Grant_L{grant.Level}_{grant.PowerUp}";
                        if (!ES3.KeyExists(grantKey))
                        {
                            if (grant.Amount > 0)
                            {
                                int newCount = GetCount(grant.PowerUp) + grant.Amount;
                                _counts[grant.PowerUp] = newCount;
                                ES3.Save(GetCountSaveKey(grant.PowerUp), newCount);
                                anyGranted = true;
                            }
                            ES3.Save(grantKey, true);
                        }
                    }
                }
            }

            if (anyGranted)
            {
                OnInventoryChanged?.Invoke();
            }
        }

        private static string GetCountSaveKey(PowerUpButtonId id) => $"PowerUp_Count_{id}";
    }
}
