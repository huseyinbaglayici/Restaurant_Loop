using System;
using RestaurantLoop.Runtime.Save;
using Zenject;

namespace RestaurantLoop.Runtime.Economy
{
    public class EconomyService : IInitializable
    {
        public const int DefaultStartingCoins = 150;

        private readonly SaveService _saveService;
        private int _coins;

        public int Coins => _coins;

        public event Action<int> OnCoinsChanged;

        public EconomyService(SaveService saveService)
        {
            _saveService = saveService;
        }

        public void Initialize()
        {
            _coins = _saveService.LoadCoins(DefaultStartingCoins);
        }

        public void AddCoins(int amount)
        {
            if (amount <= 0) return;

            _coins += amount;
            _saveService.SaveCoins(_coins);
            OnCoinsChanged?.Invoke(_coins);
        }

        public bool TrySpendCoins(int amount)
        {
            if (amount <= 0) return false;
            if (_coins < amount) return false;

            _coins -= amount;
            _saveService.SaveCoins(_coins);
            OnCoinsChanged?.Invoke(_coins);
            return true;
        }
    }
}
