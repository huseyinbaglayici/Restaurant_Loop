using System;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Economy
{
    public class EconomyService : IEconomyService, IInitializable
    {
        public const string CoinsSaveKey = "Player_Coins";
        public const int DefaultStartingCoins = 150;

        private int _coins;

        public int Coins => _coins;

        public event Action<int> OnCoinsChanged;

        public void Initialize()
        {
            _coins = ES3.KeyExists(CoinsSaveKey) ? ES3.Load<int>(CoinsSaveKey) : DefaultStartingCoins;
        }

        public void AddCoins(int amount)
        {
            if (amount <= 0) return;

            _coins += amount;
            Save();
            OnCoinsChanged?.Invoke(_coins);
        }

        public bool TrySpendCoins(int amount)
        {
            if (amount <= 0) return false;
            if (_coins < amount) return false;

            _coins -= amount;
            Save();
            OnCoinsChanged?.Invoke(_coins);
            return true;
        }

        private void Save()
        {
            ES3.Save(CoinsSaveKey, _coins);
        }
    }
}
