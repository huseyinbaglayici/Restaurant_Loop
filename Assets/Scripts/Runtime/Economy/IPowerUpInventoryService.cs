using System;
using RestaurantLoop.Runtime.Data;

namespace RestaurantLoop.Economy
{
    public interface IPowerUpInventoryService
    {
        int GetCount(PowerUpButtonId id);
        void AddCount(PowerUpButtonId id, int amount);
        bool TryUse(PowerUpButtonId id);
        bool IsUnlocked(PowerUpButtonId id, int currentLevelId);
        void CheckLevelRewards(int currentLevelId);
        void NotifyLevelFailed();

        event Action<PowerUpButtonId, int> OnCountChanged;
        event Action OnInventoryChanged;
    }
}
