using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Save;
using RestaurantLoop.Runtime.SceneFlow;
using Zenject;

namespace RestaurantLoop.Runtime.Economy
{
    // Power-up charges, unlocks and level grants, persisted through SaveService.
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

            int currentLevelId = _currentLevelProvider?.Current != null ? _currentLevelProvider.Current.LevelId : 1;
            CheckLevelRewards(currentLevelId);
        }

        // Editor-only debug toggle: every power-up unlocked with 99 charges that are never spent.
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

        // The real saved count; GetCount reports 99 while the editor's infinite toggle is on.
        private int StoredCount(PowerUpButtonId id) => _counts.TryGetValue(id, out int count) ? count : 0;

        public void AddCount(PowerUpButtonId id, int amount)
        {
            if (amount <= 0) return;

            int newCount = StoredCount(id) + amount;
            _counts[id] = newCount;
            _saveService.SavePowerUpCount(id, newCount);

            OnCountChanged?.Invoke(id, newCount);
            OnInventoryChanged?.Invoke();
        }

        public bool TryUse(PowerUpButtonId id)
        {
            if (IsInfinitePowerUpsEnabled()) return true;

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

        // Unlocks the "unlock on first fail" power-ups.
        public void NotifyLevelFailed()
        {
            if (_settings == null || _settings.Definitions == null) return;

            bool anyGranted = false;
            foreach (var def in _settings.Definitions)
            {
                if (def != null && def.UnlockOnFirstFail) anyGranted |= TryGrantUnlock(def);
            }

            if (anyGranted) OnInventoryChanged?.Invoke();
        }

        // Grants every level unlock and milestone grant reached by currentLevelId, each only once.
        public void CheckLevelRewards(int currentLevelId)
        {
            if (_settings == null) return;

            bool anyGranted = false;

            if (_settings.Definitions != null)
            {
                foreach (var def in _settings.Definitions)
                {
                    if (def == null || def.UnlockOnFirstFail) continue;
                    if (currentLevelId >= def.UnlockLevel) anyGranted |= TryGrantUnlock(def);
                }
            }

            if (_settings.LevelGrants != null)
            {
                foreach (var grant in _settings.LevelGrants)
                {
                    if (grant == null || currentLevelId < grant.Level) continue;
                    if (_saveService.IsLevelGrantClaimed(grant.Level, grant.PowerUp)) continue;

                    anyGranted |= AddWithoutNotify(grant.PowerUp, grant.Amount);
                    _saveService.MarkLevelGrantClaimed(grant.Level, grant.PowerUp);
                }
            }

            if (anyGranted) OnInventoryChanged?.Invoke();
        }

        // Gives a newly unlocked power-up its starting amount, once.
        private bool TryGrantUnlock(PowerUpDefinition def)
        {
            if (_saveService.IsPowerUpUnlockGranted(def.Id)) return false;

            bool granted = AddWithoutNotify(def.Id, def.StartingAmount);
            _saveService.MarkPowerUpUnlockGranted(def.Id);
            return granted;
        }

        // Callers fire OnInventoryChanged once for the whole batch.
        private bool AddWithoutNotify(PowerUpButtonId id, int amount)
        {
            if (amount <= 0) return false;

            int newCount = StoredCount(id) + amount;
            _counts[id] = newCount;
            _saveService.SavePowerUpCount(id, newCount);
            return true;
        }
    }
}
