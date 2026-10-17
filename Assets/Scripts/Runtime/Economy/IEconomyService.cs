using System;

namespace RestaurantLoop.Runtime.Economy
{
    public interface IEconomyService
    {
        int Coins { get; }
        void AddCoins(int amount);
        bool TrySpendCoins(int amount);
        event Action<int> OnCoinsChanged;
    }
}
